﻿// Copyright (c) Lonos Project. All rights reserved.
// Licensed under the GNU 2.0 license. See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using lonos.Kernel.Core.Scheduling;
using Mosa.Runtime.x86;

namespace lonos.Kernel.Core.Tasks
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
