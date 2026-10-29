# Instructions for humans and AI agents

Read this before changing anything. It says where things live, what the conventions are, and how to
verify a change. Sections marked _(planned)_ do not exist yet.

## The one rule that matters most

The archive rules live in the **services**, never in the client and never duplicated. If you are about
to add a length check, a writability check, or a folder-to-document mapping rule to the web client,
stop — it belongs to the Archive's `Core` project.

## Repository layout

One top-level directory per service, with that service's projects directly inside it — no `src/` or
`tests/` nesting. Shared build configuration, the compose files and the documents sit at the root.

```
TASK.md  README.md  AGENTS.md  DECISIONS.md  CLAUDE.md   the brief and the docs
docs/                                                    per-feature requirement notes
fixtures/                                                cabinets.json, sample-material/ (given, do not edit)
Nemetschek.sln                                           one solution, covers every service
global.json  Directory.Build.props                       SDK pin and shared build settings
docker-compose.yml  docker-compose.override.yml  .env    _(skeleton; rewritten when the service is containerised)_
Nemetschek.Archive/
├── Nemetschek.Archive.Api/              endpoints, DTOs, error shape, DI, OpenAPI
│   ├── Controllers/  CabinetsController — one action, no helpers, no logger, no status logic
│   ├── Contracts/    request and response shapes, one folder per feature, one type per file
│   ├── Results/      report → IActionResult (no branching)
│   ├── Middleware/   UploadFailureStatusMiddleware — the only status-code decision
│   └── Health/       CabinetCatalogHealthCheck
├── Nemetschek.Archive.Core/             the rules — NO external dependencies
│   ├── Domain/     one folder per domain entity type
│   │   ├── Cabinets/      Cabinet
│   │   ├── IndexFields/   IndexField, FieldType, IndexValue, IndexFieldRules, FieldViolation
│   │   ├── Documents/     Document, Part, DocumentDraft, PartDraft, DocumentQueryService
│   │   └── Uploads/       UploadPlanningService (the mapping) and UploadService (validate, store, report)
│   └── Ports/      ICabinetCatalog, IDocumentStore
├── Nemetschek.Archive.Infrastructure/   adapters
│   ├── Cabinets/   CabinetsFileLoader, CabinetCatalog
│   └── Documents/  FileSystemDocumentStore and the document.json DTOs
├── Nemetschek.Archive.Core.Tests/            one test project per production project
├── Nemetschek.Archive.Infrastructure.Tests/
├── Nemetschek.Archive.Api.Tests/        also holds the solution-wide architecture tests
└── Nemetschek.Archive.TestSupport/      shared test helpers: RepositoryLayout, TestCabinets
Nemetschek.Export/                       _(planned)_ same shape
```

**One test project per production project**, named `<Project>.Tests` and referencing only its subject
plus `TestSupport`. Shared test helpers go in `TestSupport`, which has no test-framework dependency.
Both rules are enforced: `Each_test_project_references_the_project_it_tests` and
`No_production_project_references_a_test_project`.

Project references: `Api → Infrastructure → Core`, and `Api → Core`. **`Core` references nothing** —
no NuGet packages, no other project.

Nothing under `Nemetschek.Archive.*` may reference `Nemetschek.Export.*`, or the reverse. The Export
service reaches the Archive over HTTP only, with its own client DTOs. That duplication is deliberate.

**These rules are enforced by tests, not by trust** — see
`Nemetschek.Archive.Api.Tests/Architecture/ProjectDependencyTests.cs`. It reads the real csproj files, so a
cross-layer or cross-service reference fails `dotnet test`. If one of those tests fails, the fix is
almost always to move the code, not to change the test.

**One solution at the root covers every service.** Per-service solutions were considered and rejected: a
`.sln` is only a list of projects for tooling and does not enforce a boundary (a `ProjectReference`
works across solution membership), while a root solution is what lets `dotnet test` from a clean clone
cover everything in one command. To work on one service in isolation, use a `.slnf` solution filter
rather than adding a second `.sln`. When the Export service arrives, add its projects to
`Nemetschek.sln` — `Every_project_in_the_repository_is_in_the_solution` will fail if you forget.

## Where the rules live

