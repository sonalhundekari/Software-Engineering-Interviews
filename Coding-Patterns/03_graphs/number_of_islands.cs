// LeetCode 200 - Number of Islands
// Pattern: DFS flood fill
//
/*
Walkthrough: scan every cell; whenever you hit an unvisited '1', that's a new island — increment the count and DFS outward in 4 directions, flipping each visited land cell to '0' so it's never counted again. No separate visited array needed since we mutate the grid in place (call out that tradeoff if the interviewer asks — if the input can't be mutated, use a bool[,] visited array instead, which just adds O(m·n) extra space).
Complexity:

Time: O(m·n) — every cell is visited exactly once. Even though DFS is called from multiple neighbors, the != '1' check makes each cell's recursive body execute only once total.
Space: O(m·n) worst case — no auxiliary data structure, but the recursion call stack can grow to the size of the entire grid if it's one giant snake-shaped island (e.g., a single row or a spiral). 
Best/average case is O(min(m,n)) for a more square-ish island. 

If asked for a stack-safe alternative, mention converting to an explicit stack-based DFS or BFS with a queue, both still O(m·n) time and space.
*/

public class NumberOfIslands
{
    public int NumIslands(char[][] grid)
    {
        if (grid == null || grid.Length == 0 || grid[0].Length == 0)
            return 0;

        int rows = grid.Length;
        int cols = grid[0].Length;
        int count = 0;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
                if (grid[r][c] == '1') 
                { 
                    Dfs(grid, r, c, rows, cols); 
                    count++; 
                }
        }
        return count;
    }

    private void Dfs(char[][] grid, int r, int c, int rows, int cols)
    {
        // out of bounds or water/visited
        if (r < 0 || r >= rows || c < 0 || c >= cols || grid[r][c] != '1')
            return;

        // mark visited by sinking the land
        grid[r][c] = '0';
        Dfs(grid, r + 1, c, rows, cols);
        Dfs(grid, r - 1, c, rows, cols);
        Dfs(grid, r, c + 1, rows, cols);
        Dfs(grid, r, c - 1, rows, cols);
    }
}
