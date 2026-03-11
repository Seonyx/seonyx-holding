using System.Collections.Generic;

namespace Seonyx.Web.Models.ViewModels.BookEditor
{
    public class ExportViewModel
    {
        public int BookProjectID { get; set; }
        public string ProjectName { get; set; }
        public List<ChapterExportItem> Chapters { get; set; }

        public ExportViewModel()
        {
            Chapters = new List<ChapterExportItem>();
        }
    }

    public class ChapterExportItem
    {
        public int ChapterID { get; set; }
        public int ChapterNumber { get; set; }
        public string ChapterTitle { get; set; }
        public int ParagraphCount { get; set; }
    }
}
