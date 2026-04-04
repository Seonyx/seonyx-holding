using System.Collections.Generic;

namespace Seonyx.Web.Models.ViewModels.BookEditor
{
    public class DraftAnalysisViewModel
    {
        public int    BookProjectID { get; set; }
        public string ProjectName   { get; set; }
        public int    ChapterID     { get; set; }
        public string ChapterTitle  { get; set; }
        public int    DraftNumber   { get; set; }
        public string BookmlChapterId { get; set; }  // e.g. "ch01"
        public bool   HasManifest   { get; set; }

        // Chapter picker
        public List<ChapterPickerItem> Chapters { get; set; }

        // Populated when HasManifest = true
        public List<WorkOrderEntryViewModel> Entries { get; set; }

        // Summary counts (for initial server-rendered state)
        public int TotalEntries  { get; set; }
        public int PendingCount  { get; set; }
        public int SkippedCount  { get; set; }
        public int OutlierCount  { get; set; }
        public int NgramCount    { get; set; }
        public int EchoCount     { get; set; }
        public int ManualCount   { get; set; }

        public DraftAnalysisViewModel()
        {
            Chapters = new List<ChapterPickerItem>();
            Entries  = new List<WorkOrderEntryViewModel>();
        }
    }

    public class ChapterPickerItem
    {
        public int    ChapterID      { get; set; }
        public int    ChapterNumber  { get; set; }
        public string ChapterTitle   { get; set; }
        public string BookmlChapterId { get; set; }
    }

    public class WorkOrderEntryViewModel
    {
        public string Id          { get; set; }  // WO-001
        public string Type        { get; set; }  // outlier-word | repetitive-ngram | proximity-echo | manual
        public string TypeLabel   { get; set; }  // human-readable label
        public double Severity    { get; set; }
        public string Status      { get; set; }  // pending | skipped
        public string Subject     { get; set; }
        public string Description { get; set; }
        public string Instruction { get; set; }
        public string ConstraintSummary { get; set; }  // abbreviated for table column

        public List<WorkOrderPidViewModel> Targets { get; set; }
        public List<WorkOrderBanViewModel> Bans    { get; set; }
        public WorkOrderLimitViewModel     Limit   { get; set; }  // null if no limit element

        public WorkOrderEntryViewModel()
        {
            Targets = new List<WorkOrderPidViewModel>();
            Bans    = new List<WorkOrderBanViewModel>();
        }
    }

    public class WorkOrderPidViewModel
    {
        public string Pid         { get; set; }
        public int?   Count       { get; set; }  // from count attribute on pid element; null for ngram/echo pids
        public int?   ParagraphID { get; set; }  // null if PID not in working copy
    }

    public class WorkOrderBanViewModel
    {
        public string BanType { get; set; }  // word | phrase
        public string Term    { get; set; }
    }

    public class WorkOrderLimitViewModel
    {
        public string LimitType { get; set; }
        public int    Value     { get; set; }
    }
}
