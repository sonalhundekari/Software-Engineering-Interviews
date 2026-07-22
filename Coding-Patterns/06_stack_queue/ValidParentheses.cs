// LeetCode 20 - Valid Parentheses
// Difficulty: Easy
// Pattern: Stack matching
//
// Time: O(n)  Space: O(n)

public class ValidParentheses
{
    public bool IsValid(string s)
    {
        var stack = new Stack<char>();
        var pairs = new Dictionary<char, char>
        {
            [')'] = '(', ['}'] = '{', [']'] = '['
        };

        foreach (char ch in s)
        {
            if (pairs.ContainsKey(ch))
            {
                if (stack.Count == 0 || stack.Peek() != pairs[ch])
                    return false;
                stack.Pop();
            }
            else
            {
                stack.Push(ch);
            }
        }
        return stack.Count == 0;
    }

    public static void Main()
    {
        var sol = new ValidParentheses();
        Console.WriteLine(sol.IsValid("()[]{}"));  // True
        Console.WriteLine(sol.IsValid("(]"));      // False
        Console.WriteLine(sol.IsValid("{[]}"));    // True
    }
}
