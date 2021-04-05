//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System;

namespace SandboxApp
{
    internal class Program
    {
        private static void Main()
        {
            var sandboxServer = new SandboxServer();

            Console.CancelKeyPress += (_, eArgs) =>
            {
                sandboxServer.Stop();

                eArgs.Cancel = true;
            };

            sandboxServer.Run();
        }
    }
}