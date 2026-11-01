using System;

namespace Ayr.MsiRemoval.Components
{
    /// <summary>
    /// A Windows software component published by MSI. On newer MSI laptops one of these installs MSI Center through Windows
    /// Update, so it goes first, before it can put back what the rest of the removal takes away.
    /// </summary>
    public sealed class DriverPackage : MsiComponent
    {
        private static readonly TimeSpan DeleteTimeout = TimeSpan.FromMinutes(2);

        private readonly string _infName;

        public DriverPackage(string infName, string name) : base(MsiComponentKind.DriverPackage, name, infName)
        {
            _infName = infName;
        }

        public override RemovalCompletion Remove(RegistryBackup backup)
        {
            new ExternalCommand("pnputil.exe", "/delete-driver " + _infName + " /uninstall /force").RunOrThrow(DeleteTimeout);
            return RemovalCompletion.Now;
        }
    }
}
