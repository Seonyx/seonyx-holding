# Phase 4 (Revised): Cross-Draft Tracking & Export Integration

## Specification for BookML Analytics Add-on

**Version:** 2.0 (replaces original Phase 4 batch controller spec)
**Date:** 2026-04-19
**Status:** Design-ready

---

## 1. Purpose

Phase 4 closes the loop. It adds cross-draft comparison at book level so you can see measurable improvement across the manuscript's lifecycle, and integrates the analysis outputs into the BookML export pipeline so everything travels with the package.

### 1.1 What This Phase Delivers

- Book-level cross-draft comparison: run analysis on draft N and draft N+1, produce a delta report showing which metrics improved, which regressed, and by how much.
- Burn-down tracking at book level: the dashboard plots outlier counts, TTR, and hapax ratio across all available drafts, giving the "pattern of improvement" visualisation from the original Phase 1 spec.
- Export integration: the BookML exporter includes `names.xml` (from the character table) and optionally the latest lexical summary in the export package.
- Post-revision validation mode: after importing a revised draft, run the book-level analysis automatically and compare against the previous draft's analysis to highlight what changed.

### 1.2 What This Phase Does Not Deliver

- No automated token budget management (the original Phase 4 scope). Token budgets are managed by the operator through existing Openclaw conventions — choosing the model, setting rate-limit-aware cron intervals, and reviewing output before import.
- No automated retry logic. Failed revisions are handled by the human editor or by rerunning the revision with adjusted instructions.

---

## 2. Cross-Draft Comparison

### 2.1 Delta Report

When two book-level analysis reports exist for the same manuscript (draft N and draft N+1), the engine computes deltas:

**Per metric:**
- Value in draft N
- Value in draft N+1
- Absolute change
- Percentage change
- Direction: improving / stable / regressing

**Per flagged word/phrase:**
- Count in draft N
- Count in draft N+1
- Change
- Status: resolved (dropped below threshold) / improved (reduced but still flagged) / unchanged / worsened / new (not flagged in draft N)

### 2.2 Delta Report Format

```xml
<book-analysis-delta xmlns="https://bookml.org/ns/analysis/1.0"
    schema-version="2.0"
    generated-at="2026-04-19T12:00:00Z"
    book-id="CTY"
    draft-from="2"
    draft-to="3">

  <metric-deltas>
    <metric name="total-words" from="60412" to="59800" change="-612" percent="-1.0" direction="stable"/>
    <metric name="type-token-ratio" from="0.070" to="0.089" change="+0.019" percent="+27.1" direction="improving"/>
    <metric name="hapax-ratio" from="0.50" to="0.56" change="+0.06" percent="+12.0" direction="improving"/>
  </metric-deltas>

  <word-deltas>
    <word value="door" from="312" to="87" change="-225" status="improved"/>
    <word value="years" from="198" to="52" change="-146" status="resolved"/>
    <word value="room" from="176" to="95" change="-81" status="improved"/>
    <word value="corridor" from="0" to="45" change="+45" status="new"/>
  </word-deltas>

  <ngram-deltas>
    <ngram phrase="shook her head" from="47" to="4" change="-43" status="resolved"/>
    <ngram phrase="thin air" from="31" to="12" change="-19" status="improved"/>
  </ngram-deltas>

  <chapter-deltas>
    <chapter id="CH01" ttr-from="0.283" ttr-to="0.312" outliers-from="35" outliers-to="12"/>
    <chapter id="CH02" ttr-from="0.301" ttr-to="0.345" outliers-from="28" outliers-to="8"/>
  </chapter-deltas>
</book-analysis-delta>
```

The `status="new"` classification is critical — it catches the validation loop problem where a revision introduces new repetition (the LLM fixes "corridor" but starts overusing "passageway"). This was a known risk from the start of the project and this is where it gets surfaced.

### 2.3 CLI Subcommand

```bash
content-analyser compare-books \
  --from path/to/draft2-book-analysis.xml \
  --to path/to/draft3-book-analysis.xml \
  --output path/to/delta-report.xml \
  --summary path/to/delta-summary.txt
```

The text summary is human-readable:

```
DRAFT COMPARISON — Draft 2 → Draft 3

Vocabulary diversity: 0.070 → 0.089 (+27.1%) ▲ IMPROVING
Unique word ratio: 0.50 → 0.56 (+12.0%) ▲ IMPROVING

RESOLVED (no longer flagged):
- "years" — was 198, now 52 (below threshold)
- "shook her head" — was 47, now 4

IMPROVED (reduced but still flagged):
- "door" — was 312, now 87 (-72%)
- "room" — was 176, now 95 (-46%)

NEW ISSUES (introduced by this revision):
- "corridor" — 45 occurrences (not present in draft 2)

UNCHANGED:
- "eyes" — was 145, now 140 (-3%, within noise)
```

