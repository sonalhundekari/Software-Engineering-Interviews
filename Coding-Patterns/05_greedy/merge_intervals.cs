// LeetCode 56 - Merge Intervals
// Difficulty: Medium
// Pattern: Sort + linear sweep
//
// Time: O(n log n)  Space: O(n)

public class MergeIntervals {
    public int[][] Merge(int[][] intervals) {
        Array.Sort(intervals, (a, b) => a[0] - b[0]);
        LinkedList<int[]> merged = new LinkedList<int[]>();
        foreach (int[] interval in intervals) {
            // if the list of merged intervals is empty or if the current
            // interval does not overlap with the previous, append it
            if (merged.Count == 0 || merged.Last.Value[1] < interval[0]) {
                merged.AddLast(interval);
            }
            // otherwise, there is overlap, so we merge the current and previous
            // intervals
            else {
                merged.Last.Value[1] =
                    Math.Max(merged.Last.Value[1], interval[1]);
            }
        }

        return merged.ToArray();
    }

    public static void Main() {
        var sol = new MergeIntervals();
        int[][] intervals = new int[][] {
            new int[] {1, 3},
            new int[] {2, 6},
            new int[] {8, 10},
            new int[] {15, 18}
        };
        var merged = sol.Merge(intervals);
        Console.WriteLine("Merged intervals:");
        foreach (var interval in merged) {
            Console.WriteLine($"[{interval[0]}, {interval[1]}]");
        }
    }
}
