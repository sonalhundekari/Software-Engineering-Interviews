# Operating System Internals, Explained

An operating system's job sounds simple: share hardware between programs and give each one the illusion that it has the machine to itself. Underneath that one sentence is most of what makes a kernel hard to build. This is a walk through the major internals — boot, memory, processes, interrupts, concurrency, and filesystems — with pointers to real source and documentation for each.

## 1. Boot: from power-on to kernel code

When a machine powers on, firmware (BIOS on older x86 systems, UEFI on modern ones) runs first, not the OS. Firmware's job is narrow: initialize just enough hardware to find a bootable device, load a small program from it, and jump to it. That small program is the bootloader, and its job is narrower still — locate the kernel image, put it in memory, switch the CPU into the right mode, and hand off control.

On x86, that mode switch is a real complication. The CPU starts in 16-bit real mode, a legacy compatibility mode dating back to the 8086. A modern kernel needs 32-bit protected mode or 64-bit long mode, so part of boot is literally reconfiguring the CPU's operating mode mid-flight — setting up a Global Descriptor Table, enabling the right control register bits, and only then jumping into 32/64-bit code. The OSDev Wiki's walkthrough of this is the most commonly cited independent reference for it: `https://wiki.osdev.org/Bare_Bones`.

Once the kernel's entry point runs, it typically does, in order: set up an initial stack, establish minimal page tables so it can use virtual memory, initialize a console/serial output so it can print debug information, and then call into a `main()`-style function that brings up the rest of the subsystems (memory allocator, interrupt handling, scheduler, drivers) one at a time. MIT's xv6 kernel — a compact, real, buildable teaching OS — shows this whole sequence in about 60 lines in `kernel/main.c` (`https://github.com/mit-pdos/xv6-riscv/blob/riscv/kernel/main.c`), which is worth reading end to end precisely because nothing is hidden behind abstraction layers.

## 2. Memory management: giving every process its own universe

Physical RAM is one flat array of bytes shared by everything running on the machine. Virtual memory is the layer that lets each process believe it has its own private, contiguous address space, isolated from every other process. Two mechanisms make this work together.

**Paging.** Physical memory is divided into fixed-size chunks (commonly 4KB pages). A process's virtual addresses are translated to physical addresses through a page table — essentially a lookup structure the CPU's memory management unit (MMU) walks on every memory access. Because that walk happens on every access, hardware caches recent translations in a Translation Lookaside Buffer (TLB) to avoid re-walking the table constantly. The OSDev Wiki's paging article covers the x86 page table format in detail: `https://wiki.osdev.org/Paging`.

**Allocation.** The kernel needs its own bookkeeping to decide which physical pages are free and hand them out on request. The simplest workable design — used by xv6 — is a linked free list of physical pages: freeing a page pushes it onto the list, allocating pops one off (`kernel/kalloc.c` in the xv6-riscv repo). Production kernels use considerably more elaborate allocators (buddy allocators, slab allocators) to reduce fragmentation and speed up allocation of odd-sized kernel objects, but the free-list version is enough to understand the core idea: physical memory management is fundamentally an inventory problem.

Two consequences fall out of virtual memory that are worth knowing by name:
- **Isolation** — one process cannot read or corrupt another's memory, because their page tables simply don't map to each other's physical pages.
- **Copy-on-write** — when a process forks, the child can initially share the parent's physical pages read-only; only when either side writes does the kernel actually copy the page. This is why `fork()` is cheap even for processes with large address spaces.

## 3. Processes, threads, and scheduling

A process is a running program: an address space, open file handles, and one or more threads of execution. A thread is a sequence of instructions plus its own stack and register state; multiple threads in one process share the same address space.

The kernel maintains a table of process/thread control blocks — structures holding each one's saved registers, program counter, memory mappings, and state (running, ready, blocked). Switching the CPU from one thread to another — a **context switch** — means saving the current thread's register state into its control block and loading another thread's saved state into the CPU registers. This is short enough in practice that xv6 implements it in a few dozen lines of assembly (`kernel/swtch.S`), which is a good concrete artifact to look at if "context switch" feels abstract.

