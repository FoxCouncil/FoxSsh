//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Security.Cryptography;
using System.Text;

namespace FoxSsh.Common.Crypto
{
    public sealed class RsaKey : PublicKeyAlgorithm
    {
        private readonly RSACryptoServiceProvider _algorithm = new RSACryptoServiceProvider();

        public override string Name => "ssh-rsa";

        public RsaKey(string key = null) : base(key) { }

        public override void ImportKey(byte[] bytes)
        {
            _algorithm.ImportCspBlob(bytes);
        }

        public override byte[] ExportKey()
        {
            return _algorithm.ExportCspBlob(true);
        }

        public override void LoadKeyAndCertificatesData(byte[] data)
        {
            using var stream = new SshDataStream(data);

            if (stream.ReadString(Encoding.ASCII) != Name)
            {
                throw new CryptographicException("Key and/or certificate algorithm mismatch.");
            }

            var args = new RSAParameters
            {
                Exponent = stream.ReadMpInt(),
                Modulus = stream.ReadMpInt()
            };

            _algorithm.ImportParameters(args);
        }

        public override byte[] CreateKeyAndCertificatesData()
        {
            using var stream = new SshDataStream();

            var args = _algorithm.ExportParameters(false);

            stream.Write(Name, Encoding.ASCII);
            stream.WriteMpInt(args.Exponent);
            stream.WriteMpInt(args.Modulus);

            return stream.ToByteArray();
        }

        public override bool VerifyData(byte[] data, byte[] signature)
        {
            return _algorithm.VerifyData(data, signature, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
        }

        public override bool VerifyHash(byte[] hash, byte[] signature)
        {
            return _algorithm.VerifyHash(hash, signature, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
        }

        public override byte[] SignData(byte[] data)
        {
            return _algorithm.SignData(data, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
        }

        public override byte[] SignHash(byte[] hash)
        {
            return _algorithm.SignHash(hash, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
        }
    }
}
