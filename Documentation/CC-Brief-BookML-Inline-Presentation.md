# Claude Code Brief — Implement BookML Inline Presentation Elements

**Target codebase:** Seonyx (.NET Framework 4.6.2 / ASP.NET MVC 5 / EF6 / SQL Server — corrected from this brief's original "4.8" guess; see Preflight Corrections below)
**Target manuscript driver:** *Chilli Racers* — currently blocked on inline mark handling
**Branch:** `main` (this repo does not use feature branches for this work; see Preflight Corrections)
**Estimated phases:** Six, described below
**Status:** P1 (schema/migration/access-layer) and P2 (importer/exporter round-trip) complete. P3–P6 not started.

---

## Preflight Corrections (post-P1/P2)

This brief's original assumptions about the codebase were largely
guesses, as brief §1.2 itself anticipated ("Names of existing tables,
columns, files, folders, namespaces, and helper classes referenced in
this brief or the amendment are **best-effort guesses**"). Preflight
investigation before P1 and P2 confirmed the actual shape. Recorded
here so a fresh reader doesn't have to re-derive it:

- **No multi-project solution split.** The brief's §2 preflight
  checklist and per-phase file lists assume projects named
  `Seonyx.Data`, `Seonyx.BookML`, `Seonyx.Analytics`, `Seonyx.Export.Epub`,
  `Seonyx.Export.Audiobook`, `Seonyx.Web`. None of these exist. The web
  app is a single monolithic `Seonyx.csproj` (root namespace
  `Seonyx.Web`); a separate, code-unshared `ContentAnalyserCli`/
  `ContentAnalysisEngine`/`ContentAnalysisHarness` cluster handles
  analytics and is not project-referenced by the web app.
- **No repository pattern.** `ParagraphRepository` (referenced
  throughout §3 and §11 of the amendment) does not exist and was not
  created. All DB access is direct `SeonyxContext` (EF6 `DbContext`)
  usage from controllers and services — the established, intentional
  convention in this codebase. P1 added `Services/ParagraphRunService.cs`
  instead, following the same `Services/`-folder pattern as
  `BookmlImporter`/`BookmlExporter`/`EpubExporter`/`AudiobookPackageBuilder`.
- **No test projects anywhere in the solution** (confirmed in
  `CLAUDE.md` too). Every reference in this brief to `*.Tests` projects
  (`Seonyx.Data.Tests`, `Seonyx.BookML.Tests`, `Seonyx.Web.Tests`) is
  void. P1 verification was manual SQL data-integrity checks; P2
  verification is the standalone `RoundTripCheckCli` console utility
  (see Deferred Decisions below) plus manual editor/export checks —
  neither uses xUnit/NUnit/MSTest.
- **Migrations live in `Database/`** (top level, capital D), not
  `db/migrations/`. Naming is descriptive kebab/snake_case
  (`add-chapter-sortorder.sql`, `fix-pidversion-unique.sql`), no numeric
  prefixes, and scripts are **forward-only** with idempotency guards —
  there are no Up/Down reversible pairs anywhere in this repo, contrary
  to §2 task 3 and §3's file-list assumptions.
- **Single `main` branch**, no feature-branch workflow — §1.6's
  "single feature branch, phase per commit group" was followed in
  spirit (each phase is its own clean commit, e.g. `ae5ad6d` for P1)
  but committed directly to `main` per this repo's actual git
  convention, not to `feature/bookml-inline-presentation`.
- **.NET Framework is 4.6.2, not 4.8** — corrected in the header above.
  This matters for the RoundTripCheckCli target choice below.

---

## Deferred Decisions

- **`RoundTripCheckCli` targets `net462`, not `net8.0`.** The
  `ContentAnalyserCli` precedent (§9 of this brief, and the amendment's
  own §12.3 rationale for repository-free, testable helper methods)
  suggested a modern-.NET standalone console utility, source-linking
  engine code the way `ContentAnalyserCli` links `ContentAnalysisEngine`.
  That precedent doesn't transfer directly: `ContentAnalyserCli`/
  `ContentAnalysisEngine` both run on a machine (Binky) where the .NET
  version is fully under local control, so cross-runtime portability is
  free. `RoundTripCheckCli`'s source (`MarkdownInlineParser.cs`,
  `InlineXmlRunConverter.cs`, `RunsToMarkdownSerializer.cs`) is
  source-linked from `Services/`, which must also compile inside the
  web app — and **Seonyx is pinned to .NET Framework 4.6.2 by its
  shared hosting provider** (mywindowshosting.com, contract through
  Feb 2027). Targeting `net8.0` for the CLI while the linked source
  must also satisfy `net462`'s narrower BCL would be a real long-term
  constraint on that source (no `System.Text.Json`, no newer LINQ, no
  records, no init-only properties) — worse than just targeting `net462`
  for one small standalone CLI. `RoundTripCheckCli.csproj` therefore
  targets `net462`, matching `Seonyx.csproj` exactly.
- **Scheduled review: Feb 2027 hosting contract renewal.** If Seonyx
  migrates off shared hosting or off .NET Framework at that point,
  revisit this constraint — `RoundTripCheckCli` (and the source-linked
  parser/converter/serializer classes) can retarget to a modern TFM and
  the parser can start using modern BCL features once the web app no
  longer needs Framework 4.6.2 compatibility.

---

## 0. Authoritative sources

Two documents define this work:

1. **`docs/specs/BookML-Inline-Presentation-Amendment.md`** — the schema amendment (what to build). *Steve will place this in the repo before you start; confirm its presence during preflight.*
2. **This brief** — the execution plan (how and in what order to build it).

Read the amendment first. Every technical decision (schema, DB shape, importer rules, exporter algorithm, editor semantics, analytics behaviour, output mappings, acceptance criteria) is specified there. This brief tells you the phasing, working rules, per-phase scope boundaries, and when to stop and ask.

If the amendment and this brief conflict on a technical detail, the amendment wins.
If they conflict on ordering, phasing, or scope boundaries, this brief wins.

---

## 1. Working rules — read before writing any code

### 1.1 Do not add unrequested work

If you notice adjacent improvements you would like to make — refactoring, extra endpoints, extra fields, "while I'm here" cleanup, generalising a helper, updating an unrelated NuGet package — **do not do them**. List them in a `NOTES-adjacent-improvements.md` file at the branch root and continue with the specified work. Steve will decide separately what, if anything, to pull in.

### 1.2 Verify before assuming

Names of existing tables, columns, files, folders, namespaces, and helper classes referenced in this brief or the amendment are **best-effort guesses**. Before writing migrations or touching a module, inspect the live code and use the real names. Specifically:

- The paragraph text column: the amendment calls it `ContentText`; verify against `INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Paragraph'`.
- Project/folder structure: this brief refers to projects like `Seonyx.Data`, `Seonyx.BookML`, `Seonyx.Analytics`, `Seonyx.Export.Epub`, `Seonyx.Web`. Verify against the actual `.sln` file and adjust references throughout.
- Migration file numbering: Seonyx uses manual SQL migration scripts under `db/migrations/` (or similar). Inspect the highest existing number and follow the existing pattern. Do not invent a new convention.

### 1.3 Do not switch Seonyx to EF6 Code First Migrations

Seonyx uses manual SQL migration scripts. HexWriter (a separate project not touched here) uses EF6 Code First. Do not conflate them. All DB changes in this work are hand-written `.sql` files following the existing Seonyx pattern.

### 1.4 No new NuGet packages without approval

This is a one-person commercial operation; every added dependency is a maintenance liability. If a phase seems to require a new package (parsing, testing, or otherwise), stop and ask Steve first. Prefer standard library and existing dependencies.

### 1.5 XML on the server, JSON only in browser JS

Server-side schemas, manifests, and file formats are XML. JSON is acceptable only for client-side JavaScript state (e.g., the paragraph editor's in-memory run model). Do not introduce JSON contracts on the server.

### 1.6 Single feature branch, phase per commit group

Work on `feature/bookml-inline-presentation`. Each phase is one or more commits that pass CI. Do not merge to `main`; Steve will review and merge per phase. Commit message prefix: `P<n>: <module>: <change>` (e.g., `P1: db: add ParagraphRun migration`).

### 1.7 Stop-and-ask triggers

Prefix any question to Steve with `❓ STOP-AND-ASK:` so he can find them. Stop and ask when:

- A module the amendment or brief refers to cannot be found under the expected path or a near variant.
- An existing test would need its **semantics** changed (not just its data shape or fixture values).
- You are unsure whether a change is in scope for the current phase.
- Something in the amendment is ambiguous when read against the real code.
- You believe a design decision in the amendment is wrong. (Do not silently override it; flag it, argue the case, and wait.)

### 1.8 Preserve existing patterns

Repository-per-aggregate, EF6 entity configuration in a central `SeonyxContext`, controller-thin/service-thicker MVC pattern. If in doubt, mimic the shape of the nearest existing equivalent module.

---

## 2. Preflight (before starting Phase P1)

Perform these checks and post the results to Steve. Do not start P1 until the checks come back consistent with the plan.

1. Confirm `docs/specs/BookML-Inline-Presentation-Amendment.md` exists in the repo. Note its git commit hash for the phase reports.
2. Inspect the `Paragraph` table schema. Report the actual name of the text column.
3. Inspect `db/migrations/` (or equivalent). Report the migration numbering convention and the highest existing number.
4. Inspect the solution's project list. Report the real project names for: Data layer, BookML import/export, Analytics, EPUB export, Audiobook export, Web/MVC.
5. Inspect the existing `ParagraphRepository` (or the closest equivalent). Report its current methods and the pattern it uses for eager loading of related entities.
6. Inspect the existing paragraph editor JS. Report whether the current model treats paragraph content as a plain string or as some structured form. (This determines the size of the P3 refactor.)
7. Inspect the BookML XSD files (`bookml-common.xsd`, `bookml-chapter.xsd`, and any others listed in memory). Confirm they are present and note their location.
8. List any items above where the reality diverges materially from the amendment's assumptions, before proceeding.

Post the preflight report to Steve as a single reply. Wait for `preflight approved`.

---

## 3. Phase P1 — Schema + DB migration + repository (backward compatible)

### Objective
`ParagraphRun` exists in the DB, populated one-run-per-existing-paragraph from the current text column; the legacy column is renamed but retained; `ParagraphRepository` can serve run-based reads and writes. Nothing user-visible changes yet.

### Files affected (adjust names to reality per preflight)
- `db/migrations/<next>_AddParagraphRun_Up.sql` (new)
- `db/migrations/<next>_AddParagraphRun_Down.sql` (new)
- `Seonyx.Data/Models/ParagraphRun.cs` (new)
- `Seonyx.Data/SeonyxContext.cs` (add DbSet + fluent configuration)
- `Seonyx.Data/Repositories/ParagraphRepository.cs` (extend)
- `Seonyx.Data.Tests/Repositories/ParagraphRepositoryTests.cs` (extend or new)

### Tasks
1. Write the up-migration per amendment §3.1: table, all constraints, index. Include the data-population step that inserts one `ParagraphRun` per existing paragraph with the paragraph's current text, all style flags 0, `OverlayType` NULL, `Seq` 1. Rename the paragraph text column to `<name>_Legacy` at the end of the same migration.
2. Write the down-migration: drop `ParagraphRun`; rename `<name>_Legacy` back to its original name.
3. Add the `ParagraphRun` entity class matching the schema exactly. EF6 fluent mapping in `SeonyxContext`.
4. Extend `ParagraphRepository` with:
    - `IList<ParagraphRun> LoadRuns(int paragraphId)` — ordered by `Seq`.
    - `void SaveRuns(int paragraphId, IList<ParagraphRun> runs)` — diff-and-persist, enforcing the coalescing rule (no adjacent rows differing only by `Seq`). Coalescing violations throw a clear exception; the caller is expected to have coalesced.
    - `string GetProseText(int paragraphId, bool includeOverlays = false)` per amendment §7.3.
    - `string GetProseTextForChapter(int chapterId, bool includeOverlays = false)` — bulk variant returning concatenated prose with paragraph boundaries preserved as `\n\n`.
5. Unit tests for each repository method, including:
    - Load/save round-trip preserves all fields.
    - Coalescing enforcement rejects adjacent identical-style runs.
    - `GetProseText` correctly excludes overlay content by default and includes it when the flag is set.
    - `IsBreak` runs are omitted from prose text.
6. Integration test: run the up-migration against a copy of a representative test database (Steve will provide a snapshot if asked). Verify every existing paragraph has exactly one run and that its `RunText` matches the legacy column byte-for-byte for a random sample of 100 paragraphs.

### Not in scope for P1
- No importer, exporter, editor UI, EPUB, or audiobook changes.
- The legacy column is renamed, not dropped. Dropping happens in P6.
- No consumers are switched to read from runs yet; all existing reads continue to hit the legacy column.

### Shippable milestone
DB migrated; runs populated; repository serves run-based reads and writes; nothing else changes; production remains functional against the legacy column.

---

## 4. Phase P2 — Importer + exporter (round-trip)

### Objective
The BookML importer produces runs from Markdown-marked plain text per amendment §4; the BookML exporter emits inline elements from runs per §5. End-to-end round-trip works against fixture data and real *Chilli Racers* content.

### Files affected
- `Seonyx.BookML/Import/MarkdownInlineParser.cs` (new)
- `Seonyx.BookML/Import/BookMLImporter.cs` (extend)
- `Seonyx.BookML/Export/BookMLExporter.cs` (extend)
- `Seonyx.BookML/Schemas/bookml-common.xsd` (extend per amendment §2.1)
- `Seonyx.BookML/Schemas/bookml-chapter.xsd` (extend per amendment §2.2)
- `Seonyx.BookML.Tests/RoundTripFixtures/` (new — 20-paragraph fixture set)
- `Seonyx.BookML.Tests/RoundTripTests.cs` (new)

### Tasks
1. Update the XSDs per amendment §2. Increment the schema version comment. Add sample paragraphs illustrating each new inline element to the schema documentation.
2. Implement `MarkdownInlineParser` for the exact subset in amendment §4.1:
    - `**strong**`, `*emph*`, `_emph_`
    - Backslash escapes for `*`, `_`, `\`
    - Triple-asterisk rejected (throw parse exception with paragraph PID in the message)
    - Mid-word underscores treated as literal
    - No small caps or line break syntax (both are editor-only, per amendment §4.1)
3. Extend `BookMLImporter` to route each paragraph's text through the parser and persist runs via `ParagraphRepository.SaveRuns`. Coalesce adjacent identical-style runs before calling the repository.
4. Implement the exporter algorithm per amendment §5.1:
    - Deterministic tag-nesting order: `strong` outermost, then `emph`, then `smallcaps`.
    - Overlay grouping: consecutive runs sharing the same non-null `OverlayType` value form one `<overlay>` element.
    - `IsBreak` runs emit `<break/>` with no text.
5. Create the 20-paragraph round-trip fixture set covering every case listed in amendment §10.3.
6. Round-trip test: for each fixture, plain text → import → runs → export → BookML → assert byte-identical to the canonical form. Include a fixture where an overlay contains mixed strong/emph.

### Not in scope for P2
- No editor UI changes (that is P3).
- No overlay auto-detection (overlays are editor-only per amendment §4.2).
- No changes to output transforms (EPUB/audiobook are P5).

### Shippable milestone
Reimporting a BookML manuscript through this code path produces correctly-marked runs; re-exporting them produces correct BookML. Round-trip fixtures pass in CI.

### Optional smoke test at end of P2
Steve will manually reimport the current *Chilli Racers* draft and spot-check a chapter. This is not an automated test; do not include Chilli Racers content in the repo.

---

## 5. Phase P3 — Editor UI

### Objective
Steve can bold, italicise, insert line breaks, mark overlays, and set emphasis types in the paragraph editor. *Chilli Racers* editing is unblocked.

*This is the largest single phase. If it exceeds one Claude Code session, break at the natural boundary between "toolbar and rendering" (tasks 1–3, 6–7) and "context menus and overlay dialog" (tasks 4–5). Commit and pause between the two halves.*

### Files affected
- `Seonyx.Web/Views/Chapter/Edit.cshtml` (extend)
- `Seonyx.Web/Scripts/paragraph-editor.js` (extend or refactor to run-list model)
- `Seonyx.Web/Content/paragraph-editor.css` (extend)
- `Seonyx.Web/Controllers/ChapterController.cs` (endpoints for run save/load)
- `Seonyx.Web.Tests/Controllers/ChapterControllerTests.cs` (extend)

### Tasks
1. **JS in-editor model:** paragraph = ordered list of run objects
   `{ text, isEmph, emphType, isStrong, isSmallCaps, overlayType, isBreak }`.
   If the current JS treats paragraph content as a plain string, refactor to the run-list model. That refactor is in scope; do not simultaneously touch unrelated JS.
2. **Toolbar buttons:** Bold (Ctrl+B), Italic (Ctrl+I), Line break (Shift+Enter). Each toggles the appropriate style on the current selection. Toggling on a mixed-style selection normalises to the styled state.
3. **In-editor visual rendering:** bold, italic, small caps via CSS. Overlays rendered as tinted inline pills with the type label shown on hover.
4. **Context menu on a selected span:**
    - "Mark as overlay…" / "Remove overlay"
    - "Set emphasis type…"
    - "Apply small caps" / "Remove small caps"
5. **Dialogs:**
    - Overlay dialog: type field with autocomplete from `OverlayType` values previously used in the current book.
    - Emphasis type dialog: same pattern, autocompleting from `EmphType` values in the current book.
6. **Save endpoint:** accepts the run list, coalesces on the server, calls `ParagraphRepository.SaveRuns`.
7. **Load endpoint:** returns the run list from `ParagraphRepository.LoadRuns`.
8. **Tests:** controller-level save/load round-trip; a JS-level test (existing test harness) exercising the run model's toggle operations if the harness supports it.

### Not in scope for P3
- No changes to chapter/book navigation UI.
- No visual redesign of the editor beyond what is needed for inline mark rendering.
- No changes to unrelated JS modules.

### Shippable milestone
*Chilli Racers* editing works end-to-end with inline marks. Steve resumes manuscript review at this point.

---

## 6. Phase P4 — Analytics engine update

### Objective
The analytics engine uses run-based prose text extraction; overlays are excluded by default.

### Files affected
- `Seonyx.Analytics/TextExtractor.cs` (or equivalent)
- `Seonyx.Analytics/AnalysisRequest.cs` (add `IncludeOverlays` option)
- `Seonyx.Web/Views/Analytics/*.cshtml` (add overlay-exclusion indicator)
- `Seonyx.Analytics.Tests/`

### Tasks
1. Switch text extraction from the legacy paragraph column to `ParagraphRepository.GetProseTextForChapter(chapterId, includeOverlays: request.IncludeOverlays)`.
2. Add `IncludeOverlays` to the analysis request (default `false`).
3. On the analytics results view, display a small indicator noting whether overlays were included in the current run (so Steve can see at a glance what he's looking at).
4. Rerun the existing CTY test analyses. Numbers will shift on any chapter with overlays; document the deltas in a `P4-numeric-drift.md` note file. Expected and correct — the shift is the point.

### Not in scope for P4
- No changes to analytics algorithms.
- No changes to analytics dashboard layout beyond the overlay indicator.

### Shippable milestone
Analytics operates on prose-only text by default; overlay inclusion is a user-selectable option; drift on CTY chapters is documented.

---

## 7. Phase P5 — EPUB and audiobook exporters

### Objective
EPUB output honours inline mappings per amendment §8.1; audiobook honours §8.2.

### Files affected (corrected at P5 planning time — see Preflight Corrections above; no `Seonyx.Export.*` projects or test projects exist)
- `Services/EpubExporter.cs` — inline mapping transform
- EPUB stylesheet template (find actual path at P5 planning time) — new CSS classes
- `Services/AudiobookPackageBuilder.cs` — chapter text file generator
- No corresponding test projects (none exist); verification approach TBD at P5 planning time, likely following the P2 precedent (manual checks, possibly a small standalone utility) rather than assuming xUnit/NUnit/MSTest

### Tasks
1. **EPUB:** implement the mapping table in amendment §8.1. Emit CSS classes for each known `@type` value on `<emph>` and `<overlay>`. Add fixture test with a chapter containing every element type.
2. **EPUB CSS:** add default styles for `.smallcaps` (`font-variant: small-caps`), `.overlay` (a tinted callout box — pick reasonable defaults; Steve can restyle later), and one class per known overlay type.
3. **Audiobook:** emph/strong/smallcaps pass through as plain text (Piper has no emphasis prosody). Overlay content skipped by default; option to include with a brief pause marker before/after. `<linebreak/>` becomes a newline in the chapter text file (renamed from `<break/>` — see amendment §2.1 implementation note).
4. Update the generated `README.txt` in the audiobook ZIP to describe the overlay-handling option.

### Not in scope for P5
- No changes to EPUB metadata, TOC generation, or cover handling.
- No changes to audiobook chunking or voice selection.

### Shippable milestone
A *Chilli Racers* EPUB export renders overlays as tinted asides in a reading app (Steve confirms visually in Calibre or a device); audiobook export omits overlay content by default.

---

## 8. Phase P6 — Migration B: drop legacy column

### Objective
The `<name>_Legacy` paragraph text column is dropped from the schema. Pure cleanup.

### Prerequisite
Phases P1–P5 merged, deployed, and observed working in Steve's live workflow. Steve's call on timing — do not start P6 until he explicitly says so.

### Files affected
- `db/migrations/<next>_DropParagraphLegacyText_Up.sql` (new)
- `db/migrations/<next>_DropParagraphLegacyText_Down.sql` (new)

### Tasks
1. Grep the whole codebase for any references to the legacy column name. If any are found, stop and report — do not run the migration. The rename should have surfaced them, but this is the final check.
2. Write the up-migration: `ALTER TABLE Paragraph DROP COLUMN <name>_Legacy`.
3. Write the down-migration: re-add the column; repopulate from runs via a SQL equivalent of `GetProseText(paragraphId, includeOverlays: true)` — full-fidelity repopulation to maximise rollback safety.

### Not in scope for P6
Anything else. This is a pure schema cleanup.

### Shippable milestone
Schema fully migrated to the new model. Amendment complete.

---

## 9. Phase reporting

At the end of each phase, before Steve reviews the PR, post a single reply containing:

1. **Files touched** — a bare list.
2. **Decisions made not explicit in the brief** — one line each, with rationale.
3. **Adjacent improvements noted** — items added to `NOTES-adjacent-improvements.md`, if any.
4. **STOP-AND-ASK items resolved during work** — with a one-line summary of how each was resolved.
5. **Test results** — passing test counts; any deliberately-skipped tests with reason.
6. **Acceptance criteria met** — checkbox against the relevant items in amendment §10.

Do not start the next phase without Steve's explicit `P<n> approved, proceed to P<n+1>`.

---

## 10. Definition of done (overall)

- All six phases merged.
- Every acceptance criterion in amendment §10 is met.
- *Chilli Racers* is editable in the browser with inline marks handled correctly.
- An EPUB export of a chapter containing overlays renders correctly in Calibre or a physical device (Steve confirms visually).
- The analytics dashboard shows the "overlays excluded" indicator by default.
- `NOTES-adjacent-improvements.md` is reviewed with Steve — items either dropped, backlogged, or promoted to their own briefs.

---

*End of brief.*
