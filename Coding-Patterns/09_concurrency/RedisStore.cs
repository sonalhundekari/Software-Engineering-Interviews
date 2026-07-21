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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

// ---------------------------------------------------------------------------
// Inline self-checks (top-level statements; replace with a test project for real)
// ---------------------------------------------------------------------------

var failures = 0;

void Check(string name, bool ok)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}");
    if (!ok) failures++;
}

bool SeqEq(object? actual, IEnumerable<string> expected)
    => actual is IReadOnlyList<string> l && l.SequenceEqual(expected);

// Build a list whose head->tail order is: x c x b x a x  (four x's).
RedisStore Build()
{
    var s = new RedisStore();
    foreach (var v in new[] { "x", "a", "x", "b", "x", "c", "x" })
        s.ListPush("L", v);
    return s;
}

// string set/get/overwrite
{
    var s = new RedisStore();
    s.Set("k", "v1");
    Check("set/get string", s.TryGet("k", out var v) && (string)v! == "v1");
    s.Set("k", "v2");
    Check("overwrite string", s.TryGet("k", out var v2) && (string)v2! == "v2");
    Check("missing key -> false", !s.TryGet("nope", out _));
}

// push order
Check("push order (head-first)",
    Build().TryGet("L", out var pushed) &&
    SeqEq(pushed, new[] { "x", "c", "x", "b", "x", "a", "x" }));

// list_remove: all three count semantics + boundaries
var cases = new (string name, string val, int count, string[] want, int removed)[]
{
    ("count=+2 (head)", "x",  2, new[] { "c", "b", "x", "a", "x" },         2),
    ("count=-2 (tail)", "x", -2, new[] { "x", "c", "x", "b", "a" },         2),
    ("count=0  (all)",  "x",  0, new[] { "c", "b", "a" },                   4),
    ("count=+1",        "x",  1, new[] { "c", "x", "b", "x", "a", "x" },     1),
    ("count=-1",        "x", -1, new[] { "x", "c", "x", "b", "x", "a" },     1),
    ("no match",        "z",  0, new[] { "x", "c", "x", "b", "x", "a", "x" },0),
    ("count > length",  "x", 99, new[] { "c", "b", "a" },                   4),
};
foreach (var c in cases)
{
    var s = Build();
    var n = s.ListRemove("L", c.val, c.count);
    var okList = s.TryGet("L", out var after) ? SeqEq(after, c.want) : c.want.Length == 0;
    Check($"list_remove {c.name}", n == c.removed && okList);
}

// emptied list deletes its key
{
    var s = new Store();
    s.ListPush("L", "x");
    s.ListPush("L", "x");
    s.ListRemove("L", "x", 0);
    Check("emptied list deletes key", !s.TryGet("L", out _));
}

// missing key is a no-op
Check("list_remove missing key -> 0", new Store().ListRemove("nope", "x", 0) == 0);

// type mismatch -> WrongTypeException
{
    var s = new Store();
    s.Set("k", "str");
    var threw = false;
    try { s.ListPush("k", "v"); } catch (WrongTypeException) { threw = true; }
    Check("push onto string -> WRONGTYPE", threw);
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : $"{failures} FAILURE(S)");
Environment.Exit(failures == 0 ? 0 : 1);

// ---------------------------------------------------------------------------
// Store
// ---------------------------------------------------------------------------

/// <summary>Tags the variant held by an <see cref="Entry"/>.</summary>
enum EntryKind
{
    String,
    List,
}

/// <summary>
/// Tagged union: exactly one of <see cref="Str"/> / <see cref="Lst"/> is
/// meaningful, selected by <see cref="Kind"/>. Preferred over <c>object</c> so
/// the kind is explicit and reads switch on <see cref="Kind"/>.
/// </summary>
sealed class Entry
{
    public EntryKind Kind { get; }
    public string? Str { get; }
    public LinkedList<string>? Lst { get; }

    private Entry(EntryKind kind, string? str, LinkedList<string>? lst)
    {
        Kind = kind;
        Str = str;
        Lst = lst;
    }

    public static Entry OfString(string value) => new(EntryKind.String, value, null);
    public static Entry OfList(LinkedList<string> list) => new(EntryKind.List, null, list);
}

