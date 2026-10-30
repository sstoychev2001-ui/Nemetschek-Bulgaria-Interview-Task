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
│   ├── Controllers/  CabinetsController — two actions, no helpers, no logger, no status logic
│   ├── Contracts/    one folder per entity with Requests/, Responses/, Mappings/, plus Shared/
│   └── Middleware/   ArchiveExceptionMiddleware — the only status-code decision
├── Nemetschek.Archive.Core/             the Archive itself — abstractions-only packages, allowlisted
│   │   every root folder is split again by entity: Cabinets, Documents, IndexFields, Uploads
│   ├── Domain/       the entities only — Cabinet, IndexField, IndexValue, Document, Part, …
│   ├── Services/     what the Archive does, including how it stores
│   │   ├── Cabinets/    CabinetCatalog
│   │   ├── Documents/   DocumentsService, DocumentsStoreService
│   │   ├── Uploads/     UploadService, UploadPlanningService
│   │   └── Interfaces/  ICabinetCatalog, IDocumentStore
│   ├── Validation/   IndexFieldRules, the per-type value validators, RelativePath
│   └── Exceptions/   UnknownCabinetException, UploadRefusedException
├── Nemetschek.Archive.Infrastructure/   composition and the edges of the container
│   ├── Cabinets/    CabinetsFileLoader — reads the deployment's cabinets.json
│   ├── Health/      CabinetCatalogHealthCheck, DocumentStorageHealthCheck
│   └── *ServiceCollectionExtensions — the dependency registrations
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

