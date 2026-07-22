// LeetCode 739 - Daily Temperatures
// Difficulty: Medium
// Pattern: Monotonic stack (decreasing)
//
// Time: O(n)  Space: O(n)

public class DailyTemperatures
{
    public int[] Solve(int[] temperatures)
    {
        int n = temperatures.Length;
        var answer = new int[n];
        var stack = new Stack<int>(); // indices

        for (int i = 0; i < n; i++)
        {
            while (stack.Count > 0 && temperatures[stack.Peek()] < temperatures[i])
            {
                int idx = stack.Pop();
                answer[idx] = i - idx;
            }
            stack.Push(i);
        }
        return answer;
    }

    public static void Main()
    {
        var sol = new DailyTemperatures();
        Console.WriteLine(string.Join(",",
            sol.Solve(new[] { 73,74,75,71,69,72,76,73 }))); // 1,1,4,2,1,1,0,0
    }
}
