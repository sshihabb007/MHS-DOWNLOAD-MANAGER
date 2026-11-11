using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ShihabMehediIDM
{
    public enum ShihabDownloadStatus
    {
        ShihabQueued = 0,
        ShihabConnecting = 1,
        ShihabDownloading = 2,
        ShihabPaused = 3,
        ShihabRebuilding = 4,
        ShihabCompleted = 5,
        ShihabError = 6,
        ShihabWaitingRefresh = 7
    }

    [DataContract]
    public class MehediSegmentChunk
    {
        [DataMember] public int ShihabSegmentIndex { get; set; }
        [DataMember] public long MehediStartByte { get; set; }
        [DataMember] public long ShihabEndByte { get; set; }
        [DataMember] public long MehediDownloadedBytes { get; set; }
        [DataMember] public string ShihabSegmentState { get; set; }
        [DataMember] public string MehediPartFilePath { get; set; }

        [IgnoreDataMember]
        public bool ShihabIsActiveWorker { get; set; }

        public long GetShihabTotalSpan()
        {
            if (ShihabEndByte < 0)
            {
                return -1;
            }
            long mehediSpan = (ShihabEndByte - MehediStartByte) + 1L;
            return mehediSpan < 0 ? 0 : mehediSpan;
        }

        public long GetMehediRemainingBytes()
        {
            if (ShihabEndByte < 0)
            {
                return -1;
            }
            long shihabTotal = GetShihabTotalSpan();
            long mehediRem = shihabTotal - MehediDownloadedBytes;
            return mehediRem < 0 ? 0 : mehediRem;
        }
    }

    [DataContract]
    public class ShihabDownloadItem
    {
        [DataMember] public string ShihabId { get; set; }
        [DataMember] public string MehediUrl { get; set; }
        [DataMember] public string ShihabFileName { get; set; }
        [DataMember] public string MehediSaveDirectory { get; set; }
        [DataMember] public string ShihabCategoryName { get; set; }
        [DataMember] public string MehediQueueName { get; set; }
        [DataMember] public string ShihabDescription { get; set; }
        [DataMember] public string MehediReferer { get; set; }
        [DataMember] public string ShihabCookies { get; set; }
        [DataMember] public string MehediUserAgent { get; set; }
        [DataMember] public string ShihabAuthUser { get; set; }
        [DataMember] public string MehediAuthPassword { get; set; }
        [DataMember] public long ShihabTotalBytes { get; set; }
        [DataMember] public long MehediDownloadedBytes { get; set; }
        [DataMember] public bool ShihabResumeSupported { get; set; }
        [DataMember] public int MehediStatusCode { get; set; }
        [DataMember] public string ShihabStatusText { get; set; }
        [DataMember] public string MehediLastTryDate { get; set; }
        [DataMember] public string ShihabDateAdded { get; set; }
        [DataMember] public int MehediMaxConnections { get; set; }
        [DataMember] public bool ShihabUseItemSpeedLimit { get; set; }
        [DataMember] public int MehediItemSpeedLimitKb { get; set; }
        [DataMember] public bool ShihabOpenWhenComplete { get; set; }
        [DataMember] public bool MehediExitAppOnComplete { get; set; }
        [DataMember] public bool ShihabShowCompleteDialog { get; set; }
        [DataMember] public string MehediSha256Hex { get; set; }
        [DataMember] public string ShihabServerLastModified { get; set; }
        [DataMember] public bool MehediIsHlsStream { get; set; }
        [DataMember] public int ShihabRetryCount { get; set; }
        [DataMember] public string MehediOriginHeader { get; set; }
        [DataMember] public bool MehediOmitCookies { get; set; }
        [DataMember] public string MehediAudioCompanionUrl { get; set; }
        [DataMember] public long ShihabAudioCompanionBytes { get; set; }
        [DataMember] public int ShihabSourceTabId { get; set; }
        [DataMember] public string ShihabQualityLabel { get; set; }
        [DataMember] public string ShihabETag { get; set; }
        [DataMember] public string ShihabETagHost { get; set; }
        [DataMember] public string ShihabExpectedChecksum { get; set; }
        [DataMember] public string ShihabComputedChecksum { get; set; }
        [DataMember] public string ShihabChecksumStatus { get; set; }
        [DataMember] public string ShihabServerIp { get; set; }
        [DataMember] public string ShihabServerCountryCode { get; set; }
        [DataMember] public bool ShihabAutoExtract { get; set; }
        [DataMember] public string ShihabArchivePassword { get; set; }
        [DataMember] public string ShihabPostBody { get; set; }
        [DataMember] public bool ShihabIsTorrent { get; set; }
        [DataMember] public string ShihabTorrentMagnetOrPath { get; set; }
        [DataMember] public bool ShihabTorrentSequential { get; set; }
        [DataMember] public bool ShihabTorrentKeepSeeding { get; set; }
        [DataMember] public List<int> ShihabSelectedTorrentFiles { get; set; }
        [DataMember] public string ShihabSubtitleLanguages { get; set; }
        [DataMember] public bool ShihabEmbedSubtitles { get; set; }
        [DataMember] public string ShihabAudioExtractFormat { get; set; }
        [DataMember] public bool ShihabIsYtDlpJob { get; set; }
        [DataMember] public string ShihabYtDlpFormatSelector { get; set; }
        [DataMember] public bool ShihabIsGalleryJob { get; set; }
        [DataMember] public int ShihabGalleryMaxItems { get; set; }
        [DataMember] public bool ShihabBypassDebrid { get; set; }
        [DataMember] public bool ShihabUseDebridForTorrent { get; set; }
        [DataMember] public bool ShihabIsPrivateIncognito { get; set; }
        [DataMember] public string ShihabPartFilePath { get; set; }
        [DataMember] public List<MehediSegmentChunk> MehediSegments { get; set; }

        [IgnoreDataMember] public double ShihabCurrentSpeedBps { get; set; }
        [IgnoreDataMember] public long MehediEtaSeconds { get; set; }
        [IgnoreDataMember] public string ShihabTransientUnlockedUrl { get; set; }
        [IgnoreDataMember] public int ShihabTransientMaxChunks { get; set; }

        [IgnoreDataMember]
        public string ShihabSubtitleLangs
        {
            get { return ShihabSubtitleLanguages; }
            set { ShihabSubtitleLanguages = value; }
        }

        [IgnoreDataMember]
        public string ShihabAudioOnlyExtractFormat
        {
            get { return ShihabAudioExtractFormat; }
            set { ShihabAudioExtractFormat = value; }
        }

        [IgnoreDataMember]
        public bool ShihabAudioOnlyExtract
        {
            get { return !string.IsNullOrWhiteSpace(ShihabAudioExtractFormat); }
            set { ShihabAudioExtractFormat = value ? "mp3" : ""; }
        }

        [IgnoreDataMember]
        public bool ShihabIsTorrentOrMagnet
        {
            get { return ShihabIsTorrent; }
            set { ShihabIsTorrent = value; }
        }

        public ShihabDownloadItem()
        {
            ShihabId = Guid.NewGuid().ToString("N");
            MehediUrl = "";
            ShihabFileName = "download.bin";
            MehediSaveDirectory = "";
            ShihabCategoryName = "General";
            MehediQueueName = "";
            ShihabDescription = "";
            MehediReferer = "";
            ShihabCookies = "";
            MehediUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
            ShihabAuthUser = "";
            MehediAuthPassword = "";
            ShihabTotalBytes = -1;
            MehediDownloadedBytes = 0;
            ShihabResumeSupported = false;
            MehediStatusCode = (int)ShihabDownloadStatus.ShihabPaused;
            ShihabStatusText = "Paused";
            MehediLastTryDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            ShihabDateAdded = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            MehediMaxConnections = 8;
            ShihabUseItemSpeedLimit = false;
            MehediItemSpeedLimitKb = 512;
            ShihabOpenWhenComplete = false;
            MehediExitAppOnComplete = false;
            ShihabShowCompleteDialog = true;
            MehediSha256Hex = "";
            ShihabServerLastModified = "";
            MehediIsHlsStream = false;
            ShihabRetryCount = 0;
            MehediOriginHeader = "";
            MehediOmitCookies = false;
            MehediAudioCompanionUrl = "";
            ShihabAudioCompanionBytes = -1;
            ShihabSourceTabId = -1;
            ShihabQualityLabel = "";
            ShihabETag = "";
            ShihabETagHost = "";
            ShihabExpectedChecksum = "";
            ShihabComputedChecksum = "";
            ShihabChecksumStatus = "";
            ShihabServerIp = "";
            ShihabServerCountryCode = "";
            ShihabAutoExtract = false;
            ShihabArchivePassword = "";
            ShihabPostBody = "";
            ShihabIsTorrent = false;
            ShihabTorrentMagnetOrPath = "";
            ShihabTorrentSequential = true;
            ShihabTorrentKeepSeeding = false;
            ShihabSelectedTorrentFiles = new List<int>();
            ShihabSubtitleLanguages = "";
            ShihabEmbedSubtitles = false;
            ShihabAudioExtractFormat = "";
            ShihabIsYtDlpJob = false;
            ShihabYtDlpFormatSelector = "";
            ShihabIsGalleryJob = false;
            ShihabGalleryMaxItems = 0;
            ShihabBypassDebrid = false;
            ShihabUseDebridForTorrent = false;
            ShihabIsPrivateIncognito = false;
            ShihabPartFilePath = "";
            MehediSegments = new List<MehediSegmentChunk>();
            ShihabCurrentSpeedBps = 0;
            MehediEtaSeconds = -1;
            ShihabTransientUnlockedUrl = "";
            ShihabTransientMaxChunks = 0;
        }

        public ShihabDownloadStatus GetShihabStatusEnum()
        {
            return (ShihabDownloadStatus)MehediStatusCode;
        }

        public void SetMehediStatusEnum(ShihabDownloadStatus shihabStatus, string mehediLabel)
        {
            MehediStatusCode = (int)shihabStatus;
            if (!string.IsNullOrEmpty(mehediLabel))
            {
                ShihabStatusText = mehediLabel;
            }
        }

        public string GetShihabFullTargetPath()
        {
            string mehediDir = string.IsNullOrEmpty(MehediSaveDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
                : MehediSaveDirectory;
            return Path.Combine(mehediDir, ShihabFileName);
        }

        public double GetMehediPercent()
        {
            if (GetShihabStatusEnum() == ShihabDownloadStatus.ShihabCompleted)
            {
                return 100.0;
            }
            if (ShihabTotalBytes <= 0)
            {
                return 0.0;
            }
            double shihabPct = (MehediDownloadedBytes * 100.0) / (double)ShihabTotalBytes;
            if (shihabPct < 0.0) return 0.0;
            if (shihabPct > 100.0) return 100.0;
            return shihabPct;
        }
    }

    [DataContract]
    public class ShihabCategoryRule
    {
        [DataMember] public string ShihabName { get; set; }
        [DataMember] public string MehediExtensionsSpaceSeparated { get; set; }
        [DataMember] public string ShihabDefaultFolder { get; set; }
        [DataMember] public bool MehediRememberLastSavePath { get; set; }

        public ShihabCategoryRule()
        {
            MehediRememberLastSavePath = true;
        }

        [OnDeserializing]
        private void OnShihabDeserializing(StreamingContext mehediCtx)
        {
            MehediRememberLastSavePath = true;
        }

        public bool MatchesMehediExtension(string shihabExtWithoutDot)
        {
            if (string.IsNullOrEmpty(shihabExtWithoutDot) || string.IsNullOrEmpty(MehediExtensionsSpaceSeparated))
            {
                return false;
            }
            string mehediClean = shihabExtWithoutDot.Trim().TrimStart('.').ToLowerInvariant();
            string[] shihabParts = MehediExtensionsSpaceSeparated.Split(new char[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int mehediIdx = 0; mehediIdx < shihabParts.Length; mehediIdx++)
            {
                string shihabRuleTok = shihabParts[mehediIdx].Trim().TrimStart('.');
                if (string.IsNullOrEmpty(shihabRuleTok)) continue;
                if (shihabRuleTok.IndexOf('*') >= 0 || shihabRuleTok.IndexOf('?') >= 0)
                {
                    if (ShihabFormatHelpers.WildcardMatchesMehediPattern(mehediClean, shihabRuleTok))
                    {
                        return true;
                    }
                }
                else if (string.Equals(shihabRuleTok, mehediClean, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }

    [DataContract]
    public class MehediQueueConfig
    {
        [DataMember] public string ShihabQueueName { get; set; }
        [DataMember] public int MehediMaxConcurrentFiles { get; set; }
        [DataMember] public int ShihabRetriesPerFile { get; set; }
        [DataMember] public bool MehediEnableScheduledStart { get; set; }
        [DataMember] public string ShihabStartTimeHhMm { get; set; }
        [DataMember] public bool MehediEnableScheduledStop { get; set; }
        [DataMember] public string ShihabStopTimeHhMm { get; set; }
        [DataMember] public string MehediActiveDaysCsv { get; set; }
        [DataMember] public bool ShihabStartOnStartup { get; set; }
        [DataMember] public bool MehediExitAppWhenDone { get; set; }
        [DataMember] public int ShihabQuotaMegabytes { get; set; }
        [DataMember] public string ShihabOnceAtDateYyyyMmDd { get; set; }
        [DataMember] public string ShihabRunCommandWhenDone { get; set; }
        [DataMember] public string ShihabSystemPowerActionWhenDone { get; set; }

        [IgnoreDataMember]
        public bool MehediExitAppWhenQueueFinishes
        {
            get { return MehediExitAppWhenDone; }
            set { MehediExitAppWhenDone = value; }
        }

        [IgnoreDataMember] public bool MehediIsRunningNow { get; set; }
        [IgnoreDataMember] public string ShihabLastAutoStartMinute { get; set; }
        [IgnoreDataMember] public string MehediLastAutoStopMinute { get; set; }

        public MehediQueueConfig()
        {
            ShihabQueueName = "Main Download Queue";
            MehediMaxConcurrentFiles = 4;
            ShihabRetriesPerFile = 3;
            MehediEnableScheduledStart = false;
            ShihabStartTimeHhMm = "01:00";
            MehediEnableScheduledStop = false;
            ShihabStopTimeHhMm = "07:00";
            MehediActiveDaysCsv = "Sun,Mon,Tue,Wed,Thu,Fri,Sat";
            ShihabStartOnStartup = false;
            MehediExitAppWhenDone = false;
            ShihabQuotaMegabytes = 0;
            ShihabOnceAtDateYyyyMmDd = "";
            ShihabRunCommandWhenDone = "";
            ShihabSystemPowerActionWhenDone = "None";
            MehediIsRunningNow = false;
            ShihabLastAutoStartMinute = "";
            MehediLastAutoStopMinute = "";
        }
    }

    [DataContract]
    public class ShihabSiteLoginCredential
    {
        [DataMember] public string ShihabServerPattern { get; set; }
        [DataMember] public string MehediUserName { get; set; }
        [DataMember] public string ShihabPassword { get; set; }
    }

    [DataContract]
    public class ShihabHostConnectionLimit
    {
        [DataMember] public string ShihabHostPattern { get; set; }
        [DataMember] public int MehediMaxConnections { get; set; }

        public ShihabHostConnectionLimit()
        {
            ShihabHostPattern = "";
            MehediMaxConnections = 4;
        }
    }

    [DataContract]
    public class ShihabDebridAccount
    {
        [DataMember] public string ShihabProvider { get; set; }
        [DataMember] public bool MehediEnabled { get; set; }
        [DataMember] public string ShihabUsername { get; set; }
        [DataMember] public string MehediEncryptedApiToken { get; set; }
        [DataMember] public string ShihabAccountStatus { get; set; }
        [DataMember] public string MehediExpiresAt { get; set; }

        [IgnoreDataMember]
        public string ShihabProviderName
        {
            get { return ShihabProvider; }
            set { ShihabProvider = value; }
        }

        [IgnoreDataMember]
        public bool ShihabEnabled
        {
            get { return MehediEnabled; }
            set { MehediEnabled = value; }
        }

        [IgnoreDataMember]
        public string ShihabApiToken
        {
            get { return MehediEncryptedApiToken; }
            set { MehediEncryptedApiToken = value; }
        }

        public ShihabDebridAccount()
        {
            ShihabProvider = "Real-Debrid";
            MehediEnabled = false;
            ShihabUsername = "";
            MehediEncryptedApiToken = "";
            ShihabAccountStatus = "Not signed in";
            MehediExpiresAt = "";
        }
    }

    [DataContract]
    public class ShihabRssFeedConfig
    {
        [DataMember] public string ShihabFeedId { get; set; }
        [DataMember] public string MehediTitle { get; set; }
        [DataMember] public string ShihabFeedUrl { get; set; }
        [DataMember] public string MehediFilterPattern { get; set; }
        [DataMember] public string ShihabSaveDirectory { get; set; }
        [DataMember] public string MehediCategoryName { get; set; }
        [DataMember] public bool ShihabAutoDownload { get; set; }
        [DataMember] public int MehediCheckIntervalMinutes { get; set; }
        [DataMember] public string ShihabLastChecked { get; set; }
        [DataMember] public List<string> MehediSeenItemGuids { get; set; }
        [DataMember] public string ShihabExcludePattern { get; set; }
        [DataMember] public bool ShihabInitialBacklogSeeded { get; set; }
        [DataMember] public List<string> ShihabDownloadedEpisodeKeys { get; set; }

        [IgnoreDataMember]
        public string ShihabFeedTitle
        {
            get { return MehediTitle; }
            set { MehediTitle = value; }
        }

        [IgnoreDataMember]
        public string ShihabIncludeRegex
        {
            get { return MehediFilterPattern; }
            set { MehediFilterPattern = value; }
        }

        public ShihabRssFeedConfig()
        {
            ShihabFeedId = Guid.NewGuid().ToString("N");
            MehediTitle = "RSS Feed";
            ShihabFeedUrl = "";
            MehediFilterPattern = "*";
            ShihabExcludePattern = "";
            ShihabSaveDirectory = "";
            MehediCategoryName = "General";
            ShihabAutoDownload = false;
            MehediCheckIntervalMinutes = 30;
            ShihabLastChecked = "";
            ShihabInitialBacklogSeeded = false;
            MehediSeenItemGuids = new List<string>();
            ShihabDownloadedEpisodeKeys = new List<string>();
        }
    }

    [DataContract]
    public class ShihabPairedRemoteDevice
    {
        [DataMember] public string ShihabDeviceId { get; set; }
        [DataMember] public string MehediDeviceName { get; set; }
        [DataMember] public string ShihabTokenHash { get; set; }
        [DataMember] public string MehediPairedDate { get; set; }
        [DataMember] public string ShihabLastSeenIp { get; set; }

        public ShihabPairedRemoteDevice()
        {
            ShihabDeviceId = Guid.NewGuid().ToString("N");
            MehediDeviceName = "Mobile Web Remote";
            ShihabTokenHash = "";
            MehediPairedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            ShihabLastSeenIp = "";
        }
    }

    [DataContract]
    public class ShihabQuotaBucketEntry
    {
        [DataMember] public long ShihabHourEpoch { get; set; }
        [DataMember] public long MehediBytesDownloaded { get; set; }
    }

    public static class ShihabSecretsVault
    {
        private const string DpapiNativePrefix = "dpapi2:";
        private const string LegacyDpapiPrefix = "dpapi:";
        private static readonly byte[] VaultEntropy = Encoding.UTF8.GetBytes("MHS.DownloadManager.ShihabSecretsVault.v2");

        public static string ProtectSecret(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return "";
            }
            if (plainText.StartsWith(DpapiNativePrefix, StringComparison.Ordinal))
            {
                return plainText;
            }
            if (plainText.StartsWith(LegacyDpapiPrefix, StringComparison.Ordinal))
            {
                string migrated = UnprotectSecret(plainText);
                if (!string.IsNullOrEmpty(migrated))
                {
                    plainText = migrated;
                }
                else
                {
                    return plainText;
                }
            }
            try
            {
                byte[] raw = Encoding.UTF8.GetBytes(plainText);
                byte[] cipher = ProtectedData.Protect(raw, VaultEntropy, DataProtectionScope.CurrentUser);
                return DpapiNativePrefix + Convert.ToBase64String(cipher);
            }
            catch
            {
                try
                {
                    byte[] raw = Encoding.UTF8.GetBytes(plainText);
                    byte[] cipher = TransformLegacySecret(raw, true);
                    return LegacyDpapiPrefix + Convert.ToBase64String(cipher);
                }
                catch
                {
                    return "";
                }
            }
        }

        public static string UnprotectSecret(string storedValue)
        {
            if (string.IsNullOrEmpty(storedValue))
            {
                return "";
            }
            if (storedValue.StartsWith(DpapiNativePrefix, StringComparison.Ordinal))
            {
                try
                {
                    string b64 = storedValue.Substring(DpapiNativePrefix.Length);
                    byte[] cipher = Convert.FromBase64String(b64);
                    byte[] plain = ProtectedData.Unprotect(cipher, VaultEntropy, DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(plain);
                }
                catch
                {
                    return "";
                }
            }
            if (!storedValue.StartsWith(LegacyDpapiPrefix, StringComparison.Ordinal))
            {
                return storedValue;
            }
            try
            {
                string b64 = storedValue.Substring(LegacyDpapiPrefix.Length);
                byte[] cipher = Convert.FromBase64String(b64);
                byte[] plain = TransformLegacySecret(cipher, false);
                return Encoding.UTF8.GetString(plain);
            }
            catch
            {
                return "";
            }
        }

        private static byte[] TransformLegacySecret(byte[] data, bool encrypt)
        {
            string seed = Environment.UserName + "|" + Environment.MachineName + "|" + Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] key = sha.ComputeHash(Encoding.UTF8.GetBytes(seed + "|MHS_VAULT_KEY"));
                byte[] ivFull = sha.ComputeHash(VaultEntropy);
                byte[] iv = new byte[16];
                Buffer.BlockCopy(ivFull, 0, iv, 0, 16);
                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    using (ICryptoTransform xform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor())
                    {
                        return xform.TransformFinalBlock(data, 0, data.Length);
                    }
                }
            }
        }

        public static string SealSecret(string plainText)
        {
            return ProtectSecret(plainText);
        }

        public static string UnsealSecret(string storedValue)
        {
            return UnprotectSecret(storedValue);
        }

        public static void SealConfigurationSecrets(MehediAppConfiguration cfg)
        {
            if (cfg == null) return;
            if (!string.IsNullOrEmpty(cfg.MehediProxyPassword))
            {
                cfg.MehediProxyPassword = ProtectSecret(cfg.MehediProxyPassword);
            }
            if (cfg.ShihabSiteLogins != null)
            {
                for (int i = 0; i < cfg.ShihabSiteLogins.Count; i++)
                {
                    if (cfg.ShihabSiteLogins[i] != null && !string.IsNullOrEmpty(cfg.ShihabSiteLogins[i].ShihabPassword))
                    {
                        cfg.ShihabSiteLogins[i].ShihabPassword = ProtectSecret(cfg.ShihabSiteLogins[i].ShihabPassword);
                    }
                }
            }
            if (cfg.ShihabDebridAccounts != null)
            {
                for (int i = 0; i < cfg.ShihabDebridAccounts.Count; i++)
                {
                    if (cfg.ShihabDebridAccounts[i] != null && !string.IsNullOrEmpty(cfg.ShihabDebridAccounts[i].MehediEncryptedApiToken))
                    {
                        cfg.ShihabDebridAccounts[i].MehediEncryptedApiToken = ProtectSecret(cfg.ShihabDebridAccounts[i].MehediEncryptedApiToken);
                    }
                }
            }
        }

        public static void UnsealConfigurationSecrets(MehediAppConfiguration cfg)
        {
            if (cfg == null) return;
            if (!string.IsNullOrEmpty(cfg.MehediProxyPassword))
            {
                cfg.MehediProxyPassword = UnprotectSecret(cfg.MehediProxyPassword);
            }
            if (cfg.ShihabSiteLogins != null)
            {
                for (int i = 0; i < cfg.ShihabSiteLogins.Count; i++)
                {
                    if (cfg.ShihabSiteLogins[i] != null && !string.IsNullOrEmpty(cfg.ShihabSiteLogins[i].ShihabPassword))
                    {
                        cfg.ShihabSiteLogins[i].ShihabPassword = UnprotectSecret(cfg.ShihabSiteLogins[i].ShihabPassword);
                    }
                }
            }
        }

        public static int ForgetAllSignIns(ShihabStateStore store)
        {
            if (store == null) return 0;
            int purged = 0;
            MehediAppConfiguration cfg = store.LoadShihabConfig();
            if (!string.IsNullOrEmpty(cfg.ShihabProxyUser) || !string.IsNullOrEmpty(cfg.MehediProxyPassword))
            {
                purged++;
            }
            cfg.ShihabProxyUser = "";
            cfg.MehediProxyPassword = "";
            if (cfg.ShihabSiteLogins != null)
            {
                purged += cfg.ShihabSiteLogins.Count;
                cfg.ShihabSiteLogins.Clear();
            }
            if (cfg.ShihabDebridAccounts != null)
            {
                for (int i = 0; i < cfg.ShihabDebridAccounts.Count; i++)
                {
                    if (!string.IsNullOrEmpty(cfg.ShihabDebridAccounts[i].MehediEncryptedApiToken)) purged++;
                    cfg.ShihabDebridAccounts[i].MehediEnabled = false;
                    cfg.ShihabDebridAccounts[i].MehediEncryptedApiToken = "";
                    cfg.ShihabDebridAccounts[i].ShihabAccountStatus = "Not signed in";
                }
            }
            if (cfg.ShihabPairedRemoteDevices != null)
            {
                purged += cfg.ShihabPairedRemoteDevices.Count;
                cfg.ShihabPairedRemoteDevices.Clear();
            }
            store.SaveMehediConfig(cfg);
            try
            {
                List<ShihabDownloadItem> items = store.LoadMehediDownloads();
                bool changed = false;
                for (int i = 0; i < items.Count; i++)
                {
                    if (!string.IsNullOrEmpty(items[i].MehediAuthPassword) || !string.IsNullOrEmpty(items[i].ShihabCookies))
                    {
                        items[i].ShihabAuthUser = "";
                        items[i].MehediAuthPassword = "";
                        items[i].ShihabCookies = "";
                        purged++;
                        changed = true;
                    }
                }
                if (changed) store.SaveShihabDownloads(items);
            }
            catch { }
            return purged;
        }
    }

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
            ShihabRemotePairingCode = "48291037";
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
                        if (ShihabFormatHelpers.WildcardMatchesMehediPattern(cleanHost, rule.ShihabHostPattern.Trim()) ||
                            string.Equals(cleanHost, rule.ShihabHostPattern.Trim(), StringComparison.OrdinalIgnoreCase))
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
            if (ShihabQuotaHistory == null || ShihabQuotaHistory.Count == 0) return 0L;
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
            Uri shihabUri;
            if (!Uri.TryCreate(mehediUrl, UriKind.Absolute, out shihabUri))
            {
                return null;
            }
            string mehediHostAndPath = (shihabUri.Host + shihabUri.AbsolutePath).ToLowerInvariant();
            for (int shihabI = 0; shihabI < ShihabSiteLogins.Count; shihabI++)
            {
                ShihabSiteLoginCredential mehediCred = ShihabSiteLogins[shihabI];
                if (mehediCred != null && !string.IsNullOrEmpty(mehediCred.ShihabServerPattern))
                {
                    string shihabPat = mehediCred.ShihabServerPattern.Trim().ToLowerInvariant();
                    shihabPat = shihabPat.Replace("https://", "").Replace("http://", "");
                    if (mehediHostAndPath.StartsWith(shihabPat) || shihabUri.Host.Equals(shihabPat, StringComparison.OrdinalIgnoreCase))
                    {
                        return new ShihabSiteLoginCredential
                        {
                            ShihabServerPattern = mehediCred.ShihabServerPattern,
                            MehediUserName = mehediCred.MehediUserName,
                            ShihabPassword = ShihabSecretsVault.UnprotectSecret(mehediCred.ShihabPassword)
                        };
                    }
                }
            }
            return null;
        }
    }

    [DataContract]
    public class ShihabDownloadDatabase
    {
        [DataMember] public List<ShihabDownloadItem> ShihabItems { get; set; }

        public ShihabDownloadDatabase()
        {
            ShihabItems = new List<ShihabDownloadItem>();
        }
    }

    public static class ShihabFormatHelpers
    {
        public static string FormatMehediBytes(long shihabBytes)
        {
            if (shihabBytes < 0)
            {
                return "Unknown";
            }
            if (shihabBytes < 1024L)
            {
                return string.Format("{0} Bytes", shihabBytes);
            }
            double mehediKb = shihabBytes / 1024.0;
            if (mehediKb < 1024.0)
            {
                return string.Format("{0:0.0} KB", mehediKb);
            }
            double shihabMb = mehediKb / 1024.0;
            if (shihabMb < 1024.0)
            {
                return string.Format("{0:0.00} MB", shihabMb);
            }
            double mehediGb = shihabMb / 1024.0;
            return string.Format("{0:0.00} GB", mehediGb);
        }

        public static string FormatShihabSpeed(double mehediBytesPerSec)
        {
            if (mehediBytesPerSec <= 1.0)
            {
                return "";
            }
            if (mehediBytesPerSec < 1024.0)
            {
                return string.Format("{0:0} B/sec", mehediBytesPerSec);
            }
            double shihabKb = mehediBytesPerSec / 1024.0;
            if (shihabKb < 1024.0)
            {
                return string.Format("{0:0.0} KB/sec", shihabKb);
            }
            double mehediMb = shihabKb / 1024.0;
            return string.Format("{0:0.00} MB/sec", mehediMb);
        }

        public static string FormatMehediTimeLeft(long shihabSeconds)
        {
            if (shihabSeconds < 0)
            {
                return "";
            }
            if (shihabSeconds < 60)
            {
                return string.Format("{0} sec", shihabSeconds);
            }
            long mehediMinutes = shihabSeconds / 60;
            long shihabRemSec = shihabSeconds % 60;
            if (mehediMinutes < 60)
            {
                return string.Format("{0} min {1} sec", mehediMinutes, shihabRemSec);
            }
            long shihabHours = mehediMinutes / 60;
            long mehediRemMin = mehediMinutes % 60;
            return string.Format("{0} hr {1} min", shihabHours, mehediRemMin);
        }

        public static string SanitizeShihabFileName(string mehediRawName)
        {
            if (string.IsNullOrWhiteSpace(mehediRawName))
            {
                return "shihab_download.bin";
            }
            string shihabName = mehediRawName.Trim().Trim('"', '\'');
            char[] mehediInvalid = Path.GetInvalidFileNameChars();
            StringBuilder shihabSb = new StringBuilder(shihabName.Length);
            for (int mehediI = 0; mehediI < shihabName.Length; mehediI++)
            {
                char shihabCh = shihabName[mehediI];
                bool mehediBad = false;
                for (int shihabJ = 0; shihabJ < mehediInvalid.Length; shihabJ++)
                {
                    if (shihabCh == mehediInvalid[shihabJ])
                    {
                        mehediBad = true;
                        break;
                    }
                }
                shihabSb.Append(mehediBad ? '_' : shihabCh);
            }
            string mehediResult = shihabSb.ToString().Trim();
            if (string.IsNullOrEmpty(mehediResult) || mehediResult == "." || mehediResult == "..")
            {
                return "shihab_download.bin";
            }
            if (mehediResult.Length > 180)
            {
                string shihabExt = Path.GetExtension(mehediResult);
                string mehediStem = Path.GetFileNameWithoutExtension(mehediResult);
                if (mehediStem.Length > 150)
                {
                    mehediStem = mehediStem.Substring(0, 150);
                }
                mehediResult = mehediStem + shihabExt;
            }
            return mehediResult;
        }

        public static string ExtractMehediFileNameFromUrl(string shihabUrl)
        {
            if (string.IsNullOrWhiteSpace(shihabUrl))
            {
                return "download.bin";
            }
            Uri mehediUri;
            if (Uri.TryCreate(shihabUrl, UriKind.Absolute, out mehediUri))
            {
                string shihabLeaf = Path.GetFileName(Uri.UnescapeDataString(mehediUri.AbsolutePath));
                if (!string.IsNullOrWhiteSpace(shihabLeaf) && shihabLeaf != "/")
                {
                    return SanitizeShihabFileName(shihabLeaf);
                }
                return SanitizeShihabFileName(mehediUri.Host + "_index.html");
            }
            return "download.bin";
        }

        public static bool IsMehediGibberishCdnFileName(string shihabCandidate)
        {
            if (string.IsNullOrWhiteSpace(shihabCandidate)) return true;
            string mehediStem = Path.GetFileNameWithoutExtension(shihabCandidate).Trim();
            if (mehediStem.Length < 2) return true;

            string shihabLower = mehediStem.ToLowerInvariant();
            if (shihabLower == "video" || shihabLower == "videoplayback" || shihabLower == "playback" ||
                shihabLower == "media" || shihabLower == "master" || shihabLower == "index" ||
                shihabLower == "playlist" || shihabLower == "stream" || shihabLower == "watch" ||
                shihabLower == "reel" || shihabLower == "reels" || shihabLower == "download" ||
                shihabLower == "audio" || shihabLower == "clip" || shihabLower == "file" ||
                shihabLower == "chunklist" || shihabLower == "manifest" ||
                shihabLower == "mhs video" || shihabLower == "mhs_video" ||
                shihabLower == "instagram" || shihabLower == "instagram video" ||
                shihabLower == "facebook" || shihabLower == "facebook video" ||
                shihabLower == "youtube" || shihabLower == "youtube video")
            {
                return true;
            }

            if (mehediStem.StartsWith("AQ", StringComparison.Ordinal) && mehediStem.Length >= 12 && mehediStem.IndexOf(' ') < 0)
            {
                return true;
            }
            if (mehediStem.StartsWith("An", StringComparison.Ordinal) && mehediStem.Length >= 12 && mehediStem.IndexOf(' ') < 0)
            {
                return true;
            }
            if (mehediStem.IndexOf(' ') < 0 && mehediStem.Length >= 22)
            {
                int shihabCaseFlips = 0;
                for (int mehediI = 1; mehediI < mehediStem.Length; mehediI++)
                {
                    bool shihabPrevUpper = char.IsUpper(mehediStem[mehediI - 1]);
                    bool mehediCurUpper = char.IsUpper(mehediStem[mehediI]);
                    bool shihabCurDigit = char.IsDigit(mehediStem[mehediI]);
                    if (shihabPrevUpper != mehediCurUpper || shihabCurDigit)
                    {
                        shihabCaseFlips++;
                    }
                }
                if (shihabCaseFlips >= 6) return true;
            }
            return false;
        }

        public static string ResolveSmartMehediFileName(string shihabRawFileName, string mehediUrl, string shihabTabTitle)
        {
            return ResolveSmartMehediFileName(shihabRawFileName, mehediUrl, shihabTabTitle, "");
        }

        public static string ResolveSmartMehediFileName(
            string shihabRawFileName,
            string mehediUrl,
            string shihabTabTitle,
            string mehediQualityLabel)
        {
            string mehediInitial = !string.IsNullOrWhiteSpace(shihabRawFileName)
                ? SanitizeShihabFileName(Path.GetFileName(shihabRawFileName))
                : ExtractMehediFileNameFromUrl(mehediUrl);

            string shihabResolved = mehediInitial;
            if (IsMehediGibberishCdnFileName(mehediInitial) && !string.IsNullOrWhiteSpace(shihabTabTitle))
            {
                string shihabCleanTitle = shihabTabTitle.Trim();
                while (shihabCleanTitle.StartsWith("(") && shihabCleanTitle.IndexOf(')') > 0 && shihabCleanTitle.IndexOf(')') <= 6)
                {
                    shihabCleanTitle = shihabCleanTitle.Substring(shihabCleanTitle.IndexOf(')') + 1).Trim();
                }
                string[] mehediSuffixes = new string[]
                {
                    " | Facebook", " - Facebook", " - YouTube", " | YouTube",
                    " • Instagram", " | Instagram", " - Instagram",
                    " | X", " / X", " | TikTok", " - Vimeo", " : Reddit"
                };
                for (int shihabS = 0; shihabS < mehediSuffixes.Length; shihabS++)
                {
                    if (shihabCleanTitle.EndsWith(mehediSuffixes[shihabS], StringComparison.OrdinalIgnoreCase))
                    {
                        shihabCleanTitle = shihabCleanTitle.Substring(0, shihabCleanTitle.Length - mehediSuffixes[shihabS].Length).Trim();
                    }
                }
                shihabCleanTitle = Regex.Replace(
                    shihabCleanTitle,
                    @"\s*(?:Hide translation|See translation|See original|Translated from [A-Za-z ]+)\s*$",
                    "",
                    RegexOptions.IgnoreCase).Trim();

                if (!string.IsNullOrWhiteSpace(shihabCleanTitle) &&
                    !string.Equals(shihabCleanTitle, "Facebook", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(shihabCleanTitle, "Reels", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(shihabCleanTitle, "YouTube", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(shihabCleanTitle, "Instagram", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(shihabCleanTitle, "MHS Video", StringComparison.OrdinalIgnoreCase))
                {
                    string mehediExt = Path.GetExtension(mehediInitial);
                    if (string.IsNullOrEmpty(mehediExt)) mehediExt = ".mp4";
                    shihabResolved = SanitizeShihabFileName(shihabCleanTitle + mehediExt);
                }
            }

            string mehediStemClean = Regex.Replace(
                Path.GetFileNameWithoutExtension(shihabResolved),
                @"\s*(?:Hide translation|See translation|See original|Translated from [A-Za-z ]+)\s*$",
                "",
                RegexOptions.IgnoreCase).Trim();
            if (!string.IsNullOrWhiteSpace(mehediStemClean))
            {
                shihabResolved = SanitizeShihabFileName(mehediStemClean + Path.GetExtension(shihabResolved));
            }

            if (!string.IsNullOrWhiteSpace(mehediQualityLabel))
            {
                string shihabQual = mehediQualityLabel.Trim();
                string mehediStem = Path.GetFileNameWithoutExtension(shihabResolved);
                string shihabExt = Path.GetExtension(shihabResolved);
                if (mehediStem.IndexOf(shihabQual, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    shihabResolved = SanitizeShihabFileName(string.Format("{0} [{1}]{2}", mehediStem, shihabQual, shihabExt));
                }
            }

            return shihabResolved;
        }

        public static string EnsureShihabUniqueFilePath(string mehediDirectory, string shihabFileName)
        {
            string mehediSafeFile = SanitizeShihabFileName(shihabFileName);
            string shihabFull = Path.Combine(mehediDirectory, mehediSafeFile);
            if (!File.Exists(shihabFull))
            {
                return mehediSafeFile;
            }
            string mehediStem = Path.GetFileNameWithoutExtension(mehediSafeFile);
            string shihabExt = Path.GetExtension(mehediSafeFile);
            for (int mehediCounter = 2; mehediCounter < 10000; mehediCounter++)
            {
                string shihabCandidate = string.Format("{0}_{1}{2}", mehediStem, mehediCounter, shihabExt);
                if (!File.Exists(Path.Combine(mehediDirectory, shihabCandidate)))
                {
                    return shihabCandidate;
                }
            }
            return string.Format("{0}_{1}{2}", mehediStem, Guid.NewGuid().ToString("N").Substring(0, 6), shihabExt);
        }

        public static bool WildcardMatchesMehediPattern(string shihabText, string mehediWildcardPattern)
        {
            if (string.IsNullOrEmpty(mehediWildcardPattern))
            {
                return false;
            }
            string shihabRegex = "^" + Regex.Escape(mehediWildcardPattern.Trim()).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Regex.IsMatch(shihabText ?? "", shihabRegex, RegexOptions.IgnoreCase);
        }
    }

    public class ShihabStateStore
    {
        private readonly object _shihabSyncLock = new object();
        public string MehediBaseDirectory { get; private set; }
        public string ShihabConfigFilePath { get; private set; }
        public string MehediDownloadsFilePath { get; private set; }

        public ShihabStateStore(string shihabCustomDirectory)
        {
            if (!string.IsNullOrWhiteSpace(shihabCustomDirectory))
            {
                MehediBaseDirectory = Path.GetFullPath(shihabCustomDirectory);
            }
            else
            {
                MehediBaseDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ShihabMehediIDM");
            }
            Directory.CreateDirectory(MehediBaseDirectory);
            ShihabConfigFilePath = Path.Combine(MehediBaseDirectory, "shihab_config.json");
            MehediDownloadsFilePath = Path.Combine(MehediBaseDirectory, "mehedi_downloads.json");
        }

        private static byte[] StripShihabUtf8Bom(byte[] mehediRaw)
        {
            if (mehediRaw != null && mehediRaw.Length >= 3 &&
                mehediRaw[0] == 0xEF && mehediRaw[1] == 0xBB && mehediRaw[2] == 0xBF)
            {
                byte[] shihabTrimmed = new byte[mehediRaw.Length - 3];
                Buffer.BlockCopy(mehediRaw, 3, shihabTrimmed, 0, shihabTrimmed.Length);
                return shihabTrimmed;
            }
            return mehediRaw;
        }

        public MehediAppConfiguration LoadShihabConfig()
        {
            lock (_shihabSyncLock)
            {
                try
                {
                    if (File.Exists(ShihabConfigFilePath))
                    {
                        byte[] mehediBytes = StripShihabUtf8Bom(File.ReadAllBytes(ShihabConfigFilePath));
                        using (MemoryStream shihabMs = new MemoryStream(mehediBytes))
                        {
                            DataContractJsonSerializer mehediSer = new DataContractJsonSerializer(typeof(MehediAppConfiguration));
                            MehediAppConfiguration shihabLoaded = mehediSer.ReadObject(shihabMs) as MehediAppConfiguration;
                            if (shihabLoaded != null)
                            {
                                if (shihabLoaded.ShihabCategories == null || shihabLoaded.ShihabCategories.Count == 0)
                                {
                                    shihabLoaded.ShihabCategories = MehediAppConfiguration.CreateShihabDefault(MehediBaseDirectory).ShihabCategories;
                                }
                                else
                                {
                                    bool hasTorrentsCat = false;
                                    for (int ci = 0; ci < shihabLoaded.ShihabCategories.Count; ci++)
                                    {
                                        if (string.Equals(shihabLoaded.ShihabCategories[ci].ShihabName, "Torrents", StringComparison.OrdinalIgnoreCase))
                                        {
                                            hasTorrentsCat = true;
                                            break;
                                        }
                                    }
                                    if (!hasTorrentsCat)
                                    {
                                        string dlRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                                        shihabLoaded.ShihabCategories.Add(new ShihabCategoryRule
                                        {
                                            ShihabName = "Torrents",
                                            MehediExtensionsSpaceSeparated = "torrent magnet",
                                            ShihabDefaultFolder = Path.Combine(dlRoot, "Torrents"),
                                            MehediRememberLastSavePath = true
                                        });
                                    }
                                }
                                if (shihabLoaded.MehediQueues == null || shihabLoaded.MehediQueues.Count == 0)
                                {
                                    shihabLoaded.MehediQueues = MehediAppConfiguration.CreateShihabDefault(MehediBaseDirectory).MehediQueues;
                                }
                                if (shihabLoaded.ShihabSiteLogins == null)
                                {
                                    shihabLoaded.ShihabSiteLogins = new List<ShihabSiteLoginCredential>();
                                }
                                if (shihabLoaded.ShihabHostConnectionLimits == null)
                                {
                                    shihabLoaded.ShihabHostConnectionLimits = new List<ShihabHostConnectionLimit>();
                                }
                                if (shihabLoaded.ShihabDebridAccounts == null || shihabLoaded.ShihabDebridAccounts.Count == 0)
                                {
                                    shihabLoaded.ShihabDebridAccounts = MehediAppConfiguration.CreateShihabDefault(MehediBaseDirectory).ShihabDebridAccounts;
                                }
                                if (shihabLoaded.ShihabRssFeeds == null)
                                {
                                    shihabLoaded.ShihabRssFeeds = new List<ShihabRssFeedConfig>();
                                }
                                if (shihabLoaded.ShihabPairedRemoteDevices == null)
                                {
                                    shihabLoaded.ShihabPairedRemoteDevices = new List<ShihabPairedRemoteDevice>();
                                }
                                if (shihabLoaded.ShihabQuotaHistory == null)
                                {
                                    shihabLoaded.ShihabQuotaHistory = new List<ShihabQuotaBucketEntry>();
                                }
                                if (shihabLoaded.ShihabCustomBrowserPaths == null)
                                {
                                    shihabLoaded.ShihabCustomBrowserPaths = new List<string>();
                                }
                                if (string.IsNullOrWhiteSpace(shihabLoaded.MehediEnabledBrowsersCsv))
                                {
                                    shihabLoaded.MehediEnabledBrowsersCsv = MehediAppConfiguration.DefaultShihabBrowsersCsv;
                                }
                                if (string.IsNullOrWhiteSpace(shihabLoaded.MehediPreventKeyName))
                                {
                                    shihabLoaded.MehediPreventKeyName = "Alt";
                                }
                                if (string.IsNullOrWhiteSpace(shihabLoaded.ShihabForceKeyName))
                                {
                                    shihabLoaded.ShihabForceKeyName = "Ins";
                                }
                                if (string.IsNullOrWhiteSpace(shihabLoaded.MehediAutoCaptureExtensions))
                                {
                                    shihabLoaded.MehediAutoCaptureExtensions = MehediAppConfiguration.DefaultShihabAutoCaptureExtensions;
                                }
                                if (string.IsNullOrWhiteSpace(shihabLoaded.ShihabExcludedSitesSpaceSeparated))
                                {
                                    shihabLoaded.ShihabExcludedSitesSpaceSeparated = MehediAppConfiguration.DefaultMehediExcludedSites;
                                }
                                if (shihabLoaded.MehediLocalBridgePort <= 0)
                                {
                                    shihabLoaded.MehediLocalBridgePort = 45779;
                                }
                                if (shihabLoaded.ShihabRemoteWebPort <= 0)
                                {
                                    shihabLoaded.ShihabRemoteWebPort = 8463;
                                }
                                if (string.IsNullOrWhiteSpace(shihabLoaded.ShihabRemotePairingCode))
                                {
                                    shihabLoaded.ShihabRemotePairingCode = MehediAppConfiguration.GenerateRandomEightDigitCode();
                                }
                                if (string.IsNullOrWhiteSpace(shihabLoaded.ShihabThemeName))
                                {
                                    shihabLoaded.ShihabThemeName = "Obsidian Dark";
                                }
                                if (shihabLoaded.ShihabUiScalePercent < 100 || shihabLoaded.ShihabUiScalePercent > 200)
                                {
                                    shihabLoaded.ShihabUiScalePercent = 100;
                                }
                                if (string.IsNullOrWhiteSpace(shihabLoaded.ShihabLanguageCode))
                                {
                                    shihabLoaded.ShihabLanguageCode = "en";
                                }
                                if (string.IsNullOrWhiteSpace(shihabLoaded.ShihabDnsMode))
                                {
                                    shihabLoaded.ShihabDnsMode = "System";
                                }
                                if (string.IsNullOrWhiteSpace(shihabLoaded.ShihabDnsProvider))
                                {
                                    shihabLoaded.ShihabDnsProvider = "Cloudflare";
                                }
                                if (string.IsNullOrWhiteSpace(shihabLoaded.ShihabExtractSubfolderMode))
                                {
                                    shihabLoaded.ShihabExtractSubfolderMode = "Smart";
                                }
                                if (shihabLoaded.ShihabDefaultMaxConnections <= 0)
                                {
                                    shihabLoaded.ShihabDefaultMaxConnections = 8;
                                }
                                if (string.IsNullOrWhiteSpace(shihabLoaded.MehediTemporaryDirectory))
                                {
                                    shihabLoaded.MehediTemporaryDirectory = Path.Combine(MehediBaseDirectory, "DwnlData");
                                }
                                Directory.CreateDirectory(shihabLoaded.MehediTemporaryDirectory);
                                return shihabLoaded;
                            }
                        }
                    }
                }
                catch
                {
                }
                MehediAppConfiguration mehediFresh = MehediAppConfiguration.CreateShihabDefault(MehediBaseDirectory);
                Directory.CreateDirectory(mehediFresh.MehediTemporaryDirectory);
                SaveMehediConfig(mehediFresh);
                return mehediFresh;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileExW(string lpExistingFileName, string lpNewFileName, int dwFlags);

        private static void ReplaceFileAtomically(string tempFile, string destFile)
        {
            const int MOVEFILE_REPLACE_EXISTING = 0x1;
            const int MOVEFILE_WRITE_THROUGH = 0x8;
            if (!MoveFileExW(tempFile, destFile, MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
            {
                if (File.Exists(destFile))
                {
                    File.Delete(destFile);
                }
                File.Move(tempFile, destFile);
            }
        }

        public string LoadOrCreateBridgeToken()
        {
            lock (_shihabSyncLock)
            {
                try
                {
                    Directory.CreateDirectory(MehediBaseDirectory);
                    string tokPath = Path.Combine(MehediBaseDirectory, "bridge.token");
                    if (File.Exists(tokPath))
                    {
                        string existing = File.ReadAllText(tokPath, Encoding.UTF8).Trim();
                        if (!string.IsNullOrWhiteSpace(existing))
                        {
                            return existing;
                        }
                    }
                    MehediAppConfiguration cfg = LoadShihabConfig();
                    if (string.IsNullOrWhiteSpace(cfg.ShihabLocalBridgeToken))
                    {
                        cfg.ShihabLocalBridgeToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                        SaveMehediConfig(cfg);
                    }
                    else
                    {
                        try { File.WriteAllText(tokPath, cfg.ShihabLocalBridgeToken, Encoding.UTF8); } catch { }
                    }
                    return cfg.ShihabLocalBridgeToken;
                }
                catch
                {
                    return Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                }
            }
        }

        public void SaveMehediConfig(MehediAppConfiguration shihabCfg)
        {
            lock (_shihabSyncLock)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(shihabCfg.ShihabLocalBridgeToken))
                    {
                        shihabCfg.ShihabLocalBridgeToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                    }
                    ShihabSecretsVault.SealConfigurationSecrets(shihabCfg);
                    Directory.CreateDirectory(MehediBaseDirectory);
                    try
                    {
                        File.WriteAllText(Path.Combine(MehediBaseDirectory, "bridge.token"), shihabCfg.ShihabLocalBridgeToken, Encoding.UTF8);
                    }
                    catch { }
                    string mehediTmp = ShihabConfigFilePath + ".tmp";
                    using (FileStream shihabFs = new FileStream(mehediTmp, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        DataContractJsonSerializer mehediSer = new DataContractJsonSerializer(typeof(MehediAppConfiguration));
                        mehediSer.WriteObject(shihabFs, shihabCfg);
                        shihabFs.Flush(true);
                    }
                    ReplaceFileAtomically(mehediTmp, ShihabConfigFilePath);
                }
                catch
                {
                }
            }
        }

        public List<ShihabDownloadItem> LoadMehediDownloads()
        {
            lock (_shihabSyncLock)
            {
                try
                {
                    if (File.Exists(MehediDownloadsFilePath))
                    {
                        byte[] shihabBytes = StripShihabUtf8Bom(File.ReadAllBytes(MehediDownloadsFilePath));
                        using (MemoryStream mehediMs = new MemoryStream(shihabBytes))
                        {
                            DataContractJsonSerializer shihabSer = new DataContractJsonSerializer(typeof(ShihabDownloadDatabase));
                            ShihabDownloadDatabase mehediDb = shihabSer.ReadObject(mehediMs) as ShihabDownloadDatabase;
                            if (mehediDb != null && mehediDb.ShihabItems != null)
                            {
                                for (int shihabI = 0; shihabI < mehediDb.ShihabItems.Count; shihabI++)
                                {
                                    ShihabDownloadItem mehediItem = mehediDb.ShihabItems[shihabI];
                                    if (!string.IsNullOrEmpty(mehediItem.MehediAuthPassword))
                                    {
                                        mehediItem.MehediAuthPassword = ShihabSecretsVault.UnprotectSecret(mehediItem.MehediAuthPassword);
                                    }
                                    if (mehediItem.MehediSegments == null)
                                    {
                                        mehediItem.MehediSegments = new List<MehediSegmentChunk>();
                                    }
                                    ShihabDownloadStatus shihabSt = mehediItem.GetShihabStatusEnum();
                                    if (shihabSt == ShihabDownloadStatus.ShihabDownloading ||
                                        shihabSt == ShihabDownloadStatus.ShihabConnecting ||
                                        shihabSt == ShihabDownloadStatus.ShihabRebuilding)
                                    {
                                        mehediItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabPaused, "Paused");
                                    }
                                }
                                return mehediDb.ShihabItems;
                            }
                        }
                    }
                }
                catch
                {
                }
                return new List<ShihabDownloadItem>();
            }
        }

        public void SaveShihabDownloads(List<ShihabDownloadItem> mehediItems)
        {
            lock (_shihabSyncLock)
            {
                try
                {
                    Directory.CreateDirectory(MehediBaseDirectory);
                    ShihabDownloadDatabase shihabDb = new ShihabDownloadDatabase();
                    shihabDb.ShihabItems = new List<ShihabDownloadItem>();
                    if (mehediItems != null)
                    {
                        for (int i = 0; i < mehediItems.Count; i++)
                        {
                            ShihabDownloadItem item = mehediItems[i];
                            if (item == null || item.ShihabIsPrivateIncognito) continue;
                            if (!string.IsNullOrEmpty(item.MehediAuthPassword))
                            {
                                item.MehediAuthPassword = ShihabSecretsVault.ProtectSecret(item.MehediAuthPassword);
                            }
                            shihabDb.ShihabItems.Add(item);
                        }
                    }
                    string mehediTmp = MehediDownloadsFilePath + ".tmp";
                    using (FileStream shihabFs = new FileStream(mehediTmp, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        DataContractJsonSerializer mehediSer = new DataContractJsonSerializer(typeof(ShihabDownloadDatabase));
                        mehediSer.WriteObject(shihabFs, shihabDb);
                        shihabFs.Flush(true);
                    }
                    ReplaceFileAtomically(mehediTmp, MehediDownloadsFilePath);
                }
                catch
                {
                }
            }
        }

        public static void ExportToShihabEf2File(string mehediFilePath, IEnumerable<ShihabDownloadItem> shihabItems)
        {
            StringBuilder mehediSb = new StringBuilder();
            foreach (ShihabDownloadItem shihabItem in shihabItems)
            {
                if (shihabItem == null || shihabItem.ShihabIsPrivateIncognito || string.IsNullOrWhiteSpace(shihabItem.MehediUrl)) continue;
                mehediSb.AppendLine("<");
                mehediSb.AppendLine(shihabItem.MehediUrl.Trim());
                if (!string.IsNullOrWhiteSpace(shihabItem.MehediReferer))
                {
                    mehediSb.AppendLine("referer: " + shihabItem.MehediReferer.Trim());
                }
                if (!string.IsNullOrWhiteSpace(shihabItem.ShihabCookies))
                {
                    mehediSb.AppendLine("cookie: " + shihabItem.ShihabCookies.Trim());
                }
                if (!string.IsNullOrWhiteSpace(shihabItem.MehediUserAgent))
                {
                    mehediSb.AppendLine("User-Agent: " + shihabItem.MehediUserAgent.Trim());
                }
                mehediSb.AppendLine(">");
            }
            File.WriteAllText(mehediFilePath, mehediSb.ToString(), new UTF8Encoding(false));
        }

        public static List<ShihabDownloadItem> ImportFromMehediEf2OrText(string shihabFilePath)
        {
            List<ShihabDownloadItem> mehediImported = new List<ShihabDownloadItem>();
            if (!File.Exists(shihabFilePath))
            {
                return mehediImported;
            }
            byte[] rawBytes = StripShihabUtf8Bom(File.ReadAllBytes(shihabFilePath));
            string fullText = Encoding.UTF8.GetString(rawBytes);
            return ParseEf2OrEmbeddedUrlsText(fullText);
        }

        public static List<ShihabDownloadItem> ParseEf2OrEmbeddedUrlsText(string fullText)
        {
            List<ShihabDownloadItem> mehediImported = new List<ShihabDownloadItem>();
            if (string.IsNullOrWhiteSpace(fullText)) return mehediImported;

            HashSet<string> seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] shihabLines = fullText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            bool mehediInsideBlock = false;
            ShihabDownloadItem shihabCurrent = null;
            Regex embeddedUrlRx = new Regex(@"(?:https?|ftps?|magnet):(?://|\?)[^\s<>""'`\)\]]+", RegexOptions.IgnoreCase);

            for (int mehediI = 0; mehediI < shihabLines.Length; mehediI++)
            {
                string shihabLine = shihabLines[mehediI].Trim();
                if (string.IsNullOrEmpty(shihabLine)) continue;
                if (shihabLine == "<")
                {
                    mehediInsideBlock = true;
                    shihabCurrent = new ShihabDownloadItem();
                    continue;
                }
                if (shihabLine == ">")
                {
                    if (shihabCurrent != null && !string.IsNullOrWhiteSpace(shihabCurrent.MehediUrl))
                    {
                        if (seenUrls.Add(shihabCurrent.MehediUrl))
                        {
                            shihabCurrent.ShihabFileName = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(shihabCurrent.MehediUrl);
                            mehediImported.Add(shihabCurrent);
                        }
                    }
                    mehediInsideBlock = false;
                    shihabCurrent = null;
                    continue;
                }
                if (mehediInsideBlock && shihabCurrent != null)
                {
                    if (shihabLine.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        shihabLine.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                        shihabLine.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                        shihabLine.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase) ||
                        shihabLine.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
                    {
                        shihabCurrent.MehediUrl = shihabLine;
                        if (shihabLine.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
                        {
                            shihabCurrent.ShihabIsTorrent = true;
                            shihabCurrent.ShihabTorrentMagnetOrPath = shihabLine;
                        }
                    }
                    else if (shihabLine.StartsWith("referer:", StringComparison.OrdinalIgnoreCase))
                    {
                        shihabCurrent.MehediReferer = shihabLine.Substring(8).Trim();
                    }
                    else if (shihabLine.StartsWith("cookie:", StringComparison.OrdinalIgnoreCase))
                    {
                        shihabCurrent.ShihabCookies = shihabLine.Substring(7).Trim();
                    }
                    else if (shihabLine.StartsWith("user-agent:", StringComparison.OrdinalIgnoreCase))
                    {
                        shihabCurrent.MehediUserAgent = shihabLine.Substring(11).Trim();
                    }
                }
                else
                {
                    MatchCollection matches = embeddedUrlRx.Matches(shihabLine);
                    for (int m = 0; m < matches.Count; m++)
                    {
                        string u = System.Net.WebUtility.HtmlDecode(matches[m].Value.TrimEnd('.', ',', ';'));
                        if (string.IsNullOrWhiteSpace(u) || !seenUrls.Add(u)) continue;
                        ShihabDownloadItem mehediPlain = new ShihabDownloadItem();
                        mehediPlain.MehediUrl = u;
                        if (u.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
                        {
                            mehediPlain.ShihabIsTorrent = true;
                            mehediPlain.ShihabTorrentMagnetOrPath = u;
                        }
                        mehediPlain.ShihabFileName = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(u);
                        mehediImported.Add(mehediPlain);
                    }
                }
            }
            return mehediImported;
        }
    }
}

