using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xaminer.App.Models;

namespace Xaminer.App
{
    [JsonSerializable(typeof(ListingsInfo))]
    [JsonSerializable(typeof(DiffInfo))]
    public partial class ComparerJsonContext : JsonSerializerContext { }

    public sealed class Comparer
    {
        private const string s_listingsFileName = "listings.json";
        private const string s_diffFileName = "diff.json";

        private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = ComparerJsonContext.Default
        };

        private static readonly DirectoryInfo s_compareDir = new(Path.Combine(Globals.TempDataDir.FullName, "comparer"));
        private static readonly SemaphoreSlim s_semaphore = new(1, 1);

        private readonly DirectoryInfo _queryDir;
        private readonly FileInfo _listingsFile;
        private readonly FileInfo _diffFile;

        public Comparer(Query query)
        {
            _queryDir = new(Path.Combine(s_compareDir.FullName, query.Encoded));
            _queryDir.Create();

            _listingsFile = new FileInfo(Path.Combine(_queryDir.FullName, s_listingsFileName));
            _diffFile = new FileInfo(Path.Combine(_queryDir.FullName, s_diffFileName));
        }

        public async Task<CompareResult> Compare(IEnumerable<Listing> newListings)
        {
            Program.LogConsole<Comparer>($"listings: {newListings.Count()}");

            var oldListings = (await RetrieveListings()).Listings;

            var except = oldListings.Except(newListings).Concat(newListings.Except(oldListings)).DistinctBy(x => x.Id);

            var stats = await UserStore.GetStats();
            var changes = GetChanges(oldListings, newListings, except.Select(x => x.Id), stats);

            await UpdateListings(newListings);

            var result = CompareResult.FromAll(changes);

            await UpdateDiff(result);

            return result;
        }

        public async Task<IEnumerable<ChangeListing>> RetrieveDiff() =>
            (_diffFile.Exists ? await Retrieve<DiffInfo>(_diffFile) ?? GetDefaultDiff() : GetDefaultDiff()).Changes;

        private async Task<ListingsInfo> RetrieveListings() =>
            _listingsFile.Exists ? await Retrieve<ListingsInfo>(_listingsFile) ?? GetDefaultListings() : GetDefaultListings();

        private IEnumerable<ChangeListing> GetChanges(IEnumerable<Listing> oldListings,
                                                      IEnumerable<Listing> newListings,
                                                      IEnumerable<ListingId> exceptIds,
                                                      IEnumerable<Stat> stats)
        {
            var changeListings = new List<ChangeListing>();

            if (!oldListings.Any())
            {
                foreach (var newListing in newListings)
                {
                    changeListings.Add(ChangeListing.FromNewListing(newListing));
                }
            }
            else
            {
                foreach (var exceptId in exceptIds)
                {
                    var oldListing = oldListings.FirstOrDefault(x => x.Id == exceptId);
                    var newListing = newListings.FirstOrDefault(x => x.Id == exceptId);

                    var changes = new Dictionary<ChangeField, Change>();

                    var nameChange = GetStringChange(oldListing?.Name, newListing?.Name);
                    if (nameChange is not null)
                        changes.Add(ChangeField.Name, nameChange);

                    var placeChange = GetStringChange(oldListing?.Place, newListing?.Place);
                    if (placeChange is not null)
                        changes.Add(ChangeField.Place, placeChange);

                    var diamondChange = GetStringChange(oldListing?.Diamond?.NameLocalized, newListing?.Diamond?.NameLocalized);
                    if (diamondChange is not null)
                        changes.Add(ChangeField.Diamond, diamondChange);

                    var providingsChange = GetStringChange(oldListing?.Providings?.NameLocalized, newListing?.Providings?.NameLocalized);
                    if (providingsChange is not null)
                        changes.Add(ChangeField.Providings, providingsChange);

                    changeListings.Add(new ChangeListing
                    (
                        Listing: (newListing ?? oldListing) ?? throw new NotSupportedException(),
                        Changes: changes
                    ));
                }
            }

            return changeListings
                .OrderByDescending(x => x.IsAdded)
                .ThenBy(x => x.IsDeleted)
                .ThenByDescending(x => (stats.FirstOrDefault(y => y.Info.Id == x.Listing.Id)?.AverageRating).GetValueOrDefault() > 0)
                .ThenBy(x => x.Listing.Age);
        }

        private Change? GetStringChange(string? oldListing, string? newListing)
        {
            if (TryChange(oldListing, newListing, out var change))
            {
                return change;
            }

            if (!string.Equals(oldListing, newListing, StringComparison.OrdinalIgnoreCase))
            {
                return new Change(oldListing, newListing, ChangeType.Updated);
            }

            return null;
        }

        private bool TryChange(string? oldValue, string? newValue, [NotNullWhen(true)] out Change? change)
        {
            change = null;

            if (oldValue is null && newValue is null)
            {
                return false;
            }
            else if (oldValue is null && newValue is not null)
            {
                change = new Change(oldValue, newValue, ChangeType.Added);
                return true;
            }
            else if (oldValue is not null && newValue is null)
            {
                change = new Change(oldValue, newValue, ChangeType.Removed);
                return true;
            }

            return false;
        }

        private async Task UpdateListings(IEnumerable<Listing> listings)
        {
            var info = await RetrieveListings();
            info = info with
            {
                Listings = listings
            };

            await Store(_listingsFile, info);

            foreach (var stat in await UserStore.GetStats())
            {
                if (listings.FirstOrDefault(x => x.Id == stat.Info.Id) is { } listing)
                {
                    await UserStore.UpdateStat(listing, (s) => s with
                    {
                        Info = s.Info with
                        {
                            Description = listing.Description
                        }
                    });
                }
            }
        }

        private async Task UpdateDiff(CompareResult result)
        {
            await Store(_diffFile, new DiffInfo
            (
                Changes: result.All
            ));
        }

        private async Task Store<T>(FileInfo file, T value)
        {
            await s_semaphore.WaitAsync();
            try
            {
                await File.WriteAllTextAsync(file.FullName, JsonSerializer.Serialize(value, s_jsonOptions));
            }
            finally
            {
                s_semaphore.Release();
            }
        }

        private async Task<T?> Retrieve<T>(FileInfo file)
        {
            await s_semaphore.WaitAsync();
            try
            {
                return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(file.FullName), s_jsonOptions);
            }
            finally
            {
                s_semaphore.Release();
            }
        }

        private static ListingsInfo GetDefaultListings() => new ListingsInfo
        (
            Listings: Enumerable.Empty<Listing>()
        );

        private static DiffInfo GetDefaultDiff() => new DiffInfo
        (
            Changes: Enumerable.Empty<ChangeListing>()
        );
    }

    public sealed record DiffInfo(IEnumerable<ChangeListing> Changes)
    {
        public int Version { get; } = 1;
    }
}
