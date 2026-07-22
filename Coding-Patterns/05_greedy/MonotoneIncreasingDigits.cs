/*
Both versions use the same greedy digit-decrement logic:

Extract the digits of n.
Scan right to left. Whenever a digit is greater than the digit to its right (a "descent"), decrement the left digit by 1, 
and record that position as the start of the "9-fill zone."

Right-to-left scanning correctly handles cascades — if decrementing a digit creates a new violation with the digit before it, 
the next loop iteration catches it (e.g. 332 → 299, 1101 → 1099).
Everything from the marked index onward becomes 9, since those digits are now free to maximize.

The two functions differ only in how they extract/reassemble digits, not in the core algorithm.
Complexity
Both versions:

Time: O(d), where d = number of digits in n (at most 10 for a 32-bit int). Equivalently O(log n).
Space: O(d), also O(log n) — or O(1) if you count the fixed 10-digit array as constant since int has a bounded digit count.

This is optimal: every digit must be inspected at least once, so O(d) is a hard lower bound.
*/
using System;

namespace MonotoneIncreasingDigits
{
    class Program
    {
        /// <summary>
        /// Returns the largest integer <= n whose digits are monotonically
        /// non-decreasing (left to right), e.g. 1234, 1119, 288999.
        /// Straightforward version using string/char array conversion.
        /// </summary>
        public static int MonotoneIncreasingDigits(int n)
        {
            if (n < 10) 
                return n; // single-digit numbers are trivially monotone

            char[] digits = n.ToString().ToCharArray();
            int markIdx = digits.Length; // index from which everything becomes '9'

            // Scan right-to-left to correctly handle cascading decrements
            // (e.g. "332" needs both the 2nd AND 1st digit decremented).
            for (int i = digits.Length - 1; i > 0; i--)
            {
                if (digits[i - 1] > digits[i])
                {
                    digits[i - 1]--;
                    markIdx = i;
                }
            }

            for (int i = markIdx; i < digits.Length; i++)
            {
                digits[i] = '9';
            }

            return int.Parse(new string(digits));
        }

        /// <summary>
        /// Optimized version: avoids string/char allocation and ToString()/Parse()
        /// overhead by working directly with digit arithmetic. Uses a small
        /// fixed-size int array (max 10 digits for a 32-bit int) instead of
        /// heap-allocated strings.
        /// </summary>
        public static int MonotoneIncreasingDigitsOptimized(int n)
        {
            if (n < 10) 
                return n;

            int[] digits = new int[10]; // max digits in a 32-bit int
            int len = 0;
            int temp = n;
            while (temp > 0)
            {
                digits[len++] = temp % 10;
                temp /= 10;
            }
            Array.Reverse(digits, 0, len); // now digits[0..len) is most-significant-first

            int markIdx = len;
            for (int i = len - 1; i > 0; i--)
            {
                if (digits[i - 1] > digits[i])
                {
                    digits[i - 1]--;
                    markIdx = i;
                }
            }

            int result = 0;
            for (int i = 0; i < len; i++)
            {
                int d = (i >= markIdx) ? 9 : digits[i];
                result = result * 10 + d;
            }
            return result;
        }

        static void Main(string[] args)
        {
            int[] testCases = { 10, 1234, 332, 100, 9, 0, 999, 1000000000, 111219 };

            Console.WriteLine("Monotone Increasing Digits (LeetCode 738)");
            Console.WriteLine("==========================================");

            foreach (int n in testCases)
            {
                int result1 = MonotoneIncreasingDigits(n);
                int result2 = MonotoneIncreasingDigitsOptimized(n);
                string match = result1 == result2 ? "OK" : "MISMATCH!";
                Console.WriteLine($"n = {n,12} -> basic: {result1,-12} optimized: {result2,-12} [{match}]");
            }

            // Interactive mode
            Console.WriteLine("\nEnter a number to test (or press Enter to quit):");
            string? input;
            while (!string.IsNullOrWhiteSpace(input = Console.ReadLine()))
            {
                if (int.TryParse(input, out int value) && value >= 0)
                {
                    Console.WriteLine($"Basic:     {MonotoneIncreasingDigits(value)}");
                    Console.WriteLine($"Optimized: {MonotoneIncreasingDigitsOptimized(value)}");
                }
                else
                {
                    Console.WriteLine("Please enter a valid non-negative integer.");
                }
                Console.WriteLine("Enter another number (or press Enter to quit):");
            }
        }
    }
}