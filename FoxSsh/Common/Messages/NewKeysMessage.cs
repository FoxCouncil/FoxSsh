//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FoxSsh.Common.Messages
{
    public class NewKeysRequestMessage : ISshMessage
    {
        public SshMessageType Type => SshMessageType.NewKeys;

        public IReadOnlyCollection<byte> Raw { get; set; }

        public void LoadRawData(SshDataStream stream) { }

        public void WriteRawData(SshDataStream stream) { }
    }
}