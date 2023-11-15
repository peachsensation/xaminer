using AngleSharp.Common;
using System.Text.Json;
using Xaminer.App.Models;

namespace Xaminer.App
{
    public sealed class UserStore
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            TypeInfoResolver = UserInfoJsonContext.Default
        };

        private static readonly FileInfo s_user = new(Path.Combine(Globals.TempDataDir.FullName, "user.json"));

        private static readonly SemaphoreSlim s_semaphore = new(1, 1);

        private static UserInfo? s_data = null;

        public static async Task<UserInfo> GetData() => await Retrieve();

        public static async Task<Stat> GetStat(ListingId id) => (await GetStats()).FirstOrDefault(x => x.Info.Id == id) ?? GetDefaultStat(id);

        public static async Task<IEnumerable<Stat>> GetStats() => (await Retrieve()).Stats ?? Enumerable.Empty<Stat>();

        public static async Task UpdateData(Func<UserInfo, UserInfo> modify)
        {
            var data = await Retrieve();
            data = modify(data);
            await Store(data);
        }

        public static async Task UpdateStat(Stat stat, Func<Stat, Stat> modify)
        {
            var stats = await GetStats();
            stat = modify(stat);
            stats = UpdateStat(stats, stat);

            var data = (await Retrieve()) with
            {
                Stats = stats
            };

            await Store(data);
        }

        public static async Task UpdateStat(Listing listing, Func<Stat, Stat> modify)
        {
            var stats = await GetStats();

            var stat = stats.FirstOrDefault(x => x.Info.Id == listing.Id) ?? GetDefaultStat(listing.Id);

            stat = stat with
            {
                Info = stat.Info with
                {
                    Description = listing.Description,
                    Url = listing.Images.FirstOrDefault()
                }
            };

            stat = modify(stat);

            stats = UpdateStat(stats, stat);

            var data = (await Retrieve()) with
            {
                Stats = stats
            };

            await Store(data);
        }

        public static async Task RemoveStat(Stat stat)
        {
            var stats = await GetStats();

            var data = (await Retrieve()) with
            {
                Stats = stats.Where(x => x.Info.Id != stat.Info.Id)
            };

            await Store(data);
        }

        private static async Task<UserInfo> Retrieve()
        {
            if (s_data is { } data)
                return data;

            if (s_user.Exists)
            {
                await s_semaphore.WaitAsync();
                try
                {
                    s_data = JsonSerializer.Deserialize<UserInfo>(await File.ReadAllTextAsync(s_user.FullName), s_jsonOptions) ?? GetDefault();
                }
                finally
                {
                    s_semaphore.Release();
                }
            }
            else
            {
                s_data = GetDefault();
            }

            return s_data;
        }

        private static async Task Store(UserInfo data)
        {
            await s_semaphore.WaitAsync();
            try
            {
                await File.WriteAllTextAsync(s_user.FullName, JsonSerializer.Serialize(data, s_jsonOptions));
                s_data = null;
            }
            finally
            {
                s_semaphore.Release();
            }
        }

        private static UserInfo GetDefault() => new
        (
            QueryInfo: new QueryInfo(null, Enumerable.Empty<Query>()),
            Stats: Enumerable.Empty<Stat>(),
            LastUpdateCheck: new LastUpdateCheck
            (
                Date: DateOnly.MinValue,
                Update: null
            )
        );

        private static Stat GetDefaultStat(ListingId id) => new
        (
            Info: new StatInfo
            (
                Id: id,
                Description: "",
                Url: null
            ),
            Online: new OnlineStats
            (
                Visits: 0,
                LastVisited: null,
                Rating: null
            ),
            IRL: new IRLStats
            (
                Rating: null
            ),
            Modified: DateTime.Now
        );

        private static IEnumerable<Stat> UpdateStat(IEnumerable<Stat> stats, Stat stat) => stats
            .Where(x => x.Info.Id != stat.Info.Id)
            .Concat(new List<Stat> { stat with
            {
                Modified = DateTime.Now
            }})
            .OrderByDescending(x => x.Modified)
            .Take(100);
    }
}
