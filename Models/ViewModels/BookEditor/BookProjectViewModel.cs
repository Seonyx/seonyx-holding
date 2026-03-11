using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Seonyx.Web.Models.ViewModels.BookEditor
{
    public class BookProjectViewModel
    {
        public int BookProjectID { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Project Name")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-]+$", ErrorMessage = "Project name can only contain letters, numbers, spaces, and hyphens.")]
        public string ProjectName { get; set; }

        public string CoverImagePath { get; set; }
        public string FolderPath { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public bool IsActive { get; set; }
        public int TotalChapters { get; set; }
        public int TotalParagraphs { get; set; }
        public int CurrentDraftNumber { get; set; }
    }

    public class BookProjectListViewModel
    {
        public List<BookProjectViewModel> Projects { get; set; }

        public BookProjectListViewModel()
        {
            Projects = new List<BookProjectViewModel>();
        }
    }
}
