using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Ayr.Interop;

namespace Ayr.Hardware
{
    /// <summary>
    /// GPU core and memory clock offsets (MHz) through MSI's own NVAPI wrapper GInf.dll, the library Dragon Center's
    /// gpuControl.exe calls. GInf.dll is 64-bit, which is why ayrctl is built for x64.
    /// </summary>
    public sealed class GpuOverclock : IDisposable
    {
        private const string LibraryName = "GInf.dll";

        private IntPtr _module;
        private bool _initialized;

        public GpuOverclock()
        {
            string path;
            try
            {
                path = VendorLibrary.GpuControl.Locate();
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException)
            {
                throw new GpuUnavailableException("GPU offsets are unavailable: " + ex.Message, ex);
            }
            // The altered search path lets GInf.dll resolve its own dependencies from its folder.
            _module = Kernel32.LoadLibraryEx(path, IntPtr.Zero, Kernel32.LoadWithAlteredSearchPath);
            if (_module == IntPtr.Zero)
            {
                var cause = new Win32Exception(Marshal.GetLastWin32Error(), "LoadLibrary failed for " + path);
                throw new GpuUnavailableException("GPU offsets are unavailable: " + cause.Message, cause);
            }
        }

        public GpuInfo Read()
        {
            using (new CrtStdoutMute())
            {
                EnsureInitialized();
                var info = new GpuInfo();
                if (!GetGInfo(ref info))
                {
                    throw new InvalidOperationException("GInf.GetGInfo failed.");
                }
                return info;
            }
        }

        public void Apply(int coreOffsetMhz, int memoryOffsetMhz)
        {
            using (new CrtStdoutMute())
            {
                EnsureInitialized();
                if (!SetGPUOC(coreOffsetMhz))
                {
                    throw new InvalidOperationException("GInf.SetGPUOC(" + coreOffsetMhz + ") failed (needs administrator).");
                }
                if (!SetMemoryOC(memoryOffsetMhz))
                {
                    throw new InvalidOperationException("GInf.SetMemoryOC(" + memoryOffsetMhz + ") failed (needs administrator).");
                }
            }
        }

        public void Dispose()
        {
            if (_initialized)
            {
                using (new CrtStdoutMute())
                {
                    Unload();
                }
                _initialized = false;
            }
            if (_module != IntPtr.Zero)
            {
                Kernel32.FreeLibrary(_module);
                _module = IntPtr.Zero;
            }
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }
            if (!InitialAndGetCurrentG())
            {
                throw new GpuUnavailableException("GPU offsets are unavailable: no NVIDIA GPU or driver answered.");
            }
            _initialized = true;
        }

        // Signatures as Dragon Center declares them (API_Dynamic._Feature.GPU_Control).
        [DllImport(LibraryName)]
        private static extern bool InitialAndGetCurrentG();

        [DllImport(LibraryName)]
        private static extern bool Unload();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool GetGInfo(ref GpuInfo info);

        [DllImport(LibraryName)]
        private static extern bool SetGPUOC(int delta);

        [DllImport(LibraryName)]
        private static extern bool SetMemoryOC(int delta);
    }
}
