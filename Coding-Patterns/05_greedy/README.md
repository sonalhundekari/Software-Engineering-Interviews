# Pattern: Intervals & Greedy

## Key Techniques
- **Sort by start time** — enables linear sweep to detect overlaps
- **Greedy selection** — make locally optimal choice at each step
- **Min-heap** — track active intervals efficiently (e.g. meeting rooms)

## Problems

| Problem | LeetCode | Difficulty | Technique |
|---------|----------|------------|-----------|
| Merge Intervals | #56 | Medium | Sort + linear merge |
| Non-overlapping Intervals | #435 | Medium | Greedy (keep min-end intervals) |
| Meeting Rooms II | #253 | Medium | Min-heap of end times |

## Pattern Cheat Sheet

```python
# Merge intervals template
intervals.sort(key=lambda x: x[0])
merged = [intervals[0]]
for start, end in intervals[1:]:
    if start <= merged[-1][1]:
        merged[-1][1] = max(merged[-1][1], end)
    else:
        merged.append([start, end])
```
