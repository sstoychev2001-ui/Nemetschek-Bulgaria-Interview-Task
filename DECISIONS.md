# Decisions

Each decision in the form "A, because B". Updated as the work progresses.

## Architecture

- **Layered, three production projects (`Api` / `Core` / `Infrastructure`), because** it is conventional,
  small enough to explain under live questioning, and gives one obvious home per concern. A separate
  `Application` layer was rejected: at four or five operations it would be pass-through ceremony.
- **One test project per production project, plus a `TestSupport` library, because** a single test project
  hides which layer a failure belongs to and lets a test quietly depend on a project it has no business
  knowing. `TestSupport` holds only what two test projects genuinely share — `RepositoryLayout` and
  `TestCabinets` — and carries no test-framework dependency of its own. The pairing is enforced by
  `Each_test_project_references_the_project_it_tests`.
- **The architecture tests live with the Api tests, because** the Api sits at the top of the dependency
  chain and is the only project that can see every other one.
- **`Core` references no project, and only an allowlisted package, because** that is what keeps
  `dotnet test` passing from a clean clone with no network and no containers, which the brief makes
  an acceptance criterion. Enforced by `Core_declares_only_allowed_package_references`.

  **Relaxed from "no packages at all" on 2026-10-29**, to exactly
  `Microsoft.Extensions.Logging.Abstractions`, so `DocumentsStoreService` can log in the `catch`
  that already handles the failure instead of behind a decorator. What the rule protects is intact:
  that package declares no dependencies of its own, touches no disk, clock or socket, and binds to
  nothing — with no provider registered, `ILogger` writes nowhere, so the rule tests still run
  offline against plain objects. The allowlist is a list of names and not a pattern on purpose:
  "abstractions-only" is a judgement, so each addition is a decision recorded here rather than a
  formality the test waves through.
- **Storing lives in `Core/Services/Documents/`, not in `Infrastructure`, because** storing documents
  *is* what a document archive does. Most of `DocumentsStoreService` is rules, not plumbing: a
  document is committed whole or not at all, the system fields are the Archive's, nothing in-flight
  survives a restart, and a page is ordered oldest-first. Those are the brief's requirements, and
  separating them from the `Directory.Move` that delivers them split one decision across two
  projects. `Infrastructure` keeps what belongs to the deployment: the dependency registrations and
  the loader for the `cabinets.json` the deployment mounts.

  Checked before moving, because it would have been a bad surprise: the store uses only
  shared-framework types (`System.IO`, `System.Text.Json`, `System.Text.Encodings.Web`), so `Core`
  declared zero packages and the enforcing test still passed. `dotnet test` also stays offline — a
  temp directory is not a container. *(It took one package later, on 2026-10-29:
  `Microsoft.Extensions.Logging.Abstractions`, so the store could log its own failures. See the
  allowlist decision under "Architecture".)*

  **The honest cost.** `IDocumentStore` is now declared and implemented in the same project, so it no
  longer separates two assemblies; it earns its place only as the seam a test fake replaces, and
  nothing would stop a future rule from calling the concrete store directly. The layering is a
  convention here rather than something the dependency tests can catch, because they check
  references and this is a question of what a project is *for*.
- **`Core` is folded by type-kind — `Domain/`, `Services/` (with `Services/Interfaces/`),
  `Validation/`, `Exceptions/` — because** the kinds are few and each one answers a different
  question about the code: what the Archive holds, what it does, what it refuses, and how it says
  so. Finding "every rule that can refuse an upload" is one folder.

  **This reverses the original feature-folding**, which argued that colocation means a behaviour
  change lands in one folder. That is the real cost: adding an index-field rule now touches
  `Domain/`, `Validation/` and possibly `Exceptions/` instead of one directory. It also retires the
  plural-folder convention, which only existed because `Cabinets/Cabinet` made the namespace and
  the type collide.
- **The ports moved from `Core/Ports/` to `Core/Services/Interfaces/`, because** they are the
  interfaces the services depend on, so they read better beside them than in a folder of their own.
  What they are has not changed: `Infrastructure` implements them, and `Core` still references
  nothing.

- **The upload mapping is side-effect-free, because** every upload rule then reasons about paths and
  names alone. Content bytes stay in `Infrastructure` and are streamed; the rules never read them.
  It lives in `UploadPlanningService`, separate from `UploadService` — see "The upload endpoint".
- **One top-level directory per service with its projects directly inside, and one solution at the root,
  because** the brief makes "`dotnet test` passes from a clean clone" an acceptance criterion, and a
  single root solution satisfies it with one command. Two per-service solutions with none at the root
  make `dotnet test` at the root fail outright with MSB1003 (verified), so it would need two
  invocations or a wrapper script.

  Two solutions were considered for the sake of the service boundary and rejected, **because a solution
  file does not enforce anything** — it is only a list of projects for tooling, and a `ProjectReference`
  works regardless of solution membership. The split would buy symbolism, not enforcement. Microsoft's
  own microservices reference sample ships one solution spanning every service. Anyone wanting to open a
  single service in isolation can use a `.slnf` solution filter without a second solution, and without
  reintroducing the drift that rotted the old scaffold's second `.sln`.
- **No project is shared between Archive and Export, because** the service boundary is a review topic.
  Export talks HTTP and keeps its own client DTOs; duplicating a few DTOs is the correct price.
- **The dependency rules are enforced by tests, not by documentation
  (`<Service>.Api.Tests/Architecture/ProjectDependencyTests.cs`, one per service), because** the layering is what
  makes the rest of the brief achievable, and a convention nothing checks is a convention that decays.
  The tests read the real csproj files and assert: Core declares no project reference and no package
  outside its allowlist, Infrastructure references only Core, Api references only Core and
  Infrastructure, dependencies point inward only, no Archive project references an Export project, and
  every project is in the solution. Verified to bite: adding a `PackageReference` to Core failed
  `Core_declares_no_package_references`, as the test was called before the allowlist.
- **Validation returns a `FieldViolation?` rather than throwing, because** the upload report must list
  many refusals at once — "a partial failure is never silent" — so a refusal has to be a value.
- **Violation reasons are complete user-facing sentences naming the field and the limit, because** the
  client is forbidden from restating server rules, so the server must phrase them.

## The Export service's projects

Created on 2026-10-30, projects only — no export rule is implemented.

- **All six projects were created at once, not just the three production ones, because** the
  convention is one test project per production project and the export rules need a home to land in
  *with* their tests. The brief makes "every export rule has a test that fails when that rule is
  broken" an acceptance criterion, and the Archive's own history is the argument: the upload rules
  shipped untested and stayed that way for days. `Both_services_have_the_same_shape` now fails if
  one service gains a project the other lacks.
- **The two empty test projects hold a skipped placeholder rather than nothing, because** an empty
  test assembly makes `dotnet test` print "No test is available in …", which reads as a
  misconfiguration rather than as work owed. Each `Skip` reason names what is missing and points
  here. The run now reports `Skipped: 1` twice, which is the truth.

  **The seven rules those tests owe**, from the brief's "Export rules", so the next session starts
  from a checklist rather than from the brief:

  1. a from-date and a to-date are inclusive calendar dates, as the user means them, filtered on
     `StoredAt`;
  2. a document with one part becomes a file named after the document;
  3. a document with several parts becomes a folder with one file per part;
  4. a document with no parts becomes an empty folder;
  5. two documents with the same title both survive, by a naming rule that has to be documented;
  6. after a document is exported, the chosen field is set to `Exported on yyyy-MM-dd`;
  7. the marker records, it does not filter — every export covers the whole range and overwrites.

  Each needs a test that fails when the rule is broken, which is an acceptance criterion. The same
  goes for `Nemetschek.Export.Infrastructure.Tests`: that the client pages through the Archive
  rather than asking for everything at once, that part content is streamed rather than buffered
  (exports may be larger than available memory), and that a half-finished export leaves nothing
  behind.
- **The Export's layering tests live in `Nemetschek.Archive.Api.Tests`, because** they read csproj
  files rather than referencing projects, so one place enforces both services without either
  depending on the other — and putting them in `Nemetschek.Export.Api.Tests` would have meant a
  second copy of `RepositoryLayout`, which the `TestSupport` removal had just finished eliminating.
  The reverse rule now exists too: `No_Export_project_references_an_Archive_project`.
- **`Export.Core`'s package allowlist is empty, because** nothing has earned a place in it yet. The
  Archive's Core took `Logging.Abstractions` for the store's own logging; if the export rules need
  the same, that is a decision to record here rather than a test to delete.
- **`Nemetschek.Export.Api` declares `InternalsVisibleTo` for its test project, because** top-level
  statements compile to an internal `Program`, and `WebApplicationFactory<Program>` has to see it to
  boot the service in process. The Archive needs no equivalent: it names its public
  `CabinetsController` as the entry-point type instead, and the Export has no public type yet.
- **The scaffold already served OpenAPI and liveness, because** OpenAPI is an acceptance criterion
  for *every* service, and tests that boot the real host are what prove a new project is wired up
  rather than merely compiling. Readiness was deliberately left out of the scaffold — it has to mean
  "the Archive is reachable", and one that answers healthy without checking anything would let a
  dependent service wait on a lie — then added with the client, then removed with all the other
  health checking on 2026-10-31. The OpenAPI test is what remains of that argument.
- Not done, and knowingly: no `Dockerfile`, no compose service, and no settings for the Archive's
  base address. One `docker compose up` must eventually start both; today it starts the Archive
  only.

## The Export service

Built on 2026-10-30. "The Export service" in `DECISIONS.md` is the rule-by-rule reading; these are the
decisions behind it.

- **Three routes were added to the Archive first, because** the Export may only reach the Archive
  over HTTP and none of them existed: a date-range filter on the query, a part's content, and a
  write of index values. This was named as an Export blocker in the audit earlier the same day, so
  it was built before the Export rather than worked around. They are the Archive's public API, not a
  private channel — a web client needs the same three.
- **The date range is a half-open window of UTC instants, built from two calendar dates and a time
  zone, because** "both inclusive, as the user means them" is a statement about the user's calendar,
  not about UTC. `CalendarRange.Of` reads the from-date's midnight and the day *after* the to-date's
  midnight in the caller's zone, so the to-date is covered in full. Tested across the March
  daylight-saving change, where the window's start is read at +2 and its end at +3.
- **The document directory's name is the index for the filter, because** it already carries the
  exact instant (`yyyyMMddTHHmmssfffZ_<id>`). A day directory outside the window is skipped without
  opening a file, and a document inside one is accepted or rejected by parsing its name — so a range
  query reads metadata only for the page it returns.
- **Finding one document by id is a scan of directory names, because** there is no id index and
  adding one would be a second structure to keep consistent with the first. A v7 id carries its own
  timestamp, so the scan can be narrowed to one day if a cabinet ever grows enough to care. Written
  down rather than optimised now.
- **Index values are validated in full before anything is written, because** a partial write would
  leave a document half-updated with no way to tell. `IndexFieldRules.ValidateWrites` judges every
  value and returns a violation per refused field, so a caller setting three fields hears about all
  three rather than fixing them one request at a time;
  `DocumentsService.SetIndexValuesAsync` throws `IndexValueRefusedException` with the lot.
- **The field rules stay in `Core`, not on the request DTO, because** they need the cabinet — which
  is a route value the body does not carry — and the upload path applies the same
  `IndexFieldRules`. Asked on 2026-10-30 whether a DTO validator could do it instead; the answer is
  that the DTO already does the half it can. `IndexValuesBatchRequest` and `IndexValueRequest` check
  the shape (at least one id, at least one value, and whether the text parses as the type it
  claims), and `IndexFieldRules` checks the rules (does the field exist, is it writable, does the
  type match, does the value fit). Moving the second half into the Api would put a business rule in
  the HTTP layer, duplicate it for the upload, and answer with the framework's
  `ValidationProblemDetails` instead of the documented shape. What *did* move is the collecting
  itself: the twelve lines of LINQ that used to sit in the service are now `ValidateWrites`, beside
  the singular rule it is built from.
- **`document.json` is replaced by staging plus one `File.Move(overwrite: true)`, because** that is
  the same atomic-rename property `AddAsync` already relies on: a reader sees the old document or
  the new one, never a half-written file.
- **The naming rule and the marker's wording live in `ExportService` as `GetEntryName` and
  `GetMarker`, not in classes of their own, because** each was one expression with one caller inside
  one service — `ZipEntryNames` existed only to hold a dictionary the caller can pass in, and
  `ExportMarker` only to hold an interpolated string. Folded in on request on 2026-10-30; the
  behaviour is unchanged, which the duplicate-naming and marker tests confirm by asserting through
  `WriteAsync` rather than against the helpers. `GetEntryName` taking the table of used names is
  what lets one method serve both uses — document titles across the whole archive, and part names
  inside a single folder-document.
- **The duplicate-title rule is ` (n)` before the extension, compared case-insensitively, because**
  a reader of the unpacked folder should see the original name and an obvious sequence — and because
  `Invoice.txt` and `invoice.txt` are two documents in the Archive but one filename on Windows and
  macOS. The same rule runs again for parts inside a folder-document.
- **The marker is written after the document's entries, because** it claims the document was
  exported; writing it first would mark a document the caller never receives. Tested by making a
  part unreadable and asserting nothing was marked.
- **The marker's date is read in the caller's time zone, because** an export running at 23:30 UTC is
  already tomorrow for a caller in Sofia, and the marker is for them to read.
- **`ExportService.WriteAsync` returns the finished archive as a readable stream rather than taking
  a destination to write into, because** the caller then has nothing to decide: the endpoint sends
  what it is given and the service owns where an archive is staged. Changed on request on
  2026-10-30, having first been written the other way round. Two consequences worth naming. The
  stream's lifetime is now the caller's — `FileStreamResult` disposes it after sending, which is
  what deletes the file — so a **failed** export has to dispose it itself, since nothing else will;
  that is `A_failed_export_leaves_no_file_behind`. And the counts the method used to return are now
  only in its log line, so `ExportReport` had no producer left and was deleted rather than left as
  dead code; the tests that asserted it now assert the zip's own entries and the markers written,
  which is the observable result rather than a reported number.
- **The archive is built into a temp file and then sent, rather than streamed into the response.**
  Two reasons, and the first was found by a test rather than by reasoning: `ZipArchive` writes
  **synchronously**, and ASP.NET Core refuses synchronous writes to the response body — the endpoint
  test failed with "Synchronous operations are disallowed" before any container ran. Streaming would
  therefore need `AllowSynchronousIO`, which ties up a thread for as long as the caller's socket
  takes. The second reason is better: building first means a failure is still a `400` or `502` with
  nothing delivered, instead of a `200` holding a truncated zip. The file is opened `DeleteOnClose`,
  holds one export, and never involves memory — part content is streamed from the Archive into the
  zip entry either way, which is the requirement that mattered. The cost is that the caller waits
  for the build and the host needs disk for one export.
- **The client asks the Archive for `size=int.MaxValue` and lets it cap, because** the Archive's
  maximum is the Archive's business; asking for its maximum is asking for whatever it allows. It
  pages until a page comes back empty. The review demo runs that cap at 5, so the paging is real
  rather than decorative.
- **The Archive client has no request timeout, because** an export streams for as long as its
  content takes and a timeout mid-zip would hand the caller a truncated archive. The readiness probe
  has the opposite setting — two seconds — because a slow answer there *is* the failure.
- **`Export.Core` took `Microsoft.Extensions.Logging.Abstractions`**, the same single package the
  Archive's `Core` has, so an export's one summary line lives with the rules that produced it. Each
  service states that allowlist in its own `ProjectDependencyTests`; they agree today, and a
  divergence would be a decision to record here rather than a surprise.
- **What the Archive refused becomes a `400`, not a `502`, because** a cabinet that does not exist
  or a marker field that is read-only is the caller's mistake. A transport failure is a `502`: that
  one is not their fault. The split is on `HttpRequestException.StatusCode`.

  **The reason no longer travels with it.** The client was written to read the refusal body into the
  exception message, so "There is no file cabinet named 'Nonsense'" reached the Export's caller;
  it now uses `EnsureSuccessStatusCode()`, which does not read the body. The status still splits
  `400` from `502`, but the `detail` a caller sees is the framework's generic sentence.
  `A_status_the_Archive_refused_with_is_carried_into_the_failure` asserts exactly that — status
  carried, sentence not — so the behaviour is pinned rather than assumed, and
  "The Export service" in `DECISIONS.md` lists it under "Known limits". Worth restoring before the client is
  built: the brief asks that an error show the server's message, and a generic one gives the client
  nothing to show.
