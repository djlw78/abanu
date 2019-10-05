﻿// This file is part of Lonos Project, an Operating System written in C#. Web: https://www.lonos.io
// Licensed under the GNU 2.0 license. See LICENSE.txt file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Lonos.Kernel.Core.Diagnostics;
using Lonos.Kernel.Core.Interrupts;
using Lonos.Kernel.Core.MemoryManagement;
using Lonos.Kernel.Core.PageManagement;
using Lonos.Kernel.Core.Processes;
using Lonos.Kernel.Core.SysCalls;
using Mosa.Runtime;
using Mosa.Runtime.x86;

#pragma warning disable SA1312 // Variable names should begin with lower-case letter

namespace Lonos.Kernel.Core.Scheduling
{
    public static class Scheduler
    {
        public const int ThreadCapacity = 256;

        public static bool Enabled;
        private static Addr SignalThreadTerminationMethodAddress;

        private static Thread[] Threads;

        private static uint CurrentThreadID;

        private static int clockTicks = 0;

        public static uint ClockTicks => (uint)clockTicks;

        public static void Setup(ThreadStart followupTask)
        {
            Enabled = false;
            Threads = new Thread[ThreadCapacity];
            ThreadsAllocated = 0;
            ThreadsMaxAllocated = 0;
            CurrentThreadID = 0;
            clockTicks = 0;

            for (uint i = 0; i < ThreadCapacity; i++)
            {
                Threads[i] = new Thread() { ThreadID = i };
            }

            SignalThreadTerminationMethodAddress = GetAddress(SignalKernelThreadTerminationMethod);

            CreateThread(ProcessManager.Idle, new ThreadStartOptions(IdleThread) { DebugName = "Idle" }).Start();
            CreateThread(ProcessManager.System, new ThreadStartOptions(followupTask) { DebugName = "KernelMain" }).Start();

            //Debug, for breakpoint
            //clockTicks++;

            //AsmDebugFunction.DebugFunction1();
        }

