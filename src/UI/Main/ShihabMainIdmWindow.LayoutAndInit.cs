using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ShihabMehediIDM
{
    public partial class ShihabMainIdmWindow : ShihabChromeForm
    {
        private const int WM_CLIPBOARDUPDATE = 0x031D;

        protected override bool ShihabResizableChrome
        {
            get { return true; }
        }

        private static readonly Regex SafeRepoSlugRegex = new Regex(
            "^[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$",
            RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));

        [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMehediCueBannerMessage(IntPtr shihabHwnd, int mehediMsg, IntPtr shihabWParam, string mehediLParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private readonly ShihabStateStore _shihabStore;
        private readonly MehediAppConfiguration _mehediConfig;
        private readonly MehediDynamicSegmentEngine _shihabEngine;
        private readonly MehediBrowserBridgeServer _mehediBridge;
        private readonly ShihabCliParsedArguments _shihabStartupCli;
        private readonly Dictionary<string, ShihabDownloadProgressForm> _mehediOpenProgressForms;

        private ShihabChromeTitleBar _shihabTitleBar;
        private MenuStrip _shihabMenuBar;
        private ToolStripMenuItem _tasksTopMenu;
        private ToolStripMenuItem _fileTopMenu;
        private ToolStripMenuItem _downloadsTopMenu;
        private ToolStripMenuItem _viewTopMenu;
        private ToolStripMenuItem _helpTopMenu;
        private ToolStripMenuItem _registrationTopMenu;
        private ShihabTopIconRibbonControl _shihabTopRibbonBar;
        private ContextMenuStrip _shihabStartQueuePopupMenu;
        private ContextMenuStrip _shihabStopQueuePopupMenu;
        private ContextMenuStrip _mehediOverflowAppMenu;
        private Panel _shihabSidebarPanel;
        private Panel _mehediSidebarTopHeading;
        private ShihabTransfersNavControl _shihabTransfersNav;
        private ShihabLocalStorageCardControl _mehediStorageCard;

        private Panel _shihabRightWorkspacePanel;
        private Panel _mehediTopActionHeaderBar;
        private string _shihabHeaderViewTitle = "All Downloads";
        private string _mehediHeaderItemsBadgeText = "0 items";

        private ShihabHeaderPillButton _shihabTorrentPillBtn;
        private ShihabHeaderPillButton _shihabMediaPillBtn;
        private ShihabHeaderPillButton _shihabRssPillBtn;
        private ShihabHeaderPillButton _mehediFolderPillBtn;
        private ShihabHeaderPillButton _shihabRemotePillBtn;
        private ShihabHeaderPillButton _shihabUpdatePillBtn;
        private ShihabHeaderPillButton _mehediMenuPillBtn;
        private ShihabRemoteControlServer _shihabRemoteServer;

        private ShihabRoundedCardPanel _shihabSearchBarPanel;
        private TextBox _mehediSearchBox;
        private Button _shihabClearSearchBtn;

        private ShihabModernListView _shihabDownloadsListView;
        private ShihabTransferFooterBarControl _mehediFooterBar;

        private NotifyIcon _mehediTrayIcon;
        private ContextMenuStrip _shihabTrayMenuRef;
        private MehediFloatingDropTargetForm _shihabDropTargetForm;
        private Timer _mehediUiTickTimer;
        private Timer _shihabPeriodicUpdateTimer;
        private Timer _shihabRssPollTimer;
        private Timer _refreshDebounceTimer;

        private Font _sidebarHeadingFont;
        private Font _headerTitleFont;
        private Font _headerBadgeFont;
        private Font _searchBoxFont;
        private Font _scaledUiFont;

        private ShihabUpdateCheckResult _shihabLatestDetectedUpdate;
        private bool _shihabUpdateCheckInFlight = false;
        private bool _shihabUpdateDialogOpen = false;
        private int _rssPollInFlight;
        private int _drivePollInFlight;
        private int _engineRefreshPending;
        private int _cleanedUpFlag;
        private bool _shihabClipboardPollInFlight = false;
        private bool _clipboardListenerRegistered = false;
        private DateTime _lastGridRefreshUtc = DateTime.MinValue;

        private string _shihabActiveTreeFilter = "ALL";
        private string _mehediSortColumnKey = "ORDER";
        private bool _shihabSortAscending = false;
        private string _shihabLastClipboardText = "";
        private bool _mehediBridgeListening = false;
        private bool _shihabExitingExplicitly = false;
        private DateTime _mehediLastDrivePollUtc = DateTime.MinValue;
        private double _shihabCachedUsedGb = 0.0;
        private double _mehediCachedTotalGb = 0.0;
        private double _shihabCachedFreeGb = 0.0;
        private readonly Queue<ShihabDownloadItem> _mehediPendingCaptureModalQueue = new Queue<ShihabDownloadItem>();
        private bool _shihabShowingCaptureModal = false;
        private readonly Dictionary<string, DateTime> _mehediRecentCapturedUrlsUtc = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        private Action<ShihabDownloadItem> _engineItemUpdatedHandler;
        private Action<ShihabDownloadItem> _engineItemCompletedHandler;
        private Action<ShihabDownloadItem> _engineItemFailedHandler;
        private Action<MehediQueueConfig> _engineQueueFinishedHandler;
        private Action<ShihabCapturedLinkDto> _bridgeSingleCaptureHandler;
        private Action<ShihabBatchCaptureDto> _bridgeBatchCaptureHandler;
        private Action _bridgeStartMainQueueHandler;
        private Action _bridgeShowMainWindowHandler;
        private Action _bridgeOpenUpdaterHandler;
        private Action _bridgeGracefulExitHandler;
        private Action _networkRestoredHandler;

        public ShihabMainIdmWindow(
            ShihabStateStore shihabStore,
            MehediAppConfiguration mehediConfig,
            MehediDynamicSegmentEngine shihabEngine,
            MehediBrowserBridgeServer mehediBridge,
            ShihabCliParsedArguments shihabStartupCli)
        {
            _shihabStore = shihabStore;
            _mehediConfig = mehediConfig;
            _shihabEngine = shihabEngine;
            _mehediBridge = mehediBridge;
            _shihabStartupCli = shihabStartupCli;
            _mehediOpenProgressForms = new Dictionary<string, ShihabDownloadProgressForm>();
            try
            {
                if (Clipboard.ContainsText())
                {
                    _shihabLastClipboardText = (Clipboard.GetText() ?? "").Trim();
                }
            }
            catch { }

            _sidebarHeadingFont = new Font("Segoe UI", 7.8f, FontStyle.Bold);
            _headerTitleFont = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            _headerBadgeFont = new Font("Segoe UI Semibold", 8.0f, FontStyle.Regular);
            _searchBoxFont = new Font("Segoe UI", 8.8f);
            _scaledUiFont = new Font("Segoe UI", 9.0f);

            Text = ShihabThemePalette.ShihabMainHeaderTitle;
            Size = new Size(1180, 690);
            MinimumSize = new Size(930, 540);
            StartPosition = FormStartPosition.CenterScreen;
            Font = _scaledUiFont;
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            KeyPreview = true;
            AllowDrop = true;
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, _mehediConfig.ShihabDarkMode);

            BuildShihabLayoutControls();
            ApplyMehediTheme();
            PopulateShihabCategoryTree();
            RefreshMehediDownloadsGrid();

            _bridgeSingleCaptureHandler = HandleBrowserShihabSingleCapture;
            _bridgeBatchCaptureHandler = HandleBrowserMehediBatchCapture;
            _bridgeStartMainQueueHandler = delegate
            {
                SafeShihabUiInvoke(delegate { _shihabEngine.StartShihabQueue("Main Download Queue"); });
            };
            _bridgeShowMainWindowHandler = delegate
            {
                SafeShihabUiInvoke(delegate { RestoreAndActivateMainWindow(); });
            };
            _bridgeOpenUpdaterHandler = delegate
            {
                SafeShihabUiInvoke(delegate
                {
                    RestoreAndActivateMainWindow();
                    if (_shihabLatestDetectedUpdate != null && _shihabLatestDetectedUpdate.UpdateAvailable)
                    {
                        OpenShihabAutoUpdateDialog(_shihabLatestDetectedUpdate);
                    }
                    else
                    {
                        TriggerShihabUpdateCheck(true);
                    }
                });
            };
            _bridgeGracefulExitHandler = delegate
            {
                SafeShihabUiInvoke(delegate { ExitShihabApplicationCleanly(); });
            };

            _mehediBridgeListening = _mehediBridge.StartShihabBridge();
            _mehediBridge.OnShihabSingleCaptureReceived += _bridgeSingleCaptureHandler;
            _mehediBridge.OnMehediBatchCaptureReceived += _bridgeBatchCaptureHandler;
            _mehediBridge.OnShihabStartMainQueueRequested += _bridgeStartMainQueueHandler;
            _mehediBridge.OnShihabShowMainWindowRequested += _bridgeShowMainWindowHandler;
            _mehediBridge.OnShihabOpenUpdaterRequested += _bridgeOpenUpdaterHandler;
            _mehediBridge.OnShihabGracefulExitRequested += _bridgeGracefulExitHandler;

            _engineItemUpdatedHandler = OnEngineItemUpdated;
            _engineItemCompletedHandler = HandleShihabItemCompleted;
            _engineItemFailedHandler = HandleMehediItemFailed;
            _engineQueueFinishedHandler = HandleQueueFinishedWithActions;

            _shihabEngine.OnShihabItemUpdated += _engineItemUpdatedHandler;
            _shihabEngine.OnMehediItemCompleted += _engineItemCompletedHandler;
            _shihabEngine.OnShihabItemFailed += _engineItemFailedHandler;
            _shihabEngine.OnQueueFinishedWithActions += _engineQueueFinishedHandler;

            _shihabRemoteServer = new ShihabRemoteControlServer(_shihabStore, _mehediConfig, _shihabEngine);
            if (_mehediConfig.ShihabRemoteWebEnabled)
            {
                _shihabRemoteServer.Start();
            }

            _networkRestoredHandler = delegate
            {
                if (_mehediConfig.ShihabAutoResumeOnNetworkRestore)
                {
                    SafeShihabUiInvoke(delegate
                    {
                        _shihabEngine.StartShihabQueue("Main Download Queue");
                    });
                }
                if (_mehediConfig.ShihabAutoCheckUpdates &&
                    (ShihabAutoUpdater.LastCachedUpdate == null || !ShihabAutoUpdater.LastCachedUpdate.Ok))
                {
                    SafeShihabUiInvoke(delegate
                    {
                        TriggerShihabUpdateCheck(false);
                    });
                }
            };
            ShihabDnsAndNetworkManager.OnNetworkRestored += _networkRestoredHandler;

            if (_shihabStartupCli != null && _shihabStartupCli.ShihabStartMinimized)
            {
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
            }

            _refreshDebounceTimer = new Timer { Interval = 80 };
            _refreshDebounceTimer.Tick += delegate
            {
                _refreshDebounceTimer.Stop();
                System.Threading.Interlocked.Exchange(ref _engineRefreshPending, 0);
                RefreshMehediDownloadsGrid();
            };

            _mehediUiTickTimer = new Timer { Interval = 650 };
            _mehediUiTickTimer.Tick += delegate { OnMehediPeriodicTimerTick(); };
            _mehediUiTickTimer.Start();

            Resize += delegate
            {
                if (WindowState == FormWindowState.Minimized && _mehediConfig.ShihabMinimizeToTray && Visible)
                {
                    MinimizeShihabToBackgroundTray(false);
                }
            };

            DragEnter += delegate(object s, DragEventArgs e)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop) ||
                    e.Data.GetDataPresent(DataFormats.UnicodeText) ||
                    e.Data.GetDataPresent(DataFormats.Text))
                {
                    e.Effect = DragDropEffects.Copy;
                }
            };
            DragDrop += delegate(object s, DragEventArgs e)
            {
                HandleDragDropDataObject(e.Data);
            };

            KeyDown += delegate(object shihabS, KeyEventArgs mehediE)
            {
                if ((mehediE.Control && mehediE.KeyCode == Keys.F) || mehediE.KeyCode == Keys.F3)
                {
                    _mehediSearchBox.Focus();
                    _mehediSearchBox.SelectAll();
                    mehediE.Handled = true;
                }
                else if (mehediE.Control && mehediE.KeyCode == Keys.N)
                {
                    PromptAndAddNewShihabUrl("");
                    mehediE.Handled = true;
                }
                else if (mehediE.Control && mehediE.KeyCode == Keys.U)
                {
                    TriggerShihabUpdateCheck(true);
                    mehediE.Handled = true;
                }
                else if (mehediE.Control && mehediE.Shift && mehediE.KeyCode == Keys.B)
                {
                    MinimizeShihabToBackgroundTray(true);
                    mehediE.Handled = true;
                }
                else if (mehediE.Control && mehediE.KeyCode == Keys.A && !_mehediSearchBox.Focused)
                {
                    _shihabDownloadsListView.BeginUpdate();
                    try
                    {
                        foreach (ListViewItem it in _shihabDownloadsListView.Items) it.Selected = true;
                    }
                    finally
                    {
                        _shihabDownloadsListView.EndUpdate();
                    }
                    mehediE.Handled = true;
                }
                else if (mehediE.KeyCode == Keys.Delete && !_mehediSearchBox.Focused)
                {
                    PromptAndDeleteSelectedDownloads(mehediE.Shift);
                    mehediE.Handled = true;
                }
                else if (mehediE.Alt && mehediE.KeyCode == Keys.Enter && !_mehediSearchBox.Focused)
                {
                    OpenSelectedShihabProperties();
                    mehediE.Handled = true;
                }
                else if (mehediE.KeyCode == Keys.Enter && !_mehediSearchBox.Focused)
                {
                    HandleDoubleClickOnShihabRow();
                    mehediE.Handled = true;
                }
                else if (mehediE.KeyCode == Keys.F2 && !_mehediSearchBox.Focused)
                {
                    OpenSelectedShihabProperties();
                    mehediE.Handled = true;
                }
            };

            Shown += delegate
            {
                ResizeShihabFileNameColumn();
                RepositionShihabHeaderControls();
                OnShihabMainFormFirstShown();
            };
            FormClosing += HandleMehediFormClosing;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                _clipboardListenerRegistered = AddClipboardFormatListener(Handle);
            }
            catch
            {
                _clipboardListenerRegistered = false;
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (_clipboardListenerRegistered)
            {
                try { RemoveClipboardFormatListener(Handle); } catch { }
                _clipboardListenerRegistered = false;
            }
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_CLIPBOARDUPDATE && _mehediConfig != null && _mehediConfig.ShihabMonitorClipboard)
            {
                PollShihabClipboardForDownloadUrl();
            }
            base.WndProc(ref m);
        }

        private void BuildShihabLayoutControls()
        {
            _shihabTitleBar = new ShihabChromeTitleBar(
                this,
                true,
                true,
                delegate { MinimizeShihabToBackgroundTray(true); },
                "Minimize to Background (System Tray) — downloads & browser capture keep running (Ctrl+Shift+B)");

            _shihabStartQueuePopupMenu = new ContextMenuStrip();
            _shihabStartQueuePopupMenu.Opening += delegate { PopulateStartQueueMenuItems(_shihabStartQueuePopupMenu.Items); };

            _shihabStopQueuePopupMenu = new ContextMenuStrip();
            _shihabStopQueuePopupMenu.Opening += delegate { PopulateStopQueueMenuItems(_shihabStopQueuePopupMenu.Items); };

            _shihabMenuBar = BuildShihabTopMenuStrip();
            MainMenuStrip = _shihabMenuBar;

            _shihabTopRibbonBar = BuildShihabTopIconRibbon();
            _mehediOverflowAppMenu = BuildMehediFullAppOverflowMenu();

            // Left Sidebar (224px wide, #121418, right 1px border #232730)
            _shihabSidebarPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 224,
                Padding = new Padding(0, 0, 1, 0),
                Visible = _mehediConfig.MehediShowCategoriesPanel
            };
            _shihabSidebarPanel.Paint += delegate(object shihabSender, PaintEventArgs mehediE)
            {
                Color shihabDiv = _mehediConfig.ShihabDarkMode ? Color.FromArgb(35, 39, 48) : Color.FromArgb(206, 214, 224);
                using (Pen mehediDivPen = new Pen(shihabDiv, 1f))
                {
                    mehediE.Graphics.DrawLine(mehediDivPen, _shihabSidebarPanel.Width - 1, 0, _shihabSidebarPanel.Width - 1, _shihabSidebarPanel.Height);
                }
            };

            _mehediSidebarTopHeading = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36
            };
            _mehediSidebarTopHeading.Paint += delegate(object shihabSender, PaintEventArgs mehediE)
            {
                mehediE.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                Color shihabHdrCol = _mehediConfig.ShihabDarkMode ? Color.FromArgb(104, 112, 131) : Color.FromArgb(100, 116, 139);
                TextRenderer.DrawText(
                    mehediE.Graphics,
                    "TRANSFERS",
                    _sidebarHeadingFont ?? Font,
                    new Rectangle(16, 11, _mehediSidebarTopHeading.Width - 24, 20),
                    shihabHdrCol,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            };

            _mehediStorageCard = new ShihabLocalStorageCardControl
            {
                Dock = DockStyle.Bottom,
                Height = 92
            };
            _mehediStorageCard.OnMehediSpeedToggleClicked += delegate
            {
                _mehediConfig.MehediGlobalSpeedLimiterEnabled = !_mehediConfig.MehediGlobalSpeedLimiterEnabled;
                _shihabStore.SaveMehediConfig(_mehediConfig);
                UpdateShihabStatusBadges();
            };

            _shihabTransfersNav = new ShihabTransfersNavControl
            {
                Dock = DockStyle.Fill,
                MehediActiveTag = "ALL"
            };
            _shihabTransfersNav.OnShihabFilterSelected += delegate
            {
                _shihabActiveTreeFilter = _shihabTransfersNav.MehediActiveTag;
                _shihabHeaderViewTitle = _shihabTransfersNav.GetActiveDisplayTitle();
                RefreshMehediDownloadsGrid();
            };

            _shihabSidebarPanel.Controls.Add(_shihabTransfersNav);
            _shihabSidebarPanel.Controls.Add(_mehediStorageCard);
            _shihabSidebarPanel.Controls.Add(_mehediSidebarTopHeading);

            // Right Workspace (#16181d)
            _shihabRightWorkspacePanel = new Panel
            {
                Dock = DockStyle.Fill
            };

            // Workspace Sub-Header Bar (42px tall: View Title + Item Count + Non-Duplicate Power Pills + Filter Box)
            _mehediTopActionHeaderBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42
            };
            _mehediTopActionHeaderBar.Paint += delegate(object shihabSender, PaintEventArgs mehediE)
            {
                Graphics shihabG = mehediE.Graphics;
                shihabG.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                shihabG.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                Color mehediBotLine = _mehediConfig.ShihabDarkMode ? Color.FromArgb(35, 39, 48) : Color.FromArgb(206, 214, 224);
                using (Pen shihabBotPen = new Pen(mehediBotLine, 1f))
                {
                    shihabG.DrawLine(shihabBotPen, 0, _mehediTopActionHeaderBar.Height - 1, _mehediTopActionHeaderBar.Width, _mehediTopActionHeaderBar.Height - 1);
                }

                Color shihabTitleCol = _mehediConfig.ShihabDarkMode ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42);
                Font titleFont = _headerTitleFont ?? Font;
                Font badgeFont = _headerBadgeFont ?? Font;

                Size mehediTitleSz = TextRenderer.MeasureText(shihabG, _shihabHeaderViewTitle, titleFont, new Size(340, 28), TextFormatFlags.NoPadding);
                Rectangle shihabTitleRect = new Rectangle(14, 0, mehediTitleSz.Width + 4, _mehediTopActionHeaderBar.Height - 1);
                TextRenderer.DrawText(
                    shihabG,
                    _shihabHeaderViewTitle,
                    titleFont,
                    shihabTitleRect,
                    shihabTitleCol,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);

                Size shihabBadgeSz = TextRenderer.MeasureText(shihabG, _mehediHeaderItemsBadgeText, badgeFont, new Size(160, 22), TextFormatFlags.NoPadding);
                int mehediBadgeW = shihabBadgeSz.Width + 14;
                Rectangle shihabBadgeRect = new Rectangle(shihabTitleRect.Right + 8, (_mehediTopActionHeaderBar.Height - 22) / 2, mehediBadgeW, 22);
                Color mehediBadgeFill = _mehediConfig.ShihabDarkMode ? Color.FromArgb(34, 38, 48) : Color.FromArgb(226, 232, 240);
                Color shihabBadgeEdge = _mehediConfig.ShihabDarkMode ? Color.FromArgb(46, 52, 66) : Color.FromArgb(203, 213, 225);
                Color mehediBadgeFg = _mehediConfig.ShihabDarkMode ? Color.FromArgb(154, 163, 181) : Color.FromArgb(71, 85, 105);

                using (System.Drawing.Drawing2D.GraphicsPath mehediBadgePath = ShihabThemePalette.CreateShihabRoundedRect(shihabBadgeRect, 6))
                using (SolidBrush shihabBadgeBr = new SolidBrush(mehediBadgeFill))
                using (Pen mehediBadgePen = new Pen(shihabBadgeEdge, 1f))
                {
                    shihabG.FillPath(shihabBadgeBr, mehediBadgePath);
                    shihabG.DrawPath(mehediBadgePen, mehediBadgePath);
                }
                TextRenderer.DrawText(
                    shihabG,
                    _mehediHeaderItemsBadgeText,
                    badgeFont,
                    shihabBadgeRect,
                    mehediBadgeFg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            };

            _shihabTorrentPillBtn = new ShihabHeaderPillButton("Torrent", 62, false);
            _shihabTorrentPillBtn.Click += delegate { OpenShihabTorrentDialog(""); };

            _shihabMediaPillBtn = new ShihabHeaderPillButton("Media / YT", 78, false);
            _shihabMediaPillBtn.Click += delegate { OpenShihabMediaAndGalleryDialog(); };

            _shihabRssPillBtn = new ShihabHeaderPillButton("RSS", 48, false);
            _shihabRssPillBtn.Click += delegate { OpenShihabRssFeedDialog(); };

            _mehediFolderPillBtn = new ShihabHeaderPillButton("Folder", 60, false);
            _mehediFolderPillBtn.Click += delegate
            {
                if (_shihabDownloadsListView.SelectedItems.Count > 0)
                {
                    OpenSelectedMehediContainingFolder();
                }
                else
                {
                    OpenDefaultShihabDownloadsDirectory();
                }
            };

            _shihabRemotePillBtn = new ShihabHeaderPillButton("Remote QR", 80, false);
            _shihabRemotePillBtn.Click += delegate { OpenShihabRemotePairingDialog(); };

            _shihabUpdatePillBtn = new ShihabHeaderPillButton("Update!", 96, true);
            _shihabUpdatePillBtn.Visible = false;
            _shihabUpdatePillBtn.Click += delegate
            {
                if (_shihabLatestDetectedUpdate != null && _shihabLatestDetectedUpdate.UpdateAvailable)
                {
                    OpenShihabAutoUpdateDialog(_shihabLatestDetectedUpdate);
                }
                else
                {
                    TriggerShihabUpdateCheck(true);
                }
            };

            _mehediMenuPillBtn = new ShihabHeaderPillButton("\u2630", 34, false);
            _mehediMenuPillBtn.Click += delegate
            {
                _mehediOverflowAppMenu.Show(_mehediMenuPillBtn, new Point(0, _mehediMenuPillBtn.Height + 2));
            };

            _shihabSearchBarPanel = new ShihabRoundedCardPanel
            {
                Size = new Size(206, 28),
                MehediCornerRadius = 6,
                Padding = new Padding(10, 4, 24, 4)
            };

            _mehediSearchBox = new TextBox
            {
                Location = new Point(10, 5),
                Width = 168,
                BorderStyle = BorderStyle.None,
                Font = _searchBoxFont
            };
            Action shihabApplySearchCue = delegate
            {
                try
                {
                    if (_mehediSearchBox.IsHandleCreated)
                    {
                        SendMehediCueBannerMessage(_mehediSearchBox.Handle, 0x1501, (IntPtr)1, "Filter transfers...");
                    }
                }
                catch { }
            };
            _mehediSearchBox.HandleCreated += delegate { shihabApplySearchCue(); };

            _shihabClearSearchBtn = new Button
            {
                Text = "\u00D7",
                Location = new Point(180, 4),
                Size = new Size(20, 20),
                FlatStyle = FlatStyle.Flat,
                Visible = false,
                Cursor = Cursors.Hand
            };
            _shihabClearSearchBtn.FlatAppearance.BorderSize = 0;
            _shihabClearSearchBtn.Click += delegate
            {
                _mehediSearchBox.Text = "";
                _mehediSearchBox.Focus();
            };
            _mehediSearchBox.TextChanged += delegate
            {
                _shihabClearSearchBtn.Visible = !string.IsNullOrEmpty(_mehediSearchBox.Text);
                RefreshMehediDownloadsGrid();
            };
            _shihabSearchBarPanel.Controls.Add(_mehediSearchBox);
            _shihabSearchBarPanel.Controls.Add(_shihabClearSearchBtn);

            _mehediTopActionHeaderBar.Controls.AddRange(new Control[]
            {
                _shihabTorrentPillBtn,
                _shihabMediaPillBtn,
                _shihabRssPillBtn,
                _mehediFolderPillBtn,
                _shihabRemotePillBtn,
                _shihabUpdatePillBtn,
                _mehediMenuPillBtn,
                _shihabSearchBarPanel
            });
            _mehediTopActionHeaderBar.Resize += delegate { RepositionShihabHeaderControls(); };

            // Main Transfers Grid (8 columns)
            _shihabDownloadsListView = new ShihabModernListView
            {
                Dock = DockStyle.Fill,
                MultiSelect = true,
                AllowDrop = true,
                ShihabTransferGridMode = true,
                MehediSortedColumnIndex = -1,
                ShihabSortAscending = false
            };
            _shihabDownloadsListView.Columns.Add("#", 44);
            _shihabDownloadsListView.Columns.Add("FILE NAME", 350);
            _shihabDownloadsListView.Columns.Add("SIZE", 84);
            _shihabDownloadsListView.Columns.Add("PROGRESS", 92);
            _shihabDownloadsListView.Columns.Add("SPEED", 86);
            _shihabDownloadsListView.Columns.Add("ETA", 68);
            _shihabDownloadsListView.Columns.Add("STATUS", 96);
            _shihabDownloadsListView.Columns.Add("DATE ADDED", 96);

            _shihabDownloadsListView.Resize += delegate { ResizeShihabFileNameColumn(); };
            _shihabDownloadsListView.SelectedIndexChanged += delegate { UpdateMehediToolbarActionIcons(); };
            _shihabDownloadsListView.ColumnClick += delegate(object shihabS, ColumnClickEventArgs mehediE)
            {
                string shihabNextKey = "ORDER";
                if (mehediE.Column == 1) shihabNextKey = "NAME";
                else if (mehediE.Column == 2) shihabNextKey = "SIZE";
                else if (mehediE.Column == 3) shihabNextKey = "PROGRESS";
                else if (mehediE.Column == 4) shihabNextKey = "SPEED";
                else if (mehediE.Column == 6) shihabNextKey = "STATUS";
                else shihabNextKey = "ORDER";

                if (_mehediSortColumnKey == shihabNextKey)
                {
                    _shihabSortAscending = !_shihabSortAscending;
                }
                else
                {
                    _mehediSortColumnKey = shihabNextKey;
                    _shihabSortAscending = (shihabNextKey == "NAME" || shihabNextKey == "STATUS");
                }
                _shihabDownloadsListView.MehediSortedColumnIndex = mehediE.Column;
                _shihabDownloadsListView.ShihabSortAscending = _shihabSortAscending;
                RefreshMehediDownloadsGrid();
            };

            _shihabDownloadsListView.DoubleClick += delegate { HandleDoubleClickOnShihabRow(); };
            _shihabDownloadsListView.DragEnter += delegate(object shihabS, DragEventArgs mehediE)
            {
                if (mehediE.Data.GetDataPresent(DataFormats.FileDrop) ||
                    mehediE.Data.GetDataPresent(DataFormats.UnicodeText) ||
                    mehediE.Data.GetDataPresent(DataFormats.Text))
                {
                    mehediE.Effect = DragDropEffects.Copy;
                }
            };
            _shihabDownloadsListView.DragDrop += delegate(object shihabS, DragEventArgs mehediE)
            {
                HandleDragDropDataObject(mehediE.Data);
            };
            _shihabDownloadsListView.ContextMenuStrip = BuildMehediRowContextMenu();

            // Bottom Footer Bar (38px tall with 4 status pills)
            _mehediFooterBar = new ShihabTransferFooterBarControl
            {
                Dock = DockStyle.Bottom,
                Height = 38
            };

            _shihabRightWorkspacePanel.Controls.Add(_shihabDownloadsListView);
            _shihabRightWorkspacePanel.Controls.Add(_mehediFooterBar);
            _shihabRightWorkspacePanel.Controls.Add(_mehediTopActionHeaderBar);

            // System Tray Icon
            _shihabTrayMenuRef = new ContextMenuStrip();
            _shihabTrayMenuRef.Items.Add("Restore MHS Download Manager", null, delegate { RestoreAndActivateMainWindow(); });
            _shihabTrayMenuRef.Items.Add("Add new download...", null, delegate { RestoreAndActivateMainWindow(); PromptAndAddNewShihabUrl(""); });
            _shihabTrayMenuRef.Items.Add("Show drop target", null, delegate { ToggleMehediFloatingDropTarget(); });
            _shihabTrayMenuRef.Items.Add("Start Main Queue", null, delegate { _shihabEngine.StartShihabQueue("Main Download Queue"); });
            _shihabTrayMenuRef.Items.Add("Stop All Downloads", null, delegate { _shihabEngine.StopAllMehediDownloads(); RefreshMehediDownloadsGrid(); });
            _shihabTrayMenuRef.Items.Add(new ToolStripSeparator());
            _shihabTrayMenuRef.Items.Add("Tell a Friend...", null, delegate { RestoreAndActivateMainWindow(); OpenShihabTellAFriendDialog(); });
            _shihabTrayMenuRef.Items.Add("Check for Updates... (Ctrl+U)", null, delegate { RestoreAndActivateMainWindow(); TriggerShihabUpdateCheck(true); });
            _shihabTrayMenuRef.Items.Add("About MHS Download Manager...", null, delegate { RestoreAndActivateMainWindow(); ShowShihabAboutDialog(); });
            _shihabTrayMenuRef.Items.Add("Exit", null, delegate { ExitShihabApplicationCleanly(); });

            _mehediTrayIcon = new NotifyIcon
            {
                Icon = Icon,
                Text = ShihabThemePalette.ShihabMainHeaderTitle,
                Visible = true,
                ContextMenuStrip = _shihabTrayMenuRef
            };
            _mehediTrayIcon.DoubleClick += delegate { RestoreAndActivateMainWindow(); };
            _mehediTrayIcon.BalloonTipClicked += delegate
            {
                if (_shihabLatestDetectedUpdate != null && _shihabLatestDetectedUpdate.UpdateAvailable)
                {
                    RestoreAndActivateMainWindow();
                    OpenShihabAutoUpdateDialog(_shihabLatestDetectedUpdate);
                }
            };

            // WinForms docks back-to-front (last added docks outermost):
            // 1) _shihabTitleBar at very top (custom DWM caption with Background Minimize beside Minimize)
            // 2) _shihabMenuBar directly underneath _shihabTitleBar
            // 3) _shihabTopRibbonBar full-width directly underneath _shihabMenuBar
            // 4) _shihabSidebarPanel on the left below _shihabTopRibbonBar
            // 5) _shihabRightWorkspacePanel filling remaining area
            Controls.Add(_shihabRightWorkspacePanel);
            Controls.Add(_shihabSidebarPanel);
            Controls.Add(_shihabTopRibbonBar);
            Controls.Add(_shihabMenuBar);
            Controls.Add(_shihabTitleBar);
        }

        private ShihabTopIconRibbonControl BuildShihabTopIconRibbon()
        {
            ShihabTopIconRibbonControl ribbon = new ShihabTopIconRibbonControl
            {
                Dock = DockStyle.Top,
                Height = 72
            };

            ribbon.Items.Add(new ShihabTopRibbonButtonSpec
            {
                Id = "add_url",
                LocalizationKey = "btn_add_url",
                DefaultText = "Add URL",
                DisplayText = "Add URL",
                ToolTipText = "Add URL (Ctrl+N) — Add a new HTTP, HTTPS, FTP, Magnet, or Media download",
                Enabled = true,
                OnPrimaryClick = delegate { PromptAndAddNewShihabUrl(""); }
            });

            ribbon.Items.Add(new ShihabTopRibbonButtonSpec
            {
                Id = "resume",
                LocalizationKey = "btn_resume",
                DefaultText = "Resume",
                DisplayText = "Resume",
                ToolTipText = "Resume — Continue downloading selected unfinished transfer(s)",
                Enabled = false,
                OnPrimaryClick = delegate { ResumeSelectedShihabDownloads(); }
            });

            ribbon.Items.Add(new ShihabTopRibbonButtonSpec
            {
                Id = "stop",
                LocalizationKey = "btn_stop",
                DefaultText = "Stop",
                DisplayText = "Stop",
                ToolTipText = "Stop — Pause selected active transfer(s)",
                Enabled = false,
                OnPrimaryClick = delegate { PauseSelectedMehediDownloads(); }
            });

            ribbon.Items.Add(new ShihabTopRibbonButtonSpec
            {
                Id = "stop_all",
                LocalizationKey = "btn_stop_all",
                DefaultText = "Stop All",
                DisplayText = "Stop All",
                ToolTipText = "Stop All — Pause all active downloads and running queues",
                Enabled = false,
                OnPrimaryClick = delegate
                {
                    _shihabEngine.StopAllMehediDownloads();
                    RefreshMehediDownloadsGrid();
                }
            });

            ribbon.Items.Add(new ShihabTopRibbonButtonSpec
            {
                Id = "delete",
                LocalizationKey = "btn_delete",
                DefaultText = "Delete",
                DisplayText = "Delete",
                ToolTipText = "Delete (Del / Shift+Del) — Remove selected transfer(s) from list or disk",
                Enabled = false,
                OnPrimaryClick = delegate
                {
                    bool shiftHeld = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
                    PromptAndDeleteSelectedDownloads(shiftHeld);
                }
            });

            ribbon.Items.Add(new ShihabTopRibbonButtonSpec
            {
                Id = "delete_completed",
                LocalizationKey = "btn_delete_completed",
                DefaultText = "Delete Completed",
                DisplayText = "Delete Completed",
                ToolTipText = "Delete Completed — Clear all finished transfers from the list (keeps files on disk)",
                Enabled = true,
                OnPrimaryClick = delegate { DeleteAllCompletedMehediDownloads(); }
            });

            ribbon.Items.Add(new ShihabTopRibbonButtonSpec
            {
                Id = "options",
                LocalizationKey = "btn_options",
                DefaultText = "Options",
                DisplayText = "Options",
                ToolTipText = "Options — Configure connections, browser integration, proxy/DNS, folders, themes & updates",
                Enabled = true,
                OnPrimaryClick = delegate { OpenShihabOptionsDialog(); }
            });

            ribbon.Items.Add(new ShihabTopRibbonButtonSpec
            {
                Id = "scheduler",
                LocalizationKey = "btn_scheduler",
                DefaultText = "Scheduler",
                DisplayText = "Scheduler",
                ToolTipText = "Scheduler — Manage download queues, timed start/stop, active days, sync & post-completion actions",
                Enabled = true,
                OnPrimaryClick = delegate { OpenMehediSchedulerDialog(); }
            });

            ribbon.Items.Add(new ShihabTopRibbonButtonSpec
            {
                Id = "start_queue",
                LocalizationKey = "btn_start_queue",
                DefaultText = "Start Queue",
                DisplayText = "Start Queue",
                ToolTipText = "Start Queue — Start Main Download Queue (click arrow to select any queue)",
                Enabled = true,
                HasSplitDropDown = true,
                OnPrimaryClick = delegate
                {
                    _shihabEngine.StartShihabQueue("Main Download Queue");
                    RefreshMehediDownloadsGrid();
                },
                OnSplitDropDownClick = delegate(Control ownerCtrl, Point pt)
                {
                    _shihabStartQueuePopupMenu.Show(ownerCtrl, pt);
                }
            });

            ribbon.Items.Add(new ShihabTopRibbonButtonSpec
            {
                Id = "stop_queue",
                LocalizationKey = "btn_stop_queue",
                DefaultText = "Stop Queue",
                DisplayText = "Stop Queue",
                ToolTipText = "Stop Queue — Stop Main Download Queue (click arrow to stop a specific queue or all queues)",
                Enabled = true,
                HasSplitDropDown = true,
                OnPrimaryClick = delegate
                {
                    _shihabEngine.StopAllShihabQueues();
                    RefreshMehediDownloadsGrid();
                },
                OnSplitDropDownClick = delegate(Control ownerCtrl, Point pt)
                {
                    _shihabStopQueuePopupMenu.Show(ownerCtrl, pt);
                }
            });

            ribbon.Items.Add(new ShihabTopRibbonButtonSpec
            {
                Id = "tell_friend",
                LocalizationKey = "btn_tell_friend",
                DefaultText = "Tell a Friend",
                DisplayText = "Tell a Friend",
                ToolTipText = "Tell a Friend — Share MHS Download Manager via QR code, link, Email, X, WhatsApp, Telegram or LinkedIn",
                Enabled = true,
                OnPrimaryClick = delegate { OpenShihabTellAFriendDialog(); }
            });

            ribbon.RecalculateItemBounds();
            return ribbon;
        }

        private string BuildValidatedGitHubRepoUrl(string suffix)
        {
            string rawSlug = (_mehediConfig != null && !string.IsNullOrWhiteSpace(_mehediConfig.ShihabUpdateRepoSlug))
                ? _mehediConfig.ShihabUpdateRepoSlug.Trim()
                : ShihabAutoUpdater.DefaultRepoSlug;
            string safeSlug = SafeRepoSlugRegex.IsMatch(rawSlug) ? rawSlug : ShihabAutoUpdater.DefaultRepoSlug;
            return "https://github.com/" + safeSlug + (suffix ?? "");
        }

        private MenuStrip BuildShihabTopMenuStrip()
        {
            MenuStrip bar = new MenuStrip
            {
                Dock = DockStyle.Top,
                Visible = true,
                ShowItemToolTips = true,
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(4, 2, 4, 2)
            };

            // 1. Tasks
            _tasksTopMenu = new ToolStripMenuItem("&Tasks");
            ToolStripMenuItem taskAddNew = new ToolStripMenuItem("Add new download...", null, delegate { PromptAndAddNewShihabUrl(""); })
            {
                ShortcutKeyDisplayString = "Ctrl+N"
            };
            ToolStripMenuItem taskAddBatch = new ToolStripMenuItem("Add batch download...", null, delegate { OpenShihabBatchWildcardDialog(); });
            ToolStripMenuItem taskAddClip = new ToolStripMenuItem("Add batch download from clipboard", null, delegate { ImportShihabClipboardUrlsBatch(); });
            ToolStripMenuItem taskTorrent = new ToolStripMenuItem("Add BitTorrent / Magnet (.torrent)...", null, delegate { OpenShihabTorrentDialog(""); });
            ToolStripMenuItem taskMedia = new ToolStripMenuItem("Video / Playlist, Subtitles && Gallery Downloader...", null, delegate { OpenShihabMediaAndGalleryDialog(); });
            ToolStripMenuItem taskRss = new ToolStripMenuItem("RSS / Atom Feed Reader && Auto-Downloader...", null, delegate { OpenShihabRssFeedDialog(); });
            ToolStripMenuItem taskGrabber = new ToolStripMenuItem("Run site grabber...", null, delegate { OpenShihabSiteGrabberDialog(); });
            ToolStripMenuItem taskDropTarget = new ToolStripMenuItem("Show drop target", null, delegate { ToggleMehediFloatingDropTarget(); });
            ToolStripMenuItem taskRemoteQr = new ToolStripMenuItem("LAN Remote Web UI && Mobile Pairing (QR)...", null, delegate { OpenShihabRemotePairingDialog(); });
            ToolStripMenuItem taskChecksum = new ToolStripMenuItem("Checksum Verifier && Safe Archive Extractor...", null, delegate { OpenShihabChecksumAndExtractDialog(); });

            ToolStripMenuItem taskExport = new ToolStripMenuItem("Export");
            taskExport.DropDownItems.Add("To MHS / IDM export file (.ef2)...", null, delegate { ExportShihabList(true); });
            taskExport.DropDownItems.Add("To text file (.txt)...", null, delegate { ExportShihabList(false); });

            ToolStripMenuItem taskImport = new ToolStripMenuItem("Import");
            taskImport.DropDownItems.Add("From MHS / IDM export or text file (.ef2 / .txt)...", null, delegate { ImportMehediList(); });

            ToolStripMenuItem taskExit = new ToolStripMenuItem("Exit", null, delegate { ExitShihabApplicationCleanly(); });

            _tasksTopMenu.DropDownOpening += delegate
            {
                taskDropTarget.Checked = _shihabDropTargetForm != null && !_shihabDropTargetForm.IsDisposed && _shihabDropTargetForm.Visible;
            };
            _tasksTopMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                taskAddNew, taskAddBatch, taskAddClip, taskTorrent, taskMedia, taskRss, taskGrabber,
                new ToolStripSeparator(),
                taskDropTarget, taskRemoteQr, taskChecksum,
                new ToolStripSeparator(),
                taskExport, taskImport,
                new ToolStripSeparator(),
                taskExit
            });

            // 2. File
            _fileTopMenu = new ToolStripMenuItem("&File");
            ToolStripMenuItem fileStop = new ToolStripMenuItem("Stop download", null, delegate { PauseSelectedMehediDownloads(); });
            ToolStripMenuItem fileResume = new ToolStripMenuItem("Resume download", null, delegate { ResumeSelectedShihabDownloads(); });
            ToolStripMenuItem fileProgress = new ToolStripMenuItem("Show live progress window", null, delegate { OpenSelectedMehediProgressWindow(); });
            ToolStripMenuItem fileOpen = new ToolStripMenuItem("Open", null, delegate { OpenSelectedShihabCompletedFile(false); }) { ShortcutKeyDisplayString = "Enter" };
            ToolStripMenuItem fileOpenWith = new ToolStripMenuItem("Open With...", null, delegate { OpenSelectedShihabCompletedFile(true); });
            ToolStripMenuItem fileOpenFolder = new ToolStripMenuItem("Open folder", null, delegate
            {
                if (_shihabDownloadsListView.SelectedItems.Count > 0) OpenSelectedMehediContainingFolder();
                else OpenDefaultShihabDownloadsDirectory();
            });
            ToolStripMenuItem fileRedownload = new ToolStripMenuItem("Redownload", null, delegate { RedownloadSelectedShihabItem(); });
            ToolStripMenuItem fileRefreshAddr = new ToolStripMenuItem("Refresh download address...", null, delegate { RefreshSelectedMehediAddress(); });
            ToolStripMenuItem fileRemove = new ToolStripMenuItem("Remove", null, delegate { PromptAndDeleteSelectedDownloads(false); }) { ShortcutKeyDisplayString = "Del" };
            ToolStripMenuItem fileRemoveDisk = new ToolStripMenuItem("Remove + delete file from disk...", null, delegate { PromptAndDeleteSelectedDownloads(true); }) { ShortcutKeyDisplayString = "Shift+Del" };
            ToolStripMenuItem fileProps = new ToolStripMenuItem("Properties...", null, delegate { OpenSelectedShihabProperties(); }) { ShortcutKeyDisplayString = "Alt+Enter" };

            _fileTopMenu.DropDownOpening += delegate
            {
                bool hasSel, canResume, canStop, canOpenCompleted;
                EvaluateSelectionCapabilities(out hasSel, out canResume, out canStop, out canOpenCompleted);
                fileStop.Enabled = canStop;
                fileResume.Enabled = canResume;
                fileProgress.Enabled = hasSel;
                fileOpen.Enabled = canOpenCompleted;
                fileOpenWith.Enabled = canOpenCompleted;
                fileOpenFolder.Enabled = true;
                fileRedownload.Enabled = hasSel;
                fileRefreshAddr.Enabled = hasSel;
                fileRemove.Enabled = hasSel;
                fileRemoveDisk.Enabled = hasSel;
                fileProps.Enabled = hasSel;
            };
            _fileTopMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                fileStop, fileResume, fileProgress,
                new ToolStripSeparator(),
                fileOpen, fileOpenWith, fileOpenFolder,
                new ToolStripSeparator(),
                fileRedownload, fileRefreshAddr, fileRemove, fileRemoveDisk,
                new ToolStripSeparator(),
                fileProps
            });

            // 3. Downloads
            _downloadsTopMenu = new ToolStripMenuItem("&Downloads");
            ToolStripMenuItem dlPauseAll = new ToolStripMenuItem("Pause all", null, delegate
            {
                _shihabEngine.StopAllMehediDownloads();
                RefreshMehediDownloadsGrid();
            });
            ToolStripMenuItem dlDeleteCompleted = new ToolStripMenuItem("Delete all completed", null, delegate { DeleteAllCompletedMehediDownloads(); });
            ToolStripMenuItem dlFind = new ToolStripMenuItem("Find...", null, delegate
            {
                _mehediSearchBox.Focus();
                _mehediSearchBox.SelectAll();
            }) { ShortcutKeyDisplayString = "Ctrl+F" };
            ToolStripMenuItem dlScheduler = new ToolStripMenuItem("Scheduler...", null, delegate { OpenMehediSchedulerDialog(); });
            ToolStripMenuItem dlStartNowSub = new ToolStripMenuItem("Start Now");
            ToolStripMenuItem dlStopSub = new ToolStripMenuItem("Stop");

            ToolStripMenuItem dlSpeedSub = new ToolStripMenuItem("Speed Limiter");
            ToolStripMenuItem dlSpeedOn = new ToolStripMenuItem("Turn On", null, delegate
            {
                _mehediConfig.MehediGlobalSpeedLimiterEnabled = true;
                _shihabStore.SaveMehediConfig(_mehediConfig);
                UpdateShihabStatusBadges();
            });
            ToolStripMenuItem dlSpeedOff = new ToolStripMenuItem("Turn Off", null, delegate
            {
                _mehediConfig.MehediGlobalSpeedLimiterEnabled = false;
                _shihabStore.SaveMehediConfig(_mehediConfig);
                UpdateShihabStatusBadges();
            });
            ToolStripMenuItem dlSpeedSettings = new ToolStripMenuItem("Settings...", null, delegate { OpenShihabOptionsDialog(); });
            dlSpeedSub.DropDownItems.AddRange(new ToolStripItem[] { dlSpeedOn, dlSpeedOff, new ToolStripSeparator(), dlSpeedSettings });

            ToolStripMenuItem dlCompletionSub = new ToolStripMenuItem("Options on completion");
            ToolStripMenuItem dlExitDone = new ToolStripMenuItem("Exit MHS Download Manager when done", null, delegate
            {
                MehediQueueConfig mq = FindMainQueueConfig();
                if (mq != null)
                {
                    mq.MehediExitAppWhenDone = !mq.MehediExitAppWhenDone;
                    _shihabStore.SaveMehediConfig(_mehediConfig);
                }
            });
            ToolStripMenuItem dlHangUpDone = new ToolStripMenuItem("Hang up modem / VPN when done", null, delegate
            {
                _mehediConfig.MehediHangUpWhenDone = !_mehediConfig.MehediHangUpWhenDone;
                _shihabStore.SaveMehediConfig(_mehediConfig);
            });
            ToolStripMenuItem dlShutdownDone = new ToolStripMenuItem("Shut down computer when done", null, delegate
            {
                MehediQueueConfig mq = FindMainQueueConfig();
                if (mq != null)
                {
                    bool currentlyShutdown = string.Equals(mq.ShihabSystemPowerActionWhenDone, "Shutdown", StringComparison.OrdinalIgnoreCase);
                    mq.ShihabSystemPowerActionWhenDone = currentlyShutdown ? "None" : "Shutdown";
                    _shihabStore.SaveMehediConfig(_mehediConfig);
                }
            });
            dlCompletionSub.DropDownItems.AddRange(new ToolStripItem[] { dlExitDone, dlHangUpDone, dlShutdownDone });

            ToolStripMenuItem dlOptions = new ToolStripMenuItem("Options...", null, delegate { OpenShihabOptionsDialog(); });

            _downloadsTopMenu.DropDownOpening += delegate
            {
                dlPauseAll.Enabled = _shihabEngine.HasAnyActiveDownloadsOrQueues();
                PopulateStartQueueMenuItems(dlStartNowSub.DropDownItems);
                PopulateStopQueueMenuItems(dlStopSub.DropDownItems);
                dlSpeedOn.Checked = _mehediConfig.MehediGlobalSpeedLimiterEnabled;
                dlSpeedOff.Checked = !_mehediConfig.MehediGlobalSpeedLimiterEnabled;
                MehediQueueConfig mq = FindMainQueueConfig();
                dlExitDone.Checked = mq != null && mq.MehediExitAppWhenDone;
                dlHangUpDone.Checked = _mehediConfig.MehediHangUpWhenDone;
                dlShutdownDone.Checked = mq != null && string.Equals(mq.ShihabSystemPowerActionWhenDone, "Shutdown", StringComparison.OrdinalIgnoreCase);
            };
            _downloadsTopMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                dlPauseAll, dlDeleteCompleted, dlFind,
                new ToolStripSeparator(),
                dlScheduler, dlStartNowSub, dlStopSub,
                new ToolStripSeparator(),
                dlSpeedSub, dlCompletionSub,
                new ToolStripSeparator(),
                dlOptions
            });

            // 4. View
            _viewTopMenu = new ToolStripMenuItem("&View");
            ToolStripMenuItem viewCategories = new ToolStripMenuItem("Show Categories / Transfers Sidebar", null, delegate
            {
                _mehediConfig.MehediShowCategoriesPanel = !_mehediConfig.MehediShowCategoriesPanel;
                _shihabSidebarPanel.Visible = _mehediConfig.MehediShowCategoriesPanel;
                _shihabStore.SaveMehediConfig(_mehediConfig);
                ResizeShihabFileNameColumn();
                RepositionShihabHeaderControls();
            });

            ToolStripMenuItem viewToolbarSub = new ToolStripMenuItem("Toolbar");
            ToolStripMenuItem viewShowRibbon = new ToolStripMenuItem("Show Top Icon Toolbar", null, delegate
            {
                _shihabTopRibbonBar.Visible = !_shihabTopRibbonBar.Visible;
            });
            ToolStripMenuItem viewLargeIcons = new ToolStripMenuItem("Large Gradient Icons", null, delegate
            {
                _shihabTopRibbonBar.Visible = true;
                _shihabTopRibbonBar.CompactMode = false;
                _shihabTopRibbonBar.UpdateMetricsForScale();
            });
            ToolStripMenuItem viewCompactIcons = new ToolStripMenuItem("Compact Icons", null, delegate
            {
                _shihabTopRibbonBar.Visible = true;
                _shihabTopRibbonBar.CompactMode = true;
                _shihabTopRibbonBar.UpdateMetricsForScale();
            });
            viewToolbarSub.DropDownItems.AddRange(new ToolStripItem[] { viewShowRibbon, new ToolStripSeparator(), viewLargeIcons, viewCompactIcons });

            ToolStripMenuItem viewDropTarget = new ToolStripMenuItem("Show Floating Drop Target", null, delegate { ToggleMehediFloatingDropTarget(); });

            ToolStripMenuItem viewArrangeSub = new ToolStripMenuItem("Arrange Files");
            AddSortMenuOption(viewArrangeSub, "By Order Added", "ORDER", 0);
            AddSortMenuOption(viewArrangeSub, "By File Name", "NAME", 1);
            AddSortMenuOption(viewArrangeSub, "By Size", "SIZE", 2);
            AddSortMenuOption(viewArrangeSub, "By Progress", "PROGRESS", 3);
            AddSortMenuOption(viewArrangeSub, "By Speed", "SPEED", 4);
            AddSortMenuOption(viewArrangeSub, "By Status", "STATUS", 6);

            ToolStripMenuItem viewThemeSub = new ToolStripMenuItem("Theme");
            string[] builtInThemes = new string[] { "Obsidian Dark", "Midnight Violet", "Nordic Slate", "Cyber Emerald", "Classic Light" };
            for (int t = 0; t < builtInThemes.Length; t++)
            {
                AddThemeMenuOption(viewThemeSub, builtInThemes[t]);
            }

            ToolStripMenuItem viewScaleSub = new ToolStripMenuItem("UI Scale");
            int[] scales = new int[] { 90, 100, 115, 125, 150 };
            for (int s = 0; s < scales.Length; s++)
            {
                AddScaleMenuOption(viewScaleSub, scales[s]);
            }

            _viewTopMenu.DropDownOpening += delegate
            {
                viewCategories.Checked = _shihabSidebarPanel.Visible;
                viewShowRibbon.Checked = _shihabTopRibbonBar.Visible;
                viewLargeIcons.Checked = _shihabTopRibbonBar.Visible && !_shihabTopRibbonBar.CompactMode;
                viewCompactIcons.Checked = _shihabTopRibbonBar.Visible && _shihabTopRibbonBar.CompactMode;
                viewDropTarget.Checked = _shihabDropTargetForm != null && !_shihabDropTargetForm.IsDisposed && _shihabDropTargetForm.Visible;
                for (int i = 0; i < viewThemeSub.DropDownItems.Count; i++)
                {
                    ToolStripMenuItem ti = viewThemeSub.DropDownItems[i] as ToolStripMenuItem;
                    if (ti != null) ti.Checked = string.Equals(_mehediConfig.ShihabThemeName, ti.Tag as string, StringComparison.OrdinalIgnoreCase);
                }
                for (int i = 0; i < viewScaleSub.DropDownItems.Count; i++)
                {
                    ToolStripMenuItem si = viewScaleSub.DropDownItems[i] as ToolStripMenuItem;
                    if (si != null && si.Tag is int) si.Checked = (_mehediConfig.ShihabUiScalePercent == (int)si.Tag);
                }
            };
            ToolStripMenuItem viewBackgroundTray = new ToolStripMenuItem("Minimize to Background (System Tray)", null, delegate
            {
                MinimizeShihabToBackgroundTray(true);
            }) { ShortcutKeyDisplayString = "Ctrl+Shift+B" };

            _viewTopMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                viewCategories, viewToolbarSub, viewDropTarget,
                new ToolStripSeparator(),
                viewArrangeSub, viewThemeSub, viewScaleSub,
                new ToolStripSeparator(),
                viewBackgroundTray
            });

            // 5. Help
            _helpTopMenu = new ToolStripMenuItem("&Help");
            _helpTopMenu.DropDownItems.Add("Browser Extension Setup Guide...", null, delegate { ShowShihabExtensionInstructions(); });
            _helpTopMenu.DropDownItems.Add("Open Browser Extension Folder...", null, delegate { OpenShihabInstalledExtensionFolder(); });
            _helpTopMenu.DropDownItems.Add("Command-Line && MCP Help...", null, delegate { ShowMehediCliHelp(); });
            _helpTopMenu.DropDownItems.Add(new ToolStripSeparator());
            _helpTopMenu.DropDownItems.Add("Tell a Friend...", null, delegate { OpenShihabTellAFriendDialog(); });
            ToolStripMenuItem helpCheckUpd = new ToolStripMenuItem("Check for Updates...", null, delegate { TriggerShihabUpdateCheck(true); })
            {
                ShortcutKeyDisplayString = "Ctrl+U"
            };
            _helpTopMenu.DropDownItems.Add(helpCheckUpd);
            _helpTopMenu.DropDownItems.Add("GitHub Releases Page...", null, delegate
            {
                ShihabThemePalette.SafeLaunchExternalUrl(BuildValidatedGitHubRepoUrl("/releases"));
            });
            _helpTopMenu.DropDownItems.Add(new ToolStripSeparator());
            _helpTopMenu.DropDownItems.Add("About MHS Download Manager...", null, delegate { ShowShihabAboutDialog(); });

            // 6. Registration
            _registrationTopMenu = new ToolStripMenuItem("&Registration");
            _registrationTopMenu.DropDownItems.Add("Registration && License Status...", null, delegate { OpenShihabRegistrationDialog(); });
            _registrationTopMenu.DropDownItems.Add("View Open-Source MIT License...", null, delegate
            {
                ShihabThemePalette.SafeLaunchExternalUrl(BuildValidatedGitHubRepoUrl("/blob/main/LICENSE"));
            });
            _registrationTopMenu.DropDownItems.Add("Star && Support on GitHub...", null, delegate
            {
                ShihabThemePalette.SafeLaunchExternalUrl(BuildValidatedGitHubRepoUrl(""));
            });

            bar.Items.AddRange(new ToolStripItem[]
            {
                _tasksTopMenu,
                _fileTopMenu,
                _downloadsTopMenu,
                _viewTopMenu,
                _helpTopMenu,
                _registrationTopMenu
            });
            return bar;
        }

        private void AddThemeMenuOption(ToolStripMenuItem parent, string tName)
        {
            ToolStripMenuItem tItem = new ToolStripMenuItem(tName, null, delegate
            {
                _mehediConfig.ShihabThemeName = tName;
                _mehediConfig.ShihabDarkMode = !string.Equals(tName, "Classic Light", StringComparison.OrdinalIgnoreCase);
                _shihabStore.SaveMehediConfig(_mehediConfig);
                ApplyMehediTheme();
            });
            tItem.Tag = tName;
            parent.DropDownItems.Add(tItem);
        }

        private void AddScaleMenuOption(ToolStripMenuItem parent, int pct)
        {
            ToolStripMenuItem sItem = new ToolStripMenuItem(pct + "%" + (pct == 100 ? " (Default)" : ""), null, delegate
            {
                _mehediConfig.ShihabUiScalePercent = pct;
                _shihabStore.SaveMehediConfig(_mehediConfig);
                ApplyMehediTheme();
            });
            sItem.Tag = pct;
            parent.DropDownItems.Add(sItem);
        }

        private void AddSortMenuOption(ToolStripMenuItem parent, string label, string sortKey, int colIdx)
        {
            parent.DropDownItems.Add(label, null, delegate
            {
                if (_mehediSortColumnKey == sortKey) _shihabSortAscending = !_shihabSortAscending;
                else
                {
                    _mehediSortColumnKey = sortKey;
                    _shihabSortAscending = (sortKey == "NAME" || sortKey == "STATUS");
                }
                _shihabDownloadsListView.MehediSortedColumnIndex = colIdx;
                _shihabDownloadsListView.ShihabSortAscending = _shihabSortAscending;
                RefreshMehediDownloadsGrid();
            });
        }

        private ToolStripMenuItem CreateStartQueueMenuItem(string qName, string label, bool checkedNow)
        {
            ToolStripMenuItem mi = new ToolStripMenuItem(label, null, delegate
            {
                _shihabEngine.StartShihabQueue(qName);
                RefreshMehediDownloadsGrid();
            });
            mi.Checked = checkedNow;
            return mi;
        }

        private ToolStripMenuItem CreateStopQueueMenuItem(string qName, string label)
        {
            return new ToolStripMenuItem(label, null, delegate
            {
                _shihabEngine.StopShihabQueue(qName);
                RefreshMehediDownloadsGrid();
            });
        }

        private ToolStripMenuItem CreateMoveToQueueMenuItem(string qName)
        {
            return new ToolStripMenuItem(qName, null, delegate { AssignSelectedShihabToQueue(qName); });
        }

        private void PopulateStartQueueMenuItems(ToolStripItemCollection items)
        {
            items.Clear();
            List<ShihabDownloadItem> all = _shihabEngine.GetShihabDownloadsSnapshot();
            if (_mehediConfig.MehediQueues != null)
            {
                for (int i = 0; i < _mehediConfig.MehediQueues.Count; i++)
                {
                    MehediQueueConfig q = _mehediConfig.MehediQueues[i];
                    if (q == null) continue;
                    string qName = q.ShihabQueueName;
                    int count = CountQueueItems(all, qName);
                    string label = string.Format("Start {0} ({1} {2}){3}",
                        qName,
                        count,
                        count == 1 ? "file" : "files",
                        q.MehediIsRunningNow ? " — Running" : "");
                    items.Add(CreateStartQueueMenuItem(qName, label, q.MehediIsRunningNow));
                }
            }
            items.Add(new ToolStripSeparator());
            items.Add("Edit Queues in Scheduler...", null, delegate { OpenMehediSchedulerDialog(); });
        }

        private void PopulateStopQueueMenuItems(ToolStripItemCollection items)
        {
            items.Clear();
            if (_mehediConfig.MehediQueues != null)
            {
                for (int i = 0; i < _mehediConfig.MehediQueues.Count; i++)
                {
                    MehediQueueConfig q = _mehediConfig.MehediQueues[i];
                    if (q == null) continue;
                    string qName = q.ShihabQueueName;
                    string label = "Stop " + qName + (q.MehediIsRunningNow ? " (Running)" : "");
                    items.Add(CreateStopQueueMenuItem(qName, label));
                }
            }
            items.Add(new ToolStripSeparator());
            items.Add("Stop All Queues", null, delegate
            {
                _shihabEngine.StopAllShihabQueues();
                RefreshMehediDownloadsGrid();
            });
            items.Add("Edit Queues in Scheduler...", null, delegate { OpenMehediSchedulerDialog(); });
        }

        private ContextMenuStrip BuildMehediFullAppOverflowMenu()
        {
            ContextMenuStrip mehediMenu = new ContextMenuStrip();

            mehediMenu.Items.Add("Add New Download (Ctrl+N)...", null, delegate { PromptAndAddNewShihabUrl(""); });
            mehediMenu.Items.Add("Add BitTorrent / Magnet (.torrent)...", null, delegate { OpenShihabTorrentDialog(""); });
            mehediMenu.Items.Add("Video / Playlist, Subtitles & Gallery Downloader...", null, delegate { OpenShihabMediaAndGalleryDialog(); });
            mehediMenu.Items.Add("RSS / Atom Feed Reader & Auto-Downloader...", null, delegate { OpenShihabRssFeedDialog(); });
            mehediMenu.Items.Add("Add Batch Download (Wildcard *)...", null, delegate { OpenShihabBatchWildcardDialog(); });
            mehediMenu.Items.Add("Add Batch from Clipboard", null, delegate { ImportShihabClipboardUrlsBatch(); });
            mehediMenu.Items.Add("Run Site Grabber...", null, delegate { OpenShihabSiteGrabberDialog(); });
            mehediMenu.Items.Add(new ToolStripSeparator());

            mehediMenu.Items.Add("LAN Remote Web UI & Mobile Pairing (QR)...", null, delegate { OpenShihabRemotePairingDialog(); });
            mehediMenu.Items.Add("Checksum Verifier & Safe Archive Extractor...", null, delegate { OpenShihabChecksumAndExtractDialog(); });
            mehediMenu.Items.Add(new ToolStripSeparator());

            mehediMenu.Items.Add("Start Main Queue", null, delegate { _shihabEngine.StartShihabQueue("Main Download Queue"); });
            mehediMenu.Items.Add("Stop Main Queue", null, delegate { _shihabEngine.StopShihabQueue("Main Download Queue"); });
            mehediMenu.Items.Add("Scheduler & Queues...", null, delegate { OpenMehediSchedulerDialog(); });
            mehediMenu.Items.Add("Delete All Completed", null, delegate { DeleteAllCompletedMehediDownloads(); });
            mehediMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem shihabExpImp = new ToolStripMenuItem("Import / Export");
            shihabExpImp.DropDownItems.Add("Export to IDM (.ef2)...", null, delegate { ExportShihabList(true); });
            shihabExpImp.DropDownItems.Add("Export to Text (.txt)...", null, delegate { ExportShihabList(false); });
            shihabExpImp.DropDownItems.Add(new ToolStripSeparator());
            shihabExpImp.DropDownItems.Add("Import (.ef2 / .txt)...", null, delegate { ImportMehediList(); });
            mehediMenu.Items.Add(shihabExpImp);

            ToolStripMenuItem mehediViewSub = new ToolStripMenuItem("View & Theme");
            mehediViewSub.DropDownItems.Add("Toggle Transfers Sidebar", null, delegate
            {
                _mehediConfig.MehediShowCategoriesPanel = !_mehediConfig.MehediShowCategoriesPanel;
                _shihabSidebarPanel.Visible = _mehediConfig.MehediShowCategoriesPanel;
                _shihabStore.SaveMehediConfig(_mehediConfig);
                ResizeShihabFileNameColumn();
                RepositionShihabHeaderControls();
            });
            mehediViewSub.DropDownItems.Add("Toggle Dark / Light Theme", null, delegate
            {
                _mehediConfig.ShihabDarkMode = !_mehediConfig.ShihabDarkMode;
                _mehediConfig.ShihabThemeName = _mehediConfig.ShihabDarkMode ? "Obsidian Dark" : "Classic Light";
                _shihabStore.SaveMehediConfig(_mehediConfig);
                ApplyMehediTheme();
            });
            mehediViewSub.DropDownItems.Add("Show Floating Drop Target", null, delegate { ToggleMehediFloatingDropTarget(); });
            mehediMenu.Items.Add(mehediViewSub);

            mehediMenu.Items.Add("Options & Settings...", null, delegate { OpenShihabOptionsDialog(); });
            mehediMenu.Items.Add("Check for Updates (GitHub Releases)... (Ctrl+U)", null, delegate { TriggerShihabUpdateCheck(true); });
            mehediMenu.Items.Add(new ToolStripSeparator());

            mehediMenu.Items.Add("Open Browser Extension Folder...", null, delegate { OpenShihabInstalledExtensionFolder(); });
            mehediMenu.Items.Add("Browser Extension Setup Guide", null, delegate { ShowShihabExtensionInstructions(); });
            mehediMenu.Items.Add("CLI Switches Help", null, delegate { ShowMehediCliHelp(); });
            mehediMenu.Items.Add("About Mehedi Hasan Shihab...", null, delegate { ShowShihabAboutDialog(); });
            mehediMenu.Items.Add(new ToolStripSeparator());
            mehediMenu.Items.Add("Exit Application", null, delegate { ExitShihabApplicationCleanly(); });

            return mehediMenu;
        }

        private ContextMenuStrip BuildMehediRowContextMenu()
        {
            ContextMenuStrip shihabCtx = new ContextMenuStrip();
            ToolStripMenuItem ctxResume = new ToolStripMenuItem("Resume Download", null, delegate { ResumeSelectedShihabDownloads(); });
            ToolStripMenuItem ctxStop = new ToolStripMenuItem("Stop Download", null, delegate { PauseSelectedMehediDownloads(); });
            ToolStripMenuItem ctxProgress = new ToolStripMenuItem("Show Live Progress Window", null, delegate { OpenSelectedMehediProgressWindow(); });
            ToolStripMenuItem ctxOpen = new ToolStripMenuItem("Open", null, delegate { OpenSelectedShihabCompletedFile(false); });
            ToolStripMenuItem ctxOpenWith = new ToolStripMenuItem("Open With...", null, delegate { OpenSelectedShihabCompletedFile(true); });
            ToolStripMenuItem ctxOpenFolder = new ToolStripMenuItem("Open Folder", null, delegate { OpenSelectedMehediContainingFolder(); });
            ToolStripMenuItem ctxRefreshAddr = new ToolStripMenuItem("Refresh Download Address...", null, delegate { RefreshSelectedMehediAddress(); });
            ToolStripMenuItem ctxRedownload = new ToolStripMenuItem("Redownload from Beginning", null, delegate { RedownloadSelectedShihabItem(); });
            ToolStripMenuItem mehediMoveQueue = new ToolStripMenuItem("Move to Queue");
            ToolStripMenuItem ctxRemove = new ToolStripMenuItem("Remove from List", null, delegate { DeleteSelectedShihabDownloads(false); });
            ToolStripMenuItem ctxRemoveDisk = new ToolStripMenuItem("Remove + Delete File from Disk...", null, delegate { PromptAndDeleteSelectedDownloads(true); });
            ToolStripMenuItem ctxProps = new ToolStripMenuItem("Properties...", null, delegate { OpenSelectedShihabProperties(); });

            shihabCtx.Opening += delegate
            {
                bool hasSel, canResume, canStop, canOpenCompleted;
                EvaluateSelectionCapabilities(out hasSel, out canResume, out canStop, out canOpenCompleted);

                ctxResume.Enabled = canResume;
                ctxStop.Enabled = canStop;
                ctxProgress.Enabled = hasSel;
                ctxOpen.Enabled = canOpenCompleted;
                ctxOpenWith.Enabled = canOpenCompleted;
                ctxOpenFolder.Enabled = hasSel;
                ctxRefreshAddr.Enabled = hasSel;
                ctxRedownload.Enabled = hasSel;
                mehediMoveQueue.Enabled = hasSel;
                ctxRemove.Enabled = hasSel;
                ctxRemoveDisk.Enabled = hasSel;
                ctxProps.Enabled = hasSel;

                mehediMoveQueue.DropDownItems.Clear();
                mehediMoveQueue.DropDownItems.Add("(None — Direct)", null, delegate { AssignSelectedShihabToQueue(""); });
                for (int shihabQ = 0; shihabQ < _mehediConfig.MehediQueues.Count; shihabQ++)
                {
                    mehediMoveQueue.DropDownItems.Add(CreateMoveToQueueMenuItem(_mehediConfig.MehediQueues[shihabQ].ShihabQueueName));
                }
            };

            shihabCtx.Items.AddRange(new ToolStripItem[]
            {
                ctxResume, ctxStop, ctxProgress,
                new ToolStripSeparator(),
                ctxOpen, ctxOpenWith, ctxOpenFolder,
                new ToolStripSeparator(),
                ctxRefreshAddr, ctxRedownload, mehediMoveQueue,
                new ToolStripSeparator(),
                ctxRemove, ctxRemoveDisk,
                new ToolStripSeparator(),
                ctxProps
            });
            return shihabCtx;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CleanupShihabResources();
                if (_shihabStartQueuePopupMenu != null) { _shihabStartQueuePopupMenu.Dispose(); _shihabStartQueuePopupMenu = null; }
                if (_shihabStopQueuePopupMenu != null) { _shihabStopQueuePopupMenu.Dispose(); _shihabStopQueuePopupMenu = null; }
                if (_mehediOverflowAppMenu != null) { _mehediOverflowAppMenu.Dispose(); _mehediOverflowAppMenu = null; }
                if (_shihabTrayMenuRef != null) { _shihabTrayMenuRef.Dispose(); _shihabTrayMenuRef = null; }
                if (_sidebarHeadingFont != null) { _sidebarHeadingFont.Dispose(); _sidebarHeadingFont = null; }
                if (_headerTitleFont != null) { _headerTitleFont.Dispose(); _headerTitleFont = null; }
                if (_headerBadgeFont != null) { _headerBadgeFont.Dispose(); _headerBadgeFont = null; }
                if (_searchBoxFont != null) { _searchBoxFont.Dispose(); _searchBoxFont = null; }
                if (_scaledUiFont != null) { _scaledUiFont.Dispose(); _scaledUiFont = null; }
            }
            base.Dispose(disposing);
        }
    }
}

