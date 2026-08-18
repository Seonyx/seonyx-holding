# RoundTripCheckCli fixtures

Each fixture is a subfolder containing a `fixture.txt` whose first line
names the fixture kind, plus the input/expected files that kind requires.
`roundtrip-check roundtrip --fixtures <this directory>` runs every
subfolder with a `fixture.txt` and reports pass/fail per fixture.

## Fixture kinds

- **`xml-roundtrip`** (`input.xml`): true round-trip. `input.xml` is
  parsed to runs via `InlineXmlRunConverter.FlattenToRuns`, then rebuilt
  to XML via `InlineXmlRunConverter.BuildParaContent`. Output must equal
  the (whitespace-normalised) input byte-for-byte — this is the "lossless
  round-trip" guarantee from the amendment, section 5.3.

- **`markdown-parse`** (`input.md`, `expected.txt`): one-way parse check.
  `input.md` is parsed via `MarkdownInlineParser.Parse`. `expected.txt`
  has one line per expected fragment, formatted `TEXT|strong|emph` (`-`
  in place of `strong`/`emph` when that flag is false). Markdown does not
  losslessly reverse to XML — there's no exporter path back to `**bold**`
  text, only to XML — so this kind verifies parsing correctness, not
  round-trip identity.

- **`markdown-roundtrip`** (`input.md`): a *different* kind of round-trip
  — through runs and back to Markdown, not to XML. `input.md` is parsed
  to fragments, converted to runs, and serialized back via
  `RunsToMarkdownSerializer.Serialize`. Output must equal `input.md`
  byte-for-byte. This is the fixture kind that verifies the serializer
  added to close the P2 plan's decision #6 gap (keeping `ParagraphText`
  truthful relative to run state).

- **`negative-nesting`** (`input.xml`): `input.xml` contains `<overlay>`
  nested inside another `<overlay>`, which the amendment (section 2.4)
  disallows. Flattening via `InlineXmlRunConverter.FlattenToRuns` must
  throw `InlineXmlNestingException`. Pass = exception thrown.

- **`negative-serialize`** (`input.xml`): `input.xml` contains
  overlay/smallcaps/linebreak markup (Markdown-inexpressible). Runs are
  flattened successfully, but serializing them back to Markdown via
  `RunsToMarkdownSerializer.Serialize` must throw
  `NonExpressibleRunException`. Pass = exception thrown. This verifies
  the serializer's contract that non-expressible runs fail loudly rather
  than silently dropping content.

- **`xml-coalesce`** (`input.xml`, `expected.xml`): `input.xml` has two
  or more adjacent XML elements with identical style (e.g. two sibling
  `<emph>` elements with no unstyled text between them — a valid but
  non-canonical author input). Flatten + `Coalesce` + rebuild must equal
  `expected.xml` (the coalesced canonical form), not `input.xml` itself.
  Verifies `InlineXmlRunConverter.Coalesce` actually merges adjacent
  identical-style runs rather than passing them through as separate
  elements, which `ParagraphRunService.SaveRuns` would otherwise reject.

## Coverage (amendment section 10.3 acceptance list)

| # | Case | Fixture(s) |
|---|------|-----------|
| 1 | Plain text | `01-plain-text` |
| 2 | Single `<strong>` span | `02-single-strong` |
| 3 | Single `<emph>` span | `03-single-emph` (real prose from `testdata/junk_draft2/ch04/ch04-chapter.xml`) |
| 4 | Nested `<strong><emph>...</emph></strong>` | `04-nested-strong-emph` |
| 5 | `<smallcaps>` (editor-applied) | `05-smallcaps` |
| 6 | `<linebreak/>` mid-paragraph | `06-linebreak-mid-paragraph` |
| 7 | `<overlay>` with mixed `<strong>`/`<emph>` | `07-overlay-mixed-strong-emph` |
| 8 | Escaped Markdown characters | `08-escaped-markdown` |
| — | Multiple `<emph>` spans in one paragraph | `09-multiple-emph-spans` (real prose, `ch04-chapter.xml` line 158) |
| — | Markdown: `**bold**` | `10-md-strong` |
| — | Markdown: `*italic*` / `_italic_` | `11-md-emph-star`, `12-md-emph-underscore` |
| — | Markdown: nested `**_bold italic_**` | `13-md-nested` |
| — | Markdown: mid-word underscore is literal | `14-md-midword-underscore-literal` |
| — | Markdown: triple-asterisk rejected | `15-md-triple-asterisk-rejected` (negative — see below) |
| — | Overlay containing a `<linebreak/>` | `16-overlay-with-linebreak` |
| — | Consecutive same-style runs coalesce | `17-coalescing` |
| — | `emph/@type` preserved through round-trip | `18-emph-type-attribute` |
| — | `markdown-roundtrip`: expressible subset | `19-md-roundtrip-expressible` |
| — | `negative-nesting`: overlay-in-overlay | `20-overlay-in-overlay-rejected` |
| — | `negative-serialize`: overlay not Markdown-expressible | `21-overlay-not-serializable` |
| — | `negative-serialize`: smallcaps not Markdown-expressible | `22-smallcaps-not-serializable` |

Fixture 15 (triple-asterisk) is verified by `markdown-parse` asserting
the parser throws, using an `expected.txt` file whose first line is the
literal token `THROWS` (see that fixture's own notes) — kept in the
`markdown-parse` kind rather than inventing a sixth fixture kind for a
single case.
