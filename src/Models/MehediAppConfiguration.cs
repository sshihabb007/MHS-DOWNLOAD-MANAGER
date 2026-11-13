using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using Microsoft.Win32;

namespace ShihabMehediIDM
{
    [DataContract]
    public class MehediAppConfiguration
    {
        public const string DefaultShihabAutoCaptureExtensions =
            "3GP 7Z AAC ACE AIF APK ARJ ASF AVI BIN BZ2 EXE GZ GZIP IMG ISO LZH M4A M4V MKV MOV MP3 MP4 MPA MPE MPEG MPG MSI MSU OGG OGV PDF PLJ PPS PPT PPTX QT R0* R1* RA RAR RM RMVB SEA SIT SITX TAR TIF TIFF TORRENT WAV WEBM WMA WMV XZ Z ZIP";

        public const string DefaultMehediExcludedSites =
            "*.update.microsoft.com download.windowsupdate.com *.download.windowsupdate.com siteseal.thawte.com ecom.cimetz.com *.voice2page.com localhost 127.0.0.1 *.local";

        public const string DefaultShihabBrowsersCsv =
            "Google Chrome,Chromium,Microsoft Edge,Brave Browser,Vivaldi,Opera,Helium,Thorium,Arc,Mozilla Firefox,Zen Browser,LibreWolf,Floorp,Waterfox";

        public const string DefaultMehediContextMenuCsv =
            "Download with MHS,Download video with MHS,Download all links with MHS";

