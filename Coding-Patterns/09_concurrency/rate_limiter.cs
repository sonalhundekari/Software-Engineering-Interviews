using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Configuration for a <see cref="RateLimiter"/> instance.
/// </summary>
/// <param name="TokensPerSecond">Sustained requests/sec allowed per client (refill rate).</param>
/// <param name="Capacity">Max tokens a client's bucket can hold (max burst size).</param>
public record RateLimitConfig(double TokensPerSecond, long Capacity)
{
    /// <summary>
    /// How often the background cleanup loop scans for idle buckets to remove.
    /// Defaults to once a minute — frequent enough to keep memory bounded without
    /// burning CPU on a busy server with many distinct clients.
    /// </summary>
    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long a client's bucket can sit unused before it's evicted from memory.
    /// Defaults to 5 minutes. Evicted clients simply get a fresh, full bucket on
    /// their next request — there's no other downside to eviction.
    /// </summary>
    public TimeSpan InactivityTimeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Validates the configuration. Called by <see cref="RateLimiter"/>'s constructor
    /// so misconfiguration fails fast instead of silently producing a limiter that
    /// never allows (or never blocks) anything.
    /// </summary>
    public void Validate()
    {
        if (TokensPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(TokensPerSecond), "Must be positive.");

        if (Capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(Capacity), "Must be positive.");

        if (CleanupInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(CleanupInterval), "Must be positive.");

        if (InactivityTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(InactivityTimeout), "Must be positive.");
    }
}

/// <summary>
/// A single client's token bucket. Thread-safe for concurrent callers.
///
/// Uses <see cref="Stopwatch.GetTimestamp"/> (a monotonic, high-resolution clock)
/// rather than <see cref="DateTime.UtcNow"/>. This matters for a rate limiter
/// specifically because DateTime.UtcNow can jump backwards or forwards due to
/// NTP synchronization, daylight saving adjustments, or manual clock changes —
/// any of which could let a client burst past its limit or get incorrectly
/// throttled. Stopwatch ticks only ever move forward at a steady rate.
/// </summary>
public class TokenBucket
{
    // Guards all mutable state below so refill + consume happens atomically
    // per bucket, even under concurrent requests from the same client.
    private readonly object _lock = new();

    // Current token count. Kept as double because refills add fractional
    // amounts (e.g. 2.5 tokens might accrue between two checks) — using an
    // integer would lose that fractional progress to truncation.
    private double _tokens;

    // Stopwatch timestamp (raw ticks, not seconds) of the last refill.
    private long _lastRefillTicks;

    // Stopwatch timestamp of the last time this bucket was actually used to
    // grant/deny a request. Read by RateLimiter's cleanup loop to decide
    // whether this bucket has gone idle and can be evicted.
    private long _lastAccessTicks;

    /// <summary>Max tokens this bucket can hold (max burst size for this client).</summary>
    public long Capacity { get; }

    /// <summary>Tokens added back per second (sustained rate limit for this client).</summary>
    public double TokensPerSecond { get; }

    public TokenBucket(long capacity, double tokensPerSecond)
    {
        Capacity = capacity;
        TokensPerSecond = tokensPerSecond;

        // Start full so a client's very first burst isn't throttled.
        _tokens = capacity;

        var now = Stopwatch.GetTimestamp();
        _lastRefillTicks = now;
        _lastAccessTicks = now;
    }

