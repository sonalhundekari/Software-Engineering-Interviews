/*
You are given an integer array height of length n. There are n vertical lines drawn such that the two endpoints of the ith line are (i, 0) and (i, height[i]).

Find two lines that together with the x-axis form a container, such that the container contains the most water.

Return the maximum amount of water a container can store.

Example 1:
Input: height = [1,8,6,2,5,4,8,3,7]
Output: 49
Explanation: The above vertical lines are represented by array [1,8,6,2,5,4,8,3,7]. In this case, the max area of water (blue section) the container can contain is 49.
Example 2:

Input: height = [1,1]
Output: 1

Time Complexity: O(n) - We traverse the array once with two pointers.
Space Complexity: O(1) - We use a constant amount of space.
*/

public class ContainerWithMostWater {
    public int MaxArea(int[] height) {
        int maxArea = 0;
        int left = 0;
        int right = height.Length - 1;

        while (left < right) {
            int width = right - left;
            
            maxArea = Math.Max(maxArea,
                               Math.Min(height[left], height[right]) * width);

            if (height[left] <= height[right]) 
            {
                left++;
            } 
            else 
            {
                right--;
            }
        }

        return maxArea;
    }

    public static void Main() {
        var container = new ContainerWithMostWater();
        int[] height1 = {1,8,6,2,5,4,8,3,7};
        int[] height2 = {1,1};

        Console.WriteLine(container.MaxArea(height1)); // Output: 49
        Console.WriteLine(container.MaxArea(height2)); // Output: 1
    }
}