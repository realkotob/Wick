using Wick.Core;

namespace Wick.Tests.Unit;

public sealed class ExceptionBufferTests
{
    private static EnrichedException MakeEnriched(string type, string message) => new()
    {
        Raw = new RawException
        {
            Type = type,
            Message = message,
            RawText = $"ERROR: {type}: {message}",
        }
    };

    [Fact]
    public void Add_BeyondCapacity_EvictsOldest()
    {
        var buffer = new ExceptionBuffer(capacity: 3);
        buffer.Add(MakeEnriched("System.Exception", "first"));
        buffer.Add(MakeEnriched("System.Exception", "second"));
        buffer.Add(MakeEnriched("System.Exception", "third"));
        buffer.Add(MakeEnriched("System.Exception", "fourth"));

        var all = buffer.GetAll();
        all.Should().HaveCount(3);
        all.Should().NotContain(e => e.Raw.Message == "first",
            "the oldest entry should have been evicted");
        all[0].Raw.Message.Should().Be("fourth", "newest should be first");
    }

    [Fact]
    public void GetAll_ReturnsNewestFirst()
    {
        var buffer = new ExceptionBuffer(capacity: 10);
        buffer.Add(MakeEnriched("System.Exception", "older"));
        buffer.Add(MakeEnriched("System.Exception", "newer"));

        var all = buffer.GetAll();
        all[0].Raw.Message.Should().Be("newer");
        all[1].Raw.Message.Should().Be("older");
    }

    [Fact]
    public void Add_ConcurrentWrites_DoNotCorrupt()
    {
        var buffer = new ExceptionBuffer(capacity: 100);

        Parallel.For(0, 200, i =>
        {
            buffer.Add(MakeEnriched("System.Exception", $"item-{i}"));
        });

        var all = buffer.GetAll();
        all.Count.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void GetSince_ReturnsEntriesAfterCursor_InOrder()
    {
        var buffer = new ExceptionBuffer(capacity: 10);
        var a = MakeException("A");
        var b = MakeException("B");
        var c = MakeException("C");

        var idA = buffer.Add(a);
        var idB = buffer.Add(b);
        var idC = buffer.Add(c);

        idA.Should().Be(1);
        idB.Should().Be(2);
        idC.Should().Be(3);

        var result = buffer.GetSince(sinceId: 1, limit: 10);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(2);
        result[1].Id.Should().Be(3);
    }

    [Fact]
    public void GetSince_NullCursor_ReturnsAllUpToLimit()
    {
        var buffer = new ExceptionBuffer(capacity: 10);
        buffer.Add(MakeException("A"));
        buffer.Add(MakeException("B"));
        buffer.Add(MakeException("C"));

        var result = buffer.GetSince(sinceId: null, limit: 2);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(1);
        result[1].Id.Should().Be(2);
    }

    [Fact]
    public void GetSince_RespectsEviction_IdsStayMonotonic()
    {
        var buffer = new ExceptionBuffer(capacity: 2);
        buffer.Add(MakeException("A")); // id 1, evicted
        buffer.Add(MakeException("B")); // id 2
        buffer.Add(MakeException("C")); // id 3

        var all = buffer.GetSince(sinceId: null, limit: 10);

        all.Should().HaveCount(2);
        all[0].Id.Should().Be(2);
        all[1].Id.Should().Be(3);
    }

    private static EnrichedException MakeException(string msg) =>
        new()
        {
            Raw = new RawException
            {
                Type = "System.Exception",
                Message = msg,
                RawText = $"ERROR: {msg}",
                Frames = Array.Empty<ExceptionFrame>(),
            },
        };
}
