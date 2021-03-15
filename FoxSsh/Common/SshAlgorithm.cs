//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using FoxSsh.Common.Compression;
using FoxSsh.Common.Crypto;

namespace FoxSsh.Common
{
    public class SshAlgorithm
    {
        public EncryptionAlgorithm Encryption;

        public HmacAlgorithm Hmac;

        public CompressionAlgorithm Compression;
    }
}