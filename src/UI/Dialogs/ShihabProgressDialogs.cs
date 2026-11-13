using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public class ShihabSegmentMapPanel : Panel
    {
        public ShihabDownloadItem MehediBoundItem { get; set; }
        public MehediDynamicSegmentEngine MehediBoundEngine { get; set; }
        public bool ShihabIsOverallBar { get; set; }

        public ShihabSegmentMapPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        private List<MehediSegmentChunk> SnapshotSegmentsSafe()
        {
            if (MehediBoundItem == null) return new List<MehediSegmentChunk>();
            if (MehediBoundEngine != null)
            {
                return MehediBoundEngine.GetItemSegmentsSnapshot(MehediBoundItem);
            }
            List<MehediSegmentChunk> segs = MehediBoundItem.MehediSegments;
            if (segs == null) return new List<MehediSegmentChunk>();
            lock (segs)
            {
                return new List<MehediSegmentChunk>(segs);
            }
        }

        protected override void OnPaint(PaintEventArgs shihabArgs)
        {
            Graphics mehediG = shihabArgs.Graphics;
            mehediG.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle shihabRect = ClientRectangle;
            using (SolidBrush mehediBg = new SolidBrush(Color.FromArgb(24, 27, 38)))
            {
                mehediG.FillRectangle(mehediBg, shihabRect);
            }

            if (MehediBoundItem != null)
            {
                if (ShihabIsOverallBar)
                {
                    double mehediPct = MehediBoundItem.GetMehediPercent();
                    int shihabFillW = (int)Math.Round((shihabRect.Width * mehediPct) / 100.0);
                    if (shihabFillW > 1)
                    {
                        using (LinearGradientBrush mehediGrad = new LinearGradientBrush(
                            new Rectangle(0, 0, Math.Max(2, shihabFillW), Math.Max(2, shihabRect.Height)),
                            Color.FromArgb(56, 189, 248),
                            Color.FromArgb(139, 92, 246),
                            0f))
                        {
                            mehediG.FillRectangle(mehediGrad, 0, 0, shihabFillW, shihabRect.Height);
                        }
                    }
                    string shihabText = string.Format("{0:0.0}%", mehediPct);
                    using (SolidBrush shihabWhite = new SolidBrush(Color.White))
                    {
                        SizeF mehediSz = mehediG.MeasureString(shihabText, ShihabThemePalette.UiSemibold85Font);
                        mehediG.DrawString(
                            shihabText,
                            ShihabThemePalette.UiSemibold85Font,
                            shihabWhite,
                            (shihabRect.Width - mehediSz.Width) / 2f,
                            (shihabRect.Height - mehediSz.Height) / 2f);
                    }
                }
                else if (MehediBoundItem.ShihabTotalBytes > 0)
                {
                    long shihabTotal = MehediBoundItem.ShihabTotalBytes;
                    List<MehediSegmentChunk> mehediSnap = SnapshotSegmentsSafe();
                    if (mehediSnap.Count > 0)
                    {
                        using (SolidBrush shihabFillBrush = new SolidBrush(Color.FromArgb(124, 92, 255)))
                        using (Pen mehediMarkerPen = new Pen(Color.FromArgb(56, 189, 248), 1.5f))
                        {
                            for (int shihabI = 0; shihabI < mehediSnap.Count; shihabI++)
                            {
                                MehediSegmentChunk mehediSeg = mehediSnap[shihabI];
                                if (mehediSeg == null) continue;
                                int shihabX0 = (int)((mehediSeg.MehediStartByte * (long)shihabRect.Width) / shihabTotal);
                                int mehediFillW = (int)((mehediSeg.MehediDownloadedBytes * (long)shihabRect.Width) / shihabTotal);
                                if (mehediSeg.MehediDownloadedBytes > 0 && mehediFillW < 2) mehediFillW = 2;
                                if (mehediFillW > 0)
                                {
                                    mehediG.FillRectangle(shihabFillBrush, shihabX0, 1, mehediFillW, shihabRect.Height - 2);
                                }
                                mehediG.DrawLine(mehediMarkerPen, shihabX0, 0, shihabX0, shihabRect.Height);
                            }
                        }
                    }
                }
            }

            using (Pen shihabBorder = new Pen(Color.FromArgb(42, 48, 68), 1f))
            {
                mehediG.DrawRectangle(shihabBorder, 0, 0, shihabRect.Width - 1, shihabRect.Height - 1);
            }
        }
    }

    public class ShihabDownloadProgressForm : ShihabChromeForm
    {
        protected override bool ShihabResizableChrome
        {
            get { return false; }
        }

        private readonly ShihabDownloadItem _shihabItem;
        private readonly MehediDynamicSegmentEngine _mehediEngine;
        private readonly MehediAppConfiguration _shihabConfig;

        private ShihabChromeTitleBar _shihabTitleBar;
        private ShihabDarkTabControl _shihabTabs;
        private Label _mehediUrlTitleLabel;
        private Label _shihabStatusVal;
        private Label _mehediFileSizeVal;
        private Label _shihabDownloadedVal;
        private Label _mehediRateVal;
        private Label _shihabEtaVal;
        private Label _mehediResumeVal;
        private ShihabSegmentMapPanel _shihabOverallBar;
        private ShihabSegmentMapPanel _mehediSegmentsMapBar;
        private ListView _shihabConnectionsList;
        private Button _mehediPauseResumeBtn;
        private Button _shihabHideDetailsBtn;
        private CheckBox _mehediItemSpeedCheck;
        private NumericUpDown _shihabItemSpeedNum;
        private CheckBox _mehediShowCompleteCheck;
        private CheckBox _shihabOpenOnDoneCheck;
        private CheckBox _mehediExitOnDoneCheck;
        private Timer _shihabUiRefreshTimer;
        private bool _mehediDetailsExpanded = true;

        public ShihabDownloadProgressForm(
            ShihabDownloadItem shihabItem,
            MehediDynamicSegmentEngine mehediEngine,
            MehediAppConfiguration shihabConfig)
        {
            _shihabItem = shihabItem;
            _mehediEngine = mehediEngine;
            _shihabConfig = shihabConfig;
            bool mehediDark = shihabConfig.ShihabDarkMode;

            Text = string.Format("{0:0.0}% — {1}", shihabItem.GetMehediPercent(), shihabItem.ShihabFileName);
            Size = new Size(596, 474);
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = ShihabThemePalette.GetMehediWindowBg(mehediDark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(mehediDark);
            Font = ShihabThemePalette.UiRegular9Font;
            Color progressBorder = ShihabThemePalette.GetShihabBorder(mehediDark);
            ApplyShihabChromeTheme(mehediDark, progressBorder);
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, mehediDark);
            Icon = ShihabThemePalette.CreateShihabAppIcon();

            _shihabTitleBar = new ShihabChromeTitleBar(
                this,
                false,
                true,
                delegate { Close(); },
                "Minimize to Background — keep download running in background");
            _shihabTitleBar.ApplyTheme(
                mehediDark,
                mehediDark ? Color.FromArgb(22, 24, 30) : Color.FromArgb(240, 244, 249),
                mehediDark ? Color.FromArgb(36, 40, 50) : Color.FromArgb(214, 222, 234));

            Panel shihabContentHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BackColor,
                ForeColor = ForeColor
            };

            _shihabTabs = new ShihabDarkTabControl
            {
                ShihabDarkMode = mehediDark,
                Location = new Point(10, 8),
                Size = new Size(574, 424)
            };

            TabPage mehediStatusTab = new TabPage("Download status")
            {
                BackColor = ShihabThemePalette.GetMehediWindowBg(mehediDark),
                ForeColor = ForeColor
            };
            TabPage shihabLimiterTab = new TabPage("Speed Limiter")
            {
                BackColor = ShihabThemePalette.GetMehediWindowBg(mehediDark),
                ForeColor = ForeColor
            };
            TabPage mehediCompletionTab = new TabPage("Options on completion")
            {
                BackColor = ShihabThemePalette.GetMehediWindowBg(mehediDark),
                ForeColor = ForeColor
            };

            BuildMehediStatusTab(mehediStatusTab, mehediDark);
            BuildShihabLimiterTab(shihabLimiterTab);
            BuildMehediCompletionTab(mehediCompletionTab);

            _shihabTabs.TabPages.Add(mehediStatusTab);
            _shihabTabs.TabPages.Add(shihabLimiterTab);
            _shihabTabs.TabPages.Add(mehediCompletionTab);
            shihabContentHost.Controls.Add(_shihabTabs);
            Controls.Add(shihabContentHost);
            Controls.Add(_shihabTitleBar);

            _shihabUiRefreshTimer = new Timer { Interval = 250 };
            _shihabUiRefreshTimer.Tick += delegate { RefreshShihabProgressView(); };
            _shihabUiRefreshTimer.Start();

            FormClosed += delegate
            {
                _shihabUiRefreshTimer.Stop();
                _shihabUiRefreshTimer.Dispose();
            };

            RefreshShihabProgressView();
        }

        private void BuildMehediStatusTab(TabPage shihabPage, bool mehediDark)
        {
            _mehediUrlTitleLabel = new Label
            {
                Text = _shihabItem.MehediUrl,
                Location = new Point(14, 12),
                Size = new Size(535, 20),
                AutoEllipsis = true,
                Font = ShihabThemePalette.UiSemibold9Font,
                ForeColor = ShihabThemePalette.GetShihabAccentBlue()
            };

            int shihabLeftX = 14;
            int mehediValX = 155;
            _shihabStatusVal = AddShihabMetricRow(shihabPage, "Status:", shihabLeftX, mehediValX, 38);
            _mehediFileSizeVal = AddShihabMetricRow(shihabPage, "File size:", shihabLeftX, mehediValX, 60);
            _shihabDownloadedVal = AddShihabMetricRow(shihabPage, "Downloaded:", shihabLeftX, mehediValX, 82);
            _mehediRateVal = AddShihabMetricRow(shihabPage, "Transfer rate:", shihabLeftX, mehediValX, 104);
            _shihabEtaVal = AddShihabMetricRow(shihabPage, "Time left:", shihabLeftX, mehediValX, 126);
            _mehediResumeVal = AddShihabMetricRow(shihabPage, "Resume capability:", shihabLeftX, mehediValX, 148);

            _shihabOverallBar = new ShihabSegmentMapPanel
            {
                MehediBoundItem = _shihabItem,
                MehediBoundEngine = _mehediEngine,
                ShihabIsOverallBar = true,
                Location = new Point(14, 174),
                Size = new Size(536, 22)
            };

            _shihabHideDetailsBtn = ShihabThemePalette.CreateMehediStyledButton("<< Hide details", ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor, 130);
            _shihabHideDetailsBtn.Location = new Point(14, 204);
            _shihabHideDetailsBtn.Click += delegate
            {
                _mehediDetailsExpanded = !_mehediDetailsExpanded;
                _shihabHideDetailsBtn.Text = _mehediDetailsExpanded ? "<< Hide details" : "Show details >>";
                _mehediSegmentsMapBar.Visible = _mehediDetailsExpanded;
                _shihabConnectionsList.Visible = _mehediDetailsExpanded;
                if (_shihabTabs != null) _shihabTabs.Height = _mehediDetailsExpanded ? 424 : 268;
                Height = _mehediDetailsExpanded ? 474 : 318;
            };

            _mehediPauseResumeBtn = ShihabThemePalette.CreateMehediStyledButton("Pause", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 112);
            _mehediPauseResumeBtn.Location = new Point(316, 204);
            _mehediPauseResumeBtn.Click += delegate
            {
                if (_mehediEngine.IsMehediItemActive(_shihabItem.ShihabId))
                {
                    _mehediEngine.PauseShihabDownload(_shihabItem);
                }
                else if (_shihabItem.GetShihabStatusEnum() != ShihabDownloadStatus.ShihabCompleted)
                {
                    _mehediEngine.StartOrResumeMehediDownload(_shihabItem);
                }
                RefreshShihabProgressView();
            };

            Button shihabCancelBtn = ShihabThemePalette.CreateMehediStyledButton("Cancel", ShihabThemePalette.GetMehediAccentRed(), Color.White, 112);
            shihabCancelBtn.Location = new Point(438, 204);
            shihabCancelBtn.Click += delegate
            {
                if (_mehediEngine.IsMehediItemActive(_shihabItem.ShihabId))
                {
                    _mehediEngine.PauseShihabDownload(_shihabItem);
                }
                Close();
            };

            Label mehediConnHeader = new Label
            {
                Text = "Start positions and download progress by connections (Dynamic Segmentation):",
                Location = new Point(14, 244),
                AutoSize = true,
                ForeColor = ShihabThemePalette.GetMehediMutedText(mehediDark)
            };

            _mehediSegmentsMapBar = new ShihabSegmentMapPanel
            {
                MehediBoundItem = _shihabItem,
                MehediBoundEngine = _mehediEngine,
                ShihabIsOverallBar = false,
                Location = new Point(14, 265),
                Size = new Size(536, 18)
            };

            _shihabConnectionsList = new ShihabModernListView
            {
                Location = new Point(14, 289),
                Size = new Size(536, 98),
                MehediDarkMode = mehediDark,
                ShihabShowFileTypeBadges = false,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark),
                ForeColor = ForeColor
            };
            _shihabConnectionsList.Columns.Add("N.", 50);
            _shihabConnectionsList.Columns.Add("Byte Range", 160);
            _shihabConnectionsList.Columns.Add("Downloaded", 115);
            _shihabConnectionsList.Columns.Add("Info", 190);

            shihabPage.Controls.AddRange(new Control[]
            {
                _mehediUrlTitleLabel, _shihabOverallBar, _shihabHideDetailsBtn,
                _mehediPauseResumeBtn, shihabCancelBtn, mehediConnHeader,
                _mehediSegmentsMapBar, _shihabConnectionsList
            });
        }

        private Label AddShihabMetricRow(TabPage mehediPage, string shihabTitle, int mehediX1, int shihabX2, int mehediY)
        {
            Label shihabKey = new Label { Text = shihabTitle, Location = new Point(mehediX1, mehediY), AutoSize = true };
            Label mehediVal = new Label
            {
                Text = "-",
                Location = new Point(shihabX2, mehediY),
                Size = new Size(390, 18),
                AutoEllipsis = true,
                Font = ShihabThemePalette.UiSemibold9Font
            };
            mehediPage.Controls.Add(shihabKey);
            mehediPage.Controls.Add(mehediVal);
            return mehediVal;
        }

        private void BuildShihabLimiterTab(TabPage mehediPage)
        {
            _mehediItemSpeedCheck = new CheckBox
            {
                Text = "Use Speed Limiter for this download",
                Location = new Point(24, 30),
                AutoSize = true,
                Checked = _shihabItem.ShihabUseItemSpeedLimit
            };

            Label shihabMaxLbl = new Label
            {
                Text = "Maximum download speed for this file (KBytes/sec):",
                Location = new Point(24, 68),
                AutoSize = true
            };

            _shihabItemSpeedNum = new NumericUpDown
            {
                Location = new Point(330, 65),
                Width = 110,
                Minimum = 8,
                Maximum = 1000000,
                Value = Math.Max(8, _shihabItem.MehediItemSpeedLimitKb)
            };

            _mehediItemSpeedCheck.CheckedChanged += delegate
            {
                _shihabItem.ShihabUseItemSpeedLimit = _mehediItemSpeedCheck.Checked;
            };
            _shihabItemSpeedNum.ValueChanged += delegate
            {
                _shihabItem.MehediItemSpeedLimitKb = (int)_shihabItemSpeedNum.Value;
            };

            Label mehediNote = new Label
            {
                Text = "Note: Per-download Speed Limiter throttles all active segments of this download smoothly using Shihab Mehedi token-bucket pacing.",
                Location = new Point(24, 112),
                Size = new Size(510, 50)
            };

            mehediPage.Controls.AddRange(new Control[] { _mehediItemSpeedCheck, shihabMaxLbl, _shihabItemSpeedNum, mehediNote });
        }

        private void BuildMehediCompletionTab(TabPage shihabPage)
        {
            _mehediShowCompleteCheck = new CheckBox
            {
                Text = "Show Download Complete dialog",
                Location = new Point(24, 30),
                AutoSize = true,
                Checked = _shihabItem.ShihabShowCompleteDialog
            };
            _shihabOpenOnDoneCheck = new CheckBox
            {
                Text = "Open downloaded file automatically when done",
                Location = new Point(24, 64),
                AutoSize = true,
                Checked = _shihabItem.ShihabOpenWhenComplete
            };
            _mehediExitOnDoneCheck = new CheckBox
            {
                Text = "Exit MHS Download Manager when this download completes",
                Location = new Point(24, 98),
                AutoSize = true,
                Checked = _shihabItem.MehediExitAppOnComplete
            };

            _mehediShowCompleteCheck.CheckedChanged += delegate { _shihabItem.ShihabShowCompleteDialog = _mehediShowCompleteCheck.Checked; };
            _shihabOpenOnDoneCheck.CheckedChanged += delegate { _shihabItem.ShihabOpenWhenComplete = _shihabOpenOnDoneCheck.Checked; };
            _mehediExitOnDoneCheck.CheckedChanged += delegate { _shihabItem.MehediExitAppOnComplete = _mehediExitOnDoneCheck.Checked; };

            shihabPage.Controls.AddRange(new Control[] { _mehediShowCompleteCheck, _shihabOpenOnDoneCheck, _mehediExitOnDoneCheck });
        }

        private void RefreshShihabProgressView()
        {
            if (IsDisposed) return;

            ShihabDownloadStatus mehediSt = _shihabItem.GetShihabStatusEnum();
            string nextProgressTitle = string.Format("{0:0.0}% — {1}", _shihabItem.GetMehediPercent(), _shihabItem.ShihabFileName);
            if (!string.Equals(Text, nextProgressTitle, StringComparison.Ordinal))
            {
                Text = nextProgressTitle;
            }
            _shihabStatusVal.Text = _shihabItem.ShihabStatusText;
            _mehediFileSizeVal.Text = ShihabFormatHelpers.FormatMehediBytes(_shihabItem.ShihabTotalBytes);
            _shihabDownloadedVal.Text = string.Format("{0} ({1:0.0}%)",
                ShihabFormatHelpers.FormatMehediBytes(_shihabItem.MehediDownloadedBytes),
                _shihabItem.GetMehediPercent());

            string shihabSpd = ShihabFormatHelpers.FormatShihabSpeed(_shihabItem.ShihabCurrentSpeedBps);
            if (!string.IsNullOrEmpty(shihabSpd) && (_shihabItem.ShihabUseItemSpeedLimit || _shihabConfig.MehediGlobalSpeedLimiterEnabled))
            {
                shihabSpd += " (Limited)";
            }
            bool mehediActive = _mehediEngine.IsMehediItemActive(_shihabItem.ShihabId);
            _mehediRateVal.Text = string.IsNullOrEmpty(shihabSpd) ? "-" : shihabSpd;
            _shihabEtaVal.Text = (mehediActive && _shihabItem.MehediEtaSeconds > 0)
                ? ShihabFormatHelpers.FormatMehediTimeLeft(_shihabItem.MehediEtaSeconds)
                : "-";
            string shihabGeoInfo = ShihabGeoIpLookup.FormatCountryBadgeText(_shihabItem.ShihabServerCountryCode, _shihabItem.ShihabServerIp);
            _mehediResumeVal.Text = (_shihabItem.ShihabResumeSupported ? "Yes" : "No") +
                (!string.IsNullOrEmpty(shihabGeoInfo) ? ("   •   Server: " + shihabGeoInfo) : "");
            _mehediResumeVal.ForeColor = _shihabItem.ShihabResumeSupported
                ? ShihabThemePalette.GetMehediAccentGreen()
                : ShihabThemePalette.GetShihabAccentAmber();

            _mehediPauseResumeBtn.Text = mehediActive ? "Pause" : "Start";
            _mehediPauseResumeBtn.Enabled = mehediSt != ShihabDownloadStatus.ShihabCompleted;

            _shihabOverallBar.Invalidate();
            _mehediSegmentsMapBar.Invalidate();

            List<MehediSegmentChunk> shihabSegs = _mehediEngine.GetItemSegmentsSnapshot(_shihabItem);

            _shihabConnectionsList.BeginUpdate();
            while (_shihabConnectionsList.Items.Count < shihabSegs.Count)
            {
                _shihabConnectionsList.Items.Add(new ListViewItem(new string[] { "", "", "", "" }));
            }
            while (_shihabConnectionsList.Items.Count > shihabSegs.Count)
            {
                _shihabConnectionsList.Items.RemoveAt(_shihabConnectionsList.Items.Count - 1);
            }
            for (int mehediI = 0; mehediI < shihabSegs.Count; mehediI++)
            {
                MehediSegmentChunk shihabSeg = shihabSegs[mehediI];
                ListViewItem mehediLvi = _shihabConnectionsList.Items[mehediI];
                mehediLvi.SubItems[0].Text = shihabSeg.ShihabSegmentIndex.ToString();
                mehediLvi.SubItems[1].Text = shihabSeg.ShihabEndByte >= 0
                    ? string.Format("{0} - {1}", ShihabFormatHelpers.FormatMehediBytes(shihabSeg.MehediStartByte), ShihabFormatHelpers.FormatMehediBytes(shihabSeg.ShihabEndByte))
                    : "Stream";
                mehediLvi.SubItems[2].Text = ShihabFormatHelpers.FormatMehediBytes(shihabSeg.MehediDownloadedBytes);
                mehediLvi.SubItems[3].Text = shihabSeg.ShihabSegmentState ?? "";
            }
            _shihabConnectionsList.EndUpdate();

            if (mehediSt == ShihabDownloadStatus.ShihabCompleted)
            {
                _shihabUiRefreshTimer.Stop();
                Close();
            }
        }
    }

    public class MehediDownloadCompleteForm : Form
    {
        private readonly Font _bannerFont;

        public MehediDownloadCompleteForm(ShihabDownloadItem shihabItem, MehediAppConfiguration mehediConfig)
        {
            bool shihabDark = mehediConfig.ShihabDarkMode;
            Text = "Download complete — MHS Download Manager";
            Size = new Size(590, 285);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            BackColor = ShihabThemePalette.GetMehediWindowBg(shihabDark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(shihabDark);
            Font = ShihabThemePalette.UiRegular9Font;
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, shihabDark);
            Icon = ShihabThemePalette.CreateShihabAppIcon();

            string mehediFullFile = shihabItem.GetShihabFullTargetPath();

            _bannerFont = new Font("Segoe UI Semibold", 11f);
            Label shihabBanner = new Label
            {
                Text = "Download Complete — " + ShihabFormatHelpers.FormatMehediBytes(shihabItem.MehediDownloadedBytes),
                Location = new Point(18, 16),
                AutoSize = true,
                Font = _bannerFont,
                ForeColor = ShihabThemePalette.GetMehediAccentGreen()
            };

            Label mehediAddrLbl = new Label { Text = "Address:", Location = new Point(18, 52), AutoSize = true };
            TextBox shihabAddrBox = new TextBox
            {
                Location = new Point(105, 49),
                Width = 450,
                ReadOnly = true,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark),
                ForeColor = ForeColor,
                Text = shihabItem.MehediUrl
            };

            Label shihabSavedLbl = new Label { Text = "Saved as:", Location = new Point(18, 88), AutoSize = true };
            TextBox mehediSavedBox = new TextBox
            {
                Location = new Point(105, 85),
                Width = 450,
                ReadOnly = true,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark),
                ForeColor = ForeColor,
                Text = mehediFullFile
            };

            Label shihabHashLbl = new Label
            {
                Text = "SHA-256: " + (string.IsNullOrEmpty(shihabItem.MehediSha256Hex) ? "-" : shihabItem.MehediSha256Hex),
                Location = new Point(18, 124),
                Size = new Size(540, 20),
                AutoEllipsis = true,
                ForeColor = ShihabThemePalette.GetMehediMutedText(shihabDark)
            };

            CheckBox mehediDontShow = new CheckBox
            {
                Text = "Don't show this dialog again",
                Location = new Point(18, 155),
                AutoSize = true
            };
            mehediDontShow.CheckedChanged += delegate
            {
                mehediConfig.ShihabShowDownloadCompleteDialog = !mehediDontShow.Checked;
            };

            Button shihabOpenBtn = ShihabThemePalette.CreateMehediStyledButton("Open", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 115);
            shihabOpenBtn.Location = new Point(18, 195);
            shihabOpenBtn.Click += delegate
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(mehediFullFile) &&
                        !ShihabProcessAndPathSafety.ContainsControlOrNewline(mehediFullFile) &&
                        File.Exists(mehediFullFile))
                    {
                        using (Process p = Process.Start(new ProcessStartInfo(mehediFullFile) { UseShellExecute = true })) { }
                    }
                }
                catch { }
                Close();
            };

            Button mehediOpenWithBtn = ShihabThemePalette.CreateMehediStyledButton("Open With...", ShihabThemePalette.GetShihabSurfaceBg(shihabDark), ForeColor, 125);
            mehediOpenWithBtn.Location = new Point(145, 195);
            mehediOpenWithBtn.Click += delegate
            {
                ShihabThemePalette.SafeOpenWithDialog(mehediFullFile);
                Close();
            };

            Button shihabFolderBtn = ShihabThemePalette.CreateMehediStyledButton("Open Folder", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 130);
            shihabFolderBtn.Location = new Point(282, 195);
            shihabFolderBtn.Click += delegate
            {
                ShihabThemePalette.SafeOpenContainingFolderOrFile(mehediFullFile, shihabItem.MehediSaveDirectory);
                Close();
            };

            Button mehediCloseBtn = ShihabThemePalette.CreateMehediStyledButton("Close", ShihabThemePalette.GetShihabSurfaceBg(shihabDark), ForeColor, 115);
            mehediCloseBtn.Location = new Point(440, 195);
            mehediCloseBtn.Click += delegate { Close(); };

            Controls.AddRange(new Control[]
            {
                shihabBanner, mehediAddrLbl, shihabAddrBox, shihabSavedLbl, mehediSavedBox,
                shihabHashLbl, mehediDontShow, shihabOpenBtn, mehediOpenWithBtn, shihabFolderBtn, mehediCloseBtn
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _bannerFont != null)
            {
                _bannerFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

