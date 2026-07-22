// LeetCode 102 - Binary Tree Level Order Traversal
// Difficulty: Medium
// Pattern: BFS with queue
//
// Time: O(n)  Space: O(n)

public class LevelOrderTraversal
{
    public IList<IList<int>> LevelOrder(TreeNode root)
    {
        var result = new List<IList<int>>();
        if (root == null) return result;

        var queue = new Queue<TreeNode>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            int levelSize = queue.Count;
            var level = new List<int>();
            for (int i = 0; i < levelSize; i++)
            {
                var node = queue.Dequeue();
                level.Add(node.Val);
                if (node.Left != null)  queue.Enqueue(node.Left);
                if (node.Right != null) queue.Enqueue(node.Right);
            }
            result.Add(level);
        }
        return result;
    }
}
