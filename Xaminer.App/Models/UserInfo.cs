using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Xaminer.App.Models
{
    [JsonSerializable(typeof(UserInfo))]
    [JsonSerializable(typeof(IEnumerable<UserInfo>))]
    [JsonSerializable(typeof(IOrderedEnumerable<UserInfo>))]
    [JsonSerializable(typeof(List<UserInfo>))]
    public partial class UserInfoJsonContext : JsonSerializerContext { }

    public sealed record UserInfo(QueryInfo QueryInfo, IEnumerable<Stat> Stats, LastUpdateCheck LastUpdateCheck)
    {
        public int Version { get; } = 1;
    }

    public sealed record Stat(StatInfo Info, OnlineStats Online, IRLStats IRL, DateTime Modified)
    {
        [JsonIgnore]
        public int AverageRating
        {
            get
            {
                var sum = Online.Rating.GetValueOrDefault() + IRL.Rating.GetValueOrDefault();
                return Online.Rating is not null && IRL.Rating is not null ? sum / 2 : sum;
            }
        }
    }

    public sealed record StatInfo(ListingId Id, string Description, Uri? Url);

    public sealed record OnlineStats(int Visits, DateTime? LastVisited, int? Rating) : RatingBase(Rating);

    public sealed record IRLStats(int? Rating) : RatingBase(Rating);

    public record RatingBase(int? Rating);

    public sealed record QueryInfo(Query? LastUsedQuery, IEnumerable<Query> Queries);

    public sealed partial record Query(string Plain)
    {
        [GeneratedRegex("""[<>:""/\|?*]""", RegexOptions.IgnoreCase, 1000 * 5)]
        private static partial Regex InvalidFileNameCharsRegex();

        [JsonIgnore]
        public string Encoded
        {
            get
            {
                var query = InvalidFileNameCharsRegex().Replace(Plain, string.Empty);
                if (string.IsNullOrWhiteSpace(query))
                    throw new NotSupportedException(nameof(query));

                return query;
            }
        }

        public override string ToString() => Plain;

        [return: NotNullIfNotNull(nameof(value))]
        public static implicit operator Query?(string? value) => value is not null ? new Query(value) : null;

        [return: NotNullIfNotNull(nameof(value))]
        public static implicit operator string?(Query? value) => value?.Plain;
    }

    public sealed record LastUpdateCheck(DateOnly Date, UpdateInfo? Update)
    {
        [JsonIgnore]
        public bool IsUpdateAvailable => Update is not null;

        public static async Task<LastUpdateCheck> FromAvailable(UpdateInfo update)
        {
            var lastUpdateCheck = new LastUpdateCheck
            (
                Date: DateOnly.FromDateTime(DateTime.Now),
                Update: update
            );

            await UserStore.UpdateData((data) => data with
            {
                LastUpdateCheck = lastUpdateCheck
            });

            return lastUpdateCheck;
        }

        public static LastUpdateCheck FromNotAvailable() => new
        (
            Date: DateOnly.FromDateTime(DateTime.Now),
            Update: null
        );
    }

    public sealed record UpdateInfo(string Body, Version Version);  
}
