# Document Archive

A small document archive: documents live in file cabinets, are described by typed index fields, and are
uploaded as folders of material and exported back out as zip archives.

**Current status: the Archive stores uploads durably and reads them back, so data survives a
restart. It is run as a container. There is no date-range query yet, no Export service, no web
client, and the image has never been built because Docker was unavailable during development.**
See [`DECISIONS.md`](DECISIONS.md) for exactly what is built and what is missing.

## What this will contain

| Piece | Technology | Status |
|---|---|---|
| Archive service | ASP.NET Core 9 | Field rules, upload, durable storage, paged read, `/health` |
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
├── Nemetschek.Archive.*.Tests/           one test project per production project, offline
└── Nemetschek.Archive.TestSupport/       shared test helpers
fixtures/                            cabinets.json and sample-material.zip (given)
```

## Build, run, test

**The service runs as a container.** That is the supported way to run it, and the only way that
stores documents somewhere they survive a restart.

```bash
docker compose up --build           # the Archive on http://localhost:8080
docker compose logs -f archive-api
docker compose down                 # keeps the data; `down -v` deletes it
```

Tests need no containers and run straight from the repository. Requires the .NET 9 SDK (pinned in
`global.json`; 9.0.203 or a later 9.0.x feature band):

```bash
dotnet build                        # warnings are errors
dotnet test                         # offline, no containers — 65 tests
```

`dotnet run` deliberately has no built-in configuration: both paths below are required and have no
defaults, so running from source means supplying them. This is so a missing setting can never
resolve to a path inside the image and lose data silently.

```bash
Archive__CabinetsFile=$PWD/fixtures/cabinets.json \
Archive__StoragePath=$PWD/.archive-data \
  dotnet run --project Nemetschek.Archive/Nemetschek.Archive.Api --no-launch-profile
```

With the service running:

| Path | Purpose |
|---|---|
| `POST /cabinets/{cabinetName}/uploads` | Upload a folder of material. `200`, or `404` if anything failed. See below. |
| `GET /cabinets/{cabinetName}/documents` | A page of stored documents, oldest first. `?page=1&size=20`, capped by `Archive:MaxPageSize`. `404` for an unknown cabinet. |
| `/swagger` | Browsable OpenAPI UI |
| `/openapi/v1.json` | OpenAPI description |
| `/health` | Liveness — is the process up. Runs no checks. |
| `/health/ready` | Readiness — can it serve requests. This is what a dependent service waits on. |

## Uploading a folder

`POST /cabinets/{cabinetName}/uploads`, as `multipart/form-data`:

| Part | Repeated | Meaning |
|---|---|---|
| `field` | no | The index field that receives the folder's name. Must be a **writable text** field of that cabinet. |
| `folderName` | no | The name of the uploaded folder. Written into `field` on every document in the upload. |
| `files` | yes | One part per file. Its `filename` is the file's path **relative to the uploaded folder** — `invoice-001.txt`, or `delivery-2026-03/page1.txt`. |
| `folders` | yes | Relative path of a folder that holds no file. A folder containing files is implied by them and does not need listing. |

The `filename` is what carries the structure, and it is relative to the *inside* of the uploaded
folder — `invoice-001.txt`, not `sample-material/invoice-001.txt`. The root folder's own name
travels in `folderName`, because that is the value written into the index field.

```bash
S=fixtures/sample-material/sample-material
curl -X POST http://localhost:5287/cabinets/Invoices/uploads \
  -F "field=Batch" \
  -F "folderName=sample-material" \
  -F "files=@$S/invoice-001.txt;filename=invoice-001.txt" \
  -F "files=@$S/delivery-2026-03/page1.txt;filename=delivery-2026-03/page1.txt" \
  -F "folders=empty-folder"
