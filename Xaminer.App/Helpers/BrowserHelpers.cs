using AngleSharp;
using AngleSharp.Dom;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Xaminer.App.Interop.CDP;
using Xaminer.App.Pages;

namespace Xaminer.App.Helpers
{
    public static class BrowserHelpers
    {
        public static bool TryGetNavigationValue(this Page page, string key, out string value)
        {
            value = string.Empty;
            var url = page.Url;

            var split = url?.Split('#', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (split?.Length != 2)
                return false;

            var query = split.Last();

            var keyValues = query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var keyValue = keyValues.FirstOrDefault(x => x.StartsWith(key, StringComparison.OrdinalIgnoreCase));
            if (keyValue is null)
                return false;

            split = keyValue.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (!string.Equals(split.First(), key, StringComparison.OrdinalIgnoreCase))
                return false;

            value = UnescapeUrl(split.LastOrDefault() ?? string.Empty);
            return true;
        }

        public static async Task SetNavigationValue(this Page page, IEnumerable<KeyValuePair<string, string?>> parameters)
        {
            var query = new StringBuilder();

            if (!parameters.Any())
                return;

            var first = true;

            foreach (var pair in parameters)
            {
                query.Append(first ? '#' : '&');
                query.Append(pair.Key);
                query.Append('=');
                if (!string.IsNullOrEmpty(pair.Value))
                    query.Append(pair.Value);

                first = false;
            }

            await page.Navigate($"about:blank{query}");
        }

        public static async Task SetContent(this Page page, IDocument doc)
        {
            await using var stream = new MemoryStream();
            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            await doc.ToHtmlAsync(writer);
            await writer.FlushAsync();

            stream.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            var html = await reader.ReadToEndAsync();
            await page.SetContent(html);
        }

        public static void NavigateAfterDelay(this Page page, [StringSyntax(StringSyntaxAttribute.Uri)] string url, TimeSpan timeSpan)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(timeSpan);
                await page.Navigate(url);
            });
        }

        public static async Task SetContentAndWait(this Page page, IDocument doc, CancellationToken token)
        {
            await page.SetContent(doc);
            await page.WaitForNavigate(token);
            await page.Reload();
        }

        public static void AddDefaults(this IDocument doc)
        {
            doc.AddDarkMode();
            doc.AddDefaultStyle();
        }

        public static string UnescapeUrl(this string url) => Uri.UnescapeDataString(url);

        public static string EscapeUrl(this Uri url) => Uri.EscapeDataString(url.ToString());

        public static void OpenBrowser(this Uri url, string browser) => Process.Start(browser, string.Concat("\"", url.ToString(), "\""));

        public static async Task<IDocument> GetDocument(string? title = null)
        {
            var context = BrowsingContext.New();
            var document = await context.OpenNewAsync();
            return document.Implementation.CreateHtmlDocument(title ?? "");
        }

        public static async Task ShowPage(Page page, IContent content)
        {
            await content.BeforeContent(page);
            using var doc = await content.GetDocument();
            await page.SetContent(doc);
        }

        public static async Task ShowInteractivePage(Page page, IContent content, CancellationToken token)
        {
            await content.BeforeContent(page);
            using var doc = await content.GetDocument();
            await page.SetContentAndWait(doc, token);

            if (await content.AfterNavigation(page) is { } reopen)
            {
                await ShowInteractivePage(page, reopen, token);
            }
        }

        public static async Task<T?> ShowInteractivePage<T>(Page page, IContent<T> content, CancellationToken token)
        {
            using var doc = await content.GetDocument();

            await content.BeforeContent(page);
            await page.SetContentAndWait(doc, token);

            if (await content.AfterNavigation(page) is { } result)
            {
                return result;
            }

            return default;
        }
    }
}
