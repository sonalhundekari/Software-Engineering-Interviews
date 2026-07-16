# MiniOS — a tiny educational OS simulation (C#)

This is a small console application that **simulates** the core internals
of an operating system so you can watch them happen step by step:

1. **Boot sequence** — firmware → bootloader → kernel init → handoff to the scheduler
2. **Priority scheduling** — a preemptive scheduler with priority aging (to prevent starvation)
3. **System calls** — a syscall table with trap-in/trap-out dispatch
4. **Paging memory management** — virtual pages mapped to physical frames, with real page faults and on-demand mapping
5. **A mini filesystem** — inodes, block allocation, a free-block bitmap
6. **Concurrency** — a real race condition, a lock-based fix, and a real deadlock, run on actual OS threads

It does not touch real hardware, boot a real machine, or run in a VM — it's a
readable model of the mechanics, in the spirit of a teaching kernel like
[xv6](https://github.com/mit-pdos/xv6-riscv), but runnable directly as a normal
program on macOS.

## Prerequisites

You need the **.NET SDK** (8.0 or later) installed on macOS.

```bash
# check if you already have it
dotnet --version

# if not, install via Homebrew
brew install --cask dotnet-sdk
```

## Running it

```bash
cd MiniOS
dotnet run
```

The program runs through four phases, in order:

1. **Boot** — firmware → bootloader → kernel init (paging manager, filesystem mount, syscall table, scheduler) → init process
2. **Scheduling** — four sample processes at different priorities run to completion, each issuing a handful of syscalls
3. **Scheduling summary** — the dispatch order the priority scheduler actually chose
4. **Concurrency demo** — a real race condition, the same workload fixed with a lock, and a real (detected) deadlock

## Project layout

| File | What it models |
|---|---|
| `Bootstrapper.cs` | The boot sequence: firmware → bootloader → kernel init → first process |
| `Kernel.cs` | Holds the initialized subsystems and admits new processes |
| `ProcessControlBlock.cs` | A simplified PCB: pid, priority, state, remaining CPU burst, pending syscalls |
| `PriorityScheduler.cs` | Preemptive priority scheduling with aging to avoid starvation |
| `Syscalls.cs` | The syscall table (`Write`, `Sleep`, `AllocMemory`, `AccessMemory`, `GetPid`, file syscalls, `Exit`) and trap dispatch |
| `PagingMemoryManager.cs` | Virtual page → physical frame translation, a physical frame free list, and on-demand page-fault handling |
| `FileSystem.cs` | Inode-based filesystem: inode table, directory, block allocation via a free bitmap |
| `ConcurrencyDemo.cs` | A real race condition, a lock-based fix, and a real lock-ordering deadlock, using actual `System.Threading.Thread`s |
| `Program.cs` | Wires it all together: boot, spawn sample processes, run the scheduler, run the concurrency demo |

## What each new subsystem demonstrates

### Paging memory (`PagingMemoryManager.cs`)

Physical memory is a fixed pool of 4KB frames (8 frames = 32KB by default,
kept small so the output is easy to read) handed out from a free list —
the same idea as xv6's `kalloc.c`. Each process gets its own virtual page
table (a `Dictionary<vpn, PageTableEntry>`), separate from every other
process's, which is what gives processes address-space isolation.

Two operations exercise it:
- `AllocMemory` maps fresh virtual pages to physical frames immediately (eager allocation).
- `AccessMemory` touches a virtual address. If the page is already mapped, you'll see a translation **hit**. If not, you'll see a real **page fault** get logged and resolved on the spot (simple demand paging) — the `compute` process in `Program.cs` deliberately touches an address it never allocated, to trigger this.

### Filesystem (`FileSystem.cs`)

A flat in-memory disk: a fixed number of small fixed-size blocks, a
free-block bitmap, an inode per file (tracking which blocks belong to it
and the file's size), and a directory mapping names to inode numbers —
the same structure xv6's `fs.c` uses, minus the write-ahead log (so this
version does **not** demonstrate crash consistency, only allocation and
lookup). `CreateFile`, `WriteFile`, `ReadFile`, and `DeleteFile` syscalls
exercise it. Watch the block numbers in the output — writing a file that
spans multiple blocks shows the allocator handing out separate blocks and
the inode tracking all of them in order.

### Concurrency (`ConcurrencyDemo.cs`)

This is the one part of MiniOS that does **not** run inside the simulated
scheduler — it uses real `System.Threading.Thread`s, because a race
condition and a deadlock are genuine timing phenomena a cooperative,
single-threaded simulation can't produce honestly. Three runs:

1. **Race condition** — several real threads increment a shared counter with no lock. `Value++` isn't atomic (it's a read, an add, and a write as three separate steps), so two threads can interleave and one increment gets silently lost. You'll usually see `actual < expected`.
2. **Fixed with a lock** — the identical workload, serialized with `lock`. The counts always match.
3. **Deadlock** — two threads each hold one lock and wait on the other, acquired in opposite order — the classic lock-ordering deadlock. A real deadlock hangs forever; this demo uses `Monitor.TryEnter` with a timeout purely so it can detect and report the deadlock instead of freezing, which is also a real mitigation strategy some production systems use.


## Further changes needed

- **Add a new process** in `Program.cs` with a different `priority` and list of `SyscallRequest`s.
- **Shrink `totalFrames`** in `PagingMemoryManager`'s constructor (set in `Bootstrapper.cs`) to force an out-of-physical-memory condition and see how it's reported.
- **Shrink `totalBlocks`** in `SimpleFileSystem` to see the filesystem run out of space.
- **Increase `incrementsPerThread`** in the race-condition demo to make the lost updates more consistently visible.
- **Add a `Fork` syscall** that calls `Kernel.SpawnProcess` on a running process's behalf — a natural next extension once you're comfortable with the current syscall dispatch pattern.

## Why C# / .NET rather than raw C and QEMU

A real bootable OS needs a cross-compiler toolchain, a bootloader (or a
Multiboot-compliant kernel image), and an emulator like QEMU to run it
safely without touching real hardware. This project trades hardware
realism for immediate runnability: `dotnet run` and to watch boot,
scheduling, paging, filesystem operations, and real concurrency bugs
happen. For any real thing afterward,
[xv6-riscv](https://github.com/mit-pdos/xv6-riscv) plus QEMU is the
standard next step.
