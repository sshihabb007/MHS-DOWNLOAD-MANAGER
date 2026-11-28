using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public partial class MehediDynamicSegmentEngine
    {
        private int _periodicSyncProbeInFlight = 0;

        private void InitializePeriodicSyncTimestampsOnStartup()
        {
            if (_mehediConfig == null || _mehediConfig.MehediQueues == null) return;
            DateTime nowUtc = DateTime.UtcNow;
            for (int i = 0; i < _mehediConfig.MehediQueues.Count; i++)
            {
                MehediQueueConfig q = _mehediConfig.MehediQueues[i];
                if (q != null &&
                    q.MehediEnablePeriodicSync &&
                    q.ShihabLastPeriodicSyncUtc == DateTime.MinValue &&
                    !HasEligiblePeriodicSyncItems(q.ShihabQueueName))
                {
                    q.ShihabLastPeriodicSyncUtc = nowUtc;
                }
            }
        }

        private bool HasEligiblePeriodicSyncItems(string queueName)
        {
            string targetQueue = ResolveCanonicalQueueName(queueName);
            lock (_shihabEngineLock)
            {
                for (int i = 0; i < _shihabDownloads.Count; i++)
                {
                    ShihabDownloadItem item = _shihabDownloads[i];
                    if (item == null || string.IsNullOrWhiteSpace(item.MehediQueueName)) continue;
                    if (!string.Equals(ResolveCanonicalQueueName(item.MehediQueueName), targetQueue, StringComparison.OrdinalIgnoreCase)) continue;
                    if (item.GetShihabStatusEnum() != ShihabDownloadStatus.ShihabCompleted) continue;
                    if (item.ShihabIsTorrent || item.ShihabIsYtDlpJob || item.ShihabIsGalleryJob || string.IsNullOrWhiteSpace(item.MehediUrl)) continue;
                    return true;
                }
            }
            return false;
        }

        public static string ResolveCanonicalQueueName(string queueName)
        {
            if (string.IsNullOrWhiteSpace(queueName)) return "Main Download Queue";
            string trimmed = queueName.Trim();
            if (string.Equals(trimmed, "Main Queue", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "Main", StringComparison.OrdinalIgnoreCase))
            {
                return "Main Download Queue";
            }
            if (string.Equals(trimmed, "Sync Queue", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "Sync", StringComparison.OrdinalIgnoreCase))
            {
                return "Synchronization Queue";
            }
            return trimmed;
        }

        public void SyncNextScheduledWakeTimer()
        {
            if (_mehediConfig == null || _mehediConfig.MehediQueues == null) return;
            DateTime now = DateTime.Now;
            DateTime earliestWake = DateTime.MaxValue;

            for (int qIdx = 0; qIdx < _mehediConfig.MehediQueues.Count; qIdx++)
            {
                MehediQueueConfig q = _mehediConfig.MehediQueues[qIdx];
                if (q == null || !q.MehediEnableScheduledStart || string.IsNullOrWhiteSpace(q.ShihabStartTimeHhMm)) continue;

                string[] parts = q.ShihabStartTimeHhMm.Trim().Split(':');
                int hh, mm;
                if (parts.Length < 2 || !int.TryParse(parts[0], out hh) || !int.TryParse(parts[1], out mm)) continue;
                if (hh < 0 || hh > 23 || mm < 0 || mm > 59) continue;

                if (!string.IsNullOrWhiteSpace(q.ShihabOnceAtDateYyyyMmDd))
                {
                    DateTime onceDay;
                    if (DateTime.TryParseExact(q.ShihabOnceAtDateYyyyMmDd.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out onceDay))
                    {
                        DateTime candidate = new DateTime(onceDay.Year, onceDay.Month, onceDay.Day, hh, mm, 0, DateTimeKind.Local);
                        if (candidate > now && candidate < earliestWake)
                        {
                            earliestWake = candidate;
                        }
                    }
                    continue;
                }

                for (int dayOffset = 0; dayOffset <= 7; dayOffset++)
                {
                    DateTime dayDate = now.Date.AddDays(dayOffset);
                    string dayShort = dayDate.ToString("ddd", CultureInfo.InvariantCulture);
                    bool dayAllowed = string.IsNullOrEmpty(q.MehediActiveDaysCsv) ||
                                      q.MehediActiveDaysCsv.IndexOf(dayShort, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!dayAllowed) continue;
                    DateTime candidate = new DateTime(dayDate.Year, dayDate.Month, dayDate.Day, hh, mm, 0, DateTimeKind.Local);
                    if (candidate > now)
                    {
                        if (candidate < earliestWake) earliestWake = candidate;
                        break;
                    }
                }
            }

            if (earliestWake < DateTime.MaxValue)
            {
                ShihabOsAndDiskSafety.ScheduleWakeTimer(earliestWake);
            }
            else
            {
                ShihabOsAndDiskSafety.ScheduleWakeTimer(DateTime.MinValue);
            }
        }

        public void TickMehediQueuesAndScheduler()
        {
            if (_mehediConfig == null || _mehediConfig.MehediQueues == null) return;
            DateTime shihabNow = DateTime.Now;
            string mehediCurrentHhMm = shihabNow.ToString("HH:mm");
            string currentYyyyMmDd = shihabNow.ToString("yyyy-MM-dd");
            string shihabDayShort = shihabNow.ToString("ddd", CultureInfo.InvariantCulture);
            bool configMutated = false;
            bool anyScheduledStartFired = false;

            for (int mehediQ = 0; mehediQ < _mehediConfig.MehediQueues.Count; mehediQ++)
            {
                MehediQueueConfig shihabQueue = _mehediConfig.MehediQueues[mehediQ];
                if (shihabQueue == null) continue;

                bool hasOnceDate = !string.IsNullOrWhiteSpace(shihabQueue.ShihabOnceAtDateYyyyMmDd);
                bool onceDateMatches = hasOnceDate && string.Equals(shihabQueue.ShihabOnceAtDateYyyyMmDd.Trim(), currentYyyyMmDd, StringComparison.OrdinalIgnoreCase);
                bool mehediDayMatches = hasOnceDate
                    ? onceDateMatches
                    : (string.IsNullOrEmpty(shihabQueue.MehediActiveDaysCsv) ||
                       shihabQueue.MehediActiveDaysCsv.IndexOf(shihabDayShort, StringComparison.OrdinalIgnoreCase) >= 0);

                if (shihabQueue.MehediEnableScheduledStart && mehediDayMatches &&
                    string.Equals((shihabQueue.ShihabStartTimeHhMm ?? "").Trim(), mehediCurrentHhMm, StringComparison.Ordinal) &&
                    shihabQueue.ShihabLastAutoStartMinute != mehediCurrentHhMm)
                {
                    shihabQueue.ShihabLastAutoStartMinute = mehediCurrentHhMm;
                    anyScheduledStartFired = true;
                    if (onceDateMatches)
                    {
                        shihabQueue.MehediEnableScheduledStart = false;
                        shihabQueue.ShihabOnceAtDateYyyyMmDd = "";
                        configMutated = true;
                    }
                    StartShihabQueue(shihabQueue.ShihabQueueName);
                }

                if (shihabQueue.MehediEnableScheduledStop && mehediDayMatches &&
                    string.Equals((shihabQueue.ShihabStopTimeHhMm ?? "").Trim(), mehediCurrentHhMm, StringComparison.Ordinal) &&
                    shihabQueue.MehediLastAutoStopMinute != mehediCurrentHhMm)
                {
                    shihabQueue.MehediLastAutoStopMinute = mehediCurrentHhMm;
                    StopShihabQueue(shihabQueue.ShihabQueueName);
                }

                if (shihabQueue.MehediEnablePeriodicSync && shihabQueue.ShihabPeriodicSyncMinutes > 0)
                {
                    int intervalMin = Math.Max(5, shihabQueue.ShihabPeriodicSyncMinutes);
                    if (shihabQueue.ShihabLastPeriodicSyncUtc == DateTime.MinValue ||
                        (DateTime.UtcNow - shihabQueue.ShihabLastPeriodicSyncUtc).TotalMinutes >= intervalMin)
                    {
                        shihabQueue.ShihabLastPeriodicSyncUtc = DateTime.UtcNow;
                        if (HasEligiblePeriodicSyncItems(shihabQueue.ShihabQueueName))
                        {
                            TriggerPeriodicQueueSyncProbeAsync(shihabQueue.ShihabQueueName);
                        }
                    }
                }

                if (shihabQueue.MehediIsRunningNow)
                {
                    PumpRunningMehediQueue(shihabQueue);
                }
            }

            if (configMutated)
            {
                try { _shihabStore.SaveMehediConfig(_mehediConfig); } catch { }
            }
            if (configMutated || anyScheduledStartFired)
            {
                SyncNextScheduledWakeTimer();
            }
        }

        public void StartMehediQueue(string mehediQueueName)
        {
            StartShihabQueue(mehediQueueName);
        }

        public void StartShihabQueue(string mehediQueueName)
        {
            if (_mehediConfig == null || _mehediConfig.MehediQueues == null) return;
            string canonical = ResolveCanonicalQueueName(mehediQueueName);
            bool isMainQueue = string.Equals(canonical, "Main Download Queue", StringComparison.OrdinalIgnoreCase);

            if (isMainQueue)
            {
                bool adoptedAny = false;
                lock (_shihabEngineLock)
                {
                    for (int idx = 0; idx < _shihabDownloads.Count; idx++)
                    {
                        ShihabDownloadItem it = _shihabDownloads[idx];
                        if (it != null &&
                            string.IsNullOrWhiteSpace(it.MehediQueueName) &&
                            it.GetShihabStatusEnum() == ShihabDownloadStatus.ShihabQueued)
                        {
                            it.MehediQueueName = "Main Download Queue";
                            adoptedAny = true;
                        }
                    }
                }
                if (adoptedAny)
                {
                    ScheduleDebouncedDownloadsSave();
                }
            }

            for (int shihabI = 0; shihabI < _mehediConfig.MehediQueues.Count; shihabI++)
            {
                MehediQueueConfig mehediQ = _mehediConfig.MehediQueues[shihabI];
                if (mehediQ != null && string.Equals(mehediQ.ShihabQueueName, canonical, StringComparison.OrdinalIgnoreCase))
                {
                    mehediQ.MehediIsRunningNow = true;
                    mehediQ.MehediHadWorkInCurrentRun = false;
                    mehediQ.MehediQueueRunBaselineBytes = SumQueueDownloadedBytes(canonical);
                    if (mehediQ.MehediEnablePeriodicSync ||
                        string.Equals(mehediQ.ShihabQueueName, "Synchronization Queue", StringComparison.OrdinalIgnoreCase))
                    {
                        mehediQ.ShihabLastPeriodicSyncUtc = DateTime.UtcNow;
                        TriggerPeriodicQueueSyncProbeAsync(mehediQ.ShihabQueueName);
                    }
                    PumpRunningMehediQueue(mehediQ);
                }
            }
        }

        private long SumQueueDownloadedBytes(string canonicalQueueName)
        {
            long sum = 0L;
            List<ShihabDownloadItem> snap = GetShihabDownloadsSnapshot();
            for (int i = 0; i < snap.Count; i++)
            {
                ShihabDownloadItem it = snap[i];
                if (it == null || string.IsNullOrWhiteSpace(it.MehediQueueName)) continue;
                if (string.Equals(ResolveCanonicalQueueName(it.MehediQueueName), canonicalQueueName, StringComparison.OrdinalIgnoreCase))
                {
                    sum += Math.Max(0L, it.MehediDownloadedBytes);
                }
            }
            return sum;
        }

        public void StopShihabQueue(string mehediQueueName)
        {
            if (_mehediConfig == null || _mehediConfig.MehediQueues == null) return;
            string canonical = ResolveCanonicalQueueName(mehediQueueName);
            for (int shihabI = 0; shihabI < _mehediConfig.MehediQueues.Count; shihabI++)
            {
                MehediQueueConfig mehediQ = _mehediConfig.MehediQueues[shihabI];
                if (mehediQ != null && string.Equals(mehediQ.ShihabQueueName, canonical, StringComparison.OrdinalIgnoreCase))
                {
                    mehediQ.MehediIsRunningNow = false;
                    mehediQ.MehediHadWorkInCurrentRun = false;
                }
            }
            List<ShihabDownloadItem> shihabSnap = GetShihabDownloadsSnapshot();
            for (int mehediJ = 0; mehediJ < shihabSnap.Count; mehediJ++)
            {
                ShihabDownloadItem shihabIt = shihabSnap[mehediJ];
                if (string.Equals(ResolveCanonicalQueueName(shihabIt.MehediQueueName), canonical, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(shihabIt.MehediQueueName) &&
                    IsMehediItemActive(shihabIt.ShihabId))
                {
                    PauseShihabDownloadInternal(shihabIt, false);
                }
            }
        }

        private void TriggerPeriodicQueueSyncProbeAsync(string queueName)
        {
            if (Interlocked.CompareExchange(ref _periodicSyncProbeInFlight, 1, 0) != 0) return;
            string targetQueue = ResolveCanonicalQueueName(queueName);

            Task.Run(delegate
            {
                try
                {
                    List<ShihabDownloadItem> snap = GetShihabDownloadsSnapshot();
                    for (int i = 0; i < snap.Count; i++)
                    {
                        ShihabDownloadItem item = snap[i];
                        if (item == null || string.IsNullOrWhiteSpace(item.MehediQueueName)) continue;
                        if (!string.Equals(ResolveCanonicalQueueName(item.MehediQueueName), targetQueue, StringComparison.OrdinalIgnoreCase)) continue;
                        if (item.GetShihabStatusEnum() != ShihabDownloadStatus.ShihabCompleted) continue;
                        if (item.ShihabIsTorrent || item.ShihabIsYtDlpJob || item.ShihabIsGalleryJob || string.IsNullOrWhiteSpace(item.MehediUrl)) continue;

                        ShihabProbeMetadata meta = ProbeMehediUrl(item.MehediUrl, item);
                        if (meta == null || !meta.MehediSuccess) continue;

                        bool changed = false;
                        if (!string.IsNullOrWhiteSpace(meta.ShihabETagHeader) &&
                            !string.IsNullOrWhiteSpace(item.ShihabETag) &&
                            !string.Equals(meta.ShihabETagHeader.Trim(), item.ShihabETag.Trim(), StringComparison.Ordinal))
                        {
                            changed = true;
                        }
                        else if (!string.IsNullOrWhiteSpace(meta.MehediLastModifiedHeader) &&
                                 !string.IsNullOrWhiteSpace(item.ShihabServerLastModified) &&
                                 !string.Equals(meta.MehediLastModifiedHeader.Trim(), item.ShihabServerLastModified.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            changed = true;
                        }
                        else if (meta.ShihabContentLength > 0 &&
                                 item.ShihabTotalBytes > 0 &&
                                 meta.ShihabContentLength != item.ShihabTotalBytes)
                        {
                            changed = true;
                        }

                        if (changed)
                        {
                            EmitShihabLog("[SyncQueue] Remote update detected for " + item.ShihabFileName + " — queueing re-download.");
                            object segLock = GetItemSegmentLock(item);
                            lock (segLock)
                            {
                                if (item.MehediSegments != null)
                                {
                                    lock (item.MehediSegments)
                                    {
                                        item.MehediSegments.Clear();
                                    }
                                }
                                item.MehediDownloadedBytes = 0;
                                item.MehediSha256Hex = "";
                                item.ShihabRetryCount = 0;
                                item.SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued (Sync Update)");
                            }
                            PersistShihabState();
                            StartOrResumeMehediDownload(item);
                        }
                    }
                }
                catch { }
                finally
                {
                    Interlocked.Exchange(ref _periodicSyncProbeInFlight, 0);
                }
            });
        }

        private void PumpRunningMehediQueue(MehediQueueConfig shihabQueue)
        {
            if (shihabQueue.ShihabQuotaMegabytes > 0)
            {
                long quotaLimitBytes = (long)shihabQueue.ShihabQuotaMegabytes * 1024L * 1024L;
                long usedInRun = SumQueueDownloadedBytes(shihabQueue.ShihabQueueName) - shihabQueue.MehediQueueRunBaselineBytes;
                if (usedInRun >= quotaLimitBytes)
                {
                    EmitShihabLog(string.Format("[Scheduler] Queue '{0}' reached per-queue quota ({1} MB); pausing queue.", shihabQueue.ShihabQueueName, shihabQueue.ShihabQuotaMegabytes));
                    StopShihabQueue(shihabQueue.ShihabQueueName);
                    return;
                }
            }

            List<ShihabDownloadItem> mehediSnap = GetShihabDownloadsSnapshot();
            int shihabActiveInQueue = 0;
            List<ShihabDownloadItem> mehediWaiting = new List<ShihabDownloadItem>();
            bool hasRetryBackoffWaiting = false;
            DateTime nowUtc = DateTime.UtcNow;

            int effectiveMaxRetries = shihabQueue.ShihabRetriesPerFile;
            if (_mehediConfig != null && _mehediConfig.MehediMaxRetryAttempts > 0)
            {
                effectiveMaxRetries = Math.Min(effectiveMaxRetries, _mehediConfig.MehediMaxRetryAttempts);
            }

            for (int shihabI = 0; shihabI < mehediSnap.Count; shihabI++)
            {
                ShihabDownloadItem mehediItem = mehediSnap[shihabI];
                if (mehediItem == null || string.IsNullOrWhiteSpace(mehediItem.MehediQueueName)) continue;
                if (!string.Equals(ResolveCanonicalQueueName(mehediItem.MehediQueueName), shihabQueue.ShihabQueueName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (IsMehediItemActive(mehediItem.ShihabId))
                {
                    shihabActiveInQueue++;
                }
                else
                {
                    bool isExplicitlyPaused;
                    DateTime nextRetryUtc;
                    lock (_shihabEngineLock)
                    {
                        isExplicitlyPaused = _explicitlyPausedIds.Contains(mehediItem.ShihabId);
                        if (!_nextRetryAfterUtc.TryGetValue(mehediItem.ShihabId, out nextRetryUtc))
                        {
                            nextRetryUtc = DateTime.MinValue;
                        }
                    }
                    if (isExplicitlyPaused)
                    {
                        continue;
                    }

                    ShihabDownloadStatus shihabSt = mehediItem.GetShihabStatusEnum();
                    if (shihabSt == ShihabDownloadStatus.ShihabQueued ||
                        shihabSt == ShihabDownloadStatus.ShihabPaused)
                    {
                        mehediWaiting.Add(mehediItem);
                    }
                    else if (shihabSt == ShihabDownloadStatus.ShihabError && mehediItem.ShihabRetryCount <= effectiveMaxRetries)
                    {
                        if (nowUtc >= nextRetryUtc)
                        {
                            mehediWaiting.Add(mehediItem);
                        }
                        else
                        {
                            hasRetryBackoffWaiting = true;
                        }
                    }
                }
            }

            if (shihabActiveInQueue > 0 || hasRetryBackoffWaiting)
            {
                shihabQueue.MehediHadWorkInCurrentRun = true;
            }

            int shihabMaxConc = Math.Max(1, shihabQueue.MehediMaxConcurrentFiles);
            int mehediSlots = shihabMaxConc - shihabActiveInQueue;
            for (int shihabK = 0; shihabK < mehediWaiting.Count && shihabK < mehediSlots; shihabK++)
            {
                shihabQueue.MehediHadWorkInCurrentRun = true;
                StartOrResumeMehediDownload(mehediWaiting[shihabK]);
                shihabActiveInQueue++;
            }

            if (shihabActiveInQueue == 0 && mehediWaiting.Count == 0 && !hasRetryBackoffWaiting)
            {
                bool completedRealWork = shihabQueue.MehediHadWorkInCurrentRun;
                shihabQueue.MehediIsRunningNow = false;
                shihabQueue.MehediHadWorkInCurrentRun = false;

                if (!completedRealWork)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(shihabQueue.ShihabRunCommandWhenDone))
                {
                    try
                    {
                        string rawPostCmd = shihabQueue.ShihabRunCommandWhenDone.Trim();
                        bool wasSealed =
                            rawPostCmd.StartsWith("dpapi2:", StringComparison.Ordinal) ||
                            rawPostCmd.StartsWith("aes2:", StringComparison.Ordinal) ||
                            rawPostCmd.StartsWith("aes3:", StringComparison.Ordinal) ||
                            rawPostCmd.StartsWith("dpapi:", StringComparison.Ordinal);
                        string effectiveCmd = wasSealed
                            ? ShihabSecretsVault.UnprotectSecret(rawPostCmd)
                            : rawPostCmd;

                        if (string.IsNullOrWhiteSpace(effectiveCmd) ||
                            (wasSealed && string.Equals(effectiveCmd, rawPostCmd, StringComparison.Ordinal)))
                        {
                            EmitShihabLog("[Scheduler] Skipped post-queue command: protected command could not be unsealed.");
                        }
                        else if (effectiveCmd.IndexOf('\0') >= 0 || effectiveCmd.IndexOf('\r') >= 0 || effectiveCmd.IndexOf('\n') >= 0)
                        {
                            EmitShihabLog("[Scheduler] Blocked post-queue command containing control/newline characters.");
                        }
                        else
                        {
                            string cmdExePath = ShihabProcessAndPathSafety.ResolveSystem32ToolPath("cmd.exe");
                            if (!string.IsNullOrEmpty(cmdExePath) && File.Exists(cmdExePath))
                            {
                                EmitShihabLog("[Scheduler] Running post-queue command: " + (wasSealed ? "<protected command>" : effectiveCmd));
                                ProcessStartInfo psi = new ProcessStartInfo(cmdExePath, "/C " + effectiveCmd)
                                {
                                    UseShellExecute = false,
                                    CreateNoWindow = true
                                };
                                using (Process postCmdProc = Process.Start(psi))
                                {
                                }
                            }
                        }
                    }
                    catch (Exception cmdEx)
                    {
                        EmitShihabLog("[Scheduler] Post-queue command failed: " + cmdEx.Message);
                    }
                }
                bool hasPowerAction =
                    shihabQueue.MehediExitAppWhenQueueFinishes ||
                    (_mehediConfig != null && _mehediConfig.MehediHangUpWhenDone) ||
                    (!string.IsNullOrWhiteSpace(shihabQueue.ShihabSystemPowerActionWhenDone) &&
                     !string.Equals(shihabQueue.ShihabSystemPowerActionWhenDone, "None", StringComparison.OrdinalIgnoreCase));
                if (hasPowerAction && OnQueueFinishedWithActions != null)
                {
                    try { OnQueueFinishedWithActions(shihabQueue); } catch { }
                }
            }
        }
    }
}

