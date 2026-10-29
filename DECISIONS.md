# Decisions

Each decision in the form "A, because B". Updated as the work progresses.

## Architecture

- **Layered, three production projects (`Api` / `Core` / `Infrastructure`), because** it is conventional,
  small enough to explain under live questioning, and gives one obvious home per concern. A separate
  `Application` layer was rejected: at four or five operations it would be pass-through ceremony.
- **One test project per production project, plus a `TestSupport` library, because** a single test project
  hides which layer a failure belongs to and lets a test quietly depend on a project it has no business
  knowing. `TestSupport` holds only what two test projects genuinely share — `RepositoryLayout` and
  `TestCabinets` — and carries no test-framework dependency of its own. Both rules are enforced by
  `Each_test_project_references_the_project_it_tests` and `No_production_project_references_a_test_project`.
- **The architecture tests live with the Api tests, because** the Api sits at the top of the dependency
  chain and is the only project that can see every other one.
- **`Core` declares the ports and references nothing, because** the brief requires `dotnet test` to pass
  from a clean clone with no network and no containers. With `IDocumentStore`/`IContentStore`/
  `ICabinetCatalog` declared in `Core/Ports/` and implemented in `Infrastructure`, rule tests use
  in-memory fakes instead of fighting a real store. Textbook layering (Business → Data) would invert
  this and drag storage into every rule test.
- **`Core` is folded by feature, not by type-kind (`Domain/Cabinets/`, `Domain/IndexFields/`, not
  `Services/`, `Models/`), because** the brief says an AI agent will be handed a small unseen change and
  judged on `AGENTS.md`; colocation means a behaviour change lands in one folder.
- **`Core/Domain/` groups the entity folders, with `Ports/` left at the root, because** a port is a
  boundary interface, not a domain entity — keeping it outside `Domain/` makes the "rules in, adapters
  out" split visible from the folder tree alone.
- **Domain folder names are plural (`Cabinets/`, not `Cabinet/`), because** a folder whose name matches
  the type inside it produces a namespace and a type with the same name, which the compiler rejects as
  ambiguous. Plural names are also the standard .NET convention.
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
  (`Nemetschek.Archive.Api.Tests/Architecture/ProjectDependencyTests.cs`), because** the layering is what
  makes the rest of the brief achievable, and a convention nothing checks is a convention that decays.
  The tests read the real csproj files and assert: Core declares no package and no project references,
  Infrastructure references only Core, Api references only Core and Infrastructure, dependencies point
  inward only, no Archive project references an Export project, and every project is in the solution.
  Verified to bite: adding a `PackageReference` to Core fails `Core_declares_no_package_references`.
- **Validation returns a `FieldViolation?` rather than throwing, because** the upload report must list
  many refusals at once — "a partial failure is never silent" — so a refusal has to be a value.
- **Violation reasons are complete user-facing sentences naming the field and the limit, because** the
  client is forbidden from restating server rules, so the server must phrase them.

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
- **The DTOs stay separate from the domain `Cabinet` / `IndexField`, because** merging them would cost
  three things that are each load-bearing:
  1. `Core` would stop being dependency-free — the serialisation and validation attributes would move
     into it, so the domain model would know the on-disk JSON format. `Core_declares_no_package_references`
     enforces the opposite today.
  2. `FieldDto.Type` is a `string?` precisely so an invalid value like `"boolean"` can be *reported*;
     `IndexField.Type` is a parsed `FieldType`. Merged, the domain would carry unparsed strings and
     `IndexFieldRules` would re-parse on every call — the thing `IndexValue` exists to avoid.
  3. A `Cabinet` is valid by construction: non-empty name, no duplicate fields, every type parsed.
     Merged, every consumer would either re-check or trust a null-forgiving `!`.

  The price is three small DTO classes and one `ToCabinet()` method. The fair counter-argument — that it
  is ~137 lines of DTO for a 30-line fixture that never changes — was weighed and rejected.
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

## Containers and health

