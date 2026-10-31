using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;

namespace Ayr.Updates
{
    /// <summary>Ayr's releases on GitHub. Only files under the repository's own release downloads are accepted.</summary>
    public sealed class GitHubReleaseFeed
    {
        private const string LatestReleaseApi = "https://api.github.com/repos/octopols/ayr/releases/latest";
        private const string DownloadPrefix = "https://github.com/octopols/ayr/releases/download/";
        private const int MaxResponseBytes = 1024 * 1024;
        private static readonly Regex TagPattern = new Regex(@"^v(\d+)\.(\d+)\.(\d+)$");

        private readonly WebDownloader _web = new WebDownloader();

        /// <summary>The latest release when it is newer than the given version, otherwise null.</summary>
        public ReleaseInfo FindNewerThan(Version current)
        {
            ReleaseInfo latest = FetchLatest();
            return latest.Version > current ? latest : null;
        }

        private ReleaseInfo FetchLatest()
        {
            GitHubRelease release;
            using (var json = new MemoryStream(_web.Get(LatestReleaseApi, MaxResponseBytes)))
            {
                release = (GitHubRelease)new DataContractJsonSerializer(typeof(GitHubRelease)).ReadObject(json);
            }
            Match tag = TagPattern.Match(release.TagName ?? "");
            if (!tag.Success)
            {
                throw new InvalidDataException("The latest release's tag \"" + release.TagName + "\" isn't a version.");
            }
            var version = new Version(Number(tag, 1), Number(tag, 2), Number(tag, 3));
            string package = "Ayr-" + version + "-win-x64.zip";
            return new ReleaseInfo(version, AssetUrl(release, package), AssetUrl(release, package + ".sha256"));
        }

        private static int Number(Match tag, int group) => int.Parse(tag.Groups[group].Value, CultureInfo.InvariantCulture);

        private static string AssetUrl(GitHubRelease release, string name)
        {
            GitHubAsset asset = release.Assets?.FirstOrDefault(candidate => candidate.Name == name)
                ?? throw new InvalidDataException("The latest release has no " + name + ".");
            if (asset.DownloadUrl == null || !asset.DownloadUrl.StartsWith(DownloadPrefix, StringComparison.Ordinal))
            {
                throw new InvalidDataException(name + " isn't hosted in Ayr's own GitHub releases.");
            }
            return asset.DownloadUrl;
        }
    }
}
