# Chilli Racers — Revision Advisory for Claude
## Context and Method Briefing — 5 May 2026

This document gives Claude the background needed to help design an openclaw work-order brief for a major structural and prose rewrite of the *Chilli Racers* manuscript. It covers: the manuscript's current state, the book editor's XML format, the openclaw execution method, and the model configuration planned for this job.

---

## 1. The Manuscript

**Working title:** Chilli Racers (no final title yet)
**Genre:** Sci-fi thriller — dystopian desert future, banned combustion engines, death commodified via mandatory corporate revivals
**Comparable works:** Mad Max meets The Running Man; cyberpunk wasteland
**Protagonist:** Flynn — naive newcomer who becomes a reluctant hero
**Antagonist:** Cage — charismatic but destructive mentor figure
**Core setting:** High-stakes illegal desert race, "La Carrera del Chile Rojo", sponsored by a chilli baron
**Theme:** Freedom's dark side — how rebellion becomes cultish and self-destructive

**Current state:**
- Draft 1: ~60,000 words across 24 chapters, AI-generated via openclaw
- Reviewed by Grok (Feb 2026) with detailed structural and prose feedback
- Export file: `chilli-racers_export_20260505_174425.bookml.zip`

**Grok review — key weaknesses identified:**

- *Pacing:* Front half over-long with repetitive Compound scenes; race only starts at Ch. 20, compressing the climax into ~15% of the book
- *Character depth:* Flynn's internal conflict understated; Birdie borders on trope; Wrench and Don underused; Cage lacks backstory payoff
- *Emotional stakes:* Mandatory revival system undercuts the weight of deaths; revival's psychological cost not explored
- *Prose:* Over-reliance on simile (200+ instances of "like a [thing]")
- *Structure suggestion:* Merge Ch. 3–6 into 3–4 tighter chapters; move race setup earlier

**Planned draft 2 scope (from revision notes):**

| Task | Change |
|------|--------|
| 1–2 | Compress Ch. 1–6 by ~1,000 words; add early mini-race/near-capture for pacing |
| 3 | Plant La Carrera del Chile Rojo as myth in Ch. 9–10 before plot reality |
| 4 | Flynn city-life flashback in Ch. 2 — sensory-triggered, fragmented, stifling |
| 5–6 | Birdie subplot — old-life detail planted early; mid-book contact attempt |
| 7 | Don's pre-ban race story — hands-on and specific, no moralising |
| 8 | Wrench foreshadowing (behavioural wrongness) + post-breakdown crew silence |
| 9 | Humanise Cage subtly — unguarded moment, no Flynn present |
| 10 | Revival's psychological cost — subtle wrongness post-revival, not physical |
| 11 | Amplify implant satire — darkly absurd ads at worst possible moments |
| 12–13 | Expand climax (Ch. 20–22) with rival crews, radio chatter, interpersonal drama |

Target word count after rewrite: ~70,000–75,000 words.

This is a **drastic structural rewrite**, not a polish pass. Chapters may be merged, split, reordered, or replaced. Paragraph counts will change significantly. This is different in kind from a translation job (where paragraph count must stay fixed) — here, structural freedom is the whole point.

---

## 2. The Book Editor XML Format (BookML)

All manuscripts in this system are stored and exchanged as BookML XML ZIPs. Claude must produce output that conforms to this format exactly.

### 2.1 ZIP structure

```
book.xml                          ← manifest + versioning
epilogue/
  epilogue-chapter.xml
  epilogue-meta.xml
  epilogue-notes.xml
ch01/
  ch01-chapter.xml
  ch01-meta.xml
  ch01-notes.xml
ch02/ ...
```

### 2.2 book.xml — manifest and versioning

```xml
<?xml version="1.0" encoding="utf-8"?>
<book bookml-version="1.0" id="chilli-racers" xmlns="https://bookml.org/ns/1.0">
  <bookinfo>
    <title>Chilli Racers</title>
    <genre>fiction</genre>
    <language>en</language>
    <versioning>
      <draft number="1" status="snapshot" created="..." based-on="0"
             author-type="ai" author="openclaw/openai-codex"
             label="Initial AI generation" />
      <draft number="2" status="in-progress" created="[ISO 8601 datetime]"
             based-on="1" author-type="ai" author="openclaw/gemini"
             label="Structural rewrite — Grok review recommendations applied" />
    </versioning>
  </bookinfo>
  <contents>
    <bodymatter>
      <component id="CH01" type="chapter" seq="1000"
                 chapter-file="ch01/ch01-chapter.xml"
                 meta-file="ch01/ch01-meta.xml"
                 notes-file="ch01/ch01-notes.xml"
                 title="Chapter 1" draft="2" />
      <!-- one <component> per chapter -->
    </bodymatter>
  </contents>
</book>
```

