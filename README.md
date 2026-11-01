# Document Archive

A small document archive: documents live in file cabinets, are described by typed index fields, and are
uploaded as folders of material and exported back out as zip archives.

**Current status: `compose up` brings up both services and the round trip works end to end** —
the sample material uploaded twice, exported as a zip, unpacked, and **15 of its 16 entries
byte-identical to the original folder**, the one difference being the nested file the upload rules
skip by design. Verified on Podman 6.1.4 via `podman compose`. Still missing: `GET /cabinets` so a
client can list fields, a CORS policy, and the web client itself.
See [`DECISIONS.md`](DECISIONS.md) for exactly what is built and what is missing.

## What this will contain

| Piece | Technology | Status |
|---|---|---|
| Archive service | ASP.NET Core 9 | Field rules, upload, durable storage, paged read |
| Export service | ASP.NET Core 9 | Export rules, zip layout, marker write-back |
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
└── Nemetschek.Archive.*.Tests/           one test project per production project, offline
Nemetschek.Export/                        the same six projects, same shape
fixtures/                            cabinets.json and sample-material.zip (given)
```

## Build, run, test

**The service runs as a container.** That is the supported way to run it, and the only way that
stores documents somewhere they survive a restart.

```bash
docker compose up --build -d        # the Archive on :8080, the Export service on :8081
docker compose logs -f archive-api
docker compose down                 # keeps the data; `down -v` deletes it
```

`docker compose up --build` is the whole command: it rebuilds the image from the current source and
recreates the container, so there is no separate build step to remember.

The compose file is plain Compose v2 with nothing engine-specific in it, so Podman runs it too, and
all the container work recorded below was in fact done on Podman 6.1.4:

```bash
podman compose up --build -d        # same single command; no workaround needed
podman compose logs -f archive-api
podman compose down
```

**`podman compose`, not `podman-compose`.** The two are different programs: `podman compose` hands
the file to the real Docker Compose v2 binary, which builds and recreates correctly, while
`podman-compose` is a separate Python reimplementation that cannot do `up --build` on Windows at
all. Reach for `podman compose` and the collapsed section below is irrelevant.

<details>
<summary>If you are stuck on <code>podman-compose</code> (the Python one)</summary>

```bash
podman build -f Nemetschek.Archive/Nemetschek.Archive.Api/Dockerfile -t nemetschek-archive-api .
podman-compose up -d --no-build
podman-compose logs
podman-compose down
```

**On Windows with Podman 6 the image has to be built separately**, because no released
`podman-compose` handles `up --build` there — two bugs straddle the versions:

| Version | `podman ps` under Podman 6 | Build from a Windows absolute path |
|---|---|---|
| ≤ 1.4.1 | **broken** | works |
| ≥ 1.5.0 | works | **broken** |

On **≤ 1.4.1**, `up` dies before starting anything with `subprocess.CalledProcessError … podman ps
… returned non-zero exit status 125`. It asks for
`--format '{{ index .Labels "io.podman.compose.config-hash"}}'`, and Podman 6 changed `ps`'s
`.Labels` from a map to a slice, so the template fails with *"cannot index slice/array with type
string"*. Fixed in 1.5.0, which reads `--format json` instead.

On **≥ 1.5.0**, `up --build` fails with *"no Containerfile or Dockerfile specified or found in
context directory"* even though the `dockerfile:` key is correct. `is_context_git_url()` runs
`urllib.parse.urlparse` on the already-absolutised context, and `C:\Users\…` parses as scheme `c`
with an empty netloc — which that function reads as a git URL, so it never adds `-f` and
`podman build` is never told about the Dockerfile.

Pre-building sidesteps the second bug: the image is already tagged, so `--no-build` is all compose
needs. Both are podman-compose bugs on Windows, not problems with the compose file or the
Dockerfile — `podman build` by hand succeeds from the identical context, and the same file builds
and runs attached under Docker.

Older podman-compose releases also crash on a foreground `up` on Windows, with `NotImplementedError`
from `asyncio.loop.add_signal_handler`; `-d` avoids it, and 1.6.0 guards the call with
`if sys.platform != 'win32'`. One such crash left the project's named volume recreated and empty.
It did not reproduce, so the cause is unconfirmed — but it is another reason to take the round-trip
demo from a `down` / `up -d` cycle, which **is** verified to preserve the data.

</details>

On an **SELinux-enforcing host** (Fedora, RHEL) the read-only `fixtures` bind mount needs a label
or the container cannot read `cabinets.json`. Both engines accept it:

```yaml
- ./fixtures:/app/fixtures:ro,z
```

It is left out of the committed file because it is a no-op everywhere else and was not testable here.

<details>
<summary>If <code>podman compose</code> cannot connect on Windows</summary>

`docker-compose.exe` reaches Podman through a Windows named pipe, `\.\pipe\<machine-name>`,
published by `win-sshproxy.exe`, which `podman machine start` launches. If that proxy is not
running — a WSL or host restart will do it — every compose command fails with *"open
//./pipe/podman-machine-default: The system cannot find the file specified"*, even though
`podman info` works, because the CLI falls back to SSH.

```bash
podman machine list                     # which machine is running
podman system connection list           # which one is the default
tasklist | grep win-sshproxy            # the proxy that publishes the pipe
```

Restarting the machine normally republishes it. If it does not, `podman-compose` avoids the
problem entirely: it drives the `podman` CLI and needs no pipe.

</details>

Tests need no containers and run straight from the repository. Requires the .NET 9 SDK (pinned in
`global.json`; 9.0.203 or a later 9.0.x feature band):

```bash
dotnet build                        # warnings are errors
dotnet test                         # offline, no containers — 449 tests
dotnet test --collect:"XPlat Code Coverage"   # 98.9% of lines
```

The endpoint tests boot the real service in process with `WebApplicationFactory`, on a throwaway
storage directory and the repository's own `fixtures/cabinets.json` — so `dotnet test` covers the
two routes, their statuses and the page cap without a container or a network.

`dotnet run` deliberately has no built-in configuration: both paths below are required and have no
defaults, so running from source means supplying them. This is so a missing setting can never
resolve to a path inside the image and lose data silently.

```bash
Archive__CabinetsFile=$PWD/fixtures/cabinets.json \
Archive__StoragePath=$PWD/.archive-data \
  dotnet run --project Nemetschek.Archive/Nemetschek.Archive.Api --no-launch-profile
