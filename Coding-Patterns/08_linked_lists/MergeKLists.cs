/*
Merge k sorted linked lists and return it as one sorted list.

Time Complexity: O(N log k) - where N is the total number of nodes in all lists and k is the number of linked lists.
        Each node is inserted into the priority queue once, and the priority queue has a size of at most k, so each insertion takes O(log k) time.
space Complexity: O(k) - The priority queue can hold at most k nodes at any time, where k is the number of linked lists.
*/
public class ListNode<T> {
    public T val;
    public ListNode<T> next;
    public ListNode(T val = default, ListNode<T> next = null) {
        this.val = val;
        this.next = next;
    }
}

public class MergeKLists<T> where T : IComparable<T> {
    public ListNode<T> MergeKLists(ListNode<T>[] lists) {
        if (lists == null || lists.Length == 0) 
            return null;

        // Min-heap ordered by node value; tie-break with an insertion index
        // to keep comparisons stable (PriorityQueue requires a comparable key).
        var pq = new PriorityQueue<ListNode<T>, (T val, int order)>();

        int order = 0;
        foreach (var node in lists) {
            if (node != null) {
                pq.Enqueue(node, (node.val, order++));
            }
        }

        var dummy = new ListNode<T>();
        var tail = dummy;

        while (pq.Count > 0) {
            var node = pq.Dequeue();
            tail.next = node;
            tail = tail.next;

            if (node.next != null) {
                pq.Enqueue(node.next, (node.next.val, order++));
            }
        }

        tail.next = null;
        return dummy.next;
    }

    /*
    Recursive divide-and-conquer: pair up lists and merge each pair, halving
    the number of lists every round until only one remains.

    Time Complexity: O(N log k) - there are log k rounds of pairwise merges,
            and each round merges a total of N nodes across all pairs.
    Space Complexity: O(log k) - for the recursion call stack (plus O(1)
            extra space per merge, since MergeTwoLists is iterative and
            reuses existing nodes rather than allocating new ones).
    */
    public ListNode<T> MergeKListsRecursive(ListNode<T>[] lists) {
        if (lists == null || lists.Length == 0) return null;
        return Merge(lists, 0, lists.Length - 1);
    }

    private ListNode<T> Merge(ListNode<T>[] lists, int left, int right) {
        if (left == right) return lists[left];

        int mid = left + (right - left) / 2;
        var leftMerged = Merge(lists, left, mid);
        var rightMerged = Merge(lists, mid + 1, right);
        return MergeTwoLists(leftMerged, rightMerged);
    }

    private ListNode<T> MergeTwoLists(ListNode<T> a, ListNode<T> b) {
        var dummy = new ListNode<T>();
        var tail = dummy;

        while (a != null && b != null) {
            if (a.val.CompareTo(b.val) <= 0) {
                tail.next = a;
                a = a.next;
            } else {
                tail.next = b;
                b = b.next;
            }
            tail = tail.next;
        }

        tail.next = a ?? b;
        return dummy.next;
    }

    public static void Main() {
        var mergeKLists = new MergeKLists<int>();

        // Example: Merge 3 sorted linked lists
        ListNode<int> list1 = new(1, new(4, new(5)));
        ListNode<int> list2 = new(1, new(3, new(4)));
        ListNode<int> list3 = new(2, new(6));

        ListNode<int>[] lists = { list1, list2, list3 };
        ListNode<int> mergedList = mergeKLists.MergeKLists(lists);

        // Print the merged linked list
        while (mergedList != null) {
            Console.Write(mergedList.val + " ");
            mergedList = mergedList.next;
        }
    }
}
