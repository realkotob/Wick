using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Wick.Providers.Godot;

/// <summary>
/// Abstraction for the Wick.Runtime companion's TCP RPC. Separated as an interface so the
/// MCP tool layer can mock it in unit tests and so a future "no bridge available" null
/// implementation can plug in cleanly.
/// </summary>
public interface IInProcessBridgeClient
{
    Task<BridgeResponse> GetSceneTreeAsync(int maxDepth, CancellationToken ct);
    Task<BridgeResponse> GetNodePropertiesAsync(string nodePath, CancellationToken ct);
    Task<BridgeResponse> CallMethodAsync(string nodePath, string method, IReadOnlyList<object?> args, CancellationToken ct);
    Task<BridgeResponse> SetPropertyAsync(string nodePath, string propertyName, object? value, CancellationToken ct);
    Task<BridgeResponse> FindNodesInGroupAsync(string group, CancellationToken ct);
}

/// <summary>
/// Uniform response shape returned by every bridge RPC. <see cref="Ok"/> mirrors the wire
/// protocol; when false, <see cref="ErrorCode"/> is one of the closed set defined in
/// <see cref="WickBridgeErrorCodes"/>.
/// </summary>
public sealed record BridgeResponse(
    bool Ok,
    JsonElement? Result,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>Closed set of bridge error codes.</summary>
public static class WickBridgeErrorCodes
{
    public const string NoLiveBridge = "no_live_bridge";
    public const string UnknownMethod = "unknown_method";
    public const string NodeNotFound = "node_not_found";
    public const string MethodNotFound = "method_not_found";
    public const string PropertyNotFound = "property_not_found";
    public const string InvalidParams = "invalid_params";
    public const string ConnectionRefused = "connection_refused";
    public const string Timeout = "timeout";
    public const string Internal = "internal";
}

/// <summary>
/// TCP client for <c>Wick.Runtime.Bridge.WickBridgeServer</c>. Opens a fresh connection
/// per call (matches the v1 server's one-request-per-connection model) and times out at
/// 5 seconds. Thread-safe: every method is independent.
/// </summary>
public sealed class InProcessBridgeClient : IInProcessBridgeClient
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly string _host;
    private readonly int _port;
    private readonly TimeSpan _timeout;

    public InProcessBridgeClient(int port, string host = "127.0.0.1", TimeSpan? timeout = null)
    {
        _host = host;
        _port = port;
        _timeout = timeout ?? DefaultTimeout;
    }

    public int Port => _port;

    public Task<BridgeResponse> GetSceneTreeAsync(int maxDepth, CancellationToken ct)
        => SendAsync("get_scene_tree", new Dictionary<string, object?> { ["max_depth"] = maxDepth }, ct);

    public Task<BridgeResponse> GetNodePropertiesAsync(string nodePath, CancellationToken ct)
        => SendAsync("get_node_properties", new Dictionary<string, object?> { ["node_path"] = nodePath }, ct);

    public Task<BridgeResponse> CallMethodAsync(string nodePath, string method, IReadOnlyList<object?> args, CancellationToken ct)
        => SendAsync("call_method", new Dictionary<string, object?>
        {
            ["node_path"] = nodePath,
            ["method"] = method,
            ["args"] = args,
        }, ct);

    public Task<BridgeResponse> SetPropertyAsync(string nodePath, string propertyName, object? value, CancellationToken ct)
        => SendAsync("set_property", new Dictionary<string, object?>
        {
            ["node_path"] = nodePath,
            ["property"] = propertyName,
            ["value"] = value,
        }, ct);

    public Task<BridgeResponse> FindNodesInGroupAsync(string group, CancellationToken ct)
        => SendAsync("find_nodes_in_group", new Dictionary<string, object?> { ["group"] = group }, ct);

    private async Task<BridgeResponse> SendAsync(string method, Dictionary<string, object?> @params, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_timeout);

        TcpClient client;
        try
        {
            client = new TcpClient();
            await client.ConnectAsync(_host, _port, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            return new BridgeResponse(false, null, WickBridgeErrorCodes.ConnectionRefused, ex.Message);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new BridgeResponse(false, null, WickBridgeErrorCodes.Timeout, "Connect timed out");
        }

        using (client)
        {
            try
            {
                using var stream = client.GetStream();
                var request = new Dictionary<string, object?>
                {
                    ["method"] = method,
                    ["params"] = @params,
                };
                var reqLine = JsonSerializer.Serialize(request) + "\n";
                var bytes = Encoding.UTF8.GetBytes(reqLine);
                await stream.WriteAsync(bytes, timeoutCts.Token).ConfigureAwait(false);
                await stream.FlushAsync(timeoutCts.Token).ConfigureAwait(false);

                using var reader = new StreamReader(stream, Encoding.UTF8);
                var responseLine = await reader.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
                if (responseLine is null)
                {
                    return new BridgeResponse(false, null, WickBridgeErrorCodes.Internal, "Empty response");
                }

                using var doc = JsonDocument.Parse(responseLine);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return new BridgeResponse(false, null, WickBridgeErrorCodes.Internal, "Malformed response (not object)");
                }
                var ok = root.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.True;
                if (ok)
                {
                    var result = root.TryGetProperty("result", out var r) ? (JsonElement?)r.Clone() : null;
                    return new BridgeResponse(true, result, null, null);
                }

                string? code = null;
                string? msg = null;
                if (root.TryGetProperty("error", out var errEl) && errEl.ValueKind == JsonValueKind.Object)
                {
                    if (errEl.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String) code = c.GetString();
                    if (errEl.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String) msg = m.GetString();
                }
                return new BridgeResponse(false, null, code ?? WickBridgeErrorCodes.Internal, msg);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return new BridgeResponse(false, null, WickBridgeErrorCodes.Timeout, "Request timed out");
            }
            catch (JsonException ex)
            {
                return new BridgeResponse(false, null, WickBridgeErrorCodes.Internal, $"Malformed response JSON: {ex.Message}");
            }
            catch (IOException ex)
            {
                return new BridgeResponse(false, null, WickBridgeErrorCodes.Internal, ex.Message);
            }
            catch (SocketException ex)
            {
                return new BridgeResponse(false, null, WickBridgeErrorCodes.ConnectionRefused, ex.Message);
            }
        }
    }
}

/// <summary>
/// Factory / registry that holds the current <see cref="IInProcessBridgeClient"/>
/// installed by the <see cref="ProcessExceptionSource"/> handshake callback. Kept as a
/// simple singleton because v1 supports exactly one agent-launched game at a time.
/// </summary>
public sealed class InProcessBridgeClientFactory
{
    private readonly object _lock = new();
    private IInProcessBridgeClient? _current;

    public IInProcessBridgeClient? Current
    {
        get { lock (_lock) { return _current; } }
    }

    public void InstallFromHandshake(int port)
    {
        lock (_lock)
        {
            _current = new InProcessBridgeClient(port);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _current = null;
        }
    }

    /// <summary>Test seam: set a stub client directly.</summary>
    public void SetForTesting(IInProcessBridgeClient? client)
    {
        lock (_lock)
        {
            _current = client;
        }
    }
}
