using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public partial class MehediDynamicSegmentEngine
    {
        private readonly ShihabStateStore _shihabStore;
        private readonly MehediAppConfiguration _mehediConfig;
        private readonly List<ShihabDownloadItem> _shihabDownloads;
        private readonly Dictionary<string, ActiveDownloadHandle> _mehediActiveDownloads;
        private readonly Dictionary<string, object> _segmentLocks;
        private readonly HashSet<string> _explicitlyPausedIds;
        private readonly Dictionary<string, DateTime> _nextRetryAfterUtc;
        private readonly Dictionary<string, int> _adaptiveHostConcurrencyCaps;
        private readonly Dictionary<string, DateTime> _hostRetryCooldownUntilUtc;
        private readonly object _shihabEngineLock = new object();
        private readonly object _mehediRateLock = new object();
        private double _globalTokenBucketBytes = 0.0;
        private long _globalLastBucketTickMs = 0L;
        private readonly Stopwatch _globalRateStopwatch = Stopwatch.StartNew();

        public event Action<ShihabDownloadItem> OnShihabItemUpdated;
        public event Action<ShihabDownloadItem> OnMehediItemCompleted;
        public event Action<ShihabDownloadItem> OnShihabItemFailed;
        public event Action<string> OnShihabEngineLog;
        public event Action<MehediQueueConfig> OnQueueFinishedWithActions;

        public MehediDynamicSegmentEngine(
            ShihabStateStore shihabStore,
            MehediAppConfiguration mehediConfig,
            List<ShihabDownloadItem> shihabDownloads)
        {
            _shihabStore = shihabStore;
            _mehediConfig = mehediConfig;
            _shihabDownloads = shihabDownloads ?? new List<ShihabDownloadItem>();
            _mehediActiveDownloads = new Dictionary<string, ActiveDownloadHandle>(StringComparer.Ordinal);
            _segmentLocks = new Dictionary<string, object>(StringComparer.Ordinal);
            _explicitlyPausedIds = new HashSet<string>(StringComparer.Ordinal);
            _nextRetryAfterUtc = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            _adaptiveHostConcurrencyCaps = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _hostRetryCooldownUntilUtc = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

            ShihabLocalNetworkRelay.OnVpnKillSwitchTripped += HandleVpnKillSwitchTripped;

            ServicePointManager.DefaultConnectionLimit = 256;
            ServicePointManager.Expect100Continue = false;
            try
            {
                ServicePointManager.CheckCertificateRevocationList = true;
            }
            catch { }

            try
            {
                // Enforce TLS 1.2 / TLS 1.3 only (drop deprecated TLS 1.0/1.1)
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)12288;
            }
            catch
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            }
        }

        private object GetItemSegmentLock(ShihabDownloadItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.ShihabId))
            {
                return new object();
            }
            lock (_shihabEngineLock)
            {
                object segLock;
                if (!_segmentLocks.TryGetValue(item.ShihabId, out segLock))
                {
                    segLock = new object();
                    _segmentLocks[item.ShihabId] = segLock;
                }
                if (item.MehediSegments == null)
                {
                    item.MehediSegments = new List<MehediSegmentChunk>();
                }
                return segLock;
            }
        }

        private void HandleVpnKillSwitchTripped(string reason)
        {
            EmitShihabLog("[KillSwitch] " + reason + " — aborting active transfers immediately.");
            List<ShihabDownloadItem> snap = GetShihabDownloadsSnapshot();
            for (int i = 0; i < snap.Count; i++)
            {
                if (IsMehediItemActive(snap[i].ShihabId))
                {
                    PauseShihabDownloadInternal(snap[i], true);
                    snap[i].SetMehediStatusEnum(ShihabDownloadStatus.ShihabPaused, "Paused (VPN Kill-Switch)");
                    NotifyShihabItemChanged(snap[i]);
                }
            }
        }

        public void StartStartupQueues()
        {
            if (_mehediConfig == null || _mehediConfig.MehediQueues == null) return;
            for (int i = 0; i < _mehediConfig.MehediQueues.Count; i++)
            {
                MehediQueueConfig q = _mehediConfig.MehediQueues[i];
                if (q != null && q.ShihabStartOnStartup)
                {
                    StartShihabQueue(q.ShihabQueueName);
                }
            }
            SyncNextScheduledWakeTimer();
        }

        public List<ShihabDownloadItem> GetShihabDownloadsSnapshot()
        {
            lock (_shihabEngineLock)
            {
                return new List<ShihabDownloadItem>(_shihabDownloads);
            }
        }

        public bool IsMehediItemActive(string shihabItemId)
        {
            if (string.IsNullOrEmpty(shihabItemId)) return false;
            lock (_shihabEngineLock)
            {
                return _mehediActiveDownloads.ContainsKey(shihabItemId);
            }
        }

        public bool HasAnyActiveDownloads()
        {
            lock (_shihabEngineLock)
            {
                return _mehediActiveDownloads.Count > 0;
            }
        }

        public bool HasAnyActiveDownloadsOrQueues()
        {
            lock (_shihabEngineLock)
            {
                if (_mehediActiveDownloads.Count > 0) return true;
            }
            if (_mehediConfig != null && _mehediConfig.MehediQueues != null)
            {
                for (int i = 0; i < _mehediConfig.MehediQueues.Count; i++)
                {
                    MehediQueueConfig q = _mehediConfig.MehediQueues[i];
                    if (q != null && q.MehediIsRunningNow) return true;
                }
            }
            return false;
        }

        public bool IsShihabQueueRunning(string queueName)
        {
            if (_mehediConfig == null || _mehediConfig.MehediQueues == null) return false;
            string canonical = ResolveCanonicalQueueName(queueName);
            for (int i = 0; i < _mehediConfig.MehediQueues.Count; i++)
            {
                MehediQueueConfig q = _mehediConfig.MehediQueues[i];
                if (q != null && string.Equals(q.ShihabQueueName, canonical, StringComparison.OrdinalIgnoreCase))
                {
                    return q.MehediIsRunningNow;
                }
            }
            return false;
        }

        public bool MoveQueueItemOrder(ShihabDownloadItem item, int direction)
        {
            if (item == null || direction == 0) return false;
            string targetQueue = ResolveCanonicalQueueName(item.MehediQueueName);
            bool moved = false;
            lock (_shihabEngineLock)
            {
                int currentIdx = -1;
                for (int i = 0; i < _shihabDownloads.Count; i++)
                {
                    if (_shihabDownloads[i] != null && string.Equals(_shihabDownloads[i].ShihabId, item.ShihabId, StringComparison.Ordinal))
                    {
                        currentIdx = i;
                        break;
                    }
                }
                if (currentIdx < 0) return false;

                int step = direction < 0 ? -1 : 1;
                int neighborIdx = -1;
                for (int j = currentIdx + step; j >= 0 && j < _shihabDownloads.Count; j += step)
                {
                    ShihabDownloadItem cand = _shihabDownloads[j];
                    if (cand != null && string.Equals(ResolveCanonicalQueueName(cand.MehediQueueName), targetQueue, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(cand.MehediQueueName))
                    {
                        neighborIdx = j;
                        break;
                    }
                }
                if (neighborIdx >= 0)
                {
                    ShihabDownloadItem tmp = _shihabDownloads[currentIdx];
                    _shihabDownloads[currentIdx] = _shihabDownloads[neighborIdx];
                    _shihabDownloads[neighborIdx] = tmp;
                    _shihabStore.SaveShihabDownloads(_shihabDownloads);
                    moved = true;
                }
            }
            if (moved)
            {
                NotifyShihabItemChanged(item);
            }
            return moved;
        }

        public void AddShihabDownloadItem(ShihabDownloadItem mehediItem, bool shihabStartImmediately)
        {
            if (mehediItem == null) return;
            lock (_shihabEngineLock)
            {
                _shihabDownloads.Insert(0, mehediItem);
                _shihabStore.SaveShihabDownloads(_shihabDownloads);
            }
            NotifyShihabItemChanged(mehediItem);
            if (shihabStartImmediately)
            {
                StartOrResumeMehediDownload(mehediItem);
            }
        }

        public void StartOrResumeMehediDownload(ShihabDownloadItem shihabItem)
        {
            if (shihabItem == null) return;
            ActiveDownloadHandle handle;
            lock (_shihabEngineLock)
            {
                if (_mehediActiveDownloads.ContainsKey(shihabItem.ShihabId))
                {
                    return;
                }
                handle = new ActiveDownloadHandle
                {
                    Cts = new CancellationTokenSource()
                };
                _mehediActiveDownloads[shihabItem.ShihabId] = handle;
                _explicitlyPausedIds.Remove(shihabItem.ShihabId);
                _nextRetryAfterUtc.Remove(shihabItem.ShihabId);
            }

            shihabItem.MehediLastTryDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabConnecting, "Connecting...");
            NotifyShihabItemChanged(shihabItem);

            handle.WorkerTask = Task.Run(delegate
            {
                ExecuteShihabDownloadLifecycle(shihabItem, handle);
            });
        }

        private void ReleaseActiveHandleIfCurrent(string shihabItemId, ActiveDownloadHandle handle)
        {
            if (string.IsNullOrEmpty(shihabItemId) || handle == null) return;
            lock (_shihabEngineLock)
            {
                ActiveDownloadHandle current;
                if (_mehediActiveDownloads.TryGetValue(shihabItemId, out current) && object.ReferenceEquals(current, handle))
                {
                    _mehediActiveDownloads.Remove(shihabItemId);
                }
            }
        }

        private void CompleteActiveDownload(ShihabDownloadItem shihabItem, ActiveDownloadHandle handle, string completeStatusLabel)
        {
            ReleaseActiveHandleIfCurrent(shihabItem.ShihabId, handle);
            lock (_shihabEngineLock)
            {
                _explicitlyPausedIds.Remove(shihabItem.ShihabId);
                _nextRetryAfterUtc.Remove(shihabItem.ShihabId);
            }
            UpdateSleepStateForActiveDownloads();
            shihabItem.ShihabCurrentSpeedBps = 0;
            shihabItem.MehediEtaSeconds = 0;
            shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabCompleted, completeStatusLabel);
            PersistShihabState();
            NotifyShihabItemChanged(shihabItem);
            if (OnMehediItemCompleted != null)
            {
                OnMehediItemCompleted(shihabItem);
            }
        }

        private void UpdateSleepStateForActiveDownloads()
        {
            bool anyActive;
            lock (_shihabEngineLock)
            {
                anyActive = _mehediActiveDownloads.Count > 0;
            }
            ShihabOsAndDiskSafety.UpdateSystemSleepPrevention(anyActive, _mehediConfig != null && _mehediConfig.ShihabPreventSystemSleep);
        }

        public void PauseShihabDownload(ShihabDownloadItem mehediItem)
        {
            PauseShihabDownloadInternal(mehediItem, true);
        }

        private ActiveDownloadHandle PauseShihabDownloadInternal(ShihabDownloadItem mehediItem, bool markExplicitlyPaused)
        {
            if (mehediItem == null) return null;
            ActiveDownloadHandle handle = null;
            lock (_shihabEngineLock)
            {
                if (markExplicitlyPaused && !string.IsNullOrEmpty(mehediItem.ShihabId))
                {
                    _explicitlyPausedIds.Add(mehediItem.ShihabId);
                }
                if (_mehediActiveDownloads.TryGetValue(mehediItem.ShihabId, out handle))
                {
                    _mehediActiveDownloads.Remove(mehediItem.ShihabId);
                }
            }
            if (handle != null && handle.Cts != null)
            {
                try { handle.Cts.Cancel(); } catch (ObjectDisposedException) { }
            }
            UpdateSleepStateForActiveDownloads();
            if (mehediItem.GetShihabStatusEnum() != ShihabDownloadStatus.ShihabCompleted)
            {
                mehediItem.ShihabCurrentSpeedBps = 0;
                mehediItem.MehediEtaSeconds = -1;
                mehediItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabPaused, string.Format("Paused ({0:0.0}%)", mehediItem.GetMehediPercent()));
                PersistShihabState();
                NotifyShihabItemChanged(mehediItem);
            }
            return handle;
        }

        public void StopAllMehediDownloads()
        {
            if (_mehediConfig != null && _mehediConfig.MehediQueues != null)
            {
                for (int q = 0; q < _mehediConfig.MehediQueues.Count; q++)
                {
                    if (_mehediConfig.MehediQueues[q] != null)
                    {
                        _mehediConfig.MehediQueues[q].MehediIsRunningNow = false;
                        _mehediConfig.MehediQueues[q].MehediHadWorkInCurrentRun = false;
                    }
                }
            }
            List<ShihabDownloadItem> shihabSnap = GetShihabDownloadsSnapshot();
            for (int mehediI = 0; mehediI < shihabSnap.Count; mehediI++)
            {
                if (IsMehediItemActive(shihabSnap[mehediI].ShihabId))
                {
                    PauseShihabDownload(shihabSnap[mehediI]);
                }
            }
        }

        public void StopAllShihabQueues()
        {
            if (_mehediConfig == null || _mehediConfig.MehediQueues == null) return;
            for (int q = 0; q < _mehediConfig.MehediQueues.Count; q++)
            {
                MehediQueueConfig qc = _mehediConfig.MehediQueues[q];
                if (qc != null)
                {
                    StopShihabQueue(qc.ShihabQueueName);
                }
            }
        }

        public void RemoveShihabDownload(ShihabDownloadItem mehediItem, bool shihabDeleteCompletedDiskFile)
        {
            if (mehediItem == null) return;
            ActiveDownloadHandle activeHandle = PauseShihabDownloadInternal(mehediItem, true);
            if (activeHandle != null && activeHandle.WorkerTask != null)
            {
                try { activeHandle.WorkerTask.Wait(4000); } catch { }
            }

            lock (_shihabEngineLock)
            {
                _shihabDownloads.Remove(mehediItem);
                if (!string.IsNullOrEmpty(mehediItem.ShihabId))
                {
                    _segmentLocks.Remove(mehediItem.ShihabId);
                    _explicitlyPausedIds.Remove(mehediItem.ShihabId);
                    _nextRetryAfterUtc.Remove(mehediItem.ShihabId);
                }
                _shihabStore.SaveShihabDownloads(_shihabDownloads);
            }
            try
            {
                string mehediPartDir = GetShihabItemTempFolder(mehediItem);
                if (!string.IsNullOrEmpty(mehediPartDir) && Directory.Exists(mehediPartDir))
                {
                    Directory.Delete(mehediPartDir, true);
                }
                string shihabTarget = mehediItem.GetShihabFullTargetPath();
                string sparsePart = !string.IsNullOrWhiteSpace(mehediItem.ShihabPartFilePath)
                    ? mehediItem.ShihabPartFilePath
                    : (shihabTarget + ".mhs.part");
                if (File.Exists(sparsePart))
                {
                    try { File.Delete(sparsePart); } catch { }
                }
                if (shihabDeleteCompletedDiskFile && File.Exists(shihabTarget))
                {
                    File.Delete(shihabTarget);
                }
            }
            catch
            {
            }
        }

        private string GetShihabItemTempFolder(ShihabDownloadItem mehediItem)
        {
            string tempRoot = ShihabProcessAndPathSafety.NormalizeFullDirectoryPath(_mehediConfig.MehediTemporaryDirectory);
            string safeId = ShihabProcessAndPathSafety.SanitizeFileName(mehediItem != null ? mehediItem.ShihabId : null, "item_tmp");
            string resolved;
            if (ShihabProcessAndPathSafety.ResolvePathStrictlyUnderRoot(tempRoot, safeId, out resolved))
            {
                return resolved;
            }
            return Path.Combine(tempRoot, "item_fallback");
        }

        private void ExecuteShihabDownloadLifecycle(ShihabDownloadItem shihabItem, ActiveDownloadHandle handle)
        {
            CancellationToken mehediToken = handle.Cts.Token;
            object shihabSegmentLock = GetItemSegmentLock(shihabItem);
            try
            {
                if (_mehediConfig.IsQuotaExceeded())
                {
                    ReleaseActiveHandleIfCurrent(shihabItem.ShihabId, handle);
                    UpdateSleepStateForActiveDownloads();
                    shihabItem.ShihabCurrentSpeedBps = 0;
                    shihabItem.MehediEtaSeconds = -1;
                    shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabPaused, "Paused (Quota limit reached)");
                    PersistShihabState();
                    NotifyShihabItemChanged(shihabItem);
                    return;
                }

                ShihabOsAndDiskSafety.UpdateSystemSleepPrevention(true, _mehediConfig.ShihabPreventSystemSleep);

                Directory.CreateDirectory(_mehediConfig.MehediTemporaryDirectory);
                string mehediItemTempDir = GetShihabItemTempFolder(shihabItem);
                Directory.CreateDirectory(mehediItemTempDir);

                if (string.IsNullOrWhiteSpace(shihabItem.MehediSaveDirectory))
                {
                    ShihabCategoryRule shihabCat = _mehediConfig.ResolveMehediCategoryForFile(shihabItem.ShihabFileName);
                    shihabItem.ShihabCategoryName = shihabCat.ShihabName;
                    shihabItem.MehediSaveDirectory = shihabCat.ShihabDefaultFolder;
                }
                Directory.CreateDirectory(shihabItem.MehediSaveDirectory);

                // 1. Handle BitTorrent / Magnet items
                if (shihabItem.ShihabIsTorrent || shihabItem.ShihabIsTorrentOrMagnet || ShihabTorrentManager.IsMagnetOrTorrentInput(shihabItem.MehediUrl))
                {
                    shihabItem.ShihabIsTorrent = true;
                    ExecuteMehediTorrentDownload(shihabItem, mehediItemTempDir, mehediToken, handle);
                    return;
                }

                // 2. Handle yt-dlp Video / Playlist / Audio extraction jobs
                if (shihabItem.ShihabIsYtDlpJob)
                {
                    ExecuteMehediYtDlpJob(shihabItem, mehediToken, handle);
                    return;
                }

                // 3. Handle gallery-dl batch image gallery jobs
                if (shihabItem.ShihabIsGalleryJob)
                {
                    ExecuteMehediGalleryDlJob(shihabItem, mehediToken, handle);
                    return;
                }

                TryHealOrResolveShihabYouTubeItem(shihabItem, mehediItemTempDir, false);

                bool mehediIsGvUrl = IsShihabGoogleVideoUrl(shihabItem.MehediUrl);
                if (mehediIsGvUrl)
                {
                    long shihabGvClen;
                    shihabItem.MehediUrl = SanitizeMehediGoogleVideoUrl(shihabItem.MehediUrl, out shihabGvClen);
                    if (shihabGvClen > 0 && shihabItem.ShihabTotalBytes <= 0)
                    {
                        shihabItem.ShihabTotalBytes = shihabGvClen;
                    }
                }

                shihabItem.ShihabTransientUnlockedUrl = "";
                shihabItem.ShihabTransientMaxChunks = 0;

                ShihabProbeMetadata mehediProbe = ProbeMehediUrl(shihabItem.MehediUrl, shihabItem);
                if (mehediToken.IsCancellationRequested) return;

                string effectiveUrl = GetEffectiveHttpUrl(shihabItem, shihabItem.MehediUrl);
                mehediIsGvUrl = IsShihabGoogleVideoUrl(effectiveUrl);

                if (mehediProbe.MehediSuccess)
                {
                    if (mehediProbe.MehediIsHlsPlaylist || effectiveUrl.IndexOf(".m3u8", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        shihabItem.MehediIsHlsStream = true;
                        ExecuteMehediHlsStreamDownload(shihabItem, mehediProbe.ShihabFinalUrl, mehediItemTempDir, mehediToken, handle);
                        return;
                    }

                    // RFC 9110 Validator Check on Resume (Strong ETag + Last-Modified + Content-Length)
                    Uri finalUri;
                    string probeHost = Uri.TryCreate(mehediProbe.ShihabFinalUrl ?? effectiveUrl, UriKind.Absolute, out finalUri)
                        ? finalUri.Host
                        : "";
                    if (shihabItem.MehediDownloadedBytes > 0)
                    {
                        bool sameValidatorHost = string.IsNullOrEmpty(shihabItem.ShihabETagHost) ||
                            string.Equals(shihabItem.ShihabETagHost, probeHost, StringComparison.OrdinalIgnoreCase);

                        bool strongEtagMismatch = sameValidatorHost &&
                            !string.IsNullOrEmpty(shihabItem.ShihabETag) &&
                            !string.IsNullOrEmpty(mehediProbe.ShihabETagHeader) &&
                            !shihabItem.ShihabETag.StartsWith("W/", StringComparison.OrdinalIgnoreCase) &&
                            !mehediProbe.ShihabETagHeader.StartsWith("W/", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(shihabItem.ShihabETag, mehediProbe.ShihabETagHeader, StringComparison.Ordinal);

                        bool lastModMismatch = sameValidatorHost &&
                            string.IsNullOrEmpty(shihabItem.ShihabETag) &&
                            !string.IsNullOrEmpty(shihabItem.ShihabServerLastModified) &&
                            !string.IsNullOrEmpty(mehediProbe.MehediLastModifiedHeader) &&
                            !string.Equals(shihabItem.ShihabServerLastModified, mehediProbe.MehediLastModifiedHeader, StringComparison.Ordinal);

                        bool lengthMismatch = shihabItem.ShihabTotalBytes > 0 &&
                            mehediProbe.ShihabContentLength > 0 &&
                            shihabItem.ShihabTotalBytes != mehediProbe.ShihabContentLength;

                        if (strongEtagMismatch || lastModMismatch || lengthMismatch)
                        {
                            EmitShihabLog(string.Format("[Validator] Remote representation changed on {0}; resetting segments to prevent corruption.", probeHost));
                            lock (shihabSegmentLock)
                            {
                                lock (shihabItem.MehediSegments)
                                {
                                    shihabItem.MehediSegments.Clear();
                                }
                                shihabItem.MehediDownloadedBytes = 0;
                                shihabItem.ShihabTotalBytes = mehediProbe.ShihabContentLength;
                            }
                            if (!string.IsNullOrWhiteSpace(shihabItem.ShihabPartFilePath) && File.Exists(shihabItem.ShihabPartFilePath))
                            {
                                try { File.Delete(shihabItem.ShihabPartFilePath); } catch { }
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(mehediProbe.ShihabETagHeader))
                    {
                        shihabItem.ShihabETag = mehediProbe.ShihabETagHeader;
                        shihabItem.ShihabETagHost = probeHost;
                    }
                    if (string.IsNullOrWhiteSpace(shihabItem.ShihabExpectedChecksum) && !string.IsNullOrWhiteSpace(mehediProbe.ShihabDigestSpec))
                    {
                        shihabItem.ShihabExpectedChecksum = mehediProbe.ShihabDigestSpec;
                    }

                    if (shihabItem.ShihabTotalBytes <= 0 && mehediProbe.ShihabContentLength > 0)
                    {
                        shihabItem.ShihabTotalBytes = mehediProbe.ShihabContentLength;
                    }
                    shihabItem.ShihabResumeSupported = mehediProbe.MehediAcceptsRanges && shihabItem.ShihabTotalBytes > 0;
                    if (!string.IsNullOrWhiteSpace(mehediProbe.MehediLastModifiedHeader))
                    {
                        shihabItem.ShihabServerLastModified = mehediProbe.MehediLastModifiedHeader;
                    }
                    if ((shihabItem.ShihabFileName == "download.bin" || shihabItem.ShihabFileName == "shihab_download.bin" || shihabItem.ShihabFileName == "videoplayback" || shihabItem.ShihabFileName == "uc") &&
                        !string.IsNullOrWhiteSpace(mehediProbe.MehediSuggestedFileName))
                    {
                        shihabItem.ShihabFileName = ShihabFormatHelpers.EnsureShihabUniqueFilePath(
                            shihabItem.MehediSaveDirectory,
                            mehediProbe.MehediSuggestedFileName);
                    }
                }
                else if (effectiveUrl.IndexOf(".m3u8", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    shihabItem.MehediIsHlsStream = true;
                    ExecuteMehediHlsStreamDownload(shihabItem, effectiveUrl, mehediItemTempDir, mehediToken, handle);
                    return;
                }

                // Disk Space (128 MB margin) & FAT32 4 GB Preflight Check
                string preflightErr;
                long remainingNeeded = shihabItem.ShihabTotalBytes > 0
                    ? Math.Max(0L, shihabItem.ShihabTotalBytes - shihabItem.MehediDownloadedBytes)
                    : 0L;
                if (!ShihabOsAndDiskSafety.VerifyDiskPreflight(shihabItem.MehediSaveDirectory, remainingNeeded, out preflightErr))
                {
                    throw new IOException(preflightErr);
                }

                // Multi-connection FTP / FTPS transfer
                if (effectiveUrl.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                    effectiveUrl.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase) ||
                    effectiveUrl.StartsWith("ftpes://", StringComparison.OrdinalIgnoreCase))
                {
                    ExecuteMehediFtpDownload(shihabItem, mehediItemTempDir, mehediToken, handle);
                    return;
                }

                bool mehediHasCompanionAudio = !string.IsNullOrWhiteSpace(shihabItem.MehediAudioCompanionUrl);
                string shihabPrimaryAssembledPath = mehediHasCompanionAudio
                    ? Path.Combine(mehediItemTempDir, "primary_video_raw.part")
                    : shihabItem.GetShihabFullTargetPath();
                string sharedSparsePartPath = shihabPrimaryAssembledPath + ".mhs.part";
                shihabItem.ShihabPartFilePath = sharedSparsePartPath;
                bool mehediShaAlreadyComputed = false;

                if (mehediIsGvUrl && !IsDirectMobileShihabGoogleVideoUrl(effectiveUrl))
                {
                    DownloadMehediGoogleVideoOrRelay(shihabItem, effectiveUrl, shihabPrimaryAssembledPath, shihabItem.ShihabTotalBytes, true, mehediToken);
                }
                else
                {
                    PrepareInitialShihabSegments(shihabItem, mehediItemTempDir, sharedSparsePartPath);
                    PersistShihabState();

                    shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabDownloading, "Receiving data...");
                    NotifyShihabItemChanged(shihabItem);

                    Stopwatch mehediRateWatch = Stopwatch.StartNew();
                    long shihabLastBytesSnapshot = CalculateMehediDownloadedSum(shihabItem, shihabSegmentLock);
                    long mehediLastRateTickMs = 0;

                    List<Task> shihabActiveTasks = new List<Task>();
                    List<SegmentWorkerTelemetry> activeTelemetries = new List<SegmentWorkerTelemetry>();
                    int mehediNextSegmentId;
                    lock (shihabSegmentLock)
                    {
                        mehediNextSegmentId = shihabItem.MehediSegments.Count;
                    }
                    int maxDynamicSegments = _mehediConfig.GetMaxAllowedConnectionsCap() * 2;
                    bool pausedByQuotaMidTransfer = false;

                    Action<MehediSegmentChunk> launchShihabWorker = null;
                    launchShihabWorker = delegate(MehediSegmentChunk mehediChunk)
                    {
                        CancellationTokenSource workerCts = CancellationTokenSource.CreateLinkedTokenSource(mehediToken);
                        SegmentWorkerTelemetry telemetry = new SegmentWorkerTelemetry();
                        telemetry.Chunk = mehediChunk;
                        telemetry.WorkerCts = workerCts;
                        telemetry.StartedMs = mehediRateWatch.ElapsedMilliseconds;

                        Task shihabWorkerTask = Task.Run(delegate
                        {
                            try
                            {
                                DownloadSingleMehediSegment(shihabItem, mehediChunk, sharedSparsePartPath, telemetry, shihabSegmentLock, workerCts.Token);

                                if (mehediToken.IsCancellationRequested) return;

                                // If shed for slowness or TTFB stall, immediately relaunch a fresh connection on the remaining range
                                if (telemetry.WasShedOrPreempted && mehediChunk.GetMehediRemainingBytes() > 0)
                                {
                                    lock (shihabSegmentLock)
                                    {
                                        mehediChunk.ShihabIsActiveWorker = true;
                                        mehediChunk.ShihabSegmentState = "Reconnecting faster worker...";
                                        launchShihabWorker(mehediChunk);
                                    }
                                    return;
                                }

                                if (shihabItem.ShihabResumeSupported && shihabItem.ShihabTotalBytes > 0)
                                {
                                    MehediSegmentChunk shihabSplitHalf = null;
                                    string splitLog = null;
                                    lock (shihabSegmentLock)
                                    {
                                        if (mehediChunk.GetMehediRemainingBytes() == 0 && shihabItem.MehediSegments.Count < maxDynamicSegments)
                                        {
                                            MehediSegmentChunk mehediLargestActive = null;
                                            long shihabLargestRem = 0;
                                            for (int mehediIdx = 0; mehediIdx < shihabItem.MehediSegments.Count; mehediIdx++)
                                            {
                                                MehediSegmentChunk shihabCand = shihabItem.MehediSegments[mehediIdx];
                                                if (shihabCand.ShihabIsActiveWorker)
                                                {
                                                    long mehediRem = shihabCand.GetMehediRemainingBytes();
                                                    if (mehediRem > shihabLargestRem)
                                                    {
                                                        shihabLargestRem = mehediRem;
                                                        mehediLargestActive = shihabCand;
                                                    }
                                                }
                                            }

                                            // 4 KiB (4096-byte) OS Page-Aligned Dynamic Segment Halving
                                            if (mehediLargestActive != null && shihabLargestRem >= 524288L)
                                            {
                                                long shihabCurrentCursor = mehediLargestActive.MehediStartByte + mehediLargestActive.MehediDownloadedBytes;
                                                long mehediOldEnd = mehediLargestActive.ShihabEndByte;
                                                long rawMidpoint = shihabCurrentCursor + ((mehediOldEnd - shihabCurrentCursor + 1L) / 2L);
                                                long shihabMidpoint = (rawMidpoint + 4095L) & ~4095L;
                                                if (shihabMidpoint <= shihabCurrentCursor || shihabMidpoint > mehediOldEnd)
                                                {
                                                    shihabMidpoint = rawMidpoint;
                                                }
                                                if (shihabMidpoint > shihabCurrentCursor + 131072L && mehediOldEnd >= shihabMidpoint)
                                                {
                                                    mehediLargestActive.ShihabEndByte = shihabMidpoint - 1L;
                                                    mehediNextSegmentId++;
                                                    shihabSplitHalf = new MehediSegmentChunk
                                                    {
                                                        ShihabSegmentIndex = mehediNextSegmentId,
                                                        MehediStartByte = shihabMidpoint,
                                                        ShihabEndByte = mehediOldEnd,
                                                        MehediDownloadedBytes = 0,
                                                        ShihabSegmentState = "Dynamic split connecting...",
                                                        MehediPartFilePath = sharedSparsePartPath,
                                                        ShihabIsActiveWorker = true
                                                    };
                                                    lock (shihabItem.MehediSegments)
                                                    {
                                                        int mehediInsertPos = shihabItem.MehediSegments.IndexOf(mehediLargestActive) + 1;
                                                        shihabItem.MehediSegments.Insert(mehediInsertPos, shihabSplitHalf);
                                                    }
                                                    splitLog = string.Format("[DynamicSplit] {0}: 4KiB-aligned split seg #{1} at byte {2} -> new seg #{3}",
                                                        shihabItem.ShihabFileName,
                                                        mehediLargestActive.ShihabSegmentIndex,
                                                        shihabMidpoint,
                                                        shihabSplitHalf.ShihabSegmentIndex);
                                                }
                                            }
                                        }
                                    }

                                    if (splitLog != null) EmitShihabLog(splitLog);
                                    if (shihabSplitHalf != null)
                                    {
                                        lock (shihabSegmentLock)
                                        {
                                            launchShihabWorker(shihabSplitHalf);
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                lock (shihabSegmentLock)
                                {
                                    activeTelemetries.Remove(telemetry);
                                }
                                try { workerCts.Dispose(); } catch { }
                            }
                        });

                        lock (shihabSegmentLock)
                        {
                            activeTelemetries.Add(telemetry);
                            shihabActiveTasks.Add(shihabWorkerTask);
                        }
                    };

                    lock (shihabSegmentLock)
                    {
                        List<MehediSegmentChunk> mehediInitialList = new List<MehediSegmentChunk>(shihabItem.MehediSegments);
                        for (int shihabI = 0; shihabI < mehediInitialList.Count; shihabI++)
                        {
                            MehediSegmentChunk mehediSeg = mehediInitialList[shihabI];
                            if (mehediSeg.ShihabEndByte < 0 || mehediSeg.GetMehediRemainingBytes() > 0)
                            {
                                mehediSeg.ShihabIsActiveWorker = true;
                                launchShihabWorker(mehediSeg);
                            }
                            else
                            {
                                mehediSeg.ShihabSegmentState = "Completed.";
                            }
                        }
                    }

                    while (!mehediToken.IsCancellationRequested)
                    {
                        if (_mehediConfig.IsQuotaExceeded())
                        {
                            pausedByQuotaMidTransfer = true;
                            ReleaseActiveHandleIfCurrent(shihabItem.ShihabId, handle);
                            try { handle.Cts.Cancel(); } catch { }
                            UpdateSleepStateForActiveDownloads();
                            shihabItem.ShihabCurrentSpeedBps = 0;
                            shihabItem.MehediEtaSeconds = -1;
                            shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabPaused, "Paused (Quota limit reached)");
                            PersistShihabState();
                            NotifyShihabItemChanged(shihabItem);
                            return;
                        }

                        long mehediNowMs = mehediRateWatch.ElapsedMilliseconds;
                        Task[] mehediSnapshotTasks;
                        string shedLog = null;
                        lock (shihabSegmentLock)
                        {
                            // TTFB Stall Preemption (> 3000 ms on final active segment) & Slow-Connection Shedding (< 1/4 peak for >= 4000 ms)
                            if (shihabItem.ShihabResumeSupported && activeTelemetries.Count > 0)
                            {
                                double peakWorkerBps = 0.0;
                                for (int i = 0; i < activeTelemetries.Count; i++)
                                {
                                    if (activeTelemetries[i].SmoothedBps > peakWorkerBps)
                                    {
                                        peakWorkerBps = activeTelemetries[i].SmoothedBps;
                                    }
                                }

                                if (activeTelemetries.Count == 1)
                                {
                                    SegmentWorkerTelemetry sole = activeTelemetries[0];
                                    if (!sole.FirstByteReceived &&
                                        !sole.WasShedOrPreempted &&
                                        (mehediNowMs - sole.StartedMs) >= 3000L &&
                                        sole.Chunk.GetMehediRemainingBytes() >= 262144L)
                                    {
                                        sole.WasShedOrPreempted = true;
                                        shedLog = string.Format("[TTFBPreempt] Preempting stalled final segment #{0} after {1} ms",
                                            sole.Chunk.ShihabSegmentIndex, mehediNowMs - sole.StartedMs);
                                        try { sole.WorkerCts.Cancel(); } catch { }
                                    }
                                }
                                else if (_mehediConfig.ShihabShedSlowConnections && peakWorkerBps > 65536.0)
                                {
                                    double slowThreshold = peakWorkerBps * 0.25;
                                    for (int i = 0; i < activeTelemetries.Count; i++)
                                    {
                                        SegmentWorkerTelemetry wt = activeTelemetries[i];
                                        if (wt.WasShedOrPreempted || !wt.FirstByteReceived || wt.Chunk.GetMehediRemainingBytes() < 262144L)
                                        {
                                            continue;
                                        }
                                        if (wt.SmoothedBps > 0 && wt.SmoothedBps < slowThreshold)
                                        {
                                            if (wt.SlowSinceMs <= 0) wt.SlowSinceMs = mehediNowMs;
                                            else if ((mehediNowMs - wt.SlowSinceMs) >= 4000L)
                                            {
                                                wt.WasShedOrPreempted = true;
                                                shedLog = string.Format("[SlowShed] Recycling slow worker on seg #{0} ({1} < 25% of peak {2})",
                                                    wt.Chunk.ShihabSegmentIndex,
                                                    ShihabFormatHelpers.FormatShihabSpeed(wt.SmoothedBps),
                                                    ShihabFormatHelpers.FormatShihabSpeed(peakWorkerBps));
                                                try { wt.WorkerCts.Cancel(); } catch { }
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            wt.SlowSinceMs = 0;
                                        }
                                    }
                                }
                            }

                            List<Task> shihabRunning = new List<Task>();
                            for (int mehediT = 0; mehediT < shihabActiveTasks.Count; mehediT++)
                            {
                                if (!shihabActiveTasks[mehediT].IsCompleted)
                                {
                                    shihabRunning.Add(shihabActiveTasks[mehediT]);
                                }
                            }
                            shihabActiveTasks.Clear();
                            shihabActiveTasks.AddRange(shihabRunning);
                            mehediSnapshotTasks = shihabRunning.ToArray();
                        }

                        if (shedLog != null) EmitShihabLog(shedLog);

                        long shihabNowBytes = CalculateMehediDownloadedSum(shihabItem, shihabSegmentLock);
                        shihabItem.MehediDownloadedBytes = shihabNowBytes;
                        long shihabDeltaMs = mehediNowMs - mehediLastRateTickMs;
                        if (shihabDeltaMs >= 250)
                        {
                            double mehediInstBps = ((shihabNowBytes - shihabLastBytesSnapshot) * 1000.0) / Math.Max(1.0, (double)shihabDeltaMs);
                            shihabItem.ShihabCurrentSpeedBps = (shihabItem.ShihabCurrentSpeedBps <= 1.0)
                                ? mehediInstBps
                                : (shihabItem.ShihabCurrentSpeedBps * 0.65) + (mehediInstBps * 0.35);

                            if (shihabItem.ShihabTotalBytes > shihabNowBytes && shihabItem.ShihabCurrentSpeedBps > 10.0)
                            {
                                shihabItem.MehediEtaSeconds = (long)((shihabItem.ShihabTotalBytes - shihabNowBytes) / shihabItem.ShihabCurrentSpeedBps);
                            }
                            else
                            {
                                shihabItem.MehediEtaSeconds = -1;
                            }

                            shihabLastBytesSnapshot = shihabNowBytes;
                            mehediLastRateTickMs = mehediNowMs;
                            shihabItem.ShihabStatusText = string.Format("{0:0.0}%", shihabItem.GetMehediPercent());
                            NotifyShihabItemChanged(shihabItem);
                        }

                        if (mehediSnapshotTasks.Length == 0)
                        {
                            lock (shihabSegmentLock)
                            {
                                bool anyStillRunning = false;
                                for (int t = 0; t < shihabActiveTasks.Count; t++)
                                {
                                    if (!shihabActiveTasks[t].IsCompleted) { anyStillRunning = true; break; }
                                }
                                if (!anyStillRunning) break;
                            }
                        }
                        WaitSleepOrCancel(150, mehediToken);
                    }

                    if (mehediToken.IsCancellationRequested || pausedByQuotaMidTransfer)
                    {
                        return;
                    }

                    shihabItem.MehediDownloadedBytes = CalculateMehediDownloadedSum(shihabItem, shihabSegmentLock);

                    bool mehediAllDone = true;
                    string shihabFailedReason = "";
                    lock (shihabSegmentLock)
                    {
                        for (int mehediI = 0; mehediI < shihabItem.MehediSegments.Count; mehediI++)
                        {
                            MehediSegmentChunk shihabSeg = shihabItem.MehediSegments[mehediI];
                            if (shihabSeg.ShihabEndByte >= 0 && shihabSeg.GetMehediRemainingBytes() > 0)
                            {
                                mehediAllDone = false;
                                shihabFailedReason = shihabSeg.ShihabSegmentState;
                                break;
                            }
                            if (shihabSeg.ShihabEndByte < 0 && shihabSeg.ShihabSegmentState != "Completed.")
                            {
                                mehediAllDone = false;
                                shihabFailedReason = shihabSeg.ShihabSegmentState;
                                break;
                            }
                        }
                    }

                    if (!mehediAllDone)
                    {
                        if (IsRealShihabGoogleVideoDomain(effectiveUrl) &&
                            TryHealOrResolveShihabYouTubeItem(shihabItem, mehediItemTempDir, true))
                        {
                            EmitShihabLog("[YouTubeAutoHeal] Refreshed expired/403 YouTube stream URL for " + shihabItem.ShihabFileName);
                            mehediHasCompanionAudio = !string.IsNullOrWhiteSpace(shihabItem.MehediAudioCompanionUrl);
                            shihabPrimaryAssembledPath = mehediHasCompanionAudio
                                ? Path.Combine(mehediItemTempDir, "primary_video_raw.part")
                                : shihabItem.GetShihabFullTargetPath();
                            DownloadDirectShihabStreamToFile(shihabItem, shihabItem.MehediUrl, shihabPrimaryAssembledPath, shihabItem.ShihabTotalBytes, true, mehediToken);
                        }
                        else
                        {
                            bool shihabCanTryBrowserRelay =
                                !IsDirectMobileShihabGoogleVideoUrl(effectiveUrl) &&
                                ((shihabFailedReason != null && (
                                    shihabFailedReason.IndexOf("403", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    shihabFailedReason.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    shihabFailedReason.IndexOf("Forbidden", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    shihabFailedReason.IndexOf("Unauthorized", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                                shihabItem.ShihabSourceTabId >= 0);

                            if (shihabCanTryBrowserRelay)
                            {
                                EmitShihabLog("[BrowserRelayFallback] Direct HTTP rejected (" + shihabFailedReason + "), requesting Browser Relay for " + shihabItem.ShihabFileName);
                                try
                                {
                                    DownloadViaShihabBrowserRelay(shihabItem, effectiveUrl, shihabPrimaryAssembledPath, shihabItem.ShihabTotalBytes, true, mehediToken);
                                }
                                catch (Exception mehediRelayEx)
                                {
                                    if (mehediToken.IsCancellationRequested) return;
                                    throw new IOException(string.IsNullOrEmpty(shihabFailedReason)
                                        ? mehediRelayEx.Message
                                        : (shihabFailedReason + " (" + mehediRelayEx.Message + ")"));
                                }
                            }
                            else
                            {
                                throw new IOException(string.IsNullOrEmpty(shihabFailedReason) ? "One or more segments interrupted." : shihabFailedReason);
                            }
                        }
                    }
                    else
                    {
                        shihabItem.ShihabCurrentSpeedBps = 0;
                        shihabItem.MehediEtaSeconds = 0;
                        shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabRebuilding, "Finalizing...");
                        NotifyShihabItemChanged(shihabItem);

                        // Zero-copy single-file sparse .mhs.part finalization
                        if (File.Exists(sharedSparsePartPath))
                        {
                            if (shihabItem.ShihabTotalBytes > 0)
                            {
                                using (FileStream trimFs = new FileStream(sharedSparsePartPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
                                {
                                    if (trimFs.Length != shihabItem.ShihabTotalBytes)
                                    {
                                        trimFs.SetLength(shihabItem.ShihabTotalBytes);
                                    }
                                    trimFs.Flush(true);
                                }
                            }
                            if (!mehediHasCompanionAudio)
                            {
                                ComputeShihabFileSha256AndSize(shihabItem, sharedSparsePartPath);
                                mehediShaAlreadyComputed = true;
                            }
                            if (!TryAtomicMoveReplace(sharedSparsePartPath, shihabPrimaryAssembledPath))
                            {
                                throw new IOException("Unable to finalize .mhs.part into target destination: " + shihabPrimaryAssembledPath);
                            }
                        }
                        else
                        {
                            RebuildMehediSegmentsToFinalFile(shihabItem, shihabSegmentLock, shihabPrimaryAssembledPath, !mehediHasCompanionAudio);
                            mehediShaAlreadyComputed = !mehediHasCompanionAudio;
                        }
                    }
                }

                if (mehediToken.IsCancellationRequested)
                {
                    return;
                }

                FinalizeShihabDownloadedFileAndOptionalAudioMux(
                    shihabItem,
                    shihabPrimaryAssembledPath,
                    mehediItemTempDir,
                    mehediShaAlreadyComputed,
                    mehediToken);

                try
                {
                    if (Directory.Exists(mehediItemTempDir))
                    {
                        Directory.Delete(mehediItemTempDir, true);
                    }
                }
                catch { }

                CompleteActiveDownload(shihabItem, handle, "Complete");
            }
            catch (Exception shihabEx)
            {
                ReleaseActiveHandleIfCurrent(shihabItem.ShihabId, handle);
                UpdateSleepStateForActiveDownloads();
                if (!mehediToken.IsCancellationRequested)
                {
                    shihabItem.ShihabCurrentSpeedBps = 0;
                    shihabItem.MehediEtaSeconds = -1;
                    shihabItem.ShihabRetryCount++;
                    int baseInterval = (_mehediConfig != null && _mehediConfig.ShihabAutoRetryIntervalSec > 0)
                        ? _mehediConfig.ShihabAutoRetryIntervalSec
                        : 10;
                    int expShift = Math.Min(4, Math.Max(0, shihabItem.ShihabRetryCount - 1));
                    int backoffSec = Math.Min(300, baseInterval * (1 << expShift));
                    lock (_shihabEngineLock)
                    {
                        _nextRetryAfterUtc[shihabItem.ShihabId] = DateTime.UtcNow.AddSeconds(backoffSec);
                    }

                    shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabError, "Error: " + shihabEx.Message);
                    PersistShihabState();
                    NotifyShihabItemChanged(shihabItem);
                    EmitShihabLog("[Error] " + shihabItem.ShihabFileName + " -> " + shihabEx.Message);
                    if (OnShihabItemFailed != null)
                    {
                        try { OnShihabItemFailed(shihabItem); } catch { }
                    }
                }
            }
            finally
            {
                ReleaseActiveHandleIfCurrent(shihabItem.ShihabId, handle);
                UpdateSleepStateForActiveDownloads();
                try { if (handle != null && handle.Cts != null) handle.Cts.Dispose(); } catch { }
            }
        }

        public void PersistShihabState()
        {
            lock (_shihabEngineLock)
            {
                _shihabStore.SaveShihabDownloads(_shihabDownloads);
            }
        }

        private void NotifyShihabItemChanged(ShihabDownloadItem mehediItem)
        {
            if (OnShihabItemUpdated != null)
            {
                OnShihabItemUpdated(mehediItem);
            }
        }

        private void EmitShihabLog(string mehediText)
        {
            if (OnShihabEngineLog != null)
            {
                OnShihabEngineLog(mehediText);
            }
        }
    }
}

