//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

namespace FoxSsh.Common
{
    internal class SshSessionContext
    {
        public string KeyExchange { get; set; }

        public string PublicKey { get; set; }

        public SshContext Client { get; set; } = new SshContext();

        public SshContext Server { get; set; } = new SshContext();

        public SshAlgorithms ExchangedAlgorithms { get; set; }
    }
}
