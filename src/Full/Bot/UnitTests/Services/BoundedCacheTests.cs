using Engine.Services;

namespace UnitTests.Services;

/// <summary>
/// Pure unit tests for <see cref="BoundedCache{TKey,TValue}"/>, which replaced the unbounded
/// process-wide dictionary that previously held every user in the tenant.
/// </summary>
[TestClass]
public class BoundedCacheTests
{
    [TestMethod]
    public void Constructor_InvalidCapacity_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new BoundedCache<string, string>(0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new BoundedCache<string, string>(-1));
    }

    [TestMethod]
    public void SetThenGet_ReturnsValue()
    {
        var cache = new BoundedCache<string, string>(10);
        cache.Set("a", "one");

        Assert.IsTrue(cache.TryGet("a", out var value));
        Assert.AreEqual("one", value);
    }

    [TestMethod]
    public void TryGet_Missing_ReturnsFalse()
    {
        var cache = new BoundedCache<string, string>(10);

        Assert.IsFalse(cache.TryGet("nope", out var value));
        Assert.IsNull(value);
    }

    [TestMethod]
    public void Set_OverwritesExistingKey()
    {
        var cache = new BoundedCache<string, string>(10);
        cache.Set("a", "one");
        cache.Set("a", "two");

        Assert.IsTrue(cache.TryGet("a", out var value));
        Assert.AreEqual("two", value);
        Assert.AreEqual(1, cache.Count);
    }

    [TestMethod]
    public void Remove_DropsEntry()
    {
        var cache = new BoundedCache<string, string>(10);
        cache.Set("a", "one");
        cache.Remove("a");

        Assert.IsFalse(cache.TryGet("a", out _));
    }

    [TestMethod]
    public void NeverExceedsCapacity()
    {
        // The whole point: memory must scale with working set, not with tenant size. The old
        // dictionary grew to every user in the tenant and was never evicted.
        const int capacity = 100;
        var cache = new BoundedCache<int, string>(capacity);

        for (var i = 0; i < 150_000; i++)
        {
            cache.Set(i, $"value-{i}");
            Assert.IsTrue(cache.Count <= capacity, $"Exceeded capacity at insert {i}: {cache.Count}");
        }

        Assert.IsTrue(cache.Count <= capacity);
    }

    [TestMethod]
    public void EvictsLeastRecentlyUsed_KeepingHotEntries()
    {
        var cache = new BoundedCache<string, string>(10);

        for (var i = 0; i < 10; i++)
        {
            cache.Set($"k{i}", $"v{i}");
        }

        // Keep k0 hot by reading it repeatedly, then push well past capacity.
        for (var i = 0; i < 20; i++)
        {
            cache.TryGet("k0", out _);
            cache.Set($"new{i}", $"v{i}");
        }

        Assert.IsTrue(cache.TryGet("k0", out _), "A repeatedly accessed entry should survive eviction");
    }

    [TestMethod]
    public void Clear_EmptiesCache()
    {
        var cache = new BoundedCache<string, string>(10);
        cache.Set("a", "one");
        cache.Set("b", "two");

        cache.Clear();

        Assert.AreEqual(0, cache.Count);
    }

    [TestMethod]
    public void ConcurrentAccess_StaysBoundedAndDoesNotThrow()
    {
        const int capacity = 500;
        var cache = new BoundedCache<int, string>(capacity);

        Parallel.For(0, 20_000, i =>
        {
            cache.Set(i, $"v{i}");
            cache.TryGet(i / 2, out _);
        });

        // Eviction is single-flight and therefore approximate under contention: a writer that
        // finds another thread already evicting proceeds without evicting itself. The cache is
        // still bounded - it just may briefly overshoot - which is the intended trade-off, since
        // the alternative is lock contention on a hot path. What must never happen is unbounded
        // growth toward tenant size.
        Assert.IsTrue(cache.Count <= capacity * 2,
            $"Cache should stay bounded under contention, was {cache.Count} (capacity {capacity})");
    }

    [TestMethod]
    public void RespectsCustomComparer()
    {
        var cache = new BoundedCache<string, string>(10, StringComparer.OrdinalIgnoreCase);
        cache.Set("Alice", "one");

        Assert.IsTrue(cache.TryGet("ALICE", out var value));
        Assert.AreEqual("one", value);
    }
}
