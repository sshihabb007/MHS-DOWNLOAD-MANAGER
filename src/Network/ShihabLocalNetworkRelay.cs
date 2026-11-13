using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    // Loopback HTTP + CONNECT Relay for SOCKS5, PAC, Custom DNS/DoH & VPN Binding
    public static class ShihabLocalNetworkRelay
    {
        private const int MaxPacBytes = 512 * 1024;
        private static readonly Regex PacRuleRx = new Regex(
            @"if\s*\(([^\{;]+)\)\s*\{?\s*return\s+[""']([^""']+)[""']",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(250));
        private static readonly Regex PacAllRetRx = new Regex(
            @"return\s+[""']([^""']+)[""']",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(250));
        private static readonly Regex PacDomainIsRx = new Regex(
            @"dnsDomainIs\s*\(\s*host\s*,\s*[""']([^""']+)[""']\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(250));
        private static readonly Regex PacShExpMatchRx = new Regex(
            @"shExpMatch\s*\(\s*(host|url)\s*,\s*[""']([^""']+)[""']\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(250));

        private static readonly object _relayLock = new object();
        private static TcpListener _listener;
        private static int _boundPort;
        private static readonly string _proxyUser = "mhsrelay";
        private static readonly string _proxyPass = GenerateRelayPassword();
        private static MehediAppConfiguration _activeConfig;
        private static string _cachedPacUrl = "";
        private static string _cachedPacScript = "";
        private static DateTime _cachedPacAtUtc = DateTime.MinValue;

        public static event Action<string> OnVpnKillSwitchTripped;

        static ShihabLocalNetworkRelay()
        {
            try
            {
                NetworkChange.NetworkAddressChanged += HandleNetworkStateChanged;
                NetworkChange.NetworkAvailabilityChanged += delegate { HandleNetworkStateChanged(null, EventArgs.Empty); };
            }
            catch { }
        }

        private static string GenerateRelayPassword()
        {
            byte[] buf = new byte[24];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(buf);
            }
            return Convert.ToBase64String(buf).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private static void HandleNetworkStateChanged(object sender, EventArgs e)
        {
            MehediAppConfiguration cfg = _activeConfig;
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.ShihabBoundNetworkAdapter)) return;
            IPAddress ip = ShihabDnsAndNetworkManager.GetBoundAdapterLocalIp(cfg.ShihabBoundNetworkAdapter);
            if (ip == null)
            {
                Action<string> handler = OnVpnKillSwitchTripped;
                if (handler != null) handler(cfg.ShihabBoundNetworkAdapter);
            }
        }

        public static bool NeedsLocalRelay(MehediAppConfiguration cfg)
        {
            if (cfg == null) return false;
            string pMode = (cfg.ShihabProxyMode ?? "").Trim();
            if (string.Equals(pMode, "SOCKS5", StringComparison.OrdinalIgnoreCase) || cfg.ShihabUseSocksProxy)
            {
                if (!string.IsNullOrWhiteSpace(cfg.MehediSocksHost) || !string.IsNullOrWhiteSpace(cfg.ShihabProxyAddress))
                    return true;
            }
            if (string.Equals(pMode, "PAC", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cfg.ShihabPacScriptUrl))
            {
                return true;
            }
            if (!string.IsNullOrWhiteSpace(cfg.ShihabDnsMode) &&
                !string.Equals(cfg.ShihabDnsMode, "System", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (!string.IsNullOrWhiteSpace(cfg.ShihabBoundNetworkAdapter))
            {
                return true;
            }
            return false;
        }

        public static IWebProxy GetOrStartConfiguredWebProxy(MehediAppConfiguration cfg)
        {
            _activeConfig = cfg;
            int port = EnsureStarted(cfg);
            if (port <= 0) return null;
            WebProxy wp = new WebProxy("http://127.0.0.1:" + port);
            wp.Credentials = new NetworkCredential(_proxyUser, _proxyPass);
            return wp;
        }

        public static string GetSubprocessProxyUrl(MehediAppConfiguration cfg)
        {
            if (!NeedsLocalRelay(cfg))
            {
                if (cfg != null && (string.Equals(cfg.ShihabProxyMode, "Manual", StringComparison.OrdinalIgnoreCase) || cfg.MehediUseHttpProxy) &&
                    !string.IsNullOrWhiteSpace(cfg.ShihabProxyAddress))
                {
                    string cleanAddr = SanitizeHostToken(cfg.ShihabProxyAddress);
                    if (!string.IsNullOrEmpty(cleanAddr))
                    {
                        return string.Format(CultureInfo.InvariantCulture, "http://{0}:{1}", cleanAddr, cfg.MehediProxyPort > 0 ? cfg.MehediProxyPort : 8080);
                    }
                }
                return "";
            }
            int port = EnsureStarted(cfg);
            if (port <= 0) return "";
            return string.Format(CultureInfo.InvariantCulture, "http://{0}:{1}@127.0.0.1:{2}", _proxyUser, _proxyPass, port);
        }

        public static void Stop()
        {
            lock (_relayLock)
            {
                if (_listener != null)
                {
                    try { _listener.Stop(); } catch { }
                    _listener = null;
                }
                _boundPort = 0;
            }
        }

        public static int EnsureStarted(MehediAppConfiguration cfg)
        {
            _activeConfig = cfg;
            lock (_relayLock)
            {
                if (_listener != null && _boundPort > 0) return _boundPort;
                try
                {
                    _listener = new TcpListener(IPAddress.Loopback, 0);
                    _listener.Start();
                    _boundPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
                    TcpListener activeListener = _listener;
                    Thread acceptThread = new Thread(delegate() { AcceptLoop(activeListener); });
                    acceptThread.IsBackground = true;
                    acceptThread.Name = "MHS-LocalNetworkRelay";
                    acceptThread.Start();
                    return _boundPort;
                }
                catch
                {
                    _listener = null;
                    _boundPort = 0;
                    return 0;
                }
            }
        }

        private static void AcceptLoop(TcpListener expectedListener)
        {
            while (true)
            {
                TcpClient client = null;
                try
                {
                    if (_listener != expectedListener || expectedListener == null) break;
                    client = expectedListener.AcceptTcpClient();
                    TcpClient captured = client;
                    ThreadPool.QueueUserWorkItem(delegate { HandleClient(captured); });
                }
                catch (ObjectDisposedException)
                {
                    if (client != null) { try { client.Close(); } catch { } }
                    break;
                }
                catch (SocketException)
                {
                    if (client != null) { try { client.Close(); } catch { } }
                    if (_listener != expectedListener) break;
                    Thread.Sleep(50);
                }
                catch
                {
                    if (client != null) { try { client.Close(); } catch { } }
                    if (_listener != expectedListener) break;
                    Thread.Sleep(50);
                }
            }
        }

        private static string SanitizeHostToken(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string s = raw.Trim().Trim('[', ']');
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch <= 0x20 || ch == 0x7F || ch == '/' || ch == '\\' || ch == '@' || ch == ';')
                {
                    return "";
                }
            }
            return s;
        }

        private static void HandleClient(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 30000;
                    client.SendTimeout = 30000;
                    using (NetworkStream rawNet = client.GetStream())
                    using (BufferedStream clientStream = new BufferedStream(rawNet, 8192))
                    {
                        List<string> headerLines = new List<string>();
                        string firstLine = ReadAsciiLine(clientStream);
                        if (string.IsNullOrEmpty(firstLine)) return;
                        while (true)
                        {
                            string h = ReadAsciiLine(clientStream);
                            if (string.IsNullOrEmpty(h)) break;
                            headerLines.Add(h);
                            if (headerLines.Count > 120) return;
                        }

                        if (!ValidateRelayAuth(headerLines))
                        {
                            byte[] unauth = Encoding.ASCII.GetBytes("HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"MHS\"\r\nConnection: close\r\n\r\n");
                            clientStream.Write(unauth, 0, unauth.Length);
                            clientStream.Flush();
                            return;
                        }

                        string[] parts = firstLine.Split(' ');
                        if (parts.Length < 2) return;
                        string method = parts[0].ToUpperInvariant();
                        string rawTarget = parts[1];

                        string targetHost = "";
                        int targetPort = 0;
                        string fullUrl = rawTarget;

                        if (method == "CONNECT")
                        {
                            int colon = rawTarget.LastIndexOf(':');
                            if (colon > 0 && (rawTarget.IndexOf(']') < 0 || colon > rawTarget.IndexOf(']')))
                            {
                                targetHost = SanitizeHostToken(rawTarget.Substring(0, colon));
                                if (!int.TryParse(rawTarget.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out targetPort))
                                {
                                    targetPort = 0;
                                }
                            }
                            else
                            {
                                targetHost = SanitizeHostToken(rawTarget);
                                targetPort = 443;
                            }
                            if (string.IsNullOrEmpty(targetHost) || targetPort <= 0 || targetPort > 65535)
                            {
                                byte[] badReq = Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\nConnection: close\r\n\r\n");
                                clientStream.Write(badReq, 0, badReq.Length);
                                clientStream.Flush();
                                return;
                            }
                            fullUrl = "https://" + targetHost + ":" + targetPort.ToString(CultureInfo.InvariantCulture) + "/";
                        }
                        else
                        {
                            Uri parsed;
                            if (!Uri.TryCreate(rawTarget, UriKind.Absolute, out parsed)) return;
                            targetHost = SanitizeHostToken(parsed.Host);
                            targetPort = parsed.Port > 0 ? parsed.Port : (string.Equals(parsed.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80);
                            if (string.IsNullOrEmpty(targetHost) || targetPort <= 0 || targetPort > 65535) return;
                        }

                        MehediAppConfiguration cfgSnapshot = _activeConfig;
                        using (Socket upstream = ConnectUpstreamSocket(targetHost, targetPort, fullUrl, cfgSnapshot))
                        using (NetworkStream upStream = new NetworkStream(upstream, false))
                        {
                            if (method == "CONNECT")
                            {
                                byte[] okBytes = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                                clientStream.Write(okBytes, 0, okBytes.Length);
                                clientStream.Flush();
                                PumpBidirectional(clientStream, upStream);
                            }
                            else
                            {
                                Uri u = new Uri(rawTarget);
                                StringBuilder reqSb = new StringBuilder();
                                reqSb.Append(method).Append(' ').Append(u.PathAndQuery).Append(" HTTP/1.1\r\n");
                                for (int i = 0; i < headerLines.Count; i++)
                                {
                                    string hl = headerLines[i];
                                    if (hl.StartsWith("Proxy-Authorization:", StringComparison.OrdinalIgnoreCase) ||
                                        hl.StartsWith("Proxy-Connection:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        continue;
                                    }
                                    reqSb.Append(hl).Append("\r\n");
                                }
                                reqSb.Append("Connection: close\r\n\r\n");
                                byte[] outHdr = Encoding.ASCII.GetBytes(reqSb.ToString());
                                upStream.Write(outHdr, 0, outHdr.Length);

                                long contentLen = 0;
                                for (int i = 0; i < headerLines.Count; i++)
                                {
                                    if (headerLines[i].StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        long.TryParse(headerLines[i].Substring(15).Trim(), out contentLen);
                                    }
                                }
                                if (contentLen > 0)
                                {
                                    byte[] bBuf = new byte[16384];
                                    long rem = contentLen;
                                    while (rem > 0)
                                    {
                                        int r = clientStream.Read(bBuf, 0, (int)Math.Min(bBuf.Length, rem));
                                        if (r <= 0) break;
                                        upStream.Write(bBuf, 0, r);
                                        rem -= r;
                                    }
                                }
                                upStream.Flush();
                                upStream.CopyTo(clientStream);
                                clientStream.Flush();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        byte[] err = Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\nContent-Type: text/plain\r\nConnection: close\r\n\r\nMHS Relay Error: " + ex.Message);
                        client.GetStream().Write(err, 0, err.Length);
                    }
                    catch { }
                }
            }
        }

        private static bool ValidateRelayAuth(List<string> headers)
        {
            string expected = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(_proxyUser + ":" + _proxyPass));
            for (int i = 0; i < headers.Count; i++)
            {
                if (headers[i].StartsWith("Proxy-Authorization:", StringComparison.OrdinalIgnoreCase))
                {
                    string v = headers[i].Substring(20).Trim();
                    if (ShihabProcessAndPathSafety.FixedTimeEqualsAscii(v, expected)) return true;
                }
            }
            return false;
        }

        private static string ReadAsciiLine(Stream s)
        {
            using (MemoryStream ms = new MemoryStream(256))
            {
                while (ms.Length < 8192)
                {
                    int b = s.ReadByte();
                    if (b < 0) break;
                    if (b == '\n') break;
                    if (b != '\r') ms.WriteByte((byte)b);
                }
                if (ms.Length == 0) return "";
                return Encoding.ASCII.GetString(ms.ToArray());
            }
        }

        private static void PumpBidirectional(Stream a, Stream b)
        {
            using (ManualResetEvent done = new ManualResetEvent(false))
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try { a.CopyTo(b); } catch { }
                    try { b.Close(); } catch { }
                    try { done.Set(); } catch { }
                });
                try { b.CopyTo(a); } catch { }
                try { a.Close(); } catch { }
                done.WaitOne(5000);
            }
        }

        private static Socket ConnectUpstreamSocket(string targetHost, int targetPort, string fullUrl, MehediAppConfiguration cfg)
        {
            IPAddress boundLocalIp = null;
            if (cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabBoundNetworkAdapter))
            {
                boundLocalIp = ShihabDnsAndNetworkManager.GetBoundAdapterLocalIp(cfg.ShihabBoundNetworkAdapter);
                if (boundLocalIp == null)
                {
                    throw new WebException("VPN / Network Adapter Kill-Switch blocked connection: '" + cfg.ShihabBoundNetworkAdapter + "' is disconnected.");
                }
            }

            string routeSpec = ResolveEffectiveUpstreamProxySpec(fullUrl, targetHost, cfg);
            if (routeSpec.StartsWith("SOCKS", StringComparison.OrdinalIgnoreCase))
            {
                string[] sp = routeSpec.Split(' ');
                string hostPort = sp.Length > 1 ? sp[1] : "";
                string sHost = cfg != null && !string.IsNullOrWhiteSpace(cfg.MehediSocksHost) ? cfg.MehediSocksHost : (cfg != null ? cfg.ShihabProxyAddress : "");
                int sPort = cfg != null && cfg.ShihabSocksPort > 0 ? cfg.ShihabSocksPort : 1080;
                if (!string.IsNullOrEmpty(hostPort) && hostPort.IndexOf(':') > 0)
                {
                    int cIdx = hostPort.LastIndexOf(':');
                    sHost = hostPort.Substring(0, cIdx);
                    int.TryParse(hostPort.Substring(cIdx + 1), out sPort);
                }
                return ConnectViaSocks5(
                    SanitizeHostToken(sHost),
                    sPort,
                    cfg != null ? cfg.ShihabProxyUser : "",
                    cfg != null ? ShihabSecretsVault.UnprotectSecret(cfg.MehediProxyPassword) : "",
                    targetHost,
                    targetPort,
                    boundLocalIp);
            }

            if (routeSpec.StartsWith("PROXY ", StringComparison.OrdinalIgnoreCase))
            {
                string hp = routeSpec.Substring(6).Trim();
                int cIdx = hp.LastIndexOf(':');
                if (cIdx > 0)
                {
                    string pHost = SanitizeHostToken(hp.Substring(0, cIdx));
                    int pPort = 8080;
                    int.TryParse(hp.Substring(cIdx + 1), out pPort);
                    if (!string.IsNullOrEmpty(pHost) && pPort > 0 && pPort <= 65535)
                    {
                        Socket proxySock = DialDirectHappyEyeballs(pHost, pPort, cfg, boundLocalIp);
                        try
                        {
                            using (NetworkStream ns = new NetworkStream(proxySock, false))
                            {
                                string connectCmd = string.Format(CultureInfo.InvariantCulture, "CONNECT {0}:{1} HTTP/1.1\r\nHost: {0}:{1}\r\n\r\n", targetHost, targetPort);
                                byte[] cmdBytes = Encoding.ASCII.GetBytes(connectCmd);
                                ns.Write(cmdBytes, 0, cmdBytes.Length);
                                string statusLine = ReadAsciiLine(ns);
                                while (!string.IsNullOrEmpty(ReadAsciiLine(ns))) { }
                                if (statusLine.IndexOf(" 200 ", StringComparison.Ordinal) < 0)
                                {
                                    throw new IOException("Upstream HTTP proxy refused CONNECT: " + statusLine);
                                }
                            }
                            return proxySock;
                        }
                        catch
                        {
                            try { proxySock.Close(); } catch { }
                            throw;
                        }
                    }
                }
            }

            return DialDirectHappyEyeballs(targetHost, targetPort, cfg, boundLocalIp);
        }

        private static string ResolveEffectiveUpstreamProxySpec(string fullUrl, string host, MehediAppConfiguration cfg)
        {
            if (cfg == null) return "DIRECT";
            string mode = (cfg.ShihabProxyMode ?? "").Trim();
            if (string.Equals(mode, "SOCKS5", StringComparison.OrdinalIgnoreCase) || cfg.ShihabUseSocksProxy)
            {
                string sHost = !string.IsNullOrWhiteSpace(cfg.MehediSocksHost) ? cfg.MehediSocksHost.Trim() : (cfg.ShihabProxyAddress ?? "").Trim();
                int sPort = cfg.ShihabSocksPort > 0 ? cfg.ShihabSocksPort : 1080;
                if (!string.IsNullOrEmpty(sHost)) return "SOCKS5 " + sHost + ":" + sPort;
            }
            if (string.Equals(mode, "PAC", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cfg.ShihabPacScriptUrl))
            {
                string script = FetchPacScript(cfg.ShihabPacScriptUrl.Trim());
                if (!string.IsNullOrEmpty(script))
                {
                    return EvaluatePacFindProxyForUrl(script, fullUrl, host);
                }
            }
            return "DIRECT";
        }

        private static string FetchPacScript(string pacUrl)
        {
            lock (_relayLock)
            {
                if (string.Equals(_cachedPacUrl, pacUrl, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(_cachedPacScript) &&
                    (DateTime.UtcNow - _cachedPacAtUtc).TotalMinutes < 10.0)
                {
                    return _cachedPacScript;
                }
            }
            try
            {
                Uri parsedPac;
                if (ShihabProcessAndPathSafety.TryValidateHttpUrl(pacUrl, false, out parsedPac))
                {
                    ShihabAutoUpdater.EnforceModernTls();
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(parsedPac);
                    req.Proxy = null;
                    req.Timeout = 6000;
                    req.ReadWriteTimeout = 6000;
                    req.AllowAutoRedirect = false;
                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    using (Stream st = resp.GetResponseStream())
                    using (MemoryStream ms = new MemoryStream())
                    {
                        if (st == null) return "";
                        byte[] buf = new byte[8192];
                        int r;
                        while ((r = st.Read(buf, 0, buf.Length)) > 0)
                        {
                            if (ms.Length + r > MaxPacBytes) return "";
                            ms.Write(buf, 0, r);
                        }
                        string body = Encoding.UTF8.GetString(ms.ToArray());
                        lock (_relayLock) { _cachedPacUrl = pacUrl; _cachedPacScript = body; _cachedPacAtUtc = DateTime.UtcNow; }
                        return body;
                    }
                }
                if (File.Exists(pacUrl))
                {
                    FileInfo fi = new FileInfo(pacUrl);
                    string ext = Path.GetExtension(pacUrl) ?? "";
                    if (fi.Length <= MaxPacBytes &&
                        (string.Equals(ext, ".pac", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(ext, ".dat", StringComparison.OrdinalIgnoreCase)))
                    {
                        string disk = File.ReadAllText(pacUrl, Encoding.UTF8);
                        lock (_relayLock) { _cachedPacUrl = pacUrl; _cachedPacScript = disk; _cachedPacAtUtc = DateTime.UtcNow; }
                        return disk;
                    }
                }
                return "";
            }
            catch { return ""; }
        }

        public static string EvaluatePacFindProxyForUrl(string pacScript, string url, string host)
        {
            if (string.IsNullOrWhiteSpace(pacScript)) return "DIRECT";
            string lowerHost = (host ?? "").ToLowerInvariant();
            string lowerUrl = (url ?? "").ToLowerInvariant();

            try
            {
                MatchCollection ruleMatches = PacRuleRx.Matches(pacScript);
                for (int i = 0; i < ruleMatches.Count; i++)
                {
                    string cond = ruleMatches[i].Groups[1].Value;
                    string retSpec = SelectPrimaryPacToken(ruleMatches[i].Groups[2].Value);
                    if (EvaluatePacCondition(cond, lowerUrl, lowerHost))
                    {
                        return retSpec;
                    }
                }

                MatchCollection allRets = PacAllRetRx.Matches(pacScript);
                if (allRets.Count > 0)
                {
                    return SelectPrimaryPacToken(allRets[allRets.Count - 1].Groups[1].Value);
                }
            }
            catch { }
            return "DIRECT";
        }

        private static string SelectPrimaryPacToken(string pacReturnVal)
        {
            if (string.IsNullOrWhiteSpace(pacReturnVal)) return "DIRECT";
            string[] parts = pacReturnVal.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (!string.IsNullOrEmpty(p)) return p;
            }
            return "DIRECT";
        }

        private static bool EvaluatePacCondition(string cond, string url, string host)
        {
            if (cond.IndexOf("isPlainHostName", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                bool plain = host.IndexOf('.') < 0;
                return cond.TrimStart().StartsWith("!", StringComparison.Ordinal) ? !plain : plain;
            }
            Match domM = PacDomainIsRx.Match(cond);
            if (domM.Success)
            {
                string dom = domM.Groups[1].Value.ToLowerInvariant();
                return host == dom.TrimStart('.') || host.EndsWith(dom.StartsWith(".", StringComparison.Ordinal) ? dom : ("." + dom), StringComparison.Ordinal);
            }
            Match expM = PacShExpMatchRx.Match(cond);
            if (expM.Success)
            {
                string subject = string.Equals(expM.Groups[1].Value, "url", StringComparison.OrdinalIgnoreCase) ? url : host;
                return ShihabFormatHelpers.WildcardMatchesMehediPattern(subject, expM.Groups[2].Value.ToLowerInvariant());
            }
            return false;
        }

        private static Socket ConnectViaSocks5(
            string socksHost,
            int socksPort,
            string username,
            string password,
            string targetHost,
            int targetPort,
            IPAddress boundLocalIp)
        {
            if (string.IsNullOrEmpty(socksHost) || socksPort <= 0 || socksPort > 65535)
            {
                throw new ArgumentException("Invalid SOCKS5 proxy host or port.");
            }
            byte[] hostBytes = Encoding.ASCII.GetBytes(targetHost ?? "");
            if (hostBytes.Length == 0 || hostBytes.Length > 255)
            {
                throw new ArgumentException("Target host exceeds SOCKS5 domain length bounds.");
            }

            Socket sock = DialDirectHappyEyeballs(socksHost, socksPort, null, boundLocalIp);
            try
            {
                using (NetworkStream ns = new NetworkStream(sock, false))
                {
                    bool hasAuth = !string.IsNullOrEmpty(username);
                    byte[] greeting = hasAuth ? new byte[] { 0x05, 0x02, 0x00, 0x02 } : new byte[] { 0x05, 0x01, 0x00 };
                    ns.Write(greeting, 0, greeting.Length);

                    byte[] choice = ReadExactBytes(ns, 2);
                    if (choice[0] != 0x05 || choice[1] == 0xFF)
                    {
                        throw new IOException("SOCKS5 proxy rejected authentication methods.");
                    }
                    if (choice[1] == 0x02)
                    {
                        byte[] uBytes = Encoding.UTF8.GetBytes(username ?? "");
                        byte[] pBytes = Encoding.UTF8.GetBytes(password ?? "");
                        if (uBytes.Length > 255 || pBytes.Length > 255)
                        {
                            throw new InvalidOperationException("SOCKS5 credentials exceed 255 byte RFC 1929 limit.");
                        }
                        byte[] authPkt = new byte[3 + uBytes.Length + pBytes.Length];
                        authPkt[0] = 0x01;
                        authPkt[1] = (byte)uBytes.Length;
                        Buffer.BlockCopy(uBytes, 0, authPkt, 2, uBytes.Length);
                        authPkt[2 + uBytes.Length] = (byte)pBytes.Length;
                        Buffer.BlockCopy(pBytes, 0, authPkt, 3 + uBytes.Length, pBytes.Length);
                        ns.Write(authPkt, 0, authPkt.Length);
                        byte[] authResp = ReadExactBytes(ns, 2);
                        if (authResp[1] != 0x00) throw new IOException("SOCKS5 username/password authentication failed.");
                    }

                    byte[] reqPkt = new byte[4 + 1 + hostBytes.Length + 2];
                    reqPkt[0] = 0x05;
                    reqPkt[1] = 0x01; // CONNECT
                    reqPkt[2] = 0x00;
                    reqPkt[3] = 0x03; // Domain name
                    reqPkt[4] = (byte)hostBytes.Length;
                    Buffer.BlockCopy(hostBytes, 0, reqPkt, 5, hostBytes.Length);
                    reqPkt[5 + hostBytes.Length] = (byte)((targetPort >> 8) & 0xFF);
                    reqPkt[6 + hostBytes.Length] = (byte)(targetPort & 0xFF);
                    ns.Write(reqPkt, 0, reqPkt.Length);

                    byte[] hdr = ReadExactBytes(ns, 4);
                    if (hdr[1] != 0x00) throw new IOException("SOCKS5 CONNECT failed with code " + hdr[1]);
                    int skip = 0;
                    if (hdr[3] == 0x01) skip = 4 + 2;
                    else if (hdr[3] == 0x04) skip = 16 + 2;
                    else if (hdr[3] == 0x03)
                    {
                        byte[] dLen = ReadExactBytes(ns, 1);
                        skip = dLen[0] + 2;
                    }
                    if (skip > 0) ReadExactBytes(ns, skip);
                }
                return sock;
            }
            catch
            {
                try { sock.Close(); } catch { }
                throw;
            }
        }

        private static byte[] ReadExactBytes(Stream s, int count)
        {
            byte[] buf = new byte[count];
            int off = 0;
            while (off < count)
            {
                int r = s.Read(buf, off, count - off);
                if (r <= 0) throw new EndOfStreamException("Unexpected EOF during proxy handshake.");
                off += r;
            }
            return buf;
        }

        private static Socket DialDirectHappyEyeballs(
            string host,
            int port,
            MehediAppConfiguration cfg,
            IPAddress boundLocalIp)
        {
            List<IPAddress> candidates = ResolveHostCandidates(host, cfg);
            if (candidates.Count == 0)
            {
                throw new SocketException((int)SocketError.HostNotFound);
            }

            Exception lastEx = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                IPAddress ip = candidates[i];
                if (boundLocalIp != null && boundLocalIp.AddressFamily != ip.AddressFamily) continue;
                Socket sock = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    sock.NoDelay = true;
                    if (boundLocalIp != null)
                    {
                        sock.Bind(new IPEndPoint(boundLocalIp, 0));
                    }
                    IAsyncResult ar = sock.BeginConnect(new IPEndPoint(ip, port), null, null);
                    bool signaled;
                    try
                    {
                        signaled = ar.AsyncWaitHandle.WaitOne(8000);
                    }
                    finally
                    {
                        try { ar.AsyncWaitHandle.Close(); } catch { }
                    }
                    if (!signaled)
                    {
                        sock.Close();
                        throw new TimeoutException("Connect timed out to " + ip);
                    }
                    sock.EndConnect(ar);
                    return sock;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    try { sock.Close(); } catch { }
                }
            }
            if (lastEx != null) throw lastEx;
            throw new SocketException((int)SocketError.NetworkUnreachable);
        }

        public static List<IPAddress> ResolveHostCandidates(string host, MehediAppConfiguration cfg)
        {
            List<IPAddress> list = new List<IPAddress>();
            IPAddress literal;
            if (IPAddress.TryParse((host ?? "").Trim('[', ']'), out literal))
            {
                list.Add(literal);
                return list;
            }

            if (cfg != null && string.Equals(cfg.ShihabDnsMode, "Plain", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(cfg.ShihabPlainDnsServer))
            {
                IPAddress resolvedPlain = QueryPlainUdpDnsA(host, cfg.ShihabPlainDnsServer.Trim());
                if (resolvedPlain != null)
                {
                    list.Add(resolvedPlain);
                    return list;
                }
                if (!cfg.ShihabDnsFallbackToSystem) return list;
            }
            else if (cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabDnsMode) &&
                     cfg.ShihabDnsMode.StartsWith("Encrypted", StringComparison.OrdinalIgnoreCase))
            {
                IPAddress dohIp = ShihabDnsAndNetworkManager.ResolveHostIp(host, cfg);
                if (dohIp != null)
                {
                    list.Add(dohIp);
                    return list;
                }
                if (!cfg.ShihabDnsFallbackToSystem) return list;
            }

            try
            {
                IPAddress[] sys = Dns.GetHostAddresses(host);
                for (int i = 0; i < sys.Length; i++)
                {
                    if (sys[i].AddressFamily == AddressFamily.InterNetworkV6) list.Add(sys[i]);
                }
                for (int i = 0; i < sys.Length; i++)
                {
                    if (sys[i].AddressFamily == AddressFamily.InterNetwork) list.Add(sys[i]);
                }
            }
            catch { }
            return list;
        }

        public static IPAddress QueryPlainUdpDnsA(string domain, string dnsServerIp)
        {
            try
            {
                IPAddress serverAddr;
                if (string.IsNullOrWhiteSpace(domain) || !IPAddress.TryParse(dnsServerIp, out serverAddr)) return null;

                byte[] txBytes = new byte[2];
                using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(txBytes);
                }
                ushort txId = (ushort)((txBytes[0] << 8) | txBytes[1]);

                byte[] pkt;
                using (MemoryStream q = new MemoryStream())
                {
                    q.WriteByte(txBytes[0]);
                    q.WriteByte(txBytes[1]);
                    q.Write(new byte[] { 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 0, 10);
                    string[] labels = domain.Trim('.').Split('.');
                    for (int i = 0; i < labels.Length; i++)
                    {
                        byte[] lb = Encoding.ASCII.GetBytes(labels[i]);
                        if (lb.Length == 0 || lb.Length > 63) return null;
                        q.WriteByte((byte)lb.Length);
                        q.Write(lb, 0, lb.Length);
                    }
                    q.Write(new byte[] { 0x00, 0x00, 0x01, 0x00, 0x01 }, 0, 5);
                    pkt = q.ToArray();
                }

                using (UdpClient udp = new UdpClient(serverAddr.AddressFamily))
                {
                    udp.Client.ReceiveTimeout = 3500;
                    udp.Client.SendTimeout = 3500;
                    IPEndPoint ep = new IPEndPoint(serverAddr, 53);
                    udp.Send(pkt, pkt.Length, ep);
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] resp = udp.Receive(ref remote);
                    if (resp == null || resp.Length < 12) return null;
                    if (!remote.Address.Equals(serverAddr) || remote.Port != 53) return null;
                    ushort respId = (ushort)((resp[0] << 8) | resp[1]);
                    if (respId != txId) return null;
                    if ((resp[2] & 0x80) == 0 || (resp[3] & 0x0F) != 0) return null;

                    int anCount = (resp[6] << 8) | resp[7];
                    if (anCount <= 0) return null;

                    int idx = 12;
                    while (idx < resp.Length && resp[idx] != 0)
                    {
                        if ((resp[idx] & 0xC0) == 0xC0) { idx += 2; goto AfterQName; }
                        idx += 1 + resp[idx];
                    }
                    idx++;
                AfterQName:
                    idx += 4;
                    for (int a = 0; a < anCount && idx + 10 <= resp.Length; a++)
                    {
                        int guard = 0;
                        while (idx < resp.Length && guard++ < 64)
                        {
                            if ((resp[idx] & 0xC0) == 0xC0) { idx += 2; break; }
                            if (resp[idx] == 0) { idx++; break; }
                            idx += 1 + resp[idx];
                        }
                        if (idx + 10 > resp.Length) break;
                        int rType = (resp[idx] << 8) | resp[idx + 1];
                        int rdLen = (resp[idx + 8] << 8) | resp[idx + 9];
                        idx += 10;
                        if (rType == 1 && rdLen == 4 && idx + 4 <= resp.Length)
                        {
                            return new IPAddress(new byte[] { resp[idx], resp[idx + 1], resp[idx + 2], resp[idx + 3] });
                        }
                        idx += rdLen;
                    }
                }
            }
            catch { }
            return null;
        }
    }
}

