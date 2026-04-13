using System.Collections.Concurrent;

namespace Wick.Core;

/// <summary>
/// Thread-safe ring buffer of enriched exceptions with monotonic sequence ids.
/// Oldest entries are evicted when capacity is exceeded; ids never reset.
/// </summary>
public sealed class ExceptionBuffer
{
    private readonly ConcurrentQueue<BufferedException> _queue = new();
    private readonly int _capacity;
    private long _nextId;

    public ExceptionBuffer(int capacity = 50)
    {
        _capacity = capacity;
    }

    /// <summary>
    /// Appends an exception and returns its assigned monotonic id.
    /// </summary>
    public long Add(EnrichedException exception)
    {
        var id = Interlocked.Increment(ref _nextId);
        _queue.Enqueue(new BufferedException(id, exception));
        while (_queue.Count > _capacity)
        {
            _queue.TryDequeue(out _);
        }
        return id;
    }

    /// <summary>
    /// Returns buffered entries with id strictly greater than <paramref name="sinceId"/>,
    /// ordered oldest-first, up to <paramref name="limit"/> entries.
    /// Null cursor returns the oldest buffered entries.
    /// </summary>
    public IReadOnlyList<BufferedException> GetSince(long? sinceId, int limit)
    {
        var cursor = sinceId ?? 0;
        return _queue
            .Where(e => e.Id > cursor)
            .OrderBy(e => e.Id)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Returns all buffered exceptions, newest first. Retained for backward compat.
    /// </summary>
    public IReadOnlyList<EnrichedException> GetAll()
    {
        return _queue.Reverse().Select(e => e.Exception).ToList();
    }

    public int Count => _queue.Count;

    public void Clear() => _queue.Clear();
}

/// <summary>
/// An exception entry with its assigned monotonic buffer id.
/// </summary>
public sealed record BufferedException(long Id, EnrichedException Exception);
