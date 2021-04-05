//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

namespace FoxSsh.Common
{
    public interface ISshService
    {
        public string Name { get; }

        SshServiceRegistry Registry { get; set; }

        public void Close(string reason);

        public bool TryParseMessage(ISshMessage message);
    }
}
