// LeetCode 1 - Two Sum
// Difficulty: Easy
// Pattern: HashMap
//
// Problem: Given an array of integers nums and an integer target,
// return indices of the two numbers that add up to target.
//
// Approach: For each number, check if (target - num) is already in
// the dictionary. If yes, return the pair. If no, store num -> index.
//
// Time: O(n)  Space: O(n)

public class TwoSum
{
    public int[] Solve(int[] nums, int target)
    {
        var seen = new Dictionary<int, int>(); // value -> index
        for (int i = 0; i < nums.Length; i++)
        {
            int complement = target - nums[i];
            if (seen.ContainsKey(complement))
                return new[] { seen[complement], i };
            seen[nums[i]] = i;
        }
        return Array.Empty<int>();
    }

    // ---- Tests ----
    public static void Main()
    {
        var sol = new TwoSum();
        Console.WriteLine(string.Join(",", sol.Solve(new[] { 2, 7, 11, 15 }, 9))); // 0,1
        Console.WriteLine(string.Join(",", sol.Solve(new[] { 3, 2, 4 }, 6)));       // 1,2
        Console.WriteLine(string.Join(",", sol.Solve(new[] { 3, 3 }, 6)));           // 0,1
    }
}
