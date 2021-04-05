//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System;
using System.Security.Cryptography;

namespace FoxSsh.Common.Crypto
{
    public class CipherInformation
    {
        public int KeySize { get; }

        public int BlockSize { get; }

        public Func<byte[], byte[], bool, EncryptionAlgorithm> Cipher { get; }

        public CipherInformation(SymmetricAlgorithm algorithm, int keySize, CipherModeExtended mode)
        {
            algorithm.KeySize = keySize;
            KeySize = algorithm.KeySize;
            BlockSize = algorithm.BlockSize;
            Cipher = (key, vi, isEncryption) => new EncryptionAlgorithm(algorithm, keySize, mode, key, vi, isEncryption);
        }
    }
}