```

**The Archive**, on `http://localhost:8080`:

| Path | Purpose |
|---|---|
| `GET /cabinets` | Every file cabinet with its index fields: each field's `name`, `label`, `type`, `maxLength`, `writable`, and `choosable` — whether a client may offer it as the upload field or the export marker field. Answers `{"cabinets":[{"name":"Invoices","displayName":"Invoices","fields":[…]}]}`. |
| `POST /cabinets/{cabinetName}/uploads` | Upload a folder of material. `200` with a report; `400` if the chosen field breaks a rule; `404` for an unknown cabinet. See below. |
| `GET /cabinets/{cabinetName}/documents` | A page of stored documents, oldest first. `?page=1&size=20&from=2026-03-01&to=2026-03-31`, capped by `Archive:MaxPageSize`. The dates are inclusive calendar dates **in UTC**, filtered on `StoredAt`. |
| `GET /cabinets/{cabinetName}/documents/{id}/parts/{partId}/content` | One part's bytes, streamed. `404` if the document or the part is not there. |
| `PATCH /cabinets/{cabinetName}/documents/index-values` | Set the same index values on one or many documents. `{"documentIds":["…"],"values":{"Batch":{"type":"Text","value":"…"}}}`. `400` if a value breaks a field rule, `404` if the cabinet does not hold one of the ids — in both cases nothing is written. Answers `{"cabinet":"…","updated":24}`. |
| `/swagger`, `/openapi/v1.json` | Browsable OpenAPI |

**The Export service**, on `http://localhost:8081`:

| Path | Purpose |
|---|---|
| `POST /exports` | Build a zip of a cabinet's documents and download it. Body below. `400` for a bad request or anything the Archive refused; `502` when the Archive is unreachable. |
| `/swagger`, `/openapi/v1.json` | Browsable OpenAPI |

## Exporting

```bash
curl -X POST http://localhost:8081/exports   -H "Content-Type: application/json"   -d '{"cabinet":"Invoices","markerField":"Batch","from":"2026-03-01","to":"2026-03-31"}'   -o export.zip
```

All four values are required. The reply is `application/zip` as an attachment named
`<cabinet>-<from>-to-<to>.zip`.

> **Dates are UTC calendar days.** There is no time-zone parameter: the client and the services are
> taken to share a zone, so `from` and `to` are read as UTC days and the marker carries the UTC
> date. A caller whose own day differs from UTC's sees UTC's boundaries — see "Removed: the time
> zone parameter" in [`DECISIONS.md`](DECISIONS.md) for what that costs.

