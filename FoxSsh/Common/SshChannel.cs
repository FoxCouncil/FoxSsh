//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using FoxSsh.Common.Messages.Channel;
using FoxSsh.Common.Services;
using System;
using System.Collections.Generic;
using System.Threading;

namespace FoxSsh.Common
{
    public class SshChannel
    {
        private readonly ConnectionService _service;

        private readonly EventWaitHandle _sendingWindow = new ManualResetEvent(false);

        public uint ClientChannelId { get; }

        public uint ClientInitialWindowSize { get; }

        public uint ClientWindowSize { get; protected set; }

        public uint ClientMaxPacketSize { get; }

        public uint ServerChannelId { get; }

        public uint ServerInitialWindowSize { get; }

        public uint ServerWindowSize { get; protected set; }

        public uint ServerMaxPacketSize { get; }

        public bool ClientClosed { get; private set; }

        public bool ClientEof { get; private set; }

        public bool ServerClosed { get; private set; }

        public bool ServerEof { get; private set; }

        public string Type { get; internal set; }

        public event Action<IReadOnlyCollection<byte>> DataReceived;

        public event Action<string> Disconnect;

        public SshChannel(ConnectionService service, uint clientChannelId, uint clientInitialWindowSize, uint clientMaxPacketSize, uint serverChannelId)
        {
            _service = service;

            ClientChannelId = clientChannelId;

            ClientInitialWindowSize = clientInitialWindowSize;

            ClientWindowSize = clientInitialWindowSize;

            ClientMaxPacketSize = clientMaxPacketSize;

            ServerChannelId = serverChannelId;

            ServerInitialWindowSize = SshCore.InitialLocalWindowSize;

            ServerWindowSize = SshCore.InitialLocalWindowSize;

            ServerMaxPacketSize = SshCore.LocalChannelDataPacketSize;
        }

        public void SendData(IReadOnlyCollection<byte> data)
        {
            if (data.Count == 0)
            {
                return;
            }

            var dataMessage = new ChannelDataMessage { RecipientChannel = ClientChannelId };

            var total = (uint)data.Count;
            var offset = 0;
            byte[] buffer = null;

            do
            {
                var packetSize = Math.Min(Math.Min(ClientWindowSize, ClientMaxPacketSize), total);

                if (packetSize == 0)
                {
                    _sendingWindow.WaitOne();

                    continue;
                }

                if (buffer == null || packetSize != buffer.Length)
                {
                    buffer = new byte[packetSize];
                }

                // ReSharper disable once SuspiciousTypeConversion.Global
                Array.Copy((Array)data, offset, buffer, 0, (int)packetSize);

                dataMessage.Data = buffer;

                _service.Registry.Session.SendMessage(dataMessage);

                ClientWindowSize -= packetSize;
                total -= packetSize;
                offset += (int)packetSize;
            }
            while (total > 0);
        }

        public void SendEof()
        {
            if (ServerEof)
            {
                return;
            }

            ServerEof = true;

            var eofMessage = new ChannelEofMessage { RecipientChannel = ClientChannelId };

            _service.Registry.Session.SendMessage(eofMessage);
        }

        public void SendClose(uint? exitCode = null)
        {
            if (ServerClosed)
            {
                return;
            }

            ServerClosed = true;

            if (exitCode.HasValue)
            {
                _service.Registry.Session.SendMessage(new ChannelExitStatusMessage { RecipientChannel = ClientChannelId, ExitStatus = exitCode.Value });
            }

            _service.Registry.Session.SendMessage(new ChannelCloseMessage { RecipientChannel = ClientChannelId });

            CheckClosed();
        }

        internal void OnData(IReadOnlyCollection<byte> data)
        {
            AttemptWindowAdjust((uint)data.Count);

            DataReceived?.Invoke(data);
        }

        internal void OnEof()
        {
            ClientEof = true;
        }

        internal void OnClose()
        {
            ClientClosed = true;

            CheckClosed();
        }

        internal void ClientAdjustWindowSize(uint bytesToAdjust)
        {
            ClientWindowSize += bytesToAdjust;

            _sendingWindow.Set();
            Thread.Sleep(1);
            _sendingWindow.Reset();
        }

        internal void AttemptWindowAdjust(uint bytesToAdjust)
        {
            ServerWindowSize -= bytesToAdjust;

            if (ServerWindowSize > ServerMaxPacketSize)
            {
                return;
            }

            _service.Registry.Session.SendMessage(new ChannelWindowAdjustMessage { 
                RecipientChannel = ClientChannelId,
                BytesToAdd = ServerInitialWindowSize - ServerWindowSize
            });

            ServerWindowSize = ServerInitialWindowSize;
        }

        internal void ForciblyClose()
        {
            Disconnect?.Invoke("Closing...");

            _service.RemoveChannel(this);
            _sendingWindow.Set();
            _sendingWindow.Close();
        }

        private void CheckClosed()
        {
            if (ClientClosed && ServerClosed)
            {
                ForciblyClose();
            }
        }
    }
}
