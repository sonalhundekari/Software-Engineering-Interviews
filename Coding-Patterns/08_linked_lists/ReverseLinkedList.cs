// LeetCode 206 - Reverse Linked List
// Difficulty: Easy
// Pattern: Iterative prev/curr/next dance
//
// Time: O(n)  Space: O(1)

public class ReverseLinkedList
{
    public ListNode Reverse(ListNode head)
    {
        ListNode prev = null, curr = head;
        while (curr != null)
        {
            var next = curr.Next;
            curr.Next = prev;
            prev = curr;
            curr = next;
        }
        return prev;
    }
}
