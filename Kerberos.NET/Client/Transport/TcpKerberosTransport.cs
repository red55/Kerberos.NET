// -----------------------------------------------------------------------
// Licensed to The .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// -----------------------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace Kerberos.NET.Transport
{
    public class TcpKerberosTransport : TcpKerberosTransportBase
    {
        private const string TcpServicePrefix = "_kerberos._tcp";
        private ILogger Logger { get; }

        public TcpKerberosTransport(ILoggerFactory loggerFactory)
            : base(loggerFactory, TcpServicePrefix)
        {
            this.Enabled = true;
            this.Logger = loggerFactory.CreateLoggerSafe<TcpKerberosTransport> ();
        }        

    }
}
