//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System;
using System.Collections.Generic;
using System.Text;

namespace FoxSsh.Server
{
    public class SshServerKeys
    {
        public const int RsaKeySize = 4096;

        public const int DssKeySize = 1024;

        public string Rsa { get; set; }

        public string Dss { get; set; }

        public DateTime Generated { get; set; }

        public string this[string keyType] => FindKey(keyType);

        private string FindKey(string keyType)
        {
            return keyType switch
            {
                "ssh-rsa" => Rsa,
                "ssh-dss" => Dss,
                _ => throw new ApplicationException($"Keytype {keyType} is not a valid SSH Server Public Key"),
            };
        }
    }
}
