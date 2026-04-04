using System.Collections.Generic;
using System.Linq;

namespace ContentAnalysisEngine.Metrics
{
    public static class HapaxMetric
    {
        /// <summary>
        /// Counts hapax legomena (words appearing exactly once) from the post-stop-word
        /// word frequency dictionary.
        /// </summary>
        public static HapaxResult Analyse(Dictionary<string, int> wordCounts)
        {
            if (wordCounts == null || wordCounts.Count == 0)
            {
                return new HapaxResult
                {
                    HapaxCount = 0,
                    HapaxRatio = 0.0,
                    HapaxWords = new List<string>()
                };
            }

            var hapaxWords = wordCounts
                .Where(kv => kv.Value == 1)
                .Select(kv => kv.Key)
                .OrderBy(w => w)
                .ToList();

            int uniqueTypes = wordCounts.Count;
            double ratio    = uniqueTypes > 0 ? (double)hapaxWords.Count / uniqueTypes : 0.0;

            return new HapaxResult
            {
                HapaxCount = hapaxWords.Count,
                HapaxRatio = System.Math.Round(ratio, 6),
                HapaxWords = hapaxWords
            };
        }
    }
}
