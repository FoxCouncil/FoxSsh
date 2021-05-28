using FoxSsh.Common;
using FoxSsh.Server;
using System;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace SandboxApp
{
    public class SandboxSession
    {
        private readonly SshServerSession _sshSession;

        private Window _screen;

        public Guid Id => _sshSession.Id;

        public string Username { get; private set; }

        private readonly ILogger _logger;

        public SandboxSession(SshServerSession sshSession)
        {
            _logger = SshLog.Factory.CreateLogger<SandboxSession>();

            _sshSession = sshSession;
            _sshSession.AuthenticationRequest += AuthenticationRequestHandler;
            _sshSession.PtyRequest += PtyRegisteredHandler;
            _sshSession.Disconnected += Disconnected;
        }

        public void Stop()
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            _screen?.Stop();
        }

        private void Disconnected(string obj)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            Stop();
        }

        private void PtyRegisteredHandler(SshPty pty)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            _screen = new Window(this, pty);
        }

        private bool AuthenticationRequestHandler(SshAuthenticationRequest request)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

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
