using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ShihabMehediIDM
{
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
        [DataMember] public bool MehediEnablePeriodicSync { get; set; }
        [DataMember] public int ShihabPeriodicSyncMinutes { get; set; }

        [IgnoreDataMember]
        public bool MehediExitAppWhenQueueFinishes
        {
            get { return MehediExitAppWhenDone; }
            set { MehediExitAppWhenDone = value; }
        }

        [IgnoreDataMember] public bool MehediIsRunningNow { get; set; }
        [IgnoreDataMember] public bool MehediHadWorkInCurrentRun { get; set; }
        [IgnoreDataMember] public string ShihabLastAutoStartMinute { get; set; }
        [IgnoreDataMember] public string MehediLastAutoStopMinute { get; set; }
        [IgnoreDataMember] public DateTime ShihabLastPeriodicSyncUtc { get; set; }

        [OnDeserializing]
        private void InitDefaultsOnDeserializing(StreamingContext ctx)
        {
            ApplyDefaultFieldValues();
        }

        public MehediQueueConfig()
        {
            ApplyDefaultFieldValues();
        }

        private void ApplyDefaultFieldValues()
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
            MehediEnablePeriodicSync = false;
            ShihabPeriodicSyncMinutes = 120;
            MehediIsRunningNow = false;
            MehediHadWorkInCurrentRun = false;
            ShihabLastAutoStartMinute = "";
            MehediLastAutoStopMinute = "";
            ShihabLastPeriodicSyncUtc = DateTime.MinValue;
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
            ApplyDefaults();
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext ctx)
        {
            ApplyDefaults();
        }

        private void ApplyDefaults()
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
}

