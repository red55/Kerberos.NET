using Kerberos.NET.Transport;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Text;

namespace Kerberos.NET.Client.Transport
{
    internal class TcpRfc3244Transport : TcpKerberosTransportBase
    {
        private const string TcpServiceTemplate = "_kpasswd._tcp";

        private ILogger Logger { get; }

        public TcpRfc3244Transport(ILoggerFactory loggerFactory) : base (loggerFactory, TcpServiceTemplate)
        {
            this.Enabled = true;
            this.Logger = loggerFactory.CreateLoggerSafe<TcpRfc3244Transport> ();
        }


    }
}
