//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using FoxSsh.Common.Services;
using System;
using System.Collections.Generic;

namespace FoxSsh.Common.Messages.Authentication
{
    internal class AuthenticationFailureMessage : ISshMessage
    {
        public SshMessageType Type => SshMessageType.UserAuthFailure;

        public IReadOnlyCollection<byte> Raw { get; set; }

        public void LoadRawData(SshDataStream stream)
        {
            throw new NotImplementedException();
        }

        public void WriteRawData(SshDataStream stream)
        {
            stream.WriteAscii(string.Join(",", AuthenticationService.SupportedMethods));
            stream.Write(false);
        }
    }
}
