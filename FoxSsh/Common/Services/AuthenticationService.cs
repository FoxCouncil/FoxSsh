//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using FoxSsh.Common.Messages.Authentication;
using System.Collections.Generic;

namespace FoxSsh.Common.Services
{
    public class AuthenticationService : ISshService
    {
        private bool _sentBanner;

        public static IEnumerable<string> SupportedMethods = new List<string> { "password", "publickey" };

        public string Name => SshCore.ServiceAuthenticationName;

        public SshServiceRegistry Registry { get; set; }

        public void Close(string reason) { }

        public bool TryParseMessage(ISshMessage message)
        {
            if (!(message.Type >= SshMessageType.UserAuthRequest && message.Type <= SshMessageType.UserAuthBanner))
            {
                return false;
            }

            ProcessMessage(message as AuthenticationServiceRequestMessage);

            return true;
        }

        private void ProcessMessage(AuthenticationServiceRequestMessage message)
        {
            var authRequest = SshAuthenticationRequest.FromRequestMessage(message);

            var result = Registry.Session.SendAuthenticationRequest(authRequest);

            if (!string.IsNullOrWhiteSpace(authRequest.Banner) && !_sentBanner)
            {
                Registry.Session.SendMessage(new AuthenticationBannerMessage { Text = authRequest.Banner });

                _sentBanner = true;
            }

            if (!authRequest.IsSupportedMethod)
            {
                Registry.Session.LogLine(SshLogLevel.Debug, $"Authentication Service [Type:{message.MethodName}] Not Valid, Sending [Types:{string.Join(",", SupportedMethods)}]");

                Registry.Session.SendMessage(new AuthenticationFailureMessage());

                return;
            }

            Registry.Session.LogLine(SshLogLevel.Debug, $"Authentication Service [Type:{message.MethodName}] [Result:{result}]");

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
