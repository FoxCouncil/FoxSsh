//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Security.Cryptography;

namespace FoxSsh.Common.Crypto
{
    public class HmacAlgorithm
    {
        private readonly KeyedHashAlgorithm _algorithm;

        public int DigestLength => _algorithm.HashSize >> 3;

        public HmacAlgorithm(KeyedHashAlgorithm algorithm, int keySize, byte[] key)
        {
            _algorithm = algorithm;
            algorithm.Key = key;
        }

        public byte[] ComputeHash(byte[] input)
        {

            return _algorithm.ComputeHash(input);
        }
    }
}
