//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System;
using System.Security.Cryptography;

namespace FoxSsh.Common.Crypto
{
    public class HmacInformation
    {
        public int KeySize { get; }

        public Func<byte[], HmacAlgorithm> Hmac { get; }

        public HmacInformation(KeyedHashAlgorithm algorithm, int keySize)
        {
            KeySize = keySize;
            Hmac = key => new HmacAlgorithm(algorithm, keySize, key);
        }
    }
}
