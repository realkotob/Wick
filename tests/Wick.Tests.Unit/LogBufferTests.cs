using Wick.Core;

namespace Wick.Tests.Unit;

public sealed class LogBufferTests
{
    [Fact]
    public void Add_BeyondCapacity_EvictsOldest()
    {
        var buffer = new LogBuffer(capacity: 3);
        buffer.Add("line1");
        buffer.Add("line2");
        buffer.Add("line3");
        buffer.Add("line4");

        var recent = buffer.GetRecent(10);
        recent.Should().HaveCount(3);
        recent.Should().NotContain("line1");
    }

    [Fact]
    public void GetRecent_ReturnsRequestedCount()
    {
        var buffer = new LogBuffer(capacity: 100);
        for (var i = 0; i < 50; i++)
            buffer.Add($"line-{i}");

        var recent = buffer.GetRecent(5);
        recent.Should().HaveCount(5);
        recent[0].Should().Be("line-49", "most recent line should be first");
    }
}
