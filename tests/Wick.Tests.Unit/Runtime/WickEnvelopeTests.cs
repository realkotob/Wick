using System.Text.Json;
using Wick.Runtime;

namespace Wick.Tests.Unit.Runtime;

[Collection("ConsoleError")]
public sealed class WickEnvelopeTests
{
    [Fact]
    public void Format_ProducesSingleLineJsonWithMarkerAndKind()
    {
        var line = WickEnvelope.Format("log", new LogPayload("X", "Information", "hello", null));

        line.Should().StartWith("{\"__wick\":1");
        line.Should().Contain("\"kind\":\"log\"");
        line.Should().NotContain("\n");
    }

    [Fact]
    public void Format_IncludesIso8601Timestamp()
    {
        var line = WickEnvelope.Format("exception", new { Type = "T", Message = "M" });

        using var doc = JsonDocument.Parse(line);
        var ts = doc.RootElement.GetProperty("timestamp").GetString();
        ts.Should().NotBeNullOrEmpty();
        System.DateTimeOffset.TryParse(ts, out var parsed).Should().BeTrue();
        parsed.Should().BeAfter(System.DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void Format_SerializesPayloadWithSnakeCaseProperties()
    {
        var payload = new HandshakePayload(Port: 9999, Version: "1.2.3");
        var line = WickEnvelope.Format("handshake", payload);

        using var doc = JsonDocument.Parse(line);
        var payloadEl = doc.RootElement.GetProperty("payload");
        payloadEl.GetProperty("port").GetInt32().Should().Be(9999);
        payloadEl.GetProperty("version").GetString().Should().Be("1.2.3");
    }

    [Fact]
    public void FormatHandshake_ReturnsValidEnvelopeLine()
    {
        var line = WickEnvelope.FormatHandshake(7878, "0.1.0");
        using var doc = JsonDocument.Parse(line);
        doc.RootElement.GetProperty("kind").GetString().Should().Be("handshake");
        doc.RootElement.GetProperty("payload").GetProperty("port").GetInt32().Should().Be(7878);
    }

    [Fact]
    public void WriteEnvelope_WritesToStderr()
    {
        var captured = new System.IO.StringWriter();
        var original = System.Console.Error;
        try
        {
            System.Console.SetError(captured);
            WickEnvelope.WriteEnvelope("log", new LogPayload("X", "Error", "boom", null));
        }
        finally
        {
            System.Console.SetError(original);
        }

        // Parallel tests may share Console.Error; find our line.
        var line = captured.ToString().Split('\n').FirstOrDefault(l => l.Contains("\"boom\""));
        line.Should().NotBeNull();
        line!.TrimStart().Should().StartWith("{\"__wick\":1");
    }
}
