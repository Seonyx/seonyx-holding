using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace ContentAnalysisEngine
{
    /// <summary>
    /// Translates a Phase 1 AnalysisReport into a bookml-workorder XML manifest.
    /// Each flagged item becomes one work order entry with severity-ranked ordering.
    ///
    /// Deduplication note: a word can appear as both an outlier-word entry AND in
    /// multiple proximity-echo entries. This is intentional -- do not deduplicate.
    /// The outlier entry addresses global overuse; the echo entries address specific
    /// close-proximity pairs. Both are valid, independent work items.
    /// </summary>
    public class WorkOrderGenerator
    {
        private static readonly XNamespace Ns = XNamespace.Get("https://bookml.org/ns/workorder/1.0");

        private readonly WorkOrderConfiguration _config;

        public WorkOrderGenerator(WorkOrderConfiguration config = null)
        {
            _config = config ?? new WorkOrderConfiguration();
        }

        public XDocument Generate(AnalysisReport report, string chapterId, int draftNumber)
        {
            if (report == null) throw new ArgumentNullException("report");

            var flaggedWords   = report.FlaggedWords   ?? new List<FlaggedWord>();
            var flaggedNgrams  = report.FlaggedNgrams  ?? new List<FlaggedNgram>();
            var proximityEchoes = report.ProximityEchoes ?? new List<ProximityEcho>();

            // --- Pre-compute normalisation values for severity formulas ---

            // totalPidCount: distinct PIDs across all flagged items (min 1 to avoid /0).
            // This is an approximation when the report has few flags, but it is the best
            // available proxy without adding a paragraph count field to AnalysisReport.
            var allPids = new HashSet<string>();
            foreach (var fw in flaggedWords)
                if (fw.Pids != null)
                    foreach (var pc in fw.Pids) allPids.Add(pc.Pid);
            foreach (var fn in flaggedNgrams)
                if (fn.Pids != null)
                    foreach (var pid in fn.Pids) allPids.Add(pid);
            foreach (var pe in proximityEchoes)
            {
                if (!string.IsNullOrEmpty(pe.PidA)) allPids.Add(pe.PidA);
                if (!string.IsNullOrEmpty(pe.PidB)) allPids.Add(pe.PidB);
            }
            double totalPidCount = Math.Max(1, allPids.Count);

            double maxZScore = flaggedWords.Count > 0
                ? flaggedWords.Max(w => w.ZScore)
                : 1.0;
            if (maxZScore <= 0) maxZScore = 1.0;

            double maxNormalisedRate = flaggedNgrams.Count > 0
                ? flaggedNgrams.Max(n => n.NormalisedRate)
                : 1.0;
            if (maxNormalisedRate <= 0) maxNormalisedRate = 1.0;

            // --- Build entries ---

            var entries = new List<WorkOrderEntry>();

            foreach (var fw in flaggedWords)
            {
                double pidCount  = fw.Pids != null ? fw.Pids.Count : 1;
                double severity  = Math.Min(100.0,
                    (fw.ZScore / maxZScore) * 50.0
                    + (pidCount / totalPidCount) * 30.0
                    + 20.0);

                var instruction = _config.OutlierWordTemplate
                    .Replace("{word}", fw.Word ?? "");

                var constraints = new List<XElement>
                {
                    new XElement(Ns + "ban",
                        new XAttribute("type", "word"),
                        fw.Word ?? ""),
                    new XElement(Ns + "limit",
                        new XAttribute("type", "max-per-chapter"),
                        new XAttribute("value", Math.Max(2, _config.DefaultMaxPerChapter))),
                    new XElement(Ns + "instruction", instruction)
                };

                var targets = BuildWordTargets(fw);

                var description = string.Format(CultureInfo.InvariantCulture,
                    "Word appears {0} times (Z-score: {1:G4}). Target maximum: {2} occurrences per chapter.",
                    fw.Count, fw.ZScore, Math.Max(2, _config.DefaultMaxPerChapter));

                entries.Add(new WorkOrderEntry
                {
                    Type        = "outlier-word",
                    Severity    = severity,
                    Subject     = fw.Word ?? "",
                    Description = description,
                    Targets     = targets,
                    Constraints = constraints
                });
            }

            foreach (var fn in flaggedNgrams)
            {
                double pidCount = fn.Pids != null ? fn.Pids.Count : 1;
                double severity = Math.Min(100.0,
                    (fn.NormalisedRate / maxNormalisedRate) * 50.0
                    + (pidCount / totalPidCount) * 30.0
                    + 20.0);

                var instruction = _config.RepetitiveNgramTemplate
                    .Replace("{phrase}", fn.Phrase ?? "");

                var constraints = new List<XElement>();
                constraints.Add(new XElement(Ns + "ban",
                    new XAttribute("type", "phrase"),
                    fn.Phrase ?? ""));

                if (_config.AutoBanVariants)
                {
                    var variants = GetPronounVariants(fn.Phrase ?? "");
                    foreach (var v in variants)
                        constraints.Add(new XElement(Ns + "ban",
                            new XAttribute("type", "phrase"),
                            v));
                }

                constraints.Add(new XElement(Ns + "instruction", instruction));

                var targets = BuildNgramTargets(fn);

                var description = string.Format(CultureInfo.InvariantCulture,
                    "Phrase appears {0} times (rate: {1:G4} per 10k words). Threshold: {2} per 10k words.",
                    fn.Count, fn.NormalisedRate, NgramThresholdLabel(fn.NgramSize));

                entries.Add(new WorkOrderEntry
                {
                    Type        = "repetitive-ngram",
                    Severity    = severity,
                    Count       = fn.Count,
                    Subject     = fn.Phrase ?? "",
                    Description = description,
                    Targets     = targets,
                    Constraints = constraints
                });
            }

            foreach (var pe in proximityEchoes)
            {
                int severityBase = EchoSeverityBase(pe.Severity);
                // pidCount for proximity echoes is always 2 (the pair), so the PID spread
                // term contributes minimally -- this is intentional, as echo severity is
                // dominated by the proximity distance itself (the severityBase).
                double pidCount = 2.0;
                double severity = Math.Min(100.0, severityBase + (pidCount / totalPidCount) * 20.0);

                var instruction = _config.ProximityEchoTemplate
                    .Replace("{secondPid}", pe.PidB ?? "")
                    .Replace("{firstPid}",  pe.PidA ?? "")
                    .Replace("{term}",      pe.Term ?? "");

                var constraints = new List<XElement>
                {
                    new XElement(Ns + "ban",
                        new XAttribute("type", "word"),
                        pe.Term ?? ""),
                    new XElement(Ns + "instruction", instruction)
                };

                var targets = new List<XElement>
                {
                    new XElement(Ns + "pid", new XAttribute("ref", pe.PidA ?? "")),
                    new XElement(Ns + "pid", new XAttribute("ref", pe.PidB ?? ""))
                };

                var description = string.Format(CultureInfo.InvariantCulture,
                    "Word repeated within {0} words ({1} severity).",
                    pe.DistanceWords, pe.Severity ?? "");

                entries.Add(new WorkOrderEntry
                {
                    Type        = "proximity-echo",
                    Severity    = severity,
                    Subject     = pe.Term ?? "",
                    Description = description,
                    Targets     = targets,
                    Constraints = constraints
                });
            }

            // --- Sort by severity descending, assign IDs ---
            entries.Sort((a, b) => b.Severity.CompareTo(a.Severity));

            // --- Build XDocument ---
            var sourceAnalysis = string.Format("{0}_draft{1}_analysis.xml",
                chapterId ?? "unknown", draftNumber);

            var manifestEl = new XElement(Ns + "workorder-manifest",
                new XAttribute("schema-version",   "1.0"),
                new XAttribute("generated-at",     DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
                new XAttribute("chapter-id",       chapterId ?? ""),
                new XAttribute("source-draft",     draftNumber.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("source-analysis",  sourceAnalysis),
                new XAttribute("total-entries",    entries.Count),
                new XAttribute("status",           "pending"));

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var entryEl = new XElement(Ns + "entry",
                    new XAttribute("id",       string.Format("WO-{0:D3}", i + 1)),
                    new XAttribute("type",     e.Type),
                    new XAttribute("severity", e.Severity.ToString("F1", CultureInfo.InvariantCulture)),
                    new XAttribute("status",   "pending"));
                if (e.Count > 0)
                    entryEl.Add(new XAttribute("count", e.Count));

                entryEl.Add(new XElement(Ns + "subject", e.Subject));
                entryEl.Add(new XElement(Ns + "description", e.Description));

                var targetsEl = new XElement(Ns + "targets");
                foreach (var t in e.Targets) targetsEl.Add(t);
                entryEl.Add(targetsEl);

                var constraintsEl = new XElement(Ns + "constraints");
                foreach (var c in e.Constraints) constraintsEl.Add(c);
                entryEl.Add(constraintsEl);

                manifestEl.Add(entryEl);
            }

            return new XDocument(new XDeclaration("1.0", "utf-8", null), manifestEl);
        }

        // --- Helpers ---

        private List<XElement> BuildWordTargets(FlaggedWord fw)
        {
            var list = new List<XElement>();
            if (fw.Pids == null) return list;
            foreach (var pc in fw.Pids)
                list.Add(new XElement(Ns + "pid",
                    new XAttribute("ref",   pc.Pid ?? ""),
                    new XAttribute("count", pc.Count)));
            return list;
        }

        private List<XElement> BuildNgramTargets(FlaggedNgram fn)
        {
            var list = new List<XElement>();
            if (fn.Pids == null) return list;
            foreach (var pid in fn.Pids)
                list.Add(new XElement(Ns + "pid", new XAttribute("ref", pid ?? "")));
            return list;
        }

        private static int EchoSeverityBase(string severity)
        {
            if (severity == null) return 50;
            switch (severity.ToLowerInvariant())
            {
                case "high":   return 90;
                case "medium": return 70;
                case "low":    return 50;
                default:       return 50;
            }
        }

        /// <summary>
        /// Returns obvious gender-swapped variants of a phrase via simple pronoun substitution.
        /// Deliberately crude -- no NLP. Catches common AI gesture tics like "shook her head".
        /// </summary>
        private static List<string> GetPronounVariants(string phrase)
        {
            var variants = new List<string>();
            if (string.IsNullOrEmpty(phrase)) return variants;

            // her <-> his (bidirectional)
            if (phrase.IndexOf("her", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var v = ReplaceWord(phrase, "her", "his");
                if (v != phrase) variants.Add(v);
            }
            else if (phrase.IndexOf("his", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var v = ReplaceWord(phrase, "his", "her");
                if (v != phrase) variants.Add(v);
            }

            // she <-> he (bidirectional)
            if (phrase.IndexOf("she", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var v = ReplaceWord(phrase, "she", "he");
                if (v != phrase) variants.Add(v);
            }
            else if (phrase.IndexOf(" he ", StringComparison.OrdinalIgnoreCase) >= 0
                  || phrase.StartsWith("he ", StringComparison.OrdinalIgnoreCase))
            {
                var v = ReplaceWord(phrase, "he", "she");
                if (v != phrase) variants.Add(v);
            }

            // "their" is gender-neutral -- no swap

            return variants;
        }

        /// <summary>
        /// Case-insensitive whole-word replace of oldWord with newWord in phrase.
        /// Returns the original string unchanged if oldWord is not present as a whole word.
        /// </summary>
        private static string ReplaceWord(string phrase, string oldWord, string newWord)
        {
            // Walk through the phrase looking for whole-word matches
            int idx = phrase.IndexOf(oldWord, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return phrase;

            bool prevOk = idx == 0 || !char.IsLetterOrDigit(phrase[idx - 1]);
            bool nextOk = idx + oldWord.Length == phrase.Length
                       || !char.IsLetterOrDigit(phrase[idx + oldWord.Length]);

            if (!prevOk || !nextOk) return phrase;

            // Preserve the case of the matched token
            string matched = phrase.Substring(idx, oldWord.Length);
            string replacement = PreserveCase(matched, newWord);
            return phrase.Substring(0, idx) + replacement + phrase.Substring(idx + oldWord.Length);
        }

        private static string PreserveCase(string original, string replacement)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(replacement))
                return replacement;
            if (char.IsUpper(original[0]))
                return char.ToUpperInvariant(replacement[0]) + replacement.Substring(1);
            return replacement;
        }

        private static string NgramThresholdLabel(int size)
        {
            switch (size)
            {
                case 2:  return "5";
                case 3:  return "3";
                case 4:  return "2";
                default: return "?";
            }
        }

        private class WorkOrderEntry
        {
            public string          Type        { get; set; }
            public double          Severity    { get; set; }
            public int             Count       { get; set; } // raw occurrence count; 0 = not applicable
            public string          Subject     { get; set; }
            public string          Description { get; set; }
            public List<XElement>  Targets     { get; set; }
            public List<XElement>  Constraints { get; set; }
        }
    }
}
