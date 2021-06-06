//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using FoxSsh.Common;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FoxSsh.Server
{
    public sealed class SshServer : IDisposable
    {
        private readonly object _sessionsLock = new object();

        private readonly List<SshServerSession> _sessions = new List<SshServerSession>();

        private TcpListener _listener;

        private bool _isRunning;

        private bool _isDisposed;

        public event Action<SshServerSession> ClientConnected;

        public event Action<SshServerSession> ClientDisconnected;

        public IPAddress ListenAddress { get; }

        public int ListenPort { get; }

        private readonly ILogger _logger;

        public SshServer(IPAddress ipAddress = null, int port = SshCore.DefaultPort, ILoggerFactory loggerFactory = null)
        {
            ipAddress ??= IPAddress.IPv6Any;

            ListenAddress = ipAddress;

            ListenPort = port;

            // Update the global LoggerFactory, if it doesn't already exist
            if (loggerFactory != null && SshLog.Factory != null &&
                (SshLog.Factory.GetType() == typeof(NullLoggerFactory)))
            {
                SshLog.Factory = loggerFactory;
            }

            // Create a Logger
            _logger = SshLog.Factory.CreateLogger<SshServer>();

            _logger.LogTrace("Server Initialized");
        }

        public SshServer(ILoggerFactory loggerFactory = null)
        {
            ListenAddress = IPAddress.IPv6Any;

            ListenPort = SshCore.DefaultPort;

            // Update the global LoggerFactory, if it doesn't already exist
            if (loggerFactory != null && SshLog.Factory != null &&
                (SshLog.Factory.GetType() == typeof(NullLoggerFactory)))
            {
                SshLog.Factory = loggerFactory;
            }

            // Create a Logger
            _logger = SshLog.Factory.CreateLogger<SshServer>();

            _logger.LogTrace("Server Initialized");
        }

        public void Start()
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            _logger.LogTrace("Server Is Starting...");

            if (_isRunning)
            {
                throw new ApplicationException("Called Start when running on FoxSshServer!");
            }

            _listener = Equals(ListenAddress, IPAddress.IPv6Any) ? TcpListener.Create(ListenPort) : new TcpListener(ListenAddress, ListenPort);

            _listener.ExclusiveAddressUse = false;
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Start();

            BeginAcceptSocket();

            _isRunning = true;

            _logger.LogInformation($"Server Has Started {_listener.LocalEndpoint}");
        }

        public void Stop()
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            _logger.LogWarning($"Server is stopping... {_listener.LocalEndpoint}");

            if (!_isRunning)
            {
                _logger.LogWarning($"Called Stop when already stopped! { _listener.LocalEndpoint}");

                throw new ApplicationException("Called Stop when stopped on FoxSshServer!");
            }

            _listener.Stop();

            _isDisposed = false;
            _isRunning = false;

            // Clean up clients.
            lock (_sessionsLock)
            {
                foreach (var session in _sessions)
                {
                    session.Disconnect();
                }
            }
        }

        public void Dispose()
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            if (_isDisposed)
            {
                return;
            }

            Stop();
        }

        private void BeginAcceptSocket()
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            try
            {
                _listener.BeginAcceptSocket(AcceptSocket, null);
            }
            catch (ObjectDisposedException)
            {
            }
            catch
            {
                if (_isRunning)
                {
                    BeginAcceptSocket();
                }
            }
        }

        private void AcceptSocket(IAsyncResult ar)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            try
            {
                var socket = _listener.EndAcceptSocket(ar);

                Task.Run(() =>
                {
                    using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

                    _logger.LogTrace($"Thread {Thread.CurrentThread.ManagedThreadId} started!");

                    var session = new SshServerSession(socket);

                    lock (_sessionsLock)
                    {
                        _sessions.Add(session);
                    }

                    ClientConnected?.Invoke(session);

                    session.Connect();

                    ClientDisconnected?.Invoke(session);

                    _logger.LogTrace($"Thread {Thread.CurrentThread.ManagedThreadId} stopped!");
                });
            }
            finally
            {
                BeginAcceptSocket();
            }
        }
    }
}