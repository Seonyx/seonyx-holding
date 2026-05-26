using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ContentAnalysisEngine.Metrics;

namespace ContentAnalysisEngine
{
    public class BookAnalyser
    {
        private readonly AnalysisConfiguration _config;

        public BookAnalyser(AnalysisConfiguration config = null)
        {
            _config = config ?? new AnalysisConfiguration();
        }

        public BookAnalysisReport Analyse(string bookXmlPath, string namesFilePath)
        {
            var manifest = BookReader.ReadManifest(bookXmlPath);

            // Merge names into stop words so they are excluded from outlier detection
            var config = _config;
            if (namesFilePath != null)
            {
                var names = NamesReader.ReadNames(namesFilePath);
                if (names.Count > 0)
                {
                    config = CloneConfig(_config);
                    if (config.AdditionalStopWords == null)
                        config.AdditionalStopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var n in names)
                    {
                        var lower = n.ToLowerInvariant();
                        config.AdditionalStopWords.Add(lower);
                        config.AdditionalStopWords.Add(lower + "'s");
                    }
                }
            }

            // Analyse each chapter
            var chapterAnalyser   = new ChapterAnalyser(config);
            var perChapterReports = new List<AnalysisReport>();
            var chapterIds        = new List<string>();

            foreach (var chRef in manifest.Chapters)
            {
                try
                {
                    var report = chapterAnalyser.Analyse(chRef.ChapterFilePath);
                    perChapterReports.Add(report);
                    chapterIds.Add(chRef.ComponentId);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        string.Format("Failed to analyse chapter '{0}': {1}", chRef.ComponentId, ex.Message), ex);
                }
            }

            int totalChapters = perChapterReports.Count;

            // ----------------------------------------------------------------
            // Book-level word frequency
            // ----------------------------------------------------------------
            // Aggregate: word -> totalCount, word -> set of chapter indices present
            var bookWordCounts   = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var wordChapterSets  = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

            for (int ci = 0; ci < perChapterReports.Count; ci++)
            {
                foreach (var fw in perChapterReports[ci].FlaggedWords ?? Enumerable.Empty<FlaggedWord>())
                {
                    int existing;
                    bookWordCounts[fw.Word] = (bookWordCounts.TryGetValue(fw.Word, out existing) ? existing : 0) + fw.Count;

                    HashSet<int> chapSet;
                    if (!wordChapterSets.TryGetValue(fw.Word, out chapSet))
                    {
                        chapSet = new HashSet<int>();
                        wordChapterSets[fw.Word] = chapSet;
                    }
                    chapSet.Add(ci);
                }
            }

            // Re-compute Z-scores against book-wide distribution
            var bookOutlierWords = new List<BookFlaggedWord>();
            if (bookWordCounts.Count > 0)
            {
                var counts    = bookWordCounts.Values.Select(c => (double)c).ToList();
                double mean   = counts.Average();
                double stddev = Math.Sqrt(counts.Select(c => (c - mean) * (c - mean)).Average());
                double threshold = config.OutlierZScoreThreshold;

                foreach (var kv in bookWordCounts)
                {
                    double zScore = stddev > 0 ? (kv.Value - mean) / stddev : 0;
                    if (zScore <= threshold) continue;

                    int chaptersPresent = wordChapterSets.ContainsKey(kv.Key)
                        ? wordChapterSets[kv.Key].Count : 0;
                    string distribution = (totalChapters > 0 && (double)chaptersPresent / totalChapters >= 0.5)
                        ? "book-wide-tic"
                        : "chapter-concentrated";

                    bookOutlierWords.Add(new BookFlaggedWord
                    {
                        Value           = kv.Key,
                        Count           = kv.Value,
                        ZScore          = Math.Round(zScore, 4),
                        ChaptersPresent = chaptersPresent,
                        Distribution    = distribution
                    });
                }

                bookOutlierWords = bookOutlierWords.OrderByDescending(w => w.ZScore).ToList();
            }

