# Phase 3 (Revised): Book-Level Analysis & Lexical Revision Brief

## Specification for BookML Analytics Add-on

**Version:** 2.0 (replaces original Phase 3 dispatcher spec)
**Date:** 2026-04-19
**Status:** Design-ready

---

## 1. Purpose

Phase 3 extends the analysis engine to operate at book level rather than per-chapter, and produces a concise lexical analysis summary document suitable for inclusion in an Openclaw revision brief. The summary identifies book-wide repetition patterns while distinguishing them from chapter-local concentrations that may be contextually appropriate.

### 1.1 What This Phase Delivers

- A book-level analysis mode in the ContentAnalysisEngine that processes all chapters together and produces cross-chapter comparison metrics.
- A `analyse-book` subcommand in the CLI that takes a BookML book manifest and analyses the full manuscript.
- A lexical summary document generator that distils the statistical findings into a prose brief paragraph suitable for appending to an existing Openclaw revision script.
- Names file integration: the summary excludes character names from flagged terms and includes the exclusion list in the output.

### 1.2 What This Phase Does Not Deliver

- No per-entry work order processing. The per-entry pipeline from the original Phase 3 is retired.
- No automated Openclaw dispatcher skill. The summary is consumed by existing revision script workflows.
- No per-entry validation or state machine. Validation of LLM rewrites is handled by the existing post-revision analysis comparison (run analysis on draft N, run analysis on draft N+1, compare).

### 1.3 What Remains Useful from Previous Work

- The content-analyser CLI (cross-platform, deployed on Binky) — gains the new `analyse-book` subcommand.
- The `WorkOrderValidator` and its checks — retained for future optional use but not part of the primary workflow.
- The Phase 2 work order UI — repurposed as an interactive dashboard for manual editing guidance (the flagged items list, severity ranking, and PID navigation are useful for human editors even without automated dispatch).
- The names file format and exclusion logic — carried forward unchanged.

---

## 2. Book-Level Analysis

### 2.1 Cross-Chapter Metrics

The engine currently analyses one chapter at a time. Book-level analysis runs all chapters and computes:

**Book-wide word frequency:** Total count of each non-stop word across all chapters. Z-score computed against the book-wide distribution. A word flagged at book level is a genuine AI tic — it's overused everywhere, not concentrated in one chapter.

**Chapter-vs-book TF-IDF:** For each flagged word, classify its distribution:
- **Book-wide tic:** Appears at elevated frequency across most or all chapters. This is what the revision brief should address.
- **Chapter-concentrated:** Appears heavily in one or two chapters but rarely elsewhere. This is likely contextually appropriate (reactor terminology in the reactor chapter) and should NOT be flagged in the revision brief. It may be noted in the per-chapter dashboard for the human editor's awareness.

**Book-wide n-gram frequency:** Same cross-chapter analysis for repeated phrases. "Shook her head" appearing 47 times across 20 chapters is a book-wide tic. "Emergency shutdown procedure" appearing 8 times in chapter 12 (the reactor chapter) is setting-appropriate.

**Book-wide TTR and hapax ratio:** Computed across the full manuscript, giving a single headline number for vocabulary health.

**Book-wide sentence length variance:** Averaged across all chapters.

### 2.2 Per-Chapter Metrics (Retained)

The existing per-chapter analysis is unchanged and still available. The dashboard displays per-chapter metrics for human review. The book-level analysis adds a layer on top, not a replacement.

### 2.3 Analysis Report Format

The book-level analysis produces an XML report following the existing schema conventions but with a new root element:

```xml
<book-analysis-report xmlns="https://bookml.org/ns/analysis/1.0"
    schema-version="2.0"
    generated-at="2026-04-19T10:00:00Z"
    book-id="CTY"
    total-words="60000"
    total-chapters="20">

  <names-excluded>
    <name value="Marnette"/>
    <name value="Frost"/>
    <!-- ... -->
  </names-excluded>

  <book-metrics
      unique-words="4200"
      type-token-ratio="0.070"
      moving-average-ttr="0.52"
      hapax-count="2100"
      hapax-ratio="0.50"
      mean-sentence-length="14.2"
      sentence-length-std-dev="6.8"/>

  <book-wide-outliers>
    <word value="door" count="312" z-score="8.4" chapters-present="18" distribution="book-wide-tic"/>
    <word value="years" count="198" z-score="5.1" chapters-present="20" distribution="book-wide-tic"/>
    <word value="reactor" count="34" z-score="3.2" chapters-present="2" distribution="chapter-concentrated"/>
  </book-wide-outliers>

  <book-wide-ngrams>
    <ngram phrase="shook her head" count="47" chapters-present="15" distribution="book-wide-tic"/>
    <ngram phrase="emergency shutdown" count="8" chapters-present="1" distribution="chapter-concentrated"/>
  </book-wide-ngrams>

  <chapter-summaries>
    <chapter id="CH01" words="4744" ttr="0.283" hapax-ratio="0.63" outlier-count="35" ngram-count="251" echo-count="982"/>
    <chapter id="CH02" words="5200" ttr="0.301" hapax-ratio="0.58" outlier-count="28" ngram-count="189" echo-count="743"/>
    <!-- ... -->
  </chapter-summaries>
</book-analysis-report>
```

The `distribution` attribute on each flagged item is the key innovation — it separates genuine tics from contextual concentrations.

---

## 3. Lexical Summary Document

### 3.1 Purpose

A prose paragraph or short document that distils the book-level analysis into human-readable text suitable for inclusion in an Openclaw revision brief. The operator copies this into their revision script alongside whatever other instructions they're giving.

