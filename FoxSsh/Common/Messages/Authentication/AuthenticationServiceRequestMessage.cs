//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using FoxSsh.Common.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FoxSsh.Common.Messages.Authentication
{
    public class AuthenticationServiceRequestMessage : ISshMessage
    {
        public SshMessageType Type => SshMessageType.UserAuthRequest;

        public IReadOnlyCollection<byte> Raw { get; set; }

        public string Username { get; private set; }

        public string ServiceName { get; private set; }

        public string MethodName { get; private set; }

        public PublicKeyRequest PublicKey { get; private set; }

        public string Password { get; private set; }

        public bool IsSupportedMethod { get; private set; }

        public void LoadRawData(SshDataStream stream) 
        {
            Username = stream.ReadStringUtf8();
            ServiceName = stream.ReadStringAscii();
            MethodName = stream.ReadStringAscii();

            switch (MethodName)
            {
                case "publickey":
                {
                    var keyReq = new PublicKeyRequest
                    {
                        HasSignature = stream.ReadBoolean(),
                        AlgorithmName = stream.ReadStringAscii(),
                        Key = stream.ReadBinary()
                    };

                    if (keyReq.HasSignature)
                    {
                        keyReq.Signature = stream.ReadBinary();
                        keyReq.PayloadWithoutSignature = Raw.Take(Raw.Count - keyReq.Signature.Count - 5).ToArray();
                    }

                    PublicKey = keyReq;

                    IsSupportedMethod = true;
                }
                break;

                case "password":
                {
                    var isEmpty = stream.ReadBoolean();

                    Password = stream.ReadStringAscii();

                    IsSupportedMethod = true;
                }
                break;

                case "none":
                {
                    IsSupportedMethod = SshCore.AnonymousAccess;
                }
                break;

                case "hostbased":
                {
                    // Not Supported Yet
                }
                break;

                default: throw new ApplicationException($"{MethodName} is an not a supported authentication...");
            }
        }

        public void WriteRawData(SshDataStream stream) { }
    }
}
