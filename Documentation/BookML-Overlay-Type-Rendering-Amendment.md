# BookML Inline Presentation — Amendment 2 — Overlay Type Rendering Strategy

**Status:** Draft for review
**Supersedes:** §8.1 (EPUB output mapping) and §8.2 (audiobook output mapping) of `BookML-Inline-Presentation-Amendment.md`. All other sections of that amendment remain in force unchanged.
**Affects:** P5 planning (EPUB and audiobook exporters — not yet implemented). No impact on P1–P4.

---

## 1. Background

The original amendment (§2.5) defined `overlay/@type` as a controlled-but-extensible string vocabulary — new values are added by authors as they discover new kinds of diegetic on-screen text. This was deliberate: the taxonomy belongs to the manuscript, not the software.

That decision raises a fair question: how does a third-party EPUB renderer (Calibre, Kindle, Apple Books, Kobo) know what to do with a type it has never seen? The answer, which the original amendment left implicit and this amendment now makes explicit, is that **it doesn't need to** — the CSS that styles overlays is bundled inside the EPUB itself, and third-party renderers apply that CSS like any other stylesheet. Renderers never interpret type semantics; they only apply rules the book itself provides.

The open-ended vocabulary is safe **only if** every EPUB ships with a baseline `.overlay` CSS rule that renders any overlay acceptably regardless of type. This amendment formalises that requirement and the fallback pattern that makes it work.

---

## 2. Replacement §8.1 — EPUB output mapping

BookML inline elements map to XHTML as follows:

| BookML | XHTML | CSS hook |
|---|---|---|
| `<emph>` (no type) | `<em>` | `em` |
| `<emph type="X">` | `<em class="emph-X">` | `.emph-X` |
| `<strong>` | `<strong>` | `strong` |
| `<smallcaps>` | `<span class="smallcaps">` | `.smallcaps` |
| `<overlay type="X">` | `<aside class="overlay overlay-X">` | `.overlay` (baseline, mandatory) and `.overlay-X` (per-type refinement, optional) |
| `<linebreak/>` | `<br/>` | — |

### 2.1 Mandatory baseline rule

Every EPUB produced by Seonyx **must** bundle a stylesheet containing at minimum a `.overlay` rule that renders overlays visually distinct from surrounding prose. This is not optional — it is what makes the open-ended type vocabulary safe.

Default baseline shipped with new projects (editable per-project, see §2.3):

```css
/* Baseline — applied to every overlay regardless of type */
.overlay {
    display: block;
    padding: 0.5em 1em;
    margin: 0.5em 0;
    border: 1px solid #99c;
    background: #eef;
    font-family: sans-serif;
    font-size: 0.9em;
}
```

### 2.2 Fallback guarantee

Any `<overlay type="X">` whose type has no corresponding `.overlay-X` rule in the bundled stylesheet renders using the baseline `.overlay` rule alone. Consequences:

- **New types never break rendering.** Worst case an overlay renders "generically overlaid" — never invisibly, never as broken prose.
- Authors can invent types freely during drafting; visual refinement can wait for publication prep.
- Third-party renderers require zero knowledge of overlay-type semantics. They apply the bundled CSS like any other stylesheet.

Per-type refinements, when present, cascade over the baseline in the normal CSS way:

```css
.overlay-sponsored-alert {
    background: #fed;
    border-color: #c93;
}

.overlay-hud {
    font-family: monospace;
    background: #001;
    color: #0f0;
}
```

### 2.3 Per-project stylesheet

Each Seonyx project stores an `overlay-styles.css` file containing the baseline rule and any per-type refinements. Authors may edit this file to add or refine per-type rules as their type vocabulary settles. A dedicated editor for that stylesheet is out of scope; direct file editing on the project's storage is sufficient for now. The exporter bundles the project's current `overlay-styles.css` into every EPUB export.

### 2.4 Class name normalisation

Overlay type values are entered verbatim by authors, and the type field accepts arbitrary strings. Because CSS class names have their own rules, the exporter normalises each type value to a CSS-safe suffix at export time:

1. Lowercase.
2. Replace any character not in `[a-z0-9-]` with a hyphen.
3. Collapse consecutive hyphens.
4. Trim leading and trailing hyphens.

So `HUD`, `hud`, and `H.U.D.` all yield class name `overlay-hud`. `Sponsored Alert` and `sponsored-alert` both yield `overlay-sponsored-alert`.

Authors are nudged toward ASCII kebab-case type values by autocomplete (which shows already-used sanitised forms) but the schema does not enforce this. Non-ASCII characters are stripped by rule 2; authors using non-Latin scripts for type values should be aware their class names will collapse to hyphens. This is unlikely to matter for the current Chilli Racers / CTY manuscripts but is noted for later multilingual work.

The **original type value is preserved verbatim in the database** and in autocomplete suggestions — normalisation applies only to the emitted CSS class name. Authors see their own spellings when picking overlay types; the CSS class is a rendering-layer concern.

---

## 3. Replacement §8.2 — Audiobook output mapping

- `<emph>`, `<strong>`, `<smallcaps>` → text passed through verbatim. Piper TTS has no native emphasis prosody; type refinement on `<emph>` is likewise irrelevant to speech output.
- `<overlay>` content is, by default, **skipped** in the generated chapter text. **Overlay `@type` is irrelevant to audiobook behaviour** — the exporter treats every overlay identically regardless of type. An option in the export dialog allows including overlays, with a brief pause marker before and after each; even when included, the type does not affect how the content is spoken.
- `<linebreak/>` → newline in the chapter text file.

---

## 4. Cross-reference to §2.5 (unchanged)

§2.5's open-ended `@type` vocabulary remains as originally defined. This amendment does not modify §2.5; it retroactively justifies §2.5's openness by formalising the rendering-layer guarantees that make it safe. P5 planning should cite §8.1's fallback guarantee (§2.2 above) when explaining why new types can be introduced freely.

---

## 5. Impact on P5 planning

When P5 (EPUB and audiobook exporters) is planned, the phase brief must include:

1. A default `overlay-styles.css` shipped with every new project, containing the baseline `.overlay` rule and starter refinements for the §2.5 initial vocabulary (`sponsored-alert`, `hud`, `system-message`, `subtitle`).
2. Deterministic type-to-class-name normalisation per §2.4.
3. Bundling of the project's current `overlay-styles.css` into every EPUB export.
4. An explicit round-trip fixture where an overlay with a never-before-seen type is exported and the resulting EPUB is verified to render via the baseline `.overlay` rule (fallback verified, not assumed).
5. An explicit test that audiobook export produces identical output regardless of overlay `@type` values (type-irrelevance verified).

---

*End of amendment.*
