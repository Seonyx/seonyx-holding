using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace ContentAnalysisEngine
{
    public class BookAnalysisReport
    {
        public string   SchemaVersion  { get; set; } = "2.0";
        public DateTime GeneratedAt    { get; set; }
        public string   BookId         { get; set; }
        public int      TotalWords     { get; set; }
        public int      TotalChapters  { get; set; }
        public List<string>             NamesExcluded    { get; set; } = new List<string>();
        public BookMetricsSummary       Metrics          { get; set; }
        public List<BookFlaggedWord>    OutlierWords     { get; set; } = new List<BookFlaggedWord>();
        public List<BookFlaggedNgram>   OutlierNgrams    { get; set; } = new List<BookFlaggedNgram>();
        public List<BookChapterSummary> ChapterSummaries { get; set; } = new List<BookChapterSummary>();

        private static readonly XNamespace Ns = XNamespace.Get("https://bookml.org/ns/analysis/1.0");

        public XDocument ToXDocument()
        {
            var m = Metrics ?? new BookMetricsSummary();

            var namesEl = new XElement(Ns + "names-excluded");
            foreach (var n in NamesExcluded ?? Enumerable.Empty<string>())
                namesEl.Add(new XElement(Ns + "name", new XAttribute("value", n)));

            var metricsEl = new XElement(Ns + "book-metrics",
                new XAttribute("unique-words",           m.UniqueWords),
                new XAttribute("type-token-ratio",       m.TypeTokenRatio.ToString("G6", CultureInfo.InvariantCulture)),
                new XAttribute("moving-average-ttr",     m.MovingAverageTTR.ToString("G6", CultureInfo.InvariantCulture)),
                new XAttribute("hapax-count",            m.HapaxCount),
                new XAttribute("hapax-ratio",            m.HapaxRatio.ToString("G6", CultureInfo.InvariantCulture)),
                new XAttribute("mean-sentence-length",   m.MeanSentenceLength.ToString("G4", CultureInfo.InvariantCulture)),
                new XAttribute("sentence-length-std-dev", m.SentenceLengthStdDev.ToString("G4", CultureInfo.InvariantCulture)));

            var outliersEl = new XElement(Ns + "book-wide-outliers");
            foreach (var w in OutlierWords ?? Enumerable.Empty<BookFlaggedWord>())
                outliersEl.Add(new XElement(Ns + "word",
                    new XAttribute("value",            w.Value ?? ""),
                    new XAttribute("count",            w.Count),
                    new XAttribute("z-score",          w.ZScore.ToString("G6", CultureInfo.InvariantCulture)),
                    new XAttribute("chapters-present", w.ChaptersPresent),
                    new XAttribute("distribution",     w.Distribution ?? "")));

            var ngramsEl = new XElement(Ns + "book-wide-ngrams");
            foreach (var ng in OutlierNgrams ?? Enumerable.Empty<BookFlaggedNgram>())
                ngramsEl.Add(new XElement(Ns + "ngram",
                    new XAttribute("phrase",           ng.Phrase ?? ""),
                    new XAttribute("count",            ng.Count),
                    new XAttribute("chapters-present", ng.ChaptersPresent),
                    new XAttribute("distribution",     ng.Distribution ?? "")));

            var chapsEl = new XElement(Ns + "chapter-summaries");
            foreach (var ch in ChapterSummaries ?? Enumerable.Empty<BookChapterSummary>())
                chapsEl.Add(new XElement(Ns + "chapter",
                    new XAttribute("id",            ch.Id ?? ""),
                    new XAttribute("words",         ch.Words),
                    new XAttribute("ttr",           ch.Ttr.ToString("G6", CultureInfo.InvariantCulture)),
                    new XAttribute("hapax-ratio",   ch.HapaxRatio.ToString("G6", CultureInfo.InvariantCulture)),
                    new XAttribute("outlier-count", ch.OutlierCount),
                    new XAttribute("ngram-count",   ch.NgramCount),
                    new XAttribute("echo-count",    ch.EchoCount)));

            var root = new XElement(Ns + "book-analysis-report",
                new XAttribute("schema-version",  SchemaVersion ?? "2.0"),
                new XAttribute("generated-at",    GeneratedAt.ToString("o", CultureInfo.InvariantCulture)),
                new XAttribute("book-id",         BookId ?? ""),
                new XAttribute("total-words",     TotalWords),
                new XAttribute("total-chapters",  TotalChapters),
                namesEl,
                metricsEl,
                outliersEl,
                ngramsEl,
                chapsEl);

            return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        }

        public static BookAnalysisReport FromXDocument(XDocument doc)
        {
            var root = doc.Root;
            if (root == null) throw new ArgumentException("Empty document.");

            var report = new BookAnalysisReport
            {
                SchemaVersion = (string)root.Attribute("schema-version") ?? "2.0",
                BookId        = (string)root.Attribute("book-id") ?? "",
                TotalWords    = ParseInt((string)root.Attribute("total-words"), 0),
                TotalChapters = ParseInt((string)root.Attribute("total-chapters"), 0),
            };

            var genAt = (string)root.Attribute("generated-at");
            if (!string.IsNullOrEmpty(genAt))
                report.GeneratedAt = DateTime.Parse(genAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

            // Names excluded
            var namesEl = root.Element(Ns + "names-excluded");
            if (namesEl != null)
                report.NamesExcluded = namesEl.Elements(Ns + "name")
                    .Select(e => (string)e.Attribute("value"))
                    .Where(v => v != null)
                    .ToList();

            // Metrics
            var mEl = root.Element(Ns + "book-metrics");
            if (mEl != null)
            {
                report.Metrics = new BookMetricsSummary
                {
                    UniqueWords          = ParseInt((string)mEl.Attribute("unique-words"), 0),
                    TypeTokenRatio       = ParseDouble((string)mEl.Attribute("type-token-ratio"), 0),
                    MovingAverageTTR     = ParseDouble((string)mEl.Attribute("moving-average-ttr"), 0),
                    HapaxCount           = ParseInt((string)mEl.Attribute("hapax-count"), 0),
                    HapaxRatio           = ParseDouble((string)mEl.Attribute("hapax-ratio"), 0),
                    MeanSentenceLength   = ParseDouble((string)mEl.Attribute("mean-sentence-length"), 0),
                    SentenceLengthStdDev = ParseDouble((string)mEl.Attribute("sentence-length-std-dev"), 0),
                };
            }

            // Outlier words
            var outliersEl = root.Element(Ns + "book-wide-outliers");
            if (outliersEl != null)
            {
                report.OutlierWords = outliersEl.Elements(Ns + "word").Select(e => new BookFlaggedWord
                {
                    Value           = (string)e.Attribute("value") ?? "",
                    Count           = ParseInt((string)e.Attribute("count"), 0),
                    ZScore          = ParseDouble((string)e.Attribute("z-score"), 0),
                    ChaptersPresent = ParseInt((string)e.Attribute("chapters-present"), 0),
                    Distribution    = (string)e.Attribute("distribution") ?? ""
                }).ToList();
            }

            // Outlier n-grams
            var ngramsEl = root.Element(Ns + "book-wide-ngrams");
            if (ngramsEl != null)
            {
                report.OutlierNgrams = ngramsEl.Elements(Ns + "ngram").Select(e => new BookFlaggedNgram
                {
                    Phrase          = (string)e.Attribute("phrase") ?? "",
                    Count           = ParseInt((string)e.Attribute("count"), 0),
                    ChaptersPresent = ParseInt((string)e.Attribute("chapters-present"), 0),
                    Distribution    = (string)e.Attribute("distribution") ?? ""
                }).ToList();
            }

            // Chapter summaries
            var chapsEl = root.Element(Ns + "chapter-summaries");
            if (chapsEl != null)
            {
                report.ChapterSummaries = chapsEl.Elements(Ns + "chapter").Select(e => new BookChapterSummary
                {
                    Id           = (string)e.Attribute("id") ?? "",
                    Words        = ParseInt((string)e.Attribute("words"), 0),
                    Ttr          = ParseDouble((string)e.Attribute("ttr"), 0),
                    HapaxRatio   = ParseDouble((string)e.Attribute("hapax-ratio"), 0),
                    OutlierCount = ParseInt((string)e.Attribute("outlier-count"), 0),
                    NgramCount   = ParseInt((string)e.Attribute("ngram-count"), 0),
                    EchoCount    = ParseInt((string)e.Attribute("echo-count"), 0),
                }).ToList();
            }

            return report;
        }

        private static int ParseInt(string value, int defaultValue)
        {
            int result;
            return (value != null && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                ? result : defaultValue;
        }

        private static double ParseDouble(string value, double defaultValue)
        {
            double result;
            return (value != null && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                ? result : defaultValue;
        }
    }

    public class BookMetricsSummary
    {
        public int    UniqueWords          { get; set; }
        public double TypeTokenRatio       { get; set; }
        public double MovingAverageTTR     { get; set; }
        public int    HapaxCount           { get; set; }
        public double HapaxRatio           { get; set; }
        public double MeanSentenceLength   { get; set; }
        public double SentenceLengthStdDev { get; set; }
    }

    public class BookFlaggedWord
    {
        public string Value           { get; set; }
        public int    Count           { get; set; }
        public double ZScore          { get; set; }
        public int    ChaptersPresent { get; set; }
        public string Distribution    { get; set; }
    }

    public class BookFlaggedNgram
    {
        public string Phrase          { get; set; }
        public int    Count           { get; set; }
        public int    ChaptersPresent { get; set; }
        public string Distribution    { get; set; }
    }

    public class BookChapterSummary
    {
        public string Id           { get; set; }
        public int    Words        { get; set; }
        public double Ttr          { get; set; }
        public double HapaxRatio   { get; set; }
        public int    OutlierCount { get; set; }
        public int    NgramCount   { get; set; }
        public int    EchoCount    { get; set; }
    }
}
