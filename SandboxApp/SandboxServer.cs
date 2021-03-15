//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using FoxSsh.Common;
using FoxSsh.Common.Messages.Channel.Request;
using FoxSsh.Common.Services;
using FoxSsh.Server;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace SandboxApp
{
    public class SandboxServer
    {
        private readonly ManualResetEvent _quitEvent = new(false);

        private readonly SshServer _sshServer;

        private readonly List<SshServerSession> _sessions = new();

        public SandboxServer()
        {
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
            _sessions.Add(session);

            session.AuthenticationRequest += Session_AuthenticationRequestHandler;
            session.PtyRegistered += Session_PtyRegisteredHandler;
        }

        private void Session_PtyRegisteredHandler(SshPty pty)
        {
            pty.Clear();
            pty.Data += (s) => pty.Send(s);
        }

        private bool Session_AuthenticationRequestHandler(SshAuthenticationRequest request)
        {
            request.Banner = $"Welcome {request.Username},\n\nYou have reached The FoxSSH Sandbox Server.\n\nPlease login...\n\n";
            
            if (request.Method == SshCore.PasswordAuthenticationMethod)
            {
                request.IsSupportedMethod = true;

                if (request.Password == "hourglass")
                {
                    return true;
                }
            }

            return false;
        }

        private void ClientDisconnectedHandler(SshServerSession session)
        {
            _sessions.Remove(session);
        }
    }
}