using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Xaminer.App.Helpers;

namespace Xaminer.App.Interop.CDP
{
    public sealed class Browser : IAsyncDisposable
    {
        public event EventHandler<Exception>? OnConnectionClosed;
        public event EventHandler<int>? OnClosed;

        private readonly ConcurrentDictionary<string, Page> _pages = new ConcurrentDictionary<string, Page>();

        private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = BrowserJsonContext.Default
        };

        private static event EventHandler<Exception>? _onConnectionClosed;
        private static event EventHandler<int>? _onClosed;

        private readonly CancellationToken _token;

        private Connection _connection;
        private static DirectoryInfo? s_profileDir;
        private string? _preferFocusTargetId;

        private Browser(Connection connection, CancellationToken token)
        {
            _connection = connection;
            _token = token;

            _onConnectionClosed += RaiseOnConnectionClosed;
            _onClosed += RaiseOnClosed;
        }

        public static async Task<Browser> Create(bool headless)
        {
            var cts = new CancellationTokenSource();

            var port = GetAvailablePort();

            StartBrowser(port, headless, cts);

            var session = await WaitForSession(port);
            var connection = await Connect(new Uri(session.WebSocketDebuggerUrl), cts.Token);

            return new Browser(connection, cts.Token);
        }

        public async Task<Page> CreatePage()
        {
            var id = RandomNumberGenerator.GetInt32(int.MaxValue);
            var targetEnableCmd = new DTCommand
            (
                Id: id,
                Method: "Target.createTarget",
                Params: new DTCommandTargetCreateTarget
                (
                    Url: "about:blank",
                    NewWindow: false
                )
            );
            var responseTargetCreateTarget = await _connection.WaitIdMessage<DTResponseTargetCreateTarget>(targetEnableCmd, (msg) =>
            {
                return msg.Id == id;
            }, _token);

            var targetId = responseTargetCreateTarget!.Result.TargetId;
            var responseTargetAttachToTarget = await AttachToTarget(targetId);

            _preferFocusTargetId ??= (await GetTargetInfos()).LastOrDefault(x => x.Type == "page")?.TargetId;

            if (_preferFocusTargetId is not null)
            {
                id = RandomNumberGenerator.GetInt32(int.MaxValue);
                var targetActivateTargetCmd = new DTCommand
                (
                    Id: id,
                    Method: "Target.activateTarget",
                    Params: new DTCommandTargetActivateTarget
                    (
                        TargetId: _preferFocusTargetId
                    )
                );
                var test = await _connection.WaitIdMessage<JsonObject>(targetActivateTargetCmd, (msg) =>
                {
                    return msg.Id == id;
                }, _token);
            }

            var page = await Page.CreatePage(_connection, targetId, responseTargetAttachToTarget.Params.SessionId, this, _token);

            _pages.TryAdd(targetId, page);

            return page;
        }

        public async Task<IList<Page>> GetPages()
        {
            foreach (var target in (await GetTargetInfos()).Where(x => x.Type == "page"))
            {
                if (!_pages.ContainsKey(target.TargetId))
                {
                    var responseTargetAttachToTarget = await AttachToTarget(target.TargetId);

                    var page = await Page.CreatePage(_connection, target.TargetId, responseTargetAttachToTarget.Params.SessionId, this, _token);
                    _pages.TryAdd(target.TargetId, page);
                }
            }

            foreach (var page in _pages.Where(x => x.Value._isDisposed))
            {
                _pages.TryRemove(page.Key, out _);
            }

            return _pages.Values.ToList();
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var page in _pages)
                await page.Value.DisposeAsync();

            _connection.Dispose();

            _onConnectionClosed -= RaiseOnConnectionClosed;
            _onClosed -= RaiseOnClosed;

            await Task.Delay(TimeSpan.FromSeconds(1));