- **Compose gives the Export the Archive by service name** (`http://archive-api:8080`) and depends
  on it only for start order. The brief's "services may start in any order and may be briefly
  unavailable" is answered in the service rather than in compose: an export attempted while the
  Archive is away returns `502` naming it unreachable, and the next one succeeds without a restart
  here. See "Health checking, removed" below for what the gating used to do.
- **Removed: the time zone parameter.** Asked about on 2026-10-30, argued against, then removed on
  the reaffirmed instruction that the client and the services share a zone for the demo. Dates are
  now read as **UTC calendar days** on both services, and the marker carries the UTC date.
  `ExportTimeZone` is gone, `CalendarRange.Of` takes two dates, and the `timeZone` query value and
  body field are gone from the Archive and the Export.

  **What it costs, stated plainly rather than glossed.** The brief's "calendar dates, both
  inclusive, **as the user means them**" is now satisfied only for a caller in UTC, and "users,
  services and containers may be in different time zones" is **no longer addressed** — the coping
  table below says so. Measured before the removal: the same request for 30 October 2026 returned
  **24** documents read in UTC, **35** in `Pacific/Kiritimati` (UTC+14) and **0** in
  `Pacific/Midway` (UTC−11), so a caller whose day differs from UTC's genuinely sees different
  documents. `An_instant_is_judged_by_its_UTC_day_whatever_offset_it_carries` and
  `A_document_belongs_to_the_UTC_day_it_was_stored_on` pin the new behaviour rather than leaving it
  implied.

  **Reinstating it** is one optional parameter per service plus the conversion `CalendarRange` used
  to do; the deleted tests are in the history. Two shapes were refused along the way and would be
  again: the **container's local zone** as the default, because the same request would answer
  differently in two deployments; and a **UTC offset** instead of an IANA id, because a range
  spanning 29 March 2026 needs `+02:00` at its start and `+03:00` at its end. `InvariantGlobalization`
  stays off — the export still needs real culture data, and a future zone would need tzdata.
- **The Export pages with one request made up front and a `do/while` for the rest, because** the
  first page is the one request an export always needs *and* the thing that says how many documents
  there are, so asking for it before testing anything costs nothing and tells the loop when to
  stop. It reads while `read < total`, which means a complete read costs exactly as many requests as
  there are pages — the earlier shape asked once more and read an empty page to discover it was
  finished. `page.Documents.Count > 0` stays as a guard so a total the Archive cannot deliver ends
  the read rather than looping for ever, which is a real risk: an earlier edit removed the
  termination entirely and the loop became infinite until the compiler's unused-parameter error gave
  it away. It is still an `IAsyncEnumerable`, so a caller that stops early pays for no further page
  — `A_later_page_is_not_asked_for_until_the_caller_needs_it`.
- **The marker is written once, after the archive is complete, by a batch route.** Built on
  2026-10-30 after the alternative below was refused. `PATCH /cabinets/{c}/documents/index-values`
  takes a list of ids and one set of values; `ExportService` collects the ids as it writes and marks
  them all once the zip is closed. Measured on the containers: an export of 24 documents now costs
  the Archive **1** `PATCH` instead of 24, and that one request fanned out to 24 document writes
  with all 24 marked.

  Three consequences, all deliberate. **A failure partway leaves nothing marked** rather than the
  first few — the better failure mode, and `Nothing_is_marked_until_the_whole_archive_is_built`
  pins it. **Ids are resolved before any write**, so one bad id refuses the whole batch instead of
  half-applying it; that costs a second pass over the directory names, which is the price of not
  leaving a batch half done. And **the per-document route was removed**: the brief asks the Archive
  to own "updates of index values", which a batch of one satisfies, so keeping two routes for one
  capability would have been two things to keep correct.

  The response is a count, not the documents. An export marks every document in its range in one
  request, so echoing them back would make the response grow with the size of the export; a caller
  that needs the new values reads them. The *request* still grows with the number of ids, which is
  noted as a limit in "The Export service" in `DECISIONS.md` — chunking one batch per page is the obvious
  next step and needs no API change.
- Rejected: **writing the marker as part of the request that reads a part's content.** Proposed on
  2026-10-30 to save the per-document `PATCH`. It cannot work, and the deciding case is the one that
  is easy to miss: a document with **no parts** makes no content request at all, so it would never
  be marked — 2 of the 24 documents in the round trip are empty folders, and the brief requires both
  that an empty subfolder be a document and that every exported document carry the marker. Four more
  reasons: the marker is per document while content is per part, so a three-part document would be
  marked three times; a `GET` would mutate, so a retry, a cache or a web client previewing a part
  would re-stamp it; marking would happen *before* the entry is in the archive; and a read route
  would have to take the caller's chosen marker field, coupling the Archive's public API to one
  caller. Batching gets the saving without any of that, which is what was built.
- Rejected: **sharing a DTO or a project with the Archive.** The client's own request and response
  types duplicate the Archive's wire shape on purpose;
  `No_Export_project_references_an_Archive_project` and its mirror enforce it. That duplication is
  the boundary.
- **`ArchiveDocument` and `ArchivePart` are one type each, serving as both the Archive's wire shape
  and the export rules' model** (2026-11-02, after two rounds of argument). I first defended the two
  types and the `ToDocument()` mapping between them, on the grounds that the response type's
  `string? Title` must not reach the zip-naming rules. That reason was real but the conclusion was
  wrong, for a reason already written down one section up: **the Archive's own `Document` is exactly
  this** — one record carrying `[JsonPropertyName]` for `document.json` *and* serving as the domain
  type, validating itself through `IJsonOnDeserialized`. The Export having two types for the same
  job was the inconsistency, not the fix for one. The same precedent settled `DocumentDraft` /
  `PartDraft`: this project trades a compiler-enforced boundary for fewer types and writes the
  looser invariant down.

  So `ArchiveDocumentResponse` and `ArchivePartResponse` are gone and the client deserialises
  straight into `ArchiveDocument` / `ArchivePart`. **What could not go is the validation**, because
  it is what makes an unusable answer a `502` rather than a `500`: both records implement
  `IJsonOnDeserialized` and throw `InvalidDataException` when the Archive sends no id, no
  title/name, or no parts list — the same check in the same place in the pipeline, now attached to
  deserialisation instead of a copy. Verified by deleting the check and watching
  `An_answer_that_is_not_a_usable_document_is_refused` fail, which also confirms
  System.Text.Json calls `OnDeserialized` on a positional record built through its constructor.

  `ArchiveDocumentPageResponse` stays in `Clients/Responses/`: a page envelope of `total` plus
  `documents` is transport and has no meaning to the rules, so it is the one type here that earns
  its place on the wire side alone.
- **The client moved from `Export.Infrastructure` into `Export.Core/Clients/` on 2026-11-02, at
  request**, with `Interfaces/`, `Requests/` and `Responses/` beside it. The case for it: talking to
  the Archive *is* what this service does — there is no export without it — so by the same reasoning
  that keeps `DocumentsStoreService` in the Archive's `Core` rather than its `Infrastructure`, the
  client belongs with the rules it serves rather than at the edge. It also puts the interface and its
  one implementation in the same folder, where previously the seam was split across two projects for
  a boundary that only ever had one crossing.

  **Checked before moving, because it would have been a bad surprise:** `Core`'s package allowlist
  is one entry long and enforced by a test, so the client had to need nothing new. It does not —
  `HttpClient`, `System.Net.Http.Json` and `System.Text.Json` are all in the shared framework, so
  `Core` still declares only `Microsoft.Extensions.Logging.Abstractions` and
  `Export_Core_declares_only_allowed_package_references` still passes.

  What stayed in `Infrastructure` is `AddArchiveClient`: the `HttpClient` registration, the base
  address with its trailing slash, and the infinite timeout a streamed export needs. That is the part
  that is genuinely about the deployment, and it keeps `Microsoft.Extensions.Http` out of `Core`.
  The DTOs were renamed `*Dto` → `*Response` to match the folder, and the anonymous object that was
  the batch marker's body became `IndexValuesBatchRequest` + `IndexValueRequest` so `Requests/` holds
  a named type and the wire shape is explicit rather than inferred from property casing. The Api test
  that asserts the exact PATCH body is what proves the serialisation did not change.

  `ArchiveHttpClientTests` moved to `Export.Core.Tests/Clients/` with the class. That left
  `Export.Infrastructure.Tests` with nothing in it — and it cannot simply go, because
  `Both_services_have_the_same_shape` requires six projects per service — so it now holds
  `ServiceRegistrationTests` for `AddArchiveClient`, which had no tests at all before: the port
  resolving to the client, a blank or relative address stopping startup, and the trailing slash.

## What an export costs, and what it does not promise

Nothing reads a part into memory. Three hops, each streaming, measured on 2026-10-30 at 200 MiB
through a container whose memory went 60 MB → 72 MB → 62 MB:

| Hop | Why it does not buffer |
|---|---|
| The Archive serves the part | `FileStream` with a 64 KiB buffer, sent by `FileStreamResult` in chunks |
| The Export reads it | `HttpCompletionOption.ResponseHeadersRead`, then `ReadAsStreamAsync` |
| The Export writes it | `CopyToAsync` into `ZipArchive.CreateEntry(…).Open()`, onto the temp file |
| The caller downloads it | `FileStreamResult` over that temp file |

What *does* grow with an export is bounded by the **number of entries**, not their size: the zip's
central directory, the table of entry names taken, one page of document metadata, and the marker
request's list of ids. An export of a million tiny documents would press on memory where one
enormous document does not.

What an export does not promise:

- **No resume and no concurrency control.** Two exports of the same range run independently and both
  write the marker; the last one wins.
- **The marker is the only record that an export happened.** There is no export history, and no way
  to ask what a previous export contained.
- **A document stored while an export runs** may or may not appear. The Archive pages by directory
  name, so one stored after the export started sorts after the page already read — skipped rather
  than duplicated, but nothing promises that.

## Tests

- **Moving the suite from xUnit to MSTest** (2026-11-02, at request). Six test projects, 37 files,
  292 tests. `xunit` and `xunit.runner.visualstudio` out, the `MSTest` 3.11.1 meta-package in,
  `Microsoft.NET.Test.Sdk` bumped 17.12.0 → 17.13.0 because MSTest 3.11 requires it and the downgrade
  was an error under `TreatWarningsAsErrors`. The implicit `<Using Include="Xunit" />` became
  `Microsoft.VisualStudio.TestTools.UnitTesting`, `[Fact]` became `[TestMethod]`, and every test class
  gained `[TestClass]`. MSTest 3.11 rather than 4.x **because** 4.x makes Microsoft.Testing.Platform
  the default runner, which on the .NET 9 SDK pinned in `global.json` needs extra opt-in properties
  for `dotnet test` to work at all; 3.11 keeps the plain VSTest path the brief's one-command
  acceptance criterion depends on. Nothing in the services changed, and the test count did not move.

  **The one trap, and it is silent:** xUnit's `Assert.Equal` compares two `IEnumerable`s element by
  element; MSTest's `Assert.AreEqual` compiles on the same two arguments and then compares
  *references*. A mechanical rename would have left ~60 sequence assertions passing for the wrong
  reason — green, and worthless. They are now `CollectionAssert.AreEqual(expected, actual.ToArray())`.
  Proved rather than assumed: reversing the two `OrderBy`s in `UploadPlanningService` fails 8 of them,
  and the whole set was found by scanning for an `Assert.AreEqual` whose expected argument is a
  collection expression, then re-reading every remaining one by hand.

  **The other four differences, each a deliberate choice of replacement:**
  - `Assert.Single(c, predicate)` → `Assert.ContainsSingle(predicate, c)`. **Argument order is
    reversed** in MSTest, for `Contains` and `DoesNotContain` too. `ContainsSingle` still returns the
    item, so the call sites that used the result are unchanged otherwise.
  - `Assert.Throws<T>` → `Assert.ThrowsExactly<T>`, and `ThrowsAny<T>` → `Throws<T>`. MSTest's
    `Throws` is the permissive one; xUnit's was the strict one. Mapped to preserve the rule each test
    states, not the method name.
  - `Assert.IsType<T>(x)` → `Assert.IsInstanceOfType<T>(x, out var typed)`, because MSTest's returns
    `void`. Eleven call sites that read `.Value` off the result became two statements. A real widening
    comes with it — xUnit's `IsType` demands the exact type, MSTest's accepts a subclass — accepted
    because every use is on a sealed `IndexValue` case or an exception type with no subclass in play.
  - `Assert.All(c, action)` has no MSTest equivalent. The two uses became
    `Assert.IsEmpty(c.Where(negated predicate))`, which gives a worse failure message and the same
    verdict. Mutation-checked: breaking the export marker's format fails it.

- **Each service's architecture rules live in its own test project** (2026-11-02, at request). One
  `ProjectDependencyTests` in `Archive.Api.Tests` had been enforcing both services — it held
  `ExportCore`, `ExportInfrastructure` and `ExportApi` constants, so the Archive's test project knew
  the Export's shape and the Export's own test project knew nothing. The file is now two mirrors of
  nine rules each, every loop scoped to its service's prefix, so neither names the other's projects.

  **The duplication this costs:** `RepositoryLayout` is now copied into `Export.Api.Tests`. That was
  the choice over the alternatives — a shared test-helper project is what `TestSupport` was, and it
  was deliberately removed; a `ProjectReference` across the services is the exact thing these rules
  forbid; and a linked `<Compile Include="..\..\Archive\…" />` would be that same coupling, just
  invisible to the rule that checks for it. So the helper is duplicated on purpose, with a comment
  saying why, the same call already made for `RepositoryFixtures`.

  **The one guard the split needed:** five of the nine rules are loops over `Projects(…)`, and a
  prefix that matched nothing would have made all five pass for free. `Projects` therefore asserts it
  found something. Both directions were then mutation-checked: a forbidden package added to
  `Export.Core` fails the *Export's* test project and leaves the Archive's green, and the reverse.

  **What this gives up:** the two repo-wide checks became per-service ones. `Every_project_in_the_repository_is_in_the_solution`
  is now `Every_project_of_the_service_is_in_the_solution`, and `Both_services_have_the_same_shape`
  became `The_service_is_built_from_six_projects` in each file. A project belonging to neither
  service would no longer be noticed by either — acceptable while the repository is two services and
  nothing else, and the six-project rule still pins what each must contain.
- **A test file sits at its subject's path** (2026-11-02, at request). The test projects had drifted
  into folders of their own invention: `CalendarRangeTests` sat under `Documents/` though
  `CalendarRange` is in `Validation/Documents/`, `IndexFieldRulesTests` under `Domain/IndexFields/`
  though the rules are in `Validation/IndexFields/`, the store's tests under `Documents/` rather than
  `Services/Documents/`, and every `Contracts` test in one flat folder against a production tree
  three levels deep. 26 files moved so that a reader who knows where the code is knows where its
  test is, with each namespace following its folder.

  Two exceptions, both deliberate: `Api.Tests/Architecture/` tests the service's dependency rules
  rather than any one file, and `Utils/` holds every class in a test project that is not a test.
  That second rule was tightened on the same day: the helpers had been scattered — `RepositoryLayout`
  under `Architecture/`, the `WebApplicationFactory` hosts at the project root, `TestCabinets` under
  `Domain/Cabinets/` and `FakeArchive` under `Services/Exports/`, each sitting wherever its first
  caller happened to be. All eight now live under their project's `Utils/`, so "is this a test or
  something a test uses?" is answered by the folder. `TestCabinetsTests` moved with its subject,
  which is the mirror rule applying to a helper as much as to production code.

  Two judgement calls worth naming. `Endpoints/` became `Controllers/`, because each of those files
  does target one controller even though it drives the whole host; the alternative was to keep a name
  that described the technique rather than the subject. And `DomainValidationTests` was split into
  `Domain/Documents/DocumentValidationTests` and `Domain/IndexFields/IndexFieldValidationTests`,
  because one file covering two domain folders cannot mirror either — the small `Validate` helper is
  now duplicated across the two, which is the same trade already made for `FixedClock`.


