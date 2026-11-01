namespace Ayr.MsiRemoval
{
    public enum RemovalCompletion
    {
        Now,

        /// <summary>Something was in use, so Windows finishes the removal at the next restart.</summary>
        AtRestart
    }
}
