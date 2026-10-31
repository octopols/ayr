using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Ayr.App;
using Ayr.Interop;

namespace Ayr.Hardware
{
    /// <summary>
    /// One of the MSI binaries Ayr needs. Ayr keeps its own copy in vendor\ next to the executables, adopted from the MSI
    /// program that installed it the first time it is found there, so MSI's software can be removed afterwards. A copy is
    /// only used when Windows confirms its signature and the verified signer is the expected publisher.
    /// </summary>
    public sealed class VendorLibrary
    {
        private const string MicroStar = "Micro-Star International CO., LTD.";
        private const string MicrosoftHardwarePublisher = "CN=Microsoft Windows Hardware Compatibility Publisher";
        private const string FoundationServiceName = "MSI Center (MSI NBFoundation Service)";
        private const string SuperChargerName = "MSI Super Charger";

        public static readonly VendorLibrary EcAccess =
            new VendorLibrary("KernCoreLib64.dll", MicroStar, MsiFoundationService.Directory, FoundationServiceName);
        // Newer kernel drivers carry Microsoft's attestation signature rather than MSI's own.
        public static readonly VendorLibrary EcDriver =
            new VendorLibrary("KernCoreLib64.sys", MicrosoftHardwarePublisher, MsiFoundationService.Directory, FoundationServiceName);
        public static readonly VendorLibrary GpuControl =
            new VendorLibrary("GInf.dll", MicroStar, MsiFoundationService.Directory, FoundationServiceName);
        public static readonly VendorLibrary AppleChargeHelper =
            new VendorLibrary("ipadchg2.exe", MicroStar, MsiSuperCharger.Directory, SuperChargerName);
        public static readonly VendorLibrary AppleChargeDriver =
            new VendorLibrary("ipadtst2_64.sys", MicroStar, MsiSuperCharger.Directory, SuperChargerName);

        private static readonly VendorLibrary[] All = { EcAccess, EcDriver, GpuControl, AppleChargeHelper, AppleChargeDriver };

        private readonly string _fileName;
        private readonly string _expectedSigner;
        private readonly string _sourceDirectory;
        private readonly string _sourceProduct;

        private VendorLibrary(string fileName, string expectedSigner, string sourceDirectory, string sourceProduct)
        {
            _fileName = fileName;
            _expectedSigner = expectedSigner;
            _sourceDirectory = sourceDirectory;
            _sourceProduct = sourceProduct;
        }

        public string VendorCopyPath => Path.Combine(AppPaths.VendorDirectory, _fileName);

        public bool IsAvailable => File.Exists(VendorCopyPath) || File.Exists(SourcePath);

        private string SourcePath => Path.Combine(_sourceDirectory, _fileName);

        /// <summary>
        /// Copies every MSI file that is installed but not yet in vendor\. Run at startup, so the files survive MSI's
        /// software being uninstalled before Ayr ever needed them. A file that fails its check is skipped and logged.
        /// </summary>
        public static void AdoptAllAvailable()
        {
            foreach (VendorLibrary library in All)
            {
                if (File.Exists(library.VendorCopyPath) || !File.Exists(library.SourcePath))
                {
                    continue;
                }
                try
                {
                    library.Adopt();
                }
                catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is UnauthorizedAccessException)
                {
                    ActivityLog.Write("could not copy " + library._fileName + ": " + ex.Message);
                }
            }
        }

        /// <summary>Returns the verified copy in vendor\, adopting the installed MSI copy first if there is none yet.</summary>
        public string Locate()
        {
            if (!File.Exists(VendorCopyPath) && File.Exists(SourcePath))
            {
                Adopt();
            }
            if (!File.Exists(VendorCopyPath))
            {
                throw new FileNotFoundException(_fileName + " was not found. Install " + _sourceProduct + " once and run Ayr so it "
                    + "can copy the file; MSI's software can be removed afterwards.", VendorCopyPath);
            }
            if (!IsTrusted(VendorCopyPath))
            {
                throw new InvalidOperationException(_fileName + " in " + AppPaths.VendorDirectory + " is not signed by the expected publisher; it will not be used.");
            }
            return VendorCopyPath;
        }

        private void Adopt()
        {
            if (!IsTrusted(SourcePath))
            {
                throw new InvalidOperationException("MSI's " + _fileName + " failed its signature check; it was not copied.");
            }
            Directory.CreateDirectory(AppPaths.VendorDirectory);
            File.Copy(SourcePath, VendorCopyPath, overwrite: false);
            ActivityLog.Write("copied " + _fileName + " from " + _sourceProduct + " into vendor\\");
        }

        private bool IsTrusted(string path)
        {
            using (X509Certificate2 signer = Wintrust.VerifiedSigner(path))
            {
                return signer != null && signer.Subject.IndexOf(_expectedSigner, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}
