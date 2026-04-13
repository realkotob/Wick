using System.Collections.Concurrent;

namespace Wick.Core;

/// <summary>
/// Thread-safe ring buffer of recent Godot log lines.
/// Used by the enricher to attach "what was happening just before the exception" context.
/// </summary>
public sealed class LogBuffer
{
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly int _capacity;

    public LogBuffer(int capacity = 200)
    {
        _capacity = capacity;
    }

    public void Add(string line)
    {
        _queue.Enqueue(line);
        while (_queue.Count > _capacity)
        {
            _queue.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Returns the N most recent log lines, newest first.
    /// </summary>
    public IReadOnlyList<string> GetRecent(int count)
    {
        return _queue.Reverse().Take(count).ToList();
    }

    public int Count => _queue.Count;

    public void Clear() => _queue.Clear();
}
