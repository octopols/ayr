using System;
using System.Drawing;

namespace Ayr.Ui
{
    /// <summary>The app's single dark palette and its fonts. Each window owns one and disposes it on close.</summary>
    public sealed class Theme : IDisposable
    {
        public readonly Color Background = Color.FromArgb(12, 13, 17);
        public readonly Color Surface = Color.FromArgb(20, 22, 28);
        public readonly Color SurfaceAlt = Color.FromArgb(32, 34, 41);
        public readonly Color Border = Color.FromArgb(46, 49, 58);
        public readonly Color Text = Color.FromArgb(240, 241, 244);
        public readonly Color TextMuted = Color.FromArgb(150, 154, 166);
        public readonly Color Accent = Color.FromArgb(232, 95, 84);
        public readonly Color AccentText = Color.FromArgb(18, 18, 20);
        public readonly Color Track = Color.FromArgb(40, 43, 51);
        public readonly Color SegmentSelected = Color.FromArgb(44, 47, 56);
        public readonly Color Positive = Color.FromArgb(98, 214, 140);
        public readonly Color PositiveBackground = Color.FromArgb(22, 48, 33);
        public readonly Color NoticeBackground = Color.FromArgb(28, 18, 19);
        public readonly Color NoticeBorder = Color.FromArgb(110, 44, 40);
        public readonly Color CaptionHover = Color.FromArgb(60, Color.White);

        public Theme()
        {
            string text = FontExists("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";
            string display = FontExists("Segoe UI Variable Display") ? "Segoe UI Variable Display" : text;
            Title = new Font(display, 13f, FontStyle.Bold);
            Section = new Font(text, 10f, FontStyle.Bold);
            Body = new Font(text, 9.5f);
            BodyBold = new Font(text, 9.5f, FontStyle.Bold);
            Small = new Font(text, 8.25f);
            Value = new Font(display, 12f, FontStyle.Bold);
        }

        public Font Title { get; }
        public Font Section { get; }
        public Font Body { get; }
        public Font BodyBold { get; }
        public Font Small { get; }
        public Font Value { get; }

        public void Dispose()
        {
            foreach (Font font in new[] { Title, Section, Body, BodyBold, Small, Value })
            {
                font.Dispose();
            }
        }

        private static bool FontExists(string name)
        {
            using (var font = new Font(name, 9f))
            {
                return string.Equals(font.Name, name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
