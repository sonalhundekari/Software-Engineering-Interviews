// LeetCode 76 - Minimum Window Substring
// Difficulty: Hard
// Pattern: Sliding window + frequency map
//
// Problem: Given strings s and t, return the minimum window substring
// of s that contains all characters in t.
//
// Time: O(|s| + |t|)  Space: O(|s| + |t|)

public class MinimumWindowSubstring
{
    public string MinWindow(string s, string t)
    {
        if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(t)) return "";

        var need = new Dictionary<char, int>();
        foreach (var c in t)
            need[c] = need.GetValueOrDefault(c, 0) + 1;

        int required = need.Count, formed = 0;
        var window = new Dictionary<char, int>();
        int left = 0, minLen = int.MaxValue, minLeft = 0;

        for (int right = 0; right < s.Length; right++)
        {
            char c = s[right];
            window[c] = window.GetValueOrDefault(c, 0) + 1;
            if (need.ContainsKey(c) && window[c] == need[c])
                formed++;

            while (formed == required)
            {
                if (right - left + 1 < minLen)
                {
                    minLen = right - left + 1;
                    minLeft = left;
                }
                char lc = s[left];
                window[lc]--;
                if (need.ContainsKey(lc) && window[lc] < need[lc])
                    formed--;
                left++;
            }
        }
        return minLen == int.MaxValue ? "" : s.Substring(minLeft, minLen);
    }

    public static void Main()
    {
        var sol = new MinimumWindowSubstring();
        Console.WriteLine(sol.MinWindow("ADOBECODEBANC", "ABC")); // BANC
        Console.WriteLine(sol.MinWindow("a", "a"));               // a
        Console.WriteLine(sol.MinWindow("a", "aa"));              // ""
    }
}