        [DataMember] public bool ShihabDarkMode { get; set; }
        [DataMember] public bool MehediShowCategoriesPanel { get; set; }
        [DataMember] public bool ShihabLaunchOnStartup { get; set; }
        [DataMember] public bool ShihabMonitorClipboard { get; set; }
        [DataMember] public bool MehediBrowserIntegrationEnabled { get; set; }
        [DataMember] public string MehediEnabledBrowsersCsv { get; set; }
        [DataMember] public List<string> ShihabCustomBrowserPaths { get; set; }
        [DataMember] public string MehediPreventKeyName { get; set; }
        [DataMember] public string ShihabForceKeyName { get; set; }
        [DataMember] public string MehediContextMenuItemsCsv { get; set; }
        [DataMember] public bool ShihabShowVideoFloatingPanel { get; set; }
        [DataMember] public bool ShihabPanelMiniMode { get; set; }
        [DataMember] public bool MehediPanelSkipAutoStart { get; set; }
        [DataMember] public int MehediMinVideoSizeKb { get; set; }
        [DataMember] public bool ShihabShowStartDownloadDialog { get; set; }
        [DataMember] public bool MehediShowDownloadProgressDialog { get; set; }
        [DataMember] public bool ShihabShowDownloadCompleteDialog { get; set; }
        [DataMember] public bool MehediShowFloatingDropTarget { get; set; }
        [DataMember] public int MehediDuplicateFileMode { get; set; }
        [DataMember] public bool ShihabEnableVirusCheck { get; set; }
        [DataMember] public string MehediVirusScannerExe { get; set; }
        [DataMember] public string ShihabVirusScannerArgs { get; set; }
        [DataMember] public string MehediConnectionProfile { get; set; }
        [DataMember] public int ShihabDefaultMaxConnections { get; set; }
        [DataMember] public bool ShihabAllowMoreThan32Connections { get; set; }
        [DataMember] public bool ShihabShedSlowConnections { get; set; }
        [DataMember] public bool ShihabVerifyResumeOverlap { get; set; }
        [DataMember] public bool ShihabEnableSparseFiles { get; set; }
        [DataMember] public bool ShihabWriteZoneIdentifier { get; set; }
        [DataMember] public bool ShihabPreventSystemSleep { get; set; }
        [DataMember] public bool ShihabFtpUsePassiveMode { get; set; }
        [DataMember] public bool ShihabFtpEnableTls { get; set; }
        [DataMember] public int ShihabAutoRetryIntervalSec { get; set; }
        [DataMember] public int MehediMaxRetryAttempts { get; set; }
        [DataMember] public bool MehediGlobalSpeedLimiterEnabled { get; set; }
        [DataMember] public int ShihabGlobalSpeedLimitKb { get; set; }
        [DataMember] public string MehediAutoCaptureExtensions { get; set; }
        [DataMember] public string ShihabExcludedSitesSpaceSeparated { get; set; }
        [DataMember] public string MehediExcludedAddressesSpaceSeparated { get; set; }
        [DataMember] public string MehediTemporaryDirectory { get; set; }
        [DataMember] public string ShihabDefaultUserAgent { get; set; }
        [DataMember] public string ShihabProxyMode { get; set; }
        [DataMember] public string ShihabPacScriptUrl { get; set; }
        [DataMember] public bool MehediUseHttpProxy { get; set; }
        [DataMember] public string ShihabProxyAddress { get; set; }
        [DataMember] public int MehediProxyPort { get; set; }
        [DataMember] public string ShihabProxyUser { get; set; }
        [DataMember] public string MehediProxyPassword { get; set; }
        [DataMember] public bool ShihabUseSocksProxy { get; set; }
        [DataMember] public string MehediSocksHost { get; set; }
        [DataMember] public int ShihabSocksPort { get; set; }
        [DataMember] public string ShihabDnsMode { get; set; }
        [DataMember] public string ShihabDnsProvider { get; set; }
        [DataMember] public string ShihabCustomDohUrl { get; set; }
        [DataMember] public bool ShihabDnsFallbackToSystem { get; set; }
        [DataMember] public string ShihabBoundNetworkAdapter { get; set; }
        [DataMember] public bool ShihabAutoResumeOnNetworkRestore { get; set; }
        [DataMember] public bool ShihabAutoExtractArchives { get; set; }
        [DataMember] public string ShihabExtractSubfolderMode { get; set; }
        [DataMember] public bool ShihabDeleteArchiveAfterExtract { get; set; }
        [DataMember] public bool ShihabTorrentSequentialDefault { get; set; }
        [DataMember] public bool ShihabTorrentKeepSeedingDefault { get; set; }
        [DataMember] public string ShihabTorrentBlocklistUrl { get; set; }
        [DataMember] public bool ShihabRemoteWebEnabled { get; set; }
        [DataMember] public int ShihabRemoteWebPort { get; set; }
        [DataMember] public string ShihabRemotePairingCode { get; set; }
        [DataMember] public List<ShihabPairedRemoteDevice> ShihabPairedRemoteDevices { get; set; }
        [DataMember] public bool ShihabAutomationApiEnabled { get; set; }
        [DataMember] public bool ShihabQuotaEnabled { get; set; }
        [DataMember] public int ShihabQuotaLimitMegabytes { get; set; }
        [DataMember] public int ShihabQuotaWindowHours { get; set; }
        [DataMember] public List<ShihabQuotaBucketEntry> ShihabQuotaHistory { get; set; }
        [DataMember] public string ShihabThemeName { get; set; }
        [DataMember] public int ShihabUiScalePercent { get; set; }
        [DataMember] public string ShihabLanguageCode { get; set; }
        [DataMember] public bool ShihabCaptureOpenedFiles { get; set; }
        [DataMember] public int ShihabCaptureMinSizeKb { get; set; }
        [DataMember] public bool ShihabCrashMonitorEnabled { get; set; }
        [DataMember] public string MehediDialUpConnectionName { get; set; }
        [DataMember] public bool ShihabRedialIfDisconnected { get; set; }
        [DataMember] public bool MehediHangUpWhenDone { get; set; }
        [DataMember] public bool MehediPlaySoundComplete { get; set; }
        [DataMember] public string ShihabSoundCompleteWav { get; set; }
        [DataMember] public bool MehediPlaySoundError { get; set; }
        [DataMember] public string ShihabSoundErrorWav { get; set; }
        [DataMember] public bool ShihabSetServerFileTimestamp { get; set; }
        [DataMember] public bool ShihabSetServerCreationDate { get; set; }
        [DataMember] public int MehediLocalBridgePort { get; set; }
        [DataMember] public string ShihabLocalBridgeToken { get; set; }
        [DataMember] public bool ShihabAutoCheckUpdates { get; set; }
        [DataMember] public bool ShihabUpdateIncludePrereleases { get; set; }
        [DataMember] public string ShihabUpdateRepoSlug { get; set; }
        [DataMember] public string ShihabUpdateApiOverrideUrl { get; set; }
        [DataMember] public string ShihabSkippedUpdateVersion { get; set; }
        [DataMember] public string ShihabLastUpdateCheckUtc { get; set; }
        [DataMember] public bool ShihabCloseToTray { get; set; }
        [DataMember] public bool ShihabMinimizeToTray { get; set; }
        [DataMember] public int ShihabDropTargetX { get; set; }
        [DataMember] public int ShihabDropTargetY { get; set; }
        [DataMember] public string ShihabPlainDnsServer { get; set; }
        [DataMember] public string ShihabNextDnsConfigId { get; set; }
        [DataMember] public string ShihabRemotePairingExpiresUtc { get; set; }
        [DataMember] public List<ShihabCategoryRule> ShihabCategories { get; set; }
        [DataMember] public List<MehediQueueConfig> MehediQueues { get; set; }
        [DataMember] public List<ShihabSiteLoginCredential> ShihabSiteLogins { get; set; }
        [DataMember] public List<ShihabHostConnectionLimit> ShihabHostConnectionLimits { get; set; }
        [DataMember] public List<ShihabDebridAccount> ShihabDebridAccounts { get; set; }
        [DataMember] public List<ShihabRssFeedConfig> ShihabRssFeeds { get; set; }

