﻿using lonos.kernel.core.Scheduling;
using Mosa.Runtime.x86;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lonos.kernel.core
{

    public static class BackgroundWorker
    {

        public static void ThreadMain()
        {
            while (true)
            {
                Scheduler.ResetTerminatedThreads();
                Native.Hlt();
            }
        }

    }

}
