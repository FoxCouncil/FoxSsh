//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using FoxSsh.Common.Messages.Channel;
using FoxSsh.Common.Messages.Channel.Request;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoxSsh.Common.Services
{
    public class ConnectionService : ISshService
    {        
        private readonly CancellationTokenSource _cancelToken = new CancellationTokenSource();

        private readonly object _channelsLock = new object();

        private readonly List<SshChannel> _channels = new List<SshChannel>();

        private readonly BlockingCollection<ISshMessage> _messageQueue = new BlockingCollection<ISshMessage>(new ConcurrentQueue<ISshMessage>());

        private int _serverChannelIdx = -1;

        public string Name => SshCore.ServiceConnectionName;

        public SshServiceRegistry Registry { get; set; }

        public event Action<SshPty> PtyRegistered;

        public ConnectionService()
        {
            Task.Run(Run);
        }

        public void Close()
        {
            _cancelToken.Cancel();

            lock (_channelsLock)
            {
                foreach (var channel in _channels.ToList())
                {
                    channel.ForciblyClose();
                }
            }
        }

        public bool TryParseMessage(ISshMessage message)
        {
            if (!(message.Type >= SshMessageType.ChannelOpen && message.Type <= SshMessageType.ChannelFailure))
            {
                return false;
            }

            ProcessMessage(message);

            return true;
        }

        internal void RemoveChannel(SshChannel sshChannel)
        {
            lock (_channelsLock)
            {
                _channels.Remove(sshChannel);
            }
        }

        private void Run()
        {
            try
            {
                while (!_cancelToken.IsCancellationRequested)
                {
                    var msg = _messageQueue.Take(_cancelToken.Token);

                    ProcessMessage(msg);
                }
            }
            catch (OperationCanceledException) { }
        }

        private void ProcessMessage(ISshMessage message)
        {
            switch (message)
            {
                case ChannelOpenMessage coMsg:
                {
                    var newChannel = new SshChannel(this, coMsg.SenderChannel, coMsg.InitialWindowSize, coMsg.MaximumPacketSize, (uint)Interlocked.Increment(ref _serverChannelIdx))
                    {
                        Type = coMsg.ChannelType
                    };

                    lock (_channelsLock)
                    {
                        _channels.Add(newChannel);
                    }

                    var confirmMsg = new ChannelOpenConfirmationMessage
                    {
                        RecipientChannel = newChannel.ClientChannelId,
                        SenderChannel = newChannel.ServerChannelId,
                        InitialWindowSize = newChannel.ServerInitialWindowSize,
                        MaximumPacketSize = newChannel.ServerMaxPacketSize
                    };

                    Registry.Session.SendMessage(confirmMsg);
                }
                break;

                case ChannelDataMessage cData:
                {
                    SshChannel incomingChannel;

                    lock (_channelsLock)
                    {
                        incomingChannel = _channels.Find(x => x.ServerChannelId == cData.RecipientChannel);
                    }

                    if (incomingChannel == null)
                    {
                        throw new SshSessionException(SshSessionExceptionType.Unknown, "Unknown channel...");
                    }

                    incomingChannel.OnData(cData.Data);
                }
                break;

                case ChannelRequestMessage coReq:
                {
                    switch (coReq.RequestType)
                    {
                        case SshCore.ChannelRequestNames.PtyRequest:
                        {
                            var ptyReq = ISshMessage.To<ChannelPtyRequestMessage>(coReq);

                            SshChannel channel;

                            lock (_channelsLock)
                            {
                                channel = _channels.Find(x => x.ServerChannelId == ptyReq.RecipientChannel);
                            }

                            if (ptyReq.WantReply)
                            {
                                Registry.Session.SendMessage(new ChannelSuccessMessage { RecipientChannel = channel.ClientChannelId });
                            }

                            var pty = new SshPty(channel)
                            {
                                HeightPx = ptyReq.HeightPx,
                                HeightRows = ptyReq.HeightRows,
                                Terminal = ptyReq.Terminal,
                                WidthChars = ptyReq.WidthChars,
                                WidthPx = ptyReq.WidthPx
                            };

                            Registry.Session.SendPtyRegistration(pty);
                        }
                        break;
                    }
                }
                break;

                default:
                {
                    throw new ApplicationException($"Could not process the {message.Type} Connection Service message.");
                }
                break;
            }
        }
    }
}
