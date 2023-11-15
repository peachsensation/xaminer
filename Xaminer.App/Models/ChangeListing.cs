using System.ComponentModel.DataAnnotations;
using System.Text;
using Xaminer.App.Helpers;

namespace Xaminer.App.Models
{
    public sealed record ChangeListing(Listing Listing, IDictionary<ChangeField, Change> Changes)
    {
        public static ChangeListing FromNewListing(Listing listing)
        {
            return new ChangeListing
            (
                Listing: listing,
                Changes: new Dictionary<ChangeField, Change>
                {
                    { ChangeField.Name, new Change(null, listing.Name, ChangeType.Added) },
                    { ChangeField.Place, new Change(null, listing.Place, ChangeType.Added) },
                    { ChangeField.Diamond, new Change(null, listing.Diamond.NameLocalized, ChangeType.Added) },
                    { ChangeField.Providings, new Change(null, listing.Providings.NameLocalized, ChangeType.Added) }
                }
            );
        }

        public bool IsDeleted => Changes.All(x => x.Value.ChangeType == ChangeType.Removed);
        public bool IsAdded => Changes.All(x => x.Value.ChangeType == ChangeType.Added);

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append(Listing.Id.Name);
            sb.Append(" • ");

            if (IsAdded)
            {
                sb.Append($"<{Strings.NewIncluded}>".ToUpperInvariant());
            }
            else if (IsDeleted)
            {
                sb.Append($"<{Strings.NewExluded}>".ToLowerInvariant());
            }
            else
            {
                for (int i = 0; i < Changes.Count; i++)
                {
                    var change = Changes.ElementAt(i);

                    sb.Append(change.Key);
                    sb.Append(": ");
                    sb.Append(change.Value);

                    if (i != (Changes.Count -1))
                    {
                        sb.Append(' ');
                    }
                }
            }


            const int maxLength = 275;

            var message = sb.ToString();
            var moreSign = message.Length > maxLength ? "..." : "";
            return string.Concat(string.Concat(message.Take(maxLength)), moreSign);
        }
    }

    public sealed record Change(string? OldValue, string? NewValue, ChangeType ChangeType)
    {
        public override string ToString() => $"[{ChangeType.GetDisplayName()} {OldValue ?? "-"} > {NewValue ?? "-"}]";
    }

    public enum ChangeField
    {
        [Display(Name = "Name", ResourceType = typeof(Strings))]
        Name,
        [Display(Name = "Place", ResourceType = typeof(Strings))]
        Place,
        [Display(Name = "Diamond", ResourceType = typeof(Strings))]
        Diamond,
        [Display(Name = "Providings", ResourceType = typeof(Strings))]
        Providings
    }
}
