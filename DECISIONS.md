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
  projects. `Infrastructure` keeps what belongs to the deployment: the dependency registrations, the
  health checks, and the loader for the `cabinets.json` the deployment mounts.

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
  (`Nemetschek.Archive.Api.Tests/Architecture/ProjectDependencyTests.cs`), because** the layering is what
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
- **`OperationCanceledException` is not logged as a failure, because** a caller going away is not a
  fault of the Archive's, and logging it at `Error` would make an abandoned upload look like a
  broken volume.
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
- ~~**`SuppressModelStateInvalidFilter` is on, because** with no endpoint choosing a status, the
  framework must not choose one either. Otherwise a blank `field` would come back as an automatic
  `400` while every other refusal came back as `404`, which is exactly the inconsistency the single
  shape exists to remove.~~ **Wrong as recorded: nothing in the solution sets that option**, and it
  is not clear it was ever set — `grep` finds the name only in this file and in `README.md`.
  Verified on 2026-10-29: a request with no `field` answers `400` with ASP.NET's automatic
  `ValidationProblemDetails` (`"title": "One or more validation errors occurred."`, the message
  under `errors.Field`), not a report and not the middleware's shape.

  **Left as it is, deliberately.** The reasoning that argued for suppression was that the automatic
  `400` would be the only `400` in a service where every other refusal was `404` — and that premise
  died when `ArchiveExceptionMiddleware` made a broken field rule `400` too. So the automatic
  response is now the *same status* as a rule refusal, carrying a `[Required]` message already
  phrased as a user-facing sentence, which is what the convention "a DTO declares its own rules"
  asks for. The residue is that a caller sees two `400` bodies — `errors.Field` for a missing
  parameter, `detail` for a broken rule — both standard `ProblemDetails`. Worth knowing when the
  client is written; not worth a filter that would push presence checks into `Core`.
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
- **The layout lives in `DocumentsStoreService` rather than in a type of its own, because** the
  `ArchiveStorageLayout` it replaced had twelve members and eight of them had exactly one caller —
  a wrapper whose methods were each read once, at the only place that used them. Inlining them
  keeps the property that mattered (the convention has one owner, and a second opinion about it
  would corrupt the store rather than fail a test) while removing a type whose whole job was to be
  delegated to. The part filename later came back into the store as `PartPath`, when the file DTOs
  were retired and the store became the only thing that knows the layout.
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
  the readiness probe writes, so without it `/health/ready` answers `503` and `depends_on` never
  releases; verified by deleting the directory from the running container. And running from source
  (`Archive__StoragePath=$PWD/.archive-data`, which `README.md` documents) involves no Dockerfile at
  all. Underneath all three: a rule of the service would otherwise depend on a line in a deployment
  artefact, which is the coupling `cabinets.json` being mounted rather than baked exists to avoid.
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
  fewer types over a compiler-enforced boundary. One type now spans both states: `Document.ToStore`
  and `Part.ToStore` leave `Id` as `Guid.Empty` and set `OpenContent`; the store overwrites the
  system fields and nulls `OpenContent`.

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

- ~~**`404` is the status for "at least one entry failed", and it is the wrong code for most of the
  cases it covers.**~~ **Resolved on 2026-10-29** by `ArchiveExceptionMiddleware`, which is the
  `400`/`404`-split-by-cause option named at the end of this entry: a broken field rule is `400`, an
  unknown cabinet is `404`, and a per-entry failure stays a `Failed` row inside a `200` report. Kept
  here because the objection is the reason the split was made, and because the per-entry case still
  needs reading the body. The original entry, as written when the `404` was asked for and
  implemented:
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
  `UploadFailureStatusMiddleware.Judge`. *(That middleware no longer exists; the split was taken.)*
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

- **Test writing is paused** (2026-10-28, at request). The existing tests are kept and passing, but
  new code from here lands untested until this is lifted. The brief makes "every upload and export rule
  has a test that fails when that rule is broken" an acceptance criterion and gates `dotnet test` from a
  clean clone, so this has to be repaid before hand-in. Recorded here rather than left implicit.

  **One exception was taken on 2026-10-29, and is flagged rather than assumed:** the store's logging
  tests shipped with the logging fix — four as `LoggingDocumentStoreTests`, then five as
  `DocumentsStoreServiceTests` once the decorator was merged in, taking the suite from 65 to 70. The
  reason is that the behaviour being added exists *because* an untested behaviour was silently
  deleted by a refactor — see the `UploadFailureStatusMiddleware` note under "AI notes" — so
  shipping it untested would have repeated the exact failure it fixes. Deleting those four tests is
  a one-file change if the pause was meant to cover this too.
