//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using FoxSsh.Common;
using FoxSsh.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SandboxApp
{
    public class SandboxServer
    {
        private readonly ManualResetEvent _quitEvent = new(false);

        private readonly SshServer _sshServer;

        private readonly List<SandboxSession> _sessions = new();

        public SandboxServer()
        {
            SshCore.ReadSocketTimeout = TimeSpan.MaxValue;

            _sshServer = new SshServer();

            _sshServer.ClientConnected += ClientConnectedHandler;
            _sshServer.ClientDisconnected += ClientDisconnectedHandler;
        }

        public void Run()
        {
            _sshServer.Start();

            _quitEvent.WaitOne();

            _sshServer.Stop();
        }

        public void Stop()
        {
            _quitEvent.Set();
        }

        private void ClientConnectedHandler(SshServerSession session)
        {
            var newSession = new SandboxSession(session);

            _sessions.Add(newSession);

            // newSession.Run();
        }

        private void ClientDisconnectedHandler(SshServerSession session)
        {
            var oldSession = _sessions.FirstOrDefault(x => x.Id == session.Id);

            oldSession?.Stop();

            _sessions.Remove(oldSession);
        }
    }
}