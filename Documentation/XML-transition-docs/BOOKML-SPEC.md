# BookML Specification v1.0

**Namespace:** `https://bookml.org/ns/1.0`  
**Schema suite:** bookml-common.xsd · bookml-book.xsd · bookml-chapter.xsd · bookml-meta.xsd · bookml-notes.xsd  
**Status:** Draft  

---

## 1. Purpose and Scope

BookML is an XML vocabulary for the interchange of book-length prose works between AI generation systems, human editors, and publishing applications. It is designed to be:

- **Unambiguous** — every element has one interpretation; AI generators will not drift
- **Relational** — paragraph-level keys link content, metadata, and annotations across files
- **Versionable** — full draft lineage is a first-class feature, not an afterthought
- **Extensible** — fiction works today; non-fiction, images, tables, and footnotes are already in the schema

---

## 2. Design Principles

1. **Separation of concerns** — prose, metadata, and annotations live in separate files joined by key
2. **Immutable identity, mutable order** — `pid` never changes; `seq` is freely rebalanced
3. **Snapshots, not diffs** — complete XML files are stored per draft; diffs are derived on demand
4. **Never delete, only resolve** — notes and drafts accumulate; resolved items are flagged not removed
5. **Self-describing files** — every file carries enough context to be understood without its siblings

---

## 3. File Architecture

One complete work consists of the following files:

```
{work-root}/
  book.xml                        ← Book manifest (one per work)
  assets/
    covers/
      cover-full.{jpg|png}        ← Full resolution cover
      cover-thumb.{jpg|png}       ← Catalogue thumbnail
    author/
      author-photo.{jpg|png}      ← Author photograph (square crop)
  {chapter-id}/
    {chapter-id}-chapter.xml      ← Prose content
    {chapter-id}-meta.xml         ← Paragraph-level metadata
    {chapter-id}-notes.xml        ← Annotations and notes
```

### 3.1 Naming Conventions

- Chapter folder IDs: lowercase, no spaces. Examples: `ch01`, `ch02`, `fm-preface`, `bm-appendix-a`
- File names follow the pattern `{chapter-id}-{type}.xml`
- Asset paths are relative to the work root (the location of `book.xml`)
- All XML files use UTF-8 encoding

---

## 4. The PID — Paragraph Identity Key

The `pid` is the most important concept in BookML. It is the immutable foreign key that joins the three chapter files together.

### 4.1 Format

```
{CHAPTERID}-{TYPECODE}{SEQUENCE}

Examples:
  CH01-P0010    Chapter 01, paragraph 10
  CH01-H0005    Chapter 01, heading 5
  CH01-F0001    Chapter 01, figure 1
  CH01-EP001    Chapter 01, epigraph paragraph 1
  CH01-B0001    Chapter 01, break marker 1
```

### 4.2 Type codes

| Code | Element      |
|------|-------------|
| P    | Paragraph    |
| H    | Heading      |
| F    | Figure       |
| T    | Table        |
| EP   | Epigraph paragraph |
| B    | Break marker |
| FN   | Footnote anchor |

### 4.3 Rules

- **Assigned once at generation time. Never changed under any circumstances.**
- Sequence reflects generation order, not display order
- All uppercase
- Zero-padded to 4 digits (3 digits acceptable for EP and FN codes)
- Used as foreign key in `para-meta[@pid]` and `para-notes[@pid]`
- Chapter ID prefix ensures global uniqueness across the whole work

---

## 5. The SEQ — Display Ordinal

The `seq` attribute governs display ordering and is entirely separate from `pid`.

### 5.1 Rules

- AI generators assign `seq` values in **multiples of 1000** (1000, 2000, 3000...)
- `seq` values are unique within their parent element scope
- When a gap between adjacent elements falls below 10, the application **must** rebalance by reassigning multiples of 1000 across the affected section
- Rebalancing **must not** alter any `pid` value or any cross-file reference
- After rebalancing, the application **must** update `<modified>` in `book.xml`
- `seq` is never used as a foreign key

---

## 6. Versioning and Draft Lineage

### 6.1 The Snapshot Model

- The **database** is the working copy — mutable, actively edited
- An **XML export** is a snapshot — immutable once created with status `snapshot`
- **Diffs** are derived on demand by comparing two snapshots; they are never stored
- The importer **never overwrites** existing draft records; it only adds new ones

### 6.2 Draft Workflow

