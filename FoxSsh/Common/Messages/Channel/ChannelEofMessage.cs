//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Collections.Generic;

namespace FoxSsh.Common.Messages.Channel
{
    public class ChannelEofMessage : ISshMessage
    {
        public SshMessageType Type => SshMessageType.ChannelEof;

        public IReadOnlyCollection<byte> Raw { get; set; }

        public uint RecipientChannel { get; set; }

        public void LoadRawData(SshDataStream stream)
        {
            RecipientChannel = stream.ReadUInt32();
        }

        public void WriteRawData(SshDataStream stream)
        {
            stream.Write(RecipientChannel);
        }
    }
}