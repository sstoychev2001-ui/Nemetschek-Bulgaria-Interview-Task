# Take-home task: a small document archive

You will build a small system that stores documents in file cabinets. Each document is made of zero or more parts (files) and is described by typed index fields, such as an invoice number or a signing date. Users upload folders of material into a file cabinet and later export documents back out as a zip archive. The system consists of two .NET services and a web client, all running in containers.

A **file cabinet** holds documents that share the same set of metadata fields, called **index fields** — think of it as a table with a fixed schema. A **document** is one record in a file cabinet: its index values plus zero or more files, called **parts**.

## Time, AI and what happens next

- **Time.** We do not set a time limit. The task is larger than an evening, and how much time people can give it varies. Spend what you reasonably can, and note in `DECISIONS.md` roughly how long it took. Please hand in within 7 days of receiving the task. If you cannot do everything, build in the order below: we would rather see the first parts done well than everything done halfway.
- **Priorities.**
  1. The Archive service with the upload rules and their tests.
  2. The Export service with the export rules and their tests.
  3. `docker compose up`, with data that survives a restart and no credentials in any image.
  4. The web client. A minimal one is fine; without one, demo through the services' OpenAPI UI.
  5. Everything else in the acceptance criteria.

  `README.md`, `AGENTS.md` and `DECISIONS.md` are always required, even if short. For anything you did not build, including items under "Things your system must cope with", write in `DECISIONS.md` what is missing and how you would do it.
- **AI.** You may — and we expect you to — use AI assistants. We assess your judgment, not your typing speed. In the review you will be asked to explain and change any part of your code live.
- **Review.** After you hand in, we meet for a 90-minute review session: you demo the system, we discuss it with you, and you make a live change to your code.

## What you build

- **Archive service**, in .NET (ASP.NET Core). It owns:
  - the file cabinets and their index fields, loaded from `fixtures/cabinets.json`;
  - documents: index values plus zero or more parts with content;
  - the upload rules, queries with a date range and paging, and updates of index values;
  - its storage. You choose it, but data must survive restarting the containers.
- **Export service**, in .NET (ASP.NET Core), as a separate process. It takes a file cabinet, a date range and a marker field, and then:
  - reads the matching documents from the Archive over HTTP, page by page;
  - fetches their content;
  - builds a zip archive by the export rules below;
  - writes the export marker back through the Archive.

  The Export service never touches the Archive's storage directly: everything goes through the Archive's HTTP API.
- **Web client**, in any technology. It lets the user:
  - pick a file cabinet and see its index fields, including each field's type and whether it can be written;
  - upload material and see the upload report;
  - start an export and download the archive.

  It shows loading, empty and error states, and on an error it shows the server's message.

One `docker compose up` starts everything. Whether the client talks to both services directly or through a reverse proxy is your decision.

## Cabinets and fields

The file cabinets are defined in `fixtures/cabinets.json`, and the Archive loads them from there. The file cabinets and their fields are fixed. You do not build any way to create, change or delete them.

The file has this shape (one field shown):

```json
{
  "cabinets": [
    {
      "name": "Invoices",
      "displayName": "Invoices",
      "fields": [
        { "name": "InvoiceNumber", "label": "Invoice number", "type": "text", "maxLength": 20, "writable": true }
      ]
    }
  ]
}
```

- `cabinets` lists the file cabinets. There are two: `Invoices` and `Contracts`.
- Each file cabinet has a technical `name`, a `displayName` and a list of `fields`.
- Each index field has:
  - `name`: the technical name;
  - `label`: the display label;
  - `type`: `text`, `number` or `date`;
  - `maxLength`: the maximum length, for `text` fields only;
  - `writable`: whether a client may set the field. The Archive rejects any write to a non-writable field.

Every document also has a title and these read-only system fields: id, `StoredAt` and `ModifiedAt` (date-time). They are not in `cabinets.json`; the Archive sets the system fields.

## Upload rules

The user picks a file cabinet, one of its writable text fields, and a folder of material. How the material reaches the system is your decision. The folder is stored by these rules:

