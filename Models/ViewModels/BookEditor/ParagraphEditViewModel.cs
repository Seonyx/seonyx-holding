using System.Collections.Generic;

namespace Seonyx.Web.Models.ViewModels.BookEditor
{
    public class ParagraphEditViewModel
    {
        public int BookProjectID { get; set; }
        public string ProjectName { get; set; }

        public int ParagraphID { get; set; }
        public int ChapterID { get; set; }
        public string UniqueID { get; set; }
        public int OrdinalPosition { get; set; }
        public string ParagraphText { get; set; }
        public string MetaText { get; set; }
        public string EditNoteText { get; set; }

        public string ChapterTitle { get; set; }
        public int ChapterNumber { get; set; }
        public int TotalChapters { get; set; }

        // Navigation
        public int? PrevParagraphID { get; set; }
        public int? NextParagraphID { get; set; }
        public int FirstParagraphID { get; set; }
        public int LastParagraphID { get; set; }
        public int TotalParagraphs { get; set; }
        public int GlobalPosition { get; set; }

        // Bookmark
        public int? BookmarkParagraphID { get; set; }
        public bool IsBookmarked { get; set; }

        // Chapter list for jump-to dropdown
        public List<ChapterSummary> ChapterList { get; set; }

        public ParagraphEditViewModel()
        {
            ChapterList = new List<ChapterSummary>();
        }
    }

    public class ChapterSummary
    {
        public int ChapterID { get; set; }
        public int ChapterNumber { get; set; }
        public string ChapterTitle { get; set; }
        public int ParagraphCount { get; set; }
        public int FirstParagraphID { get; set; }
    }
}
