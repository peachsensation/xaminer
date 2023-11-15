using System.ComponentModel.DataAnnotations;

namespace Xaminer.App.Models
{
    public enum ChangeType
    {
        [Display(Name = "ChangeNone", ResourceType = typeof(Strings))]
        None = 1,
        [Display(Name = "ChangeAdded", ResourceType = typeof(Strings))]
        Added,
        [Display(Name = "ChangeRemoved", ResourceType = typeof(Strings))]
        Removed,
        [Display(Name = "ChangeUpdated", ResourceType = typeof(Strings))]
        Updated
    }
}
