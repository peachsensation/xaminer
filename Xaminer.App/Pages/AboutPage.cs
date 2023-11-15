using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xaminer.App.Helpers;
using Xaminer.App.Interop.CDP;
using Xaminer.App.Updater;

namespace Xaminer.App.Pages
{
    public sealed class AboutPage : IContent
    {
        private readonly AboutQuery _query;

        public AboutPage(AboutQuery query)
        {
            _query = query;
        }

        public async Task BeforeContent(Page page)
        {
            if (_query.Update)
            {
                _ = Task.Run(async () =>
                {
                    await UpdateManager.DoUpdate();
                });
                await page.SetNavigationValue(new List<KeyValuePair<string, string?>>
                {
                    new("p", "about")
                });
            }
        }

        public async Task<IDocument> GetDocument()
        {
            var doc = await BrowserHelpers.GetDocument(title: Strings.About);
            doc.AddDefaults();
            await doc.AddHeader();
            doc.AddContent();

            var body = doc.CreateElement<IHtmlBodyElement>(); // body

            var div = doc.CreateElement<IHtmlDivElement>(); // div

            var appVersionP = doc.CreateElement<IHtmlParagraphElement>(); // p
            appVersionP.TextContent = $"{Strings.Version}: {UpdateManager.ApplicationVersion}";
            div.Append(appVersionP);

            var netVersionP = doc.CreateElement<IHtmlParagraphElement>(); // p
            netVersionP.TextContent = $".NET: {RuntimeInformation.FrameworkDescription.Replace(".NET", "").Trim()}";
            div.Append(netVersionP);

            var archP = doc.CreateElement<IHtmlParagraphElement>(); // p
            archP.TextContent = $"{Strings.Architecture}: {Enum.GetName(RuntimeInformation.OSArchitecture)}";
            div.Append(archP);

            var aotP = doc.CreateElement<IHtmlParagraphElement>(); // p
            aotP.TextContent = $"AOT: {(RuntimeFeature.IsDynamicCodeCompiled ? Strings.No : Strings.Yes)}";
            div.Append(aotP);

            if (UpdateManager.UpdateErrors.Any())
            {
                ErrorPage.ShowFromErrors(UpdateManager.UpdateErrors);
            }

            if (!UpdateManager.IsUpdating && await UpdateManager.IsUpdateAvailable() is { IsUpdateAvailable: true })
            {
                var updateBtn = doc.CreateElement<IHtmlButtonElement>(); // button
                updateBtn.Type = "button";
                updateBtn.TextContent = Strings.Update;
                updateBtn.SetAttribute("onclick", $"location.href='#update=true'");
                div.Append(updateBtn);
            }

            if (await UpdateManager.IsUpdateAvailable() is { Update: not null } lastCheck)
            {
                var emptyP = doc.CreateElement<IHtmlParagraphElement>(); // p
                div.Append(emptyP);

                var bodyDiv = doc.CreateElement<IHtmlDivElement>(); // div
                bodyDiv.SetStyle("white-space: pre-wrap;");
                bodyDiv.Append(doc.TextN(lastCheck.Update.Body));

                div.Append(bodyDiv);
            }

            if (UpdateManager.IsUpdating)
            {
                var meta = doc.CreateElement<IHtmlMetaElement>(); // meta
                meta.HttpEquivalent = "refresh";
                meta.Content = "2";
                doc.Head!.Append(meta);

                var divUpdate = doc.CreateElement<IHtmlDivElement>(); // div

                var updateP = doc.CreateElement<IHtmlParagraphElement>(); // p
                updateP.TextContent = $"{Strings.UpdateProgress}: {UpdateManager.Progress}%";
                divUpdate.Append(updateP);

                var refreshBtn = doc.CreateElement<IHtmlButtonElement>(); // button
                refreshBtn.Type = "button";
                refreshBtn.TextContent = Strings.Refresh;
                refreshBtn.SetAttribute("onclick", """window.location.reload()""");

                divUpdate.Append(refreshBtn);

                div.Append(divUpdate);
            }

            body.Append(div);

            body.AppendIntoContent(doc);

            return doc;
        }

        public Task<IContent?> AfterNavigation(Page page)
        {
            if (page.TryGetNavigationValue("update", out var updateValue) && bool.TryParse(updateValue, out var update))
            {
                return Task.FromResult<IContent?>(new AboutPage(new AboutQuery(Update: update)));
            }
            return Task.FromResult<IContent?>(null);
        }
    }

    public sealed record AboutQuery(bool Update = false);
}
