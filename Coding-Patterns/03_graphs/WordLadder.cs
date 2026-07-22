// LeetCode 127 - Word Ladder
// Difficulty: Hard
// Pattern: BFS shortest path with wildcard pattern map
//
// Time: O(M^2 * N)  Space: O(M^2 * N)

public class WordLadder
{
    public int LadderLength(string beginWord, string endWord, IList<string> wordList)
    {
        var wordSet = new HashSet<string>(wordList);
        if (!wordSet.Contains(endWord)) return 0;

        // Build pattern -> words map
        var patternMap = new Dictionary<string, List<string>>();
        foreach (var word in wordList)
        {
            for (int i = 0; i < word.Length; i++)
            {
                var pattern = word.Substring(0, i) + "*" + word.Substring(i + 1);
                if (!patternMap.ContainsKey(pattern)) patternMap[pattern] = new List<string>();
                patternMap[pattern].Add(word);
            }
        }

        var queue = new Queue<(string word, int len)>();
        queue.Enqueue((beginWord, 1));
        var visited = new HashSet<string> { beginWord };

        while (queue.Count > 0)
        {
            var (word, length) = queue.Dequeue();
            for (int i = 0; i < word.Length; i++)
            {
                var pattern = word.Substring(0, i) + "*" + word.Substring(i + 1);
                if (!patternMap.ContainsKey(pattern)) continue;
                foreach (var neighbor in patternMap[pattern])
                {
                    if (neighbor == endWord) return length + 1;
                    if (!visited.Contains(neighbor))
                    { visited.Add(neighbor); queue.Enqueue((neighbor, length + 1)); }
                }
            }
        }
        return 0;
    }


    public int LadderLengthOptimized(string beginWord, string endWord, IList<string> wordList)
    {
        var dict = new HashSet<string>(wordList);
        if (!dict.Contains(endWord)) return 0;

        var beginSet = new HashSet<string> { beginWord };
        var endSet = new HashSet<string> { endWord };
        var visited = new HashSet<string>();
        int length = 1;

        while (beginSet.Count > 0 && endSet.Count > 0)
        {
            // Always expand the smaller frontier — keeps search balanced
            if (beginSet.Count > endSet.Count)
                (beginSet, endSet) = (endSet, beginSet);

            var nextSet = new HashSet<string>();
            foreach (var word in beginSet)
            {
                var chars = word.ToCharArray();
                for (int i = 0; i < chars.Length; i++)
                {
                    char original = chars[i];
                    for (char c = 'a'; c <= 'z'; c++)
                    {
                        if (c == original) continue;
                        chars[i] = c;
                        var next = new string(chars);

                        // If the two frontiers touched, we're done
                        if (endSet.Contains(next)) return length + 1;

                        if (dict.Contains(next) && !visited.Contains(next))
                        {
                            nextSet.Add(next);
                            visited.Add(next);
                        }
                    }
                    chars[i] = original;
                }
            }
            beginSet = nextSet;
            length++;
        }
        return 0;
    }

    public static void Main()
    {
        var sol = new WordLadderOptimized();
        Console.WriteLine(sol.LadderLength("hit", "cog",
            new[] { "hot","dot","dog","lot","log","cog" })); // 5
        Console.WriteLine(sol.LadderLength("hit", "cog",
            new[] { "hot","dot","dog","lot","log" }));       // 0
    }
}
