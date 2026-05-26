using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContentAnalysisEngine.Metrics;

namespace ContentAnalysisEngine
{
    public class ChapterAnalyser
    {
        private readonly AnalysisConfiguration _config;

        public ChapterAnalyser(AnalysisConfiguration config = null)
        {
            _config = config ?? new AnalysisConfiguration();
        }

        /// <summary>
        /// Analyses a BookML chapter XML file and returns a populated AnalysisReport.
        /// If namesFilePath is provided, character names in that file are added to the
        /// stop-word exclusion set so they are not flagged as outlier words or n-grams.
        /// </summary>
        public AnalysisReport Analyse(string chapterXmlPath, string namesFilePath = null)
        {
            if (!File.Exists(chapterXmlPath))
                throw new FileNotFoundException("Chapter XML file not found.", chapterXmlPath);

            var paragraphs = BookmlReader.ReadParagraphs(chapterXmlPath);
            var chapterId  = BookmlReader.ReadChapterId(chapterXmlPath);

            if (namesFilePath != null)
            {
                var names = NamesReader.ReadNames(namesFilePath);
                if (_config.AdditionalStopWords == null)
                    _config.AdditionalStopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var n in names)
                    _config.AdditionalStopWords.Add(n.ToLowerInvariant());
            }

            return AnalyseParagraphs(paragraphs, chapterId, Path.GetFullPath(chapterXmlPath));
        }

        /// <summary>
        /// Analyses a list of paragraphs sourced from the database and returns a populated AnalysisReport.
        /// </summary>
        public AnalysisReport Analyse(List<ParagraphEntry> paragraphs, string chapterId)
        {
            return AnalyseParagraphs(paragraphs, chapterId, "database");
        }

        private AnalysisReport AnalyseParagraphs(List<ParagraphEntry> paragraphs, string chapterId, string source)
        {
            // Build the flat token list for TTR (all tokens, no filtering)
            var allTokens = new List<string>();
            foreach (var para in paragraphs)
                allTokens.AddRange(Tokeniser.Tokenise(para.Text));

            int totalWords = allTokens.Count;

            // Metric 1: Word frequency
            Dictionary<string, int> wordCounts;
            var flaggedWords = WordFrequencyMetric.Analyse(paragraphs, _config, out wordCounts);

            // Metric 2: N-gram frequency
            var flaggedNgrams = NgramMetric.Analyse(paragraphs, totalWords, _config);

            // Metric 3: Proximity echoes
            var proximityEchoes = ProximityEchoMetric.Analyse(paragraphs, flaggedNgrams, _config);

            // Metric 4: TTR / MATTR
            double ttr, mattr;
            TtrMetric.Analyse(allTokens, out ttr, out mattr);

            // Metric 5: Hapax legomena
            var hapax = HapaxMetric.Analyse(wordCounts);

            int uniqueWords = allTokens
                .Select(t => t.ToLowerInvariant())
                .Distinct()
                .Count();

            return new AnalysisReport
            {
                SchemaVersion    = "1.0",
                GeneratedAt      = DateTime.UtcNow,
                ChapterId        = chapterId,
                SourceFile       = source,
                Configuration    = _config,
                Metrics          = new MetricsSummary
                {
                    TotalWords       = totalWords,
                    UniqueWords      = uniqueWords,
                    TypeTokenRatio   = ttr,
                    MovingAverageTTR = mattr,
                    HapaxCount       = hapax.HapaxCount,
                    HapaxRatio       = hapax.HapaxRatio
                },
                FlaggedWords     = flaggedWords,
                FlaggedNgrams    = flaggedNgrams,
                ProximityEchoes  = proximityEchoes
            };
        }
    }
}
