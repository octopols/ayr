namespace Ayr.Hardware
{
    public enum FirmwareGeneration
    {
        /// <summary>The firmware has the data blocks Ayr controls the laptop through.</summary>
        Older,

        /// <summary>The firmware lacks them: a newer MSI laptop Ayr can't control yet.</summary>
        Newer,

        /// <summary>Windows wouldn't say, for example without administrator rights.</summary>
        Unknown
    }
}
