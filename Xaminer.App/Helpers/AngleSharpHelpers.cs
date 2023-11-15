using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Svg.Dom;
using Xaminer.App.Models;
using Xaminer.App.Updater;

namespace Xaminer.App.Helpers
{
    public static class AngleSharpHelpers
    {
        public static void AddDarkMode(this IDocument doc)
        {
            var link = doc.CreateElement<IHtmlLinkElement>(); // link
            link.Relation = "icon";
            link.Type = "image/png";
            link.Sizes.Add("16x16");
            link.Href = s_logoDataImage16;
            doc.Head!.Append(link);

            var meta = doc.CreateElement<IHtmlMetaElement>(); // meta
            meta.Name = "color-scheme";
            meta.Content = "only dark";
            doc.Head!.Append(meta);
        }

        public static void AddDefaultStyle(this IDocument doc)
        {
            var css = doc.TextN(
"""
:root {
    font-family: system-ui;
}

body {
    margin: 0px;
}

a {
    text-decoration: none;
    color: #ccc;
    margin: 0 1rem;
}

a:hover {
    color: #4d4d4d;
    transition: color 0.2s ease-in-out;
}

table {
    border-collapse: collapse;
    width: 100%;
}
                
td, th {
    border: 1px solid #dddddd;
    text-align: left;
    padding: 8px;
}

hr {
    width: 100%;
}

option:hover {
    color: #ccc;
}

.hidden {
  display: none;
}

.logoButton {
    background: none;
    color: inherit;
    border: none;
    padding: 0;
    font: inherit;
    cursor: pointer;
    outline: inherit;
}

.rating {
    display: inline-block;
}

.star {
    font-size: 1.5rem;
    cursor: pointer;
    position: relative;
    background: none;
    border: none;
    padding: 0;
    margin: 0;
}

.star::before {
    content: "★";
    top: 0;
    left: 0;
    color: #fff;
}

.star:hover::before,
.star:focus::before,
.star.active::before {
    color: orange;
    cursor: pointer;
}
""");
            doc.AppendStyle(css);

            var js =
"""
// URL updating
function updateQueryParams(queryParams, preserveKeys = "all") {
	const currentHash = window.location.hash;

	const hashObject = currentHash.substring(1)
	.split("&")
	.reduce((result, item) => {
		const parts = item.split("=");
		result[parts[0]] = parts[1];
		return result;
	}, {});

	if (preserveKeys !== "all") {
		Object.keys(hashObject).forEach(key => {
			if (!preserveKeys.includes(key)) {
				delete hashObject[key];
			}
		});
	}

	Object.keys(queryParams).forEach(paramName => {
		hashObject[paramName] = queryParams[paramName];
	});

	const newHash = Object.keys(hashObject)
		.map(key => `${key}=${hashObject[key]}`)
		.join("&");

	window.location.hash = `#${newHash}`;
}

// Rating
var stars = document.querySelectorAll(".star");
var rating = document.querySelector(".rating");

rating?.addEventListener("click", event => {
	if (!event.target.matches(".star")) return;

	stars.forEach(star => {
		if (star.getAttribute("data-value") <= event.target.getAttribute("data-value")) {
			star.classList.add("active");
		} else {
			star.classList.remove("active");
		}
	});
});

// Message box
var dialog = document.getElementById("msg-dlg");

document.getElementById("msg-dlg-open")?.addEventListener("click", event => {
    dialog.showModal();
});

document.getElementById("msg-dlg-close")?.addEventListener("click", event => {
    dialog.close();
});
""";
            var script = doc.CreateElement<IHtmlScriptElement>(); // script
            script.TextContent = js;

            doc.DocumentElement.AppendChild(script);
        }

