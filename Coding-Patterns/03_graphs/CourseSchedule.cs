// LeetCode 207 - Course Schedule
// Difficulty: Medium
// Pattern: Topological sort (Kahn's BFS)
//
// Time: O(V + E)  Space: O(V + E)

public class CourseSchedule
{
    public bool CanFinish(int numCourses, int[][] prerequisites)
    {
        var inDegree = new int[numCourses];
        var adj = new List<int>[numCourses];
        for (int i = 0; i < numCourses; i++) adj[i] = new List<int>();

        foreach (var p in prerequisites)
        {
            adj[p[1]].Add(p[0]);
            inDegree[p[0]]++;
        }

        var queue = new Queue<int>();
        for (int i = 0; i < numCourses; i++)
            if (inDegree[i] == 0) queue.Enqueue(i);

        int completed = 0;
        while (queue.Count > 0)
        {
            int course = queue.Dequeue();
            completed++;
            foreach (var next in adj[course])
                if (--inDegree[next] == 0) queue.Enqueue(next);
        }
        return completed == numCourses;
    }
}
