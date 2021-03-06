﻿// This file is part of Abanu, an Operating System written in C#. Web: https://www.abanu.org
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

namespace Abanu.Kernel.Core.Processes
{
    public struct ProcessCreateOptions
    {
        /// <summary>
        /// Determines if the Process should run with user or kernel privileges
        /// </summary>
        public bool User;
    }

}
