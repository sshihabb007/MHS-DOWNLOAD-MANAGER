using System;
using System.Collections.Concurrent;
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
        public byte[] RawInfoBytes { get; set; }
        public long TotalSizeBytes { get; set; }
        public long PieceLength { get; set; }
        public int PieceCount { get; set; }
        public byte[] PiecesSha1Concat { get; set; }
        public bool IsMultiFile { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsMagnetPendingMetadata { get; set; }
        public string Comment { get; set; }
        public string ErrorMessage { get; set; }
        public List<string> Trackers { get; set; }
        public List<List<string>> TrackerTiers { get; set; }
        public List<string> WebSeeds { get; set; }
        public List<string> DirectPeerHints { get; set; }
        public List<int> SelectOnlyIndices { get; set; }
        public List<ShihabTorrentFileEntry> Files { get; set; }

        public bool ParsedSuccessfully
        {
            get
            {
                if (!string.IsNullOrEmpty(ErrorMessage)) return false;
                if (InfoHashBytes == null || InfoHashBytes.Length != 20) return false;
                if (IsMagnetPendingMetadata) return true;
                return PieceCount > 0 && PieceLength > 0 && Files != null && Files.Count > 0;
            }
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
            RawInfoBytes = new byte[0];
            PiecesSha1Concat = new byte[0];
            ErrorMessage = "";
            Comment = "";
            Trackers = new List<string>();
            TrackerTiers = new List<List<string>>();
            WebSeeds = new List<string>();
            DirectPeerHints = new List<string>();
            SelectOnlyIndices = new List<int>();
            Files = new List<ShihabTorrentFileEntry>();
        }
    }

    public static class ShihabTorrentManager
    {
        private const int MaxTorrentDownloadBytes = 10 * 1024 * 1024;
        private const int MaxBlocklistBytes = 32 * 1024 * 1024;
        private const int MaxTrackerResponseBytes = 4 * 1024 * 1024;
        private const int MaxPieceLengthBytes = 64 * 1024 * 1024;
        private const int MinPieceLengthBytes = 16384;
        private const int MaxHttpRedirects = 5;

        private struct IpRangeEntry : IComparable<IpRangeEntry>
        {
            public uint Start;
            public uint End;
            public int CompareTo(IpRangeEntry other)
            {
                int cmp = Start.CompareTo(other.Start);
                return cmp != 0 ? cmp : End.CompareTo(other.End);
            }
        }

        private sealed class TrackerAnnounceResult
        {
            public List<IPEndPoint> Peers = new List<IPEndPoint>();
            public int IntervalSeconds = 1800;
            public int Seeders = -1;
            public int Leechers = -1;
            public string Status = "OK";
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

        internal static bool IsLocalTorrentTestAllowed()
        {
            string env = Environment.GetEnvironmentVariable("MHS_ALLOW_LOCAL_TORRENT_TEST");
            return string.Equals(env, "1", StringComparison.Ordinal) ||
                   string.Equals(env, "true", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsMagnetOrTorrentInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            string s = input.Trim().Trim('"');
            if (s.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase)) return true;
            Uri u;
            if (Uri.TryCreate(s, UriKind.Absolute, out u))
            {
                if (string.Equals(u.Scheme, "http", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(u.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                {
                    return u.AbsolutePath.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase);
                }
                if (u.IsFile)
                {
                    return u.LocalPath.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase);
                }
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

        private static string SafePercentDecode(string raw, bool plusAsSpace)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string s = plusAsSpace ? raw.Replace('+', ' ') : raw;
            List<byte> outBytes = new List<byte>(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '%' && i + 2 < s.Length)
                {
                    byte hexVal;
                    if (byte.TryParse(s.Substring(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hexVal))
                    {
                        outBytes.Add(hexVal);
                        i += 2;
                        continue;
                    }
                }
                if (s[i] <= 0x7F)
                {
                    outBytes.Add((byte)s[i]);
                }
                else
                {
                    byte[] utf = Encoding.UTF8.GetBytes(new char[] { s[i] });
                    outBytes.AddRange(utf);
                }
            }
            return Encoding.UTF8.GetString(outBytes.ToArray());
        }

        private static string NormalizeMagnetParamKey(string rawKey)
        {
            if (string.IsNullOrEmpty(rawKey)) return "";
            string k = rawKey.Trim().ToLowerInvariant();
            int dot = k.LastIndexOf('.');
            if (dot > 0 && dot < k.Length - 1)
            {
                bool allDigits = true;
                for (int i = dot + 1; i < k.Length; i++)
                {
                    if (k[i] < '0' || k[i] > '9') { allDigits = false; break; }
                }
                if (allDigits) return k.Substring(0, dot);
            }
            return k;
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
            if (string.IsNullOrWhiteSpace(magnetUri))
            {
                meta.ErrorMessage = "Empty magnet URI.";
                return meta;
            }
            string trimmed = magnetUri.Trim();
            if (!trimmed.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
            {
                meta.ErrorMessage = "Invalid magnet URI prefix.";
                return meta;
            }

            int q = trimmed.IndexOf('?');
            string query = q >= 0 ? trimmed.Substring(q + 1) : trimmed;
            string[] parts = query.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            bool sawV2Btmh = false;
            bool sawInvalidBtih = false;

            for (int i = 0; i < parts.Length; i++)
            {
                int eq = parts[i].IndexOf('=');
                if (eq <= 0) continue;
                string k = NormalizeMagnetParamKey(parts[i].Substring(0, eq));
                string rawVal = parts[i].Substring(eq + 1);
                string v = SafePercentDecode(rawVal, k == "dn");

                if (k == "dn" && !string.IsNullOrWhiteSpace(v))
                {
                    meta.Name = ShihabFormatHelpers.SanitizeShihabFileName(v);
                }
                else if (k == "xt")
                {
                    if (v.StartsWith("urn:btih:", StringComparison.OrdinalIgnoreCase))
                    {
                        byte[] ih = DecodeHexOrBase32Btih(v.Substring(9).Trim());
                        if (ih.Length == 20)
                        {
                            meta.InfoHashBytes = ih;
                            meta.InfoHashHex = BytesToHexLower(ih);
                        }
                        else
                        {
                            sawInvalidBtih = true;
                        }
                    }
                    else if (v.StartsWith("urn:btmh:", StringComparison.OrdinalIgnoreCase))
                    {
                        sawV2Btmh = true;
                    }
                }
                else if (k == "tr" && !string.IsNullOrWhiteSpace(v))
                {
                    string trClean = v.Trim();
                    if (!meta.Trackers.Contains(trClean))
                    {
                        meta.Trackers.Add(trClean);
                        meta.TrackerTiers.Add(new List<string> { trClean });
                    }
                }
                else if ((k == "ws" || k == "as") && !string.IsNullOrWhiteSpace(v))
                {
                    string wsClean = v.Trim();
                    if (!meta.WebSeeds.Contains(wsClean)) meta.WebSeeds.Add(wsClean);
                }
                else if (k == "x.pe" && !string.IsNullOrWhiteSpace(v))
                {
                    string peClean = v.Trim();
                    if (!meta.DirectPeerHints.Contains(peClean)) meta.DirectPeerHints.Add(peClean);
                }
                else if (k == "so" && !string.IsNullOrWhiteSpace(v))
                {
                    ParseBep53SelectOnly(v, meta.SelectOnlyIndices);
                }
                else if (k == "xl")
                {
                    long sz = 0;
                    if (long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out sz) && sz > 0)
                    {
                        meta.TotalSizeBytes = sz;
                    }
                }
            }

            if (meta.InfoHashBytes == null || meta.InfoHashBytes.Length != 20)
            {
                if (sawV2Btmh)
                {
                    meta.ErrorMessage = "BitTorrent v2-only magnets (urn:btmh) are not yet supported; please provide a v1 or hybrid magnet URI.";
                }
                else if (sawInvalidBtih)
                {
                    meta.ErrorMessage = "Malformed BitTorrent info-hash (btih must be 40-char hex or 32-char Base32).";
                }
                else
                {
                    meta.ErrorMessage = "Magnet URI is missing a valid xt=urn:btih info-hash.";
                }
                return meta;
            }

            meta.IsMagnetPendingMetadata = true;
            if (meta.Name == "torrent_download" && !string.IsNullOrEmpty(meta.InfoHashHex))
            {
                meta.Name = "magnet_" + meta.InfoHashHex.Substring(0, Math.Min(12, meta.InfoHashHex.Length));
            }
            return meta;
        }

        private static void ParseBep53SelectOnly(string spec, List<int> target)
        {
            if (string.IsNullOrWhiteSpace(spec) || target == null) return;
            string[] tokens = spec.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string tok = tokens[i].Trim();
                int dash = tok.IndexOf('-');
                if (dash > 0)
                {
                    int a, b;
                    if (int.TryParse(tok.Substring(0, dash), NumberStyles.None, CultureInfo.InvariantCulture, out a) &&
                        int.TryParse(tok.Substring(dash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out b) &&
                        a >= 0 && b >= a && (b - a) <= 10000)
                    {
                        for (int k = a; k <= b; k++)
                        {
                            if (!target.Contains(k)) target.Add(k);
                        }
                    }
                }
                else
                {
                    int idx;
                    if (int.TryParse(tok, NumberStyles.None, CultureInfo.InvariantCulture, out idx) && idx >= 0)
                    {
                        if (!target.Contains(idx)) target.Add(idx);
                    }
                }
            }
        }

        public static ShihabTorrentMetadata InspectTorrentOrMagnet(
            string input,
            MehediAppConfiguration cfg = null,
            string cookies = null,
            string referer = null,
            string userAgent = null)
        {
            if (string.IsNullOrWhiteSpace(input)) return new ShihabTorrentMetadata { ErrorMessage = "Empty torrent input." };
            string trimmed = input.Trim().Trim('"');
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
                try
                {
                    byte[] bytes;
                    string err;
                    if (!DownloadHttpBytesWithSafeRedirects(remoteUri, cfg, MaxTorrentDownloadBytes, 15000, cookies, referer, userAgent, out bytes, out err))
                    {
                        return new ShihabTorrentMetadata { ErrorMessage = err };
                    }
                    return InspectTorrentBytes(bytes, Path.GetFileNameWithoutExtension(remoteUri.AbsolutePath));
                }
                catch (Exception ex)
                {
                    return new ShihabTorrentMetadata { ErrorMessage = "Failed to download .torrent file: " + ex.Message };
                }
            }
            return new ShihabTorrentMetadata { ErrorMessage = "Torrent file not found: " + trimmed };
        }

        private static bool IsUriAllowedForTorrentHttp(Uri uri)
        {
            if (uri == null) return false;
            if (IsLocalTorrentTestAllowed() &&
                (string.Equals(uri.Host, "127.0.0.1", StringComparison.Ordinal) ||
                 string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(uri.Host, "::1", StringComparison.Ordinal)))
            {
                return true;
            }
            return !ShihabProcessAndPathSafety.IsHostDisallowedForOutboundSsrf(uri.Host, true);
        }

        private static bool DownloadHttpBytesWithSafeRedirects(
            Uri initialUri,
            MehediAppConfiguration cfg,
            int maxBytes,
            int timeoutMs,
            string cookies,
            string referer,
            string userAgent,
            out byte[] data,
            out string errorMessage)
        {
            data = new byte[0];
            errorMessage = "";
            Uri current = initialUri;
            ShihabAutoUpdater.EnforceModernTls();

            for (int hop = 0; hop <= MaxHttpRedirects; hop++)
            {
                if (!IsUriAllowedForTorrentHttp(current))
                {
                    errorMessage = "Blocked restricted/internal URL for BitTorrent HTTP request: " + current.Host;
                    return false;
                }

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(current);
                req.UserAgent = !string.IsNullOrWhiteSpace(userAgent)
                    ? userAgent
                    : ("MHS-Download-Manager/" + ShihabAutoUpdater.GetCurrentAppVersion());
                req.AllowAutoRedirect = false;
                req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                if (hop == 0 && !string.IsNullOrWhiteSpace(cookies)) req.Headers[HttpRequestHeader.Cookie] = cookies;
                if (hop == 0 && !string.IsNullOrWhiteSpace(referer)) req.Referer = referer;
                if (cfg != null) ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, current.AbsoluteUri);

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    int code = (int)resp.StatusCode;
                    if (code == 301 || code == 302 || code == 303 || code == 307 || code == 308)
                    {
                        string loc = resp.Headers["Location"];
                        Uri nextUri;
                        if (string.IsNullOrWhiteSpace(loc) || !Uri.TryCreate(current, loc, out nextUri) ||
                            (!string.Equals(nextUri.Scheme, "http", StringComparison.OrdinalIgnoreCase) &&
                             !string.Equals(nextUri.Scheme, "https", StringComparison.OrdinalIgnoreCase)))
                        {
                            errorMessage = "Invalid HTTP redirect target during BitTorrent fetch.";
                            return false;
                        }
                        current = nextUri;
                        continue;
                    }
                    if (code < 200 || code >= 300)
                    {
                        errorMessage = "HTTP " + code + " response from server.";
                        return false;
                    }
                    using (Stream s = resp.GetResponseStream())
                    {
                        data = ReadStreamBounded(s, maxBytes);
                        return true;
                    }
                }
            }
            errorMessage = "Exceeded maximum HTTP redirects (" + MaxHttpRedirects + ").";
            return false;
        }

        public static ShihabTorrentMetadata InspectTorrentFile(string torrentFilePath)
        {
            FileInfo fi = new FileInfo(torrentFilePath);
            if (!fi.Exists || fi.Length <= 0 || fi.Length > 32 * 1024 * 1024)
            {
                return new ShihabTorrentMetadata { ErrorMessage = "Torrent file is missing, empty, or exceeds 32 MB limit." };
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
                if (root == null || root.Kind != "dict" || root.DictValue == null)
                {
                    meta.ErrorMessage = "Invalid or truncated bencoded .torrent dictionary.";
                    return meta;
                }

                ShihabBencodeNode announceListNode;
                if (root.DictValue.TryGetValue("announce-list", out announceListNode) && announceListNode != null && announceListNode.ListValue != null)
                {
                    for (int i = 0; i < announceListNode.ListValue.Count; i++)
                    {
                        ShihabBencodeNode tier = announceListNode.ListValue[i];
                        if (tier != null && tier.ListValue != null)
                        {
                            List<string> tierUrls = new List<string>();
                            for (int j = 0; j < tier.ListValue.Count; j++)
                            {
                                if (tier.ListValue[j] == null) continue;
                                string tr = tier.ListValue[j].GetUtf8String().Trim();
                                if (!string.IsNullOrWhiteSpace(tr))
                                {
                                    if (!tierUrls.Contains(tr)) tierUrls.Add(tr);
                                    if (!meta.Trackers.Contains(tr)) meta.Trackers.Add(tr);
                                }
                            }
                            if (tierUrls.Count > 0) meta.TrackerTiers.Add(tierUrls);
                        }
                    }
                }
                ShihabBencodeNode announceNode;
                if (root.DictValue.TryGetValue("announce", out announceNode) && announceNode != null)
                {
                    string tr = announceNode.GetUtf8String().Trim();
                    if (!string.IsNullOrWhiteSpace(tr))
                    {
                        if (!meta.Trackers.Contains(tr)) meta.Trackers.Insert(0, tr);
                        if (meta.TrackerTiers.Count == 0) meta.TrackerTiers.Add(new List<string> { tr });
                    }
                }

                ShihabBencodeNode urlListNode;
                if (root.DictValue.TryGetValue("url-list", out urlListNode) && urlListNode != null)
                {
                    if (urlListNode.Kind == "bytes")
                    {
                        string ws = urlListNode.GetUtf8String().Trim();
                        if (!string.IsNullOrWhiteSpace(ws)) meta.WebSeeds.Add(ws);
                    }
                    else if (urlListNode.ListValue != null)
                    {
                        for (int i = 0; i < urlListNode.ListValue.Count; i++)
                        {
                            if (urlListNode.ListValue[i] == null) continue;
                            string ws = urlListNode.ListValue[i].GetUtf8String().Trim();
                            if (!string.IsNullOrWhiteSpace(ws) && !meta.WebSeeds.Contains(ws)) meta.WebSeeds.Add(ws);
                        }
                    }
                }

                ShihabBencodeNode commentNode;
                if (root.DictValue.TryGetValue("comment", out commentNode) && commentNode != null)
                {
                    meta.Comment = commentNode.GetUtf8String();
                }

                ShihabBencodeNode infoNode;
                if (!root.DictValue.TryGetValue("info", out infoNode) || infoNode == null || infoNode.Kind != "dict" || infoNode.DictValue == null)
                {
                    meta.ErrorMessage = "Missing required 'info' dictionary in .torrent file.";
                    return meta;
                }

                PopulateMetadataFromInfoNode(meta, raw, infoNode);
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
            if (infoLen <= 0 || infoNode.RawStartOffset < 0 || infoNode.RawEndOffset > rawContainer.Length)
            {
                meta.ErrorMessage = "Invalid 'info' byte bounds in .torrent metadata.";
                return;
            }

            byte[] rawInfo = new byte[infoLen];
            Buffer.BlockCopy(rawContainer, infoNode.RawStartOffset, rawInfo, 0, infoLen);
            meta.RawInfoBytes = rawInfo;

            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(rawInfo);
                meta.InfoHashBytes = hash;
                meta.InfoHashHex = BytesToHexLower(hash);
            }

            // Detect BEP-52 BitTorrent v2-only torrents cleanly instead of silently misparsing as 0-byte v1
            ShihabBencodeNode metaVerNode;
            bool hasMetaV2 = infoNode.DictValue.TryGetValue("meta version", out metaVerNode) && metaVerNode != null && metaVerNode.IntValue == 2;

            ShihabBencodeNode privNode;
            if (infoNode.DictValue.TryGetValue("private", out privNode) && privNode != null && privNode.Kind == "int")
            {
                meta.IsPrivate = privNode.IntValue != 0;
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
            if (!infoNode.DictValue.TryGetValue("piece length", out plNode) || plNode == null || plNode.Kind != "int")
            {
                meta.ErrorMessage = "Torrent metadata is missing valid 'piece length'.";
                return;
            }
            if (plNode.IntValue < MinPieceLengthBytes || plNode.IntValue > MaxPieceLengthBytes)
            {
                meta.ErrorMessage = "Unsupported torrent piece length (" + plNode.IntValue + " bytes; must be 16 KiB..64 MiB).";
                return;
            }
            meta.PieceLength = plNode.IntValue;

            ShihabBencodeNode piecesNode;
            if (!infoNode.DictValue.TryGetValue("pieces", out piecesNode) || piecesNode == null || piecesNode.Kind != "bytes" || piecesNode.BytesValue == null)
            {
                meta.ErrorMessage = hasMetaV2
                    ? "BitTorrent v2-only torrents (BEP-52 without v1 pieces) are not yet supported; please use a v1 or hybrid .torrent."
                    : "Torrent metadata is missing 'pieces' SHA-1 hash list.";
                return;
            }
            if (piecesNode.BytesValue.Length == 0 || (piecesNode.BytesValue.Length % 20) != 0)
            {
                meta.ErrorMessage = "Corrupt torrent 'pieces' hash buffer (length is not a non-zero multiple of 20).";
                return;
            }
            meta.PiecesSha1Concat = piecesNode.BytesValue;
            meta.PieceCount = piecesNode.BytesValue.Length / 20;

            meta.Files.Clear();
            ShihabBencodeNode filesNode;
            if (infoNode.DictValue.TryGetValue("files", out filesNode) && filesNode != null && filesNode.ListValue != null)
            {
                meta.IsMultiFile = true;
                long runningOffset = 0;
                HashSet<string> seenRelPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> seenDirPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < filesNode.ListValue.Count; i++)
                {
                    ShihabBencodeNode fDict = filesNode.ListValue[i];
                    if (fDict == null || fDict.DictValue == null)
                    {
                        meta.ErrorMessage = "Malformed file entry in multi-file torrent.";
                        return;
                    }
                    ShihabBencodeNode lenNode;
                    if (!fDict.DictValue.TryGetValue("length", out lenNode) || lenNode == null || lenNode.Kind != "int" || lenNode.IntValue < 0)
                    {
                        meta.ErrorMessage = "Invalid negative or missing file length in torrent metadata.";
                        return;
                    }
                    long fLen = lenNode.IntValue;

                    string attrStr = "";
                    ShihabBencodeNode attrNode;
                    if (fDict.DictValue.TryGetValue("attr", out attrNode) && attrNode != null)
                    {
                        attrStr = attrNode.GetUtf8String();
                    }

                    ShihabBencodeNode pathNode;
                    if (!fDict.DictValue.TryGetValue("path.utf-8", out pathNode) || pathNode == null)
                    {
                        fDict.DictValue.TryGetValue("path", out pathNode);
                    }
                    bool isPad = attrStr.IndexOf('p') >= 0;
                    if (pathNode == null || pathNode.ListValue == null || pathNode.ListValue.Count == 0)
                    {
                        meta.ErrorMessage = "Torrent file entry is missing a valid path list.";
                        return;
                    }

                    List<string> rawSegs = new List<string>(pathNode.ListValue.Count);
                    for (int p = 0; p < pathNode.ListValue.Count; p++)
                    {
                        if (pathNode.ListValue[p] == null || pathNode.ListValue[p].Kind != "bytes")
                        {
                            meta.ErrorMessage = "Malformed path component in torrent metadata.";
                            return;
                        }
                        string seg = pathNode.ListValue[p].GetUtf8String();
                        if (p == 0 && seg == ".pad") isPad = true;
                        rawSegs.Add(seg);
                    }

                    string normalizedRel = "";
                    if (!isPad)
                    {
                        string joinedRaw = string.Join("/", rawSegs.ToArray());
                        if (!ShihabProcessAndPathSafety.TryValidateRelativePathSegments(joinedRaw, out normalizedRel))
                        {
                            meta.ErrorMessage = "Blocked unsafe or reserved path in torrent metadata: " + joinedRaw;
                            return;
                        }
                        if (!seenRelPaths.Add(normalizedRel))
                        {
                            meta.ErrorMessage = "Duplicate or case-colliding file path in torrent metadata: " + normalizedRel;
                            return;
                        }
                        if (seenDirPrefixes.Contains(normalizedRel))
                        {
                            meta.ErrorMessage = "File-vs-directory collision in torrent metadata: " + normalizedRel;
                            return;
                        }
                        string[] normParts = normalizedRel.Split(Path.DirectorySeparatorChar);
                        string prefixAcc = "";
                        for (int d = 0; d < normParts.Length - 1; d++)
                        {
                            prefixAcc = d == 0 ? normParts[0] : (prefixAcc + Path.DirectorySeparatorChar + normParts[d]);
                            if (seenRelPaths.Contains(prefixAcc))
                            {
                                meta.ErrorMessage = "Directory-vs-file collision in torrent metadata: " + prefixAcc;
                                return;
                            }
                            seenDirPrefixes.Add(prefixAcc);
                        }
                    }
                    else
                    {
                        normalizedRel = ".pad_" + i;
                    }

                    if (long.MaxValue - runningOffset < fLen)
                    {
                        meta.ErrorMessage = "Torrent total file size exceeds 64-bit bounds.";
                        return;
                    }

                    meta.Files.Add(new ShihabTorrentFileEntry
                    {
                        Index = i,
                        RelativePath = normalizedRel,
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
                if (!infoNode.DictValue.TryGetValue("length", out lenNode) || lenNode == null || lenNode.Kind != "int" || lenNode.IntValue < 0)
                {
                    meta.ErrorMessage = "Single-file torrent is missing a valid non-negative 'length'.";
                    return;
                }
                long singleLen = lenNode.IntValue;
                string safeSingleName = ShihabProcessAndPathSafety.SanitizeFileName(meta.Name, "torrent_download");
                string validatedSingle;
                if (!ShihabProcessAndPathSafety.TryValidateRelativePathSegments(safeSingleName, out validatedSingle))
                {
                    validatedSingle = "torrent_download";
                }
                meta.TotalSizeBytes = singleLen;
                meta.Files.Add(new ShihabTorrentFileEntry
                {
                    Index = 0,
                    RelativePath = validatedSingle,
                    SizeBytes = singleLen,
                    ByteOffsetInTorrent = 0,
                    IsPaddingFile = false,
                    Selected = true
                });
            }

            long expectedPieces = meta.TotalSizeBytes > 0
                ? ((meta.TotalSizeBytes + meta.PieceLength - 1L) / meta.PieceLength)
                : 0L;
            if (expectedPieces != meta.PieceCount)
            {
                meta.ErrorMessage = string.Format(CultureInfo.InvariantCulture,
                    "Torrent piece count mismatch (expected {0} pieces for {1} bytes, got {2}).",
                    expectedPieces, meta.TotalSizeBytes, meta.PieceCount);
                return;
            }
            meta.IsMagnetPendingMetadata = false;
        }

        public static void EnsurePeerBlocklistLoaded(MehediAppConfiguration cfg, string baseDataDir)
        {
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.ShihabTorrentBlocklistUrl)) return;
            string src = cfg.ShihabTorrentBlocklistUrl.Trim();
            lock (_blocklistLock)
            {
                if (string.Equals(_loadedBlocklistSource, src, StringComparison.OrdinalIgnoreCase) && _blockedRanges.Count > 0) return;
            }

            try
            {
                byte[] rawBytes = null;
                if (File.Exists(src))
                {
                    FileInfo fi = new FileInfo(src);
                    if (fi.Length <= MaxBlocklistBytes)
                    {
                        rawBytes = File.ReadAllBytes(src);
                    }
                }
                else
                {
                    Uri srcUri;
                    if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(src, false, out srcUri) || !IsUriAllowedForTorrentHttp(srcUri))
                    {
                        return;
                    }
                    string urlHash;
                    using (SHA256 sha = SHA256.Create())
                    {
                        urlHash = BytesToHexLower(sha.ComputeHash(Encoding.UTF8.GetBytes(src.ToLowerInvariant()))).Substring(0, 12);
                    }
                    string cacheFile = Path.Combine(baseDataDir ?? Path.GetTempPath(), "bt_blocklist_" + urlHash + ".dat");
                    if (!File.Exists(cacheFile) || (DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFile)).TotalHours > 24)
                    {
                        string dlErr;
                        if (!DownloadHttpBytesWithSafeRedirects(srcUri, cfg, MaxBlocklistBytes, 12000, null, null, null, out rawBytes, out dlErr))
                        {
                            return;
                        }
                        try { File.WriteAllBytes(cacheFile, rawBytes); } catch { }
                    }
                    else
                    {
                        rawBytes = File.ReadAllBytes(cacheFile);
                    }
                }

                if (rawBytes == null || rawBytes.Length == 0) return;
                // Detect gzip via magic bytes 0x1F 0x8B on both local and remote files
                if (rawBytes.Length >= 2 && rawBytes[0] == 0x1F && rawBytes[1] == 0x8B)
                {
                    using (MemoryStream ms = new MemoryStream(rawBytes))
                    using (GZipStream gz = new GZipStream(ms, CompressionMode.Decompress))
                    {
                        rawBytes = ReadStreamBounded(gz, MaxBlocklistBytes);
                    }
                }

                string rawText = Encoding.UTF8.GetString(rawBytes);
                List<IpRangeEntry> parsed = new List<IpRangeEntry>();
                string[] lines = rawText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i++)
                {
                    string ln = lines[i].Trim();
                    if (ln.Length == 0 || ln[0] == '#' || ln.StartsWith("//", StringComparison.Ordinal)) continue;
                    // Support P2P ("desc:a.b.c.d-e.f.g.h"), eMule ("a.b.c.d - e.f.g.h , 000 , desc"), and CIDR
                    string rangeSpec = ln;
                    int comma = ln.IndexOf(',');
                    if (comma > 0 && ln.IndexOf('-') > 0 && ln.IndexOf('-') < comma)
                    {
                        rangeSpec = ln.Substring(0, comma).Trim();
                    }
                    else
                    {
                        int colon = ln.LastIndexOf(':');
                        if (colon >= 0) rangeSpec = ln.Substring(colon + 1).Trim();
                    }

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

                // Sort AND merge overlapping/adjacent intervals so binary search has zero false negatives
                parsed.Sort();
                List<IpRangeEntry> merged = new List<IpRangeEntry>(parsed.Count);
                for (int i = 0; i < parsed.Count; i++)
                {
                    IpRangeEntry cur = parsed[i];
                    if (merged.Count == 0)
                    {
                        merged.Add(cur);
                    }
                    else
                    {
                        IpRangeEntry last = merged[merged.Count - 1];
                        if ((ulong)cur.Start <= (ulong)last.End + 1UL)
                        {
                            if (cur.End > last.End)
                            {
                                last.End = cur.End;
                                merged[merged.Count - 1] = last;
                            }
                        }
                        else
                        {
                            merged.Add(cur);
                        }
                    }
                }

                lock (_blocklistLock)
                {
                    _blockedRanges = merged;
                    _loadedBlocklistSource = src;
                }
            }
            catch { }
        }

        private static bool TryParseIpv4Uint(string text, out uint val)
        {
            val = 0;
            string clean = (text ?? "").Trim();
            // eMule zero-padded octets like "001.002.003.004"
            string[] octs = clean.Split('.');
            if (octs.Length == 4)
            {
                uint acc = 0;
                bool ok = true;
                for (int i = 0; i < 4; i++)
                {
                    int part;
                    if (!int.TryParse(octs[i].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out part) || part < 0 || part > 255)
                    {
                        ok = false;
                        break;
                    }
                    acc = (acc << 8) | (uint)part;
                }
                if (ok) { val = acc; return true; }
            }
            IPAddress ip;
            if (!IPAddress.TryParse(clean, out ip) || ip.AddressFamily != AddressFamily.InterNetwork) return false;
            byte[] b = ip.GetAddressBytes();
            val = ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
            return true;
        }

        public static bool IsPeerBlocked(IPAddress ip)
        {
            if (ip == null) return false;
            IPAddress checkIp = ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;
            if (checkIp.AddressFamily != AddressFamily.InterNetwork) return false;
            byte[] b = checkIp.GetAddressBytes();
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

        private static bool IsPeerAddressAllowed(IPAddress ip, bool allowLocalTest)
        {
            if (ip == null) return false;
            if (IsPeerBlocked(ip)) return false;
            if (allowLocalTest && IPAddress.IsLoopback(ip)) return true;
            return !ShihabProcessAndPathSafety.IsDisallowedInternalIpAddress(ip);
        }

        public static List<IPEndPoint> DiscoverPeersFromTrackers(
            ShihabTorrentMetadata meta,
            byte[] peerId20,
            int listenPort,
            MehediAppConfiguration cfg)
        {
            List<ShihabTorrentTrackerTelemetry> ignoredTelemetry;
            return AnnounceAllTrackers(meta, peerId20, listenPort, 0, 0, meta != null ? Math.Max(0, meta.TotalSizeBytes) : 0, "started", cfg, CancellationToken.None, out ignoredTelemetry);
        }

        private static List<string> BuildEffectiveTrackerList(ShihabTorrentMetadata meta, bool allowLocalTest)
        {
            List<string> trackers = new List<string>();
            if (meta == null) return trackers;
            if (meta.TrackerTiers != null && meta.TrackerTiers.Count > 0)
            {
                for (int t = 0; t < meta.TrackerTiers.Count; t++)
                {
                    List<string> tier = meta.TrackerTiers[t];
                    if (tier == null) continue;
                    for (int j = 0; j < tier.Count; j++)
                    {
                        string tr = tier[j];
                        if (!string.IsNullOrWhiteSpace(tr) && !trackers.Contains(tr)) trackers.Add(tr);
                    }
                }
            }
            for (int i = 0; i < meta.Trackers.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(meta.Trackers[i]) && !trackers.Contains(meta.Trackers[i]))
                {
                    trackers.Add(meta.Trackers[i]);
                }
            }
            // BEP-27 strict compliance: NEVER add public trackers to private torrents!
            // Also avoid adding public internet trackers during isolated local tests.
            if (!meta.IsPrivate && !allowLocalTest && trackers.Count == 0)
            {
                for (int d = 0; d < DefaultPublicTrackers.Length; d++)
                {
                    if (!trackers.Contains(DefaultPublicTrackers[d])) trackers.Add(DefaultPublicTrackers[d]);
                }
            }
            return trackers;
        }

        private static List<IPEndPoint> AnnounceAllTrackers(
            ShihabTorrentMetadata meta,
            byte[] peerId20,
            int listenPort,
            long uploaded,
            long downloaded,
            long leftBytes,
            string eventName,
            MehediAppConfiguration cfg,
            CancellationToken cancelToken,
            out List<ShihabTorrentTrackerTelemetry> telemetryList)
        {
            List<ShihabTorrentTrackerTelemetry> localTelemetry = new List<ShihabTorrentTrackerTelemetry>();
            telemetryList = localTelemetry;
            List<IPEndPoint> peers = new List<IPEndPoint>();
            if (meta == null || meta.InfoHashBytes == null || meta.InfoHashBytes.Length != 20) return peers;

            bool allowLocalTest = IsLocalTorrentTestAllowed();
            List<string> trackers = BuildEffectiveTrackerList(meta, allowLocalTest);
            if (trackers.Count == 0) return peers;

            object listLock = new object();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<Task> tasks = new List<Task>();

            for (int i = 0; i < Math.Min(trackers.Count, 16); i++)
            {
                string tr = trackers[i];
                tasks.Add(Task.Run(delegate
                {
                    if (cancelToken.IsCancellationRequested) return;
                    TrackerAnnounceResult res = null;
                    try
                    {
                        if (tr.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            tr.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            res = AnnounceHttpTracker(tr, meta.InfoHashBytes, peerId20, listenPort, uploaded, downloaded, leftBytes, eventName, cfg, allowLocalTest, cancelToken);
                        }
                        else if (tr.StartsWith("udp://", StringComparison.OrdinalIgnoreCase))
                        {
                            res = AnnounceUdpTracker(tr, meta.InfoHashBytes, peerId20, listenPort, uploaded, downloaded, leftBytes, eventName, cfg, allowLocalTest, cancelToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        res = new TrackerAnnounceResult { Status = "Error: " + ex.Message };
                    }
                    if (res == null) return;
                    lock (listLock)
                    {
                        localTelemetry.Add(new ShihabTorrentTrackerTelemetry
                        {
                            Url = tr,
                            Status = res.Status,
                            PeersReturned = res.Peers.Count,
                            Seeders = res.Seeders,
                            Leechers = res.Leechers,
                            LastAnnounceUtc = DateTime.UtcNow
                        });
                        for (int p = 0; p < res.Peers.Count; p++)
                        {
                            IPEndPoint ep = res.Peers[p];
                            if (ep == null || ep.Port <= 0 || ep.Port > 65535) continue;
                            if (!IsPeerAddressAllowed(ep.Address, allowLocalTest)) continue;
                            if (seen.Add(ep.ToString())) peers.Add(ep);
                        }
                    }
                }));
            }

            try { Task.WaitAll(tasks.ToArray(), Math.Min(10000, Math.Max(3000, tasks.Count * 1500))); } catch { }
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

        private static TrackerAnnounceResult AnnounceHttpTracker(
            string trackerUrl,
            byte[] infoHash20,
            byte[] peerId20,
            int port,
            long uploaded,
            long downloaded,
            long leftBytes,
            string eventName,
            MehediAppConfiguration cfg,
            bool allowLocalTest,
            CancellationToken cancelToken)
        {
            TrackerAnnounceResult res = new TrackerAnnounceResult();
            Uri trUri;
            if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(trackerUrl, false, out trUri) || !IsUriAllowedForTorrentHttp(trUri))
            {
                res.Status = "Blocked (SSRF)";
                return res;
            }
            string sep = trUri.AbsoluteUri.IndexOf('?') >= 0 ? "&" : "?";
            string evPart = !string.IsNullOrEmpty(eventName) ? ("&event=" + eventName) : "";
            string full = string.Format(CultureInfo.InvariantCulture,
                "{0}{1}info_hash={2}&peer_id={3}&port={4}&uploaded={5}&downloaded={6}&left={7}&compact=1&numwant=80{8}",
                trUri.AbsoluteUri, sep, UrlEncodeRaw20Bytes(infoHash20), UrlEncodeRaw20Bytes(peerId20),
                port, uploaded, downloaded, Math.Max(0L, leftBytes), evPart);

            Uri reqUri;
            if (!Uri.TryCreate(full, UriKind.Absolute, out reqUri)) { res.Status = "Invalid URL"; return res; }

            byte[] raw;
            string dlErr;
            if (!DownloadHttpBytesWithSafeRedirects(reqUri, cfg, MaxTrackerResponseBytes, 6000, null, null, null, out raw, out dlErr))
            {
                res.Status = dlErr;
                return res;
            }
            if (cancelToken.IsCancellationRequested) return res;

            int idx = 0;
            ShihabBencodeNode root = ShihabBencodeParser.Parse(raw, ref idx);
            if (root == null || root.DictValue == null)
            {
                res.Status = "Invalid bencode";
                return res;
            }

            ShihabBencodeNode failNode;
            if (root.DictValue.TryGetValue("failure reason", out failNode) && failNode != null && !string.IsNullOrWhiteSpace(failNode.GetUtf8String()))
            {
                res.Status = "Tracker error: " + failNode.GetUtf8String();
                return res;
            }

            ShihabBencodeNode intNode, compNode, incompNode;
            if (root.DictValue.TryGetValue("interval", out intNode) && intNode != null && intNode.IntValue >= 15)
                res.IntervalSeconds = (int)Math.Min(3600, intNode.IntValue);
            if (root.DictValue.TryGetValue("complete", out compNode) && compNode != null) res.Seeders = (int)compNode.IntValue;
            if (root.DictValue.TryGetValue("incomplete", out incompNode) && incompNode != null) res.Leechers = (int)incompNode.IntValue;

            ShihabBencodeNode peersNode;
            if (root.DictValue.TryGetValue("peers", out peersNode) && peersNode != null)
            {
                if (peersNode.Kind == "bytes" && peersNode.BytesValue != null)
                {
                    ParseCompactIpv4Peers(peersNode.BytesValue, res.Peers, allowLocalTest);
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
                            ipN != null && ptN != null && IPAddress.TryParse(ipN.GetUtf8String(), out ip) &&
                            ptN.IntValue > 0 && ptN.IntValue <= 65535 && IsPeerAddressAllowed(ip, allowLocalTest))
                        {
                            res.Peers.Add(new IPEndPoint(ip, (int)ptN.IntValue));
                        }
                    }
                }
            }

            // BEP-7 IPv6 compact peers (18 bytes each: 16-byte IPv6 + 2-byte big-endian port)
            ShihabBencodeNode peers6Node;
            if (root.DictValue.TryGetValue("peers6", out peers6Node) && peers6Node != null && peers6Node.Kind == "bytes" && peers6Node.BytesValue != null)
            {
                ParseCompactIpv6Peers(peers6Node.BytesValue, res.Peers, allowLocalTest);
            }
            return res;
        }

        private static void ParseCompactIpv4Peers(byte[] pb, List<IPEndPoint> dest, bool allowLocalTest)
        {
            if (pb == null || dest == null) return;
            for (int i = 0; i + 6 <= pb.Length; i += 6)
            {
                IPAddress ip = new IPAddress(new byte[] { pb[i], pb[i + 1], pb[i + 2], pb[i + 3] });
                int pPort = (pb[i + 4] << 8) | pb[i + 5];
                if (pPort > 0 && pPort <= 65535 && IsPeerAddressAllowed(ip, allowLocalTest))
                {
                    dest.Add(new IPEndPoint(ip, pPort));
                }
            }
        }

        private static void ParseCompactIpv6Peers(byte[] pb, List<IPEndPoint> dest, bool allowLocalTest)
        {
            if (pb == null || dest == null) return;
            for (int i = 0; i + 18 <= pb.Length; i += 18)
            {
                byte[] addr = new byte[16];
                Buffer.BlockCopy(pb, i, addr, 0, 16);
                IPAddress ip = new IPAddress(addr);
                int pPort = (pb[i + 16] << 8) | pb[i + 17];
                if (pPort > 0 && pPort <= 65535 && IsPeerAddressAllowed(ip, allowLocalTest))
                {
                    dest.Add(new IPEndPoint(ip, pPort));
                }
            }
        }

        private static TrackerAnnounceResult AnnounceUdpTracker(
            string udpUrl,
            byte[] infoHash20,
            byte[] peerId20,
            int port,
            long uploaded,
            long downloaded,
            long leftBytes,
            string eventName,
            MehediAppConfiguration cfg,
            bool allowLocalTest,
            CancellationToken cancelToken)
        {
            TrackerAnnounceResult res = new TrackerAnnounceResult();
            // Fail-closed under SOCKS5 proxy: never leak direct UDP tracker packets
            if (ShihabTorrentSocketDialer.IsSocksProxyConfigured(cfg))
            {
                res.Status = "Skipped (SOCKS5 proxy active)";
                return res;
            }

            bool hasVpnBind;
            IPAddress boundIp = ShihabTorrentSocketDialer.ResolveBoundLocalIpOrFail(cfg, out hasVpnBind);
            if (hasVpnBind && boundIp == null)
            {
                res.Status = "Blocked (VPN adapter offline)";
                return res;
            }

            Uri u;
            if (!Uri.TryCreate(udpUrl, UriKind.Absolute, out u)) { res.Status = "Invalid UDP URL"; return res; }
            int trPort = u.Port > 0 ? u.Port : 80;
            bool isLoopbackHost = string.Equals(u.Host, "127.0.0.1", StringComparison.Ordinal) ||
                                  string.Equals(u.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(u.Host, "::1", StringComparison.Ordinal);
            if (!(allowLocalTest && isLoopbackHost) && ShihabProcessAndPathSafety.IsHostDisallowedForOutboundSsrf(u.Host, true))
            {
                res.Status = "Blocked (SSRF)";
                return res;
            }

            IPAddress targetIp = null;
            IPAddress parsedLit;
            if (IPAddress.TryParse(u.Host.Trim('[', ']'), out parsedLit))
            {
                if (!hasVpnBind || parsedLit.AddressFamily == boundIp.AddressFamily)
                {
                    targetIp = parsedLit;
                }
            }
            else
            {
                IPAddress[] addrs = Dns.GetHostAddresses(u.Host);
                for (int i = 0; i < addrs.Length; i++)
                {
                    if (hasVpnBind && addrs[i].AddressFamily != boundIp.AddressFamily) continue;
                    if ((allowLocalTest && IPAddress.IsLoopback(addrs[i])) || !ShihabProcessAndPathSafety.IsDisallowedInternalIpAddress(addrs[i]))
                    {
                        targetIp = addrs[i];
                        break;
                    }
                }
            }
            if (targetIp == null) { res.Status = "DNS failed"; return res; }

            uint tx1 = GenerateRandomUInt32();
            uint tx2 = GenerateRandomUInt32();
            uint keyVal = GenerateRandomUInt32();
            uint evCode = 0u;
            if (eventName == "completed") evCode = 1u;
            else if (eventName == "started") evCode = 2u;
            else if (eventName == "stopped") evCode = 3u;

            UdpClient udp = null;
            try
            {
                if (hasVpnBind && boundIp != null)
                {
                    udp = new UdpClient(new IPEndPoint(boundIp, 0));
                }
                else
                {
                    udp = new UdpClient(targetIp.AddressFamily);
                }
                udp.Client.ReceiveTimeout = 3500;
                udp.Client.SendTimeout = 3500;
                IPEndPoint remote = new IPEndPoint(targetIp, trPort);

                using (cancelToken.Register(delegate { try { udp.Close(); } catch { } }))
                {
                    byte[] connReq = new byte[16];
                    WriteBeUInt64(connReq, 0, 0x0000041727101980UL);
                    WriteBeUInt32Public(connReq, 8, 0u);
                    WriteBeUInt32Public(connReq, 12, tx1);
                    udp.Send(connReq, connReq.Length, remote);

                    IPEndPoint fromEp = new IPEndPoint(targetIp.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
                    byte[] connResp = udp.Receive(ref fromEp);
                    if (connResp == null || connResp.Length < 16 || !fromEp.Equals(remote)) { res.Status = "Bad connect reply"; return res; }
                    uint cAction = ReadBeUInt32Public(connResp, 0);
                    if (cAction == 3u && connResp.Length > 8)
                    {
                        res.Status = "UDP error: " + Encoding.UTF8.GetString(connResp, 8, connResp.Length - 8).Trim('\0');
                        return res;
                    }
                    if (cAction != 0u || ReadBeUInt32Public(connResp, 4) != tx1) { res.Status = "Tx mismatch"; return res; }
                    ulong connectionId = ReadBeUInt64(connResp, 8);

                    byte[] annReq = new byte[98];
                    WriteBeUInt64(annReq, 0, connectionId);
                    WriteBeUInt32Public(annReq, 8, 1u);
                    WriteBeUInt32Public(annReq, 12, tx2);
                    Buffer.BlockCopy(infoHash20, 0, annReq, 16, 20);
                    Buffer.BlockCopy(peerId20, 0, annReq, 36, 20);
                    WriteBeUInt64(annReq, 56, (ulong)Math.Max(0L, downloaded));
                    WriteBeUInt64(annReq, 64, (ulong)Math.Max(0L, leftBytes));
                    WriteBeUInt64(annReq, 72, (ulong)Math.Max(0L, uploaded));
                    WriteBeUInt32Public(annReq, 80, evCode);
                    WriteBeUInt32Public(annReq, 84, 0u);
                    WriteBeUInt32Public(annReq, 88, keyVal);
                    WriteBeUInt32Public(annReq, 92, 80u);
                    annReq[96] = (byte)((port >> 8) & 0xFF);
                    annReq[97] = (byte)(port & 0xFF);

                    udp.Send(annReq, annReq.Length, remote);
                    byte[] annResp = udp.Receive(ref fromEp);
                    if (annResp == null || annResp.Length < 20 || !fromEp.Equals(remote)) { res.Status = "Bad announce reply"; return res; }
                    uint aAction = ReadBeUInt32Public(annResp, 0);
                    if (aAction == 3u && annResp.Length > 8)
                    {
                        res.Status = "UDP error: " + Encoding.UTF8.GetString(annResp, 8, annResp.Length - 8).Trim('\0');
                        return res;
                    }
                    if (aAction != 1u || ReadBeUInt32Public(annResp, 4) != tx2) { res.Status = "Announce tx mismatch"; return res; }

                    res.IntervalSeconds = (int)Math.Max(15u, Math.Min(3600u, ReadBeUInt32Public(annResp, 8)));
                    res.Leechers = (int)ReadBeUInt32Public(annResp, 12);
                    res.Seeders = (int)ReadBeUInt32Public(annResp, 16);

                    if (targetIp.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        byte[] slice = new byte[annResp.Length - 20];
                        Buffer.BlockCopy(annResp, 20, slice, 0, slice.Length);
                        ParseCompactIpv6Peers(slice, res.Peers, allowLocalTest);
                    }
                    else
                    {
                        byte[] slice = new byte[annResp.Length - 20];
                        Buffer.BlockCopy(annResp, 20, slice, 0, slice.Length);
                        ParseCompactIpv4Peers(slice, res.Peers, allowLocalTest);
                    }
                }
            }
            finally
            {
                if (udp != null) { try { udp.Close(); } catch { } }
            }
            return res;
        }

        private static uint GenerateRandomUInt32()
        {
            byte[] b = new byte[4];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(b);
            }
            return ReadBeUInt32Public(b, 0);
        }

        public static bool TryResolveMagnetMetadataParallel(
            ShihabTorrentMetadata meta,
            MehediAppConfiguration cfg,
            string baseDataDir,
            CancellationToken cancelToken,
            Action<string> onStatusText = null)
        {
            if (meta == null || meta.InfoHashBytes == null || meta.InfoHashBytes.Length != 20) return false;
            if (meta.PieceCount > 0 && !meta.IsMagnetPendingMetadata) return true;

            EnsurePeerBlocklistLoaded(cfg, baseDataDir);
            byte[] peerId = Encoding.ASCII.GetBytes("-MH1070-" + Guid.NewGuid().ToString("N").Substring(0, 12));
            bool allowLocalTest = IsLocalTorrentTestAllowed();

            ConcurrentQueue<IPEndPoint> queue = new ConcurrentQueue<IPEndPoint>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            object seenLock = new object();

            Action<IPEndPoint> enqueuePeer = delegate(IPEndPoint ep)
            {
                if (ep == null || ep.Port <= 0 || ep.Port > 65535) return;
                if (!IsPeerAddressAllowed(ep.Address, allowLocalTest)) return;
                lock (seenLock)
                {
                    if (seen.Add(ep.ToString())) queue.Enqueue(ep);
                }
            };

            // 1. Direct x.pe peer hints first (instant)
            for (int i = 0; i < meta.DirectPeerHints.Count; i++)
            {
                IPEndPoint hintEp;
                if (ShihabTorrentDhtClient.TryParseHostPortEndPoint(meta.DirectPeerHints[i], out hintEp))
                {
                    enqueuePeer(hintEp);
                }
            }

            using (CancellationTokenSource raceCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken))
            {
                CancellationToken raceToken = raceCts.Token;

                // 2. Trackers + BEP-5 DHT in parallel
                Task trackerTask = Task.Run(delegate
                {
                    List<ShihabTorrentTrackerTelemetry> tStats;
                    List<IPEndPoint> tPeers = AnnounceAllTrackers(meta, peerId, 0, 0, 0, 0, "started", cfg, raceToken, out tStats);
                    for (int i = 0; i < tPeers.Count; i++) enqueuePeer(tPeers[i]);
                });

                Task dhtTask = Task.Run(delegate
                {
                    if (!meta.IsPrivate)
                    {
                        ShihabTorrentDhtClient.QueryPeersViaDht(meta.InfoHashBytes, 0, cfg, allowLocalTest, enqueuePeer, raceToken);
                    }
                });

                byte[] wonInfoBytes = null;
                object winLock = new object();
                int activeFetchers = 0;

                List<Task> fetchers = new List<Task>();
                for (int w = 0; w < 8; w++)
                {
                    fetchers.Add(Task.Run(delegate
                    {
                        Stopwatch totalSw = Stopwatch.StartNew();
                        while (!raceToken.IsCancellationRequested && totalSw.ElapsedMilliseconds < 25000)
                        {
                            IPEndPoint ep;
                            if (!queue.TryDequeue(out ep))
                            {
                                if (trackerTask.IsCompleted && dhtTask.IsCompleted && Volatile.Read(ref activeFetchers) == 0)
                                {
                                    break;
                                }
                                Thread.Sleep(80);
                                continue;
                            }
                            Interlocked.Increment(ref activeFetchers);
                            try
                            {
                                if (onStatusText != null)
                                {
                                    onStatusText(string.Format(CultureInfo.InvariantCulture, "Fetching magnet metadata ({0} peers seen)...", seen.Count));
                                }
                                byte[] candidate = TryFetchMagnetMetadataFromPeer(ep, meta.InfoHashBytes, peerId, cfg, raceToken);
                                if (candidate != null && candidate.Length > 0)
                                {
                                    using (SHA1 sha1 = SHA1.Create())
                                    {
                                        if (BytesToHexLower(sha1.ComputeHash(candidate)) == meta.InfoHashHex)
                                        {
                                            lock (winLock)
                                            {
                                                if (wonInfoBytes == null)
                                                {
                                                    wonInfoBytes = candidate;
                                                    try { raceCts.Cancel(); } catch { }
                                                }
                                            }
                                            return;
                                        }
                                    }
                                }
                            }
                            catch { }
                            finally
                            {
                                Interlocked.Decrement(ref activeFetchers);
                            }
                        }
                    }));
                }

                try { Task.WaitAll(fetchers.ToArray(), 26000); } catch { }
                try { raceCts.Cancel(); } catch { }

                if (wonInfoBytes != null)
                {
                    int pos = 0;
                    ShihabBencodeNode infoNode = ShihabBencodeParser.Parse(wonInfoBytes, ref pos);
                    if (infoNode != null && infoNode.Kind == "dict" && infoNode.DictValue != null)
                    {
                        PopulateMetadataFromInfoNode(meta, wonInfoBytes, infoNode);
                        return meta.ParsedSuccessfully && meta.PieceCount > 0;
                    }
                }
            }
            return false;
        }

        public static bool ExecuteP2pTorrentSession(
            ShihabDownloadItem item,
            ShihabTorrentMetadata meta,
            MehediAppConfiguration cfg,
            string baseDataDir,
            Action onProgressChanged,
            CancellationToken cancelToken,
            out string errorMessage,
            Action<int> onRateAndQuotaBytes = null)
        {
            errorMessage = "";
            if (meta == null) { errorMessage = "Missing torrent metadata."; return false; }
            if (!string.IsNullOrEmpty(meta.ErrorMessage)) { errorMessage = meta.ErrorMessage; return false; }
            if (meta.InfoHashBytes == null || meta.InfoHashBytes.Length != 20)
            {
                errorMessage = "Invalid or missing BitTorrent info-hash.";
                return false;
            }

            EnsurePeerBlocklistLoaded(cfg, baseDataDir);
            bool allowLocalTest = IsLocalTorrentTestAllowed();
            byte[] peerId = Encoding.ASCII.GetBytes("-MH1070-" + Guid.NewGuid().ToString("N").Substring(0, 12));

            // 1. Resolve magnet metadata in parallel via x.pe + Trackers + BEP-5 DHT
            if (meta.PieceCount <= 0 || meta.IsMagnetPendingMetadata)
            {
                item.ShihabStatusText = "Discovering peers & fetching magnet metadata...";
                if (onProgressChanged != null) onProgressChanged();

                bool gotMeta = TryResolveMagnetMetadataParallel(meta, cfg, baseDataDir, cancelToken, delegate(string st)
                {
                    item.ShihabStatusText = st;
                    if (onProgressChanged != null) onProgressChanged();
                });

                cancelToken.ThrowIfCancellationRequested();
                if (!gotMeta && meta.WebSeeds.Count == 0)
                {
                    errorMessage = !string.IsNullOrEmpty(meta.ErrorMessage)
                        ? meta.ErrorMessage
                        : "Unable to fetch magnet metadata from trackers, DHT, or peer hints.";
                    return false;
                }
            }

            if (meta.PieceCount <= 0 || meta.PieceLength <= 0 || meta.TotalSizeBytes <= 0)
            {
                errorMessage = !string.IsNullOrEmpty(meta.ErrorMessage)
                    ? meta.ErrorMessage
                    : "Torrent metadata has zero pieces or unknown total size.";
                return false;
            }

            // 2. Apply file selection (never deselect all files on a magnet unless user explicitly selected valid indices)
            HashSet<int> selectedIdx = null;
            if (item.ShihabSelectedTorrentFiles != null && item.ShihabSelectedTorrentFiles.Count > 0)
            {
                selectedIdx = new HashSet<int>(item.ShihabSelectedTorrentFiles);
            }
            else if (meta.SelectOnlyIndices != null && meta.SelectOnlyIndices.Count > 0)
            {
                selectedIdx = new HashSet<int>(meta.SelectOnlyIndices);
            }
            int selectedRealFiles = 0;
            for (int i = 0; i < meta.Files.Count; i++)
            {
                if (meta.Files[i].IsPaddingFile) meta.Files[i].Selected = false;
                else if (selectedIdx != null && selectedIdx.Count > 0) meta.Files[i].Selected = selectedIdx.Contains(meta.Files[i].Index);
                else meta.Files[i].Selected = true;
                if (meta.Files[i].Selected) selectedRealFiles++;
            }
            if (selectedRealFiles == 0)
            {
                for (int i = 0; i < meta.Files.Count; i++)
                {
                    if (!meta.Files[i].IsPaddingFile) meta.Files[i].Selected = true;
                }
            }

            bool[] pieceNeeded = new bool[meta.PieceCount];
            bool[] pieceDone = new bool[meta.PieceCount];
            bool[] pieceInFlight = new bool[meta.PieceCount];
            long selectedBytesTotal = 0;
            int totalNeededPieces = 0;
            for (int f = 0; f < meta.Files.Count; f++)
            {
                ShihabTorrentFileEntry fe = meta.Files[f];
                if (!fe.Selected) continue;
                selectedBytesTotal += fe.SizeBytes;
                if (fe.SizeBytes <= 0) continue;
                int firstP = (int)(fe.ByteOffsetInTorrent / meta.PieceLength);
                int lastP = (int)((fe.ByteOffsetInTorrent + fe.SizeBytes - 1L) / meta.PieceLength);
                for (int p = Math.Max(0, firstP); p <= Math.Min(meta.PieceCount - 1, lastP); p++)
                {
                    if (!pieceNeeded[p])
                    {
                        pieceNeeded[p] = true;
                        totalNeededPieces++;
                    }
                }
            }

            item.ShihabTotalBytes = selectedBytesTotal > 0 ? selectedBytesTotal : meta.TotalSizeBytes;
            item.ShihabResumeSupported = true;

            // 3. Resolve root save directory (supports multi-file outer folder toggle & single-file rename!)
            string rootSaveDir;
            string singleFileNameOverride = null;
            if (meta.IsMultiFile)
            {
                string folderName = ShihabProcessAndPathSafety.SanitizeFileName(meta.Name, "torrent_download");
                rootSaveDir = item.ShihabTorrentCreateSubfolder
                    ? Path.Combine(item.MehediSaveDirectory, folderName)
                    : item.MehediSaveDirectory;
                item.ShihabFileName = item.ShihabTorrentCreateSubfolder ? folderName : meta.Name;
            }
            else
            {
                rootSaveDir = item.MehediSaveDirectory;
                string canonicalSingle = ShihabProcessAndPathSafety.SanitizeFileName(meta.Files[0].RelativePath, "torrent_download");
                if (!string.IsNullOrWhiteSpace(item.ShihabFileName) &&
                    !string.Equals(item.ShihabFileName, "download.bin", StringComparison.OrdinalIgnoreCase) &&
                    !item.ShihabFileName.StartsWith("magnet_", StringComparison.OrdinalIgnoreCase) &&
                    !item.ShihabFileName.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                {
                    singleFileNameOverride = ShihabProcessAndPathSafety.SanitizeFileName(item.ShihabFileName, canonicalSingle);
                    meta.Files[0].RelativePath = singleFileNameOverride;
                }
                else
                {
                    item.ShihabFileName = canonicalSingle;
                }
            }
            Directory.CreateDirectory(rootSaveDir);

            // Create selected 0-byte files upfront (never skip empty files in multi-file torrents!)
            for (int f = 0; f < meta.Files.Count; f++)
            {
                ShihabTorrentFileEntry fe = meta.Files[f];
                if (!fe.Selected || fe.IsPaddingFile || fe.SizeBytes != 0) continue;
                string emptyPath;
                if (ShihabProcessAndPathSafety.ResolvePathStrictlyUnderRoot(rootSaveDir, fe.RelativePath, out emptyPath) &&
                    !ShihabProcessAndPathSafety.HasReparsePointUnderRoot(rootSaveDir, emptyPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(emptyPath));
                    if (!File.Exists(emptyPath))
                    {
                        using (FileStream efs = new FileStream(emptyPath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite)) { }
                    }
                }
            }

            // 4. Hash-verified Resume & Force Recheck
            string safeHashPrefix = ShihabProcessAndPathSafety.SanitizeFileName(
                meta.InfoHashHex.Substring(0, Math.Min(16, meta.InfoHashHex.Length)),
                "bt");
            string stateFile = Path.Combine(rootSaveDir, "." + safeHashPrefix + ".mhs.btstate");
            object pieceLock = new object();
            object diskLock = new object();

            bool anyExistingFileOnDisk = false;
            for (int f = 0; f < meta.Files.Count; f++)
            {
                ShihabTorrentFileEntry fe = meta.Files[f];
                if (!fe.Selected || fe.IsPaddingFile || fe.SizeBytes <= 0) continue;
                string fp;
                if (ShihabProcessAndPathSafety.ResolvePathStrictlyUnderRoot(rootSaveDir, fe.RelativePath, out fp) && File.Exists(fp))
                {
                    anyExistingFileOnDisk = true;
                    break;
                }
            }

            if (item.ShihabTorrentForceRecheck || File.Exists(stateFile) || anyExistingFileOnDisk)
            {
                item.ShihabStatusText = "Verifying existing torrent pieces (SHA-1)...";
                if (onProgressChanged != null) onProgressChanged();

                bool[] candidatePieces = new bool[meta.PieceCount];
                if (!item.ShihabTorrentForceRecheck && File.Exists(stateFile))
                {
                    try
                    {
                        string bits = File.ReadAllText(stateFile, Encoding.ASCII).Trim();
                        for (int p = 0; p < Math.Min(bits.Length, meta.PieceCount); p++)
                        {
                            if (bits[p] == '1' && pieceNeeded[p]) candidatePieces[p] = true;
                        }
                    }
                    catch { }
                }
                else
                {
                    for (int p = 0; p < meta.PieceCount; p++) candidatePieces[p] = pieceNeeded[p];
                }

                // Always verify actual disk bytes against SHA-1 so deleted/corrupt files are re-downloaded!
                for (int p = 0; p < meta.PieceCount; p++)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    if (!candidatePieces[p]) continue;
                    int pSize = GetPieceSize(meta, p);
                    byte[] diskData = ReadPieceSliceFromDisk(meta, p, 0, pSize, rootSaveDir, diskLock);
                    if (diskData != null && diskData.Length == pSize && VerifyPieceSha1(meta, p, diskData))
                    {
                        pieceDone[p] = true;
                    }
                }
                item.ShihabTorrentForceRecheck = false;
                WriteStateFileAtomically(stateFile, pieceDone);
            }

            item.MehediDownloadedBytes = ComputeCompletedSelectedBytes(meta, pieceDone);

            // 5. Start Real Incoming Listener / Seeding Server FIRST so real port is announced
            long uploadedWindowBytes = 0;
            Stopwatch uploadSw = Stopwatch.StartNew();
            object progressSync = new object();
            long downloadWindowBytes = 0;
            Stopwatch downloadSw = Stopwatch.StartNew();

            ConcurrentDictionary<string, ShihabTorrentPeerTelemetry> peerStatsMap =
                new ConcurrentDictionary<string, ShihabTorrentPeerTelemetry>(StringComparer.OrdinalIgnoreCase);
            List<ShihabTorrentTrackerTelemetry> trackerStats = new List<ShihabTorrentTrackerTelemetry>();
            object trackerStatsLock = new object();
            int dhtPeersFound = 0;
            int pexPeersFound = 0;
            int lsdPeersFound = 0;
            int activeOutPeers = 0;
            int totalKnownPeersCount = 0;
            bool isSeedingPhase = false;

            ShihabTorrentSeedServer seedServer = null;
            int listenPort = 0;

            Action publishTelemetry = delegate
            {
                ShihabTorrentLiveSnapshot snap = new ShihabTorrentLiveSnapshot();
                snap.ItemId = item.ShihabId;
                snap.InfoHashHex = meta.InfoHashHex;
                snap.IsPrivate = meta.IsPrivate;
                snap.IsSeeding = isSeedingPhase;
                snap.ListenPort = listenPort;
                snap.PieceCount = meta.PieceCount;
                snap.NeededPieces = totalNeededPieces;
                snap.ActivePeers = Volatile.Read(ref activeOutPeers) + (seedServer != null ? seedServer.ActiveIncomingPeers : 0);
                snap.TotalKnownPeers = Volatile.Read(ref totalKnownPeersCount);
                snap.DhtPeersFound = Volatile.Read(ref dhtPeersFound);
                snap.PexPeersFound = Volatile.Read(ref pexPeersFound);
                snap.LsdPeersFound = Volatile.Read(ref lsdPeersFound);
                snap.UploadedBytes = item.ShihabTorrentUploadedBytes;
                snap.UploadSpeedBps = item.ShihabTorrentUploadSpeedBps;
                snap.ShareRatio = item.ShihabTotalBytes > 0 ? ((double)item.ShihabTorrentUploadedBytes / (double)item.ShihabTotalBytes) : 0.0;

                byte[] pStates = new byte[meta.PieceCount];
                int compCount = 0;
                lock (pieceLock)
                {
                    for (int p = 0; p < meta.PieceCount; p++)
                    {
                        if (pieceDone[p])
                        {
                            pStates[p] = 2;
                            if (pieceNeeded[p]) compCount++;
                        }
                        else if (pieceInFlight[p]) pStates[p] = 1;
                        else pStates[p] = 0;
                    }
                }
                snap.CompletedPieces = compCount;
                snap.PieceStates = pStates;

                foreach (KeyValuePair<string, ShihabTorrentPeerTelemetry> kv in peerStatsMap)
                {
                    snap.Peers.Add(kv.Value);
                }
                lock (trackerStatsLock)
                {
                    snap.Trackers.AddRange(trackerStats);
                }
                for (int f = 0; f < meta.Files.Count; f++)
                {
                    ShihabTorrentFileEntry fe = meta.Files[f];
                    if (fe.IsPaddingFile) continue;
                    long fDone = ComputeFileCompletedBytes(meta, fe, pieceDone, pieceLock);
                    snap.Files.Add(new ShihabTorrentFileProgressTelemetry
                    {
                        Index = fe.Index,
                        RelativePath = fe.RelativePath,
                        SizeBytes = fe.SizeBytes,
                        CompletedBytes = fDone,
                        Selected = fe.Selected
                    });
                }
                ShihabTorrentLiveTelemetry.Publish(item.ShihabId, snap);
            };

            Action<int> recordUploaded = delegate(int upLen)
            {
                item.ShihabTorrentUploadedBytes += upLen;
                Interlocked.Add(ref uploadedWindowBytes, upLen);
                lock (progressSync)
                {
                    long ms = uploadSw.ElapsedMilliseconds;
                    if (ms >= 350)
                    {
                        long delta = Interlocked.Exchange(ref uploadedWindowBytes, 0);
                        uploadSw.Restart();
                        item.ShihabTorrentUploadSpeedBps = ms > 0 ? (delta * 1000.0 / ms) : 0.0;
                    }
                }
            };

            using (seedServer = new ShihabTorrentSeedServer(
                meta, peerId, rootSaveDir, pieceDone, pieceLock, diskLock,
                recordUploaded,
                delegate(ShihabTorrentPeerTelemetry pt) { peerStatsMap[pt.Endpoint] = pt; }))
            {
                if (seedServer.TryStart(cfg, cancelToken))
                {
                    listenPort = seedServer.ListenPort;
                    item.ShihabTorrentListenPort = listenPort;
                }

                // 6. Dynamic Peer Queue + Bad-Peer Banning + Multi-Source Discovery (Trackers + x.pe + DHT + LSD + PEX)
                ConcurrentQueue<IPEndPoint> peerQueue = new ConcurrentQueue<IPEndPoint>();
                HashSet<string> knownPeers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, int> badPeerStrikes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> bannedPeers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                object peerPoolLock = new object();

                Action<IPEndPoint, string> pushPeer = delegate(IPEndPoint ep, string sourceTag)
                {
                    if (ep == null || ep.Port <= 0 || ep.Port > 65535) return;
                    if (listenPort > 0 && ep.Port == listenPort && IPAddress.IsLoopback(ep.Address)) return;
                    if (!IsPeerAddressAllowed(ep.Address, allowLocalTest)) return;
                    string key = ep.ToString();
                    bool added = false;
                    lock (peerPoolLock)
                    {
                        if (bannedPeers.Contains(key)) return;
                        if (knownPeers.Add(key))
                        {
                            added = true;
                            totalKnownPeersCount = knownPeers.Count;
                        }
                    }
                    if (added)
                    {
                        peerQueue.Enqueue(ep);
                        if (sourceTag == "dht") Interlocked.Increment(ref dhtPeersFound);
                        else if (sourceTag == "pex") Interlocked.Increment(ref pexPeersFound);
                        else if (sourceTag == "lsd") Interlocked.Increment(ref lsdPeersFound);
                    }
                };

                // Enqueue direct x.pe peer hints immediately
                for (int i = 0; i < meta.DirectPeerHints.Count; i++)
                {
                    IPEndPoint hintEp;
                    if (ShihabTorrentDhtClient.TryParseHostPortEndPoint(meta.DirectPeerHints[i], out hintEp))
                    {
                        pushPeer(hintEp, "xpe");
                    }
                }

                // Initial Announce (if pieces still needed or seeding)
                bool needMorePieces = HasUnfinishedNeededPieces(meta.PieceCount, pieceNeeded, pieceDone, pieceLock);
                if (needMorePieces || item.ShihabTorrentKeepSeeding)
                {
                    item.ShihabStatusText = "Announcing to trackers & DHT...";
                    if (onProgressChanged != null) onProgressChanged();

                    List<ShihabTorrentTrackerTelemetry> initTStats;
                    List<IPEndPoint> initPeers = AnnounceAllTrackers(
                        meta, peerId, listenPort, item.ShihabTorrentUploadedBytes, item.MehediDownloadedBytes,
                        Math.Max(0L, item.ShihabTotalBytes - item.MehediDownloadedBytes),
                        "started", cfg, cancelToken, out initTStats);
                    lock (trackerStatsLock) { trackerStats = initTStats; }
                    for (int i = 0; i < initPeers.Count; i++) pushPeer(initPeers[i], "tracker");

                    if (!meta.IsPrivate)
                    {
                        ShihabTorrentLsdClient.AnnounceOnce(meta.InfoHashHex, listenPort, cfg);
                    }
                }

                bool sequential = item.ShihabTorrentSequential || (cfg != null && cfg.ShihabTorrentSequentialDefault);
                long lastStateFlushTick = Environment.TickCount;

                Action<int> onDownloadBytes = delegate(int gotLen)
                {
                    if (onRateAndQuotaBytes != null && gotLen > 0)
                    {
                        onRateAndQuotaBytes(gotLen);
                    }
                    Interlocked.Add(ref downloadWindowBytes, gotLen);
                    bool shouldNotify = false;
                    lock (progressSync)
                    {
                        long elapsedMs = downloadSw.ElapsedMilliseconds;
                        if (elapsedMs >= 300)
                        {
                            long delta = Interlocked.Exchange(ref downloadWindowBytes, 0);
                            downloadSw.Restart();
                            double speed = elapsedMs > 0 ? (delta * 1000.0 / elapsedMs) : 0.0;
                            item.ShihabCurrentSpeedBps = speed;
                            long doneBytes = ComputeCompletedSelectedBytes(meta, pieceDone);
                            item.MehediDownloadedBytes = Math.Min(item.ShihabTotalBytes, doneBytes);
                            long remBytes = Math.Max(0L, item.ShihabTotalBytes - item.MehediDownloadedBytes);
                            item.MehediEtaSeconds = speed > 1.0 ? (long)(remBytes / speed) : -1L;

                            int doneCnt = 0;
                            lock (pieceLock)
                            {
                                for (int p = 0; p < meta.PieceCount; p++) if (pieceNeeded[p] && pieceDone[p]) doneCnt++;
                            }
                            int totalPeersNow = Volatile.Read(ref activeOutPeers) + seedServer.ActiveIncomingPeers;
                            item.ShihabTorrentActivePeers = totalPeersNow;
                            item.ShihabStatusText = string.Format(CultureInfo.InvariantCulture,
                                "Downloading ({0}/{1} pcs, {2} peers)", doneCnt, totalNeededPieces, totalPeersNow);
                            shouldNotify = true;
                        }
                    }
                    if (shouldNotify)
                    {
                        publishTelemetry();
                        if (onProgressChanged != null) onProgressChanged();
                    }
                };

                Action maybeFlushState = delegate
                {
                    long nowTick = Environment.TickCount;
                    if (Math.Abs(nowTick - lastStateFlushTick) >= 1500)
                    {
                        lastStateFlushTick = nowTick;
                        WriteStateFileAtomically(stateFile, pieceDone);
                    }
                };

                // 7. Download Phase (WebSeeds + Continuous Dynamic TCP Peer Workers + Background DHT/Tracker Re-Announce)
                if (needMorePieces)
                {
                    using (CancellationTokenSource dlCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken))
                    {
                        CancellationToken dlToken = dlCts.Token;
                        bool dhtDiscoveryDone = meta.IsPrivate || ShihabTorrentSocketDialer.IsSocksProxyConfigured(cfg);
                        Task bgDiscoveryTask = Task.Run(delegate
                        {
                            if (!meta.IsPrivate && !ShihabTorrentSocketDialer.IsSocksProxyConfigured(cfg))
                            {
                                try
                                {
                                    ShihabTorrentDhtClient.QueryPeersViaDht(meta.InfoHashBytes, listenPort, cfg, allowLocalTest,
                                        delegate(IPEndPoint ep) { pushPeer(ep, "dht"); }, dlToken);
                                }
                                catch { }
                            }
                            dhtDiscoveryDone = true;

                            // Periodic tracker re-announce & DHT refresh while download is still active
                            Stopwatch reSw = Stopwatch.StartNew();
                            while (!dlToken.IsCancellationRequested && HasUnfinishedNeededPieces(meta.PieceCount, pieceNeeded, pieceDone, pieceLock))
                            {
                                if (reSw.ElapsedMilliseconds >= 25000)
                                {
                                    reSw.Restart();
                                    List<ShihabTorrentTrackerTelemetry> nextStats;
                                    List<IPEndPoint> more = AnnounceAllTrackers(
                                        meta, peerId, listenPort, item.ShihabTorrentUploadedBytes, item.MehediDownloadedBytes,
                                        Math.Max(0L, item.ShihabTotalBytes - item.MehediDownloadedBytes),
                                        "", cfg, dlToken, out nextStats);
                                    lock (trackerStatsLock) { trackerStats = nextStats; }
                                    for (int i = 0; i < more.Count; i++) pushPeer(more[i], "tracker");
                                }
                                try { dlToken.WaitHandle.WaitOne(400); } catch { break; }
                            }
                        });

                        List<Task> workers = new List<Task>();

                        // WebSeeds (BEP-19): supports BOTH single-file AND multi-file torrents!
                        for (int w = 0; w < meta.WebSeeds.Count; w++)
                        {
                            string wsUrl = meta.WebSeeds[w];
                            workers.Add(Task.Run(delegate
                            {
                                DownloadPiecesFromWebSeed(
                                    wsUrl, meta, cfg, pieceNeeded, pieceDone, pieceInFlight, pieceLock, diskLock,
                                    rootSaveDir, maybeFlushState, onDownloadBytes, dlToken);
                                if (!HasUnfinishedNeededPieces(meta.PieceCount, pieceNeeded, pieceDone, pieceLock))
                                {
                                    try { dlCts.Cancel(); } catch { }
                                }
                            }));
                        }

                        // Up to 20 dynamic TCP Peer Workers continuously pulling from peerQueue (NEVER discards peers > 12!)
                        int workerPoolSize = Math.Max(4, Math.Min(20, (cfg != null && cfg.ShihabDefaultMaxConnections > 0) ? cfg.ShihabDefaultMaxConnections * 2 : 16));
                        for (int w = 0; w < workerPoolSize; w++)
                        {
                            int workerSlot = w;
                            workers.Add(Task.Run(delegate
                            {
                                int idleWaits = 0;
                                while (!dlToken.IsCancellationRequested && HasUnfinishedNeededPieces(meta.PieceCount, pieceNeeded, pieceDone, pieceLock))
                                {
                                    IPEndPoint peerEp;
                                    if (!peerQueue.TryDequeue(out peerEp))
                                    {
                                        if (dhtDiscoveryDone && Volatile.Read(ref activeOutPeers) == 0 && meta.WebSeeds.Count == 0)
                                        {
                                            if (++idleWaits > 15) break; // ~3s after everything idle and no peers
                                        }
                                        else
                                        {
                                            idleWaits = 0;
                                        }
                                        try { dlToken.WaitHandle.WaitOne(200); } catch { break; }
                                        continue;
                                    }

                                    idleWaits = 0;
                                    string peerKey = peerEp.ToString();
                                    bool isBanned = false;
                                    lock (peerPoolLock) { isBanned = bannedPeers.Contains(peerKey); }
                                    if (isBanned) continue;

                                    DownloadPiecesFromTcpPeer(
                                        peerEp, meta, peerId, listenPort, cfg, sequential, workerSlot,
                                        pieceNeeded, pieceDone, pieceInFlight, pieceLock, diskLock,
                                        rootSaveDir, maybeFlushState, ref activeOutPeers,
                                        onDownloadBytes, recordUploaded,
                                        delegate(IPEndPoint pexEp)
                                        {
                                            if (!meta.IsPrivate) pushPeer(pexEp, "pex");
                                        },
                                        delegate(string badKey)
                                        {
                                            lock (peerPoolLock)
                                            {
                                                int st;
                                                badPeerStrikes.TryGetValue(badKey, out st);
                                                st++;
                                                badPeerStrikes[badKey] = st;
                                                if (st >= 1) bannedPeers.Add(badKey);
                                            }
                                        },
                                        delegate(ShihabTorrentPeerTelemetry pt)
                                        {
                                            peerStatsMap[pt.Endpoint] = pt;
                                        },
                                        dlToken);

                                    if (!HasUnfinishedNeededPieces(meta.PieceCount, pieceNeeded, pieceDone, pieceLock))
                                    {
                                        try { dlCts.Cancel(); } catch { }
                                        break;
                                    }
                                }
                            }));
                        }

                        try { Task.WaitAll(workers.ToArray()); } catch { }
                        try { dlCts.Cancel(); } catch { }
                        try { bgDiscoveryTask.Wait(1500); } catch { }
                    }
                }

                cancelToken.ThrowIfCancellationRequested();

                bool allDone = !HasUnfinishedNeededPieces(meta.PieceCount, pieceNeeded, pieceDone, pieceLock);
                if (!allDone)
                {
                    WriteStateFileAtomically(stateFile, pieceDone);
                    publishTelemetry();
                    errorMessage = Volatile.Read(ref totalKnownPeersCount) == 0 && meta.WebSeeds.Count == 0
                        ? "No active peers, DHT nodes, or WebSeeds found for this torrent."
                        : "Incomplete torrent transfer: all reachable peers disconnected before remaining pieces finished (progress verified & saved for resume).";
                    return false;
                }

                // Completed all needed pieces! Clean up .mhs.btstate and announce completed!
                try { if (File.Exists(stateFile)) File.Delete(stateFile); } catch { }
                item.MehediDownloadedBytes = item.ShihabTotalBytes;
                item.ShihabCurrentSpeedBps = 0;
                item.MehediEtaSeconds = 0;

                List<ShihabTorrentTrackerTelemetry> compTStats;
                AnnounceAllTrackers(meta, peerId, listenPort, item.ShihabTorrentUploadedBytes, item.MehediDownloadedBytes, 0L, "completed", cfg, CancellationToken.None, out compTStats);
                lock (trackerStatsLock) { trackerStats = compTStats; }
                publishTelemetry();
                if (onProgressChanged != null) onProgressChanged();

                // 8. Real Post-Completion Seeding Phase (when KeepSeeding is enabled!)
                if (item.ShihabTorrentKeepSeeding && listenPort > 0 && !cancelToken.IsCancellationRequested)
                {
                    isSeedingPhase = true;
                    double targetRatio = item.ShihabTorrentTargetShareRatio > 0.0
                        ? item.ShihabTorrentTargetShareRatio
                        : (cfg != null ? cfg.ShihabTorrentDefaultShareRatio : 0.0);
                    int maxSeedMin = item.ShihabTorrentMaxSeedMinutes > 0
                        ? item.ShihabTorrentMaxSeedMinutes
                        : (cfg != null ? cfg.ShihabTorrentDefaultSeedMinutes : 0);
                    Stopwatch seedSw = Stopwatch.StartNew();

                    while (!cancelToken.IsCancellationRequested)
                    {
                        double ratio = item.ShihabTotalBytes > 0
                            ? ((double)item.ShihabTorrentUploadedBytes / (double)item.ShihabTotalBytes)
                            : 0.0;
                        int incPeers = seedServer.ActiveIncomingPeers;
                        item.ShihabTorrentActivePeers = incPeers;
                        item.ShihabStatusText = string.Format(CultureInfo.InvariantCulture,
                            "Seeding ({0} peers, ratio {1:0.00}, up {2})",
                            incPeers, ratio, ShihabFormatHelpers.FormatMehediBytes(item.ShihabTorrentUploadedBytes));
                        publishTelemetry();
                        if (onProgressChanged != null) onProgressChanged();

                        if (targetRatio > 0.0 && ratio >= targetRatio) break;
                        if (maxSeedMin > 0 && seedSw.Elapsed.TotalMinutes >= maxSeedMin) break;

                        try { cancelToken.WaitHandle.WaitOne(500); } catch { break; }
                    }
                    isSeedingPhase = false;
                }

                // Send clean stopped event to trackers
                List<ShihabTorrentTrackerTelemetry> stopTStats;
                AnnounceAllTrackers(meta, peerId, listenPort, item.ShihabTorrentUploadedBytes, item.MehediDownloadedBytes, 0L, "stopped", cfg, CancellationToken.None, out stopTStats);
                item.ShihabTorrentUploadSpeedBps = 0;
                item.ShihabTorrentActivePeers = 0;
                publishTelemetry();
                return true;
            }
        }

        private static bool HasUnfinishedNeededPieces(int pieceCount, bool[] needed, bool[] done, object pieceLock)
        {
            lock (pieceLock)
            {
                for (int p = 0; p < pieceCount; p++)
                {
                    if (needed[p] && !done[p]) return true;
                }
            }
            return false;
        }

        private static long ComputeCompletedSelectedBytes(ShihabTorrentMetadata meta, bool[] done)
        {
            long sum = 0;
            for (int f = 0; f < meta.Files.Count; f++)
            {
                ShihabTorrentFileEntry fe = meta.Files[f];
                if (!fe.Selected || fe.IsPaddingFile || fe.SizeBytes <= 0) continue;
                int firstP = (int)(fe.ByteOffsetInTorrent / meta.PieceLength);
                int lastP = (int)((fe.ByteOffsetInTorrent + fe.SizeBytes - 1L) / meta.PieceLength);
                long fStart = fe.ByteOffsetInTorrent;
                long fEnd = fStart + fe.SizeBytes;
                for (int p = Math.Max(0, firstP); p <= Math.Min(meta.PieceCount - 1, lastP); p++)
                {
                    if (!done[p]) continue;
                    long pStart = (long)p * meta.PieceLength;
                    long pEnd = pStart + GetPieceSize(meta, p);
                    long ov = Math.Min(fEnd, pEnd) - Math.Max(fStart, pStart);
                    if (ov > 0) sum += ov;
                }
            }
            return sum;
        }

        private static long ComputeFileCompletedBytes(ShihabTorrentMetadata meta, ShihabTorrentFileEntry fe, bool[] done, object pieceLock)
        {
            if (fe == null || fe.SizeBytes <= 0) return 0;
            int firstP = (int)(fe.ByteOffsetInTorrent / meta.PieceLength);
            int lastP = (int)((fe.ByteOffsetInTorrent + fe.SizeBytes - 1L) / meta.PieceLength);
            long fStart = fe.ByteOffsetInTorrent;
            long fEnd = fStart + fe.SizeBytes;
            long sum = 0;
            lock (pieceLock)
            {
                for (int p = Math.Max(0, firstP); p <= Math.Min(meta.PieceCount - 1, lastP); p++)
                {
                    if (!done[p]) continue;
                    long pStart = (long)p * meta.PieceLength;
                    long pEnd = pStart + GetPieceSize(meta, p);
                    long ov = Math.Min(fEnd, pEnd) - Math.Max(fStart, pStart);
                    if (ov > 0) sum += ov;
                }
            }
            return Math.Min(fe.SizeBytes, sum);
        }

        private static void WriteStateFileAtomically(string stateFile, bool[] pieceDone)
        {
            try
            {
                char[] chars = new char[pieceDone.Length];
                for (int i = 0; i < pieceDone.Length; i++) chars[i] = pieceDone[i] ? '1' : '0';
                string tmp = stateFile + ".tmp";
                File.WriteAllText(tmp, new string(chars), Encoding.ASCII);
                if (File.Exists(stateFile))
                {
                    try { File.Replace(tmp, stateFile, null); return; } catch { }
                    try { File.Delete(stateFile); } catch { }
                }
                File.Move(tmp, stateFile);
            }
            catch { }
        }

        private static int GetPieceSize(ShihabTorrentMetadata meta, int pieceIndex)
        {
            long start = (long)pieceIndex * meta.PieceLength;
            long rem = meta.TotalSizeBytes - start;
            return (int)Math.Max(0L, Math.Min(meta.PieceLength, rem));
        }

        private static bool VerifyPieceSha1(ShihabTorrentMetadata meta, int pieceIndex, byte[] pieceData)
        {
            if (pieceData == null || meta.PiecesSha1Concat == null || (pieceIndex + 1) * 20 > meta.PiecesSha1Concat.Length)
            {
                return false;
            }
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] actual = sha1.ComputeHash(pieceData);
                for (int b = 0; b < 20; b++)
                {
                    if (actual[b] != meta.PiecesSha1Concat[pieceIndex * 20 + b]) return false;
                }
            }
            return true;
        }

        internal static byte[] ReadPieceSliceFromDisk(
            ShihabTorrentMetadata meta,
            int pieceIndex,
            int sliceOffset,
            int sliceLength,
            string rootSaveDir,
            object diskLock)
        {
            if (meta == null || sliceLength <= 0 || sliceOffset < 0) return null;
            int pSize = GetPieceSize(meta, pieceIndex);
            if (sliceOffset + sliceLength > pSize) return null;

            long reqGlobalStart = (long)pieceIndex * meta.PieceLength + sliceOffset;
            long reqGlobalEnd = reqGlobalStart + sliceLength;
            byte[] outBuf = new byte[sliceLength];

            lock (diskLock)
            {
                for (int f = 0; f < meta.Files.Count; f++)
                {
                    ShihabTorrentFileEntry fe = meta.Files[f];
                    if (fe.SizeBytes <= 0) continue;
                    long fStart = fe.ByteOffsetInTorrent;
                    long fEnd = fStart + fe.SizeBytes;
                    long ovStart = Math.Max(reqGlobalStart, fStart);
                    long ovEnd = Math.Min(reqGlobalEnd, fEnd);
                    if (ovEnd <= ovStart) continue;

                    if (fe.IsPaddingFile) continue; // padding bytes are zeroed
                    if (!fe.Selected) return null;

                    string fullFilePath;
                    if (!ShihabProcessAndPathSafety.ResolvePathStrictlyUnderRoot(rootSaveDir, fe.RelativePath, out fullFilePath) ||
                        ShihabProcessAndPathSafety.HasReparsePointUnderRoot(rootSaveDir, fullFilePath) ||
                        !File.Exists(fullFilePath))
                    {
                        return null;
                    }
                    int need = (int)(ovEnd - ovStart);
                    using (FileStream fs = new FileStream(fullFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        long fileOff = ovStart - fStart;
                        if (fs.Length < fileOff + need) return null;
                        fs.Seek(fileOff, SeekOrigin.Begin);
                        int readTot = 0;
                        int dstOff = (int)(ovStart - reqGlobalStart);
                        while (readTot < need)
                        {
                            int r = fs.Read(outBuf, dstOff + readTot, need - readTot);
                            if (r <= 0) return null;
                            readTot += r;
                        }
                    }
                }
            }
            return outBuf;
        }

        private static bool VerifyAndWritePieceToDisk(
            ShihabTorrentMetadata meta,
            MehediAppConfiguration cfg,
            int pieceIndex,
            byte[] pieceData,
            string rootSaveDir,
            Action maybeFlushState,
            bool[] pieceDone,
            bool[] pieceInFlight,
            object pieceLock,
            object diskWriteLock)
        {
            if (pieceData == null || !VerifyPieceSha1(meta, pieceIndex, pieceData)) return false;

            long pieceGlobalStart = (long)pieceIndex * meta.PieceLength;
            long pieceGlobalEnd = pieceGlobalStart + pieceData.Length;
            bool allowSparse = cfg == null || cfg.ShihabEnableSparseFiles;

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
                    if (ShihabProcessAndPathSafety.HasReparsePointUnderRoot(rootSaveDir, fullFilePath))
                    {
                        throw new InvalidOperationException("Blocked symlink/reparse point in torrent destination: " + fe.RelativePath);
                    }
                    using (FileStream fs = new FileStream(fullFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                    {
                        if (fs.Length != fe.SizeBytes)
                        {
                            if (allowSparse && fs.Length == 0 && fe.SizeBytes > 65536)
                            {
                                ShihabOsAndDiskSafety.TryMarkFileSparse(fs);
                            }
                            if (fs.Length > fe.SizeBytes)
                            {
                                fs.SetLength(fe.SizeBytes);
                            }
                        }
                        fs.Seek(ovStart - fStart, SeekOrigin.Begin);
                        fs.Write(pieceData, (int)(ovStart - pieceGlobalStart), (int)(ovEnd - ovStart));
                    }
                }

                lock (pieceLock)
                {
                    pieceDone[pieceIndex] = true;
                    pieceInFlight[pieceIndex] = false;
                }
                if (maybeFlushState != null) maybeFlushState();
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
                    // Prioritize first & last needed piece first (enables instant media previewing like MISTER ends_first!)
                    int firstNeeded = -1;
                    int lastNeeded = -1;
                    for (int p = 0; p < pieceCount; p++)
                    {
                        if (needed[p])
                        {
                            if (firstNeeded < 0) firstNeeded = p;
                            lastNeeded = p;
                        }
                    }
                    if (firstNeeded >= 0 && !done[firstNeeded] && !inFlight[firstNeeded] && (peerHasPiece == null || peerHasPiece[firstNeeded]))
                    {
                        inFlight[firstNeeded] = true;
                        return firstNeeded;
                    }
                    if (lastNeeded >= 0 && !done[lastNeeded] && !inFlight[lastNeeded] && (peerHasPiece == null || peerHasPiece[lastNeeded]))
                    {
                        inFlight[lastNeeded] = true;
                        return lastNeeded;
                    }
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
                // Endgame mode: allow racing remaining in-flight pieces across fast idle peers
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

        private static bool PeerHasAnyNeededPiece(int pieceCount, bool[] needed, bool[] done, bool[] peerHas, object pieceLock)
        {
            lock (pieceLock)
            {
                for (int p = 0; p < pieceCount; p++)
                {
                    if (needed[p] && !done[p] && peerHas[p]) return true;
                }
            }
            return false;
        }

        // -------------------- BEP-19 WebSeed (Single-File + Multi-File + Safe Redirects + Retry) --------------------
        private static string EncodeUrlPathSegments(string relPath)
        {
            string[] segs = (relPath ?? "").Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < segs.Length; i++)
            {
                if (i > 0) sb.Append('/');
                sb.Append(Uri.EscapeDataString(segs[i]));
            }
            return sb.ToString();
        }

        private static bool FetchHttpRangeWithRedirects(
            string targetUrl,
            long byteStart,
            long byteEnd,
            MehediAppConfiguration cfg,
            byte[] destBuf,
            int destOffset,
            Action<int> onBytesRead,
            CancellationToken cancelToken)
        {
            Uri current;
            if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(targetUrl, false, out current)) return false;
            int needLen = (int)(byteEnd - byteStart + 1L);
            if (needLen <= 0) return true;

            ShihabAutoUpdater.EnforceModernTls();
            for (int hop = 0; hop <= MaxHttpRedirects; hop++)
            {
                if (!IsUriAllowedForTorrentHttp(current)) return false;
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(current);
                req.AllowAutoRedirect = false;
                req.AddRange(byteStart, byteEnd);
                req.Timeout = 15000;
                req.ReadWriteTimeout = 15000;
                if (cfg != null) ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, current.AbsoluteUri);

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    int code = (int)resp.StatusCode;
                    if (code == 301 || code == 302 || code == 303 || code == 307 || code == 308)
                    {
                        string loc = resp.Headers["Location"];
                        Uri nextUri;
                        if (string.IsNullOrWhiteSpace(loc) || !Uri.TryCreate(current, loc, out nextUri)) return false;
                        current = nextUri;
                        continue;
                    }
                    if (resp.StatusCode != HttpStatusCode.PartialContent && !(byteStart == 0 && resp.StatusCode == HttpStatusCode.OK))
                    {
                        return false;
                    }
                    int off = 0;
                    using (Stream s = resp.GetResponseStream())
                    {
                        while (off < needLen)
                        {
                            cancelToken.ThrowIfCancellationRequested();
                            int r = s.Read(destBuf, destOffset + off, needLen - off);
                            if (r <= 0) break;
                            off += r;
                            if (onBytesRead != null) onBytesRead(r);
                        }
                    }
                    return off == needLen;
                }
            }
            return false;
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
            Action maybeFlushState,
            Action<int> onBytesRead,
            CancellationToken cancelToken)
        {
            Uri wsUri;
            if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(wsUrl, false, out wsUri) || !IsUriAllowedForTorrentHttp(wsUri))
            {
                return;
            }
            string baseUrl = wsUri.AbsoluteUri;
            int consecutiveFailures = 0;

            while (!cancelToken.IsCancellationRequested)
            {
                int pIdx = ClaimNextPieceIndex(meta.PieceCount, needed, done, inFlight, null, true, 0, pieceLock);
                if (pIdx < 0) break;
                int pSize = GetPieceSize(meta, pIdx);
                long pieceGlobalStart = (long)pIdx * meta.PieceLength;
                long pieceGlobalEnd = pieceGlobalStart + pSize;
                byte[] buf = new byte[pSize];
                bool pieceOk = false;

                try
                {
                    if (!meta.IsMultiFile)
                    {
                        string singleUrl = baseUrl.EndsWith("/", StringComparison.Ordinal)
                            ? (baseUrl + Uri.EscapeDataString(meta.Name))
                            : baseUrl;
                        pieceOk = FetchHttpRangeWithRedirects(singleUrl, pieceGlobalStart, pieceGlobalEnd - 1L, cfg, buf, 0, onBytesRead, cancelToken);
                    }
                    else
                    {
                        // BEP-19 Multi-file WebSeed: <baseUrl>/<torrent-name>/<file-rel-path>
                        string dirBase = baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : (baseUrl + "/");
                        string rootPrefix = dirBase + Uri.EscapeDataString(meta.Name) + "/";
                        pieceOk = true;
                        for (int f = 0; f < meta.Files.Count; f++)
                        {
                            ShihabTorrentFileEntry fe = meta.Files[f];
                            if (fe.SizeBytes <= 0) continue;
                            long fStart = fe.ByteOffsetInTorrent;
                            long fEnd = fStart + fe.SizeBytes;
                            long ovStart = Math.Max(pieceGlobalStart, fStart);
                            long ovEnd = Math.Min(pieceGlobalEnd, fEnd);
                            if (ovEnd <= ovStart) continue;
                            if (fe.IsPaddingFile) continue; // padding bytes remain 0

                            string fileUrl = rootPrefix + EncodeUrlPathSegments(fe.RelativePath);
                            int dstOff = (int)(ovStart - pieceGlobalStart);
                            if (!FetchHttpRangeWithRedirects(fileUrl, ovStart - fStart, ovEnd - fStart - 1L, cfg, buf, dstOff, onBytesRead, cancelToken))
                            {
                                pieceOk = false;
                                break;
                            }
                        }
                    }

                    if (pieceOk && VerifyAndWritePieceToDisk(meta, cfg, pIdx, buf, rootSaveDir, maybeFlushState, done, inFlight, pieceLock, diskWriteLock))
                    {
                        consecutiveFailures = 0;
                        continue;
                    }
                }
                catch (OperationCanceledException)
                {
                    lock (pieceLock) { if (!done[pIdx]) inFlight[pIdx] = false; }
                    break;
                }
                catch { }

                lock (pieceLock) { if (!done[pIdx]) inFlight[pIdx] = false; }
                consecutiveFailures++;
                if (consecutiveFailures >= 3) break;
                try { cancelToken.WaitHandle.WaitOne(500 * consecutiveFailures); } catch { break; }
            }
        }

        // -------------------- BEP-3 + BEP-6 + BEP-10 + BEP-11 Peer Wire Worker --------------------
        private static void DownloadPiecesFromTcpPeer(
            IPEndPoint peerEp,
            ShihabTorrentMetadata meta,
            byte[] peerId20,
            int listenPort,
            MehediAppConfiguration cfg,
            bool sequential,
            int workerSlot,
            bool[] needed,
            bool[] done,
            bool[] inFlight,
            object pieceLock,
            object diskWriteLock,
            string rootSaveDir,
            Action maybeFlushState,
            ref int activePeersCounter,
            Action<int> onBytesRead,
            Action<int> onUploadedBytes,
            Action<IPEndPoint> onPexPeerDiscovered,
            Action<string> onBadPeerStrike,
            Action<ShihabTorrentPeerTelemetry> onPeerTelemetry,
            CancellationToken cancelToken)
        {
            if (IsPeerBlocked(peerEp.Address)) return;
            Socket sock = ShihabTorrentSocketDialer.ConnectPeerTcp(peerEp, cfg, 4500, cancelToken);
            if (sock == null) return;

            bool countedActive = false;
            int claimedPiece = -1;
            string epStr = peerEp.ToString();
            long peerDownBytes = 0;
            long peerUpBytes = 0;
            bool allowLocalTest = IsLocalTorrentTestAllowed();

            try
            {
                sock.ReceiveTimeout = 25000;
                sock.SendTimeout = 10000;

                using (NetworkStream ns = new NetworkStream(sock, true))
                using (cancelToken.Register(delegate { try { sock.Close(); } catch { } }))
                {
                    byte[] hs = BuildHandshakePublic(meta.InfoHashBytes, peerId20, true, true, !meta.IsPrivate);
                    ns.Write(hs, 0, 68);
                    byte[] peerHs = ReadExactStreamPublic(ns, 68);
                    if (!IsValidBitTorrentHandshake(peerHs, meta.InfoHashBytes)) return;

                    bool peerSupportsExt = (peerHs[25] & 0x10) != 0;
                    string peerClient = DecodePeerClientName(peerHs);

                    Interlocked.Increment(ref activePeersCounter);
                    countedActive = true;

                    if (peerSupportsExt)
                    {
                        byte[] extHs = BuildExtendedHandshakePacket(listenPort, meta.RawInfoBytes != null ? meta.RawInfoBytes.Length : 0, !meta.IsPrivate);
                        ns.Write(extHs, 0, extHs.Length);
                    }

                    // Send our current bitfield if we already have verified pieces
                    SendBitfieldIfAny(ns, meta.PieceCount, done, pieceLock);

                    bool[] peerHas = new bool[meta.PieceCount];
                    bool peerHasInfoReceived = false;
                    bool peerChoking = true;
                    bool weAreInterested = false;
                    int peerUtMetadataId = 0;

                    byte[] currentPieceBuf = null;
                    int currentPieceSize = 0;
                    int currentBlockCount = 0;
                    bool[] blockRequested = null;
                    bool[] blockReceived = null;
                    int blocksCompleted = 0;
                    int outstandingBlocks = 0;
                    Stopwatch lastActivitySw = Stopwatch.StartNew();
                    Stopwatch keepAliveSw = Stopwatch.StartNew();

                    Action releaseCurrentPiece = delegate
                    {
                        if (claimedPiece >= 0)
                        {
                            lock (pieceLock)
                            {
                                if (!done[claimedPiece]) inFlight[claimedPiece] = false;
                            }
                            claimedPiece = -1;
                        }
                        currentPieceBuf = null;
                        blockRequested = null;
                        blockReceived = null;
                        blocksCompleted = 0;
                        outstandingBlocks = 0;
                    };

                    Action<bool> updateInterest = delegate(bool want)
                    {
                        if (want == weAreInterested) return;
                        weAreInterested = want;
                        ns.Write(new byte[] { 0, 0, 0, 1, (byte)(want ? 2 : 3) }, 0, 5);
                    };

                    Action emitTelemetry = delegate
                    {
                        if (onPeerTelemetry == null) return;
                        int haveCnt = 0;
                        for (int i = 0; i < peerHas.Length; i++) if (peerHas[i]) haveCnt++;
                        onPeerTelemetry(new ShihabTorrentPeerTelemetry
                        {
                            Endpoint = epStr,
                            ClientName = peerClient,
                            Direction = "Out",
                            Flags = (peerChoking ? "c" : "D") + (peerSupportsExt ? "E" : ""),
                            DownloadedBytes = peerDownBytes,
                            UploadedBytes = peerUpBytes,
                            ProgressPercent = peerHas.Length > 0 ? (haveCnt * 100.0 / peerHas.Length) : 0.0
                        });
                    };

                    // Send initial Interested so peers that send Unchoke eagerly will unchoke us
                    updateInterest(true);
                    emitTelemetry();

                    while (!cancelToken.IsCancellationRequested)
                    {
                        if (!HasUnfinishedNeededPieces(meta.PieceCount, needed, done, pieceLock))
                        {
                            break;
                        }

                        // Claim and pipeline up to 16 blocks ONLY when peer is unchoking AND has advertised pieces
                        if (!peerChoking && peerHasInfoReceived)
                        {
                            if (claimedPiece < 0)
                            {
                                claimedPiece = ClaimNextPieceIndex(meta.PieceCount, needed, done, inFlight, peerHas, sequential, peerEp.Port + workerSlot, pieceLock);
                                if (claimedPiece >= 0)
                                {
                                    currentPieceSize = GetPieceSize(meta, claimedPiece);
                                    currentPieceBuf = new byte[currentPieceSize];
                                    currentBlockCount = (currentPieceSize + 16383) / 16384;
                                    blockRequested = new bool[currentBlockCount];
                                    blockReceived = new bool[currentBlockCount];
                                    blocksCompleted = 0;
                                    outstandingBlocks = 0;
                                }
                                else
                                {
                                    // Do NOT disconnect prematurely! Mark not interested and wait for Have/Bitfield or other pieces.
                                    updateInterest(PeerHasAnyNeededPiece(meta.PieceCount, needed, done, peerHas, pieceLock));
                                }
                            }

                            if (claimedPiece >= 0)
                            {
                                for (int bIdx = 0; bIdx < currentBlockCount && outstandingBlocks < 16; bIdx++)
                                {
                                    if (blockReceived[bIdx] || blockRequested[bIdx]) continue;
                                    int blkOff = bIdx * 16384;
                                    int blkLen = Math.Min(16384, currentPieceSize - blkOff);
                                    byte[] reqMsg = new byte[17];
                                    WriteBeUInt32Public(reqMsg, 0, 13u);
                                    reqMsg[4] = 6;
                                    WriteBeUInt32Public(reqMsg, 5, (uint)claimedPiece);
                                    WriteBeUInt32Public(reqMsg, 9, (uint)blkOff);
                                    WriteBeUInt32Public(reqMsg, 13, (uint)blkLen);
                                    ns.Write(reqMsg, 0, 17);
                                    blockRequested[bIdx] = true;
                                    outstandingBlocks++;
                                }
                            }
                        }

                        // Send periodic keep-alive every 60s
                        if (keepAliveSw.ElapsedMilliseconds >= 60000)
                        {
                            keepAliveSw.Restart();
                            ns.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
                        }

                        // Non-blocking poll so we can react quickly to cancellation / completion
                        if (!sock.Poll(1000000, SelectMode.SelectRead))
                        {
                            if (lastActivitySw.ElapsedMilliseconds >= 25000) break;
                            continue;
                        }

                        byte[] lenBytes = ReadExactStreamPublic(ns, 4);
                        lastActivitySw.Restart();
                        uint msgLen = ReadBeUInt32Public(lenBytes, 0);
                        if (msgLen == 0) continue; // Keep-alive
                        if (msgLen > 2 * 1024 * 1024) break;

                        byte[] payload = ReadExactStreamPublic(ns, (int)msgLen);
                        byte msgId = payload[0];

                        if (msgId == 0) // Choke: BEP-3 peers discard pending requests! Reset cleanly so zero deadlock!
                        {
                            peerChoking = true;
                            if (claimedPiece >= 0 && blockRequested != null)
                            {
                                // Release piece back so another unchoked peer can download it immediately
                                releaseCurrentPiece();
                            }
                            emitTelemetry();
                        }
                        else if (msgId == 1) // Unchoke
                        {
                            peerChoking = false;
                            emitTelemetry();
                        }
                        else if (msgId == 2) // Interested from remote peer -> unchoke for tit-for-tat
                        {
                            ns.Write(new byte[] { 0, 0, 0, 1, 1 }, 0, 5);
                        }
                        else if (msgId == 4 && payload.Length >= 5) // Have
                        {
                            int p = (int)ReadBeUInt32Public(payload, 1);
                            if (p >= 0 && p < peerHas.Length)
                            {
                                peerHas[p] = true;
                                peerHasInfoReceived = true;
                                if (!weAreInterested && needed[p] && !done[p]) updateInterest(true);
                            }
                        }
                        else if (msgId == 5) // Bitfield
                        {
                            for (int p = 0; p < peerHas.Length; p++)
                            {
                                int byteIdx = 1 + (p >> 3);
                                if (byteIdx < payload.Length && ((payload[byteIdx] >> (7 - (p & 7))) & 1) != 0)
                                {
                                    peerHas[p] = true;
                                }
                            }
                            peerHasInfoReceived = true;
                            updateInterest(PeerHasAnyNeededPiece(meta.PieceCount, needed, done, peerHas, pieceLock));
                            emitTelemetry();
                        }
                        else if (msgId == 6 && payload.Length >= 13) // Incoming Request -> serve piece from disk!
                        {
                            int reqP = (int)ReadBeUInt32Public(payload, 1);
                            int reqBegin = (int)ReadBeUInt32Public(payload, 5);
                            int reqLen = (int)ReadBeUInt32Public(payload, 9);
                            if (reqP >= 0 && reqP < meta.PieceCount && reqBegin >= 0 && reqLen > 0 && reqLen <= 131072)
                            {
                                bool weHave = false;
                                lock (pieceLock) { weHave = done[reqP]; }
                                if (weHave)
                                {
                                    byte[] blk = ReadPieceSliceFromDisk(meta, reqP, reqBegin, reqLen, rootSaveDir, diskWriteLock);
                                    if (blk != null && blk.Length == reqLen)
                                    {
                                        byte[] pm = new byte[13 + reqLen];
                                        WriteBeUInt32Public(pm, 0, (uint)(9 + reqLen));
                                        pm[4] = 7;
                                        WriteBeUInt32Public(pm, 5, (uint)reqP);
                                        WriteBeUInt32Public(pm, 9, (uint)reqBegin);
                                        Buffer.BlockCopy(blk, 0, pm, 13, reqLen);
                                        ns.Write(pm, 0, pm.Length);
                                        peerUpBytes += reqLen;
                                        if (onUploadedBytes != null) onUploadedBytes(reqLen);
                                    }
                                }
                            }
                        }
                        else if (msgId == 7 && payload.Length > 9) // Piece block
                        {
                            int pIndex = (int)ReadBeUInt32Public(payload, 1);
                            int begin = (int)ReadBeUInt32Public(payload, 5);
                            int blkDataLen = payload.Length - 9;
                            if (outstandingBlocks > 0) outstandingBlocks--;

                            if (pIndex == claimedPiece && currentPieceBuf != null && blockReceived != null &&
                                begin >= 0 && (begin % 16384) == 0 && ((long)begin + blkDataLen) <= currentPieceSize)
                            {
                                int bIdx = begin / 16384;
                                int expectedBlkLen = Math.Min(16384, currentPieceSize - begin);
                                if (bIdx >= 0 && bIdx < currentBlockCount && blkDataLen == expectedBlkLen && !blockReceived[bIdx])
                                {
                                    Buffer.BlockCopy(payload, 9, currentPieceBuf, begin, blkDataLen);
                                    blockReceived[bIdx] = true;
                                    blocksCompleted++;
                                    peerDownBytes += blkDataLen;
                                    if (onBytesRead != null) onBytesRead(blkDataLen);

                                    if (blocksCompleted >= currentBlockCount)
                                    {
                                        int finishedIdx = claimedPiece;
                                        bool verified = VerifyAndWritePieceToDisk(
                                            meta, cfg, finishedIdx, currentPieceBuf, rootSaveDir,
                                            maybeFlushState, done, inFlight, pieceLock, diskWriteLock);

                                        claimedPiece = -1;
                                        currentPieceBuf = null;
                                        blockRequested = null;
                                        blockReceived = null;
                                        blocksCompleted = 0;
                                        outstandingBlocks = 0;

                                        if (verified)
                                        {
                                            // Broadcast HAVE to this peer
                                            byte[] haveMsg = new byte[9];
                                            WriteBeUInt32Public(haveMsg, 0, 5u);
                                            haveMsg[4] = 4;
                                            WriteBeUInt32Public(haveMsg, 5, (uint)finishedIdx);
                                            ns.Write(haveMsg, 0, 9);
                                            emitTelemetry();
                                        }
                                        else
                                        {
                                            // Corrupt piece! Release piece and strike/ban the peer!
                                            lock (pieceLock) { if (!done[finishedIdx]) inFlight[finishedIdx] = false; }
                                            if (onBadPeerStrike != null) onBadPeerStrike(epStr);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        else if (msgId == 14) // BEP-6 Fast Extension: Have All
                        {
                            for (int p = 0; p < peerHas.Length; p++) peerHas[p] = true;
                            peerHasInfoReceived = true;
                            updateInterest(PeerHasAnyNeededPiece(meta.PieceCount, needed, done, peerHas, pieceLock));
                            emitTelemetry();
                        }
                        else if (msgId == 15) // BEP-6 Fast Extension: Have None
                        {
                            peerHasInfoReceived = true;
                            updateInterest(false);
                        }
                        else if (msgId == 16 && payload.Length >= 13) // BEP-6 Fast Extension: Reject Request
                        {
                            int rejP = (int)ReadBeUInt32Public(payload, 1);
                            int rejBegin = (int)ReadBeUInt32Public(payload, 5);
                            if (outstandingBlocks > 0) outstandingBlocks--;
                            if (rejP == claimedPiece && blockRequested != null && rejBegin >= 0 && (rejBegin % 16384) == 0)
                            {
                                int bIdx = rejBegin / 16384;
                                if (bIdx >= 0 && bIdx < blockRequested.Length) blockRequested[bIdx] = false;
                            }
                        }
                        else if (msgId == 20 && payload.Length >= 2) // BEP-10 Extended (handshake / ut_pex / ut_metadata)
                        {
                            byte extId = payload[1];
                            if (extId == 0)
                            {
                                int pos = 2;
                                ShihabBencodeNode d = ShihabBencodeParser.Parse(payload, ref pos);
                                if (d != null && d.DictValue != null)
                                {
                                    ShihabBencodeNode vNode, mNode, utMetaNode;
                                    if (d.DictValue.TryGetValue("v", out vNode) && vNode != null && !string.IsNullOrWhiteSpace(vNode.GetUtf8String()))
                                    {
                                        peerClient = vNode.GetUtf8String();
                                    }
                                    if (d.DictValue.TryGetValue("m", out mNode) && mNode != null && mNode.DictValue != null &&
                                        mNode.DictValue.TryGetValue("ut_metadata", out utMetaNode) && utMetaNode != null)
                                    {
                                        peerUtMetadataId = (int)utMetaNode.IntValue;
                                    }
                                }
                            }
                            else if (extId == 1 && !meta.IsPrivate && (cfg == null || cfg.ShihabTorrentEnablePex) && onPexPeerDiscovered != null)
                            {
                                // BEP-11 ut_pex (our advertised ID = 1)
                                int pos = 2;
                                ShihabBencodeNode pex = ShihabBencodeParser.Parse(payload, ref pos);
                                if (pex != null && pex.DictValue != null)
                                {
                                    List<IPEndPoint> pexList = new List<IPEndPoint>();
                                    ShihabBencodeNode add4, add6;
                                    if (pex.DictValue.TryGetValue("added", out add4) && add4 != null && add4.Kind == "bytes" && add4.BytesValue != null)
                                    {
                                        ParseCompactIpv4Peers(add4.BytesValue, pexList, allowLocalTest);
                                    }
                                    if (pex.DictValue.TryGetValue("added6", out add6) && add6 != null && add6.Kind == "bytes" && add6.BytesValue != null)
                                    {
                                        ParseCompactIpv6Peers(add6.BytesValue, pexList, allowLocalTest);
                                    }
                                    for (int i = 0; i < pexList.Count; i++) onPexPeerDiscovered(pexList[i]);
                                }
                            }
                            else if (extId == 2 && peerUtMetadataId > 0 && meta.RawInfoBytes != null && meta.RawInfoBytes.Length > 0)
                            {
                                ShihabTorrentSeedServer.HandleUtMetadataRequest(ns, payload, (byte)peerUtMetadataId, meta.RawInfoBytes);
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

        private static void SendBitfieldIfAny(Stream ns, int pieceCount, bool[] done, object pieceLock)
        {
            if (pieceCount <= 0) return;
            int bfLen = (pieceCount + 7) >> 3;
            byte[] pkt = new byte[5 + bfLen];
            WriteBeUInt32Public(pkt, 0, (uint)(1 + bfLen));
            pkt[4] = 5;
            bool any = false;
            lock (pieceLock)
            {
                for (int p = 0; p < pieceCount && p < done.Length; p++)
                {
                    if (done[p])
                    {
                        pkt[5 + (p >> 3)] |= (byte)(1 << (7 - (p & 7)));
                        any = true;
                    }
                }
            }
            if (any) ns.Write(pkt, 0, pkt.Length);
        }

        // -------------------- BEP-9 ut_metadata Peer Fetcher (No 64-Step Cap, Strict Validation, SOCKS5/VPN Aware) --------------------
        private static byte[] TryFetchMagnetMetadataFromPeer(
            IPEndPoint peerEp,
            byte[] infoHash20,
            byte[] peerId20,
            MehediAppConfiguration cfg,
            CancellationToken cancelToken)
        {
            if (IsPeerBlocked(peerEp.Address)) return null;
            Socket sock = ShihabTorrentSocketDialer.ConnectPeerTcp(peerEp, cfg, 3500, cancelToken);
            if (sock == null) return null;

            try
            {
                sock.ReceiveTimeout = 6000;
                sock.SendTimeout = 5000;

                using (NetworkStream ns = new NetworkStream(sock, true))
                using (cancelToken.Register(delegate { try { sock.Close(); } catch { } }))
                {
                    byte[] hs = BuildHandshakePublic(infoHash20, peerId20, true, false, false);
                    ns.Write(hs, 0, 68);
                    byte[] peerHs = ReadExactStreamPublic(ns, 68);
                    if (!IsValidBitTorrentHandshake(peerHs, infoHash20) || (peerHs[25] & 0x10) == 0) return null;

                    byte[] extHsMsg = BuildExtendedHandshakePacket(0, 0, false);
                    ns.Write(extHsMsg, 0, extHsMsg.Length);

                    int peerUtMetadataId = 0;
                    int metadataSize = 0;
                    byte[] assembled = null;
                    bool[] receivedPieces = null;
                    int receivedCount = 0;
                    int totalMetaPieces = 0;
                    int nextToRequest = 0;
                    Stopwatch deadlineSw = Stopwatch.StartNew();

                    for (int step = 0; step < 2000 && deadlineSw.ElapsedMilliseconds < 15000 && !cancelToken.IsCancellationRequested; step++)
                    {
                        byte[] lb = ReadExactStreamPublic(ns, 4);
                        uint mLen = ReadBeUInt32Public(lb, 0);
                        if (mLen == 0) continue;
                        if (mLen > 2 * 1024 * 1024) return null;
                        byte[] body = ReadExactStreamPublic(ns, (int)mLen);
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
                            if (peerUtMetadataId <= 0 || metadataSize <= 0 || metadataSize > 4 * 1024 * 1024)
                            {
                                return null;
                            }
                            assembled = new byte[metadataSize];
                            totalMetaPieces = (metadataSize + 16383) / 16384;
                            receivedPieces = new bool[totalMetaPieces];
                            // Pipeline up to 4 metadata piece requests at once
                            while (nextToRequest < Math.Min(4, totalMetaPieces))
                            {
                                SendUtMetadataRequest(ns, (byte)peerUtMetadataId, nextToRequest++);
                            }
                        }
                        else if (extId == 2 && assembled != null && receivedPieces != null)
                        {
                            int pos = 2;
                            ShihabBencodeNode hdr = ShihabBencodeParser.Parse(body, ref pos);
                            if (hdr == null || hdr.DictValue == null) continue;
                            ShihabBencodeNode mtNode, pcNode;
                            if (!hdr.DictValue.TryGetValue("msg_type", out mtNode) || mtNode == null) continue;
                            if (mtNode.IntValue == 2) return null; // Peer rejected ut_metadata request
                            if (mtNode.IntValue == 1 && hdr.DictValue.TryGetValue("piece", out pcNode) && pcNode != null)
                            {
                                int p = (int)pcNode.IntValue;
                                if (p < 0 || p >= totalMetaPieces) continue;
                                int expectedChunkLen = (p == totalMetaPieces - 1) ? (metadataSize - p * 16384) : 16384;
                                int dataBytes = body.Length - pos;
                                if (dataBytes != expectedChunkLen) return null;
                                if (!receivedPieces[p])
                                {
                                    Buffer.BlockCopy(body, pos, assembled, p * 16384, dataBytes);
                                    receivedPieces[p] = true;
                                    receivedCount++;
                                }
                                if (receivedCount >= totalMetaPieces)
                                {
                                    return assembled;
                                }
                                if (nextToRequest < totalMetaPieces)
                                {
                                    SendUtMetadataRequest(ns, (byte)peerUtMetadataId, nextToRequest++);
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
            WriteBeUInt32Public(pkt, 0, (uint)(2 + dict.Length));
            pkt[4] = 20;
            pkt[5] = peerUtId;
            Buffer.BlockCopy(dict, 0, pkt, 6, dict.Length);
            ns.Write(pkt, 0, pkt.Length);
        }

        internal static byte[] BuildExtendedHandshakePacket(int listenPort, int metadataSize, bool advertisePex)
        {
            Dictionary<string, object> mDict = new Dictionary<string, object>(StringComparer.Ordinal);
            if (advertisePex) mDict["ut_pex"] = 1;
            mDict["ut_metadata"] = 2;

            Dictionary<string, object> root = new Dictionary<string, object>(StringComparer.Ordinal);
            root["m"] = mDict;
            root["reqq"] = 250;
            root["v"] = "MHS Download Manager " + ShihabAutoUpdater.GetCurrentAppVersion();
            if (listenPort > 0) root["p"] = listenPort;
            if (metadataSize > 0) root["metadata_size"] = metadataSize;

            byte[] extDict = ShihabBencodeParser.EncodeCanonical(root);
            byte[] pkt = new byte[6 + extDict.Length];
            WriteBeUInt32Public(pkt, 0, (uint)(2 + extDict.Length));
            pkt[4] = 20;
            pkt[5] = 0;
            Buffer.BlockCopy(extDict, 0, pkt, 6, extDict.Length);
            return pkt;
        }

        internal static bool IsValidBitTorrentHandshake(byte[] peerHs, byte[] expectedInfoHash20)
        {
            if (peerHs == null || peerHs.Length != 68 || expectedInfoHash20 == null || expectedInfoHash20.Length != 20) return false;
            if (peerHs[0] != 19) return false;
            string proto = Encoding.ASCII.GetString(peerHs, 1, 19);
            if (!string.Equals(proto, "BitTorrent protocol", StringComparison.Ordinal)) return false;
            for (int i = 0; i < 20; i++)
            {
                if (peerHs[28 + i] != expectedInfoHash20[i]) return false;
            }
            return true;
        }

        internal static string DecodePeerClientName(byte[] peerHs)
        {
            if (peerHs == null || peerHs.Length < 68) return "BitTorrent Peer";
            if (peerHs[48] == (byte)'-' && peerHs[55] == (byte)'-')
            {
                string code = Encoding.ASCII.GetString(peerHs, 49, 2);
                string ver = Encoding.ASCII.GetString(peerHs, 51, 4);
                if (code == "qB") return "qBittorrent " + ver;
                if (code == "TR") return "Transmission " + ver;
                if (code == "UT") return "uTorrent " + ver;
                if (code == "DE") return "Deluge " + ver;
                if (code == "LT" || code == "lt") return "libtorrent " + ver;
                if (code == "RQ") return "rqbit " + ver;
                if (code == "MH") return "MHS Download Manager " + ver;
                return code + " " + ver;
            }
            return "BitTorrent Peer";
        }

        internal static byte[] BuildHandshakePublic(byte[] infoHash20, byte[] peerId20, bool bep10Ext, bool bep6Fast, bool bep5Dht)
        {
            byte[] hs = new byte[68];
            hs[0] = 19;
            byte[] proto = Encoding.ASCII.GetBytes("BitTorrent protocol");
            Buffer.BlockCopy(proto, 0, hs, 1, 19);
            if (bep10Ext) hs[25] |= 0x10;
            if (bep6Fast) hs[27] |= 0x04;
            if (bep5Dht) hs[27] |= 0x01;
            Buffer.BlockCopy(infoHash20, 0, hs, 28, 20);
            Buffer.BlockCopy(peerId20, 0, hs, 48, 20);
            return hs;
        }

        internal static byte[] ReadExactStreamPublic(Stream s, int count)
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

        internal static void WriteBeUInt32Public(byte[] buf, int off, uint v)
        {
            buf[off] = (byte)((v >> 24) & 0xFF);
            buf[off + 1] = (byte)((v >> 16) & 0xFF);
            buf[off + 2] = (byte)((v >> 8) & 0xFF);
            buf[off + 3] = (byte)(v & 0xFF);
        }

        private static void WriteBeUInt64(byte[] buf, int off, ulong v)
        {
            WriteBeUInt32Public(buf, off, (uint)(v >> 32));
            WriteBeUInt32Public(buf, off + 4, (uint)(v & 0xFFFFFFFFUL));
        }

        internal static uint ReadBeUInt32Public(byte[] buf, int off)
        {
            return ((uint)buf[off] << 24) | ((uint)buf[off + 1] << 16) | ((uint)buf[off + 2] << 8) | buf[off + 3];
        }

        private static ulong ReadBeUInt64(byte[] buf, int off)
        {
            return ((ulong)ReadBeUInt32Public(buf, off) << 32) | ReadBeUInt32Public(buf, off + 4);
        }

        // -------------------- .torrent Creator (Fixed Blank Trackers, Empty Dir, Drive Root, Direct Hash) --------------------
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
                infoHashHex = CreateTorrentFileInternal(sourceFileOrDir, outputTorrentPath, trackers, webSeeds, "Created by MHS Download Manager", pieceLengthBytes, isPrivate);
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
                CreateTorrentFileInternal(sourceFileOrDir, outputTorrentPath, trackers, webSeeds, comment, pieceLengthBytes, isPrivate);
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
            CreateTorrentFileInternal(sourceFileOrDir, outputTorrentPath, trackers, webSeeds, comment, pieceLengthBytes, isPrivate);
        }

        private static string CreateTorrentFileInternal(
            string sourceFileOrDir,
            string outputTorrentPath,
            List<string> trackers,
            List<string> webSeeds,
            string comment,
            int pieceLengthBytes,
            bool isPrivate)
        {
            if (pieceLengthBytes < MinPieceLengthBytes) pieceLengthBytes = 262144;
            if (pieceLengthBytes > MaxPieceLengthBytes) pieceLengthBytes = 16 * 1024 * 1024;
            // Normalize to power of two
            int pow2 = MinPieceLengthBytes;
            while (pow2 < pieceLengthBytes && pow2 < MaxPieceLengthBytes) pow2 <<= 1;
            pieceLengthBytes = pow2;

            string fullSrc = Path.GetFullPath(sourceFileOrDir);
            string rootPath = Path.GetPathRoot(fullSrc);
            if (!string.Equals(fullSrc, rootPath, StringComparison.OrdinalIgnoreCase))
            {
                fullSrc = fullSrc.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            bool isDir = Directory.Exists(fullSrc);
            List<string> fullFiles = new List<string>();
            string torrentName = Path.GetFileName(fullSrc);
            if (string.IsNullOrWhiteSpace(torrentName)) torrentName = "torrent_bundle";

            if (isDir)
            {
                string[] all = Directory.GetFiles(fullSrc, "*", System.IO.SearchOption.AllDirectories);
                Array.Sort(all, StringComparer.OrdinalIgnoreCase);
                fullFiles.AddRange(all);
                if (fullFiles.Count == 0)
                {
                    throw new InvalidOperationException("Cannot create a .torrent from an empty directory.");
                }
            }
            else if (File.Exists(fullSrc))
            {
                fullFiles.Add(fullSrc);
            }
            else
            {
                throw new FileNotFoundException("Source path for torrent creation was not found.", sourceFileOrDir);
            }

            List<string> cleanTrackers = new List<string>();
            if (trackers != null)
            {
                for (int i = 0; i < trackers.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(trackers[i]))
                    {
                        string tr = trackers[i].Trim();
                        if (!cleanTrackers.Contains(tr)) cleanTrackers.Add(tr);
                    }
                }
            }

            List<string> cleanWebSeeds = new List<string>();
            if (webSeeds != null)
            {
                for (int i = 0; i < webSeeds.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(webSeeds[i]))
                    {
                        string ws = webSeeds[i].Trim();
                        if (!cleanWebSeeds.Contains(ws)) cleanWebSeeds.Add(ws);
                    }
                }
            }

            byte[] allHashes;
            long totalBytesHashed = 0;
            using (MemoryStream piecesHashes = new MemoryStream())
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] pieceBuf = new byte[pieceLengthBytes];
                int pieceFill = 0;
                for (int i = 0; i < fullFiles.Count; i++)
                {
                    using (FileStream fs = new FileStream(fullFiles[i], FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        int read;
                        while ((read = fs.Read(pieceBuf, pieceFill, pieceLengthBytes - pieceFill)) > 0)
                        {
                            pieceFill += read;
                            totalBytesHashed += read;
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
                allHashes = piecesHashes.ToArray();
            }

            if (totalBytesHashed <= 0 || allHashes.Length == 0)
            {
                throw new InvalidOperationException("Cannot create a .torrent with 0 total bytes of file content.");
            }

            // Build info dict in memory so we can compute exact SHA-1 info_hash without re-reading disk!
            byte[] infoDictBytes;
            string infoHashHex;
            using (MemoryStream infoMs = new MemoryStream())
            {
                ShihabBencodeParser.WriteAscii(infoMs, "d");
                if (isDir)
                {
                    string prefixWithSep = fullSrc.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                        ? fullSrc
                        : (fullSrc + Path.DirectorySeparatorChar);
                    ShihabBencodeParser.WriteBencodeString(infoMs, "files");
                    ShihabBencodeParser.WriteAscii(infoMs, "l");
                    for (int i = 0; i < fullFiles.Count; i++)
                    {
                        FileInfo fi = new FileInfo(fullFiles[i]);
                        ShihabBencodeParser.WriteAscii(infoMs, "d");
                        ShihabBencodeParser.WriteBencodeString(infoMs, "length");
                        ShihabBencodeParser.WriteBencodeInt(infoMs, fi.Length);
                        ShihabBencodeParser.WriteBencodeString(infoMs, "path");
                        ShihabBencodeParser.WriteAscii(infoMs, "l");
                        string rel = fullFiles[i].Substring(prefixWithSep.Length);
                        string[] segs = rel.Split(new char[] { Path.DirectorySeparatorChar, '/' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int s = 0; s < segs.Length; s++)
                        {
                            ShihabBencodeParser.WriteBencodeString(infoMs, segs[s]);
                        }
                        ShihabBencodeParser.WriteAscii(infoMs, "ee");
                    }
                    ShihabBencodeParser.WriteAscii(infoMs, "e");
                }
                else
                {
                    FileInfo fi = new FileInfo(fullFiles[0]);
                    ShihabBencodeParser.WriteBencodeString(infoMs, "length");
                    ShihabBencodeParser.WriteBencodeInt(infoMs, fi.Length);
                }

                ShihabBencodeParser.WriteBencodeKeyString(infoMs, "name", torrentName);
                ShihabBencodeParser.WriteBencodeString(infoMs, "piece length");
                ShihabBencodeParser.WriteBencodeInt(infoMs, pieceLengthBytes);
                ShihabBencodeParser.WriteBencodeKeyBytes(infoMs, "pieces", allHashes);
                if (isPrivate)
                {
                    ShihabBencodeParser.WriteBencodeString(infoMs, "private");
                    ShihabBencodeParser.WriteBencodeInt(infoMs, 1);
                }
                ShihabBencodeParser.WriteAscii(infoMs, "e");
                infoDictBytes = infoMs.ToArray();
            }

            using (SHA1 sha1 = SHA1.Create())
            {
                infoHashHex = BytesToHexLower(sha1.ComputeHash(infoDictBytes));
            }

            using (FileStream outFs = new FileStream(outputTorrentPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                ShihabBencodeParser.WriteAscii(outFs, "d");
                if (cleanTrackers.Count > 0)
                {
                    ShihabBencodeParser.WriteBencodeKeyString(outFs, "announce", cleanTrackers[0]);
                    ShihabBencodeParser.WriteBencodeString(outFs, "announce-list");
                    ShihabBencodeParser.WriteAscii(outFs, "l");
                    for (int i = 0; i < cleanTrackers.Count; i++)
                    {
                        ShihabBencodeParser.WriteAscii(outFs, "l");
                        ShihabBencodeParser.WriteBencodeString(outFs, cleanTrackers[i]);
                        ShihabBencodeParser.WriteAscii(outFs, "e");
                    }
                    ShihabBencodeParser.WriteAscii(outFs, "e");
                }
                if (!string.IsNullOrWhiteSpace(comment))
                {
                    ShihabBencodeParser.WriteBencodeKeyString(outFs, "comment", comment.Trim());
                }
                ShihabBencodeParser.WriteBencodeKeyString(outFs, "created by", "MHS Download Manager " + ShihabAutoUpdater.GetCurrentAppVersion());
                long unixNow = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                ShihabBencodeParser.WriteBencodeString(outFs, "creation date");
                ShihabBencodeParser.WriteBencodeInt(outFs, unixNow);

                ShihabBencodeParser.WriteBencodeString(outFs, "info");
                outFs.Write(infoDictBytes, 0, infoDictBytes.Length);

                if (cleanWebSeeds.Count > 0)
                {
                    ShihabBencodeParser.WriteBencodeString(outFs, "url-list");
                    ShihabBencodeParser.WriteAscii(outFs, "l");
                    for (int i = 0; i < cleanWebSeeds.Count; i++)
                    {
                        ShihabBencodeParser.WriteBencodeString(outFs, cleanWebSeeds[i]);
                    }
                    ShihabBencodeParser.WriteAscii(outFs, "e");
                }
                ShihabBencodeParser.WriteAscii(outFs, "e");
            }
            return infoHashHex;
        }
    }
}
