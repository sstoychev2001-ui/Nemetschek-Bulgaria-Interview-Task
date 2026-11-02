# Instructions for humans and AI agents

Read this before changing anything. It says where things live, what the conventions are, and how to
verify a change. Sections marked _(planned)_ do not exist yet.

## The one rule that matters most

The archive rules live in the **services**, never in the client and never duplicated. If you are about
to add a length check, a writability check, or a folder-to-document mapping rule to the web client,
stop — it belongs to the Archive's `Core` project.

## Repository layout

One top-level directory per service, with that service's projects directly inside it — no `src/` or
`tests/` nesting. Five documents at the root and no `docs/` folder: the per-feature requirement
notes were folded into `DECISIONS.md`, which is where a reviewer looks for the reasoning anyway.

```
README.md  AGENTS.md  CLAUDE.md  DECISIONS.md  TASK.md   run it, change it, work here, why, the brief
fixtures/                                                cabinets.json, sample-material/ (given, do not edit)
Nemetschek.sln                                           one solution, covers every service
global.json  Directory.Build.props                       SDK pin and shared build settings
docker-compose.yml                                       one file; both services, every setting literal
Nemetschek.Archive/
├── Nemetschek.Archive.Api/              endpoints, DTOs, error shape, DI, OpenAPI
│   ├── Controllers/  one per entity: Cabinets, Uploads, Documents — actions only, no helpers
│   ├── Contracts/    one folder per entity with Requests/, Responses/, Mappings/, plus Shared/
│   └── Middleware/   ArchiveExceptionMiddleware — the only status-code decision
│       Program.cs    settings, the CORS policy, and the pipeline order the two depend on
├── Nemetschek.Archive.Core/             the Archive itself — abstractions-only packages, allowlisted
│   │   every root folder is split again by entity: Cabinets, Documents, IndexFields, Uploads
│   ├── Domain/       the entities only — Cabinet, IndexField, IndexValue, Document, Part, …
│   ├── Services/     what the Archive does, including how it stores
│   │   ├── Cabinets/    CabinetCatalog
│   │   ├── Documents/   DocumentsService, DocumentsStoreService, DocumentMetadataFile
│   │   ├── Uploads/     UploadService, UploadPlanningService
│   │   └── Interfaces/  ICabinetCatalog, IDocumentStore
│   ├── Utils/        helpers with no rules in them, split by entity
│   │   └── Documents/   DocumentStorageUtils — the layout's names and its two parsers
│   ├── Validation/   IndexFieldRules, the per-type value validators, RelativePath
│   └── Exceptions/   UnknownCabinetException, UploadRefusedException
├── Nemetschek.Archive.Infrastructure/   composition and the edges of the container
│   ├── Cabinets/    CabinetsFileLoader — reads the deployment's cabinets.json
│   └── *ServiceCollectionExtensions — the dependency registrations
├── Nemetschek.Archive.Core.Tests/        one test project per production project
│   ├── Cabinets/    TestCabinets (the fixture in code) + the test that keeps it honest
│   ├── Domain/      the rules each domain type declares on itself
│   ├── Documents/   the store, against a temp directory
│   └── Uploads/     the folder→document rules, and running an upload
├── Nemetschek.Archive.Infrastructure.Tests/  the loader, path resolution and the DI wiring
└── Nemetschek.Archive.Api.Tests/
    ├── Architecture/  the solution-wide dependency rules, and RepositoryLayout
    ├── Contracts/     paging arithmetic and every mapping
    ├── Endpoints/     the real service in process, via WebApplicationFactory
    └── Middleware/    the one place that picks a status code
Nemetschek.Export/                       same shape as the Archive
├── Nemetschek.Export.Api/               ExportsController, DTOs, ExportExceptionMiddleware, OpenAPI
├── Nemetschek.Export.Core/              the export rules — abstractions-only packages, allowlisted
│   ├── Domain/Exports/     ArchiveDocument, ArchivePart, ExportRequest
│   ├── Services/Exports/   ExportService — the rules, the naming and the marker in one class
│   └── Clients/            ArchiveHttpClient, the only way to the Archive
│       ├── Interfaces/     IArchiveClient — the seam a test fake replaces
│       ├── Requests/       IndexValuesBatchRequest — the batch marker write
│       └── Responses/      ArchiveDocumentPageResponse — the page envelope, transport only
├── Nemetschek.Export.Infrastructure/    AddArchiveClient — the HttpClient and its base address
├── Nemetschek.Export.Core.Tests/        one test project per production project
├── Nemetschek.Export.Infrastructure.Tests/
└── Nemetschek.Export.Api.Tests/         boots the scaffold through WebApplicationFactory
```

