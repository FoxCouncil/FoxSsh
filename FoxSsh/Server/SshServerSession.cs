//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using FoxSsh.Common;
using FoxSsh.Common.Crypto;
using FoxSsh.Common.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging;

// ReSharper disable PossibleMultipleEnumeration

namespace FoxSsh.Server
{
    public class SshServerSession
    {
        private readonly Socket _socket;

        private readonly SshServiceRegistry _registry;

        private readonly EndPoint _endPoint;

        private readonly ConcurrentQueue<ISshMessage> _blockedMessagesQueue = new ConcurrentQueue<ISshMessage>();

        private readonly EventWaitHandle _hasBlockedMessages = new ManualResetEvent(true);

        private bool _disconnected;

        private uint _incomingBytes;
        private uint _incomingSequence;

        private uint _outgoingBytes;
        private uint _outgoingSequence;

        private SshSessionContext _context;

        private SshAlgorithms _algorithms;

        public event Func<SshAuthenticationRequest, bool> AuthenticationRequest;

        public event Action ShellRequest;

        public event Action<SshPty> PtyRequest;

        public event Action<string> Disconnected;

        public Guid Id { get; } = Guid.NewGuid();

        public string ClientVersion { get; private set; }

        public IEnumerable<byte> SessionId { get; private set; }

        public SshPty Pty { get; internal set; }

        private readonly ILogger _logger;

        public SshServerSession(Socket socket)
        {
            _socket = socket;
            _endPoint = _socket.RemoteEndPoint;

            _registry = new SshServiceRegistry(this);

            _logger = SshLog.Factory.CreateLogger<SshServerSession>();

            _logger.LogTrace($"Session & Service Registry Created For {_endPoint}");

            _logger.LogInformation("Client connected: " + socket.RemoteEndPoint);
        }

        public void Connect()
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            if (!_socket.Connected)
            {
                return;
            }

            const int socketBufferSize = 2 * SshCore.MaximumSshPacketSize;

