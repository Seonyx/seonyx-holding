# BookML Schema Amendment — Inline Presentation Elements

**Status:** Draft for review
**Affects:** `bookml-common.xsd`, `bookml-chapter.xsd`, runtime DB schema, importer, exporter, editor UI, analytics engine, EPUB/audiobook exporters
**Supersedes:** Nothing — net additive amendment

---

## 1. Background

Authored manuscripts contain presentation conventions (bold, italic, small caps) and depictions of in-fiction UI text (HUD banners, sponsored alerts, on-screen messages). These have so far passed through the pipeline as raw Markdown markers in text nodes, which is fragile: the markers are ambiguous (`*` is also a glyph), they survive into output unintentionally, and they cannot be styled differently per target.

This amendment formalises a small, closed vocabulary of **inline presentation elements** in BookML. The canonical principle is:

> The XML describes what the text *is*. The output stylesheets decide what it *looks like* on each target.

Markdown remains an authoring convenience at the plain-text import boundary only; it is normalised to typed XML elements at import and never reappears in the canonical store.

---

## 2. BookML Schema Additions

### 2.1 New inline element group (in `bookml-common.xsd`)

```xml
<xs:group name="InlineContent">
  <xs:choice minOccurs="0" maxOccurs="unbounded">
    <xs:element ref="emph"/>
    <xs:element ref="strong"/>
    <xs:element ref="smallcaps"/>
    <xs:element ref="overlay"/>
    <xs:element ref="break"/>
  </xs:choice>
</xs:group>

<xs:element name="emph">
  <xs:complexType mixed="true">
    <xs:group ref="InlineContent"/>
    <xs:attribute name="type" type="xs:string" use="optional"/>
  </xs:complexType>
</xs:element>

<xs:element name="strong">
  <xs:complexType mixed="true">
    <xs:group ref="InlineContent"/>
  </xs:complexType>
</xs:element>

<xs:element name="smallcaps">
  <xs:complexType mixed="true">
    <xs:group ref="InlineContent"/>
  </xs:complexType>
</xs:element>

<xs:element name="overlay">
  <xs:complexType mixed="true">
    <xs:group ref="InlineContent"/>
    <xs:attribute name="type" type="xs:string" use="required"/>
  </xs:complexType>
</xs:element>

<xs:element name="break">
  <xs:complexType/>
</xs:element>
```

### 2.2 Paragraph content model (in `bookml-chapter.xsd`)

The existing `<p>` element's content model becomes **mixed** — character data plus zero or more `InlineContent` elements:

```xml
<xs:element name="p">
  <xs:complexType mixed="true">
    <xs:group ref="InlineContent"/>
    <xs:attribute name="pid" type="PIDType" use="required"/>
    <xs:attribute name="seq" type="xs:int" use="required"/>
    <!-- other existing attributes preserved -->
  </xs:complexType>
</xs:element>
```

### 2.3 Element semantics

| Element | Meaning | Notes |
|---|---|---|
| `<emph>` | Italic / emphasis | Optional `@type` for semantic refinement (`thought`, `foreign`, `title`, `ship-name`) |
| `<strong>` | Bold / strong emphasis | No attributes |
| `<smallcaps>` | Small caps | Reserved for SFX or signage where the author wants them; rare in fiction |
| `<overlay type="...">` | Diegetic on-screen text | `@type` is a free-form classifier (`sponsored-alert`, `hud`, `system-message`, `subtitle`). Stylesheets switch on this. |
| `<break/>` | Hard line break inside a paragraph | Use sparingly; not a paragraph break |

### 2.4 Nesting rules

- Inline elements may nest, **except recursively into themselves** (no `<emph>` inside `<emph>`).
- `<overlay>` may contain any other inline element.
- `<overlay>` inside `<overlay>` is disallowed.

### 2.5 The `@type` vocabulary

Both `emph/@type` and `overlay/@type` use a controlled-but-extensible string vocabulary. Initial values:

- `emph/@type`: `thought`, `foreign`, `title`, `ship-name`
- `overlay/@type`: `sponsored-alert`, `hud`, `system-message`, `subtitle`

New values are added by the editor without schema changes. A reference list is maintained in `BookML-Inline-Types.md` (separate document).

---

## 3. Runtime Database Schema

The runtime remains fully decomposed relational; XML appears only at import/export. Inline marks are stored as **runs** — ordered, styled fragments of a paragraph's text.

### 3.1 New table

