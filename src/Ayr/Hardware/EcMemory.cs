using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Ayr.App;
using Ayr.Interop;

namespace Ayr.Hardware
{
    /// <summary>
    /// Byte access to the embedded controller's RAM, which this board maps at physical 0xFC000800, through MSI's
    /// KernCoreLib64.dll (a rebranded WinIo) and its WINIO driver. These are the calls MSI Foundation Service made, done
    /// in-process; they need administrator rights. The library only does 32-bit access, so a byte write reads the
    /// surrounding dword and writes its other three bytes back unchanged, exactly as Dragon Center's MemRwService does.
    /// </summary>
    public sealed class EcMemory
    {
        private const ulong EcRamBase = 0xFC000800;
        private const uint LowByteMask = 0xFF;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool InitializeKernCoreLibFunction();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool GetPhysLongFunction(UIntPtr physicalAddress, out uint value);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool SetPhysLongFunction(UIntPtr physicalAddress, uint value);

        private static readonly object Access = new object();
        private static GetPhysLongFunction _getPhysLong;
        private static SetPhysLongFunction _setPhysLong;
        private static EcUnavailableException _unavailable;

        public byte ReadByte(byte register)
        {
            lock (Access)
            {
                return (byte)(ReadDword(EcRamBase + register) & LowByteMask);
            }
        }

        public void WriteByte(byte register, byte value)
        {
            ulong address = EcRamBase + register;
            lock (Access)
            {
                uint current = ReadDword(address);
                WriteDword(address, (current & ~LowByteMask) | value);
            }
        }

        private static uint ReadDword(ulong physicalAddress)
        {
            EnsureLoaded();
            if (!_getPhysLong(new UIntPtr(physicalAddress), out uint value))
            {
                throw new IOException("KernCoreLib GetPhysLong failed at 0x" + physicalAddress.ToString("X"));
            }
            return value;
        }

        private static void WriteDword(ulong physicalAddress, uint value)
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

        // Never shut down again: WinIo's shutdown can stop the WINIO service that other users of the driver depend on.
        private static void Load()
        {
            string path = VendorLibrary.EcAccess.Locate();
            KernelDriver.EcAccess.EnsureRunning();
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
                throw new EntryPointNotFoundException(export + " not found in KernCoreLib64.dll.");
            }
            return (T)Marshal.GetDelegateForFunctionPointer(function, typeof(T));
        }
    }
}
