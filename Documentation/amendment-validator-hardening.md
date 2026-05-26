# Amendment: Validator Hardening — Text Preservation and Character Name Exclusion

**Applies to:** Phase 1 (ContentAnalysisEngine), Phase 3 (WorkOrderValidator), BookML Export
**Date:** 2026-04-16

---

## 1. Character Name Exclusion

### 1.1 Problem

The analysis engine flags character names (Marnette, Frost, Elara, Elom) as outlier words because they appear frequently. Work orders are generated telling the LLM to replace them. This is wrong — character names should never be candidates for remediation.

### 1.2 Solution: Names File in BookML Package

The Seonyx editor already has a database table for character names. On BookML export, the exporter writes a names file into the package:

**File:** `names.xml` in the package root, alongside `book.xml`.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<names xmlns="https://bookml.org/ns/1.0">
  <name value="Marnette" />
  <name value="Frost" />
  <name value="Timon" />
  <name value="Elara" />
  <name value="Elom" />
  <name value="Maris" />
</names>
```

The names are drawn from the character name table in the database at export time. The file travels with the package.

### 1.3 Analysis Engine Change

`ChapterAnalyser` gains an optional `namesFilePath` parameter. If provided, all names in the file are added to the stop-word exclusion set before analysis runs. They are excluded from:
- Word frequency outlier detection (§3.1) — names are never flagged.
- N-gram frequency detection (§3.2) — n-grams consisting entirely of excluded names are filtered. N-grams containing a name alongside other words are kept (e.g., "Master Frost" is still flaggable as a repetitive bigram, but "Frost" alone is not flagged as an outlier word).

Names are NOT excluded from proximity echo detection (§3.3) — a character name repeated within 50 words is still jarring to read, even if the name itself is correct. The echo entry's instruction should say "rephrase to avoid repeating the name" rather than "replace the name."

### 1.4 CLI Change

The content-analyser CLI gains an optional `--names` argument:

```bash
content-analyser analyse --chapter path/to/chapter.xml --names path/to/names.xml
content-analyser workorder --chapter path/to/chapter.xml --names path/to/names.xml --output workorders.xml --draft 3
```

If `--names` is not provided, the engine runs without name exclusions (backward compatible).

### 1.5 Validator Change

The `validate` subcommand also gains `--names`:

```bash
content-analyser validate --original ... --rewritten ... --entry-id ... --manifest ... --names path/to/names.xml
```

When provided, the validator uses the names list in ban compliance checking to ensure the LLM hasn't replaced a character name. See §2.3 below.

### 1.6 Export Change (Seonyx Editor)

The BookML exporter is updated to include `names.xml` in the export package. The file is generated from the character name table. If the table is empty, the file is still included but contains no `<name>` entries — the downstream tools treat an empty list as "no exclusions."

**The analyser must refuse to run if `--names` is specified but the file is missing or empty.** This enforces the requirement that you populate the character name table before running analysis. A clear error message: "Names file is empty — populate character names in the editor before running analysis."

Wait — you said you want the validator not to run without a valid name list, not the analyser. Let me reconsider.

**Correction:** The hard gate should be on work order generation and validation, not on analysis. The analysis can run without names to show you what it finds (including names as outliers — useful for spotting names you forgot to add). But generating work orders and validating rewrites without a names list risks producing or accepting bad instructions. So:

- `content-analyser analyse` — names file optional. If absent, names will appear as outlier words. This is informational.
- `content-analyser workorder` — names file required. Refuses to generate if not provided or empty. Error: "Cannot generate work orders without a character names file. Populate the character name table in the editor and re-export."
- `content-analyser validate` — names file required. Refuses to validate if not provided or empty. Same error pattern.

---

## 2. Unchanged Text Preservation Check

### 2.1 Problem

The LLM is instructed to rewrite specific paragraphs to remove a banned term. But it sometimes modifies text that doesn't contain the banned term — changing "her room" to "her girl" when the entry was about the word "marnette." The existing five validation checks don't catch this because they only verify constraints, not content preservation.

### 2.2 Solution: Check 6 — Text Preservation

A sixth validation check in `WorkOrderValidator`:

**For each target PID in the work order entry:**
1. Split the original paragraph into sentences (period/question mark/exclamation mark followed by whitespace or end of text).
2. Identify sentences that do NOT contain the entry's `<subject>` term (case-insensitive).
3. For each such sentence, find the corresponding sentence in the rewritten paragraph by position (same sentence index).
4. Compare the two sentences. If they differ by more than minor whitespace/punctuation normalisation, the check fails.

**Failure message:** "Text preservation violation in PID {pid}: sentence {N} was modified but does not contain the target term '{subject}'. Original: '{first 50 chars}...' Rewritten: '{first 50 chars}...'"

### 2.3 Name Preservation Check (requires names file)

When the names file is provided, add a supplementary check:

**For each target PID in the rewritten chapter:**
1. For every name in the names file, verify it appears the same number of times in the rewritten paragraph as in the original paragraph.
2. If a name's count decreased, the LLM replaced a character name — fail.

**Failure message:** "Name preservation violation in PID {pid}: character name '{name}' appears {original_count} times in original but {rewritten_count} times in rewrite."

This catches "Marnette" → "girl" and "Frost" → "elder" substitutions even when the sentence structure changed enough that the sentence-level comparison in §2.2 wouldn't align cleanly.

---

## 3. Implementation Scope

### Session 1 — Claude Code on Vimes (C# changes)

1. Add `names.xml` parsing to the engine: a `NamesReader` static class similar to `BookmlReader`.
2. Add `namesFilePath` parameter to `ChapterAnalyser.Analyse()` and wire it into the stop-word exclusion.
3. Add `--names` argument to the CLI for all three subcommands with the gating logic described in §1.6.
4. Add Check 6 (text preservation) and Check 7 (name preservation) to `WorkOrderValidator`.
5. Rebuild both targets, republish Linux binary.

### Session 2 — Seonyx Editor (export change)

1. Update the BookML exporter to include `names.xml` generated from the character name table.

### Deployment

After Session 1: republish Linux binary to Binky. The Openclaw skill needs no changes — the scripts call `content-analyser validate` which now performs the additional checks automatically when `--names` is provided. Update the skill's SKILL.md to pass `--names {inputFolder}/names.xml` on all content-analyser invocations.

---

## 4. Acceptance Criteria

1. Analysis with `--names` does not flag character names as outlier words.
2. Analysis without `--names` flags character names normally (backward compatible).
3. Work order generation refuses to run without `--names` or with an empty names file.
4. Validation refuses to run without `--names` or with an empty names file.
5. A rewrite that modifies a sentence not containing the target term fails Check 6.
6. A rewrite that removes or replaces a character name fails Check 7.
7. A clean rewrite that only modifies sentences containing the target term and preserves all character names passes all seven checks.
