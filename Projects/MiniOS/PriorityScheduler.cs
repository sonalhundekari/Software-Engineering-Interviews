namespace MiniOS;

/// <summary>
/// A preemptive priority scheduler.
///
/// Each tick, the scheduler picks the Ready process with the numerically
/// lowest Priority value (0 = highest priority) and runs it for one
/// quantum. Every other Ready process ages: its effective priority is
/// temporarily boosted the longer it waits, which prevents low-priority
/// processes from starving forever -- the same problem Windows' MLFQ
/// scheduler solves with priority boosts, and CFS solves structurally
/// by tracking accumulated virtual runtime instead of fixed priorities.
/// </summary>
public class PriorityScheduler
{
    private readonly List<ProcessControlBlock> _processes = new();
    private readonly SyscallTable _syscalls;
    private readonly Action<string> _log;
    private readonly int _quantum;
    private readonly int _agingThreshold;

    public PriorityScheduler(SyscallTable syscalls, Action<string> log, int quantum = 1, int agingThreshold = 3)
    {
        _syscalls = syscalls;
        _log = log;
        _quantum = quantum;
        _agingThreshold = agingThreshold;
    }

    public void Admit(ProcessControlBlock process)
    {
        process.State = ProcessState.Ready;
        _processes.Add(process);
        _log($"[scheduler] Admitted {process}");
    }

    /// <summary>
    /// Runs the scheduling loop until every process has terminated.
    /// Returns the order processes were selected to run in, for the
    /// summary printed at the end.
    /// </summary>
    public List<string> Run()
    {
        var runOrder = new List<string>();
        int tick = 0;

        while (_processes.Any(p => p.State is ProcessState.Ready or ProcessState.Running or ProcessState.Waiting))
        {
            tick++;
            _log($"\n--- tick {tick} ---");

            // A process sleeping in this simplified model becomes Ready
            // again after one tick, so scheduling can proceed. Real
            // kernels wake sleepers via timers or event completion.
            foreach (var p in _processes.Where(p => p.State == ProcessState.Waiting))
                p.State = ProcessState.Ready;

            var candidate = SelectNextProcess();
            if (candidate is null)
                continue; // everyone is asleep/terminated this tick

            RunOneQuantum(candidate);
            runOrder.Add($"tick {tick}: PID {candidate.Pid} ({candidate.Name})");

            AgeWaitingProcesses(candidate);

            if (tick > 500)
            {
                _log("[scheduler] safety limit reached, stopping");
                break;
            }
        }

        return runOrder;
    }

    private ProcessControlBlock? SelectNextProcess()
    {
        var ready = _processes.Where(p => p.State == ProcessState.Ready).ToList();
        if (ready.Count == 0) return null;

        // Lowest Priority number wins; ties broken by PID (FIFO-ish, stable).
        var chosen = ready.OrderBy(p => p.Priority).ThenBy(p => p.Pid).First();
        chosen.State = ProcessState.Running;
        return chosen;
    }

    private void RunOneQuantum(ProcessControlBlock process)
    {
        _log($"[scheduler] Dispatching {process}");

        // Simulate the process doing some work, and possibly issuing a syscall.
        var ticksThisRun = Math.Min(_quantum, process.RemainingBurstTicks);
        process.RemainingBurstTicks -= ticksThisRun;
        _log($"  PID {process.Pid} executes for {ticksThisRun} tick(s), {process.RemainingBurstTicks} remaining");

        if (process.PendingSyscalls.Count > 0)
        {
            var request = process.PendingSyscalls.Dequeue();
            _syscalls.Dispatch(process, request);
        }

        if (process.State == ProcessState.Terminated)
        {
            _log($"[scheduler] PID {process.Pid} exited");
            return;
        }

        if (process.RemainingBurstTicks <= 0)
        {
            process.State = ProcessState.Terminated;
            _log($"[scheduler] PID {process.Pid} completed its work and terminated");
            return;
        }

        // If the syscall didn't already put it to sleep, it goes back to Ready
        // and its priority resets to base (it "used its turn").
        if (process.State == ProcessState.Running)
        {
            process.State = ProcessState.Ready;
            process.Priority = process.BasePriority;
            process.WaitTicks = 0;
        }
    }

    private void AgeWaitingProcesses(ProcessControlBlock justRan)
    {
        foreach (var p in _processes.Where(p => p.State == ProcessState.Ready && p != justRan))
        {
            p.WaitTicks++;
            if (p.WaitTicks >= _agingThreshold && p.Priority > 0)
            {
                p.Priority--; // numerically lower = higher priority
                p.WaitTicks = 0;
                _log($"[scheduler] PID {p.Pid} aged: priority boosted to {p.Priority} after waiting");
            }
        }
    }
}
