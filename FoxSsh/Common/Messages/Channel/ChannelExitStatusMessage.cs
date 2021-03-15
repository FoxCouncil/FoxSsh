//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System;
using System.Collections.Generic;
using System.Text;

namespace FoxSsh.Common.Messages.Channel
{
    public class ChannelExitStatusMessage : ChannelRequestMessage
    {
        public uint ExitStatus { get; set; }

        public override void WriteRawData(SshDataStream stream)
        {
            RequestType = "exit-status";
            WantReply = false;

            base.WriteRawData(stream);

            stream.Write(ExitStatus);
        }
    }
}
