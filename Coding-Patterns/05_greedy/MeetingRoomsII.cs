// LeetCode 253 - Meeting Rooms II (OPTIMIZED)
// Difficulty: Medium
// Pattern: Min-heap via .NET 6+ PriorityQueue<T, TPriority>
//
// Original approach: SortedDictionary as a min-heap surrogate → O(log n) per op
//                    but with the overhead of a red-black tree
// Optimized:         Real PriorityQueue<int, int> (binary heap) → tighter constants
//
// Time: O(n log n)  same
// Space: O(n)
//
// Alternative: the "chronological ordering" technique — separate arrays
// of start times and end times, both sorted. Walk them together. No heap at all.
// Same O(n log n), sometimes taught as the "elegant" solution.

public class MeetingRoomsIIOptimized
{
    public int MinMeetingRoomsHeap(int[][] intervals)
    {
        if (intervals.Length == 0) return 0;
        Array.Sort(intervals, (a, b) => a[0] - b[0]);

        // .NET 6+ built-in min-heap
        var heap = new PriorityQueue<int, int>();  // stores end times, priority = end time

        foreach (var interval in intervals)
        {
            // If earliest-ending room is free before this meeting starts, reuse it
            if (heap.Count > 0 && heap.Peek() <= interval[0])
                heap.Dequeue();
            heap.Enqueue(interval[1], interval[1]);
        }
        return heap.Count;
    }

    // Alternative: chronological ordering — no heap needed
    public int MinMeetingRoomsChronological(int[][] intervals)
    {
        int n = intervals.Length;
        if (n == 0) 
            return 0;

        var starts = intervals.Select(i => i[0]).OrderBy(x => x).ToArray();
        var ends   = intervals.Select(i => i[1]).OrderBy(x => x).ToArray();

        int rooms = 0, endPtr = 0;
        for (int i = 0; i < n; i++)
        {
            if (starts[i] < ends[endPtr]) 
                rooms++;    // need a new room
            else 
                endPtr++;                             // reuse: an earlier meeting ended
        }
        
        return rooms;
    }

    public static void Main()
    {
        var sol = new MeetingRoomsIIOptimized();
        var tc1 = new[] { new[] { 0, 30 }, new[] { 5, 10 }, new[] { 15, 20 } };
        var tc2 = new[] { new[] { 7, 10 }, new[] { 2, 4 } };
        var tc3 = new[] { new[] { 1, 5 }, new[] { 2, 6 }, new[] { 3, 7 } };

        Console.WriteLine(sol.MinMeetingRoomsHeap(tc1));           // 2
        Console.WriteLine(sol.MinMeetingRoomsHeap(tc2));           // 1
        Console.WriteLine(sol.MinMeetingRoomsHeap(tc3));           // 3
        Console.WriteLine(sol.MinMeetingRoomsChronological(tc3));  // 3
    }
}
