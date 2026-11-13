using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public static class ShihabMcpAndCliHost
    {
        private const int MaxMcpLineChars = 2 * 1024 * 1024;
        private const int MaxBridgeResponseBytes = 4 * 1024 * 1024;
        private const int MaxProbeRedirects = 5;

        public static bool TryCallLiveBridge(
            ShihabStateStore store,
            MehediAppConfiguration cfg,
            string httpMethod,
            string path,
            string jsonBody,
            out string responseBody)
        {
            responseBody = "";
            if (store == null || cfg == null || cfg.MehediLocalBridgePort <= 0) return false;
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/", StringComparison.Ordinal) ||
                path.IndexOf('\r') >= 0 || path.IndexOf('\n') >= 0)
            {
                return false;
            }
            try
            {
                string token = store.LoadOrCreateBridgeToken();
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(
                    "http://127.0.0.1:" + cfg.MehediLocalBridgePort.ToString(CultureInfo.InvariantCulture) + path);
                req.Method = string.IsNullOrWhiteSpace(httpMethod) ? "GET" : httpMethod.Trim().ToUpperInvariant();
                req.Timeout = 2500;
                req.ReadWriteTimeout = 2500;
                req.AllowAutoRedirect = false;
                req.Proxy = null;
                req.Headers["X-MHS-Bridge-Token"] = token;
                if (req.Method == "POST")
                {
                    byte[] payload = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
                    req.ContentType = "application/json; charset=utf-8";
                    req.ContentLength = payload.Length;
                    using (Stream s = req.GetRequestStream())
                    {
                        s.Write(payload, 0, payload.Length);
                    }
                }
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (Stream rs = resp.GetResponseStream())
                {
                    if (rs != null)
                    {
                        responseBody = ReadBoundedUtf8Text(rs, MaxBridgeResponseBytes);
                    }
                    return (int)resp.StatusCode >= 200 && (int)resp.StatusCode < 300;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string ReadBoundedUtf8Text(Stream stream, int maxBytes)
        {
            if (stream == null) return "";
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] buf = new byte[16384];
                int r;
                while ((r = stream.Read(buf, 0, buf.Length)) > 0)
                {
                    if (ms.Length + r > maxBytes)
                    {
                        throw new InvalidOperationException("Response exceeded safe size limit.");
                    }
                    ms.Write(buf, 0, r);
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        public static int RunMcpStdioServer(ShihabStateStore store)
        {
            if (store == null) return 1;
            MehediAppConfiguration cfg = store.LoadShihabConfig();
            string line;
            while ((line = Console.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.Length > MaxMcpLineChars)
                {
                    WriteJsonRpcError("null", -32600, "JSON-RPC request exceeded maximum safe size.");
                    continue;
                }

                Dictionary<string, object> reqObj = ShihabJsonCodec.ParseObjectOrNull(line, 32);
                if (reqObj == null)
                {
                    WriteJsonRpcError("null", -32700, "Parse error: invalid JSON object.");
                    continue;
                }

                string method = ShihabJsonCodec.GetString(reqObj, "method");
                bool hasId = reqObj.ContainsKey("id");
                string idRaw = ShihabJsonCodec.SerializeJsonRpcId(reqObj);

                if (method == "initialize")
                {
                    Dictionary<string, object> serverInfo = new Dictionary<string, object>();
                    serverInfo["name"] = "mhs-download-manager-mcp";
                    serverInfo["version"] = ShihabAutoUpdater.GetCurrentAppVersion();

                    Dictionary<string, object> caps = new Dictionary<string, object>();
                    caps["tools"] = new Dictionary<string, object>();

                    Dictionary<string, object> initRes = new Dictionary<string, object>();
                    initRes["protocolVersion"] = "2024-11-05";
                    initRes["capabilities"] = caps;
                    initRes["serverInfo"] = serverInfo;
                    WriteJsonRpcResult(idRaw, ShihabJsonCodec.Serialize(initRes));
                }
                else if (method == "notifications/initialized" || !hasId)
                {
                    // Notification requires no JSON-RPC response
                }
                else if (method == "tools/list")
                {
                    WriteJsonRpcResult(idRaw, BuildMcpToolsListJson());
                }
                else if (method == "tools/call")
                {
                    cfg = store.LoadShihabConfig();
                    Dictionary<string, object> paramsObj = ShihabJsonCodec.GetObject(reqObj, "params");
                    string toolName = paramsObj != null ? ShihabJsonCodec.GetString(paramsObj, "name") : "";
                    Dictionary<string, object> argsObj = paramsObj != null ? ShihabJsonCodec.GetObject(paramsObj, "arguments") : null;
                    if (argsObj == null) argsObj = paramsObj ?? new Dictionary<string, object>();

                    string toolText = ExecuteMcpToolCall(store, cfg, toolName, argsObj);
                    Dictionary<string, object> textItem = new Dictionary<string, object>();
                    textItem["type"] = "text";
                    textItem["text"] = toolText ?? "";
                    List<object> contentList = new List<object>();
                    contentList.Add(textItem);
                    Dictionary<string, object> resObj = new Dictionary<string, object>();
                    resObj["content"] = contentList;
                    WriteJsonRpcResult(idRaw, ShihabJsonCodec.Serialize(resObj));
                }
                else if (hasId)
                {
                    WriteJsonRpcResult(idRaw, "{}");
                }
            }
            return 0;
        }

        private static string BuildMcpToolsListJson()
        {
            return "{\"tools\":[" +
                "{\"name\":\"list_downloads\",\"description\":\"List all downloads in MHS Download Manager with live status, size, progress, and checksum.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{}}}," +
                "{\"name\":\"add_download\",\"description\":\"Add a new URL or Magnet link to MHS Download Manager.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"},\"fileName\":{\"type\":\"string\"},\"checksum\":{\"type\":\"string\"}},\"required\":[\"url\"]}}," +
                "{\"name\":\"pause_downloads\",\"description\":\"Pause a specific download by id or 'all'.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\"}}}}," +
                "{\"name\":\"resume_downloads\",\"description\":\"Resume/start a specific download by id or 'all'.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\"}}}}," +
                "{\"name\":\"remove_downloads\",\"description\":\"Remove a download by id (optionally deleteFile=true).\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\"},\"deleteFile\":{\"type\":\"boolean\"}},\"required\":[\"id\"]}}," +
                "{\"name\":\"start_queue\",\"description\":\"Start the Main Download Queue.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{}}}," +
                "{\"name\":\"probe_url\",\"description\":\"Probe a URL for final redirect target, Content-Length, filename, and HTTP Range support.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}}," +
                "{\"name\":\"video_info\",\"description\":\"Inspect media formats and title for a video URL via yt-dlp.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}}," +
                "{\"name\":\"playlist_info\",\"description\":\"Inspect playlist items for a media playlist URL via yt-dlp.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}}," +
                "{\"name\":\"torrent_info\",\"description\":\"Inspect a .torrent file, HTTP torrent URL, or magnet URI.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"target\":{\"type\":\"string\"}},\"required\":[\"target\"]}}," +
                "{\"name\":\"start_video\",\"description\":\"Queue a media/video download job via yt-dlp.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"},\"format\":{\"type\":\"string\"},\"audioOnly\":{\"type\":\"boolean\"}},\"required\":[\"url\"]}}," +
                "{\"name\":\"start_playlist\",\"description\":\"Queue a playlist download job via yt-dlp.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}}," +
                "{\"name\":\"start_gallery\",\"description\":\"Queue an image gallery download job via gallery-dl.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}}," +
                "{\"name\":\"start_torrent\",\"description\":\"Queue a BitTorrent .torrent or magnet download job.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"target\":{\"type\":\"string\"}},\"required\":[\"target\"]}}," +
                "{\"name\":\"check_updates\",\"description\":\"Check GitHub Releases for a newer version of MHS Download Manager.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{}}}" +
                "]}";
        }

        private static string ExecuteMcpToolCall(
            ShihabStateStore store,
            MehediAppConfiguration cfg,
            string toolName,
            Dictionary<string, object> args)
        {
            string liveResp;
            if (toolName == "list_downloads")
            {
                if (TryCallLiveBridge(store, cfg, "GET", "/shihab/api/status", null, out liveResp) && !string.IsNullOrWhiteSpace(liveResp))
                {
                    return liveResp;
                }
                List<ShihabDownloadItem> items = store.LoadMehediDownloads();
                if (items.Count == 0) return "No downloads in list.";
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < items.Count; i++)
                {
                    ShihabDownloadItem it = items[i];
                    sb.AppendFormat("[{0}] {1} | {2:0.0}% of {3} | Status: {4} | URL: {5}\n",
                        it.ShihabId.Substring(0, Math.Min(8, it.ShihabId.Length)),
                        it.ShihabFileName,
                        it.GetMehediPercent(),
                        ShihabFormatHelpers.FormatMehediBytes(it.ShihabTotalBytes),
                        it.ShihabStatusText,
                        it.MehediUrl);
                }
                return sb.ToString().TrimEnd();
            }
            if (toolName == "add_download" || toolName == "start_video" || toolName == "start_playlist" || toolName == "start_gallery" || toolName == "start_torrent")
            {
                string url = ShihabJsonCodec.GetString(args, "url");
                if (string.IsNullOrWhiteSpace(url)) url = ShihabJsonCodec.GetString(args, "target");
                string fileName = ShihabJsonCodec.GetString(args, "fileName");
                string checksum = ShihabJsonCodec.GetString(args, "checksum");
                string fmt = ShihabJsonCodec.GetString(args, "format");
                bool audioOnly = ShihabJsonCodec.GetBool(args, "audioOnly", false);
                if (string.IsNullOrWhiteSpace(url)) return "Error: url/target is required.";

                string kind = toolName == "start_video" || toolName == "start_playlist" ? "ytdlp"
                    : (toolName == "start_gallery" ? "gallery" : (toolName == "start_torrent" ? "torrent" : "http"));
                if (!IsAllowedTargetForKind(url.Trim(), kind, false))
                {
                    return "Error: invalid or unsupported target URL for " + toolName + ".";
                }

                QueueOrDispatchItem(store, cfg, url.Trim(), fileName, checksum, kind, fmt, audioOnly);
                return "Queued (" + kind + "): " + url.Trim();
            }
            if (toolName == "pause_downloads")
            {
                string id = ShihabJsonCodec.GetString(args, "id");
                if (string.IsNullOrWhiteSpace(id)) id = "all";
                Dictionary<string, object> payload = new Dictionary<string, object>();
                payload["action"] = "pause";
                payload["id"] = id;
                if (TryCallLiveBridge(store, cfg, "POST", "/shihab/api/action", ShihabJsonCodec.Serialize(payload), out liveResp))
                {
                    return "Paused download(s) via live engine: " + id;
                }
                List<ShihabDownloadItem> items = store.LoadMehediDownloads();
                int count = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    if (id == "all" || items[i].ShihabId.StartsWith(id, StringComparison.OrdinalIgnoreCase))
                    {
                        items[i].SetMehediStatusEnum(ShihabDownloadStatus.ShihabPaused, "Paused");
                        count++;
                    }
                }
                store.SaveShihabDownloads(items);
                return string.Format("Paused {0} download(s).", count);
            }
            if (toolName == "resume_downloads" || toolName == "start_queue")
            {
                string id = toolName == "start_queue" ? "all" : ShihabJsonCodec.GetString(args, "id");
                if (string.IsNullOrWhiteSpace(id)) id = "all";
                string actName = toolName == "start_queue" ? "start_queue" : "resume";
                Dictionary<string, object> payload = new Dictionary<string, object>();
                payload["action"] = actName;
                payload["id"] = id;
                if (TryCallLiveBridge(store, cfg, "POST", "/shihab/api/action", ShihabJsonCodec.Serialize(payload), out liveResp))
                {
                    return "Started/resumed via live engine: " + id;
                }
                List<ShihabDownloadItem> items = store.LoadMehediDownloads();
                int count = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].GetShihabStatusEnum() != ShihabDownloadStatus.ShihabCompleted &&
                        (id == "all" || items[i].ShihabId.StartsWith(id, StringComparison.OrdinalIgnoreCase)))
                    {
                        items[i].SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued");
                        count++;
                    }
                }
                store.SaveShihabDownloads(items);
                return string.Format("Queued {0} download(s) for resumption.", count);
            }
            if (toolName == "remove_downloads")
            {
                string id = ShihabJsonCodec.GetString(args, "id");
                bool delFile = ShihabJsonCodec.GetBool(args, "deleteFile", false);
                if (string.IsNullOrWhiteSpace(id)) return "Error: id is required.";
                Dictionary<string, object> payload = new Dictionary<string, object>();
                payload["action"] = delFile ? "delete" : "remove";
                payload["id"] = id;
                if (TryCallLiveBridge(store, cfg, "POST", "/shihab/api/action", ShihabJsonCodec.Serialize(payload), out liveResp))
                {
                    return (delFile ? "Deleted " : "Removed ") + id + " via live engine.";
                }
                List<ShihabDownloadItem> items = store.LoadMehediDownloads();
                int removed = 0;
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    if (items[i].ShihabId.StartsWith(id, StringComparison.OrdinalIgnoreCase))
                    {
                        if (delFile)
                        {
                            string fp = items[i].GetShihabFullTargetPath();
                            try { if (File.Exists(fp)) File.Delete(fp); } catch { }
                        }
                        items.RemoveAt(i);
                        removed++;
                    }
                }
                store.SaveShihabDownloads(items);
                return string.Format("Removed {0} download(s).", removed);
            }
            if (toolName == "probe_url")
            {
                string url = ShihabJsonCodec.GetString(args, "url");
                if (string.IsNullOrWhiteSpace(url)) return "Error: url is required.";
                return ProbeSingleUrlSummary(url.Trim(), cfg);
            }
            if (toolName == "video_info" || toolName == "playlist_info")
            {
                string url = ShihabJsonCodec.GetString(args, "url");
                if (string.IsNullOrWhiteSpace(url)) return "Error: url is required.";
                string infoJson, err;
                if (ShihabMediaHelpersManager.QueryVideoOrPlaylistInfo(store.MehediBaseDirectory, url.Trim(), toolName == "playlist_info", out infoJson, out err))
                {
                    return infoJson;
                }
                return "Error: " + err;
            }
            if (toolName == "torrent_info")
            {
                string target = ShihabJsonCodec.GetString(args, "target");
                if (string.IsNullOrWhiteSpace(target)) target = ShihabJsonCodec.GetString(args, "url");
                if (string.IsNullOrWhiteSpace(target)) return "Error: target is required.";
                try
                {
                    ShihabTorrentMetadata meta = ShihabTorrentManager.InspectTorrentOrMagnet(target.Trim(), cfg);
                    return string.Format("Name: {0}\nInfoHash: {1}\nTotalSize: {2}\nFiles: {3}",
                        meta.Name, meta.InfoHashHex, ShihabFormatHelpers.FormatMehediBytes(meta.TotalSizeBytes), meta.Files.Count);
                }
                catch (Exception ex)
                {
                    return "Error: " + ex.Message;
                }
            }
            if (toolName == "check_updates")
            {
                ShihabUpdateCheckResult res = ShihabAutoUpdater.CheckForUpdates(cfg, true);
                if (!res.Ok) return "Update check failed: " + res.ErrorMessage;
                if (res.UpdateAvailable)
                {
                    string assetUrl = res.PrimaryAsset != null ? res.PrimaryAsset.BrowserDownloadUrl : res.HtmlUrl;
                    return string.Format("Update available: v{0} (installed: v{1}) | URL: {2}",
                        res.LatestVersion, res.CurrentVersion, assetUrl);
                }
                return string.Format("Up to date (v{0}).", res.CurrentVersion);
            }
            return "Unknown tool: " + toolName;
        }

        private static bool IsAllowedTargetForKind(string target, string kind, bool allowLocalTorrentFile)
        {
            if (string.IsNullOrWhiteSpace(target)) return false;
            string s = target.Trim();
            Uri parsed;
            if (kind == "ytdlp" || kind == "gallery")
            {
                return ShihabProcessAndPathSafety.TryValidateHttpUrl(s, false, out parsed);
            }
            if (kind == "torrent" && allowLocalTorrentFile && s.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) && File.Exists(s))
            {
                return true;
            }
            if (s.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
            {
                return s.IndexOf("xt=urn:btih:", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            if (!Uri.TryCreate(s, UriKind.Absolute, out parsed)) return false;
            if (kind == "torrent")
            {
                return parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps;
            }
            return parsed.Scheme == Uri.UriSchemeHttp ||
                   parsed.Scheme == Uri.UriSchemeHttps ||
                   string.Equals(parsed.Scheme, "ftp", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(parsed.Scheme, "ftps", StringComparison.OrdinalIgnoreCase);
        }

        private static string ProbeSingleUrlSummary(string url, MehediAppConfiguration cfg)
        {
            try
            {
                string currentUrl = url;
                for (int hop = 0; hop <= MaxProbeRedirects; hop++)
                {
                    Uri parsed;
                    if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(currentUrl, false, out parsed))
                    {
                        return "Probe error: only HTTP and HTTPS URLs are permitted.";
                    }
                    if (ShihabProcessAndPathSafety.IsHostDisallowedForOutboundSsrf(parsed.Host, true))
                    {
                        return "Probe error: blocked outbound probe to internal/loopback host (" + parsed.Host + ").";
                    }

                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(parsed);
                    req.Method = "GET";
                    req.AllowAutoRedirect = false;
                    req.UserAgent = (cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabDefaultUserAgent)) ? cfg.ShihabDefaultUserAgent : "Mozilla/5.0";
                    req.Timeout = 10000;
                    req.ReadWriteTimeout = 10000;
                    req.AddRange(0, 0);
                    ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, parsed.AbsoluteUri);

                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    {
                        int code = (int)resp.StatusCode;
                        if (code == 301 || code == 302 || code == 303 || code == 307 || code == 308)
                        {
                            string loc = resp.Headers["Location"];
                            if (string.IsNullOrWhiteSpace(loc) || hop == MaxProbeRedirects)
                            {
                                return "Probe error: excessive or empty HTTP redirects.";
                            }
                            Uri nextUri;
                            if (!Uri.TryCreate(parsed, loc, out nextUri))
                            {
                                return "Probe error: malformed HTTP redirect target.";
                            }
                            currentUrl = nextUri.AbsoluteUri;
                            continue;
                        }

                        long total = resp.ContentLength;
                        string cr = resp.Headers["Content-Range"];
                        if (!string.IsNullOrEmpty(cr))
                        {
                            int slash = cr.LastIndexOf('/');
                            long parsedTotal;
                            if (slash >= 0 && long.TryParse(cr.Substring(slash + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedTotal))
                            {
                                total = parsedTotal;
                            }
                        }
                        bool resumable = resp.StatusCode == HttpStatusCode.PartialContent ||
                            string.Equals(resp.Headers["Accept-Ranges"], "bytes", StringComparison.OrdinalIgnoreCase);
                        string finalUrl = resp.ResponseUri != null ? resp.ResponseUri.ToString() : parsed.AbsoluteUri;
                        string name = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(finalUrl);
                        return string.Format("FinalURL: {0}\nFileName: {1}\nSize: {2} ({3} bytes)\nResumable: {4}\nContentType: {5}",
                            finalUrl, name, ShihabFormatHelpers.FormatMehediBytes(total), total, resumable, resp.ContentType ?? "unknown");
                    }
                }
                return "Probe error: exceeded redirect limit.";
            }
            catch (Exception ex)
            {
                return "Probe error: " + ex.Message;
            }
        }

        private static void QueueOrDispatchItem(
            ShihabStateStore store,
            MehediAppConfiguration cfg,
            string targetUrl,
            string fileName,
            string checksum,
            string kind,
            string ytFormat,
            bool audioOnly)
        {
            string liveResp;
            Dictionary<string, object> payloadObj = new Dictionary<string, object>();
            payloadObj["url"] = targetUrl ?? "";
            payloadObj["fileName"] = fileName ?? "";
            payloadObj["checksum"] = checksum ?? "";
            payloadObj["kind"] = kind ?? "http";
            payloadObj["format"] = ytFormat ?? "";
            payloadObj["audioOnly"] = audioOnly;

            if (TryCallLiveBridge(store, cfg, "POST", "/shihab/api/add", ShihabJsonCodec.Serialize(payloadObj), out liveResp))
            {
                return;
            }

            List<ShihabDownloadItem> items = store.LoadMehediDownloads();
            ShihabDownloadItem item = new ShihabDownloadItem();
            item.MehediUrl = targetUrl;
            item.ShihabFileName = string.IsNullOrWhiteSpace(fileName)
                ? ShihabFormatHelpers.ExtractMehediFileNameFromUrl(targetUrl)
                : ShihabFormatHelpers.SanitizeShihabFileName(fileName);
            item.ShihabExpectedChecksum = checksum ?? "";
            if (kind == "ytdlp")
            {
                item.ShihabIsYtDlpJob = true;
                item.ShihabYtDlpFormatSelector = ytFormat ?? "";
                item.ShihabAudioOnlyExtract = audioOnly;
            }
            else if (kind == "gallery")
            {
                item.ShihabIsGalleryJob = true;
            }
            else if (kind == "torrent" || targetUrl.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) || targetUrl.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
            {
                item.ShihabIsTorrentOrMagnet = true;
            }
            ShihabCategoryRule cat = cfg.ResolveMehediCategoryForFile(item.ShihabFileName);
            item.ShihabCategoryName = cat.ShihabName;
            item.MehediSaveDirectory = cat.ShihabDefaultFolder;
            items.Insert(0, item);
            store.SaveShihabDownloads(items);
        }

        private static void WriteJsonRpcResult(string idRaw, string resultObjJson)
        {
            Console.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":" + idRaw + ",\"result\":" + resultObjJson + "}");
            Console.Out.Flush();
        }

        private static void WriteJsonRpcError(string idRaw, int code, string message)
        {
            Dictionary<string, object> errObj = new Dictionary<string, object>();
            errObj["code"] = code;
            errObj["message"] = message ?? "Error";
            Console.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":" + idRaw + ",\"error\":" + ShihabJsonCodec.Serialize(errObj) + "}");
            Console.Out.Flush();
        }

        public static int RunCliCommand(ShihabStateStore store, string[] args)
        {
            if (args == null || args.Length == 0) return 0;

            bool outputJson = false;
            List<string> cleaned = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--json", StringComparison.OrdinalIgnoreCase))
                {
                    outputJson = true;
                    continue;
                }
                if (string.Equals(args[i], "--shihab-config-dir", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    continue;
                }
                if (string.Equals(args[i], "--cli", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                cleaned.Add(args[i]);
            }

            if (cleaned.Count == 0)
            {
                PrintCliHelp();
                return 0;
            }

            string cmd = cleaned[0].ToLowerInvariant();
            MehediAppConfiguration cfg = store.LoadShihabConfig();

            if (cmd == "help" || cmd == "--help" || cmd == "-h")
            {
                PrintCliHelp();
                return 0;
            }
            if (cmd == "status" || cmd == "list")
            {
                string liveResp;
                if (TryCallLiveBridge(store, cfg, "GET", "/shihab/api/status", null, out liveResp) && !string.IsNullOrWhiteSpace(liveResp))
                {
                    if (outputJson)
                    {
                        Console.WriteLine(liveResp);
                        return 0;
                    }
                }
                List<ShihabDownloadItem> items = store.LoadMehediDownloads();
                if (outputJson)
                {
                    List<object> arr = new List<object>(items.Count);
                    for (int i = 0; i < items.Count; i++)
                    {
                        ShihabDownloadItem it = items[i];
                        Dictionary<string, object> row = new Dictionary<string, object>();
                        row["id"] = it.ShihabId ?? "";
                        row["fileName"] = it.ShihabFileName ?? "";
                        row["status"] = it.ShihabStatusText ?? "";
                        row["percent"] = Math.Round(it.GetMehediPercent(), 1);
                        row["totalBytes"] = it.ShihabTotalBytes;
                        row["downloadedBytes"] = it.MehediDownloadedBytes;
                        row["url"] = it.MehediUrl ?? "";
                        arr.Add(row);
                    }
                    Console.WriteLine(ShihabJsonCodec.Serialize(arr));
                    return 0;
                }
                Console.WriteLine("ID        | Status       | Progress | Size       | File Name");
                Console.WriteLine("----------+--------------+----------+------------+------------------------------");
                for (int i = 0; i < items.Count; i++)
                {
                    ShihabDownloadItem it = items[i];
                    string shortId = it.ShihabId.Substring(0, Math.Min(8, it.ShihabId.Length));
                    Console.WriteLine("{0,-9} | {1,-12} | {2,6:0.0}%  | {3,-10} | {4}",
                        shortId,
                        it.ShihabStatusText,
                        it.GetMehediPercent(),
                        ShihabFormatHelpers.FormatMehediBytes(it.ShihabTotalBytes),
                        it.ShihabFileName);
                }
                return 0;
            }
            if (cmd == "pair")
            {
                string code = ShihabRemoteControlServer.EnsureFreshPairingCode(cfg, delegate { store.SaveMehediConfig(cfg); }, false);
                string lanIp = ShihabRemoteControlServer.GetPrimaryLanIpAddress();
                string fullUrl = string.Format("http://{0}:{1}/#pair={2}", lanIp, cfg.ShihabRemoteWebPort, code);
                if (outputJson)
                {
                    Dictionary<string, object> pairObj = new Dictionary<string, object>();
                    pairObj["url"] = fullUrl;
                    pairObj["code"] = code;
                    pairObj["expiresUtc"] = cfg.ShihabRemotePairingExpiresUtc ?? "";
                    Console.WriteLine(ShihabJsonCodec.Serialize(pairObj));
                    return 0;
                }
                Console.WriteLine("Remote Web UI URL : {0}", fullUrl);
                Console.WriteLine("8-Digit Pair Code : {0} (single-use, expires in 10 min)", code);
                Console.WriteLine();
                Console.WriteLine(ShihabQrCodeEncoder.RenderTerminalUnicode(fullUrl));
                return 0;
            }
            if (cmd == "api")
            {
                if (cleaned.Count > 1)
                {
                    string mode = cleaned[1].ToLowerInvariant();
                    cfg.ShihabAutomationApiEnabled = (mode == "on" || mode == "true" || mode == "1");
                    store.SaveMehediConfig(cfg);
                }
                if (outputJson)
                {
                    Dictionary<string, object> apiObj = new Dictionary<string, object>();
                    apiObj["automationApiEnabled"] = cfg.ShihabAutomationApiEnabled;
                    apiObj["port"] = cfg.MehediLocalBridgePort;
                    Console.WriteLine(ShihabJsonCodec.Serialize(apiObj));
                }
                else
                {
                    Console.WriteLine("Local Automation API: {0}", cfg.ShihabAutomationApiEnabled ? "ON" : "OFF");
                }
                return 0;
            }
            if (cmd == "check-update" || cmd == "check-updates")
            {
                ShihabUpdateCheckResult res = ShihabAutoUpdater.CheckForUpdates(cfg, true);
                cfg.ShihabLastUpdateCheckUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                store.SaveMehediConfig(cfg);
                if (!res.Ok)
                {
                    if (outputJson)
                    {
                        Dictionary<string, object> errObj = new Dictionary<string, object>();
                        errObj["ok"] = false;
                        errObj["error"] = res.ErrorMessage ?? "";
                        Console.WriteLine(ShihabJsonCodec.Serialize(errObj));
                    }
                    else
                    {
                        Console.Error.WriteLine("Update check failed: " + res.ErrorMessage);
                    }
                    return 1;
                }
                if (outputJson)
                {
                    string assetUrl = res.PrimaryAsset != null ? res.PrimaryAsset.BrowserDownloadUrl : "";
                    string assetName = res.PrimaryAsset != null ? res.PrimaryAsset.Name : "";
                    Dictionary<string, object> updObj = new Dictionary<string, object>();
                    updObj["ok"] = true;
                    updObj["updateAvailable"] = res.UpdateAvailable;
                    updObj["currentVersion"] = res.CurrentVersion ?? "";
                    updObj["latestVersion"] = res.LatestVersion ?? "";
                    updObj["installKind"] = res.InstallMode.ToString();
                    updObj["assetName"] = assetName ?? "";
                    updObj["assetUrl"] = assetUrl ?? "";
                    Console.WriteLine(ShihabJsonCodec.Serialize(updObj));
                    return 0;
                }
                if (res.UpdateAvailable)
                {
                    string assetName = res.PrimaryAsset != null ? res.PrimaryAsset.Name : "N/A";
                    long assetSize = res.PrimaryAsset != null ? res.PrimaryAsset.Size : 0L;
                    Console.WriteLine("Update Available : v{0} (installed: v{1})", res.LatestVersion, res.CurrentVersion);
                    Console.WriteLine("Install Mode     : {0}", res.InstallMode);
                    Console.WriteLine("Package Asset    : {0} ({1})", assetName, ShihabFormatHelpers.FormatMehediBytes(assetSize));
                    Console.WriteLine("Release Page     : {0}", res.HtmlUrl);
                }
                else
                {
                    Console.WriteLine("MHS Download Manager v{0} is up to date.", res.CurrentVersion);
                }
                return 0;
            }
            if (cmd == "update")
            {
                bool verifyOnly = cleaned.Contains("--verify-only");
                ShihabUpdateCheckResult res = ShihabAutoUpdater.CheckForUpdates(cfg, true);
                if (!res.Ok)
                {
                    Console.Error.WriteLine("Update check failed: " + res.ErrorMessage);
                    return 1;
                }
                if (!res.UpdateAvailable)
                {
                    Console.WriteLine("Already running latest version v{0}.", res.CurrentVersion);
                    return 0;
                }
                string assetName = res.PrimaryAsset != null ? res.PrimaryAsset.Name : "update";
                Console.WriteLine("Downloading update v{0} ({1})...", res.LatestVersion, assetName);
                string verifiedPrimary, verifiedExtZip, verifyErr;
                bool verifiedOk = ShihabAutoUpdater.DownloadAndVerifyReleaseUpdate(
                    res,
                    cfg,
                    store.MehediBaseDirectory,
                    null,
                    CancellationToken.None,
                    out verifiedPrimary,
                    out verifiedExtZip,
                    out verifyErr);
                if (!verifiedOk)
                {
                    Console.Error.WriteLine("Update verification failed: " + verifyErr);
                    return 2;
                }
                Console.WriteLine("SHA-256 Verified : OK (ChecksumAsset={0})", res.Sha256SumsAsset != null ? res.Sha256SumsAsset.Name : "inline/none");
                Console.WriteLine("Staged Package   : {0}", verifiedPrimary);
                if (verifyOnly)
                {
                    return 0;
                }
                string applyErr;
                if (!ShihabAutoUpdater.ApplyVerifiedUpdateAndRestart(res, verifiedPrimary, verifiedExtZip, null, out applyErr))
                {
                    Console.Error.WriteLine("Apply update failed: " + applyErr);
                    return 3;
                }
                Console.WriteLine("Update installer launched successfully.");
                return 0;
            }
            if ((cmd == "video-info" || cmd == "playlist-info") && cleaned.Count > 1)
            {
                string infoJson, err;
                if (!ShihabMediaHelpersManager.QueryVideoOrPlaylistInfo(store.MehediBaseDirectory, cleaned[1], cmd == "playlist-info", out infoJson, out err))
                {
                    Console.Error.WriteLine(err);
                    return 1;
                }
                Console.WriteLine(infoJson);
                return 0;
            }
            if (cmd == "torrent-info" && cleaned.Count > 1)
            {
                string path = cleaned[1];
                ShihabTorrentMetadata meta = ShihabTorrentManager.InspectTorrentOrMagnet(path, cfg);
                if (outputJson)
                {
                    Dictionary<string, object> torObj = new Dictionary<string, object>();
                    torObj["name"] = meta.Name ?? "";
                    torObj["infoHash"] = meta.InfoHashHex ?? "";
                    torObj["totalBytes"] = meta.TotalSizeBytes;
                    torObj["fileCount"] = meta.Files.Count;
                    Console.WriteLine(ShihabJsonCodec.Serialize(torObj));
                    return 0;
                }
                Console.WriteLine("Name      : {0}", meta.Name);
                Console.WriteLine("InfoHash  : {0}", meta.InfoHashHex);
                Console.WriteLine("TotalSize : {0}", ShihabFormatHelpers.FormatMehediBytes(meta.TotalSizeBytes));
                Console.WriteLine("Files     : {0}", meta.Files.Count);
                for (int i = 0; i < meta.Files.Count; i++)
                {
                    Console.WriteLine("  [{0}] {1} ({2})", meta.Files[i].Index, meta.Files[i].RelativePath, ShihabFormatHelpers.FormatMehediBytes(meta.Files[i].SizeBytes));
                }
                return 0;
            }
            if (cmd == "torrent-create" && cleaned.Count > 1)
            {
                string src = cleaned[1];
                string outFile = (cleaned.Count > 2) ? cleaned[2] : (src.TrimEnd('\\', '/') + ".torrent");
                try
                {
                    ShihabTorrentManager.CreateTorrentFile(src, outFile, new List<string>(), new List<string>(), "Created by MHS Download Manager", 262144, false);
                    Console.WriteLine("Created torrent: {0}", Path.GetFullPath(outFile));
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("Failed to create torrent: " + ex.Message);
                    return 1;
                }
            }
            if (cmd == "install-ytdlp" || cmd == "install-ffmpeg" || cmd == "install-gallery-dl")
            {
                string tool = cmd.Substring("install-".Length);
                string err;
                bool ok = ShihabMediaHelpersManager.InstallOrUpdateTool(store.MehediBaseDirectory, tool, Console.WriteLine, out err);
                if (!ok) { Console.Error.WriteLine(err); return 1; }
                return 0;
            }
            if ((cmd == "get" || cmd == "video-get" || cmd == "playlist-get" || cmd == "gallery-get" || cmd == "torrent-get") && cleaned.Count > 1)
            {
                string targetUrl = cleaned[1].Trim();
                string kind = (cmd == "video-get" || cmd == "playlist-get") ? "ytdlp"
                    : (cmd == "gallery-get" ? "gallery" : (cmd == "torrent-get" ? "torrent" : "http"));
                if (!IsAllowedTargetForKind(targetUrl, kind, true))
                {
                    Console.Error.WriteLine("Invalid or unsupported download target for " + cmd + ".");
                    return 1;
                }
                QueueOrDispatchItem(store, cfg, targetUrl, "", "", kind, "", false);
                Console.WriteLine("Queued ({0}): {1}", kind, targetUrl);
                return 0;
            }
            if ((cmd == "pause" || cmd == "start" || cmd == "resume" || cmd == "remove" || cmd == "delete") && cleaned.Count > 1)
            {
                string targetId = cleaned[1];
                string liveResp;
                Dictionary<string, object> actObj = new Dictionary<string, object>();
                actObj["action"] = cmd;
                actObj["id"] = targetId;
                if (TryCallLiveBridge(store, cfg, "POST", "/shihab/api/action", ShihabJsonCodec.Serialize(actObj), out liveResp))
                {
                    Console.WriteLine("Completed live command '{0}' on '{1}'.", cmd, targetId);
                    return 0;
                }

                List<ShihabDownloadItem> items = store.LoadMehediDownloads();
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    if (targetId == "all" || items[i].ShihabId.StartsWith(targetId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (cmd == "pause") items[i].SetMehediStatusEnum(ShihabDownloadStatus.ShihabPaused, "Paused");
                        else if (cmd == "start" || cmd == "resume") items[i].SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued");
                        else if (cmd == "remove" || cmd == "delete")
                        {
                            if (cmd == "delete")
                            {
                                string fp = items[i].GetShihabFullTargetPath();
                                try { if (File.Exists(fp)) File.Delete(fp); } catch { }
                            }
                            items.RemoveAt(i);
                        }
                    }
                }
                store.SaveShihabDownloads(items);
                Console.WriteLine("Completed command '{0}' on '{1}'.", cmd, targetId);
                return 0;
            }

            PrintCliHelp();
            return 0;
        }

        private static void PrintCliHelp()
        {
            Console.WriteLine("MHS Download Manager CLI & Automation Commands:");
            Console.WriteLine("  mdm.exe --cli status [--json]                 List all downloads and live progress");
            Console.WriteLine("  mdm.exe --cli get <URL>                       Add a HTTP/HTTPS/FTP/Magnet download");
            Console.WriteLine("  mdm.exe --cli pause <id|all>                  Pause download(s)");
            Console.WriteLine("  mdm.exe --cli start <id|all>                  Resume/start download(s)");
            Console.WriteLine("  mdm.exe --cli remove <id|all>                 Remove download(s) from list");
            Console.WriteLine("  mdm.exe --cli delete <id|all>                 Remove download(s) and delete local file");
            Console.WriteLine("  mdm.exe --cli check-update [--json]           Check GitHub Releases for new version");
            Console.WriteLine("  mdm.exe --cli update [--verify-only]          Download, SHA-256 verify, and apply update");
            Console.WriteLine("  mdm.exe --cli pair [--json]                   Print LAN Remote Web UI URL, code & QR");
            Console.WriteLine("  mdm.exe --cli api <on|off>                    Enable or disable local automation API");
            Console.WriteLine("  mdm.exe --cli video-info <URL>                Query video formats via yt-dlp");
            Console.WriteLine("  mdm.exe --cli playlist-info <URL>             Query playlist entries via yt-dlp");
            Console.WriteLine("  mdm.exe --cli video-get <URL>                 Download video via yt-dlp");
            Console.WriteLine("  mdm.exe --cli gallery-get <URL>               Download image gallery via gallery-dl");
            Console.WriteLine("  mdm.exe --cli torrent-info <file|magnet|url>  Inspect .torrent or magnet metadata");
            Console.WriteLine("  mdm.exe --cli torrent-create <src> [out]      Create a .torrent file from file or folder");
            Console.WriteLine("  mdm.exe --cli install-ytdlp                   Install/update yt-dlp helper (SHA-256 verified)");
            Console.WriteLine("  mdm.exe --cli install-ffmpeg                  Install/update ffmpeg helper (SHA-256 verified)");
            Console.WriteLine("  mdm.exe --cli install-gallery-dl              Install/update gallery-dl helper (SHA-256 verified)");
            Console.WriteLine("  mdm.exe --mcp                                 Run Model Context Protocol stdio server");
            Console.WriteLine("  mdm.exe --headless                            Run background download daemon without GUI");
            Console.WriteLine("  mdm.exe --forget-sign-ins                     Wipe all saved credentials & tokens");
        }
    }
}

