//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FoxSsh.Common.Crypto
{
    public abstract class PublicKeyAlgorithm
    {
        public abstract string Name { get; }

        public PublicKeyAlgorithm(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                var bytes = Convert.FromBase64String(key);

                ImportKey(bytes);
            }
        }

        public string GetFingerprint()
        {
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(CreateKeyAndCertificatesData());

                return BitConverter.ToString(bytes).Replace('-', ':');
            }
        }

        public byte[] GetSignature(byte[] signatureData)
        {
            using (var stream = new SshDataStream(signatureData))
            {
                if (stream.ReadString(Encoding.ASCII) != this.Name)
                {
                    throw new CryptographicException("Signature was not created with this algorithm.");
                }

                var signature = stream.ReadBinary();

                return signature;
            }
        }

        public byte[] CreateSignatureData(byte[] data)
        {
            using (var stream = new SshDataStream())
            {
                var signature = SignData(data);

                stream.Write(Name, Encoding.ASCII);
                stream.WriteBinary(signature);

                return stream.ToByteArray();
            }
        }

        public abstract void ImportKey(byte[] bytes);

        public abstract byte[] ExportKey();

        public abstract void LoadKeyAndCertificatesData(byte[] data);

        public abstract byte[] CreateKeyAndCertificatesData();

        public abstract bool VerifyData(byte[] data, byte[] signature);

        public abstract bool VerifyHash(byte[] hash, byte[] signature);

        public abstract byte[] SignData(byte[] data);

        public abstract byte[] SignHash(byte[] hash);
    }
}
