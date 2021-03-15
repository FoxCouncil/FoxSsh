//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Collections.Generic;

namespace FoxSsh.Common.Messages.Channel
{
    public class ChannelWindowAdjustMessage : ISshMessage
    {
        public SshMessageType Type => SshMessageType.ChannelWindowAdjust;

        public IReadOnlyCollection<byte> Raw { get; set; }

        public uint RecipientChannel { get; set; }

        public uint BytesToAdd { get; set; }

        public void LoadRawData(SshDataStream stream)
        {
            RecipientChannel = stream.ReadUInt32();
            BytesToAdd = stream.ReadUInt32();
        }

        public void WriteRawData(SshDataStream stream)
        {
            stream.Write(RecipientChannel);
            stream.Write(BytesToAdd);
        }
    }
}