﻿// This file is part of Lonos Project, an Operating System written in C#. Web: https://www.lonos.io
// Licensed under the GNU 2.0 license. See LICENSE.txt file in the project root for full license information.

using System;
using Lonos.Kernel.Core.Boot;
using Lonos.Kernel.Core.Diagnostics;
using Lonos.Kernel.Core.MemoryManagement;
using Lonos.Kernel.Core.PageManagement;
using Mosa.Runtime;

namespace Lonos.Kernel.Core.Devices
{

    public static class DeviceManager
    {

        public static IFile Serial1;
        private static IFile BiosTextScreen;
        private static IFile FrameBufferTextScreen;
        public static ConsoleDevice Console;
        public static IFile Null;
        public static IFile KMsg;

        internal static FrameBuffer Fb;

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

            Fb = new FrameBuffer(BootInfo.Header->FbInfo.FbAddr, BootInfo.Header->FbInfo.FbWidth, BootInfo.Header->FbInfo.FbHeight, BootInfo.Header->FbInfo.FbPitch, 8);
            Fb.Init();

            FrameBufferTextScreen = new FrameBufferTextScreenDevice(Fb);
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