| Rule | Where |
|---|---|
| Cabinet and field definitions | `fixtures/cabinets.json`, loaded once at startup by `Infrastructure/Cabinets/CabinetsFileLoader.cs` |
| What makes a cabinets.json file invalid | DataAnnotations attributes and `IValidatableObject` on `Cabinet` / `IndexField`, plus the private `CabinetsFile` root inside the loader. Fatal at startup, never tolerated |
| The on-disk JSON contract | `[JsonPropertyName]` on **every** property — there are no serialiser options, so matching is case-sensitive and an omitted attribute silently binds to nothing |
| Field type / maxLength / writable enforcement | `Core/Domain/IndexFields/IndexFieldRules.cs` — the only place |
| Resolving a field name to a field (case-insensitive, ordinal) | `IndexFieldRules.FindField`, private. `Cabinet` is data and carries no lookup. `UploadService.StoreAsync` resolves its own, with `First` — the rules have already proved the field is there |
| Which fields may be chosen for upload or as an export marker | `IndexFieldRules.ValidateChosenTextField` |
| Folder → document mapping (upload rules) | `Core/Domain/Uploads/UploadPlanningService.cs`. See `docs/upload-requirements.md` |
| What an uploaded relative path may be | `Core/Domain/Uploads/RelativePath.cs` — rooted paths, `.`, `..` and control characters are refused |
| How deep an upload goes | `UploadPlanningService.Place` — `segments.Count > (file is null ? 1 : 2)`. The limit differs by a segment because a file's last segment is the file itself; both mean "one level of folders". The top-level document is registered *before* the depth check, so a folder holding only a nested subfolder still becomes a document |
| Part order inside a folder-document | `UploadPlanningService.PlanDocuments` — `StringComparer.Ordinal`, so `page10` before `page2` |
| Whether a request is refused before anything is stored | `UploadService.Refuse` — one method, four branches, each keeping its own user-facing sentence. Returns the refusal report, or `null` when the request is acceptable |
| Running an upload: validate, store per document, report | `Core/Domain/Uploads/UploadService.cs` |
| Upload size and part-count limits | `Program.cs`, from `Archive:MaxUploadBytes` and `Archive:MaxUploadEntries` |
| Which HTTP status an upload answers with | `Api/Middleware/UploadFailureStatusMiddleware.cs` — the only place. `200`, raised to `404` when any result is `Failed` |
| Zip layout, duplicate-title naming, marker format (export rules) | Export service _(planned)_ |
| Date-range semantics (inclusive calendar dates, user's time zone) | Archive query API _(planned)_ |
| System fields (`id`, `StoredAt`, `ModifiedAt`) | `FileSystemDocumentStore.AddAsync`. Never accepted from a client. Ids are `Guid.CreateVersion7(storedAt)`, so an id carries its own timestamp |
| Where a document's files go on disk | `FileSystemDocumentStore` — the layout is in its header comment and composed nowhere else, except the part filename, which `PartFile.PathFor` owns because it is also what records it in `document.json` |
| The on-disk document format | `Infrastructure/Documents/DocumentFile.cs` and its siblings. Same rules as the cabinets DTOs: `[JsonPropertyName]` on **every** property, and a `To…()` mapping |
| How an upload is committed | `FileSystemDocumentStore.AddAsync` — written into `tmp/`, then one atomic `Directory.Move`. A document is complete or absent, never half-written |
| Recovering from an upload that died halfway | `FileSystemDocumentStore.Prepare`, called during registration: it clears `tmp/`. Startup is the recovery point |
| Paging a cabinet's documents | `FileSystemDocumentStore.ListAsync` plus `DocumentPageRequest.Skip`/`Take`, capped by `Archive:MaxPageSize` |
| Whether the service is ready to serve | `Api/Health/CabinetCatalogHealthCheck.cs`, tagged `ready` |
| How the service is containerised | `Nemetschek.Archive.Api/Dockerfile` and the root `docker-compose.yml` |

## Conventions

- **A rule gets a test that names it.** Test method names read as the rule, so a reviewer can map
  test → rule: `Write_to_a_non_writable_field_is_refused`. If you change a rule, the failing test
  should tell you which rule you broke.
- **`dotnet test` must pass from a clean clone with no network and no containers.** Rule tests run
  against `Core` with hand-written fixtures; nothing in the test project may need a database or Docker.
- **Validation returns, it does not throw.** `IndexFieldRules` returns a `FieldViolation?` — `null`
  means allowed. The upload report needs to list many refusals at once, so refusals are values.
- **Violation reasons are user-facing sentences** naming the field and the limit. The client is not
  allowed to restate server rules, so the server has to phrase them.
- **User-supplied names are data, never filesystem paths.** Content on disk is addressed by
  server-generated ids.
- **UTF-8 end to end.** No culture-dependent string comparison; no normalisation that rewrites a name.
  `maxLength` counts UTF-16 code units (`string.Length`), matching `nvarchar(n)`.
- **Timestamps in UTC.** Time-zone conversion happens only where the user's calendar dates are
  interpreted.
- **A `<summary>` says what a member does, in a line or two.** No rationale and no implementation detail
  inside the doc block — IntelliSense shows the summary, so it has to be scannable. Where the *why*
  guards against a future mistake, put it in a plain `//` comment; durable rationale goes in
  `DECISIONS.md`.
- **No null checks on non-nullable parameters.** `Nullable` is enabled and warnings are errors, so
  passing null to a non-nullable parameter is a compile error and a runtime guard is unreachable.
  Guard only where a value genuinely arrives unchecked at runtime — configuration, deserialised JSON,
  reflection — and say so in a comment.
- **Use the SDK's exception types; do not invent one.** A missing file is `FileNotFoundException` (it
  carries `FileName` for you), malformed JSON is `JsonException`, a broken DataAnnotations rule is
  `ValidationException`, a bad argument is `ArgumentException`. A custom exception earns its place only
  when something actually catches it and needs to distinguish it — nothing here does.