```
Database (working copy)
      │  Export
      ▼
Draft N XML ─────────────────────── Archived (status: snapshot)
      │
      │  Human edit / AI critique
      ▼
Draft N+1 XML ───────────────────── Archived (status: snapshot)
      │  Import
      ▼
Database (updated working copy)
      │
      └── Diff available: any two drafts, any scope
```

### 6.3 Draft numbering

- Draft 1 is always the initial AI generation; it never changes
- `based-on="0"` indicates no parent (initial generation)
- Branching is supported: two drafts may share the same `based-on` value
- Exactly one draft per book may carry `status="in-progress"` at any time

### 6.4 Paragraph provenance

Every content element carries:

| Attribute        | Meaning                                      |
|-----------------|----------------------------------------------|
| `draft-created`  | Draft number when this element was first introduced |
| `draft-modified` | Draft number when content was last changed   |
| `modified-by`    | `human` or `ai`                              |
| `modified-date`  | ISO 8601 dateTime of last modification       |

---

## 7. File Reference: book.xml

Validated by `bookml-book.xsd`.

### Required elements

| Element                          | Notes                                  |
|----------------------------------|----------------------------------------|
| `bookinfo/title`                 |                                        |
| `bookinfo/author/surname`        | One or more authors                    |
| `bookinfo/genre`                 | See GenreType enumeration              |
| `bookinfo/language`              | ISO 639-1 code                         |
| `bookinfo/versioning/draft`      | At least one draft record required     |
| `bookinfo/created`               | ISO date                               |
| `bookinfo/modified`              | ISO date; updated on rebalancing       |
| `contents/bodymatter`            |                                        |
| `contents/bodymatter/component`  | At least one chapter                   |

### Cover types

| Type        | Recommended size   | Usage                              |
|-------------|-------------------|------------------------------------|
| `full`      | 1600×2400px       | Print and full-screen display      |
| `thumbnail` | 160×240px         | Catalogue and editor UI grid views |
| `spine`     | As required       | Print spine                        |
| `back`      | As required       | Print back cover                   |
| `ebook`     | Per platform spec | Distribution platforms             |

---

## 8. File Reference: chapter XML

Validated by `bookml-chapter.xsd`.

### Structure

```xml
<chapter id="ch01" book-id="..." draft="1" ...>
  <chapterinfo>
    <chapternumber>1</chapternumber>
    <title>...</title>
    <subtitle>...</subtitle>     <!-- optional -->
    <epigraph>...</epigraph>     <!-- optional -->
  </chapterinfo>
  <section seq="1000">           <!-- one or more; no title needed for fiction -->
    <para pid="CH01-P0010" seq="1000" type="normal"
          draft-created="1" draft-modified="1"
          modified-by="ai">
      Paragraph text here.
    </para>
    <break seq="2000" type="scene"/>
    <para pid="CH01-P0020" seq="3000" ...>...</para>
  </section>
</chapter>
```

### Fiction vs non-fiction usage

| Feature           | Fiction          | Non-fiction        |
|-------------------|------------------|--------------------|
| Sections          | Usually one implicit | Multiple named sections |
| Section titles    | Omit             | Include            |
| Headings          | Rarely used      | Frequently used    |
| Figures           | Optional         | Common             |
| Tables            | Rare             | Common             |
| Footnotes         | Rarely           | Common             |
| para type=dialogue | Common          | Rare               |

---

## 9. File Reference: meta XML

Validated by `bookml-meta.xsd`.

- The meta file is **sparse** — only paragraphs with noteworthy metadata need records
- Absent records are treated as paragraphs with all fields at default values
- One `para-meta` block per `pid`; duplicates fail schema validation

### Key metadata fields

| Field        | Fiction use               | Non-fiction use        |
|--------------|--------------------------|------------------------|
| `status`     | Editorial workflow        | Editorial workflow      |
| `pov`        | POV consistency tracking  | Not typically used      |
| `location`   | Scene geography           | Not typically used      |
| `timepoint`  | Narrative timeline        | Not typically used      |
| `characters` | Scene population          | Not typically used      |
| `tags`       | Thematic tagging          | Subject tagging         |
| `tone`       | Pacing analysis           | Not typically used      |
| `word-count` | Statistics                | Statistics              |
| `topic`      | Not typically used        | Subject classification  |
| `sources`    | Not typically used        | Research provenance     |

---

## 10. File Reference: notes XML

Validated by `bookml-notes.xsd`.

- Multiple notes per paragraph are supported and expected to accumulate over drafts
- Notes are **never deleted** — resolved notes carry `resolved="true"` with a `resolution` record
- The `id` attribute on each note uses the convention `{pid}-N{sequence}`, e.g. `CH01-P001-N001`

