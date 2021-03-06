﻿//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Security.Cryptography;

namespace FoxSsh.Common.Crypto
{
    public abstract class KeyExchangeAlgorithm
    {
        protected HashAlgorithm HashAlgorithm;

        public abstract byte[] CreateKeyExchange();

        public abstract byte[] DecryptKeyExchange(byte[] exchangeData);

        public byte[] ComputeHash(byte[] input)
        {
            return HashAlgorithm.ComputeHash(input);
        }
    }
}
