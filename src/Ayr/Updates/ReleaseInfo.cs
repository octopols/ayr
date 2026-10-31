using System;

namespace Ayr.Updates
{
    public sealed class ReleaseInfo
    {
        public ReleaseInfo(Version version, string packageUrl, string checksumUrl)
        {
            Version = version;
            PackageUrl = packageUrl;
            ChecksumUrl = checksumUrl;
        }

        public Version Version { get; }
        public string PackageUrl { get; }
        public string ChecksumUrl { get; }
    }
}
