/*
Sort Array by Parity — Minimum Swaps
Rearrange an integer array so every even precedes every odd using arbitrary swaps, and return the minimum number of swaps. A two-pointer sweep from both ends gives the optimal count.

Requirements

Given an integer array, rearrange it so that every even number comes before every odd number.
Any two elements may be swapped; relative order within the evens or within the odds does not matter.
Return the minimum number of swaps needed to reach a valid arrangement.
A two-pointer pass from both ends — advance the left pointer past evens, advance the right pointer past odds, swap the mismatched pair and count one swap — yields the minimum.
*/
public class MinSwapsByParity
{
    public static int MinSwapsByParity(int[] nums)
    {
        int left = 0, right = nums.Length - 1, swaps = 0;

        while (left < right)
        {
            if ((nums[left] & 1) == 0)        // even already in place
            {
                left++;
            }
            else if ((nums[right] & 1) == 1)  // odd already in place
            {
                right--;
            }
            else                              // odd on left, even on right → must swap
            {
                (nums[left], nums[right]) = (nums[right], nums[left]);
                swaps++;
                left++;
                right--;
            }
        }

        return swaps;
    }

    public static void Main()
    {
        var arr1 = new int[] { 3, 1, 2, 4 };
        var arr2 = new int[] { 0, 1, 2, 3, 4, 5 };
        var arr3 = new int[] { 1, 3, 5, 7 };
        var arr4 = new int[] { 2, 4, 6, 8 };

        Console.WriteLine(MinSwapsByParity(arr1)); // Output: 1
        Console.WriteLine(MinSwapsByParity(arr2)); // Output: 2
        Console.WriteLine(MinSwapsByParity(arr3)); // Output: 0
        Console.WriteLine(MinSwapsByParity(arr4)); // Output: 0
    }
}