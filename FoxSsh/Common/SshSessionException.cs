//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System;

namespace FoxSsh.Common
{
    public class SshSessionException : Exception
    {
        public SshSessionExceptionType Type { get; }

        public override string Message => string.IsNullOrWhiteSpace(base.Message) || base.Message.Contains(GetType().Name) ? Type.ToString() : base.Message;

        public SshSessionException()
        {
            Type = SshSessionExceptionType.Unknown;
        }

        public SshSessionException(string message) : base(message)
        {
            Type = SshSessionExceptionType.Unknown;
        }

        public SshSessionException(string message, Exception innerException) : base(message, innerException)
        {
            Type = SshSessionExceptionType.Unknown;
        }

        public SshSessionException(SshSessionExceptionType type)
        {
            Type = type;
        }

        public SshSessionException(SshSessionExceptionType type, string message) : base(message) 
        {
            Type = type;
        }

        public SshSessionException(SshSessionExceptionType type, string message, Exception innerException) : base(message, innerException)
        { 
            Type = type;
        }
    }
}
