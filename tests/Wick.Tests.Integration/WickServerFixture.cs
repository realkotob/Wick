using ModelContextProtocol.Client;

namespace Wick.Tests.Integration;

/// <summary>
/// Shared fixture that launches the Wick MCP server as a subprocess
/// and provides an McpClient connected to it via StdioClientTransport.
/// Implements IAsyncLifetime so xUnit manages the lifecycle per test class.
/// </summary>
public sealed class WickServerFixture : IAsyncLifetime
{
    private readonly IReadOnlyDictionary<string, string?> _envOverrides;
    private McpClient? _client;

    /// <summary>
    /// Initializes a new fixture with no environment variable overrides.
    /// Required by xUnit's class fixture construction (single public constructor rule).
    /// </summary>
    public WickServerFixture()
    {
        _envOverrides = new Dictionary<string, string?>();
    }

    /// <summary>
    /// Creates a fixture that will set the specified environment variable overrides on the
    /// server subprocess. Null values remove the inherited variable from the subprocess environment.
    /// Use this factory method when constructing a fixture manually in test code rather than via
    /// IClassFixture (which requires a single public parameterless constructor).
    /// </summary>
    internal static WickServerFixture WithEnv(IReadOnlyDictionary<string, string?> envOverrides)
        => new(envOverrides);

    private WickServerFixture(IReadOnlyDictionary<string, string?> envOverrides)
    {
        _envOverrides = envOverrides;
    }

    /// <summary>
    /// The MCP client connected to the running Wick server.
    /// Available after InitializeAsync completes.
    /// </summary>
    public McpClient Client => _client
        ?? throw new InvalidOperationException("Fixture not initialized — call InitializeAsync first");

    /// <summary>
    /// Stderr output from the server process, captured for diagnostics.
    /// </summary>
    public System.Collections.Concurrent.ConcurrentQueue<string> ServerStderr { get; } = new();

    public async ValueTask InitializeAsync()
    {
        // Resolve the repo root by walking up from the test assembly location until
        // we find Wick.slnx. This is robust to different output layouts (standard
        // bin/Release/net10.0/ and the centralized ArtifactsDir/ pattern).
        var dir = new DirectoryInfo(Path.GetDirectoryName(typeof(WickServerFixture).Assembly.Location)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Wick.slnx")))
        {
            dir = dir.Parent;
        }

        var repoRoot = dir?.FullName
            ?? throw new DirectoryNotFoundException(
                "Could not locate Wick.slnx by walking up from the test assembly. " +
                "Integration tests must run from within the Wick repo.");

        var serverProjectPath = Path.Combine(repoRoot, "src", "Wick.Server");

        if (!Directory.Exists(serverProjectPath))
        {
            throw new DirectoryNotFoundException(
                $"Wick.Server project not found at: {serverProjectPath}. " +
                $"Repo root resolved to: {repoRoot}.");
        }

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "wick-integration-test",
            Command = "dotnet",
            Arguments = ["run", "--project", serverProjectPath, "--no-build", "--configuration", "Release"],
            ShutdownTimeout = TimeSpan.FromSeconds(10),
            StandardErrorLines = line => ServerStderr.Enqueue(line),
            EnvironmentVariables = _envOverrides.ToDictionary(kv => kv.Key, kv => kv.Value),
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }

        _client = null;
    }
}