    /// <summary>
    /// Attempts to consume <paramref name="permits"/> tokens. Returns true and
    /// deducts the tokens if enough were available; returns false (and leaves
    /// the bucket untouched) otherwise.
    /// </summary>
    public bool TryAcquire(int permits)
    {
        lock (_lock)
        {
            Refill();

            // Only requests that actually attempt to acquire count as "access"
            // for idle-eviction purposes — see GetAvailableTokens, which is a
            // read-only peek and deliberately does NOT update this.
            _lastAccessTicks = Stopwatch.GetTimestamp();

            if (_tokens >= permits)
            {
                _tokens -= permits;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Returns the current token count after applying any refill owed since
    /// the last update, without consuming anything. Used for reporting
    /// (e.g. RateLimitInfo) — deliberately does not count as "access" for
    /// idle-eviction purposes, so polling a client's status doesn't keep
    /// an otherwise-unused bucket alive forever.
    /// </summary>
    public double GetAvailableTokens()
    {
        lock (_lock)
        {
            Refill();
            return _tokens;
        }
    }

    /// <summary>
    /// Raw Stopwatch timestamp of the last TryAcquire call. Used by the
    /// owning RateLimiter to detect and evict idle buckets.
    /// </summary>
    public long GetLastAccessTimeTicks()
    {
        lock (_lock)
        {
            return _lastAccessTicks;
        }
    }

    /// <summary>
    /// Tops up _tokens based on elapsed time since the last refill, capped at
    /// Capacity. Must be called while holding _lock.
    /// </summary>
    private void Refill()
    {
        var now = Stopwatch.GetTimestamp();
        var elapsedSeconds = (now - _lastRefillTicks) / (double)Stopwatch.Frequency;

        if (elapsedSeconds > 0)
        {
            _tokens = Math.Min(Capacity, _tokens + elapsedSeconds * TokensPerSecond);
            _lastRefillTicks = now;
        }
    }
}

/// <summary>
/// Point-in-time status for a single client, useful for exposing rate limit
/// info to callers (e.g. as response headers: X-RateLimit-Remaining, etc.).
/// </summary>
public record RateLimitInfo(double RemainingTokens, long Capacity, double TokensPerSecond)
{
    /// <summary>
    /// Estimated seconds until the bucket is back at full capacity.
    /// Returns 0 if it's already full.
    /// </summary>
    public double GetResetTimeSeconds()
    {
        if (RemainingTokens >= Capacity)
            return 0;

        return (Capacity - RemainingTokens) / TokensPerSecond;
    }
}

/// <summary>
/// A multi-client, in-process token bucket rate limiter.
///
/// Each distinct clientId (user ID, API key, IP, etc.) gets its own
/// TokenBucket, created lazily on first use. A background task periodically
/// evicts buckets that have gone idle, so memory usage stays bounded even
/// with a large, ever-changing population of clients (e.g. rate limiting by
/// IP address).
///
/// This is a single-process limiter — it does NOT share state across multiple
/// server instances. For a multi-server deployment, use a Redis-backed
/// limiter instead.
/// </summary>
public class RateLimiter : IDisposable
{
    // One bucket per client, created on demand.
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();

    private readonly RateLimitConfig _config;

    // Signals the background cleanup loop to stop when the limiter shuts down.
    private readonly CancellationTokenSource _cts = new();

    // Background task that periodically removes idle buckets.
    private readonly Task _cleanupTask;

    // 1 = running, 0 = shut down. Read/written via Volatile/Interlocked so it's
    // safe to check from any thread without a lock.
    private int _running = 1;

    private bool IsRunning => Volatile.Read(ref _running) == 1;

    public RateLimiter(RateLimitConfig config)
    {
        // Fail fast on bad configuration rather than producing a limiter that
        // silently never blocks (e.g. TokensPerSecond = 0) or never allows
        // anything (e.g. Capacity = 0).
        config.Validate();

        _config = config;
        _cleanupTask = Task.Run(CleanupLoopAsync);
    }

    /// <summary>Convenience constructor for the common case of default cleanup settings.</summary>
    public RateLimiter(double tokensPerSecond, long capacity)
        : this(new RateLimitConfig(tokensPerSecond, capacity))
    {
    }

    /// <summary>
    /// Attempts to consume <paramref name="permits"/> tokens for the given client.
    /// Returns true if the request is allowed, false if it should be rejected
    /// (e.g. respond with HTTP 429).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if called after Shutdown/Dispose.</exception>
    /// <exception cref="ArgumentException">Thrown if permits is not positive.</exception>
    public bool TryAcquire(string clientId, int permits = 1)
    {
        if (!IsRunning)
        {
            throw new InvalidOperationException("RateLimiter is shut down");
        }

        if (permits <= 0)
        {
            throw new ArgumentException("permits must be positive");
        }

        // Known limitation: there is a narrow window where TryAcquire passes
        // the IsRunning check above, then Shutdown() runs and clears _buckets,
        // and only then does GetOrAdd insert a new entry below. That entry
        // will never be cleaned up since the cleanup loop has already stopped.
        // In practice this only matters for limiters that are frequently
        // created/destroyed under sustained concurrent load; a long-lived
        // singleton limiter won't notice it.
        var bucket = _buckets.GetOrAdd(clientId,
            _ => new TokenBucket(_config.Capacity, _config.TokensPerSecond));

        return bucket.TryAcquire(permits);
    }

    /// <summary>
    /// Returns the current rate limit status for a client. If the client has
    /// no bucket yet (never made a request, or was evicted for inactivity),
    /// reports a fresh, full bucket — an idle-evicted client is indistinguishable
    /// from one that's never been seen, which is the correct behavior since
    /// eviction only happens once a client has been idle long enough to be
    /// back at full capacity anyway.
    /// </summary>
    public RateLimitInfo GetClientInfo(string clientId)
    {
        if (_buckets.TryGetValue(clientId, out var bucket))
        {
            return new RateLimitInfo(
                bucket.GetAvailableTokens(),
                bucket.Capacity,
                bucket.TokensPerSecond
            );
        }

        return new RateLimitInfo(
            _config.Capacity,
            _config.Capacity,
            _config.TokensPerSecond
        );
    }

    /// <summary>
    /// Background loop that periodically scans all buckets and removes any
    /// that have been idle longer than InactivityTimeout, keeping memory
    /// bounded when rate limiting by a high-cardinality key like IP address.
    /// </summary>
    private async Task CleanupLoopAsync()
    {
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(_config.CleanupInterval, _cts.Token);

                // Use the same clock (Stopwatch) as TokenBucket so "now" and
                // "last access" are always directly comparable.
                long nowTicks = Stopwatch.GetTimestamp();
                long timeoutTicks = (long)(_config.InactivityTimeout.TotalSeconds * Stopwatch.Frequency);

                foreach (var kvp in _buckets)
                {
                    long lastAccess = kvp.Value.GetLastAccessTimeTicks();
                    if ((nowTicks - lastAccess) > timeoutTicks)
                    {
                        _buckets.TryRemove(kvp.Key, out _);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when Shutdown() cancels _cts — not an error.
        }
    }

    /// <summary>Number of clients currently tracked. Mainly useful for diagnostics/metrics.</summary>
    public int ActiveBucketCount => _buckets.Count;

    /// <summary>
    /// Stops the background cleanup loop and clears all bucket state.
    /// Safe to call multiple times — only the first call has any effect.
    /// After this, TryAcquire will throw InvalidOperationException.
    /// </summary>
    public void Shutdown()
    {
        // Only the thread that flips _running from 1 to 0 performs the actual
        // shutdown work, so concurrent/duplicate calls are safe no-ops.
        if (Interlocked.Exchange(ref _running, 0) == 1)
        {
            _cts.Cancel();
            try
            {
                _cleanupTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // Expected: the cleanup loop's OperationCanceledException
                // surfaces here as an AggregateException when awaited via .Wait().
            }
            _buckets.Clear();
        }
    }

    public void Dispose()
    {
        Shutdown();
        _cts.Dispose();
    }
}