        [OnDeserializing]
        private void OnMehediConfigDeserializing(StreamingContext shihabCtx)
        {
            ShihabDarkMode = true;
            MehediShowCategoriesPanel = true;
            MehediBrowserIntegrationEnabled = true;
            MehediEnabledBrowsersCsv = DefaultShihabBrowsersCsv;
            ShihabCustomBrowserPaths = new List<string>();
            MehediPreventKeyName = "Alt";
            ShihabForceKeyName = "Ins";
            MehediContextMenuItemsCsv = DefaultMehediContextMenuCsv;
            ShihabShowVideoFloatingPanel = true;
            ShihabPanelMiniMode = false;
            MehediPanelSkipAutoStart = false;
            MehediMinVideoSizeKb = 300;
            ShihabShowStartDownloadDialog = true;
            MehediShowDownloadProgressDialog = true;
            ShihabShowDownloadCompleteDialog = true;
            MehediConnectionProfile = "High speed: Direct connection (Ethernet/Cable) / Wi-Fi / Mobile 4G/5G";
            ShihabDefaultMaxConnections = 8;
            ShihabAllowMoreThan32Connections = false;
            ShihabShedSlowConnections = true;
            ShihabVerifyResumeOverlap = true;
            ShihabEnableSparseFiles = true;
            ShihabWriteZoneIdentifier = true;
            ShihabPreventSystemSleep = true;
            ShihabFtpUsePassiveMode = true;
            ShihabFtpEnableTls = false;
            ShihabAutoRetryIntervalSec = 10;
            MehediMaxRetryAttempts = 99;
            ShihabGlobalSpeedLimitKb = 1024;
            MehediAutoCaptureExtensions = DefaultShihabAutoCaptureExtensions;
            ShihabExcludedSitesSpaceSeparated = DefaultMehediExcludedSites;
            MehediExcludedAddressesSpaceSeparated = "";
            ShihabDefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
            ShihabProxyMode = "None";
            ShihabPacScriptUrl = "";
            MehediProxyPort = 8080;
            ShihabSocksPort = 1080;
            ShihabDnsMode = "System";
            ShihabDnsProvider = "Cloudflare";
            ShihabCustomDohUrl = "https://cloudflare-dns.com/dns-query";
            ShihabDnsFallbackToSystem = true;
            ShihabPlainDnsServer = "1.1.1.1";
            ShihabNextDnsConfigId = "";
            ShihabBoundNetworkAdapter = "";
            ShihabAutoResumeOnNetworkRestore = true;
            ShihabAutoExtractArchives = false;
            ShihabExtractSubfolderMode = "Smart";
            ShihabDeleteArchiveAfterExtract = false;
            ShihabTorrentSequentialDefault = true;
            ShihabTorrentKeepSeedingDefault = false;
            ShihabTorrentBlocklistUrl = "";
            ShihabRemoteWebEnabled = false;
            ShihabRemoteWebPort = 8463;
            ShihabRemotePairingCode = "";
            ShihabRemotePairingExpiresUtc = "";
            ShihabPairedRemoteDevices = new List<ShihabPairedRemoteDevice>();
            ShihabAutomationApiEnabled = true;
            ShihabQuotaEnabled = false;
            ShihabQuotaLimitMegabytes = 10240;
            ShihabQuotaWindowHours = 24;
            ShihabQuotaHistory = new List<ShihabQuotaBucketEntry>();
            ShihabThemeName = "Obsidian Dark";
            ShihabUiScalePercent = 100;
            ShihabLanguageCode = "en";
            ShihabCaptureOpenedFiles = false;
            ShihabCaptureMinSizeKb = 0;
            ShihabCrashMonitorEnabled = true;
            MehediVirusScannerExe = "";
            ShihabVirusScannerArgs = "\"%1\"";
            MehediSocksHost = "";
            MehediDialUpConnectionName = "Default System Connection";
            ShihabSoundCompleteWav = "";
            ShihabSoundErrorWav = "";
            ShihabSetServerFileTimestamp = true;
            MehediLocalBridgePort = 45779;
            ShihabLocalBridgeToken = "";
            ShihabAutoCheckUpdates = true;
            ShihabUpdateIncludePrereleases = false;
            ShihabUpdateRepoSlug = "sshihabb007/MHS-DOWNLOAD-MANAGER";
            ShihabUpdateApiOverrideUrl = "";
            ShihabSkippedUpdateVersion = "";
            ShihabLastUpdateCheckUtc = "";
            ShihabCloseToTray = true;
            ShihabMinimizeToTray = false;
            ShihabDropTargetX = -1;
            ShihabDropTargetY = -1;
            ShihabHostConnectionLimits = new List<ShihabHostConnectionLimit>();
            ShihabDebridAccounts = new List<ShihabDebridAccount>();
            ShihabRssFeeds = new List<ShihabRssFeedConfig>();
        }

