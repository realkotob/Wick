using System.Text.Json;
using Wick.Core;
using StreamJsonRpc;

namespace Wick.Providers.Godot;

/// <summary>
/// A JSON-RPC TCP client that connects to the Godot Engine.
/// Can be multiplexed into either the Editor (Port 6505) or Runtime (Port 7777).
/// Uses plain NewLine delimited JSON for simple GDScript integration on the server side.
/// </summary>
public sealed class GodotBridgeClient : HeaderDelimitedRpcClient
{
    private sealed class BridgeTarget
    {
        [JsonRpcMethod("bridge/log")]
        public static void OnLog(string message)
        {
            Console.Error.WriteLine($"[GodotBridge] {message}");
        }
    }

    public int Port { get; }

    public GodotBridgeClient(int port) : base(new BridgeTarget())
    {
        Port = port;
    }

    protected override IJsonRpcMessageHandler CreateMessageHandler(Stream writeStream, Stream readStream, IJsonRpcMessageFormatter formatter)
    {
        // Godot StreamPeerTCP simply reads/writes strings terminated by newlines natively.
        // It's exceptionally heavy in GDScript to parse LSP Content-Length HTTP headers byte-by-byte.
        // Bypassing headers for this bridge keeps the GDScript server plugin at ~5 LOC instead of ~100 LOC.
        var reader = System.IO.Pipelines.PipeReader.Create(readStream);
        var writer = System.IO.Pipelines.PipeWriter.Create(writeStream);
        return new NewLineDelimitedMessageHandler(writer, reader, (IJsonRpcMessageTextFormatter)formatter);
    }

    public async Task<bool> EnsureConnectedAsync(CancellationToken ct = default)
    {
        if (IsConnected) return true;
        // Godot runs on local host for these internal RPCs
        return await ConnectAsync("127.0.0.1", Port, ct);
    }

    public async Task<JsonElement?> GetSceneTreeAsync(CancellationToken ct = default)
    {
        return await SendRequestAsync<JsonElement>("editor_scene_tree", null, ct);
    }

    public async Task<JsonElement?> GetNodePropertiesAsync(string nodePath, CancellationToken ct = default)
    {
        return await SendRequestAsync<JsonElement>("editor_node_properties", new { nodePath }, ct);
    }

    public async Task<JsonElement?> CallMethodAsync(string nodePath, string method, object[] args, CancellationToken ct = default)
    {
        return await SendRequestAsync<JsonElement>("editor_call_method", new { nodePath, method, args }, ct);
    }

    public async Task<JsonElement?> SetPropertyAsync(string nodePath, string property, object value, CancellationToken ct = default)
    {
        return await SendRequestAsync<JsonElement>("editor_set_property", new { nodePath, property, value }, ct);
    }

    public async Task<JsonElement?> RunSceneAsync(string scenePath, CancellationToken ct = default)
    {
        return await SendRequestAsync<JsonElement>("editor_run_scene", new { scenePath }, ct);
    }

    public async Task StopSceneAsync(CancellationToken ct = default)
    {
        await SendRequestAsync("editor_stop", null, ct);
    }

    public async Task<JsonElement?> GetPerformanceAsync(CancellationToken ct = default)
    {
        return await SendRequestAsync<JsonElement>("editor_performance", null, ct);
    }
}
