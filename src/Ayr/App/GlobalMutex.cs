using System;
using System.Threading;

namespace Ayr.App
{
    public static class GlobalMutex
    {
        public static bool Exists(string name)
        {
            try
            {
                if (!Mutex.TryOpenExisting(name, out Mutex existing))
                {
                    return false;
                }
                existing.Dispose();
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                // Owned by an elevated process, so it cannot be opened from here, but it does exist.
                return true;
            }
        }
    }
}