- **A DTO declares its own rules.** Put presence and range rules in DataAnnotations attributes on the
  property, and cross-property or cross-sibling rules in `IValidatableObject.Validate` on the same
  class. `Validator` does not recurse, so a DTO that owns children calls `Validator.ValidateObject` on
  each of them first. The loader then only reads, deserialises, validates and maps — see
  `CabinetsFileDto` and `CabinetsFileLoader`.
- **A DTO is never a domain type.** DTOs live in `Infrastructure`, have nullable properties so a missing
  value is reportable rather than fatal, and map across with a `To…()` method. Domain types live in
  `Core`, are non-nullable, hold parsed values rather than raw strings, and are valid once constructed.
  Do not collapse the two: it would put serialisation attributes in `Core` and move re-parsing into the
  rules.
- **Clean Code applies**: small functions that do one thing at a single level of abstraction, names that
  state intent, and few parameters. `CabinetsFileLoader` is the shape to copy — two methods, no private
  helpers, because the rules it would otherwise hand-code live on the DTO instead.
- **A controller holds actions and nothing else — no private methods, no logger, no status logic.**
  An action binds the request, calls the rules, and answers with the result; each of those has a
  home outside the controller. Request → domain goes on the request DTO as a `To…()` method
  (`UploadFormRequest.ToUploadRequest`), domain → response goes on the response DTO as a static `From`
  (`UploadReportResponse.From`), and report → `IActionResult` goes in `Api/Results/` as an extension
  on `ControllerBase`. `CabinetsController` is the shape to copy: one action, four lines, one
  constructor dependency.
- **Anything that depends on how an operation *went* lives in middleware, not in the endpoint.**
  `UploadFailureStatusMiddleware` is the only place in the service that picks a status code, and the
  only place a failure is logged. An action records its report with
  `UploadFailureStatusMiddleware.RecordUploadReport` and always returns `200`; the middleware hooks
  `Response.OnStarting` and raises that to `404` if any result is `Failed`. `OnStarting` is the
  mechanism because the status line is already gone by the time MVC has serialised a body, and
  buffering the response to re-read it would cost the streaming the upload path is built for.
- **One public type per file.** `UploadReport`, `UploadEntryResult` and `UploadEntryOutcome` each
  have their own file, and so do the DTOs that mirror them. A file named after a type should contain
  that type and nothing else.
- **Nothing the Archive must not lose is held in memory.** The cabinet catalog is read once at
  startup and cached, because `cabinets.json` is fixed and can never change at runtime. Documents
  are the opposite on every count, so the store reads them from disk per request. That is the whole
  reason a restart loses nothing — there is no state to rebuild, and no cache that could be stale.
- **An operation that acts on many things returns one row per thing, not grouped buckets.** The
  upload report is a single `Results` list where every entry carries its own `Stored` / `Skipped` /
  `Failed` outcome. The user's question is "what happened to *this* entry of mine", and that is
  answered by finding it once.
- **`Core/Domain/` holds one folder per domain entity type**, and the folder names are **plural**. A
  folder named `Cabinet/` containing a class `Cabinet` makes the namespace and the type collide, which
  the compiler rejects. New entity? New plural folder under `Domain/`.
- **Warnings are errors** (`Directory.Build.props`). Do not suppress; fix.
- Shared build settings live in `Directory.Build.props`. Do not restate `TargetFramework`, `Nullable`
  or `ImplicitUsings` in a csproj.
