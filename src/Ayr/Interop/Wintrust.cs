using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Ayr.Interop
{
    internal static class Wintrust
    {
        private const uint UiNone = 2;
        private const uint RevocationCheckNone = 0;
        private const uint ChoiceFile = 1;
        private const uint StateActionVerify = 1;
        private const uint StateActionClose = 2;
        private static readonly Guid GenericVerifyV2 = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct FileInfo
        {
            public uint Size;
            public string FilePath;
            public IntPtr File;
            public IntPtr KnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TrustData
        {
            public uint Size;
            public IntPtr PolicyCallbackData;
            public IntPtr SipClientData;
            public uint UiChoice;
            public uint RevocationChecks;
            public uint UnionChoice;
            public IntPtr File;
            public uint StateAction;
            public IntPtr StateData;
            public IntPtr UrlReference;
            public uint ProviderFlags;
            public uint UiContext;
            public IntPtr SignatureSettings;
        }

        // Offsets into CRYPT_PROVIDER_SGNR (x64): the certificate chain array follows cbStruct, a FILETIME and csCertChain.
        // Each CRYPT_PROVIDER_CERT starts with cbStruct, padded to 8, followed by the certificate context.
        private const int SignerCertificateChainOffset = 16;
        private const int ChainEntryCertificateOffset = 8;

        [DllImport("wintrust.dll", SetLastError = true)]
        private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);

        [DllImport("wintrust.dll")]
        private static extern IntPtr WTHelperProvDataFromStateData(IntPtr stateData);

        [DllImport("wintrust.dll")]
        private static extern IntPtr WTHelperGetProvSignerFromChain(IntPtr providerData, uint signerIndex,
            [MarshalAs(UnmanagedType.Bool)] bool counterSigner, uint counterSignerIndex);

        /// <summary>
        /// The certificate that signed the file, once Windows has verified the signature and that the file is unmodified;
        /// null otherwise. Files can carry extra, unused certificates, so this asks Windows which one actually signed.
        /// </summary>
        public static X509Certificate2 VerifiedSigner(string path)
        {
            var file = new FileInfo { Size = (uint)Marshal.SizeOf(typeof(FileInfo)), FilePath = path };
            IntPtr filePointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(FileInfo)));
            try
            {
                Marshal.StructureToPtr(file, filePointer, false);
                var data = new TrustData
                {
                    Size = (uint)Marshal.SizeOf(typeof(TrustData)),
                    UiChoice = UiNone,
                    RevocationChecks = RevocationCheckNone,
                    UnionChoice = ChoiceFile,
                    File = filePointer,
                    StateAction = StateActionVerify
                };
                Guid action = GenericVerifyV2;
                int result = WinVerifyTrust(IntPtr.Zero, ref action, ref data);
                try
                {
                    return result == 0 ? SignerOf(data.StateData) : null;
                }
                finally
                {
                    data.StateAction = StateActionClose;
                    WinVerifyTrust(IntPtr.Zero, ref action, ref data);
                }
            }
            finally
            {
                Marshal.DestroyStructure(filePointer, typeof(FileInfo));
                Marshal.FreeHGlobal(filePointer);
            }
        }

        private static X509Certificate2 SignerOf(IntPtr stateData)
        {
            IntPtr providerData = WTHelperProvDataFromStateData(stateData);
            IntPtr signer = providerData == IntPtr.Zero ? IntPtr.Zero : WTHelperGetProvSignerFromChain(providerData, 0, false, 0);
            if (signer == IntPtr.Zero)
            {
                return null;
            }
            IntPtr chain = Marshal.ReadIntPtr(signer, SignerCertificateChainOffset);
            IntPtr certificateContext = chain == IntPtr.Zero ? IntPtr.Zero : Marshal.ReadIntPtr(chain, ChainEntryCertificateOffset);
            // The X509Certificate2 constructor copies the context, so it stays valid after the state data is closed.
            return certificateContext == IntPtr.Zero ? null : new X509Certificate2(certificateContext);
        }
    }
}
