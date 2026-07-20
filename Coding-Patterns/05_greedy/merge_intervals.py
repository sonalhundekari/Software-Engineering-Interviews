# LeetCode 56 - Merge Intervals
# Difficulty: Medium
# Pattern: Sort + linear sweep
#
# Problem: Given an array of intervals, merge all overlapping intervals.
#
# Approach: Sort by start time. Walk through intervals; if current start
# <= last merged end, extend the end. Otherwise, add new interval.
#
# Time: O(n log n)  Space: O(n)

from typing import List

def merge(intervals: List[List[int]]) -> List[List[int]]:
    intervals.sort(key=lambda x: x[0])
    merged = [intervals[0]]

    for start, end in intervals[1:]:
        if start <= merged[-1][1]:
            merged[-1][1] = max(merged[-1][1], end)
        else:
            merged.append([start, end])

    return merged


# ---- Tests ----
if __name__ == "__main__":
    assert merge([[1,3],[2,6],[8,10],[15,18]]) == [[1,6],[8,10],[15,18]]
    assert merge([[1,4],[4,5]]) == [[1,5]]
    assert merge([[1,4],[2,3]]) == [[1,4]]
    print("All tests passed.")
