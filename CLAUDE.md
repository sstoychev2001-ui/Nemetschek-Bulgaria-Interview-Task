# Project instructions

Take-home task: a small document archive (two .NET services + a web client, in containers).
The authoritative brief is `TASK.md`. The fixtures in `fixtures/` are given and not to be edited.

## Standing instruction: keep the documents current

**Every prompt in this repository ends with the affected markdown documents updated** — not at the end of
the task, not "later". This holds for all sessions in this folder.

| Document | Update when |
|---|---|
| `README.md` | Build/run/test steps or required settings change |
| `AGENTS.md` | Architecture, where a rule lives, or a convention changes |
| `DECISIONS.md` | Any decision is made ("A, because B"), anything is cut, the AI was wrong, or time was spent (append to the time log) |
| `docs/*.md` | The understanding of a feature's requirements changes |

Time log entries are rough by design — the brief asks for "an honest, rough time log". Record the work done
and an approximate duration; do not invent precision. Group a session's work into one row rather than one
row per prompt.

If a prompt produced only analysis or discussion, record that in the relevant document anyway — the
decision log and requirement notes are part of the hand-in, and the time log must stay honest.

## Ground rules from the brief

- Business rules live in the services. The client never restates a server rule.
- The Export service reaches the Archive only over HTTP, never its storage.
- Every upload and export rule needs a test that fails when that rule is broken.
- `dotnet test` must pass from a clean clone with no network and no containers.
- No credentials in any image or in the repository.
- User-supplied names are data, never filesystem paths.
