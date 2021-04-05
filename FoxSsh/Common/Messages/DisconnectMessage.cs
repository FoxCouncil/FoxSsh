//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Collections.Generic;

namespace FoxSsh.Common.Messages
{
    public class DisconnectMessage : ISshMessage
    {
        public SshMessageType Type => SshMessageType.Disconnect;

        public IReadOnlyCollection<byte> Raw { get; set; }

        public SshDisconnectReason Reason { get; private set; }

        public string Description { get; private set; }

        public string Language { get; private set; }

        public DisconnectMessage() { }

        public DisconnectMessage(SshDisconnectReason reason, string description = "", string language = "en")
        {
            Reason = reason;
            Description = description;
            Language = language;
        }

        public void LoadRawData(SshDataStream stream) 
        {
            Reason = (SshDisconnectReason)stream.ReadUInt32();
            Description = stream.ReadStringUtf8();

            if (stream.DataAvailable >= 4)
            {
                Language = stream.ReadStringUtf8();
            }
        }

        public void WriteRawData(SshDataStream stream) 
        {
            stream.Write((uint)Reason);
            stream.WriteUtf8(Description);
            stream.WriteUtf8(Language ?? "en");
        }
    }
}