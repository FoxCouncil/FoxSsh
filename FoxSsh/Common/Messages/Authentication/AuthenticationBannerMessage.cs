//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Collections.Generic;

namespace FoxSsh.Common.Messages.Authentication
{
    internal class AuthenticationBannerMessage : ISshMessage
    {
        public SshMessageType Type => SshMessageType.UserAuthBanner;

        public IReadOnlyCollection<byte> Raw { get; set; }

        public string Text { get; set; }

        public string LanguageTag { get; set; } = "en";

        public void LoadRawData(SshDataStream stream) { }

        public void WriteRawData(SshDataStream stream)
        {
            stream.WriteUtf8(Text);
            stream.WriteUtf8(LanguageTag);
        }
    }
}
