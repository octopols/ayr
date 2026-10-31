using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace Ayr.Hardware
{
    /// <summary>
    /// EC RAM through MSI Foundation Service's named pipe "MSI_SERVICE_2", which works without administrator rights while
    /// that service runs. Protocol recovered from MSI's NamedPipeClientLib and MSIAPService:
    /// frames are 00 FF + big-endian total length + ASCII, encrypted with 3DES-ECB keyed by
    /// MD5("msiservice" + client name + yyyyMMdd).
    /// </summary>
    public sealed class FoundationServiceEcMemory : EcMemory
    {
        private const string PipeName = "MSI_SERVICE_2";
        // The client name the service handshake was verified with; the service derives the session key from it.
        private const string ClientName = "MSIControl";
        private const string ClientVersion = "1.0.0.0";
        private const int ConnectTimeoutMs = 3000;
        private const int FrameHeaderLength = 6;
        private const int CipherBlockLength = 8;
        private const string NotHandshakenReply = "ApplicationNotHandShake";
        private const string ErrorReply = "0;ERR";

        private bool _handshakeDone;

        public override string Description => "MSI Foundation Service pipe";

        protected override uint ReadDword(ulong physicalAddress)
        {
            string reply = Send("IO:RMREQ:" + Invariant(physicalAddress) + ";1");
            string[] parts = reply.Split(';');
            bool failed = parts.Length < 2 || parts[parts.Length - 2].EndsWith("0", StringComparison.Ordinal);
            if (failed || !uint.TryParse(parts[parts.Length - 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint value))
            {
                throw new IOException("EC read at 0x" + physicalAddress.ToString("X") + " failed: " + reply);
            }
            return value;
        }

        protected override void WriteDword(ulong physicalAddress, uint value)
        {
            string reply = Send("IO:WMREQ:" + Invariant(physicalAddress) + ";1;" + Invariant(value));
            string status = reply.Substring(reply.LastIndexOf(':') + 1);
            if (status != "1")
            {
                throw new IOException("EC write at 0x" + physicalAddress.ToString("X") + " was rejected: " + reply);
            }
        }

        private string Send(string command)
        {
            EnsureHandshake();
            byte[] response = Exchange(Encrypt(Frame(command)));
            string plain = Encoding.ASCII.GetString(response);
            if (plain.Contains(NotHandshakenReply) || plain == ErrorReply)
            {
                _handshakeDone = false;
                throw new IOException("MSI Foundation Service rejected the request: " + plain);
            }
            return Unframe(Decrypt(response));
        }

        private void EnsureHandshake()
        {
            if (_handshakeDone)
            {
                return;
            }
            string reply = Unframe(Exchange(Frame("HS:REQ:" + ClientName + ";" + ClientVersion)));
            if (reply.IndexOf(PipeName, StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new IOException("Unexpected handshake reply from MSI Foundation Service: " + reply);
            }
            _handshakeDone = true;
        }

        private static byte[] Exchange(byte[] request)
        {
            using (var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
            {
                try
                {
                    pipe.Connect(ConnectTimeoutMs);
                }
                catch (TimeoutException ex)
                {
                    throw new EcUnavailableException("MSI Foundation Service's pipe is not available; run Ayr as administrator instead.", ex);
                }
                pipe.ReadMode = PipeTransmissionMode.Message;
                pipe.Write(request, 0, request.Length);
                pipe.Flush();
                return ReadMessage(pipe);
            }
        }

        private static byte[] ReadMessage(NamedPipeClientStream pipe)
        {
            using (var message = new MemoryStream())
            {
                var chunk = new byte[4096];
                do
                {
                    int read = pipe.Read(chunk, 0, chunk.Length);
                    if (read == 0)
                    {
                        break;
                    }
                    message.Write(chunk, 0, read);
                }
                while (!pipe.IsMessageComplete);
                return message.ToArray();
            }
        }

        private static byte[] Frame(string payload)
        {
            byte[] body = Encoding.ASCII.GetBytes(payload);
            int total = FrameHeaderLength + body.Length;
            var framed = new byte[total];
            framed[0] = 0x00;
            framed[1] = 0xFF;
            framed[2] = (byte)(total >> 24);
            framed[3] = (byte)(total >> 16);
            framed[4] = (byte)(total >> 8);
            framed[5] = (byte)total;
            Buffer.BlockCopy(body, 0, framed, FrameHeaderLength, body.Length);
            return framed;
        }

        private static string Unframe(byte[] framed)
        {
            bool hasHeader = framed.Length >= FrameHeaderLength && framed[0] == 0x00 && framed[1] == 0xFF;
            if (!hasHeader)
            {
                return Encoding.ASCII.GetString(framed).TrimEnd('\0');
            }
            int total = (framed[2] << 24) | (framed[3] << 16) | (framed[4] << 8) | framed[5];
            int length = Math.Max(0, Math.Min(total, framed.Length) - FrameHeaderLength);
            return Encoding.ASCII.GetString(framed, FrameHeaderLength, length).TrimEnd('\0');
        }

        private static byte[] Encrypt(byte[] data)
        {
            using (TripleDES cipher = CreateCipher())
            using (ICryptoTransform encryptor = cipher.CreateEncryptor())
            {
                return encryptor.TransformFinalBlock(data, 0, data.Length);
            }
        }

        private static byte[] Decrypt(byte[] data)
        {
            using (TripleDES cipher = CreateCipher())
            using (ICryptoTransform decryptor = cipher.CreateDecryptor())
            {
                return decryptor.TransformFinalBlock(data, 0, data.Length - data.Length % CipherBlockLength);
            }
        }

        private static TripleDES CreateCipher()
        {
            string seed = "msiservice" + ClientName + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            using (MD5 md5 = MD5.Create())
            {
                return new TripleDESCryptoServiceProvider
                {
                    Key = md5.ComputeHash(Encoding.ASCII.GetBytes(seed)),
                    Mode = CipherMode.ECB,
                    Padding = PaddingMode.Zeros
                };
            }
        }

        private static string Invariant(ulong number) => number.ToString(CultureInfo.InvariantCulture);
    }
}
