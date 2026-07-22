/*
// Simplified Redis-like store: string values, head-pushed lists, and
// list_remove with Redis LREM count semantics. Single-file, runnable as:
//
//     dotnet run            (with a minimal .csproj)
//   or, on .NET 10+:  dotnet run RedisStore.cs
//
// Design notes:
//   - Values are a tagged union (Entry) rather than object, so the kind is
//     explicit and reads switch on Kind instead of pattern-matching a boxed value.
//   - A single ReaderWriterLockSlim guards the whole dictionary. Correct
//     starting point; per-key striping only helps under heavy write contention.
//   - Lists use LinkedList<T> (doubly linked) so count<0 is a genuine
//     tail-to-head walk, not a reverse-forward-reverse dance on an array.
*/

public class RedisStore : IDisposable
{
    // EntryKind enum represents the type of value stored in the Entry class.
    public enum EntryKind
    {
        String,
        List
    }

    // Entry class represents a value in the store, which can be either a string or a list.
    public class Entry
    {
        public EntryKind Kind { get; }
        public object Value { get; }

        public Entry(string value)
        {
            Kind = EntryKind.String;
            Value = value;
        }

        public Entry(LinkedList<string> list)
        {
            Kind = EntryKind.List;
            Value = list;
        }
    }

    // The underlying store: key -> Entry (string or list)
    private readonly Dictionary<string, Entry> _store = new();

    // ReaderWriterLockSlim to allow concurrent reads and exclusive writes
    private readonly ReaderWriterLockSlim _lock = new();

    // Set a string value for a key, overwriting any existing value.
    public void Set(string key, string value)
    {
        _lock.EnterWriteLock();
        try
        {
            _store[key] = new Entry(value);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    // Try to get the value for a key. Returns true if found, false otherwise.
    public bool TryGet(string key, out object? value)
    {
        _lock.EnterReadLock();
        try
        {
            if (_store.TryGetValue(key, out var entry))
            {
                value = entry.Value;
                return true;
            }
            value = null;
            return false;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    // Push a value onto the head of a list at the given key. Creates a new list if the key does not exist.
    public void ListPush(string key, string value)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_store.TryGetValue(key, out var entry) || entry.Kind != EntryKind.List)
            {
                entry = new Entry(new LinkedList<string>());
                _store[key] = entry;
            }
            ((LinkedList<string>)entry.Value).AddFirst(value);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
    
    // Remove occurrences of a value from the list at the given key, according to the count semantics.
    public int ListRemove(string key, string value, int count)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_store.TryGetValue(key, out var entry) || entry.Kind != EntryKind.List)
            {
                return 0;
            }

            var list = (LinkedList<string>)entry.Value;
            int removed = 0;

            // If count is 0, remove all occurrences
            if (count >= 0)
            {
                var node = list.First;
                // Remove up to 'count' occurrences from head to tail
                while (node != null && removed < count)
                {
                    var next = node.Next;
                    // Check if the current node's value matches the value to remove
                    if (node.Value == value)
                    {
                        list.Remove(node);
                        removed++;
                    }
                    node = next;
                }
            }
            // If count is negative, remove up to '-count' occurrences from tail to head
            else
            {
                var node = list.Last;
                // Remove up to '-count' occurrences from tail to head
                while (node != null && removed < -count)
                {
                    var prev = node.Previous;
                    // Check if the current node's value matches the value to remove
                    if (node.Value == value)
                    {
                        list.Remove(node);
                        removed++;
                    }
                    node = prev;
                }
            }

            return removed;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public class WrongTypeException : Exception
    {
        public WrongTypeException() : base("WRONGTYPE Operation against a key holding the wrong kind of value") { }
    }

    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _store.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }   

    public void Dispose()
    {
        _lock.Dispose();
    }

    public void PrintStore()
    {
        _lock.EnterReadLock();
        try
        {
            foreach (var kvp in _store)
            {
                Console.WriteLine($"Key: {kvp.Key}, Kind: {kvp.Value.Kind}, Value: {kvp.Value.Value}");
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void PrintList(string key)
    {
        _lock.EnterReadLock();
        try
        {
            if (_store.TryGetValue(key, out var entry) && entry.Kind == EntryKind.List)
            {
                var list = (LinkedList<string>)entry.Value;
                Console.WriteLine($"List at key '{key}': {string.Join(", ", list)}");
            }
            else
            {
                Console.WriteLine($"No list found at key '{key}' or wrong type.");
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public static void Main()
    {
        var store = new RedisStore();

        // Test Set and TryGet
        store.Set("key1", "value1");
        if (store.TryGet("key1", out var value))
        {
            Console.WriteLine($"Retrieved: {value}"); // Should print "Retrieved: value1"
        }

        // Test ListPush and ListRemove
        store.ListPush("mylist", "a");
        store.ListPush("mylist", "b");
        store.ListPush("mylist", "c");
        store.PrintList("mylist"); // Should print "List at key 'mylist': c, b, a"

        int removedCount = store.ListRemove("mylist", "b", 1);
        Console.WriteLine($"Removed {removedCount} occurrences of 'b'"); // Should print "Removed 1 occurrences of 'b'"
        store.PrintList("mylist"); // Should print "List at key 'mylist': c, a"

        removedCount = store.ListRemove("mylist", "a", -1);
        Console.WriteLine($"Removed {removedCount} occurrences of 'a' from the end"); // Should print "Removed 1 occurrences of 'a' from the end"
        store.PrintList("mylist"); // Should print "List at key 'mylist': c"

        // Clean up
        store.Clear();
    }   
}