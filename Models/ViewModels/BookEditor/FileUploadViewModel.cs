using System;
using System.Collections.Generic;
using System.Web;

namespace Seonyx.Web.Models.ViewModels.BookEditor
{
    public class FileUploadViewModel
    {
        public int BookProjectID { get; set; }
        public string ProjectName { get; set; }
        public string CoverImagePath { get; set; }
        public List<UploadedFileInfo> ExistingFiles { get; set; }
        public string ImportMessage { get; set; }
        public bool HasImportErrors { get; set; }

        // BookML package status
        public bool BookmlPackagePresent { get; set; }
        public string BookmlPackagePath { get; set; }

        public FileUploadViewModel()
        {
            ExistingFiles = new List<UploadedFileInfo>();
        }
    }

    public class UploadedFileInfo
    {
        public string FileName { get; set; }
        public string FileType { get; set; }
        public long FileSize { get; set; }
        public DateTime UploadDate { get; set; }
    }
}