What comes out, and why, is in [`DECISIONS.md`](DECISIONS.md). In
short: one part becomes a file named after the **document**, several parts become a folder with one
file per part, no parts becomes an empty folder, and a repeated title gains ` (2)` before its
extension. Every exported document then has its chosen field set to `Exported on yyyy-MM-dd`, with
the UTC date — the marker **records** the export rather than filtering it, so running the same
export twice covers the same documents and overwrites their markers.

> **The archive is built before it is sent.** It goes into a temp file that deletes itself, so a
> failure is a `400` or `502` with nothing delivered rather than a `200` holding a truncated zip.
> The caller waits for the build; memory is never the limit, because part content is streamed from
> the Archive into the zip entry.

## Uploading a folder

`POST /cabinets/{cabinetName}/uploads`, as `multipart/form-data`:

| Part | Repeated | Meaning |
|---|---|---|
| `field` | no | The index field that receives the folder's name. Must be a **writable text** field of that cabinet. |
| `folderName` | no | The name of the uploaded folder. Written into `field` on every document in the upload. |
| `files` | yes | One part per file. Its `filename` is the file's path **relative to the uploaded folder** — `invoice-001.txt`, or `delivery-2026-03/page1.txt`. |
| `folders` | yes | **Optional.** Relative path of a folder the `files` do not already imply: one that holds no file, or a nested one you want reported as skipped under its own name. A folder containing files is implied by them — listing it changes nothing. |

The `filename` is what carries the structure, and it is relative to the *inside* of the uploaded
folder — `invoice-001.txt`, not `sample-material/invoice-001.txt`. The root folder's own name
travels in `folderName`, because that is the value written into the index field.

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

When the upload runs, the answer is `200` with a report holding **one row per entry the server
saw** — each `Stored`, `Skipped` or `Failed`, ordered by entry name. A document that failed to
store does not change the status: the upload ran, and the report says what reached the Archive.

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

A request the rules turn away before anything is stored — a field that may not be chosen, a folder
name that does not fit — comes back as `ProblemDetails` with `400` and the server's own sentence in
`detail`; an unknown cabinet is `404`. A client must never restate those rules; it renders what the
server said.

> **A failure of a single entry is not a failure of the request.** A `Failed` row still comes back
> inside a `200` report, because the request itself was usable and other documents may have been
> stored. Read the body, not just the status — and note that the service logs its own storage
> failures at `Error`, which is what makes them visible to an operator. See "What the container log
> holds" below.

Three notes a caller has to know:

- **Empty folders have to be named in `folders`.** A folder with no file in it is invisible in a
  multipart body. A browser's directory picker cannot see empty folders at all, so that rule is only
  exercisable from a client that enumerates directories itself.
- **Parts of a folder-document are ordered ordinally** (`StringComparer.Ordinal`), so digits sort
  as characters: `page1.txt`, `page10.txt`, `page2.txt`. Not what a human would pick, but stable
  and identical on every machine. The report lists the part names in order, so a client never has
  to guess.
- **A missing `field` or `folderName` comes back in a different `400` body** from a broken rule:
  ASP.NET's automatic `ValidationProblemDetails`, with the message under `errors`, rather than the
  middleware's `detail`. Both are `ProblemDetails` and both carry a sentence written for a user, so
  a client that reads either key shows the server's message in both cases.

## Containers

```bash
docker compose up --build
```

The Archive is then on `http://localhost:8080` (change the published port in `docker-compose.yml`).
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

### Where that actually is on disk

`/var/lib/archive` in the container is the named volume, and the volume's real mountpoint is on the
engine's host — which on Windows is the Linux VM, not Windows itself:

```bash
podman volume inspect nemetschek-archive_archive-data --format '{{.Mountpoint}}'
# /var/lib/containers/storage/volumes/nemetschek-archive_archive-data/_data
```

That is the **rootful** store, because the default connection is `podman-machine-default-root`; a
rootless connection puts it under `/home/user/.local/share/containers/storage/volumes/…` instead.
On Windows the whole VM filesystem is one virtual disk:

```
C:\Users\qzr\.local\share\containers\podman\machine\wsl\wsldist\podman-machine-default\ext4.vhdx
```

