﻿namespace lonos.Kernel.Core.Scheduling
{
    public enum ThreadStatus
    {
        Empty = 0,
        Creating,
        Created,
        Running,
        Terminated,
        Waiting
    };
}
