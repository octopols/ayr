namespace Ayr.MsiRemoval
{
    /// <summary>
    /// In removal order: the component that reinstalls MSI's apps first, then the apps, then what they leave behind.
    /// </summary>
    public enum MsiComponentKind
    {
        DriverPackage,
        StoreApp,
        Program,
        Service,
        Driver,
        ScheduledTask,
        StartupEntry,
        Settings,
        Folder
    }
}
