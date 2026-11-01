namespace Ayr.MsiRemoval
{
    public sealed class RemovalResult
    {
        public RemovalResult(MsiComponent component, RemovalOutcome outcome, string error)
        {
            Component = component;
            Outcome = outcome;
            Error = error;
        }

        public MsiComponent Component { get; }
        public RemovalOutcome Outcome { get; }

        /// <summary>Null unless the outcome is Failed.</summary>
        public string Error { get; }
    }
}
