# LeetCode 435 - Non-overlapping Intervals
# Difficulty: Medium
# Pattern: Greedy
#
# Problem: Given an array of intervals, return the minimum number of
# intervals to remove to make the rest non-overlapping.
#
# Approach: Sort by end time. Greedily keep intervals with the earliest
# end — this leaves maximum room for future intervals.
# Count intervals we must remove (those that overlap with the last kept).
#
# Time: O(n log n)  Space: O(1)

from typing import List

def erase_overlap_intervals(intervals: List[List[int]]) -> int:
    intervals.sort(key=lambda x: x[1])  # sort by end time
    removed = 0
    prev_end = float('-inf')

    for start, end in intervals:
        if start >= prev_end:
            prev_end = end  # keep this interval
        else:
            removed += 1    # overlaps — remove it

    return removed


# ---- Tests ----
if __name__ == "__main__":
    assert erase_overlap_intervals([[1,2],[2,3],[3,4],[1,3]]) == 1
    assert erase_overlap_intervals([[1,2],[1,2],[1,2]]) == 2
    assert erase_overlap_intervals([[1,2],[2,3]]) == 0
    print("All tests passed.")
