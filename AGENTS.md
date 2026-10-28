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
├── Nemetschek.Archive.Core/             the rules — NO external dependencies
│   ├── Domain/     one folder per domain entity type
│   │   ├── Cabinets/      Cabinet
│   │   ├── IndexFields/   IndexField, FieldType, IndexValue, IndexFieldRules, FieldViolation
│   │   ├── Documents/     _(planned)_ Document, Part
│   │   └── Uploads/       _(planned)_ PlanUpload — the folder-to-document mapping
│   └── Ports/      ICabinetCatalog  (+ IDocumentStore, IContentStore _(planned)_)
├── Nemetschek.Archive.Infrastructure/   adapters
│   └── Cabinets/   CabinetsFileLoader, CabinetsFileDto, CabinetCatalog
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
| What makes a cabinets.json file invalid | `Infrastructure/Cabinets/CabinetsFileDto.cs` — DataAnnotations attributes and `IValidatableObject` on the DTOs themselves. Fatal at startup, never tolerated |
| The on-disk JSON contract | `[JsonPropertyName]` on each DTO property — there are no serialiser options |
| Field type / maxLength / writable enforcement | `Core/Domain/IndexFields/IndexFieldRules.cs` — the only place |
| Which fields may be chosen for upload or as an export marker | `IndexFieldRules.ValidateChosenTextField` |
| Folder → document mapping (upload rules) | `Core/Domain/Uploads/` _(planned)_ — see `docs/upload-requirements.md` |
| Zip layout, duplicate-title naming, marker format (export rules) | Export service _(planned)_ |
| Date-range semantics (inclusive calendar dates, user's time zone) | Archive query API _(planned)_ |
| System fields (`id`, `StoredAt`, `ModifiedAt`) | Archive only; never accepted from a client |
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
dotnet test           # offline, no containers — 70 tests across three test projects at time of writing
dotnet run --project Nemetschek.Archive/Nemetschek.Archive.Api
docker compose up --build   # NOT yet verified — written without Docker available
```

With the service running: `/swagger`, `/openapi/v1.json`, `/health` (liveness, runs no checks) and
`/health/ready` (readiness, checks the cabinet catalog). A dependent service waits on `/health/ready`,
never on `/health`.

To check a test really pins its rule, break the rule and confirm *that* test fails. Done so far for:
the writable-field rule (removing the check fails exactly its two tests) and the Core-has-no-packages
rule (adding a `PackageReference` to Core fails `Core_declares_no_package_references`).

Startup is fail-fast and verified by hand: an absent `Archive:CabinetsFile`, or a path that does not
exist, stops the service with a message naming the setting or the file. If you add a required setting,
keep that property — a service that starts and then fails every request is harder to diagnose.

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
