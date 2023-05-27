

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Text;

namespace Kerberos.NET.Transport
{
    public class UdpKerberosTransport : UdpKerberosTransportBase
    {
        private static readonly string ServericeNameTemplate = "_kerberos.udp";

        private ILogger Logger { get; }

        public UdpKerberosTransport(ILoggerFactory logger) : base (logger, ServericeNameTemplate)
        {
            this.Enabled = true;
            this.Logger = logger.CreateLoggerSafe<UdpKerberosTransport> ();
        }
    }
}
