using MiniOS;

void Log(string message) => Console.WriteLine(message);

Console.WriteLine("=====================================================");
Console.WriteLine("  MiniOS -- a tiny educational OS internals sim");
Console.WriteLine("=====================================================\n");

// --- 1. Boot ---------------------------------------------------------
var bootstrapper = new Bootstrapper(Log);
var kernel = bootstrapper.Boot();

// --- 2. Create sample processes exercising the scheduler, paging,
//        and the filesystem together. ---------------------------------
kernel.SpawnProcess(
    name: "logger",
    priority: 3, // low priority (numerically higher = lower priority)
    burstTicks: 3,
    syscalls: new[]
    {
        new SyscallRequest(SyscallType.Write, "logger: starting up"),
        new SyscallRequest(SyscallType.CreateFile, "logger.txt"),
        new SyscallRequest(SyscallType.WriteFile, "logger.txt", "boot sequence completed without errors"),
    });

kernel.SpawnProcess(
    name: "network",
    priority: 1, // high priority
    burstTicks: 2,
    syscalls: new[]
    {
        new SyscallRequest(SyscallType.AllocMemory, "8192"),   // maps 2 pages (page size 4096)
        new SyscallRequest(SyscallType.AccessMemory, "0"),     // touches page 0 -- should be a hit, it's already mapped
    });

kernel.SpawnProcess(
    name: "compute",
    priority: 2, // medium priority
    burstTicks: 4,
    syscalls: new[]
    {
        new SyscallRequest(SyscallType.GetPid),
        new SyscallRequest(SyscallType.AccessMemory, "4000"),  // nothing mapped yet for this PID -- triggers a page fault
        new SyscallRequest(SyscallType.ReadFile, "welcome.txt"), // seeded at boot, so this is deterministic regardless of scheduling order
    });

kernel.SpawnProcess(
    name: "watchdog",
    priority: 0, // highest priority
    burstTicks: 1,
    syscalls: new[]
    {
        new SyscallRequest(SyscallType.Write, "watchdog: system nominal"),
    });

// --- 3. Run the priority scheduler until every process exits ---------
var runOrder = kernel.RunScheduler();

// --- 4. Scheduling summary ---------------------------------------------
Console.WriteLine("\n=====================================================");
Console.WriteLine("  Scheduling summary (dispatch order)");
Console.WriteLine("=====================================================");
foreach (var entry in runOrder)
    Console.WriteLine("  " + entry);

// --- 5. Concurrency demo: real OS threads, run separately from the
//        simulated scheduler above, since races and deadlocks are
//        genuine timing phenomena that need real concurrent execution
//        to demonstrate honestly. ---------------------------------------
Console.WriteLine("\n=====================================================");
Console.WriteLine("  Concurrency demo (real threads, not simulated)");
Console.WriteLine("=====================================================");
ConcurrencyDemo.RunRaceCondition(Log);
Console.WriteLine();
ConcurrencyDemo.RunFixedWithLock(Log);
Console.WriteLine();
ConcurrencyDemo.RunDeadlockDemo(Log);

Console.WriteLine("\nAll processes terminated. MiniOS shutting down.");