            _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            _socket.LingerState = new LingerOption(false, 0);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, socketBufferSize);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, socketBufferSize);
            _socket.ReceiveTimeout = -1; // (int)SshCore.SocketTimeout.TotalMilliseconds;

            _logger.LogTrace("Socket Settings Initiated");

            HandshakeVersions();

            if (!Regex.IsMatch(ClientVersion, "SSH-2.0-.+"))
            {
                throw new ApplicationException($"FoxSsh server cannot accept connections from clients like: {ClientVersion}. We only support SSH2.");
            }

            _logger.LogDebug($"Socket Client Verified {ClientVersion}");

            ShouldUpdateContexts(true);

            try
            {
                while (_socket != null && _socket.Connected)
                {
                    var message = ReadMessage();

                    if (message == null)
                    {
                        break;
                    }

                    _logger.LogTrace($"Processing Message {message.Type}");

                    ProcessMessage(message);
                }
            }
            catch
            {
                // ignored
            }

            _registry.Close("SocketDisconnection");

            _logger.LogInformation($"Socket Disconnecting For {_endPoint}");
        }

        public void SendMessage(ISshMessage message)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            if (message.Type != SshMessageType.ChannelData)
            {
                _logger.LogTrace($"Sending message: {message.Type}");
            }

            if (_context != null && message.Type > SshMessageType.Debug && (message.Type < SshMessageType.KeyExchangeInitialization || (byte)message.Type > 49))
            {
                _blockedMessagesQueue.Enqueue(message);

                return;
            }

            _hasBlockedMessages.WaitOne();

            SendMessageInternal(message);
        }

        public void Disconnect(SshSessionException exception = null)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            if (_disconnected)
            {
                return;
            }

            _disconnected = true;

            try
            {
                _socket.Disconnect(false);
                _socket.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Noop
            }

            var exceptionMessage = exception is null ? "Unknown" : exception.Message;

            Disconnected?.Invoke(exceptionMessage);

            _registry.Close(exceptionMessage);
        }

        internal bool SendAuthenticationRequest(SshAuthenticationRequest request)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            var result = AuthenticationRequest?.Invoke(request);

            if (result.HasValue)
            {
                return result.Value;
            }

            request.IsSupportedMethod = false;

            return false;
        }

        internal void SendPtyRequest(SshPty pty)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            Pty = pty;

            PtyRequest?.Invoke(pty);
        }

        internal void SendShellRequest()
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            ShellRequest?.Invoke();
        }

        private ISshMessage ReadMessage()
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            var usingAlgorithms = _algorithms != null;

            var blockSize = (byte)(usingAlgorithms ? Math.Max(8, _algorithms.Client.Encryption.BlockBytesSize) : 8);
            var firstBlock = Read(blockSize);

            if (firstBlock == null)
            {
                return null;
            }

            if (usingAlgorithms)
            {
                firstBlock = _algorithms.Client.Encryption.Transform(firstBlock);
            }

            var packetLength = firstBlock.ElementAt(0) << 24 | firstBlock.ElementAt(1) << 16 | firstBlock.ElementAt(2) << 8 | firstBlock.ElementAt(3);
            var paddingLength = firstBlock.ElementAt(4);

            var bytesIncoming = packetLength - blockSize + 4;

            var nextBlocks = Read(bytesIncoming);

            if (usingAlgorithms)
            {
                nextBlocks = _algorithms.Client.Encryption.Transform(nextBlocks);
            }

            var packet = firstBlock.Concat(nextBlocks).ToArray();
            var data = packet.Skip(5).Take(packetLength - paddingLength);

            if (usingAlgorithms)
            {
                var clientHmac = Read(_algorithms.Client.Hmac.DigestLength);
                var hmac = ComputeHmac(_algorithms.Client.Hmac, packet, _incomingSequence);

                if (!clientHmac.SequenceEqual(hmac))
                {
                    throw new ApplicationException("Hmac has mismatch!");
                }

                data = _algorithms.Client.Compression.Decompress(data);
            }

            var messageType = (SshMessageType)data.ElementAt(0);

            var isMessageHandled = SshCore.MessageMapping.ContainsKey(messageType);

            if (!isMessageHandled)
            {
                throw new ApplicationException($"Message Type [{messageType}] is not handled!");
            }

            var messageObj = SshCore.GetMessageInstanceFromType(messageType);

            messageObj.Load(data);

            _incomingSequence++;
            _incomingBytes += (uint)packetLength;

            ShouldUpdateContexts();

            return messageObj;
        }

        private void SendMessageInternal(ISshMessage message)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            var usingAlgorithms = _algorithms != null;

            var blockSize = (byte)(usingAlgorithms ? Math.Max(8, _algorithms.Server.Encryption.BlockBytesSize) : 8);

            var payload = message.Write();

            if (usingAlgorithms)
            {
                payload = _algorithms.Server.Compression.Compress(payload);
            }

            // http://tools.ietf.org/html/rfc4253
            var paddingLength = (byte)(blockSize - (payload.Count() + 5) % blockSize);

            if (paddingLength < 4)
            {
                paddingLength += blockSize;
            }

            var length = (uint)payload.Count() + paddingLength + 1;

            var padding = new byte[paddingLength];

            SshCore.Rng.GetBytes(padding);

            var stream = new SshDataStream();

            stream.Write(length);
            stream.Write(paddingLength);
            stream.Write(payload);
            stream.Write(padding);

            payload = stream.ToByteArray();

            if (usingAlgorithms)
            {
                var hmac = ComputeHmac(_algorithms.Server.Hmac, payload, _outgoingSequence);
                payload = _algorithms.Server.Encryption.Transform(payload).Concat(hmac).ToArray();
            }

            Write(payload);

            _outgoingSequence++;
            _outgoingBytes += length;

            ShouldUpdateContexts();
        }

        private void ProcessMessage(ISshMessage message)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            switch (message)
            {
                case KeyExchangeInitializationMessage keyExchangeMessage:
                {
                    ShouldUpdateContexts(true);

                    _context.KeyExchange = SshCore.PickClientAlgorithm(SshCore.KeyExchangeAlgorithms.Keys, keyExchangeMessage.KeyExchangeAlgorithms);
                    _context.PublicKey = SshCore.PickClientAlgorithm(SshCore.PublicKeyAlgorithms.Keys, keyExchangeMessage.ServerHostKeyAlgorithms);
                    _context.Client.Encryption = SshCore.PickClientAlgorithm(SshCore.EncryptionAlgorithms.Keys, keyExchangeMessage.EncryptionAlgorithmsClientToServer);
                    _context.Server.Encryption = SshCore.PickClientAlgorithm(SshCore.EncryptionAlgorithms.Keys, keyExchangeMessage.EncryptionAlgorithmsServerToClient);
                    _context.Client.Hmac = SshCore.PickClientAlgorithm(SshCore.HmacAlgorithms.Keys, keyExchangeMessage.HmacAlgorithmsClientToServer);
                    _context.Server.Hmac = SshCore.PickClientAlgorithm(SshCore.HmacAlgorithms.Keys, keyExchangeMessage.HmacAlgorithmsServerToClient);
                    _context.Client.Compression = SshCore.PickClientAlgorithm(SshCore.CompressionAlgorithms.Keys, keyExchangeMessage.CompressionAlgorithmsClientToServer);
                    _context.Server.Compression = SshCore.PickClientAlgorithm(SshCore.CompressionAlgorithms.Keys, keyExchangeMessage.CompressionAlgorithmsServerToClient);

                    _context.Client.KeyExchangePayload = message.Write();
                }
                break;

                case DiffieHellmanInitializationMessage diffieHellmanMessage:
                {
                    var keyExchangeAlgorithm = SshCore.KeyExchangeAlgorithms[_context.KeyExchange]();
                    var hostKeyExchangeAlgorithm = SshCore.PublicKeyAlgorithms[_context.PublicKey](SshCore.ServerPublicKeys[_context.PublicKey]);
                    var clientCipher = SshCore.EncryptionAlgorithms[_context.Client.Encryption]();
                    var serverCipher = SshCore.EncryptionAlgorithms[_context.Server.Encryption]();
                    var clientHmac = SshCore.HmacAlgorithms[_context.Client.Hmac]();
                    var serverHmac = SshCore.HmacAlgorithms[_context.Server.Hmac]();

                    var clientExchange = diffieHellmanMessage.E;
                    var serverExchange = keyExchangeAlgorithm.CreateKeyExchange();

                    var sharedSecret = keyExchangeAlgorithm.DecryptKeyExchange(clientExchange.ToArray());
                    var hostKeyAndCertificates = hostKeyExchangeAlgorithm.CreateKeyAndCertificatesData();
                    var exchangeHash = ComputeExchangeHash(keyExchangeAlgorithm, hostKeyAndCertificates, clientExchange.ToArray(), serverExchange, sharedSecret);

                    SessionId ??= exchangeHash;

                    var clientCipherIv = ComputeEncryptionKey(keyExchangeAlgorithm, exchangeHash, clientCipher.BlockSize >> 3, sharedSecret, 'A');
                    var serverCipherIv = ComputeEncryptionKey(keyExchangeAlgorithm, exchangeHash, serverCipher.BlockSize >> 3, sharedSecret, 'B');
                    var clientCipherKey = ComputeEncryptionKey(keyExchangeAlgorithm, exchangeHash, clientCipher.KeySize >> 3, sharedSecret, 'C');
                    var serverCipherKey = ComputeEncryptionKey(keyExchangeAlgorithm, exchangeHash, serverCipher.KeySize >> 3, sharedSecret, 'D');
                    var clientHmacKey = ComputeEncryptionKey(keyExchangeAlgorithm, exchangeHash, clientHmac.KeySize >> 3, sharedSecret, 'E');
                    var serverHmacKey = ComputeEncryptionKey(keyExchangeAlgorithm, exchangeHash, serverHmac.KeySize >> 3, sharedSecret, 'F');

                    _context.ExchangedAlgorithms = new SshAlgorithms
                    {
                        KeyExchange = keyExchangeAlgorithm,
                        PublicKey = hostKeyExchangeAlgorithm,
                        Client = new SshAlgorithm
                        {
                            Encryption = clientCipher.Cipher(clientCipherKey, clientCipherIv, false),
                            Hmac = clientHmac.Hmac(clientHmacKey),
                            Compression = SshCore.CompressionAlgorithms[_context.Client.Compression]()
                        },
                        Server = new SshAlgorithm
                        {
                            Encryption = serverCipher.Cipher(serverCipherKey, serverCipherIv, true),
                            Hmac = serverHmac.Hmac(serverHmacKey),
                            Compression = SshCore.CompressionAlgorithms[_context.Server.Compression]()
                        }
                    };

                    var exchangeReply = new DiffieHellmanReplyMessage
                    {
                        HostKey = hostKeyAndCertificates,
                        F = serverExchange,
                        Signature = hostKeyExchangeAlgorithm.CreateSignatureData(exchangeHash)
                    };

                    SendMessage(exchangeReply);
                    SendMessage(new NewKeysRequestMessage());
                }
                break;

                case NewKeysRequestMessage _:
                {
                    _hasBlockedMessages.Reset();

                    _incomingBytes = 0;
                    _outgoingBytes = 0;
                    _algorithms = _context.ExchangedAlgorithms;
                    _context = null;

                    SendBlockedMessages();
                    _hasBlockedMessages.Set();
                }
                break;

                case ServiceRequestMessage serviceRequestMessage:
                {
                    var serviceName = serviceRequestMessage.Name;

                    if (!SshCore.ServiceMapping.ContainsKey(serviceName))
                    {
                        throw new ApplicationException($"Service requested {serviceName} is not serviceable...");
                    }

                    var serviceObj = _registry.Register(serviceName);

                    SendMessage(new ServiceAcceptMessage { Name = serviceObj.Name });
                }
                break;

                case IgnoreMessage _:
                {
                    // Ignore it.
                }
                break;

                case DisconnectMessage disconnectMsg:
                {
                    _logger.LogInformation("Client has requested disconnect for the following reason: " + disconnectMsg.Description);
                    Disconnect();
                }
                break;

                default:
                {
                    if (_registry != null && _registry.TryProcessMessage(message))
                    {
                        return;
                    }

                    throw new SshSessionException(SshSessionExceptionType.Unknown, $"Unknown message type: [{message}]");
                }
            }
        }

        private void SendBlockedMessages()
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            if (_blockedMessagesQueue.Count <= 0)
            {
                return;
            }

            while (_blockedMessagesQueue.TryDequeue(out var message))
            {
                SendMessageInternal(message);
            }
        }

        private byte[] ComputeEncryptionKey(KeyExchangeAlgorithm keyExchangeAlgorithm, byte[] exchangeHash, int blockSize, byte[] sharedSecret, char letter)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            var keyBuffer = new byte[blockSize];
            var keyBufferIndex = 0;
            byte[] currentHash = null;

            while (keyBufferIndex < blockSize)
            {
                using var stream = new SshDataStream();

                stream.WriteMpInt(sharedSecret);
                stream.Write(exchangeHash);

                if (currentHash == null)
                {
                    stream.Write((byte)letter);
                    stream.Write(SessionId);
                }
                else
                {
                    stream.Write(currentHash);
                }

                currentHash = keyExchangeAlgorithm.ComputeHash(stream.ToByteArray());

                var currentHashLength = Math.Min(currentHash.Length, blockSize - keyBufferIndex);

                Array.Copy(currentHash, 0, keyBuffer, keyBufferIndex, currentHashLength);

                keyBufferIndex += currentHashLength;
            }

            return keyBuffer;
        }

        private byte[] ComputeExchangeHash(KeyExchangeAlgorithm keyExchangeAlgorithm, byte[] hostKeyAndCertificates, byte[] clientExchange, byte[] serverExchange, byte[] sharedSecret)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            using var stream = new SshDataStream();

            stream.Write(ClientVersion, Encoding.ASCII);
            stream.Write(SshCore.ServerVersion, Encoding.ASCII);
            stream.WriteBinary(_context.Client.KeyExchangePayload.ToArray());
            stream.WriteBinary(_context.Server.KeyExchangePayload.ToArray());
            stream.WriteBinary(hostKeyAndCertificates);
            stream.WriteMpInt(clientExchange);
            stream.WriteMpInt(serverExchange);
            stream.WriteMpInt(sharedSecret);

            return keyExchangeAlgorithm.ComputeHash(stream.ToByteArray());
        }

        private IEnumerable<byte> Read(int length)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            var pos = 0;
            var buffer = new byte[length];

            var msSinceLastData = 0;

            while (pos < length)
            {
                try
                {
                    var asyncResult = _socket.BeginReceive(buffer, pos, length - pos, SocketFlags.None, null, null);

                    Wait(asyncResult, SshCore.ReadSocketTimeout);

                    if (!_socket.Connected)
                    {
                        throw new ApplicationException("Session socket connection lost");
                    }

                    var bytesReceived = _socket.EndReceive(asyncResult ?? throw new InvalidOperationException());

                    if (bytesReceived == 0 && _socket.Available == 0)
                    {
                        if (msSinceLastData >= SshCore.ReadSocketTimeout.TotalMilliseconds)
                        {
                            Disconnect(new SshSessionException(SshSessionExceptionType.SocketTimeout));

                            return null;
                        }

                        msSinceLastData += 50;

                        Thread.Sleep(50);
                    }
                    else
                    {
                        msSinceLastData = 0;
                    }

                    pos += bytesReceived;
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode != SocketError.WouldBlock && e.SocketErrorCode != SocketError.IOPending && e.SocketErrorCode != SocketError.NoBufferSpaceAvailable)
                    {
                        Thread.Sleep(30);
                    }
                    else if (e.SocketErrorCode == SocketError.ConnectionReset)
                    {
                        Disconnect(new SshSessionException(SshSessionExceptionType.SocketConnectionReset));

                        return null;
                    }
                    else
                    {
                        throw new ApplicationException($"Session socket threw an Exception: {e.Message}");
                    }
                }
                catch (SshSessionException sshSessionException)
                {
                    _logger.LogError($"Session Exception Thrown: {sshSessionException.Message}");

                    Disconnect(sshSessionException);

                    Disconnected?.Invoke(sshSessionException.Message);

                    return null;
                }
            }

            return buffer;
        }

        private void WriteAscii(string asciiString) => Write(Encoding.ASCII.GetBytes(asciiString));

        private void Write(IEnumerable<byte> data) => Write(data.ToArray());

        private void Write(byte[] data)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            var pos = 0;
            var length = data.Length;

            while (pos < length)
            {
                try
                {
                    var result = _socket.BeginSend(data, pos, length - pos, SocketFlags.None, null, null);

                    Wait(result, SshCore.WriteSocketTimeout);

                    pos += _socket.EndSend(result ?? throw new InvalidOperationException());
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.WouldBlock || ex.SocketErrorCode == SocketError.IOPending || ex.SocketErrorCode == SocketError.NoBufferSpaceAvailable)
                    {
                        Thread.Sleep(30);
                    }
                    else
                    {
                        Disconnect();

                        break;
                    }
                }
                catch (Exception exception)
                {
                    if (exception.Message.Contains("Cannot access a disposed object."))
                    {
                        Disconnect();
                    }

                    break;
                }
            }
        }

        private void ShouldUpdateContexts(bool forceUpdate = false)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            var isContextUpdated = false;

            if (_context == null && (forceUpdate || _incomingBytes + _outgoingBytes > 536870912))
            {
                _context = new SshSessionContext();
                isContextUpdated = true;
            }

            if (!isContextUpdated)
            {
                return;
            }

            var keyExchangeMessage = KeyExchangeInitializationMessage.Default();

            _context.Server.KeyExchangePayload = keyExchangeMessage.Write();

            SendMessage(keyExchangeMessage);
        }

        private void HandshakeVersions()
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            WriteAscii($"{SshCore.ServerVersion}\r\n");

            var buffer = new byte[255];
            var dummy = new byte[255];
            var pos = 0;

            while (pos < buffer.Length)
            {
                var result = _socket.BeginReceive(buffer, pos, buffer.Length - pos, SocketFlags.Peek, null, null);

                Wait(result, SshCore.ConnectSocketTimeout);

                var len = _socket.EndReceive(result ?? throw new InvalidOperationException());

                if (len == 0)
                {
                    throw new ApplicationException("Could not read a valid SSH version string from the client!");
                }

                for (var i = 0; i < len; i++, pos++)
                {
                    switch (pos > 0)
                    {
                        case true when buffer[pos - 1] == SshCore.CarriageReturn && buffer[pos] == SshCore.LineFeed:
                        {
                            _socket.Receive(dummy, 0, i + 1, SocketFlags.None);

                            ClientVersion = Encoding.ASCII.GetString(buffer, 0, pos - 1);

                            return;
                        }

                        case true when buffer[pos] == SshCore.LineFeed:
                        {
                            _socket.Receive(dummy, 0, i + 1, SocketFlags.None);

                            ClientVersion = Encoding.ASCII.GetString(buffer, 0, pos);

                            return;
                        }
                    }
                }

                _socket.Receive(dummy, 0, len, SocketFlags.None);
            }

            throw new ApplicationException("Could not read a valid SSH version string from the client!");
        }

        private static IEnumerable<byte> ComputeHmac(HmacAlgorithm algorithm, IEnumerable<byte> payload, uint sequence)
        {
            using var stream = new SshDataStream();

            stream.Write(sequence);
            stream.Write(payload);

            return algorithm.ComputeHash(stream.ToByteArray());
        }

        private static void Wait(IAsyncResult result, TimeSpan timeout)
        {
            if (timeout == TimeSpan.MaxValue)
            {
                result.AsyncWaitHandle.WaitOne();

                return;
            }

            if (!result.AsyncWaitHandle.WaitOne(timeout))
            {
                throw new SshSessionException(SshSessionExceptionType.SocketTimeout);
            }
        }
    }
}