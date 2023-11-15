using System.ComponentModel.DataAnnotations;

namespace Xaminer.App.Models
{
    public enum NumberType
    {
        [Display(Name = "Unkown", ResourceType = typeof(Strings))]
        Unknown,
        [Display(Name = "FixedLine", ResourceType = typeof(Strings))]
        Fixed,
        [Display(Name = "Mobile", ResourceType = typeof(Strings))]
        Mobile
    }
}