// LeetCode 153 - Find Minimum in Rotated Sorted Array
// Difficulty: Medium
// Pattern: Binary search on pivot
//
// Time: O(log n)  Space: O(1)

public class FindMinimumRotatedArray
{
    public int FindMin(int[] nums)
    {
        int lo = 0, hi = nums.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (nums[mid] > nums[hi])
                lo = mid + 1;
            else
                hi = mid;
        }
        return nums[lo];
    }

    public static void Main()
    {
        var sol = new FindMinimumRotatedArray();
        Console.WriteLine(sol.FindMin(new[] { 3,4,5,1,2 }));       // 1
        Console.WriteLine(sol.FindMin(new[] { 4,5,6,7,0,1,2 }));   // 0
        Console.WriteLine(sol.FindMin(new[] { 11,13,15,17 }));      // 11
    }
}
