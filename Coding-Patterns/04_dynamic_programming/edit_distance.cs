// LeetCode 72 - Edit Distance
// Difficulty: Medium
// Pattern: 2D DP
//
// Time: O(m * n)  Space: O(m * n)

public class EditDistance
{
    public int MinDistance(string word1, string word2)
    {
        int m = word1.Length, n = word2.Length;
        var dp = new int[m + 1, n + 1];
        for (int i = 0; i <= m; i++) dp[i, 0] = i;
        for (int j = 0; j <= n; j++) dp[0, j] = j;

        for (int i = 1; i <= m; i++)
            for (int j = 1; j <= n; j++)
                if (word1[i - 1] == word2[j - 1])
                    dp[i, j] = dp[i - 1, j - 1];
                else
                    dp[i, j] = 1 + Math.Min(dp[i - 1, j],
                                   Math.Min(dp[i, j - 1], dp[i - 1, j - 1]));

        return dp[m, n];
    }
    public int MinDistanceOptimized(string word1, string word2)
    {
        // Ensure word2 is the shorter one → smaller inner arrays
        if (word1.Length < word2.Length)
            (word1, word2) = (word2, word1);

        int m = word1.Length, n = word2.Length;
        var prev = new int[n + 1];
        var curr = new int[n + 1];

        for (int j = 0; j <= n; j++) prev[j] = j;

        for (int i = 1; i <= m; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= n; j++)
            {
                if (word1[i - 1] == word2[j - 1])
                    curr[j] = prev[j - 1];
                else
                    curr[j] = 1 + Math.Min(prev[j],
                                  Math.Min(curr[j - 1], prev[j - 1]));
            }
            (prev, curr) = (curr, prev);  // swap references; no allocation
        }
        return prev[n];
    }

    public static void Main()
    {
        var sol = new EditDistanceOptimized();
        Console.WriteLine(sol.MinDistance("horse", "ros"));           // 3
        Console.WriteLine(sol.MinDistance("intention", "execution")); // 5
    }
}
