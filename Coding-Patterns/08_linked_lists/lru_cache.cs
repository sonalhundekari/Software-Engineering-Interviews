// LeetCode 146 - LRU Cache
// Difficulty: Medium
// Pattern: HashMap + Doubly Linked List
//
// Time: O(1) for get and put  Space: O(capacity)

public class LRUCache
{
    private class Node
    {
        public int Key, Val;
        public DateTime? ExpiresAtUtc; // null = no expiration
        public Node Prev, Next;
        public Node(int k = 0, int v = 0) 
        { 
            Key = k; Val = v; 
        }
    }

    private readonly int _capacity;
    private readonly Dictionary<int, Node> _cache = new();
    private readonly Node _head = new(); // LRU end (dummy)
    private readonly Node _tail = new(); // MRU end (dummy)

    public LRUCache(int capacity)
    {
        _capacity = capacity;
        _head.Next = _tail;
        _tail.Prev = _head;
    }

    private void Remove(Node node)
    {
        node.Prev.Next = node.Next;
        node.Next.Prev = node.Prev;
    }

    private void InsertTail(Node node)
    {
        node.Prev = _tail.Prev;
        node.Next = _tail;
        _tail.Prev.Next = node;
        _tail.Prev = node;
    }

    public int Get(int key)
    {
        if (!_cache.TryGetValue(key, out var node)) 
            return -1;
        if (node.ExpiresAtUtc.HasValue && node.ExpiresAtUtc.Value <= DateTime.UtcNow)
        {
            Remove(node);
            _cache.Remove(key);
            return -1;
        }
        Remove(node);
        InsertTail(node);
        return node.Val;
    }

    public void Put(int key, int value) => PutInternal(key, value, expiresAtUtc: null);

    // Same as Put, but the entry is treated as absent (by Get) once ttl has elapsed,
    // even if it hasn't been evicted by capacity yet.
    public void PutWithTtl(int key, int value, TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl));
        PutInternal(key, value, DateTime.UtcNow + ttl);
    }

    private void PutInternal(int key, int value, DateTime? expiresAtUtc)
    {
        if (_cache.ContainsKey(key))
            Remove(_cache[key]);

        var node = new Node(key, value)
        {
                ExpiresAtUtc = expiresAtUtc
        };
        _cache[key] = node;

        InsertTail(node);
        
        if (_cache.Count > _capacity)
        {
            var lru = _head.Next;
            Remove(lru);
            _cache.Remove(lru.Key);
        }
    }

    public static void Main()
    {
        var lru = new LRUCache(2);
        lru.Put(1, 1); lru.Put(2, 2);
        Console.WriteLine(lru.Get(1));  // 1
        lru.Put(3, 3);                  // evicts key 2
        Console.WriteLine(lru.Get(2));  // -1
        lru.Put(4, 4);                  // evicts key 1
        Console.WriteLine(lru.Get(1));  // -1
        Console.WriteLine(lru.Get(3));  // 3
        Console.WriteLine(lru.Get(4));  // 4

        var ttlCache = new LRUCache(2);
        ttlCache.PutWithTtl(5, 5, TimeSpan.FromMilliseconds(50));
        Console.WriteLine(ttlCache.Get(5));  // 5 (not yet expired)
        Thread.Sleep(75);
        Console.WriteLine(ttlCache.Get(5));  // -1 (expired, though never evicted by capacity)
    }
}
