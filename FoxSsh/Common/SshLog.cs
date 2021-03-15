//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System;
using System.Diagnostics;
using System.Threading;

namespace FoxSsh.Common
{
    public static class SshLog
    {
        public static SshLogLevel LogLevel { get; set; } = Debugger.IsAttached ? SshLogLevel.Debug : SshLogLevel.Error;

        public static void WriteLine(SshLogLevel level, string line)
        {
            if (level <= LogLevel)
            {
                Console.WriteLine($"[{DateTime.UtcNow:O}]-[{SshCore.ProductName}]-[T:{Thread.CurrentThread.ManagedThreadId}]-[{level.ToString().ToUpper()}]{line}");
            }
        }
    }

    public enum SshLogLevel
    {
        None,
        Error,
        Info,
        Message,
        Trace,
        Debug,
        All
    }
}
