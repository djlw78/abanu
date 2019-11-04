﻿// This file is part of Abanu, an Operating System written in C#. Web: https://www.abanu.org
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abanu.Kernel.Core.Scheduling;
using Mosa.Runtime.x86;

namespace Abanu.Kernel.Core.Tasks
{

    /// <summary>
    /// Kernel background worker.
    /// By design, a kernel is a service and should not doing stuff at his own.
    /// Use background task carefully. Don't waste CPU cycles.
    /// </summary>
    public static class BackgroundWorker
    {

        public static void ThreadMain()
        {
            while (true)
            {
                Scheduler.ResetTerminatedThreads();
                Scheduler.Sleep(0);
            }
        }

    }

}