            // ----------------------------------------------------------------
            // Book-level n-gram frequency
            // ----------------------------------------------------------------
            var ngramCounts      = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var ngramChapterSets = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

            for (int ci = 0; ci < perChapterReports.Count; ci++)
            {
                foreach (var fn in perChapterReports[ci].FlaggedNgrams ?? Enumerable.Empty<FlaggedNgram>())
                {
                    int existing;
                    ngramCounts[fn.Phrase] = (ngramCounts.TryGetValue(fn.Phrase, out existing) ? existing : 0) + fn.Count;

                    HashSet<int> chapSet;
                    if (!ngramChapterSets.TryGetValue(fn.Phrase, out chapSet))
                    {
                        chapSet = new HashSet<int>();
                        ngramChapterSets[fn.Phrase] = chapSet;
                    }
                    chapSet.Add(ci);
                }
            }

            int totalBookWords = perChapterReports.Sum(r => r.Metrics != null ? r.Metrics.TotalWords : 0);

            var bookOutlierNgrams = new List<BookFlaggedNgram>();
            foreach (var kv in ngramCounts)
            {
                // Re-check rate threshold against book-wide word count
                double rate = totalBookWords > 0 ? (kv.Value / (double)totalBookWords) * 10000.0 : 0;
                // Use the lowest threshold (bigram) as a conservative filter — keeps all phrases
                // that were flagged in at least one chapter and appear at book-level rate > 1/10k
                if (rate < 1.0) continue;

                int chaptersPresent = ngramChapterSets.ContainsKey(kv.Key)
                    ? ngramChapterSets[kv.Key].Count : 0;
                string distribution = (totalChapters > 0 && (double)chaptersPresent / totalChapters >= 0.5)
                    ? "book-wide-tic"
                    : "chapter-concentrated";

                bookOutlierNgrams.Add(new BookFlaggedNgram
                {
                    Phrase          = kv.Key,
                    Count           = kv.Value,
                    ChaptersPresent = chaptersPresent,
                    Distribution    = distribution
                });
            }
            bookOutlierNgrams = bookOutlierNgrams.OrderByDescending(n => n.Count).ToList();

