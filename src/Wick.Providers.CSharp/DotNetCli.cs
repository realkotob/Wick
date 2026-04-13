using System.Diagnostics;
using System.Text;

namespace Wick.Providers.CSharp;

/// <summary>
/// Abstraction over <c>dotnet</c> CLI invocation so build tools can be unit-tested
/// without spawning real processes. The production implementation
/// (<see cref="DotNetCli"/>.RunAsync wrapped by <see cref="DefaultDotNetCli"/>) shells out;
/// tests supply a canned <see cref="CliResult"/>.
/// </summary>
public interface IDotNetCli
{
    Task<CliResult> RunAsync(string arguments, string workingDirectory, int timeoutSeconds = 120, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IDotNetCli"/> that shells out via <see cref="DotNetCli.RunAsync"/>.
/// </summary>
public sealed class DefaultDotNetCli : IDotNetCli
{
    public Task<CliResult> RunAsync(string arguments, string workingDirectory, int timeoutSeconds = 120, CancellationToken cancellationToken = default)
        => DotNetCli.RunAsync(arguments, workingDirectory, timeoutSeconds, cancellationToken);
}

/// <summary>
/// Wraps the dotnet CLI for build, test, clean, and package management operations.
/// </summary>
public static class DotNetCli
{
    /// <summary>
    /// Runs a dotnet CLI command and returns the result.
    /// </summary>
    public static async Task<CliResult> RunAsync(string arguments, string workingDirectory, int timeoutSeconds = 120, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return new CliResult
            {
                ExitCode = -1,
                Output = stdout.ToString(),
                Error = "Command timed out or was cancelled.",
                TimedOut = true,
            };
        }

        return new CliResult
        {
            ExitCode = process.ExitCode,
            Output = stdout.ToString(),
            Error = stderr.ToString(),
        };
    }
}

public sealed class CliResult
{
    public int ExitCode { get; init; }
    public string Output { get; init; } = "";
    public string Error { get; init; } = "";
    public bool TimedOut { get; init; }
    public bool Success => ExitCode == 0 && !TimedOut;
}
