// LeetCode 121 - Best Time to Buy and Sell Stock
// Difficulty: Easy
// Pattern: Single pass / min tracker
//
// Problem: Given an array prices where prices[i] is the stock price on day i,
// return the maximum profit from one buy and one sell (buy before sell).
//
// Approach: Track the minimum price seen so far. At each day, the best
// profit is current_price - min_so_far. Track the max of these.
//
// Time: O(n)  Space: O(1)

public class BestTimeToBuyAndSellStock
{
    public int MaxProfit(int[] prices)
    {
        int minPrice = int.MaxValue;
        int maxProfit = 0;
        foreach (int price in prices)
        {
            minPrice = Math.Min(minPrice, price);
            maxProfit = Math.Max(maxProfit, price - minPrice);
        }
        return maxProfit;
    }

    public static void Main()
    {
        var sol = new BestTimeToBuyAndSellStock();
        Console.WriteLine(sol.MaxProfit(new[] { 7, 1, 5, 3, 6, 4 })); // 5
        Console.WriteLine(sol.MaxProfit(new[] { 7, 6, 4, 3, 1 }));    // 0
    }
}