        public static unsafe void Start()
        {
            SetThreadID(0);
            Enabled = true;

            KernelMessage.WriteLine("Enable Scheduler");
            IDTManager.SetPrivilegeLevel((uint)KnownInterrupt.TerminateCurrentThread, 0x03);
            GDT.Tss->ESP0 = Threads[0].KernelStackBottom;
            GDT.LoadTaskRegister();
            Native.Int((int)KnownInterrupt.ClockTimer);

            // Normally, you should never get here
            Panic.Error("Main-Thread still alive");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void IdleThread()
        {
            while (true)
                Native.Hlt();
        }

        private static void Dummy(uint local)
        {
        }

        public static void ClockInterrupt(IntPtr stackSate)
        {
            Interlocked.Increment(ref clockTicks);

            if (!Enabled)
                return;

            // Save current stack state
            var threadID = GetCurrentThreadID();
            var th = GetCurrentThread();
            if (th != null)
            {
                if (th.Priority != 0)
                {
                    if (th.Priority > 0)
                    {
                        if (++th.PriorityInterrupts <= th.Priority)
                            return;
                    }
                    else
                    {
                        if (--th.PriorityInterrupts >= th.Priority)
                            return;
                    }
                    th.PriorityInterrupts = 0;
                }
            }

            SaveThreadState(threadID, stackSate);

            ScheduleNextThread(threadID);
        }

        private static void ScheduleNextThread(uint currentThreadID)
        {
            var nextThreadID = GetNextThread(currentThreadID);
            SwitchToThread(nextThreadID);
        }

        private static void ThreadEntry(uint threadID)
        {
            var thread = Threads[threadID];
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SignalKernelThreadTerminationMethod()
        {
            // No local variables are allowed, because the stack could be totally empty.
            // Just make a call
            Native.Int((int)KnownInterrupt.TerminateCurrentThread);
        }

        public static void TerminateCurrentThread()
        {
            KernelMessage.WriteLine("Terminating Thread");

            var threadID = GetCurrentThreadID();

            if (threadID != 0)
            {
                TerminateThread(threadID);
            }
        }

        private static void TerminateThread(uint threadID)
        {
            var thread = Threads[threadID];

            if (thread.Status == ThreadStatus.Running)
                thread.Status = ThreadStatus.Terminated;

            var nextThreadID = GetNextThread(thread.ThreadID);
            SwitchToThread(nextThreadID);
        }

        private static uint GetNextThread(uint currentThreadID)
        {
            uint threadID = currentThreadID;

            if (currentThreadID == 0)
                currentThreadID = 1;

            while (true)
            {
                threadID++;

                if (threadID == ThreadCapacity)
                    threadID = 1;

                var thread = Threads[threadID];

                if (thread.ChildThread != null)
                    continue;

                if (thread.CanScheduled)
                    return threadID;

                if (currentThreadID == threadID)
                    return 0; // idle thread
            }
        }

        public static uint GetActiveThreadCount()
        {
            uint n = 0;
            for (var i = 0; i < ThreadCapacity; i++)
                if (Threads[i].CanScheduled)
                    n++;
            return n;
        }

        private static Addr GetAddress(ThreadStart d)
        {
            return (uint)Intrinsic.GetDelegateMethodAddress(d);
        }

        private static object SyncRoot = new object();

        public static uint ThreadsAllocated;
        public static uint ThreadsMaxAllocated;

        public static unsafe Thread CreateThread(Process proc, ThreadStartOptions options)
        {
            Thread thread;
            uint threadID;
            lock (SyncRoot)
            {
                threadID = FindEmptyThreadSlot();

                if (threadID == 0)
                {
                    ResetTerminatedThreads();
                    threadID = FindEmptyThreadSlot();
                }

                thread = Threads[threadID];
                thread.Status = ThreadStatus.Creating;
            }

            // Debug:
            //options.User = false;

            thread.User = proc.User;
            thread.Debug = options.Debug;
            thread.DebugName = options.DebugName;

            var stackSize = options.StackSize;
            var argBufSize = options.ArgumentBufferSize;
            thread.ArgumentBufferSize = options.ArgumentBufferSize;

            var stackPages = KMath.DivCeil(stackSize, PhysicalPageManager.PageSize);

            if (KConfig.Trace.Threads)
                KernelMessage.WriteLine("Requesting {0} stack pages", stackPages);

            var debugPadding = 8u;
            stackSize = stackPages * PhysicalPageManager.PageSize;
            var stack = new Pointer((void*)VirtualPageManager.AllocatePages(stackPages, new AllocatePageOptions { DebugName = "ThreadStack" }));
            PageTable.KernelTable.SetWritable((uint)stack, stackSize);

            if (thread.User && proc.PageTable != PageTable.KernelTable)
                proc.PageTable.MapCopy(PageTable.KernelTable, (uint)stack, stackSize);

            stackSize -= debugPadding;
            var stackBottom = stack + (int)stackSize;

            if (KConfig.Trace.Threads)
                KernelMessage.Write("Create Thread {0}. EntryPoint: {1:X8} Stack: {2:X8}-{3:X8} Type: ", threadID, options.MethodAddr, (uint)stack, (uint)stackBottom - 1);

            if (KConfig.Trace.Threads)
            {
                if (thread.User)
                    KernelMessage.Write("User");
                else
                    KernelMessage.Write("Kernel");
            }

            if (KConfig.Trace.Threads)
            {
                if (thread.DebugName != null)
                    KernelMessage.Write(" Thread DebugName: {0}", thread.DebugName);
                if (thread.Process != null)
                    KernelMessage.WriteLine(" Process: {0}", thread.Process.Path);
            }

            // -- kernel stack
            thread.KernelStackSize = 4 * 4096;
            //thhread.tssAddr = RawVirtualFrameAllocator.RequestRawVirtalMemoryPages(1);
            PageTable.KernelTable.SetWritable(KernelStart.TssAddr, 4096);
            thread.KernelStack = VirtualPageManager.AllocatePages(
                KMath.DivCeil(thread.KernelStackSize, 4096),
                new AllocatePageOptions { DebugName = "ThreadKernelStack" }); // TODO: Decrease Kernel Stack, because Stack have to be changed directly because of multi-threading.
            thread.KernelStackBottom = thread.KernelStack + thread.KernelStackSize;

            if (KConfig.Trace.Threads)
                KernelMessage.WriteLine("tssEntry: {0:X8}, tssKernelStack: {1:X8}-{2:X8}", KernelStart.TssAddr, thread.KernelStack, thread.KernelStackBottom - 1);

            PageTable.KernelTable.SetWritable(thread.KernelStack, 256 * 4096);

            // ---
            uint stackStateOffset = 8;
            stackStateOffset += argBufSize;

            uint cS = 0x08;
            if (thread.User)
                cS = 0x1B;

            var stateSize = thread.User ? IDTTaskStack.Size : IDTStack.Size;

            thread.StackTop = (uint)stack;
            thread.StackBottom = (uint)stackBottom;

            Intrinsic.Store32(stackBottom, 4, 0xFF00001);          // Debug Marker
            Intrinsic.Store32(stackBottom, 0, 0xFF00002);          // Debug Marker

            Intrinsic.Store32(stackBottom, -4, (uint)stackBottom);
            Intrinsic.Store32(stackBottom, -(8 + (int)argBufSize), SignalThreadTerminationMethodAddress.ToInt32());  // Address of method that will raise a interrupt signal to terminate thread

            uint argAddr = (uint)stackBottom - argBufSize;

            IDTTaskStack* stackState = null;
            if (thread.User)
            {
                stackState = (IDTTaskStack*)VirtualPageManager.AllocatePages(1, new AllocatePageOptions { DebugName = "ThreadStackState" });
                if (proc.PageTable != PageTable.KernelTable)
                    proc.PageTable.MapCopy(PageTable.KernelTable, (uint)stackState, IDTTaskStack.Size);
            }
            else
            {
                stackState = (IDTTaskStack*)(stackBottom - 8 - IDTStack.Size); // IDTStackSize is correct - we don't need the Task-Members.
            }
            thread.StackState = stackState;

            if (thread.User && KConfig.Trace.Threads)
                KernelMessage.WriteLine("StackState at {0:X8}", (uint)stackState);

            stackState->Stack.EFLAGS = X86_EFlags.Reserved1;
            if (thread.User)
            {
                // Never set this values for Non-User, otherwise you will override stack informations.
                stackState->TASK_SS = 0x23;
                stackState->TASK_ESP = (uint)stackBottom - (uint)stackStateOffset;

                proc.PageTable.MapCopy(PageTable.KernelTable, thread.KernelStack, thread.KernelStackSize);
                proc.PageTable.MapCopy(PageTable.KernelTable, KernelStart.TssAddr, 4096);
            }
            if (thread.User && options.AllowUserModeIOPort)
            {
                byte IOPL = 3;
                stackState->Stack.EFLAGS = (X86_EFlags)((uint)stackState->Stack.EFLAGS).SetBits(12, 2, IOPL);
            }

            stackState->Stack.CS = cS;
            stackState->Stack.EIP = options.MethodAddr;
            stackState->Stack.EBP = (uint)(stackBottom - (int)stackStateOffset).ToInt32();

            thread.DataSelector = thread.User ? 0x23u : 0x10u;

            lock (proc.Threads)
            {
                thread.Process = proc;
                proc.Threads.Add(thread);
            }

            ThreadsAllocated++;
            if (ThreadsAllocated > ThreadsMaxAllocated)
            {
                ThreadsMaxAllocated = ThreadsAllocated;
                KernelMessage.WriteLine("Threads Max Allocated: {0}. Allocated {0} Active: {1}", ThreadsMaxAllocated, ThreadsAllocated, GetActiveThreadCount());
                if (KConfig.Trace.Threads)
                    Dump();
            }
            else if (KConfig.Trace.Threads)
            {
                KernelMessage.WriteLine("Threads Allocated {0} Active: {1}", ThreadsAllocated, GetActiveThreadCount());
            }
            return thread;
        }

        public static void Dump()
        {
            KernelMessage.WriteLine("Threads, Can scheduled:");
            Dump(true);
            KernelMessage.WriteLine("Non-Schedulable Threads:");
            Dump(false);
        }

        public static void Dump(bool canScheduled)
        {
            for (var i = 0; i < ThreadCapacity; i++)
            {
                var th = Threads[i];
                if (th.CanScheduled == canScheduled && th.Status != ThreadStatus.Empty)
                {
                    KernelMessage.Write("ThreadID={0} Status={1}", th.ThreadID, (uint)th.Status);
                    if (th.DebugName != null)
                        KernelMessage.Write(" DebugName=" + th.DebugName);
                    if (th.Process != null && th.Process.Path != null)
                        KernelMessage.Write(" proc=" + th.Process.Path);
                    if (th.Process != null)
                        KernelMessage.Write(" procID=" + th.Process.ProcessID);

                    if (th.DebugSystemMessage.Target > 0)
                    {
                        var msg = th.DebugSystemMessage;
                        KernelMessage.Write(" Target={0} arg1={1} arg2={2} arg3={3}", (uint)msg.Target, msg.Arg1, msg.Arg2, msg.Arg3);
                    }
                    KernelMessage.Write('\n');
                }
            }
        }

        public static unsafe void SaveThreadState(uint threadID, IntPtr stackState)
        {
            //Assert.True(threadID < MaxThreads, "SaveThreadState(): invalid thread id > max");

            var thread = Threads[threadID];

            if (thread.Status != ThreadStatus.Running)
                return; // New threads doesn't have a stack in use. Take the initial one.

            //Assert.True(thread != null, "SaveThreadState(): thread id = null");

            if (thread.User)
            {
                Assert.IsSet(thread.StackState, "thread.StackState is null");
                *thread.StackState = *(IDTTaskStack*)stackState;
            }
            else
            {
                thread.StackState = (IDTTaskStack*)stackState;
            }

            if (KConfig.Trace.TaskSwitch)
            {
                KernelMessage.Write("Task {0}: Stored ThreadState from {1:X8} stored at {2:X8}, EIP={3:X8}", threadID, (uint)stackState, (uint)thread.StackState, thread.StackState->Stack.EIP);
                if (thread.User)
                    KernelMessage.WriteLine(" ESP={0:X8}", thread.StackState->TASK_ESP);
                else
                    KernelMessage.WriteLine();
            }
        }

        public static Thread GetCurrentThread()
        {
            if (!Enabled)
                return null;

            return Threads[GetCurrentThreadID()];
        }

        public static uint GetCurrentThreadID()
        {
            return CurrentThreadID;

            //return Native.GetFS();
        }

        private static void SetThreadID(uint threadID)
        {
            CurrentThreadID = threadID;

            //Native.SetFS(threadID);
        }

        public static unsafe void SwitchToThread(uint threadID)
        {
            var thread = Threads[threadID];
            var proc = thread.Process;

            if (KConfig.Trace.TaskSwitch)
                KernelMessage.WriteLine("Switching to Thread {0}. StackState: {1:X8}", threadID, (uint)thread.StackState);

            //Assert.True(thread != null, "invalid thread id");

            thread.Ticks++;

            SetThreadID(threadID);

            PIC.SendEndOfInterrupt((int)KnownInterrupt.ClockTimer);

            thread.Status = ThreadStatus.Running;

            if (thread.StackState == null)
            {
                Debug.Break();
            }

            thread.StackState->Stack.EFLAGS |= X86_EFlags.InterruptEnableFlag;

            if (proc.PageTable != PageTable.KernelTable)
                Debug.Nop();

            uint pageDirAddr = proc.PageTable.GetPageTablePhysAddr();
            //KernelMessage.WriteLine("PageDirAddr: {0:X8}", pageDirAddr);
            uint stackStateAddr = (uint)thread.StackState;
            uint dataSelector = thread.DataSelector;

            if (!thread.User)
                thread.StackState = null; // just to be sure

            GDT.Tss->ESP0 = thread.KernelStackBottom;

            if (thread.Debug)
            {
                Native.Nop();
            }

            InterruptReturn(stackStateAddr, dataSelector, pageDirAddr);
        }

        [DllImport("x86/Lonos.InterruptReturn.o", EntryPoint = "InterruptReturn")]
        private static extern void InterruptReturn(uint stackStatePointer, uint dataSegment, uint pageTableAddr);

        private static uint FindEmptyThreadSlot()
        {
            for (uint i = 0; i < ThreadCapacity; i++)
            {
                if (Threads[i].Status == ThreadStatus.Empty)
                    return i;
            }

            return 0;
        }

        public static void ResetTerminatedThreads()
        {
            lock (Threads)
            {
                for (uint i = 0; i < ThreadCapacity; i++)
                {
                    if (Threads[i].Status == ThreadStatus.Terminated)
                    {
                        ResetThread(i);
                    }
                }
            }
        }

        private static unsafe void ResetThread(uint threadID)
        {
            var thread = Threads[threadID];
            lock (thread)
            {
                if (thread.Status != ThreadStatus.Terminated)
                    return;

                if (thread.Process != null)
                {
                    var proc = thread.Process;
                    lock (proc.Threads)
                    {
                        for (var i = 0; i < proc.Threads.Count; i++)
                        {
                            if (proc.Threads[i] == thread)
                                proc.Threads.RemoveAt(i);
                            break;
                        }
                    }
                }

                if (KConfig.Trace.Threads)
                    KernelMessage.WriteLine("Disposing Thread {0}", thread.DebugName);

                thread.FreeMemory();
                ThreadsAllocated--;

                thread.Status = ThreadStatus.Empty;
            }
        }

        public static void SetThreadPriority(int priority)
        {
            var th = GetCurrentThread();
            th.Priority = priority;
        }

    }

}
