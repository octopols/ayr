using Ayr.Games;

namespace Ayr.Ui
{
    /// <summary>Game rows: the user's games, marked while running, and games found on this PC that can be added.</summary>
    public static class GameRowContent
    {
        public const string AddLabel = "Add";
        public const string RemoveLabel = "Remove";
        private const string PlayingBadge = "Playing";

        public static readonly RowContent NoGames = new RowContent("No games yet", "Add a game below");

        public static RowContent Added(GameEntry game, bool running) =>
            new RowContent(game.Name, game.Path, running ? PlayingBadge : null, BadgeIcon.Play, RowButton.Available, RemoveLabel, null);

        public static RowContent Found(GameEntry game) =>
            new RowContent(game.Name, game.Path, null, BadgeIcon.Play, RowButton.Available, AddLabel, null);
    }
}
