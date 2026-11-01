using System;
using System.Globalization;
using System.Linq;
using System.Management;

namespace Ayr.Hardware
{
    /// <summary>
    /// Reads and writes the firmware's ACPI-WMI blocks through the in-box wmiacpi.sys. Each block is a list of one-byte
    /// instances addressed by enumeration order, the way Dragon Center's pre-Tiger-Lake path addresses them.
    /// </summary>
    public sealed class WmiBlockClient
    {
        private const string Namespace = "root\\WMI";
        private static readonly string[] StandardProperties = { "InstanceName", "Active" };

        public byte[] ReadBlock(WmiBlock block) => Query(block).Select(ReadPayload).ToArray();

        public byte ReadByte(WmiBlock block, int index) => ReadPayload(Instance(block, index));

        /// <summary>The whole value, for blocks such as the battery's that hold numbers larger than a byte.</summary>
        public int ReadNumber(WmiBlock block, int index)
        {
            ManagementObject instance = Instance(block, index);
            return Convert.ToInt32(instance[PayloadProperty(instance)], CultureInfo.InvariantCulture);
        }

        public void WriteByte(WmiBlock block, int index, byte value)
        {
            if (!block.IsWritable(index))
            {
                throw new InvalidOperationException(block + "[" + index + "] is not writable.");
            }
            ManagementObject instance = Instance(block, index);
            instance.SetPropertyValue(PayloadProperty(instance), value);
            instance.Put();
        }

        private static ManagementObject Instance(WmiBlock block, int index)
        {
            ManagementObject instance = Query(block).Skip(index).FirstOrDefault();
            if (instance == null)
            {
                throw new ArgumentOutOfRangeException(nameof(index), block + " has no instance " + index + ".");
            }
            return instance;
        }

        private static ManagementObject[] Query(WmiBlock block)
        {
            ManagementObject[] instances;
            try
            {
                using (var searcher = new ManagementObjectSearcher(Namespace, "SELECT * FROM " + block.ClassName))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    instances = results.Cast<ManagementObject>().ToArray();
                }
            }
            catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.InvalidClass || ex.ErrorCode == ManagementStatus.NotFound)
            {
                throw MissingFirmwareInterface(block, ex);
            }
            if (instances.Length == 0)
            {
                throw MissingFirmwareInterface(block, null);
            }
            return instances;
        }

        private static InvalidOperationException MissingFirmwareInterface(WmiBlock block, Exception cause) =>
            new InvalidOperationException("MSI's firmware interface (" + block + ") isn't available. Start the Ayr tray app once to "
                + "set it up, then restart Windows. Ayr only works on MSI laptops.", cause);

        private static byte ReadPayload(ManagementObject instance) =>
            (byte)(Convert.ToInt32(instance[PayloadProperty(instance)], CultureInfo.InvariantCulture) & 0xFF);

        // Every block has the standard InstanceName/Active pair plus exactly one payload property (MSI_CPU -> "CPU").
        private static string PayloadProperty(ManagementObject instance)
        {
            PropertyData payload = instance.Properties.Cast<PropertyData>().FirstOrDefault(property => !StandardProperties.Contains(property.Name));
            if (payload == null)
            {
                throw new InvalidOperationException(instance.ClassPath.ClassName + " has no payload property.");
            }
            return payload.Name;
        }
    }
}
