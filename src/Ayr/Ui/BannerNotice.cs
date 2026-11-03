using System;

namespace Ayr.Ui
{
    /// <summary>What the window's banner says, and the button that goes with it, if any.</summary>
    public sealed class BannerNotice
    {
        private readonly Action _onAction;

        public BannerNotice(string text) : this(text, null, null)
        {
        }

        public BannerNotice(string text, string actionLabel, Action onAction)
        {
            Text = text;
            ActionLabel = actionLabel;
            _onAction = onAction;
        }

        public string Text { get; }

        /// <summary>Null for a notice that only informs.</summary>
        public string ActionLabel { get; }

        public void Act() => _onAction?.Invoke();

        public static bool SameMessage(BannerNotice first, BannerNotice second) =>
            first?.Text == second?.Text && first?.ActionLabel == second?.ActionLabel;
    }
}
