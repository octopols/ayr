namespace Ayr.MsiRemoval.Components
{
    /// <summary>
    /// A Microsoft Store package, such as MSI Center or the Dragon Center app. It is removed for every user and also
    /// unprovisioned, so Windows does not install it again for the next new account.
    /// </summary>
    public sealed class StoreApp : MsiComponent
    {
        private readonly string _packageFullName;
        private readonly string _packageName;

        public StoreApp(string packageName, string packageFullName)
            : base(MsiComponentKind.StoreApp, FriendlyName(packageName), packageFullName)
        {
            _packageName = packageName;
            _packageFullName = packageFullName;
        }

        public override RemovalCompletion Remove(RegistryBackup backup)
        {
            new PowerShellScript(
                "Remove-AppxPackage -Package " + PowerShellScript.Literal(_packageFullName) + " -AllUsers; " +
                "Get-AppxProvisionedPackage -Online | Where-Object DisplayName -eq " + PowerShellScript.Literal(_packageName) +
                " | Remove-AppxProvisionedPackage -Online -AllUsers | Out-Null").Run();
            return RemovalCompletion.Now;
        }

        // Package names look like "9426MICRO-STARINTERNATION.DragonCenter"; the part after the dot is the app's name.
        private static string FriendlyName(string packageName)
        {
            int dot = packageName.LastIndexOf('.');
            return dot < 0 ? packageName : packageName.Substring(dot + 1);
        }
    }
}
