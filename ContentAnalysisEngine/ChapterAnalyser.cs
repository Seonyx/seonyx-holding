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
        /// </summary>
        public AnalysisReport Analyse(string chapterXmlPath)
        {
            if (!File.Exists(chapterXmlPath))
                throw new FileNotFoundException("Chapter XML file not found.", chapterXmlPath);

            var paragraphs = BookmlReader.ReadParagraphs(chapterXmlPath);
            var chapterId  = BookmlReader.ReadChapterId(chapterXmlPath);

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
                SchemaVersion   = "1.0",
                GeneratedAt     = DateTime.UtcNow,
                ChapterId       = chapterId,
                SourceFile      = Path.GetFullPath(chapterXmlPath),
                Configuration   = _config,
                Metrics         = new MetricsSummary
                {
                    TotalWords      = totalWords,
                    UniqueWords     = uniqueWords,
                    TypeTokenRatio  = ttr,
                    MovingAverageTTR = mattr,
                    HapaxCount      = hapax.HapaxCount,
                    HapaxRatio      = hapax.HapaxRatio
                },
                FlaggedWords    = flaggedWords,
                FlaggedNgrams   = flaggedNgrams,
                ProximityEchoes = proximityEchoes
            };
        }
    }
}
