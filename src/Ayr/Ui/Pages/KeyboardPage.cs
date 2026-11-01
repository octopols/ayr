using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Ayr.App;
using Ayr.Controllers;
using Ayr.Hardware;
using Ayr.Ui.Controls;

namespace Ayr.Ui.Pages
{
    /// <summary>The keyboard backlight, and what the two Fn keys that reach software do.</summary>
    public sealed class KeyboardPage : WindowPage
    {
        private readonly TitleWithSubtitle _title;
        private readonly Card _lightCard;
        private readonly TitleWithSubtitle _lightRow;
        private readonly Segmented _light;
        private readonly MutedText _lightUnavailable;
        private readonly TitleWithSubtitle _keysTitle;
        private readonly Card _keysCard;
        private readonly Dictionary<FnKey, TitleWithSubtitle> _keyRows = new Dictionary<FnKey, TitleWithSubtitle>();
        private readonly Dictionary<FnKey, Segmented> _keyChoices = new Dictionary<FnKey, Segmented>();
        private readonly Panel _keysDivider;

        private bool _lightAvailable = true;

        public KeyboardPage(Theme theme, IPageHost host) : base(theme, host)
        {
            _title = Add(SectionTitle("Keyboard", null));
            _lightCard = Add(new Card(theme));
            _lightRow = AddTo(_lightCard, Row("Backlight", "The keyboard's own light keys change it too"));
            _light = AddTo(_lightCard, Choices(KeyboardLightController.LevelNames));
            _light.Picked += level => host.Apply(() => host.Backend.ApplyKeyboardLight(level));
            _lightUnavailable = Add(new MutedText(theme, "Backlight control isn't available for this laptop yet.", theme.Background) { Visible = false });

            _keysTitle = Add(SectionTitle("Fn keys", "Performance and Fan mode move to the next mode on each press."));
            _keysCard = Add(new Card(theme));
            foreach (FnKey key in Enum.GetValues(typeof(FnKey)))
            {
                _keyRows[key] = AddTo(_keysCard, Row("Fn + " + key, null));
                Segmented choice = AddTo(_keysCard, new Segmented(theme, FnKeyBindings.Choices.Select(Label).ToList()));
                choice.Picked += index => host.Apply(() => host.Backend.SetFnKeyAction(key, FnKeyBindings.Choices[index]));
                _keyChoices[key] = choice;
            }
            _keysDivider = AddTo(_keysCard, new Panel { BackColor = theme.Border });
        }

        public override string Title => "Keyboard";

        public override IEnumerable<Control> Inputs => new Control[] { _light }.Concat(_keyChoices.Values);

        public override void ShowLive(LiveState live)
        {
            if (live.KeyboardLight.HasValue)
            {
                _light.SelectedIndex = live.KeyboardLight.Value;
            }
            if (live.KeyboardLight.HasValue != _lightAvailable)
            {
                _lightAvailable = live.KeyboardLight.HasValue;
                ContentChanged();
            }
        }

        public override void ShowSlow(SlowState slow)
        {
            foreach (FnKey key in _keyChoices.Keys)
            {
                FnKeyAction action = slow.FnKeys.ActionFor(key);
                _keyChoices[key].SelectedIndex = FnKeyBindings.Choices.IndexOf(action);
                _keyRows[key].SetSubtitle(Explanation(key, action, slow.FnKeys.SharedWithMsi(key)));
            }
        }

        public override void LayoutContent()
        {
            int padding = Scale(24);
            int width = Width - 2 * padding;
            int cardPadding = Scale(CardPadding);
            int inner = width - 2 * cardPadding;
            int rowHeight = Scale(38);
            int y = padding;
            Stack(_title, padding, width, Scale(24), Scale(16), ref y);

            _lightCard.Visible = _lightAvailable;
            _lightUnavailable.Visible = !_lightAvailable;
            if (_lightAvailable)
            {
                LayoutChoiceRow(_lightRow, _light, cardPadding, inner, cardPadding);
                Stack(_lightCard, padding, width, rowHeight + 2 * cardPadding, Scale(24), ref y);
            }
            else
            {
                Stack(_lightUnavailable, padding, width, Scale(24), Scale(24), ref y);
            }

            Stack(_keysTitle, padding, width, Scale(38), Scale(8), ref y);
            int cardY = cardPadding;
            LayoutChoiceRow(_keyRows[FnKey.F7], _keyChoices[FnKey.F7], cardPadding, inner, cardY);
            cardY += rowHeight + Scale(12);
            Stack(_keysDivider, cardPadding, inner, 1, Scale(12), ref cardY);
            LayoutChoiceRow(_keyRows[FnKey.F12], _keyChoices[FnKey.F12], cardPadding, inner, cardY);
            cardY += rowHeight;
            _keysCard.SetBounds(padding, y, width, cardY + cardPadding);
        }

        private void LayoutChoiceRow(TitleWithSubtitle row, Segmented choice, int x, int width, int y)
        {
            int choiceWidth = Scale(340);
            row.SetBounds(x, y, width - choiceWidth - Scale(16), Scale(38));
            choice.SetBounds(x + width - choiceWidth, y + Scale(2), choiceWidth, Scale(34));
        }

        private static string Label(FnKeyAction action)
        {
            switch (action)
            {
                case FnKeyAction.Nothing:
                    return "Nothing";
                case FnKeyAction.OpenAyr:
                    return "Open Ayr";
                case FnKeyAction.NextPerformance:
                    return "Performance";
                case FnKeyAction.NextFanMode:
                    return "Fan mode";
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown Fn key action.");
            }
        }

        private static string Explanation(FnKey key, FnKeyAction action, bool sharedWithMsi)
        {
            if (sharedWithMsi)
            {
                return action == FnKeyAction.Nothing ? "Left to MSI's app, which is still installed" : "MSI's app will react to it too";
            }
            return key == FnKey.F7 ? "Opened MSI Dragon Center before" : "Not used by MSI's apps";
        }
    }
}
