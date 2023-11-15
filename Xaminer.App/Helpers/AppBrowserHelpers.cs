using System.Diagnostics;

namespace Xaminer.App.Helpers
{
    public static class AppBrowserHelpers
    {
        public const string ChromeExe = "chrome.exe";
        public const string EdgeExe = "msedge.exe";
        public const string FirefoxExe = "firefox.exe";

        public static bool IsChromeInstalled => RunScript().HasFlag(AppBrowsers.Chrome);
        public static bool IsEdgeInstalled => RunScript().HasFlag(AppBrowsers.Edge);
        public static bool IsFirefoxInstalled => RunScript().HasFlag(AppBrowsers.Firefox);

        public static bool TryOpen(AppBrowser browser, Uri url)
        {
            if (Enum.GetName(browser) is not { } browserName || !Enum.TryParse<AppBrowsers>(browserName, out var browsers))
                return false;

            if (!IsAnyInstalled(browsers))
                return false;

            var browserExe = browser switch
            {
                AppBrowser.Chrome => ChromeExe,
                AppBrowser.Edge => EdgeExe,
                AppBrowser.Firefox => FirefoxExe,
                _ => null
            };

            if (browserExe is null)
                return false;

            Process.Start(new ProcessStartInfo
            {
                FileName = browserExe,
                Arguments = string.Concat("\"", url.ToString(), "\""),
                UseShellExecute = true
            });
            return true;
        }

        public static bool IsAnyInstalled(AppBrowsers browsers) => (browsers & RunScript()) != 0;

        private static AppBrowsers? s_browsers;

        private static AppBrowsers RunScript()
        {
            if (s_browsers is AppBrowsers browsers)
                return browsers;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"""-ExecutionPolicy Bypass -Command "{s_script.Replace("\"", "\\\"")}" """,
                    CreateNoWindow = true
                };

                using var process = new Process
                {
                    StartInfo = startInfo
                };
                process.Start();
                process.WaitForExit();

                browsers = (AppBrowsers)process.ExitCode;
            }
            catch
            {
                browsers = AppBrowsers.Chrome | AppBrowsers.Edge | AppBrowsers.Firefox;
            }

            s_browsers = browsers;
            return browsers;
        }

        private const string s_script =
"""
[Flags()] enum AppBrowsers
{
  Chrome = 1
  Edge = 2
  Firefox = 4
}

[AppBrowsers]$AppBrowsers = 0

if (Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe") {
    [AppBrowsers]$AppBrowsers += [AppBrowsers]::Chrome
}

if (Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe") {
    [AppBrowsers]$AppBrowsers += [AppBrowsers]::Edge
}

if (Test-Path ([IO.Path]::Combine([Environment]::GetFolderPath("ProgramFiles"), "Mozilla Firefox", 'firefox.exe'))) {
    [AppBrowsers]$AppBrowsers += [AppBrowsers]::Firefox
} 
elseif (Get-AppxPackage -Name "Mozilla.Firefox"){
    [AppBrowsers]$AppBrowsers += [AppBrowsers]::Firefox
}

exit ([int]$AppBrowsers)
""";

        public const string FirefoxDataImage24 =
