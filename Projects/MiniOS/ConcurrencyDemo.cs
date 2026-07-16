namespace MiniOS;

/// <summary>
/// Unlike the rest of MiniOS, this demo uses real .NET threads scheduled
/// by the actual macOS/.NET runtime -- not the simulated scheduler above
/// -- because a race condition and a deadlock are timing phenomena that a
/// cooperative simulation can't reproduce honestly. This is the same
/// class of bug the "Concurrency and synchronization" section of the
/// write-up describes: unsynchronized access to shared kernel data
/// structures (the process table, the free list, filesystem metadata).
/// </summary>
public static class ConcurrencyDemo
{
    private class Counter
    {
        public int Value;
        private readonly object _lock = new();

        public void IncrementUnsafe() => Value++; // NOT atomic: read, add, write are three separate steps

        public void IncrementSafe()
        {
            lock (_lock) { Value++; }
        }
    }

    /// <summary>
    /// Many threads increment a shared counter with no synchronization.
    /// Two threads can both read the same old value before either writes
    /// back the incremented result, so one increment gets silently lost.
    /// The final total is frequently (not always -- that's the nature of
    /// a race) less than the expected total.
    /// </summary>
    public static void RunRaceCondition(Action<string> log, int threadCount = 8, int incrementsPerThread = 200_000)
    {
        log($"[concurrency] Racing {threadCount} threads x {incrementsPerThread} increments, NO lock...");
        var counter = new Counter();
        var threads = new Thread[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            threads[i] = new Thread(() =>
            {
                for (int j = 0; j < incrementsPerThread; j++)
                    counter.IncrementUnsafe();
            });
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        var expected = threadCount * incrementsPerThread;
        var lost = expected - counter.Value;
        log($"[concurrency] expected={expected}, actual={counter.Value}, lost={lost} update(s)"
            + (lost > 0 ? "  <-- race condition observed" : "  (no loss this run -- races aren't guaranteed every run; try again)"));
    }

    /// <summary>The same workload, this time serialized with a lock. Every increment is preserved.</summary>
    public static void RunFixedWithLock(Action<string> log, int threadCount = 8, int incrementsPerThread = 200_000)
    {
        log($"[concurrency] Same workload, WITH a lock...");
        var counter = new Counter();
        var threads = new Thread[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            threads[i] = new Thread(() =>
            {
                for (int j = 0; j < incrementsPerThread; j++)
                    counter.IncrementSafe();
            });
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        var expected = threadCount * incrementsPerThread;
        log($"[concurrency] expected={expected}, actual={counter.Value}  <-- matches: the lock serialized every increment");
    }

    /// <summary>
    /// Classic lock-ordering deadlock: thread A takes lock 1 then wants
    /// lock 2; thread B takes lock 2 then wants lock 1. If both grab their
    /// first lock before either reaches for the second, neither can ever
    /// proceed. Real deadlocks simply hang forever; this demo uses a
    /// timed lock attempt (Monitor.TryEnter) purely so the program can
    /// detect and report the deadlock instead of freezing -- the same
    /// pragmatic mitigation (lock timeouts) some real systems use.
    /// </summary>
    public static void RunDeadlockDemo(Action<string> log)
    {
        log("[concurrency] Deadlock demo: two threads acquiring two locks in opposite order...");
        var lockA = new object();
        var lockB = new object();
        var deadlockDetected = false;

        var threadA = new Thread(() =>
        {
            lock (lockA)
            {
                log("  [thread A] holds lock A, wants lock B");
                Thread.Sleep(150); // give thread B time to grab lock B first
                if (!Monitor.TryEnter(lockB, TimeSpan.FromMilliseconds(500)))
                {
                    log("  [thread A] timed out waiting for lock B -- deadlock");
                    deadlockDetected = true;
                    return;
                }
                Monitor.Exit(lockB);
            }
        });

        var threadB = new Thread(() =>
        {
            lock (lockB)
            {
                log("  [thread B] holds lock B, wants lock A");
                Thread.Sleep(150); // give thread A time to grab lock A first
                if (!Monitor.TryEnter(lockA, TimeSpan.FromMilliseconds(500)))
                {
                    log("  [thread B] timed out waiting for lock A -- deadlock");
                    deadlockDetected = true;
                    return;
                }
                Monitor.Exit(lockA);
            }
        });

        threadA.Start();
        threadB.Start();
        threadA.Join();
        threadB.Join();

        log(deadlockDetected
            ? "[concurrency] deadlock reproduced and detected via lock timeout (a real deadlock would hang forever without one)"
            : "[concurrency] no deadlock this run -- thread interleaving is timing-dependent; try again if you want to see it");
    }
}