### Note types

| Type         | Who creates it  | Purpose                                      |
|-------------|-----------------|----------------------------------------------|
| `craft`      | Human or AI     | Editorial observation about the writing      |
| `research`   | Human or AI     | Factual reference or source citation         |
| `continuity` | Human or AI     | Cross-reference flag for consistency         |
| `query`      | Human or AI     | Open question; use `priority` and resolve    |
| `diff-hint`  | AI only         | Explanation of what changed between drafts   |
| `footnote`   | Human           | Published footnote or endnote (non-fiction)  |
| `general`    | Either          | Catch-all                                    |

### The diff-hint note

When an AI generates a new draft, it **should** create a `diff-hint` note for every paragraph it modified. This is invaluable for understanding the editorial history months later:

```xml
<note id="CH01-P002-N001" type="diff-hint"
      author-type="ai" author="Claude Opus 4" draft="3">
  <body>
    Reordered sentence structure to front-load the subject's
    agency. Original construction was passive. Also replaced
    'grey estuary' with 'dark water' to reduce colour-word
    repetition with CH01-P001.
  </body>
</note>
```

---

## 11. AI Generator Instructions

When an AI system generates BookML files, it **must** follow these rules exactly:

1. Generate all five required attributes on every `<para>`: `pid`, `seq`, `type`, `draft-created`, `draft-modified`, `modified-by`
2. Assign `seq` values in multiples of 1000, starting at 1000
3. Assign `pid` values using the format `{CHAPTERID}-P{SEQUENCE}` with 4-digit zero-padded sequence starting at 0010, incrementing by 10
4. Set `draft-created` and `draft-modified` to the current draft number
5. Set `modified-by` to `"ai"`
6. Wrap all chapter content in at least one `<section seq="1000">` element, even for fiction with no structural sections
7. Generate all three files (chapter, meta, notes) for every component
8. In the notes file, create a `diff-hint` note for every paragraph that differs from the previous draft
9. Validate that every `pid` referenced in meta and notes files exists in the chapter file

---

## 12. Application (Importer) Rules

1. **Validate first** — run XSD validation against all three files before attempting any database operation. Reject the entire import if any file fails validation.
2. **Never overwrite** existing `paragraph_versions` rows — insert new rows with the new draft number only
3. **Update book.xml `<modified>`** after any rebalancing operation
4. **Check pid integrity** — every `pid` in meta and notes files must exist in the chapter file; reject the import if orphan pids are found
5. **Set snapshot-date** on chapter files when transitioning a draft from `in-progress` to `snapshot`
6. **Exactly one `in-progress` draft** — enforce this constraint on import

---

## 13. Diff Query Pattern

To diff chapter `ch01` between draft 1 and draft 3:

```sql
SELECT 
  COALESCE(a.pid, b.pid)        AS pid,
  a.content                     AS draft_1_content,
  b.content                     AS draft_3_content,
  a.seq                         AS draft_1_seq,
  b.seq                         AS draft_3_seq,
  CASE 
    WHEN b.pid IS NULL          THEN 'deleted'
    WHEN a.pid IS NULL          THEN 'inserted'
    WHEN a.seq != b.seq 
     AND a.content = b.content  THEN 'moved'
    WHEN a.content != b.content THEN 'modified'
    ELSE                             'unchanged'
  END                           AS change_type
FROM paragraph_versions a
FULL OUTER JOIN paragraph_versions b 
  ON  a.pid        = b.pid 
  AND b.draft_number = 3
WHERE a.draft_number = 1
  AND a.chapter_id   = 'ch01'
ORDER BY COALESCE(b.seq, a.seq);
```

---

## 14. Schema File Summary

| File                | Validates          | Imports         |
|---------------------|--------------------|-----------------|
| `bookml-common.xsd` | (not used directly) | —              |
| `bookml-book.xsd`   | `book.xml`         | bookml-common   |
| `bookml-chapter.xsd`| `*-chapter.xml`    | bookml-common   |
| `bookml-meta.xsd`   | `*-meta.xml`       | bookml-common   |
| `bookml-notes.xsd`  | `*-notes.xml`      | bookml-common   |

---

## 15. Version History

| Version | Date       | Author        | Notes                    |
|---------|------------|---------------|--------------------------|
| 1.0     | 2026-02-24 | (Project)     | Initial specification    |

---

*BookML is a project-specific vocabulary. The namespace `https://bookml.org/ns/1.0` is reserved for this specification.*