- **`TestSupport` was removed and its two helpers moved into the projects that use them, because**
  a shared project for ~160 lines made every test project reference a third one, and the enforced
  convention "a test project references only its subject" had to carve out an exception for it.
  Asked for on 2026-10-30. `TestCabinets` went to `Core.Tests`, which holds the 25 rule tests that
  use it; `RepositoryLayout` went to `Api.Tests`, whose architecture tests are the only thing that
  reads csproj files.

  **The real cost, and how it was paid.** The loader test
  `The_loaded_fields_match_the_hand_written_definitions_used_by_the_rule_tests` was the only thing
  keeping `TestCabinets` and `fixtures/cabinets.json` from drifting apart — and it could not survive
  the split, because it needed the loader from `Infrastructure` and the hand-written cabinets from
  what is now `Core.Tests`. Dropping it would have left every rule test able to pass against a
  schema the service never loads. So the guard was rebuilt on both sides instead:
  `Core.Tests/Domain/Cabinets/TestCabinetsTests` parses the fixture itself with `System.Text.Json` and
  compares field for field, and `Infrastructure.Tests` now spells the fixture's full contents out in
  a `[Theory]` rather than comparing one copy to another. Two tests where there was one, neither
  needing a shared project. The duplication that remains is a four-line `RepositoryFixtures`
  (find the solution file, join `fixtures/cabinets.json`) in two test projects, which is cheaper
  than the project it replaced.
- **The upload rules are tested against `UploadPlanningService` directly, because** it takes no
  ports, reads no content and touches no clock — so one test per rule in the brief is a plain
  assertion on a returned list, named as the rule. 32 of them. `UploadService` is tested separately
  with a fake store, which is the only way to assert the brief's "a partial failure is never
  silent": the fake is told which title to fail, and the test checks the other documents still
  stored and the failed row carrying its reason.
- **The endpoint tests were removed** (2026-11-02, on instruction: a test that boots a host is not a
  unit test). Gone are the four Archive endpoint files, the Export's one, and both
  `WebApplicationFactory` hosts — 56 tests, and the `Microsoft.AspNetCore.Mvc.Testing` reference
  with them. `Export.Api.Tests` is now its architecture rules and nothing else; it stays because an
  architecture rule requires six projects per service.

  **What this gives up, stated plainly rather than buried:** `Program.cs` and every controller are
  now reachable by no test at all, so the statuses, the two error shapes, the page cap, the CORS
  policy and the route table are unverified by `dotnet test`. Api line coverage goes back to roughly
  what it was before those tests existed — 56%, against 99% with them. The brief's "every upload and
  export rule needs a test that fails when that rule is broken" still holds, because the rules
  themselves live in `Core` and are tested there; what is lost is the proof that the HTTP layer
  still hands them the right input and returns the right status. These tests had earned their keep
  twice — they caught `ZipArchive` being unable to stream into a response body, and `app.UseCors()`
  going missing in a cleanup — so the loss is not hypothetical. Verify an endpoint change by running
  the pair and calling it.
- **A test double is a `Mock<T>`, not a hand-written fake** (2026-11-02, on instruction). Removed:
  `CapturingProblemDetailsService` and `RecordingLogger` from `ArchiveExceptionMiddlewareTests`,
  `RecordingLogger<T>` from the store tests, and `FakeArchive` from the Export's. Each became a Moq
  mock whose `Callback` appends to a capture list the assertions read. Two things that cost time and
  are worth writing down: mocking `ILogger<T>` needs `IsEnabled` stubbed to `true` or the `Log*`
  extension methods return before reaching `Log` and nothing is ever recorded; and the rendered
  message is `invocation.Arguments[2]?.ToString()` — the framework's `FormattedLogValues` — which
  avoids reflecting over the generic formatter delegate to call it. `FakeArchive`'s `FailOpeningPart`
  became a second `Setup` on the same mock that overrides the first, which removed the test class's
  need to build a second service over a different client.

  **Where a hand-written double was kept:** `FixedClock` (a two-line `TimeProvider` the tests move on
  purpose) and `FakeStore` in `UploadServiceTests`. `FakeStore` is a stateful stub, not a mock — it
  records what it stored and fails for a named title, which is how the brief's "a partial failure is
  never silent" is asserted — and three of its tests need a *differently configured* store, which is
  exactly the case a shared mock serves badly.
- **The class under test is a field built in `[TestInitialize]`**, not a `private Service()` factory
  (2026-11-02, on instruction). Same change for `[TestInitialize]`/`[TestCleanup]` replacing the
  xUnit-shaped constructor and `IDisposable`, which MSTest supports but which no longer reads as the
  framework's own vocabulary. Fields built in `BeforeEach` have to be `= null!`, which is the cost:
  a `readonly` initialiser was a stronger guarantee than an assignment in a method that MSTest
  promises to call.
- **`DocumentsStoreReadWriteTests` was merged into `DocumentsStoreServiceTests`** (2026-11-02, on
  instruction). They tested one class against one temp directory with one clock, split only by which
  operations they exercised; the split cost two copies of `FixedClock`, two roots and two setups. The
  merge is 37 tests in one file, taken verbatim from both so no assertion could be reworded on the
  way, and the surviving logger is the mocked one, since the log assertions need it and the
  read/write half never asserted on logging.
- **Coverage is measured, not assumed:** `dotnet test --collect:"XPlat Code Coverage"`. The 98.9%
  of lines recorded here (Api 99.4%, Core 98.6%, Infrastructure 100%) was measured while the
  endpoint tests existed and is **stale as of 2026-11-02** — removing them takes `Api` back to
  roughly 56%, since `Program.cs` and the controllers are no longer executed by anything. Worth knowing when reading those numbers: the
  three runs each report a different line set for `Core`, so a naive merge of the Cobertura files
  understates it badly — the figure above is the union of covered lines per source file, and a
  single-run reading of `Core` alone says 92% only because the `Cabinet` and `IndexField` validation
  paths are exercised from `Infrastructure.Tests`.

## Loading the cabinet definitions

- **cabinets.json is read once at startup and held in memory, because** the brief says the cabinets are
  fixed — there is no way to create, change or delete one — so there is nothing to invalidate and no
  reason to touch the disk per request.
- **A bad cabinets.json is fatal at startup, not deferred to the first request, because** a service that
  starts and then fails every call is harder to diagnose than one that refuses to start and says why.
  The file is read inside `AddCabinetCatalog`, during service registration, so the exception escapes
  before `app.Run()`. Verified by hand three ways: a missing setting, a missing file, and a file
  declaring an unknown field type — in the last case the log shows
  `ValidationException: index field 'A' has unknown type 'boolean'` and neither
  `Application started` nor `Now listening` is ever reached.
- **SDK exception types instead of a custom `CabinetsFileException`, because** `FileNotFoundException`
  and `InvalidDataException` state the intent more precisely than one type covering both, and
  `FileNotFoundException.FileName` already carries the path the custom type was hand-rolling. A custom
  exception pays for itself only when a caller catches it to distinguish it from others; this one is
  fatal at startup with no catch site anywhere, so the single-catchable-type benefit was hypothetical.
  An unreadable or locked file is now left to propagate as `IOException` /
  `UnauthorizedAccessException` rather than wrapped, since wrapping only hid the cause.
- **`CabinetsFileLoader`, not `…Reader`, because** it returns loaded `Cabinet` objects rather than raw
  content. It is two methods with no private helpers: `Load` reads a file, `Parse` deserialises,
  validates and maps.
- **The validation rules are DataAnnotations attributes and `IValidatableObject` on the DTOs themselves,
  because** that puts each rule next to the property it constrains, and means adding or constraining a
  field changes one place instead of two. It replaced eleven hand-written private guard methods in the
  loader. `Validator` does not walk into nested objects, so each DTO validates its own children first —
  that recursion is the one piece of machinery the approach costs.
- **`Cabinet` and `IndexField` are both the domain type and the on-disk shape, because** the file is
  fixed, tiny and read once at startup, so a separate DTO plus a mapping was ~137 lines of ceremony
  for a 30-line fixture. `[JsonPropertyName]` and the DataAnnotations sit on the domain record, and
  `CabinetsFileLoader` deserialises straight into it.

  **This reverses an earlier decision**, which argued for separate `CabinetsFileDto` / `FieldDto`
  types on three grounds. Two of them turned out not to bite: `System.Text.Json` and
  `System.ComponentModel.Annotations` are in the shared framework, so `Core` takes no package and
  `Core_declares_no_package_references` still passes; and an unknown `type` is still reported,
  because `JsonStringEnumConverter` refuses it by name during deserialisation. The third stands:
  the domain type now carries serialisation attributes and knows the on-disk format.

  On 2026-10-29 `Document` / `Part` were brought into line with this rather than the other way
  round, so the codebase does it one way throughout — see "Storage".
- **No `JsonSerializerOptions`, because** the file's shape is fixed and known. Case-insensitive matching
  was replaced by an explicit `[JsonPropertyName]` on every property, which states the on-disk contract
  where it belongs; the comment and trailing-comma leniency was for hand-edited files the brief does not
  describe.
- **Malformed JSON surfaces as `JsonException` and a broken rule as `ValidationException`, because** both
  are SDK types that say exactly what happened, and nothing catches them — the service is supposed to
  die. Wrapping them in a loader-specific type only added a layer that hid the cause.
- **`Parse` is public, because** it lets the content-validation rules be tested without writing a
  temporary file per case — which both keeps `dotnet test` fast and offline, and avoids the temp files
  the brief warns about.
- **`origin` was dropped entirely, because** the exception already names what broke (the rule) or what
  was missing (the path, via `FileNotFoundException.FileName`), and threading a source label through
  every message bought precision nothing consumed. The trade-off, accepted knowingly: a validation
  failure names the rule but not the file. Only one file is ever loaded, and the startup log names it.
- **The path is a required setting (`Archive:CabinetsFile`) with no built-in default, because** the same
  image has to run with different settings and the brief requires the README to name what must be
  supplied. A default would hide a misconfiguration. `appsettings.Development.json` supplies a
  repo-relative value for local runs only.
- **The JSON shape lives only in `Infrastructure`, because** Core carries no serialisation attributes, so
  the on-disk format can change without touching the rules.
- **An absent `writable` flag means not writable, because** defaulting the other way would let an
  omission or a typo quietly open a field the brief says the Archive must protect.
- **`maxLength` on a non-text field is rejected rather than ignored, because** the brief says it applies
  to text fields only, so its presence elsewhere is far more likely a mistake in the file than an
  intention. Same for a non-positive `maxLength` on a text field — no value could ever fit.
- **A text field with no `maxLength` is unbounded, because** the brief makes `maxLength` a property of
  text fields, not a requirement of them. Rejecting it would invent a rule.
- **Duplicate cabinet or field names are rejected, because** lookup is case-insensitive, so duplicates
  would make it ambiguous which field a write refers to.
- **A file declaring zero cabinets is rejected, because** the service would start and then be unusable,
  presenting the user an empty list with no explanation.
- **The loader test compares against the hand-written `TestCabinets` used by the rule tests, because**
  the rule tests deliberately avoid file IO, and without that comparison the fixture and the test
  doubles could drift apart while every rule test still passed against the wrong schema.

## Containers

- **Health checking, removed on 2026-10-31.** Both services had `/health` and `/health/ready`, three
  checks behind them (the cabinet catalog, a storage write probe, and the Export reaching the
  Archive), a compose `healthcheck` per service, `curl` installed in both runtime images to call it,
  and `export-api` waiting on `service_healthy`. All of it is gone, on the instruction that the demo
  will give the services a moment to start and the machinery is overkill for a take-home.

  What it bought, so the cost of the removal is on the record: `depends_on: service_healthy` meant
  the Export could not be asked anything before the Archive answered, and the storage probe was the
  one thing that would have named a volume owned by `root` instead of letting every upload fail one
  at a time. What is left in its place: the Export answers `502` naming the Archive unreachable
  rather than pretending, which is pinned by a test, and both services still fail fast at startup
  on an unusable storage path or cabinet file — which is where the two Archive checks' unhealthy
  branches were already unreachable from, since registration refuses to start on either.

  `DocumentStorageRoot.CanWrite` went with it: the storage probe was its only caller, so it and its
  three tests would have been dead code. Eleven tests were removed in total — the two liveness and
  two readiness endpoint tests, four registration tests, and those three.
- **`cabinets.json` is mounted read-only, not copied into the image, because** the brief requires the
  same image to run with different settings, and nothing about a particular deployment should be baked
  in. It also means the Archive cannot write its own definitions even by mistake.
- **The runtime image is Debian-based, not Alpine, because** the export rules interpret the user's
  calendar dates in their own time zone, which needs the ICU and tzdata the Alpine images omit.
  `curl` was installed alongside it for the compose healthcheck; with that gone, so is the
  `apt-get` layer, and both runtime images are now the base image plus the published output.
- **The container runs as `$APP_UID`, not root, because** everything a user uploads is untrusted and the
  process has no reason to hold root in the container.
- **Compose publishes `${ARCHIVE_PORT:-8080}`, because** a hard-coded host port collides on a shared
  machine; `.env` carries non-secret defaults only, and no credential belongs in it or in any image.

## What the service logs

Until this was looked at, the container log held six startup lines and then nothing: every request
the service answered — including one it refused — left no trace. The cause was two things at once.
`Microsoft.AspNetCore` is pinned to `Warning` in `appsettings.json`, which silences the framework's
own request logging, and the solution contained three `ILogger` calls in total, two of them at
startup. Neither was wrong on its own; together they meant the only observable thing a running
container produced was the fact that it had started.

- **The per-request line comes from the framework's `HttpLogging` middleware, not from code we
  wrote, because** it already knows the status and the duration, and a hand-rolled middleware would
  be a second thing to keep correct. `CombineLogs = true` makes it one record per request.
- **`Microsoft.AspNetCore` stays at `Warning` and `Microsoft.AspNetCore.HttpLogging` is named back
  up to `Information`, because** turning the whole namespace up adds four or five lines per request
  that say nothing a reviewer wants, while the HTTP log is exactly the one line that does. Log
  categories match by longest prefix, so the specific key wins.
- **Bodies and headers are not logged, because** an upload body is untrusted and may be 256 MiB, and
  a header is where a credential would appear the moment there is one. The same reasoning keeps
  document titles, folder names and file names out: they are attacker-controlled UTF-8 going into a
  line-oriented log. Ids, cabinet names and counts carry the same diagnostic value and are the
  server's own.
- **The readiness probe is excluded with `UseWhen`, because** it runs every ten seconds; left in, it
  is all a mostly-idle container would log, and a real request would be the needle.
- **A store failure is logged at `Error` inside `DocumentsStoreService`, because** `UploadService`
  catches a store exception and turns it into a `Failed` row, so the response is still `200` and the
  exception is otherwise lost entirely. A volume that has stopped accepting writes has to be visible
  from outside the container.

  **This started as a `LoggingDocumentStore` decorator in `Infrastructure` and was merged into the
  store on 2026-10-29**, at request. The decorator existed only to dodge `Core`'s no-package rule;
  with `Microsoft.Extensions.Logging.Abstractions` allowlisted, the log line sits in the `catch`
  that already discards the staging directory — one class instead of two, and the failure handling
  reads in one place. The alternative considered was moving the store out to `Infrastructure`
  instead, which was refused because it would reverse "storing is what a document archive does" and
  split one decision across two projects again.
- **Cancellation is logged at `Error` like any other failure.** Not what was wanted: the guard
  excluding it was written three times and removed three times, and on 2026-10-30 the instruction
  was to make the tests reflect the code. So an upload the caller abandoned appears in the log as an
  `Error`, indistinguishable from an unwritable volume.
  `A_cancelled_store_is_logged_at_Error_like_any_other_failure` states that rather than hiding it.
  The *report* is unaffected — `UploadService` still filters cancellation, which is tested.
- **The store is registered from the container rather than constructed eagerly, because** it now
  holds an `ILogger` and there is no provider to resolve one from at registration time. Both
  registrations resolve the same singleton — `IDocumentStore` for the rules, the concrete type for
  the readiness probe.
- **`Prepare` became `static` and runs during registration, because** it has to happen before the
  container exists, and a storage root the service cannot use must stop *startup* rather than fail
  the first upload. The layout still has one owner: the `cabinets/` and `tmp/` names live in that
  class and nowhere else.
- Rejected: structured JSON console output (`AddJsonConsole`). It is what a real deployment would
  ship to a collector, but there is no collector here, and the reviewer reads these lines in a
  terminal. One line of config to change if that ever stops being true.
- Rejected: a correlation id or an `ILogger` scope per request. It is the right answer under load,
  where interleaved requests make "which line belongs to which call" unanswerable — and the
  `traceId` ASP.NET already puts in every `ProblemDetails` is the value it would carry. At one
  reviewer driving one container, the path and the refusal reason are enough to find the request.

