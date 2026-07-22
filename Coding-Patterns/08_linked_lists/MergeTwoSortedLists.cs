// LeetCode 21 - Merge Two Sorted Lists
// Difficulty: Easy
// Pattern: Dummy head + two pointers
//
// Time: O(m + n)  Space: O(1)

public class ListNode
{
    public int Val;
    public ListNode Next;
    public ListNode(int val = 0, ListNode next = null) { Val = val; Next = next; }
}

public class MergeTwoSortedLists
{
    public ListNode MergeLists(ListNode list1, ListNode list2)
    {
        var dummy = new ListNode(0);
        var curr = dummy;

        while (list1 != null && list2 != null)
        {
            if (list1.Val <= list2.Val)
            { curr.Next = list1; list1 = list1.Next; }
            else
            { curr.Next = list2; list2 = list2.Next; }
            curr = curr.Next;
        }
        curr.Next = list1 ?? list2;
        return dummy.Next;
    }
}
