using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using System.Runtime.CompilerServices;

namespace Xaminer.App.Scraper
{
    public sealed class PaginationContext
    {
        private readonly Fetcher _fetcher;

        public PaginationContext(Fetcher fetcher)
        {
            _fetcher = fetcher;
        }

        public async IAsyncEnumerable<IDocument> GetAllPages(string query, [EnumeratorCancellation] CancellationToken token)
        {
            var firstPage = await GetPageDoc(query, page: null, token);

            int? pagesCount = null;

            var pagination = firstPage.QuerySelector<IHtmlUnorderedListElement>(".pagination");
            if (pagination is not null)
            {
                pagesCount = pagination.Descendents<IHtmlAnchorElement>()
                    .Select(x =>
                    {
                        if (int.TryParse(x.GetAttribute("data-page"), out var page))
                            return page;

                        return 0;
                    })
                    .Max();
            }

            yield return firstPage;

            if (pagesCount is not int pages)
            {
                yield break;
            }

            Program.LogConsole<PaginationContext>($"Pages: {pages}");

            foreach (var pagesChunk in Enumerable.Range(2, pages -1).Chunk(5))
            {
                var tasks = new List<Task<IDocument>>();

                foreach (var page in pagesChunk)
                {
                    var pagePinnned = page;
                    tasks.Add(Task.Run(() => GetPageDoc(query, pagePinnned, token)));
                }

                var count = tasks.Count;

                while (count > 0)
                {
                    if (token.IsCancellationRequested)
                        break;

                    var docTask = await Task.WhenAny(tasks);

                    yield return await docTask;

                    Interlocked.Decrement(ref count);
                }
            }
        }

        private async Task<IDocument> GetPageDoc(string query, int? page, CancellationToken token)
        {
            var content = await _fetcher.Fetch(null, new List<KeyValuePair<string, string?>>
            {
                new("q", query),
                new("page", page?.ToString())
            }, token);
            return content;
        }
    }
}
