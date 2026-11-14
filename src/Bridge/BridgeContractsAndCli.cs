using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

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
                         mehediTok.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase) ||
                         mehediTok.StartsWith("moz-extension://", StringComparison.OrdinalIgnoreCase) ||
                         shihabLower == "idm@shihab.mehedi" ||
                         shihabLower.EndsWith("\\com.shihab.mehedi.idm.json", StringComparison.Ordinal) ||
                         shihabLower.EndsWith("/com.shihab.mehedi.idm.json", StringComparison.Ordinal) ||
                         shihabLower.EndsWith("\\com.shihab.mehedi.idm.firefox.json", StringComparison.Ordinal) ||
                         shihabLower.EndsWith("/com.shihab.mehedi.idm.firefox.json", StringComparison.Ordinal))
                {
                    mehediParsed.ShihabRunAsNativeHost = true;
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
}