        public static async Task AddHeader(this IDocument doc)
        {
            var header = (IHtmlElement)doc.CreateElement("header");

            var div = doc.CreateElement<IHtmlDivElement>(); // div
            div.ClassList.Add("logo");

            var svg = doc.CreateElement<ISvgSvgElement>(); // svg
            svg.SetAttribute("width", "24");
            svg.SetAttribute("height", "24");
            svg.SetAttribute("viewBox", "0 0 16 16");
            svg.InnerHtml = s_logoSvg;

            div.Append(svg);

            header.Append(div);

            var homeA = doc.CreateElement<IHtmlAnchorElement>(); // a
            homeA.ClassList.Add("left");
            homeA.Href = "#p=home";
            homeA.TextContent = Strings.HomePage;
            header.Append(homeA);

            var favsA = doc.CreateElement<IHtmlAnchorElement>(); // a
            favsA.ClassList.Add("left");
            favsA.Href = "#p=favs";
            favsA.TextContent = Strings.Favorites;
            header.Append(favsA);

            var versionA = doc.CreateElement<IHtmlAnchorElement>(); // a
            versionA.ClassList.Add("right");
            versionA.Href = "#p=about";
            var updateAvailableText = await UpdateManager.IsUpdateAvailable() switch
            {
                { IsUpdateAvailable: true } => $" | {Strings.UpdateAvailable}",
                _ => ""
            };
            versionA.TextContent = $"{UpdateManager.ApplicationVersion}{updateAvailableText}";
            header.Append(versionA);

            doc.Body!.Prepend(header);

            var css = doc.TextN(
"""
header {
    display: flex;
    position: sticky;
    justify-content: space-between;
    align-items: center;
    top: 0;
    left: 0;
    right: 0;
    height: 50px;
    background: rgba(20,20,20,.5);
    backdrop-filter: blur(0.1em);
    z-index: 1;
}

a.right {
    margin-left: auto;
}

.logo {
    padding-left: 4px;
    margin-right: 1rem;
}

svg {
    width: 32px;
    height: 32px;
}

#content {
    padding: 2px;
    display: flex;
}
""");
            doc.AppendStyle(css);
        }

        public static void AddContent(this IDocument doc, string? flexDirection = null)
        {
            var css = doc.TextN(
$$"""
#content {
    padding: 2px;
    display: flex;
    flex-direction: {{flexDirection ?? "row"}};
}
""");

            doc.AppendStyle(css);

            var div = doc.CreateElement<IHtmlDivElement>(); // div
            div.Id = "content";

            doc.Body!.Append(div);
        }

        public static void AppendStyle(this IDocument doc, INode css)
        {
            var style = doc.CreateElement<IHtmlStyleElement>(); // style
            style.Append(css);
            doc.Head!.Append(style);
        }

        public static void AppendIntoContent(this IHtmlBodyElement body, IDocument doc) =>
            doc.GetElementById("content")?.Append(body);

        public static void AppendTH(IDocument doc, IHtmlTableSectionElement tableHead, string value, int? columnSpan = null)
        {
            var thName = doc.CreateElement<IHtmlTableHeaderCellElement>(); // th
            thName.TextContent = value;
            if (columnSpan is { } colSpan)
                thName.ColumnSpan = colSpan;

            tableHead.Append(thName);
        }

        public static void AppendTR<T>(IDocument doc, IHtmlTableSectionElement tableBody, params T[] rowValues)
        {
            var tr = doc.CreateElement<IHtmlTableRowElement>(); // tr
            foreach (var rowValue in rowValues)
            {
                var td = tr.InsertCellAt(-1);
                if (rowValue is TableNode row)
                    row.AppendFrom(td);
                else if (rowValue is INode node)
                    td.Append(node);
            }
            tableBody.Append(tr);
        }

        public static IHtmlTableElement CreateStatsOnlineTable(IDocument doc, Stat stat)
        {
            var table = doc.CreateElement<IHtmlTableElement>(); // table

            var tableHead = doc.CreateElement<IHtmlTableSectionElement>(); // thead
            AppendTH(doc, tableHead, Strings.OnlineStats, columnSpan: 2);

            table.Head = tableHead;

            var tableBody = table.CreateBody();
            AppendTR(doc, tableBody, new object[] { TableNode.FromNode(doc.TextN($"{Strings.Visits}:")).SetWidth(25), doc.TextN(stat.Online.Visits.ToString()) });
            AppendTR(doc, tableBody, new[] { doc.TextN($"{Strings.Rating}:"), CreateRatingElement(doc, stat, stat.Online) });
            AppendTR(doc, tableBody, new[] { doc.TextN($"{Strings.LastVisit}:"), doc.TextN(stat.Online.LastVisited?.ToString()) });

            return table;
        }

