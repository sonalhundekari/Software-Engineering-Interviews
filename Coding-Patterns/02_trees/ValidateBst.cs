// LeetCode 98 - Validate Binary Search Tree
// Difficulty: Medium
// Pattern: DFS with min/max bounds
//
// Approach: Pass valid range [minVal, maxVal] down the recursion.
// Each node must be strictly between its bounds.
//
// Time: O(n)  Space: O(h)

public class TreeNode
{
    public int Val;
    public TreeNode Left, Right;
    public TreeNode(int val = 0, TreeNode left = null, TreeNode right = null)
    { Val = val; Left = left; Right = right; }
}

public class ValidateBST
{
    public bool IsValidBST(TreeNode root)
        => Validate(root, long.MinValue, long.MaxValue);

    private bool Validate(TreeNode node, long minVal, long maxVal)
    {
        if (node == null) return true;
        if (node.Val <= minVal || node.Val >= maxVal) return false;
        return Validate(node.Left, minVal, node.Val) &&
               Validate(node.Right, node.Val, maxVal);
    }

    public bool IsValidBSTOptimized(TreeNode root)
    {
        var stack = new Stack<TreeNode>();
        var current = root;
        long prev = long.MinValue;

        while (current != null || stack.Count > 0)
        {
            // Walk down all the way left
            while (current != null)
            {
                stack.Push(current);
                current = current.Left;
            }

            // Visit the smallest unvisited node
            current = stack.Pop();
            if (current.Val <= prev) return false;
            prev = current.Val;

            // Move to right subtree
            current = current.Right;
        }
        return true;
    }

    public static void Main()
    {
        var sol = new ValidateBST();
        var valid = new TreeNode(2, new TreeNode(1), new TreeNode(3));
        Console.WriteLine(sol.IsValidBST(valid)); // True

        var invalid = new TreeNode(5, new TreeNode(1),
            new TreeNode(4, new TreeNode(3), new TreeNode(6)));
        Console.WriteLine(sol.IsValidBST(invalid)); // False
    }
}