"""
data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAFk0lEQVR4AZWVA7BtWQ5AVzbO5fO3x7Zt27ZtFMa20bZt27bdz/bVOTtpfIzRq2ojTkoRHiA2+SoPJKL/OHl6FvAhVLfq/g3Bxl/BP3DxLQIY/45nP3JrouDfSZF2IiWAvYAzSRaA4l8KUCg7OOt6B+xQ0PUQR2YGGE+qOZIm4OHAAeQFqIFxIGaPAmZR+5dJAkkB4OQbthqfvDFjeMpz7CtbO4q94xpBVQDI9cdED0kL1ARYjdlxwLOBhHmnrmqAAUgafAPumIu3Ju8p1lEuH0k5vBu4lbjuCeCGgSmAvB43h778ZkqlDGcmYgIUGAGTg4F3sBXU1wQwsZ+vFcCYTzXWuyvp5qGUbB2DT3wda5u/YmXxcGAUgIfe/kaWVx5BnUSqeyvqSDAwCkwCxne0XLsaLc4CZhEnjvb9B2LclQUeyqLmzJYOpDy5E0t5xpXrFvf5zAGPPeIb+/7i9M8e+eOLf/M9rt/to27x1nWIn4ccSM6jAkm+KXn+enH+B/ceRHCig69HfnPdc8jSuZRzpZIcXYbVzaTLGLn1Beecuc9fntNVy31E0HbFUkKKsMhDXnIkj3/H4VCK4J1iOExusK64GuGVwCUOgFb8OK0AzajMB5hxKtNOmHSybtPZz3/W277rtYuiwYCG3qZk9Q5rn3Qr1hMYuvTxsFxgLedQB0keRq79mL4XU/x3h7rLePs5Kn1kJjxYhTVR6MuwkGFNr/0bLuYhTzzGa3eUoZGnYdFYnFzJ5hecxeZXnotNVpCGgAiIg5jEnGWY7RTMbIs043qSQEAod6BfMQJSdkhFHe2HEGuOzoRnPlSpBgULnPPnj9O/5Wp6Ng5i161Acgd1Ebo6oGwS1VUuLdTW04wZ7WDMZ3BuN3ZBGVns0FkocdKxX2KnA/fgd389gVPP+ijNLmMxlmhWEwupxO3nPhHcAuoL0nSGNoDUgcLqQF9oDa32tfVjiAGZA3PIzQFdldjpnK9y7cirqawRXCWj1LtIKw9Um1DOlZYkZieq0GiDtkkzqwirxoVmAs2gLIS52zZNlOqLGl1yFB5LCXE5zbt6uHWkByvfxnLoAl+lQYmyeDoCNW3T6UCpNAaNJmk6orni4rzpXFkMabjCFtwt7efc3p7uHaMZoZGZNCLaqFAb7fD4DRcyttCh3ZhiqT3LUmfrWe5Ms9CYpZNGeejm82EiYrOBrH8IljCdDjDrBtOwH5XiiFcx+aEHHzqw5e63xEozEdRblhBX0BwQvj3zHq5feji1muGyQDBHWPbYVOCdT92XV73ocHRhJc5KUAWLoSga3cFl4ffAF6TY9XUMfuLRr62vnjpmYP1YMlPvQoEFRUKHdiWwb+MFXFg8nCWp4ERZ6+Z43fozecFDzoNGL7gMqgI1IS8qZnldQp2nApfJHaveJYD5pdUX1FfNPLN7YLEwTcH5AnzCuRxim0aoMRMq+KzD6tIMjhbWqiNVgTpQA40+bzV7Y6j4w4G3mJqXoeqXPJAE97gknaurKxao97ZVKJxIgbgETgkuh/uOAOJQJzifwNnW5GVJS+1uH8qlxdirjwBGU4GT5ofeytRBGwLcn/EdBfmBsbtBvScvYtQgkhApQPT+4yQh97+GOANR2rnkS3kthlqJ2GMvAs6kqlsbX37TOwCYOXn1/QqPf1tBcZDGlmS1XMsV0xjNeWciTkUkgaiZqRVJtd1yrmiVXPRhKVZ5HXCmW5vvWJ+y9KJ3s535S1Z4IDmyRybL/5woXqQ+R2KBiwnnFREDA0uCqMebJ3p3pEc+A4y4jc0dyQFk4Wnv4+9ZvH5gh4PDv1DR9yrpuYauR7QqYiYiy1642zs5yzv2Ai4B8APL/7KTZe7hH+SfWRrucxaXABRgoPV4v+iHNqpZ37YC08AgbMX3jHun0QDln7gHvv6/kD4NB8QAAAAASUVORK5CYII=
""";

        public const string EdgeDataImage24 =