        public static MehediAppConfiguration CreateShihabDefault(string mehediBaseDataDir)
        {
            string shihabDownloadsRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            MehediAppConfiguration mehediCfg = new MehediAppConfiguration();
            mehediCfg.ShihabDarkMode = true;
            mehediCfg.MehediShowCategoriesPanel = true;
            mehediCfg.ShihabLaunchOnStartup = false;
            mehediCfg.ShihabMonitorClipboard = false;
            mehediCfg.MehediBrowserIntegrationEnabled = true;
            mehediCfg.MehediEnabledBrowsersCsv = DefaultShihabBrowsersCsv;
            mehediCfg.ShihabCustomBrowserPaths = new List<string>();
            mehediCfg.MehediPreventKeyName = "Alt";
            mehediCfg.ShihabForceKeyName = "Ins";
            mehediCfg.MehediContextMenuItemsCsv = DefaultMehediContextMenuCsv;
            mehediCfg.ShihabShowVideoFloatingPanel = true;
            mehediCfg.ShihabPanelMiniMode = false;
            mehediCfg.MehediPanelSkipAutoStart = false;
            mehediCfg.MehediMinVideoSizeKb = 300;
            mehediCfg.ShihabShowStartDownloadDialog = true;
            mehediCfg.MehediShowDownloadProgressDialog = true;
            mehediCfg.ShihabShowDownloadCompleteDialog = true;
            mehediCfg.MehediShowFloatingDropTarget = false;
            mehediCfg.MehediDuplicateFileMode = 0;
            mehediCfg.ShihabEnableVirusCheck = false;
            mehediCfg.MehediVirusScannerExe = "";
            mehediCfg.ShihabVirusScannerArgs = "\"%1\"";
            mehediCfg.MehediConnectionProfile = "High speed: Direct connection (Ethernet/Cable) / Wi-Fi / Mobile 4G/5G";
            mehediCfg.ShihabDefaultMaxConnections = 8;
            mehediCfg.ShihabAllowMoreThan32Connections = false;
            mehediCfg.ShihabShedSlowConnections = true;
            mehediCfg.ShihabVerifyResumeOverlap = true;
            mehediCfg.ShihabEnableSparseFiles = true;
            mehediCfg.ShihabWriteZoneIdentifier = true;
            mehediCfg.ShihabPreventSystemSleep = true;
            mehediCfg.ShihabFtpUsePassiveMode = true;
            mehediCfg.ShihabFtpEnableTls = false;
            mehediCfg.ShihabAutoRetryIntervalSec = 10;
            mehediCfg.MehediMaxRetryAttempts = 99;
            mehediCfg.MehediGlobalSpeedLimiterEnabled = false;
            mehediCfg.ShihabGlobalSpeedLimitKb = 1024;
            mehediCfg.MehediAutoCaptureExtensions = DefaultShihabAutoCaptureExtensions;
            mehediCfg.ShihabExcludedSitesSpaceSeparated = DefaultMehediExcludedSites;
            mehediCfg.MehediExcludedAddressesSpaceSeparated = "";
            mehediCfg.MehediTemporaryDirectory = Path.Combine(mehediBaseDataDir, "DwnlData");
            mehediCfg.ShihabDefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
            mehediCfg.ShihabProxyMode = "None";
            mehediCfg.ShihabPacScriptUrl = "";
            mehediCfg.MehediUseHttpProxy = false;
            mehediCfg.ShihabProxyAddress = "";
            mehediCfg.MehediProxyPort = 8080;
            mehediCfg.ShihabProxyUser = "";
            mehediCfg.MehediProxyPassword = "";
            mehediCfg.ShihabUseSocksProxy = false;
            mehediCfg.MehediSocksHost = "";
            mehediCfg.ShihabSocksPort = 1080;
            mehediCfg.ShihabDnsMode = "System";
            mehediCfg.ShihabDnsProvider = "Cloudflare";
            mehediCfg.ShihabCustomDohUrl = "https://cloudflare-dns.com/dns-query";
            mehediCfg.ShihabDnsFallbackToSystem = true;
            mehediCfg.ShihabBoundNetworkAdapter = "";
            mehediCfg.ShihabAutoResumeOnNetworkRestore = true;
            mehediCfg.ShihabAutoExtractArchives = false;
            mehediCfg.ShihabExtractSubfolderMode = "Smart";
            mehediCfg.ShihabDeleteArchiveAfterExtract = false;
            mehediCfg.ShihabTorrentSequentialDefault = true;
            mehediCfg.ShihabTorrentKeepSeedingDefault = false;
            mehediCfg.ShihabTorrentBlocklistUrl = "";
            mehediCfg.ShihabRemoteWebEnabled = false;
            mehediCfg.ShihabRemoteWebPort = 8463;
            mehediCfg.ShihabRemotePairingCode = GenerateRandomEightDigitCode();
            mehediCfg.ShihabPairedRemoteDevices = new List<ShihabPairedRemoteDevice>();
            mehediCfg.ShihabAutomationApiEnabled = true;
            mehediCfg.ShihabQuotaEnabled = false;
            mehediCfg.ShihabQuotaLimitMegabytes = 10240;
            mehediCfg.ShihabQuotaWindowHours = 24;
            mehediCfg.ShihabQuotaHistory = new List<ShihabQuotaBucketEntry>();
            mehediCfg.ShihabThemeName = "Obsidian Dark";
            mehediCfg.ShihabUiScalePercent = 100;
            mehediCfg.ShihabLanguageCode = "en";
            mehediCfg.ShihabCaptureOpenedFiles = false;
            mehediCfg.ShihabCaptureMinSizeKb = 0;
            mehediCfg.ShihabCrashMonitorEnabled = true;
            mehediCfg.MehediDialUpConnectionName = "Default System Connection";
            mehediCfg.ShihabRedialIfDisconnected = false;
            mehediCfg.MehediHangUpWhenDone = false;
            mehediCfg.MehediPlaySoundComplete = false;
            mehediCfg.ShihabSoundCompleteWav = "";
            mehediCfg.MehediPlaySoundError = false;
            mehediCfg.ShihabSoundErrorWav = "";
            mehediCfg.ShihabSetServerFileTimestamp = true;
            mehediCfg.ShihabSetServerCreationDate = false;
            mehediCfg.MehediLocalBridgePort = 45779;
            mehediCfg.ShihabLocalBridgeToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            mehediCfg.ShihabAutoCheckUpdates = true;
            mehediCfg.ShihabUpdateIncludePrereleases = false;
            mehediCfg.ShihabUpdateRepoSlug = "sshihabb007/MHS-DOWNLOAD-MANAGER";
            mehediCfg.ShihabUpdateApiOverrideUrl = "";
            mehediCfg.ShihabSkippedUpdateVersion = "";
            mehediCfg.ShihabLastUpdateCheckUtc = "";
            mehediCfg.ShihabCloseToTray = true;
            mehediCfg.ShihabMinimizeToTray = false;
            mehediCfg.ShihabDropTargetX = -1;
            mehediCfg.ShihabDropTargetY = -1;
            mehediCfg.ShihabPlainDnsServer = "1.1.1.1";
            mehediCfg.ShihabNextDnsConfigId = "";
            mehediCfg.ShihabRemotePairingExpiresUtc = DateTime.UtcNow.AddMinutes(10).ToString("o");

            mehediCfg.ShihabCategories = new List<ShihabCategoryRule>
            {
                new ShihabCategoryRule { ShihabName = "General", MehediExtensionsSpaceSeparated = "", ShihabDefaultFolder = shihabDownloadsRoot, MehediRememberLastSavePath = true },
                new ShihabCategoryRule { ShihabName = "Compressed", MehediExtensionsSpaceSeparated = "zip rar 7z gz bz2 xz tar tgz cab arj lzh ace z r0* r1*", ShihabDefaultFolder = Path.Combine(shihabDownloadsRoot, "Compressed"), MehediRememberLastSavePath = true },
                new ShihabCategoryRule { ShihabName = "Documents", MehediExtensionsSpaceSeparated = "pdf doc docx xls xlsx ppt pptx txt rtf csv epub mobi", ShihabDefaultFolder = Path.Combine(shihabDownloadsRoot, "Documents"), MehediRememberLastSavePath = true },
                new ShihabCategoryRule { ShihabName = "Music", MehediExtensionsSpaceSeparated = "mp3 wav flac aac ogg m4a wma aif opus", ShihabDefaultFolder = Path.Combine(shihabDownloadsRoot, "Music"), MehediRememberLastSavePath = true },
                new ShihabCategoryRule { ShihabName = "Programs", MehediExtensionsSpaceSeparated = "exe msi msu apk dmg iso img bin deb rpm bat ps1", ShihabDefaultFolder = Path.Combine(shihabDownloadsRoot, "Programs"), MehediRememberLastSavePath = true },
                new ShihabCategoryRule { ShihabName = "Video", MehediExtensionsSpaceSeparated = "mp4 mkv webm avi mov flv wmv mpg mpeg m4v 3gp ts m3u8 ogv qt rm rmvb asf", ShihabDefaultFolder = Path.Combine(shihabDownloadsRoot, "Video"), MehediRememberLastSavePath = true },
                new ShihabCategoryRule { ShihabName = "Torrents", MehediExtensionsSpaceSeparated = "torrent magnet", ShihabDefaultFolder = Path.Combine(shihabDownloadsRoot, "Torrents"), MehediRememberLastSavePath = true }
            };

            MehediQueueConfig shihabMainQueue = new MehediQueueConfig();
            shihabMainQueue.ShihabQueueName = "Main Download Queue";
            shihabMainQueue.MehediMaxConcurrentFiles = 4;

            MehediQueueConfig mehediSyncQueue = new MehediQueueConfig();
            mehediSyncQueue.ShihabQueueName = "Synchronization Queue";
            mehediSyncQueue.MehediMaxConcurrentFiles = 2;
            mehediSyncQueue.MehediEnablePeriodicSync = true;
            mehediSyncQueue.ShihabPeriodicSyncMinutes = 120;

            mehediCfg.MehediQueues = new List<MehediQueueConfig> { shihabMainQueue, mehediSyncQueue };
            mehediCfg.ShihabSiteLogins = new List<ShihabSiteLoginCredential>();
            mehediCfg.ShihabHostConnectionLimits = new List<ShihabHostConnectionLimit>();
            mehediCfg.ShihabDebridAccounts = new List<ShihabDebridAccount>
            {
                new ShihabDebridAccount { ShihabProvider = "Real-Debrid", MehediEnabled = false },
                new ShihabDebridAccount { ShihabProvider = "AllDebrid", MehediEnabled = false },
                new ShihabDebridAccount { ShihabProvider = "Premiumize", MehediEnabled = false }
            };
            mehediCfg.ShihabRssFeeds = new List<ShihabRssFeedConfig>();
            return mehediCfg;
        }

