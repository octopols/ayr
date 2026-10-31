using System.Collections.Generic;

namespace Ayr.Hardware
{
    /// <summary>
    /// One of the firmware's ACPI-WMI data blocks (class names from msiapcfg.dll's MOF). Battery, device and power blocks
    /// are read-only here; MSI_Software is writable only at the handshake index, which MSI Foundation Service set at boot.
    /// </summary>
    public sealed class WmiBlock
    {
        public static readonly WmiBlock Software = new WmiBlock("MSI_Software", writableIndex: 0);
        public static readonly WmiBlock Device = new WmiBlock("MSI_Device");
        public static readonly WmiBlock Power = new WmiBlock("MSI_Power");
        public static readonly WmiBlock MasterBattery = new WmiBlock("MSI_Master_Battery");
        public static readonly WmiBlock SlaveBattery = new WmiBlock("MSI_Slave_Battery");
        public static readonly WmiBlock Cpu = new WmiBlock("MSI_CPU", allWritable: true);
        public static readonly WmiBlock Vga = new WmiBlock("MSI_VGA", allWritable: true);
        public static readonly WmiBlock System = new WmiBlock("MSI_System", allWritable: true);
        public static readonly WmiBlock Ap = new WmiBlock("MSI_AP");

        public static readonly IReadOnlyList<WmiBlock> All = new[] { Software, Device, Power, MasterBattery, SlaveBattery, Cpu, Vga, System, Ap };

        private readonly bool _allWritable;
        private readonly int? _writableIndex;

        private WmiBlock(string className, bool allWritable = false, int? writableIndex = null)
        {
            ClassName = className;
            _allWritable = allWritable;
            _writableIndex = writableIndex;
        }

        public string ClassName { get; }

        public bool IsWritable(int index) => _allWritable || _writableIndex == index;

        public override string ToString() => ClassName;
    }
}
