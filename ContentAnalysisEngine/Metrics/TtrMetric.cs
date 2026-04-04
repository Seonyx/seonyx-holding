using System.Collections.Generic;
using System.Linq;

namespace ContentAnalysisEngine.Metrics
{
    public static class TtrMetric
    {
        private const int MattrWindowSize = 500;

        /// <summary>
        /// Computes Type-Token Ratio and Moving Average TTR (MATTR) for the token stream.
        /// Uses all tokens before stop-word filtering (TTR measures total vocabulary richness).
        /// </summary>
        public static void Analyse(List<string> allTokens, out double ttr, out double mattr)
        {
            if (allTokens == null || allTokens.Count == 0)
            {
                ttr   = 0.0;
                mattr = 0.0;
                return;
            }

            int total  = allTokens.Count;
            int unique = allTokens
                .Select(t => t.ToLowerInvariant())
                .Distinct()
                .Count();

            ttr = (double)unique / total;

            // MATTR: sliding window
            if (total <= MattrWindowSize)
            {
                // Chapter shorter than one window — TTR == MATTR
                mattr = ttr;
                return;
            }

            double windowTtrSum = 0.0;
            int    windowCount  = 0;

            for (int start = 0; start <= total - MattrWindowSize; start++)
            {
                var windowUnique = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                for (int i = start; i < start + MattrWindowSize; i++)
                    windowUnique.Add(allTokens[i]);

                windowTtrSum += (double)windowUnique.Count / MattrWindowSize;
                windowCount++;
            }

            mattr = windowCount > 0 ? System.Math.Round(windowTtrSum / windowCount, 6) : ttr;
            ttr   = System.Math.Round(ttr, 6);
        }
    }
}
