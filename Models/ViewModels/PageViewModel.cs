using System.Collections.Generic;

namespace Seonyx.Web.Models.ViewModels
{
    public class PageViewModel
    {
        public Page Page { get; set; }
        public Division Division { get; set; }
        public List<Page> Breadcrumbs { get; set; }
        public List<Page> SiblingPages { get; set; }

        public PageViewModel()
        {
            Breadcrumbs = new List<Page>();
            SiblingPages = new List<Page>();
        }
    }
}
