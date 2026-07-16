namespace MiniOS;

/// <summary>
/// The "kernel" object created once boot finishes. It just holds
/// references to the subsystems the bootstrapper initialized and
/// offers a way to admit new processes before running the scheduler.
/// </summary>
public class Kernel
{
    private readonly PagingMemoryManager _memory;
    private readonly SimpleFileSystem _fileSystem;
    private readonly SyscallTable _syscalls;
    private readonly PriorityScheduler _scheduler;
    private readonly Action<string> _log;
    private int _nextPid = 1; // PID 0 is reserved for init

    public Kernel(PagingMemoryManager memory, SimpleFileSystem fileSystem, SyscallTable syscalls, PriorityScheduler scheduler, Action<string> log)
    {
        _memory = memory;
        _fileSystem = fileSystem;
        _syscalls = syscalls;
        _scheduler = scheduler;
        _log = log;
    }

    /// <summary>
    /// Creates and admits a new process, analogous to a fork()+exec() pair
    /// handing a freshly created process to the scheduler's ready queue.
    /// </summary>
    public ProcessControlBlock SpawnProcess(string name, int priority, int burstTicks, IEnumerable<SyscallRequest> syscalls)
    {
        var pcb = new ProcessControlBlock(_nextPid++, name, priority, burstTicks, syscalls);
        _scheduler.Admit(pcb);
        return pcb;
    }

    public List<string> RunScheduler() => _scheduler.Run();
}
