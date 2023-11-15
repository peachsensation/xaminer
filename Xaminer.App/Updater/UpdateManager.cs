using System.Reflection;
using Xaminer.App.Models;

namespace Xaminer.App.Updater
{
    public static class UpdateManager
    {
        public static bool IsUpdating { get; private set; }
        public static IList<string> UpdateErrors { get; private set; } = new List<string>();
        public static int Progress { get; private set; }

        public static readonly Version ApplicationVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

        private static readonly AppUpdater s_updater = new();

        public static async Task<LastUpdateCheck> IsUpdateAvailable()
        {
            var lastUpdateCheck = (await UserStore.GetData()).LastUpdateCheck;

            if (IsUpdating)
                return lastUpdateCheck;

            if (lastUpdateCheck.Date >= DateOnly.FromDateTime(DateTime.Now))
                return lastUpdateCheck;

            var latestRelease = await s_updater.GetLatestRelease(CancellationToken.None);

            if (!Version.TryParse(latestRelease?.TagName, out var version))
                return lastUpdateCheck;

            var isUpdateAvailable = version > ApplicationVersion;

            if (isUpdateAvailable)
            {
                var update = new UpdateInfo
                (
                    Body:
$"""
{latestRelease.Name}

{latestRelease.Body ?? $"<{Strings.Empy}>"}

{latestRelease.HtmlUrl}
""",
                    Version: version
                );

                return await LastUpdateCheck.FromAvailable(update);
            }

            return LastUpdateCheck.FromNotAvailable();
        }

        public static async Task DoUpdate()
        {
            if (IsUpdating)
                return;

            IsUpdating = true;
            UpdateErrors = new List<string>();
            Progress = 0;

            var progress = new Progress<int>((value) => Progress = value);

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(Program.AppToken);
                cts.CancelAfter(TimeSpan.FromMinutes(15));

                var updateFile = await s_updater.PrepareUpdate(progress, cts.Token);

                s_updater.ExecuteUpdate(updateFile);
                Program.Exit();
            }
            catch (Exception ex)
            {
                UpdateErrors.Add(ex.Message);
                await UserStore.UpdateData((data) => data with
                {
                    LastUpdateCheck = new LastUpdateCheck
                    (
                        Date: DateOnly.MinValue,
                        Update: null
                    )
                });
            }
        }
    }
}
