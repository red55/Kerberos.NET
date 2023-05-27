using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Kerberos.NET.Configuration;
using Kerberos.NET.Dns;
using Microsoft.Extensions.Logging;
using static System.FormattableString;

namespace Kerberos.NET.Transport
{
    public class ClientDomainService
    {
        public ClientDomainService(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLoggerSafe<ClientDomainService>();
        }

        internal const int DefaultKerberosPort = 88;
        internal const int DefaultKpasswdPort  = 464;

        private static readonly Task CacheCleanup;

        private static readonly ConcurrentDictionary<string, DnsRecord> DomainCache
            = new ConcurrentDictionary<string, DnsRecord>(StringComparer.InvariantCultureIgnoreCase);

        private static readonly ConcurrentDictionary<string, DateTimeOffset> DomainServiceNegativeCache
            = new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.InvariantCultureIgnoreCase);

        private readonly Dictionary<string, HashSet<string>> pinnedKdcs
            = new Dictionary<string, HashSet<string>>(StringComparer.InvariantCultureIgnoreCase);

        private readonly ConcurrentDictionary<string, DnsRecord> negativeCache
            = new ConcurrentDictionary<string, DnsRecord>(StringComparer.InvariantCultureIgnoreCase);

        private readonly ILogger logger;

        static ClientDomainService()
        {
            CacheCleanup = Task.Run(MonitorDnsCache).ContinueWith(t => t.Dispose(), TaskScheduler.Default);
        }

        public static TimeSpan CacheCleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

        public Krb5Config Configuration { get; set; }

        public void ResetConnections()
        {
            DomainCache.Clear();
            DomainServiceNegativeCache.Clear();
            this.pinnedKdcs.Clear();
            this.negativeCache.Clear();
        }

        public virtual async Task<IEnumerable<DnsRecord>> LocateKdc(string domain, string servicePrefix)
        {
            return await LocateKrbServers (domain, servicePrefix);
        }

        internal record KrbSvcQuery {
            internal int defaultPort;
            internal IEnumerable<string> candidates;
            private static readonly KrbSvcQuery _Empty = new () { defaultPort = DefaultKerberosPort, candidates = Enumerable.Empty<string>() };
            internal static KrbSvcQuery Empty => _Empty;
        };

        

        public virtual async Task<IEnumerable<DnsRecord>> LocateKrbServers(string domain, string servicePrefix)
        {
            var q = KrbSvcQuery.Empty;

            if (Configuration.Realms.TryGetValue (domain, out var config))
            {
                q = servicePrefix.Substring (0, servicePrefix.IndexOf ('.')) switch
                {
                    "_kpasswd" => new () { candidates = config.KPasswdServer ?? config.AdminServer, defaultPort = DefaultKpasswdPort },
                    _ => q with { candidates = config.Kdc }
                };
            }

            var results = (await this.Query(domain, servicePrefix, q.candidates, q.defaultPort)).Where(r => r.Type == DnsRecordType.SRV);

            results = results.Where(s => !this.negativeCache.TryGetValue(s.Target, out DnsRecord record) || record.Expired);

            foreach (var result in results.Where(r => r.Expired).ToList())
            {
                this.negativeCache.TryRemove(result.Target, out _);
            }

            var weighted = results.GroupBy(r => r.Weight)
                                  .OrderBy(r => r.Key)
                                  .ThenByDescending(r => r.Sum(a => a.Canonical.Count()))
                                  .FirstOrDefault();

            if (weighted != null)
            {
                return weighted;
            }

            return Array.Empty<DnsRecord>();
        }

        public void NegativeCache(DnsRecord record)
        {
            if (record != null)
            {
                this.negativeCache[record.Target] = record;
            }
        }

        public void PinKdc(string realm, string kdc)
        {
            DomainCache.TryRemove(realm, out _);

            if (!this.pinnedKdcs.TryGetValue(realm, out HashSet<string> kdcs))
            {
                kdcs = new HashSet<string>();
                this.pinnedKdcs[realm] = kdcs;
            }

            kdcs.Add(kdc);
        }

