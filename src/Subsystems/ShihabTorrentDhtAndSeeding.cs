using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public sealed class ShihabTorrentPeerTelemetry
    {
        public string Endpoint;
        public string ClientName;
        public string Direction; // "Out" / "In"
        public string Flags;
        public long DownloadedBytes;
        public long UploadedBytes;
        public double ProgressPercent;
    }

    public sealed class ShihabTorrentTrackerTelemetry
    {
        public string Url;
        public string Status;
        public int PeersReturned;
        public int Seeders;
        public int Leechers;
        public DateTime LastAnnounceUtc;
    }

    public sealed class ShihabTorrentFileProgressTelemetry
    {
        public int Index;
        public string RelativePath;
        public long SizeBytes;
        public long CompletedBytes;
        public bool Selected;
    }

    public sealed class ShihabTorrentLiveSnapshot
    {
        public string ItemId;
        public string InfoHashHex;
        public bool IsPrivate;
        public bool IsSeeding;
        public int ListenPort;
        public int PieceCount;
        public int CompletedPieces;
        public int NeededPieces;
        public int ActivePeers;
        public int TotalKnownPeers;
        public int DhtPeersFound;
        public int PexPeersFound;
        public int LsdPeersFound;
        public long UploadedBytes;
        public double UploadSpeedBps;
        public double ShareRatio;
        public byte[] PieceStates; // 0 = missing/skipped, 1 = in-flight, 2 = verified
        public List<ShihabTorrentPeerTelemetry> Peers;
        public List<ShihabTorrentTrackerTelemetry> Trackers;
        public List<ShihabTorrentFileProgressTelemetry> Files;

        public ShihabTorrentLiveSnapshot()
        {
            PieceStates = new byte[0];
            Peers = new List<ShihabTorrentPeerTelemetry>();
            Trackers = new List<ShihabTorrentTrackerTelemetry>();
            Files = new List<ShihabTorrentFileProgressTelemetry>();
        }
    }

    public static class ShihabTorrentLiveTelemetry
    {
        private static readonly ConcurrentDictionary<string, ShihabTorrentLiveSnapshot> _snapshots =
            new ConcurrentDictionary<string, ShihabTorrentLiveSnapshot>(StringComparer.Ordinal);

        public static void Publish(string itemId, ShihabTorrentLiveSnapshot snap)
        {
            if (string.IsNullOrEmpty(itemId) || snap == null) return;
            _snapshots[itemId] = snap;
        }

        public static ShihabTorrentLiveSnapshot GetSnapshot(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            ShihabTorrentLiveSnapshot snap;
            return _snapshots.TryGetValue(itemId, out snap) ? snap : null;
        }

        public static void Clear(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            ShihabTorrentLiveSnapshot ignored;
            _snapshots.TryRemove(itemId, out ignored);
        }
    }

    internal static class ShihabTorrentSocketDialer
    {
        public static bool IsSocksProxyConfigured(MehediAppConfiguration cfg)
        {
            if (cfg == null) return false;
            string mode = (cfg.ShihabProxyMode ?? "").Trim();
            if (string.Equals(mode, "SOCKS5", StringComparison.OrdinalIgnoreCase) || cfg.ShihabUseSocksProxy)
            {
                string host = !string.IsNullOrWhiteSpace(cfg.MehediSocksHost) ? cfg.MehediSocksHost : cfg.ShihabProxyAddress;
                return !string.IsNullOrWhiteSpace(host);
            }
            return false;
        }

        public static bool HasBoundVpnAdapter(MehediAppConfiguration cfg)
        {
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.ShihabBoundNetworkAdapter)) return false;
            return !cfg.ShihabBoundNetworkAdapter.Trim().StartsWith("Any", StringComparison.OrdinalIgnoreCase);
        }

        public static IPAddress ResolveBoundLocalIpOrFail(MehediAppConfiguration cfg, out bool hasBinding)
        {
            hasBinding = HasBoundVpnAdapter(cfg);
            if (!hasBinding) return null;
            return ShihabDnsAndNetworkManager.GetBoundAdapterLocalIp(cfg.ShihabBoundNetworkAdapter);
        }

        public static Socket ConnectPeerTcp(
            IPEndPoint peerEp,
            MehediAppConfiguration cfg,
            int connectTimeoutMs,
            CancellationToken cancelToken)
        {
            if (peerEp == null || peerEp.Port <= 0 || peerEp.Port > 65535) return null;
            if (cancelToken.IsCancellationRequested) return null;

            bool hasVpnBind;
            IPAddress boundIp = ResolveBoundLocalIpOrFail(cfg, out hasVpnBind);
            if (hasVpnBind && boundIp == null)
            {
                // VPN Kill-Switch fail-closed: bound adapter is offline.
                return null;
            }

            if (IsSocksProxyConfigured(cfg))
            {
                return ConnectPeerViaSocks5(peerEp, cfg, boundIp, hasVpnBind, connectTimeoutMs, cancelToken);
            }

            if (hasVpnBind && boundIp.AddressFamily != peerEp.AddressFamily)
            {
                // Avoid cross-family bind SocketException; fail closed rather than leaking unbound IPv6.
                return null;
            }

            Socket sock = new Socket(peerEp.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                sock.NoDelay = true;
                if (hasVpnBind && boundIp != null)
                {
                    sock.Bind(new IPEndPoint(boundIp, 0));
                }
                if (!ConnectSocketWithTimeout(sock, peerEp, connectTimeoutMs, cancelToken))
                {
                    try { sock.Close(); } catch { }
                    return null;
                }
                return sock;
            }
            catch
            {
                try { sock.Close(); } catch { }
                return null;
            }
        }

        private static Socket ConnectPeerViaSocks5(
            IPEndPoint peerEp,
            MehediAppConfiguration cfg,
            IPAddress boundIp,
            bool hasVpnBind,
            int connectTimeoutMs,
            CancellationToken cancelToken)
        {
            string proxyHost = !string.IsNullOrWhiteSpace(cfg.MehediSocksHost)
                ? cfg.MehediSocksHost.Trim()
                : (cfg.ShihabProxyAddress ?? "").Trim();
            int proxyPort = cfg.ShihabSocksPort > 0 ? cfg.ShihabSocksPort : (cfg.MehediProxyPort > 0 ? cfg.MehediProxyPort : 1080);
            if (string.IsNullOrWhiteSpace(proxyHost) || proxyPort <= 0 || proxyPort > 65535) return null;

            IPAddress proxyIp;
            if (!IPAddress.TryParse(proxyHost, out proxyIp))
            {
                try
                {
                    IPAddress[] addrs = Dns.GetHostAddresses(proxyHost);
                    for (int i = 0; i < addrs.Length; i++)
                    {
                        if (!hasVpnBind || addrs[i].AddressFamily == boundIp.AddressFamily)
                        {
                            proxyIp = addrs[i];
                            break;
                        }
                    }
                }
                catch { }
            }
            if (proxyIp == null) return null;
            if (hasVpnBind && boundIp != null && boundIp.AddressFamily != proxyIp.AddressFamily) return null;

            Socket sock = new Socket(proxyIp.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                sock.NoDelay = true;
                sock.ReceiveTimeout = Math.Max(4000, connectTimeoutMs);
                sock.SendTimeout = Math.Max(4000, connectTimeoutMs);
                if (hasVpnBind && boundIp != null)
                {
                    sock.Bind(new IPEndPoint(boundIp, 0));
                }
                if (!ConnectSocketWithTimeout(sock, new IPEndPoint(proxyIp, proxyPort), connectTimeoutMs, cancelToken))
                {
                    try { sock.Close(); } catch { }
                    return null;
                }

                string user = cfg.ShihabProxyUser ?? "";
                string pass = cfg.MehediProxyPassword ?? "";
                bool hasAuth = !string.IsNullOrEmpty(user) || !string.IsNullOrEmpty(pass);

                using (NetworkStream ns = new NetworkStream(sock, false))
                using (cancelToken.Register(delegate { try { sock.Close(); } catch { } }))
                {
                    byte[] greeting = hasAuth ? new byte[] { 0x05, 0x02, 0x00, 0x02 } : new byte[] { 0x05, 0x01, 0x00 };
                    ns.Write(greeting, 0, greeting.Length);
                    byte[] methodResp = ReadExactRaw(ns, 2);
                    if (methodResp[0] != 0x05 || methodResp[1] == 0xFF)
                    {
                        try { sock.Close(); } catch { }
                        return null;
                    }
                    if (methodResp[1] == 0x02)
                    {
                        byte[] uBytes = Encoding.UTF8.GetBytes(user);
                        byte[] pBytes = Encoding.UTF8.GetBytes(pass);
                        if (uBytes.Length > 255 || pBytes.Length > 255) { try { sock.Close(); } catch { } return null; }
                        byte[] authReq = new byte[3 + uBytes.Length + pBytes.Length];
                        authReq[0] = 0x01;
                        authReq[1] = (byte)uBytes.Length;
                        Buffer.BlockCopy(uBytes, 0, authReq, 2, uBytes.Length);
                        authReq[2 + uBytes.Length] = (byte)pBytes.Length;
                        Buffer.BlockCopy(pBytes, 0, authReq, 3 + uBytes.Length, pBytes.Length);
                        ns.Write(authReq, 0, authReq.Length);
                        byte[] authResp = ReadExactRaw(ns, 2);
                        if (authResp[1] != 0x00)
                        {
                            try { sock.Close(); } catch { }
                            return null;
                        }
                    }

                    byte[] ipBytes = peerEp.Address.GetAddressBytes();
                    byte atyp = (peerEp.AddressFamily == AddressFamily.InterNetworkV6) ? (byte)0x04 : (byte)0x01;
                    byte[] req = new byte[4 + ipBytes.Length + 2];
                    req[0] = 0x05;
                    req[1] = 0x01; // CONNECT
                    req[2] = 0x00;
                    req[3] = atyp;
                    Buffer.BlockCopy(ipBytes, 0, req, 4, ipBytes.Length);
                    req[4 + ipBytes.Length] = (byte)((peerEp.Port >> 8) & 0xFF);
                    req[5 + ipBytes.Length] = (byte)(peerEp.Port & 0xFF);
                    ns.Write(req, 0, req.Length);

                    byte[] hdr = ReadExactRaw(ns, 4);
                    if (hdr[0] != 0x05 || hdr[1] != 0x00)
                    {
                        try { sock.Close(); } catch { }
                        return null;
                    }
                    if (hdr[3] == 0x01) ReadExactRaw(ns, 6);
                    else if (hdr[3] == 0x04) ReadExactRaw(ns, 18);
                    else if (hdr[3] == 0x03)
                    {
                        int dLen = ReadExactRaw(ns, 1)[0];
                        ReadExactRaw(ns, dLen + 2);
                    }
                }
                return sock;
            }
            catch
            {
                try { sock.Close(); } catch { }
                return null;
            }
        }

        private static bool ConnectSocketWithTimeout(Socket sock, IPEndPoint ep, int timeoutMs, CancellationToken ct)
        {
            IAsyncResult ar = null;
            WaitHandle wh = null;
            CancellationTokenRegistration reg = default(CancellationTokenRegistration);
            try
            {
                reg = ct.Register(delegate { try { sock.Close(); } catch { } });
                ar = sock.BeginConnect(ep, null, null);
                wh = ar.AsyncWaitHandle;
                if (!wh.WaitOne(timeoutMs))
                {
                    try { sock.Close(); } catch { }
                    return false;
                }
                if (ct.IsCancellationRequested)
                {
                    try { sock.Close(); } catch { }
                    return false;
                }
                sock.EndConnect(ar);
                return sock.Connected;
            }
            catch
            {
                return false;
            }
            finally
            {
                reg.Dispose();
                if (wh != null)
                {
                    try { wh.Close(); } catch { }
                }
            }
        }

        private static byte[] ReadExactRaw(Stream s, int count)
        {
            byte[] b = new byte[count];
            int off = 0;
            while (off < count)
            {
                int r = s.Read(b, off, count - off);
                if (r <= 0) throw new EndOfStreamException();
                off += r;
            }
            return b;
        }
    }

    internal static class ShihabTorrentDhtClient
    {
        private static readonly string[] DefaultBootstrapNodes = new string[]
        {
            "router.bittorrent.com:6881",
            "router.utorrent.com:6881",
            "dht.transmissionbt.com:6881",
            "dht.libtorrent.org:25401"
        };

        public static List<IPEndPoint> QueryPeersViaDht(
            byte[] infoHash20,
            int listenPort,
            MehediAppConfiguration cfg,
            bool allowLocalTest,
            Action<IPEndPoint> onPeerDiscovered,
            CancellationToken cancelToken)
        {
            List<IPEndPoint> foundPeers = new List<IPEndPoint>();
            if (infoHash20 == null || infoHash20.Length != 20) return foundPeers;
            if (cfg != null && !cfg.ShihabTorrentEnableDht) return foundPeers;
            if (ShihabTorrentSocketDialer.IsSocksProxyConfigured(cfg)) return foundPeers; // Fail-closed: zero UDP leaks under SOCKS5

            bool hasVpnBind;
            IPAddress boundIp = ShihabTorrentSocketDialer.ResolveBoundLocalIpOrFail(cfg, out hasVpnBind);
            if (hasVpnBind && boundIp == null) return foundPeers; // VPN Kill-Switch offline

            byte[] selfNodeId = new byte[20];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(selfNodeId);
            }

            List<IPEndPoint> bootstrapEps = ResolveBootstrapEndpoints(allowLocalTest);
            if (bootstrapEps.Count == 0) return foundPeers;

            HashSet<string> queriedNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> seenPeers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<IPEndPoint> frontier = new List<IPEndPoint>(bootstrapEps);
            Dictionary<string, byte[]> announceTokens = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            UdpClient udp = null;
            try
            {
                if (hasVpnBind && boundIp != null && boundIp.AddressFamily == AddressFamily.InterNetwork)
                {
                    udp = new UdpClient(new IPEndPoint(boundIp, 0));
                }
                else
                {
                    udp = new UdpClient(AddressFamily.InterNetwork);
                }
                udp.Client.ReceiveTimeout = 650;
                udp.Client.SendTimeout = 1000;

                using (cancelToken.Register(delegate { try { udp.Close(); } catch { } }))
                {
                    int maxRounds = allowLocalTest ? 2 : 3;
                    for (int round = 0; round < maxRounds && frontier.Count > 0 && !cancelToken.IsCancellationRequested; round++)
                    {
                        List<IPEndPoint> nextFrontier = new List<IPEndPoint>();
                        int batchLimit = Math.Min(frontier.Count, 24);
                        int sentThisRound = 0;

                        for (int i = 0; i < batchLimit && !cancelToken.IsCancellationRequested; i++)
                        {
                            IPEndPoint nodeEp = frontier[i];
                            if (nodeEp == null || nodeEp.Port <= 0 || nodeEp.Port > 65535) continue;
                            if (!allowLocalTest && ShihabProcessAndPathSafety.IsDisallowedInternalIpAddress(nodeEp.Address)) continue;
                            string nodeKey = nodeEp.ToString();
                            if (!queriedNodes.Add(nodeKey)) continue;

                            byte[] tx = new byte[] { (byte)('d'), (byte)((round * 32 + i) & 0x7F) };
                            byte[] pkt = BuildGetPeersQuery(tx, selfNodeId, infoHash20);
                            try
                            {
                                udp.Send(pkt, pkt.Length, nodeEp);
                                sentThisRound++;
                            }
                            catch { }
                        }

                        if (sentThisRound == 0) break;

                        Stopwatch sw = Stopwatch.StartNew();
                        int roundWindowMs = allowLocalTest ? 900 : 1800;
                        while (sw.ElapsedMilliseconds < roundWindowMs && !cancelToken.IsCancellationRequested)
                        {
                            try
                            {
                                IPEndPoint fromEp = new IPEndPoint(IPAddress.Any, 0);
                                byte[] resp = udp.Receive(ref fromEp);
                                if (resp == null || resp.Length < 10) continue;

                                int pos = 0;
                                ShihabBencodeNode root = ShihabBencodeParser.Parse(resp, ref pos);
                                if (root == null || root.DictValue == null) continue;
                                ShihabBencodeNode yNode, rNode;
                                if (!root.DictValue.TryGetValue("y", out yNode) || yNode == null || yNode.GetUtf8String() != "r") continue;
                                if (!root.DictValue.TryGetValue("r", out rNode) || rNode == null || rNode.DictValue == null) continue;

                                ShihabBencodeNode tokNode;
                                if (rNode.DictValue.TryGetValue("token", out tokNode) && tokNode != null && tokNode.BytesValue != null && tokNode.BytesValue.Length > 0)
                                {
                                    announceTokens[fromEp.ToString()] = tokNode.BytesValue;
                                }

                                ShihabBencodeNode valsNode;
                                if (rNode.DictValue.TryGetValue("values", out valsNode) && valsNode != null && valsNode.ListValue != null)
                                {
                                    for (int v = 0; v < valsNode.ListValue.Count; v++)
                                    {
                                        ShihabBencodeNode item = valsNode.ListValue[v];
                                        if (item == null || item.BytesValue == null || item.BytesValue.Length != 6) continue;
                                        byte[] pb = item.BytesValue;
                                        IPAddress pIp = new IPAddress(new byte[] { pb[0], pb[1], pb[2], pb[3] });
                                        int pPort = (pb[4] << 8) | pb[5];
                                        if (pPort <= 0 || pPort > 65535) continue;
                                        if (!allowLocalTest && ShihabProcessAndPathSafety.IsDisallowedInternalIpAddress(pIp)) continue;
                                        if (ShihabTorrentManager.IsPeerBlocked(pIp)) continue;
                                        IPEndPoint ep = new IPEndPoint(pIp, pPort);
                                        if (seenPeers.Add(ep.ToString()))
                                        {
                                            foundPeers.Add(ep);
                                            if (onPeerDiscovered != null) onPeerDiscovered(ep);
                                        }
                                    }
                                }

                                ShihabBencodeNode nodesNode;
                                if (rNode.DictValue.TryGetValue("nodes", out nodesNode) && nodesNode != null && nodesNode.BytesValue != null)
                                {
                                    byte[] nb = nodesNode.BytesValue;
                                    for (int off = 0; off + 26 <= nb.Length && nextFrontier.Count < 64; off += 26)
                                    {
                                        IPAddress nIp = new IPAddress(new byte[] { nb[off + 20], nb[off + 21], nb[off + 22], nb[off + 23] });
                                        int nPort = (nb[off + 24] << 8) | nb[off + 25];
                                        if (nPort <= 0 || nPort > 65535) continue;
                                        if (!allowLocalTest && ShihabProcessAndPathSafety.IsDisallowedInternalIpAddress(nIp)) continue;
                                        IPEndPoint nEp = new IPEndPoint(nIp, nPort);
                                        if (!queriedNodes.Contains(nEp.ToString()))
                                        {
                                            nextFrontier.Add(nEp);
                                        }
                                    }
                                }
                            }
                            catch (SocketException)
                            {
                                break;
                            }
                            catch
                            {
                                break;
                            }
                        }

                        if (foundPeers.Count >= 40) break;
                        frontier = nextFrontier;
                    }

                    // Best-effort BEP-5 announce_peer to nodes that gave us a write token
                    if (listenPort > 0 && announceTokens.Count > 0 && !cancelToken.IsCancellationRequested)
                    {
                        int announced = 0;
                        foreach (KeyValuePair<string, byte[]> kv in announceTokens)
                        {
                            if (announced >= 8 || cancelToken.IsCancellationRequested) break;
                            IPEndPoint targetEp;
                            if (TryParseHostPortEndPoint(kv.Key, out targetEp))
                            {
                                try
                                {
                                    byte[] annPkt = BuildAnnouncePeerQuery(new byte[] { (byte)'a', (byte)announced }, selfNodeId, infoHash20, listenPort, kv.Value);
                                    udp.Send(annPkt, annPkt.Length, targetEp);
                                    announced++;
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch { }
            finally
            {
                if (udp != null) { try { udp.Close(); } catch { } }
            }

            return foundPeers;
        }

        private static List<IPEndPoint> ResolveBootstrapEndpoints(bool allowLocalTest)
        {
            List<IPEndPoint> list = new List<IPEndPoint>();
            string custom = Environment.GetEnvironmentVariable("MHS_DHT_BOOTSTRAP");
            string[] sources;
            if (!string.IsNullOrWhiteSpace(custom) && allowLocalTest)
            {
                sources = custom.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            }
            else if (allowLocalTest)
            {
                return list; // Local tests without explicit bootstrap should not dial internet DHT
            }
            else
            {
                sources = DefaultBootstrapNodes;
            }

            for (int i = 0; i < sources.Length; i++)
            {
                IPEndPoint ep;
                if (TryParseHostPortEndPoint(sources[i], out ep))
                {
                    if (allowLocalTest || !ShihabProcessAndPathSafety.IsDisallowedInternalIpAddress(ep.Address))
                    {
                        list.Add(ep);
                    }
                }
            }
            return list;
        }

        internal static bool TryParseHostPortEndPoint(string hostPort, out IPEndPoint ep)
        {
            ep = null;
            if (string.IsNullOrWhiteSpace(hostPort)) return false;
            string s = hostPort.Trim();
            int lastColon = s.LastIndexOf(':');
            if (lastColon <= 0 || lastColon >= s.Length - 1) return false;
            string host = s.Substring(0, lastColon).Trim('[', ']');
            int port;
            if (!int.TryParse(s.Substring(lastColon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out port) || port <= 0 || port > 65535)
            {
                return false;
            }
            IPAddress ip;
            if (IPAddress.TryParse(host, out ip))
            {
                ep = new IPEndPoint(ip, port);
                return true;
            }
            try
            {
                IPAddress[] addrs = Dns.GetHostAddresses(host);
                for (int i = 0; i < addrs.Length; i++)
                {
                    if (addrs[i].AddressFamily == AddressFamily.InterNetwork)
                    {
                        ep = new IPEndPoint(addrs[i], port);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static byte[] BuildGetPeersQuery(byte[] tx, byte[] nodeId20, byte[] infoHash20)
        {
            Dictionary<string, object> args = new Dictionary<string, object>(StringComparer.Ordinal);
            args["id"] = nodeId20;
            args["info_hash"] = infoHash20;

            Dictionary<string, object> root = new Dictionary<string, object>(StringComparer.Ordinal);
            root["t"] = tx;
            root["y"] = "q";
            root["q"] = "get_peers";
            root["a"] = args;
            return ShihabBencodeParser.EncodeCanonical(root);
        }

        private static byte[] BuildAnnouncePeerQuery(byte[] tx, byte[] nodeId20, byte[] infoHash20, int port, byte[] token)
        {
            Dictionary<string, object> args = new Dictionary<string, object>(StringComparer.Ordinal);
            args["id"] = nodeId20;
            args["implied_port"] = 0;
            args["info_hash"] = infoHash20;
            args["port"] = port;
            args["token"] = token;

            Dictionary<string, object> root = new Dictionary<string, object>(StringComparer.Ordinal);
            root["t"] = tx;
            root["y"] = "q";
            root["q"] = "announce_peer";
            root["a"] = args;
            return ShihabBencodeParser.EncodeCanonical(root);
        }
    }

    internal static class ShihabTorrentLsdClient
    {
        public static void AnnounceOnce(string infoHashHex, int listenPort, MehediAppConfiguration cfg)
        {
            if (string.IsNullOrWhiteSpace(infoHashHex) || listenPort <= 0) return;
            if (cfg != null && !cfg.ShihabTorrentEnableLsd) return;
            if (ShihabTorrentSocketDialer.IsSocksProxyConfigured(cfg) || ShihabTorrentSocketDialer.HasBoundVpnAdapter(cfg)) return;

            try
            {
                string cookie = Guid.NewGuid().ToString("N").Substring(0, 8);
                string msg = string.Format(CultureInfo.InvariantCulture,
                    "BT-SEARCH * HTTP/1.1\r\nHost: 239.192.152.143:6771\r\nPort: {0}\r\nInfohash: {1}\r\ncookie: mhs-{2}\r\n\r\n",
                    listenPort, infoHashHex.ToLowerInvariant(), cookie);
                byte[] bytes = Encoding.ASCII.GetBytes(msg);
                using (UdpClient udp = new UdpClient(AddressFamily.InterNetwork))
                {
                    udp.Client.SendTimeout = 500;
                    udp.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Parse("239.192.152.143"), 6771));
                }
            }
            catch { }
        }
    }

    internal sealed class ShihabTorrentSeedServer : IDisposable
    {
        private readonly ShihabTorrentMetadata _meta;
        private readonly byte[] _peerId20;
        private readonly string _rootSaveDir;
        private readonly bool[] _pieceDone;
        private readonly object _pieceLock;
        private readonly object _diskLock;
        private readonly Action<int> _onUploadedBytes;
        private readonly Action<ShihabTorrentPeerTelemetry> _onPeerUpdated;
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private int _activeIncoming;
        private long _totalUploaded;

        public int ListenPort { get; private set; }
        public int ActiveIncomingPeers { get { return Volatile.Read(ref _activeIncoming); } }
        public long TotalUploadedBytes { get { return Interlocked.Read(ref _totalUploaded); } }

        public ShihabTorrentSeedServer(
            ShihabTorrentMetadata meta,
            byte[] peerId20,
            string rootSaveDir,
            bool[] pieceDone,
            object pieceLock,
            object diskLock,
            Action<int> onUploadedBytes,
            Action<ShihabTorrentPeerTelemetry> onPeerUpdated)
        {
            _meta = meta;
            _peerId20 = peerId20;
            _rootSaveDir = rootSaveDir;
            _pieceDone = pieceDone;
            _pieceLock = pieceLock;
            _diskLock = diskLock;
            _onUploadedBytes = onUploadedBytes;
            _onPeerUpdated = onPeerUpdated;
        }

        public bool TryStart(MehediAppConfiguration cfg, CancellationToken externalCancel)
        {
            // Never open a direct listening socket when SOCKS5 proxy is enabled (prevents IP leaks).
            if (ShihabTorrentSocketDialer.IsSocksProxyConfigured(cfg)) return false;

            bool hasVpnBind;
            IPAddress bindIp = ShihabTorrentSocketDialer.ResolveBoundLocalIpOrFail(cfg, out hasVpnBind);
            if (hasVpnBind && bindIp == null) return false;
            IPAddress listenAddr = (hasVpnBind && bindIp != null) ? bindIp : IPAddress.Any;

            int desiredPort = (cfg != null && cfg.ShihabTorrentListenPort > 0 && cfg.ShihabTorrentListenPort <= 65535)
                ? cfg.ShihabTorrentListenPort
                : 0;

            try
            {
                _listener = new TcpListener(listenAddr, desiredPort);
                ShihabMehediShared.ShihabProcessAndPathSafety.DisableListenerInheritance(_listener);
                _listener.Start(32);
                ShihabMehediShared.ShihabProcessAndPathSafety.DisableListenerInheritance(_listener);
            }
            catch
            {
                if (desiredPort != 0)
                {
                    try
                    {
                        _listener = new TcpListener(listenAddr, 0);
                        ShihabMehediShared.ShihabProcessAndPathSafety.DisableListenerInheritance(_listener);
                        _listener.Start(32);
                        ShihabMehediShared.ShihabProcessAndPathSafety.DisableListenerInheritance(_listener);
                    }
                    catch { _listener = null; return false; }
                }
                else
                {
                    _listener = null;
                    return false;
                }
            }

            ListenPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCancel);
            CancellationToken ct = _cts.Token;
            ct.Register(delegate { try { if (_listener != null) _listener.Stop(); } catch { } });

            Task.Run(delegate { AcceptLoop(ct); });
            return ListenPort > 0;
        }

        private void AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener != null)
            {
                TcpClient client = null;
                try
                {
                    client = _listener.AcceptTcpClient();
                }
                catch
                {
                    break;
                }
                if (client == null) continue;
                if (Volatile.Read(ref _activeIncoming) >= 24)
                {
                    try { client.Close(); } catch { }
                    continue;
                }
                TcpClient accepted = client;
                Task.Run(delegate { HandleIncomingPeer(accepted, ct); });
            }
        }

        private void HandleIncomingPeer(TcpClient client, CancellationToken ct)
        {
            Interlocked.Increment(ref _activeIncoming);
            long peerUploaded = 0;
            string remoteLabel = "incoming";
            try
            {
                IPEndPoint rep = client.Client.RemoteEndPoint as IPEndPoint;
                if (rep != null)
                {
                    remoteLabel = rep.ToString();
                    if (ShihabTorrentManager.IsPeerBlocked(rep.Address)) return;
                }
                client.NoDelay = true;
                client.ReceiveTimeout = 30000;
                client.SendTimeout = 15000;

                using (NetworkStream ns = client.GetStream())
                using (ct.Register(delegate { try { client.Close(); } catch { } }))
                {
                    byte[] peerHs = ShihabTorrentManager.ReadExactStreamPublic(ns, 68);
                    if (!ShihabTorrentManager.IsValidBitTorrentHandshake(peerHs, _meta.InfoHashBytes)) return;
                    bool peerSupportsExt = (peerHs[25] & 0x10) != 0;

                    byte[] ourHs = ShihabTorrentManager.BuildHandshakePublic(_meta.InfoHashBytes, _peerId20, true, false, !_meta.IsPrivate);
                    ns.Write(ourHs, 0, 68);

                    if (peerSupportsExt)
                    {
                        byte[] extHs = ShihabTorrentManager.BuildExtendedHandshakePacket(ListenPort, _meta.RawInfoBytes != null ? _meta.RawInfoBytes.Length : 0, !_meta.IsPrivate);
                        ns.Write(extHs, 0, extHs.Length);
                    }

                    // Send current bitfield
                    byte[] bfPkt = BuildBitfieldPacket();
                    if (bfPkt != null) ns.Write(bfPkt, 0, bfPkt.Length);

                    // Unchoke immediately to serve seeding requests
                    ns.Write(new byte[] { 0, 0, 0, 1, 1 }, 0, 5);

                    int peerUtMetaId = 0;
                    string peerClient = ShihabTorrentManager.DecodePeerClientName(peerHs);
                    ReportPeer(remoteLabel, peerClient, "U", 0, 0);

                    while (!ct.IsCancellationRequested)
                    {
                        byte[] lb = ShihabTorrentManager.ReadExactStreamPublic(ns, 4);
                        uint mLen = ShihabTorrentManager.ReadBeUInt32Public(lb, 0);
                        if (mLen == 0) continue;
                        if (mLen > 2 * 1024 * 1024) break;
                        byte[] payload = ShihabTorrentManager.ReadExactStreamPublic(ns, (int)mLen);
                        byte msgId = payload[0];

                        if (msgId == 2) // Interested -> ensure unchoked
                        {
                            ns.Write(new byte[] { 0, 0, 0, 1, 1 }, 0, 5);
                        }
                        else if (msgId == 6 && payload.Length >= 13) // Request (piece, begin, length)
                        {
                            int pIdx = (int)ShihabTorrentManager.ReadBeUInt32Public(payload, 1);
                            int begin = (int)ShihabTorrentManager.ReadBeUInt32Public(payload, 5);
                            int reqLen = (int)ShihabTorrentManager.ReadBeUInt32Public(payload, 9);
                            if (pIdx < 0 || pIdx >= _meta.PieceCount || begin < 0 || reqLen <= 0 || reqLen > 131072) continue;

                            bool havePiece = false;
                            lock (_pieceLock)
                            {
                                havePiece = pIdx < _pieceDone.Length && _pieceDone[pIdx];
                            }
                            if (!havePiece) continue;

                            byte[] block = ShihabTorrentManager.ReadPieceSliceFromDisk(_meta, pIdx, begin, reqLen, _rootSaveDir, _diskLock);
                            if (block != null && block.Length == reqLen)
                            {
                                byte[] pieceMsg = new byte[13 + reqLen];
                                ShihabTorrentManager.WriteBeUInt32Public(pieceMsg, 0, (uint)(9 + reqLen));
                                pieceMsg[4] = 7;
                                ShihabTorrentManager.WriteBeUInt32Public(pieceMsg, 5, (uint)pIdx);
                                ShihabTorrentManager.WriteBeUInt32Public(pieceMsg, 9, (uint)begin);
                                Buffer.BlockCopy(block, 0, pieceMsg, 13, reqLen);
                                ns.Write(pieceMsg, 0, pieceMsg.Length);

                                peerUploaded += reqLen;
                                Interlocked.Add(ref _totalUploaded, reqLen);
                                if (_onUploadedBytes != null) _onUploadedBytes(reqLen);
                                ReportPeer(remoteLabel, peerClient, "U", 0, peerUploaded);
                            }
                        }
                        else if (msgId == 20 && payload.Length >= 2) // BEP-10 / BEP-9 ut_metadata serving
                        {
                            byte extId = payload[1];
                            if (extId == 0)
                            {
                                int pos = 2;
                                ShihabBencodeNode d = ShihabBencodeParser.Parse(payload, ref pos);
                                if (d != null && d.DictValue != null)
                                {
                                    ShihabBencodeNode mNode, utNode, vNode;
                                    if (d.DictValue.TryGetValue("m", out mNode) && mNode != null && mNode.DictValue != null &&
                                        mNode.DictValue.TryGetValue("ut_metadata", out utNode) && utNode != null)
                                    {
                                        peerUtMetaId = (int)utNode.IntValue;
                                    }
                                    if (d.DictValue.TryGetValue("v", out vNode) && vNode != null && !string.IsNullOrWhiteSpace(vNode.GetUtf8String()))
                                    {
                                        peerClient = vNode.GetUtf8String();
                                    }
                                }
                            }
                            else if (extId == 2 && peerUtMetaId > 0 && _meta.RawInfoBytes != null && _meta.RawInfoBytes.Length > 0)
                            {
                                HandleUtMetadataRequest(ns, payload, (byte)peerUtMetaId, _meta.RawInfoBytes);
                            }
                        }
                    }
                }
            }
            catch { }
            finally
            {
                Interlocked.Decrement(ref _activeIncoming);
                try { client.Close(); } catch { }
            }
        }

        internal static void HandleUtMetadataRequest(Stream ns, byte[] payload, byte peerUtMetaId, byte[] rawInfoBytes)
        {
            int pos = 2;
            ShihabBencodeNode req = ShihabBencodeParser.Parse(payload, ref pos);
            if (req == null || req.DictValue == null) return;
            ShihabBencodeNode mtNode, pcNode;
            if (!req.DictValue.TryGetValue("msg_type", out mtNode) || mtNode == null || mtNode.IntValue != 0) return;
            if (!req.DictValue.TryGetValue("piece", out pcNode) || pcNode == null) return;
            int piece = (int)pcNode.IntValue;
            int offset = piece * 16384;
            if (piece < 0 || offset >= rawInfoBytes.Length)
            {
                Dictionary<string, object> rej = new Dictionary<string, object>(StringComparer.Ordinal);
                rej["msg_type"] = 2;
                rej["piece"] = piece;
                byte[] rejDict = ShihabBencodeParser.EncodeCanonical(rej);
                byte[] rejPkt = new byte[6 + rejDict.Length];
                ShihabTorrentManager.WriteBeUInt32Public(rejPkt, 0, (uint)(2 + rejDict.Length));
                rejPkt[4] = 20;
                rejPkt[5] = peerUtMetaId;
                Buffer.BlockCopy(rejDict, 0, rejPkt, 6, rejDict.Length);
                ns.Write(rejPkt, 0, rejPkt.Length);
                return;
            }
            int chunkLen = Math.Min(16384, rawInfoBytes.Length - offset);
            Dictionary<string, object> hdr = new Dictionary<string, object>(StringComparer.Ordinal);
            hdr["msg_type"] = 1;
            hdr["piece"] = piece;
            hdr["total_size"] = rawInfoBytes.Length;
            byte[] hdrBytes = ShihabBencodeParser.EncodeCanonical(hdr);
            byte[] pkt = new byte[6 + hdrBytes.Length + chunkLen];
            ShihabTorrentManager.WriteBeUInt32Public(pkt, 0, (uint)(2 + hdrBytes.Length + chunkLen));
            pkt[4] = 20;
            pkt[5] = peerUtMetaId;
            Buffer.BlockCopy(hdrBytes, 0, pkt, 6, hdrBytes.Length);
            Buffer.BlockCopy(rawInfoBytes, offset, pkt, 6 + hdrBytes.Length, chunkLen);
            ns.Write(pkt, 0, pkt.Length);
        }

        private byte[] BuildBitfieldPacket()
        {
            if (_meta.PieceCount <= 0) return null;
            int bfLen = (_meta.PieceCount + 7) >> 3;
            byte[] pkt = new byte[5 + bfLen];
            ShihabTorrentManager.WriteBeUInt32Public(pkt, 0, (uint)(1 + bfLen));
            pkt[4] = 5;
            bool anyBit = false;
            lock (_pieceLock)
            {
                for (int p = 0; p < _meta.PieceCount && p < _pieceDone.Length; p++)
                {
                    if (_pieceDone[p])
                    {
                        pkt[5 + (p >> 3)] |= (byte)(1 << (7 - (p & 7)));
                        anyBit = true;
                    }
                }
            }
            return anyBit ? pkt : null;
        }

        private void ReportPeer(string ep, string client, string flags, long down, long up)
        {
            if (_onPeerUpdated == null) return;
            _onPeerUpdated(new ShihabTorrentPeerTelemetry
            {
                Endpoint = ep,
                ClientName = client,
                Direction = "In",
                Flags = flags,
                DownloadedBytes = down,
                UploadedBytes = up,
                ProgressPercent = 0.0
            });
        }

        public void Dispose()
        {
            try { if (_cts != null) _cts.Cancel(); } catch { }
            try { if (_listener != null) _listener.Stop(); } catch { }
            try { if (_cts != null) _cts.Dispose(); } catch { }
            _listener = null;
            _cts = null;
        }
    }
}
