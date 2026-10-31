using System;
using System.IO;
using System.Net;

namespace Ayr.Updates
{
    /// <summary>HTTPS downloads with a timeout and a size limit.</summary>
    public sealed class WebDownloader
    {
        // GitHub's API rejects requests without a User-Agent.
        private const string UserAgent = "Ayr-updater";
        private const int ChunkBytes = 81920;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        static WebDownloader()
        {
            // Built without a target-framework attribute, the app runs with .NET 4.0's defaults, which lack TLS 1.2.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public byte[] Get(string url, int maxBytes)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = UserAgent;
            request.Timeout = (int)Timeout.TotalMilliseconds;
            request.ReadWriteTimeout = (int)Timeout.TotalMilliseconds;
            using (WebResponse response = request.GetResponse())
            using (Stream body = response.GetResponseStream())
            using (var content = new MemoryStream())
            {
                var chunk = new byte[ChunkBytes];
                int read;
                while ((read = body.Read(chunk, 0, chunk.Length)) > 0)
                {
                    if (content.Length + read > maxBytes)
                    {
                        throw new InvalidDataException(url + " is larger than expected.");
                    }
                    content.Write(chunk, 0, read);
                }
                return content.ToArray();
            }
        }
    }
}
