// LeetCode 33 - Search in Rotated Sorted Array
// Difficulty: Medium
// Pattern: Binary search with rotation check
//
// Time: O(log n)  Space: O(1)

public class SearchRotatedSortedArray
{
    public int Search(int[] nums, int target)
    {
        int lo = 0, hi = nums.Length - 1;

        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (nums[mid] == target) return mid;

            // Left half is sorted
            if (nums[lo] <= nums[mid])
            {
                if (nums[lo] <= target && target < nums[mid])
                    hi = mid - 1;
                else
                    lo = mid + 1;
            }
            // Right half is sorted
            else
            {
                if (nums[mid] < target && target <= nums[hi])
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }
        }
        return -1;
    }

    public static void Main()
    {
        var sol = new SearchRotatedSortedArray();
        Console.WriteLine(sol.Search(new[] { 4,5,6,7,0,1,2 }, 0)); // 4
        Console.WriteLine(sol.Search(new[] { 4,5,6,7,0,1,2 }, 3)); // -1
    }
}
