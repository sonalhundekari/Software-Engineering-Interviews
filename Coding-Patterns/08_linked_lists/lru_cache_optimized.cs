// LeetCode 146 - LRU Cache (OPTIMIZED)
// Difficulty: Medium
// Pattern: Dictionary + BCL LinkedList<T>
//
// Original approach: hand-rolled doubly linked list with dummy head/tail nodes
// Optimized:         use System.Collections.Generic.LinkedList<T> and
//                    LinkedListNode<T> — battle-tested, cleaner, same O(1) ops
//
// Time: O(1) get and put (same)
// Space: O(capacity)
//
// Why this matters in an interview:
// - Half the code, less room for pointer bugs
// - Signals you know the BCL — a senior signal
// - LinkedList<T> methods (AddFirst, Remove, RemoveLast) are all O(1) given a node ref
//
// Trap to avoid: you MUST hold onto the LinkedListNode<T> reference (via the dictionary
// value), not the raw value. Otherwise Remove(node) becomes O(n).

public class LRUCacheOptimized
{
    private readonly int _capacity;
    private readonly Dictionary<int, LinkedListNode<(int Key, int Val)>> _map;
    private readonly LinkedList<(int Key, int Val)> _list;

    public LRUCacheOptimized(int capacity)
    {
        _capacity = capacity;
        _map = new Dictionary<int, LinkedListNode<(int, int)>>(capacity);
        _list = new LinkedList<(int, int)>();
    }

    public int Get(int key)
    {
        if (!_map.TryGetValue(key, out var node)) return -1;
        // Move to front (most recently used)
        _list.Remove(node);
        _list.AddFirst(node);
        return node.Value.Val;
    }

    public void Put(int key, int value)
    {
        if (_map.TryGetValue(key, out var existing))
        {
            _list.Remove(existing);
            _map.Remove(key);
        }
        else if (_map.Count >= _capacity)
        {
            // Evict LRU (tail)
            var lru = _list.Last!;
            _map.Remove(lru.Value.Key);
            _list.RemoveLast();
        }

        var node = new LinkedListNode<(int, int)>((key, value));
        _list.AddFirst(node);
        _map[key] = node;
    }

    public static void Main()
    {
        var lru = new LRUCacheOptimized(2);
        lru.Put(1, 1); lru.Put(2, 2);
        Console.WriteLine(lru.Get(1));  // 1
        lru.Put(3, 3);                  // evicts key 2
        Console.WriteLine(lru.Get(2));  // -1
        lru.Put(4, 4);                  // evicts key 1
        Console.WriteLine(lru.Get(1));  // -1
        Console.WriteLine(lru.Get(3));  // 3
        Console.WriteLine(lru.Get(4));  // 4
    }
}
