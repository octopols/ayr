using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Ayr.App;
using Ayr.Interop;

namespace Ayr.Hardware
{
    /// <summary>
    /// One of the MSI binaries that ship with Ayr in vendor\ next to the executables, so no MSI software has to be installed.
    /// A file is only used when Windows confirms its signature and the verified signer is the expected publisher.
    /// </summary>
    public sealed class VendorLibrary
    {
        private const string MicroStar = "Micro-Star International CO., LTD.";
        private const string MicrosoftHardwarePublisher = "CN=Microsoft Windows Hardware Compatibility Publisher";

        public static readonly VendorLibrary EcAccess = new VendorLibrary("KernCoreLib64.dll", MicroStar);
        // Newer kernel drivers carry Microsoft's attestation signature rather than MSI's own.
        public static readonly VendorLibrary EcDriver = new VendorLibrary("KernCoreLib64.sys", MicrosoftHardwarePublisher);
        public static readonly VendorLibrary GpuControl = new VendorLibrary("GInf.dll", MicroStar);
        public static readonly VendorLibrary AppleChargeHelper = new VendorLibrary("ipadchg2.exe", MicroStar);
        public static readonly VendorLibrary AppleChargeDriver = new VendorLibrary("ipadtst2_64.sys", MicroStar);
        public static readonly VendorLibrary FirmwareInterface = new VendorLibrary("msiapcfg.dll", MicroStar);

        private readonly string _fileName;
        private readonly string _expectedSigner;

        private VendorLibrary(string fileName, string expectedSigner)
        {
            _fileName = fileName;
            _expectedSigner = expectedSigner;
        }

        public string FilePath => Path.Combine(AppPaths.VendorDirectory, _fileName);

        /// <summary>The verified path, or an error saying the installation is incomplete or has been tampered with.</summary>
        public string Locate()
        {
            if (!File.Exists(FilePath))
            {
                throw new FileNotFoundException(_fileName + " is missing from " + AppPaths.VendorDirectory + "; reinstall Ayr.", FilePath);
            }
            if (!IsTrusted())
            {
                throw new InvalidOperationException(_fileName + " in " + AppPaths.VendorDirectory + " is not signed by the expected publisher; reinstall Ayr.");
            }
            return FilePath;
        }

        private bool IsTrusted()
        {
            using (X509Certificate2 signer = Wintrust.VerifiedSigner(FilePath))
            {
                return signer != null && signer.Subject.IndexOf(_expectedSigner, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}
