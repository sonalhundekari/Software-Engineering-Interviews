// LeetCode 435 - Non-overlapping Intervals
// Difficulty: Medium
// Pattern: Greedy (sort by end time)
//
// Time: O(n log n)  Space: O(1)

public class NonOverlappingIntervals
{
    public int EraseOverlapIntervals(int[][] intervals)
    {
        // Step 1: Sort intervals based on end times
        Array.Sort(intervals, (a, b) => a[1] - b[1]);
        int removed = 0, prevEnd = int.MinValue;

        // Step 2: Iterate through intervals checking for overlaps
        foreach (var interval in intervals)
        {
            // If the current interval starts after or at the end of the previous interval, update prevEnd
            if (interval[0] >= prevEnd) 
                prevEnd = interval[1];
            // If there is an overlap, increment the removed count
            else 
                removed++;
        }
        return removed;
    }

    public static void Main()
    {
        var sol = new NonOverlappingIntervals();
        int[][] intervals = new int[][] {
            new int[] {1, 2},
            new int[] {2, 3},
            new int[] {3, 4},
            new int[] {1, 3}
        };
        int result = sol.EraseOverlapIntervals(intervals);
        Console.WriteLine($"Minimum number of intervals to remove: {result}");
    }
}
