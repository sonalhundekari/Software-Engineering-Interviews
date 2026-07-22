// LeetCode 49 - Group Anagrams
// Difficulty: Medium
// Pattern: HashMap with counting key
//
// Original approach: sort each word → O(k log k) per word
// Optimized:         count 26 chars → O(k) per word
//
// The key insight: two anagrams have identical character frequencies.
// A 26-int fingerprint uniquely identifies an anagram class,
// and building it is a single linear pass — no sort needed.
//
// Time: O(n * k)  vs O(n * k log k)   (n = # words, k = avg word length)
// Space: O(n * k)
//
// Why this matters:
// - For long words (k=100), sorting is ~7x slower per word
// - Sorting allocates a new char[]; counting uses a fixed 26-int buffer
// - Cache-friendly: sequential scan of the string vs. sort's random access

public class GroupAnagrams
{
    public IList<IList<string>> Solve(string[] strs)
    {
        var groups = new Dictionary<string, List<string>>();
        Span<int> counts = stackalloc int[26];  // no heap allocation per word

        foreach (var word in strs)
        {
            counts.Clear();
            foreach (var c in word)
                counts[c - 'a']++;

            // Build a compact key from the count array.
            // Using a delimited string is simple and unambiguous.
            var key = string.Create(52, counts.ToArray(), (span, cs) =>
            {
                for (int i = 0; i < 26; i++)
                {
                    span[i * 2] = (char)('a' + i);
                    span[i * 2 + 1] = (char)cs[i];
                }
            });

            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<string>();
                groups[key] = list;
            }
            list.Add(word);
        }
        return groups.Values.Cast<IList<string>>().ToList();
    }

    // Alternative: even simpler key using a tuple-like string
    public IList<IList<string>> SolveSimple(string[] strs)
    {
        var groups = new Dictionary<string, List<string>>();
        foreach (var word in strs)
        {
            var counts = new int[26];
            foreach (var c in word) 
                counts[c - 'a']++;
            var key = string.Join(",", counts);  // e.g. "1,0,0,1,2,..."
            if (!groups.TryGetValue(key, out var list))
                groups[key] = list = new List<string>();
            list.Add(word);
        }
        return groups.Values.Cast<IList<string>>().ToList();
    }

    public static void Main()
    {
        var sol = new GroupAnagrams();
        var result = sol.Solve(new[] { "eat", "tea", "tan", "ate", "nat", "bat" });
        foreach (var g in result) Console.WriteLine(string.Join(", ", g));
    }
}



