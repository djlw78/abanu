﻿// Copyright (c) Lonos Project. All rights reserved.
// Licensed under the GNU 2.0 license. See LICENSE.txt file in the project root for full license information.

using System;
using lonos.Kernel.Core.Boot;
using lonos.Kernel.Core.Diagnostics;
using lonos.Kernel.Core.MemoryManagement;
using lonos.Kernel.Core.PageManagement;
using Mosa.Kernel.x86;
using Mosa.Runtime;

namespace lonos.Kernel.Core.Devices
{

    public static class DeviceManager
    {

        public static IFile Serial1;
        private static IFile BiosTextScreen;
        private static IFile FrameBufferTextScreen;
        public static ConsoleDevice Console;
        public static IFile Null;
        public static IFile KMsg;

        internal static FrameBuffer fb;

        /// <summary>
        /// Pseudeo devices
        /// </summary>
        public static void InitStage1()
        {
            Null = new NullDevice();
            KMsg = new KernelMessageDevice();
            KernelMessage.SetHandler(KMsg);
        }

        /// <summary>
        /// Output and Debug devices
        /// </summary>
        public static unsafe void InitStage2()
        {
            Serial.SetupPort(Serial.COM1);
            Serial1 = new SerialDevice(Serial.COM1);

            MemoryManagement.PageTableExtensions.SetWritable(PageTable.KernelTable, Screen.ScreenMemoryAddress, Screen.ScreenMemorySize);
            Screen.EarlyInitialization();
            BiosTextScreen = new BiosTextScreenDevice();

            Screen.ApplyMode(BootInfo.Header->VBEMode);

            Console = new ConsoleDevice(BiosTextScreen);
        }

        /// <summary>
        /// Video Stage
        /// </summary>
        public static unsafe void InitFrameBuffer()
        {
            if (!BootInfo.Header->FBPresent || BootInfo.Header->VBEMode < 0x100)
            {
                KernelMessage.Path("fb", "not present");
                return;
            }

            KernelMessage.WriteLine("InitFrameBuffer");

            fb = new FrameBuffer(BootInfo.Header->FbInfo.FbAddr, BootInfo.Header->FbInfo.FbWidth, BootInfo.Header->FbInfo.FbHeight, BootInfo.Header->FbInfo.FbPitch, 8);
            fb.Init();

            FrameBufferTextScreen = new FrameBufferTextScreenDevice(fb);
            Console.SetOutputDevice(FrameBufferTextScreen);
        }

        public static IFile GetDevice(string devName)
        {
            switch (devName)
            {
                case "/dev/ttyS0":
                    return Serial1;
                case "/dev/console":
                    return Console;
                case "/dev/null":
                    return Null;
                case "/dev/kmsg":
                    return KMsg;
                default:
                    return null;
            }
        }

    }
}
