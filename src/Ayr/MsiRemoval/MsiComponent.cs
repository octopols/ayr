namespace Ayr.MsiRemoval
{
    /// <summary>One piece of MSI software found on this PC, and how to remove it.</summary>
    public abstract class MsiComponent
    {
        protected MsiComponent(MsiComponentKind kind, string name, string location)
        {
            Kind = kind;
            Name = name;
            Location = location;
        }

        public MsiComponentKind Kind { get; }
        public string Name { get; }

        /// <summary>Where it lives: a path, a registry key or a package name.</summary>
        public string Location { get; }

        public virtual bool SelectedByDefault => true;

        /// <summary>Throws when it could not be removed; anything already changed stays changed and is backed up.</summary>
        public abstract RemovalOutcome Remove(RegistryBackup backup);
    }
}
