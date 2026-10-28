# Upload feature — requirements in plain terms

Source: `TASK.md` ("Upload rules", plus the constraints in "Things your system must cope with").
Status: **analysis done; the field rules it depends on are implemented in
`Nemetschek.Archive/Nemetschek.Archive.Core/Domain/IndexFields/`. The mapping itself is not built yet —
it will live in `Core/Domain/Uploads/`.**

## 1. What the user does

The user supplies three things:

| Input | Constraint |
|---|---|
| A file cabinet | Must be one of the two in `fixtures/cabinets.json`: `Invoices`, `Contracts` |
| One index field | Must belong to that cabinet, be `type: "text"`, and be `writable: true` |
| A folder of material | Any folder from their machine; contents are untrusted |

Then the whole folder goes in as a batch of documents, and the user gets back a report.

Writable text fields that are actually selectable:
- **Invoices**: `InvoiceNumber` (20), `Supplier` (60), `Batch` (100), `Status` (12)
- **Contracts**: `ContractNumber` (30), `Party` (100), `Notes` (255)

Not selectable: `Amount`/`Value` (number), `InvoiceDate`/`SignedOn` (date), `ApprovedBy`/`Category` (not writable).

## 2. The folder-to-documents mapping

Only the **top level** of the folder matters. Each top-level entry becomes exactly one document.

```
sample-material/              <- the uploaded folder; its NAME becomes the field value
├── invoice-001.txt           -> document "invoice-001.txt", 1 part
├── empty-note.txt (0 bytes)  -> document "empty-note.txt", 1 part (empty part is still a part)
├── Договор София.txt         -> document "Договор София.txt", 1 part
├── delivery-2026-03/         -> document "delivery-2026-03", parts = page1, page2, page10
│   ├── page1.txt                (order must be documented and deterministic)
│   ├── page2.txt
│   ├── page10.txt
│   └── archive/              -> IGNORED, and reported as skipped
│       └── old-page.txt      -> IGNORED, and reported as skipped
├── Фактури Q1/               -> document "Фактури Q1", 2 parts
└── empty-folder/             -> document "empty-folder", 0 parts (still a document)
```

Rules restated:

1. **Top-level entry = one document.** File or folder, no difference in count.
2. **File → one document with one part.**
3. **Folder → one document whose parts are the files directly inside it.** The order of those parts is my choice, but I must write it down and keep it stable.
4. **Anything deeper than one level is dropped** — not silently. Every dropped entry appears in the report as skipped, with a reason.
5. **Empty folder → a document with zero parts.** It is not an error and not skipped.
6. **Title = the entry's own name**, exactly as the user had it (including the extension for files).
7. **The chosen field = the uploaded folder's name**, identical on every document in that upload.
8. **Nothing is silent.** The response says what was stored, what was skipped, and what failed, so the user can reconcile against their folder.

## 3. What the server owns (and the client must never re-implement)

- Which cabinets and fields exist, and what each field's type / maxLength / writable flag is.
- Rejecting a write to a non-writable field.
- Rejecting a value that violates the field's type or length.
- Assigning the system fields: `id`, `StoredAt`, `ModifiedAt`. The client never sends these.
- Deciding what counts as skipped vs. failed.

The client's job is to collect the three inputs, show progress, and render the report the server returns.

## 4. The awkward parts — where the real work is

These are the points where a naive implementation breaks a stated requirement.

**Transport.** HTTP has no "folder" type. The folder has to be flattened into something uploadable — a zip, or a multipart body carrying each file with its relative path. Either way the server reconstructs the tree from relative paths, which means relative paths are attacker-controlled input.

**Untrusted names.** A name can be `../../etc/passwd`, `C:\Windows\x`, `con`, a 300-character string, or contain control characters or a NUL byte. Consequence: the user's name is **data** (stored as the title / part name), never a filesystem path. Content on disk goes under server-generated ids. A zip input additionally needs zip-slip and zip-bomb defences (entry count, total uncompressed size, compression ratio).

**Unicode.** `Договор София.txt`, `Фактури Q1` must survive byte-for-byte through transport, storage, the query API and the export. That means UTF-8 end to end, no normalisation that rewrites the name, and comparisons that do not depend on the server's culture.

**Part ordering.** `page1`, `page2`, `page10` sort differently under ordinal vs. natural ordering (`page10` comes before `page2` ordinally). Both are defensible; the requirement is only that I pick one, document it, and have a test that pins it.

**Duplicate titles.** The acceptance criteria require uploading the sample material **twice**. That is 24 documents with 12 repeated titles, all of which must coexist. So title is not an identity — the id is. Deduplication is explicitly *not* wanted.

**Field length vs. folder name.** The folder name becomes the field value, so a long folder name can legitimately fail. `sample-material` is 15 characters and does not fit `Status` (maxLength 12). The right behaviour is a clear rejection naming the field and the limit, not a truncation.

**Size and memory.** The upload may be much larger than available memory, so the request body must be streamed to storage rather than buffered. No growing temp files left behind, no file handles left locked, cleanup even on failure.

**Partial failure.** If document 7 fails, documents 1–6 stay stored and the report says 7 failed. The unit of success is the document, not the upload. This has to be a deliberate decision because it is the opposite of a single transaction.

**Time zones.** `StoredAt` is set by the server. Stored in UTC; the calendar-date filtering in export is where the user's time zone has to be applied, so the upload side must not bake in a local offset.

## 5. Acceptance checklist for this feature

Each line needs a test that fails if the rule is broken.

- [ ] Top-level file → one document, one part, title = filename
- [ ] Top-level folder → one document, parts = its direct files
- [ ] Part order matches the documented order (`page1`/`page2`/`page10` case)
- [ ] Nested folder contents ignored **and** present in the report as skipped
- [ ] Empty folder → document with zero parts, not an error
- [ ] Zero-byte file → document with one empty part
- [ ] Chosen field set to the uploaded folder name on every document
- [ ] Write to a non-writable field rejected
- [ ] Write to a non-text or non-existent field rejected
- [ ] Folder name longer than the field's maxLength rejected with a clear message
- [ ] Unicode names round-trip unchanged
- [ ] Traversal / absolute / reserved names neutralised, not written to disk as paths
- [ ] Two uploads of the same folder → both sets of documents exist
- [ ] Report totals reconcile: stored + skipped + failed accounts for every entry seen
- [ ] System fields set by the server; client-supplied values ignored or rejected

## 6. Open questions to settle before coding

1. **Transport**: zip upload, or multipart with relative paths? (Affects client, validation surface, and streaming.)
2. **Part order**: ordinal (`StringComparer.Ordinal`) or natural/numeric-aware?
3. **Title for files**: with extension (`invoice-001.txt`) or without (`invoice-001`)? The brief says "the name of the file", which reads as *with*.
4. **Failure granularity**: per-document commit (report-and-continue) or reject the whole upload on the first failure?
