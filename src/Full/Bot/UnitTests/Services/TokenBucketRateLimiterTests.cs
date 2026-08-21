using Engine.Services;

namespace UnitTests.Services;

/// <summary>
/// Pure unit tests for the shared send rate limiter. Uses an injected clock so the tests are
/// deterministic and don't depend on wall-clock timing.
/// </summary>
[TestClass]
public class TokenBucketRateLimiterTests
{
    private DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private TokenBucketRateLimiter Create(int permits, TimeSpan period) =>
        new(permits, period, () => _now);

    private void Advance(TimeSpan by) => _now = _now.Add(by);

    [TestMethod]
    public void Constructor_InvalidArguments_Throw()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new TokenBucketRateLimiter(0, TimeSpan.FromSeconds(1)));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new TokenBucketRateLimiter(1, TimeSpan.Zero));
    }

    [TestMethod]
    public void StartsFull_SoAShortBurstIsNotDelayed()
    {
        var limiter = Create(10, TimeSpan.FromSeconds(10));

        for (var i = 0; i < 10; i++)
        {
            Assert.IsTrue(limiter.TryAcquire(), $"Permit {i} should be immediately available");
        }
    }

    [TestMethod]
    public void ExhaustsAfterCapacity()
    {
        var limiter = Create(3, TimeSpan.FromSeconds(30));

        Assert.IsTrue(limiter.TryAcquire());
        Assert.IsTrue(limiter.TryAcquire());
        Assert.IsTrue(limiter.TryAcquire());
        Assert.IsFalse(limiter.TryAcquire(), "Bucket should be empty");
    }

    [TestMethod]
    public void RefillsOverTime()
    {
        // 10 permits per 10s => 1 token/second.
        var limiter = Create(10, TimeSpan.FromSeconds(10));

        for (var i = 0; i < 10; i++) limiter.TryAcquire();
        Assert.IsFalse(limiter.TryAcquire());

        Advance(TimeSpan.FromSeconds(3));

        Assert.IsTrue(limiter.TryAcquire());
        Assert.IsTrue(limiter.TryAcquire());
        Assert.IsTrue(limiter.TryAcquire());
        Assert.IsFalse(limiter.TryAcquire(), "Only three seconds of refill should be available");
    }

    [TestMethod]
    public void RefillIsCappedAtCapacity()
    {
        var limiter = Create(5, TimeSpan.FromSeconds(5));

        for (var i = 0; i < 5; i++) limiter.TryAcquire();

        // Idle far longer than the window - the bucket must not over-fill and allow a huge burst.
        Advance(TimeSpan.FromHours(1));

        var acquired = 0;
        while (limiter.TryAcquire()) acquired++;

        Assert.AreEqual(5, acquired, "Bucket should refill to capacity, not beyond");
    }

    [TestMethod]
    public void Penalise_RemovesTokensSoTheWholeProcessBacksOff()
    {
        // 10/sec.
        var limiter = Create(10, TimeSpan.FromSeconds(1));
        Assert.IsTrue(limiter.TryAcquire());

        // A server said "retry after 2 seconds" - that is worth ~20 tokens at this rate.
        limiter.Penalise(TimeSpan.FromSeconds(2));

        Assert.IsFalse(limiter.TryAcquire(), "After a throttling penalty the bucket should be empty");
    }

    [TestMethod]
    public void Penalise_NonPositive_IsIgnored()
    {
        var limiter = Create(5, TimeSpan.FromSeconds(5));
        var before = limiter.AvailableTokens;

        limiter.Penalise(TimeSpan.Zero);
        limiter.Penalise(TimeSpan.FromSeconds(-5));

        Assert.AreEqual(before, limiter.AvailableTokens);
    }

    [TestMethod]
    public async Task WaitAsync_ReturnsImmediatelyWhenTokensAvailable()
    {
        var limiter = new TokenBucketRateLimiter(5, TimeSpan.FromSeconds(1));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await limiter.WaitAsync();
        sw.Stop();

        Assert.IsTrue(sw.ElapsedMilliseconds < 200, $"Should not have blocked, took {sw.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public async Task WaitAsync_ThrottlesSustainedRate()
    {
        // 20 permits/second against a real clock; 30 sends must take at least ~0.5s.
        var limiter = new TokenBucketRateLimiter(20, TimeSpan.FromSeconds(1));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 30; i++)
        {
            await limiter.WaitAsync();
        }
        sw.Stop();

        Assert.IsTrue(sw.ElapsedMilliseconds >= 300,
            $"Sustained sends should be shaped, completed in only {sw.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public async Task WaitAsync_HonoursCancellation()
    {
        var limiter = new TokenBucketRateLimiter(1, TimeSpan.FromMinutes(10));
        await limiter.WaitAsync();   // drain the single token

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => limiter.WaitAsync(cts.Token));
    }
}
