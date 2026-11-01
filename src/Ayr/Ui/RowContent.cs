namespace Ayr.Ui
{
    /// <summary>What one list row shows: a name, a detail line, an optional green badge and an optional button.</summary>
    public sealed class RowContent
    {
        public RowContent(string name, string detail)
            : this(name, detail, null, BadgeIcon.Bolt, RowButton.Hidden, null, null)
        {
        }

        public RowContent(string name, string detail, string badge, BadgeIcon badgeIcon, RowButton button, string buttonText, string blockedReason)
        {
            Name = name;
            Detail = detail;
            Badge = badge;
            BadgeIcon = badgeIcon;
            Button = button;
            ButtonText = buttonText;
            BlockedReason = blockedReason;
        }

        public string Name { get; }
        public string Detail { get; }

        /// <summary>Null for no badge.</summary>
        public string Badge { get; }

        public BadgeIcon BadgeIcon { get; }
        public RowButton Button { get; }
        public string ButtonText { get; }
        public string BlockedReason { get; }
    }
}
