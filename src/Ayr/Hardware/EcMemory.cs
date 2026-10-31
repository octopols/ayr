namespace Ayr.Hardware
{
    /// <summary>
    /// Byte access to the embedded controller's RAM, which this board maps at physical 0xFC000800. Both available
    /// backends only do 32-bit physical access, so a byte write reads the surrounding dword and writes its other three
    /// bytes back unchanged, exactly as Dragon Center's MemRwService does.
    /// </summary>
    public abstract class EcMemory
    {
        private const ulong EcRamBase = 0xFC000800;
        private const uint LowByteMask = 0xFF;

        private static readonly object Access = new object();

        public abstract string Description { get; }

        public byte ReadByte(byte register)
        {
            lock (Access)
            {
                return (byte)(ReadDword(EcRamBase + register) & LowByteMask);
            }
        }

        public void WriteByte(byte register, byte value)
        {
            ulong address = EcRamBase + register;
            lock (Access)
            {
                uint current = ReadDword(address);
                WriteDword(address, (current & ~LowByteMask) | value);
            }
        }

        protected abstract uint ReadDword(ulong physicalAddress);

        protected abstract void WriteDword(ulong physicalAddress, uint value);
    }
}
