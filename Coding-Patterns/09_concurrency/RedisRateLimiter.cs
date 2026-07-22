// Redis-backed distributed rate limiter
// Pattern: Token bucket, state shared via Redis so multiple server instances
// enforce ONE limit per client instead of each process tracking its own.
//
// Problem: rate_limiter.cs's TokenBucket lives in a ConcurrentDictionary
// inside a single process. That's fine for one server, but the moment you
// run N instances behind a load balancer, each instance has its own private
// view of every client's bucket -- a client can get up to N times the
// intended limit just by getting routed to a different instance each request.
//
// Approach:
//   - Move the bucket's state (tokens, last-refill time) out of process
//     memory and into Redis, keyed by client ID. Every app server instance
//     reads and writes the SAME Redis keys, so they all agree on how many
//     tokens a client has left.
//   - The read-refill-consume-write sequence has to happen as one atomic
//     operation, or two servers can race: both read "3 tokens available",
//     both decide to allow the request, and the client ends up consuming
//     more than it should have. A plain GET/SET pair from the client side
//     cannot prevent this -- there's no way to lock across two round trips.
//   - Redis solves this with EVAL: a Lua script sent to Redis runs to
//     completion on the server, uninterrupted by any other command (Redis
//     is single-threaded), so the whole refill+consume+persist sequence is
//     atomic even under heavy concurrent load from many app servers.
//   - "Now" is read via Redis's own TIME command *inside* the script rather
//     than passed in by the calling app server. If each server used its own
//     DateTime.UtcNow, clock skew between servers could let a client burst
//     past its limit (fast clock) or get throttled early (slow clock).
//     Redis is the one shared authority every server already depends on, so
//     it doubles as the shared clock too.
//   - No background cleanup loop is needed (contrast with RateLimiter's
//     CleanupLoopAsync in rate_limiter.cs): each bucket key carries a TTL via
//     EXPIRE, so Redis itself reclaims idle clients' state automatically.
//
// Requires the StackExchange.Redis NuGet package:
//   dotnet add package StackExchange.Redis
// and a reachable Redis (or Redis-compatible) server. Unlike redis_cache.cs,
// this file has no in-memory fallback -- simulating this in-process would
// defeat the point of the exercise, which is sharing state ACROSS processes.
//
// Time: O(1) Redis round trip per TryAcquireAsync  Space: O(1) per client (one Redis hash key)

using System;
using System.Threading.Tasks;
using StackExchange.Redis;

/// <summary>
/// Configuration for a <see cref="RedisRateLimiter"/>. Mirrors RateLimitConfig
/// from rate_limiter.cs but swaps the in-process idle-cleanup loop for a
/// Redis key TTL, since Redis already knows how to expire keys on its own.
/// </summary>
public record RedisRateLimitConfig(double TokensPerSecond, long Capacity)
{
    /// <summary>
    /// How long an idle client's bucket key survives in Redis before being
    /// expired automatically. Should comfortably exceed the time it takes to
    /// refill from empty to full, so an active bucket is never evicted
    /// mid-refill and mistaken for a brand-new client.
    /// </summary>
    public TimeSpan BucketTtl { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Validates the configuration so misconfiguration fails fast.</summary>
    public void Validate()
    {
        if (TokensPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(TokensPerSecond), "Must be positive.");

        if (Capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(Capacity), "Must be positive.");

        if (BucketTtl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(BucketTtl), "Must be positive.");
    }
}

/// <summary>
/// Point-in-time status for a single client. Mirrors RateLimitInfo from
/// rate_limiter.cs so the two limiters are drop-in-comparable.
/// </summary>
public record RedisRateLimitInfo(double RemainingTokens, long Capacity, double TokensPerSecond)
{
    /// <summary>Estimated seconds until the bucket is back at full capacity. 0 if already full.</summary>
    public double GetResetTimeSeconds()
    {
        if (RemainingTokens >= Capacity)
            return 0;

        return (Capacity - RemainingTokens) / TokensPerSecond;
    }
}

/// <summary>
/// A multi-client token bucket rate limiter whose state lives in Redis, so
/// any number of application server instances share one consistent view of
/// each client's remaining tokens.
///
/// Contrast with RateLimiter (rate_limiter.cs), which is single-process only
/// and cannot be used correctly behind a load balancer with multiple
/// instances -- see the file header for why.
/// </summary>
public class RedisRateLimiter : IDisposable
{
    // Lua script executed atomically on the Redis server. Redis processes a
    // script as a single, uninterruptible unit of work, so this whole
    // refill-then-consume sequence can never be interleaved with another
    // request touching the same key -- that's what makes it safe across
    // concurrently-racing app server instances, which a client-side
    // GET-then-SET could never guarantee (see the "Approach" notes above).
    //
    // KEYS[1] = bucket key (e.g. "ratelimit:{clientId}")
    // ARGV[1] = capacity (max tokens)
    // ARGV[2] = tokens per second (refill rate)
    // ARGV[3] = permits requested (0 for a non-consuming "peek")
    // ARGV[4] = bucket TTL in seconds
    //
    // Returns { allowed (0/1), tokens remaining after this call }
    private const string TokenBucketScript = @"
        local key = KEYS[1]
        local capacity = tonumber(ARGV[1])
        local rate = tonumber(ARGV[2])
        local permits = tonumber(ARGV[3])
        local ttl = tonumber(ARGV[4])

