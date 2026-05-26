using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ContentAnalysisEngine
{
    public class ValidationResult
    {
        public bool Passed { get; set; }
        public List<string> Diagnostics { get; set; }

        public ValidationResult()
        {
            Diagnostics = new List<string>();
        }
    }

    public class WorkOrderValidator
    {
        // Namespace for work order manifest XML
        private static readonly XNamespace WoNs = "https://bookml.org/ns/workorder/1.0";
        // Namespace for BookML chapter XML — must match BookmlReader.cs
        private static readonly XNamespace ChNs = "https://bookml.org/ns/1.0";

        public ValidationResult Validate(
            string originalChapterPath,
            string rewrittenChapterPath,
            string manifestPath,
            string entryId,
            string namesFilePath = null,
            Dictionary<string, string> preRewriteBaselines = null)
        {
            var result = new ValidationResult();

            // -------------------------------------------------------
            // Check 1 — XML well-formedness
            // -------------------------------------------------------
            XDocument rewrittenDoc;
            try
            {
                rewrittenDoc = XDocument.Load(rewrittenChapterPath);
            }
            catch (Exception ex)
            {
                result.Diagnostics.Add("Rewritten chapter is not well-formed XML: " + ex.Message);
                result.Passed = false;
                return result; // Cannot proceed without a valid rewritten doc
            }

            XDocument originalDoc = XDocument.Load(originalChapterPath);

            // -------------------------------------------------------
            // Check 2 — PID integrity
            // -------------------------------------------------------
            var originalPids  = ExtractPids(originalDoc);
            var rewrittenPids = ExtractPids(rewrittenDoc);

            var missingPids    = originalPids.Except(rewrittenPids).OrderBy(p => p).ToList();
            var unexpectedPids = rewrittenPids.Except(originalPids).OrderBy(p => p).ToList();

            if (missingPids.Count > 0)
                result.Diagnostics.Add("Missing PIDs: " + string.Join(", ", missingPids));
            if (unexpectedPids.Count > 0)
                result.Diagnostics.Add("Unexpected PIDs: " + string.Join(", ", unexpectedPids));

            // -------------------------------------------------------
            // Load manifest and find the entry
            // -------------------------------------------------------
            XDocument manifestDoc = XDocument.Load(manifestPath);
            XElement entry = manifestDoc.Descendants(WoNs + "entry")
                .FirstOrDefault(e => (string)e.Attribute("id") == entryId);

            if (entry == null)
            {
                result.Diagnostics.Add("Entry '" + entryId + "' not found in manifest.");
                result.Passed = result.Diagnostics.Count == 0;
                return result;
            }

            // Target PIDs for this entry
            var targetPids = entry
                .Element(WoNs + "targets")
                ?.Elements(WoNs + "pid")
                .Select(p => (string)p.Attribute("ref"))
                .Where(r => r != null)
                .ToList() ?? new List<string>();

            // Build a pid->text lookup for the rewritten chapter
            var rewrittenParaMap = BuildParaMap(rewrittenDoc);
            var originalParaMap  = BuildParaMap(originalDoc);

            // -------------------------------------------------------
            // Check 3 — Ban compliance
            // Only check sentences that contain the subject term — the skill rewrites
            // those sentences only, so other sentences may still contain the banned term.
            // -------------------------------------------------------
            var constraints = entry.Element(WoNs + "constraints");
            if (constraints != null)
            {
                string subject = (entry.Element(WoNs + "subject")?.Value ?? "").Trim();

                foreach (var ban in constraints.Elements(WoNs + "ban"))
                {
                    string banType = (string)ban.Attribute("type") ?? "word";
                    string banTerm = (ban.Value ?? "").Trim();
                    if (string.IsNullOrEmpty(banTerm)) continue;

                    foreach (var pid in targetPids)
                    {
                        string paraText;
                        if (!rewrittenParaMap.TryGetValue(pid, out paraText)) continue;

                        // Only check sentences that originally contained the subject
                        string origParaText;
                        if (!originalParaMap.TryGetValue(pid, out origParaText)) origParaText = paraText;
                        var origSentences = SplitSentences(origParaText);
                        var rewrSentences = SplitSentences(paraText);

                        // If sentence counts differ we can't align them — fall back to full-paragraph check
                        if (origSentences.Length != rewrSentences.Length)
                        {
                            bool violated = banType == "phrase"
                                ? paraText.IndexOf(banTerm, StringComparison.OrdinalIgnoreCase) >= 0
                                : ContainsWholeWord(paraText, banTerm);
                            if (violated)
                                result.Diagnostics.Add(
                                    string.Format("Ban violation: '{0}' found in PID {1}", banTerm, pid));
                            continue;
                        }

                        for (int si = 0; si < rewrSentences.Length; si++)
                        {
                            // Skip sentences that didn't contain the subject — they weren't rewritten
                            if (!string.IsNullOrEmpty(subject) &&
                                origSentences[si].IndexOf(subject, StringComparison.OrdinalIgnoreCase) < 0)
                                continue;

                            string sentText = rewrSentences[si];
                            bool violated = banType == "phrase"
                                ? sentText.IndexOf(banTerm, StringComparison.OrdinalIgnoreCase) >= 0
                                : ContainsWholeWord(sentText, banTerm);

                            if (violated)
                                result.Diagnostics.Add(
                                    string.Format("Ban violation: '{0}' found in PID {1} sentence {2}", banTerm, pid, si + 1));
                        }
                    }
                }

                // -------------------------------------------------------
                // Check 5 — Limit compliance (evaluated here using full chapter)
                // -------------------------------------------------------
                var limit = constraints.Elements(WoNs + "limit")
                    .FirstOrDefault(l => (string)l.Attribute("type") == "max-per-chapter");
                if (limit != null)
                {
                    int maxValue;
                    if (int.TryParse((string)limit.Attribute("value"), out maxValue))
                    {
                        string subjectWord = (entry.Element(WoNs + "subject")?.Value ?? "").Trim();
                        if (!string.IsNullOrEmpty(subjectWord))
                        {
                            int totalCount = CountWholeWordInDoc(rewrittenDoc, subjectWord);
                            if (totalCount > maxValue)
                                result.Diagnostics.Add(string.Format(
                                    "Limit exceeded: '{0}' appears {1} times, limit is {2}",
                                    subjectWord, totalCount, maxValue));
                        }
                    }
                }
            }

            // -------------------------------------------------------
            // Check 4 — Word count tolerance (25%)
            // -------------------------------------------------------
            foreach (var pid in targetPids)
            {
                string origText, rewrText;
                if (!originalParaMap.TryGetValue(pid, out origText)) continue;
                if (!rewrittenParaMap.TryGetValue(pid, out rewrText)) continue;

                int origCount = CountWords(origText);
                int rewrCount = CountWords(rewrText);

                if (origCount > 0)
                {
                    double pct = Math.Abs(rewrCount - origCount) / (double)origCount;
                    if (pct > 0.25)
                        result.Diagnostics.Add(string.Format(
                            "Word count deviation in PID {0}: original {1}, rewritten {2} ({3}% change)",
                            pid, origCount, rewrCount, Math.Round(pct * 100, 1)));
                }
            }

            // -------------------------------------------------------
            // Check 6 — Text preservation
            // -------------------------------------------------------
            string subjectTerm = (entry.Element(WoNs + "subject")?.Value ?? "").Trim();
            foreach (var pid in targetPids)
            {
                string baseText, rewrText;
                // Use pre-rewrite baseline when available; fall back to original chapter
                if (preRewriteBaselines == null || !preRewriteBaselines.TryGetValue(pid, out baseText))
                {
                    if (!originalParaMap.TryGetValue(pid, out baseText)) continue;
                }
                if (!rewrittenParaMap.TryGetValue(pid, out rewrText)) continue;

                // shadow origText name for clarity in the rest of the block
                string origText = baseText;
                var origSentences = SplitSentences(origText);
                var rewrSentences = SplitSentences(rewrText);

                if (origSentences.Length != rewrSentences.Length)
                {
                    result.Diagnostics.Add(string.Format(
                        "Text preservation violation in PID {0}: sentence count changed from {1} to {2}.",
                        pid, origSentences.Length, rewrSentences.Length));
                    continue;
                }

                if (!string.IsNullOrEmpty(subjectTerm))
                {
                    for (int si = 0; si < origSentences.Length; si++)
                    {
                        string origSent = origSentences[si];
                        // Only check sentences that do NOT contain the target term
                        if (origSent.IndexOf(subjectTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                            continue;

                        string normOrig = NormaliseSentence(origSent);
                        string normRewr = NormaliseSentence(rewrSentences[si]);
                        if (!string.Equals(normOrig, normRewr, StringComparison.OrdinalIgnoreCase))
                        {
                            string origPreview = origSent.Length > 50 ? origSent.Substring(0, 50) + "..." : origSent;
                            string rewrPreview = rewrSentences[si].Length > 50 ? rewrSentences[si].Substring(0, 50) + "..." : rewrSentences[si];
                            result.Diagnostics.Add(string.Format(
                                "Text preservation violation in PID {0}: sentence {1} was modified but does not contain the target term '{2}'. Original: '{3}' Rewritten: '{4}'",
                                pid, si + 1, subjectTerm, origPreview, rewrPreview));
                        }
                    }
                }
            }

            // -------------------------------------------------------
            // Check 7 — Name preservation (requires names file)
            // -------------------------------------------------------
            if (namesFilePath != null)
            {
                var names = NamesReader.ReadNames(namesFilePath);
                foreach (var pid in targetPids)
                {
                    string baseText, rewrText;
                    // Use pre-rewrite baseline when available; fall back to original chapter
                    if (preRewriteBaselines == null || !preRewriteBaselines.TryGetValue(pid, out baseText))
                    {
                        if (!originalParaMap.TryGetValue(pid, out baseText)) continue;
                    }
                    if (!rewrittenParaMap.TryGetValue(pid, out rewrText)) continue;

                    foreach (var name in names)
                    {
                        int origCount = CountOccurrences(baseText, name);
                        int rewrCount = CountOccurrences(rewrText, name);
                        if (rewrCount < origCount)
                            result.Diagnostics.Add(string.Format(
                                "Name preservation violation in PID {0}: character name '{1}' appears {2} time(s) in original but {3} time(s) in rewrite.",
                                pid, name, origCount, rewrCount));
                    }
                }
            }

            result.Passed = result.Diagnostics.Count == 0;
            return result;
        }

        // -------------------------------------------------------
        // Helpers
        // -------------------------------------------------------

        /// <summary>
        /// Loads pre-rewrite baseline texts from a snapshots directory.
        /// Files must match the pattern {entryId}__{pid}.txt (double underscore).
        /// Returns a pid->text dictionary for all matching files.
        /// </summary>
        public static Dictionary<string, string> LoadBaselines(string snapshotsDir, string entryId)
        {
            var baselines = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(snapshotsDir) || !Directory.Exists(snapshotsDir))
                return baselines;

            string prefix = entryId + "__";
            foreach (var file in Directory.GetFiles(snapshotsDir, prefix + "*.txt"))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                // fileName is e.g. "WO-003__CH01-P0010" — strip the prefix to get the PID
                string pid = fileName.Substring(prefix.Length);
                if (!string.IsNullOrEmpty(pid))
                    baselines[pid] = File.ReadAllText(file);
            }
            return baselines;
        }

        private static HashSet<string> ExtractPids(XDocument doc)
        {
            return new HashSet<string>(
                doc.Descendants(ChNs + "para")
                   .Select(p => (string)p.Attribute("pid"))
                   .Where(pid => !string.IsNullOrEmpty(pid)),
                StringComparer.Ordinal);
        }

        private static Dictionary<string, string> BuildParaMap(XDocument doc)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var para in doc.Descendants(ChNs + "para"))
            {
                string pid = (string)para.Attribute("pid");
                if (!string.IsNullOrEmpty(pid))
                    map[pid] = para.Value ?? "";
            }
            return map;
        }

        private static bool ContainsWholeWord(string text, string word)
        {
            // Tokenise both and check for presence
            var tokens = SplitTokens(text);
            var wordLower = word.ToLowerInvariant();
            foreach (var token in tokens)
            {
                if (token == wordLower) return true;
            }
            return false;
        }

        private static int CountWholeWordInDoc(XDocument doc, string word)
        {
            var wordLower = word.ToLowerInvariant();
            int count = 0;
            foreach (var para in doc.Descendants(ChNs + "para"))
            {
                foreach (var token in SplitTokens(para.Value ?? ""))
                {
                    if (token == wordLower) count++;
                }
            }
            return count;
        }

        private static string[] SplitTokens(string text)
        {
            return text.ToLowerInvariant()
                       .Split(new char[0], StringSplitOptions.RemoveEmptyEntries)
                       // Further split on non-letter, non-digit boundaries
                       // by re-splitting each chunk on punctuation
                       // Simple approach: split on any non-word char
                       .SelectMany(t => t.Split(
                           new[] { '-', '\'', '\u2019', ',', '.', '!', '?', ';', ':', '"', '"', '"' },
                           StringSplitOptions.RemoveEmptyEntries))
                       .Where(t => t.Length > 0)
                       .ToArray();
        }

        private static int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return text.Split(new char[0], StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private static string[] SplitSentences(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new[] { text ?? "" };
            var parts = Regex.Split(text.Trim(), @"(?<=[.?!])\s+");
            return parts.Where(s => s.Length > 0).ToArray();
        }

        private static string NormaliseSentence(string sentence)
        {
            return Regex.Replace(sentence.Trim(), @"\s+", " ");
        }

        private static int CountOccurrences(string text, string term)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term))
                return 0;
            int count = 0;
            int idx = 0;
            while ((idx = text.IndexOf(term, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                idx += term.Length;
            }
            return count;
        }
    }
}
