using System.Collections.Generic;
using System.Linq;

namespace Seonyx.Web.Services
{
    public enum PlainTextParsePolicy
    {
        // Parse failures throw MarkdownParseException (BookML import: the
        // whole import rolls back).
        Strict,

        // Parse failures produce one plain run holding the literal text,
        // plus a warning (legacy .txt import, readers, repair).
        Lenient
    }

    /// <summary>
    /// The single route from plain text (possibly carrying the Markdown
    /// subset **strong** / *emph* / _emph_) to paragraph runs, plus the
    /// rules built on it. Deliberately free of database access so every
    /// caller -- importers, the editor, the exporter, the repair action --
    /// shares one implementation.
    /// </summary>
    public static class PlainTextRunBuilder
    {
        // Matches the SQL diagnostic predicate
        // LIKE '%[^ ' + CHAR(9) + CHAR(10) + CHAR(13) + ']%' exactly, so
        // the repair preview's counts can be checked against SQL.
        public static bool HasText(string s)
        {
            if (s == null) return false;
            foreach (var c in s)
            {
                if (c != ' ' && c != '\t' && c != '\r' && c != '\n') return true;
            }
            return false;
        }

        public static IList<InlineRun> FragmentsToRuns(IEnumerable<MarkdownFragment> fragments)
        {
            return InlineXmlRunConverter.Coalesce(fragments.Select(f => new InlineRun
            {
                Text        = f.Text,
                IsEmph      = f.IsEmph,
                IsStrong    = f.IsStrong,
                IsSmallCaps = false,
                OverlayType = null,
                IsBreak     = false
            }).ToList());
        }

        /// <summary>
        /// Parses text into coalesced runs. Non-empty text never yields zero
        /// runs: if the parser returns no fragments (e.g. a lone "**"), the
        /// literal text is kept as one plain run.
        /// </summary>
        public static IList<InlineRun> BuildRunsFromPlainText(string text, string pid,
            PlainTextParsePolicy policy, out string warning)
        {
            warning = null;
            text = text ?? "";

            IList<MarkdownFragment> fragments;
            try
            {
                fragments = MarkdownInlineParser.Parse(text, pid);
            }
            catch (MarkdownParseException ex)
            {
                if (policy == PlainTextParsePolicy.Strict) throw;
                warning = ex.Message + " Stored as plain unformatted text, markers left in.";
                return new List<InlineRun> { PlainRun(text) };
            }

            var runs = FragmentsToRuns(fragments);
            if (runs.Count == 0 && text.Length > 0)
                return new List<InlineRun> { PlainRun(text) };
            return runs;
        }

        /// <summary>
        /// The runs a reader should use. Stored runs win; a paragraph with
        /// text but no stored runs is never treated as empty -- its text is
        /// parsed (lenient) instead.
        /// </summary>
        public static IList<InlineRun> EffectiveRuns(string paragraphText, string pid,
            IList<InlineRun> storedRuns, out string warning)
        {
            warning = null;
            if (storedRuns != null && storedRuns.Count > 0) return storedRuns;
            if (!HasText(paragraphText)) return new List<InlineRun>();
            return BuildRunsFromPlainText(paragraphText, pid, PlainTextParsePolicy.Lenient, out warning);
        }

        public static string ProseOf(IEnumerable<InlineRun> runs, bool includeOverlays)
        {
            return string.Concat(runs
                .Where(r => !r.IsBreak && (includeOverlays || r.OverlayType == null))
                .Select(r => r.Text));
        }

        /// <summary>
        /// The editor's load-time self-conversion predicate (W8): a paragraph
        /// whose runs are still exactly one flat, unstyled, non-empty run
        /// whose text actually contains Markdown markers. Returns the
        /// converted runs, or null if the paragraph doesn't qualify.
        /// </summary>
        public static IList<InlineRun> TryBuildLegacyMarkdownConversion(IList<InlineRun> existingRuns, string pid)
        {
            if (existingRuns == null || existingRuns.Count != 1) return null;

            var only = existingRuns[0];
            bool isFlatAndUnstyled = !only.IsEmph && !only.IsStrong && !only.IsSmallCaps
                && only.OverlayType == null && !only.IsBreak;
            if (!isFlatAndUnstyled) return null;
            if (string.IsNullOrEmpty(only.Text)) return null;

            IList<MarkdownFragment> fragments;
            try
            {
                fragments = MarkdownInlineParser.Parse(only.Text, pid);
            }
            catch (MarkdownParseException)
            {
                // Malformed markers (e.g. ***triple***) -- leave untouched.
                return null;
            }

            // "Nothing to convert" means exactly one plain fragment whose text
            // is byte-identical to the original. A single plain fragment with
            // DIFFERENT text still counts as a change (e.g. \*escaped\*
            // unescaping to *escaped*).
            bool isTrueNoOp = fragments.Count == 1
                && !fragments[0].IsStrong && !fragments[0].IsEmph
                && fragments[0].Text == only.Text;
            if (isTrueNoOp) return null;

            var converted = FragmentsToRuns(fragments);

            // A conversion to zero runs (e.g. a lone "**") would blank the
            // paragraph; leave it as it is.
            if (converted.Count == 0) return null;
            return converted;
        }

        /// <summary>
        /// True when re-importing importedText over a paragraph changes
        /// nothing a .txt file can express: either the text equals the
        /// stored ParagraphText, or it parses to the same words with the
        /// same bold/italic as the stored runs once overlay, small caps,
        /// line breaks and emphasis type (which .txt can't carry) are set
        /// aside.
        /// </summary>
        public static bool IsUnchangedByPlainTextReimport(string importedText, string storedParagraphText,
            string pid, IList<InlineRun> storedRuns)
        {
            if (importedText == storedParagraphText) return true;
            if (storedRuns == null || storedRuns.Count == 0) return false;

            IList<InlineRun> incoming;
            try
            {
                string ignored;
                incoming = BuildRunsFromPlainText(importedText, pid, PlainTextParsePolicy.Strict, out ignored);
            }
            catch (MarkdownParseException)
            {
                return false;
            }

            var a = ReduceToPlainTextExpressible(incoming);
            var b = ReduceToPlainTextExpressible(storedRuns);
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].Text != b[i].Text || a[i].IsStrong != b[i].IsStrong || a[i].IsEmph != b[i].IsEmph)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Names the formatting in these runs that a .txt file can't
        /// express, e.g. "overlay, small caps"; null if there is none.
        /// </summary>
        public static string DescribeFormattingPlainTextCannotExpress(IList<InlineRun> runs)
        {
            if (runs == null) return null;
            var found = new List<string>();
            if (runs.Any(r => r.OverlayType != null)) found.Add("overlay");
            if (runs.Any(r => r.IsSmallCaps)) found.Add("small caps");
            if (runs.Any(r => r.IsBreak)) found.Add("line break");
            if (runs.Any(r => r.EmphType != null)) found.Add("emphasis type");
            return found.Count > 0 ? string.Join(", ", found) : null;
        }

        private static IList<InlineRun> ReduceToPlainTextExpressible(IEnumerable<InlineRun> runs)
        {
            return InlineXmlRunConverter.Coalesce(runs
                .Where(r => !r.IsBreak && !string.IsNullOrEmpty(r.Text))
                .Select(r => new InlineRun { Text = r.Text, IsStrong = r.IsStrong, IsEmph = r.IsEmph })
                .ToList());
        }

        private static InlineRun PlainRun(string text)
        {
            return new InlineRun { Text = text };
        }
    }
}
