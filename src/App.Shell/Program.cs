﻿// This file is part of Lonos Project, an Operating System written in C#. Web: https://www.lonos.io
// Licensed under the GNU 2.0 license. See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Lonos.Kernel.Core;
using Lonos.Runtime;
using Mosa.Runtime.x86;

namespace Lonos.Kernel
{

    public static class Program
    {

        public static unsafe void Main()
        {
            ApplicationRuntime.Init();

            //var result = MessageManager.Send(SysCallTarget.ServiceFunc1, 55);

            SysCalls.WriteDebugChar('#');
            SysCalls.WriteDebugChar('#');
            SysCalls.WriteDebugChar('#');

            var targetProcessId = SysCalls.GetProcessIDForCommand(SysCallTarget.OpenFile);
            var buf = SysCalls.RequestMessageBuffer(4096, targetProcessId);
            var kb = SysCalls.OpenFile(buf, "/dev/keyboard");

            while (true)
            {
                SysCalls.ThreadSleep(0);

                //SysCalls.WriteDebugChar('~');
                var gotBytes = SysCalls.ReadFile(kb, buf);
                if (gotBytes > 0)
                {
                    for (var byteIdx = 0; byteIdx < gotBytes; byteIdx++)
                    {
                        var bufPtr = (byte*)buf.Start;
                        var key = bufPtr[byteIdx];
                        var s = key.ToString("x");
                        for (var i = 0; i < s.Length; i++)
                            SysCalls.WriteDebugChar(s[i]);
                        SysCalls.WriteDebugChar(' ');
                    }
                }
                //SysCalls.WriteDebugChar('?');
            }
        }

    }
}
