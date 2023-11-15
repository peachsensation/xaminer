using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Xaminer.App.Helpers;
using Xaminer.App.Interop.CDP;
using Xaminer.App.Models;

namespace Xaminer.App.Pages
{
    public class QueryEntryPage(string? defaultQuery = null) : IContent<Query>
    {
        public Task BeforeContent(Page page) => Task.CompletedTask;

        public async Task<IDocument> GetDocument()
        {
            var doc = await BrowserHelpers.GetDocument(title: Strings.Search);

            doc.AddDefaults();
            await doc.AddHeader();
            doc.AddContent();

            var css = doc.TextN(
"""
.main {
	width: 100%;
}

.search {
    height: 75px;
    display: flex;
    flex-direction: row;
    align-items: center;
}
""");
            doc.AppendStyle(css);

            var body = doc.CreateElement<IHtmlBodyElement>(); // body

            var div = doc.CreateElement<IHtmlDivElement>(); // div
            div.ClassList.Add("main");

            var search = await CreateSearch(doc);
            div.AppendChild(search);

            var lastResult = await CreateLastResult(doc);
            div.AppendChild(lastResult);

            body.AppendChild(div);

            body.AppendIntoContent(doc);

            var js =
"""
document.getElementById("btnStart").onclick = function() {
    const value = document.getElementById("places").value;

    let result;
    for (let place of document.getElementById("places-list").children) {
        if (place.value.localeCompare(value, undefined, { sensitivity: "base" }) == 0) {
            result = place.value;
            break;
       }
    }

    location.href = "#query=" + result;
}
""";
            var script = doc.CreateElement<IHtmlScriptElement>(); // script
            script.TextContent = js;

            doc.DocumentElement.AppendChild(script);

            return doc;
        }

        public async Task<Query?> AfterNavigation(Page page)
        {
            if (!page.TryGetNavigationValue("query", out var value))
            {
                return null;
            }

            var place = Places.Enumerable.FirstOrDefault(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));

            if (place is null)
            {
                return string.Empty;
            }

            var query = new Query(place);

            await UserStore.UpdateData((data) => data with
            {
                QueryInfo = data.QueryInfo with
                {
                    LastUsedQuery = query.Plain,
                    Queries = data.QueryInfo.Queries.Union(new List<Query> { query })
                }
            });

            return query;
        }

        private async Task<IHtmlDivElement> CreateSearch(IDocument doc)
        {
            var div = doc.CreateElement<IHtmlDivElement>(); // div
            div.ClassList.Add("search");

            var label = doc.CreateElement<IHtmlLabelElement>(); // label
            label.SetAttribute("for", "places");
            label.TextContent = $"{Strings.Query}:";
            div.Append(label);

            var input = doc.CreateElement<IHtmlInputElement>(); // input
            input.Id = "places";
            input.Type = "text";
            input.Name = "places";
            input.SetAttribute("list", "places-list");
            input.DefaultValue = defaultQuery ?? "";
            div.Append(input);

            var datalist = doc.CreateElement<IHtmlDataListElement>(); // datalist
            datalist.Id = "places-list";

            var options = new List<IHtmlOptionElement>();

            foreach (var placesChucks in Places.Enumerable.Chunk(Environment.ProcessorCount < 8 ? 8 : Environment.ProcessorCount))
            {
                var tasks = new List<Task>();

                foreach (var place in placesChucks)
                {
                    var placePinned = place;
                    tasks.Add(Task.Run(() =>
                    {
                        var option = doc.CreateElement<IHtmlOptionElement>(); // option
                        option.TextContent = placePinned;

                        options.Add(option);
                    }));
                }

                var count = tasks.Count;

                while (count > 0)
                {
                    var task = await Task.WhenAny(tasks);

                    Interlocked.Decrement(ref count);
                }
            }

            datalist.AppendNodes(options.ToArray());

            div.Append(datalist);

            var button = doc.CreateElement<IHtmlButtonElement>(); // button
            button.Id = "btnStart";
            button.Type = "button";
            button.TextContent = Strings.Start;
            div.Append(button);

            return div;
        }

        private async Task<IHtmlDivElement> CreateLastResult(IDocument doc)
        {
            var div = doc.CreateElement<IHtmlDivElement>(); // div

            if ((await UserStore.GetData()).QueryInfo.LastUsedQuery is { } lastUsedQuery)
            {
                div.Append(doc.CreateElement<IHtmlHrElement>() /*hr*/);

                var comparer = new Comparer(lastUsedQuery);
                var changes = await comparer.RetrieveDiff();

                var aLastDiff = doc.CreateElement<IHtmlAnchorElement>(); // a
                aLastDiff.ClassList.Add("left");
                aLastDiff.Href = $"#p=overview&use={lastUsedQuery}";
                aLastDiff.TextContent = string.Format(Strings.LastResult, lastUsedQuery, changes.Count());
                div.Append(aLastDiff);
            }

            return div;
        }
    }
}
