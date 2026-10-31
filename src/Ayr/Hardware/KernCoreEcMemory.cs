using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Ayr.App;
using Ayr.Interop;

namespace Ayr.Hardware
{
    /// <summary>
    /// EC RAM through MSI's KernCoreLib64.dll, a rebranded WinIo, and its already-installed WINIO driver. This is the call
    /// MSI Foundation Service makes behind its pipe, done in-process. Requires administrator rights.
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

        public override string Description => "KernCoreLib (WINIO driver)";

        /// <summary>A copy next to ayrctl (vendor\) wins over MSI Foundation Service's own copy.</summary>
        public static string FindLibrary()
        {
            string bundled = Path.Combine(AppPaths.VendorDirectory, LibraryName);
            return File.Exists(bundled) ? bundled : Path.Combine(MsiFoundationService.Directory, LibraryName);
        }

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

        // Loaded once per process and never shut down: WinIo's shutdown can stop the WINIO service that other users of
        // the driver depend on.
        private static void EnsureLoaded()
        {
            lock (LoadLock)
            {
                if (_getPhysLong != null)
                {
                    return;
                }
                string path = FindLibrary();
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException(LibraryName + " not found (needed for Cooler Boost).", path);
                }
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
