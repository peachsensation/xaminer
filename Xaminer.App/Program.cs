using System.Diagnostics;
using Xaminer.App.Helpers;
using Xaminer.App.Interop;
using Xaminer.App.Interop.CDP;
using Xaminer.App.Models;
using Xaminer.App.Pages;
using Xaminer.App.Scraper;

namespace Xaminer.App
{
    internal class Program
    {
        public static CancellationToken AppToken => s_appCts.Token;
   
        public static void Exit() => s_appCts.Cancel();
        public static void LogConsole<T>(Exception ex) => LogConsole<T>(ex.Message);
        public static void LogConsole<T>(string message) => Console.WriteLine($"[{typeof(T).Name}] {message}");

        private static readonly CancellationTokenSource s_appCts = new();
        private static readonly Mutex s_mutex = new(false, typeof(Program).Assembly.GetName().Name);

        public static async Task Main(string[] args)
        {
            if (!s_mutex.WaitOne(TimeSpan.Zero, exitContext: true))
                return;

            ShowDebugConsole();

            ConsoleInterop.SetConsoleCtrlHandler(new ConsoleInterop.ConsoleCtrlDelegate((x) =>
            {
                Exit();

                return true;
            }), true);

            await using var browser = await Browser.Create(headless: false);

            browser.OnConnectionClosed += (sender, ex) => Exit();
            browser.OnClosed += (sender, pid) => Exit();

            var appPage = (await browser.GetPages()).First();

            appPage.NavigateAfterDelay("about:blank#p=home", TimeSpan.FromMilliseconds(50));

            while (!AppToken.IsCancellationRequested)
            {
                try
                {
                    await foreach (var url in appPage.WaitAllForNavigate(AppToken))
                    {
                        if (appPage.TryGetNavigationValue("p", out var page))
                        {
                            if (string.Equals(page, "home", StringComparison.OrdinalIgnoreCase))
                            {
                                await BrowserHelpers.ShowPage(appPage, new LoadingPage());
                                var lastUsedQuery = (await UserStore.GetData()).QueryInfo.LastUsedQuery;
                                if (await BrowserHelpers.ShowInteractivePage(appPage, new QueryEntryPage(lastUsedQuery), AppToken) is not { } query)
                                {
                                    break;
                                }
                                else if (query == string.Empty)
                                {
                                    appPage.NavigateAfterDelay("about:blank#p=home", TimeSpan.FromMilliseconds(50));
                                    break;
                                }

                                await appPage.SetNavigationValue(new List<KeyValuePair<string, string?>>
                                {
                                    new("p", "overview"),
                                    new("query", query.Plain),
                                });
                            }
                            else if (string.Equals(page, "overview", StringComparison.OrdinalIgnoreCase))
                            {
                                await BrowserHelpers.ShowPage(appPage, new LoadingPage());

                                CompareResult? result = null;
                                if (appPage.TryGetNavigationValue("query", out var query))
                                {
                                    using var fetcher = new Fetcher(browser, Globals.ScraperBaseUrl);
                                    var paging = new PaginationContext(fetcher);
                                    var listings = await Parser.ParseListings(paging.GetAllPages(query, AppToken));

                                    var comparer = new Comparer(query);
                                    result = await comparer.Compare(listings.DistinctBy(x => x.Id));
                                }
                                else if (appPage.TryGetNavigationValue("use", out var use))
                                {
                                    var comparer = new Comparer(use);
                                    var changes = await comparer.RetrieveDiff();
                                    result = CompareResult.FromAll(changes);
                                }

                                if (result is not null)
                                    await BrowserHelpers.ShowInteractivePage(appPage, new OverviewPage(result, new OverviewQuery(Gender: SelectGender.Females)), AppToken);
                            }
                            else if (string.Equals(page, "favs", StringComparison.OrdinalIgnoreCase))
                            {
                                var data = await UserStore.GetData();
                                await BrowserHelpers.ShowInteractivePage(appPage, new FavsPage(data.Stats), AppToken);
                            }
                            else if (string.Equals(page, "about", StringComparison.OrdinalIgnoreCase))
                            {
                                await BrowserHelpers.ShowInteractivePage(appPage, new AboutPage(new AboutQuery()), AppToken);
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogConsole<Program>(ex);
                    await BrowserHelpers.ShowInteractivePage(appPage, new ErrorPage(ex), AppToken);
                    appPage.NavigateAfterDelay("about:blank#p=home", TimeSpan.FromMilliseconds(50));
                }
            }
        }

        [Conditional("DEBUG")]
        private static void ShowDebugConsole() => ConsoleInterop.AllocConsole();
    }
}