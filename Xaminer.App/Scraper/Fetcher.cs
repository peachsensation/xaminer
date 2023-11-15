using AngleSharp;
using AngleSharp.Css;
using AngleSharp.Dom;
using System.Collections.Concurrent;
using System.Text;
using Xaminer.App.Interop.CDP;

namespace Xaminer.App.Scraper
{
    public sealed class Fetcher : IDisposable
    {
        private readonly string _url;
        private readonly Browser _browser;
        private readonly IBrowsingContext _context;

        private readonly ConcurrentDictionary<Guid, IDocument> _docsDic = new();

        public Fetcher(Browser browser, string url)
        {
            _url = url;

            var config = Configuration.Default
                .WithDefaultLoader()
                .WithCss()
                .WithRenderDevice(new DefaultRenderDevice
                {
                    DeviceWidth = 1600,
                    DeviceHeight = 1000,
                });
            var context = BrowsingContext.New(config);

            _browser = browser;
            _context = context;
        }

        public async Task<bool> ElementExists(string elementId, CancellationToken token)
        {
            try
            {
                var doc = await Fetch(token);
                return doc.GetElementById(elementId) is not null;
            }
            catch
            {
                return false;
            }
        }

        public async Task<IDocument> Fetch(CancellationToken token) => await Fetch(null, Enumerable.Empty<KeyValuePair<string, string?>>(), token);

        public async Task<IDocument> Fetch(string? path, IEnumerable<KeyValuePair<string, string?>> parameters, CancellationToken token)
        {
            var pathAndQuery = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(path))
            {
                if (!path.StartsWith('/'))
                    pathAndQuery.Append('/');

                pathAndQuery.Append(path);
            }

            var first = true;
            foreach (var pair in parameters)
            {
                pathAndQuery.Append(first ? '?' : '&');
                pathAndQuery.Append(pair.Key);
                pathAndQuery.Append('=');
                if (!string.IsNullOrEmpty(pair.Value))
                    pathAndQuery.Append(Uri.EscapeDataString(pair.Value));

                first = false;
            }

            await using var browserPage = await _browser.CreatePage();
            await browserPage.Navigate($"{_url}{pathAndQuery}");
            await browserPage.WaitForNavigate(token);
            await browserPage.WaitIdle();

            await Task.Delay(250, token);

            var content = await browserPage.GetContent();

            var doc = await _context.OpenAsync(x => x.Content(content), token);
            _docsDic.TryAdd(Guid.NewGuid(), doc);

            return doc;
        }

        public void Dispose()
        {
            foreach (var docDic in _docsDic)
            {
                if (_docsDic.TryRemove(docDic.Key, out var docDicDoc))
                    docDicDoc.Dispose();
            }
            _context.Dispose();
        }
    }
}
