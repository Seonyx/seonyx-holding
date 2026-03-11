using System;

namespace Seonyx.Web.Models
{
    public class ImportLog
    {
        public int      ImportLogID       { get; set; }
        public int      BookProjectID     { get; set; }
        public DateTime ImportedAt        { get; set; }
        public bool     Success           { get; set; }
        public string   SourceFileName    { get; set; }  // original uploaded zip filename
        public int      DraftNumber       { get; set; }
        public int      ChaptersProcessed { get; set; }
        public int      ParagraphsAdded   { get; set; }
        public int      ParagraphsUpdated { get; set; }
        public int      ParagraphsRemoved { get; set; }
        public int      VersionsRecorded  { get; set; }
        public int      WarningCount      { get; set; }
        public int      ErrorCount        { get; set; }
        public string   FullLog           { get; set; }  // newline-separated warnings and errors

        public virtual BookProject BookProject { get; set; }
    }
}
