﻿// Copyright (c) Lonos Project. All rights reserved.
// Licensed under the GNU 2.0 license. See LICENSE.txt file in the project root for full license information.

namespace lonos.Kernel.Core.Scheduling
{
    public enum ThreadStatus
    {
        Empty = 0,
        Creating,
        ScheduleForStart,
        Running,
        Waiting,
        Terminated,
    };
}