- Each entry at the top level of the folder becomes **one document**, whether it is a file or a subfolder.
- A file becomes a document with one part.
- A subfolder becomes one document whose parts are its files, in an order you document. Anything nested deeper is ignored and reported as skipped.
- An empty subfolder still becomes a document, with no parts.
- The title is the name of the file or folder.
- The chosen field is set to the name of the uploaded folder. It is the same value for every document in the upload.
- The upload reports what was stored, what was skipped and what failed. A partial failure is never silent: the user can tell what reached the server.

## Export rules

The user picks a file cabinet, a writable text field for the marker, a from-date and a to-date. The export follows these rules:

- The dates are **calendar dates, both inclusive, as the user means them**. The filter is on `StoredAt`.
- A document with one part becomes a **file** named after the document.
- A document with several parts becomes a **folder** named after the document, with one file per part.
- A document with no parts becomes an **empty folder** named after the document.
- Two documents with the same title both survive the export, by a naming rule you document.
- After a document has been exported, the chosen field is set to `Exported on yyyy-MM-dd`, with the date of the export.
- The marker records that a document was exported; it is not a filter. Every export includes all documents in the date range, whether or not they are already marked, and overwrites the marker.
- The user downloads the archive through the client.
- The demo in the review runs with the Archive's maximum page size set to 5.

## Things your system must cope with

The environment and the users are not always friendly:

- Exports may be larger than the memory available to the Export service.
- Users, services and containers may be in different time zones.
- The Archive returns query results in pages, and the maximum page size is configurable.
- Export changes data: consider what state remains if it fails halfway.
- Treat everything a user uploads as untrusted.
- Names may contain any Unicode characters.
- An operation must not leave anything behind: no locked files and no temporary files that grow forever.
- The same images must run with different settings, and no credential may be in any image or in the repository.
- The client may be served from a different host than the services.
- Services may start in any order and may be briefly unavailable.
- Fields have types and limits, and the Archive enforces them.

## Acceptance criteria

- One `docker compose up` from a clean clone brings up everything. The README names the settings that have to be supplied.
- Data survives `docker compose down` followed by `docker compose up`.
- The logs of every service are visible through `docker compose logs`.
- Each service describes its HTTP API in OpenAPI, browsable while the service runs in its container.
- Failures reach callers in one documented shape for every endpoint.
- Every upload and export rule has a test that fails when that rule is broken. `dotnet test` passes from a clean clone with no network and no containers.
- Upload the shared sample material twice, export it and unpack the archive: show where the result matches the original folder and name every place where it does not, with the reason. This round trip is part of your demo in the review.
- The client never repeats a rule that belongs to the server. It shows when a request is in flight, and the same request cannot be started twice by accident.

## What you hand in

A git repository that contains:

- the source code and the compose file;
- `README.md`: how a human builds, runs and tests the system, including the settings that have to be supplied;
- `AGENTS.md`: instructions for humans and AI agents, covering the architecture, where the rules live, your conventions, how to add an operation and how to verify a change. We will test these instructions by giving an AI agent a small, unseen change to make in your repository.
- `DECISIONS.md`, with:
  - your key decisions, each in the form "A, because B";
  - what you cut and what is missing;
  - AI notes: where the AI was wrong and how you caught it;
  - an honest, rough time log;
- a git history with meaningful commits.

Send us the link to the repository on any host (make sure we can read it), or an archive of the repository that includes the `.git` folder.

## What we will talk about

In the review we will talk about these topics. The questions themselves are not part of this brief.

- architecture and the boundary between the services;
- data model and storage;
- the upload and export rules and the round trip;
- large data, time and text;
- failure halfway through an operation;
- configuration, secrets and containers;
- how the client talks to the services;
- tests;
- the instructions for humans and AI;
- how you used AI.

## Material in this folder

- `fixtures/cabinets.json`: the file cabinets and their index fields (see "Cabinets and fields").
- `fixtures/sample-material.zip`: unpack it to get the folder `sample-material/`, which is representative material for trying your system. It is also the shared sample material for the round trip.
- On Linux, extract the zip with 7z, bsdtar or `python3 -m zipfile -e sample-material.zip .`; Info-ZIP `unzip` 6.00 ignores the UTF-8 flag on entry names and garbles them.

## Out of scope

- Authentication.
- Deploying the system anywhere; `docker compose up` on your own machine is enough.
- Performance work beyond knowing where your system would break.
- UI polish.
