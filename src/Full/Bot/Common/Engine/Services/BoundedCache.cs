using System.Collections.Concurrent;

namespace Engine.Services;

/// <summary>
/// A thread-safe cache with a hard entry cap and approximate least-recently-used eviction.
///
/// <para>
/// Replaces the unbounded <c>ConcurrentDictionary</c> that previously held every user in the
/// tenant. That dictionary was populated by scanning the whole table and was never evicted,
/// so its footprint grew with tenant size rather than with working set - roughly 2.5 GB at
/// 150,000 users once rows carried card JSON and chat history, against a 1.75 GB worker.
/// </para>
///
/// <para>
/// Eviction is approximate by design: entries carry a monotonically increasing access stamp
/// and, when over capacity, the cheapest-stamped entries are dropped. This avoids the lock
/// contention of a strict LRU list on a hot path where a slightly suboptimal eviction only
/// costs one ~10 ms point read.
/// </para>
/// </summary>
public sealed class BoundedCache<TKey, TValue> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, Entry> _entries;
    private readonly int _capacity;
    private long _clock;
    private int _evicting;

    private sealed class Entry(TValue value, long stamp)
    {
        public TValue Value { get; set; } = value;
        public long Stamp { get; set; } = stamp;
    }

    public BoundedCache(int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _entries = new ConcurrentDictionary<TKey, Entry>(comparer ?? EqualityComparer<TKey>.Default);
    }

    /// <summary>Current number of cached entries.</summary>
    public int Count => _entries.Count;

    /// <summary>Maximum number of entries retained.</summary>
    public int Capacity => _capacity;

    public bool TryGet(TKey key, out TValue value)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            entry.Stamp = Interlocked.Increment(ref _clock);
            value = entry.Value;
            return true;
        }

        value = default!;
        return false;
    }

    public void Set(TKey key, TValue value)
    {
        var stamp = Interlocked.Increment(ref _clock);

        _entries.AddOrUpdate(
            key,
            _ => new Entry(value, stamp),
            (_, existing) =>
            {
                existing.Value = value;
                existing.Stamp = stamp;
                return existing;
            });

        EvictIfNeeded();
    }

    public void Remove(TKey key) => _entries.TryRemove(key, out _);

    public void Clear() => _entries.Clear();

    /// <summary>
    /// Drop the least-recently-accessed entries once over capacity.
    ///
    /// <para>
    /// Only one thread evicts at a time: concurrent evictors would each enumerate the
    /// dictionary while the others mutate it, and would duplicate the same work. Uses
    /// <see cref="ConcurrentDictionary{TKey,TValue}.ToArray"/> for a stable snapshot rather
    /// than enumerating live.
    /// </para>
    /// </summary>
    private void EvictIfNeeded()
    {
        if (_entries.Count <= _capacity) return;

        // Single-flight: if another thread is already evicting, let it do the work.
        if (Interlocked.CompareExchange(ref _evicting, 1, 0) != 0) return;

        try
        {
            // Re-check now that we hold the eviction slot.
            if (_entries.Count <= _capacity) return;

            var snapshot = _entries.ToArray();
            var overflow = snapshot.Length - _capacity;
            if (overflow <= 0) return;

            // Evict in blocks so this doesn't run on every insert once the cache is warm.
            var target = overflow + Math.Max(1, _capacity / 10);

            Array.Sort(snapshot, static (x, y) =>
            {
                var xs = x.Value?.Stamp ?? long.MinValue;
                var ys = y.Value?.Stamp ?? long.MinValue;
                return xs.CompareTo(ys);
            });

            for (var i = 0; i < target && i < snapshot.Length; i++)
            {
                _entries.TryRemove(snapshot[i].Key, out _);
            }
        }
        finally
        {
            Volatile.Write(ref _evicting, 0);
        }
    }
}
