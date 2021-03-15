//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using FoxSsh.Server;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FoxSsh.Common
{
    public class SshServiceRegistry
    {
        private readonly Dictionary<string, ISshService> _services = new Dictionary<string, ISshService>();

        public SshServerSession Session { get; private set; }

        public IReadOnlyCollection<ISshService> Services => _services.Values;

        public SshServiceRegistry(SshServerSession session)
        {
            Session = session;
        }

        public bool IsRegistered(string name)
        {
            if (!SshCore.ServiceMapping.ContainsKey(name))
            {
                throw new ApplicationException($"Service {name} not found in ServiceMapping's.");
            }

            return _services.ContainsKey(name);
        }

        public ISshService Register(string name)
        {
            Session.LogLine(SshLogLevel.Info, $"Registering Service [{name}]");

            if (!SshCore.ServiceMapping.ContainsKey(name))
            {
                throw new ApplicationException($"Service {name} not found in ServiceMapping's.");
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

        public void Unregister(string name)
        {
            if (!SshCore.ServiceMapping.ContainsKey(name))
            {
                throw new ApplicationException($"Service {name} not found in ServiceMapping's.");
            }

            if (!_services.ContainsKey(name))
            {
                throw new ApplicationException($"Service {name} is not already registered?");
            }

            _services[name].Close();
            _services.Remove(name);
        }

        public bool TryProcessMessage(ISshMessage message)
        {
            foreach (var service in _services.Values)
            {
                if (service.TryParseMessage(message))
                {
                    return true;
                }
            }

            return false;
        }

        public void Close()
        {
            _services.Values.ToList().ForEach(x => x.Close());
            _services.Clear();
        }
    }
}
