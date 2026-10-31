namespace Ayr.Ui
{
    public sealed class ModeChange
    {
        public ModeChange(string title, string detail, OsdIcon icon)
        {
            Title = title;
            Detail = detail;
            Icon = icon;
        }

        public string Title { get; }
        public string Detail { get; }
        public OsdIcon Icon { get; }
    }
}
