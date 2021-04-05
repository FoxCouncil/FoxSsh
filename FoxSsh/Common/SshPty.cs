//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FoxSsh.Common
{
    public class SshPty
    {
        private const char CodeEscape = '\u001b';

        private const string CodeReset = "c";
        private const string CodeClear = "[2J";
        private const string CodeLinewrapEnable = "[7h";
        private const string CodeLinewrapDisable = "[7l";
        private const string CodeCursorHome = "[H";
        private const string CodeCursorPos = "[{0};{1}H";
        private const string CodeCursorShow = "[?25h";
        private const string CodeCursorHide = "[?25l";

        private readonly SshChannel _channel;

        private bool _disconnected;

        public string Terminal { get; set; }

        public uint WidthChars { get; set; }

        public uint HeightChars { get; set; }

        public uint WidthPx { get; set; }

        public uint HeightPx { get; set; }

        public event Action<string> Disconnect;

        public event Action<string> Data;

        public event Action<uint, uint, uint, uint> Resize;

        public SshPty(SshChannel channel)
        {
            _channel = channel;
            _channel.DataReceived += d => Data?.Invoke(Encoding.UTF8.GetString(d.ToArray()));
            _channel.Disconnect += reason =>
            {
                _disconnected = true;

                Disconnect?.Invoke(reason);
            };
        }

        public void WriteR(char v, uint widthChars)
        {
            Send(new string(v, (int)widthChars));
        }

        public void LinewrapEnable()
        {
            SendCommand(CodeLinewrapEnable);
        }

        public void LinewrapDisable()
        {
            SendCommand(CodeLinewrapDisable);
        }

        public void CursorShow()
        {
            SendCommand(CodeCursorShow);
        }

        public void CursorHide()
        {
            SendCommand(CodeCursorHide);
        }

        public void Cursor(int x, int y)
        {
            if (x == 1 && y == 1)
            {
                SendCommand(CodeCursorHome);

                return;
            }

            SendCommand(string.Format(CodeCursorPos, y, x));
        }

        public void Send(string text)
        {
            if (_disconnected)
            {
                return;
            }

            _channel.SendData(B(text));
        }

        public void Reset()
        {
            SendCommand(CodeReset);
        }

        public void Clear()
        {
            if (_disconnected)
            {
                return;
            }

            _channel.SendData(B($"{CodeEscape + CodeCursorHome + CodeEscape + CodeClear}"));
        }

        internal void OnResize(uint widthChars, uint heightChars, uint widthPixels, uint heightPixels)
        {
            Resize?.Invoke(widthChars, heightChars, widthPixels, heightPixels);

            WidthChars = widthChars;
            HeightChars = heightChars;
            WidthPx = widthPixels;
            HeightPx = heightPixels;
        }

        private void SendCommand(string command)
        {
            if (_disconnected)
            {
                return;
            }

            _channel.SendData(C(command));
        }

        private static IReadOnlyCollection<byte> C(string code)
        {
            return B($"{CodeEscape + code}");
        }

        private static IReadOnlyCollection<byte> B(string text)
        {
            return Encoding.UTF8.GetBytes(text);
        }
    }
}
