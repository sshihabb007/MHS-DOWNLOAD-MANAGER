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
                else if (MehediBoundItem.ShihabIsTorrent)
                {
                    ShihabTorrentLiveSnapshot tSnap = ShihabTorrentLiveTelemetry.GetSnapshot(MehediBoundItem.ShihabId);
                    if (tSnap != null && tSnap.PieceStates != null && tSnap.PieceStates.Length > 0)
                    {
                        int pc = tSnap.PieceStates.Length;
                        using (SolidBrush doneBrush = new SolidBrush(Color.FromArgb(16, 185, 129)))
                        using (SolidBrush flightBrush = new SolidBrush(Color.FromArgb(56, 189, 248)))
                        {
                            for (int px = 0; px < shihabRect.Width; px++)
                            {
                                int pIdx = (int)(((long)px * pc) / Math.Max(1, shihabRect.Width));
                                if (pIdx >= pc) pIdx = pc - 1;
                                byte st = tSnap.PieceStates[pIdx];
                                if (st == 2) mehediG.FillRectangle(doneBrush, px, 1, 1, shihabRect.Height - 2);
                                else if (st == 1) mehediG.FillRectangle(flightBrush, px, 1, 1, shihabRect.Height - 2);
                            }
                        }
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

    public sealed class ShihabSpeedUnitSwitcher : Control
    {
        private bool _darkMode = true;
        private bool _useBitsPerSec = false;
        private int _hoveredSegment = -1; // 0 = MBps, 1 = Mbps
        private Font _cachedPillFont;
        private float _cachedPillFontPx;

        protected override void Dispose(bool disposing)
        {
            if (disposing && _cachedPillFont != null)
            {
                _cachedPillFont.Dispose();
                _cachedPillFont = null;
            }
            base.Dispose(disposing);
        }

        public bool ShihabDarkMode
        {
            get { return _darkMode; }
            set
            {
                if (_darkMode != value)
                {
                    _darkMode = value;
                    Invalidate();
                }
            }
        }

        public bool UseBitsPerSec
        {
            get { return _useBitsPerSec; }
            set
            {
                if (_useBitsPerSec != value)
                {
                    _useBitsPerSec = value;
                    Invalidate();
                }
            }
        }

        public event EventHandler UnitChanged;

        public ShihabSpeedUnitSwitcher()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);
            Size = new Size(106, 22);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        private void SelectSegment(bool bitsMode)
        {
            if (_useBitsPerSec != bitsMode)
            {
                _useBitsPerSec = bitsMode;
                Invalidate();
                EventHandler handler = UnitChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int seg = e.X < (Width / 2) ? 0 : 1;
            if (seg != _hoveredSegment)
            {
                _hoveredSegment = seg;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredSegment != -1)
            {
                _hoveredSegment = -1;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            bool clickedBits = e.X >= (Width / 2);
            // Clicking active side toggles cleanly; clicking opposite side selects it directly
            bool nextMode = (clickedBits == _useBitsPerSec) ? !_useBitsPerSec : clickedBits;
            SelectSegment(nextMode);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Left || keyData == Keys.Right) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left)
            {
                SelectSegment(false);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                SelectSegment(true);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                SelectSegment(!_useBitsPerSec);
                e.Handled = true;
            }
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 8 || Height < 8) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color parentBg = Parent != null ? Parent.BackColor : ShihabThemePalette.GetMehediWindowBg(_darkMode);
            using (SolidBrush clearBr = new SolidBrush(parentBg))
            {
                g.FillRectangle(clearBr, ClientRectangle);
            }

            Rectangle outer = new Rectangle(0, 0, Math.Max(4, Width - 1), Math.Max(4, Height - 1));
            Color trackBg = ShihabThemePalette.GetShihabSurfaceBg(_darkMode);
            Color borderCol = Focused
                ? ShihabThemePalette.GetShihabAccentBlue()
                : ShihabThemePalette.GetShihabBorder(_darkMode);

            using (GraphicsPath trackPath = ShihabThemePalette.CreateShihabRoundedRect(outer, Math.Min(10, Math.Max(4, Height / 2))))
            using (SolidBrush trackBr = new SolidBrush(trackBg))
            using (Pen borderPen = new Pen(borderCol, 1f))
            {
                g.FillPath(trackBr, trackPath);
                g.DrawPath(borderPen, trackPath);
            }

            int halfW = Math.Max(4, (Width - 4) / 2);
            int rightW = Math.Max(4, Width - 4 - halfW);
            int pillH = Math.Max(4, Height - 4);
            int innerRadius = Math.Min(8, Math.Max(3, pillH / 2));
            Rectangle leftRect = new Rectangle(2, 2, halfW, pillH);
            Rectangle rightRect = new Rectangle(2 + halfW, 2, rightW, pillH);
            Rectangle activeRect = _useBitsPerSec ? rightRect : leftRect;
            Rectangle inactiveRect = _useBitsPerSec ? leftRect : rightRect;
            bool hoverInactive = (_useBitsPerSec && _hoveredSegment == 0) || (!_useBitsPerSec && _hoveredSegment == 1);

            if (hoverInactive)
            {
                Color hoverCol = _darkMode
                    ? ControlPaint.Light(trackBg, 0.14f)
                    : ControlPaint.Dark(trackBg, 0.06f);
                using (GraphicsPath hoverPath = ShihabThemePalette.CreateShihabRoundedRect(inactiveRect, innerRadius))
                using (SolidBrush hoverBr = new SolidBrush(hoverCol))
                {
                    g.FillPath(hoverBr, hoverPath);
                }
            }

            using (GraphicsPath activePath = ShihabThemePalette.CreateShihabRoundedRect(activeRect, innerRadius))
            using (LinearGradientBrush pillGrad = new LinearGradientBrush(
                activeRect,
                Color.FromArgb(56, 189, 248),
                Color.FromArgb(124, 92, 255),
                0f))
            {
                g.FillPath(pillGrad, activePath);
            }

            Color activeText = Color.White;
            Color inactiveText = hoverInactive
                ? ShihabThemePalette.GetShihabPrimaryText(_darkMode)
                : ShihabThemePalette.GetMehediMutedText(_darkMode);

            // Pixel-sized font ensures "MBps" / "Mbps" never clip inside fixed-height pills under PerMonitorV2 125%–200% DPI
            float targetPx = Math.Max(8.5f, Math.Min(11.0f, pillH * 0.62f));
            if (_cachedPillFont == null || Math.Abs(_cachedPillFontPx - targetPx) > 0.1f)
            {
                if (_cachedPillFont != null) _cachedPillFont.Dispose();
                _cachedPillFont = new Font("Segoe UI", targetPx, FontStyle.Bold, GraphicsUnit.Pixel);
                _cachedPillFontPx = targetPx;
            }

            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(g, "MBps", _cachedPillFont, leftRect, _useBitsPerSec ? inactiveText : activeText, flags);
            TextRenderer.DrawText(g, "Mbps", _cachedPillFont, rightRect, _useBitsPerSec ? activeText : inactiveText, flags);
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
        private ShihabSpeedUnitSwitcher _shihabSpeedUnitSwitcher;
        private Label _shihabRateLimitVal;
        private ToolTip _shihabProgressTip;
        private bool _useBitsSpeedUnit;
        private bool _lastConfigDefaultBitsUnit;
        private Label _shihabEtaVal;
        private Label _mehediResumeVal;
        private ShihabSegmentMapPanel _shihabOverallBar;
        private ShihabSegmentMapPanel _mehediSegmentsMapBar;
        private ListView _shihabConnectionsList;
        private ListView _torrentFilesList;
        private ListView _torrentPeersList;
        private ListView _torrentTrackersList;
        private Button _mehediPauseResumeBtn;
        private Button _shihabStopLiveBtn;
        private Button _shihabHideDetailsBtn;
        private CheckBox _mehediItemSpeedCheck;
        private NumericUpDown _shihabItemSpeedNum;
        private CheckBox _mehediShowCompleteCheck;
        private CheckBox _shihabOpenOnDoneCheck;
        private CheckBox _mehediExitOnDoneCheck;
        private TextBox _shihabDeferredRenameBox;
        private Label _shihabDeferredRenameStatus;
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
            _lastConfigDefaultBitsUnit = ShihabFormatHelpers.IsBitsSpeedUnit(shihabConfig != null ? shihabConfig.ShihabDefaultSpeedUnit : null);
            _useBitsSpeedUnit = _lastConfigDefaultBitsUnit;

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
            if (_shihabItem.ShihabIsTorrent)
            {
                TabPage tFilesTab = new TabPage("Files") { BackColor = BackColor, ForeColor = ForeColor };
                TabPage tPeersTab = new TabPage("Peers") { BackColor = BackColor, ForeColor = ForeColor };
                TabPage tTrackersTab = new TabPage("Trackers") { BackColor = BackColor, ForeColor = ForeColor };
                BuildTorrentInspectorTabs(tFilesTab, tPeersTab, tTrackersTab, mehediDark);
                _shihabTabs.TabPages.Add(tFilesTab);
                _shihabTabs.TabPages.Add(tPeersTab);
                _shihabTabs.TabPages.Add(tTrackersTab);
            }
            shihabContentHost.Controls.Add(_shihabTabs);
            Controls.Add(shihabContentHost);
            Controls.Add(_shihabTitleBar);

            _shihabUiRefreshTimer = new Timer { Interval = 250 };
            _shihabUiRefreshTimer.Tick += delegate { RefreshShihabProgressView(); };
            _shihabUiRefreshTimer.Start();

            FormClosed += delegate
            {
                if (_shihabUiRefreshTimer != null)
                {
                    _shihabUiRefreshTimer.Stop();
                }
            };

            RefreshShihabProgressView();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_shihabUiRefreshTimer != null)
                {
                    try { _shihabUiRefreshTimer.Stop(); } catch { }
                    _shihabUiRefreshTimer.Dispose();
                    _shihabUiRefreshTimer = null;
                }
                if (_shihabProgressTip != null)
                {
                    _shihabProgressTip.Dispose();
                    _shihabProgressTip = null;
                }
            }
            base.Dispose(disposing);
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
            _mehediRateVal.Location = new Point(mehediValX, 102);
            _mehediRateVal.Size = new Size(122, 22);
            _mehediRateVal.TextAlign = ContentAlignment.MiddleLeft;
            _mehediRateVal.AutoEllipsis = true;

            _shihabSpeedUnitSwitcher = new ShihabSpeedUnitSwitcher
            {
                ShihabDarkMode = mehediDark,
                UseBitsPerSec = _useBitsSpeedUnit,
                Location = new Point(280, 102),
                Size = new Size(106, 22)
            };
            _shihabSpeedUnitSwitcher.UnitChanged += delegate
            {
                _useBitsSpeedUnit = _shihabSpeedUnitSwitcher.UseBitsPerSec;
                RefreshShihabProgressView();
            };

            _shihabRateLimitVal = new Label
            {
                Text = "",
                Location = new Point(392, 102),
                Size = new Size(158, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Font = ShihabThemePalette.UiRegular9Font,
                ForeColor = ShihabThemePalette.GetMehediMutedText(mehediDark)
            };

            _shihabProgressTip = new ToolTip();
            _shihabProgressTip.SetToolTip(
                _shihabSpeedUnitSwitcher,
                "Switch transfer speed unit between MBps (Bytes/sec) and Mbps (bits/sec). Change default anytime in Options > General.");

            shihabPage.Controls.Add(_shihabSpeedUnitSwitcher);
            shihabPage.Controls.Add(_shihabRateLimitVal);

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

            _shihabStopLiveBtn = ShihabThemePalette.CreateMehediStyledButton("Stop & Save MP4", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 138);
            _shihabStopLiveBtn.Location = new Point(162, 204);
            _shihabStopLiveBtn.Visible = _shihabItem.ShihabIsYtDlpJob;
            _shihabStopLiveBtn.Click += delegate
            {
                _shihabItem.ShihabStopLiveAndSaveRequested = true;
                _shihabStopLiveBtn.Enabled = false;
                _shihabStopLiveBtn.Text = "Saving MP4...";
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
                _shihabStopLiveBtn, _mehediPauseResumeBtn, shihabCancelBtn, mehediConnHeader,
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

            Label renameLbl = new Label
            {
                Text = "Rename file automatically when download completes (deferred rename):",
                Location = new Point(24, 146),
                AutoSize = true
            };
            _shihabDeferredRenameBox = new TextBox
            {
                Location = new Point(24, 170),
                Width = 370,
                Text = string.IsNullOrWhiteSpace(_shihabItem.ShihabDeferredRenameTarget) ? _shihabItem.ShihabFileName : _shihabItem.ShihabDeferredRenameTarget,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(_shihabConfig.ShihabDarkMode),
                ForeColor = ForeColor
            };
            Button applyRenameBtn = ShihabThemePalette.CreateMehediStyledButton("Apply Name", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 118);
            applyRenameBtn.Height = 25;
            applyRenameBtn.Location = new Point(402, 169);
            _shihabDeferredRenameStatus = new Label
            {
                Text = string.IsNullOrWhiteSpace(_shihabItem.ShihabDeferredRenameTarget) ? "" : ("Scheduled rename -> " + _shihabItem.ShihabDeferredRenameTarget),
                Location = new Point(24, 200),
                Size = new Size(500, 20),
                ForeColor = ShihabThemePalette.GetMehediAccentGreen()
            };
            applyRenameBtn.Click += delegate
            {
                string target = ShihabFormatHelpers.SanitizeShihabFileName((_shihabDeferredRenameBox.Text ?? "").Trim());
                if (string.IsNullOrWhiteSpace(target)) return;
                _shihabItem.ShihabDeferredRenameTarget = target;
                _shihabDeferredRenameStatus.Text = "Scheduled rename -> " + target;
            };

            shihabPage.Controls.AddRange(new Control[]
            {
                _mehediShowCompleteCheck, _shihabOpenOnDoneCheck, _mehediExitOnDoneCheck,
                renameLbl, _shihabDeferredRenameBox, applyRenameBtn, _shihabDeferredRenameStatus
            });
        }

        private void BuildTorrentInspectorTabs(TabPage filesPage, TabPage peersPage, TabPage trackersPage, bool dark)
        {
            Color surf = ShihabThemePalette.GetShihabSurfaceBg(dark);

            _torrentFilesList = new ShihabModernListView
            {
                Location = new Point(10, 10),
                Size = new Size(544, 326),
                MehediDarkMode = dark,
                ShihabShowFileTypeBadges = true,
                BackColor = surf,
                ForeColor = ForeColor
            };
            _torrentFilesList.Columns.Add("#", 40);
            _torrentFilesList.Columns.Add("File Path", 290);
            _torrentFilesList.Columns.Add("Size", 95);
            _torrentFilesList.Columns.Add("Progress", 95);

            Button recheckBtn = ShihabThemePalette.CreateMehediStyledButton("Force Recheck Pieces (SHA-1)", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 210);
            recheckBtn.Location = new Point(10, 346);
            recheckBtn.Click += delegate
            {
                _mehediEngine.PauseShihabDownload(_shihabItem);
                _shihabItem.ShihabTorrentForceRecheck = true;
                _mehediEngine.StartOrResumeMehediDownload(_shihabItem);
            };
            filesPage.Controls.AddRange(new Control[] { _torrentFilesList, recheckBtn });

            _torrentPeersList = new ShihabModernListView
            {
                Location = new Point(10, 10),
                Size = new Size(544, 368),
                MehediDarkMode = dark,
                ShihabShowFileTypeBadges = false,
                BackColor = surf,
                ForeColor = ForeColor
            };
            _torrentPeersList.Columns.Add("Peer Endpoint", 150);
            _torrentPeersList.Columns.Add("Client", 130);
            _torrentPeersList.Columns.Add("Dir/Flags", 75);
            _torrentPeersList.Columns.Add("Downloaded", 90);
            _torrentPeersList.Columns.Add("Uploaded", 85);
            peersPage.Controls.Add(_torrentPeersList);

            _torrentTrackersList = new ShihabModernListView
            {
                Location = new Point(10, 10),
                Size = new Size(544, 368),
                MehediDarkMode = dark,
                ShihabShowFileTypeBadges = false,
                BackColor = surf,
                ForeColor = ForeColor
            };
            _torrentTrackersList.Columns.Add("Tracker URL", 260);
            _torrentTrackersList.Columns.Add("Status", 135);
            _torrentTrackersList.Columns.Add("Peers", 60);
            _torrentTrackersList.Columns.Add("S / L", 70);
            trackersPage.Controls.Add(_torrentTrackersList);
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

            if (_shihabSpeedUnitSwitcher != null && _shihabConfig != null)
            {
                _shihabSpeedUnitSwitcher.ShihabDarkMode = _shihabConfig.ShihabDarkMode;
            }
            if (_shihabRateLimitVal != null && _shihabConfig != null)
            {
                _shihabRateLimitVal.ForeColor = ShihabThemePalette.GetMehediMutedText(_shihabConfig.ShihabDarkMode);
            }
            bool cfgDefaultBits = ShihabFormatHelpers.IsBitsSpeedUnit(_shihabConfig != null ? _shihabConfig.ShihabDefaultSpeedUnit : null);
            if (cfgDefaultBits != _lastConfigDefaultBitsUnit)
            {
                _lastConfigDefaultBitsUnit = cfgDefaultBits;
                _useBitsSpeedUnit = cfgDefaultBits;
                if (_shihabSpeedUnitSwitcher != null)
                {
                    _shihabSpeedUnitSwitcher.UseBitsPerSec = _useBitsSpeedUnit;
                }
            }

            string shihabSpd = ShihabFormatHelpers.FormatShihabSpeed(_shihabItem.ShihabCurrentSpeedBps, _useBitsSpeedUnit, false);
            int effectiveLimitKb = 0;
            if (_shihabItem.ShihabUseItemSpeedLimit && _shihabItem.MehediItemSpeedLimitKb > 0)
            {
                effectiveLimitKb = _shihabItem.MehediItemSpeedLimitKb;
            }
            if (_shihabConfig.MehediGlobalSpeedLimiterEnabled && _shihabConfig.ShihabGlobalSpeedLimitKb > 0)
            {
                effectiveLimitKb = effectiveLimitKb > 0
                    ? Math.Min(effectiveLimitKb, _shihabConfig.ShihabGlobalSpeedLimitKb)
                    : _shihabConfig.ShihabGlobalSpeedLimitKb;
            }
            if (_shihabRateLimitVal != null)
            {
                _shihabRateLimitVal.Text = (!string.IsNullOrEmpty(shihabSpd) && effectiveLimitKb > 0)
                    ? string.Format("(limit: {0})", ShihabFormatHelpers.FormatSpeedLimitCap(effectiveLimitKb, _useBitsSpeedUnit))
                    : "";
            }
            bool mehediActive = _mehediEngine.IsMehediItemActive(_shihabItem.ShihabId);
            if (_shihabStopLiveBtn != null)
            {
                _shihabStopLiveBtn.Visible = _shihabItem.ShihabIsYtDlpJob && mehediActive;
            }
            _mehediRateVal.Text = string.IsNullOrEmpty(shihabSpd) ? "-" : shihabSpd;
            _shihabEtaVal.Text = (mehediActive && _shihabItem.MehediEtaSeconds > 0)
                ? ShihabFormatHelpers.FormatMehediTimeLeft(_shihabItem.MehediEtaSeconds)
                : "-";
            string shihabGeoInfo = ShihabGeoIpLookup.FormatCountryBadgeText(_shihabItem.ShihabServerCountryCode, _shihabItem.ShihabServerIp);
            if (_shihabItem.ShihabIsTorrent)
            {
                ShihabTorrentLiveSnapshot tSnap = ShihabTorrentLiveTelemetry.GetSnapshot(_shihabItem.ShihabId);
                string upStr = ShihabFormatHelpers.FormatShihabSpeed(_shihabItem.ShihabTorrentUploadSpeedBps, _useBitsSpeedUnit, false);
                double ratio = _shihabItem.ShihabTotalBytes > 0 ? ((double)_shihabItem.ShihabTorrentUploadedBytes / (double)_shihabItem.ShihabTotalBytes) : 0.0;
                _mehediResumeVal.Text = string.Format("Yes (SHA-1 Verified)  •  Peers: {0}  •  Up: {1}  •  Ratio: {2:0.00}",
                    _shihabItem.ShihabTorrentActivePeers,
                    string.IsNullOrEmpty(upStr) ? ShihabFormatHelpers.FormatZeroSpeed(_useBitsSpeedUnit, true) : upStr,
                    ratio);
                _mehediResumeVal.ForeColor = ShihabThemePalette.GetMehediAccentGreen();
                RefreshTorrentInspectorLists(tSnap);
            }
            else
            {
                _mehediResumeVal.Text = (_shihabItem.ShihabResumeSupported ? "Yes" : "No") +
                    (!string.IsNullOrEmpty(shihabGeoInfo) ? ("   •   Server: " + shihabGeoInfo) : "");
                _mehediResumeVal.ForeColor = _shihabItem.ShihabResumeSupported
                    ? ShihabThemePalette.GetMehediAccentGreen()
                    : ShihabThemePalette.GetShihabAccentAmber();
            }

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

        private void RefreshTorrentInspectorLists(ShihabTorrentLiveSnapshot snap)
        {
            if (snap == null) return;
            if (_torrentFilesList != null)
            {
                _torrentFilesList.BeginUpdate();
                try
                {
                    while (_torrentFilesList.Items.Count < snap.Files.Count)
                        _torrentFilesList.Items.Add(new ListViewItem(new string[] { "", "", "", "" }));
                    while (_torrentFilesList.Items.Count > snap.Files.Count)
                        _torrentFilesList.Items.RemoveAt(_torrentFilesList.Items.Count - 1);
                    for (int i = 0; i < snap.Files.Count; i++)
                    {
                        ShihabTorrentFileProgressTelemetry f = snap.Files[i];
                        double pct = f.SizeBytes > 0 ? Math.Min(100.0, (f.CompletedBytes * 100.0) / f.SizeBytes) : (f.Selected ? 100.0 : 0.0);
                        ListViewItem lvi = _torrentFilesList.Items[i];
                        lvi.SubItems[0].Text = f.Index.ToString();
                        lvi.SubItems[1].Text = f.RelativePath;
                        lvi.SubItems[2].Text = ShihabFormatHelpers.FormatMehediBytes(f.SizeBytes);
                        lvi.SubItems[3].Text = f.Selected ? string.Format("{0:0.0}%", pct) : "Skipped";
                    }
                }
                finally { _torrentFilesList.EndUpdate(); }
            }
            if (_torrentPeersList != null)
            {
                _torrentPeersList.BeginUpdate();
                try
                {
                    while (_torrentPeersList.Items.Count < snap.Peers.Count)
                        _torrentPeersList.Items.Add(new ListViewItem(new string[] { "", "", "", "", "" }));
                    while (_torrentPeersList.Items.Count > snap.Peers.Count)
                        _torrentPeersList.Items.RemoveAt(_torrentPeersList.Items.Count - 1);
                    for (int i = 0; i < snap.Peers.Count; i++)
                    {
                        ShihabTorrentPeerTelemetry p = snap.Peers[i];
                        ListViewItem lvi = _torrentPeersList.Items[i];
                        lvi.SubItems[0].Text = p.Endpoint;
                        lvi.SubItems[1].Text = p.ClientName;
                        lvi.SubItems[2].Text = p.Direction + " " + p.Flags;
                        lvi.SubItems[3].Text = ShihabFormatHelpers.FormatMehediBytes(p.DownloadedBytes);
                        lvi.SubItems[4].Text = ShihabFormatHelpers.FormatMehediBytes(p.UploadedBytes);
                    }
                }
                finally { _torrentPeersList.EndUpdate(); }
            }
            if (_torrentTrackersList != null)
            {
                _torrentTrackersList.BeginUpdate();
                try
                {
                    while (_torrentTrackersList.Items.Count < snap.Trackers.Count)
                        _torrentTrackersList.Items.Add(new ListViewItem(new string[] { "", "", "", "" }));
                    while (_torrentTrackersList.Items.Count > snap.Trackers.Count)
                        _torrentTrackersList.Items.RemoveAt(_torrentTrackersList.Items.Count - 1);
                    for (int i = 0; i < snap.Trackers.Count; i++)
                    {
                        ShihabTorrentTrackerTelemetry t = snap.Trackers[i];
                        ListViewItem lvi = _torrentTrackersList.Items[i];
                        lvi.SubItems[0].Text = t.Url;
                        lvi.SubItems[1].Text = t.Status;
                        lvi.SubItems[2].Text = t.PeersReturned.ToString();
                        lvi.SubItems[3].Text = string.Format("{0} / {1}", t.Seeders >= 0 ? t.Seeders.ToString() : "-", t.Leechers >= 0 ? t.Leechers.ToString() : "-");
                    }
                }
                finally { _torrentTrackersList.EndUpdate(); }
            }
        }
    }

    public class MehediDownloadCompleteForm : Form
    {
        private readonly Font _bannerFont;

        public MehediDownloadCompleteForm(ShihabDownloadItem shihabItem, MehediAppConfiguration mehediConfig, Action saveConfigCallback = null)
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
                if (saveConfigCallback != null)
                {
                    try { saveConfigCallback(); } catch { }
                }
            };

            Label snoozeLbl = new Label { Text = "Snooze popups:", Location = new Point(255, 157), AutoSize = true };
            ComboBox snoozeCombo = new ComboBox
            {
                Location = new Point(352, 153),
                Width = 203,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark),
                ForeColor = ForeColor
            };
            snoozeCombo.Items.AddRange(new object[] { "No snooze", "Snooze 30 minutes", "Snooze 1 hour", "Snooze 8 hours", "Until restart" });
            snoozeCombo.SelectedIndex = 0;
            snoozeCombo.SelectedIndexChanged += delegate
            {
                if (snoozeCombo.SelectedIndex == 0) mehediConfig.ShihabCompleteDialogSnoozeUntilUtc = "";
                else if (snoozeCombo.SelectedIndex == 1) mehediConfig.ShihabCompleteDialogSnoozeUntilUtc = DateTime.UtcNow.AddMinutes(30).ToString("o");
                else if (snoozeCombo.SelectedIndex == 2) mehediConfig.ShihabCompleteDialogSnoozeUntilUtc = DateTime.UtcNow.AddHours(1).ToString("o");
                else if (snoozeCombo.SelectedIndex == 3) mehediConfig.ShihabCompleteDialogSnoozeUntilUtc = DateTime.UtcNow.AddHours(8).ToString("o");
                else if (snoozeCombo.SelectedIndex == 4) mehediConfig.ShihabCompleteDialogSnoozeUntilUtc = "RESTART";
                if (saveConfigCallback != null)
                {
                    try { saveConfigCallback(); } catch { }
                }
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
                shihabHashLbl, mehediDontShow, snoozeLbl, snoozeCombo, shihabOpenBtn, mehediOpenWithBtn, shihabFolderBtn, mehediCloseBtn
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

