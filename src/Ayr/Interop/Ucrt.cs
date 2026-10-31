using System;
using System.Runtime.InteropServices;

namespace Ayr.Interop
{
    internal static class Ucrt
    {
        public const int StdoutDescriptor = 1;
        public const int OpenWriteOnly = 0x0001;

        [DllImport("ucrtbase.dll", EntryPoint = "_dup", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Duplicate(int descriptor);

        [DllImport("ucrtbase.dll", EntryPoint = "_dup2", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Redirect(int source, int target);

        [DllImport("ucrtbase.dll", EntryPoint = "_open", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern int Open(string path, int flags);

        [DllImport("ucrtbase.dll", EntryPoint = "_close", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Close(int descriptor);

        [DllImport("ucrtbase.dll", EntryPoint = "fflush", CallingConvention = CallingConvention.Cdecl)]
        public static extern int FlushAll(IntPtr stream);
    }
}