**Versioning rules:**
- The source (draft 1) export will have all `<draft>` entries with `status="snapshot"`
- The output must add a new `<draft number="2" status="in-progress" ...>` entry
- All `<component>` elements must have their `draft=` attribute updated to `"2"`

### 2.3 Chapter XML file

```xml
<?xml version="1.0" encoding="utf-8"?>
<chapter bookml-version="1.0" id="ch01" book-id="chilli-racers" draft="2">
  <chapterinfo>
    <chapternumber>1</chapternumber>
    <title>Chapter Title Here</title>
  </chapterinfo>
  <section id="ch01-s01" seq="1000">
    <para pid="CH01-P001" seq="1000" type="normal"
          draft-created="1" draft-modified="2"
          modified-by="ai" modified-date="[ISO 8601]">
      Paragraph text goes here.
    </para>
    <para pid="CH01-P002" seq="2000" type="dialogue" speaker="Flynn">
      "Dialogue text here."
    </para>
    <break type="scene"/>
  </section>
</chapter>
```

### 2.4 Paragraph ID (`pid`) rules

- Format: `{CHAPTERID}-P{TOKEN}` e.g. `CH01-P001` or `CH01-P-7K2Q9ZPL`
- Characters: `ABCDEFGHJKLMNPQRSTUVWXYZ23456789` (no O, I, 0, 1)
- **Immutable for unchanged paragraphs** — a para from draft 1 keeps its pid in draft 2
- New paragraphs added in draft 2 get new pids, unique within the chapter
- Deleted paragraphs simply disappear — their pids are not reused

### 2.5 Key attributes on `<para>`

| Attribute | Rule for rewrite |
|-----------|-----------------|
| `pid` | Keep unchanged for surviving paras; assign new unique pid for new paras |
| `seq` | Renumber sequentially in steps of 1000 to leave room for future insertions |
| `type` | Set appropriately: `normal`, `dialogue`, `heading`, `epigraph` etc. |
| `draft-created` | Keep original value for surviving paras; set to `2` for new paras |
| `draft-modified` | Update to `2` for any para whose text changed; keep `1` for untouched paras |
| `modified-by` | Set to `ai` for AI-modified content |
| `modified-date` | ISO 8601 datetime of this revision pass |

### 2.6 Meta and notes files

- `ch{N}-meta.xml` — paragraph-level metadata (pov, location, timepoint, topic, tags, word-count). Update to reflect structural changes but do not over-engineer — a stub with the new pids is sufficient for import.
- `ch{N}-notes.xml` — annotations. Can be left as empty stubs for new draft.

### 2.7 Validation

Before packaging, every chapter file must pass:
```bash
xmllint --noout ch*/ch*-chapter.xml
```

**Paragraph count check — important known issue:**
Empty paragraphs self-close as `<para ... />` with no closing tag, so `</para>` counts will be wrong. Always count using the opening tag with a trailing space:
```bash
grep -c '<para ' ch01/ch01-chapter.xml
```

---

## 3. OpenClaw Method

OpenClaw is an AI agent orchestration system running on the local network machine **Nobby** (192.168.69.66). It accepts a written task brief (a Markdown file) and executes the tasks in it sequentially, using timed intervals between tasks to avoid hitting API rate limits.

### 3.1 Model split for this job

| Role | Model |
|------|-------|
| Fiction prose generation and rewriting | **Google Gemini** (to be configured) |
| Tooling: file I/O, XML manipulation, validation, packaging, scheduling | **Local Qwen 3.6** (already configured on Nobby) |

Gemini handles the creative work; Qwen handles everything mechanical. The brief must be written so that task instructions are clear about which model should perform each step. In practice openclaw's brief format routes creative prompts to the designated fiction model and tool calls to the local model automatically — but the brief should make the division of labour explicit.

### 3.2 Work-order structure

