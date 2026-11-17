using System;
using System.Collections.Generic;
using System.Linq;
using Seonyx.Web.Models;

namespace Seonyx.Web.Services
{
    /// <summary>
    /// Read/write access to ParagraphRun rows. Used by the BookML
    /// importer/exporter (P2) and the paragraph editor (P3), which is the
    /// authoritative source of truth for a paragraph's styled content.
    /// Paragraph.ParagraphText (mapped to ParagraphText_Legacy) is kept as
    /// a derived plain-text mirror for SQL search only.
    /// </summary>
    public class ParagraphRunService
    {
        /// <summary>Runs for one paragraph, ordered by Seq.</summary>
        public IList<ParagraphRun> LoadRuns(SeonyxContext db, int paragraphId)
        {
            return db.ParagraphRuns
                .Where(r => r.ParagraphID == paragraphId)
                .OrderBy(r => r.Seq)
                .ToList();
        }

        /// <summary>
        /// Replaces all runs for a paragraph with the given ordered list.
        /// Rejects an incoming list containing adjacent runs with identical
        /// style flags -- the caller must coalesce before calling this.
        /// </summary>
        public void SaveRuns(SeonyxContext db, int paragraphId, IList<ParagraphRun> runs)
        {
            AssertNoAdjacentCoalescingViolations(paragraphId, runs);

            var existing = db.ParagraphRuns.Where(r => r.ParagraphID == paragraphId);
            db.ParagraphRuns.RemoveRange(existing);

            for (int i = 0; i < runs.Count; i++)
            {
                var run = runs[i];
                run.ParagraphRunID = 0;
                run.ParagraphID = paragraphId;
                run.Seq = i + 1;
                db.ParagraphRuns.Add(run);
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Builds runs from plain text via the shared route
        /// (PlainTextRunBuilder) and saves them. Any parse warning is
        /// appended to warnings (lenient policy only).
        /// </summary>
        public void SaveRunsFromPlainText(SeonyxContext db, int paragraphId, string text, string pid,
            PlainTextParsePolicy policy, IList<string> warnings)
        {
            string warning;
            var runs = PlainTextRunBuilder.BuildRunsFromPlainText(text, pid, policy, out warning);
            if (warning != null && warnings != null) warnings.Add(warning);
            SaveRuns(db, paragraphId, ToParagraphRuns(runs));
        }

        /// <summary>
        /// Runs a reader should use for one paragraph: stored runs, or the
        /// paragraph's text parsed (lenient) when it has none.
        /// </summary>
        public IList<InlineRun> LoadEffectiveRuns(SeonyxContext db, Paragraph paragraph, out string warning)
        {
            return PlainTextRunBuilder.EffectiveRuns(paragraph.ParagraphText, paragraph.UniqueID,
                ToInlineRuns(LoadRuns(db, paragraph.ParagraphID)), out warning);
        }

        /// <summary>
        /// Concatenated text of a paragraph's effective runs, in Seq order.
        /// Break runs contribute nothing. Overlay runs excluded unless
        /// includeOverlays is true. Amendment section 7.3.
        /// </summary>
        public string GetProseText(SeonyxContext db, int paragraphId, bool includeOverlays = false)
        {
            var stored = ToInlineRuns(LoadRuns(db, paragraphId));
            string text = null, pid = null;
            if (stored.Count == 0)
            {
                var paragraph = db.Paragraphs.Find(paragraphId);
                if (paragraph != null)
                {
                    text = paragraph.ParagraphText;
                    pid = paragraph.UniqueID;
                }
            }

            string warning;
            return PlainTextRunBuilder.ProseOf(
                PlainTextRunBuilder.EffectiveRuns(text, pid, stored, out warning), includeOverlays);
        }

        /// <summary>
        /// Bulk variant: prose text per paragraph across a chapter, joined
        /// with a blank line between paragraphs, in paragraph OrdinalPosition
        /// order.
        /// </summary>
        public string GetProseTextForChapter(SeonyxContext db, int chapterId, bool includeOverlays = false)
        {
            var paragraphs = db.Paragraphs
                .Where(p => p.ChapterID == chapterId)
                .OrderBy(p => p.OrdinalPosition)
                .Select(p => new { p.ParagraphID, p.UniqueID, p.ParagraphText })
                .ToList();

            var ids = paragraphs.Select(p => p.ParagraphID).ToList();
            var runsById = db.ParagraphRuns
                .Where(r => ids.Contains(r.ParagraphID))
                .ToList()
                .GroupBy(r => r.ParagraphID)
                .ToDictionary(g => g.Key, g => ToInlineRuns(g.OrderBy(r => r.Seq).ToList()));

            var paragraphTexts = paragraphs.Select(p =>
            {
                IList<InlineRun> stored;
                if (!runsById.TryGetValue(p.ParagraphID, out stored)) stored = new List<InlineRun>();
                string warning;
                return PlainTextRunBuilder.ProseOf(
                    PlainTextRunBuilder.EffectiveRuns(p.ParagraphText, p.UniqueID, stored, out warning), includeOverlays);
            });

            return string.Join("\n\n", paragraphTexts);
        }

        /// <summary>
        /// Converts a paragraph's leftover literal Markdown into real runs
        /// (run repair, Pass B): if the paragraph qualifies (see
        /// PlainTextRunBuilder.TryBuildLegacyMarkdownConversion) AND passes
        /// the stricter safety check (ReviewLegacyMarkdownConversion),
        /// persist the converted runs and resync the ParagraphText mirror.
        /// Returns the converted runs, or null if nothing was changed.
        /// </summary>
        public IList<InlineRun> TryConvertLegacyMarkdownRun(SeonyxContext db, Paragraph paragraph,
            IList<ParagraphRun> existingRuns)
        {
            var converted = PlainTextRunBuilder.TryBuildLegacyMarkdownConversion(
                ToInlineRuns(existingRuns), paragraph.UniqueID);
            if (converted == null) return null;
            if (PlainTextRunBuilder.ReviewLegacyMarkdownConversion(existingRuns[0].RunText, converted) != null)
                return null;

            SaveRuns(db, paragraph.ParagraphID, ToParagraphRuns(converted));

            // Keep the plain-text mirror in sync, as the save endpoints do --
            // SearchParagraphIds queries it directly in SQL.
            paragraph.ParagraphText = GetProseText(db, paragraph.ParagraphID);
            db.SaveChanges();

            return converted;
        }

        public static IList<InlineRun> ToInlineRuns(IEnumerable<ParagraphRun> runs)
        {
            return runs.Select(r => new InlineRun
            {
                Text        = r.RunText,
                IsEmph      = r.IsEmph,
                EmphType    = r.EmphType,
                IsStrong    = r.IsStrong,
                IsSmallCaps = r.IsSmallCaps,
                OverlayType = r.OverlayType,
                IsBreak     = r.IsBreak
            }).ToList();
        }

        public static IList<ParagraphRun> ToParagraphRuns(IEnumerable<InlineRun> runs)
        {
            return runs.Select(r => new ParagraphRun
            {
                RunText     = r.Text,
                IsEmph      = r.IsEmph,
                EmphType    = r.EmphType,
                IsStrong    = r.IsStrong,
                IsSmallCaps = r.IsSmallCaps,
                OverlayType = r.OverlayType,
                IsBreak     = r.IsBreak
            }).ToList();
        }

        private void AssertNoAdjacentCoalescingViolations(int paragraphId, IList<ParagraphRun> runs)
        {
            for (int i = 1; i < runs.Count; i++)
            {
                var a = runs[i - 1];
                var b = runs[i];
                if (a.IsEmph == b.IsEmph && a.EmphType == b.EmphType
                    && a.IsStrong == b.IsStrong && a.IsSmallCaps == b.IsSmallCaps
                    && a.OverlayType == b.OverlayType && a.IsBreak == b.IsBreak && !a.IsBreak)
                {
                    throw new InvalidOperationException(string.Format(
                        "ParagraphRunService.SaveRuns: paragraph {0} has adjacent runs " +
                        "at positions {1}/{2} with identical style -- caller must coalesce before saving.",
                        paragraphId, i - 1, i));
                }
            }
        }
    }
}
