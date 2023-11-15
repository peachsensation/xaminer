using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Xaminer.App.Helpers;
using Xaminer.App.Interop.CDP;

namespace Xaminer.App.Pages
{
    public sealed class ErrorPage(Exception exception) : IContent
    {
        public static Exception ShowFromErrors(IEnumerable<string> errors)
        {
            var exceptions = new List<Exception>();
            foreach (var error in errors)
                exceptions.Add(new Exception(error));

            Program.LogConsole<ErrorPage>(string.Join(" | ", errors));

            throw new AggregateException(null, exceptions);
        }

        public Task BeforeContent(Page page) => Task.CompletedTask;

        public async Task<IDocument> GetDocument()
        {
            var doc = await BrowserHelpers.GetDocument(title: Strings.Error);
            doc.AddDefaults();
            doc.AddContent(flexDirection: "column");

            var body = doc.CreateElement<IHtmlBodyElement>(); // body

            var h = doc.CreateElement<IHtmlHeadingElement>(); // h
            h.TextContent = Strings.ErrorTitle;
            body.Append(h);

            IHtmlDivElement div;
            if (exception is AggregateException aggregate)
            {
                div = AppendErrorDialog(doc, aggregate.InnerExceptions.Select(x => x.Message));
            }
            else
            {
                div = AppendErrorDialog(doc, Enumerable.Repeat(exception.Message, 1));
            }

            body.Append(div);

            doc.AppendBrAfter(div);

            var button = doc.CreateElement<IHtmlButtonElement>(); // button
            button.TextContent = Strings.BackToHome;
            button.SetAttribute("onclick", """updateQueryParams({r: "✓"})""");

            body.Append(button);

            body.AppendIntoContent(doc);

            return doc;
        }

        public Task<IContent?> AfterNavigation(Page page) => Task.FromResult<IContent?>(null);

        private IHtmlDivElement AppendErrorDialog(IDocument doc, IEnumerable<string> errors)
        {
            var div = doc.CreateElement<IHtmlDivElement>(); // div

            var openBtn = doc.CreateElement<IHtmlButtonElement>(); // button
            openBtn.Id = "msg-dlg-open";
            openBtn.TextContent = Strings.ShowErrors;
            div.Append(openBtn);

            var dialog = doc.CreateElement<IHtmlDialogElement>(); // dialog
            dialog.Id = "msg-dlg";

            foreach (var error in errors)
            {
                var errorP = doc.CreateElement<IHtmlParagraphElement>(); // p
                errorP.TextContent = error;
                dialog.Append(errorP);
            }

            var closeBtn = doc.CreateElement<IHtmlButtonElement>(); // button
            closeBtn.Id = "msg-dlg-close";
            closeBtn.TextContent = Strings.Close;
            dialog.Append(closeBtn);

            div.Append(dialog);

            return div;
        }
    }
}
