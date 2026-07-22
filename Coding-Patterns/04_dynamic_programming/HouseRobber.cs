// LeetCode 198 - House Robber
// Difficulty: Medium
// Pattern: 1D DP (space-optimized)
//
// Time: O(n)  Space: O(1)

public class HouseRobber
{
    public int Rob(int[] nums)
    {
        int prev2 = 0, prev1 = 0;
        foreach (var num in nums)
        {
            int curr = Math.Max(prev1, prev2 + num);
            prev2 = prev1;
            prev1 = curr;
        }
        return prev1;
    }
}
