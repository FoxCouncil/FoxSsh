//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Collections.Generic;

namespace FoxSsh.Common.Messages.Channel
{
    public class ChannelOpenConfirmationMessage : ISshMessage
    {
        public SshMessageType Type => SshMessageType.ChannelOpenConfirmation;

        public IReadOnlyCollection<byte> Raw { get; set; }

        public uint RecipientChannel { get; set; }

        public uint SenderChannel { get; set; }

        public uint InitialWindowSize { get; set; }

        public uint MaximumPacketSize { get; set; }

        public virtual void LoadRawData(SshDataStream stream) { }

        public virtual void WriteRawData(SshDataStream stream)
        {
            stream.Write(RecipientChannel);
            stream.Write(SenderChannel);
            stream.Write(InitialWindowSize);
            stream.Write(MaximumPacketSize);
        }
    }
}