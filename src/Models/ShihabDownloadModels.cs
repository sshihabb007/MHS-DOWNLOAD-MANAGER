using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.IO;

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
            ApplyDefaults();
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext ctx)
        {
            ApplyDefaults();
        }

        private void ApplyDefaults()
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
            return Path.Combine(mehediDir, ShihabFormatHelpers.SanitizeShihabFileName(ShihabFileName));
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

        internal ShihabDownloadItem CreatePersistenceCopyWithProtectedSecrets()
        {
            ShihabDownloadItem copy = (ShihabDownloadItem)MemberwiseClone();
            if (MehediSegments != null)
            {
                lock (MehediSegments)
                {
                    copy.MehediSegments = new List<MehediSegmentChunk>(MehediSegments.Count);
                    for (int i = 0; i < MehediSegments.Count; i++)
                    {
                        MehediSegmentChunk s = MehediSegments[i];
                        if (s != null)
                        {
                            copy.MehediSegments.Add(new MehediSegmentChunk
                            {
                                ShihabSegmentIndex = s.ShihabSegmentIndex,
                                MehediStartByte = s.MehediStartByte,
                                ShihabEndByte = s.ShihabEndByte,
                                MehediDownloadedBytes = s.MehediDownloadedBytes,
                                ShihabSegmentState = s.ShihabSegmentState,
                                MehediPartFilePath = s.MehediPartFilePath
                            });
                        }
                    }
                }
            }
            if (ShihabSelectedTorrentFiles != null)
            {
                copy.ShihabSelectedTorrentFiles = new List<int>(ShihabSelectedTorrentFiles);
            }
            ShihabSecretsVault.SealDownloadItemSecrets(copy);
            return copy;
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
}