- **The debt was re-measured on 2026-10-30 and is unchanged in substance**: of 70 tests, 9 are
  architecture, 31 the `cabinets.json` loader, 25 the index-field rules and 5 the store's logging.
  `UploadPlanningService` and `UploadService` have none, so the folder→document rules — the brief's
  priority 1 — are still carried by hand verification alone.
- **The debt now has a shape.** Every upload rule shipped on 2026-10-29 is uncovered:
  top-level file → one document; top-level folder → parts are its direct files; natural part order;
  nested entries skipped **and** reported; empty folder → document with zero parts; zero-byte file →
  one empty part; the chosen field set on every document; traversal and rooted paths refused;
  Unicode names unchanged; two uploads of the same folder both surviving; per-document failure
  reported and the rest still attempted. The checklist in `docs/upload-requirements.md` §5 is the
  list to work through. All of it was verified by hand instead (see "How to verify a change" in
  `AGENTS.md`), which is not the same thing and does not satisfy the acceptance criterion.
- **The verified container evidence is from a VM that no longer exists** (2026-10-29). Podman was
  removed and reinstalled during the compose detour, so the machine, images and volumes are new and
  the original twelve-document round trip cannot be pointed at any more. `compose up --build` and the
  healthcheck are confirmed working on the rebuilt machine, but the full acceptance run — upload the
  sample material, `down`, `up`, same documents in the same order — is owed again on it, and so are
  the rootless-UID and `Archive__*` binding checks that `README.md` still records from before.
- **The container exits on its own, and `restart: unless-stopped` does not bring it back**
  (observed twice on 2026-10-29). `podman ps -a` shows `Exited (0) … (healthy)` after a quiet
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
  real number is 65.

## What is built, what is missing

**Built:** the solution, shared build configuration, cabinet and index-field model, the field rules
(type / `maxLength` / `writable`, and which fields may be chosen for upload or as an export marker),
the enforced dependency rules, the `cabinets.json` loader with its validation and fail-fast startup,
a runnable Api with OpenAPI, SwaggerUI and `/health`, and **the upload endpoint end to end** — the
folder-to-document mapping, ordinal part ordering, untrusted-path handling, per-document commit and
the one-row-per-entry report. 70 passing tests across three test projects.

Also built: **durable storage** — documents are directories of files on a mounted volume, committed
by one atomic rename, with `tmp/` swept at startup — `GET /cabinets/{name}/documents`, a paged read
capped by `Archive:MaxPageSize`, **the containers** (`compose up --build`, data surviving `down` and
`up`, readiness gating), and **the logs** (one line per request, per stored document and per
refusal, with a storage failure at `Error`).

**Missing.** Reviewed in full on 2026-10-30; the Archive is **not** feature-complete even leaving
the Export service and the client aside. In the order the brief's priorities imply:

| Gap | Brief | Note |
|---|---|---|
| **Tests for the folder→document upload rules** | Priority 1, and an acceptance criterion | `UploadPlanningService` has no test at all. The 25 index-field tests cover type / `maxLength` / `writable`; nothing covers "each top-level entry is one document", "a file becomes one part", "a subfolder's parts are its direct files", "deeper is skipped *and reported*", "an empty subfolder becomes a document with no parts", the title, the chosen field on every document, or part order. Verified by hand only — see "Open debt" |
| **Read a part's content over HTTP** | "fetches their content", and the Export never touches the Archive's storage | No endpoint exists; `PartResponse` carries `Id` / `Name` / `Order` / `SizeInBytes` and no content or link. **Blocks the Export service and the client's download.** Would be `GET /cabinets/{cabinet}/documents/{id}/parts/{partId}/content`, streamed from the part file with its `SizeInBytes` as `Content-Length` |
| **Update index values** | "updates of index values"; "writes the export marker back through the Archive" | No endpoint. **Blocks the export marker.** Would be a `PATCH` of named values through `IndexFieldRules.ValidateWrite`, setting `ModifiedAt`, rewriting `document.json` by the same staging-and-rename route `AddAsync` uses |
| **Date-range filter on the query** | "queries with a date range and paging"; the filter is on `StoredAt`, inclusive calendar dates as the user means them | Paging exists, the filter does not. This is also where the whole time-zone requirement lands: the day directories make the pruning cheap, but nothing yet converts a user's calendar date to a UTC instant |
| **`GET /cabinets`** | The client picks a cabinet and sees each field's type and whether it is writable | No endpoint, so a client cannot learn the fields without restating them — which the brief forbids. The catalog is already in memory; this is a read of `ICabinetCatalog` and a response DTO |
| **CORS** | "The client may be served from a different host than the services" | No policy configured |