**One test project per production project**, named `<Project>.Tests` and referencing only its
subject. There is no shared test-helper project: `TestSupport` was removed, and a helper now lives
in the one project that needs it — `TestCabinets` in `Core.Tests`, `RepositoryLayout` in
`Api.Tests`, and a four-line `RepositoryFixtures` duplicated in the two projects that only need the
fixture path. Both project rules are enforced:
`Each_test_project_references_the_project_it_tests` and
`No_production_project_references_a_test_project`.

A test project may take a package the project it tests does not — `Mvc.Testing` in `Api.Tests`,
`DependencyInjection` and `Logging` in `Infrastructure.Tests`. Only `Core` has an allowlist.

Project references: `Api → Infrastructure → Core`, and `Api → Core`. **`Core` references no other
project**, and takes a NuGet package only from the allowlist in
`Core_declares_only_allowed_package_references` — today `Microsoft.Extensions.Logging.Abstractions`
and nothing else. Abstractions are allowed because they keep `dotnet test` offline; anything with a
disk, clock or socket behind it belongs in `Infrastructure`.

Nothing under `Nemetschek.Archive.*` may reference `Nemetschek.Export.*`, or the reverse. The Export
service reaches the Archive over HTTP only, with its own client DTOs. That duplication is deliberate.

**These rules are enforced by tests, not by trust** — see
`Nemetschek.Archive.Api.Tests/Architecture/ProjectDependencyTests.cs`, which covers **both**
services. It reads csproj files rather than referencing projects, so one place can enforce the
layering of each service without either depending on the other. It reads the real csproj files, so a
cross-layer or cross-service reference fails `dotnet test`. If one of those tests fails, the fix is
almost always to move the code, not to change the test.

**One solution at the root covers every service.** Per-service solutions were considered and rejected: a
`.sln` is only a list of projects for tooling and does not enforce a boundary (a `ProjectReference`
works across solution membership), while a root solution is what lets `dotnet test` from a clean clone
cover everything in one command. To work on one service in isolation, use a `.slnf` solution filter
rather than adding a second `.sln`. All twelve projects are in `Nemetschek.sln` —
`Every_project_in_the_repository_is_in_the_solution` fails if a new one is left out, and
`Both_services_have_the_same_shape` fails if one service gains a project the other lacks.

## Where the rules live

