//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FoxSsh.Common
{
    public interface ISshMessage
    {
        public SshMessageType Type { get; }

        IReadOnlyCollection<byte> Raw { get; set; }

        public void LoadRawData(SshDataStream stream);

        public void WriteRawData(SshDataStream stream);

        public void Load(IEnumerable<byte> data) => Load(this, data.ToArray());

        public IEnumerable<byte> Write() => Write(this);

        protected static void Load(ISshMessage packet, byte[] data)
        {
            packet.Raw = data;

            using var stream = new SshDataStream(data);

            var dataPacketType = (SshMessageType)stream.ReadByte();

            if (packet.Type != dataPacketType)
            {
                throw new ApplicationException("There was a missmatch between packet Types {this} and {that}");
            }

            packet.LoadRawData(stream);
        }

        protected static IEnumerable<byte> Write(ISshMessage packet)
        {
            using var stream = new SshDataStream();

            stream.Write((byte)packet.Type);

            packet.WriteRawData(stream);

            return stream.ToByteArray();
        }

        public static T To<T>(ISshMessage msg) where T : ISshMessage, new()
        {
            var newMsg = new T();
            newMsg.Load(msg.Raw);
            return newMsg;
        }
    }
}
