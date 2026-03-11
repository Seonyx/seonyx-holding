using System.Collections.Generic;

namespace Seonyx.Web.Models.ViewModels.BookEditor
{
    public class DraftDiffRow
    {
        public string Pid      { get; set; }
        public string TextA    { get; set; }    // null if this pid only exists in draft B (added)
        public string TextB    { get; set; }    // null if this pid only exists in draft A (removed)
        public int    SeqA     { get; set; }
        public int    SeqB     { get; set; }
        public string Status             { get; set; }    // added | removed | modified | unchanged
        public int?   CurrentParagraphID { get; set; }    // set when the para exists in the working copy
    }

    public class DraftDiffChapter
    {
        public int                  ChapterNumber  { get; set; }
        public string               ChapterTitle   { get; set; }
        public List<DraftDiffRow>   Rows           { get; set; }
        public int                  AddedCount     { get; set; }
        public int                  RemovedCount   { get; set; }
        public int                  ModifiedCount  { get; set; }
        public int                  UnchangedCount { get; set; }

        public bool HasChanges => AddedCount > 0 || RemovedCount > 0 || ModifiedCount > 0;

        public DraftDiffChapter()
        {
            Rows = new List<DraftDiffRow>();
        }
    }

    public class DraftDiffViewModel
    {
        public int                        BookProjectID  { get; set; }
        public string                     ProjectName    { get; set; }
        public int                        DraftA         { get; set; }
        public string                     LabelA         { get; set; }
        public int                        DraftB         { get; set; }
        public string                     LabelB         { get; set; }
        public List<DraftDiffChapter>     Chapters       { get; set; }
        public int                        TotalAdded     { get; set; }
        public int                        TotalRemoved   { get; set; }
        public int                        TotalModified  { get; set; }
        public int                        TotalUnchanged { get; set; }
        public List<int>                  AvailableDrafts { get; set; }

        // Chapter pagination
        public int  CurrentChapterNumber { get; set; }
        public int? PrevChapterNumber    { get; set; }
        public int? NextChapterNumber    { get; set; }

        public DraftDiffViewModel()
        {
            Chapters        = new List<DraftDiffChapter>();
            AvailableDrafts = new List<int>();
        }
    }
}
