using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public sealed class ShihabClickNLoadServer : IDisposable
    {
        public const int DefaultPort = 9666;
        private const int MaxHeaderBytes = 32768;
        private const int MaxBodyBytes = 256 * 1024;
        private const int MaxLinksPerRequest = 500;
        private const int RateLimitMaxRequestsPerMinute = 10;

        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);
        private static readonly Regex JkQuotedHexRegex = new Regex(
            @"['""]([0-9a-fA-F]{32})['""]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout);
        private static readonly Regex JkRawHexRegex = new Regex(
            @"\b([0-9a-fA-F]{32})\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout);

        private readonly MehediAppConfiguration _config;
        private readonly MehediDynamicSegmentEngine _engine;
        private readonly Action<List<string>, string, string> _onLinksReceived;
        private readonly int _portOverride;
        private readonly object _rateLock = new object();
        private readonly Dictionary<string, List<DateTime>> _originHits = new Dictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);

        private TcpListener _listener;
        private CancellationTokenSource _stopCts;
        private volatile bool _running;

        public bool IsRunning { get { return _running; } }

        public int BoundPort
        {
            get
            {
                if (_portOverride > 0) return _portOverride;
                if (_config != null && _config.ShihabClickNLoadPort > 0) return _config.ShihabClickNLoadPort;
                return DefaultPort;
            }
        }

        public ShihabClickNLoadServer(
            MehediAppConfiguration config,
            MehediDynamicSegmentEngine engine,
            Action<List<string>, string, string> onLinksReceived = null,
            int portOverride = 0)
        {
            _config = config;
            _engine = engine;
            _onLinksReceived = onLinksReceived;
            _portOverride = portOverride;
        }

        public bool Start()
        {
            if (_running) return true;
            try
            {
                int port = BoundPort;
                _listener = new TcpListener(IPAddress.Loopback, port);
                _listener.Start();
                _running = true;
                _stopCts = new CancellationTokenSource();
                CancellationToken tok = _stopCts.Token;
                Task.Run(delegate { AcceptLoop(tok); });
                return true;
            }
            catch
            {
                _running = false;
                try { if (_listener != null) _listener.Stop(); } catch { }
                _listener = null;
                return false;
            }
        }

        public void Stop()
        {
            _running = false;
            try { if (_stopCts != null) _stopCts.Cancel(); } catch { }
            try { if (_listener != null) _listener.Stop(); } catch { }
            _listener = null;
        }

        public void Dispose()
        {
            Stop();
            try { if (_stopCts != null) _stopCts.Dispose(); } catch { }
            _stopCts = null;
        }

        private void AcceptLoop(CancellationToken tok)
        {
            while (!tok.IsCancellationRequested && _running && _listener != null)
            {
                TcpClient client = null;
                try
                {
                    client = _listener.AcceptTcpClient();
                    TcpClient captured = client;
                    Task.Run(delegate { HandleClient(captured); });
                }
                catch
                {
                    try { if (client != null) client.Close(); } catch { }
                    if (!_running || tok.IsCancellationRequested) break;
                    Thread.Sleep(50);
                }
            }
        }

        private static bool IsLoopbackHostHeader(string hostHeader)
        {
            if (string.IsNullOrWhiteSpace(hostHeader)) return true;
            string raw = hostHeader.Trim();
            if (ShihabProcessAndPathSafety.ContainsControlOrNewline(raw)) return false;
            string hostOnly = raw;
            if (raw.StartsWith("[", StringComparison.Ordinal))
            {
                int closeIdx = raw.IndexOf(']');
                if (closeIdx <= 0) return false;
                hostOnly = raw.Substring(0, closeIdx + 1);
            }
            else
            {
                int firstColon = raw.IndexOf(':');
                int lastColon = raw.LastIndexOf(':');
                if (firstColon > 0 && firstColon == lastColon)
                {
                    hostOnly = raw.Substring(0, firstColon);
                }
            }
            return string.Equals(hostOnly, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(hostOnly, "localhost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(hostOnly, "[::1]", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(hostOnly, "::1", StringComparison.OrdinalIgnoreCase);
        }

        private bool TryConsumeRateLimit(string originKey)
        {
            string key = string.IsNullOrWhiteSpace(originKey) ? "anonymous" : originKey.Trim().ToLowerInvariant();
            DateTime now = DateTime.UtcNow;
            DateTime cutoff = now.AddSeconds(-60.0);
            lock (_rateLock)
            {
                if (_originHits.Count > 256)
                {
                    _originHits.Clear();
                }
                List<DateTime> hits;
                if (!_originHits.TryGetValue(key, out hits))
                {
                    hits = new List<DateTime>();
                    _originHits[key] = hits;
                }
                for (int i = hits.Count - 1; i >= 0; i--)
                {
                    if (hits[i] < cutoff) hits.RemoveAt(i);
                }
                if (hits.Count >= RateLimitMaxRequestsPerMinute)
                {
                    return false;
                }
                hits.Add(now);
                return true;
            }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 10000;
                    client.SendTimeout = 10000;
                    using (NetworkStream ns = client.GetStream())
                    using (BufferedStream bs = new BufferedStream(ns, 8192))
                    {
                        string method;
                        string path;
                        string hostHeader;
                        string originHeader;
                        int contentLength;
                        if (!ReadHeaders(bs, out method, out path, out hostHeader, out originHeader, out contentLength))
                        {
                            return;
                        }

                        string safeCorsOrigin = (!string.IsNullOrWhiteSpace(originHeader) && !ShihabProcessAndPathSafety.ContainsControlOrNewline(originHeader))
                            ? originHeader.Trim()
                            : "*";

                        if (!IsLoopbackHostHeader(hostHeader))
                        {
                            WriteResponse(ns, 403, "Forbidden", "text/plain; charset=utf-8", "Forbidden Host", safeCorsOrigin);
                            return;
                        }

                        if (string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                        {
                            WriteResponse(ns, 204, "No Content", "text/plain; charset=utf-8", "", safeCorsOrigin);
                            return;
                        }

                        string cleanPath = (path ?? "/").Split('?')[0].ToLowerInvariant();
                        if (cleanPath == "/jdcheck.js" && string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                        {
                            WriteResponse(ns, 200, "OK", "application/javascript; charset=utf-8", "jdownloader=true;\nvar version='" + ShihabAutoUpdater.GetCurrentAppVersion() + "';\n", safeCorsOrigin);
                            return;
                        }

                        if (cleanPath == "/crossdomain.xml" && string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                        {
                            string xml = "<?xml version=\"1.0\"?>\r\n<!DOCTYPE cross-domain-policy SYSTEM \"http://www.macromedia.com/xml/dtds/cross-domain-policy.dtd\">\r\n<cross-domain-policy><allow-access-from domain=\"*\" /></cross-domain-policy>";
                            WriteResponse(ns, 200, "OK", "application/xml; charset=utf-8", xml, safeCorsOrigin);
                            return;
                        }

                        if ((cleanPath == "/flash/add" || cleanPath == "/flash/addcrypted2") &&
                            string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!TryConsumeRateLimit(originHeader))
                            {
                                if (contentLength > 0 && contentLength <= MaxBodyBytes)
                                {
                                    byte[] discard = new byte[Math.Min(contentLength, 8192)];
                                    int rem = contentLength;
                                    while (rem > 0)
                                    {
                                        int r = bs.Read(discard, 0, Math.Min(rem, discard.Length));
                                        if (r <= 0) break;
                                        rem -= r;
                                    }
                                }
                                WriteResponse(ns, 429, "Too Many Requests", "text/plain; charset=utf-8", "Rate limit exceeded (max 10 requests/minute).", safeCorsOrigin);
                                try { client.Client.Shutdown(SocketShutdown.Send); } catch { }
                                return;
                            }

                            if (contentLength < 0 || contentLength > MaxBodyBytes)
                            {
                                WriteResponse(ns, 413, "Payload Too Large", "text/plain; charset=utf-8", "Click'n'Load body exceeds 256 KB limit.", safeCorsOrigin);
                                return;
                            }

                            byte[] bodyBytes = new byte[contentLength];
                            int got = ReadExact(bs, bodyBytes, contentLength);
                            if (got < contentLength)
                            {
                                WriteResponse(ns, 400, "Bad Request", "text/plain; charset=utf-8", "Truncated request body.", safeCorsOrigin);
                                return;
                            }

                            string formBody = Encoding.UTF8.GetString(bodyBytes, 0, got);
                            Dictionary<string, string> fields = ParseFormUrlEncoded(formBody);

                            string sourceUrl = GetField(fields, "source");
                            string passwords = GetField(fields, "passwords");
                            string rawUrlsBlock = "";

                            if (cleanPath == "/flash/add")
                            {
                                rawUrlsBlock = GetField(fields, "urls");
                            }
                            else
                            {
                                string jk = GetField(fields, "jk");
                                string crypted = GetField(fields, "crypted");
                                string decrypted;
                                if (!TryDecryptClickNLoadPayload(jk, crypted, out decrypted))
                                {
                                    WriteResponse(ns, 400, "Bad Request", "text/plain; charset=utf-8", "Failed to decrypt Click'n'Load2 payload.", safeCorsOrigin);
                                    return;
                                }
                                rawUrlsBlock = decrypted;
                            }

                            List<string> acceptedUrls = FilterSafeClickNLoadUrls(rawUrlsBlock);
                            if (acceptedUrls.Count == 0)
                            {
                                WriteResponse(ns, 400, "Bad Request", "text/plain; charset=utf-8", "No valid external HTTP/HTTPS URLs found.", safeCorsOrigin);
                                return;
                            }

                            string safeSource = "";
                            Uri srcUri;
                            if (ShihabProcessAndPathSafety.TryValidateHttpUrl(sourceUrl, false, out srcUri) &&
                                !ShihabProcessAndPathSafety.IsHostDisallowedForOutboundSsrf(srcUri.Host, true))
                            {
                                safeSource = srcUri.AbsoluteUri;
                            }

                            DispatchAcceptedLinks(acceptedUrls, safeSource, passwords);
                            WriteResponse(ns, 200, "OK", "text/plain; charset=utf-8", "success\r\n", safeCorsOrigin);
                            return;
                        }

                        WriteResponse(ns, 404, "Not Found", "text/plain; charset=utf-8", "Not found", safeCorsOrigin);
                    }
                }
                catch { }
            }
        }

        private void DispatchAcceptedLinks(List<string> urls, string sourceReferer, string passwords)
        {
            if (urls == null || urls.Count == 0) return;
            if (_onLinksReceived != null)
            {
                try { _onLinksReceived(urls, sourceReferer ?? "", passwords ?? ""); } catch { }
                return;
            }
            if (_engine == null) return;

            string firstPassword = "";
            if (!string.IsNullOrWhiteSpace(passwords))
            {
                string[] pwParts = passwords.Split(new char[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (pwParts.Length > 0) firstPassword = pwParts[0].Trim();
            }

            for (int i = 0; i < urls.Count; i++)
            {
                string url = urls[i];
                string fileName = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(url);
                ShihabCategoryRule cat = _config != null
                    ? _config.ResolveMehediCategoryForFile(fileName)
                    : new ShihabCategoryRule { ShihabName = "General", ShihabDefaultFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") };

                ShihabDownloadItem item = new ShihabDownloadItem
                {
                    MehediUrl = url,
                    ShihabFileName = ShihabFormatHelpers.EnsureShihabUniqueFilePath(cat.ShihabDefaultFolder, fileName),
                    MehediSaveDirectory = cat.ShihabDefaultFolder,
                    ShihabCategoryName = cat.ShihabName,
                    MehediReferer = sourceReferer ?? "",
                    ShihabArchivePassword = firstPassword,
                    ShihabDescription = "Click'n'Load 2",
                    MehediQueueName = "Main Download Queue"
                };
                item.SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Click'n'Load Queued");
                _engine.AddShihabDownloadItem(item, false);
            }
        }

        public static List<string> FilterSafeClickNLoadUrls(string rawText)
        {
            List<string> results = new List<string>();
            if (string.IsNullOrWhiteSpace(rawText)) return results;

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = rawText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length && results.Count < MaxLinksPerRequest; i++)
            {
                string cand = lines[i].Trim();
                if (string.IsNullOrEmpty(cand)) continue;
                Uri parsed;
                if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(cand, false, out parsed))
                {
                    continue;
                }
                // Strict SSRF protection: reject loopback, link-local, RFC1918 LAN, and metadata endpoints
                if (ShihabProcessAndPathSafety.IsHostDisallowedForOutboundSsrf(parsed.Host, true))
                {
                    continue;
                }
                string normalized = parsed.AbsoluteUri;
                if (seen.Add(normalized))
                {
                    results.Add(normalized);
                }
            }
            return results;
        }

        public static bool TryDecryptClickNLoadPayload(string jkScript, string cryptedBase64, out string decryptedText)
        {
            decryptedText = "";
            if (string.IsNullOrWhiteSpace(jkScript) || string.IsNullOrWhiteSpace(cryptedBase64)) return false;

            string hexKey = "";
            try
            {
                Match mQuoted = JkQuotedHexRegex.Match(jkScript);
                if (mQuoted.Success)
                {
                    hexKey = mQuoted.Groups[1].Value;
                }
                else
                {
                    Match mRaw = JkRawHexRegex.Match(jkScript);
                    if (mRaw.Success) hexKey = mRaw.Groups[1].Value;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }

            if (string.IsNullOrEmpty(hexKey) || hexKey.Length != 32) return false;

            byte[] keyBytes = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                byte b;
                if (!byte.TryParse(hexKey.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
                {
                    return false;
                }
                keyBytes[i] = b;
            }

            byte[] cipherBytes;
            try
            {
                string normB64 = cryptedBase64.Trim().Replace(' ', '+').Replace("\r", "").Replace("\n", "");
                int mod4 = normB64.Length % 4;
                if (mod4 != 0) normB64 += new string('=', 4 - mod4);
                cipherBytes = Convert.FromBase64String(normB64);
            }
            catch
            {
                return false;
            }

            if (cipherBytes == null || cipherBytes.Length == 0 || (cipherBytes.Length % 16) != 0)
            {
                return false;
            }

            try
            {
                using (AesManaged aes = new AesManaged())
                {
                    aes.KeySize = 128;
                    aes.BlockSize = 128;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.None;
                    aes.Key = keyBytes;
                    aes.IV = keyBytes;
                    using (ICryptoTransform dec = aes.CreateDecryptor())
                    {
                        byte[] plainBytes = dec.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                        int len = plainBytes.Length;
                        // Strip zero padding (standard CNL2) or valid PKCS#7 padding
                        while (len > 0 && plainBytes[len - 1] == 0)
                        {
                            len--;
                        }
                        if (len > 0)
                        {
                            byte lastPad = plainBytes[len - 1];
                            if (lastPad >= 1 && lastPad <= 16 && lastPad <= len)
                            {
                                bool validPkcs7 = true;
                                for (int p = 0; p < lastPad; p++)
                                {
                                    if (plainBytes[len - 1 - p] != lastPad)
                                    {
                                        validPkcs7 = false;
                                        break;
                                    }
                                }
                                if (validPkcs7)
                                {
                                    len -= lastPad;
                                }
                            }
                        }
                        decryptedText = Encoding.UTF8.GetString(plainBytes, 0, Math.Max(0, len)).TrimEnd('\0');
                        return !string.IsNullOrWhiteSpace(decryptedText);
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private static Dictionary<string, string> ParseFormUrlEncoded(string body)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(body)) return map;
            string[] pairs = body.Split('&');
            for (int i = 0; i < pairs.Length; i++)
            {
                string pair = pairs[i];
                if (string.IsNullOrEmpty(pair)) continue;
                int eq = pair.IndexOf('=');
                string rawK = eq >= 0 ? pair.Substring(0, eq) : pair;
                string rawV = eq >= 0 ? pair.Substring(eq + 1) : "";
                try
                {
                    string k = Uri.UnescapeDataString(rawK.Replace('+', ' '));
                    string v = Uri.UnescapeDataString(rawV.Replace('+', ' '));
                    map[k] = v;
                }
                catch { }
            }
            return map;
        }

        private static string GetField(Dictionary<string, string> map, string key)
        {
            string val;
            return (map != null && map.TryGetValue(key, out val)) ? (val ?? "") : "";
        }

        private static bool ReadHeaders(
            Stream stream,
            out string method,
            out string path,
            out string hostHeader,
            out string originHeader,
            out int contentLength)
        {
            method = "";
            path = "/";
            hostHeader = "";
            originHeader = "";
            contentLength = 0;

            using (MemoryStream ms = new MemoryStream())
            {
                int p3 = -1, p2 = -1, p1 = -1;
                while (ms.Length < MaxHeaderBytes)
                {
                    int b = stream.ReadByte();
                    if (b < 0) break;
                    ms.WriteByte((byte)b);
                    if (p3 == '\r' && p2 == '\n' && p1 == '\r' && b == '\n') break;
                    p3 = p2; p2 = p1; p1 = b;
                }
                string hdrText = Encoding.ASCII.GetString(ms.ToArray());
                if (string.IsNullOrWhiteSpace(hdrText)) return false;
                string[] lines = hdrText.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0) return false;
                string[] reqParts = lines[0].Split(' ');
                if (reqParts.Length < 2) return false;
                method = reqParts[0].Trim();
                path = reqParts[1].Trim();

                for (int i = 1; i < lines.Length; i++)
                {
                    string ln = lines[i];
                    if (ln.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        int parsedLen;
                        if (!int.TryParse(ln.Substring(15).Trim(), out parsedLen)) contentLength = -1;
                        else contentLength = parsedLen;
                    }
                    else if (ln.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                    {
                        hostHeader = ln.Substring(5).Trim();
                    }
                    else if (ln.StartsWith("Origin:", StringComparison.OrdinalIgnoreCase))
                    {
                        originHeader = ln.Substring(7).Trim();
                    }
                }
            }
            return true;
        }

        private static int ReadExact(Stream s, byte[] buf, int count)
        {
            int total = 0;
            while (total < count)
            {
                int r = s.Read(buf, total, count - total);
                if (r <= 0) break;
                total += r;
            }
            return total;
        }

        private static void WriteResponse(NetworkStream ns, int statusCode, string reason, string contentType, string body, string corsOrigin)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body ?? "");
            StringBuilder sb = new StringBuilder(384);
            sb.AppendFormat("HTTP/1.1 {0} {1}\r\n", statusCode, reason);
            sb.AppendFormat("Content-Type: {0}\r\n", contentType);
            sb.Append("X-Content-Type-Options: nosniff\r\n");
            sb.AppendFormat("Access-Control-Allow-Origin: {0}\r\n", string.IsNullOrEmpty(corsOrigin) ? "*" : corsOrigin);
            sb.Append("Access-Control-Allow-Private-Network: true\r\n");
            sb.Append("Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n");
            sb.Append("Access-Control-Allow-Headers: Content-Type, X-Requested-With\r\n");
            sb.Append("Connection: close\r\n");
            sb.AppendFormat("Content-Length: {0}\r\n\r\n", bodyBytes.Length);
            byte[] hdr = Encoding.ASCII.GetBytes(sb.ToString());
            ns.Write(hdr, 0, hdr.Length);
            if (bodyBytes.Length > 0) ns.Write(bodyBytes, 0, bodyBytes.Length);
            ns.Flush();
        }
    }
}
