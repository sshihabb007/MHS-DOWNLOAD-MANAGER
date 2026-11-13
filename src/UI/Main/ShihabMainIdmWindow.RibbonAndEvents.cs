using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public partial class ShihabMainIdmWindow
    {
        private void ApplyMehediTheme()
        {
            string themeName = !string.IsNullOrWhiteSpace(_mehediConfig.ShihabThemeName)
                ? _mehediConfig.ShihabThemeName
                : (_mehediConfig.ShihabDarkMode ? "Obsidian Dark" : "Classic Light");
            ShihabCustomThemePreset preset = ShihabThemeManager.ResolveThemePalette(themeName, _shihabStore.MehediBaseDirectory);
            ShihabThemePalette.ApplyThemePreset(preset);
            bool shihabDark = ShihabThemeManager.IsDarkThemePreset(preset);
            _mehediConfig.ShihabDarkMode = shihabDark;

            Color mehediWorkspaceBg = ShihabThemePalette.GetMehediWindowBg(shihabDark);
            Color shihabSideBg = ShihabThemePalette.GetShihabSidebarBg(shihabDark);
            Color shihabSurfBg = ShihabThemePalette.GetShihabSurfaceBg(shihabDark);
            Color shihabFg = ShihabThemePalette.GetShihabPrimaryText(shihabDark);
            Color mehediMuted = ShihabThemePalette.GetMehediMutedText(shihabDark);

            bool isRtl = ShihabThemeManager.IsRightToLeftLanguage(_mehediConfig.ShihabLanguageCode);
            RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;

            int uiScale = _mehediConfig.ShihabUiScalePercent > 0 ? Math.Max(80, Math.Min(150, _mehediConfig.ShihabUiScalePercent)) : 100;
            float scaledFontPt = 9.0f * (uiScale / 100.0f);
            if (_scaledUiFont == null || Math.Abs(_scaledUiFont.Size - scaledFontPt) > 0.15f)
            {
                Font nextFont = new Font("Segoe UI", scaledFontPt);
                Font oldFont = _scaledUiFont;
                _scaledUiFont = nextFont;
                Font = nextFont;
                _shihabMenuBar.Font = nextFont;
                if (oldFont != null) oldFont.Dispose();
            }
            else if (_shihabMenuBar.Font != _scaledUiFont)
            {
                _shihabMenuBar.Font = _scaledUiFont;
            }

            string lang = _mehediConfig.ShihabLanguageCode;
            if (_tasksTopMenu != null) _tasksTopMenu.Text = ShihabThemeManager.TranslateUiString(lang, "menu_tasks", "&Tasks");
            if (_fileTopMenu != null) _fileTopMenu.Text = ShihabThemeManager.TranslateUiString(lang, "menu_file", "&File");
            if (_downloadsTopMenu != null) _downloadsTopMenu.Text = ShihabThemeManager.TranslateUiString(lang, "menu_downloads", "&Downloads");
            if (_viewTopMenu != null) _viewTopMenu.Text = ShihabThemeManager.TranslateUiString(lang, "menu_view", "&View");
            if (_helpTopMenu != null) _helpTopMenu.Text = ShihabThemeManager.TranslateUiString(lang, "menu_help", "&Help");
            if (_registrationTopMenu != null) _registrationTopMenu.Text = ShihabThemeManager.TranslateUiString(lang, "menu_registration", "&Registration");
            if (_shihabTitleBar != null)
            {
                Color titleBg = shihabDark ? Color.FromArgb(22, 24, 30) : Color.FromArgb(240, 244, 249);
                Color titleDiv = shihabDark ? Color.FromArgb(36, 40, 50) : Color.FromArgb(214, 222, 234);
                _shihabTitleBar.ApplyTheme(shihabDark, titleBg, titleDiv);
            }

            if (_shihabTopRibbonBar != null)
            {
                _shihabTopRibbonBar.ShihabDarkMode = shihabDark;
                _shihabTopRibbonBar.UiScalePercent = uiScale;
                for (int i = 0; i < _shihabTopRibbonBar.Items.Count; i++)
                {
                    ShihabTopRibbonButtonSpec spec = _shihabTopRibbonBar.Items[i];
                    spec.DisplayText = ShihabThemeManager.TranslateUiString(lang, spec.LocalizationKey, spec.DefaultText);
                }
                _shihabTopRibbonBar.UpdateMetricsForScale();
            }

            Color chromeBorder = ShihabThemePalette.GetShihabBorder(shihabDark);
            ApplyShihabChromeTheme(shihabDark, chromeBorder);
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, shihabDark);
            BackColor = mehediWorkspaceBg;
            ForeColor = shihabFg;

            MehediDarkToolStripRenderer shihabMenuRenderer = new MehediDarkToolStripRenderer(shihabDark, false);
            _shihabMenuBar.BackColor = shihabDark ? Color.FromArgb(27, 29, 35) : Color.FromArgb(244, 246, 250);
            _shihabMenuBar.ForeColor = shihabFg;
            _shihabMenuBar.Renderer = shihabMenuRenderer;
            if (_shihabStartQueuePopupMenu != null) _shihabStartQueuePopupMenu.Renderer = shihabMenuRenderer;
            if (_shihabStopQueuePopupMenu != null) _shihabStopQueuePopupMenu.Renderer = shihabMenuRenderer;
            if (_mehediOverflowAppMenu != null) _mehediOverflowAppMenu.Renderer = shihabMenuRenderer;
            if (_shihabDownloadsListView.ContextMenuStrip != null) _shihabDownloadsListView.ContextMenuStrip.Renderer = shihabMenuRenderer;
            if (_shihabTrayMenuRef != null) _shihabTrayMenuRef.Renderer = shihabMenuRenderer;

            _shihabSidebarPanel.BackColor = shihabSideBg;
            _mehediSidebarTopHeading.BackColor = shihabSideBg;
            _shihabTransfersNav.ShihabDarkMode = shihabDark;
            _shihabTransfersNav.BackColor = shihabSideBg;
            _mehediStorageCard.ShihabDarkMode = shihabDark;
            _mehediStorageCard.BackColor = shihabSideBg;
            _shihabSidebarPanel.Invalidate(true);

            _shihabRightWorkspacePanel.BackColor = mehediWorkspaceBg;
            _mehediTopActionHeaderBar.BackColor = mehediWorkspaceBg;

            ShihabHeaderPillButton[] mehediPills = new ShihabHeaderPillButton[]
            {
                _shihabTorrentPillBtn,
                _shihabMediaPillBtn,
                _shihabRssPillBtn,
                _mehediFolderPillBtn,
                _shihabRemotePillBtn,
                _shihabUpdatePillBtn,
                _mehediMenuPillBtn
            };
            for (int shihabI = 0; shihabI < mehediPills.Length; shihabI++)
            {
                if (mehediPills[shihabI] != null)
                {
                    mehediPills[shihabI].ShihabDarkMode = shihabDark;
                    mehediPills[shihabI].Invalidate();
                }
            }

            _shihabSearchBarPanel.BackColor = mehediWorkspaceBg;
            _shihabSearchBarPanel.MehediFillColor = shihabDark ? shihabSurfBg : Color.White;
            _shihabSearchBarPanel.ShihabBorderColor = ShihabThemePalette.GetShihabBorder(shihabDark);
            _mehediSearchBox.BackColor = _shihabSearchBarPanel.MehediFillColor;
            _mehediSearchBox.ForeColor = shihabFg;
            _shihabClearSearchBtn.BackColor = _shihabSearchBarPanel.MehediFillColor;
            _shihabClearSearchBtn.ForeColor = mehediMuted;
            _shihabSearchBarPanel.Invalidate();

            _shihabDownloadsListView.MehediDarkMode = shihabDark;
            _shihabDownloadsListView.BackColor = shihabDark ? mehediWorkspaceBg : Color.White;
            _shihabDownloadsListView.ForeColor = shihabFg;
            _shihabDownloadsListView.Invalidate();

            _mehediFooterBar.ShihabDarkMode = shihabDark;
            _mehediFooterBar.BackColor = shihabSideBg;
            _mehediFooterBar.Invalidate();

            ShihabThemePalette.ApplyShihabDarkThemeToControlTree(this, shihabDark);
            _mehediSearchBox.BackColor = _shihabSearchBarPanel.MehediFillColor;
            RepositionShihabHeaderControls();
            _mehediTopActionHeaderBar.Invalidate();
            UpdateShihabStatusBadges();
        }

        private void RepositionShihabHeaderControls()
        {
            if (_mehediTopActionHeaderBar == null) return;
            int shihabBarW = _mehediTopActionHeaderBar.ClientSize.Width;
            int mehediBtnY = (_mehediTopActionHeaderBar.Height - 28) / 2;

            _shihabSearchBarPanel.Location = new Point(Math.Max(280, shihabBarW - _shihabSearchBarPanel.Width - 12), (_mehediTopActionHeaderBar.Height - _shihabSearchBarPanel.Height) / 2);

            int mehediTitleEnd = 195;
            using (Graphics shihabMeasureG = _mehediTopActionHeaderBar.CreateGraphics())
            {
                Size mehediT = TextRenderer.MeasureText(shihabMeasureG, _shihabHeaderViewTitle, _headerTitleFont ?? Font, new Size(340, 28), TextFormatFlags.NoPadding);
                Size shihabB = TextRenderer.MeasureText(shihabMeasureG, _mehediHeaderItemsBadgeText, _headerBadgeFont ?? Font, new Size(160, 22), TextFormatFlags.NoPadding);
                mehediTitleEnd = 14 + mehediT.Width + 4 + 8 + shihabB.Width + 14 + 14;
            }

            bool hasUpdateBanner = _shihabLatestDetectedUpdate != null && _shihabLatestDetectedUpdate.UpdateAvailable;
            ShihabHeaderPillButton[] shihabBtns = new ShihabHeaderPillButton[]
            {
                _shihabTorrentPillBtn,
                _shihabMediaPillBtn,
                _shihabRssPillBtn,
                _mehediFolderPillBtn,
                _shihabRemotePillBtn,
                _shihabUpdatePillBtn,
                _mehediMenuPillBtn
            };

            int mehediCurX = mehediTitleEnd;
            for (int shihabI = 0; shihabI < shihabBtns.Length; shihabI++)
            {
                ShihabHeaderPillButton mehediBtn = shihabBtns[shihabI];
                if (mehediBtn == null) continue;
                if (mehediBtn == _shihabUpdatePillBtn && !hasUpdateBanner)
                {
                    mehediBtn.Visible = false;
                    continue;
                }
                bool shihabFits = (mehediCurX + mehediBtn.Width <= _shihabSearchBarPanel.Left - 8) || mehediBtn == _mehediMenuPillBtn;
                mehediBtn.Visible = shihabFits;
                if (shihabFits)
                {
                    mehediBtn.Location = new Point(mehediCurX, mehediBtnY);
                    mehediCurX += mehediBtn.Width + 6;
                }
            }
            _mehediTopActionHeaderBar.Invalidate();
        }

        private void ResizeShihabFileNameColumn()
        {
            if (_shihabDownloadsListView == null || _shihabDownloadsListView.Columns.Count < 8) return;
            int mehediAvail = _shihabDownloadsListView.ClientSize.Width;
            if (mehediAvail <= 200) return;

            int shihabFixedSum = 0;
            for (int mehediC = 0; mehediC < _shihabDownloadsListView.Columns.Count; mehediC++)
            {
                if (mehediC != 1)
                {
                    shihabFixedSum += _shihabDownloadsListView.Columns[mehediC].Width;
                }
            }
            int mehediTargetNameW = Math.Max(210, mehediAvail - shihabFixedSum);
            if (Math.Abs(_shihabDownloadsListView.Columns[1].Width - mehediTargetNameW) > 1)
            {
                _shihabDownloadsListView.Columns[1].Width = mehediTargetNameW;
            }
        }

        private void OnShihabMainFormFirstShown()
        {
            if (_mehediConfig.MehediShowFloatingDropTarget)
            {
                ToggleMehediFloatingDropTarget();
            }

            if (_shihabStartupCli != null && !string.IsNullOrWhiteSpace(_shihabStartupCli.ShihabUpdatedFromVersion))
            {
                try
                {
                    if (_mehediTrayIcon != null)
                    {
                        _mehediTrayIcon.ShowBalloonTip(
                            8000,
                            "MHS Download Manager Updated!",
                            string.Format("Successfully updated from v{0} to v{1}.\r\nTip: Click Reload on MHS extension in chrome://extensions if unpacked.",
                                _shihabStartupCli.ShihabUpdatedFromVersion,
                                ShihabAutoUpdater.GetCurrentAppVersion()),
                            ToolTipIcon.Info);
                    }
                }
                catch { }
            }

            string uncleanCrashLog;
            if (ShihabCrashMonitor.CheckAndClearUncleanShutdownMarker(_shihabStore.MehediBaseDirectory, out uncleanCrashLog))
            {
                List<ShihabDownloadItem> snap = _shihabEngine.GetShihabDownloadsSnapshot();
                List<ShihabDownloadItem> unfinished = new List<ShihabDownloadItem>();
                for (int i = 0; i < snap.Count; i++)
                {
                    if (snap[i].GetShihabStatusEnum() != ShihabDownloadStatus.ShihabCompleted)
                    {
                        unfinished.Add(snap[i]);
                    }
                }
                if (unfinished.Count > 0)
                {
                    DialogResult recoverAns = MessageBox.Show(
                        this,
                        string.Format("MHS Download Manager detected an unexpected exit in the previous session.\r\n\r\nWould you like to automatically resume your {0} unfinished download(s) from their exact verified byte checkpoints?", unfinished.Count),
                        "Crash Recovery — MHS Download Manager",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    if (recoverAns == DialogResult.Yes)
                    {
                        for (int i = 0; i < unfinished.Count; i++)
                        {
                            _shihabEngine.StartOrResumeMehediDownload(unfinished[i]);
                        }
                    }
                }
            }

            _shihabEngine.StartStartupQueues();

            if (_shihabStartupCli != null)
            {
                if (_shihabStartupCli.ShihabStartMinimized)
                {
                    Hide();
                }

                if (_shihabStartupCli.MehediAutoCloseMs > 0)
                {
                    System.Windows.Forms.Timer mehediAutoClose = new System.Windows.Forms.Timer { Interval = Math.Max(400, _shihabStartupCli.MehediAutoCloseMs) };
                    mehediAutoClose.Tick += delegate
                    {
                        mehediAutoClose.Stop();
                        mehediAutoClose.Dispose();
                        ExitShihabApplicationCleanly();
                    };
                    mehediAutoClose.Start();
                }

                if (!string.IsNullOrWhiteSpace(_shihabStartupCli.ShihabDirectUrl))
                {
                    ShihabCapturedLinkDto shihabCliDto = new ShihabCapturedLinkDto
                    {
                        ShihabUrl = _shihabStartupCli.ShihabDirectUrl,
                        ShihabFileName = _shihabStartupCli.ShihabLocalFileName ?? "",
                        ShihabSaveDirectory = _shihabStartupCli.MehediLocalSavePath ?? "",
                        ShihabSilentMode = _shihabStartupCli.MehediSilentNoPrompt,
                        MehediAddToQueueOnly = _shihabStartupCli.MehediAddToQueueOnly,
                        MehediExitWhenDone = _shihabStartupCli.ShihabExitOnDone
                    };
                    HandleBrowserShihabSingleCapture(shihabCliDto);
                }

                if (_shihabStartupCli.ShihabStartMainQueue)
                {
                    _shihabEngine.StartShihabQueue("Main Download Queue");
                }
            }

            // Automatic GitHub Release Update Check: 5s after startup + every 6 hours
            System.Windows.Forms.Timer startupUpdateTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            startupUpdateTimer.Tick += delegate
            {
                startupUpdateTimer.Stop();
                startupUpdateTimer.Dispose();
                if (!IsDisposed && _mehediConfig.ShihabAutoCheckUpdates)
                {
                    TriggerShihabUpdateCheck(false);
                }
            };
            startupUpdateTimer.Start();

            _shihabPeriodicUpdateTimer = new System.Windows.Forms.Timer { Interval = 6 * 60 * 60 * 1000 };
            _shihabPeriodicUpdateTimer.Tick += delegate
            {
                if (!IsDisposed && _mehediConfig.ShihabAutoCheckUpdates)
                {
                    TriggerShihabUpdateCheck(false);
                }
            };
            _shihabPeriodicUpdateTimer.Start();

            // Background RSS / Atom Feed Auto-Downloader Poller (every 60s)
            _shihabRssPollTimer = new System.Windows.Forms.Timer { Interval = 60000 };
            _shihabRssPollTimer.Tick += delegate { PollRssSubscriptionsInBackgroundAsync(); };
            _shihabRssPollTimer.Start();
        }

        private void RestoreAndActivateMainWindow()
        {
            if (IsDisposed) return;
            Show();
            ShowInTaskbar = true;
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }
            RefreshShihabChromeSurface();
            ShihabThemePalette.ApplyShihabWindowIcons(this);
            Activate();
            BringToFront();
        }

        private void MinimizeShihabToBackgroundTray(bool setMinimizedWindowState)
        {
            if (IsDisposed) return;
            try
            {
                if (_mehediTrayIcon != null)
                {
                    _mehediTrayIcon.Visible = true;
                }
            }
            catch { }
            if (setMinimizedWindowState && WindowState != FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Minimized;
            }
            Hide();
            ShowInTaskbar = false;
            ShihabOsAndDiskSafety.TrimWorkingSetMemory();
        }

        private void TriggerShihabUpdateCheck(bool manualUserInitiated)
        {
            if (_shihabUpdateCheckInFlight || IsDisposed) return;
            _shihabUpdateCheckInFlight = true;

            Task.Run(delegate
            {
                ShihabUpdateCheckResult res = ShihabAutoUpdater.CheckForUpdates(_mehediConfig, manualUserInitiated);
                SafeShihabUiInvoke(delegate
                {
                    _shihabUpdateCheckInFlight = false;
                    if (IsDisposed || res == null) return;
                    _shihabStore.SaveMehediConfig(_mehediConfig);

                    if (res.Ok && res.UpdateAvailable)
                    {
                        _shihabLatestDetectedUpdate = res;
                        if (_shihabUpdatePillBtn != null)
                        {
                            _shihabUpdatePillBtn.Text = "Update v" + res.LatestVersion;
                        }
                        RepositionShihabHeaderControls();

                        try
                        {
                            if (_mehediTrayIcon != null && !manualUserInitiated)
                            {
                                _mehediTrayIcon.ShowBalloonTip(
                                    8000,
                                    "Update Available — MHS Download Manager v" + res.LatestVersion,
                                    string.Format("A new GitHub release (v{0}) is ready! Click to view release notes and install.", res.LatestVersion),
                                    ToolTipIcon.Info);
                            }
                        }
                        catch { }

                        PlayShihabConfiguredSound(_mehediConfig.MehediPlaySoundComplete, _mehediConfig.ShihabSoundCompleteWav, false);
                        OpenShihabAutoUpdateDialog(res);
                    }
                    else if (manualUserInitiated)
                    {
                        if (!res.Ok)
                        {
                            MessageBox.Show(
                                this,
                                string.IsNullOrWhiteSpace(res.ErrorMessage) ? "Unable to contact GitHub Releases API." : res.ErrorMessage,
                                "Check for Updates — MHS Download Manager",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                        else
                        {
                            MessageBox.Show(
                                this,
                                string.Format("You are running the latest version of MHS Download Manager (v{0}).\r\n\r\nRepository: {1}",
                                    res.CurrentVersion,
                                    string.IsNullOrWhiteSpace(_mehediConfig.ShihabUpdateRepoSlug) ? ShihabAutoUpdater.DefaultRepoSlug : _mehediConfig.ShihabUpdateRepoSlug),
                                "Up to Date — MHS Download Manager",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                    }
                });
            });
        }

        private void OpenShihabAutoUpdateDialog(ShihabUpdateCheckResult res)
        {
            if (res == null || !res.UpdateAvailable || _shihabUpdateDialogOpen || IsDisposed) return;
            _shihabUpdateDialogOpen = true;
            try
            {
                using (ShihabAutoUpdateDialog dlg = new ShihabAutoUpdateDialog(res, _mehediConfig, _shihabStore, ExitShihabApplicationCleanly))
                {
                    IWin32Window owner = GetShihabDialogOwnerOrNull();
                    DialogResult ans = owner != null ? dlg.ShowDialog(owner) : dlg.ShowDialog();
                    if (ans == DialogResult.Ignore)
                    {
                        _shihabLatestDetectedUpdate = null;
                        RepositionShihabHeaderControls();
                    }
                }
            }
            finally
            {
                _shihabUpdateDialogOpen = false;
            }
        }

        private void PollRssSubscriptionsInBackgroundAsync()
        {
            if (IsDisposed) return;
            if (_mehediConfig.ShihabRssFeeds == null || _mehediConfig.ShihabRssFeeds.Count == 0) return;
            if (Interlocked.CompareExchange(ref _rssPollInFlight, 1, 0) != 0) return;

            Task.Run(delegate
            {
                try
                {
                    List<KeyValuePair<ShihabRssFeedConfig, ShihabRssEntryItem>> matched =
                        new List<KeyValuePair<ShihabRssFeedConfig, ShihabRssEntryItem>>();
                    bool dirty = ShihabRssFeedManager.PollDueFeedsInBackground(_mehediConfig, delegate(ShihabRssFeedConfig sub, ShihabRssEntryItem entry)
                    {
                        if (sub != null && sub.ShihabAutoDownload && entry != null && !string.IsNullOrWhiteSpace(entry.DownloadUrl))
                        {
                            matched.Add(new KeyValuePair<ShihabRssFeedConfig, ShihabRssEntryItem>(sub, entry));
                        }
                    });

                    if (dirty || matched.Count > 0)
                    {
                        SafeShihabUiInvoke(delegate
                        {
                            for (int i = 0; i < matched.Count; i++)
                            {
                                ShihabRssFeedConfig sub = matched[i].Key;
                                ShihabRssEntryItem entry = matched[i].Value;
                                string dlUrl = entry.DownloadUrl.Trim();
                                if (!IsAllowedDownloadSourceUrl(dlUrl)) continue;

                                ShihabDownloadItem rssItem = new ShihabDownloadItem
                                {
                                    MehediUrl = dlUrl,
                                    ShihabDescription = "RSS Auto: " + (entry.Title ?? sub.ShihabFeedTitle ?? ""),
                                    ShihabFileName = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(dlUrl),
                                    MehediMaxConnections = _mehediConfig.ShihabDefaultMaxConnections
                                };
                                if (dlUrl.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase) ||
                                    dlUrl.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                                {
                                    rssItem.ShihabIsTorrent = true;
                                    rssItem.ShihabTorrentMagnetOrPath = dlUrl;
                                    rssItem.ShihabCategoryName = "Torrents";
                                }
                                else
                                {
                                    ShihabCategoryRule cat = _mehediConfig.ResolveMehediCategoryForFile(rssItem.ShihabFileName);
                                    rssItem.ShihabCategoryName = string.IsNullOrWhiteSpace(sub.MehediCategoryName) ? cat.ShihabName : sub.MehediCategoryName;
                                    rssItem.MehediSaveDirectory = ResolveSafeSaveDirectory(sub.ShihabSaveDirectory, cat.ShihabDefaultFolder);
                                }
                                if (string.IsNullOrWhiteSpace(rssItem.MehediSaveDirectory))
                                {
                                    rssItem.MehediSaveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                                }
                                try { Directory.CreateDirectory(rssItem.MehediSaveDirectory); } catch { }
                                rssItem.ShihabFileName = ShihabFormatHelpers.EnsureShihabUniqueFilePath(rssItem.MehediSaveDirectory, rssItem.ShihabFileName);
                                _shihabEngine.AddShihabDownloadItem(rssItem, true);
                            }
                            _shihabStore.SaveMehediConfig(_mehediConfig);
                        });
                    }
                }
                catch { }
                finally
                {
                    Interlocked.Exchange(ref _rssPollInFlight, 0);
                }
            });
        }

        private void HandleQueueFinishedWithActions(MehediQueueConfig queue)
        {
            if (queue == null) return;
            SafeShihabUiInvoke(delegate
            {
                if (IsDisposed) return;
                if (_mehediConfig.MehediHangUpWhenDone)
                {
                    try
                    {
                        string rasdialExe = Path.Combine(Environment.SystemDirectory, "rasdial.exe");
                        ProcessStartInfo rasPsi = new ProcessStartInfo(File.Exists(rasdialExe) ? rasdialExe : "rasdial.exe", "/disconnect")
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using (Process proc = Process.Start(rasPsi)) { }
                    }
                    catch { }
                }
                if (queue.MehediExitAppWhenDone)
                {
                    ExitShihabApplicationCleanly();
                    return;
                }

                string pwr = (queue.ShihabSystemPowerActionWhenDone ?? "None").Trim();
                if (string.IsNullOrEmpty(pwr) || string.Equals(pwr, "None", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                int secondsLeft = 30;
                bool dark = _mehediConfig.ShihabDarkMode;
                using (Form prompt = new Form())
                {
                    prompt.Text = "Queue Post-Completion Action — MHS Download Manager";
                    prompt.ClientSize = new Size(440, 165);
                    prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                    prompt.MaximizeBox = false;
                    prompt.MinimizeBox = false;
                    prompt.StartPosition = FormStartPosition.CenterScreen;
                    prompt.TopMost = true;
                    prompt.BackColor = ShihabThemePalette.GetMehediWindowBg(dark);
                    prompt.ForeColor = ShihabThemePalette.GetShihabPrimaryText(dark);
                    prompt.Font = ShihabThemePalette.UiSemibold92Font;
                    ShihabThemePalette.ApplyMehediNativeDarkFrame(prompt, dark);

                    Label msgLbl = new Label
                    {
                        Location = new Point(18, 18),
                        Size = new Size(404, 72),
                        Text = string.Format("Queue \"{0}\" has finished all downloads.\r\n\r\nSystem action [{1}] will execute in {2} seconds unless canceled.",
                            queue.ShihabQueueName, pwr, secondsLeft)
                    };
                    Button execNowBtn = ShihabThemePalette.CreateMehediStyledButton(pwr + " Now", ShihabThemePalette.GetMehediAccentRed(), Color.White, 130);
                    execNowBtn.Location = new Point(166, 110);
                    execNowBtn.DialogResult = DialogResult.OK;

                    Button cancelBtn = ShihabThemePalette.CreateMehediStyledButton("Cancel Action", ShihabThemePalette.GetShihabSurfaceBg(dark), prompt.ForeColor, 120);
                    cancelBtn.Location = new Point(302, 110);
                    cancelBtn.DialogResult = DialogResult.Cancel;

                    prompt.Controls.AddRange(new Control[] { msgLbl, execNowBtn, cancelBtn });
                    prompt.CancelButton = cancelBtn;

                    using (System.Windows.Forms.Timer countdown = new System.Windows.Forms.Timer { Interval = 1000 })
                    {
                        countdown.Tick += delegate
                        {
                            secondsLeft--;
                            if (secondsLeft <= 0)
                            {
                                countdown.Stop();
                                prompt.DialogResult = DialogResult.OK;
                                prompt.Close();
                                return;
                            }
                            msgLbl.Text = string.Format("Queue \"{0}\" has finished all downloads.\r\n\r\nSystem action [{1}] will execute in {2} seconds unless canceled.",
                                queue.ShihabQueueName, pwr, secondsLeft);
                        };
                        countdown.Start();

                        IWin32Window owner = GetShihabDialogOwnerOrNull();
                        DialogResult ans = owner != null ? prompt.ShowDialog(owner) : prompt.ShowDialog();
                        countdown.Stop();
                        if (ans != DialogResult.OK)
                        {
                            return;
                        }
                    }
                }

                if (string.Equals(pwr, "Sleep", StringComparison.OrdinalIgnoreCase))
                {
                    ShihabOsAndDiskSafety.EnterSystemSleepOrHibernate(false);
                }
                else if (string.Equals(pwr, "Hibernate", StringComparison.OrdinalIgnoreCase))
                {
                    ShihabOsAndDiskSafety.EnterSystemSleepOrHibernate(true);
                }
                else if (string.Equals(pwr, "Shutdown", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        string shutdownExe = Path.Combine(Environment.SystemDirectory, "shutdown.exe");
                        ProcessStartInfo psi = new ProcessStartInfo(File.Exists(shutdownExe) ? shutdownExe : "shutdown.exe", "/s /t 5 /c \"MHS Download Manager queue completed.\"")
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using (Process p = Process.Start(psi)) { }
                        ExitShihabApplicationCleanly();
                    }
                    catch { }
                }
            });
        }

        private void HandleDragDropDataObject(IDataObject data)
        {
            if (data == null) return;
            try
            {
                if (data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = data.GetData(DataFormats.FileDrop) as string[];
                    if (files != null)
                    {
                        for (int i = 0; i < files.Length; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(files[i]))
                            {
                                HandleDroppedTextOrFilePath(files[i]);
                            }
                        }
                    }
                    return;
                }
                string droppedText = (data.GetData(DataFormats.UnicodeText) ?? data.GetData(DataFormats.Text) ?? "").ToString().Trim();
                if (!string.IsNullOrEmpty(droppedText))
                {
                    HandleDroppedTextOrFilePath(droppedText);
                }
            }
            catch { }
        }

        private void HandleDroppedTextOrFilePath(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            string trimmed = raw.Trim();
            if (ShihabProcessAndPathSafety.ContainsControlOrNewline(trimmed)) return;

            if (File.Exists(trimmed))
            {
                string ext = (Path.GetExtension(trimmed) ?? "").ToLowerInvariant();
                if (ext == ".torrent")
                {
                    RestoreAndActivateMainWindow();
                    OpenShihabTorrentDialog(trimmed);
                    return;
                }
                if (ext == ".ef2" || ext == ".txt")
                {
                    List<ShihabDownloadItem> imported = ShihabStateStore.ImportFromMehediEf2OrText(trimmed);
                    for (int i = 0; i < imported.Count; i++)
                    {
                        ShihabDownloadItem it = imported[i];
                        ShihabCategoryRule cat = _mehediConfig.ResolveMehediCategoryForFile(it.ShihabFileName);
                        it.ShihabCategoryName = cat.ShihabName;
                        it.MehediSaveDirectory = cat.ShihabDefaultFolder;
                        it.MehediQueueName = "Main Download Queue";
                        it.SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued");
                        _shihabEngine.AddShihabDownloadItem(it, false);
                    }
                    RefreshMehediDownloadsGrid();
                    return;
                }
            }
            PromptAndAddNewShihabUrl(trimmed);
        }

        private void OnMehediPeriodicTimerTick()
        {
            _shihabEngine.TickMehediQueuesAndScheduler();
            RefreshMehediDownloadsGrid();

            // Fallback polling ONLY if OS clipboard listener registration failed
            if (!_clipboardListenerRegistered && _mehediConfig.ShihabMonitorClipboard)
            {
                PollShihabClipboardForDownloadUrl();
            }
        }

        private void PollShihabClipboardForDownloadUrl()
        {
            if (_shihabClipboardPollInFlight) return;
            _shihabClipboardPollInFlight = true;
            try
            {
                if (!Clipboard.ContainsText()) return;
                string mehediText = Clipboard.GetText().Trim();
                if (string.IsNullOrEmpty(mehediText) || mehediText == _shihabLastClipboardText || mehediText.Length > 2048 ||
                    ShihabProcessAndPathSafety.ContainsControlOrNewline(mehediText))
                {
                    return;
                }
                _shihabLastClipboardText = mehediText;

                if (mehediText.StartsWith("magnet:?xt=urn:btih:", StringComparison.OrdinalIgnoreCase))
                {
                    OpenShihabTorrentDialog(mehediText);
                    return;
                }

                Uri shihabUri;
                if (!Uri.TryCreate(mehediText, UriKind.Absolute, out shihabUri)) return;
                if (shihabUri.Scheme != "http" && shihabUri.Scheme != "https" && shihabUri.Scheme != "ftp" && shihabUri.Scheme != "ftps") return;

                string mehediExt = Path.GetExtension(shihabUri.AbsolutePath).TrimStart('.');
                if (string.IsNullOrEmpty(mehediExt)) return;

                if (_mehediConfig.MatchesShihabAutoCaptureExtension(mehediExt))
                {
                    ShihabCapturedLinkDto shihabClipDto = new ShihabCapturedLinkDto
                    {
                        ShihabUrl = mehediText,
                        ShihabFileName = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(mehediText),
                        ShihabSilentMode = false
                    };
                    HandleBrowserShihabSingleCapture(shihabClipDto);
                }
            }
            catch { }
            finally
            {
                _shihabClipboardPollInFlight = false;
            }
        }

        private static bool IsAllowedDownloadSourceUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || ShihabProcessAndPathSafety.ContainsControlOrNewline(url))
            {
                return false;
            }
            string t = url.Trim();
            if (t.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase)) return true;
            Uri u;
            if (!Uri.TryCreate(t, UriKind.Absolute, out u)) return false;
            return string.Equals(u.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(u.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(u.Scheme, Uri.UriSchemeFtp, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(u.Scheme, "ftps", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveSafeSaveDirectory(string candidateDir, string fallbackDir)
        {
            if (!string.IsNullOrWhiteSpace(candidateDir) && !ShihabProcessAndPathSafety.ContainsControlOrNewline(candidateDir))
            {
                try
                {
                    if (Path.IsPathRooted(candidateDir))
                    {
                        return Path.GetFullPath(candidateDir.Trim());
                    }
                }
                catch { }
            }
            return fallbackDir;
        }

        private IWin32Window GetShihabDialogOwnerOrNull()
        {
            if (Visible && WindowState != FormWindowState.Minimized)
            {
                return this;
            }
            return null;
        }

        private static void PlayShihabConfiguredSound(bool mehediEnabled, string shihabWavPath, bool mehediIsError)
        {
            if (!mehediEnabled) return;
            try
            {
                if (!string.IsNullOrWhiteSpace(shihabWavPath) &&
                    !ShihabProcessAndPathSafety.ContainsControlOrNewline(shihabWavPath) &&
                    File.Exists(shihabWavPath))
                {
                    using (SoundPlayer mehediPlayer = new SoundPlayer(shihabWavPath))
                    {
                        mehediPlayer.Play();
                    }
                    return;
                }
                if (mehediIsError) SystemSounds.Hand.Play();
                else SystemSounds.Asterisk.Play();
            }
            catch { }
        }

        private void HandleBrowserShihabSingleCapture(ShihabCapturedLinkDto mehediDto)
        {
            SafeShihabUiInvoke(delegate
            {
                if (mehediDto == null || string.IsNullOrWhiteSpace(mehediDto.ShihabUrl))
                {
                    return;
                }

                string shihabTrimmedUrl = mehediDto.ShihabUrl.Trim();
                if (!IsAllowedDownloadSourceUrl(shihabTrimmedUrl))
                {
                    return;
                }
                DateTime mehediUtcNow = DateTime.UtcNow;

                List<string> shihabExpiredUrls = null;
                foreach (KeyValuePair<string, DateTime> mehediKv in _mehediRecentCapturedUrlsUtc)
                {
                    if ((mehediUtcNow - mehediKv.Value).TotalSeconds > 30.0)
                    {
                        if (shihabExpiredUrls == null) shihabExpiredUrls = new List<string>();
                        shihabExpiredUrls.Add(mehediKv.Key);
                    }
                }
                if (shihabExpiredUrls != null)
                {
                    for (int shihabExpIdx = 0; shihabExpIdx < shihabExpiredUrls.Count; shihabExpIdx++)
                    {
                        _mehediRecentCapturedUrlsUtc.Remove(shihabExpiredUrls[shihabExpIdx]);
                    }
                }

                DateTime shihabPrevSeenUtc;
                if (_mehediRecentCapturedUrlsUtc.TryGetValue(shihabTrimmedUrl, out shihabPrevSeenUtc) &&
                    (mehediUtcNow - shihabPrevSeenUtc).TotalSeconds < 4.0)
                {
                    return;
                }
                _mehediRecentCapturedUrlsUtc[shihabTrimmedUrl] = mehediUtcNow;

                ShihabDownloadItem shihabNewItem = new ShihabDownloadItem();
                shihabNewItem.MehediUrl = shihabTrimmedUrl;
                shihabNewItem.MehediReferer = mehediDto.MehediReferrer ?? "";
                shihabNewItem.ShihabCookies = mehediDto.ShihabCookies ?? "";
                shihabNewItem.MehediOriginHeader = mehediDto.ShihabOrigin ?? "";
                shihabNewItem.MehediOmitCookies = mehediDto.MehediOmitCookies;
                shihabNewItem.MehediAudioCompanionUrl = mehediDto.ShihabAudioUrl ?? "";
                shihabNewItem.ShihabAudioCompanionBytes = mehediDto.MehediAudioSize > 0 ? mehediDto.MehediAudioSize : -1L;
                shihabNewItem.ShihabSourceTabId = mehediDto.ShihabTabId >= 0 ? mehediDto.ShihabTabId : -1;
                shihabNewItem.ShihabQualityLabel = mehediDto.MehediQualityLabel ?? "";
                shihabNewItem.ShihabPostBody = mehediDto.ShihabPostBody ?? "";
                shihabNewItem.ShihabExpectedChecksum = mehediDto.ShihabExpectedChecksum ?? "";
                shihabNewItem.ShihabIsPrivateIncognito = mehediDto.ShihabIncognito;
                string dtoKind = (mehediDto.ShihabJobKind ?? "").Trim().ToLowerInvariant();
                if (dtoKind == "ytdlp" || dtoKind == "video" || dtoKind == "playlist")
                {
                    shihabNewItem.ShihabIsYtDlpJob = true;
                }
                else if (dtoKind == "gallery")
                {
                    shihabNewItem.ShihabIsGalleryJob = true;
                }
                if (!string.IsNullOrWhiteSpace(mehediDto.ShihabFormatSelector))
                {
                    shihabNewItem.ShihabYtDlpFormatSelector = mehediDto.ShihabFormatSelector.Trim();
                }
                if (mehediDto.ShihabAudioOnly)
                {
                    shihabNewItem.ShihabAudioExtractFormat = "mp3";
                }
                if (shihabTrimmedUrl.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase) ||
                    shihabTrimmedUrl.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                {
                    shihabNewItem.ShihabIsTorrent = true;
                    shihabNewItem.ShihabTorrentMagnetOrPath = shihabTrimmedUrl;
                }
                if (!string.IsNullOrWhiteSpace(mehediDto.MehediUserAgent))
                {
                    shihabNewItem.MehediUserAgent = mehediDto.MehediUserAgent;
                }
                shihabNewItem.ShihabDescription = mehediDto.MehediTabTitle ?? "";
                shihabNewItem.ShihabTotalBytes = mehediDto.ShihabFileSize > 0 ? mehediDto.ShihabFileSize : -1L;
                shihabNewItem.MehediMaxConnections = _mehediConfig.ShihabDefaultMaxConnections;
                shihabNewItem.MehediExitAppOnComplete = mehediDto.MehediExitWhenDone;
                shihabNewItem.ShihabShowCompleteDialog = _mehediConfig.ShihabShowDownloadCompleteDialog && !mehediDto.ShihabSilentMode;

                string mehediRawName = ShihabFormatHelpers.ResolveSmartMehediFileName(
                    mehediDto.ShihabFileName,
                    shihabNewItem.MehediUrl,
                    mehediDto.MehediTabTitle,
                    shihabNewItem.ShihabQualityLabel);
                shihabNewItem.ShihabFileName = mehediRawName;

                ShihabCategoryRule shihabCat = _mehediConfig.ResolveMehediCategoryForFile(mehediRawName);
                shihabNewItem.ShihabCategoryName = shihabCat.ShihabName;
                string mehediTargetDir = ResolveSafeSaveDirectory(mehediDto.ShihabSaveDirectory, shihabCat.ShihabDefaultFolder);
                try { Directory.CreateDirectory(mehediTargetDir); } catch { }
                shihabNewItem.MehediSaveDirectory = mehediTargetDir;
                shihabNewItem.ShihabFileName = (_mehediConfig.MehediDuplicateFileMode == 1)
                    ? ShihabFormatHelpers.SanitizeShihabFileName(mehediRawName)
                    : ShihabFormatHelpers.EnsureShihabUniqueFilePath(mehediTargetDir, mehediRawName);

                if (mehediDto.MehediAddToQueueOnly)
                {
                    shihabNewItem.MehediQueueName = "Main Download Queue";
                    shihabNewItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued");
                    _shihabEngine.AddShihabDownloadItem(shihabNewItem, false);
                    return;
                }

                if (mehediDto.ShihabSilentMode || !_mehediConfig.ShihabShowStartDownloadDialog)
                {
                    _shihabEngine.AddShihabDownloadItem(shihabNewItem, true);
                    if (_mehediConfig.MehediShowDownloadProgressDialog && !mehediDto.ShihabSilentMode)
                    {
                        ShowProgressWindowForShihabItem(shihabNewItem);
                    }
                    return;
                }

                foreach (ShihabDownloadItem mehediQueued in _mehediPendingCaptureModalQueue)
                {
                    if (string.Equals(mehediQueued.MehediUrl, shihabNewItem.MehediUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }
                _mehediPendingCaptureModalQueue.Enqueue(shihabNewItem);
                PumpNextPendingShihabCaptureModal();
            });
        }

        private void PumpNextPendingShihabCaptureModal()
        {
            if (_shihabShowingCaptureModal || IsDisposed)
            {
                return;
            }
            _shihabShowingCaptureModal = true;
            try
            {
                while (_mehediPendingCaptureModalQueue.Count > 0 && !IsDisposed)
                {
                    ShihabDownloadItem shihabNextItem = _mehediPendingCaptureModalQueue.Dequeue();
                    using (MehediDownloadFileInfoForm shihabInfoDlg = new MehediDownloadFileInfoForm(shihabNextItem, _mehediConfig, _shihabEngine))
                    {
                        IWin32Window mehediOwner = GetShihabDialogOwnerOrNull();
                        if (mehediOwner == null) shihabInfoDlg.StartPosition = FormStartPosition.CenterScreen;
                        DialogResult shihabDlgRes = mehediOwner != null ? shihabInfoDlg.ShowDialog(mehediOwner) : shihabInfoDlg.ShowDialog();
                        if (shihabDlgRes == DialogResult.OK)
                        {
                            _shihabStore.SaveMehediConfig(_mehediConfig);
                            _shihabEngine.AddShihabDownloadItem(shihabNextItem, shihabInfoDlg.ShihabStartImmediatelySelected);
                            if (shihabInfoDlg.ShihabStartImmediatelySelected && _mehediConfig.MehediShowDownloadProgressDialog)
                            {
                                ShowProgressWindowForShihabItem(shihabNextItem);
                            }
                        }
                    }
                }
            }
            finally
            {
                _shihabShowingCaptureModal = false;
            }
        }

        private void HandleBrowserMehediBatchCapture(ShihabBatchCaptureDto shihabBatch)
        {
            SafeShihabUiInvoke(delegate
            {
                using (MehediDownloadAllLinksForm mehediDlg = new MehediDownloadAllLinksForm(shihabBatch, _mehediConfig.ShihabDarkMode))
                {
                    IWin32Window shihabOwner = GetShihabDialogOwnerOrNull();
                    DialogResult mehediRes = shihabOwner != null ? mehediDlg.ShowDialog(shihabOwner) : mehediDlg.ShowDialog();
                    if (mehediRes == DialogResult.OK)
                    {
                        for (int shihabI = 0; shihabI < mehediDlg.ShihabChosenLinks.Count; shihabI++)
                        {
                            MehediBatchLinkEntryDto mehediLink = mehediDlg.ShihabChosenLinks[shihabI];
                            EnqueueSimpleShihabUrl(
                                mehediLink.ShihabUrl,
                                shihabBatch.ShihabPageUrl ?? "",
                                shihabBatch.ShihabCookies ?? "",
                                shihabBatch.MehediUserAgent ?? "",
                                mehediLink.MehediText ?? "",
                                mehediDlg.MehediStartNow);
                        }
                    }
                }
            });
        }

        private void HandleShihabItemCompleted(ShihabDownloadItem mehediItem)
        {
            SafeShihabUiInvoke(delegate
            {
                RefreshMehediDownloadsGrid();
                PlayShihabConfiguredSound(_mehediConfig.MehediPlaySoundComplete, _mehediConfig.ShihabSoundCompleteWav, false);
                if (mehediItem.ShihabOpenWhenComplete)
                {
                    try
                    {
                        string shihabTarget = mehediItem.GetShihabFullTargetPath();
                        if (!string.IsNullOrWhiteSpace(shihabTarget) &&
                            !ShihabProcessAndPathSafety.ContainsControlOrNewline(shihabTarget) &&
                            File.Exists(shihabTarget))
                        {
                            using (Process p = Process.Start(new ProcessStartInfo(shihabTarget) { UseShellExecute = true })) { }
                        }
                    }
                    catch { }
                }
                if (mehediItem.MehediExitAppOnComplete)
                {
                    ExitShihabApplicationCleanly();
                    return;
                }
                if (mehediItem.ShihabShowCompleteDialog && _mehediConfig.ShihabShowDownloadCompleteDialog)
                {
                    MehediDownloadCompleteForm shihabDoneForm = new MehediDownloadCompleteForm(mehediItem, _mehediConfig);
                    IWin32Window mehediOwner = GetShihabDialogOwnerOrNull();
                    if (mehediOwner != null) shihabDoneForm.Show(mehediOwner);
                    else shihabDoneForm.Show();
                }
            });
        }

        private void HandleMehediItemFailed(ShihabDownloadItem shihabFailedItem)
        {
            SafeShihabUiInvoke(delegate
            {
                PlayShihabConfiguredSound(_mehediConfig.MehediPlaySoundError, _mehediConfig.ShihabSoundErrorWav, true);
            });
        }

        private void SafeShihabUiInvoke(Action mehediAction)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            try
            {
                if (InvokeRequired) BeginInvoke(mehediAction);
                else mehediAction();
            }
            catch { }
        }

        private void HandleMehediFormClosing(object shihabSender, FormClosingEventArgs mehediArgs)
        {
            if (!_shihabExitingExplicitly && mehediArgs.CloseReason == CloseReason.UserClosing)
            {
                if (_mehediConfig.ShihabCloseToTray)
                {
                    mehediArgs.Cancel = true;
                    MinimizeShihabToBackgroundTray(false);
                    return;
                }
                _shihabExitingExplicitly = true;
            }
            CleanupShihabResources();
        }

        public void ExitShihabApplicationCleanly()
        {
            _shihabExitingExplicitly = true;
            CleanupShihabResources();
            Close();
            Application.Exit();
        }

        private void CleanupShihabResources()
        {
            if (Interlocked.Exchange(ref _cleanedUpFlag, 1) != 0) return;

            try
            {
                if (_shihabEngine != null)
                {
                    if (_engineItemUpdatedHandler != null) _shihabEngine.OnShihabItemUpdated -= _engineItemUpdatedHandler;
                    if (_engineItemCompletedHandler != null) _shihabEngine.OnMehediItemCompleted -= _engineItemCompletedHandler;
                    if (_engineItemFailedHandler != null) _shihabEngine.OnShihabItemFailed -= _engineItemFailedHandler;
                    if (_engineQueueFinishedHandler != null) _shihabEngine.OnQueueFinishedWithActions -= _engineQueueFinishedHandler;
                }
                if (_mehediBridge != null)
                {
                    if (_bridgeSingleCaptureHandler != null) _mehediBridge.OnShihabSingleCaptureReceived -= _bridgeSingleCaptureHandler;
                    if (_bridgeBatchCaptureHandler != null) _mehediBridge.OnMehediBatchCaptureReceived -= _bridgeBatchCaptureHandler;
                    if (_bridgeStartMainQueueHandler != null) _mehediBridge.OnShihabStartMainQueueRequested -= _bridgeStartMainQueueHandler;
                    if (_bridgeShowMainWindowHandler != null) _mehediBridge.OnShihabShowMainWindowRequested -= _bridgeShowMainWindowHandler;
                    if (_bridgeOpenUpdaterHandler != null) _mehediBridge.OnShihabOpenUpdaterRequested -= _bridgeOpenUpdaterHandler;
                    if (_bridgeGracefulExitHandler != null) _mehediBridge.OnShihabGracefulExitRequested -= _bridgeGracefulExitHandler;
                }
                if (_networkRestoredHandler != null)
                {
                    ShihabDnsAndNetworkManager.OnNetworkRestored -= _networkRestoredHandler;
                }
            }
            catch { }

            try { if (_refreshDebounceTimer != null) { _refreshDebounceTimer.Stop(); _refreshDebounceTimer.Dispose(); _refreshDebounceTimer = null; } } catch { }
            try { if (_mehediUiTickTimer != null) { _mehediUiTickTimer.Stop(); _mehediUiTickTimer.Dispose(); _mehediUiTickTimer = null; } } catch { }
            try { if (_shihabPeriodicUpdateTimer != null) { _shihabPeriodicUpdateTimer.Stop(); _shihabPeriodicUpdateTimer.Dispose(); _shihabPeriodicUpdateTimer = null; } } catch { }
            try { if (_shihabRssPollTimer != null) { _shihabRssPollTimer.Stop(); _shihabRssPollTimer.Dispose(); _shihabRssPollTimer = null; } } catch { }
            try { _shihabEngine.StopAllMehediDownloads(); } catch { }
            try { _shihabEngine.PersistShihabState(); } catch { }
            try { _shihabStore.SaveMehediConfig(_mehediConfig); } catch { }
            try { _mehediBridge.Dispose(); } catch { }
            try { if (_shihabRemoteServer != null) _shihabRemoteServer.Dispose(); } catch { }
            try { ShihabLocalNetworkRelay.Stop(); } catch { }
            try
            {
                if (_shihabDropTargetForm != null && !_shihabDropTargetForm.IsDisposed) _shihabDropTargetForm.Close();
            }
            catch { }
            try
            {
                if (_mehediTrayIcon != null)
                {
                    _mehediTrayIcon.Visible = false;
                    _mehediTrayIcon.Dispose();
                    _mehediTrayIcon = null;
                }
            }
            catch { }
            try { ShihabCrashMonitor.MarkCleanShutdown(); } catch { }
        }
    }
}