        public static string GenerateRandomEightDigitCode()
        {
            byte[] b = new byte[4];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(b);
            }
            uint val = BitConverter.ToUInt32(b, 0) % 90000000u + 10000000u;
            return val.ToString();
        }

        public int GetMaxAllowedConnectionsCap()
        {
            return ShihabAllowMoreThan32Connections ? 128 : 32;
        }

        public int GetEffectiveMaxConnectionsForHost(string host, int requestedConnections)
        {
            int cap = GetMaxAllowedConnectionsCap();
            int effective = Math.Max(1, Math.Min(cap, requestedConnections));
            if (!string.IsNullOrEmpty(host) && ShihabHostConnectionLimits != null)
            {
                string cleanHost = host.Trim().ToLowerInvariant();
                for (int i = 0; i < ShihabHostConnectionLimits.Count; i++)
                {
                    ShihabHostConnectionLimit rule = ShihabHostConnectionLimits[i];
                    if (rule != null && !string.IsNullOrEmpty(rule.ShihabHostPattern) && rule.MehediMaxConnections > 0)
                    {
                        string pat = rule.ShihabHostPattern.Trim();
                        if (ShihabFormatHelpers.WildcardMatchesMehediPattern(cleanHost, pat) ||
                            string.Equals(cleanHost, pat, StringComparison.OrdinalIgnoreCase))
                        {
                            effective = Math.Min(effective, rule.MehediMaxConnections);
                        }
                    }
                }
            }
            return Math.Max(1, effective);
        }

