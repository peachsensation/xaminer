using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Xaminer.App.APIS
{
    public sealed class Github
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = GithubJsonContext.Default
        };

        public async Task<GithubRelease> GetLatestRelease(CancellationToken token)
        {
            using var client = GetClient();
            return (await client.GetFromJsonAsync<GithubRelease>(Globals.GithubLatestReleaseUrl, s_jsonOptions, token))!;
        }

        public async Task<Stream> DownloadAssetAsStream(GithubAsset asset)
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync();
        }

        private HttpClient GetClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
            };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(new ProductHeaderValue(Guid.NewGuid().ToString("N"), "1.0")));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
            return client;
        }
    }

    [JsonSerializable(typeof(GithubRelease))]
    [JsonSerializable(typeof(JsonObject))]
    [JsonSerializable(typeof(JsonArray))]
    public partial class GithubJsonContext : JsonSerializerContext { }

    public sealed record GithubRelease(
        Uri Url,
        [property: JsonPropertyName("assets_url")] Uri AssetsUrl,
        [property: JsonPropertyName("upload_url")] string UploadUrl,
        [property: JsonPropertyName("html_url")] Uri HtmlUrl,
        int Id,
        GithubAuthor Author,
        [property: JsonPropertyName("node_id")] string NodeId,
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("target_commitish")] string TargetCommitish,
        string? Name,
        bool Draft,
        bool Prerelease,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt,
        [property: JsonPropertyName("published_at")] DateTime? PublishedAt,
        IEnumerable<GithubAsset> Assets,
        [property: JsonPropertyName("tarball_url")] Uri? TarballUrl,
        [property: JsonPropertyName("zipball_url")] Uri? ZipballUrl,
        string? Body,
        [property: JsonPropertyName("discussion_url")] Uri DiscussionUrl,
        GithubReactions Reactions,
        [property: JsonPropertyName("mentions_count")] int MentionsCount);

    public sealed record GithubAuthor(
        string Login,
        int Id,
        [property: JsonPropertyName("node_id")] string NodeId,
        [property: JsonPropertyName("avatar_url")] Uri AvatarUrl,
        [property: JsonPropertyName("gravatar_id")] string? GravatarId,
        Uri Url,
        [property: JsonPropertyName("html_url")] Uri HtmlUrl,
        [property: JsonPropertyName("followers_url")] Uri FollowersUrl,
        [property: JsonPropertyName("following_url")] string FollowingUrl,
        [property: JsonPropertyName("gists_url")] string GistsUrl,
        [property: JsonPropertyName("starred_url")] string StarredUrl,
        [property: JsonPropertyName("subscriptions_url")] Uri SubscriptionsUrl,
        [property: JsonPropertyName("organizations_url")] Uri OrganizationsUrl,
        [property: JsonPropertyName("repos_url")] Uri ReposUrl,
        [property: JsonPropertyName("events_url")] string EventsUrl,
        [property: JsonPropertyName("received_events_url")] Uri ReceivedEventsUrl,
        string Type,
        [property: JsonPropertyName("site_admin")] bool SiteAdmin);

    public sealed record GithubAsset(
        Uri Url,
        int Id,
        [property: JsonPropertyName("node_id")] string NodeId,
        string Name,
        string? Label,
        GithubAuthor? Uploader,
        [property: JsonPropertyName("content_type")] string ContentType,
        string State,
        int Size,
        [property: JsonPropertyName("download_count")] int DownloadCount,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt,
        [property: JsonPropertyName("published_at")] DateTime PublishedAt,
        [property: JsonPropertyName("browser_download_url")] Uri BrowserDownloadUrl);

    public sealed record GithubReactions(
        Uri Url,
        [property: JsonPropertyName("total_count")] int TotalCount,
        [property: JsonPropertyName("+1")] int PlusOne,
        [property: JsonPropertyName("-1")] int MinusOne,
        int Laugh,
        int Hooray,
        int Confused,
        int Heart,
        int Rocket,
        int Eyes);
}
