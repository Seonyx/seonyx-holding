using System.Collections.Generic;

namespace Seonyx.Web.Models.ViewModels
{
    public class HomeViewModel
    {
        public string HeroContent { get; set; }
        public List<Division> Divisions { get; set; }

        public HomeViewModel()
        {
            Divisions = new List<Division>();
        }
    }
}