| Rule | Where |
|---|---|
| Cabinet and field definitions | `fixtures/cabinets.json`, loaded once at startup by `Infrastructure/Cabinets/CabinetsFileLoader.cs` |
| What makes a cabinets.json file invalid | DataAnnotations attributes and `IValidatableObject` on `Cabinet` / `IndexField`, plus the private `CabinetsFile` root inside the loader. Fatal at startup, never tolerated |
| The on-disk JSON contract | `[JsonPropertyName]` on **every** property — there are no serialiser options, so matching is case-sensitive and an omitted attribute silently binds to nothing |
| Field type / maxLength / writable enforcement | `Core/Validation/IndexFields/IndexFieldRules.cs` — the only place. `ValidateWrite` judges one value, `ValidateWrites` judges a batch and returns a violation per refused field; a DTO may not take this over, because the rule needs the cabinet and the upload path calls the same function |
| Resolving a field name to a field (case-insensitive, ordinal) | `IndexFieldRules.FindField`, private. `Cabinet` is data and carries no lookup. `UploadService.StoreAsync` resolves its own, with `First` — the rules have already proved the field is there |
| Which fields may be chosen for upload or as an export marker | `IndexFieldRules.ValidateChosenTextField` |
| Folder → document mapping (upload rules) | `Core/Domain/Uploads/UploadPlanningService.cs`. See "The upload endpoint" in `DECISIONS.md` |
| What an uploaded relative path may be | `Core/Domain/Uploads/RelativePath.cs` — rooted paths, `.`, `..` and control characters are refused |
| How deep an upload goes | `UploadPlanningService.MaxFileSegments` (2) and `MaxFolderSegments` (1), passed into `Place` as `maxSegments`. The limits differ by a segment because a file's last segment is the file itself; both mean "one level of folders". The top-level document is registered *before* the depth check, so a folder holding only a nested subfolder still becomes a document |
| Part order inside a folder-document | `UploadPlanningService.PlanEntries` — `StringComparer.Ordinal`, so `page10` before `page2` |
| Whether a request is refused before anything is stored | `UploadService.Refuse` throws `UploadRefusedException`; a missing cabinet throws `UnknownCabinetException`. A refusal is terminal and there is only ever one of them, which is why it throws where a per-entry refusal stays a value in the report |
| Running an upload: validate, store per document, report | `Core/Domain/Uploads/UploadService.cs` |
| Upload size and part-count limits | `Program.cs`, from `Archive:MaxUploadBytes` and `Archive:MaxUploadEntries` |
| Whether a browser client on another host may call | The CORS policy in each `Program.cs`, from `Archive:AllowedOrigins` / `Export:AllowedOrigins` — comma-separated, and **empty means any origin**. `app.UseCors()` sits *before* the exception middleware in both, so a refusal carries the headers too; without that a browser withholds the error body and the client cannot show the server's message. The Export also exposes `Content-Disposition` so a client can read the zip's name rather than restate it |
| Which HTTP status a refusal answers with | `Api/Middleware/ArchiveExceptionMiddleware.cs` — a `switch` over the expected exception types, one arm each. `UnknownCabinetException` → `404`, `UploadRefusedException` → `400`, anything else rethrown for `UseExceptionHandler` → `500`. A new expected refusal is one more arm |
| Zip layout (file / folder / empty folder per document) | `Export.Core/Services/Exports/ExportService.WriteDocumentAsync`. See "The Export service" in `DECISIONS.md` |
| Duplicate-title naming (` (2)`, ` (3)`, before the extension) | `ExportService.GetEntryName` — the only place. It takes the table of names already used, so the same call names documents across the archive and parts inside one folder-document, each with its own table |
| The marker's wording and date | `ExportService.GetMarker` — `Exported on yyyy-MM-dd`, the UTC date, formatted invariantly |
| Marking after the content, never before | `ExportService.WriteAsync` — one request after the whole archive is built, naming every document. The marker claims a document was exported, so a failure partway leaves nothing marked |
| How the Export pages the Archive | `ArchiveHttpClient.ReadDocumentsAsync` — one request made before anything is tested, then a page at a time while the documents yielded so far fall short of the total it reported, yielding lazily as an `IAsyncEnumerable` so a caller that stops early pays for no further page. The condition is tested *after* yielding and before the next fetch, so no page past the end is ever asked for |
| Reading the Archive from the Export service | `Export.Core/Clients/ArchiveHttpClient.cs` — over HTTP only. It deserialises straight into `ArchiveDocument` / `ArchivePart`, which are the wire shape *and* the rules' model in one type, validating themselves in `OnDeserialized`; only the page envelope lives under `Clients/Responses/`. The `HttpClient` it takes is configured in `Export.Infrastructure/ExportServiceCollectionExtensions.cs`, which owns the base address. Enforced both ways by `No_Archive_project_references_an_Export_project` and `No_Export_project_references_an_Archive_project` |
| Which HTTP status an export refusal answers with | `Export.Api/Middleware/ExportExceptionMiddleware.cs` — what the Archive refused as `400`, an unreachable Archive as `502`, anything unrecognised rethrown for `UseExceptionHandler` |
| Where the zip is built | `ExportService.WriteAsync` — into a `DeleteOnClose` temp file (`ExportService.Staging`), returned as a readable stream the caller owns and disposes. Building before sending is what keeps a failure a status rather than a truncated `200`; the reasoning is in "The Export service" in `DECISIONS.md` |
| Reading the Archive from the Export service | `Nemetschek.Export.Infrastructure` _(planned)_ — over HTTP only, with its own DTOs. Enforced both ways by `No_Archive_project_references_an_Export_project` and `No_Export_project_references_an_Archive_project` |
| Date-range semantics (inclusive calendar dates, UTC) | `Core/Validation/Documents/CalendarRange.cs` — turns two calendar dates into a half-open window of UTC instants; `Domain/Documents/StoredRange.cs` holds it and prunes whole days. There is no time-zone parameter: see "Removed: the time zone parameter" in `DECISIONS.md` |
| Reading a part's content | `DocumentsController.PartContent` → `DocumentsService.OpenPartAsync` → `DocumentsStoreService.OpenPartAsync`, which streams the part file from disk |
| Updating index values | `DocumentsController.SetIndexValues` → `DocumentsService.SetIndexValuesAsync`, one route for one **or many** documents (`PATCH /cabinets/{c}/documents/index-values`). It validates every value with `IndexFieldRules.ValidateWrite` and resolves every id before anything is written, so a bad value or a bad id changes nothing; then `DocumentsStoreService.SetIndexValuesAsync` rewrites each `document.json` by staging plus one atomic `File.Move(overwrite: true)` and moves `ModifiedAt`. It returns nothing: the service has already resolved every id, so the count it answers with is the id count There is no per-document route — it was removed once the batch existed |
| Listing cabinets and their fields | `CabinetsController.Cabinets` → `ICabinetCatalog.All` → `CabinetResponseMappings`. Each field answers its `type`, its `maxLength`, `writable`, and `choosable` — the last so a client can fill an upload-field or marker-field picker without restating the rule that both must be writable text |
| System fields (`id`, `StoredAt`, `ModifiedAt`) | `DocumentsStoreService.AddAsync` overwrites whatever arrived. Ids are `Guid.CreateVersion7(storedAt)`, so an id carries its own timestamp |
| Telling an unstored document from a stored one | A document on its way in is built with an object initialiser, which leaves `Id` as `Guid.Empty`; `Part.ToStore(...)` sets `OpenContent`. The store fills in the system fields and nulls `OpenContent`. One type spans both states — see `DECISIONS.md` |
| Where a document's files go on disk | `Core/Utils/Documents/DocumentStorageUtils.cs` — the folder names, the day and instant formats, and the two parsers that read a path back. Composing a path from them is done where it is used, in `DocumentsStoreService` |
| The on-disk document format | `[JsonPropertyName]` on `Document` and `Part` themselves, as with `Cabinet` and `IndexField`. `IndexValue` is polymorphic — `[JsonPolymorphic]` writes the `type` discriminator, and every `Type` override needs `[JsonIgnore]` or it is written twice |
| How an upload is committed | `DocumentsStoreService.AddAsync` — written into `tmp/`, then one atomic `Directory.Move`. A document is complete or absent, never half-written. `DocumentMetadataFile` writes the `document.json` either side of that move |
| Recovering from an upload that died halfway | `DocumentsServiceCollectionExtensions.PrepareStorage`, called from `AddDocumentsStore`: it creates `cabinets/`, then clears and recreates `tmp/`. Registration is the recovery point, and from there on the store takes both directories for granted |
| Paging a cabinet's documents | `DocumentsStoreService.ListAsync` plus `DocumentPageRequest.Skip`/`Take`, capped by `Archive:MaxPageSize` |
| What reaches the container log | Four places and no others: the two startup paths in `Program.cs`, the HTTP logging options also in `Program.cs` (one line per request), `DocumentsStoreService.AddAsync` (the document stored, or the failure it could not store), and `ArchiveExceptionMiddleware` (refusals). `README.md` lists the resulting lines |
| Which log categories are on | `appsettings.json` — `Microsoft.AspNetCore` stays at `Warning`, with `Microsoft.AspNetCore.HttpLogging` named back up to `Information`. Longest-prefix wins, so the specific key beats the general one |
| How the service is containerised | `Nemetschek.Archive.Api/Dockerfile` and the root `docker-compose.yml` |

