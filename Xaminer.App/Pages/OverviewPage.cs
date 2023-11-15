using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Xaminer.App.Helpers;
using Xaminer.App.Interop.CDP;
using Xaminer.App.Models;
using ASH = Xaminer.App.Helpers.AngleSharpHelpers;

namespace Xaminer.App.Pages
{
    public sealed class OverviewPage(CompareResult compResult, OverviewQuery query) : IContent
    {
        const int s_itemsPerPage = 35;

        public async Task BeforeContent(Page page)
        {
            await page.SetNavigationValue(new List<KeyValuePair<string, string?>>
            {
                new("gender", query.Gender?.ToString()),
                new("page", query.Page.ToString()),
                new("id", query.Id?.Number.ToString())
            });
        }

        public async Task<IDocument> GetDocument()
        {
            var changes = query.Gender switch
            {
                SelectGender.Females => compResult.Females,
                SelectGender.Males => compResult.Males,
                SelectGender.Shemales => compResult.Shemales,
                SelectGender.Couples => compResult.Couples,
                _ => compResult.All
            };

            var changesPerPage = changes.Skip((query.Page - 1) * s_itemsPerPage).Take(s_itemsPerPage);
            var prevPageNumber = query.Page > 1 ? query.Page - 1 : default(int?);
            var nextPageNumber = changes.Count() > query.Page * s_itemsPerPage ? query.Page + 1 : default(int?);

            var doc = await BrowserHelpers.GetDocument(title: Strings.Results);
            doc.AddDefaults();
            await doc.AddHeader();
            doc.AddContent();

            var css = doc.TextN(
"""
#left-pane {
	flex: 1;
	overflow: auto;
	padding: 10px;
}

#right-pane {
	width: 50%;
	padding: 10px;
	align-items: center;
}

select {
    width: 100%;
    overflow: hidden;
}

#drag-bar {
	width: 4px;
	background-color: #ccc;
	cursor: col-resize;
}
""");
            doc.AppendStyle(css);

            var body = doc.CreateElement<IHtmlBodyElement>(); // body

            var leftPane = doc.CreateElement<IHtmlDivElement>(); // div
            leftPane.Id = "left-pane";

            var dragBar = doc.CreateElement<IHtmlDivElement>(); // div
            dragBar.Id = "drag-bar";

            var rightPane = doc.CreateElement<IHtmlDivElement>(); // div
            rightPane.Id = "right-pane";

            var leftPaneDiv = doc.CreateElement<IHtmlDivElement>(); // div

            leftPaneDiv.Append(CreateGenderFilter(doc, query.Gender, compResult));

            var rightPaneDiv = doc.CreateElement<IHtmlDivElement>(); // div

            var select = doc.CreateElement<IHtmlSelectElement>(); // select
            select.Id = "listings";
            select.Size = changesPerPage.Count() switch
            {
                var count when count > 2 => count,
                _ => 2
            };

            foreach (var change in changesPerPage)
            {
                var id = change.Listing.Id.Number.ToString();

                var option = doc.CreateElement<IHtmlOptionElement>(); // option
                option.TextContent = await GetListingText(change);
                option.Value = id;
                select.Append(option);

                var div = doc.CreateElement<IHtmlDivElement>(); // div
                div.Id = id;
                div.ClassList.Add("hidden");

                var listingTable = CreateListingTable(doc, change.Listing);
                div.Append(listingTable);

                var statsOnlineTable = ASH.CreateStatsOnlineTable(doc, await change.Listing.GetStat());
                div.Append(doc.CreateElement<IHtmlHrElement>() /*hr*/);
                div.Append(statsOnlineTable);

                var statsIRLTable = ASH.CreateStatsIRLTable(doc, await change.Listing.GetStat());
                div.Append(doc.CreateElement<IHtmlHrElement>() /*hr*/);
                div.Append(statsIRLTable);

                rightPaneDiv.Append(div);
            }

            leftPaneDiv.Append(select);

            leftPane.Append(leftPaneDiv);
            rightPane.Append(rightPaneDiv);

            body.Append(leftPane);
            body.Append(dragBar);
            body.Append(rightPane);

            if (prevPageNumber is int prevNumber)
            {
                var buttonPrevPage = ASH.CreateSvgBtn(doc, """<path d="M13 1 L3 8 L13 15" />""", $$"""updateQueryParams({page: "{{prevNumber}}"})""");
                body.Prepend(buttonPrevPage);
            }

            if (nextPageNumber is int nextNumber)
            {
                var buttonNextPage = ASH.CreateSvgBtn(doc, """<path d="M3 1 L13 8 L3 15" />""", $$"""updateQueryParams({page: "{{nextNumber}}"})""");
                body.Append(buttonNextPage);
            }

            body.AppendIntoContent(doc);

            var js =
"""
// Filters
const radioButtons = document.getElementsByName("gender");
for (let i = 0; i < radioButtons.length; i++) {
	const radioButton = radioButtons[i];
	radioButton.addEventListener("click", () => {
		if (radioButton.checked && window.location.hash.includes(`gender=${radioButton.value}`)) {
            updateQueryParams({gender: ""})
		} else {
            updateQueryParams({gender: radioButton.value})
		}
	});
}

// Pane dragging
const dragBar = document.getElementById("drag-bar");
const leftPane = document.getElementById("left-pane");
const rightPane = document.getElementById("right-pane");

let isDragging = false;

dragBar.addEventListener("mousedown", () => {
	isDragging = true;
});

document.addEventListener("mouseup", () => {
	isDragging = false;
});

document.addEventListener("mousemove", (event) => {
    if (!isDragging) {
		return;
	}

	const mouseX = event.clientX;
	const leftPaneWidth = leftPane.getBoundingClientRect().width;
	const totalWidth = window.innerWidth;
	const rightPaneWidth = totalWidth - mouseX;

	leftPane.style.width = `${(100 * leftPaneWidth / totalWidth)}%`;
	rightPane.style.width = `${(100 * rightPaneWidth / totalWidth)}%`;
});

// Datalist behaviour
const select = document.getElementById("listings");
const divs = document.querySelectorAll("#right-pane  div[class]");

select.addEventListener("change", () => {
	const selectedOption = select.value;
	divs.forEach(div => {
		if (div.id === selectedOption) {
			div.classList.remove("hidden");
		} else {
			div.classList.add("hidden");
		}
    });
});

select.addEventListener("mouseover", (event) => {
	const hoveredOption = event.target.value;
	if (hoveredOption !== select.value) {
		divs.forEach(div => {
			if (div.id === hoveredOption) {
				div.classList.remove("hidden");
			} else {
				div.classList.add("hidden");
			}
		});
	}
});

select.addEventListener("mouseout", () => {
	const selectedOption = select.value;
	divs.forEach(div => {
		if (div.id === selectedOption) {
			div.classList.remove("hidden");
		} else {
			div.classList.add("hidden");
		}
	});
});

(() => {
	const params = new URLSearchParams(window.location.hash.substring(1));
	const id = params.get("id") ?? "";
	select.value = id;
	select.dispatchEvent(new Event("change"));
})();
""";
            var script = doc.CreateElement<IHtmlScriptElement>(); // script
            script.TextContent = js;

            doc.DocumentElement.AppendChild(script);

            return doc;
        }

