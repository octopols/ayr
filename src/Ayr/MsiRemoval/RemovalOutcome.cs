namespace Ayr.MsiRemoval
{
    public enum RemovalOutcome
    {
        Removed,

        /// <summary>Something was in use, so Windows finishes the removal at the next restart.</summary>
        RemovedAtRestart,

        Failed
    }
}
