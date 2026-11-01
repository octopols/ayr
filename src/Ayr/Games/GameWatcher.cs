using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace Ayr.Games
{
    /// <summary>
    /// Tracks which of the user's games are running. Windows reports every process start and stop as an event, so nothing
    /// is polled; the running processes are listed only at start and when the game list changes.
    /// </summary>
    public sealed class GameWatcher : IDisposable
    {
        private readonly GameLibrary _library = new GameLibrary();
        private readonly object _sync = new object();
        private readonly Dictionary<int, GameEntry> _running = new Dictionary<int, GameEntry>();
        private readonly ManagementEventWatcher _starts = new ManagementEventWatcher(new WqlEventQuery("Win32_ProcessStartTrace"));
        private readonly ManagementEventWatcher _stops = new ManagementEventWatcher(new WqlEventQuery("Win32_ProcessStopTrace"));
        private List<GameEntry> _games = new List<GameEntry>();
        private DateTime _libraryVersion = DateTime.MinValue;

        public GameWatcher()
        {
            _starts.EventArrived += (sender, e) => OnStarted(ProcessIdOf(e));
            _stops.EventArrived += (sender, e) => OnStopped(ProcessIdOf(e));
            // Watching starts before the first listing, so a game launched in between is not missed.
            _starts.Start();
            _stops.Start();
            ReloadIfChanged();
        }

        /// <summary>The name of a running game, or null when none is running.</summary>
        public string ActiveGame
        {
            get
            {
                ReloadIfChanged();
                lock (_sync)
                {
                    return _running.Values.Select(game => game.Name).FirstOrDefault();
                }
            }
        }

        public void Dispose()
        {
            _starts.Stop();
            _stops.Stop();
            _starts.Dispose();
            _stops.Dispose();
        }

        private static int ProcessIdOf(EventArrivedEventArgs e) => Convert.ToInt32(e.NewEvent["ProcessID"]);

        private void ReloadIfChanged()
        {
            DateTime version = _library.Version;
            if (version == _libraryVersion)
            {
                return;
            }
            List<GameEntry> games = _library.Load();
            Dictionary<int, string> processes = ProcessPaths.All();
            lock (_sync)
            {
                _libraryVersion = version;
                _games = games;
                _running.Clear();
                foreach (KeyValuePair<int, string> process in processes)
                {
                    GameEntry game = GameFor(process.Value);
                    if (game != null)
                    {
                        _running[process.Key] = game;
                    }
                }
            }
        }

        private void OnStarted(int processId)
        {
            string path = ProcessPaths.Of(processId);
            if (path == null)
            {
                return;
            }
            lock (_sync)
            {
                GameEntry game = GameFor(path);
                if (game != null)
                {
                    _running[processId] = game;
                }
            }
        }

        private void OnStopped(int processId)
        {
            lock (_sync)
            {
                _running.Remove(processId);
            }
        }

        private GameEntry GameFor(string processPath) => _games.FirstOrDefault(game => game.Matches(processPath));
    }
}
