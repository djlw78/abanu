﻿using System;
using Mosa.Runtime;

namespace lonos.kernel.core
{

    internal static class Start
    {

        public unsafe static void Main()
        {
            BootInfo.SetupStage1();

            // Field needs to be explicit set, because InitializeAssembly is not invoked yet.
            Memory.InitialKernelProtect();

            ManagedMemoy.InitializeGCMemory();
            Mosa.Runtime.StartUp.InitializeAssembly();
            //Mosa.Runtime.StartUp.InitializeRuntimeMetadata();

            ApiContext.Current = new ApiHost();

            // Setup some pseudo devices
            Devices.InitStage1();

            //Setup Output and Debug devices
            Devices.InitStage2();

            // Write first output
            KernelMessage.WriteLine("<KERNEL:CONSOLE:BEGIN>");
            KernelMessage.WriteLine("Starting Lonos Kernel...");

            KernelMessage.WriteLine("KConfig.UseKernelMemoryProtection: {0}", KConfig.UseKernelMemoryProtection);
            KernelMessage.WriteLine("KConfig.UsePAE: {0}", KConfig.UsePAE);
            KernelMessage.WriteLine("Apply PageTableType: {0}", (uint)BootInfo.Header->PageTableType);

            ulongtest1();
            ulongtest2();

            // Detect environment (Memory Maps, Video Mode, etc.)
            BootInfo.SetupStage2();

            KernelMemoryMapManager.Setup();
            //KernelMemoryMapManager.Allocate(0x1000 * 1000, BootInfoMemoryType.PageDirectory);

            // Read own ELF-Headers and Sections
            KernelElf.Setup();

            // Initialize the embedded code (actually only a little proof of conecept code)
            NativeCalls.Setup();

            //InitialKernelProtect();

            PageFrameManager.Setup();

            KernelMessage.WriteLine("free: {0}", PageFrameManager.PagesAvailable);
            PageFrameManager.AllocatePages(PageFrameRequestFlags.Default, 10);
            KernelMessage.WriteLine("free: {0}", PageFrameManager.PagesAvailable);
            RawVirtualFrameAllocator.Setup();

            Memory.Setup();

            // Now Memory Sub System is working. At this point it's valid
            // to allocate memory dynamicly

            Devices.InitFrameBuffer();

            // Setup Programmable Interrupt Table
            PIC.Setup();

            // Setup Interrupt Descriptor Table
            // Important Note: IDT depends on GDT. Never setup IDT before GDT.
            IDTManager.Setup();

            KernelMessage.WriteLine("Initialize Runtime Metadata");
            Mosa.Runtime.StartUp.InitializeRuntimeMetadata();

            var tssAddr = RawVirtualFrameAllocator.RequestRawVirtalMemoryPages(1);
            Memory.InitialKernelProtect_MakeWritable_BySize(tssAddr, 4096);
            GDT.SetupUserMode(tssAddr);

            Scheduler.Setup();
            Scheduler.CreateThread(Thread1, 0x4000);
            Scheduler.CreateThread(Thread2, 0x4000);
            Scheduler.Start();

            KernelMessage.WriteLine("Performing some tests");
            Tests();

            KernelMessage.WriteLine("Enter Main Loop");
            AppMain();
        }

        private static void Thread1()
        {
            uint i = 0;
            while (true)
            {
                i++;
                //if (Scheduler.ClockTicks % 100 == 0)
                Screen.Goto(0, 0);
                Screen.Write("TH1:");
                Screen.Write(i, 10);
            }
        }

        private static void Thread2()
        {
            uint i = 0;
            while (i < 100)
            {
                i++;
                //if (Scheduler.ClockTicks % 100 == 0)
                Screen.Goto(1, 0);
                Screen.Write("TH2:");
                Screen.Write(i, 10);
            }
        }

        // public unsafe static void InitialKernelProtect()
        // {
        //     KernelMessage.WriteLine("Protecting Memory...");

        //     // PageDirectoryEntry* pde = (PageDirectoryEntry*)AddrPageDirectory;
        //     // for (int index = 0; index < 1024; index++)
        //     // {
        //     // 	pde[index].Writable = false;
        //     // }

