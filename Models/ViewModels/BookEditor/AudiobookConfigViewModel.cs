using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Seonyx.Web.Services;

namespace Seonyx.Web.Models.ViewModels.BookEditor
{
    public class AudiobookConfigViewModel
    {
        public int    BookProjectID { get; set; }
        public string ProjectName   { get; set; }
        public string Author        { get; set; }

        [Required(ErrorMessage = "Please select a voice.")]
        public string SelectedVoiceId { get; set; }

        public List<VoiceInfo> Voices { get; set; }

        [Required(ErrorMessage = "Please select a draft.")]
        public int SelectedDraftNumber { get; set; }

        public List<DraftOption> AvailableDrafts { get; set; }
    }

    public class DraftOption
    {
        public int    DraftNumber { get; set; }
        public string Label       { get; set; }   // e.g. "Draft 3 (current)" or "Draft 2 - snapshot"
    }
}
