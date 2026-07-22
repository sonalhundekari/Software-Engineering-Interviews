// LeetCode 236 - Lowest Common Ancestor of a Binary Tree
// Difficulty: Medium
// Pattern: DFS post-order
//
// Approach: Post-order DFS. Return node if it matches p or q.
// If both left and right are non-null, current node is LCA.
//
// Time: O(n)  Space: O(h)

public class LowestCommonAncestor
{
    public TreeNode LCA(TreeNode root, TreeNode p, TreeNode q)
    {
        if (root == null || root == p || root == q) return root;
        var left = LCA(root.Left, p, q);
        var right = LCA(root.Right, p, q);
        if (left != null && right != null) return root;
        return left ?? right;
    }
}
