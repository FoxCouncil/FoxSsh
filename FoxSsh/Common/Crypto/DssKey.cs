//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Security.Cryptography;
using System.Text;

namespace FoxSsh.Common.Crypto
{
    public class DssKey : PublicKeyAlgorithm
    {
        private readonly DSACryptoServiceProvider _algorithm = new DSACryptoServiceProvider();

        public override string Name => "ssh-dss";

        public DssKey(string key = null) : base(key) { }

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
            using (var stream = new SshDataStream(data))
            {
                if (stream.ReadString(Encoding.ASCII) != Name)
                {
                    throw new CryptographicException("Key and/or certificate algorithm missmatch.");
                }

                var args = new DSAParameters
                {
                    P = stream.ReadMpInt(),
                    Q = stream.ReadMpInt(),
                    G = stream.ReadMpInt(),
                    Y = stream.ReadMpInt()
                };

                _algorithm.ImportParameters(args);
            }
        }

        public override byte[] CreateKeyAndCertificatesData()
        {
            using (var stream = new SshDataStream())
            {
                var args = _algorithm.ExportParameters(false);

                stream.Write(this.Name, Encoding.ASCII);

                stream.WriteMpInt(args.P);
                stream.WriteMpInt(args.Q);
                stream.WriteMpInt(args.G);
                stream.WriteMpInt(args.Y);

                return stream.ToByteArray();
            }
        }

        public override bool VerifyData(byte[] data, byte[] signature)
        {
            return _algorithm.VerifyData(data, signature);
        }

        public override bool VerifyHash(byte[] hash, byte[] signature)
        {
            return _algorithm.VerifyHash(hash, "SHA1", signature);
        }

        public override byte[] SignData(byte[] data)
        {
            return _algorithm.SignData(data);
        }

        public override byte[] SignHash(byte[] hash)
        {
            return _algorithm.SignHash(hash, "SHA1");
        }
    }
}