## The upload endpoint

- **The mapping, against the given sample material**, since this is the shape every upload rule is
  stated about:

  ```
  sample-material/              the uploaded folder; its NAME becomes the field value
  ├── invoice-001.txt           -> document "invoice-001.txt", 1 part
  ├── empty-note.txt (0 bytes)  -> document "empty-note.txt", 1 part (an empty part is still a part)
  ├── Договор София.txt         -> document "Договор София.txt", 1 part
  ├── delivery-2026-03/         -> document "delivery-2026-03", parts page1, page10, page2 (ordinal)
  │   └── archive/old-page.txt  -> IGNORED, and reported as skipped
  ├── Фактури Q1/               -> document "Фактури Q1", 2 parts
  └── empty-folder/             -> document "empty-folder", 0 parts (still a document)
  ```

  Only the top level becomes documents; one level of folders becomes parts; anything deeper is
  skipped *and* reported. A zero-byte file and an empty folder are different documents, which is
  what the fixture exists to catch.
- **The transport is `multipart/form-data` with a relative path per file, not a zip body, because** the
  client then sends the folder as the user picked it, and the server never decompresses untrusted
  input — no zip-slip check, no bomb limits, no entry-ratio policy to get wrong. The cost is that the
  client sends many parts instead of one, and that empty folders have to be named explicitly.
- **The relative path travels as each part's `filename`, because** that is what
  `formData.append('files', file, file.webkitRelativePath)` produces in a browser, so the obvious
  client needs no extra encoding. It is attacker-controlled and treated as such.
- **`folderName` is an explicit field rather than inferred from the first path segment, because**
  inferring it would make an upload of a folder whose only entry is a nested subfolder ambiguous, and
  the server should never have to guess a value it is about to write into an index field.
- **`folders` stays a second parameter beside `files`, rather than one parameter carrying both,
  because** an empty folder cannot be expressed as a multipart part at all — a part is a name plus
  bytes, and the brief requires an empty subfolder to become a document with no parts. Folding it
  into `files` as a part with empty content would make it indistinguishable from the sample
  material's zero-byte `empty-note.txt`, which has to become a document with *one* part; the fixture
  exists to force exactly that distinction. Verified in a single request on 2026-10-29:
  `empty-folder` → `parts: []`, `empty-note.txt` → `parts: ["empty-note.txt"]`.

  The cost is honest and small: `folders` is optional, a folder holding files is implied by its
  files and is never named — naming one anyway is a verified no-op — and `webkitdirectory` cannot
  enumerate empty folders, so the web client sends `files` alone and the "single parameter" shape is
  what an ordinary upload already looks like. Two one-parameter alternatives were weighed and
  refused. A **zip body** does record empty directories, but makes the server decompress untrusted
  input to recover structure the client already had (zip-slip, bombs, entry ratios) — refused above
  on the same grounds. A **trailing-slash marker** (`filename="empty-folder/"`) puts meaning inside
  the one string the brief says to treat as untrusted, and clients normalise it inconsistently.
