using System.Text.Json;
using Wick.Providers.Godot;

namespace Wick.Tests.Unit.Runtime;

public sealed class WickEnvelopeParserTests
{
    [Fact]
    public void IsEnvelope_RecognizesMarker()
    {
        WickEnvelopeParser.IsEnvelope("{\"__wick\":1,\"kind\":\"log\"}").Should().BeTrue();
        WickEnvelopeParser.IsEnvelope("  {\"__wick\":1,\"kind\":\"log\"}  ").Should().BeTrue();
    }

    [Fact]
    public void IsEnvelope_RejectsRawGodotOutput()
    {
        WickEnvelopeParser.IsEnvelope("ERROR: something went wrong").Should().BeFalse();
        WickEnvelopeParser.IsEnvelope("").Should().BeFalse();
        WickEnvelopeParser.IsEnvelope("{\"other\":1}").Should().BeFalse();
    }

    [Fact]
    public void Parse_HandshakeEnvelope_ExtractsPort()
    {
        var line = "{\"__wick\":1,\"kind\":\"handshake\",\"timestamp\":\"2026-04-11T00:00:00Z\",\"payload\":{\"port\":4242,\"version\":\"1.0.0\"}}";
        var evt = WickEnvelopeParser.Parse(line);
        evt.Should().NotBeNull();
        evt!.Kind.Should().Be("handshake");
        evt.Payload.Should().NotBeNull();
        WickEnvelopeParser.TryProjectHandshakePort(evt.Payload!.Value).Should().Be(4242);
    }

    [Fact]
    public void Parse_ExceptionEnvelope_ProjectsRawException()
    {
        var line =
            "{\"__wick\":1,\"kind\":\"exception\",\"timestamp\":\"2026-04-11T00:00:00Z\"," +
            "\"payload\":{\"type\":\"System.InvalidOperationException\",\"message\":\"boom\"," +
            "\"stack_trace\":null,\"frames\":[" +
            "{\"method\":\"MyGame.Player.Shoot\",\"file_path\":\"Player.cs\",\"line\":42,\"is_user_code\":true}" +
            "]}}";

        var evt = WickEnvelopeParser.Parse(line);
        evt.Should().NotBeNull();
        var raw = WickEnvelopeParser.TryProjectException(evt!.Payload!.Value, line);
        raw.Should().NotBeNull();
        raw!.Type.Should().Be("System.InvalidOperationException");
        raw.Message.Should().Be("boom");
        raw.Frames.Should().HaveCount(1);
        raw.Frames[0].Method.Should().Be("MyGame.Player.Shoot");
        raw.Frames[0].FilePath.Should().Be("Player.cs");
        raw.Frames[0].Line.Should().Be(42);
        raw.Frames[0].IsUserCode.Should().BeTrue();
    }

    [Fact]
    public void Parse_LogEnvelope_ProjectsDisplayLine()
    {
        var line =
            "{\"__wick\":1,\"kind\":\"log\",\"timestamp\":\"2026-04-11T00:00:00Z\"," +
            "\"payload\":{\"category\":\"MyGame.Combat\",\"level\":\"Error\",\"message\":\"missed shot\",\"exception\":null}}";

        var evt = WickEnvelopeParser.Parse(line);
        var display = WickEnvelopeParser.TryProjectLogLine(evt!.Payload!.Value);
        display.Should().NotBeNull();
        display.Should().Contain("[wick]");
        display.Should().Contain("Error");
        display.Should().Contain("MyGame.Combat");
        display.Should().Contain("missed shot");
    }

    [Fact]
    public void Parse_MalformedEnvelope_ReturnsNull()
    {
        WickEnvelopeParser.Parse("{\"__wick\":1, not valid json").Should().BeNull();
    }

    [Fact]
    public void Parse_EnvelopeWithoutKind_ReturnsNull()
    {
        WickEnvelopeParser.Parse("{\"__wick\":1,\"payload\":{}}").Should().BeNull();
    }
}
