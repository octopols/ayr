using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Ayr.App;
using Ayr.Interop;

namespace Ayr.Hardware
{
    /// <summary>
    /// One of the three MSI binaries Ayr needs. Ayr keeps its own copy in vendor\ next to the executables, adopted from MSI
    /// Foundation Service's folder the first time it is found there, so MSI's software can be removed afterwards. A copy
    /// is only used when Windows confirms its signature and the signer is the expected publisher.
    /// </summary>
    public sealed class VendorLibrary
    {
        private const string MicroStar = "O=\"Micro-Star International CO., LTD.\"";
        private const string MicrosoftHardwarePublisher = "CN=Microsoft Windows Hardware Compatibility Publisher";

        public static readonly VendorLibrary EcAccess = new VendorLibrary("KernCoreLib64.dll", MicroStar);
        // Kernel drivers carry Microsoft's attestation signature rather than MSI's own.
        public static readonly VendorLibrary EcDriver = new VendorLibrary("KernCoreLib64.sys", MicrosoftHardwarePublisher);
        public static readonly VendorLibrary GpuControl = new VendorLibrary("GInf.dll", MicroStar);

        private readonly string _fileName;
        private readonly string _expectedSigner;

        private VendorLibrary(string fileName, string expectedSigner)
        {
            _fileName = fileName;
            _expectedSigner = expectedSigner;
        }

        public string Path => System.IO.Path.Combine(AppPaths.VendorDirectory, _fileName);

        private string MsiInstalledPath => System.IO.Path.Combine(MsiFoundationService.Directory, _fileName);

        /// <summary>Returns the verified copy in vendor\, adopting MSI's installed copy first if there is none yet.</summary>
        public string Locate()
        {
            if (!File.Exists(Path) && File.Exists(MsiInstalledPath))
            {
                Adopt();
            }
            if (!File.Exists(Path))
            {
                throw new FileNotFoundException(_fileName + " was not found. Install MSI Center (or MSI NBFoundation Service) once, "
                    + "run Ayr so it can copy the file, and MSI's software can then be removed.", Path);
            }
            if (!IsTrusted(Path))
            {
                throw new InvalidOperationException(_fileName + " in " + AppPaths.VendorDirectory + " is not signed by the expected publisher; it will not be loaded.");
            }
            return Path;
        }

        private void Adopt()
        {
            if (!IsTrusted(MsiInstalledPath))
            {
                throw new InvalidOperationException("MSI's " + _fileName + " failed its signature check; it was not copied.");
            }
            Directory.CreateDirectory(AppPaths.VendorDirectory);
            File.Copy(MsiInstalledPath, Path, overwrite: false);
            ActivityLog.Write("copied " + _fileName + " from MSI Foundation Service into vendor\\");
        }

        private bool IsTrusted(string path)
        {
            using (X509Certificate2 signer = Wintrust.VerifiedSigner(path))
            {
                return signer != null && signer.Subject.IndexOf(_expectedSigner, StringComparison.Ordinal) >= 0;
            }
        }
    }
}
