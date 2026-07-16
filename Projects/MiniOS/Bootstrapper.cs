namespace MiniOS;

/// <summary>
/// Simulates the boot sequence described in the "Boot: from power-on to
/// kernel code" write-up: firmware -> bootloader -> kernel entry -> kernel
/// subsystem init -> handoff to the scheduler. Nothing here touches real
/// hardware; each stage just prints what a real bootstrapper would be
/// doing at that point (compare to xv6's kernel/entry.S and kernel/main.c,
/// which do this for real on RISC-V).
/// </summary>
public class Bootstrapper
{
    private readonly Action<string> _log;

    public Bootstrapper(Action<string> log)
    {
        _log = log;
    }

    /// <summary>
    /// Runs every boot stage in order and returns the fully initialized
    /// kernel, ready to admit processes and start scheduling.
    /// </summary>
    public Kernel Boot()
    {
        Stage("Firmware", "Power-on self test complete, locating boot device");
        Sleep();

        Stage("Bootloader", "Loading kernel image into memory");
        Sleep();

        Stage("Bootloader", "Switching CPU into kernel-appropriate execution mode");
        Sleep();

        Stage("Kernel entry", "Setting up initial stack and jumping to kernel main()");
        Sleep();

        Stage("Kernel init", "Initializing paging memory manager (physical frames + per-process page tables)");
        var memory = new PagingMemoryManager(_log);
        Sleep();

        Stage("Kernel init", "Mounting root filesystem (in-memory inode table + block device)");
        var fileSystem = new SimpleFileSystem(_log);
        fileSystem.Create("welcome.txt");
        fileSystem.Write("welcome.txt", "MiniOS filesystem online");
        Sleep();

        Stage("Kernel init", "Initializing interrupt/trap vector table");
        Sleep();

        Stage("Kernel init", "Installing system call table");
        var syscalls = new SyscallTable(memory, fileSystem, _log);
        Sleep();

        Stage("Kernel init", "Initializing scheduler");
        var scheduler = new PriorityScheduler(syscalls, _log);
        Sleep();

        Stage("Kernel init", "Spawning init process (PID 0)");
        var kernel = new Kernel(memory, fileSystem, syscalls, scheduler, _log);
        var init = new ProcessControlBlock(0, "init", priority: 0, burstTicks: 1,
            new[] { new SyscallRequest(SyscallType.Write, "init process alive") });
        scheduler.Admit(init);
        Sleep();

        Stage("Boot complete", "Handing off control to the scheduler");
        _log("");

        return kernel;
    }

    private void Stage(string stage, string detail) => _log($"[{stage,-12}] {detail}");

    // A tiny artificial delay so the boot stages are readable as they
    // print, rather than flashing by instantly. Purely cosmetic.
    private static void Sleep() => Thread.Sleep(80);
}