**There is no Windows folder holding the documents as files**, and `\\wsl$\podman-machine-default\…`
cannot reach the rootful store either — `volumes/` is `drwx------ root` while `\\wsl$` connects as
uid 1000, so Explorer gets *permission denied*. Three ways in that do work:

```bash
podman exec <container> ls -R /var/lib/archive/cabinets            # through the container
podman machine ssh --username root 'ls -R <mountpoint>'            # through the VM, as root
podman cp <container>:/var/lib/archive ./archive-dump              # copy it out to Windows
```

Under Docker Desktop on Windows the same reasoning applies — the volume lives inside the
`docker-desktop` WSL VM, not in a Windows directory. `docker volume inspect` names the mountpoint.

**Verified end to end on Podman 6.1.4** (WSL backend). The acceptance criterion passes through
compose itself:

```
compose up --build -d  ->  container up, the service answering
upload sample material ->  200, entries Stored and Skipped
compose down           ->  container removed, named volume kept
compose up -d          ->  same documents, same parts, same order
```

Also checked there:

- The container runs as `uid=1654(app)` and the named volume is owned by `app`, so the Dockerfile's
  `mkdir`/`chown` before `USER $APP_UID` is doing its job.
- All five `Archive__*` variables bind — confirmed by setting `Archive__MaxPageSize=5` and getting
  a page of 5 — and the service refuses to start when either required path is absent.
- The refusal statuses: `404` unknown cabinet, `400` read-only field, `400` folder name too long.

**Not verified: the `docker` CLI itself**, because no Docker daemon was available at the time. The
file is exercised by Docker's own tooling though — `podman compose` hands it to the Docker Compose
v2 binary, which parses and interpolates it cleanly (`docker-compose config` renders the full
resolved service) — and nothing in it is Podman-specific. The one thing a Docker user on an
SELinux host would need is the `,z` label noted above.

Podman was removed and reinstalled on 2026-10-29 while chasing the `podman-compose` bugs above, so
the machine, its images and its volumes are newer than this document's earlier runs — the documents
in the volume today are from an upload after that reinstall, not the original twelve.

## Settings that have to be supplied

| Setting | Service | Purpose |
|---|---|---|
| `Archive:CabinetsFile` | Archive | **Required.** Path to `cabinets.json`. Absolute, or relative to the service's content root. The service refuses to start without it, and refuses to start if the file is missing or unusable. |
| `Archive:StoragePath` | Archive | **Required.** Directory the Archive keeps documents in. **It must be on a mounted volume, or data will not survive a restart.** The service refuses to start without it, or if the directory cannot be created. |
| `Archive:MaxUploadBytes` | Archive | Largest upload body accepted, in bytes. Default 268435456 (256 MiB). |
| `Archive:MaxUploadEntries` | Archive | Most parts accepted in one multipart body. Default 10000. |
| `Archive:MaxPageSize` | Archive | Most documents one page may hold. Default 50; the review demo runs with 5. |
| `Archive:AllowedOrigins` | Archive | Origins a browser client may call from, comma-separated — `http://localhost:5173,https://client.example`. **Unset means any origin**, which is what the demo wants: nothing here is authenticated and no cookie is read, so there is no credential for a hostile page to borrow. Set it to pin the client's host. |
| `Export:AllowedOrigins` | Export | The same, for the Export service. It also exposes `Content-Disposition`, so a cross-origin client can read the zip's file name off the response instead of inventing one. |
| `Export:ArchiveBaseAddress` | Export | **Required.** The Archive's HTTP base address, such as `http://archive-api:8080`. The service refuses to start without it, and refuses an address that is not absolute. There is no default on purpose: the Export reaches the Archive over HTTP and nothing else, so a wrong or missing address has to fail at startup rather than at the first export. |

Every setting can be supplied as an environment variable using ASP.NET Core's double-underscore form,
so the same image runs with different settings:

```bash
Archive__CabinetsFile=/app/fixtures/cabinets.json
Archive__StoragePath=/var/lib/archive
```

`docker-compose.yml` supplies every one of these, literally — there is no `.env` and no
`docker-compose.override.yml`, so one file is the whole deployment and what runs is what you read.
They are still environment variables, which is what lets the same images run with different ones:

```bash
# the review demo, without editing the file
docker compose run -e Archive__MaxPageSize=5 archive-api
```

**`name: nemetschek-archive` at the top of the file is load-bearing.** It prefixes the volume
(`nemetschek-archive_archive-data`); without it Compose names the project after the directory, and
the Archive would come up pointing at a different, empty volume.