**Scheduling** is the policy layer that decides which ready thread runs next. Common approaches:
- **Round robin** — cycle through ready threads in order, giving each a fixed time slice. Simple, fair, easy to reason about; xv6 uses exactly this.
- **Priority scheduling** — higher-priority threads preempt lower-priority ones. Risks starvation of low-priority work unless the kernel periodically boosts waiting threads' priority.
- **Completely Fair Scheduler (CFS)** — Linux's default scheduler for years, which tracks each thread's accumulated "virtual runtime" and always runs whichever thread has received the least CPU time so far, approximating an ideal of every thread getting an equal share. The kernel documentation describes the design directly: `https://docs.kernel.org/scheduler/sched-design-CFS.html`.

A **preemptive** kernel can interrupt a running thread involuntarily (typically via a timer interrupt) to give another thread a turn; a **cooperative** kernel relies on threads voluntarily yielding. Nearly all general-purpose OSes today are preemptive, because a single misbehaving or long-running thread in a cooperative system can freeze the whole machine.

## 4. Interrupts, traps, and system calls

The kernel doesn't run continuously in the background — it runs in response to events. Three kinds matter:

- **Hardware interrupts** — a device (timer, disk, network card, keyboard) signals the CPU asynchronously that it needs attention. The CPU stops what it's doing, looks up a handler in an Interrupt Descriptor Table (on x86) or equivalent structure, and runs it.
- **Exceptions** — synchronous, caused by the currently executing instruction itself (a page fault, a divide-by-zero, an illegal instruction).
- **System calls** — a deliberate, synchronous request from a user-mode program asking the kernel to do something on its behalf (open a file, allocate memory, send data over a socket). Mechanically, a syscall is usually implemented as a special trap instruction (`int 0x80` historically on x86, the faster `syscall`/`sysenter` instructions on modern x86-64) that switches the CPU from user mode to kernel mode and jumps to a fixed entry point.

All three funnel through similar machinery: save the interrupted context, switch to kernel privilege and (usually) a kernel stack, run a handler, then restore context and return. The distinction between "user mode" and "kernel mode" is enforced by the CPU itself via privilege rings (x86) or exception levels (ARM) — it is not just a software convention, which is what makes it a real security boundary rather than a polite agreement between programs. The OSDev Wiki covers both the interrupt mechanics (`https://wiki.osdev.org/Interrupts`) and syscall implementation approaches (`https://wiki.osdev.org/System_Calls`) independent of any one kernel's source.

Once inside the kernel, a syscall handler typically dispatches through a table indexed by syscall number to the actual implementation function — xv6's `kernel/syscall.c` is a compact, readable example of exactly this pattern.

## 5. Concurrency and synchronization

The moment a kernel supports multiple threads (and especially multiple CPU cores), it has to protect shared data structures — the process table, the free page list, filesystem metadata — from simultaneous, conflicting access. Two primitives cover most cases:

- **Spinlocks** — a thread trying to acquire a held lock just loops ("spins") checking it repeatedly until it's free. Cheap when the lock is expected to be held only briefly (as is common inside the kernel itself), wasteful otherwise, since the spinning thread burns CPU doing nothing useful.
- **Sleep locks / mutexes** — a thread that can't acquire the lock gives up the CPU entirely and is woken up later when the lock becomes available. Better for locks that might be held for a while (e.g., during disk I/O), since spinning would waste an entire time slice.

The general hazards these primitives guard against — race conditions (two threads modifying shared state without coordination) and deadlocks (two or more threads each waiting on a resource the other holds) — are the source of a disproportionate share of kernel bugs, precisely because they're often timing-dependent and don't reproduce reliably. This is a large enough topic on its own that it has its own canonical treatment: Chapter 28–32 of *Operating Systems: Three Easy Pieces* (free, `https://pages.cs.wisc.edu/~remzi/OSTEP/`) is widely used as the standard explanation of locks, condition variables, and deadlock, independent of any specific kernel's implementation.

