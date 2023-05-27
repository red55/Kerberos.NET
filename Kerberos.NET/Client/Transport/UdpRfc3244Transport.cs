using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Text;

namespace Kerberos.NET.Transport
{
    public class UdpRfc3244Transport : UdpKerberosTransportBase
    {
        private static readonly string ServericeNameTemplate = "_kpasswd.udp";

        private ILogger Logger { get; }

        public UdpRfc3244Transport(ILoggerFactory loggerFactory) : base (loggerFactory, ServericeNameTemplate)
        {
            this.Enabled = true;
            this.Logger = loggerFactory.CreateLoggerSafe<UdpRfc3244Transport> ();
        }
    }
}
