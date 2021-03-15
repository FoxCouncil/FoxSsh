//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FoxSsh.Common.Messages
{
    public class KeyExchangeInitializationMessage : ISshMessage
    {
        private const int CookieSize = 16;

        public SshMessageType Type => SshMessageType.KeyExchangeInitialization;

        public IReadOnlyCollection<byte> Raw { get; set; }

        public IEnumerable<byte> Cookie { get; private set; } = new byte[CookieSize];

        public IEnumerable<string> KeyExchangeAlgorithms { get; set; }

        public IEnumerable<string> ServerHostKeyAlgorithms { get; set; }

        public IEnumerable<string> EncryptionAlgorithmsClientToServer { get; set; }

        public IEnumerable<string> EncryptionAlgorithmsServerToClient { get; set; }

        public IEnumerable<string> HmacAlgorithmsClientToServer { get; set; }

        public IEnumerable<string> HmacAlgorithmsServerToClient { get; set; }

        public IEnumerable<string> CompressionAlgorithmsClientToServer { get; set; }

        public IEnumerable<string> CompressionAlgorithmsServerToClient { get; set; }

        public IEnumerable<string> LanguagesClientToServer { get; set; }

        public IEnumerable<string> LanguagesServerToClient { get; set; }

        public bool FirstPacketFollows { get; set; }

        public uint Reserved { get; set; }

        public KeyExchangeInitializationMessage()
        {
            SshCore.Rng.GetBytes((byte[])Cookie);
        }

        public static ISshMessage Default()
        {
            var message = new KeyExchangeInitializationMessage
            {
                KeyExchangeAlgorithms = SshCore.KeyExchangeAlgorithms.Keys.ToArray(),
                ServerHostKeyAlgorithms = SshCore.PublicKeyAlgorithms.Keys.ToArray(),
                EncryptionAlgorithmsClientToServer = SshCore.EncryptionAlgorithms.Keys.ToArray(),
                EncryptionAlgorithmsServerToClient = SshCore.EncryptionAlgorithms.Keys.ToArray(),
                HmacAlgorithmsClientToServer = SshCore.HmacAlgorithms.Keys.ToArray(),
                HmacAlgorithmsServerToClient = SshCore.HmacAlgorithms.Keys.ToArray(),
                CompressionAlgorithmsClientToServer = SshCore.CompressionAlgorithms.Keys.ToArray(),
                CompressionAlgorithmsServerToClient = SshCore.CompressionAlgorithms.Keys.ToArray(),
                LanguagesClientToServer = new[] { "" },
                LanguagesServerToClient = new[] { "" },
                FirstPacketFollows = false,
                Reserved = 0
            };

            return message;
        }

        public void LoadRawData(SshDataStream stream)
        {
            Cookie = stream.ReadBinary(CookieSize);

            KeyExchangeAlgorithms = stream.ReadString(Encoding.ASCII).Split(',');
            ServerHostKeyAlgorithms = stream.ReadString(Encoding.ASCII).Split(',');
            EncryptionAlgorithmsClientToServer = stream.ReadString(Encoding.ASCII).Split(',');
            EncryptionAlgorithmsServerToClient = stream.ReadString(Encoding.ASCII).Split(',');
            HmacAlgorithmsClientToServer = stream.ReadString(Encoding.ASCII).Split(',');
            HmacAlgorithmsServerToClient = stream.ReadString(Encoding.ASCII).Split(',');
            CompressionAlgorithmsClientToServer = stream.ReadString(Encoding.ASCII).Split(',');
            CompressionAlgorithmsServerToClient = stream.ReadString(Encoding.ASCII).Split(',');
            LanguagesClientToServer = stream.ReadString(Encoding.ASCII).Split(',');
            LanguagesServerToClient = stream.ReadString(Encoding.ASCII).Split(',');

            FirstPacketFollows = stream.ReadBoolean();
            Reserved = stream.ReadUInt32();
        }

        public void WriteRawData(SshDataStream stream)
        {
            stream.Write(Cookie);

            stream.Write(string.Join(",", KeyExchangeAlgorithms), Encoding.ASCII);
            stream.Write(string.Join(",", ServerHostKeyAlgorithms), Encoding.ASCII);
            stream.Write(string.Join(",", EncryptionAlgorithmsClientToServer), Encoding.ASCII);
            stream.Write(string.Join(",", EncryptionAlgorithmsServerToClient), Encoding.ASCII);
            stream.Write(string.Join(",", HmacAlgorithmsClientToServer), Encoding.ASCII);
            stream.Write(string.Join(",", HmacAlgorithmsServerToClient), Encoding.ASCII);
            stream.Write(string.Join(",", CompressionAlgorithmsClientToServer), Encoding.ASCII);
            stream.Write(string.Join(",", CompressionAlgorithmsServerToClient), Encoding.ASCII);
            stream.Write(string.Join(",", LanguagesClientToServer), Encoding.ASCII);
            stream.Write(string.Join(",", LanguagesServerToClient), Encoding.ASCII);
            stream.Write(FirstPacketFollows);
            stream.Write(Reserved);
        }
    }
}