        -- Redis's own clock, not the calling app server's -- see file header.
        local time = redis.call('TIME')
        local now = tonumber(time[1]) + tonumber(time[2]) / 1000000

        local bucket = redis.call('HMGET', key, 'tokens', 'last_refill')
        local tokens = tonumber(bucket[1])
        local last_refill = tonumber(bucket[2])

        if tokens == nil then
            -- No bucket yet for this client: start full, same as
            -- TokenBucket's constructor in rate_limiter.cs, so a client's
            -- very first burst isn't throttled.
            tokens = capacity
            last_refill = now
        end

        local elapsed = math.max(0, now - last_refill)
        tokens = math.min(capacity, tokens + elapsed * rate)

        local allowed = 0
        if tokens >= permits then
            tokens = tokens - permits
            allowed = 1
        end

        redis.call('HSET', key, 'tokens', tostring(tokens), 'last_refill', tostring(now))
        -- Refresh the TTL on every touch so active clients never expire
        -- mid-use; only genuinely idle clients' keys age out.
        redis.call('EXPIRE', key, ttl)

        return {allowed, tostring(tokens)}
    ";

    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly RedisRateLimitConfig _config;

    // Namespaces bucket keys so this limiter's keys don't collide with other
    // Redis users sharing the same server/database.
    private readonly string _keyPrefix;

    /// <param name="connectionString">e.g. "localhost:6379" -- see StackExchange.Redis docs for full connection string syntax.</param>
    /// <param name="config">Rate limit parameters shared by every app server instance pointed at the same Redis.</param>
    /// <param name="keyPrefix">Prefix applied to every bucket key, e.g. "ratelimit:{clientId}".</param>
    public RedisRateLimiter(string connectionString, RedisRateLimitConfig config, string keyPrefix = "ratelimit:")
    {
        // Fail fast on bad configuration rather than silently never
        // blocking (e.g. TokensPerSecond = 0) or never allowing anything.
        config.Validate();

        _config = config;
        _keyPrefix = keyPrefix;
        _redis = ConnectionMultiplexer.Connect(connectionString);
        _db = _redis.GetDatabase();
    }

    /// <summary>
    /// Attempts to consume <paramref name="permits"/> tokens for the given
    /// client. Returns true if the request is allowed, false if it should be
    /// rejected (e.g. respond with HTTP 429).
    ///
    /// Safe to call concurrently from any number of processes/machines
    /// against the same Redis instance -- the atomicity comes from
    /// TokenBucketScript running entirely inside Redis, not from any locking
    /// on the C# side.
    /// </summary>
    public async Task<bool> TryAcquireAsync(string clientId, int permits = 1)
    {
        if (permits <= 0)
        {
            throw new ArgumentException("permits must be positive");
        }

        var (allowed, _) = await AcquireAsync(clientId, permits);
        return allowed;
    }

    /// <summary>
    /// Returns the current rate limit status for a client. Implemented as a
    /// 0-permit "acquire" so it still applies any owed refill and persists
    /// it (mirroring TokenBucket.GetAvailableTokens in rate_limiter.cs),
    /// without ever failing or consuming a token.
    /// </summary>
    public async Task<RedisRateLimitInfo> GetClientInfoAsync(string clientId)
    {
        var (_, tokensRemaining) = await AcquireAsync(clientId, 0);
        return new RedisRateLimitInfo(tokensRemaining, _config.Capacity, _config.TokensPerSecond);
    }

    private async Task<(bool allowed, double tokensRemaining)> AcquireAsync(string clientId, int permits)
    {
        var key = _keyPrefix + clientId;

        var redisResult = await _db.ScriptEvaluateAsync(
            TokenBucketScript,
            new RedisKey[] { key },
            new RedisValue[] { _config.Capacity, _config.TokensPerSecond, permits, (long)_config.BucketTtl.TotalSeconds });

        var values = (RedisValue[])redisResult!;
        bool allowed = (long)values[0] == 1;
        double tokensRemaining = (double)values[1];
        return (allowed, tokensRemaining);
    }

    public void Dispose() => _redis.Dispose();

    /// <summary>
    /// Demo -- requires a Redis server reachable at localhost:6379
    /// (e.g. `docker run -p 6379:6379 redis`). Uses two separate
    /// RedisRateLimiter instances standing in for two separate app servers,
    /// both pointed at the same Redis keys, to show the limit is enforced
    /// globally rather than per-process.
    /// </summary>
    public static async Task Main()
    {
        var config = new RedisRateLimitConfig(TokensPerSecond: 1, Capacity: 3);

        using var serverA = new RedisRateLimiter("localhost:6379", config);
        using var serverB = new RedisRateLimiter("localhost:6379", config);

        Console.WriteLine(await serverA.TryAcquireAsync("client-1")); // True  (3 -> 2)
        Console.WriteLine(await serverB.TryAcquireAsync("client-1")); // True  (2 -> 1) -- serverB sees serverA's consumption
        Console.WriteLine(await serverA.TryAcquireAsync("client-1")); // True  (1 -> 0)
        Console.WriteLine(await serverB.TryAcquireAsync("client-1")); // False -- out of tokens, enforced across both "servers"

        var info = await serverA.GetClientInfoAsync("client-1");
        Console.WriteLine($"Remaining: {info.RemainingTokens}, resets in {info.GetResetTimeSeconds():F1}s");
    }
}
