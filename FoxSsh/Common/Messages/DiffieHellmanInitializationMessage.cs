//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FoxSsh.Common.Messages
{
    public class DiffieHellmanInitializationMessage : ISshMessage
    {
        public SshMessageType Type => SshMessageType.DiffieHellmanInitialization;

        public IReadOnlyCollection<byte> Raw { get; set; }

        public IReadOnlyCollection<byte> E { get; set; }

        public void LoadRawData(SshDataStream stream)
        {
            E = stream.ReadMpInt();
        }

        public void WriteRawData(SshDataStream stream) {}
    }
}