---

## 3. Burn-Down Tracking

### 3.1 Report Persistence

Book-level analysis reports are persisted alongside frozen drafts, following the same lifecycle from Phase 1 §6:
- When a new draft is imported, the analysis engine runs against the now-frozen previous draft and saves the book-level report as `book_draft{N}_analysis.xml`.
- The current working draft is analysed on demand for the dashboard but not persisted.

### 3.2 Dashboard Visualisation

The book-level dashboard gains a burn-down chart:

**X-axis:** Draft number (1, 2, 3, ...).
**Y-axis (primary):** Total book-wide outlier count (words + phrases classified as `book-wide-tic`).
**Y-axis (secondary):** TTR trend line.

Each frozen draft is a stable data point. The current working draft is a live indicator at the right edge.

The chart tells the story: "Draft 1 (Llama) had 450 outliers. After the Codex revision pass (draft 2), that dropped to 180. After human editing (draft 3), it's down to 45. TTR climbed from 0.07 to 0.12."

This is the "pattern of improvement" visualisation — the primary deliverable from the original Phase 1 spec, now realised at book level.

### 3.3 Model Attribution

Where drafts carry `modified-by` provenance attributes, the delta report attributes improvements to the responsible model or human editor. The burn-down chart can colour-code data points by contributor: "Llama generated, Codex revised, Human polished."

---

## 4. Export Integration

### 4.1 names.xml

The BookML exporter includes `names.xml` in every export package. Generated from the character name table in the database. If the table is empty, the file contains zero entries — the CLI will refuse to generate work orders or run validation against an empty names file, enforcing the requirement to populate the character table.

### 4.2 Lexical Summary (Optional)

The exporter optionally includes the latest lexical summary text file in the package. This is a convenience — the operator can also generate it on Binky using the CLI. But having it in the package means it's immediately available for inclusion in revision scripts without an extra step.

### 4.3 Analysis Reports (Optional)

The exporter optionally includes the latest book-level analysis report XML in the package. This allows the CLI on Binky to run `compare-books` against the previous draft's analysis without needing to re-analyse the previous draft locally.

---

## 5. Post-Revision Workflow

The complete workflow with these additions:

**Early draft (Openclaw-assisted):**
1. Export BookML package from cloud editor (includes names.xml).
2. On Binky: `content-analyser analyse-book` → produces book analysis + lexical summary.
3. Paste lexical summary into Openclaw revision script alongside other instructions.
4. Openclaw processes manuscript with LLM, produces revised draft.
5. Import revised draft to cloud editor.
6. Editor runs book-level analysis on new draft, compares against previous → delta report and burn-down chart update.
7. Review delta: did outliers drop? Did new issues appear? If new issues appeared, address in next revision pass.

**Mature draft (human editing):**
1. View book-level dashboard in editor — see outlier words, TTR, burn-down trend.
2. Drill into per-chapter view for specific issues.
3. Edit manually in the editor.
4. Check dashboard again — live indicator shows improvement.

---

## 6. Implementation Scope

### Session 1 — Claude Code on Vimes (C# changes)

1. `BookAnalyser` class — processes all chapters from a book manifest. (May already exist from Phase 3 Session 1 if that was completed.)
2. `BookDeltaComparer` class — takes two book-level analysis reports and produces the delta report.
3. `compare-books` CLI subcommand.
4. Delta summary text generator.
5. Rebuild and republish.

### Session 2 — Seonyx Editor (export + dashboard)

1. Update BookML exporter to include `names.xml`.
2. Optionally include lexical summary and analysis report in export.
3. Add burn-down chart to the book-level dashboard.
4. Add delta report view (draft comparison with resolved/improved/new/unchanged categories).
5. Trigger analysis on draft import for automatic comparison.

---

## 7. Acceptance Criteria

1. `content-analyser analyse-book` produces a correct book-level analysis with distribution classifications across a full manuscript.
2. `content-analyser compare-books` produces a correct delta report identifying resolved, improved, new, and unchanged items.
3. New repetition introduced by a revision (the "corridor → passageway" problem) is flagged with `status="new"` in the delta report.
4. The burn-down chart in the dashboard displays stable data points for frozen drafts and a live indicator for the current working draft.
5. The BookML exporter includes `names.xml` in every package.
6. The lexical summary text is suitable for direct inclusion in an Openclaw revision script.
7. The per-chapter analysis and dashboard continue to work independently.
