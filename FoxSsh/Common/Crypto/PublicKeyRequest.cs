//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Collections.Generic;

namespace FoxSsh.Common.Crypto
{
    public class PublicKeyRequest
    {
        public bool HasSignature { get; set; }

        public string AlgorithmName { get; set; }

        public IReadOnlyCollection<byte> Key { get; set; }

        public IReadOnlyCollection<byte> Signature { get; set; }

        public IReadOnlyCollection<byte> PayloadWithoutSignature { get; set; }
    }
}