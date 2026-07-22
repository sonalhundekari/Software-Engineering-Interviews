// LeetCode 133 - Clone Graph
// Difficulty: Medium
// Pattern: DFS + HashMap
//
// Time: O(V + E)  Space: O(V)

public class GraphNode
{
    public int val;
    public IList<GraphNode> neighbors;
    public GraphNode(int v = 0) { val = v; neighbors = new List<GraphNode>(); }
}

public class CloneGraph
{
    private Dictionary<GraphNode, GraphNode> _visited = new();

    public GraphNode CloneGraphNode(GraphNode node)
    {
        if (node == null) return null;
        if (_visited.ContainsKey(node)) return _visited[node];
        var clone = new GraphNode(node.val);
        _visited[node] = clone;
        foreach (var neighbor in node.neighbors)
            clone.neighbors.Add(CloneGraphNode(neighbor));
        return clone;
    }
}
