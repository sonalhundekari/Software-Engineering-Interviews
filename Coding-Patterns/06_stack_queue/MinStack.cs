// LeetCode 155 - Min Stack (OPTIMIZED)
// Difficulty: Medium
// Pattern: Auxiliary min-stack — only track NEW minimums
//
// Original approach: pair every element with the current min → 2 * n memory
// Optimized:         separate min-stack that only pushes when a new min appears
//
// The key insight: we only need to know the minimum at each depth. If the top
// of the main stack isn't a new min, the previous min is still valid.
//
// Time: O(1) all ops (same)
// Space: O(n) worst case (all-decreasing sequence), but typically much less.
//        For a random sequence, expected O(log n) for the min-stack.

public class MinStackOptimized
{
    private readonly Stack<int> _stack = new();
    private readonly Stack<int> _mins = new();  // only pushed when new min appears

    public void Push(int val)
    {
        _stack.Push(val);
        // Push to min-stack only if val is a new min (or ties current min)
        if (_mins.Count == 0 || val <= _mins.Peek())
            _mins.Push(val);
    }

    public void Pop()
    {
        int val = _stack.Pop();
        if (val == _mins.Peek()) _mins.Pop();  // this element WAS the min
    }

    public int Top() => _stack.Peek();
    public int GetMin() => _mins.Peek();

    public static void Main()
    {
        var ms = new MinStackOptimized();
        ms.Push(-2); ms.Push(0); ms.Push(-3);
        Console.WriteLine(ms.GetMin()); // -3
        ms.Pop();
        Console.WriteLine(ms.Top());    // 0
        Console.WriteLine(ms.GetMin()); // -2

        // Duplicate min test — this is where naïve "push new min only if strictly less" breaks
        var m2 = new MinStackOptimized();
        m2.Push(1); m2.Push(1); m2.Push(1);
        m2.Pop(); m2.Pop();
        Console.WriteLine(m2.GetMin()); // 1 (still valid)
    }
}
