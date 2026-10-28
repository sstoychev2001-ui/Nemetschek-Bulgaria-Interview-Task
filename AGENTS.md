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
Task/fixtures/                                           cabinets.json, sample-material.zip (given, do not edit)
Nemetschek.sln                                           one solution, covers every service
global.json  Directory.Build.props                       SDK pin and shared build settings
docker-compose.yml  docker-compose.override.yml  .env    _(skeleton; rewritten when the service is containerised)_
Nemetschek.Archive/
├── Nemetschek.Archive.Api/              endpoints, DTOs, error shape, DI, OpenAPI
├── Nemetschek.Archive.Core/             the rules — NO external dependencies
│   ├── Cabinets/   Cabinet, IndexField, FieldType, IndexValue, IndexFieldRules, FieldViolation
│   ├── Ports/      ICabinetCatalog  (+ IDocumentStore, IContentStore _(planned)_)
│   ├── Upload/     _(planned)_ PlanUpload — the folder-to-document mapping
│   └── Documents/  _(planned)_ Document, Part, query contracts
├── Nemetschek.Archive.Infrastructure/   adapters: cabinets.json loader, stores _(planned)_
└── Nemetschek.Archive.Tests/            one test project for the service
Nemetschek.Export/                       _(planned)_ same shape
```

Project references: `Api → Infrastructure → Core`, and `Api → Core`. **`Core` references nothing** —
no NuGet packages, no other project.

Nothing under `Nemetschek.Archive.*` may reference `Nemetschek.Export.*`, or the reverse. The Export
service reaches the Archive over HTTP only, with its own client DTOs. That duplication is deliberate.

**These rules are enforced by tests, not by trust** — see
`Nemetschek.Archive.Tests/Architecture/ProjectDependencyTests.cs`. It reads the real csproj files, so a
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
| Cabinet and field definitions | `Task/fixtures/cabinets.json`, loaded read-only by Infrastructure |
| Field type / maxLength / writable enforcement | `Core/Cabinets/IndexFieldRules.cs` — the only place |
| Which fields may be chosen for upload or as an export marker | `IndexFieldRules.ValidateChosenTextField` |
| Folder → document mapping (upload rules) | `Core/Upload/` _(planned)_ — see `docs/upload-requirements.md` |
| Zip layout, duplicate-title naming, marker format (export rules) | Export service _(planned)_ |
| Date-range semantics (inclusive calendar dates, user's time zone) | Archive query API _(planned)_ |
| System fields (`id`, `StoredAt`, `ModifiedAt`) | Archive only; never accepted from a client |

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
dotnet test           # offline, no containers — 32 tests at time of writing
```

To check a test really pins its rule, break the rule and confirm *that* test fails. Done so far for:
the writable-field rule (removing the check fails exactly its two tests) and the Core-has-no-packages
rule (adding a `PackageReference` to Core fails `Core_declares_no_package_references`).

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
