# Document Archive

A small document archive: documents live in file cabinets, are described by typed index fields, and are
uploaded as folders of material and exported back out as zip archives.

**Current status: the Archive service's solution and field rules exist and are tested. There is no
upload, no storage, no Export service, no web client, and the containers do not work yet.** See
[`DECISIONS.md`](DECISIONS.md) for exactly what is built and what is missing.

## What this will contain

| Piece | Technology | Status |
|---|---|---|
| Archive service | ASP.NET Core 9 | Solution, field rules and a `/health` endpoint |
| Export service | ASP.NET Core 9 | Not started |
| Web client | TBD | Not started |

The Export service never touches the Archive's storage; it only uses the Archive's HTTP API.

## Layout

One top-level directory per service, with that service's projects directly inside.

```
Nemetschek.sln                            one solution for every service
Nemetschek.Archive/
├── Nemetschek.Archive.Api/               HTTP surface, OpenAPI
├── Nemetschek.Archive.Core/              the rules, no external dependencies
├── Nemetschek.Archive.Infrastructure/    adapters for storage and fixtures
└── Nemetschek.Archive.Tests/             rule tests, offline
Task/fixtures/                            cabinets.json and sample-material.zip (given)
```

## Build, run, test

Requires the .NET 9 SDK (pinned in `global.json`; 9.0.203 or a later 9.0.x feature band).

```bash
dotnet build                        # warnings are errors
dotnet test                         # offline, no containers — 32 tests
dotnet run --project Nemetschek.Archive/Nemetschek.Archive.Api
```

With the service running, the OpenAPI description is at `/openapi/v1.json`, the browsable UI at
`/swagger`, and a liveness probe at `/health`.

`docker compose up` does **not** work yet — the compose files at the root are the old skeleton and have
not been rewritten for this solution.

## Settings that have to be supplied

_(none yet — this table is filled in as configuration is added)_

| Setting | Service | Purpose |
|---|---|---|
| | | |

No credentials are baked into any image or committed to this repository. `.env` currently holds only
`COMPOSE_PROJECT_NAME`.

## The sample material

`Task/fixtures/sample-material.zip` unpacks to `sample-material/`, which contains 12 top-level entries:
9 files (including a zero-byte file, Cyrillic filenames, and a name with spaces), two folders with
files, one folder with a nested subfolder whose contents must be skipped, and one empty folder.

On Linux, extract with `7z`, `bsdtar` or `python3 -m zipfile -e sample-material.zip .` — Info-ZIP
`unzip` 6.00 garbles the UTF-8 names.

## Documentation map

- [`TASK.md`](TASK.md) — the original brief (authoritative)
- [`AGENTS.md`](AGENTS.md) — layout, where the rules live, conventions, how to add an operation
- [`DECISIONS.md`](DECISIONS.md) — key decisions, what is missing, AI notes, time log
- [`docs/upload-requirements.md`](docs/upload-requirements.md) — the upload rules in plain terms
