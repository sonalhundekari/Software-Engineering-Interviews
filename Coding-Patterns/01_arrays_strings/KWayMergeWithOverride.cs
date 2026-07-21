// ============================================================================
// Merge K Sorted (Key, Value) Linked Lists with Later-List Override
// ============================================================================
//
// PROBLEM
// -------
// Given k linked lists l_0 .. l_{k-1}, each sorted ascending by key, with
// unique keys within a single list, merge them into one sorted list. When a
// key appears in multiple lists, the value from the list with the HIGHEST
// index wins (l_2 overrides l_1 overrides l_0).
//
// COMPLEXITY
// ----------
// Let N = total number of (key, value) pairs across all lists, k = list count.
//
//   Time:  O(N log k)
//          - Every node is enqueued and dequeued exactly once.
//          - Each heap operation costs O(log k) since the heap never holds
//            more than one live entry per list.
//          - Initial seeding of k heads: O(k log k) (or O(k) with heapify),
//            dominated by the main loop.
//
//   Space: O(k) auxiliary for the heap (one entry per non-exhausted list).
//          The output list is O(N) but that is required output, not overhead.
//
//   Large-k follow-up: if k is huge and lists are short (length ~1), then
//   N ~ k and the algorithm degenerates to O(k log k) — i.e. sorting. Without
//   structural assumptions (bounded key domain -> counting sort, disjoint key
//   ranges -> concatenation) there is no asymptotic improvement; the
//   comparison-sort lower bound applies. A tournament (loser) tree gives the
//   same asymptotics with better constants and cache behavior.
//
// IMPLEMENTATION DETAILS
// ----------------------
// 1. MIN-heap, not max-heap. Output must be ascending by key, so at each step
//    we need the globally smallest unprocessed key. The override resolution is
//    independent of heap polarity — it happens in the drain loop below.
//
// 2. Heap priority is the tuple (Key, ListIndex). The secondary ListIndex
//    ordering means equal keys pop in ascending list order, so "last popped
//    wins" would suffice. We still track winnerIdx explicitly so the logic
//    stays correct even if the comparer is changed to key-only, and because
//    stating the rule explicitly ("largest list index wins") is clearer in an
//    interview.
//
// 3. Drain pattern for duplicate keys: after popping the minimum, peek the
//    heap; while the top entry has the SAME key, pop it too. Among all popped
//    entries for the key, keep the value from the largest list index. Emit
//    exactly one output node per key.
//
// 4. Each popped node's successor (Node.Next) is pushed back immediately,
//    keeping at most one live cursor per list in the heap at all times —
//    this is what bounds heap size to O(k).
//
// 5. Dummy head node avoids special-casing the first emitted node; handles
//    the all-lists-empty case by returning dummy.Next == null.
//
// 6. Null lists in the input array are skipped during seeding.
//
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;

public class ListNode
{
    public int Key;
    public int Value;
    public ListNode Next;

    public ListNode(int key, int value)
    {
        Key = key;
        Value = value;
    }
}

public static class KWayMerger
{
    public static ListNode MergeWithOverride(ListNode[] lists)
    {
        // Min-heap keyed on (Key, ListIndex). See IMPLEMENTATION DETAILS #1, #2.
        var heap = new PriorityQueue<(ListNode Node, int ListIdx), (int Key, int ListIdx)>();

        // Seed with the head of each non-empty list. O(k log k).
        for (int i = 0; i < lists.Length; i++)
        {
            if (lists[i] != null)
                heap.Enqueue((lists[i], i), (lists[i].Key, i));
        }

        var dummy = new ListNode(0, 0);   // Detail #5: dummy head
        var tail = dummy;

        while (heap.Count > 0)
        {
            // Pop the globally smallest key.
            var (node, idx) = heap.Dequeue();
            int currentKey = node.Key;

            // Track the winning entry: largest list index seen for this key.
            var winner = node;
            int winnerIdx = idx;

            // Advance this list's cursor back into the heap. Detail #4.
            if (node.Next != null)
                heap.Enqueue((node.Next, idx), (node.Next.Key, idx));

            // Drain all remaining entries with the same key. Detail #3.
            while (heap.Count > 0 && heap.Peek().Node.Key == currentKey)
            {
                var (dupNode, dupIdx) = heap.Dequeue();

                if (dupIdx > winnerIdx)   // later list overrides earlier
                {
                    winner = dupNode;
                    winnerIdx = dupIdx;
                }

                if (dupNode.Next != null)
                    heap.Enqueue((dupNode.Next, dupIdx), (dupNode.Next.Key, dupIdx));
            }

            // Emit exactly one node per key.
            tail.Next = new ListNode(currentKey, winner.Value);
            tail = tail.Next;
        }

        return dummy.Next;
    }

