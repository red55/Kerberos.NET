﻿using Microsoft.Extensions.Logging;

using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Kerberos.NET.Client;
using Kerberos.NET.Dns;
using Kerberos.NET.Configuration;
using System.Collections.Generic;

namespace Kerberos.NET.Transport
{
    public class TcpKerberosTransportBase : KerberosTransportBase
    {
        protected static readonly ISocketPool Pool = CreateSocketPool ();
        public static ISocketPool CreateSocketPool() => new SocketPool ();
        private ILogger Logger { get; }

        public TcpKerberosTransportBase (ILoggerFactory loggerFactory, string servicePrefix) : base (loggerFactory, servicePrefix)
        {
            this.Logger = loggerFactory.CreateLoggerSafe<TcpKerberosTransportBase> ();
        }

        public static int MaxPoolSize
        {
            get => Pool.MaxPoolSize;
            set => Pool.MaxPoolSize = value;
        }

        public static TimeSpan ScavengeWindow
        {
            get => Pool.ScavengeWindow;
            set => Pool.ScavengeWindow = value;
        }

        public override async Task<T> SendMessage<T>(
            string domain,
            ReadOnlyMemory<byte> encoded,
            CancellationToken cancellation = default
        )
        {
            try
            {
                using (var client = await this.GetClient (domain).ConfigureAwait (false))
                {
                    var stream = client.GetStream ();

                    await WriteMessage (encoded, stream, cancellation).ConfigureAwait (false);

                    return await ReadResponse<T> (stream, cancellation, this.ReceiveTimeout).ConfigureAwait (false);
                }
            }
            catch (SocketException sx)
            {
                this.Logger.LogDebug (sx, "TCP Socket exception during Connect {SocketCode}", sx.SocketErrorCode);

                throw new KerberosTransportException ("TCP Connect failed", sx);
            }
        }
        /*
        protected override async Task<DnsRecord> LocatePreferredServer(string domain, string servicePrefix)
        {
            this.Configuration.Realms.TryGetValue (domain, out Krb5RealmConfig config);
            ICollection<string> kdc = config?.Kdc;

            return await LocatePreferredServer (domain, servicePrefix, kdc);
        }*/

        protected async Task<ITcpSocket> GetClient(string domain)
        {
            var attempts = this.MaximumAttempts;
            SocketException lastThrown = null;

            do
            {
                var target = await this.LocatePreferredServer (domain, KerberosServicePrefix);

                this.Logger.LogTrace ("TCP connecting to {Target} on port {Port}", target.Target, target.Port);

                ITcpSocket client = null;

                bool connected = false;

                try
                {
                    client = await Pool.Request (target, this.ConnectTimeout).ConfigureAwait (false);

                    if (client != null)
                    {
                        connected = true;
                    }
                }
                catch (SocketException ex)
                {
                    lastThrown = ex;
                }

                if (!connected)
                {
                    lastThrown = lastThrown ?? new SocketException ((int)SocketError.TimedOut);

                    this.ClientRealmService.NegativeCache (target);

                    continue;
                }

                this.Logger.LogDebug ("TCP connected to {Target} on port {Port}", target.Target, target.Port);

                client.SendTimeout = this.SendTimeout;
                client.ReceiveTimeout = this.ReceiveTimeout;

                return client;
            }
            while (--attempts > 0);

            throw lastThrown;
        }


        private static async Task<T> ReadResponse<T>(NetworkStream stream, CancellationToken cancellation, TimeSpan readTimeout)
            where T : Asn1.IAsn1ApplicationEncoder<T>, new()
        {
            using (var messageSizeBytesRented = CryptoPool.Rent<byte> (4))
            {
                var messageSizeBytes = messageSizeBytesRented.Memory.Slice (0, 4);

                await Tcp.ReadFromStream (messageSizeBytes, stream, cancellation, readTimeout).ConfigureAwait (false);

                var messageSize = BinaryPrimitives.ReadInt32BigEndian (messageSizeBytes.Span);

                var response = await Tcp.ReadFromStream (messageSize, stream, cancellation, readTimeout).ConfigureAwait (false);

                return Decode<T> (response);
            }
        }

        private static async Task WriteMessage(ReadOnlyMemory<byte> encoded, NetworkStream stream, CancellationToken cancellation)
        {
            var length = encoded.Length + 4;

            using (var messageRented = CryptoPool.Rent<byte> (length))
            {
                var message = messageRented.Memory.Slice (0, length);

                Tcp.FormatKerberosMessageStream (encoded, message);

                await stream.WriteAsync (message, cancellation).ConfigureAwait (false);
            }
        }
    }

}
