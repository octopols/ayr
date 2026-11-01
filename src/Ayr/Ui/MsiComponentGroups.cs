using System;
using Ayr.MsiRemoval;

namespace Ayr.Ui
{
    public static class MsiComponentGroups
    {
        public static MsiComponentGroup Of(MsiComponentKind kind)
        {
            switch (kind)
            {
                case MsiComponentKind.DriverPackage:
                case MsiComponentKind.StoreApp:
                case MsiComponentKind.Program:
                    return MsiComponentGroup.Apps;
                case MsiComponentKind.Service:
                case MsiComponentKind.Driver:
                    return MsiComponentGroup.ServicesAndDrivers;
                case MsiComponentKind.ScheduledTask:
                case MsiComponentKind.StartupEntry:
                case MsiComponentKind.Settings:
                case MsiComponentKind.Folder:
                    return MsiComponentGroup.Leftovers;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown MSI component kind.");
            }
        }

        public static string Heading(MsiComponentGroup group)
        {
            switch (group)
            {
                case MsiComponentGroup.Apps:
                    return "APPS";
                case MsiComponentGroup.ServicesAndDrivers:
                    return "SERVICES AND DRIVERS";
                case MsiComponentGroup.Leftovers:
                    return "LEFTOVERS";
                default:
                    throw new ArgumentOutOfRangeException(nameof(group), group, "Unknown group.");
            }
        }
    }
}
