﻿using Lonos.Kernel.Core;

namespace Lonos.os.Core.x86
{
    public static class Start
    {
        public static void Main()
        {
            Kernel.Core.KernelStart.Main();
            Kernel.Core.x86.DummyClass.DummyCall();
            while (true)
            { }
        }
    }
}
