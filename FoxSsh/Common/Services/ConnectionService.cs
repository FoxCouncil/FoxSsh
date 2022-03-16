//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using FoxSsh.Common.Messages.Channel;
using FoxSsh.Common.Messages.Channel.Request;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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

        public SshPty Pty { get; private set; }

#pragma warning disable 67
        public event Action<SshPty> PtyRegistered;
#pragma warning restore 67

        private readonly ILogger _logger;

        public ConnectionService()
        {
            _logger = SshLog.Factory.CreateLogger<ConnectionService>();

            Task.Run(Run);
        }

        public void Close(string reason)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

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
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            if (!(message.Type >= SshMessageType.ChannelOpen && message.Type <= SshMessageType.ChannelFailure))
            {
                return false;
            }

            ProcessMessage(message);

            return true;
        }

        internal void RemoveChannel(SshChannel sshChannel)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            lock (_channelsLock)
            {
                _channels.Remove(sshChannel);
            }
        }

        private void Run()
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

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
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

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
                    _logger.LogTrace($"Handling ChannelRequestMessage: {coReq.RequestType}");

                    switch (coReq.RequestType)
                    {
                        case SshCore.ChannelRequestNames.PtyRequest:
                        {
                            var ptyReq = ISshMessage.To<ChannelPtyRequestMessage>(coReq);

                            var channel = GetChannel(ptyReq.RecipientChannel);

                            if (ptyReq.WantReply)
                            {
                                Registry.Session.SendMessage(new ChannelSuccessMessage { RecipientChannel = channel.ClientChannelId });
                            }

                            Pty = new SshPty(channel)
                            {
                                HeightPx = ptyReq.HeightPx,
                                HeightChars = ptyReq.HeightRows,
                                Terminal = ptyReq.Terminal,
                                WidthChars = ptyReq.WidthChars,
                                WidthPx = ptyReq.WidthPx
                            };

                            Registry.Session.SendPtyRequest(Pty);
                        }
                        break;

                        case SshCore.ChannelRequestNames.ShellRequest:
                        {
                            var shellReq = ISshMessage.To<ChannelShellRequestMessage>(coReq);

                            if (shellReq.WantReply)
                            {
                                var channel = GetChannel(shellReq.RecipientChannel);

                                Registry.Session.SendMessage(new ChannelSuccessMessage { RecipientChannel = channel.ClientChannelId });
                            }

                            Registry.Session.SendShellRequest();
                        }
                        break;

                        case SshCore.ChannelRequestNames.WindowChange:
                        {
                            var windowChangeReq = ISshMessage.To<ChannelWindowChangeRequestMessage>(coReq);

                            Pty.OnResize(windowChangeReq.WidthChars, windowChangeReq.HeightChars, windowChangeReq.WidthPixels, windowChangeReq.HeightPixels);

                            if (windowChangeReq.WantReply)
                            {
                                var channel = GetChannel(windowChangeReq.RecipientChannel);

                                Registry.Session.SendMessage(new ChannelSuccessMessage { RecipientChannel = channel.ClientChannelId });
                            }
                        }
                        break;

                        case SshCore.ChannelRequestNames.WinAdjPutty:
                        {
                            var channel = GetChannel(coReq.RecipientChannel);

                            Registry.Session.SendMessage(new ChannelFailureMessage { RecipientChannel = channel.ClientChannelId });
                        }
                        break;

                        default:
                        {
                            _logger.LogTrace(coReq.RequestType);

                            // throw new ApplicationException($"Could not process the {message.Type} Connection Service message.");
                        }
                        break;
                    }
                }
                break;

                case ChannelWindowAdjustMessage adjustMessage:
                {
                    var channel = GetChannel(adjustMessage.RecipientChannel);

                    channel.ClientAdjustWindowSize(adjustMessage.BytesToAdd);
                }
                break;

                default:
                {
                    throw new ApplicationException($"Could not process the {message.Type} Connection Service message.");
                }
            }
        }

        private SshChannel GetChannel(uint channelId)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            SshChannel channel;

            lock (_channelsLock)
            {
                channel = _channels.Find(x => x.ServerChannelId == channelId);
            }

            return channel;
        }
    }
}
