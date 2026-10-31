using System;
using System.IO;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Ayr.Interop;

namespace Ayr.App
{
    /// <summary>
    /// Gives .NET its own copy of the stdout handle. CrtStdoutMute rebinds the C runtime's stdout, which closes the handle
    /// behind it; without a private copy, Console.Out would be left writing to a closed handle.
    /// </summary>
    public static class IndependentConsole
    {
        private const int Utf8CodePage = 65001;
        private static readonly IntPtr InvalidHandle = new IntPtr(-1);

        public static TextWriter CreateStdout()
        {
            IntPtr original = Kernel32.GetStdHandle(Kernel32.StdOutputHandle);
            IntPtr process = Kernel32.GetCurrentProcess();
            if (original == IntPtr.Zero || original == InvalidHandle
                || !Kernel32.DuplicateHandle(process, original, process, out IntPtr duplicate, 0, false, Kernel32.DuplicateSameAccess))
            {
                return Console.Out;
            }
            var handle = new SafeFileHandle(duplicate, true);
            try
            {
                var writer = new StreamWriter(new FileStream(handle, FileAccess.Write), ConsoleEncoding()) { AutoFlush = true };
                Console.SetOut(writer);
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is UnauthorizedAccessException)
            {
                handle.Dispose();
            }
            return Console.Out;
        }

        // A UTF-8 console must not receive a byte-order mark at the start of the output.
        private static Encoding ConsoleEncoding() =>
            Console.OutputEncoding.CodePage == Utf8CodePage ? new UTF8Encoding(false) : Console.OutputEncoding;
    }
}
