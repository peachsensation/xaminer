using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Xaminer.App.Models
{
    public sealed record ListingId(int Number, string Name)
    {
        [JsonIgnore]
        public Uri PageUrl => new($"{Globals.ScraperBaseUrl}/advertenties/{FullName}");

        [JsonIgnore]
        public string FullName => $"{Number}-{Name}";

        public static ListingId Parse(string fullName)
        {
            ArgumentNullException.ThrowIfNull(fullName, nameof(fullName));

            var split = fullName.Split('-', StringSplitOptions.TrimEntries);

            return new ListingId
            (
                Number: int.Parse(split[0]),
                Name: string.Concat(split[1..])
            );
        }

        public static bool TryParse(string fullName, [NotNullWhen(true)] out ListingId? id)
        {
            try
            {
                id = Parse(fullName);
                return true;
            }
            catch
            {
                id = null;
                return false;
            }
        }

        public bool Equals(ListingId? other) => Number == other?.Number;

        public override int GetHashCode() => HashCode.Combine(Number);

        public override string ToString() => FullName;

        [return: NotNullIfNotNull(nameof(value))]
        public static implicit operator int?(ListingId? value) => value?.Number;
    }
}