### 3.2 Format

Plain text. Generated by the CLI alongside the XML report.

### 3.3 Content

The summary includes:

1. **Headline metrics:** Total word count, TTR, hapax ratio, and a plain-language assessment ("Vocabulary diversity is below typical fiction range — consider varying word choices.")

2. **Book-wide outlier words:** Only items classified as `book-wide-tic`, listed with counts and expected ranges. Character names excluded. Example: "The word 'door' appears 312 times across 18 chapters (expected ~80 for a manuscript of this length)."

3. **Book-wide repetitive phrases:** Only `book-wide-tic` n-grams. Example: "The phrase 'shook her head' appears 47 times across 15 chapters."

4. **Character name exclusion list:** "The following character names must not be altered: Marnette, Frost, Timon, Elara, Elom, Maris."

5. **General instruction:** "Reduce the flagged terms, vary the vocabulary, and improve lexical diversity while preserving narrative tone, character voice, and plot continuity. Do not flatten chapter-specific terminology that is appropriate to the setting."

### 3.4 Example Output

```
LEXICAL ANALYSIS SUMMARY — Carrier to Yesterday, Draft 2

Manuscript: 60,412 words across 20 chapters.
Vocabulary diversity (TTR): 0.070 — below the typical fiction range of 0.10–0.15.
Unique word ratio (hapax): 0.50 — adequate but could be richer.

BOOK-WIDE OVERUSED WORDS (appear excessively across most chapters):
- "door" — 312 occurrences across 18 chapters (expected ~80)
- "years" — 198 occurrences across 20 chapters (expected ~60)
- "room" — 176 occurrences across 19 chapters (expected ~50)
- "eyes" — 145 occurrences across 20 chapters (expected ~45)
- "something" — 132 occurrences across 18 chapters (expected ~40)

BOOK-WIDE OVERUSED PHRASES:
- "shook her head" — 47 occurrences across 15 chapters
- "thin air" — 31 occurrences across 12 chapters
- "red dust" — 28 occurrences across 14 chapters

CHARACTER NAMES (do not alter): Marnette, Frost, Timon, Elara, Elom, Maris.

INSTRUCTION: Reduce the flagged terms, vary the vocabulary, and improve
lexical diversity while preserving narrative tone, character voice, and
plot continuity. Chapter-specific terminology appropriate to the setting
(e.g., technical terms in scenes set in specific locations) should be
preserved even if locally concentrated.
```

### 3.5 Integration with Revision Scripts

The operator uses this summary in one of two ways:

**For early drafts (Openclaw-assisted revision):** Paste the summary into the Openclaw revision script alongside other instructions. Openclaw processes the full manuscript chapter by chapter with the combined brief. This is the existing workflow — the summary is just a better-informed supplement to the revision instructions.

**For mature drafts (human editing):** Read the summary in the dashboard, use the per-chapter analysis to locate specific issues, and fix them manually in the editor. No Openclaw involvement.

---

## 4. CLI Extensions

### 4.1 analyse-book Subcommand

```bash
content-analyser analyse-book \
  --manifest path/to/book.xml \
  --names path/to/names.xml \
  --output path/to/book-analysis.xml \
  --summary path/to/lexical-summary.txt
```

- `--manifest` — path to the BookML `book.xml` file. The CLI reads the chapter list from this file and analyses each chapter in sequence.
- `--names` — required. Character name exclusion list.
- `--output` — path for the XML analysis report.
- `--summary` — path for the plain-text lexical summary. If omitted, the summary is written to stdout.

The command processes all chapters, computes book-level metrics, classifies each flagged item as `book-wide-tic` or `chapter-concentrated`, and produces both output files.

### 4.2 Existing Subcommands (Unchanged)

`analyse` (per-chapter) and `workorder` remain available for the per-chapter dashboard use case. The `validate` subcommand remains available for optional post-revision spot-checking.

---

## 5. Dashboard Integration

The Phase 2 UI gains a book-level view alongside the existing per-chapter view:

**Book Analysis Dashboard:** Displays book-wide metrics, the outlier word list with distribution classification, and the n-gram list. Items classified as `chapter-concentrated` are visually distinct (greyed, flagged with a location indicator) so the human editor knows they're likely intentional.

**Summary Export:** A button to export the lexical summary as a text file for inclusion in revision scripts.

**Per-Chapter View (existing):** Unchanged. Still shows per-chapter metrics, flagged items, and PID navigation for manual editing.

---

## 6. Implementation Scope

### Session 1 — Claude Code on Vimes (C# changes)

1. Add `BookAnalyser` class to the engine — iterates chapters from a book manifest, runs per-chapter analysis, then computes book-level cross-chapter metrics and distribution classification.
2. Add `LexicalSummaryGenerator` class — takes a book-level analysis report and produces the plain-text summary.
3. Add `analyse-book` subcommand to the CLI.
4. Rebuild both targets, republish Linux binary.

### Session 2 — Dashboard UI (Seonyx editor)

1. Add book-level analysis view to the Draft Analysis dashboard.
2. Add summary export button.

---

## 7. Acceptance Criteria

1. `content-analyser analyse-book` processes a full BookML manuscript and produces the XML report with distribution classifications.
2. Words concentrated in one or two chapters are classified as `chapter-concentrated`, not `book-wide-tic`.
3. Words spread across most chapters are classified as `book-wide-tic`.
4. Character names are excluded from all flagged lists.
5. The lexical summary text accurately reflects the book-level findings and is suitable for pasting into a revision script.
6. The summary does not include chapter-concentrated items — only book-wide tics.
7. The per-chapter analysis continues to work independently for dashboard use.
