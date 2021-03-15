//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using FoxSsh.Common.Compression;
using FoxSsh.Common.Crypto;
using FoxSsh.Server;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace FoxSsh.Common
{
    public static class SshCore
    {
        internal static class ChannelRequestNames 
        {
            public const string PtyRequest = "pty-req";
        }

        public const byte CarriageReturn = 0x0d;

        public const byte LineFeed = 0x0a;

        public const int DefaultPort = 22;

        public const int LocalChannelDataPacketSize = 1024 * 32;

        public const int MaximumSshPacketSize = LocalChannelDataPacketSize;

        public const int InitialLocalWindowSize = LocalChannelDataPacketSize * 32;

        public const string ProductName = "FoxSsh";

        public const string ServerVersion = "SSH-2.0-" + ProductName + "Server";

        public const string ClientVersion = "SSH-2.0-" + ProductName + "Client";

        public const string ServerKeysFilename = "foxssh-server.public-keys.json";

        public const string ServiceAuthenticationName = "ssh-userauth";

        public const string ServiceConnectionName = "ssh-connection";

        public const string PasswordAuthenticationMethod = "password";

        public static readonly Dictionary<SshMessageType, Type> MessageMapping = new Dictionary<SshMessageType, Type>();

        public static readonly Dictionary<string, Type> ServiceMapping = new Dictionary<string, Type>();

        public static SshServerKeys ServerPublicKeys { get; private set; }

        public static readonly Dictionary<string, Func<string, PublicKeyAlgorithm>> PublicKeyAlgorithms = new Dictionary<string, Func<string, PublicKeyAlgorithm>>();

        public static readonly Dictionary<string, Func<KeyExchangeAlgorithm>> KeyExchangeAlgorithms = new Dictionary<string, Func<KeyExchangeAlgorithm>>();

        public static readonly Dictionary<string, Func<CipherInformation>> EncryptionAlgorithms = new Dictionary<string, Func<CipherInformation>>();

        public static readonly Dictionary<string, Func<HmacInformation>> HmacAlgorithms = new Dictionary<string, Func<HmacInformation>>();

        public static readonly Dictionary<string, Func<CompressionAlgorithm>> CompressionAlgorithms = new Dictionary<string, Func<CompressionAlgorithm>>();

        public static RandomNumberGenerator Rng { get; private set; }

        public static TimeSpan SocketTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public static bool AnonymousAccess { get; set; }

        static SshCore()
        {
            Rng = RandomNumberGenerator.Create();

            PublicKeyAlgorithms.Add("ssh-rsa", x => new RsaKey(x));
            PublicKeyAlgorithms.Add("ssh-dss", x => new DssKey(x));

            KeyExchangeAlgorithms.Add("diffie-hellman-group14-sha1", () => new DiffieHellmanGroupSha1(new DiffieHellman(2048)));
            KeyExchangeAlgorithms.Add("diffie-hellman-group1-sha1", () => new DiffieHellmanGroupSha1(new DiffieHellman(1024)));

            EncryptionAlgorithms.Add("aes128-ctr", () => new CipherInformation(new AesCryptoServiceProvider(), 128, CipherModeExtended.CTR));
            EncryptionAlgorithms.Add("aes192-ctr", () => new CipherInformation(new AesCryptoServiceProvider(), 192, CipherModeExtended.CTR));
            EncryptionAlgorithms.Add("aes256-ctr", () => new CipherInformation(new AesCryptoServiceProvider(), 256, CipherModeExtended.CTR));
            EncryptionAlgorithms.Add("aes128-cbc", () => new CipherInformation(new AesCryptoServiceProvider(), 128, CipherModeExtended.CBC));
            EncryptionAlgorithms.Add("3des-cbc",   () => new CipherInformation(new TripleDESCryptoServiceProvider(), 192, CipherModeExtended.CBC));
            EncryptionAlgorithms.Add("aes192-cbc", () => new CipherInformation(new AesCryptoServiceProvider(), 192, CipherModeExtended.CBC));
            EncryptionAlgorithms.Add("aes256-cbc", () => new CipherInformation(new AesCryptoServiceProvider(), 256, CipherModeExtended.CBC));

            HmacAlgorithms.Add("hmac-md5", () => new HmacInformation(new HMACMD5(), 128));
            HmacAlgorithms.Add("hmac-sha1", () => new HmacInformation(new HMACSHA1(), 160));

            CompressionAlgorithms.Add("none", () => new CompressionNone());

            var types = typeof(SshCore).Assembly.GetTypes();
            var allClasses = types.Where(type => typeof(ISshMessage).IsAssignableFrom(type)).ToList();
            var firstImplementations = allClasses.Where(t => !allClasses.Contains(t.BaseType) && t.Name != "ISshMessage");
            foreach (var type in firstImplementations)
            {
                var messageObject = (ISshMessage)Activator.CreateInstance(type);

                MessageMapping.Add(messageObject.Type, type);
            }

            var serviceTypes = typeof(SshCore).Assembly.GetTypes().Where(x => x.GetInterfaces().Contains(typeof(ISshService)));
            foreach (var type in serviceTypes)
            {
                var serviceObject = (ISshService)Activator.CreateInstance(type);

                ServiceMapping.Add(serviceObject.Name, type);
            }

            if (ServerPublicKeys != null)
            {
                return;
            }

            if (File.Exists(ServerKeysFilename))
            {
                ServerPublicKeys = JsonSerializer.Deserialize<SshServerKeys>(File.ReadAllText(ServerKeysFilename));
            }
            else
            {
                var newServerKeys = new SshServerKeys
                {
                    Rsa = Convert.ToBase64String(new RSACryptoServiceProvider(SshServerKeys.RsaKeySize).ExportCspBlob(true)),
                    Dss = Convert.ToBase64String(new DSACryptoServiceProvider(SshServerKeys.DssKeySize).ExportCspBlob(true)),
                    Generated = DateTime.Now
                };

                File.WriteAllText(ServerKeysFilename, JsonSerializer.Serialize(newServerKeys, new JsonSerializerOptions { WriteIndented = true }));

                ServerPublicKeys = newServerKeys;
            }
        }

        internal static ISshMessage GetMessageInstanceFromType(SshMessageType messageType)
        {
            return (ISshMessage)Activator.CreateInstance(MessageMapping[messageType]);
        }

        public static string PickClientAlgorithm(IEnumerable<string> serverAlgorithms, IEnumerable<string> clientAlgorithms)
        {
            foreach (var client in clientAlgorithms)
            {
                foreach (var server in serverAlgorithms)
                {
                    if (client == server)
                    {
                        return client;
                    }
                }
            }

            throw new SshCoreException("Failed to negotiate an algorithm.");
        }
    }
}
