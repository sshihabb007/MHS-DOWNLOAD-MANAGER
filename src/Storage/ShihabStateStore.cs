using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public class ShihabStateStore
    {
        private static readonly Regex EmbeddedUrlRx = new Regex(
            @"(?:https?|ftps?|magnet):(?://|\?)[^\s<>""'`\)\]]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(250));

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

        private static void BackupCorruptFileIfExists(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string bak = filePath + ".corrupt." + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".bak";
                    File.Copy(filePath, bak, true);
                }
            }
            catch { }
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
                                NormalizeLoadedConfiguration(shihabLoaded);
                                return shihabLoaded;
                            }
                        }
                        BackupCorruptFileIfExists(ShihabConfigFilePath);
                    }
                }
                catch
                {
                    BackupCorruptFileIfExists(ShihabConfigFilePath);
                }

                MehediAppConfiguration mehediFresh = MehediAppConfiguration.CreateShihabDefault(MehediBaseDirectory);
                Directory.CreateDirectory(mehediFresh.MehediTemporaryDirectory);
                SaveMehediConfig(mehediFresh);
                return mehediFresh;
            }
        }

        private void NormalizeLoadedConfiguration(MehediAppConfiguration shihabLoaded)
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
            else
            {
                bool hasMainQueue = false;
                bool hasSyncQueue = false;
                for (int qi = 0; qi < shihabLoaded.MehediQueues.Count; qi++)
                {
                    MehediQueueConfig qc = shihabLoaded.MehediQueues[qi];
                    if (qc == null) continue;
                    if (qc.ShihabPeriodicSyncMinutes <= 0) qc.ShihabPeriodicSyncMinutes = 120;
                    if (string.Equals(qc.ShihabQueueName, "Main Download Queue", StringComparison.OrdinalIgnoreCase))
                    {
                        hasMainQueue = true;
                    }
                    else if (string.Equals(qc.ShihabQueueName, "Synchronization Queue", StringComparison.OrdinalIgnoreCase))
                    {
                        hasSyncQueue = true;
                    }
                }
                if (!hasMainQueue)
                {
                    shihabLoaded.MehediQueues.Insert(0, new MehediQueueConfig
                    {
                        ShihabQueueName = "Main Download Queue",
                        MehediMaxConcurrentFiles = 4
                    });
                }
                if (!hasSyncQueue)
                {
                    int insertPos = Math.Min(1, shihabLoaded.MehediQueues.Count);
                    shihabLoaded.MehediQueues.Insert(insertPos, new MehediQueueConfig
                    {
                        ShihabQueueName = "Synchronization Queue",
                        MehediMaxConcurrentFiles = 2,
                        MehediEnablePeriodicSync = true,
                        ShihabPeriodicSyncMinutes = 120
                    });
                }
            }

            if (shihabLoaded.ShihabSiteLogins == null) shihabLoaded.ShihabSiteLogins = new List<ShihabSiteLoginCredential>();
            if (shihabLoaded.ShihabHostConnectionLimits == null) shihabLoaded.ShihabHostConnectionLimits = new List<ShihabHostConnectionLimit>();
            if (shihabLoaded.ShihabDebridAccounts == null || shihabLoaded.ShihabDebridAccounts.Count == 0)
            {
                shihabLoaded.ShihabDebridAccounts = MehediAppConfiguration.CreateShihabDefault(MehediBaseDirectory).ShihabDebridAccounts;
            }
            if (shihabLoaded.ShihabRssFeeds == null) shihabLoaded.ShihabRssFeeds = new List<ShihabRssFeedConfig>();
            if (shihabLoaded.ShihabPairedRemoteDevices == null) shihabLoaded.ShihabPairedRemoteDevices = new List<ShihabPairedRemoteDevice>();
            if (shihabLoaded.ShihabQuotaHistory == null) shihabLoaded.ShihabQuotaHistory = new List<ShihabQuotaBucketEntry>();
            if (shihabLoaded.ShihabCustomBrowserPaths == null) shihabLoaded.ShihabCustomBrowserPaths = new List<string>();
            if (string.IsNullOrWhiteSpace(shihabLoaded.MehediEnabledBrowsersCsv)) shihabLoaded.MehediEnabledBrowsersCsv = MehediAppConfiguration.DefaultShihabBrowsersCsv;
            if (string.IsNullOrWhiteSpace(shihabLoaded.MehediPreventKeyName)) shihabLoaded.MehediPreventKeyName = "Alt";
            if (string.IsNullOrWhiteSpace(shihabLoaded.ShihabForceKeyName)) shihabLoaded.ShihabForceKeyName = "Ins";
            if (string.IsNullOrWhiteSpace(shihabLoaded.MehediAutoCaptureExtensions)) shihabLoaded.MehediAutoCaptureExtensions = MehediAppConfiguration.DefaultShihabAutoCaptureExtensions;
            if (string.IsNullOrWhiteSpace(shihabLoaded.ShihabExcludedSitesSpaceSeparated)) shihabLoaded.ShihabExcludedSitesSpaceSeparated = MehediAppConfiguration.DefaultMehediExcludedSites;
            if (shihabLoaded.MehediLocalBridgePort <= 0) shihabLoaded.MehediLocalBridgePort = 45779;
            if (shihabLoaded.ShihabRemoteWebPort <= 0) shihabLoaded.ShihabRemoteWebPort = 8463;
            if (string.IsNullOrWhiteSpace(shihabLoaded.ShihabRemotePairingCode) ||
                string.Equals(shihabLoaded.ShihabRemotePairingCode.Trim(), "48291037", StringComparison.Ordinal))
            {
                shihabLoaded.ShihabRemotePairingCode = MehediAppConfiguration.GenerateRandomEightDigitCode();
                shihabLoaded.ShihabRemotePairingExpiresUtc = DateTime.UtcNow.AddMinutes(10).ToString("o");
            }
            if (string.IsNullOrWhiteSpace(shihabLoaded.ShihabThemeName)) shihabLoaded.ShihabThemeName = "Obsidian Dark";
            if (shihabLoaded.ShihabUiScalePercent < 80 || shihabLoaded.ShihabUiScalePercent > 200) shihabLoaded.ShihabUiScalePercent = 100;
            if (string.IsNullOrWhiteSpace(shihabLoaded.ShihabLanguageCode)) shihabLoaded.ShihabLanguageCode = "en";
            if (string.IsNullOrWhiteSpace(shihabLoaded.ShihabDnsMode)) shihabLoaded.ShihabDnsMode = "System";
            if (string.IsNullOrWhiteSpace(shihabLoaded.ShihabDnsProvider)) shihabLoaded.ShihabDnsProvider = "Cloudflare";
            if (string.IsNullOrWhiteSpace(shihabLoaded.ShihabExtractSubfolderMode)) shihabLoaded.ShihabExtractSubfolderMode = "Smart";
            if (shihabLoaded.ShihabDefaultMaxConnections <= 0) shihabLoaded.ShihabDefaultMaxConnections = 8;
            if (string.IsNullOrWhiteSpace(shihabLoaded.MehediTemporaryDirectory))
            {
                shihabLoaded.MehediTemporaryDirectory = Path.Combine(MehediBaseDirectory, "DwnlData");
            }
            Directory.CreateDirectory(shihabLoaded.MehediTemporaryDirectory);
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
                            ShihabSecretsVault.ApplyRestrictedUserFileAcl(tokPath);
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
                        WriteProtectedTokenFile(tokPath, cfg.ShihabLocalBridgeToken);
                    }
                    return cfg.ShihabLocalBridgeToken;
                }
                catch
                {
                    return Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                }
            }
        }

        private static void WriteProtectedTokenFile(string tokPath, string token)
        {
            string tmp = tokPath + "." + Path.GetRandomFileName() + ".tmp";
            try
            {
                File.WriteAllText(tmp, token, new UTF8Encoding(false));
                ShihabSecretsVault.ApplyRestrictedUserFileAcl(tmp);
                ReplaceFileAtomically(tmp, tokPath);
                ShihabSecretsVault.ApplyRestrictedUserFileAcl(tokPath);
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        public void SaveMehediConfig(MehediAppConfiguration shihabCfg)
        {
            if (shihabCfg == null) return;
            lock (_shihabSyncLock)
            {
                string mehediTmp = ShihabConfigFilePath + "." + Path.GetRandomFileName() + ".tmp";
                try
                {
                    if (string.IsNullOrWhiteSpace(shihabCfg.ShihabLocalBridgeToken))
                    {
                        shihabCfg.ShihabLocalBridgeToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                    }
                    ShihabSecretsVault.SealConfigurationSecrets(shihabCfg);
                    Directory.CreateDirectory(MehediBaseDirectory);
                    WriteProtectedTokenFile(Path.Combine(MehediBaseDirectory, "bridge.token"), shihabCfg.ShihabLocalBridgeToken);

                    // Serialize a snapshot of quota history so concurrent workers cannot throw during enumeration
                    List<ShihabQuotaBucketEntry> originalQuota = shihabCfg.ShihabQuotaHistory;
                    try
                    {
                        shihabCfg.ShihabQuotaHistory = shihabCfg.CloneQuotaHistorySnapshot();
                        using (FileStream shihabFs = new FileStream(mehediTmp, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            DataContractJsonSerializer mehediSer = new DataContractJsonSerializer(typeof(MehediAppConfiguration));
                            mehediSer.WriteObject(shihabFs, shihabCfg);
                            shihabFs.Flush(true);
                        }
                    }
                    finally
                    {
                        shihabCfg.ShihabQuotaHistory = originalQuota;
                    }

                    ShihabSecretsVault.ApplyRestrictedUserFileAcl(mehediTmp);
                    ReplaceFileAtomically(mehediTmp, ShihabConfigFilePath);
                    ShihabSecretsVault.ApplyRestrictedUserFileAcl(ShihabConfigFilePath);
                }
                catch
                {
                    try { if (File.Exists(mehediTmp)) File.Delete(mehediTmp); } catch { }
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
                                    if (mehediItem == null) continue;
                                    ShihabSecretsVault.UnsealDownloadItemSecrets(mehediItem);
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
                        BackupCorruptFileIfExists(MehediDownloadsFilePath);
                    }
                }
                catch
                {
                    BackupCorruptFileIfExists(MehediDownloadsFilePath);
                }
                return new List<ShihabDownloadItem>();
            }
        }

        public void SaveShihabDownloads(List<ShihabDownloadItem> mehediItems)
        {
            lock (_shihabSyncLock)
            {
                string mehediTmp = MehediDownloadsFilePath + "." + Path.GetRandomFileName() + ".tmp";
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
                            shihabDb.ShihabItems.Add(item.CreatePersistenceCopyWithProtectedSecrets());
                        }
                    }
                    using (FileStream shihabFs = new FileStream(mehediTmp, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        DataContractJsonSerializer mehediSer = new DataContractJsonSerializer(typeof(ShihabDownloadDatabase));
                        mehediSer.WriteObject(shihabFs, shihabDb);
                        shihabFs.Flush(true);
                    }
                    ShihabSecretsVault.ApplyRestrictedUserFileAcl(mehediTmp);
                    ReplaceFileAtomically(mehediTmp, MehediDownloadsFilePath);
                    ShihabSecretsVault.ApplyRestrictedUserFileAcl(MehediDownloadsFilePath);
                }
                catch
                {
                    try { if (File.Exists(mehediTmp)) File.Delete(mehediTmp); } catch { }
                }
            }
        }

        private static string StripLineBreaks(string s)
        {
            return (s ?? "").Replace("\r", "").Replace("\n", "").Trim();
        }

        public static void ExportToShihabEf2File(string mehediFilePath, IEnumerable<ShihabDownloadItem> shihabItems)
        {
            StringBuilder mehediSb = new StringBuilder();
            foreach (ShihabDownloadItem shihabItem in shihabItems)
            {
                if (shihabItem == null || shihabItem.ShihabIsPrivateIncognito || string.IsNullOrWhiteSpace(shihabItem.MehediUrl)) continue;
                mehediSb.AppendLine("<");
                mehediSb.AppendLine(StripLineBreaks(shihabItem.MehediUrl));
                if (!string.IsNullOrWhiteSpace(shihabItem.MehediReferer))
                {
                    mehediSb.AppendLine("referer: " + StripLineBreaks(shihabItem.MehediReferer));
                }
                if (!string.IsNullOrWhiteSpace(shihabItem.ShihabCookies))
                {
                    string cookies = ShihabSecretsVault.UnprotectSecret(shihabItem.ShihabCookies);
                    if (!string.IsNullOrWhiteSpace(cookies))
                    {
                        mehediSb.AppendLine("cookie: " + StripLineBreaks(cookies));
                    }
                }
                if (!string.IsNullOrWhiteSpace(shihabItem.MehediUserAgent))
                {
                    mehediSb.AppendLine("User-Agent: " + StripLineBreaks(shihabItem.MehediUserAgent));
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
            FileInfo fi = new FileInfo(shihabFilePath);
            if (fi.Length > 16 * 1024 * 1024)
            {
                throw new InvalidDataException("EF2 import file exceeds 16 MiB limit.");
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
                        Uri validated;
                        if (ShihabProcessAndPathSafety.TryValidateExternalShellUri(shihabCurrent.MehediUrl, true, out validated) &&
                            seenUrls.Add(shihabCurrent.MehediUrl))
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
                    MatchCollection matches;
                    try { matches = EmbeddedUrlRx.Matches(shihabLine); }
                    catch { continue; }
                    for (int m = 0; m < matches.Count; m++)
                    {
                        string u = WebUtility.HtmlDecode(matches[m].Value.TrimEnd('.', ',', ';'));
                        Uri validated;
                        if (string.IsNullOrWhiteSpace(u) ||
                            !ShihabProcessAndPathSafety.TryValidateExternalShellUri(u, true, out validated) ||
                            !seenUrls.Add(u))
                        {
                            continue;
                        }
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