        public static IHtmlTableElement CreateStatsIRLTable(IDocument doc, Stat stat)
        {
            var table = doc.CreateElement<IHtmlTableElement>(); // table

            var tableHead = doc.CreateElement<IHtmlTableSectionElement>(); // thead
            AppendTH(doc, tableHead, Strings.IRLStats, columnSpan: 2);

            table.Head = tableHead;

            var tableBody = table.CreateBody();
            AppendTR(doc, tableBody, new object[] { TableNode.FromNode(doc.TextN($"{Strings.Rating}:")).SetWidth(25), CreateRatingElement(doc, stat, stat.IRL) });

            return table;
        }

        public static IHtmlDivElement CreateRatingElement(IDocument doc, Stat stats, RatingBase rating)
        {
            var div = doc.CreateElement<IHtmlDivElement>(); // dic
            div.ClassList.Add("rating");

            foreach (var number in Enumerable.Range(1, 5))
            {
                var button = doc.CreateElement<IHtmlButtonElement>(); // span
                button.Type = "button";
                button.ClassList.Add("star");
                if (rating.Rating >= number)
                    button.ClassList.Add("active");

                var queryName = rating switch
                {
                    OnlineStats => "onlineRating",
                    IRLStats => "IRLRating",
                    _ => ""
                };

                button.SetAttribute("data-value", number.ToString());
                button.SetAttribute("onclick", $$"""updateQueryParams({id: "{{stats.Info.Id}}", {{queryName}}: "{{number}}"})""");

                div.AppendChild(button);
            }

            return div;
        }

        public static INode CreatePageUrlNode(IDocument doc, ListingId id)
        {
            var div = doc.CreateElement<IHtmlDivElement>(); // div

            var a = doc.CreateElement<IHtmlAnchorElement>(); // a
            a.Href = $$"""javascript:updateQueryParams({id: '{{id}}', browser: '0'})""";
            a.Append(doc.TextN(id.PageUrl.ToString()));

            div.Append(a);

            if (AppBrowserHelpers.IsFirefoxInstalled)
                div.Append(CreateBrowserButton(AppBrowserHelpers.FirefoxDataImage24, AppBrowser.Firefox));

            if (AppBrowserHelpers.IsEdgeInstalled)
                div.Append(CreateBrowserButton(AppBrowserHelpers.EdgeDataImage24, AppBrowser.Edge));

            if (AppBrowserHelpers.IsChromeInstalled)
                div.Append(CreateBrowserButton(AppBrowserHelpers.ChromeDataImage24, AppBrowser.Chrome));

            IHtmlButtonElement CreateBrowserButton(string imgData, AppBrowser browser)
            {
                var img = doc.CreateElement<IHtmlImageElement>(); // img
                img.Source = imgData;
                var button = doc.CreateElement<IHtmlButtonElement>(); // button
                button.Type = "button";
                button.Title = id.PageUrl.ToString();
                button.ClassList.Add("logoButton");
                button.SetAttribute("onclick", $$"""updateQueryParams({id: "{{id}}", browser: "{{(int)browser}}"})""");
                button.Append(img);
                return button;
            }

            return div;
        }

        public static IHtmlButtonElement CreateSvgBtn(IDocument doc, string svgPath, string onclick)
        {
            var svg = doc.CreateElement<ISvgSvgElement>(); // svg
            svg.SetAttribute("width", "16");
            svg.SetAttribute("height", "16");
            svg.SetAttribute("fill", "white");
            svg.InnerHtml = svgPath;

            var button = doc.CreateElement<IHtmlButtonElement>(); // button
            button.Type = "button";
            button.SetAttribute("onclick", onclick);
            button.Append(svg);

            return button;
        }

        public static INode TextN(this IDocument doc, string? text) => doc.CreateTextNode(text ?? "");

        public static void AppendBrAfter(this IDocument doc, INode element) => ((IHtmlElement?)element.Parent)?.Append(doc.CreateElement<IHtmlBreakRowElement>());