    // ------------------------------------------------------------------------
    // Helpers for building and printing lists in tests
    // ------------------------------------------------------------------------

    public static ListNode Build(params (int Key, int Value)[] pairs)
    {
        var dummy = new ListNode(0, 0);
        var tail = dummy;
        foreach (var (k, v) in pairs)
        {
            tail.Next = new ListNode(k, v);
            tail = tail.Next;
        }
        return dummy.Next;
    }

    public static string Print(ListNode head)
    {
        var sb = new StringBuilder("[");
        for (var cur = head; cur != null; cur = cur.Next)
        {
            sb.Append($"({cur.Key},{cur.Value})");
            if (cur.Next != null) 
                sb.Append(", ");
        }
        sb.Append(']');
        return sb.ToString();
    }
}

public static class Program
{
    public static void Main()
    {
        // --- Example from the problem statement -----------------------------
        // l_0 = [(1,70), (3,20), (5,30)]
        // l_1 = [(2,40), (3,50)]
        // l_2 = [(1,15), (4,80), (5,90)]
        // Expected: [(1,15), (2,40), (3,50), (4,80), (5,90)]
        var lists = new[]
        {
            KWayMerger.Build((1, 70), (3, 20), (5, 30)),
            KWayMerger.Build((2, 40), (3, 50)),
            KWayMerger.Build((1, 15), (4, 80), (5, 90)),
        };

        var merged = KWayMerger.MergeWithOverride(lists);
        Console.WriteLine("Example:       " + KWayMerger.Print(merged));

        // --- Edge case: all lists empty -------------------------------------
        var empty = KWayMerger.MergeWithOverride(new ListNode[] { null, null });
        Console.WriteLine("All empty:     " + KWayMerger.Print(empty));   // []

        // --- Edge case: single list passes through unchanged ----------------
        var single = KWayMerger.MergeWithOverride(new[]
        {
            KWayMerger.Build((10, 1), (20, 2), (30, 3)),
        });
        Console.WriteLine("Single list:   " + KWayMerger.Print(single));

        // --- Edge case: same key in every list, highest index wins ----------
        var allCollide = KWayMerger.MergeWithOverride(new[]
        {
            KWayMerger.Build((7, 100)),
            KWayMerger.Build((7, 200)),
            KWayMerger.Build((7, 300)),
        });
        Console.WriteLine("All collide:   " + KWayMerger.Print(allCollide)); // [(7,300)]

        // --- Edge case: null entries mixed with real lists ------------------
        var withNulls = KWayMerger.MergeWithOverride(new[]
        {
            null,
            KWayMerger.Build((2, 5), (4, 6)),
            null,
            KWayMerger.Build((1, 9), (4, 60)),
        });
        Console.WriteLine("With nulls:    " + KWayMerger.Print(withNulls)); // [(1,9), (2,5), (4,60)]
    }
}

/* Expected output:

Example:       [(1,15), (2,40), (3,50), (4,80), (5,90)]
All empty:     []
Single list:   [(10,1), (20,2), (30,3)]
All collide:   [(7,300)]
With nulls:    [(1,9), (2,5), (4,60)]

*/
