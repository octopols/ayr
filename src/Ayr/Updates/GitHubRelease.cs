using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Ayr.Updates
{
    /// <summary>The fields Ayr reads from GitHub's "latest release" response; everything else is ignored.</summary>
    [DataContract]
    public sealed class GitHubRelease
    {
        [DataMember(Name = "tag_name")]
        public string TagName { get; set; }

        [DataMember(Name = "assets")]
        public List<GitHubAsset> Assets { get; set; }
    }
}