        public void RecordQuotaBytes(long deltaBytes)
        {
            if (deltaBytes <= 0) return;
            if (ShihabQuotaHistory == null)
            {
                ShihabQuotaHistory = new List<ShihabQuotaBucketEntry>();
            }
            long currentHour = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalHours;
            lock (ShihabQuotaHistory)
            {
                ShihabQuotaBucketEntry found = null;
                for (int i = 0; i < ShihabQuotaHistory.Count; i++)
                {
                    if (ShihabQuotaHistory[i].ShihabHourEpoch == currentHour)
                    {
                        found = ShihabQuotaHistory[i];
                        break;
                    }
                }
                if (found == null)
                {
                    found = new ShihabQuotaBucketEntry { ShihabHourEpoch = currentHour, MehediBytesDownloaded = 0 };
                    ShihabQuotaHistory.Add(found);
                }
                found.MehediBytesDownloaded += deltaBytes;

                int keepHours = Math.Max(1, ShihabQuotaWindowHours) + 24;
                ShihabQuotaHistory.RemoveAll(delegate(ShihabQuotaBucketEntry b)
                {
                    return b == null || (currentHour - b.ShihabHourEpoch) > keepHours;
                });
            }
        }

        public long GetCurrentQuotaWindowBytes()
        {
            if (ShihabQuotaHistory == null) return 0L;
            long currentHour = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalHours;
            int windowHours = Math.Max(1, ShihabQuotaWindowHours);
            long sum = 0L;
            lock (ShihabQuotaHistory)
            {
                for (int i = 0; i < ShihabQuotaHistory.Count; i++)
                {
                    ShihabQuotaBucketEntry entry = ShihabQuotaHistory[i];
                    if (entry != null && (currentHour - entry.ShihabHourEpoch) < windowHours)
                    {
                        sum += entry.MehediBytesDownloaded;
                    }
                }
            }
            return sum;
        }

