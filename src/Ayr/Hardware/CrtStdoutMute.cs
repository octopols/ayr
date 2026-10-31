using System;
using Ayr.Interop;

namespace Ayr.Hardware
{
    /// <summary>
    /// GInf.dll printf()s NVAPI debug lines through the shared C runtime, buffered until process exit. While muted, the C
    /// runtime's stdout points at NUL and its buffer is flushed there before stdout is restored. .NET's Console writes to
    /// the OS handle directly and is not affected.
    /// </summary>
    public sealed class CrtStdoutMute : IDisposable
    {
        private readonly int _savedStdout = -1;
        private readonly int _nul = -1;

        public CrtStdoutMute()
        {
            try
            {
                _savedStdout = Ucrt.Duplicate(Ucrt.StdoutDescriptor);
                _nul = Ucrt.Open("NUL", Ucrt.OpenWriteOnly);
            }
            catch (DllNotFoundException)
            {
                // Without the C runtime GInf.dll could not have loaded either, so there is nothing to mute.
                return;
            }
            if (IsActive)
            {
                Ucrt.Redirect(_nul, Ucrt.StdoutDescriptor);
            }
            else
            {
                CloseIfOpen(_savedStdout);
                CloseIfOpen(_nul);
            }
        }

        private bool IsActive => _savedStdout >= 0 && _nul >= 0;

        public void Dispose()
        {
            if (!IsActive)
            {
                return;
            }
            Ucrt.FlushAll(IntPtr.Zero);
            Ucrt.Redirect(_savedStdout, Ucrt.StdoutDescriptor);
            Ucrt.Close(_savedStdout);
            Ucrt.Close(_nul);
        }

        private static void CloseIfOpen(int descriptor)
        {
            if (descriptor >= 0)
            {
                Ucrt.Close(descriptor);
            }
        }
    }
}