            s_profileDir?.Delete(recursive: true);
        }

        private static async Task<Connection> Connect(Uri url, CancellationToken token)
        {
            var connection = await Connection.Create(url, token);

            _ = connection.StartReciever().ContinueWith((task) =>
            {
                connection.Dispose();
                _onConnectionClosed?.Invoke(null, task.Exception!.GetBaseException());
            }, TaskContinuationOptions.OnlyOnFaulted);

            return connection;
        }

        private static void StartBrowser(int port, bool headless, CancellationTokenSource cts)
        {
            if (!AppBrowserHelpers.IsAnyInstalled(AppBrowsers.Edge | AppBrowsers.Chrome))
                throw new NotSupportedException();

            var browserExe =
                AppBrowserHelpers.IsEdgeInstalled ? AppBrowserHelpers.EdgeExe :
                AppBrowserHelpers.IsChromeInstalled ? AppBrowserHelpers.ChromeExe :
                string.Empty;

            var args = string.Join(' ', GetArguments(port, headless));
            var startInfo = new ProcessStartInfo
            {
                FileName = browserExe,
                Arguments = args,
                UseShellExecute = true
            };

            var process = new Process
            {
                StartInfo = startInfo
            };

            process.Start();

            cts.Token.Register(() =>
            {
                process.Kill();
                _onClosed?.Invoke(null, process.Id);
            });

            _ = Task.Run(async () =>
            {
                await process.WaitForExitAsync();

                cts.Cancel();
                cts.Dispose();
                process.Dispose();
            });
        }

        private static async Task<DTJsonVersionResponse> WaitForSession(int port)
        {
            const int tryCount = 4;

            using var http = new HttpClient();

            for (int i = 0; i < tryCount; i++)
            {
                try
                {
                    return (await http.GetFromJsonAsync<DTJsonVersionResponse>($"http://127.0.0.1:{port}/json/version", s_jsonOptions))!;
                }
                catch { }
            }

            throw new TimeoutException();
        }

        private static IEnumerable<string> GetArguments(int port, bool headless)
        {
            if (s_profileDir is null)
            {
                s_profileDir = new(Path.Combine(Globals.TempDataDir.FullName, "edg_profile"));

                if (s_profileDir.Exists)
                {
                    s_profileDir.Delete(recursive: true);
                }
            }

            yield return "about:blank";
            yield return $"--remote-debugging-port={port}";
            yield return "--no-first-run";
            yield return "--enable-automation";
            yield return "--disable-prompt-on-repost";
            yield return "--disable-extensions";
            yield return "--disable-component-update";
            yield return "--disable-sync";
            yield return "--disable-features=Translate,BackForwardCache,AcceptCHFrame,MediaRouter,OptimizationHints";
            yield return "--disable-background-networking";
            yield return "--disable-default-apps";
            yield return "--disable-infobars";
            yield return "--disable-client-side-phishing-detection";
            yield return "--disable-gpu";
            yield return "--disable-notifications";
            yield return "--disable-breakpad";
            yield return "--profile-directory=Rescue";
            yield return "--no-profiles";
            yield return $"""--user-data-dir="{s_profileDir.FullName}" """;

            if (headless)
                yield return "--headless";

#if RELEASE
            yield return "--start-maximized";
#endif
        }

        private static int GetAvailablePort()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            socket.Bind(new IPEndPoint(IPAddress.Any, 0));

            if (socket.LocalEndPoint is not IPEndPoint endPoint)
                throw new InvalidOperationException();

            return endPoint.Port;
        }

        private async Task<IEnumerable<DTResponseGetTargetsResultTargetInfos>> GetTargetInfos()
        {
            var id = RandomNumberGenerator.GetInt32(int.MaxValue);
            var targetGetTargetsCmd = new DTCommand
            (
                Id: id,
                Method: "Target.getTargets"
            );
            var responseGetTargets = await _connection.WaitIdMessage<DTResponseGetTargets>(targetGetTargetsCmd, (msg) =>
            {
                return msg.Id == id;
            }, _token);
            return responseGetTargets.Result.TargetInfos;
        }

        private async Task<DTResponseTargetAttachToTarget> AttachToTarget(string targetId)
        {
            var id = RandomNumberGenerator.GetInt32(int.MaxValue);
            var targetAttachToTargetCmd = new DTCommand
            (
                Id: id,
                Method: "Target.attachToTarget",
                Params: new DTCommandTargetAttachToTarget
                (
                    TargetId: targetId,
                    Flatten: true
                )
            );
            var responseTargetAttachToTarget = await _connection.WaitMethodMessage<DTResponseTargetAttachToTarget>(targetAttachToTargetCmd, (msg) =>
            {
                return msg.Method == "Target.attachedToTarget" &&
                msg.Json["params"]?["targetInfo"]?["targetId"]?.GetValue<string>() == targetId;
            }, _token);
            return responseTargetAttachToTarget;
        }

        private void RaiseOnConnectionClosed(object? sender, Exception e) => OnConnectionClosed?.Invoke(sender, e);

        private void RaiseOnClosed(object? sender, int e) => OnClosed?.Invoke(sender, e);
    }

    [JsonSerializable(typeof(DTJsonVersionResponse))]
    public partial class BrowserJsonContext : JsonSerializerContext { }

    public sealed record DTJsonVersionResponse(string Browser,
        [property: JsonPropertyName("Protocol-Version")] string ProtocolVersion,
        [property: JsonPropertyName("User-Agent")] string UserAgent,
        [property: JsonPropertyName("V8-Version")] string V8Version,
        [property: JsonPropertyName("WebKit-Version")] string WebKitVersion,
        string WebSocketDebuggerUrl);

}
