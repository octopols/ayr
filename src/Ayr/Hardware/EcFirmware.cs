using System;
using System.Linq;
using System.Text;

namespace Ayr.Hardware
{
    /// <summary>
    /// The embedded controller's firmware version, such as "16W1EMS2.103". Registers outside the ones every v1 laptop
    /// shares (charge limit, keyboard light) are only used on firmware known to have them where Ayr expects.
    /// </summary>
    public sealed class EcFirmware
    {
        private const byte VersionAddress = 0xA0;
        private const int VersionLength = 12;

        // The firmware the Linux msi-ec driver lists with the charge limit at 0xEF and the keyboard light at 0xF3
        // (its "G1_7" table: GF63 and GF65 Thin of the 9th and 10th generations).
        private static readonly string[] ChargeAndLightFirmware =
        {
            "16R3EMS1.100", "16R3EMS1.102", "16R3EMS1.104", "16R4EMS1.101", "16R4EMS1.102", "16R4EMS2.101", "16R4EMS2.102",
            "16R5EMS1.101", "16R5EMS1.102", "16W1EMS1.102", "16W1EMS1.103", "16W1EMS1.104", "16W1EMS2.103", "16W2EMS1.101"
        };

        private EcFirmware(string version)
        {
            Version = version;
        }

        public string Version { get; }

        public bool HasChargeLimitAndKeyboardLight => ChargeAndLightFirmware.Contains(Version, StringComparer.OrdinalIgnoreCase);

        public static EcFirmware Read(EcMemory ec)
        {
            var version = new StringBuilder(VersionLength);
            for (int offset = 0; offset < VersionLength; offset++)
            {
                version.Append((char)ec.ReadByte((byte)(VersionAddress + offset)));
            }
            return new EcFirmware(version.ToString().TrimEnd('\0', ' '));
        }
    }
}
