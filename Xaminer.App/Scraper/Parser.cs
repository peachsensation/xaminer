using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Xaminer.App.Models;

namespace Xaminer.App.Scraper
{
    public sealed class Parser
    {
        public static async Task<IEnumerable<Listing>> ParseListings(IAsyncEnumerable<IDocument> docs)
        {
            var listings = new List<Listing>();

            await foreach (var doc in docs)
            {
                var body = doc.Body;

                if (body is null)
                    continue;

                var items = doc.QuerySelectorAll(".grid-item");

                Program.LogConsole<Parser>($"Grid items: {items.Length}");

                foreach (var item in items)
                {
                    var href = ((IHtmlAnchorElement?)item.QuerySelector(".adv-heading")?.Children.FirstOrDefault())?.Href;
                    if (!Uri.TryCreate(href, UriKind.Absolute, out var url))
                    {
                        continue;
                    }

                    var id = Path.GetFileNameWithoutExtension(url.LocalPath);

                    var name = item.QuerySelector(".pr-name").GetInnerText().Trim();
                    var place = item.QuerySelector(".location").GetInnerText().Trim();
                    var generAge = item.QuerySelector(".listing-gender")?.GetInnerText().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var gender = generAge?[0] ?? "";
                    var age = string.Concat(generAge?[1].TakeWhile(char.IsNumber) ?? new List<char> { '0' });
                    var categories = item.QuerySelector(".categories").GetInnerText().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var phone = Phone.Parse(item.QuerySelector(".ph-number")?.GetAttribute("data-number"));
                    var diamond = item.QuerySelector(".diamond")?.ClassList.FirstOrDefault() ?? string.Empty;
                    var agency = item.QuerySelector(".cat-ribbon").GetInnerText();

                    var images = new List<Uri>();
                    foreach (var img in item.QuerySelectorAll<IHtmlImageElement>("#slideshow > a > img"))
                    {
                        if (Uri.TryCreate(img.Source, UriKind.Absolute, out var source))
                        {
                            images.Add(source);
                        }
                    }

                    listings.Add(new Listing
                    (
                        Id: ListingId.Parse(id),
                        Name: name,
                        Place: place,
                        Gender: Gender.Parse(gender),
                        Age: int.Parse(age),
                        Providings: Providings.Parse(categories),
                        Phone: phone,
                        Diamond: Diamond.Parse(diamond),
                        Agency: Agency.Parse(agency),
                        Images: images
                    ));
                }
            }

            return listings;
        }
    }
}
