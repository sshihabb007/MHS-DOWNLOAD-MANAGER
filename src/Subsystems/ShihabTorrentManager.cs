using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public class ShihabTorrentFileEntry
    {
        public int Index { get; set; }
        public string RelativePath { get; set; }
        public long SizeBytes { get; set; }
        public long ByteOffsetInTorrent { get; set; }
        public bool IsPaddingFile { get; set; }
        public bool Selected { get; set; }

        public long Length
        {
            get { return SizeBytes; }
            set { SizeBytes = value; }
        }
    }

    public class ShihabTorrentMetadata
    {
        public string Name { get; set; }
        public string InfoHashHex { get; set; }
        public byte[] InfoHashBytes { get; set; }
        public long TotalSizeBytes { get; set; }
        public long PieceLength { get; set; }
        public int PieceCount { get; set; }
        public byte[] PiecesSha1Concat { get; set; }
        public bool IsMultiFile { get; set; }
        public string Comment { get; set; }
        public string ErrorMessage { get; set; }
        public List<string> Trackers { get; set; }
        public List<string> WebSeeds { get; set; }
        public List<ShihabTorrentFileEntry> Files { get; set; }

        public bool ParsedSuccessfully
        {
            get { return string.IsNullOrEmpty(ErrorMessage) && (Files.Count > 0 || !string.IsNullOrEmpty(InfoHashHex)); }
            set { }
        }

        public long TotalSize
        {
            get { return TotalSizeBytes; }
            set { TotalSizeBytes = value; }
        }

        public ShihabTorrentMetadata()
        {
            Name = "torrent_download";
            InfoHashHex = "";
            InfoHashBytes = new byte[0];
            PiecesSha1Concat = new byte[0];
            ErrorMessage = "";
            Trackers = new List<string>();
            WebSeeds = new List<string>();
            Files = new List<ShihabTorrentFileEntry>();
        }
    }

    public static class ShihabTorrentManager
    {
        private const int MaxTorrentDownloadBytes = 10 * 1024 * 1024;
        private const int MaxBlocklistBytes = 32 * 1024 * 1024;
        private const int MaxTrackerResponseBytes = 4 * 1024 * 1024;

        private struct IpRangeEntry : IComparable<IpRangeEntry>
        {
            public uint Start;
            public uint End;
            public int CompareTo(IpRangeEntry other) { return Start.CompareTo(other.Start); }
        }

        private static readonly object _blocklistLock = new object();
        private static string _loadedBlocklistSource = "";
        private static List<IpRangeEntry> _blockedRanges = new List<IpRangeEntry>();

        private static readonly string[] DefaultPublicTrackers = new string[]
        {
            "udp://tracker.opentrackr.org:1337/announce",
            "udp://open.stealth.si:80/announce",
            "udp://tracker.torrent.eu.org:451/announce",
            "udp://exodus.desync.com:6969/announce"
        };

        public static bool IsMagnetOrTorrentInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            string s = input.Trim();
            if (s.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase)) return true;
            Uri u;
            if (Uri.TryCreate(s, UriKind.Absolute, out u))
            {
                return u.AbsolutePath.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase);
            }
            return s.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase);
        }

        public static byte[] DecodeHexOrBase32Btih(string rawHash)
        {
            if (string.IsNullOrWhiteSpace(rawHash)) return new byte[0];
            string clean = rawHash.Trim();
            if (clean.Length == 40)
            {
                byte[] bytes = new byte[20];
                for (int i = 0; i < 20; i++)
                {
                    byte b;
                    if (!byte.TryParse(clean.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
                        return new byte[0];
                    bytes[i] = b;
                }
                return bytes;
            }
            if (clean.Length == 32)
            {
                string upper = clean.ToUpperInvariant();
                int bitBuf = 0;
                int bitCount = 0;
                byte[] outBytes = new byte[20];
                int outIdx = 0;
                for (int i = 0; i < 32; i++)
                {
                    char c = upper[i];
                    int val;
                    if (c >= 'A' && c <= 'Z') val = c - 'A';
                    else if (c >= '2' && c <= '7') val = 26 + (c - '2');
                    else return new byte[0];
                    bitBuf = (bitBuf << 5) | val;
                    bitCount += 5;
                    if (bitCount >= 8 && outIdx < 20)
                    {
                        bitCount -= 8;
                        outBytes[outIdx++] = (byte)((bitBuf >> bitCount) & 0xFF);
                    }
                }
                return outIdx == 20 ? outBytes : new byte[0];
            }
            return new byte[0];
        }

        private static string BytesToHexLower(byte[] data)
        {
            if (data == null || data.Length == 0) return "";
            StringBuilder sb = new StringBuilder(data.Length * 2);
            for (int i = 0; i < data.Length; i++) sb.Append(data[i].ToString("x2"));
            return sb.ToString();
        }

        private static byte[] ReadStreamBounded(Stream s, int maxBytes)
        {
            if (s == null) return new byte[0];
            byte[] buf = new byte[16384];
            using (MemoryStream ms = new MemoryStream())
            {
                int total = 0;
                int r;
                while (total < maxBytes && (r = s.Read(buf, 0, Math.Min(buf.Length, maxBytes - total))) > 0)
                {
                    ms.Write(buf, 0, r);
                    total += r;
                }
                return ms.ToArray();
            }
        }

        public static ShihabTorrentMetadata ParseMagnetUri(string magnetUri)
        {
            ShihabTorrentMetadata meta = new ShihabTorrentMetadata();
            if (string.IsNullOrWhiteSpace(magnetUri)) return meta;
            int q = magnetUri.IndexOf('?');
            string query = q >= 0 ? magnetUri.Substring(q + 1) : magnetUri;
            string[] parts = query.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                int eq = parts[i].IndexOf('=');
                if (eq <= 0) continue;
                string k = parts[i].Substring(0, eq).ToLowerInvariant();
                string v = Uri.UnescapeDataString(parts[i].Substring(eq + 1).Replace('+', ' '));
                if (k == "dn" && !string.IsNullOrWhiteSpace(v))
                {
                    meta.Name = ShihabFormatHelpers.SanitizeShihabFileName(v);
                }
                else if (k == "xt" && v.StartsWith("urn:btih:", StringComparison.OrdinalIgnoreCase))
                {
                    byte[] ih = DecodeHexOrBase32Btih(v.Substring(9).Trim());
                    if (ih.Length == 20)
                    {
                        meta.InfoHashBytes = ih;
                        meta.InfoHashHex = BytesToHexLower(ih);
                    }
                    else
                    {
                        meta.InfoHashHex = v.Substring(9).Trim().ToLowerInvariant();
                    }
                }
                else if (k == "tr" && !string.IsNullOrWhiteSpace(v))
                {
                    if (!meta.Trackers.Contains(v.Trim())) meta.Trackers.Add(v.Trim());
                }
                else if ((k == "ws" || k == "as") && !string.IsNullOrWhiteSpace(v))
                {
                    if (!meta.WebSeeds.Contains(v.Trim())) meta.WebSeeds.Add(v.Trim());
                }
                else if (k == "xl")
                {
                    long sz = 0;
                    if (long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out sz) && sz > 0) meta.TotalSizeBytes = sz;
                }
            }
            for (int d = 0; d < DefaultPublicTrackers.Length; d++)
            {
                if (!meta.Trackers.Contains(DefaultPublicTrackers[d])) meta.Trackers.Add(DefaultPublicTrackers[d]);
            }
            if (meta.Name == "torrent_download" && !string.IsNullOrEmpty(meta.InfoHashHex))
            {
                meta.Name = "magnet_" + meta.InfoHashHex.Substring(0, Math.Min(12, meta.InfoHashHex.Length));
            }
            meta.Files.Add(new ShihabTorrentFileEntry
            {
                Index = 0,
                RelativePath = meta.Name,
                SizeBytes = meta.TotalSizeBytes,
                Selected = true
            });
            return meta;
        }

        public static ShihabTorrentMetadata InspectTorrentOrMagnet(string input, MehediAppConfiguration cfg = null)
        {
            if (string.IsNullOrWhiteSpace(input)) return new ShihabTorrentMetadata { ErrorMessage = "Empty torrent input." };
            string trimmed = input.Trim();
            if (trimmed.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
            {
                return ParseMagnetUri(trimmed);
            }
            if (File.Exists(trimmed))
            {
                return InspectTorrentFile(trimmed);
            }
            Uri remoteUri;
            if (ShihabProcessAndPathSafety.TryValidateHttpUrl(trimmed, false, out remoteUri))
            {
                if (ShihabProcessAndPathSafety.IsHostDisallowedForOutboundSsrf(remoteUri.Host, true))
                {
                    return new ShihabTorrentMetadata { ErrorMessage = "Blocked restricted/internal URL for .torrent download." };
                }
                try
                {
                    ShihabAutoUpdater.EnforceModernTls();
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(remoteUri);
                    req.UserAgent = "MHS-Download-Manager/" + ShihabAutoUpdater.GetCurrentAppVersion();
                    req.AllowAutoRedirect = false;
                    req.Timeout = 15000;
                    req.ReadWriteTimeout = 15000;
                    if (cfg != null) ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, remoteUri.AbsoluteUri);
                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    using (Stream s = resp.GetResponseStream())
                    {
                        byte[] bytes = ReadStreamBounded(s, MaxTorrentDownloadBytes);
                        return InspectTorrentBytes(bytes, Path.GetFileNameWithoutExtension(remoteUri.AbsolutePath));
                    }
                }
                catch (Exception ex)
                {
                    return new ShihabTorrentMetadata { ErrorMessage = "Failed to download .torrent file: " + ex.Message };
                }
            }
            return new ShihabTorrentMetadata { ErrorMessage = "Torrent file not found: " + trimmed };
        }

        public static ShihabTorrentMetadata InspectTorrentFile(string torrentFilePath)
        {
            FileInfo fi = new FileInfo(torrentFilePath);
            if (!fi.Exists || fi.Length > 32 * 1024 * 1024)
            {
                return new ShihabTorrentMetadata { ErrorMessage = "Torrent file is missing or exceeds 32 MB limit." };
            }
            byte[] raw = File.ReadAllBytes(torrentFilePath);
            return InspectTorrentBytes(raw, Path.GetFileNameWithoutExtension(torrentFilePath));
        }

        public static ShihabTorrentMetadata InspectTorrentBytes(byte[] raw, string fallbackName)
        {
            ShihabTorrentMetadata meta = new ShihabTorrentMetadata();
            meta.Name = string.IsNullOrWhiteSpace(fallbackName) ? "torrent_download" : ShihabFormatHelpers.SanitizeShihabFileName(fallbackName);
            if (raw == null || raw.Length == 0)
            {
                meta.ErrorMessage = "Empty .torrent data.";
                return meta;
            }

            try
            {
                int idx = 0;
                ShihabBencodeNode root = ShihabBencodeParser.Parse(raw, ref idx);
                if (root == null || root.DictValue == null)
                {
                    meta.ErrorMessage = "Invalid bencoded .torrent dictionary.";
                    return meta;
                }

                ShihabBencodeNode announceNode;
                if (root.DictValue.TryGetValue("announce", out announceNode) && announceNode != null)
                {
                    string tr = announceNode.GetUtf8String();
                    if (!string.IsNullOrWhiteSpace(tr)) meta.Trackers.Add(tr);
                }
                ShihabBencodeNode announceListNode;
                if (root.DictValue.TryGetValue("announce-list", out announceListNode) && announceListNode != null && announceListNode.ListValue != null)
                {
                    for (int i = 0; i < announceListNode.ListValue.Count; i++)
                    {
                        ShihabBencodeNode tier = announceListNode.ListValue[i];
                        if (tier != null && tier.ListValue != null)
                        {
                            for (int j = 0; j < tier.ListValue.Count; j++)
                            {
                                if (tier.ListValue[j] == null) continue;
                                string tr = tier.ListValue[j].GetUtf8String();
                                if (!string.IsNullOrWhiteSpace(tr) && !meta.Trackers.Contains(tr))
                                {
                                    meta.Trackers.Add(tr);
                                }
                            }
                        }
                    }
                }

                ShihabBencodeNode urlListNode;
                if (root.DictValue.TryGetValue("url-list", out urlListNode) && urlListNode != null)
                {
                    if (urlListNode.Kind == "bytes")
                    {
                        string ws = urlListNode.GetUtf8String();
                        if (!string.IsNullOrWhiteSpace(ws)) meta.WebSeeds.Add(ws);
                    }
                    else if (urlListNode.ListValue != null)
                    {
                        for (int i = 0; i < urlListNode.ListValue.Count; i++)
                        {
                            if (urlListNode.ListValue[i] == null) continue;
                            string ws = urlListNode.ListValue[i].GetUtf8String();
                            if (!string.IsNullOrWhiteSpace(ws)) meta.WebSeeds.Add(ws);
                        }
                    }
                }

                ShihabBencodeNode commentNode;
                if (root.DictValue.TryGetValue("comment", out commentNode) && commentNode != null)
                {
                    meta.Comment = commentNode.GetUtf8String();
                }

                ShihabBencodeNode infoNode;
                if (root.DictValue.TryGetValue("info", out infoNode) && infoNode != null && infoNode.DictValue != null)
                {
                    PopulateMetadataFromInfoNode(meta, raw, infoNode);
                }
            }
            catch (Exception ex)
            {
                meta.ErrorMessage = "Malformed .torrent metadata: " + ex.Message;
            }
            return meta;
        }

        private static void PopulateMetadataFromInfoNode(ShihabTorrentMetadata meta, byte[] rawContainer, ShihabBencodeNode infoNode)
        {
            int infoLen = infoNode.RawEndOffset - infoNode.RawStartOffset;
            if (infoLen > 0 && infoNode.RawStartOffset >= 0 && infoNode.RawEndOffset <= rawContainer.Length)
            {
                using (SHA1 sha1 = SHA1.Create())
                {
                    byte[] hash = sha1.ComputeHash(rawContainer, infoNode.RawStartOffset, infoLen);
                    meta.InfoHashBytes = hash;
                    meta.InfoHashHex = BytesToHexLower(hash);
                }
            }

            ShihabBencodeNode nameNode;
            if (infoNode.DictValue.TryGetValue("name.utf-8", out nameNode) && nameNode != null && !string.IsNullOrWhiteSpace(nameNode.GetUtf8String()))
            {
                meta.Name = ShihabFormatHelpers.SanitizeShihabFileName(nameNode.GetUtf8String());
            }
            else if (infoNode.DictValue.TryGetValue("name", out nameNode) && nameNode != null && !string.IsNullOrWhiteSpace(nameNode.GetUtf8String()))
            {
                meta.Name = ShihabFormatHelpers.SanitizeShihabFileName(nameNode.GetUtf8String());
            }

            ShihabBencodeNode plNode;
            if (infoNode.DictValue.TryGetValue("piece length", out plNode) && plNode != null)
            {
                meta.PieceLength = plNode.IntValue;
            }
            ShihabBencodeNode piecesNode;
            if (infoNode.DictValue.TryGetValue("pieces", out piecesNode) && piecesNode != null && piecesNode.BytesValue != null)
            {
                meta.PiecesSha1Concat = piecesNode.BytesValue;
                meta.PieceCount = piecesNode.BytesValue.Length / 20;
            }

            meta.Files.Clear();
            ShihabBencodeNode filesNode;
            if (infoNode.DictValue.TryGetValue("files", out filesNode) && filesNode != null && filesNode.ListValue != null)
            {
                meta.IsMultiFile = true;
                long runningOffset = 0;
                for (int i = 0; i < filesNode.ListValue.Count; i++)
                {
                    ShihabBencodeNode fDict = filesNode.ListValue[i];
                    if (fDict == null || fDict.DictValue == null) continue;
                    long fLen = 0;
                    ShihabBencodeNode lenNode;
                    if (fDict.DictValue.TryGetValue("length", out lenNode) && lenNode != null)
                    {
                        fLen = Math.Max(0, lenNode.IntValue);
                    }
                    string attrStr = "";
                    ShihabBencodeNode attrNode;
                    if (fDict.DictValue.TryGetValue("attr", out attrNode) && attrNode != null)
                    {
                        attrStr = attrNode.GetUtf8String();
                    }

                    string relPath = "file_" + i;
                    ShihabBencodeNode pathNode;
                    if (!fDict.DictValue.TryGetValue("path.utf-8", out pathNode) || pathNode == null)
                    {
                        fDict.DictValue.TryGetValue("path", out pathNode);
                    }
                    bool isPad = attrStr.IndexOf('p') >= 0;
                    if (pathNode != null && pathNode.ListValue != null)
                    {
                        List<string> segs = new List<string>();
                        for (int p = 0; p < pathNode.ListValue.Count; p++)
                        {
                            if (pathNode.ListValue[p] == null) continue;
                            string seg = pathNode.ListValue[p].GetUtf8String();
                            if (p == 0 && seg == ".pad") isPad = true;
                            if (string.IsNullOrWhiteSpace(seg) || seg == ".." || seg == "." || seg.IndexOf(':') >= 0 || seg.IndexOf('\0') >= 0) continue;
                            segs.Add(ShihabProcessAndPathSafety.SanitizeFileName(seg, "seg_" + p));
                        }
                        if (segs.Count > 0) relPath = string.Join(Path.DirectorySeparatorChar.ToString(), segs.ToArray());
                    }

                    meta.Files.Add(new ShihabTorrentFileEntry
                    {
                        Index = i,
                        RelativePath = relPath,
                        SizeBytes = fLen,
                        ByteOffsetInTorrent = runningOffset,
                        IsPaddingFile = isPad,
                        Selected = !isPad
                    });
                    runningOffset += fLen;
                }
                meta.TotalSizeBytes = runningOffset;
            }
            else
            {
                meta.IsMultiFile = false;
                ShihabBencodeNode lenNode;
                long singleLen = 0;
                if (infoNode.DictValue.TryGetValue("length", out lenNode) && lenNode != null)
                {
                    singleLen = Math.Max(0, lenNode.IntValue);
                }
                meta.TotalSizeBytes = singleLen;
                meta.Files.Add(new ShihabTorrentFileEntry
                {
                    Index = 0,
                    RelativePath = ShihabProcessAndPathSafety.SanitizeFileName(meta.Name, "torrent_download"),
                    SizeBytes = singleLen,
                    ByteOffsetInTorrent = 0,
                    IsPaddingFile = false,
                    Selected = true
                });
            }
        }

        public static void EnsurePeerBlocklistLoaded(MehediAppConfiguration cfg, string baseDataDir)
        {
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.ShihabTorrentBlocklistUrl)) return;
            string src = cfg.ShihabTorrentBlocklistUrl.Trim();
            lock (_blocklistLock)
            {
                if (string.Equals(_loadedBlocklistSource, src, StringComparison.OrdinalIgnoreCase)) return;
                _loadedBlocklistSource = src;
                _blockedRanges.Clear();
            }

            try
            {
                string rawText = "";
                if (File.Exists(src))
                {
                    FileInfo fi = new FileInfo(src);
                    if (fi.Length <= MaxBlocklistBytes)
                    {
                        rawText = File.ReadAllText(src, Encoding.UTF8);
                    }
                }
                else
                {
                    Uri srcUri;
                    if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(src, false, out srcUri) ||
                        ShihabProcessAndPathSafety.IsHostDisallowedForOutboundSsrf(srcUri.Host, true))
                    {
                        return;
                    }
                    string cacheFile = Path.Combine(baseDataDir ?? Path.GetTempPath(), "bt_blocklist.txt");
                    if (!File.Exists(cacheFile) || (DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFile)).TotalHours > 24)
                    {
                        ShihabAutoUpdater.EnforceModernTls();
                        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(srcUri);
                        req.AllowAutoRedirect = false;
                        req.Timeout = 10000;
                        req.ReadWriteTimeout = 10000;
                        ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, srcUri.AbsoluteUri);
                        using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                        using (Stream st = resp.GetResponseStream())
                        {
                            if (src.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                            {
                                using (GZipStream gz = new GZipStream(st, CompressionMode.Decompress))
                                {
                                    rawText = Encoding.UTF8.GetString(ReadStreamBounded(gz, MaxBlocklistBytes));
                                }
                            }
                            else
                            {
                                rawText = Encoding.UTF8.GetString(ReadStreamBounded(st, MaxBlocklistBytes));
                            }
                        }
                        try { File.WriteAllText(cacheFile, rawText, Encoding.UTF8); } catch { }
                    }
                    else
                    {
                        rawText = File.ReadAllText(cacheFile, Encoding.UTF8);
                    }
                }

                List<IpRangeEntry> parsed = new List<IpRangeEntry>();
                string[] lines = rawText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i++)
                {
                    string ln = lines[i].Trim();
                    if (ln.Length == 0 || ln[0] == '#' || ln.StartsWith("//", StringComparison.Ordinal)) continue;
                    int colon = ln.LastIndexOf(':');
                    string rangeSpec = colon >= 0 ? ln.Substring(colon + 1).Trim() : ln;
                    int dash = rangeSpec.IndexOf('-');
                    if (dash > 0)
                    {
                        uint a, b;
                        if (TryParseIpv4Uint(rangeSpec.Substring(0, dash), out a) &&
                            TryParseIpv4Uint(rangeSpec.Substring(dash + 1), out b))
                        {
                            parsed.Add(new IpRangeEntry { Start = Math.Min(a, b), End = Math.Max(a, b) });
                        }
                    }
                    else
                    {
                        int slash = rangeSpec.IndexOf('/');
                        int prefix;
                        uint ipVal;
                        if (slash > 0 &&
                            TryParseIpv4Uint(rangeSpec.Substring(0, slash), out ipVal) &&
                            int.TryParse(rangeSpec.Substring(slash + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out prefix) &&
                            prefix >= 0 && prefix <= 32)
                        {
                            uint mask = prefix == 0 ? 0u : (0xFFFFFFFFu << (32 - prefix));
                            uint start = ipVal & mask;
                            uint end = start | ~mask;
                            parsed.Add(new IpRangeEntry { Start = start, End = end });
                        }
                    }
                }
                parsed.Sort();
                lock (_blocklistLock) { _blockedRanges = parsed; }
            }
            catch { }
        }

        private static bool TryParseIpv4Uint(string text, out uint val)
        {
            val = 0;
            IPAddress ip;
            if (!IPAddress.TryParse((text ?? "").Trim(), out ip) || ip.AddressFamily != AddressFamily.InterNetwork) return false;
            byte[] b = ip.GetAddressBytes();
            val = ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
            return true;
        }

        public static bool IsPeerBlocked(IPAddress ip)
        {
            if (ip == null || ip.AddressFamily != AddressFamily.InterNetwork) return false;
            byte[] b = ip.GetAddressBytes();
            uint v = ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
            lock (_blocklistLock)
            {
                int lo = 0;
                int hi = _blockedRanges.Count - 1;
                while (lo <= hi)
                {
                    int mid = lo + ((hi - lo) >> 1);
                    IpRangeEntry r = _blockedRanges[mid];
                    if (v < r.Start) hi = mid - 1;
                    else if (v > r.End) lo = mid + 1;
                    else return true;
                }
            }
            return false;
        }

        public static List<IPEndPoint> DiscoverPeersFromTrackers(
            ShihabTorrentMetadata meta,
            byte[] peerId20,
            int listenPort,
            MehediAppConfiguration cfg)
        {
            List<IPEndPoint> peers = new List<IPEndPoint>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (meta == null || meta.InfoHashBytes == null || meta.InfoHashBytes.Length != 20) return peers;

            List<string> trackers = new List<string>(meta.Trackers);
            for (int d = 0; d < DefaultPublicTrackers.Length; d++)
            {
                if (!trackers.Contains(DefaultPublicTrackers[d])) trackers.Add(DefaultPublicTrackers[d]);
            }

            for (int i = 0; i < trackers.Count && peers.Count < 80; i++)
            {
                string tr = trackers[i];
                if (string.IsNullOrWhiteSpace(tr)) continue;
                try
                {
                    List<IPEndPoint> batch = null;
                    if (tr.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        tr.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        batch = AnnounceHttpTracker(tr, meta.InfoHashBytes, peerId20, listenPort, Math.Max(0, meta.TotalSizeBytes), cfg);
                    }
                    else if (tr.StartsWith("udp://", StringComparison.OrdinalIgnoreCase))
                    {
                        batch = AnnounceUdpTracker(tr, meta.InfoHashBytes, peerId20, listenPort, Math.Max(0, meta.TotalSizeBytes));
                    }
                    if (batch != null)
                    {
                        for (int p = 0; p < batch.Count; p++)
                        {
                            IPEndPoint ep = batch[p];
                            if (ep == null || ep.Port <= 0 || IsPeerBlocked(ep.Address)) continue;
                            string key = ep.ToString();
                            if (seen.Add(key)) peers.Add(ep);
                        }
                    }
                }
                catch { }
            }
            return peers;
        }

        private static string UrlEncodeRaw20Bytes(byte[] b20)
        {
            StringBuilder sb = new StringBuilder(60);
            for (int i = 0; i < b20.Length; i++)
            {
                sb.Append('%').Append(b20[i].ToString("X2"));
            }
            return sb.ToString();
        }

        private static List<IPEndPoint> AnnounceHttpTracker(
            string trackerUrl,
            byte[] infoHash20,
            byte[] peerId20,
            int port,
            long leftBytes,
            MehediAppConfiguration cfg)
        {
            List<IPEndPoint> list = new List<IPEndPoint>();
            Uri trUri;
            if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(trackerUrl, false, out trUri) ||
                ShihabProcessAndPathSafety.IsHostDisallowedForOutboundSsrf(trUri.Host, true))
            {
                return list;
            }
            string sep = trackerUrl.IndexOf('?') >= 0 ? "&" : "?";
            string full = string.Format(CultureInfo.InvariantCulture,
                "{0}{1}info_hash={2}&peer_id={3}&port={4}&uploaded=0&downloaded=0&left={5}&compact=1&numwant=50&event=started",
                trUri.AbsoluteUri, sep, UrlEncodeRaw20Bytes(infoHash20), UrlEncodeRaw20Bytes(peerId20), port, leftBytes);

            ShihabAutoUpdater.EnforceModernTls();
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(full);
            req.UserAgent = "MHS-Download-Manager/1.0";
            req.AllowAutoRedirect = false;
            req.Timeout = 6000;
            req.ReadWriteTimeout = 6000;
            if (cfg != null) ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, full);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (Stream s = resp.GetResponseStream())
            {
                byte[] raw = ReadStreamBounded(s, MaxTrackerResponseBytes);
                int idx = 0;
                ShihabBencodeNode root = ShihabBencodeParser.Parse(raw, ref idx);
                if (root != null && root.DictValue != null)
                {
                    ShihabBencodeNode peersNode;
                    if (root.DictValue.TryGetValue("peers", out peersNode) && peersNode != null)
                    {
                        if (peersNode.Kind == "bytes" && peersNode.BytesValue != null)
                        {
                            byte[] pb = peersNode.BytesValue;
                            for (int i = 0; i + 6 <= pb.Length; i += 6)
                            {
                                IPAddress ip = new IPAddress(new byte[] { pb[i], pb[i + 1], pb[i + 2], pb[i + 3] });
                                int pPort = (pb[i + 4] << 8) | pb[i + 5];
                                if (pPort > 0) list.Add(new IPEndPoint(ip, pPort));
                            }
                        }
                        else if (peersNode.ListValue != null)
                        {
                            for (int i = 0; i < peersNode.ListValue.Count; i++)
                            {
                                ShihabBencodeNode pd = peersNode.ListValue[i];
                                if (pd == null || pd.DictValue == null) continue;
                                ShihabBencodeNode ipN, ptN;
                                IPAddress ip;
                                if (pd.DictValue.TryGetValue("ip", out ipN) && pd.DictValue.TryGetValue("port", out ptN) &&
                                    ipN != null && ptN != null && IPAddress.TryParse(ipN.GetUtf8String(), out ip) && ptN.IntValue > 0 && ptN.IntValue <= 65535)
                                {
                                    list.Add(new IPEndPoint(ip, (int)ptN.IntValue));
                                }
                            }
                        }
                    }
                }
            }
            return list;
        }

        private static List<IPEndPoint> AnnounceUdpTracker(
            string udpUrl,
            byte[] infoHash20,
            byte[] peerId20,
            int port,
            long leftBytes)
        {
            List<IPEndPoint> list = new List<IPEndPoint>();
            Uri u;
            if (!Uri.TryCreate(udpUrl, UriKind.Absolute, out u)) return list;
            int trPort = u.Port > 0 ? u.Port : 80;
            if (ShihabProcessAndPathSafety.IsHostDisallowedForOutboundSsrf(u.Host, true))
            {
                return list;
            }
            IPAddress[] addrs = Dns.GetHostAddresses(u.Host);
            IPAddress targetIp = null;
            for (int i = 0; i < addrs.Length; i++)
            {
                if (addrs[i].AddressFamily == AddressFamily.InterNetwork && !ShihabProcessAndPathSafety.IsDisallowedInternalIpAddress(addrs[i]))
                {
                    targetIp = addrs[i];
                    break;
                }
            }
            if (targetIp == null) return list;

            using (UdpClient udp = new UdpClient(AddressFamily.InterNetwork))
            {
                udp.Client.ReceiveTimeout = 4000;
                udp.Client.SendTimeout = 4000;
                IPEndPoint remote = new IPEndPoint(targetIp, trPort);

                byte[] connReq = new byte[16];
                WriteBeUInt64(connReq, 0, 0x0000041727101980UL);
                WriteBeUInt32(connReq, 8, 0u);
                uint txId = (uint)(Environment.TickCount ^ 0x5A5AA5A5);
                WriteBeUInt32(connReq, 12, txId);
                udp.Send(connReq, connReq.Length, remote);

                IPEndPoint fromEp = new IPEndPoint(IPAddress.Any, 0);
                byte[] connResp = udp.Receive(ref fromEp);
                if (connResp == null || connResp.Length < 16) return list;
                if (ReadBeUInt32(connResp, 0) != 0u || ReadBeUInt32(connResp, 4) != txId) return list;
                ulong connectionId = ReadBeUInt64(connResp, 8);

                byte[] annReq = new byte[98];
                WriteBeUInt64(annReq, 0, connectionId);
                WriteBeUInt32(annReq, 8, 1u);
                uint txId2 = txId + 1u;
                WriteBeUInt32(annReq, 12, txId2);
                Buffer.BlockCopy(infoHash20, 0, annReq, 16, 20);
                Buffer.BlockCopy(peerId20, 0, annReq, 36, 20);
                WriteBeUInt64(annReq, 56, 0UL);
                WriteBeUInt64(annReq, 64, (ulong)Math.Max(0L, leftBytes));
                WriteBeUInt64(annReq, 72, 0UL);
                WriteBeUInt32(annReq, 80, 2u);
                WriteBeUInt32(annReq, 84, 0u);
                WriteBeUInt32(annReq, 88, txId ^ 0x12345678u);
                WriteBeUInt32(annReq, 92, 50u);
                annReq[96] = (byte)((port >> 8) & 0xFF);
                annReq[97] = (byte)(port & 0xFF);

                udp.Send(annReq, annReq.Length, remote);
                byte[] annResp = udp.Receive(ref fromEp);
                if (annResp == null || annResp.Length < 20) return list;
                if (ReadBeUInt32(annResp, 0) != 1u || ReadBeUInt32(annResp, 4) != txId2) return list;

                for (int i = 20; i + 6 <= annResp.Length; i += 6)
                {
                    IPAddress ip = new IPAddress(new byte[] { annResp[i], annResp[i + 1], annResp[i + 2], annResp[i + 3] });
                    int pPort = (annResp[i + 4] << 8) | annResp[i + 5];
                    if (pPort > 0) list.Add(new IPEndPoint(ip, pPort));
                }
            }
            return list;
        }

        public static bool ExecuteP2pTorrentSession(
            ShihabDownloadItem item,
            ShihabTorrentMetadata meta,
            MehediAppConfiguration cfg,
            string baseDataDir,
            Action onProgressChanged,
            CancellationToken cancelToken,
            out string errorMessage)
        {
            errorMessage = "";
            if (meta == null) { errorMessage = "Missing torrent metadata."; return false; }
            EnsurePeerBlocklistLoaded(cfg, baseDataDir);

            byte[] peerId = Encoding.ASCII.GetBytes("-MH1050-" + Guid.NewGuid().ToString("N").Substring(0, 12));
            int listenPort = 51413;

            item.ShihabStatusText = "Announcing to trackers...";
            if (onProgressChanged != null) onProgressChanged();
            List<IPEndPoint> peers = DiscoverPeersFromTrackers(meta, peerId, listenPort, cfg);

            if (meta.PieceCount <= 0 && meta.InfoHashBytes != null && meta.InfoHashBytes.Length == 20)
            {
                item.ShihabStatusText = string.Format("Fetching magnet metadata ({0} peers)...", peers.Count);
                if (onProgressChanged != null) onProgressChanged();
                bool gotMeta = false;
                for (int p = 0; p < peers.Count && !cancelToken.IsCancellationRequested; p++)
                {
                    byte[] infoDictBytes = TryFetchMagnetMetadataFromPeer(peers[p], meta.InfoHashBytes, peerId, cfg, cancelToken);
                    if (infoDictBytes != null && infoDictBytes.Length > 0)
                    {
                        using (SHA1 sha1 = SHA1.Create())
                        {
                            byte[] check = sha1.ComputeHash(infoDictBytes);
                            if (BytesToHexLower(check) == meta.InfoHashHex)
                            {
                                try
                                {
                                    int pos = 0;
                                    ShihabBencodeNode infoNode = ShihabBencodeParser.Parse(infoDictBytes, ref pos);
                                    if (infoNode != null && infoNode.DictValue != null)
                                    {
                                        PopulateMetadataFromInfoNode(meta, infoDictBytes, infoNode);
                                        gotMeta = meta.PieceCount > 0;
                                        if (gotMeta) break;
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
                if (!gotMeta && meta.WebSeeds.Count == 0)
                {
                    errorMessage = peers.Count == 0
                        ? "No BitTorrent peers responded from trackers (check firewall/UDP trackers or configure a Debrid account)."
                        : "Connected peers did not supply BEP-9 magnet metadata.";
                    return false;
                }
            }

            if (meta.PieceCount <= 0 || meta.PieceLength <= 0 || meta.TotalSizeBytes <= 0)
            {
                errorMessage = "Torrent metadata has zero pieces or unknown total size.";
                return false;
            }

            HashSet<int> selectedIdx = null;
            if (item.ShihabSelectedTorrentFiles != null && item.ShihabSelectedTorrentFiles.Count > 0)
            {
                selectedIdx = new HashSet<int>(item.ShihabSelectedTorrentFiles);
            }
            for (int i = 0; i < meta.Files.Count; i++)
            {
                if (meta.Files[i].IsPaddingFile) meta.Files[i].Selected = false;
                else if (selectedIdx != null && selectedIdx.Count > 0) meta.Files[i].Selected = selectedIdx.Contains(meta.Files[i].Index);
            }

            bool[] pieceNeeded = new bool[meta.PieceCount];
            bool[] pieceDone = new bool[meta.PieceCount];
            bool[] pieceInFlight = new bool[meta.PieceCount];
            long selectedBytesTotal = 0;
            for (int f = 0; f < meta.Files.Count; f++)
            {
                ShihabTorrentFileEntry fe = meta.Files[f];
                if (!fe.Selected || fe.SizeBytes <= 0) continue;
                selectedBytesTotal += fe.SizeBytes;
                int firstP = (int)(fe.ByteOffsetInTorrent / meta.PieceLength);
                int lastP = (int)((fe.ByteOffsetInTorrent + fe.SizeBytes - 1) / meta.PieceLength);
                for (int p = Math.Max(0, firstP); p <= Math.Min(meta.PieceCount - 1, lastP); p++)
                {
                    pieceNeeded[p] = true;
                }
            }

            item.ShihabTotalBytes = selectedBytesTotal > 0 ? selectedBytesTotal : meta.TotalSizeBytes;
            string rootSaveDir = meta.IsMultiFile
                ? Path.Combine(item.MehediSaveDirectory, ShihabProcessAndPathSafety.SanitizeFileName(meta.Name, "torrent_download"))
                : item.MehediSaveDirectory;
            Directory.CreateDirectory(rootSaveDir);

            string safeHashPrefix = ShihabProcessAndPathSafety.SanitizeFileName(
                meta.InfoHashHex.Substring(0, Math.Min(16, meta.InfoHashHex.Length)),
                "bt");
            string stateFile = Path.Combine(rootSaveDir, "." + safeHashPrefix + ".mhs.btstate");
            long verifiedBytesSoFar = 0;
            if (File.Exists(stateFile))
            {
                try
                {
                    string stateBits = File.ReadAllText(stateFile, Encoding.ASCII).Trim();
                    for (int p = 0; p < Math.Min(stateBits.Length, meta.PieceCount); p++)
                    {
                        if (stateBits[p] == '1' && pieceNeeded[p])
                        {
                            pieceDone[p] = true;
                            verifiedBytesSoFar += GetPieceSize(meta, p);
                        }
                    }
                }
                catch { }
            }
            item.MehediDownloadedBytes = Math.Min(item.ShihabTotalBytes, verifiedBytesSoFar);

            if (peers.Count == 0 && meta.WebSeeds.Count == 0)
            {
                errorMessage = "No active peers or WebSeeds found for this torrent.";
                return false;
            }

            object pieceLock = new object();
            object diskWriteLock = new object();
            long windowBytes = 0;
            Stopwatch speedSw = Stopwatch.StartNew();
            int activePeers = 0;
            bool sequential = item.ShihabTorrentSequential || (cfg != null && cfg.ShihabTorrentSequentialDefault);

            List<Task> workers = new List<Task>();

            for (int w = 0; w < meta.WebSeeds.Count && !meta.IsMultiFile; w++)
            {
                string wsUrl = meta.WebSeeds[w];
                workers.Add(Task.Run(delegate
                {
                    DownloadPiecesFromWebSeed(wsUrl, meta, cfg, pieceNeeded, pieceDone, pieceInFlight, pieceLock, diskWriteLock, rootSaveDir, stateFile,
                        delegate(int gotLen)
                        {
                            Interlocked.Add(ref windowBytes, gotLen);
                            UpdateTorrentProgressUi(item, meta, pieceNeeded, pieceDone, pieceLock, ref windowBytes, speedSw, Math.Max(1, activePeers), onProgressChanged);
                        }, cancelToken);
                }));
            }

            for (int pIdx = 0; pIdx < Math.Min(peers.Count, 12); pIdx++)
            {
                IPEndPoint peerEp = peers[pIdx];
                workers.Add(Task.Run(delegate
                {
                    DownloadPiecesFromTcpPeer(peerEp, meta, peerId, cfg, sequential, pieceNeeded, pieceDone, pieceInFlight, pieceLock, diskWriteLock,
                        rootSaveDir, stateFile, ref activePeers,
                        delegate(int gotLen)
                        {
                            Interlocked.Add(ref windowBytes, gotLen);
                            UpdateTorrentProgressUi(item, meta, pieceNeeded, pieceDone, pieceLock, ref windowBytes, speedSw, activePeers, onProgressChanged);
                        }, cancelToken);
                }));
            }

            try { Task.WaitAll(workers.ToArray()); } catch { }
            cancelToken.ThrowIfCancellationRequested();

            bool allDone = true;
            lock (pieceLock)
            {
                for (int p = 0; p < meta.PieceCount; p++)
                {
                    if (pieceNeeded[p] && !pieceDone[p]) { allDone = false; break; }
                }
            }

            if (!allDone)
            {
                errorMessage = "Incomplete torrent transfer: all peers disconnected before remaining pieces finished (progress saved for resume).";
                return false;
            }

            try { if (File.Exists(stateFile)) File.Delete(stateFile); } catch { }
            item.MehediDownloadedBytes = item.ShihabTotalBytes;
            item.ShihabCurrentSpeedBps = 0;
            if (meta.IsMultiFile) item.ShihabFileName = ShihabProcessAndPathSafety.SanitizeFileName(meta.Name, "torrent_download");
            else if (meta.Files.Count == 1) item.ShihabFileName = ShihabProcessAndPathSafety.SanitizeFileName(meta.Files[0].RelativePath, "torrent_download");
            return true;
        }

        private static void UpdateTorrentProgressUi(
            ShihabDownloadItem item,
            ShihabTorrentMetadata meta,
            bool[] needed,
            bool[] done,
            object pieceLock,
            ref long windowBytes,
            Stopwatch sw,
            int activePeers,
            Action onProgress)
        {
            long elapsedMs = sw.ElapsedMilliseconds;
            if (elapsedMs < 350) return;
            long bytesDelta = Interlocked.Exchange(ref windowBytes, 0);
            sw.Restart();

            int doneCount = 0;
            int needCount = 0;
            long doneBytes = 0;
            lock (pieceLock)
            {
                for (int p = 0; p < meta.PieceCount; p++)
                {
                    if (!needed[p]) continue;
                    needCount++;
                    if (done[p])
                    {
                        doneCount++;
                        doneBytes += GetPieceSize(meta, p);
                    }
                }
            }
            item.MehediDownloadedBytes = Math.Min(item.ShihabTotalBytes, doneBytes);
            item.ShihabCurrentSpeedBps = elapsedMs > 0 ? (bytesDelta * 1000.0 / elapsedMs) : 0;
            item.ShihabStatusText = string.Format("Downloading ({0}/{1} pcs, {2} peers)", doneCount, needCount, activePeers);
            if (onProgress != null) onProgress();
        }

        private static int GetPieceSize(ShihabTorrentMetadata meta, int pieceIndex)
        {
            long start = (long)pieceIndex * meta.PieceLength;
            long rem = meta.TotalSizeBytes - start;
            return (int)Math.Max(0L, Math.Min(meta.PieceLength, rem));
        }

        private static bool VerifyAndWritePieceToDisk(
            ShihabTorrentMetadata meta,
            int pieceIndex,
            byte[] pieceData,
            string rootSaveDir,
            string stateFile,
            bool[] pieceDone,
            object pieceLock,
            object diskWriteLock)
        {
            if (pieceData == null) return false;
            if (meta.PiecesSha1Concat != null && (pieceIndex + 1) * 20 <= meta.PiecesSha1Concat.Length)
            {
                using (SHA1 sha1 = SHA1.Create())
                {
                    byte[] actual = sha1.ComputeHash(pieceData);
                    for (int b = 0; b < 20; b++)
                    {
                        if (actual[b] != meta.PiecesSha1Concat[pieceIndex * 20 + b])
                        {
                            return false;
                        }
                    }
                }
            }

            long pieceGlobalStart = (long)pieceIndex * meta.PieceLength;
            long pieceGlobalEnd = pieceGlobalStart + pieceData.Length;

            lock (diskWriteLock)
            {
                for (int f = 0; f < meta.Files.Count; f++)
                {
                    ShihabTorrentFileEntry fe = meta.Files[f];
                    if (!fe.Selected || fe.IsPaddingFile || fe.SizeBytes <= 0) continue;
                    long fStart = fe.ByteOffsetInTorrent;
                    long fEnd = fStart + fe.SizeBytes;
                    long ovStart = Math.Max(pieceGlobalStart, fStart);
                    long ovEnd = Math.Min(pieceGlobalEnd, fEnd);
                    if (ovEnd <= ovStart) continue;

                    string fullFilePath;
                    if (!ShihabProcessAndPathSafety.ResolvePathStrictlyUnderRoot(rootSaveDir, fe.RelativePath, out fullFilePath))
                    {
                        throw new InvalidOperationException("Blocked torrent path traversal: " + fe.RelativePath);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(fullFilePath));
                    using (FileStream fs = new FileStream(fullFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                    {
                        if (fs.Length == 0 && fe.SizeBytes > 65536)
                        {
                            ShihabOsAndDiskSafety.TryMarkFileSparse(fs);
                        }
                        fs.Seek(ovStart - fStart, SeekOrigin.Begin);
                        fs.Write(pieceData, (int)(ovStart - pieceGlobalStart), (int)(ovEnd - ovStart));
                    }
                }

                lock (pieceLock)
                {
                    pieceDone[pieceIndex] = true;
                    char[] chars = new char[meta.PieceCount];
                    for (int i = 0; i < meta.PieceCount; i++) chars[i] = pieceDone[i] ? '1' : '0';
                    try { File.WriteAllText(stateFile, new string(chars), Encoding.ASCII); } catch { }
                }
            }
            return true;
        }

        private static int ClaimNextPieceIndex(
            int pieceCount,
            bool[] needed,
            bool[] done,
            bool[] inFlight,
            bool[] peerHasPiece,
            bool sequential,
            int peerSeedHint,
            object pieceLock)
        {
            lock (pieceLock)
            {
                if (sequential)
                {
                    for (int p = 0; p < pieceCount; p++)
                    {
                        if (needed[p] && !done[p] && !inFlight[p] && (peerHasPiece == null || peerHasPiece[p]))
                        {
                            inFlight[p] = true;
                            return p;
                        }
                    }
                }
                else
                {
                    int start = Math.Abs(peerSeedHint) % Math.Max(1, pieceCount);
                    for (int k = 0; k < pieceCount; k++)
                    {
                        int p = (start + k) % pieceCount;
                        if (needed[p] && !done[p] && !inFlight[p] && (peerHasPiece == null || peerHasPiece[p]))
                        {
                            inFlight[p] = true;
                            return p;
                        }
                    }
                }
                for (int p = 0; p < pieceCount; p++)
                {
                    if (needed[p] && !done[p] && (peerHasPiece == null || peerHasPiece[p]))
                    {
                        return p;
                    }
                }
            }
            return -1;
        }

        private static void DownloadPiecesFromWebSeed(
            string wsUrl,
            ShihabTorrentMetadata meta,
            MehediAppConfiguration cfg,
            bool[] needed,
            bool[] done,
            bool[] inFlight,
            object pieceLock,
            object diskWriteLock,
            string rootSaveDir,
            string stateFile,
            Action<int> onBytesRead,
            CancellationToken cancelToken)
        {
            Uri wsUri;
            if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(wsUrl, false, out wsUri) ||
                ShihabProcessAndPathSafety.IsHostDisallowedForOutboundSsrf(wsUri.Host, true))
            {
                return;
            }
            ShihabAutoUpdater.EnforceModernTls();
            while (!cancelToken.IsCancellationRequested)
            {
                int pIdx = ClaimNextPieceIndex(meta.PieceCount, needed, done, inFlight, null, true, 0, pieceLock);
                if (pIdx < 0) break;
                int pSize = GetPieceSize(meta, pIdx);
                long byteStart = (long)pIdx * meta.PieceLength;
                long byteEnd = byteStart + pSize - 1;
                try
                {
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(wsUri);
                    req.AllowAutoRedirect = false;
                    req.AddRange(byteStart, byteEnd);
                    req.Timeout = 15000;
                    req.ReadWriteTimeout = 15000;
                    if (cfg != null) ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, wsUri.AbsoluteUri);
                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    {
                        if (resp.StatusCode != HttpStatusCode.PartialContent && pIdx > 0) break;
                        byte[] buf = new byte[pSize];
                        int off = 0;
                        using (Stream s = resp.GetResponseStream())
                        {
                            while (off < pSize)
                            {
                                cancelToken.ThrowIfCancellationRequested();
                                int r = s.Read(buf, off, pSize - off);
                                if (r <= 0) break;
                                off += r;
                                if (onBytesRead != null) onBytesRead(r);
                            }
                        }
                        if (off == pSize && VerifyAndWritePieceToDisk(meta, pIdx, buf, rootSaveDir, stateFile, done, pieceLock, diskWriteLock))
                        {
                            continue;
                        }
                    }
                }
                catch { }
                lock (pieceLock) { inFlight[pIdx] = false; }
                break;
            }
        }

        private static void DownloadPiecesFromTcpPeer(
            IPEndPoint peerEp,
            ShihabTorrentMetadata meta,
            byte[] peerId20,
            MehediAppConfiguration cfg,
            bool sequential,
            bool[] needed,
            bool[] done,
            bool[] inFlight,
            object pieceLock,
            object diskWriteLock,
            string rootSaveDir,
            string stateFile,
            ref int activePeersCounter,
            Action<int> onBytesRead,
            CancellationToken cancelToken)
        {
            if (IsPeerBlocked(peerEp.Address)) return;
            Socket sock = new Socket(peerEp.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            bool countedActive = false;
            int claimedPiece = -1;
            try
            {
                if (cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabBoundNetworkAdapter))
                {
                    IPAddress boundIp = ShihabDnsAndNetworkManager.GetBoundAdapterLocalIp(cfg.ShihabBoundNetworkAdapter);
                    if (boundIp == null) return;
                    sock.Bind(new IPEndPoint(boundIp, 0));
                }
                sock.ReceiveTimeout = 12000;
                sock.SendTimeout = 10000;
                IAsyncResult ar = sock.BeginConnect(peerEp, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(5000)) { sock.Close(); return; }
                sock.EndConnect(ar);

                using (NetworkStream ns = new NetworkStream(sock, true))
                using (cancelToken.Register(delegate { try { sock.Close(); } catch { } }))
                {
                    byte[] hs = BuildBitTorrentHandshake(meta.InfoHashBytes, peerId20, false);
                    ns.Write(hs, 0, 68);
                    byte[] peerHs = ReadExactStream(ns, 68);
                    if (peerHs[0] != 19) return;
                    for (int i = 0; i < 20; i++)
                    {
                        if (peerHs[28 + i] != meta.InfoHashBytes[i]) return;
                    }

                    Interlocked.Increment(ref activePeersCounter);
                    countedActive = true;

                    bool[] peerHas = new bool[meta.PieceCount];
                    bool peerChoking = true;

                    ns.Write(new byte[] { 0, 0, 0, 1, 2 }, 0, 5);

                    byte[] currentPieceBuf = null;
                    int currentPieceSize = 0;
                    int currentPieceReceived = 0;
                    int nextRequestOffset = 0;
                    int outstandingBlocks = 0;

                    while (!cancelToken.IsCancellationRequested)
                    {
                        if (!peerChoking)
                        {
                            if (claimedPiece < 0)
                            {
                                claimedPiece = ClaimNextPieceIndex(meta.PieceCount, needed, done, inFlight, peerHas, sequential, peerEp.Port, pieceLock);
                                if (claimedPiece < 0) break;
                                currentPieceSize = GetPieceSize(meta, claimedPiece);
                                currentPieceBuf = new byte[currentPieceSize];
                                currentPieceReceived = 0;
                                nextRequestOffset = 0;
                                outstandingBlocks = 0;
                            }

                            while (outstandingBlocks < 8 && nextRequestOffset < currentPieceSize)
                            {
                                int blkLen = Math.Min(16384, currentPieceSize - nextRequestOffset);
                                byte[] reqMsg = new byte[17];
                                WriteBeUInt32(reqMsg, 0, 13u);
                                reqMsg[4] = 6;
                                WriteBeUInt32(reqMsg, 5, (uint)claimedPiece);
                                WriteBeUInt32(reqMsg, 9, (uint)nextRequestOffset);
                                WriteBeUInt32(reqMsg, 13, (uint)blkLen);
                                ns.Write(reqMsg, 0, 17);
                                nextRequestOffset += blkLen;
                                outstandingBlocks++;
                            }
                        }

                        byte[] lenBytes = ReadExactStream(ns, 4);
                        uint msgLen = ReadBeUInt32(lenBytes, 0);
                        if (msgLen == 0) continue;
                        if (msgLen > 2 * 1024 * 1024) break;

                        byte[] payload = ReadExactStream(ns, (int)msgLen);
                        byte msgId = payload[0];

                        if (msgId == 0) peerChoking = true;
                        else if (msgId == 1) peerChoking = false;
                        else if (msgId == 4 && payload.Length >= 5)
                        {
                            int p = (int)ReadBeUInt32(payload, 1);
                            if (p >= 0 && p < peerHas.Length) peerHas[p] = true;
                        }
                        else if (msgId == 5)
                        {
                            for (int p = 0; p < peerHas.Length; p++)
                            {
                                int byteIdx = 1 + (p >> 3);
                                if (byteIdx < payload.Length && ((payload[byteIdx] >> (7 - (p & 7))) & 1) != 0)
                                {
                                    peerHas[p] = true;
                                }
                            }
                        }
                        else if (msgId == 7 && payload.Length > 9)
                        {
                            int pIndex = (int)ReadBeUInt32(payload, 1);
                            int begin = (int)ReadBeUInt32(payload, 5);
                            int blkDataLen = payload.Length - 9;
                            if (outstandingBlocks > 0) outstandingBlocks--;
                            if (pIndex == claimedPiece && currentPieceBuf != null && begin >= 0 && ((long)begin + blkDataLen) <= currentPieceSize)
                            {
                                Buffer.BlockCopy(payload, 9, currentPieceBuf, begin, blkDataLen);
                                currentPieceReceived += blkDataLen;
                                if (onBytesRead != null) onBytesRead(blkDataLen);

                                if (currentPieceReceived >= currentPieceSize)
                                {
                                    if (!VerifyAndWritePieceToDisk(meta, claimedPiece, currentPieceBuf, rootSaveDir, stateFile, done, pieceLock, diskWriteLock))
                                    {
                                        lock (pieceLock) { inFlight[claimedPiece] = false; }
                                    }
                                    claimedPiece = -1;
                                    currentPieceBuf = null;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            finally
            {
                if (claimedPiece >= 0)
                {
                    lock (pieceLock) { if (!done[claimedPiece]) inFlight[claimedPiece] = false; }
                }
                if (countedActive) Interlocked.Decrement(ref activePeersCounter);
                try { sock.Close(); } catch { }
            }
        }

        private static byte[] TryFetchMagnetMetadataFromPeer(
            IPEndPoint peerEp,
            byte[] infoHash20,
            byte[] peerId20,
            MehediAppConfiguration cfg,
            CancellationToken cancelToken)
        {
            if (IsPeerBlocked(peerEp.Address)) return null;
            Socket sock = new Socket(peerEp.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                sock.ReceiveTimeout = 6000;
                sock.SendTimeout = 6000;
                IAsyncResult ar = sock.BeginConnect(peerEp, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(3500)) { sock.Close(); return null; }
                sock.EndConnect(ar);

                using (NetworkStream ns = new NetworkStream(sock, true))
                using (cancelToken.Register(delegate { try { sock.Close(); } catch { } }))
                {
                    byte[] hs = BuildBitTorrentHandshake(infoHash20, peerId20, true);
                    ns.Write(hs, 0, 68);
                    byte[] peerHs = ReadExactStream(ns, 68);
                    if (peerHs[0] != 19 || (peerHs[25] & 0x10) == 0) return null;

                    byte[] extHsDict = Encoding.ASCII.GetBytes("d1:md11:ut_metadatai2eee");
                    byte[] extHsMsg = new byte[6 + extHsDict.Length];
                    WriteBeUInt32(extHsMsg, 0, (uint)(2 + extHsDict.Length));
                    extHsMsg[4] = 20;
                    extHsMsg[5] = 0;
                    Buffer.BlockCopy(extHsDict, 0, extHsMsg, 6, extHsDict.Length);
                    ns.Write(extHsMsg, 0, extHsMsg.Length);

                    int peerUtMetadataId = 0;
                    int metadataSize = 0;
                    byte[] assembled = null;
                    int nextMetaPiece = 0;
                    int totalMetaPieces = 0;

                    for (int step = 0; step < 64 && !cancelToken.IsCancellationRequested; step++)
                    {
                        byte[] lb = ReadExactStream(ns, 4);
                        uint mLen = ReadBeUInt32(lb, 0);
                        if (mLen == 0) continue;
                        if (mLen > 2 * 1024 * 1024) return null;
                        byte[] body = ReadExactStream(ns, (int)mLen);
                        if (body[0] != 20 || body.Length < 2) continue;

                        byte extId = body[1];
                        if (extId == 0)
                        {
                            int pos = 2;
                            ShihabBencodeNode d = ShihabBencodeParser.Parse(body, ref pos);
                            if (d != null && d.DictValue != null)
                            {
                                ShihabBencodeNode mNode, szNode;
                                if (d.DictValue.TryGetValue("m", out mNode) && mNode != null && mNode.DictValue != null)
                                {
                                    ShihabBencodeNode utNode;
                                    if (mNode.DictValue.TryGetValue("ut_metadata", out utNode) && utNode != null)
                                        peerUtMetadataId = (int)utNode.IntValue;
                                }
                                if (d.DictValue.TryGetValue("metadata_size", out szNode) && szNode != null)
                                    metadataSize = (int)szNode.IntValue;
                            }
                            if (peerUtMetadataId > 0 && metadataSize > 0 && metadataSize <= 4 * 1024 * 1024)
                            {
                                assembled = new byte[metadataSize];
                                totalMetaPieces = (metadataSize + 16383) / 16384;
                                SendUtMetadataRequest(ns, (byte)peerUtMetadataId, 0);
                                nextMetaPiece = 1;
                            }
                        }
                        else if (extId == 2 && assembled != null)
                        {
                            int pos = 2;
                            ShihabBencodeNode hdr = ShihabBencodeParser.Parse(body, ref pos);
                            if (hdr != null && hdr.DictValue != null)
                            {
                                ShihabBencodeNode mtNode, pcNode;
                                if (hdr.DictValue.TryGetValue("msg_type", out mtNode) && mtNode != null && mtNode.IntValue == 1 &&
                                    hdr.DictValue.TryGetValue("piece", out pcNode) && pcNode != null)
                                {
                                    int p = (int)pcNode.IntValue;
                                    int dataBytes = body.Length - pos;
                                    long dstOffL = (long)p * 16384L;
                                    if (p >= 0 && dstOffL >= 0 && dataBytes > 0 && (dstOffL + dataBytes) <= assembled.Length)
                                    {
                                        Buffer.BlockCopy(body, pos, assembled, (int)dstOffL, dataBytes);
                                    }
                                    if (nextMetaPiece < totalMetaPieces)
                                    {
                                        SendUtMetadataRequest(ns, (byte)peerUtMetadataId, nextMetaPiece++);
                                    }
                                    else
                                    {
                                        return assembled;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            finally { try { sock.Close(); } catch { } }
            return null;
        }

        private static void SendUtMetadataRequest(Stream ns, byte peerUtId, int pieceIdx)
        {
            byte[] dict = Encoding.ASCII.GetBytes(string.Format(CultureInfo.InvariantCulture, "d8:msg_typei0e5:piecei{0}ee", pieceIdx));
            byte[] pkt = new byte[6 + dict.Length];
            WriteBeUInt32(pkt, 0, (uint)(2 + dict.Length));
            pkt[4] = 20;
            pkt[5] = peerUtId;
            Buffer.BlockCopy(dict, 0, pkt, 6, dict.Length);
            ns.Write(pkt, 0, pkt.Length);
        }

        private static byte[] BuildBitTorrentHandshake(byte[] infoHash20, byte[] peerId20, bool advertiseExtendedBep10)
        {
            byte[] hs = new byte[68];
            hs[0] = 19;
            byte[] proto = Encoding.ASCII.GetBytes("BitTorrent protocol");
            Buffer.BlockCopy(proto, 0, hs, 1, 19);
            if (advertiseExtendedBep10) hs[25] = 0x10;
            Buffer.BlockCopy(infoHash20, 0, hs, 28, 20);
            Buffer.BlockCopy(peerId20, 0, hs, 48, 20);
            return hs;
        }

        private static byte[] ReadExactStream(Stream s, int count)
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

        private static void WriteBeUInt32(byte[] buf, int off, uint v)
        {
            buf[off] = (byte)((v >> 24) & 0xFF);
            buf[off + 1] = (byte)((v >> 16) & 0xFF);
            buf[off + 2] = (byte)((v >> 8) & 0xFF);
            buf[off + 3] = (byte)(v & 0xFF);
        }

        private static void WriteBeUInt64(byte[] buf, int off, ulong v)
        {
            WriteBeUInt32(buf, off, (uint)(v >> 32));
            WriteBeUInt32(buf, off + 4, (uint)(v & 0xFFFFFFFFUL));
        }

        private static uint ReadBeUInt32(byte[] buf, int off)
        {
            return ((uint)buf[off] << 24) | ((uint)buf[off + 1] << 16) | ((uint)buf[off + 2] << 8) | buf[off + 3];
        }

        private static ulong ReadBeUInt64(byte[] buf, int off)
        {
            return ((ulong)ReadBeUInt32(buf, off) << 32) | ReadBeUInt32(buf, off + 4);
        }

        public static bool CreateTorrentFile(
            string sourceFileOrDir,
            string outputTorrentPath,
            List<string> trackers,
            List<string> webSeeds,
            bool isPrivate,
            int pieceLengthBytes,
            out string infoHashHex,
            out string errorMessage)
        {
            infoHashHex = "";
            errorMessage = "";
            try
            {
                CreateTorrentFile(sourceFileOrDir, outputTorrentPath, trackers, webSeeds, "Created by MHS Download Manager", pieceLengthBytes, isPrivate);
                ShihabTorrentMetadata meta = InspectTorrentFile(outputTorrentPath);
                infoHashHex = meta != null ? (meta.InfoHashHex ?? "") : "";
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static bool CreateTorrentFile(
            string sourceFileOrDir,
            string outputTorrentPath,
            List<string> trackers,
            List<string> webSeeds,
            string comment,
            int pieceLengthBytes,
            bool isPrivate,
            out string errorMessage)
        {
            errorMessage = "";
            try
            {
                CreateTorrentFile(sourceFileOrDir, outputTorrentPath, trackers, webSeeds, comment, pieceLengthBytes, isPrivate);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static void CreateTorrentFile(
            string sourceFileOrDir,
            string outputTorrentPath,
            List<string> trackers,
            List<string> webSeeds,
            string comment,
            int pieceLengthBytes,
            bool isPrivate)
        {
            if (pieceLengthBytes < 16384) pieceLengthBytes = 262144;
            if (pieceLengthBytes > 64 * 1024 * 1024) pieceLengthBytes = 16 * 1024 * 1024;
            string fullSrc = Path.GetFullPath(sourceFileOrDir).TrimEnd(Path.DirectorySeparatorChar);
            bool isDir = Directory.Exists(fullSrc);
            List<string> fullFiles = new List<string>();
            string torrentName = Path.GetFileName(fullSrc);

            if (isDir)
            {
                string[] all = Directory.GetFiles(fullSrc, "*", System.IO.SearchOption.AllDirectories);
                Array.Sort(all, StringComparer.OrdinalIgnoreCase);
                fullFiles.AddRange(all);
            }
            else if (File.Exists(fullSrc))
            {
                fullFiles.Add(fullSrc);
            }
            else
            {
                throw new FileNotFoundException("Source path for torrent creation was not found.", sourceFileOrDir);
            }

            MemoryStream piecesHashes = new MemoryStream();
            byte[] pieceBuf = new byte[pieceLengthBytes];
            int pieceFill = 0;
            using (SHA1 sha1 = SHA1.Create())
            {
                for (int i = 0; i < fullFiles.Count; i++)
                {
                    using (FileStream fs = new FileStream(fullFiles[i], FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        int read;
                        while ((read = fs.Read(pieceBuf, pieceFill, pieceLengthBytes - pieceFill)) > 0)
                        {
                            pieceFill += read;
                            if (pieceFill == pieceLengthBytes)
                            {
                                byte[] h = sha1.ComputeHash(pieceBuf, 0, pieceLengthBytes);
                                piecesHashes.Write(h, 0, 20);
                                pieceFill = 0;
                            }
                        }
                    }
                }
                if (pieceFill > 0)
                {
                    byte[] h = sha1.ComputeHash(pieceBuf, 0, pieceFill);
                    piecesHashes.Write(h, 0, 20);
                }
            }

            using (FileStream outFs = new FileStream(outputTorrentPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                ShihabBencodeParser.WriteAscii(outFs, "d");
                if (trackers != null && trackers.Count > 0 && !string.IsNullOrWhiteSpace(trackers[0]))
                {
                    ShihabBencodeParser.WriteBencodeKeyString(outFs, "announce", trackers[0].Trim());
                    ShihabBencodeParser.WriteBencodeString(outFs, "announce-list");
                    ShihabBencodeParser.WriteAscii(outFs, "l");
                    for (int i = 0; i < trackers.Count; i++)
                    {
                        if (string.IsNullOrWhiteSpace(trackers[i])) continue;
                        ShihabBencodeParser.WriteAscii(outFs, "l");
                        ShihabBencodeParser.WriteBencodeString(outFs, trackers[i].Trim());
                        ShihabBencodeParser.WriteAscii(outFs, "e");
                    }
                    ShihabBencodeParser.WriteAscii(outFs, "e");
                }
                if (!string.IsNullOrWhiteSpace(comment))
                {
                    ShihabBencodeParser.WriteBencodeKeyString(outFs, "comment", comment.Trim());
                }
                ShihabBencodeParser.WriteBencodeKeyString(outFs, "created by", "MHS Download Manager 1.0.6");
                long unixNow = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                ShihabBencodeParser.WriteBencodeString(outFs, "creation date");
                ShihabBencodeParser.WriteBencodeInt(outFs, unixNow);

                ShihabBencodeParser.WriteBencodeString(outFs, "info");
                ShihabBencodeParser.WriteAscii(outFs, "d");
                if (isDir)
                {
                    ShihabBencodeParser.WriteBencodeString(outFs, "files");
                    ShihabBencodeParser.WriteAscii(outFs, "l");
                    for (int i = 0; i < fullFiles.Count; i++)
                    {
                        FileInfo fi = new FileInfo(fullFiles[i]);
                        ShihabBencodeParser.WriteAscii(outFs, "d");
                        ShihabBencodeParser.WriteBencodeString(outFs, "length");
                        ShihabBencodeParser.WriteBencodeInt(outFs, fi.Length);
                        ShihabBencodeParser.WriteBencodeString(outFs, "path");
                        ShihabBencodeParser.WriteAscii(outFs, "l");
                        string rel = fullFiles[i].Substring(fullSrc.Length + 1);
                        string[] segs = rel.Split(new char[] { Path.DirectorySeparatorChar, '/' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int s = 0; s < segs.Length; s++)
                        {
                            ShihabBencodeParser.WriteBencodeString(outFs, segs[s]);
                        }
                        ShihabBencodeParser.WriteAscii(outFs, "ee");
                    }
                    ShihabBencodeParser.WriteAscii(outFs, "e");
                }
                else
                {
                    FileInfo fi = new FileInfo(fullFiles[0]);
                    ShihabBencodeParser.WriteBencodeString(outFs, "length");
                    ShihabBencodeParser.WriteBencodeInt(outFs, fi.Length);
                }

                ShihabBencodeParser.WriteBencodeKeyString(outFs, "name", torrentName);
                ShihabBencodeParser.WriteBencodeString(outFs, "piece length");
                ShihabBencodeParser.WriteBencodeInt(outFs, pieceLengthBytes);
                ShihabBencodeParser.WriteBencodeString(outFs, "pieces");
                byte[] allHashes = piecesHashes.ToArray();
                ShihabBencodeParser.WriteAscii(outFs, allHashes.Length.ToString(CultureInfo.InvariantCulture) + ":");
                outFs.Write(allHashes, 0, allHashes.Length);
                if (isPrivate)
                {
                    ShihabBencodeParser.WriteBencodeString(outFs, "private");
                    ShihabBencodeParser.WriteBencodeInt(outFs, 1);
                }
                ShihabBencodeParser.WriteAscii(outFs, "e");

                if (webSeeds != null && webSeeds.Count > 0)
                {
                    ShihabBencodeParser.WriteBencodeString(outFs, "url-list");
                    ShihabBencodeParser.WriteAscii(outFs, "l");
                    for (int i = 0; i < webSeeds.Count; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(webSeeds[i]))
                        {
                            ShihabBencodeParser.WriteBencodeString(outFs, webSeeds[i].Trim());
                        }
                    }
                    ShihabBencodeParser.WriteAscii(outFs, "e");
                }
                ShihabBencodeParser.WriteAscii(outFs, "e");
            }
        }
    }
}

