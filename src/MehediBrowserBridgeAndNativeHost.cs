using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ShihabMehediIDM
{
    [DataContract]
    public class ShihabCapturedLinkDto
    {
        [DataMember(Name = "url")] public string ShihabUrl { get; set; }
        [DataMember(Name = "referrer")] public string MehediReferrer { get; set; }
        [DataMember(Name = "cookies")] public string ShihabCookies { get; set; }
        [DataMember(Name = "userAgent")] public string MehediUserAgent { get; set; }
        [DataMember(Name = "fileName")] public string ShihabFileName { get; set; }
        [DataMember(Name = "mimeType")] public string MehediMimeType { get; set; }
        [DataMember(Name = "fileSize")] public long ShihabFileSize { get; set; }
        [DataMember(Name = "tabTitle")] public string MehediTabTitle { get; set; }
        [DataMember(Name = "silentMode")] public bool ShihabSilentMode { get; set; }
        [DataMember(Name = "addToQueueOnly")] public bool MehediAddToQueueOnly { get; set; }
        [DataMember(Name = "saveDirectory")] public string ShihabSaveDirectory { get; set; }
        [DataMember(Name = "exitWhenDone")] public bool MehediExitWhenDone { get; set; }
        [DataMember(Name = "origin")] public string ShihabOrigin { get; set; }
        [DataMember(Name = "omitCookies")] public bool MehediOmitCookies { get; set; }
        [DataMember(Name = "audioUrl")] public string ShihabAudioUrl { get; set; }
        [DataMember(Name = "audioSize")] public long MehediAudioSize { get; set; }
        [DataMember(Name = "tabId")] public int ShihabTabId { get; set; }
        [DataMember(Name = "qualityLabel")] public string MehediQualityLabel { get; set; }
        [DataMember(Name = "postBody")] public string ShihabPostBody { get; set; }
        [DataMember(Name = "expectedChecksum")] public string ShihabExpectedChecksum { get; set; }
        [DataMember(Name = "incognito")] public bool ShihabIncognito { get; set; }
        [DataMember(Name = "kind")] public string ShihabJobKind { get; set; }
        [DataMember(Name = "format")] public string ShihabFormatSelector { get; set; }
        [DataMember(Name = "audioOnly")] public bool ShihabAudioOnly { get; set; }
    }

    [DataContract]
    public class MehediRelayJobDto
    {
        [DataMember(Name = "jobId")] public string ShihabJobId { get; set; }
        [DataMember(Name = "tabId")] public int MehediTabId { get; set; }
        [DataMember(Name = "url")] public string ShihabUrl { get; set; }
        [DataMember(Name = "referrer")] public string MehediReferrer { get; set; }
        [DataMember(Name = "origin")] public string ShihabOrigin { get; set; }
        [DataMember(Name = "omitCookies")] public bool MehediOmitCookies { get; set; }
        [DataMember(Name = "totalBytes")] public long ShihabTotalBytes { get; set; }
    }

    [DataContract]
    public class ShihabRelayJobsReplyDto
    {
        [DataMember(Name = "ok")] public bool ShihabOk { get; set; }
        [DataMember(Name = "jobs")] public List<MehediRelayJobDto> MehediJobs { get; set; }
    }

    public static class ShihabBrowserRelayHub
    {
        private sealed class MehediActiveRelaySession
        {
            public string ShihabJobId;
            public MehediRelayJobDto MehediDto;
            public FileStream MehediOutStream;
            public readonly object ShihabStreamLock = new object();
            public long MehediBytesReceived;
            public long ShihabTotalBytes;
            public bool MehediClaimedByExtension;
            public long ShihabCreatedTickMs;
            public long ShihabLastActivityTickMs;
            public bool MehediFinished;
            public bool ShihabSucceeded;
            public string MehediErrorMessage;
            public readonly ManualResetEventSlim ShihabDoneSignal = new ManualResetEventSlim(false);
            public Action<long, long> MehediProgressCallback;
        }

        private static readonly object _shihabHubLock = new object();
        private static readonly Dictionary<string, MehediActiveRelaySession> _mehediSessions =
            new Dictionary<string, MehediActiveRelaySession>(StringComparer.OrdinalIgnoreCase);
        private static readonly Stopwatch _shihabClock = Stopwatch.StartNew();

        public static List<MehediRelayJobDto> ClaimPendingShihabRelayJobs()
        {
            List<MehediRelayJobDto> mehediPending = new List<MehediRelayJobDto>();
            long shihabNowMs = _shihabClock.ElapsedMilliseconds;
            lock (_shihabHubLock)
            {
                foreach (KeyValuePair<string, MehediActiveRelaySession> mehediPair in _mehediSessions)
                {
                    MehediActiveRelaySession shihabSess = mehediPair.Value;
                    if (shihabSess.MehediFinished) continue;
                    if (!shihabSess.MehediClaimedByExtension)
                    {
                        shihabSess.MehediClaimedByExtension = true;
                        shihabSess.ShihabLastActivityTickMs = shihabNowMs;
                        mehediPending.Add(shihabSess.MehediDto);
                    }
                }
            }
            return mehediPending;
        }

        public static bool AppendMehediRelayChunk(
            string shihabJobId,
            long mehediOffset,
            long shihabReportedTotal,
            byte[] mehediChunkBytes,
            int shihabChunkCount)
        {
            if (string.IsNullOrEmpty(shihabJobId)) return false;
            MehediActiveRelaySession mehediSess = null;
            lock (_shihabHubLock)
            {
                _mehediSessions.TryGetValue(shihabJobId, out mehediSess);
            }
            if (mehediSess == null || mehediSess.MehediFinished)
            {
                return false;
            }

            long shihabCurrentReceived;
            long mehediCurrentTotal;
            lock (mehediSess.ShihabStreamLock)
            {
                if (mehediSess.MehediFinished || mehediSess.MehediOutStream == null)
                {
                    return false;
                }
                if (mehediOffset >= 0 && mehediSess.MehediOutStream.Position != mehediOffset)
                {
                    mehediSess.MehediOutStream.Seek(mehediOffset, SeekOrigin.Begin);
                }
                if (mehediChunkBytes != null && shihabChunkCount > 0)
                {
                    mehediSess.MehediOutStream.Write(mehediChunkBytes, 0, shihabChunkCount);
                }
                long shihabEndPos = mehediSess.MehediOutStream.Position;
                if (shihabEndPos > mehediSess.MehediBytesReceived)
                {
                    mehediSess.MehediBytesReceived = shihabEndPos;
                }
                if (shihabReportedTotal > 0)
                {
                    mehediSess.ShihabTotalBytes = shihabReportedTotal;
                }
                mehediSess.ShihabLastActivityTickMs = _shihabClock.ElapsedMilliseconds;
                shihabCurrentReceived = mehediSess.MehediBytesReceived;
                mehediCurrentTotal = mehediSess.ShihabTotalBytes;
            }

            if (mehediSess.MehediProgressCallback != null)
            {
                try { mehediSess.MehediProgressCallback(shihabCurrentReceived, mehediCurrentTotal); } catch { }
            }
            return true;
        }

        public static bool CompleteShihabRelayJob(string mehediJobId, bool shihabSuccess, string mehediError)
        {
            if (string.IsNullOrEmpty(mehediJobId)) return false;
            MehediActiveRelaySession shihabSess = null;
            lock (_shihabHubLock)
            {
                _mehediSessions.TryGetValue(mehediJobId, out shihabSess);
            }
            if (shihabSess == null) return false;

            lock (shihabSess.ShihabStreamLock)
            {
                if (!shihabSess.MehediFinished)
                {
                    shihabSess.MehediFinished = true;
                    shihabSess.ShihabSucceeded = shihabSuccess;
                    shihabSess.MehediErrorMessage = mehediError ?? "";
                    if (shihabSess.MehediOutStream != null)
                    {
                        try { shihabSess.MehediOutStream.Flush(); shihabSess.MehediOutStream.Dispose(); } catch { }
                        shihabSess.MehediOutStream = null;
                    }
                    shihabSess.ShihabDoneSignal.Set();
                }
            }
            return true;
        }

        public static long ExecuteMehediRelayDownload(
            string shihabUrl,
            string mehediTargetFilePath,
            int shihabSourceTabId,
            string mehediReferrer,
            string shihabOrigin,
            bool mehediOmitCookies,
            long shihabExpectedTotalBytes,
            Action<long, long> mehediProgressCallback,
            CancellationToken shihabToken)
        {
            string mehediJobId = Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(Path.GetDirectoryName(mehediTargetFilePath));
            FileStream shihabFs = new FileStream(mehediTargetFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);

            long mehediNowMs = _shihabClock.ElapsedMilliseconds;
            MehediActiveRelaySession shihabSess = new MehediActiveRelaySession
            {
                ShihabJobId = mehediJobId,
                MehediDto = new MehediRelayJobDto
                {
                    ShihabJobId = mehediJobId,
                    MehediTabId = shihabSourceTabId,
                    ShihabUrl = shihabUrl,
                    MehediReferrer = mehediReferrer ?? "",
                    ShihabOrigin = shihabOrigin ?? "",
                    MehediOmitCookies = mehediOmitCookies,
                    ShihabTotalBytes = shihabExpectedTotalBytes
                },
                MehediOutStream = shihabFs,
                MehediBytesReceived = 0,
                ShihabTotalBytes = shihabExpectedTotalBytes,
                MehediClaimedByExtension = false,
                ShihabCreatedTickMs = mehediNowMs,
                ShihabLastActivityTickMs = mehediNowMs,
                MehediProgressCallback = mehediProgressCallback
            };

            lock (_shihabHubLock)
            {
                _mehediSessions[mehediJobId] = shihabSess;
            }

            try
            {
                while (!shihabSess.ShihabDoneSignal.Wait(200))
                {
                    if (shihabToken.IsCancellationRequested)
                    {
                        CompleteShihabRelayJob(mehediJobId, false, "Cancelled");
                        throw new OperationCanceledException(shihabToken);
                    }
                    long shihabElapsedNow = _shihabClock.ElapsedMilliseconds;
                    if (!shihabSess.MehediClaimedByExtension && (shihabElapsedNow - shihabSess.ShihabCreatedTickMs) > 10000L)
                    {
                        CompleteShihabRelayJob(mehediJobId, false, "Browser extension relay did not claim job within 10s");
                        break;
                    }
                    long mehediIdleLimitMs = shihabSess.MehediBytesReceived > 0 ? 45000L : 25000L;
                    if (shihabSess.MehediClaimedByExtension && (shihabElapsedNow - shihabSess.ShihabLastActivityTickMs) > mehediIdleLimitMs)
                    {
                        CompleteShihabRelayJob(mehediJobId, false, "Browser extension relay timed out while streaming chunks");
                        break;
                    }
                }

                if (!shihabSess.ShihabSucceeded)
                {
                    throw new IOException(string.IsNullOrEmpty(shihabSess.MehediErrorMessage)
                        ? "Browser relay transfer failed."
                        : shihabSess.MehediErrorMessage);
                }
                if (shihabSess.MehediBytesReceived <= 0)
                {
                    throw new IOException("Browser relay returned 0 bytes.");
                }
                return shihabSess.MehediBytesReceived;
            }
            finally
            {
                lock (_shihabHubLock)
                {
                    _mehediSessions.Remove(mehediJobId);
                }
                lock (shihabSess.ShihabStreamLock)
                {
                    if (shihabSess.MehediOutStream != null)
                    {
                        try { shihabSess.MehediOutStream.Dispose(); } catch { }
                        shihabSess.MehediOutStream = null;
                    }
                }
                shihabSess.ShihabDoneSignal.Dispose();
            }
        }
    }

    [DataContract]
    public class MehediBatchLinkEntryDto
    {
        [DataMember(Name = "url")] public string ShihabUrl { get; set; }
        [DataMember(Name = "text")] public string MehediText { get; set; }
        [DataMember(Name = "fileType")] public string ShihabFileType { get; set; }
        [DataMember(Name = "referer")] public string ShihabReferer { get; set; }
    }

    [DataContract]
    public class ShihabBatchCaptureDto
    {
        [DataMember(Name = "pageUrl")] public string ShihabPageUrl { get; set; }
        [DataMember(Name = "pageTitle")] public string MehediPageTitle { get; set; }
        [DataMember(Name = "cookies")] public string ShihabCookies { get; set; }
        [DataMember(Name = "userAgent")] public string MehediUserAgent { get; set; }
        [DataMember(Name = "links")] public List<MehediBatchLinkEntryDto> ShihabLinks { get; set; }
    }

    [DataContract]
    public class MehediBridgeStatusReply
    {
        [DataMember(Name = "ok")] public bool ShihabOk { get; set; }
        [DataMember(Name = "app")] public string MehediAppName { get; set; }
        [DataMember(Name = "version")] public string ShihabVersion { get; set; }
        [DataMember(Name = "integrationEnabled")] public bool MehediIntegrationEnabled { get; set; }
        [DataMember(Name = "showVideoPanel")] public bool ShihabShowVideoPanel { get; set; }
        [DataMember(Name = "panelMiniMode")] public bool MehediPanelMiniMode { get; set; }
        [DataMember(Name = "panelSkipAutoStart")] public bool ShihabPanelSkipAutoStart { get; set; }
        [DataMember(Name = "minVideoSizeKb")] public int MehediMinVideoSizeKb { get; set; }
        [DataMember(Name = "autoCaptureExtensions")] public string ShihabAutoCaptureExtensions { get; set; }
        [DataMember(Name = "excludedSites")] public string MehediExcludedSites { get; set; }
        [DataMember(Name = "excludedAddresses")] public string ShihabExcludedAddresses { get; set; }
        [DataMember(Name = "preventKey")] public string MehediPreventKey { get; set; }
        [DataMember(Name = "forceKey")] public string ShihabForceKey { get; set; }
        [DataMember(Name = "captureOpenedFiles")] public bool ShihabCaptureOpenedFiles { get; set; }
        [DataMember(Name = "captureMinSizeKb")] public int ShihabCaptureMinSizeKb { get; set; }
        [DataMember(Name = "activeDownloads")] public int ShihabActiveDownloads { get; set; }
        [DataMember(Name = "updateAvailable")] public bool ShihabUpdateAvailable { get; set; }
        [DataMember(Name = "latestVersion")] public string ShihabLatestVersion { get; set; }
        [DataMember(Name = "latestHtmlUrl")] public string ShihabLatestHtmlUrl { get; set; }
        [DataMember(Name = "message")] public string MehediMessage { get; set; }
    }

    [DataContract]
    public class ShihabNativeEnvelopeDto
    {
        [DataMember(Name = "action")] public string ShihabAction { get; set; }
        [DataMember(Name = "capture")] public ShihabCapturedLinkDto MehediCapture { get; set; }
        [DataMember(Name = "batch")] public ShihabBatchCaptureDto ShihabBatch { get; set; }
        [DataMember(Name = "videoId")] public string MehediVideoId { get; set; }
        [DataMember(Name = "visitorData")] public string ShihabVisitorData { get; set; }
    }

    public class ShihabCliParsedArguments
    {
        public string ShihabDirectUrl { get; set; }
        public string MehediLocalSavePath { get; set; }
        public string ShihabLocalFileName { get; set; }
        public bool MehediSilentNoPrompt { get; set; }
        public bool ShihabExitOnDone { get; set; }
        public bool MehediAddToQueueOnly { get; set; }
        public bool ShihabStartMainQueue { get; set; }
        public string MehediCustomConfigDir { get; set; }
        public bool ShihabRunAsNativeHost { get; set; }
        public bool ShihabRunAsMcpServer { get; set; }
        public bool ShihabRunAsHeadlessCli { get; set; }
        public bool ShihabRunHeadlessDaemon { get; set; }
        public bool ShihabRunCrashWatchdog { get; set; }
        public int ShihabCrashWatchdogPid { get; set; }
        public string ShihabCrashWatchdogDir { get; set; }
        public string ShihabUpdatedFromVersion { get; set; }
        public bool ShihabForgetSignIns { get; set; }
        public string[] ShihabRawArgs { get; set; }
        public bool ShihabStartMinimized { get; set; }
        public int MehediAutoCloseMs { get; set; }
        public bool ShihabHasActionableCommand { get; set; }

        public static ShihabCliParsedArguments ParseMehediArgs(string[] shihabArgs)
        {
            ShihabCliParsedArguments mehediParsed = new ShihabCliParsedArguments();
            mehediParsed.ShihabDirectUrl = "";
            mehediParsed.MehediLocalSavePath = "";
            mehediParsed.ShihabLocalFileName = "";
            mehediParsed.MehediCustomConfigDir = "";
            mehediParsed.ShihabCrashWatchdogDir = "";
            mehediParsed.ShihabUpdatedFromVersion = "";
            mehediParsed.MehediAutoCloseMs = 0;
            mehediParsed.ShihabRawArgs = shihabArgs ?? new string[0];

            if (shihabArgs == null || shihabArgs.Length == 0) return mehediParsed;

            string firstLower = (shihabArgs[0] ?? "").Trim().ToLowerInvariant();
            if (firstLower == "status" ||
                firstLower == "list" ||
                firstLower == "get" ||
                firstLower == "pause" ||
                firstLower == "start" ||
                firstLower == "resume" ||
                firstLower == "remove" ||
                firstLower == "delete" ||
                firstLower == "pair" ||
                firstLower == "api" ||
                firstLower == "check-update" ||
                firstLower == "check-updates" ||
                firstLower == "update" ||
                firstLower == "video-info" ||
                firstLower == "playlist-info" ||
                firstLower == "video-get" ||
                firstLower == "playlist-get" ||
                firstLower == "gallery-get" ||
                firstLower == "torrent-info" ||
                firstLower == "torrent-get" ||
                firstLower == "torrent-create" ||
                firstLower == "install-ytdlp" ||
                firstLower == "install-ffmpeg" ||
                firstLower == "install-gallery-dl" ||
                firstLower == "help" ||
                firstLower == "--help" ||
                firstLower == "-h")
            {
                mehediParsed.ShihabRunAsHeadlessCli = true;
            }

            for (int shihabI = 0; shihabI < shihabArgs.Length; shihabI++)
            {
                string mehediTok = shihabArgs[shihabI] ?? "";
                string shihabLower = mehediTok.Trim().ToLowerInvariant();

                if (shihabLower == "--mcp" || shihabLower == "mcp")
                {
                    mehediParsed.ShihabRunAsMcpServer = true;
                }
                else if (shihabLower == "--headless")
                {
                    mehediParsed.ShihabRunHeadlessDaemon = true;
                }
                else if (shihabLower == "--crash-monitor")
                {
                    mehediParsed.ShihabRunCrashWatchdog = true;
                    if (shihabI + 1 < shihabArgs.Length)
                    {
                        int pidVal;
                        if (int.TryParse(shihabArgs[++shihabI], out pidVal))
                        {
                            mehediParsed.ShihabCrashWatchdogPid = pidVal;
                        }
                    }
                    if (shihabI + 1 < shihabArgs.Length)
                    {
                        mehediParsed.ShihabCrashWatchdogDir = shihabArgs[++shihabI];
                    }
                }
                else if (shihabLower == "--shihab-updated-from")
                {
                    if (shihabI + 1 < shihabArgs.Length)
                    {
                        mehediParsed.ShihabUpdatedFromVersion = shihabArgs[++shihabI];
                    }
                }
                else if (shihabLower == "--forget-sign-ins")
                {
                    mehediParsed.ShihabForgetSignIns = true;
                }
                else if (shihabLower == "--cli")
                {
                    mehediParsed.ShihabRunAsHeadlessCli = true;
                }
                else if (shihabLower == "/d" || shihabLower == "-d")
                {
                    if (shihabI + 1 < shihabArgs.Length)
                    {
                        mehediParsed.ShihabDirectUrl = shihabArgs[++shihabI];
                        mehediParsed.ShihabHasActionableCommand = true;
                    }
                }
                else if (shihabLower == "/p" || shihabLower == "-p")
                {
                    if (shihabI + 1 < shihabArgs.Length)
                    {
                        mehediParsed.MehediLocalSavePath = shihabArgs[++shihabI];
                    }
                }
                else if (shihabLower == "/f" || shihabLower == "-f")
                {
                    if (shihabI + 1 < shihabArgs.Length)
                    {
                        mehediParsed.ShihabLocalFileName = shihabArgs[++shihabI];
                    }
                }
                else if (shihabLower == "/n" || shihabLower == "-n")
                {
                    mehediParsed.MehediSilentNoPrompt = true;
                }
                else if (shihabLower == "/q" || shihabLower == "-q")
                {
                    mehediParsed.ShihabExitOnDone = true;
                }
                else if (shihabLower == "/a" || shihabLower == "-a")
                {
                    mehediParsed.MehediAddToQueueOnly = true;
                }
                else if (shihabLower == "/s" || shihabLower == "-s")
                {
                    mehediParsed.ShihabStartMainQueue = true;
                    mehediParsed.ShihabHasActionableCommand = true;
                }
                else if (shihabLower == "--shihab-config-dir")
                {
                    if (shihabI + 1 < shihabArgs.Length)
                    {
                        mehediParsed.MehediCustomConfigDir = shihabArgs[++shihabI];
                    }
                }
                else if (shihabLower == "--shihab-native-messaging-host" ||
                         mehediTok.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase))
                {
                    if (shihabLower == "--shihab-native-messaging-host")
                    {
                        mehediParsed.ShihabRunAsNativeHost = true;
                    }
                }
                else if (shihabLower == "--shihab-minimized")
                {
                    mehediParsed.ShihabStartMinimized = true;
                }
                else if (shihabLower == "--shihab-auto-close-ms")
                {
                    if (shihabI + 1 < shihabArgs.Length)
                    {
                        int shihabMs;
                        if (int.TryParse(shihabArgs[++shihabI], out shihabMs))
                        {
                            mehediParsed.MehediAutoCloseMs = shihabMs;
                        }
                    }
                }
                else if (mehediTok.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                         mehediTok.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                         mehediTok.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                         mehediTok.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase) ||
                         mehediTok.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase) ||
                         mehediTok.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                {
                    mehediParsed.ShihabDirectUrl = mehediTok;
                    mehediParsed.ShihabHasActionableCommand = true;
                }
            }
            return mehediParsed;
        }
    }

    public class MehediBrowserBridgeServer : IDisposable
    {
        private readonly ShihabStateStore _shihabStore;
        private readonly MehediAppConfiguration _shihabConfig;
        private readonly MehediDynamicSegmentEngine _mehediEngine;
        private TcpListener _shihabTcpListener;
        private CancellationTokenSource _mehediStopCts;
        private volatile bool _shihabIsRunning;

        public event Action<ShihabCapturedLinkDto> OnShihabSingleCaptureReceived;
        public event Action<ShihabBatchCaptureDto> OnMehediBatchCaptureReceived;
        public event Action OnShihabStartMainQueueRequested;
        public event Action OnShihabShowMainWindowRequested;
        public event Action OnShihabGracefulExitRequested;

        public MehediBrowserBridgeServer(MehediAppConfiguration shihabConfig, MehediDynamicSegmentEngine mehediEngine)
            : this(new ShihabStateStore(null), shihabConfig, mehediEngine)
        {
        }

        public MehediBrowserBridgeServer(ShihabStateStore shihabStore, MehediAppConfiguration shihabConfig, MehediDynamicSegmentEngine mehediEngine)
        {
            _shihabStore = shihabStore ?? new ShihabStateStore(null);
            _shihabConfig = shihabConfig;
            _mehediEngine = mehediEngine;
        }

        private string GetBridgeSecretToken()
        {
            return _shihabStore != null ? _shihabStore.LoadOrCreateBridgeToken() : "";
        }

        private static bool IsTrustedExtensionOrigin(string originHeader)
        {
            if (string.IsNullOrWhiteSpace(originHeader)) return false;
            string trimmed = originHeader.Trim();
            return trimmed.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("moz-extension://", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsValidBridgeToken(string providedToken)
        {
            if (string.IsNullOrWhiteSpace(providedToken)) return false;
            string expected = GetBridgeSecretToken();
            if (string.IsNullOrWhiteSpace(expected)) return false;
            string candidate = providedToken.Trim();
            if (candidate.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate.Substring(7).Trim();
            }
            return ShihabRemoteControlServer.ConstantTimeEquals(candidate, expected);
        }

        public bool StartShihabBridge()
        {
            try
            {
                int mehediPort = _shihabConfig.MehediLocalBridgePort > 0 ? _shihabConfig.MehediLocalBridgePort : 45779;
                _shihabTcpListener = new TcpListener(IPAddress.Loopback, mehediPort);
                _shihabTcpListener.Start();
                _shihabIsRunning = true;
                _mehediStopCts = new CancellationTokenSource();
                CancellationToken shihabTok = _mehediStopCts.Token;

                Task.Run(delegate
                {
                    AcceptMehediRequestsLoop(shihabTok);
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool RestartShihabBridge()
        {
            try
            {
                _shihabIsRunning = false;
                if (_mehediStopCts != null)
                {
                    try { _mehediStopCts.Cancel(); } catch { }
                }
                if (_shihabTcpListener != null)
                {
                    try { _shihabTcpListener.Stop(); } catch { }
                    _shihabTcpListener = null;
                }
                Thread.Sleep(80);
            }
            catch
            {
            }
            return StartShihabBridge();
        }

        private void AcceptMehediRequestsLoop(CancellationToken shihabToken)
        {
            while (!shihabToken.IsCancellationRequested && _shihabIsRunning && _shihabTcpListener != null)
            {
                TcpClient mehediClient = null;
                try
                {
                    mehediClient = _shihabTcpListener.AcceptTcpClient();
                }
                catch
                {
                    break;
                }
                if (mehediClient != null)
                {
                    TcpClient shihabCaptured = mehediClient;
                    Task.Run(delegate
                    {
                        HandleSingleShihabTcpClient(shihabCaptured);
                    });
                }
            }
        }

        private void HandleSingleShihabTcpClient(TcpClient mehediClient)
        {
            using (mehediClient)
            {
                try
                {
                    mehediClient.ReceiveTimeout = 20000;
                    mehediClient.SendTimeout = 20000;
                    using (NetworkStream shihabStream = mehediClient.GetStream())
                    {
                        string mehediMethod;
                        string shihabPath;
                        string originHeader;
                        string bridgeTokenHeader;
                        string mehediBody;
                        byte[] shihabRawBodyBytes;
                        int mehediRawBodyCount;
                        if (!ReadParsedShihabHttpRequest(
                            shihabStream,
                            out mehediMethod,
                            out shihabPath,
                            out originHeader,
                            out bridgeTokenHeader,
                            out mehediBody,
                            out shihabRawBodyBytes,
                            out mehediRawBodyCount))
                        {
                            return;
                        }

                        // Reject untrusted cross-origin browser requests (anti-CSRF / Private Network Access defense)
                        bool hasOrigin = !string.IsNullOrWhiteSpace(originHeader);
                        bool isTrustedExtOrigin = IsTrustedExtensionOrigin(originHeader);
                        if (hasOrigin && !isTrustedExtOrigin)
                        {
                            WriteRawMehediHttpResponse(shihabStream, 403, "Forbidden", "{\"ok\":false,\"message\":\"Untrusted Origin rejected\"}", "");
                            return;
                        }

                        string allowedCorsOrigin = isTrustedExtOrigin ? originHeader.Trim() : "";

                        if (string.Equals(mehediMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                        {
                            WriteRawMehediHttpResponse(shihabStream, 204, "No Content", "", allowedCorsOrigin);
                            return;
                        }

                        string shihabCleanPath = shihabPath.Split('?')[0].ToLowerInvariant();
                        if (shihabCleanPath == "/shihab/ping" || shihabCleanPath == "/")
                        {
                            WriteMehediJsonTcpResponse(shihabStream, 200, BuildShihabStatusReply("MHS Download Manager Bridge Ready"), allowedCorsOrigin);
                            return;
                        }

                        bool isAuthorizedCaller = isTrustedExtOrigin || IsValidBridgeToken(bridgeTokenHeader);
                        if (!isAuthorizedCaller)
                        {
                            WriteMehediJsonTcpResponse(shihabStream, 401, BuildShihabErrorReply("Unauthorized bridge request: valid extension Origin or X-MHS-Bridge-Token required"), allowedCorsOrigin);
                            return;
                        }

                        if (shihabCleanPath == "/shihab/show" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
                        {
                            if (OnShihabShowMainWindowRequested != null)
                            {
                                try { OnShihabShowMainWindowRequested(); } catch { }
                            }
                            WriteMehediJsonTcpResponse(shihabStream, 200, BuildShihabStatusReply("Main window activated"), allowedCorsOrigin);
                            return;
                        }

                        if (shihabCleanPath == "/shihab/exit" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
                        {
                            try { _mehediEngine.PersistShihabState(); } catch { }
                            try { ShihabCrashMonitor.MarkCleanShutdown(); } catch { }
                            WriteRawMehediHttpResponse(shihabStream, 200, "OK", "{\"ok\":true,\"message\":\"Shutting down cleanly\"}", allowedCorsOrigin);
                            Task.Run(delegate
                            {
                                Thread.Sleep(120);
                                if (OnShihabGracefulExitRequested != null)
                                {
                                    try { OnShihabGracefulExitRequested(); return; } catch { }
                                }
                                try { System.Windows.Forms.Application.Exit(); } catch { }
                            });
                            return;
                        }

                        if (shihabCleanPath == "/shihab/api/status" && string.Equals(mehediMethod, "GET", StringComparison.OrdinalIgnoreCase))
                        {
                            List<ShihabDownloadItem> items = _mehediEngine.GetShihabDownloadsSnapshot();
                            StringBuilder jb = new StringBuilder("[");
                            for (int i = 0; i < items.Count; i++)
                            {
                                ShihabDownloadItem it = items[i];
                                if (i > 0) jb.Append(",");
                                jb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                                    "{{\"id\":\"{0}\",\"fileName\":\"{1}\",\"status\":\"{2}\",\"percent\":{3:0.0},\"totalBytes\":{4},\"downloadedBytes\":{5},\"speedBps\":{6:0},\"checksumStatus\":\"{7}\",\"url\":\"{8}\"}}",
                                    EscapeJsonVal(it.ShihabId),
                                    EscapeJsonVal(it.ShihabFileName),
                                    EscapeJsonVal(it.ShihabStatusText),
                                    it.GetMehediPercent(),
                                    it.ShihabTotalBytes,
                                    it.MehediDownloadedBytes,
                                    it.ShihabCurrentSpeedBps,
                                    EscapeJsonVal(it.ShihabChecksumStatus),
                                    EscapeJsonVal(it.MehediUrl));
                            }
                            jb.Append("]");
                            WriteRawMehediHttpResponse(shihabStream, 200, "OK", jb.ToString(), allowedCorsOrigin);
                            return;
                        }

                        if (shihabCleanPath == "/shihab/api/add" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
                        {
                            string url = ShihabDebridManager.ExtractJsonStringField(mehediBody, "url");
                            string fileName = ShihabDebridManager.ExtractJsonStringField(mehediBody, "fileName");
                            string checksum = ShihabDebridManager.ExtractJsonStringField(mehediBody, "checksum");
                            string kind = ShihabDebridManager.ExtractJsonStringField(mehediBody, "kind");
                            string format = ShihabDebridManager.ExtractJsonStringField(mehediBody, "format");
                            string postBody = ShihabDebridManager.ExtractJsonStringField(mehediBody, "postBody");
                            bool audioOnly = mehediBody.IndexOf("\"audioOnly\":true", StringComparison.OrdinalIgnoreCase) >= 0;
                            bool incognito = mehediBody.IndexOf("\"incognito\":true", StringComparison.OrdinalIgnoreCase) >= 0;

                            if (string.IsNullOrWhiteSpace(url))
                            {
                                WriteRawMehediHttpResponse(shihabStream, 400, "Bad Request", "{\"ok\":false,\"error\":\"url is required\"}", allowedCorsOrigin);
                                return;
                            }

                            ShihabDownloadItem newItem = CreateItemFromApiOrCapture(url.Trim(), fileName, checksum, kind, format, audioOnly, incognito, postBody);
                            _mehediEngine.AddShihabDownloadItem(newItem, true);
                            WriteRawMehediHttpResponse(shihabStream, 200, "OK",
                                "{\"ok\":true,\"id\":\"" + EscapeJsonVal(newItem.ShihabId) + "\",\"fileName\":\"" + EscapeJsonVal(newItem.ShihabFileName) + "\"}",
                                allowedCorsOrigin);
                            return;
                        }

                        if (shihabCleanPath == "/shihab/api/action" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
                        {
                            string act = ShihabDebridManager.ExtractJsonStringField(mehediBody, "action").Trim().ToLowerInvariant();
                            string targetId = ShihabDebridManager.ExtractJsonStringField(mehediBody, "id").Trim();
                            ExecuteBridgeActionOnEngine(act, targetId);
                            WriteRawMehediHttpResponse(shihabStream, 200, "OK", "{\"ok\":true}", allowedCorsOrigin);
                            return;
                        }

                        if (shihabCleanPath == "/shihab/relay-jobs" && string.Equals(mehediMethod, "GET", StringComparison.OrdinalIgnoreCase))
                        {
                            ShihabRelayJobsReplyDto mehediRelayReply = new ShihabRelayJobsReplyDto
                            {
                                ShihabOk = true,
                                MehediJobs = ShihabBrowserRelayHub.ClaimPendingShihabRelayJobs()
                            };
                            WriteMehediJsonTcpResponse(shihabStream, 200, mehediRelayReply, allowedCorsOrigin);
                            return;
                        }

                        if (shihabCleanPath == "/shihab/relay-chunk" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
                        {
                            string shihabJobId = ParseMehediQueryParam(shihabPath, "jobId");
                            long mehediOffset = -1L;
                            long shihabTotal = -1L;
                            long.TryParse(ParseMehediQueryParam(shihabPath, "offset"), out mehediOffset);
                            long.TryParse(ParseMehediQueryParam(shihabPath, "total"), out shihabTotal);

                            bool mehediAccepted = ShihabBrowserRelayHub.AppendMehediRelayChunk(
                                shihabJobId,
                                mehediOffset,
                                shihabTotal,
                                shihabRawBodyBytes,
                                mehediRawBodyCount);
                            if (mehediAccepted)
                            {
                                WriteRawMehediHttpResponse(shihabStream, 200, "OK", "{\"ok\":true}", allowedCorsOrigin);
                            }
                            else
                            {
                                WriteRawMehediHttpResponse(shihabStream, 410, "Gone", "{\"ok\":false,\"message\":\"cancelled\"}", allowedCorsOrigin);
                            }
                            return;
                        }

                        if (shihabCleanPath == "/shihab/relay-done" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
                        {
                            string mehediJobId = ParseMehediQueryParam(shihabPath, "jobId");
                            string shihabOkParam = ParseMehediQueryParam(shihabPath, "ok");
                            string mehediErrParam = ParseMehediQueryParam(shihabPath, "error");
                            bool shihabSuccess = shihabOkParam == "1" || string.Equals(shihabOkParam, "true", StringComparison.OrdinalIgnoreCase);
                            ShihabBrowserRelayHub.CompleteShihabRelayJob(mehediJobId, shihabSuccess, mehediErrParam);
                            WriteRawMehediHttpResponse(shihabStream, 200, "OK", "{\"ok\":true}", allowedCorsOrigin);
                            return;
                        }

                        if (shihabCleanPath == "/shihab/toggle" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
                        {
                            _shihabConfig.MehediBrowserIntegrationEnabled = !_shihabConfig.MehediBrowserIntegrationEnabled;
                            WriteMehediJsonTcpResponse(shihabStream, 200, BuildShihabStatusReply("Toggled integration"), allowedCorsOrigin);
                            return;
                        }

                        if (shihabCleanPath == "/shihab/yt-streams" && string.Equals(mehediMethod, "GET", StringComparison.OrdinalIgnoreCase))
                        {
                            string mehediYtId = ParseMehediQueryParam(shihabPath, "videoId");
                            string shihabVisData = ParseMehediQueryParam(shihabPath, "visitorData");
                            bool mehediForceYt = string.Equals(ParseMehediQueryParam(shihabPath, "force"), "1", StringComparison.OrdinalIgnoreCase);
                            ShihabYouTubeStreamsReplyDto shihabYtReply = MehediYouTubeInnerTubeResolver.ResolveShihabYouTubeStreams(
                                mehediYtId,
                                shihabVisData,
                                mehediForceYt);
                            WriteMehediJsonTcpResponse(shihabStream, shihabYtReply.ShihabOk ? 200 : 400, shihabYtReply, allowedCorsOrigin);
                            return;
                        }

                        if (shihabCleanPath == "/shihab/capture" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
                        {
                            ShihabCapturedLinkDto shihabDto = DeserializeMehediJson<ShihabCapturedLinkDto>(mehediBody);
                            if (shihabDto != null && !string.IsNullOrWhiteSpace(shihabDto.ShihabUrl))
                            {
                                if (OnShihabSingleCaptureReceived != null)
                                {
                                    OnShihabSingleCaptureReceived(shihabDto);
                                }
                                else
                                {
                                    ShihabDownloadItem headlessItem = CreateItemFromCaptureDto(shihabDto);
                                    _mehediEngine.AddShihabDownloadItem(headlessItem, !shihabDto.MehediAddToQueueOnly);
                                }
                                WriteMehediJsonTcpResponse(shihabStream, 200, BuildShihabStatusReply("Download transferred to MHS Download Manager"), allowedCorsOrigin);
                                return;
                            }
                            WriteMehediJsonTcpResponse(shihabStream, 400, BuildShihabErrorReply("Missing URL"), allowedCorsOrigin);
                            return;
                        }

                        if (shihabCleanPath == "/shihab/capture-batch" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
                        {
                            ShihabBatchCaptureDto mehediBatchDto = DeserializeMehediJson<ShihabBatchCaptureDto>(mehediBody);
                            if (mehediBatchDto != null && mehediBatchDto.ShihabLinks != null && mehediBatchDto.ShihabLinks.Count > 0)
                            {
                                if (OnMehediBatchCaptureReceived != null)
                                {
                                    OnMehediBatchCaptureReceived(mehediBatchDto);
                                }
                                WriteMehediJsonTcpResponse(shihabStream, 200, BuildShihabStatusReply(
                                    string.Format("Transferred {0} links to MHS Download Manager", mehediBatchDto.ShihabLinks.Count)), allowedCorsOrigin);
                                return;
                            }
                            WriteMehediJsonTcpResponse(shihabStream, 400, BuildShihabErrorReply("No links supplied"), allowedCorsOrigin);
                            return;
                        }

                        if (shihabCleanPath == "/shihab/start-queue" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
                        {
                            if (OnShihabStartMainQueueRequested != null)
                            {
                                OnShihabStartMainQueueRequested();
                            }
                            else
                            {
                                _mehediEngine.StartShihabQueue("Main Download Queue");
                            }
                            WriteMehediJsonTcpResponse(shihabStream, 200, BuildShihabStatusReply("Main Download Queue started"), allowedCorsOrigin);
                            return;
                        }

                        WriteMehediJsonTcpResponse(shihabStream, 404, BuildShihabErrorReply("Unknown Shihab endpoint"), allowedCorsOrigin);
                    }
                }
                catch
                {
                }
            }
        }

        private ShihabDownloadItem CreateItemFromApiOrCapture(
            string url,
            string fileName,
            string checksum,
            string kind,
            string format,
            bool audioOnly,
            bool incognito,
            string postBody)
        {
            ShihabDownloadItem item = new ShihabDownloadItem();
            item.MehediUrl = url;
            item.ShihabFileName = string.IsNullOrWhiteSpace(fileName)
                ? ShihabFormatHelpers.ExtractMehediFileNameFromUrl(url)
                : ShihabFormatHelpers.SanitizeShihabFileName(fileName);
            item.ShihabExpectedChecksum = checksum ?? "";
            item.ShihabIsPrivateIncognito = incognito;
            item.ShihabPostBody = postBody ?? "";

            string k = (kind ?? "").Trim().ToLowerInvariant();
            if (k == "ytdlp" || k == "video" || k == "playlist")
            {
                item.ShihabIsYtDlpJob = true;
                item.ShihabYtDlpFormatSelector = format ?? "";
                item.ShihabAudioOnlyExtract = audioOnly;
            }
            else if (k == "gallery")
            {
                item.ShihabIsGalleryJob = true;
            }
            else if (k == "torrent" || url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) || url.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
            {
                item.ShihabIsTorrentOrMagnet = true;
                item.ShihabTorrentMagnetOrPath = url;
            }

            ShihabCategoryRule cat = _shihabConfig.ResolveMehediCategoryForFile(item.ShihabFileName);
            item.ShihabCategoryName = cat.ShihabName;
            item.MehediSaveDirectory = cat.ShihabDefaultFolder;
            item.ShihabFileName = ShihabFormatHelpers.EnsureShihabUniqueFilePath(item.MehediSaveDirectory, item.ShihabFileName);
            return item;
        }

        private ShihabDownloadItem CreateItemFromCaptureDto(ShihabCapturedLinkDto dto)
        {
            ShihabDownloadItem item = CreateItemFromApiOrCapture(
                dto.ShihabUrl,
                dto.ShihabFileName,
                dto.ShihabExpectedChecksum,
                dto.ShihabJobKind,
                dto.ShihabFormatSelector,
                dto.ShihabAudioOnly,
                dto.ShihabIncognito,
                dto.ShihabPostBody);
            item.MehediReferer = dto.MehediReferrer ?? "";
            item.ShihabCookies = dto.ShihabCookies ?? "";
            if (!string.IsNullOrWhiteSpace(dto.MehediUserAgent)) item.MehediUserAgent = dto.MehediUserAgent;
            if (!string.IsNullOrWhiteSpace(dto.ShihabSaveDirectory)) item.MehediSaveDirectory = dto.ShihabSaveDirectory;
            item.MehediOriginHeader = dto.ShihabOrigin ?? "";
            item.MehediOmitCookies = dto.MehediOmitCookies;
            item.MehediAudioCompanionUrl = dto.ShihabAudioUrl ?? "";
            item.ShihabAudioCompanionBytes = dto.MehediAudioSize;
            item.ShihabSourceTabId = dto.ShihabTabId;
            item.ShihabQualityLabel = dto.MehediQualityLabel ?? "";
            return item;
        }

        private void ExecuteBridgeActionOnEngine(string act, string targetId)
        {
            if (act == "start_queue")
            {
                _mehediEngine.StartShihabQueue("Main Download Queue");
                return;
            }
            if (act == "stop_queue")
            {
                _mehediEngine.StopShihabQueue("Main Download Queue");
                return;
            }
            List<ShihabDownloadItem> snap = _mehediEngine.GetShihabDownloadsSnapshot();
            for (int i = 0; i < snap.Count; i++)
            {
                ShihabDownloadItem it = snap[i];
                bool matches = string.Equals(targetId, "all", StringComparison.OrdinalIgnoreCase) ||
                               (!string.IsNullOrEmpty(targetId) && it.ShihabId.StartsWith(targetId, StringComparison.OrdinalIgnoreCase));
                if (!matches) continue;
                if (act == "pause") _mehediEngine.PauseShihabDownload(it);
                else if (act == "start" || act == "resume") _mehediEngine.StartOrResumeMehediDownload(it);
                else if (act == "remove") _mehediEngine.RemoveShihabDownload(it, false);
                else if (act == "delete") _mehediEngine.RemoveShihabDownload(it, true);
            }
        }

        private static string EscapeJsonVal(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        }

        private static string ParseMehediQueryParam(string shihabRawPath, string mehediParamName)
        {
            if (string.IsNullOrEmpty(shihabRawPath) || string.IsNullOrEmpty(mehediParamName)) return "";
            int shihabQPos = shihabRawPath.IndexOf('?');
            if (shihabQPos < 0 || shihabQPos + 1 >= shihabRawPath.Length) return "";
            string mehediQuery = shihabRawPath.Substring(shihabQPos + 1);
            string[] shihabPairs = mehediQuery.Split('&');
            for (int mehediI = 0; mehediI < shihabPairs.Length; mehediI++)
            {
                string shihabPair = shihabPairs[mehediI];
                int mehediEq = shihabPair.IndexOf('=');
                if (mehediEq > 0)
                {
                    string shihabK = Uri.UnescapeDataString(shihabPair.Substring(0, mehediEq));
                    if (string.Equals(shihabK, mehediParamName, StringComparison.OrdinalIgnoreCase))
                    {
                        return Uri.UnescapeDataString(shihabPair.Substring(mehediEq + 1).Replace('+', ' '));
                    }
                }
            }
            return "";
        }

        private static bool ReadParsedShihabHttpRequest(
            NetworkStream mehediStream,
            out string shihabMethod,
            out string mehediPath,
            out string originHeader,
            out string bridgeTokenHeader,
            out string shihabBody,
            out byte[] mehediRawBodyBytes,
            out int shihabRawBodyCount)
        {
            shihabMethod = "";
            mehediPath = "/";
            originHeader = "";
            bridgeTokenHeader = "";
            shihabBody = "";
            mehediRawBodyBytes = null;
            shihabRawBodyCount = 0;

            MemoryStream mehediHeaderBuf = new MemoryStream();
            int shihabPrev3 = -1, mehediPrev2 = -1, shihabPrev1 = -1;
            while (mehediHeaderBuf.Length < 65536)
            {
                int mehediByte = mehediStream.ReadByte();
                if (mehediByte < 0) break;
                mehediHeaderBuf.WriteByte((byte)mehediByte);
                if (shihabPrev3 == '\r' && mehediPrev2 == '\n' && shihabPrev1 == '\r' && mehediByte == '\n')
                {
                    break;
                }
                shihabPrev3 = mehediPrev2;
                mehediPrev2 = shihabPrev1;
                shihabPrev1 = mehediByte;
            }

            string shihabHeaderText = Encoding.ASCII.GetString(mehediHeaderBuf.ToArray());
            if (string.IsNullOrWhiteSpace(shihabHeaderText)) return false;

            string[] mehediLines = shihabHeaderText.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (mehediLines.Length == 0) return false;

            string[] shihabReqParts = mehediLines[0].Split(' ');
            if (shihabReqParts.Length < 2) return false;
            shihabMethod = shihabReqParts[0].Trim();
            mehediPath = shihabReqParts[1].Trim();

            int shihabContentLen = 0;
            for (int mehediI = 1; mehediI < mehediLines.Length; mehediI++)
            {
                string shihabLine = mehediLines[mehediI];
                if (shihabLine.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(shihabLine.Substring(15).Trim(), out shihabContentLen);
                }
                else if (shihabLine.StartsWith("Origin:", StringComparison.OrdinalIgnoreCase))
                {
                    originHeader = shihabLine.Substring(7).Trim();
                }
                else if (shihabLine.StartsWith("X-MHS-Bridge-Token:", StringComparison.OrdinalIgnoreCase))
                {
                    bridgeTokenHeader = shihabLine.Substring(19).Trim();
                }
                else if (string.IsNullOrEmpty(bridgeTokenHeader) && shihabLine.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                {
                    bridgeTokenHeader = shihabLine.Substring(14).Trim();
                }
            }

            string mehediLowerPathOnly = mehediPath.Split('?')[0].ToLowerInvariant();
            bool shihabIsBinaryRelay = mehediLowerPathOnly == "/shihab/relay-chunk";

            if (shihabContentLen > 0 && shihabContentLen <= 32 * 1024 * 1024)
            {
                mehediRawBodyBytes = new byte[shihabContentLen];
                shihabRawBodyCount = ReadExactShihabBytes(mehediStream, mehediRawBodyBytes, shihabContentLen);
                if (!shihabIsBinaryRelay)
                {
                    shihabBody = Encoding.UTF8.GetString(mehediRawBodyBytes, 0, shihabRawBodyCount);
                }
            }
            return true;
        }

        private static void WriteMehediJsonTcpResponse<T>(NetworkStream shihabStream, int mehediStatus, T shihabPayload, string allowedCorsOrigin)
        {
            string mehediJson = SerializeShihabJson(shihabPayload);
            string shihabReason = mehediStatus == 200 ? "OK" : (mehediStatus == 400 ? "Bad Request" : (mehediStatus == 401 ? "Unauthorized" : (mehediStatus == 403 ? "Forbidden" : "Error")));
            WriteRawMehediHttpResponse(shihabStream, mehediStatus, shihabReason, mehediJson, allowedCorsOrigin);
        }

        private static void WriteRawMehediHttpResponse(NetworkStream shihabStream, int mehediStatus, string shihabReason, string mehediJsonBody, string allowedCorsOrigin)
        {
            byte[] shihabBodyBytes = Encoding.UTF8.GetBytes(mehediJsonBody ?? "");
            StringBuilder hdr = new StringBuilder(384);
            hdr.AppendFormat("HTTP/1.1 {0} {1}\r\n", mehediStatus, shihabReason);
            hdr.Append("Content-Type: application/json; charset=utf-8\r\n");
            if (!string.IsNullOrEmpty(allowedCorsOrigin))
            {
                hdr.Append("Access-Control-Allow-Origin: ").Append(allowedCorsOrigin).Append("\r\n");
                hdr.Append("Vary: Origin\r\n");
                hdr.Append("Access-Control-Allow-Private-Network: true\r\n");
                hdr.Append("Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n");
                hdr.Append("Access-Control-Allow-Headers: Content-Type, X-Shihab-Client, X-MHS-Bridge-Token, Authorization\r\n");
            }
            hdr.Append("Connection: close\r\n");
            hdr.AppendFormat("Content-Length: {0}\r\n\r\n", shihabBodyBytes.Length);
            byte[] shihabHdrBytes = Encoding.ASCII.GetBytes(hdr.ToString());
            shihabStream.Write(shihabHdrBytes, 0, shihabHdrBytes.Length);
            if (shihabBodyBytes.Length > 0)
            {
                shihabStream.Write(shihabBodyBytes, 0, shihabBodyBytes.Length);
            }
            shihabStream.Flush();
        }

        private MehediBridgeStatusReply BuildShihabStatusReply(string mehediMsg)
        {
            List<ShihabDownloadItem> shihabSnap = _mehediEngine.GetShihabDownloadsSnapshot();
            int mehediActive = 0;
            for (int shihabI = 0; shihabI < shihabSnap.Count; shihabI++)
            {
                if (_mehediEngine.IsMehediItemActive(shihabSnap[shihabI].ShihabId))
                {
                    mehediActive++;
                }
            }
            ShihabUpdateCheckResult cachedUpd = ShihabAutoUpdater.LastCachedUpdate;
            bool updAvail = cachedUpd != null && cachedUpd.UpdateAvailable;
            string curVer = ShihabAutoUpdater.GetCurrentAppVersion();
            return new MehediBridgeStatusReply
            {
                ShihabOk = true,
                MehediAppName = "MHS Download Manager",
                ShihabVersion = curVer,
                MehediIntegrationEnabled = _shihabConfig.MehediBrowserIntegrationEnabled,
                ShihabShowVideoPanel = _shihabConfig.ShihabShowVideoFloatingPanel,
                MehediPanelMiniMode = _shihabConfig.ShihabPanelMiniMode,
                ShihabPanelSkipAutoStart = _shihabConfig.MehediPanelSkipAutoStart,
                MehediMinVideoSizeKb = _shihabConfig.MehediMinVideoSizeKb,
                ShihabAutoCaptureExtensions = _shihabConfig.MehediAutoCaptureExtensions,
                MehediExcludedSites = _shihabConfig.ShihabExcludedSitesSpaceSeparated,
                ShihabExcludedAddresses = _shihabConfig.MehediExcludedAddressesSpaceSeparated ?? "",
                MehediPreventKey = string.IsNullOrWhiteSpace(_shihabConfig.MehediPreventKeyName) ? "Alt" : _shihabConfig.MehediPreventKeyName,
                ShihabForceKey = string.IsNullOrWhiteSpace(_shihabConfig.ShihabForceKeyName) ? "Ins" : _shihabConfig.ShihabForceKeyName,
                ShihabCaptureOpenedFiles = _shihabConfig.ShihabCaptureOpenedFiles,
                ShihabCaptureMinSizeKb = _shihabConfig.ShihabCaptureMinSizeKb,
                ShihabActiveDownloads = mehediActive,
                ShihabUpdateAvailable = updAvail,
                ShihabLatestVersion = updAvail ? cachedUpd.LatestVersion : curVer,
                ShihabLatestHtmlUrl = updAvail ? cachedUpd.HtmlUrl : "",
                MehediMessage = mehediMsg
            };
        }

        private static MehediBridgeStatusReply BuildShihabErrorReply(string mehediErr)
        {
            return new MehediBridgeStatusReply
            {
                ShihabOk = false,
                MehediAppName = "MHS Download Manager",
                ShihabVersion = ShihabAutoUpdater.GetCurrentAppVersion(),
                MehediMessage = mehediErr
            };
        }

        public static T DeserializeMehediJson<T>(string shihabJson) where T : class
        {
            if (string.IsNullOrWhiteSpace(shihabJson)) return null;
            string mehediClean = shihabJson.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            byte[] mehediBytes = Encoding.UTF8.GetBytes(mehediClean);
            using (MemoryStream shihabMs = new MemoryStream(mehediBytes))
            {
                DataContractJsonSerializer mehediSer = new DataContractJsonSerializer(typeof(T));
                return mehediSer.ReadObject(shihabMs) as T;
            }
        }

        public static string SerializeShihabJson<T>(T mehediObj)
        {
            using (MemoryStream shihabMs = new MemoryStream())
            {
                DataContractJsonSerializer mehediSer = new DataContractJsonSerializer(typeof(T));
                mehediSer.WriteObject(shihabMs, mehediObj);
                return Encoding.UTF8.GetString(shihabMs.ToArray());
            }
        }

        public static bool SendUrlToExistingShihabInstance(int shihabPort, string url, string saveDir, string fileName)
        {
            try
            {
                ShihabCapturedLinkDto shihabDto = new ShihabCapturedLinkDto
                {
                    ShihabUrl = url ?? "",
                    ShihabFileName = fileName ?? "",
                    ShihabSaveDirectory = saveDir ?? "",
                    ShihabSilentMode = true
                };
                string mehediJson = SerializeShihabJson(shihabDto);
                PostLocalShihabJson(shihabPort, "/shihab/capture", mehediJson, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool ForwardCliToRunningShihabInstance(ShihabCliParsedArguments mehediCli, int shihabPort)
        {
            try
            {
                string customDir = mehediCli != null ? mehediCli.MehediCustomConfigDir : null;
                if (!string.IsNullOrWhiteSpace(mehediCli.ShihabDirectUrl))
                {
                    ShihabCapturedLinkDto shihabDto = new ShihabCapturedLinkDto
                    {
                        ShihabUrl = mehediCli.ShihabDirectUrl,
                        ShihabFileName = mehediCli.ShihabLocalFileName ?? "",
                        ShihabSaveDirectory = mehediCli.MehediLocalSavePath ?? "",
                        ShihabSilentMode = mehediCli.MehediSilentNoPrompt,
                        MehediAddToQueueOnly = mehediCli.MehediAddToQueueOnly,
                        MehediExitWhenDone = mehediCli.ShihabExitOnDone
                    };
                    string mehediJson = SerializeShihabJson(shihabDto);
                    PostLocalShihabJson(shihabPort, "/shihab/capture", mehediJson, customDir);
                }
                if (mehediCli.ShihabStartMainQueue)
                {
                    PostLocalShihabJson(shihabPort, "/shihab/start-queue", "{}", customDir);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string PostLocalShihabJson(int mehediPort, string shihabPath, string mehediJsonBody)
        {
            return PostLocalShihabJson(mehediPort, shihabPath, mehediJsonBody, null);
        }

        public static string PostLocalShihabJson(int mehediPort, string shihabPath, string mehediJsonBody, string customConfigDir)
        {
            HttpWebRequest shihabReq = (HttpWebRequest)WebRequest.Create(string.Format("http://127.0.0.1:{0}{1}", mehediPort, shihabPath));
            shihabReq.Method = "POST";
            shihabReq.Timeout = 3000;
            shihabReq.Proxy = null;
            shihabReq.ContentType = "application/json; charset=utf-8";
            try
            {
                ShihabStateStore tokenStore = new ShihabStateStore(customConfigDir);
                string token = tokenStore.LoadOrCreateBridgeToken();
                if (!string.IsNullOrEmpty(token))
                {
                    shihabReq.Headers["X-MHS-Bridge-Token"] = token;
                }
            }
            catch { }
            byte[] mehediData = Encoding.UTF8.GetBytes(mehediJsonBody ?? "{}");
            shihabReq.ContentLength = mehediData.Length;
            using (Stream shihabOut = shihabReq.GetRequestStream())
            {
                shihabOut.Write(mehediData, 0, mehediData.Length);
            }
            using (HttpWebResponse mehediResp = (HttpWebResponse)shihabReq.GetResponse())
            using (StreamReader shihabReader = new StreamReader(mehediResp.GetResponseStream(), Encoding.UTF8))
            {
                return shihabReader.ReadToEnd();
            }
        }

        public static string GetLocalMehediPing(int shihabPort)
        {
            HttpWebRequest mehediReq = (HttpWebRequest)WebRequest.Create(string.Format("http://127.0.0.1:{0}/shihab/ping", shihabPort));
            mehediReq.Method = "GET";
            mehediReq.Timeout = 2000;
            using (HttpWebResponse shihabResp = (HttpWebResponse)mehediReq.GetResponse())
            using (StreamReader mehediReader = new StreamReader(shihabResp.GetResponseStream(), Encoding.UTF8))
            {
                return mehediReader.ReadToEnd();
            }
        }

        public static int RunShihabNativeMessagingStdioLoop(int mehediBridgePort)
        {
            // Mirrors IDMMsgHost.exe: 4-byte little-endian length prefix + UTF-8 JSON over stdin/stdout
            Stream shihabStdin = Console.OpenStandardInput();
            Stream mehediStdout = Console.OpenStandardOutput();
            byte[] shihabLenBuf = new byte[4];

            while (true)
            {
                int mehediReadHeader = ReadExactShihabBytes(shihabStdin, shihabLenBuf, 4);
                if (mehediReadHeader == 0)
                {
                    break;
                }
                if (mehediReadHeader < 4)
                {
                    return 1;
                }
                int shihabMsgLen = BitConverter.ToInt32(shihabLenBuf, 0);
                if (shihabMsgLen <= 0 || shihabMsgLen > 16 * 1024 * 1024)
                {
                    break;
                }
                byte[] mehediMsgBytes = new byte[shihabMsgLen];
                int shihabGot = ReadExactShihabBytes(shihabStdin, mehediMsgBytes, shihabMsgLen);
                if (shihabGot < shihabMsgLen)
                {
                    break;
                }

                string mehediInputJson = Encoding.UTF8.GetString(mehediMsgBytes);
                string shihabReplyJson = ProcessSingleMehediNativeMessage(mehediInputJson, mehediBridgePort);
                byte[] mehediReplyBytes = Encoding.UTF8.GetBytes(shihabReplyJson);
                byte[] shihabOutLen = BitConverter.GetBytes(mehediReplyBytes.Length);
                mehediStdout.Write(shihabOutLen, 0, 4);
                mehediStdout.Write(mehediReplyBytes, 0, mehediReplyBytes.Length);
                mehediStdout.Flush();
            }
            return 0;
        }

        private static int ReadExactShihabBytes(Stream mehediStream, byte[] shihabBuffer, int mehediCount)
        {
            int shihabOffset = 0;
            while (shihabOffset < mehediCount)
            {
                int mehediGot = mehediStream.Read(shihabBuffer, shihabOffset, mehediCount - shihabOffset);
                if (mehediGot <= 0)
                {
                    return shihabOffset;
                }
                shihabOffset += mehediGot;
            }
            return shihabOffset;
        }

        private static string ProcessSingleMehediNativeMessage(string shihabInputJson, int mehediPort)
        {
            try
            {
                ShihabNativeEnvelopeDto mehediEnv = DeserializeMehediJson<ShihabNativeEnvelopeDto>(shihabInputJson);
                string shihabAct = (mehediEnv != null && mehediEnv.ShihabAction != null)
                    ? mehediEnv.ShihabAction.ToLowerInvariant()
                    : "ping";

                EnsureShihabDesktopAppRunning(mehediPort);

                if (shihabAct == "yt-streams" && mehediEnv != null && !string.IsNullOrWhiteSpace(mehediEnv.MehediVideoId))
                {
                    return SerializeShihabJson(MehediYouTubeInnerTubeResolver.ResolveShihabYouTubeStreams(
                        mehediEnv.MehediVideoId,
                        mehediEnv.ShihabVisitorData,
                        false));
                }
                if (shihabAct == "capture" && mehediEnv.MehediCapture != null)
                {
                    return PostLocalShihabJson(mehediPort, "/shihab/capture", SerializeShihabJson(mehediEnv.MehediCapture));
                }
                if (shihabAct == "capture-batch" && mehediEnv.ShihabBatch != null)
                {
                    return PostLocalShihabJson(mehediPort, "/shihab/capture-batch", SerializeShihabJson(mehediEnv.ShihabBatch));
                }
                return GetLocalMehediPing(mehediPort);
            }
            catch (Exception shihabEx)
            {
                return SerializeShihabJson(BuildShihabErrorReply("NativeHost error: " + shihabEx.Message));
            }
        }

        private static void EnsureShihabDesktopAppRunning(int mehediPort)
        {
            try
            {
                GetLocalMehediPing(mehediPort);
                return;
            }
            catch
            {
            }
            try
            {
                string shihabExePath = Process.GetCurrentProcess().MainModule.FileName;
                ProcessStartInfo mehediPsi = new ProcessStartInfo(shihabExePath);
                mehediPsi.UseShellExecute = false;
                Process.Start(mehediPsi);
                for (int shihabWait = 0; shihabWait < 15; shihabWait++)
                {
                    Thread.Sleep(200);
                    try
                    {
                        GetLocalMehediPing(mehediPort);
                        return;
                    }
                    catch { }
                }
            }
            catch { }
        }

        public static string RegisterShihabNativeMessagingHostForCurrentUser(string mehediExtensionId, string shihabDataDir)
        {
            string mehediCleanId = (mehediExtensionId ?? "").Trim();
            if (string.IsNullOrEmpty(mehediCleanId))
            {
                throw new ArgumentException("Please enter your Chrome/Edge unpacked Extension ID (32 lowercase letters).");
            }
            string shihabExePath = Process.GetCurrentProcess().MainModule.FileName;
            string mehediWrapperBat = Path.Combine(shihabDataDir, "shihab_native_host_launcher.cmd");
            File.WriteAllText(
                mehediWrapperBat,
                "@echo off\r\n\"" + shihabExePath + "\" --shihab-native-messaging-host %*\r\n",
                Encoding.ASCII);

            string shihabManifestPath = Path.Combine(shihabDataDir, "com.shihab.mehedi.idm.json");
            string mehediEscapedBat = mehediWrapperBat.Replace("\\", "\\\\");
            string shihabManifestJson =
                "{\r\n" +
                "  \"name\": \"com.shihab.mehedi.idm\",\r\n" +
                "  \"description\": \"MHS Download Manager Native Messaging Host (mdm.exe)\",\r\n" +
                "  \"path\": \"" + mehediEscapedBat + "\",\r\n" +
                "  \"type\": \"stdio\",\r\n" +
                "  \"allowed_origins\": [\r\n" +
                "    \"chrome-extension://" + mehediCleanId + "/\"\r\n" +
                "  ]\r\n" +
                "}\r\n";
            File.WriteAllText(shihabManifestPath, shihabManifestJson, Encoding.UTF8);

            // Also write Firefox/Gecko native messaging manifest (Zen, LibreWolf, Floorp, Waterfox, Firefox)
            string shihabFirefoxManifestPath = Path.Combine(shihabDataDir, "com.shihab.mehedi.idm.firefox.json");
            string shihabFirefoxManifestJson =
                "{\r\n" +
                "  \"name\": \"com.shihab.mehedi.idm\",\r\n" +
                "  \"description\": \"MHS Download Manager Native Messaging Host (mdm.exe)\",\r\n" +
                "  \"path\": \"" + mehediEscapedBat + "\",\r\n" +
                "  \"type\": \"stdio\",\r\n" +
                "  \"allowed_extensions\": [\r\n" +
                "    \"mhs-download-manager@shihab.mehedi.idm\"\r\n" +
                "  ]\r\n" +
                "}\r\n";
            File.WriteAllText(shihabFirefoxManifestPath, shihabFirefoxManifestJson, Encoding.UTF8);

            // Register across all 9 Chromium-family browsers
            string[] mehediChromiumKeys = new string[]
            {
                @"Software\Google\Chrome\NativeMessagingHosts\com.shihab.mehedi.idm",
                @"Software\Chromium\NativeMessagingHosts\com.shihab.mehedi.idm",
                @"Software\Microsoft\Edge\NativeMessagingHosts\com.shihab.mehedi.idm",
                @"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\com.shihab.mehedi.idm",
                @"Software\Vivaldi\NativeMessagingHosts\com.shihab.mehedi.idm",
                @"Software\Opera Software\NativeMessagingHosts\com.shihab.mehedi.idm",
                @"Software\Helium\NativeMessagingHosts\com.shihab.mehedi.idm",
                @"Software\Thorium\NativeMessagingHosts\com.shihab.mehedi.idm",
                @"Software\Arc\NativeMessagingHosts\com.shihab.mehedi.idm"
            };
            for (int shihabI = 0; shihabI < mehediChromiumKeys.Length; shihabI++)
            {
                using (RegistryKey mehediKey = Registry.CurrentUser.CreateSubKey(mehediChromiumKeys[shihabI]))
                {
                    if (mehediKey != null)
                    {
                        mehediKey.SetValue("", shihabManifestPath, RegistryValueKind.String);
                    }
                }
            }

            // Register across all 5 Firefox/Gecko-family browsers
            string[] mehediFirefoxKeys = new string[]
            {
                @"Software\Mozilla\NativeMessagingHosts\com.shihab.mehedi.idm",
                @"Software\Zen\NativeMessagingHosts\com.shihab.mehedi.idm",
                @"Software\LibreWolf\NativeMessagingHosts\com.shihab.mehedi.idm",
                @"Software\Floorp\NativeMessagingHosts\com.shihab.mehedi.idm",
                @"Software\Waterfox\NativeMessagingHosts\com.shihab.mehedi.idm"
            };
            for (int shihabF = 0; shihabF < mehediFirefoxKeys.Length; shihabF++)
            {
                using (RegistryKey mehediKey = Registry.CurrentUser.CreateSubKey(mehediFirefoxKeys[shihabF]))
                {
                    if (mehediKey != null)
                    {
                        mehediKey.SetValue("", shihabFirefoxManifestPath, RegistryValueKind.String);
                    }
                }
            }
            return shihabManifestPath;
        }

        public void Dispose()
        {
            try
            {
                _shihabIsRunning = false;
                if (_mehediStopCts != null) _mehediStopCts.Cancel();
                if (_shihabTcpListener != null)
                {
                    _shihabTcpListener.Stop();
                }
            }
            catch { }
        }
    }
}

