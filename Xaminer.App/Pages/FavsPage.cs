using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Xaminer.App.Helpers;
using Xaminer.App.Interop.CDP;
using Xaminer.App.Models;
using ASH = Xaminer.App.Helpers.AngleSharpHelpers;

namespace Xaminer.App.Pages
{
    public sealed class FavsPage(IEnumerable<Stat> stats) : IContent
    {
        public async Task BeforeContent(Page page)
        {
            await page.SetNavigationValue(new List<KeyValuePair<string, string?>>
            {
                new("p", "favs")
            });
        }

        public async Task<IDocument> GetDocument()
        {
            var doc = await BrowserHelpers.GetDocument(title: Strings.Favorites);
            doc.AddDefaults();
            await doc.AddHeader();
            doc.AddContent(flexDirection: "column");

            var body = doc.CreateElement<IHtmlBodyElement>(); // body

            foreach (var stat in stats
                .Where(x => x.AverageRating > 0)
                .OrderByDescending(x => x.AverageRating)
                .ThenByDescending(x => x.Online.LastVisited))
            {
                var table = doc.CreateElement<IHtmlTableElement>(); // table

                var tableBody = table.CreateBody();

                var removeBtn = ASH.CreateSvgBtn(doc, s_removeSvg, $$"""updateQueryParams({id: "{{stat.Info.Id}}", delete: true})""");
                removeBtn.Title = Strings.Delete;
                removeBtn.SetStyle("width: 30px; height: 30px");

                ASH.AppendTR(doc, tableBody, new object[] { TableNode.FromNode(doc.TextN($"{Strings.Description}:")).SetWidth(25), doc.TextN(stat.Info.Description), removeBtn });

                var img = doc.CreateElement<IHtmlImageElement>(); // img
                img.Source = stat.Info.Url?.ToString();
                img.SetAttribute("loading", "lazy");

                ASH.AppendTR(doc, tableBody, new object[] { doc.TextN($"{Strings.ProfileImage}:"), TableNode.FromNode(img).SetColSpan(2) });

                var linkNode = ASH.CreatePageUrlNode(doc, stat.Info.Id);
                ASH.AppendTR(doc, tableBody, new object[] { doc.TextN($"{Strings.PageUrl}:"), TableNode.FromNode(linkNode).SetColSpan(2) });

                body.Append(table);

                var statsOnlineTable = ASH.CreateStatsOnlineTable(doc, stat);
                body.Append(statsOnlineTable);

                var statsIRLTable = ASH.CreateStatsIRLTable(doc, stat);
                body.Append(statsIRLTable);

                body.Append(doc.CreateElement<IHtmlHrElement>() /*hr*/);
            }

            body.AppendIntoContent(doc);

            return doc;
        }

        public async Task<IContent?> AfterNavigation(Page page)
        {
            var result = false;

            if (page.TryGetNavigationValue("id", out var idValue) && ListingId.TryParse(idValue, out var id) &&
                GetStatById(id) is { } stat)
            {
                if ((page.TryGetNavigationValue("onlineRating", out var ratingValue) || page.TryGetNavigationValue("irlRating", out ratingValue)) &&
                    int.TryParse(ratingValue, out var newRating))
                {
                    await UserStore.UpdateStat(stat, (stat) =>
                    {
                        if (page.TryGetNavigationValue("onlineRating", out var ratingValue))
                        {
                            return stat with
                            {
                                Online = stat.Online with
                                {
                                    Rating = newRating
                                }
                            };
                        }
                        else
                        {
                            return stat with
                            {
                                IRL = stat.IRL with
                                {
                                    Rating = newRating
                                }
                            };
                        }
                    });
                }

                if (page.TryGetNavigationValue("browser", out var browserValue) && int.TryParse(browserValue, out var browser))
                {
                    if (!AppBrowserHelpers.TryOpen((AppBrowser)browser, stat.Info.Id.PageUrl))
                    {
                        var newPage = await page.Browser.CreatePage();
                        await newPage.Navigate(stat.Info.Id.PageUrl.ToString());
                    }

                    await UserStore.UpdateStat(stat, (stat) => stat with
                    {
                        Online = stat.Online with
                        {
                            Visits = stat.Online.Visits +1,
                            LastVisited = DateTime.Now,
                        }
                    });
                }

                if (page.TryGetNavigationValue("delete", out var deleteValue) && bool.TryParse(deleteValue, out var delete))
                {
                    await UserStore.RemoveStat(stat);
                }

                result = true;
            }

            return result ? new FavsPage(await UserStore.GetStats()) : null;
        }

        private const string s_removeSvg =
"""
<svg width="10.627" height="18.333" version="1.1" viewBox="0 0 10.627 18.333" xmlns="http://www.w3.org/2000/svg">
	<g transform="matrix(.5 0 0 .5 1.9595e-5 -9.9838e-6)" fill="none" stroke="#000">
		<rect x="2.7185" y="10.271" width="15.817" height="25.531" rx=".24714" ry=".33594" stroke-width="1.7288"></rect>
		<polygon transform="matrix(.24714 0 0 .33594 -5.19 -3.1669)" points="28 24 100 24 104 40 24 40" stroke-linejoin="round" stroke-width="6"></polygon>
		<rect x="8.6499" y=".86442" width="3.9542" height="4.0313" rx=".24714" ry=".33594" stroke-width="1.7288"></rect>
	</g>
	<g transform="matrix(.5 0 0 .5 1.9595e-5 -9.9838e-6)" stroke="#000" stroke-linecap="round" stroke-width="1.7288">
		<line x1="6.6727" x2="6.6727" y1="27.74" y2="19.677"></line>
		<line x1="10.627" x2="10.627" y1="27.74" y2="19.677"></line>
		<line x1="14.581" x2="14.581" y1="27.74" y2="19.677"></line>
	</g>
</svg>
""";

        private Stat? GetStatById(ListingId id) => stats.FirstOrDefault(x => x.Info.Id == id);
    }
}
