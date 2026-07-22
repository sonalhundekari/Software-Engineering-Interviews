// LeetCode 84 - Largest Rectangle in Histogram
// Difficulty: Hard
// Pattern: Monotonic stack (increasing)
//
// Time: O(n)  Space: O(n)

public class LargestRectangleHistogram
{
    public int LargestRectangleArea(int[] heights)
    {
        var stack = new Stack<int>(); // indices
        int maxArea = 0;
        // Append sentinel 0 to flush remaining stack
        var h = heights.Append(0).ToArray();

        for (int i = 0; i < h.Length; i++)
        {
            while (stack.Count > 0 && h[stack.Peek()] > h[i])
            {
                int height = h[stack.Pop()];
                int width = stack.Count == 0 ? i : i - stack.Peek() - 1;
                maxArea = Math.Max(maxArea, height * width);
            }
            stack.Push(i);
        }
        return maxArea;
    }

    public static void Main()
    {
        var sol = new LargestRectangleHistogram();
        Console.WriteLine(sol.LargestRectangleArea(new[] { 2,1,5,6,2,3 })); // 10
        Console.WriteLine(sol.LargestRectangleArea(new[] { 2,4 }));          // 4
    }
}
