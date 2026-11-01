namespace Ayr.MsiRemoval
{
    public sealed class RemovalResult
    {
        private RemovalResult(MsiComponent component, RemovalOutcome outcome, string error)
        {
            Component = component;
            Outcome = outcome;
            Error = error;
        }

        public MsiComponent Component { get; }
        public RemovalOutcome Outcome { get; }

        /// <summary>Null unless the outcome is Failed.</summary>
        public string Error { get; }

        public static RemovalResult Succeeded(MsiComponent component, RemovalCompletion completion) =>
            new RemovalResult(component, completion == RemovalCompletion.AtRestart ? RemovalOutcome.RemovedAtRestart : RemovalOutcome.Removed, null);

        public static RemovalResult Failed(MsiComponent component, string error) =>
            new RemovalResult(component, RemovalOutcome.Failed, error);
    }
}
