# Document Archive

Documents live in file cabinets, are described by typed index fields, and are uploaded as folders of
material and exported back out as zip archives. Two ASP.NET Core 9 services in containers: the
**Archive** owns the rules and the storage, the **Export** reaches it over HTTP and never touches its
storage.

**Status: `compose up` brings up both services and the round trip works end to end** — the sample
material uploaded twice, exported, unpacked, and **15 of its 16 entries byte-identical** to the
original folder, the one difference being the nested file the upload rules skip by design. Verified
on Podman 6.1.4. **The web client is not started**; both services have browsable OpenAPI, which the
brief accepts as the fallback demo. [`DECISIONS.md`](DECISIONS.md) lists every gap.

## Layout

```
Nemetschek.sln                            one solution for every service
Nemetschek.Archive/
├── Nemetschek.Archive.Api/               HTTP surface, OpenAPI
├── Nemetschek.Archive.Core/              the rules, no external dependencies
├── Nemetschek.Archive.Infrastructure/    adapters for storage and fixtures
└── Nemetschek.Archive.*.Tests/           one test project per production project, offline
Nemetschek.Export/                        the same six projects, same shape
fixtures/                                 cabinets.json and sample-material.zip (given)
```

## Build, run, test

**The service runs as a container.** That is the supported way to run it, and the only way that
stores documents somewhere they survive a restart.

```bash
docker compose up --build -d        # the Archive on :8080, the Export on :8081
docker compose logs -f archive-api
docker compose down                 # keeps the data; `down -v` deletes it
```

`up --build` is the whole command: it rebuilds the image from the current source and recreates the
container, so there is no separate build step.

The compose file is plain Compose v2 with nothing engine-specific in it, and all the container work
here was done on **Podman 6.1.4** — use `podman compose`, which hands the file to the real Docker
Compose v2 binary. Not `podman-compose`, a separate Python program that cannot do `up --build` on
Windows at all; the two bugs behind that are in [`DECISIONS.md`](DECISIONS.md) under "Toolchain".

On an **SELinux-enforcing host** (Fedora, RHEL) the read-only `fixtures` mount needs a label —
`./fixtures:/app/fixtures:ro,z` — left out of the committed file because it is a no-op elsewhere and
was not testable here.

Tests need no containers and run straight from the repository, with the .NET 9 SDK pinned in
`global.json` (9.0.203 or a later 9.0.x feature band):

```bash
dotnet build                        # warnings are errors
dotnet test                         # offline, no containers — 225 tests
```

The suite is unit tests only — MSTest with Moq, every dependency mocked or pointed at a throwaway
temp directory, and **nothing boots a host**. That keeps `dotnet test` offline and fast, and it
means **the HTTP surface is not covered by a test**: the statuses, the error shapes and the page cap
are verified by running the pair and calling them.

## Running from source

Both services run with nothing supplied. Each `appsettings.json` lists every setting it reads as a
typed placeholder, and `appsettings.Development.json` fills in the two paths and the Archive's
address, so the launch profiles need no environment variables:

```bash
dotnet run --project Nemetschek.Archive/Nemetschek.Archive.Api   # http://localhost:5287
dotnet run --project Nemetschek.Export/Nemetschek.Export.Api     # http://localhost:5288
```

The Archive reads `fixtures/cabinets.json` and stores documents in `.archive-data/` at the repository
root, which is git-ignored. Delete it to start from an empty archive.

**Those defaults are Development-only on purpose.** In Production — what the containers run — the
required settings are blank, and blank is treated as missing, so startup fails naming the setting
rather than resolving to a path inside the image and losing data silently:

```bash
ASPNETCORE_ENVIRONMENT=Production dotnet run --no-launch-profile \
  --project Nemetschek.Archive/Nemetschek.Archive.Api
# Unhandled exception. System.InvalidOperationException: Setting 'Archive:CabinetsFile' is required
```

## The HTTP API

**The Archive**, on `http://localhost:8080`:

| Path | Purpose |
|---|---|
| `GET /cabinets` | Every cabinet with its index fields: each field's `name`, `label`, `type`, `maxLength`, `writable`, and `choosable` — whether a client may offer it as the upload field or the export marker field. |
| `POST /cabinets/{cabinetName}/uploads` | Upload a folder of material. `200` with a report; `400` if the chosen field breaks a rule; `404` for an unknown cabinet. |
| `GET /cabinets/{cabinetName}/documents` | A page of stored documents, oldest first. `?page=1&size=20&from=2026-03-01&to=2026-03-31`, capped by `Archive:MaxPageSize`. Dates are inclusive calendar days **in UTC**, filtered on `StoredAt`. |
| `GET /cabinets/{cabinetName}/documents/{id}/parts/{partId}/content` | One part's bytes, streamed. `404` if the document or part is not there. |
| `PATCH /cabinets/{cabinetName}/documents/index-values` | Set the same index values on one or many documents: `{"documentIds":["…"],"values":{"Batch":{"type":"Text","value":"…"}}}`. `400` if a value breaks a field rule, `404` if the cabinet does not hold one of the ids — in both cases nothing is written. |

**The Export**, on `http://localhost:8081`:

| Path | Purpose |
|---|---|
| `POST /exports` | Build a zip of a cabinet's documents and download it. `400` for a bad request or anything the Archive refused; `502` when the Archive is unreachable. |

Both services serve `/swagger` and `/openapi/v1.json`, where every endpoint and every path parameter
and JSON body field carries its own description. Two sets are blank there on .NET 9 — the four paging
values on `GET /documents` and the four multipart fields on `POST /uploads` — so the tables in this
file are where to read those; see "The OpenAPI descriptions" in [`DECISIONS.md`](DECISIONS.md).

## Uploading a folder

`POST /cabinets/{cabinetName}/uploads`, as `multipart/form-data`:

| Part | Repeated | Meaning |
|---|---|---|
| `field` | no | The index field that receives the folder's name. Must be a **writable text** field of that cabinet. |
| `folderName` | no | The name of the uploaded folder. Written into `field` on every document in the upload. |
| `files` | yes | One part per file. Its `filename` is the file's path **relative to the uploaded folder** — `invoice-001.txt`, or `delivery-2026-03/page1.txt`. |
| `folders` | yes | **Optional.** A folder the `files` do not already imply: one that holds no file, or a nested one you want reported as skipped under its own name. A folder containing files is implied by them. |

The `filename` carries the structure, and it is relative to the *inside* of the uploaded folder —
`invoice-001.txt`, not `sample-material/invoice-001.txt`. The root folder's own name travels in
`folderName`, because that is the value written into the index field.

```bash
S=fixtures/sample-material/sample-material
curl -X POST http://localhost:8080/cabinets/Invoices/uploads \
  -F "field=Batch" \
  -F "folderName=sample-material" \
  -F "files=@$S/invoice-001.txt;filename=invoice-001.txt" \
  -F "files=@$S/delivery-2026-03/page1.txt;filename=delivery-2026-03/page1.txt" \
  -F "folders=empty-folder"
```

> **`;filename=` is not optional.** Without it curl sends only the base name, so
> `delivery-2026-03/page1.txt` arrives as `page1.txt` and becomes its own top-level document instead
> of a part — a silent difference in the result, not an error. On Windows, use a script rather than
> curl for the Cyrillic fixtures: the shell mangles the argument before curl sees it.

From a browser, the directory picker already produces these paths:

```js
const data = new FormData();
data.append("field", "Batch");
data.append("folderName", picked[0].webkitRelativePath.split("/")[0]);
for (const file of picked) {
  data.append("files", file, file.webkitRelativePath.split("/").slice(1).join("/"));
}
await fetch(`/cabinets/${cabinet}/uploads`, { method: "POST", body: data });
```

Do not set `Content-Type` by hand — the browser has to add the boundary.

The answer is `200` with **one row per entry the server saw**, each `Stored`, `Skipped` or `Failed`,
ordered by entry name:

```json
{
  "cabinet": "Invoices", "field": "Batch", "folderName": "sample-material",
  "results": [
    { "entry": "delivery-2026-03", "outcome": "Stored",
      "documentId": "c4c78b83-…", "parts": ["page1.txt", "page10.txt", "page2.txt"], "reason": null },
    { "entry": "delivery-2026-03/archive/old-page.txt", "outcome": "Skipped",
      "documentId": null, "parts": [], "reason": "… is nested more than one folder deep." }
  ]
}
```

Four things a caller has to know:

- **A failed entry is not a failed request.** A `Failed` row still comes back inside a `200`, because
  the request itself was usable and other documents may have been stored. Read the body, not just the
  status. The service logs storage failures at `Error`, which is what makes them visible to an operator.
- **Empty folders have to be named in `folders`.** A folder with no file in it is invisible in a
  multipart body, and a browser's directory picker cannot see one at all.
- **Parts of a folder-document are ordered ordinally**, so digits sort as characters: `page1.txt`,
  `page10.txt`, `page2.txt`. Stable and identical on every machine. The report lists them in order.
