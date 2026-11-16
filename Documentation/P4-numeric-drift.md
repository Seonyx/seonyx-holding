# P4 — Numeric Drift from Overlay-Aware Analytics

## Status: placeholder — real drift data not yet captured

This phase changed `ContentAnalysisEngine`'s text extraction
(`BookmlReader.ExtractText`) to exclude `<overlay>` text (diegetic
on-screen text, e.g. HUD banners) from analysis by default, per the
BookML Inline Presentation amendment §7.1/7.2. On any chapter containing
overlay-tagged text, word counts, TTR, flagged words, and flagged n-grams
will shift versus a pre-P4 analysis run of the same chapter — this is
expected and correct, not a regression.

Real before/after drift data requires a chapter with genuine
editor-added overlay content (via the P3 right-click "Mark as overlay…"
UI), which didn't exist anywhere in the repo as of this phase — the
editor UI to create overlays was only built in the P3 session
immediately preceding this one. Verification during implementation used
hand-built fixtures covering the extraction logic's correctness (word
boundaries at overlay/linebreak excision points), not real manuscript
drift.

## To fill this in

Once a real CTY (or Chilli Racers) chapter has overlay-tagged content —
either from manual P3 editor testing already on the live DB, or newly
tagged for this purpose — export its chapter XML and run:

```bash
content-analyser analyse --chapter <chapter>.xml --names <names>.xml > before.xml
content-analyser analyse --chapter <chapter>.xml --names <names>.xml --include-overlays > after.xml
diff before.xml after.xml
```

Then summarize here: which metrics moved, by how much, and confirm the
shift traces back to the overlay-tagged spans specifically (not some
other unrelated change) — e.g. total word count drop should roughly
match the overlay text's own word count, flagged-word/n-gram lists
should differ only where overlay text contributed to them.
