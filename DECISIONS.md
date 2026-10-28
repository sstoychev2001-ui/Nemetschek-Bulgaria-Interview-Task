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
- **The upload mapping will be a pure function, because** every upload rule then gets a short test with
  no fixtures, no HTTP and no containers. Content bytes stay in `Infrastructure` and are streamed; the
  rules never see them.
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

## Still open — not yet decided

Listed so they are not decided by accident:

1. **Upload transport**: zip body vs. multipart with relative paths. Both make relative paths untrusted
   input; zip additionally needs slip and bomb limits, multipart needs streaming multipart parsing.
2. **Part order inside a folder-document**: ordinal (`page1`, `page10`, `page2`) vs. natural
   (`page1`, `page2`, `page10`). Needs a choice, a documented line, and a test.
3. **Document title for files**: with extension or without. The brief's "the name of the file" reads as
   *with*.
4. **Upload failure granularity**: commit per document and report failures, vs. reject the whole upload.
   "A partial failure is never silent" suggests per-document with a report.
5. **Storage**: which store the Archive uses, and how content is kept so large exports can stream.

## Open debt

- **Test writing is paused** (2026-10-28, at request). The existing 70 tests are kept and passing, but
  new code from here lands untested until this is lifted. The brief makes "every upload and export rule
  has a test that fails when that rule is broken" an acceptance criterion and gates `dotnet test` from a
  clean clone, so this has to be repaid before hand-in. Recorded here rather than left implicit.

## What is built, what is missing

**Built:** the solution, shared build configuration, cabinet and index-field model, the field rules
(type / `maxLength` / `writable`, and which fields may be chosen for upload or as an export marker),
the enforced dependency rules, the `cabinets.json` loader with its validation and fail-fast startup,
and a runnable Api with OpenAPI, SwaggerUI and a `/health` endpoint. 70 passing tests across
three test projects.

**Missing:** the upload mapping and endpoint, storage, queries and paging, index-value updates, a
`GET /cabinets` endpoint so a client can list fields, the whole Export service, the web client, and
working containers. The compose files at the root are the old skeleton and do not yet build this
solution.

Against "Things your system must cope with", nothing is addressed yet beyond text handling and the
time-zone groundwork (`DateOnly`, UTC intent, no `InvariantGlobalization`).

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