- **A missing `field` or `folderName` comes back in a different `400` body** from a broken rule:
  ASP.NET's `ValidationProblemDetails` with the message under `errors`, rather than the middleware's
  `detail`. Both are `ProblemDetails` carrying a sentence written for a user, so a client that reads
  either key shows the server's message in both cases.

A request the rules turn away before anything is stored is `400` with the server's own sentence; an
unknown cabinet is `404`. **A client must never restate those rules** — it renders what the server said.

## Exporting

```bash
curl -X POST http://localhost:8081/exports -H "Content-Type: application/json" \
  -d '{"cabinet":"Invoices","markerField":"Batch","from":"2026-03-01","to":"2026-03-31"}' -o export.zip
```

All four values are required. The reply is `application/zip` as an attachment named
`<cabinet>-<from>-to-<to>.zip`.

What comes out: one part becomes a file named after the **document**, several parts become a folder
with one file per part, no parts becomes an empty folder, and a repeated title gains ` (2)` before
its extension. Every exported document then has its chosen field set to `Exported on yyyy-MM-dd` with
the UTC date — the marker **records** the export rather than filtering it, so running the same export
twice covers the same documents and overwrites their markers. The marker field must be a writable
text field with room for 22 characters.

> **Dates are UTC calendar days.** There is no time-zone parameter: the client and the services are
> taken to share a zone. A caller whose own day differs from UTC's sees UTC's boundaries — see
> "Removed: the time zone parameter" in [`DECISIONS.md`](DECISIONS.md) for what that costs.

> **The archive is built before it is sent**, into a temp file that deletes itself, so a failure is a
> `400` or `502` with nothing delivered rather than a `200` holding a truncated zip. Memory is never
> the limit: part content streams from the Archive into the zip entry.

## Settings

**No setting has a code default.** Every one is read as `Configuration["Section:Key"] ?? throw`, so an
absent key stops startup naming itself. Each service's `appsettings.json` is the full list;
`appsettings.Development.json` fills the blanks for running from source; `docker-compose.yml` supplies
every key explicitly, so the containers depend on neither file.

| Setting | Service | Purpose |
|---|---|---|
| `Archive:CabinetsFile` | Archive | Path to `cabinets.json`. Absolute, or relative to the content root. Refuses to start without it, or if the file is unusable. |
| `Archive:StoragePath` | Archive | Directory the Archive keeps documents in. **Must be on a mounted volume, or data will not survive a restart.** |
| `Archive:MaxUploadBytes` | Archive | Largest upload body, in bytes. 268435456 (256 MiB). |
| `Archive:MaxUploadEntries` | Archive | Most parts in one multipart body. 10000. |
| `Archive:MaxPageSize` | Archive | Most documents one page may hold. 50; the review demo runs with 5. |
| `Archive:AllowedOrigins` | Archive | Origins a browser client may call from, comma-separated. **Empty means any origin**, which is what the demo wants: nothing is authenticated and no cookie is read, so there is no credential for a hostile page to borrow. |
| `Export:AllowedOrigins` | Export | The same. The Export also exposes `Content-Disposition`, so a cross-origin client can read the zip's name instead of inventing one. |
| `Export:ArchiveBaseAddress` | Export | The Archive's base address, such as `http://archive-api:8080`. Refuses to start without it, or on an address that is not absolute. |

Every setting can be supplied as an environment variable in ASP.NET Core's double-underscore form, so
the same image runs with different settings:

```bash
Archive__CabinetsFile=/app/fixtures/cabinets.json
Archive__StoragePath=/var/lib/archive
```

`docker-compose.yml` supplies every one literally — there is no `.env` and no override file, so one
file is the whole deployment and what runs is what you read.

**`name: nemetschek-archive` at the top of that file is load-bearing.** It prefixes the volume
(`nemetschek-archive_archive-data`); without it Compose names the project after the directory and the
Archive comes up pointing at a different, empty volume.

Both resolved paths are logged at startup, so there is never a need to work out where the data went:

```
Loaded cabinet definitions from /app/fixtures/cabinets.json
Storing documents under /var/lib/archive
```

No credentials are baked into any image or committed to this repository.

## Data that survives a restart

Documents live in the named volume `archive-data`, mounted at `/var/lib/archive`. **`down` destroys
containers but keeps named volumes** — only `down -v` removes them — so this is the acceptance test:

```bash
docker compose up -d
# upload the sample material, then:
curl -s "http://localhost:8080/cabinets/Invoices/documents?size=100" | jq '.total'   # 12
docker compose down && docker compose up -d
curl -s "http://localhost:8080/cabinets/Invoices/documents?size=100" | jq '.total'   # still 12
```

`archive-data` is a **named volume, not** `.archive-data` in this repository — those are two
independent archives. Documents uploaded by `dotnet run` are invisible to the container and vice
versa. Seeing zero documents right after `compose up` when you uploaded locally is that, not a bug.

The layout inside is one directory per document:

```bash
docker compose exec archive-api ls -R /var/lib/archive/cabinets
# cabinets/Invoices/2026-10-29/20261029T083022485Z_01a186c8-…/
#   document.json        title, index values, StoredAt, part list
#   parts/000_7697…bin   content, named by server-generated id
```

**There is no Windows folder holding the documents** — a named volume lives inside the engine's Linux
VM, and the rootful store is unreadable through `\\wsl$`. Three ways in that do work, and the full
explanation is in [`DECISIONS.md`](DECISIONS.md):

```bash
podman exec <container> ls -R /var/lib/archive/cabinets      # through the container
podman machine ssh --username root 'ls -R <mountpoint>'      # through the VM, as root
podman cp <container>:/var/lib/archive ./archive-dump        # copy it out
```

This survives a container restart because a volume is host filesystem. It is **not** proof against a
host power loss, which would need `fsync` per file; the brief does not ask for that.

**Not verified: the `docker` CLI itself**, because no Docker daemon was available. Nothing in the
compose file is Podman-specific and `podman compose` hands it to the Docker Compose v2 binary, so
"runs under Docker" is an inference, not an observation.

## What the container log holds

Everything goes to stdout. Four kinds of line, and nothing else:

| Line | When |
|---|---|
| The two resolved paths above | Once, at startup |
| `Request and Response: … Path … StatusCode … Duration` | One per request, after it is answered |
| `Stored <id> in <cabinet> with <n> part(s)` | One per document an upload actually stored |
| `400 The upload was refused: <reason>` | A refusal, with the sentence the caller was given |

A storage failure is logged at `Error` with its exception — the only place a volume that has stopped
accepting writes shows up, since the upload report turns a failed document into a `Failed` row and
still answers `200`.

**Bodies and headers are never logged**: an upload body is untrusted and may be 256 MiB, and a header
is where a credential would appear once there is one.

## The round trip

The brief's demo: upload the sample material twice, export it, unpack, and account for every
difference. Run on 2026-10-30 against both containers.

**Result: 24 documents in, 30 entries out, and 15 of the original folder's 16 entries byte-identical
by SHA-256.** The one difference is a rule: `delivery-2026-03/archive/old-page.txt` is absent,
because it is nested more than one folder deep so the **upload** never stored it — the report said
`Skipped` at the time, and the export can only hold what the Archive holds.

Everything else matched, including the cases the fixtures exist to break:

- `Договор София.txt` and `Фактури Q1/фактура-1.txt` — Cyrillic names, unchanged
- `readme with spaces.txt` — spaces, unchanged
- `empty-note.txt` — zero bytes, still zero bytes, and still a *file*
- `empty-folder/` — no parts, still an empty folder, and told apart from the zero-byte file
- `delivery-2026-03/` — three parts in `page1, page10, page2` order, the Archive's ordinal order
- the second upload's twelve documents came out as ` (2)`

Afterwards all 24 exported documents carried `Batch = Exported on 2026-10-30`, and the 11 documents
stored the previous day — outside the range — were neither exported nor marked.

## The sample material

`fixtures/sample-material.zip` unpacks at `fixtures/sample-material/sample-material/` (the zip carries
its own top-level folder). 12 top-level entries: 9 files (including a zero-byte file, Cyrillic
filenames, and a name with spaces), two folders with files, one folder with a nested subfolder whose
contents must be skipped, and one empty folder.

On Linux, extract with `7z`, `bsdtar` or `python3 -m zipfile -e sample-material.zip .` — Info-ZIP
`unzip` 6.00 garbles the UTF-8 names.

## Documentation map

- [`TASK.md`](TASK.md) — the original brief (authoritative)
- [`AGENTS.md`](AGENTS.md) — layout, where the rules live, conventions, how to add an operation
- [`DECISIONS.md`](DECISIONS.md) — decisions, what is missing, AI notes, time log, and the upload and
  export rules in plain terms
- [`CLAUDE.md`](CLAUDE.md) — the standing instruction for sessions in this folder