        internal List<ShihabQuotaBucketEntry> CloneQuotaHistorySnapshot()
        {
            List<ShihabQuotaBucketEntry> copy = new List<ShihabQuotaBucketEntry>();
            if (ShihabQuotaHistory == null) return copy;
            lock (ShihabQuotaHistory)
            {
                for (int i = 0; i < ShihabQuotaHistory.Count; i++)
                {
                    ShihabQuotaBucketEntry src = ShihabQuotaHistory[i];
                    if (src != null)
                    {
                        copy.Add(new ShihabQuotaBucketEntry
                        {
                            ShihabHourEpoch = src.ShihabHourEpoch,
                            MehediBytesDownloaded = src.MehediBytesDownloaded
                        });
                    }
                }
            }
            return copy;
        }

        public bool IsQuotaExceeded()
        {
            if (!ShihabQuotaEnabled || ShihabQuotaLimitMegabytes <= 0) return false;
            long limitBytes = (long)ShihabQuotaLimitMegabytes * 1024L * 1024L;
            return GetCurrentQuotaWindowBytes() >= limitBytes;
        }

        public bool MatchesShihabAutoCaptureExtension(string mehediExtWithoutDot)
        {
            if (string.IsNullOrWhiteSpace(mehediExtWithoutDot) || string.IsNullOrWhiteSpace(MehediAutoCaptureExtensions))
            {
                return false;
            }
            string shihabClean = mehediExtWithoutDot.Trim().TrimStart('.');
            if (string.IsNullOrEmpty(shihabClean)) return false;

            string[] mehediTokens = MehediAutoCaptureExtensions.Split(new char[] { ' ', ',', ';', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int shihabI = 0; shihabI < mehediTokens.Length; shihabI++)
            {
                string mehediPat = mehediTokens[shihabI].Trim().TrimStart('.');
                if (string.IsNullOrEmpty(mehediPat)) continue;
                if (mehediPat.IndexOf('*') >= 0 || mehediPat.IndexOf('?') >= 0)
                {
                    if (ShihabFormatHelpers.WildcardMatchesMehediPattern(shihabClean, mehediPat))
                    {
                        return true;
                    }
                }
                else if (string.Equals(mehediPat, shihabClean, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static void SyncShihabWindowsStartupRegistry(bool mehediEnable)
        {
            try
            {
                const string shihabRunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
                const string mehediValueName = "MHSDownloadManager";
                const string legacyValueName = "MHS_Download_Manager";
                using (RegistryKey shihabKey = Registry.CurrentUser.CreateSubKey(shihabRunSubKey))
                {
                    if (shihabKey == null) return;
                    try { shihabKey.DeleteValue(legacyValueName, false); } catch { }
                    if (mehediEnable)
                    {
                        string mehediExe = Process.GetCurrentProcess().MainModule.FileName;
                        string shihabDesired = "\"" + mehediExe + "\" --shihab-minimized";
                        string mehediExisting = shihabKey.GetValue(mehediValueName) as string;
                        if (!string.Equals(mehediExisting, shihabDesired, StringComparison.Ordinal))
                        {
                            shihabKey.SetValue(mehediValueName, shihabDesired, RegistryValueKind.String);
                        }
                    }
                    else
                    {
                        object shihabCurVal = shihabKey.GetValue(mehediValueName, null);
                        if (shihabCurVal != null)
                        {
                            shihabKey.DeleteValue(mehediValueName, false);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        public ShihabCategoryRule ResolveMehediCategoryForFile(string shihabFileName)
        {
            string mehediExt = "";
            if (!string.IsNullOrEmpty(shihabFileName))
            {
                mehediExt = Path.GetExtension(shihabFileName).TrimStart('.');
            }
            if (ShihabCategories != null)
            {
                for (int shihabIdx = 0; shihabIdx < ShihabCategories.Count; shihabIdx++)
                {
                    ShihabCategoryRule mehediCat = ShihabCategories[shihabIdx];
                    if (mehediCat.MatchesMehediExtension(mehediExt))
                    {
                        return mehediCat;
                    }
                }
                for (int shihabIdx = 0; shihabIdx < ShihabCategories.Count; shihabIdx++)
                {
                    if (string.Equals(ShihabCategories[shihabIdx].ShihabName, "General", StringComparison.OrdinalIgnoreCase))
                    {
                        return ShihabCategories[shihabIdx];
                    }
                }
            }
            return new ShihabCategoryRule
            {
                ShihabName = "General",
                MehediExtensionsSpaceSeparated = "",
                ShihabDefaultFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            };
        }

        public ShihabSiteLoginCredential FindShihabCredentialForUrl(string mehediUrl)
        {
            if (string.IsNullOrEmpty(mehediUrl) || ShihabSiteLogins == null)
            {
                return null;
            }
            Uri targetUri;
            if (!Uri.TryCreate(mehediUrl, UriKind.Absolute, out targetUri) || string.IsNullOrEmpty(targetUri.Host))
            {
                return null;
            }

            string targetHost = targetUri.Host.ToLowerInvariant();
            string targetScheme = (targetUri.Scheme ?? "").ToLowerInvariant();
            string targetPath = string.IsNullOrEmpty(targetUri.AbsolutePath) ? "/" : targetUri.AbsolutePath.ToLowerInvariant();

            for (int i = 0; i < ShihabSiteLogins.Count; i++)
            {
                ShihabSiteLoginCredential cred = ShihabSiteLogins[i];
                if (cred == null || string.IsNullOrWhiteSpace(cred.ShihabServerPattern)) continue;

                string pat = cred.ShihabServerPattern.Trim().ToLowerInvariant();
                string requiredScheme = "";
                if (pat.StartsWith("https://", StringComparison.Ordinal))
                {
                    requiredScheme = "https";
                    pat = pat.Substring(8);
                }
                else if (pat.StartsWith("http://", StringComparison.Ordinal))
                {
                    requiredScheme = "http";
                    pat = pat.Substring(7);
                }
                else if (pat.StartsWith("ftps://", StringComparison.Ordinal))
                {
                    requiredScheme = "ftps";
                    pat = pat.Substring(7);
                }
                else if (pat.StartsWith("ftp://", StringComparison.Ordinal))
                {
                    requiredScheme = "ftp";
                    pat = pat.Substring(6);
                }

                if (!string.IsNullOrEmpty(requiredScheme) && !string.Equals(requiredScheme, targetScheme, StringComparison.Ordinal))
                {
                    continue;
                }

                int slashIdx = pat.IndexOf('/');
                string patHost = (slashIdx >= 0 ? pat.Substring(0, slashIdx) : pat).TrimEnd(':');
                string patPath = slashIdx >= 0 ? pat.Substring(slashIdx) : "";
                int portColon = patHost.LastIndexOf(':');
                if (portColon > 0 && patHost.IndexOf(']') < 0)
                {
                    int explicitPort;
                    if (int.TryParse(patHost.Substring(portColon + 1), out explicitPort))
                    {
                        if (targetUri.Port != explicitPort) continue;
                        patHost = patHost.Substring(0, portColon);
                    }
                }
                if (string.IsNullOrEmpty(patHost)) continue;

                bool hostMatches;
                if (patHost.StartsWith("*.", StringComparison.Ordinal))
                {
                    string suffix = patHost.Substring(1); // ".domain.com"
                    hostMatches = targetHost.EndsWith(suffix, StringComparison.Ordinal) ||
                                  string.Equals(targetHost, suffix.Substring(1), StringComparison.Ordinal);
                }
                else
                {
                    hostMatches = string.Equals(targetHost, patHost, StringComparison.Ordinal);
                }
                if (!hostMatches) continue;

                if (!string.IsNullOrEmpty(patPath) && patPath != "/")
                {
                    string normPatPath = patPath.TrimEnd('/');
                    if (!string.Equals(targetPath, normPatPath, StringComparison.Ordinal) &&
                        !targetPath.StartsWith(normPatPath + "/", StringComparison.Ordinal))
                    {
                        continue;
                    }
                }

                return new ShihabSiteLoginCredential
                {
                    ShihabServerPattern = cred.ShihabServerPattern,
                    MehediUserName = cred.MehediUserName,
                    ShihabPassword = ShihabSecretsVault.UnprotectSecret(cred.ShihabPassword)
                };
            }
            return null;
        }
    }
}

