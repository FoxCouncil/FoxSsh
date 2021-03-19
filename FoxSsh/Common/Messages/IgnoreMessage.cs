//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Collections.Generic;

namespace FoxSsh.Common.Messages
{
    public class IgnoreMessage : ISshMessage
    {
        public SshMessageType Type => SshMessageType.Ignore;

        public IReadOnlyCollection<byte> Raw { get; set; }

        public void LoadRawData(SshDataStream stream) { }

        public void WriteRawData(SshDataStream stream) { }
    }
}