using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public class ShihabNetworkAdapterChoice
    {
        public string AdapterId { get; set; }
        public string AdapterName { get; set; }
        public string Description { get; set; }
        public string IpAddressText { get; set; }
        public bool IsUp { get; set; }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(AdapterId))
            {
                return "Any / System Default";
            }
            return string.Format("{0} ({1}) [{2}]",
                AdapterName,
                string.IsNullOrEmpty(IpAddressText) ? "Disconnected" : IpAddressText,
                IsUp ? "UP" : "DOWN");
        }
    }

    public static class ShihabDnsAndNetworkManager
    {
        private sealed class DnsCacheRecord
        {
            public IPAddress Address;
            public DateTime ExpiresUtc;
        }

        private const int MaxDohCacheEntries = 512;
        private const int MaxDohResponseBytes = 512 * 1024;
        private static readonly TimeSpan DohRegexTimeout = TimeSpan.FromMilliseconds(250);
        private static readonly Regex DohIpv4DataRegex = new Regex(
            "\"data\"\\s*:\\s*\"([0-9]{1,3}(?:\\.[0-9]{1,3}){3})\"",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            DohRegexTimeout);

        private static readonly Dictionary<string, DnsCacheRecord> DohCache = new Dictionary<string, DnsCacheRecord>(StringComparer.OrdinalIgnoreCase);
        private static int _netwatchRegistered = 0;
        public static event Action OnNetworkRestored;

        public static void EnsureNetworkWatcherStarted()
        {
            if (Interlocked.CompareExchange(ref _netwatchRegistered, 1, 0) != 0) return;
            try
            {
                NetworkChange.NetworkAvailabilityChanged += delegate(object sender, NetworkAvailabilityEventArgs e)
                {
                    lock (DohCache) { DohCache.Clear(); }
                    if (e != null && e.IsAvailable && OnNetworkRestored != null)
                    {
                        OnNetworkRestored();
                    }
                };
                NetworkChange.NetworkAddressChanged += delegate(object sender, EventArgs e)
                {
                    lock (DohCache) { DohCache.Clear(); }
                    if (NetworkInterface.GetIsNetworkAvailable() && OnNetworkRestored != null)
                    {
                        OnNetworkRestored();
                    }
                };
            }
            catch
            {
            }
        }

        public static List<ShihabNetworkAdapterChoice> GetAvailableNetworkAdapters()
        {
            List<ShihabNetworkAdapterChoice> list = new List<ShihabNetworkAdapterChoice>();
            list.Add(new ShihabNetworkAdapterChoice
            {
                AdapterId = "",
                AdapterName = "Any / System Default",
                Description = "Route through default Windows routing table",
                IpAddressText = "",
                IsUp = true
            });
            try
            {
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < nics.Length; i++)
                {
                    NetworkInterface nic = nics[i];
                    if (nic == null || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    string ipStr = "";
                    try
                    {
                        IPInterfaceProperties props = nic.GetIPProperties();
                        for (int u = 0; u < props.UnicastAddresses.Count; u++)
                        {
                            IPAddress addr = props.UnicastAddresses[u].Address;
                            if (addr != null && addr.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr))
                            {
                                ipStr = addr.ToString();
                                break;
                            }
                        }
                    }
                    catch
                    {
                    }
                    list.Add(new ShihabNetworkAdapterChoice
                    {
                        AdapterId = nic.Name,
                        AdapterName = nic.Name,
                        Description = nic.Description,
                        IpAddressText = ipStr,
                        IsUp = nic.OperationalStatus == OperationalStatus.Up && !string.IsNullOrEmpty(ipStr)
                    });
                }
            }
            catch
            {
            }
            return list;
        }

        public static IPAddress GetBoundAdapterLocalIp(string adapterNameOrId)
        {
            if (string.IsNullOrWhiteSpace(adapterNameOrId)) return null;
            MehediAppConfiguration tmp = new MehediAppConfiguration();
            tmp.ShihabBoundNetworkAdapter = adapterNameOrId.Trim();
            IPAddress ip;
            string reason;
            if (TryGetBoundAdapterLocalIp(tmp, out ip, out reason))
            {
                return ip;
            }
            return null;
        }

        public static IPAddress ResolveHostIp(string host, MehediAppConfiguration cfg)
        {
            string cc;
            return ResolveHostIpWithDohAndGeo(host, cfg, out cc);
        }

        public static bool TryGetBoundAdapterLocalIp(MehediAppConfiguration cfg, out IPAddress boundIp, out string failReason)
        {
            boundIp = null;
            failReason = "";
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.ShihabBoundNetworkAdapter))
            {
                return true;
            }
            string wanted = cfg.ShihabBoundNetworkAdapter.Trim();
            if (string.Equals(wanted, "Any", StringComparison.OrdinalIgnoreCase) ||
                wanted.StartsWith("Any ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            try
            {
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < nics.Length; i++)
                {
                    NetworkInterface nic = nics[i];
                    if (nic == null) continue;
                    if (string.Equals(nic.Name, wanted, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(nic.Id, wanted, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(nic.Description, wanted, StringComparison.OrdinalIgnoreCase))
                    {
                        if (nic.OperationalStatus != OperationalStatus.Up)
                        {
                            failReason = string.Format("VPN/Adapter Kill-Switch triggered: Bound network adapter '{0}' is currently DOWN.", nic.Name);
                            return false;
                        }
                        IPInterfaceProperties props = nic.GetIPProperties();
                        for (int u = 0; u < props.UnicastAddresses.Count; u++)
                        {
                            IPAddress addr = props.UnicastAddresses[u].Address;
                            if (addr != null && addr.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr))
                            {
                                boundIp = addr;
                                return true;
                            }
                        }
                        failReason = string.Format("VPN/Adapter Kill-Switch triggered: Bound network adapter '{0}' has no active IPv4 address.", nic.Name);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                failReason = "Adapter lookup failed: " + ex.Message;
                return false;
            }
            failReason = string.Format("VPN/Adapter Kill-Switch triggered: Bound network adapter '{0}' was not found.", wanted);
            return false;
        }

        public static IPAddress ResolveHostIpWithDohAndGeo(
            string host,
            MehediAppConfiguration cfg,
            out string countryCode)
        {
            countryCode = "UN";
            if (string.IsNullOrWhiteSpace(host)) return null;

            IPAddress parsed;
            if (IPAddress.TryParse(host.Trim('[', ']'), out parsed))
            {
                countryCode = ShihabGeoIpLookup.LookupCountryCodeForIp(parsed);
                return parsed;
            }

            string cleanHost = host.Trim().ToLowerInvariant();
            if (cleanHost == "localhost" || cleanHost.EndsWith(".local", StringComparison.OrdinalIgnoreCase) || cleanHost.IndexOf('.') < 0)
            {
                countryCode = "LO";
                return IPAddress.Loopback;
            }

            string cacheKey = cleanHost + "|" + (cfg != null ? (cfg.ShihabDnsMode + ":" + cfg.ShihabDnsProvider + ":" + cfg.ShihabPlainDnsServer) : "sys");
            DateTime nowUtc = DateTime.UtcNow;
            lock (DohCache)
            {
                DnsCacheRecord rec;
                if (DohCache.TryGetValue(cacheKey, out rec) && rec != null && rec.ExpiresUtc > nowUtc)
                {
                    countryCode = ShihabGeoIpLookup.LookupCountryCodeForIp(rec.Address);
                    return rec.Address;
                }
            }

            IPAddress resolvedIp = null;
            string dnsMode = cfg != null ? (cfg.ShihabDnsMode ?? "System").Trim() : "System";
            if (dnsMode.StartsWith("Encrypted", StringComparison.OrdinalIgnoreCase))
            {
                resolvedIp = QueryDnsOverHttpsJson(cleanHost, cfg);
                if (resolvedIp == null && cfg != null && !cfg.ShihabDnsFallbackToSystem)
                {
                    return null;
                }
            }
            else if (string.Equals(dnsMode, "Plain", StringComparison.OrdinalIgnoreCase) &&
                     cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabPlainDnsServer))
            {
                resolvedIp = ShihabLocalNetworkRelay.QueryPlainUdpDnsA(cleanHost, cfg.ShihabPlainDnsServer.Trim());
                if (resolvedIp == null && !cfg.ShihabDnsFallbackToSystem)
                {
                    return null;
                }
            }

            if (resolvedIp == null)
            {
                try
                {
                    IPAddress[] addrs = Dns.GetHostAddresses(cleanHost);
                    if (addrs != null && addrs.Length > 0)
                    {
                        for (int i = 0; i < addrs.Length; i++)
                        {
                            if (addrs[i].AddressFamily == AddressFamily.InterNetwork)
                            {
                                resolvedIp = addrs[i];
                                break;
                            }
                        }
                        if (resolvedIp == null) resolvedIp = addrs[0];
                    }
                }
                catch
                {
                }
            }

            if (resolvedIp != null)
            {
                lock (DohCache)
                {
                    if (DohCache.Count >= MaxDohCacheEntries)
                    {
                        List<string> expired = new List<string>();
                        foreach (KeyValuePair<string, DnsCacheRecord> kv in DohCache)
                        {
                            if (kv.Value == null || kv.Value.ExpiresUtc <= nowUtc)
                            {
                                expired.Add(kv.Key);
                            }
                        }
                        for (int i = 0; i < expired.Count; i++) DohCache.Remove(expired[i]);
                        if (DohCache.Count >= MaxDohCacheEntries)
                        {
                            DohCache.Clear();
                        }
                    }

                    DohCache[cacheKey] = new DnsCacheRecord
                    {
                        Address = resolvedIp,
                        ExpiresUtc = DateTime.UtcNow.AddMinutes(5)
                    };
                }
                countryCode = ShihabGeoIpLookup.LookupCountryCodeForIp(resolvedIp);
            }
            return resolvedIp;
        }

        private static IPAddress QueryDnsOverHttpsJson(string host, MehediAppConfiguration cfg)
        {
            try
            {
                ShihabAutoUpdater.EnforceModernTls();
                string provider = (cfg != null && !string.IsNullOrEmpty(cfg.ShihabDnsProvider)) ? cfg.ShihabDnsProvider : "Cloudflare";
                string endpoint = "https://1.1.1.1/dns-query";
                if (string.Equals(provider, "Google", StringComparison.OrdinalIgnoreCase))
                    endpoint = "https://8.8.8.8/resolve";
                else if (string.Equals(provider, "Quad9", StringComparison.OrdinalIgnoreCase))
                    endpoint = "https://9.9.9.9:5053/dns-query";
                else if (string.Equals(provider, "AdGuard", StringComparison.OrdinalIgnoreCase))
                    endpoint = "https://94.140.14.14/resolve";
                else if (string.Equals(provider, "NextDNS", StringComparison.OrdinalIgnoreCase))
                {
                    string nextId = cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabNextDnsConfigId) ? cfg.ShihabNextDnsConfigId.Trim() : "";
                    endpoint = string.IsNullOrEmpty(nextId) ? "https://dns.nextdns.io/dns-query" : ("https://dns.nextdns.io/" + Uri.EscapeDataString(nextId));
                }
                else if (string.Equals(provider, "Custom", StringComparison.OrdinalIgnoreCase) && cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabCustomDohUrl))
                {
                    Uri validatedDoh;
                    if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(cfg.ShihabCustomDohUrl.Trim(), true, out validatedDoh))
                    {
                        return null;
                    }
                    endpoint = validatedDoh.AbsoluteUri;
                }

                string url = endpoint + (endpoint.IndexOf('?') >= 0 ? "&" : "?") + "name=" + Uri.EscapeDataString(host) + "&type=A";
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Accept = "application/dns-json";
                req.UserAgent = "MHS-DoH-Resolver/1.1";
                req.AllowAutoRedirect = false;
                req.Proxy = null;
                req.Timeout = 4500;
                req.ReadWriteTimeout = 4500;

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (Stream s = resp.GetResponseStream())
                {
                    string json = ReadBoundedUtf8(s, MaxDohResponseBytes);
                    if (string.IsNullOrEmpty(json)) return null;
                    MatchCollection matches = DohIpv4DataRegex.Matches(json);
                    for (int i = 0; i < matches.Count; i++)
                    {
                        IPAddress ip;
                        if (IPAddress.TryParse(matches[i].Groups[1].Value, out ip))
                        {
                            return ip;
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private static string ReadBoundedUtf8(Stream s, int maxBytes)
        {
            if (s == null) return "";
            byte[] buf = new byte[8192];
            using (MemoryStream ms = new MemoryStream())
            {
                int total = 0;
                int read;
                while (total < maxBytes && (read = s.Read(buf, 0, Math.Min(buf.Length, maxBytes - total))) > 0)
                {
                    ms.Write(buf, 0, read);
                    total += read;
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private static bool IsLoopbackTargetUrl(string targetUrl)
        {
            Uri u;
            if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out u)) return false;
            if (u.IsLoopback) return true;
            string h = (u.Host ?? "").Trim('[', ']').ToLowerInvariant();
            return h == "localhost" || h == "127.0.0.1" || h == "::1";
        }

        public static void ApplyNetworkBindingAndProxy(HttpWebRequest req, MehediAppConfiguration cfg, string targetUrl)
        {
            ApplyAdapterAndProxyPolicy(req, targetUrl, cfg);
        }

        public static void ApplyAdapterAndProxyPolicy(HttpWebRequest req, string targetUrl, MehediAppConfiguration cfg)
        {
            if (req == null) return;
            ShihabAutoUpdater.EnforceModernTls();
            if (cfg == null) return;

            if (!string.IsNullOrWhiteSpace(cfg.ShihabBoundNetworkAdapter) &&
                !cfg.ShihabBoundNetworkAdapter.StartsWith("Any", StringComparison.OrdinalIgnoreCase))
            {
                IPAddress boundLocalIp;
                string failReason;
                if (!TryGetBoundAdapterLocalIp(cfg, out boundLocalIp, out failReason) || boundLocalIp == null)
                {
                    throw new WebException(string.IsNullOrEmpty(failReason)
                        ? "Bound network adapter is offline (VPN Kill-Switch)."
                        : failReason);
                }
                if (!IsLoopbackTargetUrl(targetUrl))
                {
                    req.ServicePoint.BindIPEndPointDelegate = delegate(ServicePoint sp, IPEndPoint remoteEndPoint, int retryCount)
                    {
                        return new IPEndPoint(boundLocalIp, 0);
                    };
                }
            }

            if (IsLoopbackTargetUrl(targetUrl))
            {
                req.Proxy = null;
                return;
            }

            if (ShihabLocalNetworkRelay.NeedsLocalRelay(cfg))
            {
                IWebProxy relayProxy = ShihabLocalNetworkRelay.GetOrStartConfiguredWebProxy(cfg);
                if (relayProxy != null)
                {
                    req.Proxy = relayProxy;
                    return;
                }
            }

            string pMode = (cfg.ShihabProxyMode ?? "").Trim();
            if (string.Equals(pMode, "None", StringComparison.OrdinalIgnoreCase) && !cfg.MehediUseHttpProxy)
            {
                req.Proxy = null;
                return;
            }

            if ((string.Equals(pMode, "Manual", StringComparison.OrdinalIgnoreCase) || cfg.MehediUseHttpProxy) &&
                !string.IsNullOrWhiteSpace(cfg.ShihabProxyAddress))
            {
                WebProxy wp = new WebProxy(cfg.ShihabProxyAddress.Trim(), cfg.MehediProxyPort > 0 ? cfg.MehediProxyPort : 8080);
                if (!string.IsNullOrWhiteSpace(cfg.ShihabProxyUser))
                {
                    wp.Credentials = new NetworkCredential(
                        cfg.ShihabProxyUser.Trim(),
                        ShihabSecretsVault.UnprotectSecret(cfg.MehediProxyPassword ?? ""));
                }
                req.Proxy = wp;
            }
        }
    }
}

