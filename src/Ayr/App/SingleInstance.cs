using System;
using System.Threading;

namespace Ayr.App
{
    /// <summary>One tray per machine, held for the lifetime of the tray process.</summary>
    public sealed class SingleInstance : IDisposable
    {
        private const string MutexName = "Global\\Ayr.Tray";

        private readonly Mutex _mutex;

        private SingleInstance(Mutex mutex)
        {
            _mutex = mutex;
        }

        public static bool IsRunning => GlobalMutex.Exists(MutexName);

        /// <summary>Returns null when another tray won the race.</summary>
        public static SingleInstance TryAcquire()
        {
            var mutex = new Mutex(true, MutexName, out bool created);
            if (created)
            {
                return new SingleInstance(mutex);
            }
            mutex.Dispose();
            return null;
        }

        public void Dispose()
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
