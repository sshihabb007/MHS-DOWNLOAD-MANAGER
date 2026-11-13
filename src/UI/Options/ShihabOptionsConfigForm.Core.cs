using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ShihabMehediIDM
{
    public partial class ShihabOptionsConfigForm : Form
    {
        private readonly MehediAppConfiguration _shihabConfig;
        private readonly ShihabStateStore _mehediStore;
        private readonly MehediBrowserBridgeServer _shihabBridge;
        private readonly Action _shihabCleanExitCallback;

        public bool ShihabBridgeWasRestarted { get; private set; }
        public bool MehediBridgeRestartSucceeded { get; private set; }

        private Color _surfColor;
        private Color _mutedColor;
        private Font _sectionBannerFont;

        private string _shihabPendingPreventKey;
        private string _mehediPendingForceKey;
        private string _shihabPendingCtxMenuCsv;
        private bool _mehediPendingShowPanel;
        private bool _shihabPendingMiniMode;
        private bool _mehediPendingSkipAuto;
        private int _shihabPendingMinVidKb;
        private string _mehediPendingExcludedAddr;

        private List<string> _snapCustomBrowserPaths;
        private List<ShihabCategoryRule> _snapCategories;
        private List<ShihabSiteLoginCredential> _snapSiteLogins;
        private List<ShihabDebridAccount> _snapDebridAccounts;
        private List<ShihabHostConnectionLimit> _snapHostLimits;
        private bool _committedOk;
        private bool _vaultWasPurged;

        // Tab 1: General
        private CheckBox _shihabBrowserChk;
        private CheckBox _mehediStartupChk;
        private CheckBox _shihabClipChk;
        private CheckedListBox _shihabBrowsersClb;
        private CheckBox _shihabMinTrayChk;
        private CheckBox _shihabCloseTrayChk;
        private ComboBox _shihabLangCombo;
        private ComboBox _shihabScaleCombo;

        // Tab 2: File types
        private TextBox _mehediTypesBox;
        private TextBox _mehediExclBox;
        private CheckBox _shihabCapOpenedChk;
        private NumericUpDown _shihabMinSizeCapNum;

        // Tab 3: Save to
        private CheckBox _shihabServerCreationChk;
        private CheckBox _mehediServerTimeChk;
        private TextBox _shihabTmpBox;

        // Tab 4: Downloads
        private CheckBox _mehediStartDlgChk;
        private CheckBox _shihabProgDlgChk;
        private CheckBox _mehediCompDlgChk;
        private CheckBox _shihabDarkChk;
        private ComboBox _shihabThemePresetCombo;
        private ComboBox _shihabExtSubCombo;
        private CheckBox _shihabAutoExtGlobalChk;
        private CheckBox _shihabDelArchChk;
        private CheckBox _shihabMotwChk;
        private CheckBox _shihabSleepChk;
        private ComboBox _shihabDupCombo;
        private CheckBox _shihabAvEnableChk;
        private TextBox _mehediAvExeBox;
        private TextBox _shihabAvArgsBox;

        public ShihabOptionsConfigForm(
            MehediAppConfiguration shihabConfig,
            ShihabStateStore mehediStore,
            MehediBrowserBridgeServer shihabBridge = null,
            Action shihabCleanExitCallback = null)
        {
            _shihabConfig = shihabConfig;
            _mehediStore = mehediStore;
            _shihabBridge = shihabBridge;
            _shihabCleanExitCallback = shihabCleanExitCallback;
            bool shihabDark = shihabConfig.ShihabDarkMode;

            SnapshotEditableCollections();

            Text = "Configuration — " + ShihabThemePalette.ShihabMainHeaderTitle;
            Size = new Size(680, 560);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ShihabThemePalette.GetMehediWindowBg(shihabDark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(shihabDark);
            Font = ShihabThemePalette.UiRegular9Font;
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, shihabDark);

            _surfColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark);
            _mutedColor = ShihabThemePalette.GetMehediMutedText(shihabDark);
            _sectionBannerFont = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);

            ShihabDarkTabControl mehediTabs = new ShihabDarkTabControl
            {
                ShihabDarkMode = shihabDark,
                Location = new Point(12, 10),
                Size = new Size(640, 458),
                Multiline = true
            };

            TabPage shihabGenTab = CreateMehediPage("General", shihabDark);
            TabPage mehediTypesTab = CreateMehediPage("File types", shihabDark);
            TabPage shihabSaveTab = CreateMehediPage("Save to", shihabDark);
            TabPage mehediDownloadsTab = CreateMehediPage("Downloads", shihabDark);
            TabPage mehediConnTab = CreateMehediPage("Connection", shihabDark);
            TabPage shihabProxyTab = CreateMehediPage("Proxy / Socks", shihabDark);
            TabPage mehediLoginsTab = CreateMehediPage("Sites Logins", shihabDark);
            TabPage shihabDialTab = CreateMehediPage("Dial Up / VPN", shihabDark);
            TabPage mehediSoundsTab = CreateMehediPage("Sounds", shihabDark);
            TabPage shihabDebridTab = CreateMehediPage("Debrid & Vault", shihabDark);
            TabPage shihabUpdatesP2pTab = CreateMehediPage("Updates & P2P", shihabDark);

            _shihabPendingPreventKey = string.IsNullOrWhiteSpace(_shihabConfig.MehediPreventKeyName) ? "Alt" : _shihabConfig.MehediPreventKeyName;
            _mehediPendingForceKey = string.IsNullOrWhiteSpace(_shihabConfig.ShihabForceKeyName) ? "Ins" : _shihabConfig.ShihabForceKeyName;
            _shihabPendingCtxMenuCsv = string.IsNullOrWhiteSpace(_shihabConfig.MehediContextMenuItemsCsv)
                ? MehediAppConfiguration.DefaultMehediContextMenuCsv
                : _shihabConfig.MehediContextMenuItemsCsv;
            _mehediPendingShowPanel = _shihabConfig.ShihabShowVideoFloatingPanel;
            _shihabPendingMiniMode = _shihabConfig.ShihabPanelMiniMode;
            _mehediPendingSkipAuto = _shihabConfig.MehediPanelSkipAutoStart;
            _shihabPendingMinVidKb = Math.Max(0, _shihabConfig.MehediMinVideoSizeKb);
            _mehediPendingExcludedAddr = _shihabConfig.MehediExcludedAddressesSpaceSeparated ?? "";

            BuildGeneralTab(shihabGenTab, shihabDark);
            BuildFileTypesTab(mehediTypesTab, shihabDark);
            BuildSaveToTab(shihabSaveTab, shihabDark);
            BuildDownloadsTab(mehediDownloadsTab, shihabDark);
            BuildConnectionTab(mehediConnTab, shihabDark);
            BuildProxyTab(shihabProxyTab, shihabDark);
            BuildLoginsTab(mehediLoginsTab, shihabDark);
            BuildDialUpVpnTab(shihabDialTab, shihabDark);
            BuildSoundsTab(mehediSoundsTab, shihabDark);
            BuildDebridVaultTab(shihabDebridTab, shihabDark);
            BuildUpdatesP2pTab(shihabUpdatesP2pTab, shihabDark);

            mehediTabs.TabPages.AddRange(new TabPage[]
            {
                shihabGenTab, mehediTypesTab, shihabSaveTab, mehediDownloadsTab,
                mehediConnTab, shihabProxyTab, mehediLoginsTab, shihabDialTab, mehediSoundsTab, shihabDebridTab, shihabUpdatesP2pTab
            });

            BuildBottomActionButtons(mehediTabs);

            FormClosed += delegate
            {
                if (!_committedOk)
                {
                    RestoreSnapshotOnCancel();
                }
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _sectionBannerFont != null)
            {
                _sectionBannerFont.Dispose();
                _sectionBannerFont = null;
            }
            base.Dispose(disposing);
        }

        private void SnapshotEditableCollections()
        {
            _snapCustomBrowserPaths = _shihabConfig.ShihabCustomBrowserPaths != null
                ? new List<string>(_shihabConfig.ShihabCustomBrowserPaths)
                : new List<string>();

            _snapCategories = new List<ShihabCategoryRule>();
            if (_shihabConfig.ShihabCategories != null)
            {
                for (int i = 0; i < _shihabConfig.ShihabCategories.Count; i++)
                {
                    ShihabCategoryRule src = _shihabConfig.ShihabCategories[i];
                    if (src == null) continue;
                    _snapCategories.Add(new ShihabCategoryRule
                    {
                        ShihabName = src.ShihabName,
                        MehediExtensionsSpaceSeparated = src.MehediExtensionsSpaceSeparated,
                        ShihabDefaultFolder = src.ShihabDefaultFolder,
                        MehediRememberLastSavePath = src.MehediRememberLastSavePath
                    });
                }
            }

            _snapSiteLogins = new List<ShihabSiteLoginCredential>();
            if (_shihabConfig.ShihabSiteLogins != null)
            {
                for (int i = 0; i < _shihabConfig.ShihabSiteLogins.Count; i++)
                {
                    ShihabSiteLoginCredential src = _shihabConfig.ShihabSiteLogins[i];
                    if (src == null) continue;
                    _snapSiteLogins.Add(new ShihabSiteLoginCredential
                    {
                        ShihabServerPattern = src.ShihabServerPattern,
                        MehediUserName = src.MehediUserName,
                        ShihabPassword = src.ShihabPassword
                    });
                }
            }

            _snapDebridAccounts = new List<ShihabDebridAccount>();
            if (_shihabConfig.ShihabDebridAccounts != null)
            {
                for (int i = 0; i < _shihabConfig.ShihabDebridAccounts.Count; i++)
                {
                    ShihabDebridAccount src = _shihabConfig.ShihabDebridAccounts[i];
                    if (src == null) continue;
                    _snapDebridAccounts.Add(new ShihabDebridAccount
                    {
                        ShihabProviderName = src.ShihabProviderName,
                        ShihabApiToken = src.ShihabApiToken,
                        ShihabEnabled = src.ShihabEnabled,
                        ShihabAccountStatus = src.ShihabAccountStatus
                    });
                }
            }

            _snapHostLimits = new List<ShihabHostConnectionLimit>();
            if (_shihabConfig.ShihabHostConnectionLimits != null)
            {
                for (int i = 0; i < _shihabConfig.ShihabHostConnectionLimits.Count; i++)
                {
                    ShihabHostConnectionLimit src = _shihabConfig.ShihabHostConnectionLimits[i];
                    if (src == null) continue;
                    _snapHostLimits.Add(new ShihabHostConnectionLimit
                    {
                        ShihabHostPattern = src.ShihabHostPattern,
                        MehediMaxConnections = src.MehediMaxConnections
                    });
                }
            }
        }

        private void RestoreSnapshotOnCancel()
        {
            if (_shihabConfig == null) return;
            _shihabConfig.ShihabCustomBrowserPaths = _snapCustomBrowserPaths ?? new List<string>();
            _shihabConfig.ShihabCategories = _snapCategories ?? new List<ShihabCategoryRule>();
            _shihabConfig.ShihabSiteLogins = _vaultWasPurged ? new List<ShihabSiteLoginCredential>() : (_snapSiteLogins ?? new List<ShihabSiteLoginCredential>());
            _shihabConfig.ShihabDebridAccounts = _vaultWasPurged ? new List<ShihabDebridAccount>() : (_snapDebridAccounts ?? new List<ShihabDebridAccount>());
            _shihabConfig.ShihabHostConnectionLimits = _snapHostLimits ?? new List<ShihabHostConnectionLimit>();
        }

        private void BuildGeneralTab(TabPage shihabGenTab, bool shihabDark)
        {
            AddShihabSectionBanner(shihabGenTab, "Browser/System Integration", shihabDark);

            _shihabBrowserChk = new CheckBox
            {
                Text = "Use advanced browser integration",
                Location = new Point(18, 44),
                AutoSize = true,
                Checked = _shihabConfig.MehediBrowserIntegrationEnabled
            };
            Button mehediRestartBridgeBtn = ShihabThemePalette.CreateMehediStyledButton("Restart", _surfColor, ForeColor, 96);
            mehediRestartBridgeBtn.Height = 26;
            mehediRestartBridgeBtn.Location = new Point(498, 40);
            mehediRestartBridgeBtn.Click += delegate
            {
                if (_shihabBridge != null)
                {
                    ShihabBridgeWasRestarted = true;
                    MehediBridgeRestartSucceeded = _shihabBridge.RestartShihabBridge();
                    MessageBox.Show(
                        this,
                        MehediBridgeRestartSucceeded
                            ? ("Advanced browser integration bridge restarted on 127.0.0.1:" + _shihabConfig.MehediLocalBridgePort + ".")
                            : "Could not re-bind browser integration port.",
                        "MHS Download Manager",
                        MessageBoxButtons.OK,
                        MehediBridgeRestartSucceeded ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show(this, "Browser integration bridge is managed by the main MHS window.", "MHS Download Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            _mehediStartupChk = new CheckBox
            {
                Text = "Launch MHS Download Manager on startup",
                Location = new Point(18, 74),
                AutoSize = true,
                Checked = _shihabConfig.ShihabLaunchOnStartup
            };
            _shihabClipChk = new CheckBox
            {
                Text = "Automatically start downloading of URLs placed to clipboard",
                Location = new Point(18, 100),
                AutoSize = true,
                Checked = _shihabConfig.ShihabMonitorClipboard
            };

            GroupBox mehediBrowsersGrp = new GroupBox
            {
                Text = "Capture downloads from the following browsers:",
                Location = new Point(16, 128),
                Size = new Size(592, 196),
                ForeColor = ForeColor
            };
            _shihabBrowsersClb = new CheckedListBox
            {
                Location = new Point(14, 22),
                Size = new Size(564, 102),
                CheckOnClick = true,
                BackColor = _surfColor,
                ForeColor = ForeColor,
                BorderStyle = BorderStyle.FixedSingle
            };

            string[] mehediKnownBrowsers = new string[]
            {
                "Apple Safari", "Arc Browser", "Brave Browser", "Chromium", "Floorp", "Google Chrome",
                "Helium Browser", "Internet Explorer", "LibreWolf", "Microsoft Edge", "Microsoft Edge Legacy",
                "Mozilla Firefox", "Opera", "Thorium Browser", "Vivaldi", "Waterfox", "Zen Browser"
            };
            HashSet<string> shihabEnabledBrowserSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] mehediSavedBrowsers = (_shihabConfig.MehediEnabledBrowsersCsv ?? MehediAppConfiguration.DefaultShihabBrowsersCsv)
                .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int shihabB = 0; shihabB < mehediSavedBrowsers.Length; shihabB++)
            {
                shihabEnabledBrowserSet.Add(mehediSavedBrowsers[shihabB].Trim());
            }
            for (int mehediK = 0; mehediK < mehediKnownBrowsers.Length; mehediK++)
            {
                _shihabBrowsersClb.Items.Add(mehediKnownBrowsers[mehediK], shihabEnabledBrowserSet.Contains(mehediKnownBrowsers[mehediK]));
            }
            if (_shihabConfig.ShihabCustomBrowserPaths != null)
            {
                for (int shihabC = 0; shihabC < _shihabConfig.ShihabCustomBrowserPaths.Count; shihabC++)
                {
                    string mehediCustLabel = Path.GetFileName(_shihabConfig.ShihabCustomBrowserPaths[shihabC]);
                    if (!string.IsNullOrEmpty(mehediCustLabel))
                    {
                        _shihabBrowsersClb.Items.Add(mehediCustLabel, shihabEnabledBrowserSet.Contains(mehediCustLabel));
                    }
                }
            }

            Button shihabAddBrowserBtn = ShihabThemePalette.CreateMehediStyledButton("Add browser...", _surfColor, ForeColor, 132);
            shihabAddBrowserBtn.Height = 26;
            shihabAddBrowserBtn.Location = new Point(446, 128);
            shihabAddBrowserBtn.Click += delegate
            {
                using (OpenFileDialog mehediOfd = new OpenFileDialog())
                {
                    mehediOfd.Filter = "Browser Executable (*.exe)|*.exe";
                    mehediOfd.Title = "Select Browser Executable — MHS Download Manager";
                    if (mehediOfd.ShowDialog(this) == DialogResult.OK)
                    {
                        if (_shihabConfig.ShihabCustomBrowserPaths == null)
                        {
                            _shihabConfig.ShihabCustomBrowserPaths = new List<string>();
                        }
                        if (!_shihabConfig.ShihabCustomBrowserPaths.Contains(mehediOfd.FileName))
                        {
                            _shihabConfig.ShihabCustomBrowserPaths.Add(mehediOfd.FileName);
                        }
                        string shihabExeName = Path.GetFileName(mehediOfd.FileName);
                        _shihabBrowsersClb.Items.Add(shihabExeName, true);
                    }
                }
            };

            Label mehediKeysLbl = new Label
            {
                Text = "Customize keys to prevent or force downloading with MHS",
                Location = new Point(14, 163),
                AutoSize = true
            };
            Button shihabKeysBtn = ShihabThemePalette.CreateMehediStyledButton("Keys...", _surfColor, ForeColor, 96);
            shihabKeysBtn.Height = 26;
            shihabKeysBtn.Location = new Point(482, 158);
            shihabKeysBtn.Click += delegate
            {
                ShowShihabKeysModal(shihabDark, ref _shihabPendingPreventKey, ref _mehediPendingForceKey);
            };
            mehediBrowsersGrp.Controls.AddRange(new Control[] { _shihabBrowsersClb, shihabAddBrowserBtn, mehediKeysLbl, shihabKeysBtn });

            Label shihabCtxMenuLbl = new Label
            {
                Text = "Customize MHS menu items in context menu of browsers",
                Location = new Point(30, 338),
                AutoSize = true
            };
            Button mehediCtxEditBtn = ShihabThemePalette.CreateMehediStyledButton("Edit...", _surfColor, ForeColor, 96);
            mehediCtxEditBtn.Height = 26;
            mehediCtxEditBtn.Location = new Point(498, 333);
            mehediCtxEditBtn.Click += delegate
            {
                ShowMehediContextMenuModal(shihabDark, ref _shihabPendingCtxMenuCsv);
            };

            Label mehediPanelsLbl = new Label
            {
                Text = "Customize MHS Download panels in browsers",
                Location = new Point(30, 372),
                AutoSize = true
            };
            Button shihabPanelsEditBtn = ShihabThemePalette.CreateMehediStyledButton("Edit...", _surfColor, ForeColor, 96);
            shihabPanelsEditBtn.Height = 26;
            shihabPanelsEditBtn.Location = new Point(498, 367);
            shihabPanelsEditBtn.Click += delegate
            {
                ShowShihabDownloadPanelsModal(
                    shihabDark,
                    ref _mehediPendingShowPanel,
                    ref _shihabPendingMiniMode,
                    ref _mehediPendingSkipAuto,
                    ref _shihabPendingMinVidKb);
            };

            _shihabMinTrayChk = new CheckBox
            {
                Text = "Minimize to Tray",
                Location = new Point(250, 74),
                AutoSize = true,
                Checked = _shihabConfig.ShihabMinimizeToTray
            };
            _shihabCloseTrayChk = new CheckBox
            {
                Text = "Close (X) to Tray",
                Location = new Point(385, 74),
                AutoSize = true,
                Checked = _shihabConfig.ShihabCloseToTray
            };

            Label shihabLangLbl = new Label { Text = "Language:", Location = new Point(30, 398), AutoSize = true };
            _shihabLangCombo = new ComboBox
            {
                Location = new Point(98, 395),
                Width = 175,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = _surfColor,
                ForeColor = ForeColor
            };
            int selLangIdx = 0;
            string curIso = ShihabThemeManager.ExtractLanguageIsoCode(_shihabConfig.ShihabLanguageCode);
            for (int li = 0; li < ShihabThemeManager.SupportedLanguages.Length; li++)
            {
                _shihabLangCombo.Items.Add(ShihabThemeManager.SupportedLanguages[li]);
                if (ShihabThemeManager.SupportedLanguages[li].StartsWith(curIso + " ", StringComparison.OrdinalIgnoreCase))
                {
                    selLangIdx = li;
                }
            }
            _shihabLangCombo.SelectedIndex = selLangIdx;

            Label shihabScaleLbl = new Label { Text = "UI Scale:", Location = new Point(290, 398), AutoSize = true };
            _shihabScaleCombo = new ComboBox
            {
                Location = new Point(350, 395),
                Width = 90,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = _surfColor,
                ForeColor = ForeColor
            };
            int[] scaleChoices = new int[] { 100, 125, 150, 175, 200 };
            int selScaleIdx = 0;
            for (int si = 0; si < scaleChoices.Length; si++)
            {
                _shihabScaleCombo.Items.Add(scaleChoices[si] + "%");
                if (_shihabConfig.ShihabUiScalePercent == scaleChoices[si]) selScaleIdx = si;
            }
            _shihabScaleCombo.SelectedIndex = selScaleIdx;

            shihabGenTab.Controls.AddRange(new Control[]
            {
                _shihabBrowserChk, mehediRestartBridgeBtn, _mehediStartupChk, _shihabMinTrayChk, _shihabCloseTrayChk, _shihabClipChk,
                mehediBrowsersGrp, shihabCtxMenuLbl, mehediCtxEditBtn, mehediPanelsLbl, shihabPanelsEditBtn,
                shihabLangLbl, _shihabLangCombo, shihabScaleLbl, _shihabScaleCombo
            });
        }

        private void BuildFileTypesTab(TabPage mehediTypesTab, bool shihabDark)
        {
            AddShihabSectionBanner(mehediTypesTab, "Downloaded file types", shihabDark);

            Label shihabTypesLbl = new Label
            {
                Text = "Automatically start downloading the following file types:",
                Location = new Point(16, 42),
                AutoSize = true
            };
            _mehediTypesBox = new TextBox
            {
                Location = new Point(16, 64),
                Size = new Size(592, 92),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = _surfColor,
                ForeColor = ForeColor,
                Text = _shihabConfig.MehediAutoCaptureExtensions
            };
            Button shihabTypesDefaultBtn = ShihabThemePalette.CreateMehediStyledButton("Default", _surfColor, ForeColor, 98);
            shihabTypesDefaultBtn.Height = 26;
            shihabTypesDefaultBtn.Location = new Point(510, 162);
            shihabTypesDefaultBtn.Click += delegate
            {
                _mehediTypesBox.Text = MehediAppConfiguration.DefaultShihabAutoCaptureExtensions;
            };

            Label shihabExclLbl = new Label
            {
                Text = "Don't start downloading automatically from the following sites:",
                Location = new Point(16, 196),
                AutoSize = true
            };
            _mehediExclBox = new TextBox
            {
                Location = new Point(16, 218),
                Size = new Size(592, 74),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = _surfColor,
                ForeColor = ForeColor,
                Text = _shihabConfig.ShihabExcludedSitesSpaceSeparated
            };
            Label shihabSepHintLbl = new Label
            {
                Text = "(separate names by spaces)",
                Location = new Point(16, 298),
                AutoSize = true,
                ForeColor = _mutedColor
            };
            Button mehediSitesDefaultBtn = ShihabThemePalette.CreateMehediStyledButton("Default", _surfColor, ForeColor, 98);
            mehediSitesDefaultBtn.Height = 26;
            mehediSitesDefaultBtn.Location = new Point(510, 296);
            mehediSitesDefaultBtn.Click += delegate
            {
                _mehediExclBox.Text = MehediAppConfiguration.DefaultMehediExcludedSites;
            };

            Label shihabAddrExclLbl = new Label
            {
                Text = "Don't start downloading automatically from the following addresses:",
                Location = new Point(16, 336),
                AutoSize = true
            };
            Button mehediAddrEditBtn = ShihabThemePalette.CreateMehediStyledButton("Edit list ...", _surfColor, ForeColor, 165);
            mehediAddrEditBtn.Height = 28;
            mehediAddrEditBtn.Location = new Point(16, 360);
            mehediAddrEditBtn.Click += delegate
            {
                ShowMehediExcludedAddressesModal(shihabDark, ref _mehediPendingExcludedAddr);
            };

            _shihabCapOpenedChk = new CheckBox
            {
                Text = "Capture files opened in browser (PDF, MP3, MP4)",
                Location = new Point(205, 342),
                AutoSize = true,
                Checked = _shihabConfig.ShihabCaptureOpenedFiles
            };
            Label shihabMinSizeCapLbl = new Label
            {
                Text = "Min capture size (KB):",
                Location = new Point(205, 370),
                AutoSize = true
            };
            _shihabMinSizeCapNum = new NumericUpDown
            {
                Location = new Point(345, 367),
                Width = 90,
                Minimum = 0,
                Maximum = 1000000,
                Value = Math.Max(0, _shihabConfig.ShihabCaptureMinSizeKb)
            };

            mehediTypesTab.Controls.AddRange(new Control[]
            {
                shihabTypesLbl, _mehediTypesBox, shihabTypesDefaultBtn,
                shihabExclLbl, _mehediExclBox, shihabSepHintLbl, mehediSitesDefaultBtn,
                shihabAddrExclLbl, mehediAddrEditBtn, _shihabCapOpenedChk, shihabMinSizeCapLbl, _shihabMinSizeCapNum
            });
        }

        private void BuildSaveToTab(TabPage shihabSaveTab, bool shihabDark)
        {
            AddShihabSectionBanner(shihabSaveTab, "Categories, file types, folders", shihabDark);

            GroupBox mehediSaveGrp = new GroupBox
            {
                Text = "Save To...",
                Location = new Point(16, 40),
                Size = new Size(592, 212),
                ForeColor = ForeColor
            };
            Label shihabCatPickLbl = new Label { Text = "Category", Location = new Point(14, 20), AutoSize = true };
            ComboBox mehediCatPick = new ComboBox
            {
                Location = new Point(14, 40),
                Width = 285,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = _surfColor,
                ForeColor = ForeColor
            };
            Button shihabNewCatBtn = ShihabThemePalette.CreateMehediStyledButton("New", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 94);
            shihabNewCatBtn.Height = 25;
            shihabNewCatBtn.Location = new Point(484, 16);
            Button mehediEditCatBtn = ShihabThemePalette.CreateMehediStyledButton("Edit...", _surfColor, ForeColor, 94);
            mehediEditCatBtn.Height = 25;
            mehediEditCatBtn.Location = new Point(484, 45);

            Label shihabCatTypesLbl = new Label
            {
                Text = "Automatically put in \"General\" category the following file types:",
                Location = new Point(14, 74),
                AutoSize = true
            };
            TextBox mehediCatTypesBox = new TextBox
            {
                Location = new Point(14, 94),
                Width = 564,
                BackColor = _surfColor,
                ForeColor = ForeColor
            };
            Label mehediCatDirLbl = new Label
            {
                Text = "Default download directory for \"General\" category",
                Location = new Point(14, 126),
                AutoSize = true
            };
            TextBox shihabCatDirBox = new TextBox
            {
                Location = new Point(14, 146),
                Width = 460,
                BackColor = _surfColor,
                ForeColor = ForeColor
            };
            Button shihabCatBrowse = ShihabThemePalette.CreateMehediStyledButton("Browse", _surfColor, ForeColor, 94);
            shihabCatBrowse.Height = 25;
            shihabCatBrowse.Location = new Point(484, 144);

            CheckBox mehediCatRememberChk = new CheckBox
            {
                Text = "Change folder for \"General\" category on last selected",
                Location = new Point(14, 180),
                AutoSize = true
            };

            bool shihabSuppressCatSync = false;
            Action reloadMehediCategoryCombo = delegate
            {
                shihabSuppressCatSync = true;
                int mehediPrev = mehediCatPick.SelectedIndex;
                mehediCatPick.Items.Clear();
                for (int shihabC = 0; shihabC < _shihabConfig.ShihabCategories.Count; shihabC++)
                {
                    mehediCatPick.Items.Add(_shihabConfig.ShihabCategories[shihabC].ShihabName);
                }
                shihabSuppressCatSync = false;
                if (mehediCatPick.Items.Count > 0)
                {
                    mehediCatPick.SelectedIndex = (mehediPrev >= 0 && mehediPrev < mehediCatPick.Items.Count) ? mehediPrev : 0;
                }
            };

            mehediCatPick.SelectedIndexChanged += delegate
            {
                int shihabIdx = mehediCatPick.SelectedIndex;
                if (shihabIdx < 0 || shihabIdx >= _shihabConfig.ShihabCategories.Count) return;
                shihabSuppressCatSync = true;
                ShihabCategoryRule mehediRule = _shihabConfig.ShihabCategories[shihabIdx];
                bool shihabIsGeneral = string.Equals(mehediRule.ShihabName, "General", StringComparison.OrdinalIgnoreCase);
                mehediEditCatBtn.Enabled = !shihabIsGeneral;
                shihabCatTypesLbl.Text = "Automatically put in \"" + mehediRule.ShihabName + "\" category the following file types:";
                mehediCatDirLbl.Text = "Default download directory for \"" + mehediRule.ShihabName + "\" category";
                mehediCatRememberChk.Text = "Change folder for \"" + mehediRule.ShihabName + "\" category on last selected";
                mehediCatTypesBox.ReadOnly = shihabIsGeneral;
                mehediCatTypesBox.Text = shihabIsGeneral
                    ? "The file types that are not listed in any other category"
                    : (mehediRule.MehediExtensionsSpaceSeparated ?? "");
                shihabCatDirBox.Text = mehediRule.ShihabDefaultFolder ?? "";
                mehediCatRememberChk.Checked = mehediRule.MehediRememberLastSavePath;
                shihabSuppressCatSync = false;
            };

            mehediCatTypesBox.TextChanged += delegate
            {
                if (shihabSuppressCatSync) return;
                int shihabIdx = mehediCatPick.SelectedIndex;
                if (shihabIdx >= 0 && shihabIdx < _shihabConfig.ShihabCategories.Count)
                {
                    ShihabCategoryRule mehediRule = _shihabConfig.ShihabCategories[shihabIdx];
                    if (!string.Equals(mehediRule.ShihabName, "General", StringComparison.OrdinalIgnoreCase))
                    {
                        mehediRule.MehediExtensionsSpaceSeparated = mehediCatTypesBox.Text.Trim();
                    }
                }
            };
            shihabCatDirBox.TextChanged += delegate
            {
                if (shihabSuppressCatSync) return;
                int mehediIdx = mehediCatPick.SelectedIndex;
                if (mehediIdx >= 0 && mehediIdx < _shihabConfig.ShihabCategories.Count)
                {
                    _shihabConfig.ShihabCategories[mehediIdx].ShihabDefaultFolder = shihabCatDirBox.Text.Trim();
                }
            };
            mehediCatRememberChk.CheckedChanged += delegate
            {
                if (shihabSuppressCatSync) return;
                int shihabIdx = mehediCatPick.SelectedIndex;
                if (shihabIdx >= 0 && shihabIdx < _shihabConfig.ShihabCategories.Count)
                {
                    _shihabConfig.ShihabCategories[shihabIdx].MehediRememberLastSavePath = mehediCatRememberChk.Checked;
                }
            };
            shihabCatBrowse.Click += delegate
            {
                using (FolderBrowserDialog mehediFbd = new FolderBrowserDialog())
                {
                    mehediFbd.SelectedPath = shihabCatDirBox.Text;
                    if (mehediFbd.ShowDialog(this) == DialogResult.OK)
                    {
                        shihabCatDirBox.Text = mehediFbd.SelectedPath;
                    }
                }
            };
            shihabNewCatBtn.Click += delegate
            {
                ShowShihabCategoryEditorModal(shihabDark, null, reloadMehediCategoryCombo, mehediCatPick);
            };
            mehediEditCatBtn.Click += delegate
            {
                int shihabIdx = mehediCatPick.SelectedIndex;
                if (shihabIdx >= 0 && shihabIdx < _shihabConfig.ShihabCategories.Count)
                {
                    ShihabCategoryRule mehediSel = _shihabConfig.ShihabCategories[shihabIdx];
                    if (!string.Equals(mehediSel.ShihabName, "General", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowShihabCategoryEditorModal(shihabDark, mehediSel, reloadMehediCategoryCombo, mehediCatPick);
                    }
                }
            };
            reloadMehediCategoryCombo();

            mehediSaveGrp.Controls.AddRange(new Control[]
            {
                shihabCatPickLbl, mehediCatPick, shihabNewCatBtn, mehediEditCatBtn,
                shihabCatTypesLbl, mehediCatTypesBox, mehediCatDirLbl, shihabCatDirBox, shihabCatBrowse, mehediCatRememberChk
            });

            _shihabServerCreationChk = new CheckBox
            {
                Text = "Set file creation date as provided by the server",
                Location = new Point(26, 258),
                AutoSize = true,
                Checked = _shihabConfig.ShihabSetServerCreationDate
            };
            _mehediServerTimeChk = new CheckBox
            {
                Text = "Set file modification date/time as provided by the server",
                Location = new Point(332, 258),
                AutoSize = true,
                Checked = _shihabConfig.ShihabSetServerFileTimestamp
            };

            GroupBox shihabTempGrp = new GroupBox
            {
                Text = "Temporary directory",
                Location = new Point(16, 284),
                Size = new Size(592, 118),
                ForeColor = ForeColor
            };
            _shihabTmpBox = new TextBox
            {
                Location = new Point(14, 24),
                Width = 460,
                Text = _shihabConfig.MehediTemporaryDirectory,
                BackColor = _surfColor,
                ForeColor = ForeColor
            };
            Button mehediTmpBrowse = ShihabThemePalette.CreateMehediStyledButton("Browse", _surfColor, ForeColor, 94);
            mehediTmpBrowse.Height = 25;
            mehediTmpBrowse.Location = new Point(484, 22);
            mehediTmpBrowse.Click += delegate
            {
                using (FolderBrowserDialog shihabFbd = new FolderBrowserDialog())
                {
                    shihabFbd.SelectedPath = _shihabTmpBox.Text;
                    if (shihabFbd.ShowDialog(this) == DialogResult.OK)
                    {
                        _shihabTmpBox.Text = shihabFbd.SelectedPath;
                    }
                }
            };
            Label shihabTmpNoteLbl = new Label
            {
                Text = "Temporary directory is required for storing file parts during download.\r\nIf you have several physical drives on your computer, you should select different physical drives for temporary directory and \"Save To\" folders for faster assembling of downloaded files.",
                Location = new Point(14, 56),
                Size = new Size(564, 54),
                ForeColor = _mutedColor
            };
            shihabTempGrp.Controls.AddRange(new Control[] { _shihabTmpBox, mehediTmpBrowse, shihabTmpNoteLbl });

            shihabSaveTab.Controls.AddRange(new Control[] { mehediSaveGrp, _shihabServerCreationChk, _mehediServerTimeChk, shihabTempGrp });
        }

        private void BuildDownloadsTab(TabPage mehediDownloadsTab, bool shihabDark)
        {
            AddShihabSectionBanner(mehediDownloadsTab, "Download dialogs, duplicates & virus protection", shihabDark);

            _mehediStartDlgChk = new CheckBox { Text = "Show start download dialog ('Download File Info')", Location = new Point(18, 42), AutoSize = true, Checked = _shihabConfig.ShihabShowStartDownloadDialog };
            _shihabProgDlgChk = new CheckBox { Text = "Show download progress dialog during active transfers", Location = new Point(18, 66), AutoSize = true, Checked = _shihabConfig.MehediShowDownloadProgressDialog };
            _mehediCompDlgChk = new CheckBox { Text = "Show download complete dialog when finished", Location = new Point(18, 90), AutoSize = true, Checked = _shihabConfig.ShihabShowDownloadCompleteDialog };
            _shihabDarkChk = new CheckBox { Text = "Dark Mode UI Theme", Location = new Point(18, 114), AutoSize = true, Checked = _shihabConfig.ShihabDarkMode };

            Label shihabThemePresetLbl = new Label { Text = "Theme:", Location = new Point(185, 116), AutoSize = true };
            _shihabThemePresetCombo = new ComboBox
            {
                Location = new Point(238, 113),
                Width = 165,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = _surfColor,
                ForeColor = ForeColor
            };
            List<string> allThemeNames = ShihabThemeManager.GetAllAvailableThemeNames(_mehediStore != null ? _mehediStore.MehediBaseDirectory : "");
            int selThemeIdx = 0;
            for (int ti = 0; ti < allThemeNames.Count; ti++)
            {
                _shihabThemePresetCombo.Items.Add(allThemeNames[ti]);
                if (string.Equals(allThemeNames[ti], _shihabConfig.ShihabThemeName, StringComparison.OrdinalIgnoreCase)) selThemeIdx = ti;
            }
            if (_shihabThemePresetCombo.Items.Count > 0) _shihabThemePresetCombo.SelectedIndex = selThemeIdx;

            bool suppressThemeSync = false;
            _shihabThemePresetCombo.SelectedIndexChanged += delegate
            {
                if (suppressThemeSync || _shihabThemePresetCombo.SelectedItem == null) return;
                suppressThemeSync = true;
                ShihabCustomThemePreset p = ShihabThemeManager.ResolveThemePalette(
                    _shihabThemePresetCombo.SelectedItem.ToString(),
                    _mehediStore != null ? _mehediStore.MehediBaseDirectory : "");
                _shihabDarkChk.Checked = ShihabThemeManager.IsDarkThemePreset(p);
                suppressThemeSync = false;
            };
            _shihabDarkChk.CheckedChanged += delegate
            {
                if (suppressThemeSync) return;
                suppressThemeSync = true;
                string targetTheme = _shihabDarkChk.Checked ? "Obsidian Dark" : "Classic Light";
                int idx = _shihabThemePresetCombo.FindStringExact(targetTheme);
                if (idx >= 0) _shihabThemePresetCombo.SelectedIndex = idx;
                suppressThemeSync = false;
            };

            Label shihabExtSubLbl = new Label { Text = "Subfolder:", Location = new Point(412, 116), AutoSize = true };
            _shihabExtSubCombo = new ComboBox
            {
                Location = new Point(474, 113),
                Width = 82,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = _surfColor,
                ForeColor = ForeColor
            };
            _shihabExtSubCombo.Items.AddRange(new object[] { "Smart", "Always", "Never" });
            int subIdx = _shihabExtSubCombo.FindStringExact(_shihabConfig.ShihabExtractSubfolderMode ?? "Smart");
            _shihabExtSubCombo.SelectedIndex = subIdx >= 0 ? subIdx : 0;

            _shihabAutoExtGlobalChk = new CheckBox { Text = "Auto-extract archives", Location = new Point(18, 138), AutoSize = true, Checked = _shihabConfig.ShihabAutoExtractArchives };
            _shihabDelArchChk = new CheckBox { Text = "Del archive", Location = new Point(178, 138), AutoSize = true, Checked = _shihabConfig.ShihabDeleteArchiveAfterExtract };
            _shihabMotwChk = new CheckBox { Text = "Mark-of-the-Web", Location = new Point(285, 138), AutoSize = true, Checked = _shihabConfig.ShihabWriteZoneIdentifier };
            _shihabSleepChk = new CheckBox { Text = "Prevent Sleep", Location = new Point(435, 138), AutoSize = true, Checked = _shihabConfig.ShihabPreventSystemSleep };

            Label mehediDupLbl = new Label { Text = "If target file already exists on disk:", Location = new Point(18, 166), AutoSize = true };
            _shihabDupCombo = new ComboBox
            {
                Location = new Point(245, 163),
                Width = 340,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = _surfColor,
                ForeColor = ForeColor
            };
            _shihabDupCombo.Items.Add("Auto-rename new file with number (file_2.ext)");
            _shihabDupCombo.Items.Add("Overwrite existing file");
            _shihabDupCombo.SelectedIndex = (_shihabConfig.MehediDuplicateFileMode == 1) ? 1 : 0;

            GroupBox mehediAvGrp = new GroupBox
            {
                Text = "Virus checking on download completion",
                Location = new Point(16, 204),
                Size = new Size(592, 125),
                ForeColor = ForeColor
            };
            _shihabAvEnableChk = new CheckBox
            {
                Text = "Automatically check downloaded files with virus scanner:",
                Location = new Point(14, 24),
                AutoSize = true,
                Checked = _shihabConfig.ShihabEnableVirusCheck
            };
            _mehediAvExeBox = new TextBox
            {
                Location = new Point(14, 52),
                Width = 460,
                Text = _shihabConfig.MehediVirusScannerExe ?? "",
                BackColor = _surfColor,
                ForeColor = ForeColor
            };
            Button shihabAvBrowseBtn = ShihabThemePalette.CreateMehediStyledButton("Browse", _surfColor, ForeColor, 94);
            shihabAvBrowseBtn.Height = 25;
            shihabAvBrowseBtn.Location = new Point(484, 50);
            shihabAvBrowseBtn.Click += delegate
            {
                using (OpenFileDialog mehediOfd = new OpenFileDialog())
                {
                    mehediOfd.Filter = "Antivirus Scanner (*.exe)|*.exe";
                    if (mehediOfd.ShowDialog(this) == DialogResult.OK) _mehediAvExeBox.Text = mehediOfd.FileName;
                }
            };
            Label mehediAvArgsLbl = new Label { Text = "Command line parameters (%1 = downloaded file):", Location = new Point(14, 88), AutoSize = true };
            _shihabAvArgsBox = new TextBox
            {
                Location = new Point(315, 85),
                Width = 263,
                Text = string.IsNullOrWhiteSpace(_shihabConfig.ShihabVirusScannerArgs) ? "\"%1\"" : _shihabConfig.ShihabVirusScannerArgs,
                BackColor = _surfColor,
                ForeColor = ForeColor
            };
            mehediAvGrp.Controls.AddRange(new Control[] { _shihabAvEnableChk, _mehediAvExeBox, shihabAvBrowseBtn, mehediAvArgsLbl, _shihabAvArgsBox });

            GroupBox mehediNativeGrp = new GroupBox
            {
                Text = "Optional Chrome / Edge Native Messaging Host (com.shihab.mehedi.idm)",
                Location = new Point(16, 336),
                Size = new Size(592, 64),
                ForeColor = ForeColor
            };
            Label shihabExtIdLbl = new Label { Text = "Extension ID:", Location = new Point(12, 27), AutoSize = true };
            TextBox mehediExtIdBox = new TextBox { Location = new Point(98, 24), Width = 305, BackColor = _surfColor, ForeColor = ForeColor };
            Button shihabRegNativeBtn = ShihabThemePalette.CreateMehediStyledButton("Register Native Host", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 165);
            shihabRegNativeBtn.Height = 26;
            shihabRegNativeBtn.Location = new Point(413, 22);
            shihabRegNativeBtn.Click += delegate
            {
                try
                {
                    string mehediPath = MehediBrowserBridgeServer.RegisterShihabNativeMessagingHostForCurrentUser(
                        mehediExtIdBox.Text,
                        _mehediStore.MehediBaseDirectory);
                    MessageBox.Show(this, "Registered Native Messaging manifest at:\n" + mehediPath, "MHS Download Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception shihabEx)
                {
                    MessageBox.Show(this, shihabEx.Message, "MHS Download Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            mehediNativeGrp.Controls.AddRange(new Control[] { shihabExtIdLbl, mehediExtIdBox, shihabRegNativeBtn });

            mehediDownloadsTab.Controls.AddRange(new Control[]
            {
                _mehediStartDlgChk, _shihabProgDlgChk, _mehediCompDlgChk, _shihabDarkChk,
                shihabThemePresetLbl, _shihabThemePresetCombo, shihabExtSubLbl, _shihabExtSubCombo,
                _shihabAutoExtGlobalChk, _shihabDelArchChk, _shihabMotwChk, _shihabSleepChk,
                mehediDupLbl, _shihabDupCombo, mehediAvGrp, mehediNativeGrp
            });
        }

        private void AddShihabSectionBanner(TabPage mehediPage, string shihabHeaderText, bool mehediDark)
        {
            Label shihabHdr = new Label
            {
                Text = shihabHeaderText,
                Location = new Point(16, 10),
                Size = new Size(592, 20),
                TextAlign = ContentAlignment.MiddleRight,
                Font = _sectionBannerFont,
                ForeColor = ShihabThemePalette.GetShihabPrimaryText(mehediDark)
            };
            Panel mehediLine = new Panel
            {
                Location = new Point(16, 32),
                Size = new Size(592, 1),
                BackColor = mehediDark ? Color.FromArgb(62, 74, 96) : Color.FromArgb(185, 194, 208)
            };
            mehediPage.Controls.Add(shihabHdr);
            mehediPage.Controls.Add(mehediLine);
        }

        private static Form CreateMehediSubModal(string shihabTitle, Size mehediSize, bool shihabDark)
        {
            Form mehediModal = new Form
            {
                Text = shihabTitle,
                Size = mehediSize,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = ShihabThemePalette.GetMehediWindowBg(shihabDark),
                ForeColor = ShihabThemePalette.GetShihabPrimaryText(shihabDark),
                Font = ShihabThemePalette.UiRegular9Font,
                Icon = ShihabThemePalette.CreateShihabAppIcon()
            };
            ShihabThemePalette.ApplyMehediNativeDarkFrame(mehediModal, shihabDark);
            return mehediModal;
        }

        private static TabPage CreateMehediPage(string shihabTitle, bool mehediDark)
        {
            return new TabPage(shihabTitle)
            {
                BackColor = ShihabThemePalette.GetMehediWindowBg(mehediDark),
                ForeColor = ShihabThemePalette.GetShihabPrimaryText(mehediDark)
            };
        }
    }
}

