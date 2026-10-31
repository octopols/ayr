using System.Runtime.Serialization;

namespace Ayr.Updates
{
    [DataContract]
    public sealed class GitHubAsset
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "browser_download_url")]
        public string DownloadUrl { get; set; }
    }
}
