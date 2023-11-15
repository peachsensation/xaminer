using System.ComponentModel.DataAnnotations;

namespace Xaminer.App.Models
{
    public enum SelectGender
    {
        [Display(Name = "Females", ResourceType = typeof(Strings))]
        Females,
        [Display(Name = "Males", ResourceType = typeof(Strings))]
        Males,
        [Display(Name = "Shemales", ResourceType = typeof(Strings))]
        Shemales,
        [Display(Name = "Couples", ResourceType = typeof(Strings))]
        Couples
    }
}