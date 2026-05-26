using System.Collections.Generic;

namespace ContentAnalysisEngine
{
    public static class Tokeniser
    {
        /// <summary>
        /// Splits text into lowercase tokens on any non-letter/non-digit character.
        /// Returns ALL tokens including single-char ones — callers decide what to filter.
        /// Empty strings from adjacent delimiters are discarded.
        /// </summary>
        public static List<string> Tokenise(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            var tokens = new List<string>();
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsLetterOrDigit(c))
                {
                    current.Append(char.ToLowerInvariant(c));
                }
                else if (c == '\'' || c == '\u2019') // straight or right-curly apostrophe
                {
                    // Keep as part of token only when flanked by letters on both sides
                    bool prevLetter = current.Length > 0;
                    bool nextLetter = (i + 1 < text.Length) && char.IsLetter(text[i + 1]);
                    if (prevLetter && nextLetter)
                        current.Append('\''); // normalise curly to straight
                    else
                    {
                        if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
                    }
                }
                else
                {
                    if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
                }
            }
            if (current.Length > 0)
                tokens.Add(current.ToString());

            return tokens;
        }

        /// <summary>
        /// Merges the built-in stop-word list with any additional stop words from config.
        /// </summary>
        public static HashSet<string> GetEffectiveStopWords(AnalysisConfiguration config)
        {
            var effective = new HashSet<string>(StopWords, System.StringComparer.OrdinalIgnoreCase);
            if (config != null && config.AdditionalStopWords != null)
            {
                foreach (var w in config.AdditionalStopWords)
                    effective.Add(w);
            }
            return effective;
        }

        /// <summary>
        /// NLTK English stop-word list (~179 words), hardcoded.
        /// </summary>
        public static readonly HashSet<string> StopWords = new HashSet<string>(
            System.StringComparer.OrdinalIgnoreCase)
        {
            "i", "me", "my", "myself", "we", "our", "ours", "ourselves",
            "you", "your", "yours", "yourself", "yourselves",
            "he", "him", "his", "himself",
            "she", "her", "hers", "herself",
            "it", "its", "itself",
            "they", "them", "their", "theirs", "themselves",
            "what", "which", "who", "whom",
            "this", "that", "these", "those",
            "am", "is", "are", "was", "were", "be", "been", "being",
            "have", "has", "had", "having",
            "do", "does", "did", "doing",
            "a", "an", "the",
            "and", "but", "if", "or", "because", "as", "until", "while",
            "of", "at", "by", "for", "with", "about", "against", "between",
            "into", "through", "during", "before", "after", "above", "below",
            "to", "from", "up", "down", "in", "out", "on", "off", "over",
            "under", "again", "further", "then", "once",
            "here", "there", "when", "where", "why", "how",
            "all", "both", "each", "few", "more", "most", "other", "some",
            "such", "no", "nor", "not", "only", "own", "same", "so", "than",
            "too", "very", "can", "will", "just", "should",
            "now", "d", "ll", "m", "o", "re", "ve", "y",
            "ain", "ma",
            // Contractions (tokeniser now preserves intra-word apostrophes)
            "don't", "doesn't", "didn't", "won't", "wouldn't", "couldn't", "shouldn't",
            "can't", "isn't", "wasn't", "aren't", "weren't", "hasn't", "haven't", "hadn't",
            "it's", "that's", "there's", "he's", "she's",
            "i'm", "i've", "i'd", "i'll",
            "we're", "we've", "we'd", "we'll",
            "they're", "they've", "they'd", "they'll",
            "you're", "you've", "you'd", "you'll",
            "let's", "who's", "what's",
            // Extended common function words
            "also", "back", "came", "come", "could", "every", "get", "go",
            "going", "got", "had", "has", "him", "his", "however", "into",
            "its", "just", "know", "like", "look", "made", "make", "may",
            "might", "much", "new", "old", "one", "said", "saw", "say",
            "see", "still", "take", "think", "though", "thought", "told",
            "two", "used", "way", "well", "went", "whether", "would",
            "yet", "your"
        };
    }
}
