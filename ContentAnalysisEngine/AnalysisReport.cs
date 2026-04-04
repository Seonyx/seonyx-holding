using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace ContentAnalysisEngine
{
    public class AnalysisReport
    {
        public string SchemaVersion { get; set; } = "1.0";
        public DateTime GeneratedAt { get; set; }
        public string ChapterId { get; set; }
        public string SourceFile { get; set; }
        public AnalysisConfiguration Configuration { get; set; }
        public MetricsSummary Metrics { get; set; }
        public List<FlaggedWord> FlaggedWords { get; set; }
        public List<FlaggedNgram> FlaggedNgrams { get; set; }
        public List<ProximityEcho> ProximityEchoes { get; set; }

        private static readonly XNamespace Ns = XNamespace.Get("https://bookml.org/ns/analysis/1.0");

        public XDocument ToXDocument()
        {
            var cfg = Configuration ?? new AnalysisConfiguration();
            var m   = Metrics ?? new MetricsSummary();

            var cfgEl = new XElement(Ns + "configuration",
                new XAttribute("outlier-z-score-threshold",   cfg.OutlierZScoreThreshold.ToString("G", CultureInfo.InvariantCulture)),
                new XAttribute("bigram-threshold-per-10k",    cfg.BigramThresholdPer10k),
                new XAttribute("trigram-threshold-per-10k",   cfg.TrigramThresholdPer10k),
                new XAttribute("fourgram-threshold-per-10k",  cfg.FourgramThresholdPer10k),
                new XAttribute("echo-window-words",           cfg.EchoWindowWords));

            if (cfg.AdditionalStopWords != null)
                foreach (var w in cfg.AdditionalStopWords.OrderBy(x => x))
                    cfgEl.Add(new XElement(Ns + "stop-word", new XAttribute("value", w)));

            if (cfg.AlwaysFlagWords != null)
                foreach (var w in cfg.AlwaysFlagWords.OrderBy(x => x))
                    cfgEl.Add(new XElement(Ns + "always-flag", new XAttribute("value", w)));

            var metricsEl = new XElement(Ns + "metrics",
                new XAttribute("total-words",        m.TotalWords),
                new XAttribute("unique-words",       m.UniqueWords),
                new XAttribute("type-token-ratio",   m.TypeTokenRatio.ToString("G6", CultureInfo.InvariantCulture)),
                new XAttribute("moving-average-ttr", m.MovingAverageTTR.ToString("G6", CultureInfo.InvariantCulture)),
                new XAttribute("hapax-count",        m.HapaxCount),
                new XAttribute("hapax-ratio",        m.HapaxRatio.ToString("G6", CultureInfo.InvariantCulture)));

            var wordsEl = new XElement(Ns + "flagged-words");
            foreach (var fw in FlaggedWords ?? Enumerable.Empty<FlaggedWord>())
            {
                var el = new XElement(Ns + "word",
                    new XAttribute("value",   fw.Word ?? ""),
                    new XAttribute("count",   fw.Count),
                    new XAttribute("z-score", fw.ZScore.ToString("G6", CultureInfo.InvariantCulture)));
                foreach (var pc in fw.Pids ?? Enumerable.Empty<PidCount>())
                    el.Add(new XElement(Ns + "pid",
                        new XAttribute("ref",   pc.Pid ?? ""),
                        new XAttribute("count", pc.Count)));
                wordsEl.Add(el);
            }

            var ngramsEl = new XElement(Ns + "flagged-ngrams");
            foreach (var fn in FlaggedNgrams ?? Enumerable.Empty<FlaggedNgram>())
            {
                var el = new XElement(Ns + "ngram",
                    new XAttribute("phrase",           fn.Phrase ?? ""),
                    new XAttribute("size",             fn.NgramSize),
                    new XAttribute("count",            fn.Count),
                    new XAttribute("normalised-rate",  fn.NormalisedRate.ToString("G6", CultureInfo.InvariantCulture)));
                foreach (var pid in fn.Pids ?? Enumerable.Empty<string>())
                    el.Add(new XElement(Ns + "pid", new XAttribute("ref", pid ?? "")));
                ngramsEl.Add(el);
            }

            var echoesEl = new XElement(Ns + "proximity-echoes");
            foreach (var pe in ProximityEchoes ?? Enumerable.Empty<ProximityEcho>())
            {
                echoesEl.Add(new XElement(Ns + "echo",
                    new XAttribute("term",           pe.Term ?? ""),
                    new XAttribute("pid-a",          pe.PidA ?? ""),
                    new XAttribute("pid-b",          pe.PidB ?? ""),
                    new XAttribute("distance-words", pe.DistanceWords),
                    new XAttribute("severity",       pe.Severity ?? "")));
            }

            var root = new XElement(Ns + "analysis-report",
                new XAttribute("schema-version", SchemaVersion ?? "1.0"),
                new XAttribute("generated-at",   GeneratedAt.ToString("o", CultureInfo.InvariantCulture)),
                new XAttribute("chapter-id",     ChapterId ?? ""),
                new XAttribute("source-file",    SourceFile ?? ""),
                cfgEl,
                metricsEl,
                wordsEl,
                ngramsEl,
                echoesEl);

            return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        }

        public static AnalysisReport FromXDocument(XDocument doc)
        {
            var root = doc.Root;
            if (root == null) throw new ArgumentException("Empty document.");

            var report = new AnalysisReport
            {
                SchemaVersion = (string)root.Attribute("schema-version") ?? "1.0",
                ChapterId     = (string)root.Attribute("chapter-id") ?? "",
                SourceFile    = (string)root.Attribute("source-file") ?? "",
            };

            var genAt = (string)root.Attribute("generated-at");
            if (!string.IsNullOrEmpty(genAt))
                report.GeneratedAt = DateTime.Parse(genAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

            // Configuration
            var cfgEl = root.Element(Ns + "configuration");
            if (cfgEl != null)
            {
                var cfg = new AnalysisConfiguration();
                cfg.OutlierZScoreThreshold  = ParseDouble((string)cfgEl.Attribute("outlier-z-score-threshold"), 2.0);
                cfg.BigramThresholdPer10k   = ParseInt((string)cfgEl.Attribute("bigram-threshold-per-10k"), 5);
                cfg.TrigramThresholdPer10k  = ParseInt((string)cfgEl.Attribute("trigram-threshold-per-10k"), 3);
                cfg.FourgramThresholdPer10k = ParseInt((string)cfgEl.Attribute("fourgram-threshold-per-10k"), 2);
                cfg.EchoWindowWords         = ParseInt((string)cfgEl.Attribute("echo-window-words"), 500);

                var stopWords = cfgEl.Elements(Ns + "stop-word").Select(e => (string)e.Attribute("value")).Where(v => v != null).ToList();
                if (stopWords.Count > 0) cfg.AdditionalStopWords = new HashSet<string>(stopWords);

                var flagWords = cfgEl.Elements(Ns + "always-flag").Select(e => (string)e.Attribute("value")).Where(v => v != null).ToList();
                if (flagWords.Count > 0) cfg.AlwaysFlagWords = new HashSet<string>(flagWords);

                report.Configuration = cfg;
            }

            // Metrics
            var mEl = root.Element(Ns + "metrics");
            if (mEl != null)
            {
                report.Metrics = new MetricsSummary
                {
                    TotalWords       = ParseInt((string)mEl.Attribute("total-words"), 0),
                    UniqueWords      = ParseInt((string)mEl.Attribute("unique-words"), 0),
                    TypeTokenRatio   = ParseDouble((string)mEl.Attribute("type-token-ratio"), 0),
                    MovingAverageTTR = ParseDouble((string)mEl.Attribute("moving-average-ttr"), 0),
                    HapaxCount       = ParseInt((string)mEl.Attribute("hapax-count"), 0),
                    HapaxRatio       = ParseDouble((string)mEl.Attribute("hapax-ratio"), 0),
                };
            }

            // Flagged words
            report.FlaggedWords = new List<FlaggedWord>();
            var wordsEl = root.Element(Ns + "flagged-words");
            if (wordsEl != null)
            {
                foreach (var el in wordsEl.Elements(Ns + "word"))
                {
                    var fw = new FlaggedWord
                    {
                        Word   = (string)el.Attribute("value") ?? "",
                        Count  = ParseInt((string)el.Attribute("count"), 0),
                        ZScore = ParseDouble((string)el.Attribute("z-score"), 0),
                        Pids   = el.Elements(Ns + "pid").Select(p => new PidCount
                        {
                            Pid   = (string)p.Attribute("ref") ?? "",
                            Count = ParseInt((string)p.Attribute("count"), 0)
                        }).ToList()
                    };
                    report.FlaggedWords.Add(fw);
                }
            }

            // Flagged n-grams
            report.FlaggedNgrams = new List<FlaggedNgram>();
            var ngramsEl = root.Element(Ns + "flagged-ngrams");
            if (ngramsEl != null)
            {
                foreach (var el in ngramsEl.Elements(Ns + "ngram"))
                {
                    var fn = new FlaggedNgram
                    {
                        Phrase         = (string)el.Attribute("phrase") ?? "",
                        NgramSize      = ParseInt((string)el.Attribute("size"), 2),
                        Count          = ParseInt((string)el.Attribute("count"), 0),
                        NormalisedRate = ParseDouble((string)el.Attribute("normalised-rate"), 0),
                        Pids           = el.Elements(Ns + "pid").Select(p => (string)p.Attribute("ref") ?? "").ToList()
                    };
                    report.FlaggedNgrams.Add(fn);
                }
            }

            // Proximity echoes
            report.ProximityEchoes = new List<ProximityEcho>();
            var echoesEl = root.Element(Ns + "proximity-echoes");
            if (echoesEl != null)
            {
                foreach (var el in echoesEl.Elements(Ns + "echo"))
                {
                    report.ProximityEchoes.Add(new ProximityEcho
                    {
                        Term          = (string)el.Attribute("term") ?? "",
                        PidA          = (string)el.Attribute("pid-a") ?? "",
                        PidB          = (string)el.Attribute("pid-b") ?? "",
                        DistanceWords = ParseInt((string)el.Attribute("distance-words"), 0),
                        Severity      = (string)el.Attribute("severity") ?? ""
                    });
                }
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

    public class MetricsSummary
    {
        public int    TotalWords       { get; set; }
        public int    UniqueWords      { get; set; }
        public double TypeTokenRatio   { get; set; }
        public double MovingAverageTTR { get; set; }
        public int    HapaxCount       { get; set; }
        public double HapaxRatio       { get; set; }
    }

    public class FlaggedWord
    {
        public string        Word   { get; set; }
        public int           Count  { get; set; }
        public double        ZScore { get; set; }
        public List<PidCount> Pids  { get; set; }
    }

    public class PidCount
    {
        public string Pid   { get; set; }
        public int    Count { get; set; }
    }

    public class FlaggedNgram
    {
        public string       Phrase         { get; set; }
        public int          NgramSize      { get; set; }
        public int          Count          { get; set; }
        public double       NormalisedRate { get; set; }
        public List<string> Pids           { get; set; }
    }

    public class ProximityEcho
    {
        public string Term          { get; set; }
        public string PidA          { get; set; }
        public string PidB          { get; set; }
        public int    DistanceWords { get; set; }
        public string Severity      { get; set; }
    }

    public class HapaxResult
    {
        public int          HapaxCount { get; set; }
        public double       HapaxRatio { get; set; }
        public List<string> HapaxWords { get; set; }
    }
}