## Conventions

- **A rule gets a test that names it.** Test method names read as the rule, so a reviewer can map
  test → rule: `Write_to_a_non_writable_field_is_refused`. If you change a rule, the failing test
  should tell you which rule you broke.
- **A test asserts what the code does, not what it should do.** Where the two differ, the test
  states the behaviour and the disagreement is recorded in `DECISIONS.md` — a test written against
  an intention the code does not hold is a test that fails for the wrong reason. The store logging
  a cancelled upload at `Error` is the live example.
- **A skipped test is how an owed rule is recorded, not how a failing one is silenced.** There are
  none left — the Export placeholders were replaced by the real rule tests — but if you add one,
  give the `Skip` a reason naming what is missing. Never add `Skip` to a test that used to pass.
- **An endpoint is tested through the real service.** `Api.Tests/Endpoints/ArchiveApplication.cs` is
  a `WebApplicationFactory` over `Program.cs` on a temp storage directory, so the statuses, the
  error shapes and the page cap are covered as a caller sees them, with no container and no
  network. Add an endpoint, add a row there.
- **`dotnet test` must pass from a clean clone with no network and no containers.** Rule tests run
  against `Core` with hand-written fixtures; nothing in the test project may need a database or Docker.
- **A lookup that cannot answer throws; it does not return `null`.** `IDocumentStore.FindAsync`,
  `OpenPartAsync` and `SetIndexValuesAsync` raise `DocumentNotFoundException` themselves, so no
  caller tests a result before using it — the method responsible for the operation is the one that
  reports it failed. Note the deliberate asymmetry with the rule below: *validation* returns
  violations as values because a report lists many at once, while a *lookup* has exactly one way to
  fail and nothing useful to say about it beyond that.
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
  home outside the controller. `CabinetsController` is the shape to copy: one action, one line.
  **One controller per entity**, routed at the path that entity owns — `cabinets`,
  `cabinets/{cabinetName}/uploads`, `cabinets/{cabinetName}/documents` — so the Api is split the way
  `Contracts/` and `Core/Services/` already are. A new operation on documents goes in
  `DocumentsController`; a new entity gets its own.
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
- **No comments except `<summary>` blocks. Anywhere.** Production code and test code alike: a name
  is the explanation, and anything needing a paragraph is a decision, which belongs in
  `DECISIONS.md` where a reviewer will find it. Swept twice on 2026-10-30 — 168 lines out of the
  test projects, then 95 out of all 114 source files — so a file that grows one back is a file to
  fix. If a rule only existed in a comment, move it to `DECISIONS.md` before deleting it: the
  on-disk layout diagram went there rather than being lost.
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
- **A log line carries what the request line does not.** The HTTP log already records the method,
  the path, the query, the status and the duration of every call, so a service's own entry adds only
  what a reader cannot get from that — counts, ids, and the fact that something was written.
  `ExportService` logs "Exported N document(s) and M file(s), each marked" and nothing from the
  request; the cabinet and the dates were in it once and came out again.
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
   `Infrastructure` holds the dependency registrations and whatever else the containerised
   deployment needs.