            // ----------------------------------------------------------------
            // Book-level TTR, hapax, sentence length
            // ----------------------------------------------------------------
            // Combine all tokens across chapters for TTR/MATTR
            var allTokens     = new List<string>();
            var allWordCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var report in perChapterReports)
            {
                // Reconstruct token contribution from per-chapter word counts
                // We need the raw flagged words counts — but those are post-filter.
                // For TTR we need the full unfiltered token stream. Use per-chapter totals.
                // Best available: sum TotalWords for total, UniqueWords for an approximation.
                // For MATTR we need the actual token list — collect from paragraphs directly.
            }

            // Collect paragraphs directly for accurate TTR across all chapters
            var allParas = new List<ParagraphEntry>();
            foreach (var chRef in manifest.Chapters)
            {
                try { allParas.AddRange(BookmlReader.ReadParagraphs(chRef.ChapterFilePath)); }
                catch { /* already analysed successfully above; ignore re-read errors */ }
            }

            foreach (var para in allParas)
                allTokens.AddRange(Tokeniser.Tokenise(para.Text));

            double ttr, mattr;
            TtrMetric.Analyse(allTokens, out ttr, out mattr);

            // Build full word counts from all tokens for hapax (must use unfiltered counts)
            foreach (var token in allTokens)
            {
                string lower = token.ToLowerInvariant();
                int existing;
                allWordCounts[lower] = (allWordCounts.TryGetValue(lower, out existing) ? existing : 0) + 1;
            }
            var hapax = HapaxMetric.Analyse(allWordCounts);

            int uniqueWords = allTokens
                .Select(t => t.ToLowerInvariant())
                .Distinct()
                .Count();

            // Sentence length mean/stddev
            double meanSentenceLength = 0, sentenceLengthStdDev = 0;
            ComputeSentenceLengthStats(allParas, out meanSentenceLength, out sentenceLengthStdDev);

            // ----------------------------------------------------------------
            // Chapter summaries
            // ----------------------------------------------------------------
            var chapterSummaries = new List<BookChapterSummary>();
            for (int i = 0; i < perChapterReports.Count; i++)
            {
                var r  = perChapterReports[i];
                var m  = r.Metrics ?? new MetricsSummary();
                chapterSummaries.Add(new BookChapterSummary
                {
                    Id           = chapterIds[i],
                    Words        = m.TotalWords,
                    Ttr          = m.TypeTokenRatio,
                    HapaxRatio   = m.HapaxRatio,
                    OutlierCount = (r.FlaggedWords ?? Enumerable.Empty<FlaggedWord>()).Count(),
                    NgramCount   = (r.FlaggedNgrams ?? Enumerable.Empty<FlaggedNgram>()).Count(),
                    EchoCount    = (r.ProximityEchoes ?? Enumerable.Empty<ProximityEcho>()).Count(),
                });
            }

            // Names excluded list (for output)
            var namesExcluded = namesFilePath != null
                ? NamesReader.ReadNames(namesFilePath).OrderBy(n => n).ToList()
                : new List<string>();

            return new BookAnalysisReport
            {
                GeneratedAt      = DateTime.UtcNow,
                BookId           = manifest.BookId,
                TotalWords       = totalBookWords,
                TotalChapters    = totalChapters,
                NamesExcluded    = namesExcluded,
                Metrics          = new BookMetricsSummary
                {
                    UniqueWords          = uniqueWords,
                    TypeTokenRatio       = Math.Round(ttr, 6),
                    MovingAverageTTR     = Math.Round(mattr, 6),
                    HapaxCount           = hapax.HapaxCount,
                    HapaxRatio           = hapax.HapaxRatio,
                    MeanSentenceLength   = Math.Round(meanSentenceLength, 2),
                    SentenceLengthStdDev = Math.Round(sentenceLengthStdDev, 2),
                },
                OutlierWords     = bookOutlierWords,
                OutlierNgrams    = bookOutlierNgrams,
                ChapterSummaries = chapterSummaries,
            };
        }

        private static void ComputeSentenceLengthStats(
            List<ParagraphEntry> paragraphs,
            out double mean,
            out double stddev)
        {
            var lengths = new List<int>();
            foreach (var para in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(para.Text)) continue;
                var sentences = Regex.Split(para.Text.Trim(), @"(?<=[.?!])\s+")
                    .Where(s => s.Length > 0);
                foreach (var s in sentences)
                {
                    int wordCount = s.Split(new char[0], StringSplitOptions.RemoveEmptyEntries).Length;
                    if (wordCount > 0) lengths.Add(wordCount);
                }
            }

            if (lengths.Count == 0) { mean = 0; stddev = 0; return; }

            double m = lengths.Average();
            mean   = m;
            stddev = Math.Sqrt(lengths.Select(l => (l - m) * (l - m)).Average());
        }

        private static AnalysisConfiguration CloneConfig(AnalysisConfiguration src)
        {
            return new AnalysisConfiguration
            {
                OutlierZScoreThreshold  = src.OutlierZScoreThreshold,
                BigramThresholdPer10k   = src.BigramThresholdPer10k,
                TrigramThresholdPer10k  = src.TrigramThresholdPer10k,
                FourgramThresholdPer10k = src.FourgramThresholdPer10k,
                EchoWindowWords         = src.EchoWindowWords,
                AdditionalStopWords     = src.AdditionalStopWords != null
                    ? new HashSet<string>(src.AdditionalStopWords, StringComparer.OrdinalIgnoreCase)
                    : null,
                AlwaysFlagWords         = src.AlwaysFlagWords != null
                    ? new HashSet<string>(src.AlwaysFlagWords, StringComparer.OrdinalIgnoreCase)
                    : null,
            };
        }
    }
}
