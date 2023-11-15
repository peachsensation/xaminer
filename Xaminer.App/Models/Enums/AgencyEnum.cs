using System.ComponentModel.DataAnnotations;

namespace Xaminer.App.Models
{
    public enum AgencyEnum
    {
        [Display(Name = "Agency", ResourceType = typeof(Strings))]
        Agency,
        [Display(Name = "Virtual", ResourceType = typeof(Strings))]
        Virtual
    }
}