4. Add the endpoint in `Api`, using the shared error shape.
5. Add a test per rule that fails when the rule is broken.
6. Update the documents listed at the bottom of this file.

## The two services and the boundary between them

The Export service reads the Archive **over HTTP and no other way**: `IArchiveClient` is the only
door and `ArchiveHttpClient` the only implementation, both under `Export.Core/Clients/`, and it
keeps its own request and document types rather than sharing one with the Archive — and those
document types are single records carrying both the JSON attributes and the rules' invariants, the
same way the Archive's `Document` does. What stayed in
`Export.Infrastructure` is the `HttpClient` registration — the base address, the trailing slash and
the infinite timeout a streamed export needs — so `Core` names no package for any of it. Two
architecture tests enforce that neither service references the other.

What that boundary costs, and why it is worth it: the Export cannot filter by date, read content or
write a marker without the Archive exposing a route for it. Three routes exist for exactly that
reason, and they are the Archive's API rather than the Export's private channel — a web client uses
the same ones.

```
POST /exports  ──►  GET  /cabinets/{c}/documents?from&to&page&size           (page by page)
                    GET  /cabinets/{c}/documents/{id}/parts/{partId}/content  (streamed)
                    PATCH /cabinets/{c}/documents/index-values                (the marker, in one call)
```

