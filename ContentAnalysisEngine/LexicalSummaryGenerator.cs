using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ContentAnalysisEngine
{
    public static class LexicalSummaryGenerator
    {
        public static string Generate(BookAnalysisReport report)
        {
            if (report == null) throw new ArgumentNullException("report");

            var m   = report.Metrics ?? new BookMetricsSummary();
            var sb  = new StringBuilder();

            string title = string.IsNullOrEmpty(report.BookId)
                ? "Unknown"
                : report.BookId;

            sb.AppendLine(string.Format("LEXICAL ANALYSIS SUMMARY -- {0}", title));
            sb.AppendLine();

            sb.AppendLine(string.Format(
                "Manuscript: {0:N0} words across {1} chapter{2}.",
                report.TotalWords,
                report.TotalChapters,
                report.TotalChapters == 1 ? "" : "s"));

            string ttrAssessment;
            if (m.TypeTokenRatio < 0.08)
                ttrAssessment = "below the typical fiction range of 0.10-0.15";
            else if (m.TypeTokenRatio < 0.10)
                ttrAssessment = "approaching the typical fiction range of 0.10-0.15";
            else
                ttrAssessment = "within the typical fiction range of 0.10-0.15";

            sb.AppendLine(string.Format(
                "Vocabulary diversity (TTR): {0} -- {1}.",
                m.TypeTokenRatio.ToString("G3", CultureInfo.InvariantCulture),
                ttrAssessment));

            string hapaxAssessment = m.HapaxRatio >= 0.55
                ? "good"
                : m.HapaxRatio >= 0.45 ? "adequate but could be richer" : "low -- vocabulary may be repetitive";

            sb.AppendLine(string.Format(
                "Unique word ratio (hapax): {0} -- {1}.",
                m.HapaxRatio.ToString("G3", CultureInfo.InvariantCulture),
                hapaxAssessment));

            sb.AppendLine(string.Format(
                "Mean sentence length: {0} words (std dev {1}).",
                m.MeanSentenceLength.ToString("G3", CultureInfo.InvariantCulture),
                m.SentenceLengthStdDev.ToString("G3", CultureInfo.InvariantCulture)));

            // Book-wide outlier words (book-wide-tic only)
            var bookWideTicWords = (report.OutlierWords ?? Enumerable.Empty<BookFlaggedWord>())
                .Where(w => string.Equals(w.Distribution, "book-wide-tic", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (bookWideTicWords.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("BOOK-WIDE OVERUSED WORDS (appear excessively across most chapters):");
                foreach (var w in bookWideTicWords)
                {
                    sb.AppendLine(string.Format(
                        "- \"{0}\" -- {1} occurrences across {2} chapter{3}",
                        w.Value,
                        w.Count,
                        w.ChaptersPresent,
                        w.ChaptersPresent == 1 ? "" : "s"));
                }
            }

            // Book-wide outlier n-grams (book-wide-tic only)
            var bookWideTicNgrams = (report.OutlierNgrams ?? Enumerable.Empty<BookFlaggedNgram>())
                .Where(n => string.Equals(n.Distribution, "book-wide-tic", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (bookWideTicNgrams.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("BOOK-WIDE OVERUSED PHRASES:");
                foreach (var n in bookWideTicNgrams)
                {
                    sb.AppendLine(string.Format(
                        "- \"{0}\" -- {1} occurrences across {2} chapter{3}",
                        n.Phrase,
                        n.Count,
                        n.ChaptersPresent,
                        n.ChaptersPresent == 1 ? "" : "s"));
                }
            }

            // Character names
            var names = report.NamesExcluded ?? new List<string>();
            if (names.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(string.Format(
                    "CHARACTER NAMES (do not alter): {0}.",
                    string.Join(", ", names)));
            }

            // Standing instruction
            sb.AppendLine();
            sb.AppendLine("INSTRUCTION: Reduce the flagged terms, vary the vocabulary, and improve");
            sb.AppendLine("lexical diversity while preserving narrative tone, character voice, and");
            sb.AppendLine("plot continuity. Chapter-specific terminology appropriate to the setting");
            sb.AppendLine("(e.g., technical terms in scenes set in specific locations) should be");
            sb.AppendLine("preserved even if locally concentrated.");

            return sb.ToString();
        }
    }
}
