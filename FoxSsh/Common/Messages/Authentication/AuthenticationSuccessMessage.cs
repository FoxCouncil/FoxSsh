//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System;
using System.Collections.Generic;
using System.Text;

namespace FoxSsh.Common.Messages.Authentication
{
    class AuthenticationSuccessMessage : ISshMessage
    {
        public SshMessageType Type => SshMessageType.UserAuthSuccess;

        public IReadOnlyCollection<byte> Raw { get; set; }

        public void LoadRawData(SshDataStream stream) { }

        public void WriteRawData(SshDataStream stream) { }
    }
}
