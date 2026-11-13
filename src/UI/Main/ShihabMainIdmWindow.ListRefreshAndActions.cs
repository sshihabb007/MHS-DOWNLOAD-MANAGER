using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public partial class ShihabMainIdmWindow
    {
        private void OnEngineItemUpdated(ShihabDownloadItem unused)
        {
            if (Interlocked.Exchange(ref _engineRefreshPending, 1) == 0)
            {
                SafeShihabUiInvoke(delegate { HandleCoalescedEngineRefreshSignal(); });
            }
        }

        private void HandleCoalescedEngineRefreshSignal()
        {
            if (IsDisposed) return;
            double msSinceLast = (DateTime.UtcNow - _lastGridRefreshUtc).TotalMilliseconds;
            if (msSinceLast >= 80.0)
            {
                Interlocked.Exchange(ref _engineRefreshPending, 0);
                if (_refreshDebounceTimer != null && _refreshDebounceTimer.Enabled)
                {
                    _refreshDebounceTimer.Stop();
                }
                RefreshMehediDownloadsGrid();
            }
            else if (_refreshDebounceTimer != null && !_refreshDebounceTimer.Enabled)
            {
                _refreshDebounceTimer.Start();
            }
        }

        private void PopulateShihabCategoryTree()
        {
            if (_shihabTransfersNav == null) return;
            _shihabTransfersNav.MehediActiveTag = _shihabActiveTreeFilter;
            _shihabHeaderViewTitle = _shihabTransfersNav.GetActiveDisplayTitle();
            _shihabTransfersNav.Invalidate();
            RepositionShihabHeaderControls();
        }

        private MehediQueueConfig FindMainQueueConfig()
        {
            if (_mehediConfig == null || _mehediConfig.MehediQueues == null) return null;
            for (int i = 0; i < _mehediConfig.MehediQueues.Count; i++)
            {
                MehediQueueConfig q = _mehediConfig.MehediQueues[i];
                if (q != null && string.Equals(q.ShihabQueueName, "Main Download Queue", StringComparison.OrdinalIgnoreCase))
                {
                    return q;
                }
            }
            return _mehediConfig.MehediQueues.Count > 0 ? _mehediConfig.MehediQueues[0] : null;
        }

        private int CountQueueItems(List<ShihabDownloadItem> all, string queueName)
        {
            int c = 0;
            for (int i = 0; i < all.Count; i++)
            {
                ShihabDownloadItem it = all[i];
                if (it == null) continue;
                if (!string.IsNullOrWhiteSpace(it.MehediQueueName) &&
                    string.Equals(MehediDynamicSegmentEngine.ResolveCanonicalQueueName(it.MehediQueueName), queueName, StringComparison.OrdinalIgnoreCase))
                {
                    c++;
                }
                else if (string.IsNullOrWhiteSpace(it.MehediQueueName) &&
                         it.GetShihabStatusEnum() == ShihabDownloadStatus.ShihabQueued &&
                         string.Equals(queueName, "Main Download Queue", StringComparison.OrdinalIgnoreCase))
                {
                    c++;
                }
            }
            return c;
        }

        private static bool CategoryMatchesFilterName(string itemCategory, string filterCategory, bool isTorrentItem)
        {
            if (string.Equals(filterCategory, "Torrents", StringComparison.OrdinalIgnoreCase))
            {
                return isTorrentItem || string.Equals(itemCategory, "Torrents", StringComparison.OrdinalIgnoreCase);
            }
            if (string.Equals(filterCategory, "Audio", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filterCategory, "Music", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(itemCategory, "Audio", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(itemCategory, "Music", StringComparison.OrdinalIgnoreCase);
            }
            return string.Equals(itemCategory, filterCategory, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesShihabTreeAndSearch(ShihabDownloadItem mehediItem)
        {
            ShihabDownloadStatus shihabSt = mehediItem.GetShihabStatusEnum();
            bool mehediIsDone = shihabSt == ShihabDownloadStatus.ShihabCompleted;
            bool shihabIsActive = _shihabEngine.IsMehediItemActive(mehediItem.ShihabId) ||
                                  shihabSt == ShihabDownloadStatus.ShihabDownloading ||
                                  shihabSt == ShihabDownloadStatus.ShihabConnecting ||
                                  shihabSt == ShihabDownloadStatus.ShihabRebuilding;

            if (_shihabActiveTreeFilter == "DOWNLOADING" && !shihabIsActive) return false;
            if (_shihabActiveTreeFilter == "PAUSED" && (mehediIsDone || shihabIsActive || shihabSt != ShihabDownloadStatus.ShihabPaused)) return false;
            if (_shihabActiveTreeFilter == "UNFINISHED" && mehediIsDone) return false;
            if (_shihabActiveTreeFilter == "FINISHED" && !mehediIsDone) return false;
            if (_shihabActiveTreeFilter == "GRABBER_PROJECTS" && (mehediItem.ShihabDescription ?? "").IndexOf("Grabber", StringComparison.OrdinalIgnoreCase) < 0) return false;
            if (_shihabActiveTreeFilter == "QUEUES_ALL" && (mehediIsDone || (string.IsNullOrEmpty(mehediItem.MehediQueueName) && shihabSt != ShihabDownloadStatus.ShihabQueued))) return false;

            if (_shihabActiveTreeFilter.StartsWith("CAT:"))
            {
                string shihabC = _shihabActiveTreeFilter.Substring(4);
                if (!CategoryMatchesFilterName(mehediItem.ShihabCategoryName, shihabC, mehediItem.ShihabIsTorrent)) return false;
            }
            else if (_shihabActiveTreeFilter.StartsWith("UNFIN_CAT:"))
            {
                string mehediC = _shihabActiveTreeFilter.Substring(10);
                if (mehediIsDone || !CategoryMatchesFilterName(mehediItem.ShihabCategoryName, mehediC, mehediItem.ShihabIsTorrent)) return false;
            }
            else if (_shihabActiveTreeFilter.StartsWith("FIN_CAT:"))
            {
                string shihabC = _shihabActiveTreeFilter.Substring(8);
                if (!mehediIsDone || !CategoryMatchesFilterName(mehediItem.ShihabCategoryName, shihabC, mehediItem.ShihabIsTorrent)) return false;
            }
            else if (_shihabActiveTreeFilter.StartsWith("QUEUE:"))
            {
                string mehediQ = _shihabActiveTreeFilter.Substring(6);
                if (!string.Equals(mehediItem.MehediQueueName, mehediQ, StringComparison.OrdinalIgnoreCase)) return false;
            }

            string shihabSearch = _mehediSearchBox.Text.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(shihabSearch))
            {
                bool mehediHit =
                    (mehediItem.ShihabFileName ?? "").ToLowerInvariant().Contains(shihabSearch) ||
                    (mehediItem.MehediUrl ?? "").ToLowerInvariant().Contains(shihabSearch) ||
                    (mehediItem.ShihabDescription ?? "").ToLowerInvariant().Contains(shihabSearch) ||
                    (mehediItem.MehediReferer ?? "").ToLowerInvariant().Contains(shihabSearch);
                if (!mehediHit) return false;
            }
            return true;
        }

        private static string ExtractShihabCompactDate(ShihabDownloadItem mehediItem)
        {
            string shihabRaw = !string.IsNullOrWhiteSpace(mehediItem.ShihabDateAdded)
                ? mehediItem.ShihabDateAdded
                : (mehediItem.MehediLastTryDate ?? "");
            shihabRaw = shihabRaw.Trim();
            if (shihabRaw.Length >= 10)
            {
                return shihabRaw.Substring(0, 10);
            }
            return shihabRaw;
        }

        private static string ResolveCleanStatusWord(ShihabDownloadItem shihabItem, bool mehediRowActive)
        {
            ShihabDownloadStatus shihabSt = shihabItem.GetShihabStatusEnum();
            if (shihabSt == ShihabDownloadStatus.ShihabCompleted) return "Completed";
            if (mehediRowActive || shihabSt == ShihabDownloadStatus.ShihabDownloading) return "Downloading";
            if (shihabSt == ShihabDownloadStatus.ShihabConnecting) return "Connecting";
            if (shihabSt == ShihabDownloadStatus.ShihabRebuilding) return "Muxing";
            if (shihabSt == ShihabDownloadStatus.ShihabQueued) return "Scheduled";
            if (shihabSt == ShihabDownloadStatus.ShihabError) return "Error";
            return "Paused";
        }

        private static void UpdateSubItemIfChanged(ListViewItem row, int subIndex, string nextText)
        {
            string normalized = nextText ?? "";
            if (!string.Equals(row.SubItems[subIndex].Text, normalized, StringComparison.Ordinal))
            {
                row.SubItems[subIndex].Text = normalized;
            }
        }

        private void RefreshMehediDownloadsGrid()
        {
            if (IsDisposed) return;
            _lastGridRefreshUtc = DateTime.UtcNow;

            List<ShihabDownloadItem> shihabAll = _shihabEngine.GetShihabDownloadsSnapshot();
            Dictionary<string, int> mehediOrderMap = new Dictionary<string, int>(shihabAll.Count, StringComparer.Ordinal);

            int[] shihabSidebarCounts = new int[11];
            shihabSidebarCounts[0] = shihabAll.Count;

            long mehediSumTotalBytes = 0;
            long shihabSumDownloadedBytes = 0;
            double mehediGlobalSpeedBps = 0.0;
            int shihabActiveSockets = 0;

            for (int shihabI = 0; shihabI < shihabAll.Count; shihabI++)
            {
                ShihabDownloadItem mehediIt = shihabAll[shihabI];
                mehediOrderMap[mehediIt.ShihabId] = shihabI + 1;

                ShihabDownloadStatus shihabSt = mehediIt.GetShihabStatusEnum();
                bool mehediDone = shihabSt == ShihabDownloadStatus.ShihabCompleted;
                bool shihabLive = _shihabEngine.IsMehediItemActive(mehediIt.ShihabId) ||
                                  shihabSt == ShihabDownloadStatus.ShihabDownloading ||
                                  shihabSt == ShihabDownloadStatus.ShihabConnecting ||
                                  shihabSt == ShihabDownloadStatus.ShihabRebuilding;

                if (shihabLive) shihabSidebarCounts[1]++;
                if (mehediDone) shihabSidebarCounts[2]++;
                if (!mehediDone && !shihabLive && shihabSt == ShihabDownloadStatus.ShihabPaused) shihabSidebarCounts[3]++;
                if (!mehediDone && (!string.IsNullOrEmpty(mehediIt.MehediQueueName) || shihabSt == ShihabDownloadStatus.ShihabQueued)) shihabSidebarCounts[4]++;

                string mehediCat = mehediIt.ShihabCategoryName ?? "";
                if (string.Equals(mehediCat, "Video", StringComparison.OrdinalIgnoreCase)) shihabSidebarCounts[5]++;
                else if (string.Equals(mehediCat, "Audio", StringComparison.OrdinalIgnoreCase) || string.Equals(mehediCat, "Music", StringComparison.OrdinalIgnoreCase)) shihabSidebarCounts[6]++;
                else if (string.Equals(mehediCat, "Documents", StringComparison.OrdinalIgnoreCase)) shihabSidebarCounts[7]++;
                else if (string.Equals(mehediCat, "Compressed", StringComparison.OrdinalIgnoreCase)) shihabSidebarCounts[8]++;
                else if (string.Equals(mehediCat, "Programs", StringComparison.OrdinalIgnoreCase)) shihabSidebarCounts[9]++;
                else if (string.Equals(mehediCat, "Torrents", StringComparison.OrdinalIgnoreCase) || mehediIt.ShihabIsTorrent) shihabSidebarCounts[10]++;

                long shihabKnownTot = mehediIt.ShihabTotalBytes > 0 ? mehediIt.ShihabTotalBytes : Math.Max(0L, mehediIt.MehediDownloadedBytes);
                long mehediKnownDown = mehediDone ? shihabKnownTot : Math.Max(0L, mehediIt.MehediDownloadedBytes);
                mehediSumTotalBytes += shihabKnownTot;
                shihabSumDownloadedBytes += mehediKnownDown;

                if (shihabLive)
                {
                    mehediGlobalSpeedBps += mehediIt.ShihabCurrentSpeedBps;
                    int shihabItemWorkers = 0;
                    List<MehediSegmentChunk> segSnap = _shihabEngine.GetItemSegmentsSnapshot(mehediIt);
                    if (segSnap != null)
                    {
                        for (int mehediSegIdx = 0; mehediSegIdx < segSnap.Count; mehediSegIdx++)
                        {
                            MehediSegmentChunk shihabChunk = segSnap[mehediSegIdx];
                            if (shihabChunk != null && shihabChunk.ShihabIsActiveWorker)
                            {
                                shihabItemWorkers++;
                            }
                        }
                    }
                    shihabActiveSockets += Math.Max(1, shihabItemWorkers);
                }
            }

            if (_shihabTransfersNav != null)
            {
                _shihabTransfersNav.UpdateShihabCounts(shihabSidebarCounts);
            }

            List<ShihabDownloadItem> mehediFiltered = new List<ShihabDownloadItem>();
            for (int shihabI = 0; shihabI < shihabAll.Count; shihabI++)
            {
                if (MatchesShihabTreeAndSearch(shihabAll[shihabI]))
                {
                    mehediFiltered.Add(shihabAll[shihabI]);
                }
            }

            if (_mehediSortColumnKey == "ORDER")
            {
                if (!_shihabSortAscending)
                {
                    mehediFiltered.Reverse();
                }
            }
            else
            {
                int mehediDir = _shihabSortAscending ? 1 : -1;
                mehediFiltered.Sort(delegate(ShihabDownloadItem shihabA, ShihabDownloadItem mehediB)
                {
                    if (_mehediSortColumnKey == "NAME") return mehediDir * string.Compare(shihabA.ShihabFileName, mehediB.ShihabFileName, StringComparison.OrdinalIgnoreCase);
                    if (_mehediSortColumnKey == "SIZE") return mehediDir * shihabA.ShihabTotalBytes.CompareTo(mehediB.ShihabTotalBytes);
                    if (_mehediSortColumnKey == "PROGRESS") return mehediDir * shihabA.GetMehediPercent().CompareTo(mehediB.GetMehediPercent());
                    if (_mehediSortColumnKey == "STATUS") return mehediDir * string.Compare(shihabA.ShihabStatusText, mehediB.ShihabStatusText, StringComparison.OrdinalIgnoreCase);
                    if (_mehediSortColumnKey == "SPEED") return mehediDir * shihabA.ShihabCurrentSpeedBps.CompareTo(mehediB.ShihabCurrentSpeedBps);
                    return 0;
                });
            }

            string shihabNewBadgeTxt = mehediFiltered.Count == 1 ? "1 item" : string.Format("{0} items", mehediFiltered.Count);
            if (!string.Equals(_mehediHeaderItemsBadgeText, shihabNewBadgeTxt, StringComparison.Ordinal))
            {
                _mehediHeaderItemsBadgeText = shihabNewBadgeTxt;
                RepositionShihabHeaderControls();
            }

            HashSet<string> shihabSelectedIds = new HashSet<string>();
            foreach (ListViewItem mehediSel in _shihabDownloadsListView.SelectedItems)
            {
                ShihabDownloadItem shihabTag = mehediSel.Tag as ShihabDownloadItem;
                if (shihabTag != null) shihabSelectedIds.Add(shihabTag.ShihabId);
            }

            _shihabDownloadsListView.BeginUpdate();
            try
            {
                while (_shihabDownloadsListView.Items.Count < mehediFiltered.Count)
                {
                    _shihabDownloadsListView.Items.Add(new ListViewItem(new string[] { "", "", "", "", "", "", "", "" }));
                }
                while (_shihabDownloadsListView.Items.Count > mehediFiltered.Count)
                {
                    _shihabDownloadsListView.Items.RemoveAt(_shihabDownloadsListView.Items.Count - 1);
                }

                for (int mehediIdx = 0; mehediIdx < mehediFiltered.Count; mehediIdx++)
                {
                    ShihabDownloadItem shihabIt = mehediFiltered[mehediIdx];
                    ListViewItem mehediRow = _shihabDownloadsListView.Items[mehediIdx];
                    mehediRow.Tag = shihabIt;

                    int shihabOrderNum;
                    if (!mehediOrderMap.TryGetValue(shihabIt.ShihabId, out shihabOrderNum))
                    {
                        shihabOrderNum = mehediIdx + 1;
                    }

                    bool mehediRowActive = _shihabEngine.IsMehediItemActive(shihabIt.ShihabId) ||
                                           shihabIt.GetShihabStatusEnum() == ShihabDownloadStatus.ShihabDownloading;
                    double shihabPctVal = shihabIt.GetMehediPercent();

                    UpdateSubItemIfChanged(mehediRow, 0, shihabOrderNum.ToString());
                    UpdateSubItemIfChanged(mehediRow, 1, shihabIt.ShihabFileName ?? "");
                    UpdateSubItemIfChanged(mehediRow, 2, shihabIt.ShihabTotalBytes > 0
                        ? ShihabFormatHelpers.FormatMehediBytes(shihabIt.ShihabTotalBytes)
                        : (shihabIt.MehediDownloadedBytes > 0 ? ShihabFormatHelpers.FormatMehediBytes(shihabIt.MehediDownloadedBytes) : "--"));
                    UpdateSubItemIfChanged(mehediRow, 3, (shihabIt.GetShihabStatusEnum() == ShihabDownloadStatus.ShihabCompleted || shihabIt.ShihabTotalBytes > 0)
                        ? string.Format("{0:0}%", shihabPctVal)
                        : (mehediRowActive ? "Live" : "0%"));
                    UpdateSubItemIfChanged(mehediRow, 4, (mehediRowActive && shihabIt.ShihabCurrentSpeedBps > 1.0)
                        ? ShihabFormatHelpers.FormatShihabSpeed(shihabIt.ShihabCurrentSpeedBps).Replace("/sec", "/s")
                        : "0 B/s");
                    UpdateSubItemIfChanged(mehediRow, 5, (mehediRowActive && shihabIt.MehediEtaSeconds > 0)
                        ? ShihabFormatHelpers.FormatMehediTimeLeft(shihabIt.MehediEtaSeconds)
                        : "0s");

                    string geoPrefix = !string.IsNullOrWhiteSpace(shihabIt.ShihabServerCountryCode)
                        ? ("[" + shihabIt.ShihabServerCountryCode + "] ")
                        : "";
                    string chkBadge = (!string.IsNullOrWhiteSpace(shihabIt.ShihabChecksumStatus) && shihabIt.ShihabChecksumStatus.StartsWith("Verified", StringComparison.OrdinalIgnoreCase))
                        ? " ✔"
                        : "";
                    UpdateSubItemIfChanged(mehediRow, 6, geoPrefix + ResolveCleanStatusWord(shihabIt, mehediRowActive) + chkBadge);
                    UpdateSubItemIfChanged(mehediRow, 7, ExtractShihabCompactDate(shihabIt));

                    bool shouldSelect = shihabSelectedIds.Contains(shihabIt.ShihabId);
                    if (mehediRow.Selected != shouldSelect)
                    {
                        mehediRow.Selected = shouldSelect;
                    }
                }
            }
            finally
            {
                _shihabDownloadsListView.EndUpdate();
            }

            string shihabSpeedPill = mehediGlobalSpeedBps > 5.0
                ? ("Speed: " + ShihabFormatHelpers.FormatShihabSpeed(mehediGlobalSpeedBps).Replace("/sec", "/s"))
                : "Speed: 0 KB/s";
            if (_mehediFooterBar != null)
            {
                _mehediFooterBar.SetMehediFooterMetrics(
                    "Total: " + ShihabFormatHelpers.FormatMehediBytes(mehediSumTotalBytes),
                    "Downloaded: " + ShihabFormatHelpers.FormatMehediBytes(shihabSumDownloadedBytes),
                    shihabSpeedPill,
                    string.Format("Active Sockets: {0}", shihabActiveSockets));
            }

            if (_shihabDropTargetForm != null && !_shihabDropTargetForm.IsDisposed && _shihabDropTargetForm.Visible)
            {
                _shihabDropTargetForm.UpdateShihabLiveSpeed(mehediGlobalSpeedBps);
            }
            UpdateMehediToolbarActionIcons();
            UpdateShihabStatusBadges();
        }

        private void UpdateShihabStatusBadges()
        {
            if (_mehediStorageCard == null) return;

            string mehediSpeedLimitLabel = _mehediConfig.MehediGlobalSpeedLimiterEnabled
                ? string.Format("Speed: {0} KB/s", _mehediConfig.ShihabGlobalSpeedLimitKb)
                : "Speed: Unlimited";
            _mehediStorageCard.SetShihabStorageAndSpeed(_shihabCachedUsedGb, _mehediCachedTotalGb, _shihabCachedFreeGb, mehediSpeedLimitLabel);

            DateTime shihabNowUtc = DateTime.UtcNow;
            if ((shihabNowUtc - _mehediLastDrivePollUtc).TotalSeconds >= 5.0 &&
                Interlocked.CompareExchange(ref _drivePollInFlight, 1, 0) == 0)
            {
                _mehediLastDrivePollUtc = shihabNowUtc;
                string shihabSampleFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (_mehediConfig.ShihabCategories != null && _mehediConfig.ShihabCategories.Count > 0)
                {
                    for (int mehediI = 0; mehediI < _mehediConfig.ShihabCategories.Count; mehediI++)
                    {
                        if (string.Equals(_mehediConfig.ShihabCategories[mehediI].ShihabName, "Video", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(_mehediConfig.ShihabCategories[mehediI].ShihabDefaultFolder))
                        {
                            shihabSampleFolder = _mehediConfig.ShihabCategories[mehediI].ShihabDefaultFolder;
                            break;
                        }
                    }
                }

                Task.Run(delegate
                {
                    try
                    {
                        string mehediRoot = Path.GetPathRoot(Path.GetFullPath(shihabSampleFolder));
                        if (!string.IsNullOrEmpty(mehediRoot))
                        {
                            DriveInfo shihabDrive = new DriveInfo(mehediRoot);
                            if (shihabDrive.IsReady)
                            {
                                double mehediBytesPerGb = 1024.0 * 1024.0 * 1024.0;
                                double totalGb = shihabDrive.TotalSize / mehediBytesPerGb;
                                double freeGb = shihabDrive.AvailableFreeSpace / mehediBytesPerGb;
                                double usedGb = Math.Max(0.0, totalGb - freeGb);

                                SafeShihabUiInvoke(delegate
                                {
                                    if (IsDisposed || _mehediStorageCard == null) return;
                                    _mehediCachedTotalGb = totalGb;
                                    _shihabCachedFreeGb = freeGb;
                                    _shihabCachedUsedGb = usedGb;
                                    string curSpeedLbl = _mehediConfig.MehediGlobalSpeedLimiterEnabled
                                        ? string.Format("Speed: {0} KB/s", _mehediConfig.ShihabGlobalSpeedLimitKb)
                                        : "Speed: Unlimited";
                                    _mehediStorageCard.SetShihabStorageAndSpeed(usedGb, totalGb, freeGb, curSpeedLbl);
                                });
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        Interlocked.Exchange(ref _drivePollInFlight, 0);
                    }
                });
            }
        }

        private void EvaluateSelectionCapabilities(out bool hasSelection, out bool canResume, out bool canStop, out bool canOpenCompleted)
        {
            hasSelection = false;
            canResume = false;
            canStop = false;
            canOpenCompleted = false;

            if (_shihabDownloadsListView == null || _shihabDownloadsListView.SelectedItems.Count == 0)
            {
                return;
            }

            List<ShihabDownloadItem> selected = GetSelectedShihabItems();
            hasSelection = selected.Count > 0;
            for (int i = 0; i < selected.Count; i++)
            {
                ShihabDownloadItem it = selected[i];
                if (it == null) continue;
                ShihabDownloadStatus st = it.GetShihabStatusEnum();
                bool active = _shihabEngine.IsMehediItemActive(it.ShihabId) ||
                              st == ShihabDownloadStatus.ShihabDownloading ||
                              st == ShihabDownloadStatus.ShihabConnecting ||
                              st == ShihabDownloadStatus.ShihabRebuilding;
                if (active)
                {
                    canStop = true;
                }
                else if (st != ShihabDownloadStatus.ShihabCompleted)
                {
                    canResume = true;
                }
                if (st == ShihabDownloadStatus.ShihabCompleted)
                {
                    canOpenCompleted = true;
                }
            }
        }

        private void UpdateMehediToolbarActionIcons()
        {
            if (_shihabTopRibbonBar == null) return;
            bool hasSel, canResume, canStop, canOpenCompleted;
            EvaluateSelectionCapabilities(out hasSel, out canResume, out canStop, out canOpenCompleted);
            bool anyActiveAnywhere = _shihabEngine.HasAnyActiveDownloadsOrQueues();

            _shihabTopRibbonBar.SetButtonEnabled("resume", canResume);
            _shihabTopRibbonBar.SetButtonEnabled("stop", canStop);
            _shihabTopRibbonBar.SetButtonEnabled("stop_all", anyActiveAnywhere);
            _shihabTopRibbonBar.SetButtonEnabled("delete", hasSel);
        }

        private void EnqueueSimpleShihabUrl(
            string mehediUrl,
            string shihabReferer,
            string mehediCookies,
            string shihabUserAgent,
            string mehediDesc,
            bool shihabStartNow)
        {
            if (!IsAllowedDownloadSourceUrl(mehediUrl)) return;

            ShihabDownloadItem mehediItem = new ShihabDownloadItem();
            mehediItem.MehediUrl = mehediUrl.Trim();
            mehediItem.MehediReferer = shihabReferer;
            mehediItem.ShihabCookies = mehediCookies;
            if (!string.IsNullOrWhiteSpace(shihabUserAgent)) mehediItem.MehediUserAgent = shihabUserAgent;
            mehediItem.ShihabDescription = mehediDesc;
            mehediItem.ShihabFileName = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(mehediItem.MehediUrl);
            ShihabCategoryRule shihabCat = _mehediConfig.ResolveMehediCategoryForFile(mehediItem.ShihabFileName);
            mehediItem.ShihabCategoryName = shihabCat.ShihabName;
            try { Directory.CreateDirectory(shihabCat.ShihabDefaultFolder); } catch { }
            mehediItem.MehediSaveDirectory = shihabCat.ShihabDefaultFolder;
            mehediItem.ShihabFileName = ShihabFormatHelpers.EnsureShihabUniqueFilePath(shihabCat.ShihabDefaultFolder, mehediItem.ShihabFileName);
            mehediItem.MehediMaxConnections = _mehediConfig.ShihabDefaultMaxConnections;
            if (!shihabStartNow)
            {
                mehediItem.MehediQueueName = "Main Download Queue";
                mehediItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued");
            }
            _shihabEngine.AddShihabDownloadItem(mehediItem, shihabStartNow);
        }

        private void PromptAndAddNewShihabUrl(string mehediPrefillUrl)
        {
            if (!string.IsNullOrWhiteSpace(mehediPrefillUrl) &&
                (mehediPrefillUrl.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase) ||
                 mehediPrefillUrl.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase)))
            {
                OpenShihabTorrentDialog(mehediPrefillUrl);
                return;
            }
            using (MehediAddUrlAuthDialog shihabAddDlg = new MehediAddUrlAuthDialog(_mehediConfig.ShihabDarkMode, mehediPrefillUrl))
            {
                if (shihabAddDlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (shihabAddDlg.ShihabEnteredUrl.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase) ||
                        shihabAddDlg.ShihabEnteredUrl.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                    {
                        OpenShihabTorrentDialog(shihabAddDlg.ShihabEnteredUrl);
                        return;
                    }
                    ShihabDownloadItem mehediDraft = new ShihabDownloadItem
                    {
                        MehediUrl = shihabAddDlg.ShihabEnteredUrl,
                        MehediReferer = shihabAddDlg.MehediEnteredReferer,
                        ShihabAuthUser = shihabAddDlg.MehediEnteredUser,
                        MehediAuthPassword = shihabAddDlg.ShihabEnteredPassword,
                        ShihabFileName = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(shihabAddDlg.ShihabEnteredUrl),
                        MehediMaxConnections = _mehediConfig.ShihabDefaultMaxConnections
                    };
                    using (MehediDownloadFileInfoForm shihabInfo = new MehediDownloadFileInfoForm(mehediDraft, _mehediConfig, _shihabEngine))
                    {
                        if (shihabInfo.ShowDialog(this) == DialogResult.OK)
                        {
                            _shihabEngine.AddShihabDownloadItem(mehediDraft, shihabInfo.ShihabStartImmediatelySelected);
                            if (shihabInfo.ShihabStartImmediatelySelected && _mehediConfig.MehediShowDownloadProgressDialog)
                            {
                                ShowProgressWindowForShihabItem(mehediDraft);
                            }
                        }
                    }
                }
            }
        }

        private void ShowProgressWindowForShihabItem(ShihabDownloadItem mehediItem)
        {
            ShihabDownloadProgressForm shihabExisting;
            if (_mehediOpenProgressForms.TryGetValue(mehediItem.ShihabId, out shihabExisting) && !shihabExisting.IsDisposed)
            {
                shihabExisting.BringToFront();
                return;
            }
            ShihabDownloadProgressForm mehediProg = new ShihabDownloadProgressForm(mehediItem, _shihabEngine, _mehediConfig);
            _mehediOpenProgressForms[mehediItem.ShihabId] = mehediProg;
            mehediProg.FormClosed += delegate { _mehediOpenProgressForms.Remove(mehediItem.ShihabId); };
            IWin32Window shihabOwner = GetShihabDialogOwnerOrNull();
            if (shihabOwner != null) mehediProg.Show(shihabOwner);
            else mehediProg.Show();
            try { mehediProg.Activate(); mehediProg.BringToFront(); } catch { }
        }

        private List<ShihabDownloadItem> GetSelectedShihabItems()
        {
            List<ShihabDownloadItem> mehediList = new List<ShihabDownloadItem>();
            foreach (ListViewItem shihabRow in _shihabDownloadsListView.SelectedItems)
            {
                if (shihabRow.Tag is ShihabDownloadItem)
                {
                    mehediList.Add((ShihabDownloadItem)shihabRow.Tag);
                }
            }
            return mehediList;
        }

        private void ResumeSelectedShihabDownloads()
        {
            List<ShihabDownloadItem> mehediSel = GetSelectedShihabItems();
            for (int shihabI = 0; shihabI < mehediSel.Count; shihabI++)
            {
                if (mehediSel[shihabI].GetShihabStatusEnum() != ShihabDownloadStatus.ShihabCompleted)
                {
                    _shihabEngine.StartOrResumeMehediDownload(mehediSel[shihabI]);
                }
            }
        }

        private void PauseSelectedMehediDownloads()
        {
            List<ShihabDownloadItem> shihabSel = GetSelectedShihabItems();
            for (int mehediI = 0; mehediI < shihabSel.Count; mehediI++)
            {
                _shihabEngine.PauseShihabDownload(shihabSel[mehediI]);
            }
        }

        private void PromptAndDeleteSelectedDownloads(bool initialDeleteFromDisk)
        {
            List<ShihabDownloadItem> shihabSel = GetSelectedShihabItems();
            if (shihabSel.Count == 0) return;
            using (ShihabDeleteConfirmDialog dlg = new ShihabDeleteConfirmDialog(shihabSel, initialDeleteFromDisk, _mehediConfig.ShihabDarkMode))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    DeleteSelectedShihabDownloads(dlg.AlsoDeleteDiskFiles);
                }
            }
        }

        private void DeleteSelectedShihabDownloads(bool mehediAlsoDeleteDiskFile)
        {
            List<ShihabDownloadItem> shihabSel = GetSelectedShihabItems();
            if (shihabSel.Count == 0) return;
            for (int mehediI = 0; mehediI < shihabSel.Count; mehediI++)
            {
                _shihabEngine.RemoveShihabDownload(shihabSel[mehediI], mehediAlsoDeleteDiskFile);
            }
            RefreshMehediDownloadsGrid();
        }

        private void DeleteAllCompletedMehediDownloads()
        {
            List<ShihabDownloadItem> shihabAll = _shihabEngine.GetShihabDownloadsSnapshot();
            for (int mehediI = 0; mehediI < shihabAll.Count; mehediI++)
            {
                if (shihabAll[mehediI].GetShihabStatusEnum() == ShihabDownloadStatus.ShihabCompleted)
                {
                    _shihabEngine.RemoveShihabDownload(shihabAll[mehediI], false);
                }
            }
            RefreshMehediDownloadsGrid();
        }

        private void RedownloadSelectedShihabItem()
        {
            List<ShihabDownloadItem> mehediSel = GetSelectedShihabItems();
            if (mehediSel.Count == 0) return;
            ShihabDownloadItem shihabItem = mehediSel[0];
            _shihabEngine.PauseShihabDownload(shihabItem);
            _shihabEngine.ClearItemSegmentsSafely(shihabItem);
            shihabItem.MehediDownloadedBytes = 0;
            shihabItem.MehediSha256Hex = "";
            _shihabEngine.StartOrResumeMehediDownload(shihabItem);
            if (_mehediConfig.MehediShowDownloadProgressDialog) ShowProgressWindowForShihabItem(shihabItem);
        }

        private void RefreshSelectedMehediAddress()
        {
            List<ShihabDownloadItem> shihabSel = GetSelectedShihabItems();
            if (shihabSel.Count == 0) return;
            ShihabDownloadItem mehediItem = shihabSel[0];
            using (MehediAddUrlAuthDialog shihabRefreshDlg = new MehediAddUrlAuthDialog(_mehediConfig.ShihabDarkMode, mehediItem.MehediUrl))
            {
                shihabRefreshDlg.Text = "Refresh Expired Download Address — " + mehediItem.ShihabFileName;
                if (shihabRefreshDlg.ShowDialog(this) == DialogResult.OK)
                {
                    mehediItem.MehediUrl = shihabRefreshDlg.ShihabEnteredUrl;
                    if (!string.IsNullOrEmpty(shihabRefreshDlg.MehediEnteredReferer))
                    {
                        mehediItem.MehediReferer = shihabRefreshDlg.MehediEnteredReferer;
                    }
                    _shihabEngine.PersistShihabState();
                    _shihabEngine.StartOrResumeMehediDownload(mehediItem);
                }
            }
        }

        private void AssignSelectedShihabToQueue(string mehediQueueName)
        {
            List<ShihabDownloadItem> shihabSel = GetSelectedShihabItems();
            for (int mehediI = 0; mehediI < shihabSel.Count; mehediI++)
            {
                ShihabDownloadItem it = shihabSel[mehediI];
                if (it == null) continue;
                it.MehediQueueName = mehediQueueName ?? "";
                ShihabDownloadStatus st = it.GetShihabStatusEnum();
                bool active = _shihabEngine.IsMehediItemActive(it.ShihabId);
                if (!string.IsNullOrWhiteSpace(it.MehediQueueName))
                {
                    if (!active && st != ShihabDownloadStatus.ShihabCompleted)
                    {
                        it.SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued");
                    }
                }
                else if (!active && st == ShihabDownloadStatus.ShihabQueued)
                {
                    it.SetMehediStatusEnum(ShihabDownloadStatus.ShihabPaused, "Paused");
                }
            }
            _shihabEngine.PersistShihabState();
            RefreshMehediDownloadsGrid();
        }

        private void OpenSelectedMehediProgressWindow()
        {
            List<ShihabDownloadItem> shihabSel = GetSelectedShihabItems();
            if (shihabSel.Count > 0) ShowProgressWindowForShihabItem(shihabSel[0]);
        }

        private void OpenSelectedShihabCompletedFile(bool mehediOpenWith)
        {
            List<ShihabDownloadItem> shihabSel = GetSelectedShihabItems();
            if (shihabSel.Count == 0) return;
            string mehediPath = shihabSel[0].GetShihabFullTargetPath();
            if (string.IsNullOrWhiteSpace(mehediPath) ||
                ShihabProcessAndPathSafety.ContainsControlOrNewline(mehediPath) ||
                !File.Exists(mehediPath))
            {
                return;
            }
            try
            {
                if (mehediOpenWith)
                {
                    ShihabThemePalette.SafeOpenWithDialog(mehediPath);
                }
                else
                {
                    using (Process p = Process.Start(new ProcessStartInfo(mehediPath) { UseShellExecute = true })) { }
                }
            }
            catch { }
        }

        private void OpenSelectedMehediContainingFolder()
        {
            List<ShihabDownloadItem> shihabSel = GetSelectedShihabItems();
            if (shihabSel.Count == 0) return;
            string mehediPath = shihabSel[0].GetShihabFullTargetPath();
            ShihabThemePalette.SafeOpenContainingFolderOrFile(mehediPath, shihabSel[0].MehediSaveDirectory);
        }

        private void OpenDefaultShihabDownloadsDirectory()
        {
            try
            {
                string shihabDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (_shihabActiveTreeFilter.StartsWith("CAT:"))
                {
                    string mehediCatName = _shihabActiveTreeFilter.Substring(4);
                    for (int shihabI = 0; shihabI < _mehediConfig.ShihabCategories.Count; shihabI++)
                    {
                        if (string.Equals(_mehediConfig.ShihabCategories[shihabI].ShihabName, mehediCatName, StringComparison.OrdinalIgnoreCase))
                        {
                            shihabDir = _mehediConfig.ShihabCategories[shihabI].ShihabDefaultFolder;
                            break;
                        }
                    }
                }
                OpenDirectoryInExplorerSafe(shihabDir);
            }
            catch { }
        }

        private static void OpenDirectoryInExplorerSafe(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || ShihabProcessAndPathSafety.ContainsControlOrNewline(folderPath)) return;
            try
            {
                string full = Path.GetFullPath(folderPath);
                if (!Directory.Exists(full)) return;
                string explorerExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
                using (Process p = Process.Start(new ProcessStartInfo(File.Exists(explorerExe) ? explorerExe : "explorer.exe", "\"" + full + "\""))) { }
            }
            catch { }
        }

        private void OpenSelectedShihabProperties()
        {
            List<ShihabDownloadItem> mehediSel = GetSelectedShihabItems();
            if (mehediSel.Count == 0) return;
            using (ShihabFilePropertiesForm shihabProp = new ShihabFilePropertiesForm(mehediSel[0], _mehediConfig.ShihabDarkMode))
            {
                if (shihabProp.ShowDialog(this) == DialogResult.OK)
                {
                    _shihabEngine.PersistShihabState();
                    RefreshMehediDownloadsGrid();
                }
            }
        }

        private void HandleDoubleClickOnShihabRow()
        {
            List<ShihabDownloadItem> mehediSel = GetSelectedShihabItems();
            if (mehediSel.Count == 0) return;
            if (mehediSel[0].GetShihabStatusEnum() == ShihabDownloadStatus.ShihabCompleted)
            {
                OpenSelectedShihabCompletedFile(false);
            }
            else
            {
                ShowProgressWindowForShihabItem(mehediSel[0]);
            }
        }

        private void OpenShihabOptionsDialog()
        {
            using (ShihabOptionsConfigForm mehediOpts = new ShihabOptionsConfigForm(_mehediConfig, _shihabStore, _mehediBridge, ExitShihabApplicationCleanly))
            {
                if (mehediOpts.ShowDialog(this) == DialogResult.OK)
                {
                    if (mehediOpts.ShihabBridgeWasRestarted)
                    {
                        _mehediBridgeListening = mehediOpts.MehediBridgeRestartSucceeded;
                    }
                    ApplyMehediTheme();
                    PopulateShihabCategoryTree();
                    RefreshMehediDownloadsGrid();
                    UpdateShihabStatusBadges();
                }
            }
        }

        private void OpenMehediSchedulerDialog()
        {
            using (MehediSchedulerQueuesForm shihabSched = new MehediSchedulerQueuesForm(_mehediConfig, _shihabEngine, _shihabStore))
            {
                shihabSched.ShowDialog(this);
                PopulateShihabCategoryTree();
                RefreshMehediDownloadsGrid();
            }
        }

        private void OpenShihabBatchWildcardDialog()
        {
            using (ShihabBatchDownloadForm mehediBatch = new ShihabBatchDownloadForm(_mehediConfig.ShihabDarkMode))
            {
                if (mehediBatch.ShowDialog(this) == DialogResult.OK)
                {
                    for (int shihabI = 0; shihabI < mehediBatch.ShihabGeneratedUrls.Count; shihabI++)
                    {
                        EnqueueSimpleShihabUrl(mehediBatch.ShihabGeneratedUrls[shihabI], "", "", "", "Batch wildcard", mehediBatch.MehediStartImmediately);
                    }
                }
            }
        }

        private void ImportShihabClipboardUrlsBatch()
        {
            try
            {
                if (!Clipboard.ContainsText())
                {
                    MessageBox.Show(this, "Cannot find any web address in the clipboard!", "MHS Download Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                string[] mehediTokens = Clipboard.GetText().Split(new char[] { '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                ShihabBatchCaptureDto shihabDto = new ShihabBatchCaptureDto { ShihabLinks = new List<MehediBatchLinkEntryDto>() };
                for (int mehediI = 0; mehediI < mehediTokens.Length; mehediI++)
                {
                    string tok = mehediTokens[mehediI];
                    Uri u;
                    if (!ShihabProcessAndPathSafety.ContainsControlOrNewline(tok) &&
                        Uri.TryCreate(tok, UriKind.Absolute, out u) &&
                        (string.Equals(u.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(u.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
                    {
                        shihabDto.ShihabLinks.Add(new MehediBatchLinkEntryDto { ShihabUrl = tok, MehediText = "Clipboard URL" });
                    }
                }
                if (shihabDto.ShihabLinks.Count == 0)
                {
                    MessageBox.Show(this, "Cannot find any http:// or https:// web address in clipboard!", "MHS Download Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                HandleBrowserMehediBatchCapture(shihabDto);
            }
            catch { }
        }

        private void OpenShihabSiteGrabberDialog()
        {
            using (ShihabSiteGrabberForm mehediGrab = new ShihabSiteGrabberForm(_mehediConfig.ShihabDarkMode))
            {
                if (mehediGrab.ShowDialog(this) == DialogResult.OK)
                {
                    for (int shihabI = 0; shihabI < mehediGrab.ShihabSelectedGrabbedUrls.Count; shihabI++)
                    {
                        EnqueueSimpleShihabUrl(mehediGrab.ShihabSelectedGrabbedUrls[shihabI], "", "", "", "Site Grabber", mehediGrab.MehediStartImmediately);
                    }
                }
            }
        }

        private void OpenShihabTorrentDialog(string initialMagnetOrPath)
        {
            using (ShihabTorrentAddDialog dlg = new ShihabTorrentAddDialog(_mehediConfig, initialMagnetOrPath))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ConfiguredTorrentItem != null)
                {
                    _shihabEngine.AddShihabDownloadItem(dlg.ConfiguredTorrentItem, dlg.StartImmediately);
                    if (dlg.StartImmediately && _mehediConfig.MehediShowDownloadProgressDialog)
                    {
                        ShowProgressWindowForShihabItem(dlg.ConfiguredTorrentItem);
                    }
                }
            }
        }

        private void OpenShihabMediaAndGalleryDialog()
        {
            using (ShihabMediaAndGalleryDialog dlg = new ShihabMediaAndGalleryDialog(_mehediConfig, _shihabStore))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (dlg.ConfiguredMediaItems != null && dlg.ConfiguredMediaItems.Count > 0)
                    {
                        for (int i = 0; i < dlg.ConfiguredMediaItems.Count; i++)
                        {
                            ShihabDownloadItem item = dlg.ConfiguredMediaItems[i];
                            if (item == null || !IsAllowedDownloadSourceUrl(item.MehediUrl)) continue;
                            ShihabCategoryRule cat = _mehediConfig.ResolveMehediCategoryForFile(item.ShihabFileName);
                            if (string.IsNullOrWhiteSpace(item.MehediSaveDirectory))
                            {
                                item.ShihabCategoryName = cat.ShihabName;
                                item.MehediSaveDirectory = cat.ShihabDefaultFolder;
                            }
                            else
                            {
                                item.MehediSaveDirectory = ResolveSafeSaveDirectory(item.MehediSaveDirectory, cat.ShihabDefaultFolder);
                            }
                            try { Directory.CreateDirectory(item.MehediSaveDirectory); } catch { }
                            item.ShihabFileName = ShihabFormatHelpers.EnsureShihabUniqueFilePath(item.MehediSaveDirectory, item.ShihabFileName);
                            _shihabEngine.AddShihabDownloadItem(item, true);
                        }
                    }
                    else if (dlg.DiscoveredDownloadUrls.Count > 0)
                    {
                        for (int i = 0; i < dlg.DiscoveredDownloadUrls.Count; i++)
                        {
                            EnqueueSimpleShihabUrl(dlg.DiscoveredDownloadUrls[i], "", "", "", "Media / Gallery", true);
                        }
                    }
                }
            }
        }

        private void OpenShihabRssFeedDialog()
        {
            using (ShihabRssFeedDialog dlg = new ShihabRssFeedDialog(_mehediConfig, _shihabStore))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedEnclosureUrls.Count > 0)
                {
                    for (int i = 0; i < dlg.SelectedEnclosureUrls.Count; i++)
                    {
                        EnqueueSimpleShihabUrl(dlg.SelectedEnclosureUrls[i], "", "", "", "RSS Enclosure", true);
                    }
                }
            }
        }

        private void OpenShihabRemotePairingDialog()
        {
            using (ShihabRemotePairingDialog dlg = new ShihabRemotePairingDialog(_mehediConfig, _shihabStore))
            {
                dlg.ShowDialog(this);
                if (dlg.RemoteConfigChanged && _shihabRemoteServer != null)
                {
                    if (_mehediConfig.ShihabRemoteWebEnabled) _shihabRemoteServer.Start();
                    else _shihabRemoteServer.Stop();
                }
            }
        }

        private void OpenShihabChecksumAndExtractDialog()
        {
            using (ShihabChecksumAndExtractDialog dlg = new ShihabChecksumAndExtractDialog(_mehediConfig.ShihabDarkMode))
            {
                dlg.ShowDialog(this);
            }
        }

        private void ToggleMehediFloatingDropTarget()
        {
            if (_shihabDropTargetForm == null || _shihabDropTargetForm.IsDisposed)
            {
                _shihabDropTargetForm = new MehediFloatingDropTargetForm(
                    delegate(string shihabDropped)
                    {
                        HandleDroppedTextOrFilePath(shihabDropped);
                    },
                    _mehediConfig.ShihabDropTargetX,
                    _mehediConfig.ShihabDropTargetY,
                    delegate(int newX, int newY)
                    {
                        _mehediConfig.ShihabDropTargetX = newX;
                        _mehediConfig.ShihabDropTargetY = newY;
                        _shihabStore.SaveMehediConfig(_mehediConfig);
                    });
                _shihabDropTargetForm.Show();
                _mehediConfig.MehediShowFloatingDropTarget = true;
            }
            else
            {
                _shihabDropTargetForm.Visible = !_shihabDropTargetForm.Visible;
                _mehediConfig.MehediShowFloatingDropTarget = _shihabDropTargetForm.Visible;
            }
            _shihabStore.SaveMehediConfig(_mehediConfig);
        }

        private void ExportShihabList(bool mehediEf2Format)
        {
            using (SaveFileDialog shihabSfd = new SaveFileDialog())
            {
                shihabSfd.Filter = mehediEf2Format ? "IDM Export Files (*.ef2)|*.ef2" : "Text Files (*.txt)|*.txt";
                shihabSfd.FileName = mehediEf2Format ? "shihab_downloads.ef2" : "shihab_downloads.txt";
                if (shihabSfd.ShowDialog(this) == DialogResult.OK)
                {
                    List<ShihabDownloadItem> mehediItems = _shihabEngine.GetShihabDownloadsSnapshot();
                    if (mehediEf2Format)
                    {
                        ShihabStateStore.ExportToShihabEf2File(shihabSfd.FileName, mehediItems);
                    }
                    else
                    {
                        List<string> shihabLines = new List<string>();
                        for (int mehediI = 0; mehediI < mehediItems.Count; mehediI++) shihabLines.Add(mehediItems[mehediI].MehediUrl);
                        File.WriteAllLines(shihabSfd.FileName, shihabLines.ToArray());
                    }
                }
            }
        }

        private void ImportMehediList()
        {
            using (OpenFileDialog shihabOfd = new OpenFileDialog())
            {
                shihabOfd.Filter = "IDM Export & Text Files (*.ef2;*.txt)|*.ef2;*.txt|All Files (*.*)|*.*";
                if (shihabOfd.ShowDialog(this) == DialogResult.OK)
                {
                    List<ShihabDownloadItem> mehediImported = ShihabStateStore.ImportFromMehediEf2OrText(shihabOfd.FileName);
                    for (int shihabI = 0; shihabI < mehediImported.Count; shihabI++)
                    {
                        ShihabDownloadItem mehediIt = mehediImported[shihabI];
                        ShihabCategoryRule shihabCat = _mehediConfig.ResolveMehediCategoryForFile(mehediIt.ShihabFileName);
                        mehediIt.ShihabCategoryName = shihabCat.ShihabName;
                        mehediIt.MehediSaveDirectory = shihabCat.ShihabDefaultFolder;
                        mehediIt.MehediQueueName = "Main Download Queue";
                        mehediIt.SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued");
                        _shihabEngine.AddShihabDownloadItem(mehediIt, false);
                    }
                }
            }
        }

        private static string ResolveShihabExtensionDirectoryPath()
        {
            string shihabInstalledExt = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "extension"));
            if (Directory.Exists(shihabInstalledExt))
            {
                return shihabInstalledExt;
            }
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\extension"));
        }

        private void OpenShihabInstalledExtensionFolder()
        {
            string mehediExtFolder = ResolveShihabExtensionDirectoryPath();
            try
            {
                Clipboard.SetText(mehediExtFolder);
            }
            catch { }
            if (Directory.Exists(mehediExtFolder))
            {
                OpenDirectoryInExplorerSafe(mehediExtFolder);
            }
            else
            {
                ShowShihabExtensionInstructions();
            }
        }

        private void ShowShihabExtensionInstructions()
        {
            string mehediExtFolder = ResolveShihabExtensionDirectoryPath();
            try { Clipboard.SetText(mehediExtFolder); } catch { }
            DialogResult shihabOpenNow = MessageBox.Show(
                this,
                "MHS Download Manager Integration Module (Manifest V3 — Chrome / Brave / Edge / Opera):\n\n" +
                "1. Open chrome://extensions or brave://extensions (or edge://extensions)\n" +
                "2. Enable 'Developer mode' in the top-right corner\n" +
                "3. Click 'Load unpacked' and select:\n   " + mehediExtFolder + "\n   (Path copied to clipboard!)\n\n" +
                "Would you like to open the extension folder in File Explorer now?",
                ShihabThemePalette.ShihabMainHeaderTitle + " — Browser Extension",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (shihabOpenNow == DialogResult.Yes && Directory.Exists(mehediExtFolder))
            {
                OpenDirectoryInExplorerSafe(mehediExtFolder);
            }
        }

        private void ShowMehediCliHelp()
        {
            MessageBox.Show(
                this,
                "MHS Download Manager Command-Line, Auto-Updater & MCP Switches:\n\n" +
                "IDM-Compatible Switches:\n" +
                "  mdm.exe /d <URL> [/p Path] [/f FileName] [/n] [/q] [/a] [/s]\n\n" +
                "Subcommands & Automation:\n" +
                "  mdm.exe --cli list | add <url> | pause <id> | resume <id> | cancel <id>\n" +
                "  mdm.exe --cli check-update [--json]     Check GitHub Releases for updates\n" +
                "  mdm.exe --cli update [--verify-only]    Verify SHA256SUMS.txt & install update\n" +
                "  mdm.exe --cli pair                      Print LAN Web UI URL, PIN & QR code\n" +
                "  mdm.exe --mcp                           Start JSON-RPC 2.0 MCP server (15 tools)\n" +
                "  mdm.exe --headless                      Run daemon + browser bridge without GUI",
                ShihabThemePalette.ShihabMainHeaderTitle + " — CLI & MCP Help",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void OpenShihabTellAFriendDialog()
        {
            using (ShihabTellAFriendDialog dlg = new ShihabTellAFriendDialog(_mehediConfig))
            {
                dlg.ShowDialog(this);
            }
        }

        private void OpenShihabRegistrationDialog()
        {
            using (ShihabRegistrationStatusDialog dlg = new ShihabRegistrationStatusDialog(_mehediConfig))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.CheckUpdatesRequested)
                {
                    TriggerShihabUpdateCheck(true);
                }
            }
        }

        private void ShowShihabAboutDialog()
        {
            using (ShihabAboutDeveloperDialog mehediAboutDlg = new ShihabAboutDeveloperDialog(_mehediConfig.ShihabDarkMode))
            {
                mehediAboutDlg.ShowDialog(this);
            }
        }
    }
}