```

> **`;filename=` is not optional.** Without it curl sends only the base name, so
> `delivery-2026-03/page1.txt` arrives as `page1.txt` and becomes its own top-level document
> instead of a part — a silent difference in the result, not an error. On Windows, use a script
> rather than curl for the Cyrillic fixtures: the shell mangles the argument before curl sees it.

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

Do not set `Content-Type` by hand — the browser has to add the boundary. `webkitRelativePath`'s
first segment is the picked folder's name, which is why it is both the source of `folderName` and
stripped from each file's path.

The response is **always the same shape**, whatever happened: a report with **one row per entry the
server saw**, each `Stored`, `Skipped` or `Failed`, ordered by entry name. Only the status code
differs — `200 OK` when nothing failed, `404 Not Found` when at least one row is `Failed`:

```json
{
  "cabinet": "Invoices", "field": "Batch", "folderName": "sample-material",
  "results": [
    { "entry": "delivery-2026-03", "outcome": "Stored",
      "documentId": "c4c78b83-…", "parts": ["page1.txt", "page10.txt", "page2.txt"], "reason": null },
    { "entry": "delivery-2026-03/archive/old-page.txt", "outcome": "Skipped",
      "documentId": null, "parts": [], "reason": "'delivery-2026-03/archive/old-page.txt' is nested more than one folder deep. …" }
  ]
}
```

A request the rules turn away before anything is stored — an unknown cabinet, a field that may not be
chosen, a folder name that does not fit — comes back as the same report with a single `Failed` row
whose `reason` is the server's own sentence, and a `404`. A client must never restate those rules; it
renders what the server said.

> **`404` is deliberate here and is a known compromise.** It is correct for an unknown cabinet and
> wrong for a partial failure, where some documents *were* created. Read the body, not the status:
> `EnsureSuccessStatusCode`-style clients will throw and may discard the report. Recorded in
> [`DECISIONS.md`](DECISIONS.md) under "Cut, knowingly".

Three notes a caller has to know:

- **Empty folders have to be named in `folders`.** A folder with no file in it is invisible in a
  multipart body. A browser's directory picker cannot see empty folders at all, so that rule is only
  exercisable from a client that enumerates directories itself.
- **Parts of a folder-document are ordered ordinally** (`StringComparer.Ordinal`), so digits sort
  as characters: `page1.txt`, `page10.txt`, `page2.txt`. Not what a human would pick, but stable
  and identical on every machine. The report lists the part names in order, so a client never has
  to guess.
- **Model validation is switched off** (`SuppressModelStateInvalidFilter`). A blank `field` or
  `folderName` is refused by the rules and reported by name, rather than coming back as an
  automatic `400` that would be the one status the middleware does not produce.

## Containers

```bash
docker compose up --build
```

The Archive is then on `http://localhost:8080` (change with `ARCHIVE_PORT` in `.env`).
`./fixtures` is mounted read-only at `/app/fixtures`, so `cabinets.json` is **not** baked into the
image and the same image runs with a different cabinet set.

### Data that survives a restart

Documents live in the named volume `archive-data`, mounted at `/var/lib/archive`. **`docker compose
down` destroys containers but keeps named volumes** — only `docker compose down -v` removes them —
so this is what the acceptance criterion tests:

```bash
docker compose up -d
# upload the sample material, then:
curl -s "http://localhost:8080/cabinets/Invoices/documents?size=100" | jq '.total'   # 12

docker compose down
docker compose up -d
curl -s "http://localhost:8080/cabinets/Invoices/documents?size=100" | jq '.total'   # still 12
```

`archive-data` is a **named volume**, not a bind mount, so it is not `.archive-data` in this
repository — those are two independent archives. Documents uploaded by `dotnet run` land in
`.archive-data` and are invisible to the container; documents uploaded to the container land in
the volume and are invisible locally. Seeing zero documents right after `docker compose up` when
you uploaded locally is that, not a bug.

The layout inside the volume is one directory per document, so it can be inspected directly:

```bash
docker compose exec archive-api ls -R /var/lib/archive/cabinets
# cabinets/Invoices/2026-10-29/20261029T083022485Z_01a186c8-…/
#   document.json        title, index values, StoredAt, part list
#   parts/000_7697…bin   content, named by server-generated id
```

This survives a container restart because a volume is host filesystem. It is **not** proof against
a host power loss, which would need `fsync` per file; the brief does not ask for that.

