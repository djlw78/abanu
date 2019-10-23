﻿// This file is part of Lonos Project, an Operating System written in C#. Web: https://www.lonos.io
// Licensed under the GNU 2.0 license. See LICENSE.txt file in the project root for full license information.

using System;
using Lonos.Kernel.Core.PageManagement;

namespace Lonos.Kernel.Core.Boot
{

    public unsafe struct BootInfoHeader
    {
        // Custom, Random Number. Kernel uses is to detect of BootInfo is available
        public const uint BootInfoMagic = 0x52F8E5E1;

        public uint Magic;

        public BootInfoMemory* MemoryMapArray;
        public uint MemoryMapLength;

        /// <summary>
        /// This is only the Heap for the BootInfo. It's not the Kernel's Heap.
        /// </summary>
        public Addr HeapStart;
        public USize HeapSize;

        public bool VBEPresent;
        public uint VBEMode;

        public uint InstalledPhysicalMemory;
        public PageTableType PageTableType;
        public ulong KernelBootStartCycles;

        public bool FBPresent;
        public BootInfoFramebufferInfo FbInfo;
    }
}
