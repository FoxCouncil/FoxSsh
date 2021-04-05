//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using FoxSsh.Common.Crypto;
using FoxSsh.Common.Messages.Authentication;

namespace FoxSsh.Common
{
    public class SshAuthenticationRequest
    {
        public string Banner { get; set; }

        public bool IsSupportedMethod { get; set; }

        public string Method { get; set; }

        public string Password { get; set; }

        public PublicKeyRequest PublicKey { get; set; }

        public string Username { get; set; }

        internal static SshAuthenticationRequest FromRequestMessage(AuthenticationServiceRequestMessage message)
        {
            return new SshAuthenticationRequest
            {
                IsSupportedMethod = message.IsSupportedMethod,
                Method = message.MethodName,
                Password = message.Password,
                PublicKey = message.PublicKey,
                Username = message.Username
            };
        }
    }
}
