using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Ayr.App;
using Ayr.Interop;

namespace Ayr.Hardware
{
    /// <summary>
    /// EC RAM through MSI's KernCoreLib64.dll, a rebranded WinIo, and its WINIO driver, which Ayr starts when needed. These
    /// are the calls MSI Foundation Service made behind its pipe, done in-process. Requires administrator rights.
    /// </summary>
    public sealed class KernCoreEcMemory : EcMemory
    {
        private const string LibraryName = "KernCoreLib64.dll";

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool InitializeKernCoreLibFunction();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool GetPhysLongFunction(UIntPtr physicalAddress, out uint value);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool SetPhysLongFunction(UIntPtr physicalAddress, uint value);

        private static readonly object LoadLock = new object();
        private static GetPhysLongFunction _getPhysLong;
        private static SetPhysLongFunction _setPhysLong;
        private static EcUnavailableException _unavailable;

        public override string Description => "KernCoreLib (WINIO driver)";

        protected override uint ReadDword(ulong physicalAddress)
        {
            EnsureLoaded();
            if (!_getPhysLong(new UIntPtr(physicalAddress), out uint value))
            {
                throw new IOException("KernCoreLib GetPhysLong failed at 0x" + physicalAddress.ToString("X"));
            }
            return value;
        }

        protected override void WriteDword(ulong physicalAddress, uint value)
        {
            EnsureLoaded();
            if (!_setPhysLong(new UIntPtr(physicalAddress), value))
            {
                throw new IOException("KernCoreLib SetPhysLong failed at 0x" + physicalAddress.ToString("X"));
            }
        }

        // Set up once per process. A failure is remembered rather than retried on every read, so a system without the
        // driver isn't asked to start it every couple of seconds.
        private static void EnsureLoaded()
        {
            lock (LoadLock)
            {
                if (_unavailable != null)
                {
                    throw _unavailable;
                }
                if (_getPhysLong != null)
                {
                    return;
                }
                try
                {
                    Load();
                }
                catch (Exception ex) when (ex is IOException || ex is Win32Exception || ex is InvalidOperationException || ex is EntryPointNotFoundException)
                {
                    _unavailable = new EcUnavailableException("Cooler Boost is unavailable: " + ex.Message, ex);
                    ActivityLog.Write(_unavailable.Message);
                    throw _unavailable;
                }
            }
        }

        // Never shut down again: WinIo's shutdown can stop the WINIO service that other users of the driver depend on.
        private static void Load()
        {
            string path = VendorLibrary.EcAccess.Locate();
            WinIoDriver.EnsureRunning();
            IntPtr module = Kernel32.LoadLibraryEx(path, IntPtr.Zero, Kernel32.LoadWithAlteredSearchPath);
            if (module == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "LoadLibrary failed for " + path);
            }
            if (!Bind<InitializeKernCoreLibFunction>(module, "InitializeKernCoreLib")())
            {
                throw new IOException("InitializeKernCoreLib failed (needs administrator rights and the WINIO driver).");
            }
            _setPhysLong = Bind<SetPhysLongFunction>(module, "SetPhysLong");
            _getPhysLong = Bind<GetPhysLongFunction>(module, "GetPhysLong");
        }

        private static T Bind<T>(IntPtr module, string export) where T : Delegate
        {
            IntPtr function = Kernel32.GetProcAddress(module, export);
            if (function == IntPtr.Zero)
            {
                throw new EntryPointNotFoundException(export + " not found in " + LibraryName + ".");
            }
            return (T)Marshal.GetDelegateForFunctionPointer(function, typeof(T));
        }
    }
}
