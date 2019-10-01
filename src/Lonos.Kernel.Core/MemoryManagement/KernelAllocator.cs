﻿// This file is part of Lonos Project, an Operating System written in C#. Web: https://www.lonos.io
// Licensed under the GNU 2.0 license. See LICENSE.txt file in the project root for full license information.

using System;
//using pmeta = lonos.test.malloc4.malloc_meta*; //not possible
using System.Runtime.InteropServices;
using Lonos.Kernel.Core.Diagnostics;

#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1300 // Element should begin with upper-case letter
#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
#pragma warning disable SA1303 // Const field names should begin with upper-case letter

#if BITS_64
using malloc_meta = System.UInt64;
using size_t = System.UInt64;
#else
using malloc_meta = System.UInt32;
using size_t = System.UInt32;
#endif

namespace Lonos.Kernel.Core.MemoryManagement
{

    public unsafe class KernelAllocator : Allocator
    {

        protected override void* mmap(uint unknown, uint flags, size_t pages)
        {
            var bytes = pages * 4096;
            var ptr = (byte*)VirtualPageManager.AllocatePages(pages);
            //KernelMessage.WriteLine("mmap: Pages: {0}, Addr: {1:X8}", pages, (uint)ptr);
            for (var i = 0; i < bytes; i++)
                *(ptr + i) = 0;

            return ptr;
        }

        protected override uint munmap(void* addr)
        {
            return 0;
        }

        protected override void malloc_abort(string msg)
        {
            Panic.Error(msg);
        }

    }

}