"""
data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAFRElEQVR4AXWVA7BlSdZG17czz72P5aq/8I9t27ZtBMa27ZnAmIGxbdszbXeX8eq1ni5P5t5zK6KNFbH2Pj7pFFfBbf9w1CAEVC5iqIXt0nijGEgMlxNnLlz2/vxsk4HCFdA9/vtpLkt/9XEJqACF3m1D9RlBuffEa02cRWPEcACjI2L0X9nwu16XfgCUHZtWrUm9AIKL0ENPew8XwdHF52egBGV7UD8+8XGBA5VQxakEBTRGDIAh2Vbpdpb3dHLvTcDXANbPnG+AA2QzB2Bh4UVZUIB7ifTDQOsCi6DWwIwwmaQAPIgQIXnkZGTTdbL5Vzt58GDgWW2Z8qlOT8Cx+5VDh1+WgAK6F+G/ByGlFllDkCMAHDCQIUwRWWaGWZDMPaeYWJ7ZpNEG4FFATFA+dOilBlHDY4fMfmg5I6N6LU2UAgosG1hDrUE4IACQApNj5pasWk513OTyyGTlXcBbpEg5zyYBlEH9mCWt6597Qbt6aLEZrvTwUjGDPJ2Z2jTD9K512FzGxwEKREWq2DGtkqw2ORWa1L7Z5F8CzrQb73p7rbXc1n30uIP/Oj32/OnU5rx9ywz6idZnGNdpBiviwrMvrIt/2e29PYuujlcYAy1QgIqOicvkJadKSv6yieRjoR2vPePQP/ezfKhfu5u2ZutOoZQnGqZAuCtKilGPpVNX8DJk7oaNU0YGhYiKh0+MiaTqQvDg1YF18tJa4vBxB++9vFBptmy3aLp4SpgJgEjJwY2iH9js/MdNYu2c5Rd3Nw8flTYP3X1sHgV3p3pQKoAQsaPJ9Zr5P/969PaVBb+WZjfiNiU5GIFHYDnXOhik2H3S94DHcim/rv383djQe4wzrO7jVL1QasUUcg8kzxHezYf/E5uiMzer3MWry0w4YAgPFCf+jXrqvz8B0L3zvTvrbz8FMJ7ZvvAxojzGY6DqQ0odYWoRbbhVSWU5oizk2mxwWQ7cQcIBC8fzFNp/Jpy3QLrB7Ryg3b9YNj/zQgGETwlawseUMiJpkmkhSlut7Yj6G+D8jDUXREQPp4tF4MgN1I6xw7vduvMm0m2B3+VNTdr9yW0twHWfv/cV4Ejj4j5ObWmB4hGlY+ZjU7wRIN/3ie9e/M033rQXYhOuQCEsQW8VDfum7jzAc4GP3OS1/y0A49WZnWJ0XxghSgda3AulRIqkwyn0WGB3spRy9SAifg+6LYQDFiFsNIDAsFyBmxL+3pPfeac3brrj4QQc3nqXU28q8yeL9uYo5oFerfrL8jnzXwJWd92xl8BrTnMtBF8O4pWgBIEiAAMZQAICeIPC2wv/sf1tAJO8D/gAV8MZr1txgPzrL7w9AcdF8G2Ix4NaqjfeTENK4A6WAAJ4KxEPIPyd01tXfw8MuQydze3s2nF6VrTj3fM34udIliMIAIIXh3gAivVELdGZyT6zHuuvQXdWBAAVuAvws+Hi7H4iTiD8iMKNiJ2jQ/luxGh9eHtjAKWGvOE/3/Gl2z0uIY4Cj4jgjxCZUFu2XbdJ5/wL+RQggAThAMA1L/JSwo95DzUzZwAJqIY1bDjuhxXIwJ+Ae0ZomTJuyvy2aLddt9BfcsIDGVwUgAqMgBYExIWE34vU/BlLeWKdiDY85NJ+WrrVwzJQCP4P8XGJx5MyzeI5dI6egwVgGSREQDi4o/Dv4OXFwAJw2c0fbXjYh7ksS7d4cAIqAMRtJJ5B07136i9dO19wcDb1V7Ay6qm0++T1d4R/CTgOQO6XvnvpDz4CcIWfPMgkdNmHY8s1ttM0G7V2Hmnt3AuBBS5i7pS/JmobgHMF/genpveVAhnqCgAAAABJRU5ErkJggg==
""";

        public const string ChromeDataImage24 =
