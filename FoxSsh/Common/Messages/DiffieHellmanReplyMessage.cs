//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Collections.Generic;
using System.Linq;

namespace FoxSsh.Common.Messages
{
    public class DiffieHellmanReplyMessage : ISshMessage
    {
        public SshMessageType Type => SshMessageType.DiffieHellmanReply;

        public IReadOnlyCollection<byte> Raw { get; set; }

        public IReadOnlyCollection<byte> HostKey { get; set; }

        public IReadOnlyCollection<byte> F { get; set; }

        public IReadOnlyCollection<byte> Signature { get; set; }

        public void LoadRawData(SshDataStream stream) { }

        public void WriteRawData(SshDataStream stream)
        {
            stream.WriteBinary(HostKey.ToArray());
            stream.WriteMpInt(F.ToArray());
            stream.WriteBinary(Signature.ToArray());
        }
    }
}