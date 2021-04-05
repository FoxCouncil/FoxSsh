using FoxSsh.Common;
using FoxSsh.Server;
using System;

namespace SandboxApp
{
    public class SandboxSession
    {
        private readonly SshServerSession _sshSession;

        private Window _screen;

        public Guid Id => _sshSession.Id;

        public string Username { get; private set; }

        public SandboxSession(SshServerSession sshSession)
        {
            _sshSession = sshSession;
            _sshSession.AuthenticationRequest += AuthenticationRequestHandler;
            _sshSession.PtyRequest += PtyRegisteredHandler;
            _sshSession.Disconnected += Disconnected;
        }

        public void Stop()
        {
            _screen?.Stop();
        }

        private void Disconnected(string obj)
        {
            Stop();
        }

        private void PtyRegisteredHandler(SshPty pty)
        {
            _screen = new Window(this, pty);
        }

        private bool AuthenticationRequestHandler(SshAuthenticationRequest request)
        {
            request.Banner = $"Welcome {request.Username},\n\nYou have reached The FoxSSH Sandbox Server.\n\nPlease login...\n\n";

            if (request.Method != SshCore.PasswordAuthenticationMethod)
            {
                return false;
            }

            request.IsSupportedMethod = true;

            if (request.Password != "hourglass")
            {
                return false;
            }

            Username = request.Username;

            return true;
        }
    }
}
