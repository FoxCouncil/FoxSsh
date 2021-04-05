//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

namespace FoxSsh.Common.Messages.Channel.Request
{
    internal class ChannelWindowChangeRequestMessage : ChannelRequestMessage
    {
        public uint WidthChars { get; private set; }

        public uint HeightChars { get; private set; }

        public uint WidthPixels { get; private set; }

        public uint HeightPixels { get; private set; }

        public override void LoadRawData(SshDataStream stream)
        {
            base.LoadRawData(stream);

            WidthChars = stream.ReadUInt32();
            HeightChars = stream.ReadUInt32();
            WidthPixels = stream.ReadUInt32();
            HeightPixels = stream.ReadUInt32();
        }
    }
}