- **Liveness and readiness are separate endpoints, because** they answer different questions and a
  dependent service needs the second one. `/health` runs no checks and only says the process is up;
  `/health/ready` checks the cabinet catalog resolved. The brief notes services may start in any order
  and be briefly unavailable, so compose and the future Export service gate on `/health/ready`.
  Honest caveat: the readiness check's unhealthy branch is currently unreachable, because the loader
  already refuses to start on a cabinet set that is empty.
- **`cabinets.json` is mounted read-only, not copied into the image, because** the brief requires the
  same image to run with different settings, and nothing about a particular deployment should be baked
  in. It also means the Archive cannot write its own definitions even by mistake.
- **The runtime image is Debian-based, not Alpine, because** the export rules interpret the user's
  calendar dates in their own time zone, which needs the ICU and tzdata the Alpine images omit.
- **`curl` is installed in the runtime image, because** the `aspnet` image ships no HTTP client and the
  compose healthcheck needs something to call. The alternative — no healthcheck — would leave
  `depends_on` unable to wait for readiness.
- **The container runs as `$APP_UID`, not root, because** everything a user uploads is untrusted and the
  process has no reason to hold root in the container.
- **Compose publishes `${ARCHIVE_PORT:-8080}`, because** a hard-coded host port collides on a shared
  machine; `.env` carries non-secret defaults only, and no credential belongs in it or in any image.

## The upload endpoint

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
- **`PlaceFile` and `PlaceFolder` merged into one `Place` taking a nullable `UploadedFile`,
  because** they differed only in the depth limit and whether a part is appended. The nullable
  argument is the discriminator: `segments.Count > (file is null ? 1 : 2)`.
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
- **`SuppressModelStateInvalidFilter` is on, because** with no endpoint choosing a status, the
  framework must not choose one either. Otherwise a blank `field` would come back as an automatic
  `400` while every other refusal came back as `404`, which is exactly the inconsistency the single
  shape exists to remove. The DataAnnotations stay on `UploadForm` for OpenAPI; a blank value now
  reaches the rules and is refused by name in the report.
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
- **The layout is `cabinets/<Cabinet>/<yyyy-MM-dd>/<timestamp>_<id>/`, because** it makes the
  directory tree the index. A date range prunes to a few day directories without opening a file,
  and the timestamp prefix makes an ordinal sort of the names chronological — so a page is chosen
  from directory names alone and only the documents actually on it are read. Zero-padded and
  ordinal so the order cannot depend on the container's locale.
- **The layout lives in `FileSystemDocumentStore` rather than in a type of its own, because** the
  `ArchiveStorageLayout` it replaced had twelve members and eight of them had exactly one caller —
  a wrapper whose methods were each read once, at the only place that used them. Inlining them
  keeps the property that mattered (the convention has one owner, and a second opinion about it
  would corrupt the store rather than fail a test) while removing a type whose whole job was to be
  delegated to. The one piece that stayed shared is `PartFile.PathFor`, and it moved onto the DTO
  that writes the path into `document.json`, so the file on disk and the metadata pointing at it
  are produced by the same line.
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
- **`Archive:StoragePath` is required with no default, because** a default would silently store
  documents inside the container's own writable layer, where they are lost on the next restart —
  the exact failure the setting exists to prevent. Same fail-fast reasoning as
  `Archive:CabinetsFile`, and verified both ways: absent, and pointing somewhere unusable.
- **Readiness checks that the storage path is writable, because** the usual container failure is a
  named volume whose root is owned by `root` while the process runs as `$APP_UID`. Without the
  check that appears as every upload failing one at a time, with nothing pointing at the mount.
  Honest caveat, the same one the cabinet check has: the unhealthy branch is hard to reach in
  practice, because registration already refuses to start on a directory it cannot create.
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
- **xUnit with plain assertions, because** FluentAssertions v8 is commercially licensed and the
  assertions here are simple enough not to need a DSL.
- **`TreatWarningsAsErrors`, because** a warning a reviewer sees is a warning nobody fixes. It has
  already earned its place: it caught dead code during a mutation check.
- **OpenAPI is not gated on the Development environment, because** the brief requires the description to
  be browsable while the service runs in its container.
