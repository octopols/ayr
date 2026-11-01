namespace Ayr.MsiRemoval
{
    /// <summary>Told about each component as the removal reaches it and once it is done. Called on the removal's thread.</summary>
    public interface IRemovalProgress
    {
        void Starting(MsiComponent component);

        void Finished(RemovalResult result);
    }
}
