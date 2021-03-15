//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Collections.Generic;
using System.Linq;

namespace FoxSsh.Common.Messages.Channel
{
    public class ChannelDataMessage : ISshMessage
    {
        public SshMessageType Type => SshMessageType.ChannelData;

        public IReadOnlyCollection<byte> Raw { get; set; }

        public uint RecipientChannel { get; set; }

        public IReadOnlyCollection<byte> Data { get; set; }

        public void LoadRawData(SshDataStream stream)
        {
            RecipientChannel = stream.ReadUInt32();
            Data = stream.ReadBinary();
        }

        public void WriteRawData(SshDataStream stream)
        {
            stream.Write(RecipientChannel);
            stream.WriteBinary(Data.ToArray());
        }
    }
}