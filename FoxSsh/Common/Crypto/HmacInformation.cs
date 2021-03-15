//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FoxSsh.Common.Crypto
{
    public class HmacInformation
    {
        public int KeySize { get; private set; }

        public Func<byte[], HmacAlgorithm> Hmac { get; private set; }

        public HmacInformation(KeyedHashAlgorithm algorithm, int keySize)
        {
            KeySize = keySize;
            Hmac = key => new HmacAlgorithm(algorithm, keySize, key);
        }
    }
}
