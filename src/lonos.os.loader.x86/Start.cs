﻿using Lonos.Kernel.Loader;

namespace Lonos.os.Loader.x86
{
    public static class Start
    {
        public static void Main()
        {
            Kernel.Loader.LoaderStart.Main();
            Kernel.Loader.x86.DummyClass.DummyCall();
            while (true)
            { }
        }
    }
}
