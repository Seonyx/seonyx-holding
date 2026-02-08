using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("SiteSettings")]
    public class SiteSetting
    {
        [Key]
        [StringLength(100)]
        public string SettingKey { get; set; }

        public string SettingValue { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public DateTime ModifiedDate { get; set; }

        public SiteSetting()
        {
            ModifiedDate = DateTime.Now;
        }
    }
}
