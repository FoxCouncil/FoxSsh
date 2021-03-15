//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System;

namespace FoxSsh.Common
{
    public class SshCoreException : Exception
    {
        public SshCoreException() { }

        public SshCoreException(string message) : base(message) { }

        public SshCoreException(string message, Exception innerException) : base(message, innerException) { }
    }
}