/*
Given n non-negative integers representing an elevation map where the width of each bar is 1, 
compute how much water it can trap after raining.

Time Complexity: O(n) - We traverse the array once with two pointers.
Space Complexity: O(1) - We use a constant amount of space.
*/

public class TrappingRainWater {
    public int Trap(int[] height) {
        int left = 0, right = height.Length - 1;
        int result = 0;
        int left_max = 0, right_max = 0;

        while(left < right)
        {
            if(height[left] < height[right])
            {
                left_max = Math.Max(left_max, height[left]);
                result += left_max - height[left];
                ++left;
            }
            else
            {
                right_max = Math.Max(right_max, height[right]);
                result += right_max - height[right];
                --right;
            }
        }

        return result;
    }

    public static void Main() {
        var trappingRainWater = new TrappingRainWater();
        int[] height1 = {0,1,0,2,1,0,1,3,2,1,2,1};
        int[] height2 = {4,2,0,3,2,5};

        Console.WriteLine(trappingRainWater.Trap(height1)); // Output: 6
        Console.WriteLine(trappingRainWater.Trap(height2)); // Output: 9
    }
}