- **No null checks on non-nullable parameters, because** `Nullable` is enabled and warnings are errors,
  so passing null to a non-nullable parameter is a compile error — a runtime guard for it is unreachable
  code that implies the contract is weaker than it is. Guards remain only where a value genuinely
  arrives unchecked at runtime: the configuration-sourced cabinets path, where an empty string is a
  realistic misconfiguration the compiler cannot see.
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
  `docs/upload-requirements.md` §5 checklist as "traversal / absolute / reserved names neutralised".

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

- **`404` is the status for "at least one entry failed", and it is the wrong code for most of the
  cases it covers.** Asked for and implemented on 2026-10-29, with the objection recorded here
  rather than silently obeyed:
  - It is *correct* for an unknown cabinet.
  - It is *wrong* for a partial failure. If 11 of 12 documents store and one fails, `404` tells the
    caller "this URI does not exist" while the body says eleven documents were created. `404` is
    about the resource, not about the work.
  - It is *wrong* for a broken field rule, which is the caller sending something unusable — `400`.
  - The practical cost: `HttpClient.EnsureSuccessStatusCode`, Refit and most `fetch` wrappers throw
    on `404` and routinely drop the body, so the report can be lost exactly when it carries the
    reason. The brief requires the client to "show the server's message" on an error, so the client
    must be written to read the body of a `404`.

  `207 Multi-Status` for a mixed result, or `400`/`404` split by cause, would each carry the same
  signal without lying about the URI. The change is one line in
  `UploadFailureStatusMiddleware.Judge`.
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

- **Test writing is paused** (2026-10-28, at request). The existing 65 tests are kept and passing, but
  new code from here lands untested until this is lifted. The brief makes "every upload and export rule
  has a test that fails when that rule is broken" an acceptance criterion and gates `dotnet test` from a
  clean clone, so this has to be repaid before hand-in. Recorded here rather than left implicit.
- **The debt now has a shape.** Every upload rule shipped on 2026-10-29 is uncovered:
  top-level file → one document; top-level folder → parts are its direct files; natural part order;
  nested entries skipped **and** reported; empty folder → document with zero parts; zero-byte file →
  one empty part; the chosen field set on every document; traversal and rooted paths refused;
  Unicode names unchanged; two uploads of the same folder both surviving; per-document failure
  reported and the rest still attempted. The checklist in `docs/upload-requirements.md` §5 is the
  list to work through. All of it was verified by hand instead (see "How to verify a change" in
  `AGENTS.md`), which is not the same thing and does not satisfy the acceptance criterion.
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
  real number is 65.

## What is built, what is missing

**Built:** the solution, shared build configuration, cabinet and index-field model, the field rules
(type / `maxLength` / `writable`, and which fields may be chosen for upload or as an export marker),
the enforced dependency rules, the `cabinets.json` loader with its validation and fail-fast startup,
a runnable Api with OpenAPI, SwaggerUI and `/health`, and **the upload endpoint end to end** — the
folder-to-document mapping, natural part ordering, untrusted-path handling, per-document commit and
the one-row-per-entry report. 65 passing tests across three test projects.

Also built: **durable storage** — documents are directories of files on a mounted volume, committed
by one atomic rename, with `tmp/` swept at startup — and `GET /cabinets/{name}/documents`, a paged
read capped by `Archive:MaxPageSize`.

**Missing:** the date-range filter on queries, index-value updates, a `GET /cabinets` endpoint so a
client can list fields, the whole Export service, the web client, and verified containers. **No
test covers any upload rule or the store** — see "Open debt".

Against "Things your system must cope with":

