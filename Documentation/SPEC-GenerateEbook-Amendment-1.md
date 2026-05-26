# Amendment 1: Generate Ebook Spec — Data Source Correction
**Applies to:** SPEC-GenerateEbook.md  
**Sections affected:** §2 Inputs, §5 BookML→EPUB Mapping, §9 Implementation Approach  
**Reason:** Original spec assumed the EPUB builder would consume BookML XML or parsed DOM objects.  
The editor has no runtime XML representation. The DB is the sole source of truth.  
The EPUB builder must query the relational DB directly, exactly as `BookmlExporter` does.

---

## Corrected §2 — Inputs

| Input | Source | Notes |
|-------|--------|-------|
| Paragraph content | `Paragraph.ParagraphText` | Plain prose string — no markup to parse |
| Paragraph type | `ParagraphVersion.ParaType` | Distinguishes body, chapter heading, scene break, etc. |
| Display order | `ParagraphVersion.Seq` | Ascending integer — sort by this; do not expose in output |
| Chapter grouping | Foreign key from `ParagraphVersion` to chapter | Used to group paragraphs into per-chapter XHTML files |
| Book / project metadata | Project config table (same source as `BookmlExporter`) | Title, author, language, UUID |
| Copyright config | UI form fields (unchanged from original spec §4) | Rights holder, year, ARC flag |
| Cover image | User file picker | JPEG or PNG |

**Removed from inputs:** BookML XML files, parsed DOM objects, `BookmlImporter` output.  
XML is never present in the runtime path. Do not introduce an XML parse step.

---

## Corrected §5 — Data Source Mapping (replaces "BookML → EPUB Mapping")

Rename this section to **"DB → EPUB Mapping"**.

### 5.1 Metadata (`content.opf`)

Query the same project metadata fields that `BookmlExporter` uses for the `<book>` element attributes.  
No change to OPF output fields — only the source changes from XML attributes to DB columns.

### 5.2 Spine order

Order chapters by their natural sequence in the DB.  
Within each chapter, order paragraphs by `ParagraphVersion.Seq` ascending.  
`Seq` is a display ordinal only — it must not appear in any EPUB output (same constraint as PIDs;  
see original AC-9, which remains unchanged).

### 5.3 Chapter content → XHTML (replaces "Chapter XML → XHTML")

`ChapterRenderer.js` receives an array of plain-object rows from the DB query, not XML.  
Each row has the shape:

```javascript
{
  seq:       1000,              // integer, sort key only
  paraType:  'body',           // see type table below
  text:      'The quick...',   // ParagraphText — plain prose string
}
```

**ParaType mapping:**

| `ParaType` value | XHTML output |
|------------------|--------------|
| `'body'` | `<p>{text}</p>` |
| `'chapter-title'` | `<h1 class="chapter-title">{text}</h1>` |
| `'chapter-number'` | `<p class="chapter-number">{text}</p>` |
| `'scene-break'` | `<hr class="scene-break"/>` (ignore `text` value) |

> **Note to Claude Code:** Confirm the exact `ParaType` string values used in the DB against  
> the values written by `BookmlImporter`. Use those exact strings in the mapping — do not guess.  
> If a `ParaType` is encountered that is not in the table above, emit an HTML comment  
> `<!-- unknown paraType: {value} -->` and skip the paragraph, rather than crashing.

**Inline markup:** `ParagraphText` is stored as plain prose. There is no inline markup  
(no `<em>`, no `<strong>`) to translate. Wrap each paragraph's text in the appropriate  
block element and HTML-escape the string. If the project later adopts inline markup  
conventions, this function is the single place to extend.

**No XML parsing step.** Do not pass paragraph text through an XML parser.  
Use `document.createTextNode()` or equivalent safe text-node API to insert prose,  
not `innerHTML` or string concatenation, to prevent any accidental XSS or  
well-formedness breakage in the XHTML output.

### 5.4 – 5.5 (Navigation, NCX)

Unchanged. Chapter titles for ToC generation come from the `'chapter-title'` rows  
in the DB query, not from XML attributes.

---

## Corrected §9 — Implementation Approach

### Module structure (revised)

```
src/
  export/
    epub/
      EpubBuilder.js       ← orchestrates full build; entry point for UI
      EpubQueryService.js  ← NEW: queries DB and returns structured chapter data
      OpfGenerator.js      ← produces content.opf (unchanged)
      NavGenerator.js      ← produces nav.xhtml and toc.ncx (unchanged)
      ChapterRenderer.js   ← DB row array → XHTML (revised: no XML input)
      CopyrightPage.js     ← unchanged
      CoverPage.js         ← unchanged
      epubStyles.js        ← unchanged
      epubUtils.js         ← unchanged
```

### New module: `EpubQueryService.js`

Responsible for all DB access. Mirrors the data-gathering layer of `BookmlExporter`  
and should reuse any existing query helpers or repository classes from that exporter  
rather than duplicating SQL.

Returns a structure of the form:

```javascript
{
  metadata: {
    title:    'Carrier to Yesterday',
    author:   'Steve Avis',
    language: 'en',
    uuid:     'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx',
  },
  chapters: [
    {
      chapterId:  'CH01',
      paragraphs: [
        { seq: 1000, paraType: 'chapter-number', text: 'Chapter One' },
        { seq: 2000, paraType: 'chapter-title',  text: 'Madrid, Tuesday' },
        { seq: 3000, paraType: 'body',           text: 'The quick brown...' },
        // ...
      ],
    },
    // ...
  ],
}
```

### Revised `EpubBuilder.js` entry point signature

```javascript
import { buildEpub } from './export/epub/EpubBuilder.js';

async function handleGenerateEbook() {
  const blob = await buildEpub({
    // No XML input. Builder queries DB internally via EpubQueryService.
    copyright: {
      author:        rightsHolder,   // from UI field
      year:          copyrightYear,  // from UI field
      arcDisclaimer: arcEnabled,     // from UI checkbox
    },
    coverImageFile,                  // File object from picker, or null
  });

  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `${slugify(metadata.title)}_${dateStamp()}.epub`;
  a.click();
  URL.revokeObjectURL(url);
}
```

---

## Unchanged sections

The following sections of the original spec are **unaffected** by this amendment  
and should be implemented exactly as written:

- §3 Output (EPUB structure, filename pattern)
- §4 Copyright page (template, UI fields, ARC logic)
- §6 Stylesheet (`book.css`)
- §7 Cover page (`cover.xhtml`)
- §8 UI integration (button placement, config panel, generation flow UX)
- §10 Acceptance criteria (all ten ACs, including AC-9 — no PIDs or seq in output HTML)
- §11 Out of scope
- §12 Dependencies
- Notes for Claude Code (mimetype, well-formed XHTML, zero-padded filenames, full manifest)

---

## Implementation note: reuse `BookmlExporter` query logic

`BookmlExporter` already knows how to walk the DB and retrieve paragraphs in the  
correct order with the correct fields. `EpubQueryService` should call the same  
repository/service layer — not duplicate the SQL. If `BookmlExporter` exposes  
an intermediate data structure after its DB fetch but before its XML serialisation,  
that is the ideal tap-in point for `EpubQueryService`. If it does not, extract one.  
This will also benefit `BookmlExporter` itself (separation of query from serialisation).
