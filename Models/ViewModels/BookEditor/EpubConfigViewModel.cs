using System.ComponentModel.DataAnnotations;

namespace Seonyx.Web.Models.ViewModels.BookEditor
{
    public enum EpubCoverOption
    {
        UseExisting  = 0,
        UseGenerated = 1,
        Upload       = 2
    }

    public class EpubConfigViewModel
    {
        public int    BookProjectID   { get; set; }
        public string ProjectName     { get; set; }
        public bool   HasExistingCover { get; set; }

        [Required(ErrorMessage = "Rights holder is required.")]
        [StringLength(200)]
        [Display(Name = "Rights holder")]
        public string RightsHolder  { get; set; }

        [Required]
        [Range(1900, 2100)]
        [Display(Name = "Copyright year")]
        public int    CopyrightYear { get; set; }

        [Display(Name = "Include ARC disclaimer")]
        public bool   ArcDisclaimer { get; set; }

        public EpubCoverOption CoverOption { get; set; }
    }
}
