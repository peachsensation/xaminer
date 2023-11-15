using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace Xaminer.App.Interop.CDP
{
    public sealed class Connection : IDisposable
    {
        public Uri ConnectionUrl;

        private readonly ConcurrentDictionary<Guid, Channel<IdMessageResponse>> _idMessageChannels = new();
        private readonly ConcurrentDictionary<Guid, Channel<MethodMessageResponse>> _methodMessageChannels = new();
        private readonly ClientWebSocket _client;
        private readonly CancellationToken _token;

        private readonly Channel<DTCommand> _commands = Channel.CreateBounded<DTCommand>(new BoundedChannelOptions(10)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = ConnectionJsonContext.Default
        };

        private Connection(Uri url, ClientWebSocket client, CancellationToken token)
        {
            ConnectionUrl = url;
            _client = client;
            _token = token;
        }

        public static async Task<Connection> Create(Uri url, CancellationToken token)
        {
            var client = new ClientWebSocket();
            client.Options.KeepAliveInterval = TimeSpan.Zero;

            await client.ConnectAsync(url, token);

            return new Connection(url, client, token);
        }

        public async Task<TResponse> WaitIdMessage<TResponse>(DTCommand request, Func<IdMessageResponse, bool> check, CancellationToken token)
        {
            var channel = Channel.CreateUnbounded<IdMessageResponse>();
            var guid = Guid.NewGuid();
            _idMessageChannels.TryAdd(guid, channel);

            await SendMessage(request, token);

            var result = default(TResponse);
            await foreach (var msg in channel.Reader.ReadAllAsync(token))
            {
                if (check(msg))
                {
                    result = (TResponse?)msg.Json.Deserialize(typeof(TResponse), s_jsonOptions);
                    break;
                }
            }

            channel.Writer.Complete();
            _idMessageChannels.TryRemove(guid, out _);

            return result!;
        }

        public async Task<TResponse> WaitMethodMessage<TResponse>(DTCommand request, Func<MethodMessageResponse, bool> check, CancellationToken token)
        {
            var channel = Channel.CreateUnbounded<MethodMessageResponse>();
            var guid = Guid.NewGuid();
            _methodMessageChannels.TryAdd(guid, channel);

            await SendMessage(request, token);

            var result = default(TResponse);
            await foreach (var msg in channel.Reader.ReadAllAsync(token))
            {
                if (check(msg))
                {
                    result = (TResponse?)msg.Json.Deserialize(typeof(TResponse), s_jsonOptions);
                    break;
                }
            }

            channel.Writer.Complete();
            _methodMessageChannels.TryRemove(guid, out _);

            return result!;
        }

        public async IAsyncEnumerable<TResponse> WaitMethodMessages<TResponse>(Func<MethodMessageResponse, bool> check,
            [EnumeratorCancellation] CancellationToken token)
        {
            var channel = Channel.CreateUnbounded<MethodMessageResponse>();
            var guid = Guid.NewGuid();
            _methodMessageChannels.TryAdd(guid, channel);

            await foreach (var msg in channel.Reader.ReadAllAsync(token))
            {
                if (check(msg))
                {
                    yield return (TResponse?)msg.Json.Deserialize(typeof(TResponse), s_jsonOptions)!;
                }
            }

            channel.Writer.Complete();
            _methodMessageChannels.TryRemove(guid, out _);
        }

        public async IAsyncEnumerable<TResponse> WaitMethodMessages<TResponse>(DTCommand request, Func<MethodMessageResponse, bool> check,
            [EnumeratorCancellation] CancellationToken token)
        {
            var channel = Channel.CreateUnbounded<MethodMessageResponse>();
            var guid = Guid.NewGuid();
            _methodMessageChannels.TryAdd(guid, channel);

            await SendMessage(request, token);

            await foreach (var msg in channel.Reader.ReadAllAsync(token))
            {
                if (check(msg))
                {
                    yield return (TResponse?)msg.Json.Deserialize(typeof(TResponse), s_jsonOptions)!;
                }
            }

            channel.Writer.Complete();
            _methodMessageChannels.TryRemove(guid, out _);
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        private async Task SendMessage(DTCommand request, CancellationToken token)
        {
            var msg = await CreateRequestMessage(request, token);
            await _client.SendAsync(msg, WebSocketMessageType.Text, endOfMessage: true, token).AsTask();
        }

        public async Task StartReciever()
        {
            while (!_token.IsCancellationRequested)
            {
                await using var stream = new MemoryStream();
                var buffer = new Memory<byte>(new byte[2048]);

                ValueWebSocketReceiveResult result;
                do
                {
                    result = await _client.ReceiveAsync(buffer, _token);
                    await stream.WriteAsync(buffer[..result.Count], _token);

                } while (!result.EndOfMessage);

                stream.Seek(0, SeekOrigin.Begin);

                if (await JsonSerializer.DeserializeAsync<JsonObject>(stream, s_jsonOptions, _token) is not { } jsonObject)
                    continue;

                var errors = new List<string>();

                if (jsonObject.TryGetPropertyValue("error", out var error) && error?["message"]?.GetValue<string>() is { } message)
                {
                    errors.Add(message);
                    while (_commands.Reader.Count > 0)
                    {
                        var command = await _commands.Reader.ReadAsync(_token);
                        if (command.Id == jsonObject["id"]?.GetValue<int>())
                        {
                            errors.Add(command.ToString());
                            break;
                        }
                    }

                    Program.LogConsole<Connection>(string.Join(" | ", errors));
                }

                if (jsonObject.TryGetPropertyValue("id", out var idProp) && idProp?.GetValue<int>() is int id)
                {
                    var sessionId = jsonObject["sessionId"]?.GetValue<string>();

                    Program.LogConsole<Connection>($"RECEIVE: {id} | {sessionId}");

                    foreach (var idMessageChannel in _idMessageChannels)
                    {
                        await idMessageChannel.Value.Writer.WriteAsync(new IdMessageResponse
                        (
                            Id: id,
                            SessionId: sessionId,
                            Json: jsonObject,
                            Errors: errors
                        ));
                    }
                }
                else if (jsonObject.TryGetPropertyValue("method", out var methodProp) && methodProp?.GetValue<string>() is string method)
                {
                    var sessionId = jsonObject["sessionId"]?.GetValue<string>();

                    Program.LogConsole<Connection>($"RECEIVE: {method} | {sessionId}");

                    foreach (var idMessageChannel in _methodMessageChannels)
                    {
                        await idMessageChannel.Value.Writer.WriteAsync(new MethodMessageResponse
                        (
                            Method: method,
                            SessionId: sessionId,
                            Json: jsonObject,
                            Errors: errors
                        ));
                    }
                }
            }
        }

        private async Task<Memory<byte>> CreateRequestMessage(DTCommand message, CancellationToken token)
        {
            Program.LogConsole<Connection>($"SEND: {message.Id} | {message.Method} | {message.SessionId}");

            await _commands.Writer.WriteAsync(message, token);

            await using var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, message, s_jsonOptions, token);
            return new Memory<byte>(stream.ToArray());
        }
    }

    public sealed record IdMessageResponse(int Id, string? SessionId, JsonObject Json, IEnumerable<string> Errors);
    public sealed record MethodMessageResponse(string Method, string? SessionId, JsonObject Json, IEnumerable<string> Errors);

    [JsonSerializable(typeof(DTCommand))]
    [JsonSerializable(typeof(DTCommandPageNavigate))]
    [JsonSerializable(typeof(DTCommandPageSetDocumentContent))]
    [JsonSerializable(typeof(DTCommandPageFrameNavigated))]
    [JsonSerializable(typeof(DTCommandPageSetLifecycleEventsEnabled))]
    [JsonSerializable(typeof(DTCommandPageReload))]
    [JsonSerializable(typeof(DTCommandTargetCreateTarget))]
    [JsonSerializable(typeof(DTCommandTargetActivateTarget))]
    [JsonSerializable(typeof(DTCommandTargetAttachToTarget))]
    [JsonSerializable(typeof(DTCommandTargetCloseTarget))]
    [JsonSerializable(typeof(DTCommandRuntimeEvaluate))]
    [JsonSerializable(typeof(DTCommandRuntimeGetProperties))]
    [JsonSerializable(typeof(DTCommandRuntimeCallFunctionOn))]

    [JsonSerializable(typeof(DTResponseTargetCreateTarget))]
    [JsonSerializable(typeof(DTResponseTargetAttachToTarget))]
    [JsonSerializable(typeof(DTResponseGetTargets))]
    [JsonSerializable(typeof(DTResponsePageEnable))]
    [JsonSerializable(typeof(DTResponsePageLifecycleEvent))]
    [JsonSerializable(typeof(DTResponsePageNavigate))]
    [JsonSerializable(typeof(DTResponsePageGetFrameTree))]
    [JsonSerializable(typeof(DTResponsePageGetNavigationHistory))]
    [JsonSerializable(typeof(DTResponsePageFrameNavigated))]
    [JsonSerializable(typeof(DTResponsePageFrameNavigatedWithinDocument))]
    [JsonSerializable(typeof(DTResponseRuntimeExecutionContextCreated))]
    [JsonSerializable(typeof(DTResponseRuntimeEvaluate))]
    [JsonSerializable(typeof(DTResponseRuntimeCallFunctionOn))]

    [JsonSerializable(typeof(JsonObject))]
    [JsonSerializable(typeof(JsonArray))]
    public partial class ConnectionJsonContext : JsonSerializerContext { }

    public sealed record DTCommand(int Id, string Method, string? SessionId = null, string? TargetId = null, object? Params = null);
    public sealed record DTCommandPageNavigate(string Url);
    public sealed record DTCommandPageSetDocumentContent(string FrameId, string Html);
    public sealed record DTCommandPageFrameNavigated(string FrameId, string Type);
    public sealed record DTCommandPageSetLifecycleEventsEnabled(bool Enabled);
    public sealed record DTCommandPageReload(string FrameId);

    public sealed record DTCommandTargetCreateTarget(string Url, bool NewWindow);
    public sealed record DTCommandTargetActivateTarget(string TargetId);
    public sealed record DTCommandTargetAttachToTarget(string TargetId, bool Flatten);
    public sealed record DTCommandTargetCloseTarget(string TargetId);

    public sealed record DTCommandRuntimeEvaluate(string Expression, int? contextId = null, bool? ReturnByValue = null, bool? AwaitPromise = null, bool? UserGesture = null);
    public sealed record DTCommandRuntimeGetProperties(string ObjectId, bool? OwnProperties = null);
    public sealed record DTCommandRuntimeCallFunctionOn(string FunctionDeclaration,
        int ExecutionContextId, bool ReturnByValue, bool AwaitPromise, bool UserGesture, IEnumerable<DTCommandRuntimeCallFunctionOnArguments>? Arguments = null);
    public sealed record DTCommandRuntimeCallFunctionOnArguments(string Value);

    public sealed record DTResponseTargetCreateTarget(int Id, DTResponseTargetCreateTargetResult Result);
    public sealed record DTResponseTargetCreateTargetResult(string TargetId);
    public sealed record DTResponseTargetAttachToTarget(string Method, DTResponseTargetAttachToTargetParams Params);
    public sealed record DTResponseTargetAttachToTargetParams(string SessionId, bool WaitingForDebugger, DTResponseTargetAttachToTargetParamsTargetInfo TargetInfo);
    public sealed record DTResponseTargetAttachToTargetParamsTargetInfo(string TargetId, string Type, string Title, string Url, bool Attached, bool CanAccessOpener, string BrowserContextId);
    public sealed record DTResponseGetTargets(int Id, DTResponseGetTargetsResult Result);
    public sealed record DTResponseGetTargetsResult(IEnumerable<DTResponseGetTargetsResultTargetInfos> TargetInfos);
    public sealed record DTResponseGetTargetsResultTargetInfos(string TargetId, string Type, string Title, string Url, bool Attached,
        bool CanAccessOpener, string BrowserContextId, int Pid);

    public sealed record DTResponsePageEnable(int Id, DTResponsePageEnableResult Result);
    public sealed record DTResponsePageEnableResult(string SessionId);
    public sealed record DTResponsePageLifecycleEvent(string Method, string SessionId, DTResponsePageLifecycleEventParams Params);
    public sealed record DTResponsePageLifecycleEventParams(string FrameId, string LoaderId, string Name, float Timestamp);
    public sealed record DTResponsePageNavigate(int Id, string SessionId, DTResponsePageNavigateResult Result);
    public sealed record DTResponsePageNavigateResult(string FrameId, string LoaderId);
    public sealed record DTResponsePageGetFrameTree(int Id, string sessionId, DTResponsePageGetFrameTreeResult Result);
    public sealed record DTResponsePageGetFrameTreeResult(DTResponsePageGetFrameTreeResultFrameTree FrameTree);
    public sealed record DTResponsePageGetFrameTreeResultFrameTree(DTResponsePageGetFrameTreeResultFrameTreeFrame Frame,
        IEnumerable<DTResponsePageGetFrameTreeResultFrameTreeFrame> ChildFrames);
    public sealed record DTResponsePageGetFrameTreeResultFrameTreeFrame(string Id, int ExecutionContextId, string LoaderId, string Url, string DomainAndRegistry,
        string SecurityOrigin, string MimeType, DTResponsePageGetFrameTreeResultFrameTreeFrameAdFrameStatus AdFrameStatus, string SecureContextType, string CrossOriginIsolatedContextType, JsonArray GatedAPIFeatures);
    public sealed record DTResponsePageGetFrameTreeResultFrameTreeFrameAdFrameStatus(string AdFrameType);
    public sealed record DTResponsePageGetNavigationHistory(int Id, string sessionId, DTResponsePageGetNavigationHistoryResult Result);
    public sealed record DTResponsePageGetNavigationHistoryResult(int CurrentIndex, IList<DTResponsePageGetNavigationHistoryResultEntry> Entries);
    public sealed record DTResponsePageGetNavigationHistoryResultEntry(int Id, string Url, string UserTypedURL, string Title, string TransitionType);
    public sealed record DTResponsePageFrameNavigated(string Method, string SessionId, DTResponsePageFrameNavigatedParams Params);
    public sealed record DTResponsePageFrameNavigatedParams(DTResponsePageFrameNavigatedParamsFrame Frame, string Type);
    public sealed record DTResponsePageFrameNavigatedParamsFrame(string Id, string LoaderId, string Url, string? UrlFragment, string DomainAndRegistry,
        string SecurityOrigin, string MimeType, DTResponsePageFrameNavigatedParamsFrameAdFrameStatus AdFrameStatus, string SecureContextType,
        string CrossOriginIsolatedContextType, JsonArray GatedAPIFeatures);
    public sealed record DTResponsePageFrameNavigatedParamsFrameAdFrameStatus(string AdFrameType);
    public sealed record DTResponsePageFrameNavigatedWithinDocument(string Method, string SessionId, DTResponsePageFrameNavigatedWithinDocumentParams Params);
    public sealed record DTResponsePageFrameNavigatedWithinDocumentParams(string FrameId, string Url);

    public sealed record DTResponseRuntimeExecutionContextCreated(string Method, string SessionId, DTResponseRuntimeExecutionContextParams Params);
    public sealed record DTResponseRuntimeExecutionContextParams(DTResponseRuntimeExecutionContextParamsContext Context);
    public sealed record DTResponseRuntimeExecutionContextParamsContext(int Id, string Origin, string Name, string UniqueId, DTResponseRuntimeExecutionContextParamsContextAuxData AuxData);
    public sealed record DTResponseRuntimeExecutionContextParamsContextAuxData(bool IsDefault, string Type, string FrameId);
    public sealed record DTResponseRuntimeEvaluate(int Id, string sessionId, DTResponseRuntimeEvaluateResult Result);
    public sealed record DTResponseRuntimeEvaluateResult(DTResponseRuntimeEvaluateResultResult Result);
    public sealed record DTResponseRuntimeEvaluateResultResult(string Type, string ClassName, string Description, string ObjectId);
    public sealed record DTResponseRuntimeCallFunctionOn(int Id, string sessionId, DTResponseRuntimeCallFunctionOnResult Result);
    public sealed record DTResponseRuntimeCallFunctionOnResult(DTResponseRuntimeCallFunctionOnResultResult Result);
    public sealed record DTResponseRuntimeCallFunctionOnResultResult(string Type, string Value);
}
