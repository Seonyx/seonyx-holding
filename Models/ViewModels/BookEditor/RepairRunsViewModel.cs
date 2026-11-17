using System.Collections.Generic;
using Seonyx.Web.Services;

namespace Seonyx.Web.Models.ViewModels.BookEditor
{
    public class RepairRunsViewModel
    {
        // False: read-only preview. True: results after the POST ran.
        public bool IsResult { get; set; }

        // Set when the POST stopped at its time budget before finishing.
        public bool StoppedEarly { get; set; }

        public RepairPassSummary PassA { get; set; }
        public RepairPassSummary PassB { get; set; }
        public int RowLimit { get; set; }
    }

    public class RepairPassSummary
    {
        // Preview: paragraphs that would change. Result: paragraphs changed.
        public int Count { get; set; }
        public List<RepairParagraphRow> Rows { get; set; }
        public List<string> Warnings { get; set; }

        // Pass B: paragraphs that would convert but failed the stricter
        // safety check, so they are left untouched for a manual look.
        public int SkippedCount { get; set; }
        public List<RepairParagraphRow> SkippedRows { get; set; }

        public RepairPassSummary()
        {
            Rows = new List<RepairParagraphRow>();
            Warnings = new List<string>();
            SkippedRows = new List<RepairParagraphRow>();
        }
    }

    public class RepairParagraphRow
    {
        public int ProjectID { get; set; }
        public int ParagraphID { get; set; }
        public string Pid { get; set; }
        public string Before { get; set; }
        public IList<InlineRun> After { get; set; }

        // Conversion leaves formatting on text with no letters or digits
        // (e.g. "* * *" becoming an italic space) -- worth a look.
        public bool Flagged { get; set; }

        // Why a skipped Pass B paragraph was left untouched.
        public string SkipReason { get; set; }
    }
}
