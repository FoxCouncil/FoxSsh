// !! // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License | | // Copyright 2021 The Fox Council

using FoxSsh.Common;
using FoxSsh.Common.Crypto;
using FoxSsh.Common.Messages;
using FoxSsh.Common.Messages.Channel.Request;
using FoxSsh.Common.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace FoxSsh.Server
{
    public class SshServerSession
    {
        private readonly Socket _socket;

        private uint _incomingBytes;
        private uint _incomingSequence;

        private uint _outgoingBytes;
        private uint _outgoingSequence;

        private readonly SshServiceRegistry _registry;

        private SshSessionContext _context;

        private SshAlgorithms _algorithms;

        private readonly EndPoint _endPoint;

        private readonly ConcurrentQueue<ISshMessage> _blockedMessagesQueue = new ConcurrentQueue<ISshMessage>();

        private readonly EventWaitHandle _hasBlockedMessages = new ManualResetEvent(true);

        public event Func<SshAuthenticationRequest, bool> AuthenticationRequest;

        public event Action<SshPty> PtyRegistered;

        public event Action<string> Disconnected;

        public Guid Id { get; } = Guid.NewGuid();

        public string ClientVersion { get; private set; }

        public IEnumerable<byte> SessionId { get; private set; }

        public SshPty Pty { get; internal set; }

        public SshServerSession(Socket socket)
        {
            _socket = socket;
            _endPoint = _socket.RemoteEndPoint;

            _registry = new SshServiceRegistry(this);

            LogLine(SshLogLevel.Info, $"Session & Service Registry Created For {_endPoint}");
        }

        public void LogLine(SshLogLevel level, string log) => SshLog.WriteLine(level, $"-[S:{Id}] {log}");

        public void Connect()
        {
            if (!_socket.Connected)
            {
                return;
            }

            const int socketBufferSize = 2 * SshCore.MaximumSshPacketSize;

            _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            _socket.LingerState = new LingerOption(enable: false, seconds: 0);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, socketBufferSize);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, socketBufferSize);
            _socket.ReceiveTimeout = (int)SshCore.SocketTimeout.TotalMilliseconds;

            LogLine(SshLogLevel.Info, "Socket Settings Initiated");

            HandshakeVersions();

            if (!Regex.IsMatch(ClientVersion, "SSH-2.0-.+"))
            {
                throw new ApplicationException($"FoxSsh's server cannot accept connections from clients like: {ClientVersion}. We only support SSH2.");
            }

            LogLine(SshLogLevel.Info, $"Socket Client Verified {ClientVersion}");

            ShouldUpdateContexts(true);

            try
            {
                while (_socket != null && _socket.Connected)
                {
                    var message = ReadMessage();

                    if (message == null)
                    {
                        continue;
                    }

                    LogLine(SshLogLevel.Info, $"Processing Message {message.Type}");

                    ProcessMessage(message);
                }
            }
            finally
            {
            }

            _registry.Close();

            LogLine(SshLogLevel.Info, $"Socket Disconnecting For {_endPoint}");
        }

        public void SendMessage(ISshMessage message)
        {
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
            _socket.Disconnect(false);

            Disconnected?.Invoke(exception is null ? "Unknown" : exception.Message);
        }

        internal bool SendAuthenticationRequest(SshAuthenticationRequest request)
        {
            var result = AuthenticationRequest?.Invoke(request);

            if (result.HasValue)
            {
                return result.Value;
            }

            request.IsSupportedMethod = false;

            return false;
        }

        internal void SendPtyRegistration(SshPty pty)
        {
            Pty = pty;

            PtyRegistered?.Invoke(pty);
        }

        private ISshMessage ReadMessage()
        {
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
                throw new ApplicationException($"Message Type {messageType} is not handled!");
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
            var usingAlgorithms = _algorithms != null;

            var blockSize = (byte)(usingAlgorithms ? Math.Max(8, _algorithms.Server.Encryption.BlockBytesSize) : 8);

            var payload = message.Write();

            if (usingAlgorithms)
            {
                payload = _algorithms.Server.Compression.Compress(payload);
            }

            // http://tools.ietf.org/html/rfc4253
            // 6.  Binary Packet Protocol the total length of (packet_length || padding_length ||
            // payload || padding) is a multiple of the cipher block size or 8, padding length must
            // between 4 and 255 bytes.
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
            if (_registry != null && _registry.TryProcessMessage(message))
            {
                return;
            }

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

                    if (SessionId == null)
                    {
                        SessionId = exchangeHash;
                    }

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
                        throw new ApplicationException($"Service requested {serviceName} is not servicable...");
                    }

                    var serviceObj = _registry.Register(serviceName);

                    SendMessage(new ServiceAcceptMessage { Name = serviceObj.Name });
                }
                break;
            }
        }

        private void SendBlockedMessages()
        {
            if (_blockedMessagesQueue.Count > 0)
            {
                while (_blockedMessagesQueue.TryDequeue(out ISshMessage message))
                {
                    SendMessageInternal(message);
                }
            }
        }

        private byte[] ComputeEncryptionKey(KeyExchangeAlgorithm keyExchangeAlgorithm, byte[] exchangeHash, int blockSize, byte[] sharedSecret, char letter)
        {
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

                int currentHashLength = Math.Min(currentHash.Length, blockSize - keyBufferIndex);
                Array.Copy(currentHash, 0, keyBuffer, keyBufferIndex, currentHashLength);

                keyBufferIndex += currentHashLength;
            }

            return keyBuffer;
        }

        private byte[] ComputeExchangeHash(KeyExchangeAlgorithm keyExchangeAlgorithm, byte[] hostKeyAndCertificates, byte[] clientExchange, byte[] serverExchange, byte[] sharedSecret)
        {
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
            var pos = 0;
            var buffer = new byte[length];

            var msSinceLastData = 0;

            while (pos < length)
            {
                try
                {
                    var asyncResult = _socket.BeginReceive(buffer, pos, length - pos, SocketFlags.None, null, null);

                    Wait(asyncResult);

                    var bytesReceived = _socket.EndReceive(asyncResult);

                    if (!_socket.Connected)
                    {
                        throw new ApplicationException("Session socket connection lost");
                    }

                    if (bytesReceived == 0 && _socket.Available == 0)
                    {
                        if (msSinceLastData >= SshCore.SocketTimeout.TotalMilliseconds)
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
                    if (e.SocketErrorCode == SocketError.WouldBlock || e.SocketErrorCode == SocketError.IOPending || e.SocketErrorCode == SocketError.NoBufferSpaceAvailable)
                    {
                        Thread.Sleep(30);
                    }
                    else
                    {
                        if (e.SocketErrorCode == SocketError.ConnectionReset)
                        {
                            Disconnect(new SshSessionException(SshSessionExceptionType.SocketConnectionReset));

                            return null;
                        }
                        else
                        {
                            throw new ApplicationException($"Session socket threw an Exception: {e.Message}");
                        }
                    }
                }
                catch (SshSessionException sessEx)
                {
                    LogLine(SshLogLevel.Info, $"Session Exception Thrown: {sessEx.Message}");

                    Disconnect(sessEx);

                    Disconnected?.Invoke(sessEx.Message);

                    return null;
                }
            }

            return buffer;
        }

        private void WriteAscii(string asciiString) => Write(Encoding.ASCII.GetBytes(asciiString));

        private void Write(IEnumerable<byte> data) => Write(data.ToArray());

        private void Write(byte[] data)
        {
            var pos = 0;
            var length = data.Length;

            while (pos < length)
            {
                try
                {
                    var result = _socket.BeginSend(data, pos, length - pos, SocketFlags.None, null, null);

                    Wait(result);

                    pos += _socket.EndSend(result);
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
                    }
                }
            }
        }

        private void ShouldUpdateContexts(bool forceUpdate = false)
        {
            var isContextUpdated = false;

            if (_context == null && (forceUpdate || _incomingBytes + _outgoingBytes > 536870912))
            {
                _context = new SshSessionContext();
                isContextUpdated = true;
            }

            if (isContextUpdated)
            {
                var keyExchangeMessage = KeyExchangeInitializationMessage.Default();

                _context.Server.KeyExchangePayload = keyExchangeMessage.Write();

                SendMessage(keyExchangeMessage);
            }
        }

        private void HandshakeVersions()
        {
            WriteAscii($"{SshCore.ServerVersion}\r\n");

            var buffer = new byte[255];
            var dummy = new byte[255];
            var pos = 0;
            var len = 0;

            while (pos < buffer.Length)
            {
                var result = _socket.BeginReceive(buffer, pos, buffer.Length - pos, SocketFlags.Peek, null, null);

                Wait(result);

                len = _socket.EndReceive(result);

                if (len == 0)
                {
                    throw new ApplicationException("Could not read a valid SSH version string from the client!");
                }

                for (var i = 0; i < len; i++, pos++)
                {
                    if (pos > 0 && buffer[pos - 1] == SshCore.CarriageReturn && buffer[pos] == SshCore.LineFeed)
                    {
                        _socket.Receive(dummy, 0, i + 1, SocketFlags.None);

                        ClientVersion = Encoding.ASCII.GetString(buffer, 0, pos - 1);

                        return;
                    }
                    else if (pos > 0 && buffer[pos] == SshCore.LineFeed)
                    {
                        _socket.Receive(dummy, 0, i + 1, SocketFlags.None);

                        ClientVersion = Encoding.ASCII.GetString(buffer, 0, pos);

                        return;
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

        private void Wait(IAsyncResult result)
        {
            if (!result.AsyncWaitHandle.WaitOne(SshCore.SocketTimeout))
            {
                throw new SshSessionException(SshSessionExceptionType.SocketTimeout);
            }
        }
    }
}