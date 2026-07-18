# Redis-style in-memory cache
# Pattern: TTL expiration + LRU eviction, thread-safe
#
# Problem: Build a thread-safe cache that mimics how Redis is commonly used
# as a cache layer -- SET/GET/DEL with optional per-key expiry (EXPIRE/TTL),
# plus bounded memory via LRU eviction once a capacity limit is reached.
#
# Approach:
#   - dict: key -> node for O(1) lookup (same idea as LRUCache)
#   - Doubly linked list: tracks recency so the least-recently-used key can
#     be evicted in O(1) once the cache is over capacity
#   - Each node also carries an absolute expiry timestamp
#   - Lazy expiration: a get/exists/expire on an already-expired key removes
#     it on the spot instead of returning stale data
#   - Active expiration: a background thread sweeps for expired keys, so
#     entries nobody ever reads again still get reclaimed
#   - A single RLock serializes every operation -- the simplest way to keep
#     the index and the LRU list consistent; real Redis sidesteps this
#     entirely by being single-threaded
#
# Time: O(1) for set/get/delete/expire/ttl  Space: O(capacity)

import threading
import time
from typing import Any, Optional


class _Node:
    __slots__ = ("key", "value", "expires_at", "prev", "next")

    def __init__(self, key=None, value=None, expires_at=None):
        self.key = key
        self.value = value
        self.expires_at = expires_at  # monotonic seconds, None = no expiry
        self.prev: Optional["_Node"] = None
        self.next: Optional["_Node"] = None


class RedisCache:
    def __init__(self, capacity: int, sweep_interval: float = 0.2):
        if capacity <= 0:
            raise ValueError("capacity must be positive")
        self._capacity = capacity

        # Guards everything below.
        self._lock = threading.RLock()
        self._index: dict[str, _Node] = {}

        # Dummy head/tail: head.next is the least-recently-used entry,
        # tail.prev is the most-recently-used entry.
        self._head = _Node()
        self._tail = _Node()
        self._head.next = self._tail
        self._tail.prev = self._head

        # Background sweeper reclaims expired keys even if nothing ever
        # calls get() on them again.
        self._stop = threading.Event()
        self._sweeper = threading.Thread(
            target=self._sweep_loop, args=(sweep_interval,), daemon=True
        )
        self._sweeper.start()

    def set(self, key: str, value: Any, ttl: Optional[float] = None) -> None:
        """Sets a key, optionally with a TTL in seconds. Overwrites any existing value."""
        expires_at = time.monotonic() + ttl if ttl is not None else None
        with self._lock:
            node = self._index.get(key)
            if node is not None:
                node.value = value
                node.expires_at = expires_at
                self._move_to_most_recent(node)
                return

            node = _Node(key, value, expires_at)
            self._index[key] = node
            self._insert_most_recent(node)

            if len(self._index) > self._capacity:
                self._evict_least_recently_used()

    def get(self, key: str) -> Optional[Any]:
        """Returns the value, or None if missing/expired.

        Use `exists` instead if you need to distinguish a stored None from a miss.
        """
        with self._lock:
            node = self._index.get(key)
            if node is None:
                return None
            if self._is_expired(node):
                self._remove_node(node)  # lazy expiration
                return None
            self._move_to_most_recent(node)
            return node.value

    def exists(self, key: str) -> bool:
        with self._lock:
            node = self._index.get(key)
            if node is None:
                return False
            if self._is_expired(node):
                self._remove_node(node)
                return False
            return True

    def delete(self, key: str) -> bool:
        with self._lock:
            node = self._index.get(key)
            if node is None:
                return False
            self._remove_node(node)
            return True

    def expire(self, key: str, ttl: float) -> bool:
        """Sets/resets a key's TTL. Returns False if the key is missing or already expired."""
        with self._lock:
            node = self._index.get(key)
            if node is None or self._is_expired(node):
                return False
            node.expires_at = time.monotonic() + ttl
            return True

    def ttl(self, key: str) -> float:
        """Seconds remaining, -1 if no expiry set, -2 if missing/expired."""
        with self._lock:
            node = self._index.get(key)
            if node is None or self._is_expired(node):
                return -2
            if node.expires_at is None:
                return -1
            return max(0.0, node.expires_at - time.monotonic())

    def __len__(self) -> int:
        with self._lock:
            return len(self._index)

    def _is_expired(self, node: _Node) -> bool:
        return node.expires_at is not None and node.expires_at <= time.monotonic()

    def _remove_node(self, node: _Node) -> None:
        node.prev.next = node.next
        node.next.prev = node.prev
        del self._index[node.key]

    def _insert_most_recent(self, node: _Node) -> None:
        node.prev = self._tail.prev
        node.next = self._tail
        self._tail.prev.next = node
        self._tail.prev = node

    def _move_to_most_recent(self, node: _Node) -> None:
        node.prev.next = node.next
        node.next.prev = node.prev
        self._insert_most_recent(node)

    def _evict_least_recently_used(self) -> None:
        self._remove_node(self._head.next)

    def _sweep_loop(self, interval: float) -> None:
        """Active expiration: runs on a background thread so idle keys are
        reclaimed even if nobody ever calls get() on them again."""
        while not self._stop.wait(interval):
            with self._lock:
                expired = [n for n in self._index.values() if self._is_expired(n)]
                for node in expired:
                    self._remove_node(node)

    def close(self) -> None:
        self._stop.set()
        self._sweeper.join(timeout=1)

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        self.close()


# ---- Tests ----
if __name__ == "__main__":
    with RedisCache(capacity=2) as cache:
        cache.set("a", "1")
        cache.set("b", "2")
        assert cache.get("a") == "1"      # touch "a" -> now most-recently-used
        cache.set("c", "3")               # over capacity -> evicts "b" (LRU)
        assert cache.get("b") is None
        assert cache.get("a") == "1"
        assert cache.get("c") == "3"

        cache.set("ttl-key", "temp", ttl=0.05)
        assert cache.exists("ttl-key")
        time.sleep(0.1)
        assert not cache.exists("ttl-key")  # expired

        print("All tests passed.")
