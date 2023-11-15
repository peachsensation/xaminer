using System.ComponentModel.DataAnnotations;

namespace Xaminer.App.Models
{
    public enum GenderEnum
    {
        [Display(Name = "Unkown", ResourceType = typeof(Strings))]
        Unknown = 0,
        [Display(Name = "Female", ResourceType = typeof(Strings))]
        Female,
        [Display(Name = "Male", ResourceType = typeof(Strings))]
        Male,
        [Display(Name = "Shemale", ResourceType = typeof(Strings))]
        Shemale,
        [Display(Name = "Couple", ResourceType = typeof(Strings))]
        Couple
    }
}