using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace Seonyx.Web.Models.ViewModels.Admin
{
    public class AdminPageViewModel
    {
        public int PageId { get; set; }

        [Required]
        [StringLength(200)]
        public string Slug { get; set; }

        [Required]
        [StringLength(500)]
        public string Title { get; set; }

        [StringLength(500)]
        public string MetaDescription { get; set; }

        [StringLength(500)]
        public string MetaKeywords { get; set; }

        [Required]
        [AllowHtml]
        public string Content { get; set; }

        public int? ParentPageId { get; set; }

        public int? DivisionId { get; set; }

        public int SortOrder { get; set; }

        public bool IsPublished { get; set; }

        public bool ShowInNavigation { get; set; }

        public List<SelectListItem> ParentPages { get; set; }
        public List<SelectListItem> Divisions { get; set; }

        public AdminPageViewModel()
        {
            IsPublished = true;
            ShowInNavigation = true;
            ParentPages = new List<SelectListItem>();
            Divisions = new List<SelectListItem>();
        }
    }
}