Then, out of scope for this review but named for completeness: the whole Export service, the web
client, and the round-trip demo that needs both.

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
| The logs of every service are visible | Addressed for the one service that exists — see "What the service logs". `compose logs` shows startup, one line per request, one per stored document, and refusals |
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
| 2026-10-29 | ~0.2 h | Found that foreground `podman-compose up` crashes on Windows (`asyncio.add_signal_handler` is unimplemented there) and documented `-d` as required, with `podman-compose logs` for output. One crash left the named volume empty; could not reproduce, so recorded as unconfirmed rather than asserted. Re-verified `down` → `up -d` preserves documents |
| 2026-10-29 | ~0.5 h | Got the stack running through compose: installed `podman-compose` and verified `up --build` → upload → `down` → `up` keeps the documents, with the compose healthcheck reporting healthy. Diagnosed why `podman compose` fails on this machine (no `win-sshproxy.exe`, so no named pipe) and documented it. Confirmed `security_opt: no-new-privileges:true` works on both engines |
| 2026-10-29 | ~0.3 h | Added placeholders to `appsettings.json` for every setting compose injects. The two required paths are documented by comment only and carry no value, not even `null`: the JSON provider surfaces `null` as an empty string, which `?? throw` does not catch, so a placeholder value would have replaced the named startup error with a generic one. Verified both named errors. Documented running under Podman in `README.md`, including the missing-named-pipe failure and the build/run fallback, and corrected the README claims that the image had never been built |
| 2026-10-29 | ~0.6 h | Split every Core root folder by entity (`Domain/Cabinets`, `Validation/IndexFields`, …) and moved `CabinetCatalog` and `DocumentsStoreService` into `Core/Services/<entity>/`, leaving `Infrastructure` with the dependency registrations, health checks and the cabinets loader. Per-file usings recomputed from the types each file actually references. Verified the container round trip, the refusal statuses and persistence across recreate |
| 2026-10-29 | ~0.7 h | Replaced the status middleware with `ArchiveExceptionMiddleware`: Core throws `UnknownCabinetException` / `UploadRefusedException`, the middleware maps them with a `switch` to `404` / `400` and rethrows anything else for `UseExceptionHandler`. Restructured `Core` into `Domain` / `Services` (+`Interfaces`) / `Validation` / `Exceptions`, moved `PagingLimits` into `Contracts/Shared`. Verified every status in the container |
| 2026-10-29 | ~0.9 h | Merged `DocumentDraft`/`PartDraft` into `Document`/`Part` and moved the on-disk JSON shape onto them, matching how cabinets already worked; retired the file DTOs and their mappings. Split `Contracts/` into one folder per entity with `Requests`/`Responses`/`Mappings`, deleted `Api/Results/`, and moved both health checks into `Infrastructure/Health/` registered by the `Add…` extension that owns them. Caught and fixed a duplicated `"Type"` in the stored JSON by reading the file. Re-verified the container round trip and restart |
| 2026-10-29 | ~0.1 h | Replaced the inline `file is null ? 1 : 2` depth limit with a `maxSegments` parameter and two named constants; wrote up why `DocumentDraft` exists and why that is consistent with merging `UploadEntry`. No behaviour change |
| 2026-10-29 | ~0.8 h | **Built and ran the image for the first time, on Podman 6.1.4.** Verified rootless UID 1654 owns the volume, the upload round trip, and that destroying and recreating the container keeps all 12 documents. Merged `PlannedDocument` + `UploadEntryResult` into `UploadEntry`, moved all mapping into extension classes, and refused AutoMapper on licence and fit |
| 2026-10-29 | ~0.4 h | Made the service container-only: merged `PlanState` into `UploadEntryOutcome`, deleted the local `.archive-data` and the `Archive` section of `appsettings.Development.json`, moved `MaxPageSize` / `MaxUploadBytes` / `MaxUploadEntries` into compose from `.env`, added `stop_grace_period` and `no-new-privileges`, and reworked `README.md` around `docker compose`. Verified the four `Archive__*` names bind by running the service with exactly the variables compose sets |
| 2026-10-29 | ~0.1 h | Clarified in `README.md` that the container's named volume and the local `.archive-data` are two independent archives, and that `down` keeps named volumes while `down -v` destroys them |
| 2026-10-29 | ~0.2 h | Folded `ArchiveStorageLayout` into `DocumentsStoreService` — eight of its twelve members had a single caller. Re-verified upload, restart, paging and the refusal paths |
| 2026-10-29 | ~0.1 h | Confirmed where storage actually resolves to; documented in `README.md` that a relative path resolves against the content root rather than the shell's directory, and that both resolved paths are logged at startup |
| 2026-10-29 | ~1.2 h | Settled the storage question (files, not a database) and built it: `DocumentsStoreService`, `ArchiveStorageLayout`, the `document.json` DTOs, the required `Archive:StoragePath`, a storage readiness check, `GET /cabinets/{name}/documents` with paging, the compose volume and the Dockerfile ownership fix. Verified a cold restart keeps all 12 documents, two uploads give 24, content is byte-identical by SHA-256, and `tmp/` is swept |
| 2026-10-29 | ~0.2 h | Merged `Refused` and `Refuse` into one method returning the refusal report; checked the brief first and kept the per-rule reasons, which it requires. Verified all six refusal branches by hand |
| 2026-10-29 | ~0.3 h | Extracted the mapping back out as `UploadPlanningService`, returning one `List<PlannedDocument>` with a `PlanState` per entry; renamed `UploadForm` → `UploadFormRequest` and `UploadEntryResultResponse` → `UploadEntryResult`. Re-verified the sample material and the subfolder-only case |
| 2026-10-29 | ~0.3 h | Folded `UploadPlanner` into `UploadService.Plan` and `UploadService.Place`, retiring `UploadPlan`, `PlannedDocument`, `PlannedPart`, `SkippedEntry` and the `FileIndex` indirection. Verified the sample material is unchanged and that a subfolder-only folder still becomes a document |
| 2026-10-29 | ~0.1 h | Walked through how `PlaceFile` handles nesting deeper than one level. No code change; surfaced that an intermediate folder is only reported as skipped if the client names it, now written down in `docs/upload-requirements.md` and `AGENTS.md` |
| 2026-10-29 | ~0.2 h | Worked out and tested the example request payload (raw multipart, curl, browser `FormData`); added the verified recipes to `README.md`. Found that curl drops the relative path unless `;filename=` is given explicitly |
| 2026-10-29 | ~0.2 h | Dropped `NaturalNameComparer` for `StringComparer.Ordinal` in the planner and the report, reversing the earlier part-order decision; corrected the four documents that asserted natural order |
| 2026-10-29 | ~0.2 h | Walked through `UploadPlanner` line by line as review preparation. No code change; the read turned up the `IsRooted` false positive recorded under "Known defects" |
| 2026-10-29 | ~0.2 h | Took `FindField` off `Cabinet`: a private copy in `IndexFieldRules`, an inline `First` in `UploadService.StoreAsync` that retires a null-forgiving `!`, and two tests moved onto `Fields` directly |
| 2026-10-29 | ~0.5 h | Reworked the response: dropped the `UploadOutcome` hierarchy and the `ProblemDetails` branches, moved the status decision into `UploadFailureStatusMiddleware` (`200`, raised to `404` on any failure) along with the failure logging, took the logger out of the controller, suppressed the automatic model-validation `400`, and split the report and DTO types one per file. Re-verified by hand against the sample material and all four refusal paths |
| 2026-10-29 | ~0.4 h | Diagnosed the recurring `podman ps … exit status 125` crash on `podman-compose up`: not the compose file but Podman 6 changing `ps`'s `.Labels` from a map to a slice, which breaks podman-compose ≤ 1.4.1's `{{ index .Labels … }}` template. Upgrading 1.2.0 → 1.6.0 cleared it and exposed the opposite bug — `is_context_git_url()` reads a Windows absolute context path as a git URL and drops `-f <dockerfile>` — so no released version does `up --build` here. Settled on pre-building with `podman build` plus `up -d --no-build`, verified healthy end to end, and wrote the version matrix into `README.md` |
| 2026-10-29 | ~0.3 h | Podman reinstalled, and `podman compose up --build` works as the single command — the earlier dead end was `podman-compose` (the Python reimplementation), never `podman compose`, which delegates to the Docker Compose v2 binary; the volume labels (`com.docker.compose.version: 2.29.7`) and the hyphenated container name are how that was confirmed. Traced where documents actually live: named volume → `/var/lib/containers/storage/volumes/nemetschek-archive_archive-data/_data` in the rootful store inside the VM → a single `ext4.vhdx` on Windows, unreachable over `\\wsl$` because `volumes/` is root-only. Recorded the three ways in, and corrected every "no engine installed" claim the previous step had put into the three documents |
| 2026-10-29 | ~0.4 h | Asked for `podman-compose up` to be the single command; established it cannot be, since plain `up` only builds when the image is absent and `up --build` hits the context-path bug. Was about to move the Dockerfile to the context root so Podman's own discovery would find it without `-f`, when the call was made to drop Podman instead. **Uninstalled Podman wholesale**: the machine, five orphaned `podman-machine-*` WSL distros, the CLI, Podman Desktop (winget reported success but left 523 MB in `C:\Program Files`, so the all-users NSIS uninstaller was run elevated), the `podman-compose` pip package, the `containers` state directories and the stale `PATH` entry. `Ubuntu` and `Ubuntu-26.04` kept. Took the unrelated `buyoff` MySQL/Redis volumes, the kind/minikube clusters and kafka with it — confirmed first, chosen without a backup. The repository was left alone; the Dockerfile move was not made, since `docker compose up --build` needs no workaround |
| 2026-10-29 | ~1.5 h | Repaired a non-compiling `HEAD` (restored `FieldType.Date`, the `IndexValue.Date` case, a date validator and the four deleted tests; fixed the missing `[JsonPropertyName("writable")]` that made every field read-only). Settled the four open upload questions. Built the upload path: document model, `IDocumentStore`, `UploadPlanner`, `NaturalNameComparer`, `RelativePath`, `UploadService`, the in-memory placeholder store, the multipart contracts and `CabinetsController`. Verified by hand against the real sample material and all five refusal paths. Reworked the controller to a `cabinets` base route, a flat one-row-per-entry report, and no private methods. Updated README, AGENTS and this file |
| 2026-10-29 | ~0.6 h | Diagnosed "nothing is logged to container logs": the pipe was fine — `podman logs` had the six startup lines — but the service emitted nothing afterwards, because `Microsoft.AspNetCore` is pinned to `Warning` and the whole solution held three `ILogger` calls, two of them at startup. Added the framework `HttpLogging` middleware (one combined line per request, no bodies or headers, `/health` excluded) and a `LoggingDocumentStore` decorator in `Infrastructure` so a store failure the upload report turns into a `Failed` row is still logged at `Error`. Four tests pin the decorator, and deleting the `LogError` was confirmed to fail exactly the one that names it. Traced the missing failure log back to the `UploadFailureStatusMiddleware` retirement and recorded it under AI notes. Verified in the container: a read, an upload of two documents and a refused upload each produce their line, the readiness probe none. The same trail turned up three stale document claims and corrected them: `docs/upload-requirements.md` and `README.md` still said a `Failed` row raises the status to `404`, and `AGENTS.md`'s verification table predicted `404` for four field refusals that in fact answer `400` (all seven statuses re-checked against the container). The `404`-for-any-failure cut in "Cut, knowingly" is marked resolved
| 2026-10-29 | ~0.2 h | Answered why the upload takes `folders` as well as `files`: an empty folder has no multipart representation, and folding it into `files` as an empty part would make it indistinguishable from the sample material's zero-byte `empty-note.txt`, which must become a document with one part. Demonstrated both in one request against the container. No code change; the reasoning and the two refused one-parameter alternatives (zip body, trailing-slash marker) written into `DECISIONS.md` and `docs/upload-requirements.md`. Also noticed the container had exited 0 on its own despite `restart: unless-stopped` — recorded under "Open debt", not yet diagnosed |
| 2026-10-29 | ~0.2 h | Weighed renaming `folders` to `emptyFolders` and refused it: the parameter also carries nested folders, which usually hold files, so the name would misdescribe `delivery-2026-03/archive`. Corrected my own overstatement from the previous entry that it exists only for empty folders. Confirmed against the container that naming a folder which has files is a no-op, and that a named nested folder gets its own `Skipped` row beside its files'. Rewrote the `Folders` / `FolderPaths` summaries, which had said only that folders holding no file "need to be listed" — true but hiding the second use |
| 2026-10-29 | ~0.1 h | Found a third stale claim while checking the refusal shapes: `README.md` and `DECISIONS.md` both said `SuppressModelStateInvalidFilter` is on, and nothing in the solution sets it — a missing `field` answers ASP.NET's automatic `ValidationProblemDetails`. Left the behaviour as it is (the argument for suppressing it assumed every other refusal was `404`, which stopped being true) and corrected both documents; the two different `400` bodies are now written down as something the client has to know |
| 2026-10-29 | ~0.5 h | Merged `LoggingDocumentStore` into `DocumentsStoreService` at request, after putting the choice back: the decorator existed only to dodge `Core`'s no-package rule, so `Core_declares_no_package_references` became `Core_declares_only_allowed_package_references` with `Microsoft.Extensions.Logging.Abstractions` as the single allowed name, rather than moving the store out to `Infrastructure` and reversing "storing is what a document archive does". `Prepare` is now `static` so registration can still fail startup on an unusable root before any container exists to resolve a logger from. Tests moved to `Core.Tests/Documents/DocumentsStoreServiceTests.cs` against a temp directory (5, from 4; suite 70) and the mutation re-checked — deleting the whole `catch` arm fails exactly the one test, and deleting only the `LogError` does not compile. Verified in the container: the log category is now the store's own, `empty-folder` logs `0 part(s)` against the file's `1` |
| 2026-10-29 | ~0.2 h | Answered why `Prepare` is needed when the Dockerfile already does `mkdir -p /var/lib/archive`: the image layer holds an empty directory (shown with `podman run --entrypoint ls`), a volume replaces that path at runtime, `tmp/` is what the readiness probe writes into — deleting it from the running container gives `503` until a restart — and running from source has no Dockerfile at all. Also demonstrated the sweep: a planted `tmp/abandoned-upload` is gone after a restart. Written into `DECISIONS.md` and `AGENTS.md`; no code change |
| 2026-10-30 | ~0.3 h | Audited the brief against the code to answer "are we done apart from Export and the UI". Answer: no — the Archive is missing a part-content endpoint, index-value updates, the date-range filter and `GET /cabinets`, has no CORS policy, and still has zero tests over the folder→document upload rules (the brief's priority 1). The first two block the Export service, so they precede it. Counted the suite by class to get the coverage claim right (9 architecture + 31 loader + 25 field rules + 5 store), and rewrote "What is built, what is missing" as a table with how each gap would be done. No code change |
| 2026-10-30 | ~0.2 h | Fixed a broken build: `DocumentPage` and `FieldViolation` were absent from the working tree, so eleven files in `Core` would not compile. Traced it to the merge at `HEAD` (`6f0ae8f`), which took the deleting side of `660a26b` — a 28-file, 843-deletion commit from `origin/main`. Restored both from `d1fc81b`, then checked every other path that commit deleted: all of them are old locations the Core restructure superseded (`Ports/` → `Services/Interfaces/`, `Domain/IndexFields/` → `Validation/IndexFields/`, the deliberately merged `*Draft` types), each with a live counterpart. The cancellation guard in `AddAsync` had also gone missing for the second time and was restored again, which is what the suite caught |
