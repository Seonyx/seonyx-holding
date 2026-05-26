using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace ContentAnalysisEngine
{
    public static class BookDeltaComparer
    {
        public static BookAnalysisDelta Compare(
            BookAnalysisReport from, BookAnalysisReport to,
            int draftFrom, int draftTo)
        {
            if (from == null) throw new ArgumentNullException("from");
            if (to   == null) throw new ArgumentNullException("to");

            var delta = new BookAnalysisDelta
            {
                BookId      = to.BookId ?? from.BookId ?? "",
                DraftFrom   = draftFrom,
                DraftTo     = draftTo,
                GeneratedAt = DateTime.UtcNow,
            };

            // Metric deltas
            var fm = from.Metrics ?? new BookMetricsSummary();
            var tm = to.Metrics   ?? new BookMetricsSummary();

            delta.MetricDeltas = new List<MetricDelta>
            {
                MakeMetric("total-words",         from.TotalWords,       to.TotalWords,       "neutral"),
                MakeMetric("type-token-ratio",    fm.TypeTokenRatio,     tm.TypeTokenRatio,   "higher"),
                MakeMetric("moving-average-ttr",  fm.MovingAverageTTR,   tm.MovingAverageTTR, "higher"),
                MakeMetric("hapax-ratio",         fm.HapaxRatio,         tm.HapaxRatio,       "higher"),
                MakeMetric("mean-sentence-length",fm.MeanSentenceLength, tm.MeanSentenceLength,"neutral"),
            };

            // Word deltas
            var fromWords = (from.OutlierWords ?? Enumerable.Empty<BookFlaggedWord>())
                .ToDictionary(w => w.Value ?? "", w => w.Count, StringComparer.OrdinalIgnoreCase);
            var toWords = (to.OutlierWords ?? Enumerable.Empty<BookFlaggedWord>())
                .ToDictionary(w => w.Value ?? "", w => w.Count, StringComparer.OrdinalIgnoreCase);

            var allWordKeys = new HashSet<string>(fromWords.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var k in toWords.Keys) allWordKeys.Add(k);

            delta.WordDeltas = new List<WordDelta>();
            foreach (var word in allWordKeys.OrderBy(w => w))
            {
                int fc, tc;
                bool inFrom = fromWords.TryGetValue(word, out fc);
                bool inTo   = toWords.TryGetValue(word, out tc);
                delta.WordDeltas.Add(new WordDelta
                {
                    Value  = word,
                    From   = inFrom ? fc : 0,
                    To     = inTo   ? tc : 0,
                    Change = (inTo ? tc : 0) - (inFrom ? fc : 0),
                    Status = ClassifyDeltaStatus(inFrom, inTo, fc, tc),
                });
            }

            // N-gram deltas
            var fromNgrams = (from.OutlierNgrams ?? Enumerable.Empty<BookFlaggedNgram>())
                .ToDictionary(n => n.Phrase ?? "", n => n.Count, StringComparer.OrdinalIgnoreCase);
            var toNgrams = (to.OutlierNgrams ?? Enumerable.Empty<BookFlaggedNgram>())
                .ToDictionary(n => n.Phrase ?? "", n => n.Count, StringComparer.OrdinalIgnoreCase);

            var allNgramKeys = new HashSet<string>(fromNgrams.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var k in toNgrams.Keys) allNgramKeys.Add(k);

            delta.NgramDeltas = new List<NgramDelta>();
            foreach (var phrase in allNgramKeys.OrderBy(p => p))
            {
                int fc, tc;
                bool inFrom = fromNgrams.TryGetValue(phrase, out fc);
                bool inTo   = toNgrams.TryGetValue(phrase, out tc);
                delta.NgramDeltas.Add(new NgramDelta
                {
                    Phrase = phrase,
                    From   = inFrom ? fc : 0,
                    To     = inTo   ? tc : 0,
                    Change = (inTo ? tc : 0) - (inFrom ? fc : 0),
                    Status = ClassifyDeltaStatus(inFrom, inTo, fc, tc),
                });
            }

            // Chapter deltas (join on Id)
            var fromChaps = (from.ChapterSummaries ?? Enumerable.Empty<BookChapterSummary>())
                .ToDictionary(c => c.Id ?? "", c => c, StringComparer.OrdinalIgnoreCase);
            var toChaps = (to.ChapterSummaries ?? Enumerable.Empty<BookChapterSummary>())
                .ToDictionary(c => c.Id ?? "", c => c, StringComparer.OrdinalIgnoreCase);

            var allChapIds = new HashSet<string>(fromChaps.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var k in toChaps.Keys) allChapIds.Add(k);

            delta.ChapterDeltas = new List<ChapterDelta>();
            foreach (var id in allChapIds.OrderBy(i => i))
            {
                BookChapterSummary fc2, tc2;
                fromChaps.TryGetValue(id, out fc2);
                toChaps.TryGetValue(id, out tc2);
                if (fc2 == null) fc2 = new BookChapterSummary { Id = id };
                if (tc2 == null) tc2 = new BookChapterSummary { Id = id };
                delta.ChapterDeltas.Add(new ChapterDelta
                {
                    Id           = id,
                    TtrFrom      = fc2.Ttr,
                    TtrTo        = tc2.Ttr,
                    OutliersFrom = fc2.OutlierCount,
                    OutliersTo   = tc2.OutlierCount,
                });
            }

            return delta;
        }

        private static string ClassifyDeltaStatus(bool inFrom, bool inTo, int from, int to)
        {
            if (!inFrom && inTo)  return "new";
            if (inFrom && !inTo)  return "resolved";
            // both present
            double pct = from > 0 ? Math.Abs(to - from) / (double)from : 0;
            if (to < from) return pct <= 0.05 ? "unchanged" : "improved";
            if (to > from) return pct <= 0.05 ? "unchanged" : "worsened";
            return "unchanged";
        }

        private static MetricDelta MakeMetric(string name, double from, double to, string improvingDirection)
        {
            double change  = to - from;
            double pct     = from != 0 ? (change / Math.Abs(from)) * 100.0 : 0;
            string dir;
            if (Math.Abs(pct) <= 2.0)
                dir = "stable";
            else if (improvingDirection == "higher")
                dir = change > 0 ? "improving" : "regressing";
            else if (improvingDirection == "lower")
                dir = change < 0 ? "improving" : "regressing";
            else
                dir = "stable";

            return new MetricDelta
            {
                Name      = name,
                From      = from,
                To        = to,
                Change    = change,
                Percent   = Math.Round(pct, 1),
                Direction = dir,
            };
        }
    }

    // ---------------------------------------------------------------------------

    public class BookAnalysisDelta
    {
        public string   BookId      { get; set; }
        public int      DraftFrom   { get; set; }
        public int      DraftTo     { get; set; }
        public DateTime GeneratedAt { get; set; }

        public List<MetricDelta>  MetricDeltas  { get; set; } = new List<MetricDelta>();
        public List<WordDelta>    WordDeltas    { get; set; } = new List<WordDelta>();
        public List<NgramDelta>   NgramDeltas   { get; set; } = new List<NgramDelta>();
        public List<ChapterDelta> ChapterDeltas { get; set; } = new List<ChapterDelta>();

        private static readonly XNamespace Ns = XNamespace.Get("https://bookml.org/ns/analysis/1.0");

        public XDocument ToXDocument()
        {
            var metricsEl = new XElement(Ns + "metric-deltas");
            foreach (var m in MetricDeltas ?? Enumerable.Empty<MetricDelta>())
                metricsEl.Add(new XElement(Ns + "metric",
                    new XAttribute("name",      m.Name ?? ""),
                    new XAttribute("from",      FormatNumber(m.From)),
                    new XAttribute("to",        FormatNumber(m.To)),
                    new XAttribute("change",    FormatChange(m.Change)),
                    new XAttribute("percent",   FormatChange(m.Percent)),
                    new XAttribute("direction", m.Direction ?? "")));

            var wordsEl = new XElement(Ns + "word-deltas");
            foreach (var w in WordDeltas ?? Enumerable.Empty<WordDelta>())
                wordsEl.Add(new XElement(Ns + "word",
                    new XAttribute("value",  w.Value ?? ""),
                    new XAttribute("from",   w.From),
                    new XAttribute("to",     w.To),
                    new XAttribute("change", FormatChange(w.Change)),
                    new XAttribute("status", w.Status ?? "")));

            var ngramsEl = new XElement(Ns + "ngram-deltas");
            foreach (var n in NgramDeltas ?? Enumerable.Empty<NgramDelta>())
                ngramsEl.Add(new XElement(Ns + "ngram",
                    new XAttribute("phrase", n.Phrase ?? ""),
                    new XAttribute("from",   n.From),
                    new XAttribute("to",     n.To),
                    new XAttribute("change", FormatChange(n.Change)),
                    new XAttribute("status", n.Status ?? "")));

            var chapsEl = new XElement(Ns + "chapter-deltas");
            foreach (var c in ChapterDeltas ?? Enumerable.Empty<ChapterDelta>())
                chapsEl.Add(new XElement(Ns + "chapter",
                    new XAttribute("id",           c.Id ?? ""),
                    new XAttribute("ttr-from",     c.TtrFrom.ToString("G6", CultureInfo.InvariantCulture)),
                    new XAttribute("ttr-to",       c.TtrTo.ToString("G6", CultureInfo.InvariantCulture)),
                    new XAttribute("outliers-from",c.OutliersFrom),
                    new XAttribute("outliers-to",  c.OutliersTo)));

            var root = new XElement(Ns + "book-analysis-delta",
                new XAttribute("schema-version", "2.0"),
                new XAttribute("generated-at",   GeneratedAt.ToString("o", CultureInfo.InvariantCulture)),
                new XAttribute("book-id",        BookId ?? ""),
                new XAttribute("draft-from",     DraftFrom),
                new XAttribute("draft-to",       DraftTo),
                metricsEl, wordsEl, ngramsEl, chapsEl);

            return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        }

        public string ToSummaryText()
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("DRAFT COMPARISON -- Draft {0} to Draft {1}", DraftFrom, DraftTo));
            sb.AppendLine();

            // Metrics
            foreach (var m in MetricDeltas ?? Enumerable.Empty<MetricDelta>())
            {
                string arrow = m.Direction == "improving" ? "UP IMPROVING"
                             : m.Direction == "regressing" ? "DOWN REGRESSING"
                             : "-- stable";
                sb.AppendLine(string.Format(
                    "{0}: {1} -> {2} ({3}{4}%) {5}",
                    m.Name,
                    FormatNumber(m.From),
                    FormatNumber(m.To),
                    m.Change >= 0 ? "+" : "",
                    m.Percent.ToString("G3", CultureInfo.InvariantCulture),
                    arrow));
            }

            AppendWordSection(sb, "RESOLVED (no longer flagged):", "resolved", WordDeltas);
            AppendWordSection(sb, "IMPROVED (reduced but still flagged):", "improved", WordDeltas);
            AppendWordSection(sb, "WORSENED:", "worsened", WordDeltas);
            AppendWordSection(sb, "NEW ISSUES (introduced by this revision):", "new", WordDeltas);
            AppendWordSection(sb, "UNCHANGED:", "unchanged", WordDeltas);

            AppendNgramSection(sb, "RESOLVED PHRASES:", "resolved", NgramDeltas);
            AppendNgramSection(sb, "IMPROVED PHRASES:", "improved", NgramDeltas);
            AppendNgramSection(sb, "WORSENED PHRASES:", "worsened", NgramDeltas);
            AppendNgramSection(sb, "NEW PHRASES:", "new", NgramDeltas);

            return sb.ToString();
        }

        private static void AppendWordSection(StringBuilder sb, string header, string status, List<WordDelta> words)
        {
            var items = (words ?? Enumerable.Empty<WordDelta>())
                .Where(w => w.Status == status)
                .ToList();
            if (items.Count == 0) return;
            sb.AppendLine();
            sb.AppendLine(header);
            foreach (var w in items)
            {
                if (status == "new")
                    sb.AppendLine(string.Format("- \"{0}\" -- {1} occurrences (not present in draft {2})", w.Value, w.To, 0));
                else if (status == "resolved")
                    sb.AppendLine(string.Format("- \"{0}\" -- was {1}, now below threshold", w.Value, w.From));
                else
                {
                    double pct = w.From > 0 ? ((double)w.Change / w.From) * 100.0 : 0;
                    sb.AppendLine(string.Format("- \"{0}\" -- was {1}, now {2} ({3}{4:0}%)",
                        w.Value, w.From, w.To, pct >= 0 ? "+" : "", pct));
                }
            }
        }

        private static void AppendNgramSection(StringBuilder sb, string header, string status, List<NgramDelta> ngrams)
        {
            var items = (ngrams ?? Enumerable.Empty<NgramDelta>())
                .Where(n => n.Status == status)
                .ToList();
            if (items.Count == 0) return;
            sb.AppendLine();
            sb.AppendLine(header);
            foreach (var n in items)
            {
                if (status == "new")
                    sb.AppendLine(string.Format("- \"{0}\" -- {1} occurrences", n.Phrase, n.To));
                else if (status == "resolved")
                    sb.AppendLine(string.Format("- \"{0}\" -- was {1}, now below threshold", n.Phrase, n.From));
                else
                {
                    double pct = n.From > 0 ? ((double)n.Change / n.From) * 100.0 : 0;
                    sb.AppendLine(string.Format("- \"{0}\" -- was {1}, now {2} ({3}{4:0}%)",
                        n.Phrase, n.From, n.To, pct >= 0 ? "+" : "", pct));
                }
            }
        }

        private static string FormatNumber(double v)
        {
            return v == Math.Floor(v) ? ((long)v).ToString(CultureInfo.InvariantCulture)
                                      : v.ToString("G6", CultureInfo.InvariantCulture);
        }

        private static string FormatChange(double v)
        {
            string s = FormatNumber(Math.Abs(v));
            return v >= 0 ? "+" + s : "-" + s;
        }
    }

    public class MetricDelta
    {
        public string Name      { get; set; }
        public double From      { get; set; }
        public double To        { get; set; }
        public double Change    { get; set; }
        public double Percent   { get; set; }
        public string Direction { get; set; }
    }

    public class WordDelta
    {
        public string Value  { get; set; }
        public int    From   { get; set; }
        public int    To     { get; set; }
        public int    Change { get; set; }
        public string Status { get; set; }
    }

    public class NgramDelta
    {
        public string Phrase { get; set; }
        public int    From   { get; set; }
        public int    To     { get; set; }
        public int    Change { get; set; }
        public string Status { get; set; }
    }

    public class ChapterDelta
    {
        public string Id           { get; set; }
        public double TtrFrom      { get; set; }
        public double TtrTo        { get; set; }
        public int    OutliersFrom { get; set; }
        public int    OutliersTo   { get; set; }
    }
}