"""
data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAFCklEQVR4AYWWA5AlWRqFv3Mz32uUq2a7atq2baxt27aNwNi2bdu2bXumXD31MvOe3XjxoqIVsV9a5+Tl/4sd8NSSmUGSgIIaSZp0SGqRQFIn8NoWzxJJBiLboFc3LmZL+nr7E2rCSQiLkL4haZOkCZLqagb9kp6TdB3SicC9tfdr3zKEXtu4BGr09vSlQC6pQ+IgSZ9DQlttbHVN9ciZkn4BvC6pqkGNgABBT09vapwDG4DHgc8Brr0cAcMQBmLtmUFfAB6XtA7IQSk1Yb22aSkvVwYSoGgYyDdIuq72p5mkEhIKgZAEUGCoBMQtS5MhlSQRynE9cKOzUNXUzRvnBCCuvvftnZ+Y0vhYQI2SCokEiZCkkGcUAwNgo6phQlpuQMkwRKRmkisolUP3wG1jZgCv1214Ici0Vp0u+vj4M2e/2P/5Ig1ZQCUJlCQUvb2URrXT/qH3M3zGTEhtdz9Iz8sXicFXCOVmcAGA5IyoUmjIzgK+UHmiKVHnz6ey6KNLF059eeDevY96wg2DUUUSCFXxHto+9Rla//pvbu5r4c7nAWDpRFjb8QY8+wvyl84glEZi5+AIsqkgDyZLgHvS+k+8QPub877xwk7DOGfVe4ofX/Fq2ttQoujupu3Tn2H4rvvzlWP7ufiOlygMAIngo4vaOOrbp1M/kFB56VRCqRGqJi5QTEn9jaoBKWRiU91AzgVL28Kmh7uZ9Go/2ah2Wv/6n6r4WTd3MrqtjG0AhDjnrrdAbZz2vQPQ29dB3g0hAWLAAN4IoMUnf6bd+JFQuLV3ePC6J/r1jyMfoO2H3+T+7+7OR/7zEi2NZfLCbEkaRGd/hYt/P5Z1/hGDTx9BUm6BmBssiJ3gOWm0W7DrCkHd5kI3Tm/gpql1fG3SdG5/AYoIttkW4+qzO5+DdfPnYQMkoCgwmDqgJcXGNgZkoyJy3Oqd+Fo5kESw+P8oqW0BCGCDBJhgu9N2P9FE7BGDBY+OGcFpw17zxg4gEUJsixBJgGUTgJ6HkVJAQHDNqB9Cp7Jrm1n8/Np7MItsRyBksaAxHcn1XzmGH52YcPLtXYxtLmM8JP5yV4UvLGvh1O+/S3bLSpR310oQI3YA3wcsSlGC7WupGRhCWQmv9r/F3246gBO+8w8wnH1PN4UBIAi+uaKZfb9ZR+ejv3dT9oZi2gTOwYooBvB1ACkKOPpE4Le2EwO5I03lek5/5Epsc8i3fsXvPjCGu54HgOUTob29h1Nv+4u/lp0pDWuBWAAB5AQAcyKAZh35kQQoEGdgvmA7A5dsCBJdm3sZNbKNT89cw7xRk7HhoTee4qSHbuGgMQ/w4fZAJTMJEXBmuwQ+G/g8kKRb9MFf2HzQdrWsNmkRI03D6ukZ7OPgO86liJGA6S4Svjc6+sMdZVWySCIDFNglyT3gXwAAVn7jOOY+MjsBCsM62zdgMM5wtSRbBA6IhpTIZXNeY8LwjCyaUHsXIuCNwPXYVc3/GUwAYM7DM1Igt73O5sJqScC2C+xgUIL1ZkX8dXyf/zChx4OZYqqYYAvcg+Mnq+LEoaim/KZJ1GD2g9OqD2KM7bYPAj5vG2wEbC5g/LCCqxd0MSKJFNEIA/Fs7J8Dr+FsSLxmMJktmfXAlKHAXRTFItvfwGwUntibU3fYjAF/tuPdgcEKz6eK14KHgj5xcPugn988lW2Zed/EAGjLl/sH8462NLY8uKKPiLdKW1wMJNgGItvwX49ZkLRYxcE6AAAAAElFTkSuQmCC
""";
    }

    [Flags]
    public enum AppBrowsers
    {
        Chrome = 1,
        Edge = 2,
        Firefox = 4
    }

    public enum AppBrowser
    {
        Unknown = 0,
        Chrome,
        Edge,
        Firefox
    }
}