        //     // PageTable.PageTableEntry* pte = (PageTable.PageTableEntry*)PageTable.AddrPageTable;
        //     // for (int index = 0; index < 1024 * 32; index++)
        //     // 	pte[index].Writable = false;

        //     // InitialKernelProtect_MakeWritable_ByRegion(0, 90 * 1024 * 1024);

        //     KernelMessage.WriteLine("Reload CR3 to {0:X8}", PageTable.AddrPageDirectory);
        //     Native.SetCR3(PageTable.AddrPageDirectory);
        //     //Native.Invlpg();
        //     KernelMessage.WriteLine("Protecting Memory done");
        // }

        // public unsafe static void InitialKernelProtect_MakeWritable_ByRegion(uint startVirtAddr, uint endVirtAddr)
        // {
        //     InitialKernelProtect_MakeWritable_BySize(startVirtAddr, endVirtAddr - startVirtAddr);
        // }

        // public unsafe static void InitialKernelProtect_MakeWritable_BySize(uint virtAddr, uint size)
        // {
        //     var pages = KMath.DivCeil(size, 4096);
        //     for (var i = 0; i < pages; i++)
        //     {
        //         var entry = PageTable.GetTableEntry(virtAddr);
        //         entry->Writable = true;
        //     }
        // }

        static void ulongtest1()
        {
            uint mask = 0x00004000;
            uint v1 = 0x00000007;
            uint r1 = v1.SetBits(12, 52, mask, 12); //52 with uint makes no sense, but this doesnt matter in this case, the result just works as expected. It works correct with count<32, too, of course.
                                                    // r1 =  00004007
            ulong v2 = v1;
            ulong r2 = v2.SetBits(12, 52, mask, 12);
            uint r2Int = (uint)r2;
            // r2Int = 00000007. This is wrong. It should be the same as r1.

            KernelMessage.WriteLine("bla1: {0:X8}", r1);
            KernelMessage.WriteLine("bla2: {0:X8}", r2Int);
        }

        static unsafe void ulongtest2()
        {
            ulong addr = 0x0000000019ad000;
            ulong data = 40004005;
            ulong result = data.SetBits(12, 52, addr, 12);

            var rAddr = (uint*)(void*)&result;
            var r1 = rAddr[0];
            var r2 = rAddr[1];

            KernelMessage.WriteLine("r1: {0:X8}", r1);
            KernelMessage.WriteLine("r2: {0:X8}", r2);
        }

        public static void Tests()
        {
            var ar = new KList<uint>(sizeof(uint));
            ar.Add(44);
            ar.Add(55);
            KernelMessage.WriteLine("CNT: {0}", ManagedMemoy.AllocationCount);
            foreach (var num in ar)
            {
                KernelMessage.WriteLine("VAL: {0}", num);
            }
            KernelMessage.WriteLine("CNT: {0}", ManagedMemoy.AllocationCount);
            ar.Destroy();

            KernelMessage.WriteLine("Pages free: {0}", PageFrameManager.PagesAvailable);

            for (var i = 0; i < 10000; i++)
            {
                var s = new int[] { 1, 2, 3, 4, };
                s[1] = 5;
                Memory.FreeObject(s);
            }
            KernelMessage.WriteLine("Pages free: {0}", PageFrameManager.PagesAvailable);
            //Memory.FreeObject(s);


        }

        public static void AppMain()
        {
            // We have nothing todo (yet). So let's stop.
            Debug.Break();
        }

        private static void Dummy()
        {
            //This is a dummy call, that get never executed.
            //Its requied, because we need a real reference to Mosa.Runtime.x86
            //Without that, the .NET compiler will optimize that reference away
            //if its nowhere used. Than the Compiler dosnt know about that Refernce
            //and the Compilation will fail
            Mosa.Runtime.x86.Internal.GetStackFrame(0);
        }

        public const uint Columns = 80;

        /// <summary>
        /// The rows
        /// </summary>
        public const uint Rows = 40;

        public static void RawWrite(uint row, uint column, char chr, byte color)
        {
            IntPtr address = new IntPtr(0x0B8000 + ((row * Columns + column) * 2));

            Intrinsic.Store8(address, (byte)chr);
            Intrinsic.Store8(address, 1, color);
        }

    }
}
