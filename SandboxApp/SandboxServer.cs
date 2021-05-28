//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using FoxSsh.Common;
using FoxSsh.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace SandboxApp
{
    public class SandboxServer
    {
        private readonly ManualResetEvent _quitEvent = new(false);

        private readonly SshServer _sshServer;

        private readonly List<SandboxSession> _sessions = new();

        private readonly ILogger _logger;

        private readonly ILoggerFactory loggerFactory;

        public SandboxServer()
        {
            SshCore.ReadSocketTimeout = TimeSpan.MaxValue;

            loggerFactory = LoggerFactory.Create(builder =>
                    builder
                        .AddSimpleConsole(options =>
                        {
                            options.IncludeScopes = true;                                           // Disable to reduce verbosity
                            options.SingleLine = false;                                             // Multiple line separates the timestamp, scope, and message and is more readable in the console.
                            options.TimestampFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.fffffffZ ";    // Custom, or any of https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings
                            options.UseUtcTimestamp = true;                                         // Set to false for local timezone
                        })
                        .SetMinimumLevel(LogLevel.Trace));
            //loggerFactory.AddFile("Logs/{Date}.log");         // Example of adding file logging using Serilog via https://www.nuget.org/packages/serilog.extensions.logging.file

            _logger = loggerFactory.CreateLogger<SandboxServer>();

            _sshServer = new SshServer(loggerFactory);

            _sshServer.ClientConnected += ClientConnectedHandler;
            _sshServer.ClientDisconnected += ClientDisconnectedHandler;
        }

        public void Run()
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            _sshServer.Start();

            _quitEvent.WaitOne();

            _sshServer.Stop();
        }

        public void Stop()
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            _quitEvent.Set();
        }

        private void ClientConnectedHandler(SshServerSession session)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            var newSession = new SandboxSession(session);

            _sessions.Add(newSession);

            // newSession.Run();
        }

        private void ClientDisconnectedHandler(SshServerSession session)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            var oldSession = _sessions.FirstOrDefault(x => x.Id == session.Id);

            oldSession?.Stop();

            _sessions.Remove(oldSession);
        }
    }
}