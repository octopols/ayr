using System;

namespace Ayr.Battery
{
    /// <summary>One setting of a Windows power plan, named by its subgroup and setting GUIDs.</summary>
    public sealed class PowerSetting
    {
        public PowerSetting(string key, Guid subgroup, Guid setting)
        {
            Key = key;
            Subgroup = subgroup;
            Setting = setting;
        }

        /// <summary>A short name for saving the setting's value to a file.</summary>
        public string Key { get; }

        public Guid Subgroup { get; }
        public Guid Setting { get; }
    }
}
