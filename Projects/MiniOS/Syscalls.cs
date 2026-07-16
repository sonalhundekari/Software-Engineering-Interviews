namespace MiniOS;

/// <summary>
/// The set of system calls this mini kernel understands. Real kernels
/// have hundreds (see kernel/syscall.c in xv6 for a minimal real example,
/// or the syscall table in the Linux kernel for a production one). This
/// is intentionally a small, representative subset spanning process,
/// memory, and file operations.
/// </summary>
public enum SyscallType
{
    Write,        // write a string to the console, like write()
    Sleep,        // voluntarily give up the CPU for N ticks, like sleep()/yield()
    AllocMemory,  // request N bytes of virtual memory, backed by fresh page mappings, like sbrk()/mmap()
    AccessMemory, // touch a virtual address; may hit or may page-fault
    GetPid,       // return the caller's own process id, like getpid()
    CreateFile,   // create a file, like creat()
    WriteFile,    // write data to a file, like write(fd, ...)
    ReadFile,     // read data from a file, like read(fd, ...)
    DeleteFile,   // remove a file, like unlink()
    Exit          // terminate the process, like exit()
}

/// <summary>
/// A single syscall a process wants to make, plus whatever arguments it
/// needs. This is analogous to the arguments a user program places in
/// registers before executing a trap instruction (e.g. `syscall` on
/// x86-64). Most calls need only one argument; WriteFile needs two
/// (filename and the data to write), so a second slot is provided.
/// </summary>
public readonly struct SyscallRequest
{
    public SyscallType Type { get; }
    public string Argument { get; }
    public string Argument2 { get; }

    public SyscallRequest(SyscallType type, string argument = "", string argument2 = "")
    {
        Type = type;
        Argument = argument;
        Argument2 = argument2;
    }

    public override string ToString() =>
        Argument2.Length > 0 ? $"{Type}({Argument}, {Argument2})" :
        Argument.Length > 0 ? $"{Type}({Argument})" : $"{Type}()";
}

/// <summary>
/// The kernel-side syscall table: a dispatcher that maps a syscall number
/// (here, an enum) to the kernel function that actually handles it, the
/// same pattern xv6's syscall.c or the Linux syscall table uses.
///
/// Calling Dispatch() simulates the user-mode -> kernel-mode trap: the
/// caller "traps in", the kernel runs privileged code on the process's
/// behalf (here, possibly touching the paging system or the filesystem),
/// and control returns to the process.
/// </summary>
public class SyscallTable
{
    private readonly PagingMemoryManager _memory;
    private readonly SimpleFileSystem _fileSystem;
    private readonly Action<string> _log;

    public SyscallTable(PagingMemoryManager memory, SimpleFileSystem fileSystem, Action<string> log)
    {
        _memory = memory;
        _fileSystem = fileSystem;
        _log = log;
    }

    public void Dispatch(ProcessControlBlock process, SyscallRequest request)
    {
        _log($"  [trap]  PID {process.Pid} -> kernel mode: {request}");

        switch (request.Type)
        {
            case SyscallType.Write:
                _log($"  [sys_write]  PID {process.Pid}: \"{request.Argument}\"");
                break;

            case SyscallType.Sleep:
                _log($"  [sys_sleep]  PID {process.Pid} yields the CPU voluntarily");
                process.State = ProcessState.Waiting;
                break;

            case SyscallType.AllocMemory:
                var bytes = int.Parse(request.Argument);
                var virtualAddress = _memory.Allocate(process.Pid, bytes);
                _log($"  [sys_alloc]  PID {process.Pid}: {bytes} bytes -> starting virtual address 0x{virtualAddress:X}");
                break;

            case SyscallType.AccessMemory:
                var address = Convert.ToInt32(request.Argument, 16);
                _log($"  [sys_access] PID {process.Pid}: touching virtual address 0x{address:X}");
                _memory.AccessAddress(process.Pid, address);
                break;

            case SyscallType.GetPid:
                _log($"  [sys_getpid] PID {process.Pid}: returned {process.Pid}");
                break;

            case SyscallType.CreateFile:
                _fileSystem.Create(request.Argument);
                break;

            case SyscallType.WriteFile:
                _fileSystem.Write(request.Argument, request.Argument2);
                break;

            case SyscallType.ReadFile:
                var content = _fileSystem.Read(request.Argument);
                _log($"  [sys_read]   PID {process.Pid} read '{request.Argument}': \"{content}\"");
                break;

            case SyscallType.DeleteFile:
                _fileSystem.Delete(request.Argument);
                break;

            case SyscallType.Exit:
                _log($"  [sys_exit]   PID {process.Pid} terminating");
                process.State = ProcessState.Terminated;
                process.RemainingBurstTicks = 0;
                _memory.Free(process.Pid);
                break;
        }

        _log($"  [trap]  PID {process.Pid} <- return to user mode");
    }
}