        public async Task<IContent?> AfterNavigation(Page page)
        {
            var query = new OverviewQuery();
            var result = false;

            if (page.TryGetNavigationValue("page", out var pageValue) && int.TryParse(pageValue, out var newPageNumber))
            {
                query = query with { Page = newPageNumber };
                result = true;
            }

            if (page.TryGetNavigationValue("gender", out var genderValue))
            {
                var selectGender = Enum.TryParse<SelectGender>(genderValue, ignoreCase: false, out var gender) ? gender : (SelectGender?)null;

                if (query.Gender != selectGender)
                    query = query with { Gender = selectGender, Page = 1 };
                else
                    query = query with { Gender = selectGender };

                result = true;
            }

            if (page.TryGetNavigationValue("id", out var idValue) && ListingId.TryParse(idValue, out var id) &&
                GetListingById(id) is { } listing)
            {
                if ((page.TryGetNavigationValue("onlineRating", out var ratingValue) || page.TryGetNavigationValue("irlRating", out ratingValue)) &&
                    int.TryParse(ratingValue, out var newRating))
                {
                    await UserStore.UpdateStat(listing, (stat) =>
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
                    if (!AppBrowserHelpers.TryOpen((AppBrowser)browser, listing.Id.PageUrl))
                    {
                        var newPage = await page.Browser.CreatePage();
                        await newPage.Navigate(listing.Id.PageUrl.ToString());
                    }

                    await UserStore.UpdateStat(listing, (stat) => stat with
                    {
                        Online = stat.Online with
                        {
                            Visits = stat.Online.Visits +1,
                            LastVisited = DateTime.Now,
                        }
                    });
                }

                query = query with { Id = listing.Id };
                result = true;
            }

            return result ? new OverviewPage(compResult, query) : null;
        }

        private IHtmlTableElement CreateListingTable(IDocument doc, Listing listing)
        {
            var table = doc.CreateElement<IHtmlTableElement>(); // table

            var tableHead = doc.CreateElement<IHtmlTableSectionElement>(); // thead

            ASH.AppendTH(doc, tableHead, Strings.Property);
            ASH.AppendTH(doc, tableHead, Strings.Value);

            table.Head = tableHead;

            var tableBody = table.CreateBody();
            ASH.AppendTR(doc, tableBody, new[] { doc.TextN($"{Strings.Name}:"), doc.TextN(listing.Name) });
            ASH.AppendTR(doc, tableBody, new[] { doc.TextN($"{Strings.Gender}:"), doc.TextN(listing.Gender.NameLocalized) });
            ASH.AppendTR(doc, tableBody, new[] { doc.TextN($"{Strings.Age}:"), doc.TextN(listing.Age.ToString()) });
            ASH.AppendTR(doc, tableBody, new[] { doc.TextN($"{Strings.Providings}:"), doc.TextN(listing.Providings.NameLocalized) });
            ASH.AppendTR(doc, tableBody, new[] { doc.TextN($"{Strings.Phone}:"), doc.TextN(listing.Phone?.ToString("N")) });

            var profileNode = CreateProfileImageNode(doc, listing);
            ASH.AppendTR(doc, tableBody, new[] { doc.TextN($"{Strings.ProfileImage}:"), profileNode });

            var linkNode = ASH.CreatePageUrlNode(doc, listing.Id);
            ASH.AppendTR(doc, tableBody, new[] { doc.TextN($"{Strings.PageUrl}:"), linkNode });

            return table;
        }

        private INode CreateProfileImageNode(IDocument doc, Listing listing)
        {
            var containerDiv = doc.CreateElement<IHtmlDivElement>(); // div

            if (listing.Agency is { } agency)
            {
                var agencyDiv = doc.CreateElement<IHtmlDivElement>(); // div
                agencyDiv.TextContent = $"<{agency.Name.ToUpperInvariant()}>";
                containerDiv.Append(agencyDiv);
            }

            var img = doc.CreateElement<IHtmlImageElement>(); // img
            img.Source = listing.Images.FirstOrDefault()?.ToString();
            img.SetAttribute("loading", "lazy");

            containerDiv.Append(img);

            return containerDiv;
        }

        private IHtmlDivElement CreateGenderFilter(IDocument doc, SelectGender? checkedGender, CompareResult result)
        {
            var genderDiv = doc.CreateElement<IHtmlDivElement>(); // div

            foreach (var value in EnumHelper<SelectGender>.GetValues())
            {
                var count = value switch
                {
                    SelectGender.Females => result.Females.Count(),
                    SelectGender.Males => result.Males.Count(),
                    SelectGender.Shemales => result.Shemales.Count(),
                    SelectGender.Couples => result.Couples.Count(),
                    _ => throw new NotSupportedException()
                };

                var input = doc.CreateElement<IHtmlInputElement>(); // input
                input.Type = "radio";
                input.Name = "gender";
                input.DefaultValue = value.ToString();
                input.TextContent = $"{value.GetDisplayName()} ({count})";

                if (value == checkedGender)
                {
                    input.IsDefaultChecked = true;
                }

                genderDiv.Append(input);
            }

            return genderDiv;
        }

        private async Task<string> GetListingText(ChangeListing change)
        {
            var rating = (await change.Listing.GetStat()).AverageRating;

            if (rating == 0)
                return change.ToString();

            return $"{change} | {new string('★', rating)}";
        }

        private Listing? GetListingById(ListingId id) => compResult.All.FirstOrDefault(x => x.Listing.Id == id)?.Listing;
    }

    public sealed record OverviewQuery(int Page = 1,
                                       int Count = 1,
                                       SelectGender? Gender = null,
                                       ListingId? Id = null);
}
