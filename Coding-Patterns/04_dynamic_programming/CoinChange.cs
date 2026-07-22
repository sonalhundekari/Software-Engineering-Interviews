// LeetCode 322 - Coin Change
// Difficulty: Medium
// Pattern: 1D DP (unbounded knapsack)
//
// Time: O(amount * coins.Length)  Space: O(amount)

public class CoinChange
{
    public int Solve(int[] coins, int amount)
    {
        var dp = new int[amount + 1];
        Array.Fill(dp, int.MaxValue);
        dp[0] = 0;

        for (int i = 1; i <= amount; i++)
            foreach (var coin in coins)
                if (coin <= i && dp[i - coin] != int.MaxValue)
                    dp[i] = Math.Min(dp[i], dp[i - coin] + 1);

        return dp[amount] == int.MaxValue ? -1 : dp[amount];
    }

    public static void Main()
    {
        var sol = new CoinChange();
        Console.WriteLine(sol.Solve(new[] { 1, 2, 5 }, 11)); // 3
        Console.WriteLine(sol.Solve(new[] { 2 }, 3));         // -1
    }
}