## 6. Filesystems

A filesystem's job is to organize the raw block device (disk, SSD) into named files and directories, and to survive crashes without corrupting data. A few recurring ideas:

- **Inodes** — a filesystem typically stores file metadata (size, permissions, block pointers) separately from filenames. An inode holds the metadata; a directory is just a mapping from names to inode numbers. This separation is why hard links work — multiple directory entries can point at the same inode.
- **Block allocation** — like physical memory, disk blocks need their own free-space tracking (bitmaps are common) and an allocation strategy that tries to keep a file's blocks physically close together to reduce seek time on spinning disks (less relevant, though not irrelevant, on SSDs).
- **Crash consistency** — a write to a file often requires updating multiple on-disk structures (a data block, an inode, a free-space bitmap). If the system crashes between those writes, the filesystem can be left in an inconsistent state. Two common defenses are journaling (writing an intent log before making the real changes, so a crash mid-update can be replayed or rolled back on reboot) and copy-on-write filesystems (like ZFS or Btrfs, which never overwrite data in place, so an in-progress write simply doesn't get linked into the tree if it doesn't complete).

xv6's filesystem (`kernel/fs.c`, `kernel/log.c` in the xv6-riscv repo) implements a minimal inode-based design with a write-ahead log specifically to demonstrate crash consistency in isolation, without the complexity of a production filesystem like ext4 or ZFS around it.

## 7. How Windows, iOS, and Android differ on these same components

The internals above are universal concepts, but real production OSes implement each one differently based on their constraints — desktop responsiveness, battery-powered mobile hardware with no swap, or running on top of an existing kernel.

**Kernel architecture.** Windows NT is a hybrid kernel: most subsystems live in kernel space (`ntoskrnl.exe`), but it retains conceptual roots in microkernel design from its original architecture, and user-mode subsystems (like the Win32 subsystem) sit above it. iOS runs on XNU, Apple's own hybrid kernel that literally fuses two lineages together — a Mach microkernel core (message-passing, tasks, ports) with a BSD layer bolted on top (processes, POSIX APIs, the network stack, filesystem). Apple's own documentation describes it as combining "the Mach kernel developed at Carnegie Mellon University with components from FreeBSD and a C++ API for writing drivers called IOKit." Android, by contrast, doesn't have its own kernel at all — it runs a modified Linux kernel, with Android-specific additions layered in (Binder for IPC, wakelocks for power management, the Low Memory Killer) rather than a from-scratch design.

**Scheduling.** Linux's CFS (used by Android) tracks each thread's accumulated "virtual runtime" in a red-black tree and always runs whichever thread has received the least CPU time, aiming at proportional fairness — useful for servers with many competing workloads. Windows uses a fundamentally different model: a preemptive, priority-based scheduler with 32 priority levels arranged as a multilevel feedback queue, where threads get dynamic priority boosts for interactivity (a thread that just finished waiting on I/O, like reacting to a keypress, gets bumped up temporarily) and gradual priority decay if they hog the CPU. The design goal is different too — Windows optimizes for a responsive foreground GUI thread over strict fairness across all threads. XNU's scheduler (used by iOS and macOS) is also priority-based but layered with Apple's Quality-of-Service (QoS) classes, letting apps mark work as user-interactive, user-initiated, utility, or background, and more recently is aware of asymmetric core layouts (performance vs. efficiency cores on Apple Silicon), a scheduling problem CFS and Windows' MLFQ didn't originally have to solve since they targeted symmetric CPUs.

