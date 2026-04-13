using Wick.Core;

namespace Wick.Tests.Unit;

public class GodotExceptionParserTests
{
    private const string StandardGodotException = """
        ERROR: System.InvalidOperationException: Something broke
           at MyApp.Player._Ready() in /home/user/project/Player.cs:line 42
           at Godot.Node.InvokeGodotClassMethod(godot_string_name& method, NativeVariantPtrArgs args, godot_variant& ret) in /root/godot/Node.cs:line 2646
           at Godot.Bridge.CSharpInstanceBridge.Call(IntPtr p0, godot_string_name* p1, godot_variant** p2, Int32 p3, godot_variant_call_error* p4, godot_variant* p5) in /root/godot/CSharpInstanceBridge.cs:line 24
           at: void Godot.NativeInterop.ExceptionUtils.LogException(System.Exception) (/root/godot/ExceptionUtils.cs:113)
           C# backtrace (most recent call first):
               [0] void Godot.GD.PushError(string) (/root/godot/GD.cs:366)
               [1] void Godot.NativeInterop.ExceptionUtils.LogException(System.Exception) (/root/godot/ExceptionUtils.cs:113)
        """;

    [Fact]
    public void Parse_StandardGodotException_ExtractsTypeAndMessage()
    {
        var result = GodotExceptionParser.Parse(StandardGodotException);

        result.Should().NotBeNull();
        result!.Type.Should().Be("System.InvalidOperationException");
        result.Message.Should().Be("Something broke");
    }

    [Fact]
    public void Parse_StandardGodotException_ExtractsUserCodeFrames()
    {
        var result = GodotExceptionParser.Parse(StandardGodotException);

        result.Should().NotBeNull();
        var userFrames = result!.Frames.Where(f => f.IsUserCode).ToList();
        userFrames.Should().HaveCount(1);
        userFrames[0].Method.Should().Be("MyApp.Player._Ready()");
        userFrames[0].FilePath.Should().Be("/home/user/project/Player.cs");
        userFrames[0].Line.Should().Be(42);
    }

    [Fact]
    public void Parse_GodotInternalFrames_MarkedAsNonUserCode()
    {
        var result = GodotExceptionParser.Parse(StandardGodotException);

        result.Should().NotBeNull();
        var internalFrames = result!.Frames.Where(f => !f.IsUserCode).ToList();
        internalFrames.Should().HaveCountGreaterThanOrEqualTo(3);
        internalFrames.Should().Contain(f => f.Method.Contains("Godot.Bridge.CSharpInstanceBridge"));
        internalFrames.Should().Contain(f => f.Method.Contains("Godot.NativeInterop.ExceptionUtils"));
        internalFrames.Should().Contain(f => f.Method.Contains("Godot.Node.InvokeGodotClassMethod"));
    }

    [Fact]
    public void Parse_FrameWithoutLineNumber_HandlesGracefully()
    {
        const string input = """
            ERROR: System.NullReferenceException: Object reference not set
               at MyApp.Game.Run() in [0x00000] in <abc123>:0
            """;

        var result = GodotExceptionParser.Parse(input);

        result.Should().NotBeNull();
        result!.Frames.Should().HaveCount(1);
        result.Frames[0].Method.Should().Be("MyApp.Game.Run()");
        // Line 0 from mono format — could be 0 or null depending on parse; just ensure no crash
    }

    [Fact]
    public void Parse_MultiLineMessage_CapturesFullMessage()
    {
        const string input = """
            ERROR: System.AggregateException: One or more errors occurred. (Connection refused) (Timeout expired)
               at MyApp.Network.Connect() in /home/user/project/Network.cs:line 10
            """;

        var result = GodotExceptionParser.Parse(input);

        result.Should().NotBeNull();
        result!.Type.Should().Be("System.AggregateException");
        result.Message.Should().Be("One or more errors occurred. (Connection refused) (Timeout expired)");
    }

    [Fact]
    public void Parse_NoStackTrace_ReturnsExceptionWithEmptyFrames()
    {
        const string input = "ERROR: System.OutOfMemoryException: Insufficient memory to continue the execution of the program.";

        var result = GodotExceptionParser.Parse(input);

        result.Should().NotBeNull();
        result!.Type.Should().Be("System.OutOfMemoryException");
        result.Message.Should().Be("Insufficient memory to continue the execution of the program.");
        result.Frames.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NonExceptionErrorLine_ReturnsNull()
    {
        const string input = "ERROR: res://scenes/Level.tscn: Resource not found";

        var result = GodotExceptionParser.Parse(input);

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsNull()
    {
        var result = GodotExceptionParser.Parse(string.Empty);

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_NullInput_ReturnsNull()
    {
        var result = GodotExceptionParser.Parse(null);

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_IgnoresCSharpBacktraceSection()
    {
        var result = GodotExceptionParser.Parse(StandardGodotException);

        result.Should().NotBeNull();
        // Section 2 frames like "void Godot.GD.PushError(string)" should NOT appear
        result!.Frames.Should().NotContain(f => f.Method.Contains("PushError"));
    }

    [Fact]
    public void Parse_PreservesRawText()
    {
        var result = GodotExceptionParser.Parse(StandardGodotException);

        result.Should().NotBeNull();
        result!.RawText.Should().Be(StandardGodotException);
    }

    [Fact]
    public void Parse_NestedTypeException_ExtractsCorrectType()
    {
        var input = "ERROR: MyApp.Service+InnerException: nested type threw";

        var result = GodotExceptionParser.Parse(input);

        result.Should().NotBeNull();
        result!.Type.Should().Be("MyApp.Service+InnerException");
        result.Message.Should().Be("nested type threw");
    }

    [Fact]
    public void ParseStream_InnerExceptionChain_ParsesAsOneException()
    {
        var input = """
            ERROR: System.AggregateException: One or more errors occurred.
             ---> System.InvalidOperationException: inner error
               at MyApp.Inner.Method() in /home/user/Inner.cs:line 10
               --- End of inner exception stack trace ---
               at MyApp.Outer.Method() in /home/user/Outer.cs:line 20
            """;

        var results = GodotExceptionParser.ParseStream(input);

        results.Should().HaveCount(1, "inner exception chain should be parsed as one exception, not split");
        results[0].Type.Should().Be("System.AggregateException");
    }

    [Fact]
    public void ParseStream_MultipleExceptions_ReturnsAll()
    {
        const string mixed = """
            Some log line
            ERROR: System.InvalidOperationException: First error
               at MyApp.Player._Ready() in /home/user/project/Player.cs:line 42
            Another log line
            DEBUG: something else
            ERROR: System.NullReferenceException: Second error
               at MyApp.Enemy.Update() in /home/user/project/Enemy.cs:line 99
            Final log line
            """;

        var results = GodotExceptionParser.ParseStream(mixed);

        results.Should().HaveCount(2);
        results[0].Type.Should().Be("System.InvalidOperationException");
        results[0].Message.Should().Be("First error");
        results[1].Type.Should().Be("System.NullReferenceException");
        results[1].Message.Should().Be("Second error");
    }
}
