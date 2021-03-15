//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FoxSsh.Common.Messages
{
    public class ServiceRequestMessage : ISshMessage
    {
        public SshMessageType Type => SshMessageType.ServiceRequest;

        public IReadOnlyCollection<byte> Raw { get; set; }

        public string Name { get; private set; }

        public void LoadRawData(SshDataStream stream) 
        {
            Name = stream.ReadString(Encoding.ASCII);
        }

        public void WriteRawData(SshDataStream stream) { }
    }
}