**Memory management under pressure.** This is where mobile OSes diverge sharply from desktop/server ones. Traditional systems (Linux servers, Windows, macOS) can swap infrequently-used pages out to disk when RAM runs low. iOS devices historically had no swap at all (flash storage made traditional swapping impractical and would wear the storage out), so instead of swapping, XNU runs a mechanism called **Jetsam**: when free memory drops below a threshold, a dedicated kernel thread kills the lowest-priority processes outright, working down a priority-ordered list until enough memory is reclaimed. Android's Linux kernel has an analogous but separately-implemented mechanism, the **Low Memory Killer Daemon (LMKD)**: user-space Android assigns each process an `oom_score_adj` value reflecting how visible/important it is to the user (a foreground app is heavily protected; a cached background app is an easy kill target), and LMKD terminates processes starting from the least important when memory pressure crosses configured thresholds. Windows, running on hardware that generally does have a page file, leans more on traditional demand paging and its working-set trimmer rather than outright killing processes — though Windows will still terminate processes under severe memory pressure, it's not the first-line strategy the way Jetsam and LMKD are on mobile.

**Inter-process communication.** xv6 and most teaching OSes barely need IPC. Production OSes each picked a different primary mechanism: Windows favors named pipes, and RPC/COM for structured cross-process calls; XNU's IPC is built directly on Mach ports and messages, since that's the microkernel heritage the whole kernel is built around; Android layered its own **Binder** IPC mechanism on top of Linux (which normally uses pipes, sockets, or System V IPC) specifically because app-to-app and app-to-system-service communication needed to be fast and to carry object references and security context, which standard Linux IPC wasn't designed for.

**Filesystems.** Windows uses NTFS, with its own journaling, access control lists, and metadata model distinct from the inode design described above. iOS/macOS moved to **APFS**, a copy-on-write filesystem designed for flash storage, with native support for snapshots and space-efficient clones. Android inherits whatever the underlying Linux kernel supports — commonly **ext4** or **F2FS** (a filesystem specifically designed around flash storage's erase-block behavior, unlike ext4 which was designed with spinning disks in mind).

**The throughline.** All three still implement the same conceptual components covered above — some scheduler, some memory reclamation strategy, some IPC mechanism, some filesystem with crash consistency — but the specific choices trace directly back to what the system was built for: Windows for general-purpose desktop/server responsiveness, XNU/iOS for battery-and-flash-constrained mobile hardware with strict interactive latency requirements, and Android for running a mobile experience on top of an unmodified-where-possible Linux base.

## Where to go deeper

- **xv6-riscv** — `https://github.com/mit-pdos/xv6-riscv` — a complete, small, buildable Unix-like kernel; the companion book explaining the source line by line is at `https://github.com/mit-pdos/xv6-riscv-book`.
- **OSDev Wiki** — `https://wiki.osdev.org` — the most comprehensive community reference for hardware-level mechanics (paging, interrupts, ACPI, drivers) independent of any single kernel.
- **Operating Systems: Three Easy Pieces** — `https://pages.cs.wisc.edu/~remzi/OSTEP/` — free textbook organized around the three themes of virtualization (CPU, memory), concurrency, and persistence (storage).
- **Linux kernel documentation** — `https://docs.kernel.org` — for how these same ideas scale up in a production, multi-core, general-purpose kernel (the scheduler docs and memory management docs are both readable without needing the full source tree).
- **The Little Book About OS Development** — `https://littleosbook.github.io/` — a from-scratch x86 tutorial that builds up a minimal kernel step by step, useful as a second worked example alongside xv6.
- **XNU source (mirror)** — `https://github.com/opensource-apple/xnu` — Apple's hybrid Mach/BSD kernel source, for the iOS/macOS side of things.
- **Linux kernel CFS scheduler design docs** — `https://docs.kernel.org/scheduler/sched-design-CFS.html` — the canonical explanation of Linux's (and by extension Android's) default scheduler.
- **Windows thread scheduling** — `https://learn.microsoft.com/en-us/windows/win32/procthread/thread-scheduling` — Microsoft's own documentation on the priority-based, multilevel feedback queue model NT uses.