`appsettings.json` lists every setting each service reads, the required ones as empty placeholders
that show the expected type. It carries no comment keys — the table above is the documentation, so
the file stays plain JSON a reader can diff against what a deployment actually supplies. **An empty
placeholder counts as unset**: startup fails naming the setting, exactly as a missing key does.

`appsettings.Development.json` defaults the two Archive paths to repository-relative locations
(`../../fixtures/cabinets.json`, `../../.archive-data`) so that a local `dotnet run` needs no
environment variables. That is safe only because it is the Development file and the containers run
as `Production`: a relative path reaching a container would resolve against the **content root** —
`/app` inside the image — so a deployment that forgot to mount a volume would store documents in
the container's own layer and lose them at the next restart, without a word.

**Both resolved paths are logged at startup**, so there is never a need to work out where the data
went:

```
Loaded cabinet definitions from /app/fixtures/cabinets.json
Storing documents under /var/lib/archive
```

No credentials are baked into any image or committed to this repository, and there is no `.env`
for one to hide in.

**The Export service is given the Archive by service name**, `http://archive-api:8080`, which is the
compose network's own DNS. It never sees the Archive's volume: the only way in is the three routes
above, which is the boundary the brief requires and the architecture tests enforce.

## What the container log holds

Everything goes to stdout, so `docker compose logs -f archive-api` (or `podman compose logs …`) is
the whole story. Four kinds of line, and nothing else:

| Line | When |
|---|---|
| The two resolved paths above | Once, at startup |
| `Request and Response: … Path … StatusCode … Duration` | One per request, after it is answered |
| `Stored <id> in <cabinet> with <n> part(s)` | One per document an upload actually stored |
| `400 The upload was refused: <reason>` | A refusal, with the sentence the caller was given |

A storage failure is logged at `Error` with its exception. That matters because the upload report
turns a failed document into a `Failed` row and still answers `200` — so the log is the only place a
volume that has stopped accepting writes shows up.

One deliberate omission: **request and response bodies and headers are never logged**: an upload
body is untrusted and may be 256 MiB, and a header is where a credential would appear once there is
one. Set `Microsoft.AspNetCore` to `Information` in `appsettings.json` if you want the framework's
own per-request detail back.

## The round trip

The brief's demo: upload the shared sample material twice, export it, unpack the archive, and
account for every difference. Run on 2026-10-30 against both containers.

```bash
# twice, so duplicate titles have to be dealt with
python upload-sample-material.py          # the recipe is in "Uploading a folder" above
python upload-sample-material.py

TODAY=$(date -u +%Y-%m-%d)
curl -X POST http://localhost:8081/exports -H "Content-Type: application/json"   -d "{\"cabinet\":\"Invoices\",\"markerField\":\"Batch\",\"from\":\"$TODAY\",\"to\":\"$TODAY\"}"   -o export.zip
```

**Result: 24 documents in, 30 entries out, and 15 of the original folder's 16 entries byte-identical
by SHA-256.** One difference, and it is a rule:

| Difference | Reason |
|---|---|
| `delivery-2026-03/archive/old-page.txt` is absent | Nested more than one folder deep, so the **upload** never stored it. The upload report said `Skipped` with that reason at the time; the export can only hold what the Archive holds |

Everything else matched, including the cases the fixtures exist to break:

- `Договор София.txt` and `Фактури Q1/фактура-1.txt` — Cyrillic names, unchanged;
- `readme with spaces.txt` — spaces, unchanged;
- `empty-note.txt` — zero bytes, still zero bytes, and still a *file*;
- `empty-folder/` — no parts, still an empty folder, and told apart from the zero-byte file;
- `delivery-2026-03/` — three parts in `page1, page10, page2` order, the Archive's ordinal order;
- the second upload's twelve documents came out as ` (2)`: `invoice-001 (2).txt`,
  `delivery-2026-03 (2)/`, `empty-folder (2)/`, `Договор София (2).txt`.

Afterwards, all 24 exported documents carried `Batch = Exported on 2026-10-30`, and the 11
documents stored on the previous day — outside the range — were neither exported nor marked, which
is the date filter working against real stored data rather than a test fixture.

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
- [`DECISIONS.md`](DECISIONS.md) — key decisions, what is missing, AI notes, time log, and the
  upload and export rules in plain terms
- [`CLAUDE.md`](CLAUDE.md) — the standing instruction for sessions in this folder