        public void ClearPinnedKdc(string realm)
        {
            DomainCache.TryRemove(realm, out _);

            if (this.pinnedKdcs.TryGetValue(realm, out HashSet<string> kdcs))
            {
                kdcs.Clear();
            }
        }
/*        protected virtual async Task<IEnumerable<DnsRecord>> Query(string domain, string servicePrefix)
        {

            return await Query(domain, servicePrefix, kdc, DefaultKerberosPort);

        }*/
        protected virtual async Task<IEnumerable<DnsRecord>> Query(string domain, string servicePrefix, IEnumerable<string> serverCandidates, int defaultKerberosPort)
        {
            var records = new List<DnsRecord> ();

            if (this.pinnedKdcs.TryGetValue (domain, out HashSet<string> kdcs))
            {
                records.AddRange (kdcs.Select (k => ParseKdcEntryAsSrvRecord (k, domain, servicePrefix, defaultKerberosPort)).Where (k => k != null));
            }

            if (serverCandidates != null)
            {
                records.AddRange (serverCandidates.Select (k => ParseKdcEntryAsSrvRecord (k, domain, servicePrefix, defaultKerberosPort)).Where (k => k != null));
            }            

            if (this.Configuration.Defaults.DnsLookupKdc && servicePrefix.StartsWith("_kerberos") || servicePrefix.StartsWith ("_kpasswd")) // allow DNS queries for RF3244 protocol
            {
                try
                {
                    var dnsRecords = (await this.QueryDns (domain, servicePrefix));
                    foreach (var r in dnsRecords.Where (q => q.Port <= 0)) //fix bad SRV records with wrong port
                    {
                        r.Port = defaultKerberosPort;                        
                    }
                    records.AddRange(dnsRecords);
                    
                }
                catch (DnsNotSupportedException ex)
                {
                    this.logger.LogDebug(ex, "DNS isn't supported on this platform");
                }
            }

            return records;
        }

        private async Task<IEnumerable<DnsRecord>> QueryDns(string domain, string servicePrefix)
        {
            var lookup = Invariant($"{servicePrefix}.{domain}");
            var dnsResults = Enumerable.Empty<DnsRecord>();

            bool skipLookup = false;

            if (DomainServiceNegativeCache.TryGetValue(lookup, out DateTimeOffset expires))
            {
                if (DateTimeOffset.UtcNow > expires)
                {
                    DomainServiceNegativeCache.TryRemove(lookup, out _);
                }
                else
                {
                    skipLookup = true;
                }
            }

            if (!skipLookup)
            {
                this.logger.LogDebug("Querying DNS {Lookup}", lookup);

                dnsResults = await DnsQuery.QuerySrv(lookup);

                if (!dnsResults.Any())
                {
                    DomainServiceNegativeCache[lookup] = DateTimeOffset.UtcNow.AddMinutes(5);

                    this.logger.LogDebug("DNS failed {Lookup} so negative caching", lookup);
                }                
            }

            return dnsResults;
        }

        private static DnsRecord ParseKdcEntryAsSrvRecord(string kdc, string realm, string servicePrefix)
        {
            return ParseKdcEntryAsSrvRecord (kdc, realm, servicePrefix, DefaultKerberosPort);
        }
        private static DnsRecord ParseKdcEntryAsSrvRecord(string kdc, string realm, string servicePrefix, int defaultKerberosPort)
        {
            if (IsUri(kdc))
            {
                return new DnsRecord
                {
                    Target = kdc,
                    Type = DnsRecordType.SRV,
                    Name = realm
                };
            }

            var split = kdc.Split(':');

            var record = new DnsRecord
            {
                Target = split[0],
                Type = DnsRecordType.SRV,
                Name = $"{servicePrefix}.{realm}"
            };

            if (split.Length > 1)
            {
                record.Port = int.Parse(split[1], CultureInfo.InvariantCulture);
            }
            else
            {
                record.Port = defaultKerberosPort;
            }

            return record;
        }

        private static bool IsUri(string kdc)
        {
            return Uri.TryCreate(kdc, UriKind.Absolute, out Uri result) &&
                ("https".Equals(result.Scheme, StringComparison.OrdinalIgnoreCase) ||
                 "http".Equals(result.Scheme, StringComparison.OrdinalIgnoreCase));
        }

        private static async Task MonitorDnsCache()
        {
            // allows any callers to modify CacheCleanupInterval
            // without having to wait the full 5 minute default.

            await Task.Delay(TimeSpan.FromSeconds(5));

            // yes this is somewhat redundant

            while (!CacheCleanup.IsCompleted)
            {
                foreach (var entry in DomainCache.ToList())
                {
                    if (entry.Value.Expired)
                    {
                        DomainCache.TryRemove(entry.Key, out _);
                    }
                }

                foreach (var entry in DomainServiceNegativeCache.ToList())
                {
                    if (DateTimeOffset.UtcNow > entry.Value)
                    {
                        DomainServiceNegativeCache.TryRemove(entry.Key, out _);
                    }
                }

                await Task.Delay(CacheCleanupInterval);
            }
        }
    }
}
