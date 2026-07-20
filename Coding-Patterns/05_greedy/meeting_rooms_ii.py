# LeetCode 253 - Meeting Rooms II
# Difficulty: Medium
# Pattern: Min-heap of end times
#
# Problem: Given an array of meeting time intervals [start, end],
# return the minimum number of conference rooms required.
#
# Approach: Sort by start time. Use a min-heap of end times.
# For each meeting, if its start >= heap's min end, reuse that room (pop).
# Always push current end. Heap size = rooms in use.
#
# Time: O(n log n)  Space: O(n)

from typing import List
import heapq

def min_meeting_rooms(intervals: List[List[int]]) -> int:
    if not intervals:
        return 0

    intervals.sort(key=lambda x: x[0])
    heap = []  # min-heap of end times

    for start, end in intervals:
        if heap and heap[0] <= start:
            heapq.heapreplace(heap, end)  # reuse room
        else:
            heapq.heappush(heap, end)     # new room needed

    return len(heap)


# ---- Tests ----
if __name__ == "__main__":
    assert min_meeting_rooms([[0,30],[5,10],[15,20]]) == 2
    assert min_meeting_rooms([[7,10],[2,4]]) == 1
    assert min_meeting_rooms([[1,5],[2,6],[3,7]]) == 3
    print("All tests passed.")
