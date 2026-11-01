using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Ayr.Ui.Controls;

namespace Ayr.Ui.Pages
{
    /// <summary>
    /// One page of the main window. It lays its content out to fit the page area exactly, so the window never scrolls,
    /// and reads its data from the window's shared live and slow refreshes.
    /// </summary>
    public abstract class WindowPage : Panel
    {
        protected const int CardPadding = 14;
        protected const int ColumnGap = 24;

        protected WindowPage(Theme theme, IPageHost host)
        {
            Theme = theme;
            Host = host;
            BackColor = theme.Background;
        }

        public abstract string Title { get; }

        /// <summary>Every control that changes something; they are disabled while a change is being applied.</summary>
        public abstract IEnumerable<Control> Inputs { get; }

        protected Theme Theme { get; }
        protected IPageHost Host { get; }

        public virtual void ShowLive(LiveState live)
        {
        }

        public virtual void ShowSlow(SlowState slow)
        {
        }

        /// <summary>Positions the content for the page's current size.</summary>
        public abstract void LayoutContent();

        protected int Scale(int pixels) => Host.Scale(pixels);

        /// <summary>Background results are delivered through the window: a page that was never shown has no handle of its own.</summary>
        protected Control Window => FindForm() ?? (Control)this;

        /// <summary>A hidden page is laid out when it is shown, so only the visible one needs it now.</summary>
        protected void ContentChanged()
        {
            if (Visible)
            {
                LayoutContent();
            }
        }

        protected Segmented Choices<T>(IEnumerable<T> values) =>
            new Segmented(Theme, values.Select(value => value.ToString()).ToList());

        protected TitleWithSubtitle SectionTitle(string title, string subtitle) =>
            new TitleWithSubtitle(Theme, title, subtitle, Theme.Section, Theme.Background);

        protected TitleWithSubtitle Row(string title, string subtitle) =>
            new TitleWithSubtitle(Theme, title, subtitle, Theme.BodyBold, Theme.Surface);

        /// <summary>A switch named after the row it sits in, so a screen reader says what it turns on.</summary>
        protected ToggleSwitch SwitchFor(TitleWithSubtitle row) => new ToggleSwitch(Theme) { AccessibleName = row.Title };

        protected T Add<T>(T control) where T : Control => AddTo(this, control);

        protected static T AddTo<T>(Control parent, T control) where T : Control
        {
            parent.Controls.Add(control);
            return control;
        }

        protected static void Stack(Control control, int x, int width, int height, int gapAfter, ref int y)
        {
            control.SetBounds(x, y, width, height);
            y += height + gapAfter;
        }

        /// <summary>A title and subtitle on the left of a card row, with a toggle on the right.</summary>
        protected void LayoutToggleRow(TitleWithSubtitle row, ToggleSwitch toggle, int x, int width, ref int y)
        {
            row.SetBounds(x, y, width - Scale(60), Scale(38));
            toggle.SetBounds(x + width - Scale(40), y + Scale(8), Scale(40), Scale(22));
            y += Scale(38);
        }
    }
}