**The `docker` commands themselves are not yet verified** — Docker is unavailable on the machine
this was developed on, so the image has never been built. Everything the container depends on was
verified by running the published service directly with exactly the environment variables
`docker-compose.yml` sets: all four `Archive__*` names bind (checked by setting
`Archive__MaxPageSize=5` and seeing a page of 5), the service refuses to start when either
required path is absent, and documents survive stopping and restarting the process.

What to check on the first real `docker compose up`, in this order — these are the steps that
cannot be exercised without Docker:

1. The image builds. `global.json` pins SDK 9.0.203 with `rollForward: latestFeature`, so the
   `sdk:9.0` tag must carry 9.0.2xx or later.
2. The first upload succeeds. If it fails with a permission error, the volume root is owned by
   root — check the `mkdir`/`chown` in the Dockerfile ran before `USER $APP_UID`.
3. `/health/ready` goes healthy; the compose healthcheck needs the `curl` installed in the image.
4. `docker compose down && docker compose up -d`, then re-read the documents.

## Settings that have to be supplied

| Setting | Service | Purpose |
|---|---|---|
| `Archive:CabinetsFile` | Archive | **Required.** Path to `cabinets.json`. Absolute, or relative to the service's content root. The service refuses to start without it, and refuses to start if the file is missing or unusable. |
| `Archive:StoragePath` | Archive | **Required.** Directory the Archive keeps documents in. **It must be on a mounted volume, or data will not survive a restart.** The service refuses to start without it, or if the directory cannot be created. |
| `Archive:MaxUploadBytes` | Archive | Largest upload body accepted, in bytes. Default 268435456 (256 MiB). |
| `Archive:MaxUploadEntries` | Archive | Most parts accepted in one multipart body. Default 10000. |
| `Archive:MaxPageSize` | Archive | Most documents one page may hold. Default 50; the review demo runs with 5. |

Every setting can be supplied as an environment variable using ASP.NET Core's double-underscore form,
so the same image runs with different settings:

```bash
Archive__CabinetsFile=/app/fixtures/cabinets.json
Archive__StoragePath=/var/lib/archive
```

`docker-compose.yml` supplies every one of these, with the tunable ones defaulted in `.env`:

| `.env` variable | Sets |
|---|---|
| `ARCHIVE_PORT` | The published host port |
| `ARCHIVE_MAX_PAGE_SIZE` | `Archive:MaxPageSize` — **set this to 5 for the review demo** |
| `ARCHIVE_MAX_UPLOAD_BYTES` | `Archive:MaxUploadBytes` |
| `ARCHIVE_MAX_UPLOAD_ENTRIES` | `Archive:MaxUploadEntries` |

Neither path is defaulted in `appsettings.Development.json`, on purpose: a relative path there
would resolve against the **content root** — `/app` inside the image — so a deployment that forgot
to mount a volume would store documents in the container's own layer and lose them at the next
restart, without a word. Required-and-absent fails at startup instead, naming the setting.

**Both resolved paths are logged at startup**, so there is never a need to work out where the data
went:

```
Loaded cabinet definitions from /app/fixtures/cabinets.json
Storing documents under /var/lib/archive
```

No credentials are baked into any image or committed to this repository. `.env` currently holds only
`COMPOSE_PROJECT_NAME`.

## The sample material

`fixtures/sample-material.zip` is unpacked at `fixtures/sample-material/sample-material/` (the zip
carries its own top-level folder). It contains 12 top-level entries: 9 files (including a zero-byte
file, Cyrillic filenames, and a name with spaces), two folders with files, one folder with a nested
subfolder whose contents must be skipped, and one empty folder.

On Linux, extract with `7z`, `bsdtar` or `python3 -m zipfile -e sample-material.zip .` — Info-ZIP
`unzip` 6.00 garbles the UTF-8 names.

## Documentation map

- [`TASK.md`](TASK.md) — the original brief (authoritative)
- [`AGENTS.md`](AGENTS.md) — layout, where the rules live, conventions, how to add an operation
- [`DECISIONS.md`](DECISIONS.md) — key decisions, what is missing, AI notes, time log
- [`docs/upload-requirements.md`](docs/upload-requirements.md) — the upload rules in plain terms
