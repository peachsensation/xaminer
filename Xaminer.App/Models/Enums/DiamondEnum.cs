using System.ComponentModel.DataAnnotations;

namespace Xaminer.App.Models
{
    public enum DiamondEnum
    {
        [Display(Name = "DiamondUnset", ResourceType = typeof(Strings))]
        Unset = 0,
        [Display(Name = "DiamondPremium", ResourceType = typeof(Strings))]
        Premium,
        [Display(Name = "DiamondExclusive", ResourceType = typeof(Strings))]
        Exclusive
    }
}
