namespace Xaminer.App
{
    public static class Globals
    {
        public static readonly DirectoryInfo TempDataDir = new(Path.Combine(AppContext.BaseDirectory, "data"));

        public const string ScraperBaseUrl = "https://www.kinky.nl";

        public const string GithubLatestReleaseUrl = "https://api.github.com/repos/peachsensation/xaminer/releases/latest";

    }
}
