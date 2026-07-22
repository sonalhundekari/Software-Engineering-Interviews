/*
Number of Islands
Given a 2D grid map of '1's (land) and '0's (water), count the number of islands. An island is surrounded by water and is formed by connecting adjacent
lands horizontally or vertically. You may assume all four edges of the grid are all surrounded by water.

Time Complexity: O(m*n) - We traverse the entire grid once.
Space Complexity: O(m*n) - In the worst case, we may have to store all the cells in the stack during DFS.

*/

public class NumOfIslands {
    int nr;
    int nc;

    public int NumIslands(char[][] grid)
    {
        nr = grid.Length;
        nc = grid[0].Length;

        int totalIsland = 0;

        for (int i = 0; i < nr; i++)
        {
            for (int j = 0; j < nc; j++)
            {
                if (grid[i][j] == '1')
                {
                    totalIsland++;
                    findIsland(grid, i, j);
                }
            }
        }
        return totalIsland;
    }

    public void findIsland(char[][] grid, int i, int j)
    {
        if (isValidCell(grid, i, j))
        {
            grid[i][j] = '0';
            findIsland(grid, i + 1, j);
            findIsland(grid, i - 1, j);
            findIsland(grid, i, j + 1);
            findIsland(grid, i, j - 1);
        }
    }

    private bool isValidCell(char[][] grid, int i, int j)
    {
        return i >= 0 && i < nr && j >= 0 && j < nc && grid[i][j] == '1';
    }

    public static void Main()
    {
        var numOfIslands = new NumOfIslands();
        char[][] grid = new char[][]
        {
            new char[] { '1', '1', '0', '0', '0' },
            new char[] { '1', '1', '0', '0', '0' },
            new char[] { '0', '0', '1', '0', '0' },
            new char[] { '0', '0', '0', '1', '1' }
        };

        Console.WriteLine(numOfIslands.NumIslands(grid)); // Output: 3
    }
}