A brief is a Markdown file with:
1. **Objective** — what the output must be
2. **Rate limit rules** — the minimum interval between task completions and the model to use
3. **Content rules** — what to change, what to leave alone, XML integrity rules
4. **Task list** — one numbered task per chapter (or logical unit of work), each with:
   - Source file path
   - What to do (specific revision instructions for that chapter)
   - Validation steps
   - Output file path
   - Telegram heartbeat message
5. **Final task** — versioning update, draft attribute sweep, validation, ZIP packaging

### 3.3 Rate limiting

- Minimum **5 hours** between task completions for Gemini API (cloud model)
- Local Qwen 3.6 calls (tooling) are not rate-limited and run between Gemini calls without delay
- If a task fails: wait 6 hours, retry once, log the failure
- Never run chapter tasks in parallel

### 3.4 Heartbeats

Openclaw sends a Telegram notification on each task completion. The brief must include the exact heartbeat message string for each task. Keep them informative: chapter number, what was done, and any count (word count, para count).

### 3.5 Structural rewrite vs translation — key differences

| Concern | Translation job | Structural rewrite |
|---------|-----------------|--------------------|
| Paragraph count | Must match source exactly | Will change — chapters restructured |
| Chapter count | Fixed | May change if chapters merged/split |
| Pid on existing paras | Unchanged | Unchanged for surviving paras |
| Pid on new paras | N/A | New unique pids assigned |
| `draft-modified` | Set to new draft for all | Only set for changed paras |
| Meta files | Copy unchanged | Update to reflect structural changes |
| Prose language | Translate only | Full rewrite in same language |

### 3.6 Directory convention

```
~/chilli-rewrite/
  source/          ← unzipped source export (read-only)
  output/          ← working output tree (written chapter by chapter)
  logs/            ← one log file per task
```

### 3.7 Final task checklist

The final task (after all chapters are done) must:
1. Add the new draft `<versioning>` entry to `book.xml`
2. Update `draft="1"` to `draft="2"` on all `<component>` elements in `book.xml`
3. Update `draft="1"` to `draft="2"` on every `<chapter>` root element (sed sweep)
4. Run `xmllint` over all output chapter files
5. Verify paragraph counts are plausible (not zero, not wildly different from source)
6. Package: `zip -r chilli-racers_draft2.bookml.zip .` from inside the output directory
7. Send final Telegram heartbeat with chapter count, total para count, output filename

---

## 4. What Claude on the Web Is Being Asked to Do

Using this document as background, Claude should help draft the openclaw task brief for the Chilli Racers draft 2 structural rewrite. This means:

1. **Confirming the draft number** from `book.xml` in the export (the user will supply this)
2. **Confirming the chapter list** from the `<component>` entries in `book.xml`
3. **Designing the task list** — one task per chapter (or merged-chapter block where Grok recommended merging), with specific revision instructions derived from the Grok review and the draft 2 rewrite notes summarised in Section 1 above
4. **Writing the chapter-level prose briefs** — each task's instruction block telling Gemini what to rewrite and how, chapter by chapter
5. **Writing the final task** — versioning, sweep, validate, package
6. **Flagging any chapters** where the revision notes say to merge, split, or restructure significantly, so the chapter IDs and component list in `book.xml` can be updated accordingly

The output of this process is a single Markdown file that can be placed on Nobby and handed directly to openclaw.

---

## 5. Known Issues and Gotchas

- **Para count check:** Use `grep -c '<para '` (space after `para`), never `grep -c '</para>'`. Self-closing empty paras have no closing tag and will be missed.
- **book.xml `<draft number=` vs `draft=` attribute:** The `sed` sweep `s/draft="1"/draft="2"/g` is safe because `<draft number="1"` does not contain the substring `draft="1"` — the versioning entries use `number=` not `draft=`. Always verify after the sweep that versioning entries are intact.
- **Chapter IDs in book.xml after merges:** If chapters are merged (e.g. Ch. 3–6 become Ch. 3–4), the `<component>` entries and chapter directory names must be updated to match. The importer uses the `id=` attribute to identify chapters, so retired chapter IDs must be removed from book.xml entirely.
- **Seq numbering:** Use steps of 1000 for `seq=` on `<para>` elements to leave room for future human edits without renumbering.
- **XML encoding:** All output must be UTF-8. Smart quotes, em-dashes, and accented characters are fine as literal UTF-8 characters — do not escape them as XML entities.
- **Gemini context window:** For very long chapters, Gemini may need the chapter split into sections for processing. Qwen should reassemble the output before writing the final file. Account for this in the brief if any chapter exceeds ~4,000 words.
