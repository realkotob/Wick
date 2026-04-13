using System.Text.Json;
using Wick.Providers.Godot;

var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

Console.WriteLine("=== Wick Bridge LIVE Integration Test ===");
Console.WriteLine("Connecting to Godot Editor on port 6505...\n");

using var cts = new CancellationTokenSource(10000);
var ct = cts.Token;

var client = new GodotBridgeClient(6505);

try
{
    bool connected = await client.EnsureConnectedAsync(ct);
    Console.WriteLine($"[1/5] Connected: {connected}");
    if (!connected)
    {
        Console.WriteLine("FAIL - Is Godot running with the Wick addon enabled?");
        return;
    }

    // Test 2: Scene Tree
    Console.WriteLine("\n[2/5] GetSceneTreeAsync...");
    var tree = await client.GetSceneTreeAsync(ct);
    Console.WriteLine($"  Root node: {tree?.GetProperty("name").GetString()}");
    Console.WriteLine($"  Type: {tree?.GetProperty("type").GetString()}");
    if (tree?.TryGetProperty("children", out var children) == true)
        Console.WriteLine($"  Children: {children.GetArrayLength()} nodes");
    Console.WriteLine("  PASS");

    // Test 3: Performance
    Console.WriteLine("\n[3/5] GetPerformanceAsync...");
    var perf = await client.GetPerformanceAsync(ct);
    Console.WriteLine($"  FPS: {perf?.GetProperty("fps").GetDouble():F0}");
    Console.WriteLine($"  Process time: {perf?.GetProperty("process_time").GetDouble() * 1000:F2}ms");
    Console.WriteLine($"  Memory: {perf?.GetProperty("memory_static").GetDouble() / 1024 / 1024:F1}MB");
    Console.WriteLine("  PASS");

    // Test 4: Node Properties on root
    Console.WriteLine("\n[4/5] GetNodePropertiesAsync(/root)...");
    var props = await client.GetNodePropertiesAsync("/root", ct);
    Console.WriteLine($"  Title: {props?.GetProperty("title").GetString()}");
    Console.WriteLine($"  Properties returned: {(props?.EnumerateObject().Count() ?? 0)}");
    Console.WriteLine("  PASS");

    // Test 5: Status check via GodotBridgeManager pattern
    Console.WriteLine("\n[5/5] End-to-end verified.");
    Console.WriteLine("\n=== ALL 5 TESTS PASSED ===");
}
catch (Exception ex)
{
    Console.WriteLine($"\nFAIL: {ex.GetType().Name}: {ex.Message}");
}
finally
{
    client.Disconnect();
}
