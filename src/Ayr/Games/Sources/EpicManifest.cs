using System.Runtime.Serialization;

namespace Ayr.Games.Sources
{
    /// <summary>The fields Ayr reads from an Epic Games Launcher install manifest (*.item).</summary>
    [DataContract]
    public sealed class EpicManifest
    {
        [DataMember(Name = "DisplayName")]
        public string DisplayName { get; set; }

        [DataMember(Name = "InstallLocation")]
        public string InstallLocation { get; set; }

        [DataMember(Name = "bIsApplication")]
        public bool IsApplication { get; set; }
    }
}
