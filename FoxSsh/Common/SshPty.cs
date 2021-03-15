using FoxSsh.Common.Messages.Channel.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FoxSsh.Common
{
    public class SshPty
    {
        const char C = '\u001b';

        const string CodeClear = "[2J";
        const string CodeCursorHome = "[H";

        readonly SshChannel _channel;

        public string Terminal { get; set; }

        public uint WidthChars { get; set; }

        public uint HeightRows { get; set; }

        public uint WidthPx { get; set; }

        public uint HeightPx { get; set; }

        public Action<string> Data;

        public SshPty(SshChannel channel)
        {
            _channel = channel;
            _channel.DataRecieved += (d) => Data?.Invoke(Encoding.UTF8.GetString(d.ToArray()));
        }

        public void Send(string text)
        {
            _channel.SendData(B(text));
        }

        public void Clear()
        {
            _channel.SendData(B($"{C + CodeCursorHome + C + CodeClear}"));
        }

        private IReadOnlyCollection<byte> B(string text)
        {
            return Encoding.UTF8.GetBytes(text);
        }
    }
}