Project references: `Api → Infrastructure → Core`, and `Api → Core`. **`Core` references no other
project**, and takes a NuGet package only from the allowlist in
`Core_declares_only_allowed_package_references` — today `Microsoft.Extensions.Logging.Abstractions`
and nothing else. Abstractions are allowed because they keep `dotnet test` offline; anything with a
disk, clock or socket behind it belongs in `Infrastructure`.

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
| How deep an upload goes | `UploadPlanningService.MaxFileSegments` (2) and `MaxFolderSegments` (1), passed into `Place` as `maxSegments`. The limits differ by a segment because a file's last segment is the file itself; both mean "one level of folders". The top-level document is registered *before* the depth check, so a folder holding only a nested subfolder still becomes a document |
| Part order inside a folder-document | `UploadPlanningService.PlanEntries` — `StringComparer.Ordinal`, so `page10` before `page2` |
| Whether a request is refused before anything is stored | `UploadService.Refuse` throws `UploadRefusedException`; a missing cabinet throws `UnknownCabinetException`. A refusal is terminal and there is only ever one of them, which is why it throws where a per-entry refusal stays a value in the report |
| Running an upload: validate, store per document, report | `Core/Domain/Uploads/UploadService.cs` |
| Upload size and part-count limits | `Program.cs`, from `Archive:MaxUploadBytes` and `Archive:MaxUploadEntries` |
| Which HTTP status a refusal answers with | `Api/Middleware/ArchiveExceptionMiddleware.cs` — a `switch` over the expected exception types, one arm each. `UnknownCabinetException` → `404`, `UploadRefusedException` → `400`, anything else rethrown for `UseExceptionHandler` → `500`. A new expected refusal is one more arm |
| Zip layout, duplicate-title naming, marker format (export rules) | Export service _(planned)_ |
| Date-range semantics (inclusive calendar dates, user's time zone) | Archive query API _(planned)_ |
| Reading a part's content | _(planned)_ — no endpoint yet, and the Export service cannot be built without one. It belongs beside `Documents` in `CabinetsController`, streaming the part file the store addresses by id |
| Updating index values | _(planned)_ — no endpoint yet. The write rule already exists (`IndexFieldRules.ValidateWrite`); what is missing is the endpoint, `ModifiedAt`, and a rewrite of `document.json` by the same staging-and-rename route as `AddAsync` |
| Listing cabinets and their fields | _(planned)_ — no `GET /cabinets`, so a client cannot learn field types or writability without restating them |
| System fields (`id`, `StoredAt`, `ModifiedAt`) | `DocumentsStoreService.AddAsync` overwrites whatever arrived. Ids are `Guid.CreateVersion7(storedAt)`, so an id carries its own timestamp |
| Telling an unstored document from a stored one | `Document.ToStore(...)` and `Part.ToStore(...)` leave `Id` as `Guid.Empty` and set `Part.OpenContent`. The store fills in the system fields and nulls `OpenContent`. One type spans both states — see `DECISIONS.md` |
| Where a document's files go on disk | `Core/Services/Documents/DocumentsStoreService.cs` — the layout is in its header comment and composed nowhere else, including the part filename (`PartPath`) |
| The on-disk document format | `[JsonPropertyName]` on `Document` and `Part` themselves, as with `Cabinet` and `IndexField`. `IndexValue` is polymorphic — `[JsonPolymorphic]` writes the `type` discriminator, and every `Type` override needs `[JsonIgnore]` or it is written twice |
| How an upload is committed | `DocumentsStoreService.AddAsync` — written into `tmp/`, then one atomic `Directory.Move`. A document is complete or absent, never half-written |
| Recovering from an upload that died halfway | `DocumentsStoreService.Prepare`, called during registration: it clears `tmp/`. Startup is the recovery point. It is `static` because it runs before the container exists to resolve a logger from, and it also creates the `tmp/` the readiness probe writes into — delete that directory and `/health/ready` answers `503` |
| Paging a cabinet's documents | `DocumentsStoreService.ListAsync` plus `DocumentPageRequest.Skip`/`Take`, capped by `Archive:MaxPageSize` |
| Whether the service is ready to serve | `Infrastructure/Health/` — the catalog and the storage probe, both tagged `ready` and registered by the `Add…` extension that owns them |
| What reaches the container log | Four places and no others: the two startup paths in `Program.cs`, the HTTP logging options also in `Program.cs` (one line per request), `DocumentsStoreService.AddAsync` (the document stored, or the failure it could not store), and `ArchiveExceptionMiddleware` (refusals). `README.md` lists the resulting lines |
| Which log categories are on | `appsettings.json` — `Microsoft.AspNetCore` stays at `Warning`, with `Microsoft.AspNetCore.HttpLogging` named back up to `Information`. Longest-prefix wins, so the specific key beats the general one |
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
  `Cabinet`, `IndexField` and `CabinetsFileLoader`.
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
  home outside the controller. `CabinetsController` is the shape to copy: one action, four lines.
- **Contracts are data; mapping sits beside them.** `Contracts/` holds one folder per entity, each
  with `Requests/`, `Responses/` and `Mappings/`. A request or response is a record with no
  behaviour; every conversion is an extension method in a class named after the type it produces —
  `DocumentResponseMappings`, `UploadEntryResultMappings` — one class per mapped type.
- **A controller answers directly.** There is no `Results/` layer: an action calls the rules, maps
  the result and returns `Ok(...)` or `Problem(...)` inline. Anything conditional on *how* an
  operation went belongs in middleware instead.
- **No mapping library.** AutoMapper is commercially licensed, the same reason FluentAssertions was
  turned down. Roughly half these conversions are not property copies anyway — the `IndexValue`
  hierarchy becomes a tagged shape, and `IFormFile` becomes a `Func<Stream>` — so a convention-based
  mapper would need custom converters for exactly the parts that are hard. If one is ever wanted,
  it should be Mapperly: MIT, source-generated, and it fails at compile time rather than at runtime.
- **No comments except `<summary>` blocks.** Rationale belongs in `DECISIONS.md`, not inline.
- **Anything that depends on how an operation *went* lives in middleware, not in the endpoint.**
  `ArchiveExceptionMiddleware` is the only place in the service that picks a status code. Core
  throws for a refusal that is terminal — `UnknownCabinetException`, `UploadRefusedException` — and
  the middleware maps each with one `switch` arm, answering `ProblemDetails`. Anything it does not
  recognise is rethrown for `UseExceptionHandler`, which answers `500`, so a bug is never swallowed
  as a refusal. Adding an expected refusal is one arm and nothing else.
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
- **A service logs its own work, including in `Core`.** `Core` may take
  `Microsoft.Extensions.Logging.Abstractions` — it is on the allowlist in
  `Core_declares_only_allowed_package_references` — so `DocumentsStoreService` logs the document it
  stored and the failure it could not, in the `catch` that already handles it. Do not reach for a
  decorator to get around the package rule; the rule allows abstractions and forbids everything
  with a disk, clock or socket behind it. A controller still gets no logger — the per-request line
  comes from HTTP logging middleware.
- **Nothing a user supplied is logged.** A document title, a folder name and a file name are
  attacker-controlled UTF-8, so the log holds server-generated ids, cabinet names and counts
  instead. Bodies and headers are off for the same reason, plus size. A refusal's reason is logged
  because it is a sentence the service itself composed.
- **Warnings are errors** (`Directory.Build.props`). Do not suppress; fix.
- Shared build settings live in `Directory.Build.props`. Do not restate `TargetFramework`, `Nullable`
  or `ImplicitUsings` in a csproj.
- No credential in any image or in this repository. Configuration comes from the environment.

## How to add an operation

1. Decide which service owns it. If it touches storage, it is the Archive.
2. If it is a rule, put it in `Core`, in the feature folder it belongs to — not in the endpoint, not
   in Infrastructure, not in the client.
3. Storing is part of what the Archive *does*, so a store lives in `Core/Services/<Entity>/` behind
   an interface in `Core/Services/Interfaces/<Entity>/`. The interface stays because it is the seam
   a test fake replaces, not because the implementation is elsewhere. **Never add a package
   reference to `Core`** — that rule is enforced, and it is what keeps `dotnet test` offline.
   `Infrastructure` holds the dependency registrations, the health checks and whatever else the
   containerised deployment needs.
4. Add the endpoint in `Api`, using the shared error shape.
5. Add a test per rule that fails when the rule is broken.
6. Update the documents listed at the bottom of this file.

## How to verify a change

```bash
dotnet build          # warnings are errors
dotnet test           # offline, no containers — 70 tests across three test projects at time of writing
docker compose up --build   # the supported way to run it; rebuilds and recreates in one command

# Running from source needs both paths, because neither is defaulted:
Archive__CabinetsFile=$PWD/fixtures/cabinets.json Archive__StoragePath=$PWD/.archive-data \
  dotnet run --project Nemetschek.Archive/Nemetschek.Archive.Api --no-launch-profile
```

The engine here is **Podman 6.1.4**, driven with `podman compose` (which delegates to the Docker
Compose v2 binary). Use that, not `podman-compose` — the Python reimplementation cannot do
`up --build` on Windows; `README.md` has the detail. The two `dotnet` lines need no engine at all
and remain the gate for every change.

**Documents land inside the VM, not in a Windows folder:**
`podman volume inspect nemetschek-archive_archive-data --format '{{.Mountpoint}}'` names the path,
and `podman exec … ls -R /var/lib/archive/cabinets` is the quickest way to look. See "Where that
actually is on disk" in `README.md` before going hunting for it on the host.

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
the writable-field rule (removing the check fails exactly its two tests), the Core-package rule
(adding a `PackageReference` to Core failed `Core_declares_no_package_references`, as the test was
then called), and the store-failure log (deleting the whole `catch` arm in
`DocumentsStoreService.AddAsync` fails exactly
`A_store_failure_is_logged_with_its_exception_and_rethrown`; deleting only the `LogError` does **not**
compile, since the now-unused `exception` variable is a warning and warnings are errors).

Startup is fail-fast and verified by hand: an absent `Archive:CabinetsFile`, or a path that does not
exist, stops the service with a message naming the setting or the file. If you add a required setting,
keep that property — a service that starts and then fails every request is harder to diagnose.

The upload endpoint has been exercised by hand against the real `fixtures/sample-material`, which is
how a change to it should be checked until the rule tests are written:

A refusal comes back as `ProblemDetails`, so what to check is the status **and** the `detail`
sentence. A *per-entry* failure is different: it stays a row in the report and the status is still
`200` — see `docs/upload-requirements.md`.

| Request | Expected |
|---|---|
| `Invoices` / `Batch` / `sample-material` | `200`, 12 `Stored` and 2 `Skipped`; `delivery-2026-03` has parts `page1.txt, page10.txt, page2.txt` in that order; `Договор София.txt` and `Фактури Q1` come back byte-identical |
| `Invoices` / `Status` | `400` — "accepts at most 12 characters, but the value has 15" |
| `Invoices` / `ApprovedBy` | `400` — "is read-only and cannot be chosen" |
| `Invoices` / `Amount` | `400` — "holds a number value; a text field is required" |
| `Invoices` / `Nonsense` | `400` — "has no index field 'Nonsense'" |
| no `field` at all | `400` — the model-validation shape, "Choose the index field the folder's name is written into." |
| unknown cabinet | `404` — "There is no file cabinet named …" |

All seven re-verified against the running container on 2026-10-29.

And for storage, which is only meaningfully checked by restarting:

| Check | Expected |
|---|---|
| Upload, stop the service, start it again, `GET …/documents` | Same documents, same ids, read from disk — there is no in-memory state |
| Upload the sample material twice | `total` is 24; the 12 repeated titles all coexist, since the id is the identity and the title is not |
| `ls` the storage path | One directory per document; part files named by GUID; the user's names only inside `document.json`, unescaped |
| A stray directory under `tmp/` | Gone after the next start |
| `Archive:StoragePath` absent, or pointing somewhere unusable | The service refuses to start and names the setting or the path |

`docker compose logs -f archive-api` was checked by hand against the running container: a read, an
upload that stored two documents and a refused upload each produce their line, and the readiness
probe produces none. `README.md` lists the lines to expect.

Still to come, once the service is containerised: `docker compose up` with browsable OpenAPI per
service, `docker compose down && up` preserving data, `docker compose logs` showing *every* service, and
the round trip — upload `sample-material` twice, export, unpack, account for every difference.

## Documentation upkeep — required

Any change to this repository updates the affected documents in the same change:

- `README.md` — build, run, test, and the settings that must be supplied
- `AGENTS.md` — this file: layout, rule locations, conventions
- `DECISIONS.md` — one line per decision as "A, because B"; plus cuts, AI notes, time log
- `docs/` — the per-feature requirement notes

A change that alters behaviour and leaves the documents stale is incomplete.
