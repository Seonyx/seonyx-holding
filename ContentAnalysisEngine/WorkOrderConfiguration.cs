namespace ContentAnalysisEngine
{
    public class WorkOrderConfiguration
    {
        public string OutlierWordTemplate { get; set; } =
            "Replace occurrences of '{word}' with varied alternatives appropriate to the narrative context.";

        public string RepetitiveNgramTemplate { get; set; } =
            "Replace the repeated phrase '{phrase}' with varied alternatives. No two replacements should use the same wording.";

        public string ProximityEchoTemplate { get; set; } =
            "In PID {secondPid}, rephrase to avoid the word '{term}'. The occurrence in PID {firstPid} may remain.";

        /// <summary>
        /// Target maximum occurrences per chapter for outlier words after remediation.
        /// Used as the value of the max-per-chapter limit constraint.
        /// Note: the spec calls for max(2, expectedFrequency) but the mean word frequency
        /// is not persisted in the AnalysisReport, so this configurable default is used instead.
        /// </summary>
        public int DefaultMaxPerChapter { get; set; } = 5;

        /// <summary>
        /// When true, n-gram entries that contain a gendered pronoun (her/his/she/he)
        /// will include additional ban constraints for the obvious gender-swapped variants.
        /// This is a deliberate simple heuristic -- no NLP involved.
        /// </summary>
        public bool AutoBanVariants { get; set; } = true;
    }
}