```sql
CREATE TABLE ParagraphRun (
    ParagraphRunID  INT IDENTITY(1,1) PRIMARY KEY,
    ParagraphID     INT NOT NULL,
    Seq             INT NOT NULL,                -- ordinal within paragraph
    RunText         NVARCHAR(MAX) NULL,          -- NULL only when IsBreak = 1
    IsEmph          BIT NOT NULL DEFAULT 0,
    EmphType        NVARCHAR(32) NULL,           -- non-null only when IsEmph = 1
    IsStrong        BIT NOT NULL DEFAULT 0,
    IsSmallCaps     BIT NOT NULL DEFAULT 0,
    OverlayType     NVARCHAR(32) NULL,           -- non-null => run is inside an overlay
    IsBreak         BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_ParagraphRun_Paragraph
        FOREIGN KEY (ParagraphID) REFERENCES Paragraph(ParagraphID)
        ON DELETE CASCADE,
    CONSTRAINT UQ_ParagraphRun_Para_Seq
        UNIQUE (ParagraphID, Seq),
    CONSTRAINT CK_ParagraphRun_BreakHasNoText
        CHECK (IsBreak = 0 OR RunText IS NULL),
    CONSTRAINT CK_ParagraphRun_EmphTypeOnlyWhenEmph
        CHECK (EmphType IS NULL OR IsEmph = 1)
);

CREATE INDEX IX_ParagraphRun_Paragraph ON ParagraphRun(ParagraphID, Seq);
```

### 3.2 Storage rules

- Every paragraph has **at least one** `ParagraphRun` row.
- A plain-text paragraph is exactly one run: all style flags 0, `OverlayType` NULL, text in `RunText`.
- Adjacent runs with identical style attributes are coalesced on write (importer/editor responsibility) — there should never be two adjacent rows differing only by `Seq`.
- The current text column on `Paragraph` is **deprecated** by this amendment. The runs become authoritative. See migration approach in §11.2.

### 3.3 Nested inline representation

Nested inlines are flattened onto the leaf run. A fragment like `<strong>SPONSORED <emph>ALERT</emph></strong>` becomes two runs:

| Seq | RunText | IsStrong | IsEmph |
|---|---|---|---|
| 1 | `SPONSORED ` | 1 | 0 |
| 2 | `ALERT` | 1 | 1 |

Overlay grouping is reconstructed at export time by detecting consecutive runs sharing the same non-null `OverlayType` value.

---

## 4. Importer Behaviour (Plain Text → DB)

### 4.1 Markdown subset

The importer accepts a deliberately small Markdown subset for inline marks:

