# Pattern: Concurrency

## Key Techniques
- **Locking for atomicity** — a single lock (or a lock per key/bucket) protects the read-modify-write sequences that would otherwise race under concurrent callers
- **Monotonic clocks over wall clocks** — `Stopwatch` / `time.monotonic()` never jump backwards or forwards (NTP sync, DST, manual clock changes), which matters for anything measuring elapsed time like rate limits or TTLs
- **Background sweep thread (active expiration)** — a timer/thread periodically reclaims state (idle buckets, expired keys) so memory stays bounded even if nobody ever touches that key again
- **Lazy expiration** — a read that lands on stale/expired state reclaims it on the spot, so callers never see data past its TTL even between sweeps
- **HashMap + doubly linked list for O(1) LRU** — same structure as the LRU Cache pattern, reused here to evict the least-recently-used entry once a cache is over capacity

## Problems

| Problem | Difficulty | Technique |
|---------|------------|-----------|
| Rate Limiter | Medium | Token bucket per client + background idle-bucket cleanup |
| Redis Cache | Medium | HashMap + doubly linked list (LRU) + TTL expiration, thread-safe |
| Redis Rate Limiter | Hard | Token bucket, state shared in Redis + atomic Lua script (EVAL) for multi-server correctness |

## Pattern Cheat Sheet

```python
# Lock-guarded read-modify-write (avoids lost updates under concurrency)
with lock:
    value = read_current_state()
    new_value = compute(value)
    write_new_state(new_value)

# Lazy + active expiration for TTL-based state
def get(key):
    with lock:
        entry = store.get(key)
        if entry is None:
            return None
        if entry.expires_at <= time.monotonic():
            del store[key]      # lazy expiration
            return None
        return entry.value

def sweep_loop():                # active expiration, runs on a background thread
    while not stop.wait(interval):
        with lock:
            for key, entry in list(store.items()):
                if entry.expires_at <= time.monotonic():
                    del store[key]
```
