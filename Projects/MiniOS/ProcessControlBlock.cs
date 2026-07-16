namespace MiniOS;

/// <summary>
/// The lifecycle states a process can be in. Mirrors the classic
/// New -> Ready -> Running -> (Waiting) -> Terminated state machine
/// used by real kernels.
/// </summary>
public enum ProcessState
{
    New,
    Ready,
    Running,
    Waiting,
    Terminated
}

/// <summary>
/// A simplified Process Control Block (PCB). Real kernels store saved
/// register state, a page table pointer, open file handles, etc. This
/// keeps only what's needed to demonstrate scheduling and syscalls.
/// </summary>
public class ProcessControlBlock
{
    public int Pid { get; }
    public string Name { get; }

    /// <summary>Lower numeric value = higher priority (0 is highest), matching the convention used by most real schedulers (e.g. Windows' 0-31 priority levels).</summary>
    public int Priority { get; set; }

    /// <summary>Original priority, kept so we can demonstrate aging (temporary priority boosts to prevent starvation).</summary>
    public int BasePriority { get; }

    public ProcessState State { get; set; } = ProcessState.New;

    /// <summary>Remaining CPU time (in simulated ticks) the process still needs before it completes.</summary>
    public int RemainingBurstTicks { get; set; }

    /// <summary>How many ticks this process has waited in the Ready queue without running. Used for aging.</summary>
    public int WaitTicks { get; set; }

    /// <summary>The sequence of syscalls this process will issue while it runs, consumed one at a time.</summary>
    public Queue<SyscallRequest> PendingSyscalls { get; }

    public ProcessControlBlock(int pid, string name, int priority, int burstTicks, IEnumerable<SyscallRequest>? syscalls = null)
    {
        Pid = pid;
        Name = name;
        Priority = priority;
        BasePriority = priority;
        RemainingBurstTicks = burstTicks;
        PendingSyscalls = new Queue<SyscallRequest>(syscalls ?? Array.Empty<SyscallRequest>());
    }

    public override string ToString() =>
        $"PID {Pid,2} [{Name,-10}] prio={Priority} state={State,-10} remaining={RemainingBurstTicks}";
}
