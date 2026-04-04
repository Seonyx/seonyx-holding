using System.Collections.Generic;

namespace ContentAnalysisEngine
{
    public class AnalysisConfiguration
    {
        /// <summary>Z-score threshold above which a word is flagged as an outlier. Default: 2.0.</summary>
        public double OutlierZScoreThreshold  { get; set; } = 2.0;

        /// <summary>Bigram occurrences per 10,000 words above which a bigram is flagged. Default: 5.</summary>
        public int    BigramThresholdPer10k   { get; set; } = 5;

        /// <summary>Trigram occurrences per 10,000 words above which a trigram is flagged. Default: 3.</summary>
        public int    TrigramThresholdPer10k  { get; set; } = 3;

        /// <summary>4-gram occurrences per 10,000 words above which a 4-gram is flagged. Default: 2.</summary>
        public int    FourgramThresholdPer10k { get; set; } = 2;

        /// <summary>Proximity echo window in words. Pairs closer than this are flagged. Default: 500.</summary>
        public int    EchoWindowWords         { get; set; } = 500;

        /// <summary>Additional stop words merged with the built-in list. Null means none.</summary>
        public HashSet<string> AdditionalStopWords { get; set; }

        /// <summary>Words that are always flagged regardless of Z-score. Null means none.</summary>
        public HashSet<string> AlwaysFlagWords { get; set; }
    }
}
