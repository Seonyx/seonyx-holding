using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ContentAnalysisEngine.Metrics
{
    public static class NgramMetric
    {
        /// <summary>
        /// Generates and analyses n-grams (n=2,3,4) across all paragraphs.
        /// Uses the full token stream (no stop-word filtering) so phrases like
        /// "I said" and "at the" are captured. All-stop-word n-grams are then
        /// filtered out after generation.
        /// </summary>
        public static List<FlaggedNgram> Analyse(
            List<ParagraphEntry> paragraphs,
            int totalWords,
            AnalysisConfiguration config)
        {
            var stopWords = Tokeniser.GetEffectiveStopWords(config);

            // Build a flat list of (token, pid) pairs — one entry per token in order
            var tokenPids = new List<TokenPos>();
            foreach (var para in paragraphs)
            {
                var tokens = Tokeniser.Tokenise(para.Text);
                foreach (var token in tokens)
                    tokenPids.Add(new TokenPos { Token = token, Pid = para.Pid });
            }

            if (tokenPids.Count == 0)
                return new List<FlaggedNgram>();

            // n-gram phrase -> (count, set of PIDs)
            var ngrams = new Dictionary<string, NgramAccum>();

            for (int n = 2; n <= 4; n++)
            {
                for (int i = 0; i <= tokenPids.Count - n; i++)
                {
                    // Build the n-gram phrase from tokens i..i+n-1
                    var sb = new StringBuilder();
                    bool allStop = true;
                    for (int j = 0; j < n; j++)
                    {
                        if (j > 0) sb.Append(' ');
                        string tok = tokenPids[i + j].Token;
                        sb.Append(tok);
                        if (!stopWords.Contains(tok))
                            allStop = false;
                    }

                    // Skip n-grams composed entirely of stop words
                    if (allStop) continue;

                    string phrase = sb.ToString();
                    NgramAccum accum;
                    if (!ngrams.TryGetValue(phrase, out accum))
                    {
                        accum = new NgramAccum { N = n };
                        ngrams[phrase] = accum;
                    }
                    accum.Count++;
                    accum.Pids.Add(tokenPids[i].Pid);
                }
            }

            if (ngrams.Count == 0 || totalWords == 0)
                return new List<FlaggedNgram>();

            int bigramThreshold   = config != null ? config.BigramThresholdPer10k   : 5;
            int trigramThreshold  = config != null ? config.TrigramThresholdPer10k  : 3;
            int fourgramThreshold = config != null ? config.FourgramThresholdPer10k : 2;

            var flagged = new List<FlaggedNgram>();
            foreach (var kv in ngrams)
            {
                double rate = (kv.Value.Count / (double)totalWords) * 10000.0;

                int threshold;
                switch (kv.Value.N)
                {
                    case 2:  threshold = bigramThreshold;   break;
                    case 3:  threshold = trigramThreshold;  break;
                    default: threshold = fourgramThreshold; break;
                }

                if (rate < threshold) continue;

                flagged.Add(new FlaggedNgram
                {
                    Phrase          = kv.Key,
                    NgramSize       = kv.Value.N,
                    Count           = kv.Value.Count,
                    NormalisedRate  = System.Math.Round(rate, 4),
                    Pids            = kv.Value.Pids.Distinct().ToList()
                });
            }

            return flagged.OrderByDescending(f => f.NormalisedRate).ToList();
        }

        private class TokenPos
        {
            public string Token { get; set; }
            public string Pid   { get; set; }
        }

        private class NgramAccum
        {
            public int         N     { get; set; }
            public int         Count { get; set; }
            public List<string> Pids  { get; set; } = new List<string>();
        }
    }
}
