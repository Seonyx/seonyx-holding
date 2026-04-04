using System;
using System.Collections.Generic;
using System.Linq;

namespace ContentAnalysisEngine.Metrics
{
    public static class WordFrequencyMetric
    {
        /// <summary>
        /// Analyses word frequency across paragraphs, flags statistical outliers.
        /// Outputs the word-count dictionary (post stop-word filter) for reuse by HapaxMetric.
        /// </summary>
        public static List<FlaggedWord> Analyse(
            List<ParagraphEntry> paragraphs,
            AnalysisConfiguration config,
            out Dictionary<string, int> wordCounts)
        {
            var stopWords  = Tokeniser.GetEffectiveStopWords(config);
            var alwaysFlag = config != null && config.AlwaysFlagWords != null
                ? config.AlwaysFlagWords
                : new HashSet<string>();

            // word -> pid -> count
            var pidCounts = new Dictionary<string, Dictionary<string, int>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var para in paragraphs)
            {
                var tokens = Tokeniser.Tokenise(para.Text);
                foreach (var token in tokens)
                {
                    // Skip stop words and punctuation remnants (length < 2)
                    if (token.Length < 2) continue;
                    if (stopWords.Contains(token)) continue;

                    Dictionary<string, int> perPid;
                    if (!pidCounts.TryGetValue(token, out perPid))
                    {
                        perPid = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        pidCounts[token] = perPid;
                    }

                    int existing;
                    perPid[para.Pid] = perPid.TryGetValue(para.Pid, out existing) ? existing + 1 : 1;
                }
            }

            // Build total count per word
            wordCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in pidCounts)
                wordCounts[kv.Key] = kv.Value.Values.Sum();

            if (wordCounts.Count == 0)
                return new List<FlaggedWord>();

            // Compute mean and standard deviation
            var counts = wordCounts.Values.Select(c => (double)c).ToList();
            double mean = counts.Average();
            double variance = counts.Select(c => (c - mean) * (c - mean)).Average();
            double stddev = Math.Sqrt(variance);

            double threshold = config != null ? config.OutlierZScoreThreshold : 2.0;

            var flagged = new List<FlaggedWord>();
            foreach (var kv in wordCounts)
            {
                double zScore = stddev > 0 ? (kv.Value - mean) / stddev : 0;
                bool isOutlier = zScore > threshold;
                bool isAlwaysFlag = alwaysFlag.Contains(kv.Key);

                if (!isOutlier && !isAlwaysFlag)
                    continue;

                var pidList = pidCounts[kv.Key]
                    .OrderByDescending(p => p.Value)
                    .Select(p => new PidCount { Pid = p.Key, Count = p.Value })
                    .ToList();

                flagged.Add(new FlaggedWord
                {
                    Word   = kv.Key,
                    Count  = kv.Value,
                    ZScore = Math.Round(zScore, 4),
                    Pids   = pidList
                });
            }

            return flagged.OrderByDescending(f => f.ZScore).ToList();
        }
    }
}