| Item | State |
|---|---|
| Treat everything a user uploads as untrusted | Addressed — `RelativePath` refuses rooted paths, `.`, `..` and control characters; names are never used as paths; body size and part count are bounded |
| Names may contain any Unicode character | Addressed — verified that `Договор София.txt` and `Фактури Q1` round-trip unchanged, and that stored content is byte-identical to the source by SHA-256 |
| Data survives restarting the containers | Addressed — documents are files on a named volume, and the service holds nothing in memory. Verified by uploading, stopping the service, starting it cold and reading the same 12 documents with the same ids back |
| An operation must not leave anything behind | Addressed on the upload side — content streams are opened and disposed in one `await using`, a failed document's staging directory is deleted, and `tmp/` is swept at startup. Verified: a planted staging directory is gone after the next start |
| Export changes data: what remains if it fails halfway | Partly — an upload commits per document by atomic rename, so a document is complete or absent and the report says which. The export side does not exist |
| Exports larger than memory | Partly — part content is now streamed to disk and can be streamed back out, so the store no longer caps it. The upload endpoint still needs the whole body to arrive first, which is named in "The upload endpoint" |
| Configurable maximum page size | Addressed — `Archive:MaxPageSize`, default 50. The date-range filter is still missing |
| Time zones | Groundwork only — `DateOnly`, UTC `StoredAt`/`ModifiedAt`, no `InvariantGlobalization` |
| Services may start in any order | Partly — `/health/ready` exists for a dependent service to wait on |
| Different hosts for client and services | Not addressed — no CORS policy yet |
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
- **A bulk path rewrite missed the case that mattered.** After the fixtures moved to `fixtures/`, a
  `sed` over `Task/fixtures/` fixed the documents and `appsettings` but silently skipped
  `RepositoryLayout.Path("Task", "fixtures", ...)`, where the segments are separate C# string
  arguments rather than a slash-delimited path. Caught because the tests failed, not because the
  rewrite reported anything — a textual find-and-replace cannot see a path that is not spelled as one.

## Time log

Rough, as the brief asks for. Durations are approximate and grouped per working session.

