//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FoxSsh.Common
{
    public static class SshLog
    {

        public static ILoggerFactory Factory = NullLoggerFactory.Instance;
    }
}
