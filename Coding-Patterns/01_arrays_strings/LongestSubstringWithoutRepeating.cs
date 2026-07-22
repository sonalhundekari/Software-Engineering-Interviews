// LeetCode 3 - Longest Substring Without Repeating Characters
// Difficulty: Medium
// Pattern: Sliding window + HashSet
//
// Problem: Find the length of the longest substring without repeating characters.
//
// Approach: Sliding window [left, right]. Expand right, shrink left on duplicates.
//
// Time: O(n)  Space: O(min(n, alphabet_size))

public class LongestSubstringWithoutRepeating
{
    public int LengthOfLongestSubstring(string s)
    {
        var charSet = new HashSet<char>();
        int left = 0, maxLen = 0;

        for (int right = 0; right < s.Length; right++)
        {
            while (charSet.Contains(s[right]))
            {
                charSet.Remove(s[left]);
                left++;
            }
            charSet.Add(s[right]);
            maxLen = Math.Max(maxLen, right - left + 1);
        }
        return maxLen;
    }

    // Original approach: HashSet + shrink left one char at a time on collision
    // Optimized:         Dictionary<char,int> + jump left directly past duplicate
    //
    // The key insight: when we see a repeated char at index i, we don't need to
    // shrink left one step at a time — we can jump left directly to (lastIndex + 1).
    // One pass instead of amortized-two-passes.
    //
    // Time: O(n) — same big-O, but only one visit per char (not 2n in worst case)
    // Space: O(min(n, alphabet))
    public int LengthOfLongestSubstringOptimized(string s)
    {
        // ASCII fast path: array indexed by char code
        Span<int> lastIndex = stackalloc int[128];
        for (int i = 0; i < 128; i++) 
            lastIndex[i] = -1;

        int left = 0, maxLen = 0;
        for (int right = 0; right < s.Length; right++)
        {
            char c = s[right];
            if (lastIndex[c] >= left)
            {
                // Jump left past the previous occurrence — no shrinking loop.
                left = lastIndex[c] + 1;
            }
            lastIndex[c] = right;
            maxLen = Math.Max(maxLen, right - left + 1);
        }
        return maxLen;
    }

    public static void Main()
    {
        var sol = new LongestSubstringWithoutRepeating();
        Console.WriteLine(sol.LengthOfLongestSubstring("abcabcbb")); // 3
        Console.WriteLine(sol.LengthOfLongestSubstring("bbbbb"));    // 1
        Console.WriteLine(sol.LengthOfLongestSubstring("pwwkew"));   // 3
    }
}