| Date | Time | What |
|---|---|---|
| 2026-10-28 | ~0.5 h | Read the brief and fixtures, inspected the sample material, wrote the upload requirements analysis and scaffolded `README.md` / `AGENTS.md` / `DECISIONS.md` |
| 2026-10-28 | ~0.1 h | Document upkeep: paused and then resumed time tracking |
| 2026-10-28 | ~1.0 h | Assessed the .NET Core 2.0 scaffold, agreed the architecture, rebuilt the solution on .NET 9, implemented the cabinet/index-field model and field rules with 25 tests, restructured the repository layout twice |
| 2026-10-28 | ~0.3 h | Revisited the one-vs-two-solutions question; added the enforced dependency tests and the repository-layout test helper |
| 2026-10-28 | ~0.7 h | Built the cabinets.json loader, catalog and validation with 33 tests; wired fail-fast startup and the required `Archive:CabinetsFile` setting; verified the running service's endpoints |
| 2026-10-28 | ~0.5 h | Review pass: dropped the custom exception for SDK types, renamed reader to loader, merged parsing into it, applied Clean Code decomposition, and moved Core onto `Domain/<entity>/` folders |
| 2026-10-28 | ~0.4 h | Moved the cabinets.json rules onto the DTOs as DataAnnotations attributes, removed the serialiser options and all eleven loader guard methods, and closed three rules that had no test |
| 2026-10-28 | ~0.4 h | Split the single test project into one per production project plus a shared TestSupport library, and added two tests that enforce the split |
| 2026-10-28 | ~0.4 h | Cleared the Program.cs TODOs: split liveness from readiness with a catalog health check, and wrote the Dockerfile, .dockerignore and compose files (unverified — no Docker available) |
| 2026-10-29 | ~0.4 h | Made the service container-only: merged `PlanState` into `UploadEntryOutcome`, deleted the local `.archive-data` and the `Archive` section of `appsettings.Development.json`, moved `MaxPageSize` / `MaxUploadBytes` / `MaxUploadEntries` into compose from `.env`, added `stop_grace_period` and `no-new-privileges`, and reworked `README.md` around `docker compose`. Verified the four `Archive__*` names bind by running the service with exactly the variables compose sets |
| 2026-10-29 | ~0.1 h | Clarified in `README.md` that the container's named volume and the local `.archive-data` are two independent archives, and that `down` keeps named volumes while `down -v` destroys them |
| 2026-10-29 | ~0.2 h | Folded `ArchiveStorageLayout` into `FileSystemDocumentStore` — eight of its twelve members had a single caller — and moved the part filename onto `PartFile.PathFor`. Re-verified upload, restart, paging and the refusal paths |
| 2026-10-29 | ~0.1 h | Confirmed where storage actually resolves to; documented in `README.md` that a relative path resolves against the content root rather than the shell's directory, and that both resolved paths are logged at startup |
| 2026-10-29 | ~1.2 h | Settled the storage question (files, not a database) and built it: `FileSystemDocumentStore`, `ArchiveStorageLayout`, the `document.json` DTOs, the required `Archive:StoragePath`, a storage readiness check, `GET /cabinets/{name}/documents` with paging, the compose volume and the Dockerfile ownership fix. Verified a cold restart keeps all 12 documents, two uploads give 24, content is byte-identical by SHA-256, and `tmp/` is swept |
| 2026-10-29 | ~0.2 h | Merged `Refused` and `Refuse` into one method returning the refusal report; checked the brief first and kept the per-rule reasons, which it requires. Verified all six refusal branches by hand |
| 2026-10-29 | ~0.3 h | Extracted the mapping back out as `UploadPlanningService`, returning one `List<PlannedDocument>` with a `PlanState` per entry; renamed `UploadForm` → `UploadFormRequest` and `UploadEntryResultResponse` → `UploadEntryResult`. Re-verified the sample material and the subfolder-only case |
| 2026-10-29 | ~0.3 h | Folded `UploadPlanner` into `UploadService.Plan` and `UploadService.Place`, retiring `UploadPlan`, `PlannedDocument`, `PlannedPart`, `SkippedEntry` and the `FileIndex` indirection. Verified the sample material is unchanged and that a subfolder-only folder still becomes a document |
| 2026-10-29 | ~0.1 h | Walked through how `PlaceFile` handles nesting deeper than one level. No code change; surfaced that an intermediate folder is only reported as skipped if the client names it, now written down in `docs/upload-requirements.md` and `AGENTS.md` |
| 2026-10-29 | ~0.2 h | Worked out and tested the example request payload (raw multipart, curl, browser `FormData`); added the verified recipes to `README.md`. Found that curl drops the relative path unless `;filename=` is given explicitly |
| 2026-10-29 | ~0.2 h | Dropped `NaturalNameComparer` for `StringComparer.Ordinal` in the planner and the report, reversing the earlier part-order decision; corrected the four documents that asserted natural order |
| 2026-10-29 | ~0.2 h | Walked through `UploadPlanner` line by line as review preparation. No code change; the read turned up the `IsRooted` false positive recorded under "Known defects" |
| 2026-10-29 | ~0.2 h | Took `FindField` off `Cabinet`: a private copy in `IndexFieldRules`, an inline `First` in `UploadService.StoreAsync` that retires a null-forgiving `!`, and two tests moved onto `Fields` directly |
| 2026-10-29 | ~0.5 h | Reworked the response: dropped the `UploadOutcome` hierarchy and the `ProblemDetails` branches, moved the status decision into `UploadFailureStatusMiddleware` (`200`, raised to `404` on any failure) along with the failure logging, took the logger out of the controller, suppressed the automatic model-validation `400`, and split the report and DTO types one per file. Re-verified by hand against the sample material and all four refusal paths |
| 2026-10-29 | ~1.5 h | Repaired a non-compiling `HEAD` (restored `FieldType.Date`, the `IndexValue.Date` case, a date validator and the four deleted tests; fixed the missing `[JsonPropertyName("writable")]` that made every field read-only). Settled the four open upload questions. Built the upload path: document model, `IDocumentStore`, `UploadPlanner`, `NaturalNameComparer`, `RelativePath`, `UploadService`, the in-memory placeholder store, the multipart contracts and `CabinetsController`. Verified by hand against the real sample material and all five refusal paths. Reworked the controller to a `cabinets` base route, a flat one-row-per-entry report, and no private methods. Updated README, AGENTS and this file |
