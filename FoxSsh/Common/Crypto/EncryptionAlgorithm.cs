//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;

namespace FoxSsh.Common.Crypto
{
    public class EncryptionAlgorithm
    {
        private readonly SymmetricAlgorithm _algorithm;
        private readonly CipherModeExtended _mode;
        private readonly ICryptoTransform _transform;

        public int BlockBytesSize => _algorithm.BlockSize >> 3;

        public EncryptionAlgorithm(SymmetricAlgorithm algorithm, int keySize, CipherModeExtended mode, byte[] key, byte[] iv, bool isEncryption)
        {
            algorithm.KeySize = keySize;
            algorithm.Key = key;
            algorithm.IV = iv;
            algorithm.Padding = PaddingMode.None;

            _algorithm = algorithm;
            _mode = mode;

            _transform = CreateTransform(isEncryption);
        }

        public IEnumerable<byte> Transform(IEnumerable<byte> input)
        {
            var bytes = input as byte[] ?? input.ToArray();

            var output = new byte[bytes.Length];

            _transform.TransformBlock(bytes, 0, bytes.Length, output, 0);

            return output;
        }

        private ICryptoTransform CreateTransform(bool isEncryption)
        {
            switch (_mode)
            {
                case CipherModeExtended.CBC:
                {
                    _algorithm.Mode = CipherMode.CBC;

                    return isEncryption ? _algorithm.CreateEncryptor() : _algorithm.CreateDecryptor();
                }

                case CipherModeExtended.CTR:
                {
                    return new CtrModeCryptoTransform(_algorithm);
                }

                default:
                {
                    throw new InvalidEnumArgumentException($"Invalid mode: {_mode}");
                }
            }
        }
    }
}
