using AngleSharp.Dom;
using AngleSharp.Svg.Dom;
using Xaminer.App.Helpers;
using Xaminer.App.Interop.CDP;

namespace Xaminer.App.Pages
{
    public class LoadingPage : IContent
    {
        public Task BeforeContent(Page page) => Task.CompletedTask;

        public async Task<IDocument> GetDocument()
        {
            var doc = await BrowserHelpers.GetDocument(title: Strings.Loading);
            doc.AddDefaults();

            var css = doc.TextN(
"""
.spinner {
    width:100%;
    height:100%;
    position:fixed;
    top:0;
    left:0;
    bottom:0;
    right:0;
}
""");
            doc.AppendStyle(css);

            var svg = doc.CreateElement<ISvgSvgElement>(); // svg
            svg.ClassList.Add("spinner");
            svg.SetAttribute("viewBox", "0 0 100 100");

            svg.InnerHtml =
""" 
<circle cx="50" cy="50" r="40" stroke="#70c542" stroke-width="4" fill="none">
  <animate attributeName="stroke-dasharray" from="0 251" to="251 251" dur="2s" repeatCount="indefinite" />
  <animate attributeName="stroke-dashoffset" from="0" to="251" dur="2s" repeatCount="indefinite" />
  <animate attributeName="stroke-dashoffset" from="0" to="-251" dur="2s" repeatCount="indefinite" />
</circle>
""";

            doc.Body!.Append(svg);

            return doc;
        }

        public Task<IContent?> AfterNavigation(Page page) => Task.FromResult<IContent?>(null);
    }
}
