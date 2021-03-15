//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FoxSsh.Common.Messages
{
    public class ServiceAcceptMessage : ISshMessage
    {
        public SshMessageType Type => SshMessageType.ServiceAccept;

        public IReadOnlyCollection<byte> Raw { get; set; }

        public string Name { get; set; }

        public void LoadRawData(SshDataStream stream) { }

        public void WriteRawData(SshDataStream stream)
        {
            stream.WriteAscii(Name);
        }
    }
}