- No credential in any image or in this repository. Configuration comes from the environment.

## How to add an operation

1. Decide which service owns it. If it touches storage, it is the Archive.
2. If it is a rule, put it in `Core`, in the feature folder it belongs to — not in the endpoint, not
   in Infrastructure, not in the client.
3. If it needs a disk, a database or the clock, declare an interface in `Core/Ports/` and implement it
   in `Infrastructure`. Never add a package reference to `Core`.
4. Add the endpoint in `Api`, using the shared error shape.
5. Add a test per rule that fails when the rule is broken.
6. Update the documents listed at the bottom of this file.

## How to verify a change

```bash
dotnet build          # warnings are errors
dotnet test           # offline, no containers — 65 tests across three test projects at time of writing
docker compose up --build   # the supported way to run it; NOT yet verified — no Docker available

# Running from source needs both paths, because neither is defaulted:
Archive__CabinetsFile=$PWD/fixtures/cabinets.json Archive__StoragePath=$PWD/.archive-data \
  dotnet run --project Nemetschek.Archive/Nemetschek.Archive.Api --no-launch-profile
```

**Configuration is single-sourced in `docker-compose.yml`**, with the tunables defaulted in `.env`.
`appsettings.Development.json` deliberately carries no `Archive` section: a relative path there
resolves against the content root, which inside the image is `/app`, so a forgotten volume mount
would store documents in the container's own layer and lose them silently. Adding a default back
would reintroduce exactly that. If you add a setting, add it to `docker-compose.yml`, to the table
in `README.md`, and to `.env` if it is one an operator would change.

With the service running: `/swagger`, `/openapi/v1.json`, `/health` (liveness, runs no checks) and
`/health/ready` (readiness, checks the cabinet catalog). A dependent service waits on `/health/ready`,
never on `/health`.

To check a test really pins its rule, break the rule and confirm *that* test fails. Done so far for:
the writable-field rule (removing the check fails exactly its two tests) and the Core-has-no-packages
rule (adding a `PackageReference` to Core fails `Core_declares_no_package_references`).

Startup is fail-fast and verified by hand: an absent `Archive:CabinetsFile`, or a path that does not
exist, stops the service with a message naming the setting or the file. If you add a required setting,
keep that property — a service that starts and then fails every request is harder to diagnose.

The upload endpoint has been exercised by hand against the real `fixtures/sample-material`, which is
how a change to it should be checked until the rule tests are written:

Every refused request comes back as the same report shape with one `Failed` row, so what to check is
the status **and** that row's `reason`.

| Request | Expected |
|---|---|
| `Invoices` / `Batch` / `sample-material` | `200`, 12 `Stored` and 2 `Skipped`; `delivery-2026-03` has parts `page1.txt, page10.txt, page2.txt` in that order; `Договор София.txt` and `Фактури Q1` come back byte-identical |
| `Invoices` / `Status` | `404` — "accepts at most 12 characters, but the value has 15" |
| `Invoices` / `ApprovedBy` | `404` — "is read-only and cannot be chosen" |
| `Invoices` / `Amount` | `404` — "holds a number value; a text field is required" |
| `Invoices` / `Nonsense` | `404` — "has no index field 'Nonsense'" |
| unknown cabinet | `404` — "There is no file cabinet named …" |

And for storage, which is only meaningfully checked by restarting:

| Check | Expected |
|---|---|
| Upload, stop the service, start it again, `GET …/documents` | Same documents, same ids, read from disk — there is no in-memory state |
| Upload the sample material twice | `total` is 24; the 12 repeated titles all coexist, since the id is the identity and the title is not |
| `ls` the storage path | One directory per document; part files named by GUID; the user's names only inside `document.json`, unescaped |
| A stray directory under `tmp/` | Gone after the next start |
| `Archive:StoragePath` absent, or pointing somewhere unusable | The service refuses to start and names the setting or the path |

Still to come, once the service is containerised: `docker compose up` with browsable OpenAPI per
service, `docker compose down && up` preserving data, `docker compose logs` showing every service, and
the round trip — upload `sample-material` twice, export, unpack, account for every difference.

## Documentation upkeep — required

Any change to this repository updates the affected documents in the same change:

- `README.md` — build, run, test, and the settings that must be supplied
- `AGENTS.md` — this file: layout, rule locations, conventions
- `DECISIONS.md` — one line per decision as "A, because B"; plus cuts, AI notes, time log
- `docs/` — the per-feature requirement notes

A change that alters behaviour and leaves the documents stale is incomplete.
