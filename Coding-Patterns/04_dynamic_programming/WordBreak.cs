// LeetCode 139 - Word Break
// Difficulty: Medium
// Pattern: 1D DP + HashSet lookup
//
// Time: O(n^2)  Space: O(n)
// Original approach: for each i, try all j < i, check s.Substring(j, i-j) in HashSet
//                    → O(n^2) substrings created + hashed
// Optimized:         Trie of dictionary words; for each i, walk the trie forward
//                    → no substring allocations; early termination
//
// Time: O(n * L) where L = max word length (typically << n^2)
// Space: O(total chars in dictionary)
public class WordBreak
{
    public bool CanBreak(string s, IList<string> wordDict)
    {
        var wordSet = new HashSet<string>(wordDict);
        int n = s.Length;
        var dp = new bool[n + 1];
        dp[0] = true;

        for (int i = 1; i <= n; i++)
            for (int j = 0; j < i; j++)
                if (dp[j] && wordSet.Contains(s.Substring(j, i - j)))
                { dp[i] = true; break; }

        return dp[n];
    }
    private class TrieNode
    {
        public TrieNode[] Children = new TrieNode[26];
        public bool IsWord;
    }

    public bool CanBreak(string s, IList<string> wordDict)
    {
        // Build trie
        var root = new TrieNode();
        foreach (var word in wordDict)
        {
            var node = root;
            foreach (var c in word)
            {
                int idx = c - 'a';
                node.Children[idx] ??= new TrieNode();
                node = node.Children[idx];
            }
            node.IsWord = true;
        }

        int n = s.Length;
        var dp = new bool[n + 1];
        dp[0] = true;

        for (int i = 0; i < n; i++)
        {
            if (!dp[i]) continue;

            // Walk the trie starting at s[i]
            var node = root;
            for (int j = i; j < n; j++)
            {
                int idx = s[j] - 'a';
                if (node.Children[idx] == null) break;  // no word extends this prefix
                node = node.Children[idx];
                if (node.IsWord) dp[j + 1] = true;
            }
        }
        return dp[n];
    }

    public static void Main()
    {
        var sol = new WordBreakOptimized();
        Console.WriteLine(sol.CanBreak("leetcode", new[] { "leet", "code" }));         // True
        Console.WriteLine(sol.CanBreak("applepenapple", new[] { "apple", "pen" }));     // True
        Console.WriteLine(sol.CanBreak("catsandog",
            new[] { "cats", "dog", "sand", "and", "cat" }));                             // False
    }
}
