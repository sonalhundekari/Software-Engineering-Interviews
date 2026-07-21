// Redis-style in-memory cache
// Pattern: TTL expiration + LRU eviction, thread-safe
//
// Problem: Build a thread-safe cache that mimics how Redis is commonly used
// as a cache layer -- SET/GET/DEL with optional per-key expiry (EXPIRE/TTL),
// plus bounded memory via LRU eviction once a capacity limit is reached.
//
// Approach:
//   - Dictionary: key -> node for O(1) lookup (same idea as LRUCache)
//   - Doubly linked list: tracks recency so the least-recently-used key can
//     be evicted in O(1) once the cache is over capacity
//   - Each node also carries an absolute expiry timestamp
//   - Lazy expiration: a Get/Exists/Expire on an already-expired key removes
//     it on the spot instead of returning stale data
//   - Active expiration: a background timer sweeps for expired keys, so
//     entries nobody ever reads again still get reclaimed
//   - A single lock serializes every operation -- the simplest way to keep
//     the index and the LRU list consistent; real Redis sidesteps this
//     entirely by being single-threaded
//
// Time: O(1) for Set/Get/Delete/Expire/Ttl  Space: O(capacity)

using System;
using System.Collections.Generic;
using System.Threading;

public class RedisCache : IDisposable
{
    private class Node
    {
        public string Key = null!;
        public object? Value;
        public DateTime? ExpiresAt; // null = no expiry
        public Node? Prev;
        public Node? Next;
    }

    // Guards every field below.
    private readonly object _lock = new();
    private readonly Dictionary<string, Node> _index = new();

    // Dummy head/tail: _head.Next is the least-recently-used entry,
    // _tail.Prev is the most-recently-used entry.
    private readonly Node _head = new();
    private readonly Node _tail = new();
    private readonly int _capacity;

    // Periodically reclaims expired keys even if nothing ever calls Get on them.
    private readonly Timer _sweepTimer;

    public RedisCache(int capacity, TimeSpan? sweepInterval = null)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Must be positive.");

        _capacity = capacity;
        _head.Next = _tail;
        _tail.Prev = _head;

        var interval = sweepInterval ?? TimeSpan.FromMilliseconds(200);
        _sweepTimer = new Timer(_ => Sweep(), null, interval, interval);
    }

    /// <summary>Sets a key, optionally with a TTL. Overwrites any existing value.</summary>
    public void Set(string key, object value, TimeSpan? ttl = null)
    {
        lock (_lock)
        {
            var expiresAt = ttl.HasValue ? DateTime.UtcNow.Add(ttl.Value) : (DateTime?)null;

            if (_index.TryGetValue(key, out var existing))
            {
                existing.Value = value;
                existing.ExpiresAt = expiresAt;
                MoveToMostRecent(existing);
                return;
            }

            var node = new Node { Key = key, Value = value, ExpiresAt = expiresAt };
            _index[key] = node;
            InsertMostRecent(node);

            if (_index.Count > _capacity)
            {
                EvictLeastRecentlyUsed();
            }
        }
    }

    /// <summary>Returns true and the value if the key exists and hasn't expired.</summary>
    public bool TryGet(string key, out object? value)
    {
        lock (_lock)
        {
            if (_index.TryGetValue(key, out var node))
            {
                if (!IsExpired(node))
                {
                    MoveToMostRecent(node);
                    value = node.Value;
                    return true;
                }

                RemoveNode(node); // lazy expiration
            }

            value = null;
            return false;
        }
    }

    public bool Exists(string key)
    {
        lock (_lock)
        {
            return TryGet(key, out _);
        }
    }

    public bool Delete(string key)
    {
        lock (_lock)
        {
            if (_index.TryGetValue(key, out var node))
            {
                RemoveNode(node);
                return true;
            }
            return false;
        }
    }

    /// <summary>Sets/resets a key's TTL. Returns false if the key is missing or already expired.</summary>
    public bool Expire(string key, TimeSpan ttl)
    {
        lock (_lock)
        {
            if (!_index.TryGetValue(key, out var node) || IsExpired(node)) return false;
            node.ExpiresAt = DateTime.UtcNow.Add(ttl);
            return true;
        }
    }

    /// <summary>Seconds remaining, -1 if no expiry set, -2 if missing/expired.</summary>
    public double Ttl(string key)
    {
        lock (_lock)
        {
            if (!_index.TryGetValue(key, out var node) || IsExpired(node)) return -2;
            if (node.ExpiresAt is null) return -1;
            return Math.Max(0, (node.ExpiresAt.Value - DateTime.UtcNow).TotalSeconds);
        }
    }

    public int Count { get { lock (_lock) return _index.Count; } }

    private static bool IsExpired(Node node) =>
        node.ExpiresAt.HasValue && node.ExpiresAt.Value <= DateTime.UtcNow;

    private void RemoveNode(Node node)
    {
        node.Prev!.Next = node.Next;
        node.Next!.Prev = node.Prev;
        _index.Remove(node.Key);
    }

    private void InsertMostRecent(Node node)
    {
        node.Prev = _tail.Prev;
        node.Next = _tail;
        _tail.Prev!.Next = node;
        _tail.Prev = node;
    }

    private void MoveToMostRecent(Node node)
    {
        node.Prev!.Next = node.Next;
        node.Next!.Prev = node.Prev;
        InsertMostRecent(node);
    }

    private void EvictLeastRecentlyUsed()
    {
        var lru = _head.Next!;
        RemoveNode(lru);
    }

    // Active expiration: runs on a timer so idle keys are reclaimed even if
    // nobody ever calls TryGet on them again.
    private void Sweep()
    {
        lock (_lock)
        {
            var expired = new List<Node>();
            foreach (var node in _index.Values)
            {
                if (IsExpired(node)) expired.Add(node);
            }
            foreach (var node in expired) RemoveNode(node);
        }
    }

    public void Dispose() => _sweepTimer.Dispose();

    public static void Main()
    {
        using var cache = new RedisCache(capacity: 2);

        cache.Set("a", "1");
        cache.Set("b", "2");
        cache.TryGet("a", out _);            // touch "a" -> now most-recently-used
        cache.Set("c", "3");                 // over capacity -> evicts "b" (LRU)

        Console.WriteLine(cache.Exists("b")); // False - evicted
        cache.TryGet("a", out var a);
        Console.WriteLine(a);                 // 1
        cache.TryGet("c", out var c);
        Console.WriteLine(c);                 // 3

        cache.Set("ttl-key", "temp", TimeSpan.FromMilliseconds(50));
        Console.WriteLine(cache.Exists("ttl-key")); // True
        Thread.Sleep(100);
        Console.WriteLine(cache.Exists("ttl-key")); // False - expired
    }
}
