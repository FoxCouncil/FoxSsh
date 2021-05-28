//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using FoxSsh.Common.Messages.Authentication;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace FoxSsh.Common.Services
{
    public class AuthenticationService : ISshService
    {
        private bool _sentBanner;

        public static IEnumerable<string> SupportedMethods = new List<string> { "password", "publickey" };

        public string Name => SshCore.ServiceAuthenticationName;

        public SshServiceRegistry Registry { get; set; }

        private readonly ILogger _logger;

        public AuthenticationService()
        {
            _logger = SshLog.Factory.CreateLogger<AuthenticationService>();
        }

        public void Close(string reason)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");
        }

        public bool TryParseMessage(ISshMessage message)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            if (!(message.Type >= SshMessageType.UserAuthRequest && message.Type <= SshMessageType.UserAuthBanner))
            {
                return false;
            }

            ProcessMessage(message as AuthenticationServiceRequestMessage);

            return true;
        }

        private void ProcessMessage(AuthenticationServiceRequestMessage message)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            var authRequest = SshAuthenticationRequest.FromRequestMessage(message);

            var result = Registry.Session.SendAuthenticationRequest(authRequest);

            if (!string.IsNullOrWhiteSpace(authRequest.Banner) && !_sentBanner)
            {
                Registry.Session.SendMessage(new AuthenticationBannerMessage { Text = authRequest.Banner });

                _sentBanner = true;
            }

            if (!authRequest.IsSupportedMethod)
            {
                _logger.LogDebug($"Authentication Service [Type:{message.MethodName}] Not Valid, Sending [Types:{string.Join(",", SupportedMethods)}]");

                Registry.Session.SendMessage(new AuthenticationFailureMessage());

                return;
            }

            _logger.LogDebug($"Authentication Service [Type:{message.MethodName}] [Result:{result}]");

            if (result)
            {
                var connectionService = (ConnectionService)Registry.Register(SshCore.ServiceConnectionName);

                connectionService.PtyRegistered += Registry.Session.SendPtyRequest;

                Registry.Session.SendMessage(new AuthenticationSuccessMessage());
            }
            else
            {
                Registry.Session.SendMessage(new AuthenticationFailureMessage());
            }
        }
    }
}
