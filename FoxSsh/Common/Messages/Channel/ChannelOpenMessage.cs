//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Collections.Generic;

namespace FoxSsh.Common.Messages.Channel
{
    public class ChannelOpenMessage : ISshMessage
    {
        public SshMessageType Type => SshMessageType.ChannelOpen;

        public IReadOnlyCollection<byte> Raw { get; set; }

        public string ChannelType { get; private set; }

        public uint SenderChannel { get; private set; }

        public uint InitialWindowSize { get; private set; }

        public uint MaximumPacketSize { get; private set; }

        public void LoadRawData(SshDataStream stream)
        {
            ChannelType = stream.ReadStringAscii();
            SenderChannel = stream.ReadUInt32();
            InitialWindowSize = stream.ReadUInt32();
            MaximumPacketSize = stream.ReadUInt32();
        }

        public void WriteRawData(SshDataStream stream) { }
    }
}