`compose up` starts both. `export-api` has a plain `depends_on`, so it is started after the
Archive but does not wait for it to answer: an export attempted in that window gets a `502` naming
the Archive as unreachable, which is the honest answer and the one its tests pin. Give the Archive
a moment before the first export.

## How to verify a change

```bash
dotnet build          # warnings are errors
dotnet test           # offline, no containers — 448 tests across six test projects
docker compose up --build -d          # both services; give the Archive a moment before exporting

# Running from source needs both paths, because neither is defaulted:
Archive__CabinetsFile=$PWD/fixtures/cabinets.json Archive__StoragePath=$PWD/.archive-data   dotnet run --project Nemetschek.Archive/Nemetschek.Archive.Api --no-launch-profile
```

The two `dotnet` lines are the gate for every change and need no engine. The engine here is
**Podman 6.1.4**, driven with `podman compose`, which delegates to the Docker Compose v2 binary —
not `podman-compose`, which cannot do `up --build` on Windows. `README.md` has the detail, and the
`docker` CLI itself has never been exercised.

**A change that touches storage, the endpoints or the export is not verified until the round trip
runs**: upload the sample material twice, export it, unpack it, and account for every difference.
The expected result is in `README.md` under "The round trip" — 15 of the original folder's 16
entries byte-identical, the sixteenth being the nested file the upload rules skip.

**Documents land inside the VM, not in a Windows folder.**
`podman volume inspect nemetschek-archive_archive-data --format '{{.Mountpoint}}'` names the path;
`podman exec … ls -R /var/lib/archive/cabinets` is the quickest way to look.

**Configuration is single-sourced in `docker-compose.yml`**, literally — there is no `.env` and no
override file. Keep `name: nemetschek-archive` at the top: it prefixes the documents' volume, and
losing it brings the Archive up against an empty one. `appsettings.Development.json` deliberately
carries no `Archive` section, because a relative path there resolves against the content root —
`/app` inside the image — so a forgotten volume mount would store documents in the container's own
layer and lose them silently. If you add a setting, add it to `docker-compose.yml` and to the table
in `README.md`.

**To check a test really pins its rule, break the rule and confirm *that* test fails.** Done for the
writable-field rule (removing the check fails exactly its two tests), the Core-package rule (adding
a `PackageReference` to `Core` fails the allowlist test), and the store-failure log — where deleting
the whole `catch` arm fails exactly `A_store_failure_is_logged_with_its_exception_and_rethrown`,
while deleting only the `LogError` does **not** compile, because the unused `exception` variable is
a warning and warnings are errors.

**Startup is fail-fast.** An absent `Archive:CabinetsFile` or `Export:ArchiveBaseAddress`, or a
storage path that cannot be created, stops the service with a message naming the setting. Keep that
property for any setting you add: a service that starts and then fails every request is harder to
diagnose than one that refuses to start.

## Documentation upkeep — required

Any change to this repository updates the affected documents in the same change:

- `README.md` — build, run, test, and the settings that must be supplied
- `AGENTS.md` — this file: layout, rule locations, conventions
- `DECISIONS.md` — one line per decision as "A, because B"; plus cuts, AI notes, time log
- The feature-level reasoning lives in `DECISIONS.md` under the section for that feature

A change that alters behaviour and leaves the documents stale is incomplete.
