using System.ComponentModel.DataAnnotations;

namespace Xaminer.App.Models
{
    [Flags]
    public enum ProvidingEnum
    {
        [Display(Name = "Home", ResourceType = typeof(Strings))]
        Home = 1,
        [Display(Name = "Escort", ResourceType = typeof(Strings))]
        Escort = 2,
        [Display(Name = "VirtualSex", ResourceType = typeof(Strings))]
        VirtualSex = 4,
        [Display(Name = "EroticMassage", ResourceType = typeof(Strings))]
        EroticMassage = 8,
        [Display(Name = "BDSM", ResourceType = typeof(Strings))]
        BDSM = 16,
        [Display(Name = "RedLights", ResourceType = typeof(Strings))]
        RedLights = 32,
        [Display(Name = "MassageClub", ResourceType = typeof(Strings))]
        MassageClub = 64,
        [Display(Name = "Cinema", ResourceType = typeof(Strings))]
        Cinema = 128
    }
}