using System;
using System.Collections.Generic;
using System.Linq;

namespace Ayr.MsiRemoval.Components
{
    /// <summary>
    /// Store-signed packages carry a generated publisher ID rather than MSI's name, but the Store prefixes every package
    /// name with the developer's account, "MICRO-STARINTERNATION", so both are checked.
    /// </summary>
    public sealed class StoreAppSource : IMsiComponentSource
    {
        private const char Separator = '\t';

        public IEnumerable<MsiComponent> Find()
        {
            string output = new PowerShellScript(
                "Get-AppxPackage -AllUsers | Where-Object { $_.Publisher -like '*Micro-Star*' -or $_.Name -like '*MICRO-STAR*' } | " +
                "ForEach-Object { $_.Name + [char]9 + $_.PackageFullName }").Run();
            return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split(Separator))
                .Where(parts => parts.Length == 2)
                .Select(parts => new StoreApp(parts[0].Trim(), parts[1].Trim()));
        }
    }
}
