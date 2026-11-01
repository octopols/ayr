using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ayr.Agent;
using Ayr.App;
using Ayr.Games;

namespace Ayr.Cli
{
    /// <summary>"ayrctl game ...": Game mode and the list of games that turn it on.</summary>
    public sealed class GameCommands
    {
        public const string Usage =
@"ayrctl game                          Game mode on/off, your games, which are running, games found on this PC
ayrctl game on|off                   Turbo, GPU overclock and Cooler Boost while one of your games runs
ayrctl game add ""<folder or .exe>""   add a game (an install folder covers every program the game starts)
ayrctl game remove ""<folder or .exe>""  remove a game";

        private const int PathArgumentIndex = 2;

        private readonly TextWriter _output;
        private readonly GameModeController _games = new GameModeController();

        public GameCommands(TextWriter output)
        {
            _output = output;
        }

        public static bool NeedsElevation(string subcommand) => subcommand == "on" || subcommand == "off";

        /// <param name="args">The full command line, with paths in their original case.</param>
        public ExitCode Execute(string subcommand, IReadOnlyList<string> args)
        {
            string path = args.Count == PathArgumentIndex + 1 ? args[PathArgumentIndex] : null;
            switch (subcommand)
            {
                case null:
                case "status":
                    WriteStatus();
                    return ExitCode.Success;
                case "on":
                    _games.SetEnabled(true);
                    Supervisor.EnsureBackgroundInstance();
                    _output.WriteLine("Game mode is on.");
                    WriteStatus();
                    return ExitCode.Success;
                case "off":
                    _games.SetEnabled(false);
                    _output.WriteLine("Game mode is off.");
                    return ExitCode.Success;
                case "add" when path != null:
                    GameEntry added = _games.AddPath(path);
                    _output.WriteLine("Added {0} ({1}).", added.Name, added.Path);
                    return ExitCode.Success;
                case "remove" when path != null:
                    return Remove(path);
                default:
                    _output.WriteLine(Usage);
                    return ExitCode.Usage;
            }
        }

        private ExitCode Remove(string path)
        {
            GameEntry game = _games.FindAdded(path);
            if (game == null)
            {
                _output.WriteLine("{0} isn't in your games.", path);
                return ExitCode.Failure;
            }
            _games.Remove(game);
            _output.WriteLine("Removed {0}.", game.Name);
            return ExitCode.Success;
        }

        private void WriteStatus()
        {
            GameOverview overview = _games.Overview();
            _output.WriteLine("Game mode    : {0}", DesiredState.Load().GameMode ? "on" : "off");
            _output.WriteLine("Your games   : {0}", overview.Added.Count == 0 ? "none" : overview.Added.Count.ToString());
            foreach (GameEntry game in overview.Added)
            {
                string running = overview.Running.Any(game.SamePathAs) ? "  [running]" : "";
                _output.WriteLine("  {0}  {1}{2}", game.Name, game.Path, running);
            }
            if (overview.FoundNotAdded.Count > 0)
            {
                _output.WriteLine("Found on this PC:");
                foreach (GameEntry game in overview.FoundNotAdded)
                {
                    _output.WriteLine("  {0}  {1}", game.Name, game.Path);
                }
            }
        }
    }
}