- **The parameter is `folders` and not `emptyFolders`, because** empty folders are not all it
  carries. A **nested** folder has to be named there too, or it is never reported as skipped under
  its own name — and a nested folder usually holds files, so `emptyFolders` would be a false
  description of the value being sent. `delivery-2026-03/archive` is the case: it holds
  `old-page.txt`. The set is "folders the client saw that its files do not imply", and it has
  exactly two members, an empty folder and a nested one. Asked for on 2026-10-29 and refused with
  that reason; the two XML summaries were rewritten instead, since the old wording ("only folders
  holding no file need to be listed") was true about *necessity* and quietly hid the nested case —
  which is how the rename came to look obvious. Narrowing the parameter to empty folders only would
  be a real alternative, at the price of the brief's "anything nested deeper is ignored and reported
  as skipped" applying to files but never to the folder itself.
- **Part order inside a folder-document is ordinal (`StringComparer.Ordinal`), because** the brief
  asks only that the order be deterministic and documented, and ordinal is both without any code to
  own. The documented consequence is that digits sort as characters: `page1.txt`, `page10.txt`,
  `page2.txt`. The report lists part names in order, so a client is told the order rather than
  having to infer it.

  **Reversed on 2026-10-29.** Natural, numeric-aware ordering was chosen first, on the grounds that
  the sample material was built with `page1/page2/page10` precisely to force the choice. It was
  implemented as a `NaturalNameComparer` and then removed as unnecessary for now. The trade is real
  and worth stating plainly: ordinal costs the reading order a human expects and buys back ~90
  lines of comparer — with its own edge cases in leading zeros, overflow on long digit runs, and
  non-ASCII digits — that nothing yet depends on. Reinstating it is one `IComparer<string>` and two
  call sites in `UploadPlanner.Documents` plus one in `UploadService.StoreAsync`.
- **Documents and skipped entries are ordered the same way, because** the report then reads straight
  down against the user's own folder listing, with a skipped entry sitting directly under the
  document it came out of.
- **Ordering is ordinal rather than culture-aware everywhere, because** the same upload must come
  back in the same order whatever locale the container runs in. `InvariantGlobalization` is
  deliberately off for the export's date handling, so the culture is a real variable here.
- **A document's title keeps the file extension (`invoice-001.txt`), because** the brief says "the
  title is the name of the file", and the export has to reproduce the original folder — without the
  extension it would have to reattach one from somewhere.
- **The unit of success is the document, not the upload, because** the brief says "a partial failure
  is never silent". Documents 1–6 stay stored when 7 fails, 8 onwards are still attempted, and the
  report says which. `UploadService.StoreOneAsync` therefore catches everything the store can throw
  except `OperationCanceledException` — on cancellation there is nobody left to read the report.
- **The folder-to-documents mapping is its own service, `UploadPlanningService`, because** it
  answers a different question from `UploadService`: *what does this folder become*, as against
  *what happened when we tried to store it*. It takes no ports, reads no content and touches no
  clock, so it stays testable on its own, and `UploadService` is left with validation, storage and
  the report.

  Two earlier shapes were tried and are worth knowing about, because the current one keeps what
  each was reaching for:
  1. A static `UploadPlanner` with its own `UploadPlan` / `PlannedDocument` / `PlannedPart` /
     `SkippedEntry` types. It could not see streams, so a part referred to its content by
     *position* in the file list (`PlannedPart.FileIndex`) and the caller dereferenced the index
     afterwards — indirection that existed only to preserve the seam.
  2. Two private methods on `UploadService`. That retired the index, because a part could carry
     its `UploadedFile` straight through, but it put the mapping and the orchestration in one
     class and made the mapping unreachable from a test.

  The current shape has the index gone *and* the seam back.
- **`Plan` returns one list of `PlannedDocument`, each carrying an `UploadEntryOutcome`, not a pair of
  lists, because** every entry of the uploaded folder then has exactly one row with its own
  verdict, and the caller filters that one list rather than reconciling two. `ToStore` carries a
  `DocumentDraft`; `Skipped` carries the reason. It mirrors the report the endpoint returns, where
  the same entry reappears as `Stored`, `Skipped` or `Failed`.
- **`PlaceFile` and `PlaceFolder` merged into one `Place`, because** they differed only in the
  depth limit and whether a part is appended. The limit is now a `maxSegments` parameter fed by two
  named constants — `MaxFileSegments` (2: the folder it sits in and its own name) and
  `MaxFolderSegments` (1: its own name) — rather than an inline `file is null ? 1 : 2`, so the two
  numbers explain themselves at the call site instead of in the condition.
- **The trap avoided in every version of this:** entries are partitioned as they are placed, not
  filtered up front. A too-deep path still registers its top-level document *before* the depth
  check, so a folder whose only content is a subfolder remains one document with no parts.
  Filtering the deep paths out first would delete such a folder silently — and **the sample
  material cannot catch it**, because `delivery-2026-03` has direct files as well as `archive/`.
  Verified by hand instead, with a synthetic upload of `only-nested/deeper/file.txt`:
  `only-nested` comes back `Stored` with zero parts and the file `Skipped`.
- **The Api's `UploadEntryResult` deliberately shares its name with the domain's, because** it is
  the same concept on the wire, and the response DTOs are named after what they carry rather than
  after being DTOs. The price is that `UploadEntryResult.From` has to alias the domain type
  (`using DomainEntryResult = …`), since inside `Api.Contracts.Uploads` the unqualified name binds
  to the nearer one. Worth knowing before adding a second method that takes the domain type.
- **One `Refuse` method returns the refusal report directly, rather than a `FieldViolation` that a
  second method wraps, because** every caller did the same two steps and the split only existed to
  keep the unknown-cabinet case — which has no `Cabinet` to validate — out of the rule checks.
  Taking a `Cabinet?` lets that case be the first branch. Cost, accepted: `UploadAsync` needs one
  `cabinet!`, because the compiler cannot see that a `null` return implies the cabinet was found.
  That contract is stated in the method's summary.
- **Each refusal branch keeps its own sentence; they were deliberately *not* collapsed into one
  generic reason, because** the brief requires it in three places: the client "shows the server's
  message" on an error, the client "never repeats a rule that belongs to the server" — so only the
  server can phrase "at most 12 characters" — and failures must reach callers in one documented
  shape. `Text_value_one_over_the_limit_is_refused_and_the_reason_names_the_limit` already pins
  it. Verified by hand that all six branches still name their own rule.
- **A request-level refusal throws; a per-entry refusal stays a value, because** they are different
  shapes of answer. There is only ever one request-level refusal and it is terminal — no cabinet, or
  a field that may not be chosen — so nothing needs to collect several of them, and an exception
  carries it out of `UploadService` without every caller having to check a return. A skipped or
  failed *entry* is the opposite: the report must list many at once, which is why
  `IndexFieldRules` still returns a `FieldViolation?`.

  This is also the condition the repo's own rule sets for inventing an exception type — *"a custom
  exception earns its place only when something actually catches it and needs to distinguish it"*.
  `ArchiveExceptionMiddleware` is that catcher, so `UnknownCabinetException` and
  `UploadRefusedException` now earn theirs.
- **`ArchiveExceptionMiddleware` maps exceptions with a `switch`, because** adding an expected
  refusal should be one arm and nothing else — no new `catch` block, no second place to register
  it. Anything the switch does not recognise is rethrown for `UseExceptionHandler`, which answers
  `500` as `ProblemDetails`; the middleware never swallows a bug.

  Status mapping is by cause rather than one blanket code: `404` only for an unknown cabinet, `400`
  for a broken index-field rule, `200` when the upload ran and some documents failed. `500` was
  considered and rejected — most of these are the caller's mistake, and `5xx` is what retry
  policies and alerting act on, so a read-only field would be re-sent automatically and could page
  someone.
- **The refusal body is `ProblemDetails`, not the upload report, because** a refusal has nothing to
  report: nothing was stored. The rule's own sentence arrives in `detail`, so the client still shows
  the server's message. This gives up the earlier "one body shape whatever happened" property; what
  replaces it is the standard shape the brief asks for and the status telling you which it is.
- **The report is one list with an outcome per entry, not three grouped lists, because** the question
  a user actually has is "what happened to *this* entry of mine?", and one list answers it by being
  read once. The totals that used to be computed server-side are gone: they are a count of a list the
  client already holds, not a rule. `Skipped` and `Failed` stay distinct values — skipped is the rules
  working as written, failed is the Archive falling short and can be retried.
- **The report lists part names, not just a part count, because** the part order is itself a rule, and
  a count cannot be checked against the user's folder.
- **The endpoint answers one shape for every outcome — the report — and never `ProblemDetails`,
  because** a refusal and a partial failure are the same question to the caller: *what reached the
  Archive?* A request the rules turn away before anything is stored becomes a report with a single
  `Failed` row carrying the rule's own sentence. One shape, one parser, and the "failures reach
  callers in one documented shape" criterion is met by the body rather than by the status.
- **No endpoint picks a status code; `UploadFailureStatusMiddleware` does, because** a status is a
  property of how the operation went, and keeping that out of the action leaves the controller with
  nothing but binding and delegation — no branching, no logger, no `ProblemDetails` construction.
  It hooks `Response.OnStarting` rather than reading the response body: the status line is already
  on the wire by the time MVC has serialised a body, and buffering the response into a
  `MemoryStream` to parse the JSON back out would cost the streaming the rest of the upload path is
  built for.
- **Model validation is left on, so a missing form field answers ASP.NET's own
  `ValidationProblemDetails`.** Both documents claimed the filter was suppressed and nothing in the
  solution set it — found on 2026-10-30 by testing the statuses rather than reading the prose. The
  reasoning for suppressing it had expired anyway: it existed so the automatic `400` would not be
  the odd one out when every other refusal was `404`, and `ArchiveExceptionMiddleware` made a broken
  field rule `400` too. The residue is two `400` bodies — `errors.Field` for a missing parameter,
  `detail` for a broken rule — both standard `ProblemDetails`, both documented in `README.md`.
- **`RelativePath` refuses rooted paths, `.`, `..` and control characters, because** a traversal
  attempt should be *reported* to the user rather than quietly stored as a document title. It cannot
  escape anything either way — a name is data and content on disk is addressed by a server-generated
  id — so this is defence in depth plus an honest report. Every other Unicode character is kept
  exactly as the user had it.
- **The upload body size and part count are bounded by settings (`Archive:MaxUploadBytes`,
  `Archive:MaxUploadEntries`) with defaults, because** everything a user uploads is untrusted and the
  same image has to run with different settings. Defaults mean the service still runs out of the box.
- **Files are bound as `IFormFile` rather than read with `MultipartReader`, because** ASP.NET already
  spools a part over 64 KiB to a temp file it cleans up itself, and `OpenReadStream()` then gives the
  deferred, re-openable stream the store port wants. **This is where the system breaks first:** the
  whole body must arrive before any of it is stored. The upgrade path, if uploads outgrow it, is
  `MultipartReader` spooling each section to a temp file under the service's own control. Named here
  rather than discovered later.
- **An empty folder has to be listed in the `folders` field, because** a folder with no file in it
  does not appear in a multipart body at all. Consequence worth stating plainly: a browser's
  directory picker cannot enumerate empty folders, so the "an empty subfolder still becomes a
  document" rule is only reachable from a client that walks the filesystem itself. The rule is
  implemented and exercised; the browser is the limitation.
- **The controller has no private methods, because** an action does exactly three things and each has
  a better home: request → domain on the request DTO, domain → response on the response DTO, and
  outcome → `IActionResult` in `Api/Results/` as extensions on `ControllerBase`. Extensions on
  `ControllerBase` rather than free functions building `ObjectResult`, so the responses still go
  through `Problem` / `ValidationProblem` and pick up the `ProblemDetailsFactory` the rest of the
  service uses — hand-built `ProblemDetails` would silently lose the trace id.
- **Storage is an in-process placeholder (`InMemoryDocumentStore`), because** the store is still an
  open decision and the endpoint needed something to resolve. It knowingly breaks two things the brief
  requires — data does not survive a restart, and part content is held as a byte array so an upload
  larger than memory will not fit. Everything above it is written for a durable, streaming store, so
  replacing that one class is the whole change.

- **`GET /cabinets` answers `choosable` per field as well as `writable`, because** the rule for the
  upload field and the export marker field is "writable **and** text", and a client that filters a
  dropdown on `writable && type == "text"` has restated a server rule — which the brief forbids. The
  Archive already holds the rule as `IndexFieldRules.ValidateChosenTextField`, the same call that
  refuses an upload or an export naming a field it may not use, so the flag *is* that rule rather
  than a second statement of it — change the rule and the flag follows. `Cabinet.WritableTextFields`
  expressed the same predicate on the domain type and was removed on 2026-11-02 once the mapping
  asked the rule directly: one caller, and a parallel definition of a rule that already had a home.
  `writable` stays alongside `choosable` because the brief asks for it by name and it answers a
  different question: whether a write to the field is allowed at all. On the fixture's `Invoices`
  that is `InvoiceNumber`, `Supplier`, `Batch`, `Status` choosable, with `Amount` and `InvoiceDate`
  writable but not choosable and `ApprovedBy` neither.
- **The whole catalog comes back in one response rather than a route per cabinet, because** there are
  two cabinets, fixed at deployment, and a client needs every one of them to populate its first
  picker. A `GET /cabinets/{name}` would be a second round trip for data already sent. The response
  is wrapped as `{"cabinets":[…]}` rather than a bare array, which matches `cabinets.json` and
  leaves room to say something about the set later.
- **One controller per entity** — `CabinetsController`, `UploadsController`, `DocumentsController`,
  each routed at the path its entity owns — **because** `Contracts/` and `Core/Services/` are already
  split that way, and the single `CabinetsController` had become five actions over three entities.
  The routes did not change, which is what the endpoint tests prove: all of them still pass against
  the same URLs. `ArchiveApplication` still names `CabinetsController` as the
  `WebApplicationFactory` entry-point type, which only needs to be a type in the Api assembly.

- **CORS is configured per service from one comma-separated setting, and an unset setting means
  any origin, because** the brief says the client may be served from another host but does not say
  which, and no credential exists to protect: there is no authentication, no cookie and no session,
  so a hostile page that calls these services learns only what an unauthenticated caller could fetch
  with `curl` anyway. Refusing every origin by default would mean the client cannot work until
  someone finds the setting, and the usual middle ground — hard-coding `http://localhost:5173` —
  bakes one developer's dev server into the image the brief requires to run anywhere. Narrowing it
  is `Archive__AllowedOrigins=https://client.example`, one variable, no rebuild.

  **What this costs, stated because it is a real trade:** with any origin allowed, any page in any
  browser can read this Archive and start exports. That is acceptable for a take-home demo over
  `localhost` and would not be past it: the moment authentication exists, `AllowAnyOrigin` has to
  become an allowlist, and ASP.NET will not even let it stay — a policy cannot combine
  `AllowAnyOrigin` with `AllowCredentials`, which is the framework enforcing this exact boundary.
- **`app.UseCors()` runs before `UseExceptionHandler` and the exception middleware, because** a
  cross-origin caller can only read a response body the CORS headers permit it to read. Put the
  other way round, a `404` or a `400` would reach the browser stripped, and the client — which the
  brief requires to show the server's message — would have nothing to show but a network error.
  **No longer covered by a test**: the ten CORS endpoint tests were removed on 2026-11-02 on the
  instruction that they exercise framework behaviour rather than this system's rules, which is a
  fair reading — `AddCors`/`UseCors` is ASP.NET's, and the brief asks for tests of the upload and
  export rules. The cost is specific rather than theoretical, so it is worth naming: those tests are
  what caught `app.UseCors()` being deleted during a cleanup pass on 2026-11-01, when `AddCors`
  stayed registered and silently applied to nothing. The ordering above, and the policy's existence
  at all, are now held by reading `Program.cs`.
- **The Export exposes `Content-Disposition`, because** an export is a download and the file name is
  part of the answer. A cross-origin client cannot read an unexposed response header, so without
  this it would have to build the name itself from the cabinet and the dates — the zip's naming rule
  restated in the client, which is the duplication the brief forbids.

- **Every setting appears in `appsettings.json` as a placeholder — blank for the required ones, the
  real default for the tunables — and the live defaults sit in `appsettings.Development.json`,
  because** the two questions "what does this service read?" and "what will it run with?" deserve
  different answers in different files. The placeholder file is the contract: open it and the full
  set is visible with its types, with no prose to go stale. The Development file is the convenience:
  `dotnet run` and F5 start both services with nothing supplied, because the launch profiles set
  `ASPNETCORE_ENVIRONMENT=Development`.

  Putting the defaults in `appsettings.json` instead would have been simpler and was rejected: that
  file is what the *container* reads, and a real `StoragePath` there is exactly the failure the
  setting exists to prevent — a deployment that forgets it would store documents in the container's
  writable layer and lose them on the next restart, quietly. Development-only defaults keep that
  protection whole: verified both ways, the service starting from source with no variables set, and
  `ASPNETCORE_ENVIRONMENT=Production` failing with `Setting 'Archive:CabinetsFile' is required`.
- **Every setting is read the same way, and none has a code default** (2026-11-02, on
  instruction): `builder.Configuration["Section:Key"] ?? throw new InvalidOperationException(
  "Setting 'Section:Key' is required")`, in both services, for all eight settings. The argument for
  uniformity is that a reader can scan the top of `Program.cs` and see the complete set with no
  special cases — previously three settings used `GetValue` with an inline default, so the real
  limits were visible only in code, and `appsettings.json` could disagree with them silently. Now
  the file is the single statement of what the limits are, and a missing key is a startup failure
  rather than a quiet fallback. The numeric ones are read as strings and parsed with
  `CultureInfo.InvariantCulture`, so a container's locale cannot change what `268435456` means.

  **What this costs, and it is a real trade-off that was argued before the change:** `??` only
  fires on an *absent* key, and `appsettings.json` ships every key with a placeholder — so for the
  blank ones the throw is unreachable. A blank `Archive:CabinetsFile` now passes the `??` and is
  refused one layer down by `AddCabinetCatalog`'s `ArgumentException.ThrowIfNullOrWhiteSpace`:
  verified, `The value cannot be an empty string or composed entirely of whitespace. (Parameter
  'cabinetsFilePath')`. Startup still stops — the fail-fast requirement holds — but the message
  names the parameter instead of the setting a reader has to go and set. The earlier
  blank-is-treated-as-missing form preserved that message; it was replaced for the uniform shape,
  knowingly. Restoring it is one predicate, if the message matters more than the uniformity.
- **The Export's Development default is `http://localhost:5287`, not the container's `:8080`,
  because** it is the Archive's own launch-profile address, so the pair works from source with
  nothing supplied. Compose overrides it with the service name, which is the only place `:8080` is
  right.

- **`DocumentStorageRoot` was removed on 2026-11-02 and its `Prepare` became a private
  `PrepareStorage` in `DocumentsServiceCollectionExtensions`, because** preparing a directory is
  something the *deployment* does once at startup, not something the Archive does — and `AddDocumentsStore`
  was the only caller. What is left of the type was a `string`, so `DocumentsStoreService` now takes
  the resolved storage path directly: one fewer type registered in DI, one fewer indirection between
  the store and the five places it composes a path.

  **What this gives up:** the store no longer carries anything that could create its own directories,
  so the two it needs — `cabinets/` and `tmp/` — are a precondition rather than something it can
  establish. In the service that is true either way, since registration runs before the first
  request; in tests it means a fixture has to create `tmp/` itself, which the two store test classes
  now do with `Directory.CreateDirectory(DocumentStorageUtils.Staging(root))`. Worth knowing because
  the failure mode if someone forgets is a `DirectoryNotFoundException` from `SetIndexValuesAsync`,
  which stages its `document.json` with `File.Create` and so will not create a parent.

  `DocumentStorageRootTests` went with the type; the three behaviours it pinned were already covered
  on the other side of the seam by `ServiceRegistrationTests` — the directories being created, a
  left-behind staging directory being swept, and an unusable path stopping startup. The registration
  test that read `DocumentStorageRoot.Path` to prove a relative path resolves now asserts the
  prepared directories exist at the resolved location instead, which proves the same thing by
  observation rather than by asking the object.

## Storage

- **Documents are files on a mounted volume, one directory each, with no database, because** every
  argument for a database here failed when it was examined:
  - *Query performance* — the brief puts performance out of scope beyond knowing where the system
    would break.
  - *Relational logic* — there is none. No joins, no transactions spanning aggregates, no
    referential integrity beyond document→parts, which is containment rather than a relationship.
  - *Index values* — a sparse, typed, per-cabinet bag. In SQL that is an EAV table with five
    nullable value columns of which one is ever filled; in JSON it is
    `{ "Batch": { "type": "Text", "value": "…" } }`. The data is document-shaped.
  - *Content* — has to sit on the filesystem regardless, so the Export service can stream it out
    rather than buffer it. A database would therefore leave **two** stores to keep consistent and
    two recovery paths, where files leave one.

  The price, stated plainly: the directory tree is a hand-rolled index, and a new query shape means
  a new convention or a scan.
- **The layout in full**, since the code no longer carries a diagram of it:

  ```
  <root>/
    tmp/                                     staging; swept at startup
    cabinets/<Cabinet>/
      <yyyy-MM-dd>/                          the day, so a date range prunes without opening a file
        <yyyyMMddTHHmmssfff>Z_<id>/          the exact instant, so a window needs no metadata
          document.json                      index values and the part list
          parts/<order:D3>_<partId>.bin      content, addressed by a generated id
  ```

  The names and the two formats live in `Core/Utils/Documents/DocumentStorageUtils.cs`, which also
  holds the parsers that read them back; composing a path from them is done where it is used.
- **The layout is `cabinets/<Cabinet>/<yyyy-MM-dd>/<timestamp>_<id>/`, because** it makes the
  directory tree the index. A date range prunes to a few day directories without opening a file,
  and the timestamp prefix makes an ordinal sort of the names chronological — so a page is chosen
  from directory names alone and only the documents actually on it are read. Zero-padded and
  ordinal so the order cannot depend on the container's locale.
- **The store was split into four on 2026-10-30, at request**, having grown to 379 lines and 21
  members as the Export's three routes landed on it. What came out, and why each earns a file:
  `DocumentStorageUtils` (in `Core/Utils/Documents/`) holds the folder names, the day and instant
  formats, and the two parsers that read a path back — pure, so the layout is now testable without
  a filesystem, which 15 new tests do; `DocumentStorageRoot` held `Prepare` and `CanWrite`, which
  answered "is the deployment usable" rather than "where does this document go" — it is gone now,
  `CanWrite` with the health checks on 2026-10-31 and `Prepare` into the registration on
  2026-11-02, since preparing a directory is something a deployment does once rather than something
  the Archive does; `DocumentMetadataFile` holds
  reading and writing `document.json`, and owns the serialiser options because nothing else uses
  them. The store keeps the port's five operations and the commit-whole-or-not-at-all property that
  each of them turns on. 379 lines became 293 + 100 + 53 + 48.

  **Only path helpers with more than one caller survived the split.** `CabinetsRoot`,
  `DayDirectory`, `DocumentDirectoryName`, `PartFile`, `PartFilePattern` and `IsDocument` were each
  a single-use `Path.Combine` or `EndsWith`, and a wrapper read once says less than the call it
  replaces — the same reasoning that folded `ArchiveStorageLayout` in originally. The **formats**
  stayed in `DocumentStorageUtils` even though composing moved out, because composing and parsing
  have to agree: `DayOf` reads what a day directory was named with, and `StoredAtOf` reads the
  instant out of a document directory's name. One owner for those strings is what keeps a date-range
  query from silently reading the wrong documents.
- **A wrapper whose methods each have one caller is noise** — the rule that decides what may be
  extracted, learned the hard way. `ArchiveStorageLayout` had twelve members, eight of them read at
  exactly one place, and folding it into the store removed a type whose whole job was to be
  delegated to. The 2026-10-30 split above is the same rule applied in the other direction: the
  layout came back out once its helpers had several callers each, and only those did.
- **Part content files are named by a server-generated GUID, because** the brief requires that user
  names never become filesystem paths. The user's name is stored inside `document.json` as text,
  where `../../etc/passwd` is just an unusual title.
- **Document ids are `Guid.CreateVersion7(storedAt)`, because** version 7 is time-ordered, so an id
  carries its own timestamp and a document's directory can be derived from its id alone. That is
  what removes the need for a second, hand-maintained id→path index — and with it a second thing
  that could be left inconsistent by a crash.
- **A document is written into `tmp/` and then moved into place with a single
  `Directory.Move`, because** a rename within one filesystem is atomic, so a document directory
  either exists complete or does not exist. A reader can never see a half-written document, which
  is the brief's "consider what state remains if it fails halfway". Verified by hand.
- **`tmp/` is cleared during service registration, because** a staging directory can only survive a
  process that died mid-upload, and the document it belonged to was never committed. Startup is
  therefore the recovery point, and nothing accumulates — the brief's "no temporary files that grow
  forever". Verified: a directory planted under `tmp/` is gone after the next start.
- **`Prepare` creates the storage directories rather than relying on the Dockerfile's
  `mkdir -p /var/lib/archive`, because** the two do different jobs and the Dockerfile cannot do
  this one. Asked on 2026-10-29 and checked rather than argued:
  `podman run --entrypoint ls nemetschek-archive-api -la /var/lib/archive` shows the image layer
  holding an **empty** directory — no `cabinets/`, no `tmp/`. The Dockerfile line exists so the
  mountpoint is owned by `$APP_UID`, which is what lets a rootless container write into a *fresh*
  named volume at all; it is not a source of the layout.

  Three further reasons it cannot be assumed, in order of how quickly they bite. A volume
  **replaces** that path at runtime — an engine pre-populates only a new, empty named volume from
  the image, so a bind mount or a volume with content already in it gets nothing. `tmp/` is where
  every upload is staged before its atomic move, so without it the first upload fails; verified by
  deleting the directory from the running container. And running from source
  (`Archive__StoragePath=$PWD/.archive-data`, which `README.md` documents) involves no Dockerfile at
  all. Underneath all three: a rule of the service would otherwise depend on a line in a deployment
  artefact, which is the coupling `cabinets.json` being mounted rather than baked exists to avoid.
- **`Archive:StoragePath` is required with no default, because** a default would silently store
  documents inside the container's own writable layer, where they are lost on the next restart —
  the exact failure the setting exists to prevent. Same fail-fast reasoning as
  `Archive:CabinetsFile`, and verified both ways: absent, and pointing somewhere unusable.
- **A named volume whose root is owned by `root` while the process runs as `$APP_UID` is the usual
  container failure, and nothing now names it.** The readiness probe used to; with health checking
  removed, that failure appears as every upload failing one at a time, logged at `Error` with the
  path. The Dockerfile's `chown` below is what prevents it in the first place.
- **The Dockerfile creates and `chown`s `/var/lib/archive` before `USER $APP_UID`, because** an
  empty named volume takes its ownership from the image's directory at first mount. Without it the
  volume root belongs to root and the first upload fails with a permission error.
- **The store writes its JSON with `UnsafeRelaxedJsonEscaping`, because** the default encoder
  escapes every non-ASCII character, so `Фактури Q1` would be stored as `Фа…`. That
  round-trips correctly but makes the volume unreadable to anyone inspecting it, and the round-trip
  demo turns on being able to see that the names came through. "Unsafe" refers to HTML contexts;
  these files are never interpolated into markup.
- **Reading a document is not cached, because** there is then no state to rebuild at startup and no
  cache that can be stale. It is the opposite choice from the cabinet catalog, deliberately: that
  one is fixed, tiny and never changes, and documents are none of those things.

- **The service is run as a container only, and `appsettings.Development.json` carries no `Archive`
  section, because** a relative default there resolves against the content root — `/app` inside the
  image — so a deployment that forgot to mount the volume would quietly store documents in the
  container's own writable layer and lose them at the next restart. Required-and-absent fails at
  startup naming the setting, which is the louder and more useful failure. The cost is that
  `dotnet run` now needs both paths supplied; the command is in `README.md`.
- **`Archive:MaxPageSize`, `MaxUploadBytes` and `MaxUploadEntries` are compose environment
  variables fed from `.env`, because** the brief requires the maximum page size to be configurable
  and the review demo runs with it set to 5 — which is now a one-line edit in `.env` rather than a
  rebuild. Verified by starting the published service with exactly the variables compose sets and
  confirming a request for 100 documents came back capped at 5; a typo in a variable name would
  otherwise fall back to the default in silence.
- **`PlanState` was merged into `UploadEntryOutcome`, because** two enums described the same entry
  at two moments in its life, and the planner's `ToStore` and the report's `Stored` were the same
  statement. One vocabulary now runs from plan to response: planning says `Stored` or `Skipped`,
  and storing either confirms it or turns a `Stored` into `Failed`. `Failed` is the one value
  planning never produces, which reads as the honest asymmetry rather than a missing case.

- **Mapping lives in extension classes (`Api/Mapping/`, `Infrastructure/Documents/
  DocumentFileMappings`), not on the DTOs, because** a contract should be a shape and nothing else.
  Fourteen `From` / `To…()` methods moved out of ten DTOs into three files; the DTOs are now records
  with no behaviour.
- **No mapping library, because** AutoMapper has been commercially licensed since v15, which is the
  same ground on which FluentAssertions was already refused. Fit is poor too: about half these
  conversions are not property copies — `IndexValue` is a closed hierarchy that becomes a tagged
  JSON shape, `IFormFile` becomes a `Func<Stream>`, `UploadEntry` has factories rather than a
  source object — so a convention-based mapper would need hand-written converters for precisely the
  parts that are hard, and silent null-mapping on a rename is the failure mode you least want while
  the rule tests are still outstanding. If one is ever wanted it should be **Mapperly**: MIT,
  source-generated, compile-time errors, no reflection.
- **`DocumentDraft` / `PartDraft` were merged into `Document` / `Part`, because** the project chose
  fewer types over a compiler-enforced boundary. One type now spans both states: a document on its
  way in is written as an object initialiser, leaving `Id` as `Guid.Empty`, and `Part.ToStore` sets
  `OpenContent`; the store overwrites the system fields and nulls `OpenContent`. `Document` has no
  factory of its own — with every required property carrying a default, the initialiser says the
  same thing in the same number of lines, and only `Part` needs one to pair a name with a
  `Func<Stream>`.

  **What was given up, stated because it was argued before the change and accepted:** a client can
  now *express* an `Id`, a `StoredAt` and a `ModifiedAt` on the way in. The brief says those are the
  Archive's alone, so `DocumentsStoreService.AddAsync` overwrites all three unconditionally —
  "ignored" rather than "unrepresentable", which the upload checklist does allow. The second cost
  is `Part.OpenContent`, now a nullable `Func<Stream>?` that is set on the way in and null on the
  way out; a part reaching the store without it throws `InvalidOperationException`, where before it
  could not be written at all.
- **The on-disk document format lives on `Document` and `Part` themselves, because** the cabinets
  model already worked that way — `Cabinet` and `IndexField` carry `[JsonPropertyName]` in `Core` —
  and having the two halves of the same codebase disagree was worse than either choice alone. The
  separate `DocumentFile` / `PartFile` / `IndexValueFile` DTOs and their mappings are gone.

  The price, knowingly paid: `Core` now encodes the JSON layout of one particular store, so a
  second store would inherit the first one's format. Two further consequences found by running it:
  1. `IndexValue` is a closed hierarchy, so it needs `[JsonPolymorphic]` with a `type`
     discriminator. **Every `Type` override needs `[JsonIgnore]`** — putting it only on the
     abstract declaration silently wrote `"Type": "Text"` alongside the discriminator. Caught by
     reading the stored file, not by a test.
  2. Deserialising `IReadOnlyDictionary<string, IndexValue>` produces a dictionary with the default
     comparer, so the `OrdinalIgnoreCase` lookup that the mapping used to rebuild is lost on read.
     Nothing looks an index value up by name yet; the export marker will, and will need this fixed.
- **`PlannedDocument` and `UploadEntryResult` merged into `UploadEntry`, because** they described
  the same thing at two moments: what the rules intend for an entry, and what became of it.
  Planning produces `Stored` or `Skipped`; storing rewrites the entry with `with`, either filling in
  `DocumentId` and `Parts` or flipping it to `Failed`. A skipped entry now passes through the store
  loop untouched, which is the merge paying for itself. It also retired a name collision: the Api's
  `UploadEntryResult` no longer needs a `using` alias to talk about the domain type.

  The cost: one record with two fields that are only meaningful before storing (`Draft`) and two
  only after (`DocumentId`, `Parts`). That is the usual price of modelling a lifecycle as one type
  rather than two.

## Model and text

- **`IndexValue` is a closed record hierarchy (`Text`/`Number`/`Date`), because** a value then carries
  its own type and the rules never re-parse strings to find out what they are holding.
- **Date index fields use `DateOnly`, because** an index date has no time and no offset, so it cannot
  drift when it crosses a time zone. Number fields use `decimal` because the fixture's numbers are
  amounts.
- **`maxLength` counts UTF-16 code units (`string.Length`), because** that is what both a .NET string
  and an `nvarchar(n)` column mean by length, so the limit matches what storage can actually hold.
  Grapheme-cluster counting would be more user-meaningful but would not match storage; documented
  rather than silently chosen.
- **Cabinet and field names are matched case-insensitively (ordinal), because** the fixtures contain no
  two names differing only by case, so it cannot be ambiguous, and it spares clients from guessing.
- **`Cabinet` carries no `FindField`; the lookup sits with the code that needs it, because** a
  cabinet is a definition loaded from a file, not a service, and the one caller outside the rules
  wanted a different contract anyway. `IndexFieldRules` keeps a private `FindField` returning
  `IndexField?`, since "no such field" is one of the refusals it reports. `UploadService.StoreAsync`
  uses `First`, because by then the rules have already proved the field exists — which also retired
  the null-forgiving `!` that the shared helper forced on it.

  The honest cost: the `OrdinalIgnoreCase` comparison is now written twice rather than once, so a
  change to how names are matched is a two-file change. Accepted because the two call sites want
  genuinely different return types, and the invariant that makes the match safe — no two fields in
  a cabinet differing only by case — is enforced at load time by `Cabinet.Validate`, not by the
  lookup.
- **`InvariantGlobalization` is deliberately NOT set, because** the export rules interpret the user's
  calendar dates in their time zone, which needs real time-zone and culture data.

## Toolchain

- **Projects are named `Nemetschek.*`, because** that is the correct spelling of the company name; the
  original folder had it as `Nemetshcek`. Fixed before it reached every namespace, assembly and image.
- **The .NET Core 2.0 scaffold was replaced rather than patched, because** it targeted `netcoreapp2.0`
  with no 2.x runtime installed, so `dotnet test` could not have passed — an acceptance criterion. Its
  `Startup.cs` also used APIs removed years ago, and it had two solution files one of which pointed at
  csproj paths that did not exist. Carried forward: `.gitignore`, `.gitattributes`, `.env`, and the
  `x-logging` rotation block from the compose file.
- **MSTest with plain assertions, because** it was asked for; plain assertions because
  FluentAssertions v8 is commercially licensed and the assertions here are simple enough not to need
  a DSL. The suite was xUnit until 2026-11-02 — see "Moving the suite from xUnit to MSTest" below for
  what the move cost and the one trap it hid.
- **`TreatWarningsAsErrors`, because** a warning a reviewer sees is a warning nobody fixes. It has
  already earned its place: it caught dead code during a mutation check.
- **OpenAPI is not gated on the Development environment, because** the brief requires the description to
  be browsable while the service runs in its container.
- **No null checks on non-nullable parameters, because** `Nullable` is enabled and warnings are errors,
  so passing null to a non-nullable parameter is a compile error — a runtime guard for it is unreachable
  code that implies the contract is weaker than it is. Guards remain only where a value genuinely
  arrives unchecked at runtime: the configuration-sourced cabinets path, where an empty string is a
  realistic misconfiguration the compiler cannot see.
- **The engine is Podman 6.1.4 driven by `podman compose`, not `podman-compose`, because** the goal
  was one command — `compose up --build` — that rebuilds the image and recreates the container, and
  only `podman compose` delivers it. The two are different programs: `podman compose` hands the file
  to the real Docker Compose v2 binary, while `podman-compose` is a Python reimplementation that
  cannot do `up --build` on Windows at all. No released version of it can: ≤ 1.4.1 crash in `up`
  asking `podman ps` for `{{ index .Labels … }}`, which Podman 6 broke by turning `ps`'s `.Labels`
  from a map into a slice; ≥ 1.5.0 fixed that with `--format json` but their `is_context_git_url()`
  runs `urlparse` on the absolute context path and reads `C:\Users\…` as a git URL (scheme `c`, empty
  netloc), so `-f <dockerfile>` is never passed and the build cannot find the Dockerfile. The
  workarounds — pre-build then `--no-build`, or move the Dockerfile to the context root so Podman's
  own discovery finds it without `-f` — both bend the repository around one tool's bug on one OS, so
  neither was taken and **the repository is unchanged by any of it**. Podman was uninstalled
  wholesale in between, on the assumption that Docker was the way out, and then reinstalled once
  `podman compose` turned out to be; the detour cost the machine's images and volumes, and the twelve
  documents of the original verified round trip went with them.
- **The storage path is deliberately opaque on the host, and that is worth stating because** "where
  are the files" has a surprising answer on Windows: `/var/lib/archive` is a named volume, whose
  mountpoint is inside the Linux VM
  (`/var/lib/containers/storage/volumes/nemetschek-archive_archive-data/_data`, the rootful store
  since the default connection is `…-default-root`), and on Windows that whole filesystem is a single
  `ext4.vhdx`. There is no Windows directory of documents, and `\\wsl$` cannot read the rootful store
  because `volumes/` is `drwx------ root` while the share connects as uid 1000. This is a property of
  named volumes, not a choice of this project — and it is the right trade, because the alternative, a
  bind mount to a Windows path, would put a user-supplied cabinet name next to host filesystem
  semantics the brief explicitly warns about. `podman exec`, `podman machine ssh --username root` and
  `podman cp` are the three ways in; all three are in `README.md`.
- **XML `<summary>` blocks say what a member does and nothing more, because** the summary is what
  IntelliSense shows and what a reader scans first; rationale buried there makes it useless for that
  job. The *why* lives in a short `//` comment where it guards a future mistake, and here when durable.

## Known defects

Found but not yet fixed, so they are not mistaken for decisions.

- **`RelativePath.IsRooted` refuses any name whose second character is a colon.** The drive-letter
  check is `path.Length > 1 && path[1] == ':'`, with no test for a following separator, so a file
  legitimately named `a:b.txt` — legal on Linux, and names are untrusted user data anyway — is
  reported as "not a path inside the uploaded folder" and never stored. The fix is to require the
  separator as well: `path.Length > 2 && char.IsAsciiLetter(path[0]) && path[1] == ':' &&
  path[2] is '/' or '\\'`. That still refuses `C:\Windows\x` and `C:/x`, and lets `C:x` through as
  an ordinary name, which is harmless because a name is never used as a path. Found on 2026-10-29
  by reading the planner, not by a test — the gap it sits in is on the
  "The upload endpoint" in `DECISIONS.md` §5 checklist as "traversal / absolute / reserved names neutralised".

## Known limits of the storage

Deliberate, not oversights, and each one is where that part of the system breaks first.

- **A document is found by listing one day directory.** The id derives the day, so this never
  scans a whole cabinet, but a single day holding very many documents is the first thing to
  degrade. A real index would be the answer if it ever mattered.
- **Concurrent updates to one document are last-writer-wins.** `document.json` is replaced with a
  sibling-then-`File.Move`, which is atomic, but there is no transaction between two writers.
  Nothing updates an index value yet; this becomes real with the export marker.
- **Survives a container restart, not a host power loss.** A volume is host filesystem and the
  page cache belongs to the host kernel, so a clean stop loses nothing without `fsync`. Surviving
  a power cut would need `Flush(true)` per file and a directory fsync. The brief asks for the
  former, not the latter; worth stating rather than being caught by.
- **One writer.** Two Archive instances on one volume would be safe for documents, which never
  share a directory, but not for an index-value update to the same document.

## Cut, knowingly

Things that are wrong on purpose, so they are reversed on purpose rather than discovered.

- **A per-entry failure stays a `Failed` row inside a `200`; the status answers "was the request
  usable".** An earlier version raised the whole response to `404` when any row failed, which was
  asked for and objected to here before being resolved on 2026-10-29 by
  `ArchiveExceptionMiddleware`: a broken field rule is `400`, an unknown cabinet is `404`, and a
  report is `200`. The objection is kept because it is why the split was made — `404` is about the
  resource, not the work, and `EnsureSuccessStatusCode`-style clients drop the body that carries the
  reason.
- **Success is `200`, not `201`.** An upload creates many documents, so there is no single
  `Location` to point at, and `201` would be a claim about creation that a report full of failures
  contradicts.

## Still open — not yet decided

Listed so they are not decided by accident. Items 1–4 were settled on 2026-10-29 and have moved to
"The upload endpoint" above: multipart with relative paths; natural part order; titles keep the
extension; per-document commit with a report.

1. **Whether `date` index fields are supported.** Raised on 2026-10-29 and **not yet resolved.**
   `fixtures/cabinets.json` declares `Invoices.InvoiceDate` and `Contracts.SignedOn` as `"type":
   "date"`, and the fixtures are given and not to be edited, so `FieldType` must have a `Date` member
   or `JsonStringEnumConverter` throws and the Archive refuses to start — which is the state `HEAD`
   was in at the start of that session. The real choice is between keeping the enum member while
   dropping `IndexValue.Date` and its validator (date fields listed, writes refused by the existing
   type rule), and having the loader tolerate and drop unknown field types (which would cost the
   `An_unknown_field_type_is_refused` rule). Neither upload nor export needs dates: both use writable
   **text** fields only.

## Open debt

- **The test debt is repaid.** Writing was paused on 2026-10-28 at request and lifted on
  2026-10-30: 421 tests, every upload and export rule covered, `dotnet test` still offline with no
  containers. What is left uncovered is deliberate — the `Discard` catch arm that swallows a failed
  cleanup (no portable way to make a delete fail) and the `_ => throw` arms that cannot be reached
  while every `FieldType` and `IndexValue` case is handled.

- **The verified container evidence is from a VM that no longer exists** (2026-10-29). Podman was
  removed and reinstalled during the compose detour, so the machine, images and volumes are new and
  the original twelve-document round trip cannot be pointed at any more. `compose up --build` and both
  services answering are confirmed working on the rebuilt machine, but the full acceptance run — upload the
  sample material, `down`, `up`, same documents in the same order — is owed again on it, and so are
  the rootless-UID and `Archive__*` binding checks that `README.md` still records from before.
- **The container exits on its own, and `restart: unless-stopped` does not bring it back**
  (observed twice on 2026-10-29). `podman ps -a` shows `Exited (0)` after a quiet
  period, and the log ends with "Application is shutting down…" — so the process received a clean
  SIGTERM rather than crashing. Suspected to be the Podman machine's WSL backend being suspended by
  Windows, which would stop the container without compose seeing a failure, but that is a guess:
  nothing has been measured. It matters for the review demo, where a container that has quietly gone
  away looks identical to one that is broken. `podman start <name>` brings it straight back with its
  volume intact. Not diagnosed.
- **The `docker` CLI remains unexercised.** Every container claim rests on Podman. Nothing in the
  compose file or Dockerfile is Podman-specific and `podman compose` runs the file through the real
  Docker Compose v2 binary, which is most of the way there — but "runs under Docker" is still an
  inference, not an observation.
- **The store is uncovered too**, and is the easiest thing here to test offline: a temp directory,
  no containers, the pattern already written in `CabinetsFileLoaderTests.LoadFromJson`. Owed:
  a document round-trip with Unicode names and index values of each type, an upload that throws
  before the rename leaving nothing behind, `tmp/` swept at startup, two uploads of the same folder
  both surviving, paging boundaries, and a cabinet name that cannot be a directory being refused.
- **`HEAD` does not compile, and has not for two commits.** Verified by building each in a clean
  `git worktree`, not by reading the files:
  - `024179a` ("Add validation logic") — `FieldType` had only `Text, Number`, while `TestCabinets`
    and two test files used `FieldType.Date`. `dotnet build` fails, so `dotnet test` cannot run.
  - `f6e1b93` ("Add date field types") — restores `FieldType.Date` and has `IndexFieldRules`
    construct a `DateValueValidator`, but `DateValueValidator.cs` was **never staged**. Fails with
    `CS0246: The type or namespace name 'DateValueValidator' could not be found`.

  The working tree is green; the committed history is not. "`dotnet test` passes from a clean clone"
  is an acceptance criterion, so **the next commit must include
  `Core/Domain/IndexFields/Validation/DateValueValidator.cs`** or the hand-in fails it on checkout.
  It also means the "70 tests" figure in the earlier documents was never true of a clean clone — the
  real number was 65 at that point, and is 281 now.

## What is built, what is missing

**Built:** the solution, shared build configuration, cabinet and index-field model, the field rules
(type / `maxLength` / `writable`, and which fields may be chosen for upload or as an export marker),
the enforced dependency rules, the `cabinets.json` loader with its validation and fail-fast startup,
a runnable Api with OpenAPI and SwaggerUI, and **the upload endpoint end to end** — the
folder-to-document mapping, ordinal part ordering, untrusted-path handling, per-document commit and
the one-row-per-entry report. 411 passing tests across six test projects.

Also built: **durable storage** — documents are directories of files on a mounted volume, committed
by one atomic rename, with `tmp/` swept at startup — `GET /cabinets/{name}/documents`, a paged read
capped by `Archive:MaxPageSize`, **the containers** (`compose up --build`, data surviving `down`
and `up`), and **the logs** (one line per request, per stored document and per
refusal, with a storage failure at `Error`).

**Missing**, after the Export service was built later on 2026-10-30:

| Gap | Brief | Note |
|---|---|---|
| **The web client** | Priority 4 | Not started. Both services have browsable OpenAPI, which the brief accepts as the fallback demo |

Everything else the brief asks of the two services is built. The export rules that were listed as
owed are done and tested, the three Archive routes they needed exist, `GET /cabinets` now answers
the schema a client needs, and `compose up` starts both services, the Export started after the
Archive.

Against "Things your system must cope with":

| Item | State |
|---|---|
| Treat everything a user uploads as untrusted | Addressed — `RelativePath` refuses rooted paths, `.`, `..` and control characters; names are never used as paths; body size and part count are bounded |
| Names may contain any Unicode character | Addressed — verified that `Договор София.txt` and `Фактури Q1` round-trip unchanged, and that stored content is byte-identical to the source by SHA-256 |
| Data survives restarting the containers | Addressed — documents are files on a named volume, and the service holds nothing in memory. Verified by uploading, stopping the service, starting it cold and reading the same 12 documents with the same ids back |
| An operation must not leave anything behind | Addressed on the upload side — content streams are opened and disposed in one `await using`, a failed document's staging directory is deleted, and `tmp/` is swept at startup. Verified: a planted staging directory is gone after the next start |
| Export changes data: what remains if it fails halfway | Addressed — an upload commits per document by atomic rename; an export builds the whole archive into a temp file before sending, so a failure is a status with nothing delivered rather than a truncated `200`. Documents already marked stay marked, which is deliberate and written down in "The Export service" in `DECISIONS.md` |
| Exports larger than memory | Addressed, and **measured** on 2026-10-30: a 200 MiB part exported through a container whose memory went from 60 MB to a 72 MB peak and back to 62 MB, producing a valid 200 MiB archive. Part content is streamed socket → zip entry → temp file in 80 KiB copies, and the response streams from that file. What does scale with an export is bounded by the **number** of entries, not their size: the zip's central directory, the duplicate-name table, and one page of document metadata. The upload endpoint still needs the whole body to arrive first, which is named in "The upload endpoint" |
| Configurable maximum page size | Addressed — `Archive:MaxPageSize`, default 50, enforced on every read and the cap the Export pages against. The date-range filter it is combined with is built and tested |
| Time zones | **Not addressed, on purpose** — the time-zone parameter was removed on 2026-10-30 because the client and the services share a zone for the demo. Dates are UTC calendar days and the marker carries the UTC date, so a caller in another zone sees UTC's day boundaries. The measurement of what that costs, and how to reinstate it, are under "Removed: the time zone parameter" |
| Services may start in any order | Addressed in the service, not in compose — an export attempted while the Archive is away returns `502` naming it unreachable, and the next one succeeds without a restart. The `service_healthy` gate that used to hold the Export back was removed with the health checks on 2026-10-31 |
| Different hosts for client and services | Addressed — a CORS policy on both services, from `Archive:AllowedOrigins` / `Export:AllowedOrigins`, empty meaning any origin. `UseCors` runs before the error shapes so a refusal is readable cross-origin, and the Export exposes `Content-Disposition` for the download's name |
| The logs of every service are visible | Addressed for both — see "What the service logs". `compose logs` shows startup, one line per request, one per stored document, and refusals |
| Failures reach callers in one documented shape | Partly — a rule refusal and an unknown cabinet are `ProblemDetails` from `ArchiveExceptionMiddleware`, but a missing form field is ASP.NET's own `ValidationProblemDetails`. Two shapes, both documented in `README.md`; reconciling them is open |
| Fields have types and limits | Addressed — `IndexFieldRules`, enforced on the upload path |

## AI notes

Where the AI was wrong, and how it was caught.

- **Listing the sample material** crashed on the Cyrillic entry names — Windows console defaulting to
  cp1252. Caught immediately by the traceback; re-run with UTF-8 output. Worth recording because it is
  the same class of bug the system itself has to avoid: the fixture names are chosen to break careless
  encoding handling.
- **The plan contradicted the chosen option.** "Upgrade in place" was selected, but the plan then said
  to collapse and delete the whole scaffold directory. The safety classifier blocked the `rm -rf` and
  the contradiction surfaced; the decision was referred back rather than assumed. The tree was untracked,
  so the deletion would have been unrecoverable.
- **A package version was pinned from memory** (`Swashbuckle.AspNetCore.SwaggerUI 7.2.0`) rather than
  resolved. It happened to be valid, confirmed by an actual restore and build — but the right move is to
  let `dotnet add package` resolve, or verify by building, never to assume.
- **The first mutation check did not compile.** Replacing a condition with `if (false)` tripped
  `TreatWarningsAsErrors` on CS0162 instead of running the tests. Redone by deleting the block, which
  then failed exactly the two tests that assert the rule.
- **`dotnet add package` pulled a .NET 10 assembly into a net9.0 project.**
  `Microsoft.Extensions.DependencyInjection.Abstractions` resolved to 10.0.14 by default, which would
  sit alongside the ASP.NET Core 9 shared framework. Caught by reading the tool's output instead of
  trusting it, and pinned to 9.0.4. Keep `Microsoft.Extensions.*` in step with the target framework.
- **A mutation check found a rule with no test at all.** Deleting the `[Required]` attribute from
  `FieldDto.Label` broke **nothing** — 65 of 65 still passed, which meant the "a field must have a
  label" rule had never been covered. Writing the rule and believing it was tested is precisely the
  failure the brief's "every rule has a test that fails when that rule is broken" is aimed at. Added
  tests for a field with no label, a field with no name, and a cabinet with no `fields` property, then
  re-ran the same mutation and confirmed it now fails. Lesson: mutate every rule, not a sample.
- **A blanket find-and-replace over exception types hit a case that differed.** Swapping
  `CabinetsFileException` for `InvalidDataException` across the test file also rewrote the missing-file
  test, which should have become `FileNotFoundException`. The compiler caught it (`InvalidDataException`
  has no `FilePath`). Second instance of the same class of mistake as the path rewrite below.
- **The committed tree did not compile, and the fix on offer was to delete the tests.** `024179a` had
  `FieldType` reduced to `Text, Number` while `TestCabinets` and two test files still used
  `FieldType.Date` and `IndexValue.Date` — so `dotnet build` failed and `dotnet test`, an acceptance
  criterion, could not run at all. Part-way through the next session those three files were rewritten
  to strip every `Date` reference, which makes the build go green by deleting
  `Write_to_a_field_the_cabinet_does_not_have_is_refused`, `Date_value_in_a_date_field_is_allowed`
  and two `[InlineData]` rows. Caught by reading the diff rather than the build status: the tests had
  been *removed*, not fixed, and the fixture still declares two `date` fields. Restored, then the
  underlying question (do we support dates at all?) was put back to a human instead of being settled
  by whichever side of the compile error was easier to change. A green build is not evidence.
- **A conclusion about `HEAD` was drawn from the working tree and was briefly wrong.** After the
  repair, `git show HEAD:…/FieldType.cs` came back *with* `Date`, which looked like proof that the
  original diagnosis had been mistaken. It was not: `HEAD` had moved — `f6e1b93` was committed
  mid-session — so the two readings were of different commits. Settled by `git reflog` and by
  building each commit in a throwaway `git worktree` instead of reasoning about file contents. That
  check is also what exposed `f6e1b93`'s unstaged `DateValueValidator.cs`. Lesson: "does this commit
  build" is a question to answer by building that commit.
- **A rule with no test, found by the compiler rather than by a test.** `IndexField.Writable` was the
  one property with no `[JsonPropertyName]`. There are no serialiser options, so matching is
  case-sensitive, `"writable": true` in `cabinets.json` bound to nothing, and **every field loaded as
  read-only** — which would have refused every upload. Nothing failed, because the only test that
  compares loaded fields against `TestCabinets` was itself passing for an unrelated reason until the
  `Date` restoration made it run. Same lesson as the `[Required]` case below: the attribute was
  decoration to the eye and load-bearing in fact.
- **A refactor deleted the only operator-visible record of a failure, and nothing noticed.**
  `UploadFailureStatusMiddleware` logged one `LogWarning` per failed entry, with a comment saying in
  as many words that "the rules cannot log — Core has no dependencies — so this is the only place a
  failure becomes visible to an operator". Replacing it with `ArchiveExceptionMiddleware` was the
  right call for the status-code decision, but the logging went with it and was not re-homed. The
  build stayed green and every test passed, because no test asserted that anything is ever logged —
  the deleted comment was the only trace that the behaviour had ever been deliberate. Found by
  asking why a running container logs nothing after startup, not by any check in the repository.
  Lessons: when a type is retired, account for each thing it did rather than for the one that
  prompted the change; and a behaviour with no test is a behaviour a refactor may silently drop —
  the replacement now has one. The replacement deliberately does **not** log the entry name the old
  middleware did, since that is user-supplied text.
- **A merge silently deleted two live types, and only the compiler noticed.** The merge at `6f0ae8f`
  took the deleting side of a 28-file commit from `origin/main`, so `DocumentPage` and
  `FieldViolation` vanished while every other path in that commit was an old location the Core
  restructure had legitimately superseded. Nothing distinguished the two cases at merge time. Two
  lessons: after a merge that deletes files, `dotnet build` is the cheapest check there is, and the
  way to tell a superseded path from a lost one is to look for the live counterpart of each —
  mechanically, not by recognising names. Recovered from `d1fc81b` rather than retyped, so the
  restored files are the reviewed originals.
- **The export streamed its zip straight into the response, and only a test caught that it could
  not.** `ZipArchive` writes synchronously; ASP.NET Core refuses synchronous writes to the response
  body. The design read fine, the unit tests passed against a `MemoryStream`, and the endpoint test
  failed with `InvalidOperationException: Synchronous operations are disallowed` — which would
  otherwise have been a 500 on the first real export in the container, after the containers had been
  declared working. Two lessons. A test that boots the real host catches a class of mistake no unit
  test can, which is the argument for having written those. And the diagnosis needed the host's own
  log: the `ProblemDetails` body said only "An error occurred", so a capturing `ILoggerProvider` was
  wired into the test factory to read the real exception, then removed. The fix — build into a temp
  file, then send — turned out to be better than the original design, because it makes a failure a
  status rather than a truncated `200`.
- **A paging loop that tested the condition after the fetch asked for a page past the end.**
  `ReadDocumentsAsync` had become `while (read < page.Total && page.Documents.Count > 0)` with the
  next fetch at the bottom of the body, so every read issued one request more than it owed: the
  condition was re-tested against the *new* page's total, after that page had already been asked
  for. Two of its own tests say otherwise — `One_request_is_enough_when_the_first_page_holds_
  everything` saw two requests, and `No_request_is_made_for_a_page_that_is_not_owed` saw three — so
  the rule was covered; the suite went from green to red mid-session when the file changed. The
  loop now yields, then breaks on `read >= page.Total`, and keeps `Documents.Count > 0` as the
  guard for a total the Archive cannot deliver. Against the Archive the wasted request is cheap;
  against the review demo it is one line in the log per export that no one can account for.

- **Paging the directory walk before counting it made `Total` the page size.** A cleanup moved
  `ListAsync`'s `.Skip(skip).Take(take)` from the read loop up into the enumeration, so the list it
  counted held one page rather than the whole window. `DocumentPage.Total` then answered "5" for a
  cabinet of 24. Seven tests said so immediately, which is the system working — but the consequence
  is worth recording because it lands squarely on the brief's demo: with `Archive:MaxPageSize` set
  to 5, the Export's paging loop breaks on `read >= page.Total` after the first page, so an export
  would have quietly contained the first five documents and nothing else. The count and the page are
  two different questions of the same walk: materialise it once, count all of it, read a slice.

- **A hand cleanup of the store silently disabled the instant filter, and a green suite said nothing.**
  Rewriting `DocumentDirectories` to name its locals changed `StoredAtOf(directory)` to
  `StoredAtOf(day)`. A day directory is named `2026-03-01`, which the parser cannot read as an instant,
  so it returned `null` and the `storedAt.Includes(...)` check never fired — the window degraded from
  instant precision to whole days. All 421 tests passed, because every range test went through
  `CalendarRange.Of(day, day)`, which is day-aligned by construction and cannot tell the two apart.
  The rule was only reachable by building a `StoredRange` directly, which nothing did. Fixed the one
  token and added `A_window_that_starts_inside_a_day_excludes_what_was_stored_before_it`, verified by
  reinstating the mutation and watching it fail. Lesson: a filter tested only through a coarser
  constructor is an untested filter, however many tests pass through it.
- **A bulk path rewrite missed the case that mattered.** After the fixtures moved to `fixtures/`, a
  `sed` over `Task/fixtures/` fixed the documents and `appsettings` but silently skipped
  `RepositoryLayout.Path("Task", "fixtures", ...)`, where the segments are separate C# string
  arguments rather than a slash-delimited path. Caught because the tests failed, not because the
  rewrite reported anything — a textual find-and-replace cannot see a path that is not spelled as one.

## Time log

Rough, as the brief asks for, and grouped per session rather than per prompt. ~34.6 h in total.

| Date | Time | What |
|---|---|---|
| 2026-10-28 | ~1.5 h | Read the brief and the fixtures, wrote the upload requirements analysis, assessed the .NET Core 2.0 scaffold and agreed the architecture. Rebuilt the solution on .NET 9 with the layered split and the dependency rules enforced by tests. |
| 2026-10-28 | ~1.3 h | The `cabinets.json` loader: validation moved onto the DTOs as DataAnnotations, which removed eleven hand-written guard methods; SDK exception types instead of a custom one; fail-fast startup. |
| 2026-10-28 | ~1.5 h | Split one test project into one per production project plus a shared `TestSupport`, wrote the architecture tests that enforce the layering, and closed three rules that had no test. Dockerfile, `.dockerignore` and the first compose file — unverified, no engine available yet. |
| 2026-10-29 | ~3.0 h | Built the upload path end to end: document model, `IDocumentStore`, the folder-to-document planner, `RelativePath`, `UploadService`, the multipart contracts and `CabinetsController`. Verified by hand against the real sample material and every refusal path. Repaired a non-compiling `HEAD` on the way (restored `FieldType.Date` and four deleted tests, fixed a missing `[JsonPropertyName]` that had made every field read-only). |
| 2026-10-29 | ~2.0 h | Settled storage — files on a volume, no database — and built it: the directory-as-index layout, `document.json`, atomic commit by rename, `tmp/` swept at startup, the required `Archive:StoragePath`, a storage readiness check, and the paged read. Verified that a cold restart keeps every document and that two uploads of the same folder coexist. |
| 2026-10-29 | ~2.5 h | Several rounds of consolidation at review pace: merged `DocumentDraft`/`PartDraft` into `Document`/`Part`, `PlannedDocument` + `UploadEntryResult` into `UploadEntry`, folded `ArchiveStorageLayout` into the store, replaced the status middleware with `ArchiveExceptionMiddleware`, restructured `Core` by entity and `Contracts/` by entity, and refused AutoMapper on licence and fit. |
| 2026-10-29 | ~3.5 h | Containers, the hard way. Diagnosed `podman-compose`'s two Windows bugs, uninstalled and reinstalled Podman, established that `podman compose` (the Docker Compose v2 binary) works as the single command, and traced where the named volume actually lives. **First successful build and run of the image**, with the upload round trip and a destroy/recreate both verified. |
| 2026-10-29 | ~1.7 h | Container-only configuration: every setting through compose, `.env` for the tunables, `no-new-privileges`, `stop_grace_period`, and `appsettings.json` documenting the full set. Verified each `Archive__*` name binds by running with exactly the variables compose sets. |
| 2026-10-30 | ~1.0 h | Diagnosed "nothing is logged": the pipe was fine, but `Microsoft.AspNetCore` was pinned to `Warning` and the solution held three `ILogger` calls, two of them at startup. Added HTTP logging (one line per request, no bodies, `/health` excluded) and a store failure at `Error`. Traced the missing failure log to the `UploadFailureStatusMiddleware` retirement and found three documented behaviours the code had stopped doing. |
| 2026-10-30 | ~1.6 h | Removed `TestSupport` and took the suite from 70 tests to 281 (98.9% of lines): the folder-to-document rules, `UploadService` including partial failure, the store against a temp directory, the mappings, the paging arithmetic, the status middleware, and endpoint tests that boot the real service. Rebuilt the fixture-drift guard the removal would have destroyed. |
| 2026-10-30 | ~0.5 h | Created `Nemetschek.Export` — six projects, same shape as the Archive — and extended the architecture tests with the Export's layering rules so the boundary is enforced from the first commit. |
| 2026-10-30 | ~3.5 h | Implemented the Export service and the three Archive routes it needs: the date-range filter, a part's content streamed, and index values written back. Zip layout, the duplicate-title rule, the marker, the paging client with its own DTOs, the `502`/`400` split, and compose running both services. Ran the brief's round trip — sample material twice, exported, unpacked, 15 of 16 entries byte-identical. An endpoint test caught that `ZipArchive` cannot stream into a response body. |
| 2026-10-30 | ~1.3 h | Review pass over the Export at request: `WriteAsync` returns the archive as a stream, the marker became one batch request after the zip is closed (24 `PATCH`es to 1), the per-document route was removed, the helpers folded into `ExportService`, and the refusal collection moved into `IndexFieldRules`. |
| 2026-10-30 | ~1.4 h | Split `DocumentsStoreService` (379 lines, 21 members) into the layout utils, the storage root and the metadata file, leaving the port's five operations. Removed `.env` and `docker-compose.override.yml` for one literal compose file — keeping `name:`, without which the Archive would have come up against an empty volume. |
| 2026-10-30 | ~1.1 h | Answered review questions with evidence rather than opinion: why `folders` is not `emptyFolders`, why `Prepare` is needed when the Dockerfile makes the directory, whether the export loads files into memory (measured: 200 MiB through a 12 MB rise), whether the time zone can go (measured: 24 / 35 / 0 documents for the same request in three zones), and why an export of `Status` returns `400` (the marker is 22 characters; the field holds 12). |
| 2026-10-30 | ~1.0 h | Two comment sweeps — 168 lines out of the test projects, then 95 out of all 114 source files — rehousing the one rule that lived only in a comment. Reorganised the top-level documents into `docs/` and condensed them. |
| 2026-10-30 | ~0.5 h | Reorganised the documents: the brief and the decision log moved to `TASK.md` and `DECISIONS.md`, with all 58 cross-references rewritten and every markdown link checked to resolve. `AGENTS.md` and `CLAUDE.md` stayed at the root deliberately — both are discovered there by convention, and the brief tests the instructions by handing an agent an unseen change, which fails if `AGENTS.md` is not where an agent looks. Condensed as well as moved: the decision log lost its seven struck-through superseded blocks (collapsed into the decision that stands, keeping the reasoning that still decides things) and its 64 one-row-per-prompt time entries became 16 grouped per session, with the per-day totals unchanged at 4.3 / 12.7 / 11.4 h. `AGENTS.md`'s verify section was rewritten around the round trip, and "The upload endpoint" in `DECISIONS.md` stopped claiming the upload rules have no tests — its checklist is now ticked, naming the test files. 1348 lines of decisions became 1240, 195 of upload notes became 109 |
| 2026-10-31 | ~0.4 h | Deleted the `docs/` folder. Flagged first that two of its four files were not reiterations: `task.md` is the given brief every other document cites, and `decisions.md` is a named hand-in deliverable — so both moved back to the root as `TASK.md` and `DECISIONS.md`, which is where the brief names them, and the folder went with the two requirement notes. Before deleting those, folded the three things recorded only in them into `DECISIONS.md`: the sample-material mapping tree the upload rules are stated about, the three streaming hops with the 200 MiB measurement, and the export's three non-promises (no resume, no export history, a document stored mid-export may or may not appear). Rewrote every reference, deduplicated the README's documentation map, and checked every markdown link resolves. Five documents at the root, 421 tests, 0 warnings |
| 2026-10-31 | ~0.2 h | `IDocumentStore.SetIndexValuesAsync` returns `Task` instead of `Task<Document?>`. Its only caller used the result as a null check, and `DocumentsService` already resolves every id before writing, so the count it answers with is simply the id count. The five store tests that asserted on the returned document now read it back with `FindAsync` instead — a better assertion, since it proves the change reached disk rather than that an in-memory copy was built. The missing-document test now also checks a sibling document was left alone and nothing was staged. 421 tests, 0 warnings |
| 2026-10-31 | ~0.4 h | Made the store report its own failures: `FindAsync`, `OpenPartAsync` and `SetIndexValuesAsync` throw `DocumentNotFoundException` instead of returning `null`, with the private directory lookup throwing once so all three lose their `is not { } directory` checks. `DocumentsService` dropped every conditional with them — `OpenPartAsync` is now a one-line delegation and the batch write is two plain loops. Found on the way that the batch's two passes had been merged into one, which had quietly dropped "resolve every id before writing any"; restored them, so a bad id still leaves the good documents untouched. Five tests that asserted `null` now assert the refusal, one of them also checking a sibling document was left alone and nothing staged. 421 tests, 0 warnings; the endpoint tests confirm the routes still answer `404` rather than `500` |
| 2026-10-31 | ~0.3 h | Followed a hand cleanup of `DocumentsStoreService` through the tests. `Document.ToStore` was gone, so the 14 call sites across four test files became object initialisers, matching what `UploadPlanningService` now does; the three that then reached for `with { Id = ... }` set `Id` in the initialiser instead. Reading the diff rather than the build status found the cleanup had also turned `StoredAtOf(directory)` into `StoredAtOf(day)`, which disabled the instant filter without failing a single test — fixed, and covered by a window that starts midday. 422 tests, 0 warnings |
| 2026-10-31 | ~0.2 h | Fixed `ArchiveHttpClient.ReadDocumentsAsync`, which was asking for one page past the end: the loop tested `read < page.Total` only after fetching, so the condition was judged against a page already requested. It now breaks on `read >= page.Total` after yielding, keeping `Documents.Count > 0` for a total the Archive cannot deliver. Found by running the whole suite rather than the project being worked on — two Export tests had turned red mid-session. 422 tests, 0 warnings |
| 2026-10-31 | ~0.4 h | Removed health checking from both services on instruction: the two endpoints each, the three checks behind them, the compose `healthcheck` blocks, the `service_healthy` gate, and the `apt-get` layer that existed only to put `curl` in the runtime images for it. `DocumentStorageRoot.CanWrite` went too — the storage probe was its only caller. 11 tests removed with the behaviour they pinned; 411 remain, 0 warnings. Recorded what the gating bought, since the brief's "services may start in any order" is now answered by the Export's `502` alone. |
| 2026-11-01 | ~0.6 h | Built `GET /cabinets`, the last Archive gap: every cabinet with its fields, each answering `type`, `maxLength`, `writable` and `choosable`. `choosable` exists so a client's upload-field and marker-field pickers do not restate "writable and text" — `Cabinet.WritableTextFields` put on the wire. Then split the five-action `CabinetsController` into one controller per entity, routes unchanged; the existing endpoint tests passing against the same URLs is the proof. 23 new tests (mapping and endpoint), and a mutation that makes every field choosable fails 7 of them. 434 tests, 0 warnings |
| 2026-11-01 | ~0.5 h | CORS on both services, closing the last non-client gap: one comma-separated `AllowedOrigins` setting each, empty meaning any origin, `UseCors` placed before the error shapes so a refusal stays readable cross-origin, and `Content-Disposition` exposed on the Export so the download's name is not restated in the client. 10 endpoint tests; removing `app.UseCors()` fails 4 of 5 per service. Caught that exact removal for real: a cleanup pass stripped the call along with its comment, which would have left `AddCors` registered and never applied. 444 tests, 0 warnings |
| 2026-11-01 | ~0.5 h | Made both services runnable from source with nothing supplied: every setting now appears in `appsettings.json` as a typed placeholder, and the live values sit in `appsettings.Development.json`, which keeps the container's fail-fast protection intact. A blank required setting is now treated as missing, or the placeholder would have degraded the error from one naming the setting to an `ArgumentException` about an empty string; `RequiredSettingTests` pins the message on both services. Verified by running the pair and calling `GET /cabinets`, and by confirming Production still refuses to start. Also fixed `RepositoryLayout`, whose csproj scan counted a git worktree's copy of every project and threw in its static initializer. 450 tests, 0 warnings |
| 2026-11-02 | ~0.3 h | Removed `Cabinet.WritableTextFields`. It had one caller — the `choosable` flag on `GET /cabinets` — and restated a rule `IndexFieldRules.ValidateChosenTextField` already owned, so the mapping now asks that rule directly and the flag cannot drift from what actually refuses an upload or an export. Mutating the flag's source to plain `writable` fails 5 tests. Took the three `WebApplicationFactory` knobs with it, left unused when the `RequiredSettingTests` were deleted, and corrected the settings decision that claimed those tests still pinned the blank-is-missing message. 444 tests, 0 warnings |
| 2026-11-02 | ~0.1 h | `DocumentNotFoundException` became two constructors instead of one with an optional `Guid? partId` and a ternary over it. Each overload carries one literal message, so the wrong one cannot be chosen and neither call site can pass a null part id. Both overloads are already reached by the store tests — the missing document through the directory lookup, the missing part through the file lookup. 444 tests, 0 warnings |
| 2026-11-02 | ~0.2 h | Answered whether `SetIndexValuesAsync`'s `merged` dictionary is redundant: it is not, and both of its jobs are pinned. Assigning `values` straight onto the document fails `Setting_one_value_leaves_the_others_alone`, because the caller names only the fields it is changing; dropping just the `OrdinalIgnoreCase` comparer fails `A_value_is_matched_to_its_field_whatever_case_it_is_given_in`, because System.Text.Json rebuilds the dictionary case-sensitively on read. Both proved by mutation. Found seven tests already red on the way: `ListAsync` had been paged before it was counted, making `Total` the page size — restored, since at the demo's page size of 5 it would have cut every export to five documents. 444 tests, 0 warnings |
| 2026-11-02 | ~0.4 h | Removed `DocumentStorageRoot`: `Prepare` became a private `PrepareStorage` in `DocumentsServiceCollectionExtensions`, its only caller, and `DocumentsStoreService` now takes the resolved path as a `string` — one fewer DI registration and one fewer hop to the five places it composes a path. The type's tests went with it, their three behaviours already covered by `ServiceRegistrationTests` on the registration side; the test that read `.Path` to prove relative resolution now asserts the prepared directories exist at the resolved location. Also dropped the redundant intermediate `part` record in `WritePartsAsync` — a `Guid` local and one `with` where there were two. 439 tests, 0 warnings |
| 2026-11-02 | ~0.3 h | Rewrote every configuration read in both `Program.cs` files to one shape — `Configuration[key] ?? throw` — and removed the three `GetValue` defaults, so the eight settings are stated once, in `appsettings.json`, rather than half in code. Numeric values are parsed with `InvariantCulture`. Verified both ends: Development still starts with nothing supplied and the page cap binds (a request for 999 came back capped at 50), and Production still refuses to start. Recorded the cost — with placeholders present the `??` is unreachable for blank keys, so a blank path is now refused by the DI extension with a message naming the parameter rather than the setting. 439 tests, 0 warnings |
| 2026-11-02 | ~0.7 h | Moved `ArchiveHttpClient` from `Export.Infrastructure` into `Export.Core/Clients/`, with `Interfaces/`, `Requests/` and `Responses/` beside it. Checked first that `Core`'s one-entry package allowlist still holds — `HttpClient` and `System.Net.Http.Json` are shared-framework, so no new reference. The response DTOs were renamed to match the folder and the anonymous PATCH body became a named `IndexValuesBatchRequest`, with the Api test that asserts the exact body proving the serialisation is unchanged. `AddArchiveClient` stayed in `Infrastructure` and finally has tests — the project was left empty by the move and cannot be deleted, since an architecture test requires six projects per service. Two of my own new assertions were wrong and the suite said so: `archive-api:8080` *is* an absolute URI (scheme `archive-api`), and a typed client is registered under the service type's name, not the implementation's. 448 tests, 0 warnings |
| 2026-11-02 | ~0.1 h | Analysis only: whether `ArchiveDocument` and `ArchivePart` should join `Clients/Responses/`. They should not — they are the port's contract, built by three HTTP-free test fakes, and the nullable-to-non-nullable step between response and domain is what the second type is for. Recorded the naming flaw the question exposed instead, and offered the rename to `Document`/`Part`. No code change |
| 2026-11-02 | ~0.3 h | Collapsed the Export's two-types-and-a-mapping into one type each: `ArchiveDocumentResponse` and `ArchivePartResponse` deleted, the client deserialising straight into `ArchiveDocument` / `ArchivePart`, which now carry `[JsonPropertyName]` and validate themselves through `IJsonOnDeserialized` — the Archive's own `Document` pattern, which is what made my defence of the mapping wrong the turn before. The `InvalidDataException` that keeps an unusable answer a `502` survives, proved by mutation. 448 tests, 0 warnings |
| 2026-11-02 | ~0.5 h | Made the test projects mirror the projects they test: 26 files moved so each sits at its subject's path, namespaces following. `CalendarRangeTests` was under `Documents/` for a type in `Validation/Documents/`, `IndexFieldRulesTests` under `Domain/` for rules in `Validation/`, and the four `Contracts` tests were flat against a tree three deep. `Endpoints/` became `Controllers/`, the host fixtures moved to the root beside the `Program.cs` tests, and `DomainValidationTests` split in two so each half mirrors its own domain folder. Same 448 tests, 0 warnings — a structural move should change the count by nothing, and it did not |
| 2026-11-02 | ~0.4 h | Split `ProjectDependencyTests` so each service enforces its own layering: the Archive's copy no longer holds the Export's project constants, and the Export has the mirror in its own `Api.Tests/Architecture/`. `RepositoryLayout` is duplicated there on purpose — a shared helper project was removed long ago and a cross-service reference is what these rules forbid. Added the guard that stops a prefix filter matching nothing and making six loop rules vacuous, then mutation-checked both directions: a forbidden package in `Export.Core` fails only the Export's tests, and the reverse. 452 tests, 0 warnings |
| 2026-11-02 | ~0.3 h | Gathered every non-test class in the test projects into its project's `Utils/` — the two `WebApplicationFactory` hosts, both `RepositoryLayout` copies, both `RepositoryFixtures`, `TestCabinets` (with its own test, which follows its subject) and `FakeArchive`. Removed the ten CORS endpoint tests on instruction, as framework behaviour rather than this system's rules; recorded what that gives up, since those tests are what caught `app.UseCors()` going missing the day before. 442 tests, 0 warnings |
| 2026-11-02 | ~0.9 h | Converted all six test projects from xUnit to MSTest 3.11.1 at request — 37 files, no production code touched. Most of it was mechanical, but one difference is silent rather than loud: xUnit's `Assert.Equal` compares two `IEnumerable`s element by element and MSTest's `Assert.AreEqual` compiles on the same arguments and compares references, so a plain rename would have left ~60 sequence assertions green and worthless. Those are now `CollectionAssert.AreEqual` over arrays, found by scanning for a collection-expression expected argument and then re-reading every remaining `AreEqual` by hand. Argument order reverses for `ContainsSingle`/`Contains`/`DoesNotContain`, `Throws` and `ThrowsExactly` swap strictness, `IsInstanceOfType` returns `void` so eleven call sites take its `out` parameter, and `Assert.All` has no equivalent. Verified by mutation rather than by the suite going green: reversing the two `OrderBy`s in `UploadPlanningService` fails 8 of the converted collection assertions, and breaking the export marker's format fails the converted `Assert.All`. 292 tests, 0 warnings |
| 2026-11-02 | ~1.1 h | Reshaped the suite around MSTest's own vocabulary and Moq, in four passes on instruction. `[TestInitialize]`/`[TestCleanup]` replaced the xUnit-shaped constructor and `IDisposable` in nine classes; the `private Service()` factories became fields built in `BeforeEach`; `CapturingProblemDetailsService`, two `RecordingLogger`s and `FakeArchive` became `Mock<T>`s with capture lists; and `DocumentsStoreReadWriteTests` merged into `DocumentsStoreServiceTests` — 37 tests spliced verbatim from both files so the merge could not reword an assertion. Removed every test that boots a `WebApplicationFactory` host: four Archive endpoint files, the Export's one, both hosts and the `Mvc.Testing` reference, 56 tests. Recorded what that costs rather than letting the number speak — `Program.cs` and the controllers are now executed by nothing, so the statuses, error shapes, page cap and CORS are unverified by `dotnet test` and Api coverage drops from 99% to about 56%. Mutation-checked the rewrites rather than trusting green: the mocked logger catches a level change in both the middleware and the store, the mocked Archive catches a marker-format change in 3 tests, and a status-code change fails 2. Also renamed every `_`-prefixed private field to plain lowercase, including one in `CabinetCatalog`. 225 tests, 0 warnings |