| Markdown | Inline element |
|---|---|
| `**text**` | `<strong>text</strong>` |
| `*text*` | `<emph>text</emph>` |
| `_text_` | `<emph>text</emph>` |
| `\*`, `\_`, `\\` | Literal `*`, `_`, `\` |

CommonMark's full inline grammar is **not** supported. In particular:
- Triple-asterisk syntax (`***x***`) is rejected; use nested `**_x_**` or `*__x__*` instead.
- Underscores inside words (`foo_bar_baz`) are treated as literal underscores.
- Backslash-escape applies only to the three characters above.

**Small caps** (`<smallcaps>`) and **line breaks** (`<break/>`) have no Markdown shorthand. They are applied in the editor only. Rationale: there is no agreed Markdown convention for either; both are rare enough in fiction that requiring editor-only application costs nothing in practice while keeping the import grammar unambiguous.

### 4.2 Overlay handling

Overlays are **not** auto-detected from prose markers. The importer treats all bold/italic uniformly as `<strong>`/`<emph>`. Conversion of a span to `<overlay>` is an editor action (see §6).

Rationale: guessing overlay intent from text is brittle; the semantic decision belongs in the editor where the writer can confirm it.

### 4.3 Coalescing on write

After parsing, adjacent runs with identical style attributes are coalesced before insert. The importer never emits two consecutive rows differing only by `Seq`.

---

## 5. Exporter Behaviour (DB → BookML)

### 5.1 Algorithm

For each paragraph:

1. Fetch `ParagraphRun` rows ordered by `Seq`.
2. Walk runs, maintaining an open-overlay state.
3. When `OverlayType` changes between consecutive runs, close any open `<overlay>` and open a new one if the new value is non-null.
4. For each run, emit text wrapped in the appropriate combination of `<emph>` / `<strong>` / `<smallcaps>` tags (deterministic nesting order: `strong` outermost, then `emph`, then `smallcaps`).
5. For `IsBreak = 1` runs, emit `<break/>` with no text content.

### 5.2 Deterministic output

The exporter must produce byte-identical output for byte-identical run sequences. Tag-nesting order is fixed; whitespace handling is preserved verbatim from `RunText`.

### 5.3 Round-trip guarantee

Import → store → export must round-trip losslessly for the supported inline vocabulary. A round-trip test fixture is part of the acceptance criteria (§10).

---

## 6. Editor UI Impact

### 6.1 Toolbar additions

The paragraph editor gains:

- **Bold** (Ctrl+B) — toggles `<strong>` on selected span
- **Italic** (Ctrl+I) — toggles `<emph>` on selected span
- **Line break** (Shift+Enter) — inserts `<break/>`

### 6.2 Inline style context menu

A right-click context menu on a selected span offers:

- **Mark as overlay…** — opens a dialog with a type field (free text, autocomplete from previously-used values in the project) and OK/Cancel. The selected span's runs are updated with `OverlayType = <chosen value>`. **Remove overlay** is the inverse action when the selection is within an existing overlay.
- **Set emphasis type…** — for a selection within an `<emph>` span, assigns or changes the `@type` attribute. Autocomplete from project's known values.
- **Apply small caps / Remove small caps** — toggles `<smallcaps>` on selected span. No keyboard shortcut; deliberately tucked into the context menu as a low-frequency action.

### 6.3 Visual rendering in the editor

The editor renders inline marks visually (bold, italic, small caps via CSS). Overlays are rendered in a tinted inline pill so they are visually distinct from prose emphasis. Type labels appear on hover.

### 6.4 Internal model

The editor's in-browser model is a sequence of run objects matching the DB shape. On save, the model is diffed against the existing rows and the changes persisted; on load, rows are read directly into the model. JSON is acceptable here as client-side JavaScript state per established convention.

---

## 7. Analytics Engine Interaction

The analytics engine extracts paragraph text for statistical analysis (word frequency, n-grams, proximity echo, etc.). With inline runs in place:

### 7.1 Default extraction

By default, the analytics extractor returns the **concatenated `RunText` of all runs where `OverlayType IS NULL`**. Overlay content is excluded because it represents diegetic UI text, not authorial narrative voice, and would distort prose statistics.

Inline emphasis (`<emph>`, `<strong>`, `<smallcaps>`) does **not** affect extraction — the underlying text is included as normal prose.

### 7.2 Optional overlay inclusion

The extractor accepts an `includeOverlays` flag (default `false`) for cases where a user explicitly wants to analyse overlay text (e.g., to count sponsor mentions across a chapter).

### 7.3 Prose-text helper

Prose-text extraction is implemented as a method on `ParagraphRepository`:

```csharp
public string GetProseText(int paragraphId, bool includeOverlays = false)
{
    var runs = _context.ParagraphRuns
        .Where(r => r.ParagraphID == paragraphId
                 && !r.IsBreak
                 && (includeOverlays || r.OverlayType == null))
        .OrderBy(r => r.Seq)
        .Select(r => r.RunText)
        .ToList();

    return string.Concat(runs);
}
```

A bulk variant (`GetProseTextForChapter(int chapterId, bool includeOverlays)`) returns paragraph text in `seq` order for analytics ingestion. Both methods are unit-testable in isolation and consistent with the existing repository pattern.

---

## 8. Output Transform Notes (Forward-Looking)

Full output transform specs are out of scope for this amendment. Recorded here so Claude Code is aware of the mapping intent when revising EPUB/audiobook code:

### 8.1 EPUB

| BookML | XHTML | CSS hook |
|---|---|---|
| `<emph>` (no type) | `<em>` | `em` |
| `<emph type="X">` | `<em class="emph-X">` | `.emph-X` |
| `<strong>` | `<strong>` | `strong` |
| `<smallcaps>` | `<span class="smallcaps">` | `.smallcaps { font-variant: small-caps; }` |
| `<overlay type="X">` | `<aside class="overlay overlay-X">` | `.overlay`, `.overlay-X` |
| `<break/>` | `<br/>` | — |

### 8.2 Audiobook (Piper TTS)

- `<emph>`, `<strong>`, `<smallcaps>` → text passed through verbatim (Piper has no native emphasis prosody).
- `<overlay>` content is, by default, **skipped** in the generated chapter text. An option in the export dialog allows including overlays (with a brief pause before/after).
- `<break/>` → newline in the chapter text file.

---

## 9. Out of Scope

- **Scene breaks** (`***` on its own line, etc.) — block-level, not inline. Tracked separately.
- **Tables, lists, images** inside prose paragraphs — not a current fiction requirement.
- **Hyperlinks** in prose — deferred. If introduced later, will be a sixth inline element (`<link href="…">`).
- **Track-changes / comments at sub-paragraph level** — deferred; current revision tracking is paragraph-level.
- **ODT and PDF output transforms** — no immediate target; specs to be drafted when those paths are activated.

---

## 10. Acceptance Criteria

1. The BookML XSDs validate documents containing all five inline elements with attributes as specified.
2. `ParagraphRun` table is created with constraints and index as specified.
3. The plain-text importer with the §4.1 Markdown subset round-trips through `ParagraphRun` storage and the BookML exporter without loss of inline marks, verified by a fixture set of at least 20 paragraphs covering:
   - Plain text
   - Single `<strong>` span
   - Single `<emph>` span
   - Nested `<strong><emph>…</emph></strong>`
   - `<smallcaps>` (editor-applied, exported, re-imported)
   - `<break/>` mid-paragraph (editor-applied)
   - `<overlay>` containing mixed `<strong>`/`<emph>`
   - Escaped Markdown characters
4. The editor UI supports toolbar bold / italic / line break and the context-menu actions (overlay marking, emph type, small caps), and persists changes correctly.
5. The analytics extractor returns prose-only text by default and includes overlays only when `includeOverlays = true`.
6. Existing chapters in the database are migrated: every `Paragraph` gains at least one `ParagraphRun` row containing the original text (one row, no styling). The legacy text column on `Paragraph` is renamed (not dropped) by this migration.
7. The EPUB exporter emits inline marks per §8.1.
8. The audiobook export skips overlay content by default and offers an "include overlays" option.

---

## 11. Areas of the Book Editor Requiring Revision (Claude Code Advisory)

The following existing modules will require revision in light of this amendment. Listed in roughly the order they should be addressed:

### 11.1 BookML schema files

Update `bookml-common.xsd` and `bookml-chapter.xsd` per §2. Increment the schema version. Add inline element samples to schema documentation.

### 11.2 Database migration (two-step)

**Migration A** (delivered with this amendment): adds `ParagraphRun` with FK, constraints, and index; populates one default run per existing paragraph from the current text column; renames the existing text column on `Paragraph` to `<currentname>_Legacy`. Reversible.

> *Implementation note for Claude Code:* The current name of the paragraph text column has been referred to in this spec as `ContentText` but is unverified. Inspect the live schema and substitute the actual column name throughout the migration.

**Migration B** (delivered after acceptance criteria pass and all consumers updated): drops the `_Legacy` column. Not reversible.

### 11.3 `ParagraphRepository` (and related repositories)

- Read: load `ParagraphRun` rows alongside paragraphs (eager load).
- Write: replace single-text update with run-list diff-and-persist.
- The model returned upward to controllers should be a paragraph with an ordered list of runs.
- Add `GetProseText(...)` and `GetProseTextForChapter(...)` per §7.3.

### 11.4 BookML importer

Add the Markdown subset parser (§4). Output a sequence of runs per paragraph rather than a single text blob. Coalesce on write.

### 11.5 BookML exporter

Implement the §5 walk-and-emit algorithm. Add fixture-based round-trip tests.

### 11.6 Editor UI (front-end)

Toolbar additions (bold, italic, line break), context menus (overlay, emph type, small caps), overlay dialog, in-editor visual rendering, and the run-based JS model. CSS for overlay rendering in-editor. *(This is the largest single piece of work in the amendment and may warrant a separate session brief.)*

### 11.7 Analytics engine (Phase 1 dashboard)

Update text extraction to use `ParagraphRepository.GetProseText(...)`. Add the `includeOverlays` flag to the analysis request (default `false`). Existing test runs against CTY chapters will produce slightly different numbers once overlay content is excluded — this is expected and correct.

### 11.8 Character Sketch add-on

No direct impact on schema. However, the alias-extraction subquery should be updated to consume prose text via the repository method rather than reading the legacy text column directly.

### 11.9 EPUB exporter

Implement the §8.1 mapping. Add CSS classes to the EPUB stylesheet template for each known `@type` value.

### 11.10 Audiobook export (ZIP generator)

Update the chapter-text files included in the ZIP to honour the §8.2 behaviour. Document the overlay-handling option in the generated `README.txt`.

### 11.11 Work-order schema (Analytics Phase 2)

If work orders quote paragraph content as context for the model, they should quote the prose-only text and reference overlays separately if relevant. The work-order XSD may need a `<context-text>` and optional `<context-overlays>` distinction.

---

## 12. Decisions Captured

For audit and to make explicit the choices baked into this draft:

1. **Markdown import subset** is limited to `**bold**`, `*italic*`/`_italic_`, with backslash escapes for `*`, `_`, `\`. No syntax for small caps or line breaks; both are editor-only actions.
2. **Migration is two-step**: rename-then-drop the legacy paragraph text column across two migrations.
3. **Prose-text extraction is a C# repository method**, not a SQL view. Consistent with existing patterns, more testable, no DB-side code to manage.
4. **Initial `@type` vocabularies** are seeded with the values in §2.5. The editor extends them without schema changes.
5. **Overlays are editor-only**, never auto-detected from imported prose markers.
6. **EPUB → KDP** is the only output target currently in scope beyond the editor itself. ODT, PDF, and other targets are deferred.

---

*End of amendment.*