        private static readonly string s_logoSvg =
"""
<defs>
	<mask id="c" fill="#ffffff">
		<path d="m0.29289 1.7071c-0.39052-0.39052-0.39052-1.0237 0-1.4142s1.0237-0.39052 1.4142 0l8 8c0.39052 0.39052 0.39052 1.0237 0 1.4142-0.39052 0.39052-1.0237 0.39052-1.4142 0z"/>
	</mask>
	<mask id="d" fill="#ffffff">
		<path d="m3.5858 5-3.2929-3.2929c-0.39052-0.39052-0.39052-1.0237 0-1.4142s1.0237-0.39052 1.4142 0l3.2929 3.2929 3.2929-3.2929c0.39052-0.39052 1.0237-0.39052 1.4142 0 0.39052 0.39052 0.39052 1.0237 0 1.4142l-3.2929 3.2929 3.2929 3.2929c0.39052 0.39052 0.39052 1.0237 0 1.4142-0.39052 0.39052-1.0237 0.39052-1.4142 0l-3.2929-3.2929-3.2929 3.2929c-0.39052 0.39052-1.0237 0.39052-1.4142 0-0.39052-0.39052-0.39052-1.0237 0-1.4142z"/>
	</mask>
</defs>
<path d="m2.2857 2.2857-2.2857 2.2857 2.2857 2.2858-2.2857 2.2857 2.2857 2.2857-2.2857 2.2857 2.2857 2.2857 2.2857-2.2857 2.2857 2.2857 2.2857-2.2857 2.2857 2.2857 4.5714-4.5714-2.2857-2.2857 2.2857-2.2857-2.2857-2.2858 2.2857-2.2857-2.2857-2.2857-2.2857 2.2857-2.2857-2.2857-2.2857 2.2857-2.2857-2.2857z" fill="#6d2064" stroke-width="3.4553"/>
<g transform="matrix(.66667 0 0 .66667 -23.117 4.3689)">
	<path d="m40.557-0.070336c-0.56804-0.56804-0.56804-1.489 0-2.057 0.56804-0.56804 1.489-0.56804 2.057 0l11.636 11.636c0.56804 0.56803 0.56804 1.489 0 2.057-0.56804 0.56803-1.489 0.56803-2.057 0z" stroke-width="1.4545"/>
	<g transform="matrix(1.4545,0,0,1.4545,40.131,-2.5534)" fill="#1aebff" mask="url(#c)" stroke-width=".99995">
		<rect transform="translate(-8,-6)" width="24" height="24" stroke-width=".99995"/>
	</g>
	<g transform="matrix(0,-1.4545,1.4545,0,38.676,13.447)">
		<mask>
			<path d="m3.5858 5-3.2929-3.2929c-0.39052-0.39052-0.39052-1.0237 0-1.4142s1.0237-0.39052 1.4142 0l3.2929 3.2929 3.2929-3.2929c0.39052-0.39052 1.0237-0.39052 1.4142 0 0.39052 0.39052 0.39052 1.0237 0 1.4142l-3.2929 3.2929 3.2929 3.2929c0.39052 0.39052 0.39052 1.0237 0 1.4142-0.39052 0.39052-1.0237 0.39052-1.4142 0l-3.2929-3.2929-3.2929 3.2929c-0.39052 0.39052-1.0237 0.39052-1.4142 0-0.39052-0.39052-0.39052-1.0237 0-1.4142z"/>
		</mask>
		<path d="m3.5858 5-3.2929-3.2929c-0.39052-0.39052-0.39052-1.0237 0-1.4142s1.0237-0.39052 1.4142 0l3.2929 3.2929 3.2929-3.2929c0.39052-0.39052 1.0237-0.39052 1.4142 0 0.39052 0.39052 0.39052 1.0237 0 1.4142l-3.2929 3.2929 3.2929 3.2929c0.39052 0.39052 0.39052 1.0237 0 1.4142-0.39052 0.39052-1.0237 0.39052-1.4142 0l-3.2929-3.2929-3.2929 3.2929c-0.39052 0.39052-1.0237 0.39052-1.4142 0-0.39052-0.39052-0.39052-1.0237 0-1.4142z"/>
		<g fill="#edbbe7" mask="url(#d)">
			<rect transform="translate(-7,-7)" width="24" height="24"/>
		</g>
	</g>
</g>
""";

