﻿using System;
using System.Diagnostics;

namespace lonos.Kernel.Core
{
    public delegate void ExceptionHandler(string message);

    public static class Assert
    {

        public delegate void DAssertErrorHandler(string errorMessage);

        static DAssertErrorHandler ErrorHandler;

        public static void Setup(DAssertErrorHandler errorHandler)
        {
            ErrorHandler = errorHandler;
        }

        private static void AssertError(string message)
        {
            if (ErrorHandler == null)
            {
                throw new Exception(message);
            }
            ErrorHandler(message);
        }

        [Conditional("DEBUG")]
        public static void InRange(uint value, uint length)
        {
            if (value >= length)
                AssertError("Out of Range");
        }

        [Conditional("DEBUG")]
        public static void True(bool condition)
        {
            if (!condition)
                AssertError("Assert.True failed");
        }

        [Conditional("DEBUG")]
        public static void True(bool condition, string userMessage)
        {
            if (!condition)
                AssertError(userMessage);
        }

        [Conditional("DEBUG")]
        public static void False(bool condition)
        {
            if (condition)
                AssertError("Assert.False failed");
        }

        [Conditional("DEBUG")]
        public static void False(bool condition, string userMessage)
        {
            if (condition)
                AssertError(userMessage);
        }

        [Conditional("DEBUG")]
        public static void IsSet(uint value, string userMessage)
        {
            if (value == 0)
                AssertError(userMessage);
        }

        [Conditional("DEBUG")]
        public static void IsSet(IntPtr value, string userMessage)
        {
            if (value == IntPtr.Zero)
                AssertError(userMessage);
        }

        [Conditional("DEBUG")]
        public static void IsSet(Addr value, string userMessage)
        {
            if (value == Addr.Zero)
                AssertError(userMessage);
        }

    }
}
