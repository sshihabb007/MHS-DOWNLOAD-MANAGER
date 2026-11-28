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
                "{\"name\":\"list_downloads\",\"description\":\"List downloads in MHS Download Manager with live status, size, progress, checksum, category, queue, and free disk space. Supports optional filtering.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\"},\"status\":{\"type\":\"string\"},\"search\":{\"type\":\"string\"},\"category\":{\"type\":\"string\"},\"queue\":{\"type\":\"string\"},\"limit\":{\"type\":\"integer\"}}}}," +
                "{\"name\":\"add_download\",\"description\":\"Add a new URL, Magnet link, or local .torrent file to MHS Download Manager.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"},\"fileName\":{\"type\":\"string\"},\"checksum\":{\"type\":\"string\"},\"saveDir\":{\"type\":\"string\"},\"category\":{\"type\":\"string\"},\"queue\":{\"type\":\"string\"},\"private\":{\"type\":\"boolean\"},\"cookie\":{\"type\":\"string\"},\"referer\":{\"type\":\"string\"},\"userAgent\":{\"type\":\"string\"},\"limitKb\":{\"type\":\"integer\"},\"start\":{\"type\":\"boolean\"},\"deferredRename\":{\"type\":\"string\"}},\"required\":[\"url\"]}}," +
                "{\"name\":\"pause_downloads\",\"description\":\"Pause a specific download by id or 'all'.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\"}}}}," +
                "{\"name\":\"resume_downloads\",\"description\":\"Resume/start a specific download by id or 'all'.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\"}}}}," +
                "{\"name\":\"remove_downloads\",\"description\":\"Remove a download by id (optionally deleteFile=true).\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\"},\"deleteFile\":{\"type\":\"boolean\"}},\"required\":[\"id\"]}}," +
                "{\"name\":\"start_queue\",\"description\":\"Start a download queue by name (defaults to Main Download Queue).\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"queue\":{\"type\":\"string\"}}}}," +
                "{\"name\":\"stop_queue\",\"description\":\"Stop a running download queue by name (defaults to Main Download Queue, or 'all').\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"queue\":{\"type\":\"string\"}}}}," +
                "{\"name\":\"probe_url\",\"description\":\"Probe a URL for final redirect target, Content-Length, filename, and HTTP Range support.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}}," +
                "{\"name\":\"video_info\",\"description\":\"Inspect media formats and title for a video URL via yt-dlp.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}}," +
                "{\"name\":\"playlist_info\",\"description\":\"Inspect playlist items for a media playlist URL via yt-dlp.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}}," +
                "{\"name\":\"torrent_info\",\"description\":\"Inspect a .torrent file, HTTP torrent URL, or magnet URI.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"target\":{\"type\":\"string\"}},\"required\":[\"target\"]}}," +
                "{\"name\":\"start_video\",\"description\":\"Queue a media/video download job via yt-dlp (format='compatible' selects H.264+AAC).\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"},\"format\":{\"type\":\"string\"},\"audioOnly\":{\"type\":\"boolean\"}},\"required\":[\"url\"]}}," +
                "{\"name\":\"start_playlist\",\"description\":\"Queue a playlist download job via yt-dlp.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}}," +
                "{\"name\":\"start_gallery\",\"description\":\"Queue an image gallery download job via gallery-dl.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}}," +
                "{\"name\":\"start_torrent\",\"description\":\"Queue a BitTorrent .torrent or magnet download job.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"target\":{\"type\":\"string\"}},\"required\":[\"target\"]}}," +
                "{\"name\":\"check_updates\",\"description\":\"Check GitHub Releases for a newer version of MHS Download Manager.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{}}}" +
                "]}";
        }

        private static string BuildStatusQueryString(string id, string status, string search, string category, string queue, int limit)
        {
            List<string> q = new List<string>();
            if (!string.IsNullOrWhiteSpace(id)) q.Add("id=" + Uri.EscapeDataString(id.Trim()));
            if (!string.IsNullOrWhiteSpace(status)) q.Add("status=" + Uri.EscapeDataString(status.Trim()));
            if (!string.IsNullOrWhiteSpace(search)) q.Add("search=" + Uri.EscapeDataString(search.Trim()));
            if (!string.IsNullOrWhiteSpace(category)) q.Add("category=" + Uri.EscapeDataString(category.Trim()));
            if (!string.IsNullOrWhiteSpace(queue)) q.Add("queue=" + Uri.EscapeDataString(queue.Trim()));
            if (limit > 0) q.Add("limit=" + limit.ToString(CultureInfo.InvariantCulture));
            return q.Count > 0 ? ("/shihab/api/status?" + string.Join("&", q.ToArray())) : "/shihab/api/status";
        }

        private static List<ShihabDownloadItem> FilterOfflineItems(
            List<ShihabDownloadItem> items,
            string id,
            string status,
            string search,
            string category,
            string queue,
            int limit)
        {
            List<ShihabDownloadItem> filtered = new List<ShihabDownloadItem>();
            if (items == null) return filtered;
            for (int i = 0; i < items.Count; i++)
            {
                ShihabDownloadItem it = items[i];
                if (it == null) continue;
                if (!string.IsNullOrWhiteSpace(id) && !it.ShihabId.StartsWith(id.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrWhiteSpace(status) && (it.ShihabStatusText ?? "").IndexOf(status.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!string.IsNullOrWhiteSpace(category) && !string.Equals(it.ShihabCategoryName ?? "", category.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrWhiteSpace(queue) && !string.Equals(it.MehediQueueName ?? "", queue.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrWhiteSpace(search) &&
                    (it.ShihabFileName ?? "").IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) < 0 &&
                    (it.MehediUrl ?? "").IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                filtered.Add(it);
                if (limit > 0 && filtered.Count >= limit) break;
            }
            return filtered;
        }

        private static long TryGetFreeDiskBytesSafe(string folder)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folder)) folder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string root = Path.GetPathRoot(Path.GetFullPath(folder));
                if (string.IsNullOrEmpty(root)) return -1L;
                DriveInfo di = new DriveInfo(root);
                return di.IsReady ? di.AvailableFreeSpace : -1L;
            }
            catch { return -1L; }
        }

        private static bool MatchesDownloadIdStrict(string candidateId, string requestedId, bool allowAllWildcard)
        {
            if (string.IsNullOrEmpty(candidateId) || string.IsNullOrWhiteSpace(requestedId)) return false;
            string trimmed = requestedId.Trim();
            if (allowAllWildcard && string.Equals(trimmed, "all", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(candidateId, trimmed, StringComparison.OrdinalIgnoreCase)) return true;
            return trimmed.Length >= 8 && candidateId.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase);
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
                string fId = ShihabJsonCodec.GetString(args, "id");
                string fStatus = ShihabJsonCodec.GetString(args, "status");
                string fSearch = ShihabJsonCodec.GetString(args, "search");
                string fCat = ShihabJsonCodec.GetString(args, "category");
                string fQueue = ShihabJsonCodec.GetString(args, "queue");
                int fLimit = (int)ShihabJsonCodec.GetLong(args, "limit", 0);
                string statusPath = BuildStatusQueryString(fId, fStatus, fSearch, fCat, fQueue, fLimit);
                if (TryCallLiveBridge(store, cfg, "GET", statusPath, null, out liveResp) && !string.IsNullOrWhiteSpace(liveResp))
                {
                    return liveResp;
                }
                List<ShihabDownloadItem> items = FilterOfflineItems(store.LoadMehediDownloads(), fId, fStatus, fSearch, fCat, fQueue, fLimit);
                long freeBytes = TryGetFreeDiskBytesSafe(cfg != null && cfg.ShihabCategories != null && cfg.ShihabCategories.Count > 0 ? cfg.ShihabCategories[0].ShihabDefaultFolder : "");
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
                    row["category"] = it.ShihabCategoryName ?? "";
                    row["queue"] = it.MehediQueueName ?? "";
                    row["url"] = it.MehediUrl ?? "";
                    row["private"] = it.ShihabIsPrivateIncognito;
                    row["freeSpaceBytes"] = TryGetFreeDiskBytesSafe(it.MehediSaveDirectory) >= 0 ? TryGetFreeDiskBytesSafe(it.MehediSaveDirectory) : freeBytes;
                    arr.Add(row);
                }
                return ShihabJsonCodec.Serialize(arr);
            }
            if (toolName == "add_download" || toolName == "start_video" || toolName == "start_playlist" || toolName == "start_gallery" || toolName == "start_torrent")
            {
                string url = ShihabJsonCodec.GetString(args, "url");
                if (string.IsNullOrWhiteSpace(url)) url = ShihabJsonCodec.GetString(args, "target");
                string fileName = ShihabJsonCodec.GetString(args, "fileName");
                string checksum = ShihabJsonCodec.GetString(args, "checksum");
                string fmt = ShihabJsonCodec.GetString(args, "format");
                bool audioOnly = ShihabJsonCodec.GetBool(args, "audioOnly", false);
                bool isPriv = ShihabJsonCodec.GetBool(args, "private", false) || ShihabJsonCodec.GetBool(args, "incognito", false);
                string reqQueue = ShihabJsonCodec.GetString(args, "queue");
                string reqCat = ShihabJsonCodec.GetString(args, "category");
                string reqSaveDir = ShihabJsonCodec.GetString(args, "saveDir");
                string reqCookie = ShihabJsonCodec.GetString(args, "cookie");
                string reqReferer = ShihabJsonCodec.GetString(args, "referer");
                string reqUa = ShihabJsonCodec.GetString(args, "userAgent");
                string reqDefRename = ShihabJsonCodec.GetString(args, "deferredRename");
                int reqLimitKb = (int)ShihabJsonCodec.GetLong(args, "limitKb", 0);
                bool startNow = ShihabJsonCodec.GetBool(args, "start", true);
                if (string.IsNullOrWhiteSpace(url)) return "Error: url/target is required.";

                string kind = toolName == "start_video" || toolName == "start_playlist" ? "ytdlp"
                    : (toolName == "start_gallery" ? "gallery" : (toolName == "start_torrent" ? "torrent" : "http"));
                if (kind == "http" && ShihabTorrentManager.IsMagnetOrTorrentInput(url.Trim()))
                {
                    kind = "torrent";
                }
                if (!IsAllowedTargetForKind(url.Trim(), kind, true))
                {
                    return "Error: invalid or unsupported target URL for " + toolName + ".";
                }

                QueueOrDispatchItem(
                    store, cfg, url.Trim(), fileName, checksum, kind, fmt, audioOnly,
                    isPriv, reqQueue, reqCat, reqSaveDir, reqCookie, reqReferer, reqUa, reqLimitKb, startNow, reqDefRename);
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
                    if (MatchesDownloadIdStrict(items[i].ShihabId, id, true))
                    {
                        items[i].SetMehediStatusEnum(ShihabDownloadStatus.ShihabPaused, "Paused");
                        count++;
                    }
                }
                store.SaveShihabDownloads(items, cfg != null && cfg.ShihabKeepPrivateDownloads);
                return string.Format("Paused {0} download(s).", count);
            }
            if (toolName == "resume_downloads" || toolName == "start_queue" || toolName == "stop_queue")
            {
                string queueName = ShihabJsonCodec.GetString(args, "queue");
                if (string.IsNullOrWhiteSpace(queueName)) queueName = "Main Download Queue";
                string id = (toolName == "start_queue" || toolName == "stop_queue") ? "all" : ShihabJsonCodec.GetString(args, "id");
                if (string.IsNullOrWhiteSpace(id)) id = "all";
                string actName = toolName == "start_queue" ? "start_queue" : (toolName == "stop_queue" ? "stop_queue" : "resume");
                Dictionary<string, object> payload = new Dictionary<string, object>();
                payload["action"] = actName;
                payload["id"] = id;
                payload["queue"] = queueName;
                if (TryCallLiveBridge(store, cfg, "POST", "/shihab/api/action", ShihabJsonCodec.Serialize(payload), out liveResp))
                {
                    return toolName == "stop_queue"
                        ? ("Stopped queue via live engine: " + queueName)
                        : ("Started/resumed via live engine: " + (toolName == "start_queue" ? queueName : id));
                }
                List<ShihabDownloadItem> items = store.LoadMehediDownloads();
                int count = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].GetShihabStatusEnum() == ShihabDownloadStatus.ShihabCompleted) continue;
                    bool matches = (toolName == "start_queue" || toolName == "stop_queue")
                        ? (string.Equals(queueName, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(items[i].MehediQueueName ?? "", queueName, StringComparison.OrdinalIgnoreCase))
                        : MatchesDownloadIdStrict(items[i].ShihabId, id, true);
                    if (matches)
                    {
                        if (toolName == "stop_queue") items[i].SetMehediStatusEnum(ShihabDownloadStatus.ShihabPaused, "Paused");
                        else items[i].SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued");
                        count++;
                    }
                }
                store.SaveShihabDownloads(items, cfg != null && cfg.ShihabKeepPrivateDownloads);
                return toolName == "stop_queue"
                    ? string.Format("Stopped queue '{0}' ({1} item(s)).", queueName, count)
                    : string.Format("Queued {0} download(s) for resumption.", count);
            }
            if (toolName == "remove_downloads")
            {
                string id = ShihabJsonCodec.GetString(args, "id");
                bool delFile = ShihabJsonCodec.GetBool(args, "deleteFile", false);
                if (string.IsNullOrWhiteSpace(id)) return "Error: id is required.";
                if (delFile && string.Equals(id.Trim(), "all", StringComparison.OrdinalIgnoreCase))
                {
                    return "Error: explicit download IDs are required for destructive disk deletion; wildcard 'all' is disallowed when deleteFile=true.";
                }
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
                    if (MatchesDownloadIdStrict(items[i].ShihabId, id, !delFile))
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
            if (kind == "torrent" && allowLocalTorrentFile)
            {
                string validLocalTor;
                if (ShihabProcessAndPathSafety.TryValidateLocalTorrentFilePath(s, out validLocalTor))
                {
                    return true;
                }
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
            bool audioOnly,
            bool isPrivate = false,
            string queueName = "",
            string categoryName = "",
            string saveDir = "",
            string cookie = "",
            string referer = "",
            string userAgent = "",
            int limitKb = 0,
            bool startNow = true,
            string deferredRename = "")
        {
            string liveResp;
            Dictionary<string, object> payloadObj = new Dictionary<string, object>();
            payloadObj["url"] = targetUrl ?? "";
            payloadObj["fileName"] = fileName ?? "";
            payloadObj["checksum"] = checksum ?? "";
            payloadObj["kind"] = kind ?? "http";
            payloadObj["format"] = ytFormat ?? "";
            payloadObj["audioOnly"] = audioOnly;
            payloadObj["private"] = isPrivate;
            payloadObj["incognito"] = isPrivate;
            if (!string.IsNullOrWhiteSpace(queueName)) payloadObj["queue"] = queueName.Trim();
            if (!string.IsNullOrWhiteSpace(categoryName)) payloadObj["category"] = categoryName.Trim();
            if (!string.IsNullOrWhiteSpace(saveDir)) payloadObj["saveDir"] = saveDir.Trim();
            if (!string.IsNullOrWhiteSpace(cookie)) payloadObj["cookie"] = cookie;
            if (!string.IsNullOrWhiteSpace(referer)) payloadObj["referer"] = referer;
            if (!string.IsNullOrWhiteSpace(userAgent)) payloadObj["userAgent"] = userAgent;
            if (!string.IsNullOrWhiteSpace(deferredRename)) payloadObj["deferredRename"] = deferredRename.Trim();
            if (limitKb > 0) payloadObj["limitKb"] = limitKb;
            payloadObj["start"] = startNow;

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
            item.ShihabIsPrivateIncognito = isPrivate;
            if (!string.IsNullOrWhiteSpace(cookie)) item.ShihabCookies = cookie;
            if (!string.IsNullOrWhiteSpace(referer)) item.MehediReferer = referer;
            if (!string.IsNullOrWhiteSpace(userAgent)) item.MehediUserAgent = userAgent;
            if (!string.IsNullOrWhiteSpace(deferredRename)) item.ShihabDeferredRenameTarget = deferredRename.Trim();
            if (limitKb > 0)
            {
                item.ShihabUseItemSpeedLimit = true;
                item.MehediItemSpeedLimitKb = Math.Min(1000000, limitKb);
            }
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
                item.ShihabTorrentMagnetOrPath = targetUrl;
            }
            ShihabCategoryRule cat = cfg.ResolveMehediCategoryForFile(item.ShihabFileName);
            item.ShihabCategoryName = !string.IsNullOrWhiteSpace(categoryName) ? categoryName.Trim() : cat.ShihabName;
            string safeSaveDir = ShihabProcessAndPathSafety.NormalizeSafeCaptureSaveDirectory(saveDir);
            item.MehediSaveDirectory = !string.IsNullOrWhiteSpace(safeSaveDir) ? safeSaveDir : cat.ShihabDefaultFolder;
            item.MehediQueueName = !string.IsNullOrWhiteSpace(queueName) ? queueName.Trim() : "Main Download Queue";
            item.SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued");
            items.Insert(0, item);
            store.SaveShihabDownloads(items, cfg != null && cfg.ShihabKeepPrivateDownloads);
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
                string fId = "", fStatus = "", fSearch = "", fCat = "", fQueue = "";
                int fLimit = 0;
                for (int fi = 1; fi < cleaned.Count; fi++)
                {
                    string tok = cleaned[fi].ToLowerInvariant();
                    if ((tok == "--id") && fi + 1 < cleaned.Count) fId = cleaned[++fi];
                    else if ((tok == "--status") && fi + 1 < cleaned.Count) fStatus = cleaned[++fi];
                    else if ((tok == "--search") && fi + 1 < cleaned.Count) fSearch = cleaned[++fi];
                    else if ((tok == "--category" || tok == "-c") && fi + 1 < cleaned.Count) fCat = cleaned[++fi];
                    else if ((tok == "--queue" || tok == "-q") && fi + 1 < cleaned.Count) fQueue = cleaned[++fi];
                    else if ((tok == "--limit" || tok == "-n") && fi + 1 < cleaned.Count) int.TryParse(cleaned[++fi], out fLimit);
                }

                string statusPath = BuildStatusQueryString(fId, fStatus, fSearch, fCat, fQueue, fLimit);
                string liveResp;
                if (TryCallLiveBridge(store, cfg, "GET", statusPath, null, out liveResp) && !string.IsNullOrWhiteSpace(liveResp))
                {
                    if (outputJson)
                    {
                        Console.WriteLine(liveResp);
                        return 0;
                    }
                    // Parse and render live bridge JSON instead of falling back to stale disk snapshot
                    object parsedLive;
                    if (ShihabJsonCodec.TryParse(liveResp, out parsedLive, 32) && parsedLive is List<object>)
                    {
                        List<object> liveRows = (List<object>)parsedLive;
                        Console.WriteLine("ID        | Status       | Progress | Size       | File Name");
                        Console.WriteLine("----------+--------------+----------+------------+------------------------------");
                        for (int i = 0; i < liveRows.Count; i++)
                        {
                            Dictionary<string, object> r = liveRows[i] as Dictionary<string, object>;
                            if (r == null) continue;
                            string idStr = ShihabJsonCodec.GetString(r, "id", "");
                            string shortId = idStr.Substring(0, Math.Min(8, idStr.Length));
                            string stStr = ShihabJsonCodec.GetString(r, "status", "");
                            string fnStr = ShihabJsonCodec.GetString(r, "fileName", "");
                            long totBytes = ShihabJsonCodec.GetLong(r, "totalBytes", -1);
                            double pct = 0.0;
                            object pRaw;
                            if (r.TryGetValue("percent", out pRaw) && pRaw != null)
                            {
                                double.TryParse(Convert.ToString(pRaw, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out pct);
                            }
                            Console.WriteLine("{0,-9} | {1,-12} | {2,6:0.0}%  | {3,-10} | {4}",
                                shortId,
                                stStr.Length > 12 ? stStr.Substring(0, 12) : stStr,
                                pct,
                                ShihabFormatHelpers.FormatMehediBytes(totBytes),
                                fnStr);
                        }
                        return 0;
                    }
                }
                List<ShihabDownloadItem> items = FilterOfflineItems(store.LoadMehediDownloads(), fId, fStatus, fSearch, fCat, fQueue, fLimit);
                long freeBytes = TryGetFreeDiskBytesSafe(cfg != null && cfg.ShihabCategories != null && cfg.ShihabCategories.Count > 0 ? cfg.ShihabCategories[0].ShihabDefaultFolder : "");
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
                        row["category"] = it.ShihabCategoryName ?? "";
                        row["queue"] = it.MehediQueueName ?? "";
                        row["url"] = it.MehediUrl ?? "";
                        row["private"] = it.ShihabIsPrivateIncognito;
                        row["freeSpaceBytes"] = freeBytes;
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
                if (meta == null || !meta.ParsedSuccessfully)
                {
                    string errText = (meta != null && !string.IsNullOrWhiteSpace(meta.ErrorMessage))
                        ? meta.ErrorMessage
                        : "Invalid or malformed torrent/magnet metadata.";
                    if (outputJson)
                    {
                        Dictionary<string, object> errObj = new Dictionary<string, object>();
                        errObj["ok"] = false;
                        errObj["error"] = errText;
                        Console.WriteLine(ShihabJsonCodec.Serialize(errObj));
                    }
                    else
                    {
                        Console.Error.WriteLine("Error: " + errText);
                    }
                    return 1;
                }
                if (outputJson)
                {
                    Dictionary<string, object> torObj = new Dictionary<string, object>();
                    torObj["name"] = meta.Name ?? "";
                    torObj["infoHash"] = meta.InfoHashHex ?? "";
                    torObj["totalBytes"] = meta.TotalSizeBytes;
                    torObj["fileCount"] = meta.Files.Count;
                    torObj["isPrivate"] = meta.IsPrivate;
                    torObj["trackerCount"] = meta.Trackers.Count;
                    Console.WriteLine(ShihabJsonCodec.Serialize(torObj));
                    return 0;
                }
                Console.WriteLine("Name      : {0}", meta.Name);
                Console.WriteLine("InfoHash  : {0}", meta.InfoHashHex);
                Console.WriteLine("TotalSize : {0}", ShihabFormatHelpers.FormatMehediBytes(meta.TotalSizeBytes));
                Console.WriteLine("Private   : {0}", meta.IsPrivate ? "Yes (BEP-27)" : "No");
                Console.WriteLine("Trackers  : {0}", meta.Trackers.Count);
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
            if (cmd == "install-ytdlp" || cmd == "install-ffmpeg" || cmd == "install-deno" || cmd == "install-gallery-dl")
            {
                string tool = cmd.Substring("install-".Length);
                string err;
                bool ok = ShihabMediaHelpersManager.InstallOrUpdateTool(store.MehediBaseDirectory, tool, Console.WriteLine, out err, cfg);
                if (!ok) { Console.Error.WriteLine(err); return 1; }
                return 0;
            }
            if (cmd == "start-queue" || cmd == "stop-queue")
            {
                string qName = (cleaned.Count > 1 && !string.IsNullOrWhiteSpace(cleaned[1])) ? cleaned[1].Trim() : "Main Download Queue";
                string liveResp;
                Dictionary<string, object> qObj = new Dictionary<string, object>();
                qObj["action"] = cmd == "start-queue" ? "start_queue" : "stop_queue";
                qObj["id"] = "all";
                qObj["queue"] = qName;
                if (TryCallLiveBridge(store, cfg, "POST", "/shihab/api/action", ShihabJsonCodec.Serialize(qObj), out liveResp))
                {
                    Console.WriteLine("Completed live command '{0}' for queue '{1}'.", cmd, qName);
                    return 0;
                }
                List<ShihabDownloadItem> qItems = store.LoadMehediDownloads();
                for (int i = 0; i < qItems.Count; i++)
                {
                    if (qItems[i].GetShihabStatusEnum() == ShihabDownloadStatus.ShihabCompleted) continue;
                    if (string.Equals(qName, "all", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(qItems[i].MehediQueueName ?? "", qName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (cmd == "stop-queue") qItems[i].SetMehediStatusEnum(ShihabDownloadStatus.ShihabPaused, "Paused");
                        else qItems[i].SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued");
                    }
                }
                store.SaveShihabDownloads(qItems, cfg != null && cfg.ShihabKeepPrivateDownloads);
                Console.WriteLine("Completed command '{0}' for queue '{1}'.", cmd, qName);
                return 0;
            }
            if ((cmd == "get" || cmd == "add" || cmd == "video-get" || cmd == "playlist-get" || cmd == "gallery-get" || cmd == "torrent-get") && cleaned.Count > 1)
            {
                string targetUrl = "";
                string outDir = "";
                string outFileName = "";
                string categoryName = "";
                string queueName = "";
                string checksum = "";
                string cookie = "";
                string referer = "";
                string userAgent = "";
                string ytQuality = "";
                string deferredRename = "";
                int limitKb = 0;
                bool isPrivate = false;
                bool audioOnly = false;
                bool startNow = true;

                for (int ci = 1; ci < cleaned.Count; ci++)
                {
                    string tok = cleaned[ci];
                    string low = tok.ToLowerInvariant();
                    if ((low == "-o" || low == "--output") && ci + 1 < cleaned.Count) outDir = cleaned[++ci];
                    else if ((low == "-f" || low == "--filename") && ci + 1 < cleaned.Count) outFileName = cleaned[++ci];
                    else if ((low == "-c" || low == "--category") && ci + 1 < cleaned.Count) categoryName = cleaned[++ci];
                    else if ((low == "-q" || low == "--queue") && ci + 1 < cleaned.Count)
                    {
                        string qVal = cleaned[++ci];
                        if (cmd == "video-get" || cmd == "playlist-get") ytQuality = qVal;
                        else queueName = qVal;
                    }
                    else if (low == "--quality" && ci + 1 < cleaned.Count) ytQuality = cleaned[++ci];
                    else if ((low == "-l" || low == "--limit") && ci + 1 < cleaned.Count) int.TryParse(cleaned[++ci], out limitKb);
                    else if (low == "--checksum" && ci + 1 < cleaned.Count) checksum = cleaned[++ci];
                    else if (low == "--cookie" && ci + 1 < cleaned.Count) cookie = cleaned[++ci];
                    else if (low == "--referer" && ci + 1 < cleaned.Count) referer = cleaned[++ci];
                    else if (low == "--user-agent" && ci + 1 < cleaned.Count) userAgent = cleaned[++ci];
                    else if (low == "--deferred-rename" && ci + 1 < cleaned.Count) deferredRename = cleaned[++ci];
                    else if (low == "--private" || low == "--incognito") isPrivate = true;
                    else if (low == "--audio-only") audioOnly = true;
                    else if (low == "-s" || low == "--start") startNow = true;
                    else if (low == "--no-start" || low == "--queue-only") startNow = false;
                    else if (string.IsNullOrEmpty(targetUrl) && !tok.StartsWith("-", StringComparison.Ordinal)) targetUrl = tok.Trim();
                }

                string kind = (cmd == "video-get" || cmd == "playlist-get") ? "ytdlp"
                    : (cmd == "gallery-get" ? "gallery" : (cmd == "torrent-get" || ShihabTorrentManager.IsMagnetOrTorrentInput(targetUrl) ? "torrent" : "http"));
                if (!IsAllowedTargetForKind(targetUrl, kind, true))
                {
                    Console.Error.WriteLine("Invalid or unsupported download target for " + cmd + ".");
                    return 1;
                }
                if (string.Equals(ytQuality, "compatible", StringComparison.OrdinalIgnoreCase))
                {
                    ytQuality = ShihabMediaHelpersManager.CompatibleFormatSelector;
                }
                QueueOrDispatchItem(
                    store, cfg, targetUrl, outFileName, checksum, kind, ytQuality, audioOnly,
                    isPrivate, queueName, categoryName, outDir, cookie, referer, userAgent, limitKb, startNow, deferredRename);
                Console.WriteLine("Queued ({0}): {1}", kind, targetUrl);
                return 0;
            }
            if ((cmd == "pause" || cmd == "start" || cmd == "resume" || cmd == "remove" || cmd == "delete") && cleaned.Count > 1)
            {
                string targetId = cleaned[1];
                if (cmd == "delete" && string.Equals((targetId ?? "").Trim(), "all", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine("Explicit download IDs are required for destructive 'delete'; wildcard 'all' is disallowed.");
                    return 1;
                }
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
                    if (MatchesDownloadIdStrict(items[i].ShihabId, targetId, cmd != "delete"))
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
            Console.WriteLine("  mdm.exe --cli status [--json] [--id X] [--status S] [--search Q] [--category C] [--queue Q] [--limit N]");
            Console.WriteLine("  mdm.exe --cli add <URL> [-o dir] [-f name] [-c cat] [-q queue] [-l kbps] [--private] [--checksum spec] [--cookie c] [--referer r] [--user-agent ua] [--no-start]");
            Console.WriteLine("  mdm.exe --cli get <URL> [options...]          Alias for 'add'");
            Console.WriteLine("  mdm.exe --cli pause <id|all>                  Pause download(s)");
            Console.WriteLine("  mdm.exe --cli start <id|all>                  Resume/start download(s)");
            Console.WriteLine("  mdm.exe --cli start-queue [name]              Start a queue (default: Main Download Queue)");
            Console.WriteLine("  mdm.exe --cli stop-queue [name|all]           Stop a queue (default: Main Download Queue)");
            Console.WriteLine("  mdm.exe --cli remove <id|all>                 Remove download(s) from list");
            Console.WriteLine("  mdm.exe --cli delete <id|all>                 Remove download(s) and delete local file");
            Console.WriteLine("  mdm.exe --cli check-update [--json]           Check GitHub Releases for new version");
            Console.WriteLine("  mdm.exe --cli update [--verify-only]          Download, SHA-256 verify, and apply update");
            Console.WriteLine("  mdm.exe --cli pair [--json]                   Print LAN Remote Web UI URL, code & QR");
            Console.WriteLine("  mdm.exe --cli api <on|off>                    Enable or disable local automation API");
            Console.WriteLine("  mdm.exe --cli video-info <URL>                Query video formats via yt-dlp");
            Console.WriteLine("  mdm.exe --cli playlist-info <URL>             Query playlist entries via yt-dlp");
            Console.WriteLine("  mdm.exe --cli video-get <URL> [-q compatible|fmt] [--audio-only]");
            Console.WriteLine("  mdm.exe --cli gallery-get <URL>               Download image gallery via gallery-dl");
            Console.WriteLine("  mdm.exe --cli torrent-get <file|magnet|url>   Download a .torrent file or magnet link");
            Console.WriteLine("  mdm.exe --cli torrent-info <file|magnet|url>  Inspect .torrent or magnet metadata");
            Console.WriteLine("  mdm.exe --cli torrent-create <src> [out]      Create a .torrent file from file or folder");
            Console.WriteLine("  mdm.exe --cli install-ytdlp                   Install/update yt-dlp helper (SHA-256 verified)");
            Console.WriteLine("  mdm.exe --cli install-ffmpeg                  Install/update ffmpeg helper (SHA-256 verified)");
            Console.WriteLine("  mdm.exe --cli install-deno                    Install/update Deno JS runtime (SHA-256 verified)");
            Console.WriteLine("  mdm.exe --cli install-gallery-dl              Install/update gallery-dl helper (SHA-256 verified)");
            Console.WriteLine("  mdm.exe --mcp                                 Run Model Context Protocol stdio server (16 tools)");
            Console.WriteLine("  mdm.exe --headless                            Run background download daemon without GUI");
            Console.WriteLine("  mdm.exe --forget-sign-ins                     Wipe all saved credentials & tokens");
        }
    }
}

