using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ContentAnalysisEngine.Metrics
{
    public static class ProximityEchoMetric
    {
        /// <summary>
        /// Detects words and flagged n-grams that repeat within a configurable word-distance window.
        /// </summary>
        public static List<ProximityEcho> Analyse(
            List<ParagraphEntry> paragraphs,
            List<FlaggedNgram> flaggedNgrams,
            AnalysisConfiguration config)
        {
            var stopWords  = Tokeniser.GetEffectiveStopWords(config);
            int window     = config != null ? config.EchoWindowWords : 500;

            // Build flat token list: (token, pid, globalIndex)
            var tokenList = new List<TokenPosition>();
            int idx = 0;
            foreach (var para in paragraphs)
            {
                var tokens = Tokeniser.Tokenise(para.Text);
                foreach (var token in tokens)
                {
                    tokenList.Add(new TokenPosition
                    {
                        Token       = token,
                        Pid         = para.Pid,
                        GlobalIndex = idx++
                    });
                }
            }

            if (tokenList.Count == 0)
                return new List<ProximityEcho>();

            var echoes = new List<ProximityEcho>();

            // --- Single-word proximity echoes ---
            // Group non-stop words by token
            var byToken = tokenList
                .Where(t => !stopWords.Contains(t.Token) && t.Token.Length >= 2)
                .GroupBy(t => t.Token)
                .Where(g => g.Count() >= 2);

            foreach (var group in byToken)
            {
                var positions = group.OrderBy(t => t.GlobalIndex).ToList();
                for (int i = 0; i < positions.Count - 1; i++)
                {
                    int distance = positions[i + 1].GlobalIndex - positions[i].GlobalIndex;
                    if (distance < window)
                    {
                        echoes.Add(new ProximityEcho
                        {
                            Term          = group.Key,
                            PidA          = positions[i].Pid,
                            PidB          = positions[i + 1].Pid,
                            DistanceWords = distance,
                            Severity      = GetSeverity(distance)
                        });
                    }
                }
            }

            // --- N-gram proximity echoes ---
            if (flaggedNgrams != null && flaggedNgrams.Count > 0)
            {
                // For each flagged n-gram, find all positions in the token list
                foreach (var ngram in flaggedNgrams)
                {
                    var ngramTokens = ngram.Phrase.Split(' ');
                    int n = ngramTokens.Length;
                    var positions = new List<TokenPosition>();

                    for (int i = 0; i <= tokenList.Count - n; i++)
                    {
                        bool match = true;
                        for (int j = 0; j < n; j++)
                        {
                            if (tokenList[i + j].Token != ngramTokens[j])
                            {
                                match = false;
                                break;
                            }
                        }
                        if (match)
                            positions.Add(tokenList[i]);
                    }

                    for (int i = 0; i < positions.Count - 1; i++)
                    {
                        int distance = positions[i + 1].GlobalIndex - positions[i].GlobalIndex;
                        if (distance < window)
                        {
                            echoes.Add(new ProximityEcho
                            {
                                Term          = ngram.Phrase,
                                PidA          = positions[i].Pid,
                                PidB          = positions[i + 1].Pid,
                                DistanceWords = distance,
                                Severity      = GetSeverity(distance)
                            });
                        }
                    }
                }
            }

            return echoes.OrderBy(e => e.DistanceWords).ToList();
        }

        private static string GetSeverity(int distance)
        {
            if (distance < 100)  return "high";
            if (distance < 300)  return "medium";
            return "low";
        }

        private class TokenPosition
        {
            public string Token       { get; set; }
            public string Pid         { get; set; }
            public int    GlobalIndex { get; set; }
        }
    }
}