/// <summary>Raised when an operation targets a key of the wrong kind (Redis WRONGTYPE).</summary>
sealed class WrongTypeException : Exception
{
    public WrongTypeException()
        : base("WRONGTYPE operation against a key holding the wrong kind of value") { }
}

/// <summary>
/// Concurrency-safe, Redis-like key/value store. A single
/// <see cref="ReaderWriterLockSlim"/> guards the whole dictionary; lists use
/// <see cref="LinkedList{T}"/> so the count&lt;0 case is a real tail-to-head walk.
/// </summary>
sealed class RedisStore : IDisposable
{
    private readonly Dictionary<string, Entry> _data = new();
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);

    /// <summary>Store a string at <paramref name="key"/>, overwriting any existing value.</summary>
    public void Set(string key, string value)
    {
        _lock.EnterWriteLock();
        try { _data[key] = Entry.OfString(value); }
        finally { _lock.ExitWriteLock(); }
    }

    /// <summary>
    /// Push <paramref name="value"/> onto the head of the list at
    /// <paramref name="key"/>, creating it if absent.
    /// </summary>
    /// <exception cref="WrongTypeException">If the key already holds a string.</exception>
    public void ListPush(string key, string value)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_data.TryGetValue(key, out var entry))
            {
                entry = Entry.OfList(new LinkedList<string>());
                _data[key] = entry;
            }
            else if (entry.Kind != EntryKind.List)
            {
                throw new WrongTypeException();
            }

            entry.Lst!.AddFirst(value);
        }
        finally { _lock.ExitWriteLock(); }
    }

    /// <summary>
    /// Return the value at <paramref name="key"/>: a <see cref="string"/>, or an
    /// <see cref="IReadOnlyList{String}"/> head->tail snapshot copy for a list.
    /// Returns false if absent.
    /// </summary>
    public bool TryGet(string key, out object? value)
    {
        _lock.EnterReadLock();
        try
        {
            if (!_data.TryGetValue(key, out var entry))
            {
                value = null;
                return false;
            }

            value = entry.Kind switch
            {
                EntryKind.String => entry.Str!,
                EntryKind.List => new List<string>(entry.Lst!), // copy; caller can't mutate internals
                _ => null,
            };
            return true;
        }
        finally { _lock.ExitReadLock(); }
    }

    /// <summary>
    /// Remove occurrences of <paramref name="value"/> from the list at
    /// <paramref name="key"/> (Redis LREM):
    /// <list type="bullet">
    /// <item><c>count &gt; 0</c>: up to <c>count</c> matches, head → tail.</item>
    /// <item><c>count &lt; 0</c>: up to <c>|count|</c> matches, tail → head.</item>
    /// <item><c>count == 0</c>: every match.</item>
    /// </list>
    /// Returns the number removed. Missing key is a no-op (0); the key is
    /// deleted once its list empties.
    /// </summary>
    /// <exception cref="WrongTypeException">If the key holds a string.</exception>
    public int ListRemove(string key, string value, int count)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_data.TryGetValue(key, out var entry))
            {
                return 0; // no-op on missing key
            }
            if (entry.Kind != EntryKind.List)
            {
                throw new WrongTypeException();
            }

            var list = entry.Lst!;
            var removed = 0;

            if (count >= 0)
            {
                var limit = count; // 0 means unlimited
                var node = list.First;
                while (node is not null)
                {
                    var next = node.Next; // capture before a possible Remove invalidates node
                    if (node.Value == value)
                    {
                        list.Remove(node);
                        removed++;
                        if (limit > 0 && removed >= limit) break;
                    }
                    node = next;
                }
            }
            else
            {
                var limit = -count;
                var node = list.Last;
                while (node is not null)
                {
                    var prev = node.Previous; // capture before a possible Remove invalidates node
                    if (node.Value == value)
                    {
                        list.Remove(node);
                        removed++;
                        if (removed >= limit) break;
                    }
                    node = prev;
                }
            }

            if (list.Count == 0)
            {
                _data.Remove(key);
            }
            return removed;
        }
        finally { _lock.ExitWriteLock(); }
    }

    public void Dispose() => _lock.Dispose();
}
