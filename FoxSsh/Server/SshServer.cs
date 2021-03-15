//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using FoxSsh.Common;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

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

        public IPAddress ListenAddress { get; private set; }

        public int ListenPort { get; private set; }

        public SshServer(IPAddress ipAddress = null, int port = SshCore.DefaultPort)
        {
            if (ipAddress == null)
            {
                ipAddress = IPAddress.IPv6Any;
            }

            ListenAddress = ipAddress;

            ListenPort = port;

            LogLine(SshLogLevel.Info, "Server Initialized");
        }

        public static void LogLine(SshLogLevel level, string log) => SshLog.WriteLine(level, $"-[SERVER] {log}");

        public void Start()
        {
            LogLine(SshLogLevel.Info, "Server Is Starting...");

            if (_isRunning)
            {
                throw new ApplicationException("Called Start when running on FoxSshServer!");
            }

            if (ListenAddress == IPAddress.IPv6Any)
            {
                _listener = TcpListener.Create(ListenPort);
            }
            else
            {
                _listener = new TcpListener(ListenAddress, ListenPort);
            }

            _listener.ExclusiveAddressUse = false;
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Start();

            BeginAcceptSocket();

            _isRunning = true;

            LogLine(SshLogLevel.Info, $"Server Has Started {_listener.LocalEndpoint}");
        }

        public void Stop()
        {
            if (!_isRunning)
            {
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
            if (_isDisposed)
            {
                return;
            }

            Stop();
        }

        private void BeginAcceptSocket()
        {
            try
            {
                _listener.BeginAcceptSocket(AcceptSocket, null);
            }
            catch (ObjectDisposedException)
            {
                return;
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
            try
            {
                var socket = _listener.EndAcceptSocket(ar);

                Task.Run(() =>
                {
                    LogLine(SshLogLevel.Info, $"Thread {Thread.CurrentThread.ManagedThreadId} started!");

                    var session = new SshServerSession(socket);

                    lock (_sessionsLock)
                    {
                        _sessions.Add(session);
                    }

                    ClientConnected?.Invoke(session);

                    session.Connect();

                    ClientDisconnected?.Invoke(session);

                    LogLine(SshLogLevel.Info, $"Thread {Thread.CurrentThread.ManagedThreadId} stopped!");
                });
            }
            finally
            {
                BeginAcceptSocket();
            }
        }
    }
}