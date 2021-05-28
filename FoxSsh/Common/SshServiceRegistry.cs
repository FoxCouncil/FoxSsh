//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using FoxSsh.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace FoxSsh.Common
{
    public class SshServiceRegistry
    {
        private readonly Dictionary<string, ISshService> _services = new Dictionary<string, ISshService>();

        public SshServerSession Session { get; }

        public IReadOnlyCollection<ISshService> Services => _services.Values;

        private readonly ILogger _logger;

        public SshServiceRegistry(SshServerSession session)
        {
            _logger = SshLog.Factory.CreateLogger<SshServiceRegistry>();

            Session = session;
        }

        public bool IsRegistered(string name)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            if (!SshCore.ServiceMapping.ContainsKey(name))
            {
                throw new ApplicationException($"Service {name} not found in ServiceMappings.");
            }

            return _services.ContainsKey(name);
        }

        public ISshService Register(string name)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            _logger.LogDebug($"Registering Service [{name}]");

            if (!SshCore.ServiceMapping.ContainsKey(name))
            {
                throw new ApplicationException($"Service {name} not found in ServiceMappings.");
            }

            if (_services.ContainsKey(name))
            {
                throw new ApplicationException($"Service {name} already registered.");
            }

            var newServiceObject = (ISshService)Activator.CreateInstance(SshCore.ServiceMapping[name]);

            newServiceObject.Registry = this;

            _services.Add(name, newServiceObject);

            return newServiceObject;
        }

        public void UnRegister(string name)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            if (!SshCore.ServiceMapping.ContainsKey(name))
            {
                throw new ApplicationException($"Service {name} not found in ServiceMappings.");
            }

            if (!_services.ContainsKey(name))
            {
                throw new ApplicationException($"Service {name} is not already registered?");
            }

            _services[name].Close("UnRegistering");
            _services.Remove(name);
        }

        public bool TryProcessMessage(ISshMessage message)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            return _services.Values.Any(service => service.TryParseMessage(message));
        }

        public void Close(string reason)
        {
            using var scope = _logger.BeginScope($"{GetType().Name}=>{MethodBase.GetCurrentMethod()?.Name}");

            if (!_services.Any())
            {
                return;
            }

            _services.Values.ToList().ForEach(x => x.Close(reason));
            _services.Clear();
        }
    }
}
