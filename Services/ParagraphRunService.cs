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
        /// Concatenated RunText for one paragraph, in Seq order. Break runs
        /// contribute nothing. Overlay runs excluded unless includeOverlays
        /// is true. Amendment section 7.3.
        /// </summary>
        public string GetProseText(SeonyxContext db, int paragraphId, bool includeOverlays = false)
        {
            var runs = db.ParagraphRuns
                .Where(r => r.ParagraphID == paragraphId
                         && !r.IsBreak
                         && (includeOverlays || r.OverlayType == null))
                .OrderBy(r => r.Seq)
                .Select(r => r.RunText)
                .ToList();

            return string.Concat(runs);
        }

        /// <summary>
        /// Bulk variant: prose text per paragraph across a chapter, joined
        /// with a blank line between paragraphs, in paragraph OrdinalPosition
        /// order. For analytics ingestion (not used until P4).
        /// </summary>
        public string GetProseTextForChapter(SeonyxContext db, int chapterId, bool includeOverlays = false)
        {
            var paragraphIds = db.Paragraphs
                .Where(p => p.ChapterID == chapterId)
                .OrderBy(p => p.OrdinalPosition)
                .Select(p => p.ParagraphID)
                .ToList();

            var paragraphTexts = paragraphIds
                .Select(id => GetProseText(db, id, includeOverlays));

            return string.Join("\n\n", paragraphTexts);
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
