# Decisions

Each decision in the form "A, because B". Updated as the work progresses.

## Architecture

- **Layered, three projects plus one test project (`Api` / `Core` / `Infrastructure` / `Tests`),
  because** it is conventional, small enough to explain under live questioning, and gives one obvious
  home per concern. A separate `Application` layer was rejected: at four or five operations it would be
  pass-through ceremony.
- **`Core` declares the ports and references nothing, because** the brief requires `dotnet test` to pass
  from a clean clone with no network and no containers. With `IDocumentStore`/`IContentStore`/
  `ICabinetCatalog` declared in `Core/Ports/` and implemented in `Infrastructure`, rule tests use
  in-memory fakes instead of fighting a real store. Textbook layering (Business → Data) would invert
  this and drag storage into every rule test.
- **`Core` is folded by feature, not by type-kind (`Cabinets/`, `Upload/`, not `Services/`, `Models/`),
  because** the brief says an AI agent will be handed a small unseen change and judged on `AGENTS.md`;
  colocation means a behaviour change lands in one folder.
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
  (`Nemetschek.Archive.Tests/Architecture/ProjectDependencyTests.cs`), because** the layering is what
  makes the rest of the brief achievable, and a convention nothing checks is a convention that decays.
  The tests read the real csproj files and assert: Core declares no package and no project references,
  Infrastructure references only Core, Api references only Core and Infrastructure, dependencies point
  inward only, no Archive project references an Export project, and every project is in the solution.
  Verified to bite: adding a `PackageReference` to Core fails `Core_declares_no_package_references`.
- **Validation returns a `FieldViolation?` rather than throwing, because** the upload report must list
  many refusals at once — "a partial failure is never silent" — so a refusal has to be a value.
- **Violation reasons are complete user-facing sentences naming the field and the limit, because** the
  client is forbidden from restating server rules, so the server must phrase them.

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

## What is built, what is missing

**Built:** the solution, shared build configuration, cabinet and index-field model, the field rules
(type / `maxLength` / `writable`, and which fields may be chosen for upload or as an export marker),
the enforced dependency rules, and a runnable Api with OpenAPI, SwaggerUI and a `/health` endpoint.
32 passing tests.

**Missing:** everything else — the cabinets.json loader, the upload mapping and endpoint, storage,
queries and paging, index-value updates, the whole Export service, the web client, and working
containers. The compose files at the root are the old skeleton and do not yet build this solution.

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

## Time log

Rough, as the brief asks for. Durations are approximate and grouped per working session.

| Date | Time | What |
|---|---|---|
| 2026-10-28 | ~0.5 h | Read the brief and fixtures, inspected the sample material, wrote the upload requirements analysis and scaffolded `README.md` / `AGENTS.md` / `DECISIONS.md` |
| 2026-10-28 | ~0.1 h | Document upkeep: paused and then resumed time tracking |
| 2026-10-28 | ~1.0 h | Assessed the .NET Core 2.0 scaffold, agreed the architecture, rebuilt the solution on .NET 9, implemented the cabinet/index-field model and field rules with 25 tests, restructured the repository layout twice |
| 2026-10-28 | ~0.3 h | Revisited the one-vs-two-solutions question; added the enforced dependency tests and the repository-layout test helper |
