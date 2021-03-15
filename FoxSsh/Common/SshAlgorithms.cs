//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using FoxSsh.Common.Crypto;

namespace FoxSsh.Common
{
    public class SshAlgorithms
    {
        public KeyExchangeAlgorithm KeyExchange { get; set; }

        public PublicKeyAlgorithm PublicKey { get; set; }

        public SshAlgorithm Server { get; set; }

        public SshAlgorithm Client { get; set; }
    }
}