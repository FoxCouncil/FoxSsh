//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

namespace FoxSsh.Common.Messages.Channel.Request
{
    public class ChannelPtyRequestMessage : ChannelRequestMessage
    {
        public string Terminal { get; set; }

        public uint WidthChars { get; set; }

        public uint HeightRows { get; set; }

        public uint WidthPx { get; set; }

        public uint HeightPx { get; set; }

        public string Modes { get; set; }

        public override void LoadRawData(SshDataStream stream)
        {
            base.LoadRawData(stream);

            Terminal = stream.ReadStringAscii();
            WidthChars = stream.ReadUInt32();
            HeightRows = stream.ReadUInt32();
            WidthPx = stream.ReadUInt32();
            HeightPx = stream.ReadUInt32();
            Modes = stream.ReadStringAscii();
        }
    }
}
