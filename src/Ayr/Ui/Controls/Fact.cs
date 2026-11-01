namespace Ayr.Ui.Controls
{
    public sealed class Fact
    {
        public Fact(string label, string value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }
        public string Value { get; }
    }
}
