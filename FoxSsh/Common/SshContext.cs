//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Collections.Generic;

namespace FoxSsh.Common
{
    public class SshContext
    {
        public string Encryption { get; set; }

        public string Hmac { get; set; }

        public string Compression { get; set; }

        public IEnumerable<byte> KeyExchangePayload { get; set; }
    }
}