        private static readonly string s_logoDataImage16 =
        """
data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAACXBIWXMAAA7DAAAOwwHHb6hkAAAAGXRFWHRTb2Z0d2FyZQB3d3cuaW5rc2NhcGUub3Jnm+48GgAAAqlJREFUOMttk0tIVGEYhp9zmUbn2Dhli5nG1MkosWhmUWRBOG1ad1mHmqjRqI2ratfSIBq7TIFdVFpIrsSNXUCmIEzCNMLLLOpokAlB6NEj2syZv8VcGq1v9/9878fH876fJIQgWx2+BlcStUslGY7oPUsA7b7meoC7enfv/3qk7IAOX4PLwhYD4Qfpk0IiaGE7A6InPV5qUEgMbu2RhBA5sakp/g+nD3D0VRzNTM4D5WyuTX8S4o6y3DfvsrDF5ALZX3T2MFNNF5ipraLs1bhrWyKVU6ouG94jZS7j2xIAAvru6o8vyRbqIAh/5cn9XD91jqej02huLy8fhjA1FQC720H7jTCNdY1oPifAm3v6o3oAWUEKA8vzn+cwzBWqd+/l6fsZNI+X4WgIyjVar7Xi1Jy8GXuLqRsAtVm4GQbNAQsRs7sdxaFrIZyak6nvX+gssRFN7cTpKGJ45AVj/e+2IKFDBrAgABRvLK4RvRnFMA0Oeit5VlCG01HEwNQoI0NjW8XLClJM+dW3UP/XKrBWE8zMx6k5XgOAsbbC5RPVTNdWUf56nAzYZQUpGNG7J2WBCOaPtbsdXGxpzL2dju08GZ2hyFPK8IMQqw4VkOYgMZdj0OZr6pWgzu5xELqaZhDX4wz1D9FypSXNZOELjTXVmIvfOV93h2yQZACVZLjAXbiWFc9+neX57X5M3SDaeR/DXOHg7sq0O24v04d2AcJvofbmguQ7sW/P0cAR4nqcgdv9pDZSGSZJxic/EjgWoLTEzcStLiomfmY2SJ6R2iqaJkH45QKFQo/G+o/VFWs9tT2fNlCsumzYdxRmcpBeP6L3LMlAF0Bq3WJVN/pY/10G0qc8q4IgNSSXEv+IcxDbfc31AhHMxjN9XOqgghSO6N2TeWcdzhcD/AGXjzccfYo1pwAAAABJRU5ErkJggg==
""";
    }

    public sealed record TableNode
    {
        public static TableNode FromNode(INode node) => new(node);

        private readonly INode _node;

        private SizeKind? _size;
        private int? _sizeValue;

        private SpanKind? _span;
        private int? _spanValue;

        public TableNode SetColSpan(int number) => this with
        {
            _span = SpanKind.Column,
            _spanValue = number
        };

        public TableNode SetRowSpan(int number) => this with
        {
            _span = SpanKind.Row,
            _spanValue = number
        };

        public TableNode SetWidth(int size) => this with
        {
            _size = SizeKind.Width,
            _sizeValue = size
        };

        public TableNode SetHeight(int size) => this with
        {
            _size = SizeKind.Height,
            _sizeValue = size
        };

        public void AppendFrom(IHtmlTableCellElement element)
        {
            switch (_size)
            {
                case SizeKind.Width:
                    element.SetStyle($"width: {_sizeValue.GetValueOrDefault()}%");
                    break;
                case SizeKind.Height:
                    element.SetStyle($"height: {_sizeValue.GetValueOrDefault()}%");
                    break;
                default:
                    break;
            }

            switch (_span)
            {
                case SpanKind.Column:
                    element.ColumnSpan = _spanValue.GetValueOrDefault();
                    break;
                case SpanKind.Row:
                    element.RowSpan = _spanValue.GetValueOrDefault();
                    break;
                default:
                    break;
            }
            element.Append(_node);
        }

        private TableNode(INode node)
        {
            _node = node;
        }

        private enum SizeKind
        {
            Width,
            Height
        }

        private enum SpanKind
        {
            Column,
            Row
        }
    }
}
