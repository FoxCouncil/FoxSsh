//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Collections.Generic;

namespace FoxSsh.Common.Messages.Channel
{
    public class ChannelRequestMessage : ISshMessage
    {
        public SshMessageType Type => SshMessageType.ChannelRequest;

        public IReadOnlyCollection<byte> Raw { get; set; }

        public uint RecipientChannel { get; set; }

        public string RequestType { get; set; }

        public bool WantReply { get; set; }

        public virtual void LoadRawData(SshDataStream stream)
        {
            RecipientChannel = stream.ReadUInt32();
            RequestType = stream.ReadStringAscii();
            WantReply = stream.ReadBoolean();
        }

        public virtual void WriteRawData(SshDataStream stream)
        {
            stream.Write(RecipientChannel);
            stream.WriteAscii(RequestType);
            stream.Write(WantReply);
        }
    }
}