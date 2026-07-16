namespace MiniOS;

/// <summary>
/// One entry in a process's page table: which physical frame a virtual
/// page maps to, and whether it's actually present in physical memory.
/// Real page table entries also carry permission bits (read/write/exec),
/// a dirty bit, and an accessed bit; this keeps only what's needed to
/// demonstrate translation and page faults.
/// </summary>
public struct PageTableEntry
{
    public int FrameNumber;
    public bool Present;
}

/// <summary>
/// A small paging system: physical memory is divided into fixed-size
/// frames handed out from a free list (compare to xv6's kernel/kalloc.c),
/// and each process gets its own virtual-page-to-physical-frame table
/// (compare to kernel/vm.c). Accessing a virtual address that isn't yet
/// mapped triggers a page fault, which this simulation resolves on the
/// spot by allocating a fresh frame -- i.e. simple demand paging.
/// </summary>
public class PagingMemoryManager
{
    public const int PageSize = 4096; // bytes per page/frame
    private readonly int _totalFrames;
    private readonly Stack<int> _freeFrames = new();

    // pid -> (virtual page number -> page table entry)
    private readonly Dictionary<int, Dictionary<int, PageTableEntry>> _pageTables = new();
    // pid -> next unused virtual page number, so successive allocations
    // grow the process's virtual address space like a real heap would.
    private readonly Dictionary<int, int> _nextVirtualPage = new();

    private readonly Action<string> _log;

    public PagingMemoryManager(Action<string> log, int totalFrames = 8) // 8 frames = 32KB of simulated physical RAM
    {
        _log = log;
        _totalFrames = totalFrames;
        for (int i = totalFrames - 1; i >= 0; i--)
            _freeFrames.Push(i);
    }

    /// <summary>
    /// Maps enough fresh virtual pages to cover the requested byte count
    /// and backs each with a physical frame immediately (eager allocation,
    /// like a simple sbrk()/mmap()). Returns the starting virtual address.
    /// </summary>
    public int Allocate(int pid, int bytes)
    {
        var table = GetOrCreateTable(pid);
        var pagesNeeded = (bytes + PageSize - 1) / PageSize;
        var startVpn = _nextVirtualPage.GetValueOrDefault(pid, 0);

        for (int i = 0; i < pagesNeeded; i++)
        {
            var vpn = startVpn + i;
            var frame = AllocateFrame(pid);
            table[vpn] = new PageTableEntry { FrameNumber = frame, Present = true };
            _log($"  [paging]     mapped virtual page {vpn} -> physical frame {frame} for PID {pid}");
        }

        _nextVirtualPage[pid] = startVpn + pagesNeeded;
        return startVpn * PageSize;
    }

    /// <summary>
    /// Simulates a process touching a virtual address. If the containing
    /// page is already mapped, this is a normal translation (a "hit").
    /// If not, it's a page fault: the kernel handler allocates a frame
    /// on demand and completes the access, the same way real demand
    /// paging resolves a fault on first touch.
    /// </summary>
    public void AccessAddress(int pid, int virtualAddress)
    {
        var table = GetOrCreateTable(pid);
        var vpn = virtualAddress / PageSize;

        if (table.TryGetValue(vpn, out var entry) && entry.Present)
        {
            var physicalAddress = entry.FrameNumber * PageSize + (virtualAddress % PageSize);
            _log($"  [paging]     translation hit: va 0x{virtualAddress:X} -> pa 0x{physicalAddress:X} (frame {entry.FrameNumber})");
            return;
        }

        _log($"  [pagefault]  PID {pid} faulted on virtual page {vpn} (va 0x{virtualAddress:X}) -- not mapped");
        var frame = AllocateFrame(pid);
        table[vpn] = new PageTableEntry { FrameNumber = frame, Present = true };
        _nextVirtualPage[pid] = Math.Max(_nextVirtualPage.GetValueOrDefault(pid, 0), vpn + 1);
        var resolvedAddress = frame * PageSize + (virtualAddress % PageSize);
        _log($"  [pagefault]  resolved: virtual page {vpn} -> physical frame {frame}, pa 0x{resolvedAddress:X}");
    }

    /// <summary>Releases every frame owned by a process, e.g. on exit().</summary>
    public void Free(int pid)
    {
        if (_pageTables.TryGetValue(pid, out var table))
        {
            foreach (var entry in table.Values.Where(e => e.Present))
                _freeFrames.Push(entry.FrameNumber);
            _pageTables.Remove(pid);
        }
        _nextVirtualPage.Remove(pid);
    }

    private int AllocateFrame(int pid)
    {
        if (_freeFrames.Count == 0)
            throw new InvalidOperationException($"MiniOS: out of physical memory allocating for PID {pid} ({_totalFrames} frames all in use)");
        return _freeFrames.Pop();
    }

    private Dictionary<int, PageTableEntry> GetOrCreateTable(int pid)
    {
        if (!_pageTables.TryGetValue(pid, out var table))
        {
            table = new Dictionary<int, PageTableEntry>();
            _pageTables[pid] = table;
        }
        return table;
    }
}
