namespace Engine.Services;

/// <summary>
/// A token-bucket rate limiter shared across all senders in the process.
///
/// <para>
/// Teams proactive messaging is throttled per bot per tenant (roughly 1,800 operations per
/// 30 seconds). The dispatcher previously had no rate shaping at all: it dequeued 32 messages
/// and pushed 8 concurrently as fast as it could, saturating the limit within seconds of a
/// send starting and then taking sustained 429s for the rest of the run.
/// </para>
///
/// <para>
/// Deliberately staying <em>under</em> the documented limit is both faster overall and far less
/// disruptive than discovering it: a throttled request still costs a round trip, still consumes
/// the tenant's Graph budget, and (before this change) could end up dropping the nudge.
/// </para>
/// </summary>
public sealed class TokenBucketRateLimiter
{
    private readonly double _tokensPerSecond;
    private readonly double _capacity;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly Func<DateTime> _utcNow;

    private double _tokens;
    private DateTime _lastRefill;

    /// <param name="permits">Operations allowed per <paramref name="perPeriod"/>.</param>
    /// <param name="perPeriod">Window the permit count applies to.</param>
    /// <param name="utcNow">Clock override, for tests.</param>
    public TokenBucketRateLimiter(int permits, TimeSpan perPeriod, Func<DateTime>? utcNow = null)
    {
        if (permits < 1) throw new ArgumentOutOfRangeException(nameof(permits));
        if (perPeriod <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(perPeriod));

        _tokensPerSecond = permits / perPeriod.TotalSeconds;
        _capacity = permits;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);

        _tokens = permits;
        _lastRefill = _utcNow();
    }

    /// <summary>Tokens currently available (for tests and diagnostics).</summary>
    public double AvailableTokens
    {
        get
        {
            lock (this) { return _tokens; }
        }
    }

    /// <summary>
    /// Wait until a permit is available, then consume it.
    /// </summary>
    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            TimeSpan delay;

            await _mutex.WaitAsync(cancellationToken);
            try
            {
                Refill();

                if (_tokens >= 1)
                {
                    _tokens -= 1;
                    return;
                }

                // How long until one token is available.
                var deficit = 1 - _tokens;
                delay = TimeSpan.FromSeconds(deficit / _tokensPerSecond);
            }
            finally
            {
                _mutex.Release();
            }

            // Cap the sleep so cancellation stays responsive on long waits.
            if (delay > TimeSpan.FromSeconds(1)) delay = TimeSpan.FromSeconds(1);
            if (delay < TimeSpan.FromMilliseconds(1)) delay = TimeSpan.FromMilliseconds(1);

            await Task.Delay(delay, cancellationToken);
        }
    }

    /// <summary>
    /// Try to consume a permit without waiting.
    /// </summary>
    public bool TryAcquire()
    {
        _mutex.Wait();
        try
        {
            Refill();
            if (_tokens < 1) return false;

            _tokens -= 1;
            return true;
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>
    /// Remove tokens to back off after a throttling response, so the whole process slows down
    /// rather than each caller discovering the limit independently.
    /// </summary>
    public void Penalise(TimeSpan retryAfter)
    {
        if (retryAfter <= TimeSpan.Zero) return;

        _mutex.Wait();
        try
        {
            Refill();
            var penalty = retryAfter.TotalSeconds * _tokensPerSecond;
            _tokens = Math.Max(-_capacity, _tokens - penalty);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private void Refill()
    {
        var now = _utcNow();
        var elapsed = (now - _lastRefill).TotalSeconds;
        if (elapsed <= 0) return;

        _tokens = Math.Min(_capacity, _tokens + elapsed * _tokensPerSecond);
        _lastRefill = now;
    }
}
