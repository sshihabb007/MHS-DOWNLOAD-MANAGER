using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShihabMehediIDM
{
    public class ShihabOptionsConfigForm : Form
    {
        private readonly MehediAppConfiguration _shihabConfig;
        private readonly ShihabStateStore _mehediStore;
        private readonly MehediBrowserBridgeServer _shihabBridge;
        private readonly Action _shihabCleanExitCallback;

        public bool ShihabBridgeWasRestarted { get; private set; }
        public bool MehediBridgeRestartSucceeded { get; private set; }

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

            Text = "Configuration — " + ShihabThemePalette.ShihabMainHeaderTitle;
            Size = new Size(680, 560);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ShihabThemePalette.GetMehediWindowBg(shihabDark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(shihabDark);
            Font = new Font("Segoe UI", 9f);
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, shihabDark);

            Color mehediSurf = ShihabThemePalette.GetShihabSurfaceBg(shihabDark);
            Color shihabMuted = ShihabThemePalette.GetMehediMutedText(shihabDark);

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

            // Working copies for modal sub-dialogs
            string shihabPendingPreventKey = string.IsNullOrWhiteSpace(_shihabConfig.MehediPreventKeyName) ? "Alt" : _shihabConfig.MehediPreventKeyName;
            string mehediPendingForceKey = string.IsNullOrWhiteSpace(_shihabConfig.ShihabForceKeyName) ? "Ins" : _shihabConfig.ShihabForceKeyName;
            string shihabPendingCtxMenuCsv = string.IsNullOrWhiteSpace(_shihabConfig.MehediContextMenuItemsCsv)
                ? MehediAppConfiguration.DefaultMehediContextMenuCsv
                : _shihabConfig.MehediContextMenuItemsCsv;
            bool mehediPendingShowPanel = _shihabConfig.ShihabShowVideoFloatingPanel;
            bool shihabPendingMiniMode = _shihabConfig.ShihabPanelMiniMode;
            bool mehediPendingSkipAuto = _shihabConfig.MehediPanelSkipAutoStart;
            int shihabPendingMinVidKb = Math.Max(0, _shihabConfig.MehediMinVideoSizeKb);
            string mehediPendingExcludedAddr = _shihabConfig.MehediExcludedAddressesSpaceSeparated ?? "";

            // ==================== 1. GENERAL TAB ====================
            AddShihabSectionBanner(shihabGenTab, "Browser/System Integration", shihabDark);

            CheckBox shihabBrowserChk = new CheckBox
            {
                Text = "Use advanced browser integration",
                Location = new Point(18, 44),
                AutoSize = true,
                Checked = _shihabConfig.MehediBrowserIntegrationEnabled
            };
            Button mehediRestartBridgeBtn = ShihabThemePalette.CreateMehediStyledButton("Restart", mehediSurf, ForeColor, 96);
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

            CheckBox mehediStartupChk = new CheckBox
            {
                Text = "Launch MHS Download Manager on startup",
                Location = new Point(18, 74),
                AutoSize = true,
                Checked = _shihabConfig.ShihabLaunchOnStartup
            };
            CheckBox shihabClipChk = new CheckBox
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
            CheckedListBox shihabBrowsersClb = new CheckedListBox
            {
                Location = new Point(14, 22),
                Size = new Size(564, 102),
                CheckOnClick = true,
                BackColor = mehediSurf,
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
                shihabBrowsersClb.Items.Add(mehediKnownBrowsers[mehediK], shihabEnabledBrowserSet.Contains(mehediKnownBrowsers[mehediK]));
            }
            if (_shihabConfig.ShihabCustomBrowserPaths != null)
            {
                for (int shihabC = 0; shihabC < _shihabConfig.ShihabCustomBrowserPaths.Count; shihabC++)
                {
                    string mehediCustLabel = Path.GetFileName(_shihabConfig.ShihabCustomBrowserPaths[shihabC]);
                    if (!string.IsNullOrEmpty(mehediCustLabel))
                    {
                        shihabBrowsersClb.Items.Add(mehediCustLabel, shihabEnabledBrowserSet.Contains(mehediCustLabel));
                    }
                }
            }

            Button shihabAddBrowserBtn = ShihabThemePalette.CreateMehediStyledButton("Add browser...", mehediSurf, ForeColor, 132);
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
                        shihabBrowsersClb.Items.Add(shihabExeName, true);
                    }
                }
            };

            Label mehediKeysLbl = new Label
            {
                Text = "Customize keys to prevent or force downloading with MHS",
                Location = new Point(14, 163),
                AutoSize = true
            };
            Button shihabKeysBtn = ShihabThemePalette.CreateMehediStyledButton("Keys...", mehediSurf, ForeColor, 96);
            shihabKeysBtn.Height = 26;
            shihabKeysBtn.Location = new Point(482, 158);
            shihabKeysBtn.Click += delegate
            {
                ShowShihabKeysModal(shihabDark, ref shihabPendingPreventKey, ref mehediPendingForceKey);
            };
            mehediBrowsersGrp.Controls.AddRange(new Control[] { shihabBrowsersClb, shihabAddBrowserBtn, mehediKeysLbl, shihabKeysBtn });

            Label shihabCtxMenuLbl = new Label
            {
                Text = "Customize MHS menu items in context menu of browsers",
                Location = new Point(30, 338),
                AutoSize = true
            };
            Button mehediCtxEditBtn = ShihabThemePalette.CreateMehediStyledButton("Edit...", mehediSurf, ForeColor, 96);
            mehediCtxEditBtn.Height = 26;
            mehediCtxEditBtn.Location = new Point(498, 333);
            mehediCtxEditBtn.Click += delegate
            {
                ShowMehediContextMenuModal(shihabDark, ref shihabPendingCtxMenuCsv);
            };

            Label mehediPanelsLbl = new Label
            {
                Text = "Customize MHS Download panels in browsers",
                Location = new Point(30, 372),
                AutoSize = true
            };
            Button shihabPanelsEditBtn = ShihabThemePalette.CreateMehediStyledButton("Edit...", mehediSurf, ForeColor, 96);
            shihabPanelsEditBtn.Height = 26;
            shihabPanelsEditBtn.Location = new Point(498, 367);
            shihabPanelsEditBtn.Click += delegate
            {
                ShowShihabDownloadPanelsModal(
                    shihabDark,
                    ref mehediPendingShowPanel,
                    ref shihabPendingMiniMode,
                    ref mehediPendingSkipAuto,
                    ref shihabPendingMinVidKb);
            };

            CheckBox shihabMinTrayChk = new CheckBox
            {
                Text = "Minimize to Tray",
                Location = new Point(250, 74),
                AutoSize = true,
                Checked = _shihabConfig.ShihabMinimizeToTray
            };
            CheckBox shihabCloseTrayChk = new CheckBox
            {
                Text = "Close (X) to Tray",
                Location = new Point(385, 74),
                AutoSize = true,
                Checked = _shihabConfig.ShihabCloseToTray
            };

            Label shihabLangLbl = new Label { Text = "Language:", Location = new Point(30, 398), AutoSize = true };
            ComboBox shihabLangCombo = new ComboBox
            {
                Location = new Point(98, 395),
                Width = 175,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = mehediSurf,
                ForeColor = ForeColor
            };
            int selLangIdx = 0;
            string curIso = ShihabThemeManager.ExtractLanguageIsoCode(_shihabConfig.ShihabLanguageCode);
            for (int li = 0; li < ShihabThemeManager.SupportedLanguages.Length; li++)
            {
                shihabLangCombo.Items.Add(ShihabThemeManager.SupportedLanguages[li]);
                if (ShihabThemeManager.SupportedLanguages[li].StartsWith(curIso + " ", StringComparison.OrdinalIgnoreCase))
                {
                    selLangIdx = li;
                }
            }
            shihabLangCombo.SelectedIndex = selLangIdx;

            Label shihabScaleLbl = new Label { Text = "UI Scale:", Location = new Point(290, 398), AutoSize = true };
            ComboBox shihabScaleCombo = new ComboBox
            {
                Location = new Point(350, 395),
                Width = 90,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = mehediSurf,
                ForeColor = ForeColor
            };
            int[] scaleChoices = new int[] { 100, 125, 150, 175, 200 };
            int selScaleIdx = 0;
            for (int si = 0; si < scaleChoices.Length; si++)
            {
                shihabScaleCombo.Items.Add(scaleChoices[si] + "%");
                if (_shihabConfig.ShihabUiScalePercent == scaleChoices[si]) selScaleIdx = si;
            }
            shihabScaleCombo.SelectedIndex = selScaleIdx;

            shihabGenTab.Controls.AddRange(new Control[]
            {
                shihabBrowserChk, mehediRestartBridgeBtn, mehediStartupChk, shihabMinTrayChk, shihabCloseTrayChk, shihabClipChk,
                mehediBrowsersGrp, shihabCtxMenuLbl, mehediCtxEditBtn, mehediPanelsLbl, shihabPanelsEditBtn,
                shihabLangLbl, shihabLangCombo, shihabScaleLbl, shihabScaleCombo
            });

            // ==================== 2. FILE TYPES TAB ====================
            AddShihabSectionBanner(mehediTypesTab, "Downloaded file types", shihabDark);

            Label shihabTypesLbl = new Label
            {
                Text = "Automatically start downloading the following file types:",
                Location = new Point(16, 42),
                AutoSize = true
            };
            TextBox mehediTypesBox = new TextBox
            {
                Location = new Point(16, 64),
                Size = new Size(592, 92),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = mehediSurf,
                ForeColor = ForeColor,
                Text = _shihabConfig.MehediAutoCaptureExtensions
            };
            Button shihabTypesDefaultBtn = ShihabThemePalette.CreateMehediStyledButton("Default", mehediSurf, ForeColor, 98);
            shihabTypesDefaultBtn.Height = 26;
            shihabTypesDefaultBtn.Location = new Point(510, 162);
            shihabTypesDefaultBtn.Click += delegate
            {
                mehediTypesBox.Text = MehediAppConfiguration.DefaultShihabAutoCaptureExtensions;
            };

            Label shihabExclLbl = new Label
            {
                Text = "Don't start downloading automatically from the following sites:",
                Location = new Point(16, 196),
                AutoSize = true
            };
            TextBox mehediExclBox = new TextBox
            {
                Location = new Point(16, 218),
                Size = new Size(592, 74),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = mehediSurf,
                ForeColor = ForeColor,
                Text = _shihabConfig.ShihabExcludedSitesSpaceSeparated
            };
            Label shihabSepHintLbl = new Label
            {
                Text = "(separate names by spaces)",
                Location = new Point(16, 298),
                AutoSize = true,
                ForeColor = shihabMuted
            };
            Button mehediSitesDefaultBtn = ShihabThemePalette.CreateMehediStyledButton("Default", mehediSurf, ForeColor, 98);
            mehediSitesDefaultBtn.Height = 26;
            mehediSitesDefaultBtn.Location = new Point(510, 296);
            mehediSitesDefaultBtn.Click += delegate
            {
                mehediExclBox.Text = MehediAppConfiguration.DefaultMehediExcludedSites;
            };

            Label shihabAddrExclLbl = new Label
            {
                Text = "Don't start downloading automatically from the following addresses:",
                Location = new Point(16, 336),
                AutoSize = true
            };
            Button mehediAddrEditBtn = ShihabThemePalette.CreateMehediStyledButton("Edit list ...", mehediSurf, ForeColor, 165);
            mehediAddrEditBtn.Height = 28;
            mehediAddrEditBtn.Location = new Point(16, 360);
            mehediAddrEditBtn.Click += delegate
            {
                ShowMehediExcludedAddressesModal(shihabDark, ref mehediPendingExcludedAddr);
            };

            CheckBox shihabCapOpenedChk = new CheckBox
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
            NumericUpDown shihabMinSizeCapNum = new NumericUpDown
            {
                Location = new Point(345, 367),
                Width = 90,
                Minimum = 0,
                Maximum = 1000000,
                Value = Math.Max(0, _shihabConfig.ShihabCaptureMinSizeKb)
            };

            mehediTypesTab.Controls.AddRange(new Control[]
            {
                shihabTypesLbl, mehediTypesBox, shihabTypesDefaultBtn,
                shihabExclLbl, mehediExclBox, shihabSepHintLbl, mehediSitesDefaultBtn,
                shihabAddrExclLbl, mehediAddrEditBtn, shihabCapOpenedChk, shihabMinSizeCapLbl, shihabMinSizeCapNum
            });

            // ==================== 3. SAVE TO TAB ====================
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
                BackColor = mehediSurf,
                ForeColor = ForeColor
            };
            Button shihabNewCatBtn = ShihabThemePalette.CreateMehediStyledButton("New", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 94);
            shihabNewCatBtn.Height = 25;
            shihabNewCatBtn.Location = new Point(484, 16);
            Button mehediEditCatBtn = ShihabThemePalette.CreateMehediStyledButton("Edit...", mehediSurf, ForeColor, 94);
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
                BackColor = mehediSurf,
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
                BackColor = mehediSurf,
                ForeColor = ForeColor
            };
            Button shihabCatBrowse = ShihabThemePalette.CreateMehediStyledButton("Browse", mehediSurf, ForeColor, 94);
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

            CheckBox shihabServerCreationChk = new CheckBox
            {
                Text = "Set file creation date as provided by the server",
                Location = new Point(26, 258),
                AutoSize = true,
                Checked = _shihabConfig.ShihabSetServerCreationDate
            };
            CheckBox mehediServerTimeChk = new CheckBox
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
            TextBox shihabTmpBox = new TextBox
            {
                Location = new Point(14, 24),
                Width = 460,
                Text = _shihabConfig.MehediTemporaryDirectory,
                BackColor = mehediSurf,
                ForeColor = ForeColor
            };
            Button mehediTmpBrowse = ShihabThemePalette.CreateMehediStyledButton("Browse", mehediSurf, ForeColor, 94);
            mehediTmpBrowse.Height = 25;
            mehediTmpBrowse.Location = new Point(484, 22);
            mehediTmpBrowse.Click += delegate
            {
                using (FolderBrowserDialog shihabFbd = new FolderBrowserDialog())
                {
                    shihabFbd.SelectedPath = shihabTmpBox.Text;
                    if (shihabFbd.ShowDialog(this) == DialogResult.OK)
                    {
                        shihabTmpBox.Text = shihabFbd.SelectedPath;
                    }
                }
            };
            Label shihabTmpNoteLbl = new Label
            {
                Text = "Temporary directory is required for storing file parts during download.\r\nIf you have several physical drives on your computer, you should select different physical drives for temporary directory and \"Save To\" folders for faster assembling of downloaded files.",
                Location = new Point(14, 56),
                Size = new Size(564, 54),
                ForeColor = shihabMuted
            };
            shihabTempGrp.Controls.AddRange(new Control[] { shihabTmpBox, mehediTmpBrowse, shihabTmpNoteLbl });

            shihabSaveTab.Controls.AddRange(new Control[] { mehediSaveGrp, shihabServerCreationChk, mehediServerTimeChk, shihabTempGrp });

            // ==================== 4. DOWNLOADS TAB ====================
            AddShihabSectionBanner(mehediDownloadsTab, "Download dialogs, duplicates & virus protection", shihabDark);

            CheckBox mehediStartDlgChk = new CheckBox { Text = "Show start download dialog ('Download File Info')", Location = new Point(18, 42), AutoSize = true, Checked = _shihabConfig.ShihabShowStartDownloadDialog };
            CheckBox shihabProgDlgChk = new CheckBox { Text = "Show download progress dialog during active transfers", Location = new Point(18, 66), AutoSize = true, Checked = _shihabConfig.MehediShowDownloadProgressDialog };
            CheckBox mehediCompDlgChk = new CheckBox { Text = "Show download complete dialog when finished", Location = new Point(18, 90), AutoSize = true, Checked = _shihabConfig.ShihabShowDownloadCompleteDialog };
            CheckBox shihabDarkChk = new CheckBox { Text = "Dark Mode UI Theme", Location = new Point(18, 114), AutoSize = true, Checked = _shihabConfig.ShihabDarkMode };

            Label shihabThemePresetLbl = new Label { Text = "Theme:", Location = new Point(185, 116), AutoSize = true };
            ComboBox shihabThemePresetCombo = new ComboBox
            {
                Location = new Point(238, 113),
                Width = 165,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = mehediSurf,
                ForeColor = ForeColor
            };
            List<string> allThemeNames = ShihabThemeManager.GetAllAvailableThemeNames(_mehediStore != null ? _mehediStore.MehediBaseDirectory : "");
            int selThemeIdx = 0;
            for (int ti = 0; ti < allThemeNames.Count; ti++)
            {
                shihabThemePresetCombo.Items.Add(allThemeNames[ti]);
                if (string.Equals(allThemeNames[ti], _shihabConfig.ShihabThemeName, StringComparison.OrdinalIgnoreCase)) selThemeIdx = ti;
            }
            if (shihabThemePresetCombo.Items.Count > 0) shihabThemePresetCombo.SelectedIndex = selThemeIdx;

            bool suppressThemeSync = false;
            shihabThemePresetCombo.SelectedIndexChanged += delegate
            {
                if (suppressThemeSync || shihabThemePresetCombo.SelectedItem == null) return;
                suppressThemeSync = true;
                ShihabCustomThemePreset p = ShihabThemeManager.ResolveThemePalette(
                    shihabThemePresetCombo.SelectedItem.ToString(),
                    _mehediStore != null ? _mehediStore.MehediBaseDirectory : "");
                shihabDarkChk.Checked = ShihabThemeManager.IsDarkThemePreset(p);
                suppressThemeSync = false;
            };
            shihabDarkChk.CheckedChanged += delegate
            {
                if (suppressThemeSync) return;
                suppressThemeSync = true;
                string targetTheme = shihabDarkChk.Checked ? "Obsidian Dark" : "Classic Light";
                int idx = shihabThemePresetCombo.FindStringExact(targetTheme);
                if (idx >= 0) shihabThemePresetCombo.SelectedIndex = idx;
                suppressThemeSync = false;
            };

            Label shihabExtSubLbl = new Label { Text = "Subfolder:", Location = new Point(412, 116), AutoSize = true };
            ComboBox shihabExtSubCombo = new ComboBox
            {
                Location = new Point(474, 113),
                Width = 82,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = mehediSurf,
                ForeColor = ForeColor
            };
            shihabExtSubCombo.Items.AddRange(new object[] { "Smart", "Always", "Never" });
            int subIdx = shihabExtSubCombo.FindStringExact(_shihabConfig.ShihabExtractSubfolderMode ?? "Smart");
            shihabExtSubCombo.SelectedIndex = subIdx >= 0 ? subIdx : 0;

            CheckBox shihabAutoExtGlobalChk = new CheckBox { Text = "Auto-extract archives", Location = new Point(18, 138), AutoSize = true, Checked = _shihabConfig.ShihabAutoExtractArchives };
            CheckBox shihabDelArchChk = new CheckBox { Text = "Del archive", Location = new Point(178, 138), AutoSize = true, Checked = _shihabConfig.ShihabDeleteArchiveAfterExtract };
            CheckBox shihabMotwChk = new CheckBox { Text = "Mark-of-the-Web", Location = new Point(285, 138), AutoSize = true, Checked = _shihabConfig.ShihabWriteZoneIdentifier };
            CheckBox shihabSleepChk = new CheckBox { Text = "Prevent Sleep", Location = new Point(435, 138), AutoSize = true, Checked = _shihabConfig.ShihabPreventSystemSleep };

            Label mehediDupLbl = new Label { Text = "If target file already exists on disk:", Location = new Point(18, 166), AutoSize = true };
            ComboBox shihabDupCombo = new ComboBox
            {
                Location = new Point(245, 163),
                Width = 340,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = mehediSurf,
                ForeColor = ForeColor
            };
            shihabDupCombo.Items.Add("Auto-rename new file with number (file_2.ext)");
            shihabDupCombo.Items.Add("Overwrite existing file");
            shihabDupCombo.SelectedIndex = (_shihabConfig.MehediDuplicateFileMode == 1) ? 1 : 0;

            GroupBox mehediAvGrp = new GroupBox
            {
                Text = "Virus checking on download completion",
                Location = new Point(16, 204),
                Size = new Size(592, 125),
                ForeColor = ForeColor
            };
            CheckBox shihabAvEnableChk = new CheckBox
            {
                Text = "Automatically check downloaded files with virus scanner:",
                Location = new Point(14, 24),
                AutoSize = true,
                Checked = _shihabConfig.ShihabEnableVirusCheck
            };
            TextBox mehediAvExeBox = new TextBox
            {
                Location = new Point(14, 52),
                Width = 460,
                Text = _shihabConfig.MehediVirusScannerExe ?? "",
                BackColor = mehediSurf,
                ForeColor = ForeColor
            };
            Button shihabAvBrowseBtn = ShihabThemePalette.CreateMehediStyledButton("Browse", mehediSurf, ForeColor, 94);
            shihabAvBrowseBtn.Height = 25;
            shihabAvBrowseBtn.Location = new Point(484, 50);
            shihabAvBrowseBtn.Click += delegate
            {
                using (OpenFileDialog mehediOfd = new OpenFileDialog())
                {
                    mehediOfd.Filter = "Antivirus Scanner (*.exe)|*.exe";
                    if (mehediOfd.ShowDialog(this) == DialogResult.OK) mehediAvExeBox.Text = mehediOfd.FileName;
                }
            };
            Label mehediAvArgsLbl = new Label { Text = "Command line parameters (%1 = downloaded file):", Location = new Point(14, 88), AutoSize = true };
            TextBox shihabAvArgsBox = new TextBox
            {
                Location = new Point(315, 85),
                Width = 263,
                Text = string.IsNullOrWhiteSpace(_shihabConfig.ShihabVirusScannerArgs) ? "\"%1\"" : _shihabConfig.ShihabVirusScannerArgs,
                BackColor = mehediSurf,
                ForeColor = ForeColor
            };
            mehediAvGrp.Controls.AddRange(new Control[] { shihabAvEnableChk, mehediAvExeBox, shihabAvBrowseBtn, mehediAvArgsLbl, shihabAvArgsBox });

            GroupBox mehediNativeGrp = new GroupBox
            {
                Text = "Optional Chrome / Edge Native Messaging Host (com.shihab.mehedi.idm)",
                Location = new Point(16, 336),
                Size = new Size(592, 64),
                ForeColor = ForeColor
            };
            Label shihabExtIdLbl = new Label { Text = "Extension ID:", Location = new Point(12, 27), AutoSize = true };
            TextBox mehediExtIdBox = new TextBox { Location = new Point(98, 24), Width = 305, BackColor = mehediSurf, ForeColor = ForeColor };
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
                mehediStartDlgChk, shihabProgDlgChk, mehediCompDlgChk, shihabDarkChk,
                shihabThemePresetLbl, shihabThemePresetCombo, shihabExtSubLbl, shihabExtSubCombo,
                shihabAutoExtGlobalChk, shihabDelArchChk, shihabMotwChk, shihabSleepChk,
                mehediDupLbl, shihabDupCombo, mehediAvGrp, mehediNativeGrp
            });

            // ==================== 5. CONNECTION TAB ====================
            AddShihabSectionBanner(mehediConnTab, "Connection type and Dynamic Segmentation (1..128)", shihabDark);

            Label shihabConnTypeLbl = new Label { Text = "Connection Type / Speed:", Location = new Point(18, 42), AutoSize = true };
            ComboBox mehediConnTypeCombo = new ComboBox
            {
                Location = new Point(185, 39),
                Width = 420,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = mehediSurf,
                ForeColor = ForeColor
            };
            mehediConnTypeCombo.Items.Add("High speed: Direct connection (Ethernet/Cable) / Wi-Fi / Mobile 4G/5G");
            mehediConnTypeCombo.Items.Add("Medium speed: DSL / Cable 2M / Mobile 3G");
            mehediConnTypeCombo.Items.Add("Low speed / Metered connection");
            int shihabConnProfIdx = mehediConnTypeCombo.FindStringExact(_shihabConfig.MehediConnectionProfile ?? "");
            mehediConnTypeCombo.SelectedIndex = shihabConnProfIdx >= 0 ? shihabConnProfIdx : 0;

            Label shihabMaxConnLbl = new Label { Text = "Default max. conn. number (Dynamic Segmentation 1..128):", Location = new Point(18, 76), AutoSize = true };
            NumericUpDown mehediMaxConnNum = new NumericUpDown { Location = new Point(495, 73), Width = 95, Minimum = 1, Maximum = 128, Value = Math.Max(1, Math.Min(128, _shihabConfig.ShihabDefaultMaxConnections)) };

            CheckBox shihabUnlock128Chk = new CheckBox { Text = "Unlock >32 connections per file (up to 128)", Location = new Point(18, 104), AutoSize = true, Checked = _shihabConfig.ShihabAllowMoreThan32Connections };
            CheckBox shihabShedSlowChk = new CheckBox { Text = "Shed slow connections (< 1/4 peak)", Location = new Point(310, 104), AutoSize = true, Checked = _shihabConfig.ShihabShedSlowConnections };
            CheckBox shihabOverlapChk = new CheckBox { Text = "Verify 64-byte resume overlap", Location = new Point(18, 130), AutoSize = true, Checked = _shihabConfig.ShihabVerifyResumeOverlap };
            CheckBox shihabSparseChk = new CheckBox { Text = "Use NTFS Sparse files", Location = new Point(310, 130), AutoSize = true, Checked = _shihabConfig.ShihabEnableSparseFiles };

            Label mehediRetrySecLbl = new Label { Text = "Pause between auto-retry attempts (seconds):", Location = new Point(18, 160), AutoSize = true };
            NumericUpDown shihabRetrySecNum = new NumericUpDown { Location = new Point(335, 157), Width = 80, Minimum = 1, Maximum = 3600, Value = Math.Max(1, Math.Min(3600, _shihabConfig.ShihabAutoRetryIntervalSec > 0 ? _shihabConfig.ShihabAutoRetryIntervalSec : 10)) };
            Label mehediMaxRetLbl = new Label { Text = "Max retries:", Location = new Point(430, 160), AutoSize = true };
            NumericUpDown shihabMaxRetNum = new NumericUpDown { Location = new Point(510, 157), Width = 80, Minimum = 0, Maximum = 999, Value = Math.Max(0, Math.Min(999, _shihabConfig.MehediMaxRetryAttempts > 0 ? _shihabConfig.MehediMaxRetryAttempts : 99)) };

            CheckBox shihabGlobalLimChk = new CheckBox { Text = "Enable Global Speed Limiter across all active downloads", Location = new Point(18, 194), AutoSize = true, Checked = _shihabConfig.MehediGlobalSpeedLimiterEnabled };
            Label mehediGlobSpdLbl = new Label { Text = "Maximum global download transfer rate (KBytes/sec):", Location = new Point(18, 222), AutoSize = true };
            NumericUpDown shihabGlobSpdNum = new NumericUpDown { Location = new Point(365, 219), Width = 115, Minimum = 16, Maximum = 2000000, Value = Math.Max(16, _shihabConfig.ShihabGlobalSpeedLimitKb) };

            Label mehediUaLbl = new Label { Text = "User-Agent for manually added downloads:", Location = new Point(18, 254), AutoSize = true };
            TextBox shihabUaBox = new TextBox { Location = new Point(18, 276), Width = 585, Text = _shihabConfig.ShihabDefaultUserAgent, BackColor = mehediSurf, ForeColor = ForeColor };

            GroupBox shihabQuotaGrp = new GroupBox
            {
                Text = "Rolling Download Quota & Per-Host Connection Limits",
                Location = new Point(18, 310),
                Size = new Size(585, 86),
                ForeColor = ForeColor
            };
            CheckBox shihabQuotaChk = new CheckBox { Text = "Enable Rolling Quota (MB):", Location = new Point(14, 24), AutoSize = true, Checked = _shihabConfig.ShihabQuotaEnabled };
            NumericUpDown shihabQuotaMbNum = new NumericUpDown { Location = new Point(205, 21), Width = 95, Minimum = 10, Maximum = 10000000, Value = Math.Max(10, _shihabConfig.ShihabQuotaLimitMegabytes) };
            Label shihabQuotaWinLbl = new Label { Text = "Window (hours):", Location = new Point(312, 24), AutoSize = true };
            NumericUpDown shihabQuotaHrsNum = new NumericUpDown { Location = new Point(415, 21), Width = 65, Minimum = 1, Maximum = 720, Value = Math.Max(1, Math.Min(720, _shihabConfig.ShihabQuotaWindowHours)) };
            Label shihabQuotaUsedLbl = new Label
            {
                Text = "Used in window: " + ShihabFormatHelpers.FormatMehediBytes(_shihabConfig.GetCurrentQuotaWindowBytes()),
                Location = new Point(14, 54),
                AutoSize = true,
                ForeColor = shihabMuted
            };
            Button shihabHostLimBtn = ShihabThemePalette.CreateMehediStyledButton("Per-Host Limits...", mehediSurf, ForeColor, 145);
            shihabHostLimBtn.Height = 25;
            shihabHostLimBtn.Location = new Point(425, 50);
            shihabHostLimBtn.Click += delegate { ShowShihabHostLimitsModal(shihabDark); };
            shihabQuotaGrp.Controls.AddRange(new Control[] { shihabQuotaChk, shihabQuotaMbNum, shihabQuotaWinLbl, shihabQuotaHrsNum, shihabQuotaUsedLbl, shihabHostLimBtn });

            mehediConnTab.Controls.AddRange(new Control[]
            {
                shihabConnTypeLbl, mehediConnTypeCombo, shihabMaxConnLbl, mehediMaxConnNum,
                shihabUnlock128Chk, shihabShedSlowChk, shihabOverlapChk, shihabSparseChk,
                mehediRetrySecLbl, shihabRetrySecNum, mehediMaxRetLbl, shihabMaxRetNum,
                shihabGlobalLimChk, mehediGlobSpdLbl, shihabGlobSpdNum, mehediUaLbl, shihabUaBox,
                shihabQuotaGrp
            });

            // ==================== 6. PROXY / SOCKS TAB ====================
            AddShihabSectionBanner(shihabProxyTab, "Proxy and SOCKS configuration", shihabDark);

            GroupBox mehediHttpPrxGrp = new GroupBox
            {
                Text = "HTTP / HTTPS / FTP Proxy",
                Location = new Point(16, 42),
                Size = new Size(592, 138),
                ForeColor = ForeColor
            };
            CheckBox shihabUseProxyChk = new CheckBox { Text = "Use HTTP / HTTPS Proxy server", Location = new Point(14, 24), AutoSize = true, Checked = _shihabConfig.MehediUseHttpProxy };
            Label mehediPrxHostLbl = new Label { Text = "Proxy server address:", Location = new Point(14, 60), AutoSize = true };
            TextBox shihabPrxHostBox = new TextBox { Location = new Point(155, 57), Width = 265, Text = _shihabConfig.ShihabProxyAddress ?? "", BackColor = mehediSurf, ForeColor = ForeColor };
            Label mehediPrxPortLbl = new Label { Text = "Port:", Location = new Point(435, 60), AutoSize = true };
            NumericUpDown shihabPrxPortNum = new NumericUpDown { Location = new Point(478, 57), Width = 95, Minimum = 1, Maximum = 65535, Value = Math.Max(1, Math.Min(65535, _shihabConfig.MehediProxyPort > 0 ? _shihabConfig.MehediProxyPort : 8080)) };
            Label mehediPrxUserLbl = new Label { Text = "User name:", Location = new Point(14, 98), AutoSize = true };
            TextBox shihabPrxUserBox = new TextBox { Location = new Point(155, 95), Width = 175, Text = _shihabConfig.ShihabProxyUser ?? "", BackColor = mehediSurf, ForeColor = ForeColor };
            Label mehediPrxPassLbl = new Label { Text = "Password:", Location = new Point(345, 98), AutoSize = true };
            TextBox shihabPrxPassBox = new TextBox { Location = new Point(418, 95), Width = 155, UseSystemPasswordChar = true, Text = _shihabConfig.MehediProxyPassword ?? "", BackColor = mehediSurf, ForeColor = ForeColor };
            mehediHttpPrxGrp.Controls.AddRange(new Control[] { shihabUseProxyChk, mehediPrxHostLbl, shihabPrxHostBox, mehediPrxPortLbl, shihabPrxPortNum, mehediPrxUserLbl, shihabPrxUserBox, mehediPrxPassLbl, shihabPrxPassBox });

            GroupBox shihabSocksGrp = new GroupBox
            {
                Text = "SOCKS Proxy",
                Location = new Point(16, 194),
                Size = new Size(592, 105),
                ForeColor = ForeColor
            };
            CheckBox mehediUseSocksChk = new CheckBox { Text = "Use SOCKS4 / SOCKS5 proxy server", Location = new Point(14, 24), AutoSize = true, Checked = _shihabConfig.ShihabUseSocksProxy };
            Label shihabSocksHostLbl = new Label { Text = "SOCKS server address:", Location = new Point(14, 62), AutoSize = true };
            TextBox mehediSocksHostBox = new TextBox { Location = new Point(155, 59), Width = 265, Text = _shihabConfig.MehediSocksHost ?? "", BackColor = mehediSurf, ForeColor = ForeColor };
            Label shihabSocksPortLbl = new Label { Text = "Port:", Location = new Point(435, 62), AutoSize = true };
            NumericUpDown mehediSocksPortNum = new NumericUpDown { Location = new Point(478, 59), Width = 95, Minimum = 1, Maximum = 65535, Value = Math.Max(1, Math.Min(65535, _shihabConfig.ShihabSocksPort > 0 ? _shihabConfig.ShihabSocksPort : 1080)) };
            shihabSocksGrp.Controls.AddRange(new Control[] { mehediUseSocksChk, shihabSocksHostLbl, mehediSocksHostBox, shihabSocksPortLbl, mehediSocksPortNum });

            GroupBox shihabPacDnsGrp = new GroupBox
            {
                Text = "PAC Auto-Config Script & DNS Mode (System / Plain UDP / Encrypted DoH)",
                Location = new Point(16, 302),
                Size = new Size(592, 112),
                ForeColor = ForeColor
            };
            Label shihabPacUrlLbl = new Label { Text = "PAC Script URL (.pac):", Location = new Point(14, 22), AutoSize = true };
            TextBox shihabPacUrlBox = new TextBox { Location = new Point(155, 19), Width = 418, Text = _shihabConfig.ShihabPacScriptUrl ?? "", BackColor = mehediSurf, ForeColor = ForeColor };

            Label shihabDnsModeLbl = new Label { Text = "DNS Mode:", Location = new Point(14, 52), AutoSize = true };
            ComboBox shihabDnsModeCombo = new ComboBox
            {
                Location = new Point(92, 49),
                Width = 122,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = mehediSurf,
                ForeColor = ForeColor
            };
            shihabDnsModeCombo.Items.AddRange(new object[] { "System", "PlainDNS", "EncryptedDoH" });
            int dnsModeIdx = shihabDnsModeCombo.FindStringExact(_shihabConfig.ShihabDnsMode ?? "System");
            shihabDnsModeCombo.SelectedIndex = dnsModeIdx >= 0 ? dnsModeIdx : 0;

            ComboBox shihabDohProvCombo = new ComboBox
            {
                Location = new Point(222, 49),
                Width = 115,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = mehediSurf,
                ForeColor = ForeColor
            };
            shihabDohProvCombo.Items.AddRange(new object[] { "Cloudflare", "Google", "Quad9", "AdGuard", "NextDNS", "Custom" });
            int dohIdx = shihabDohProvCombo.FindStringExact(_shihabConfig.ShihabDnsProvider ?? "Cloudflare");
            shihabDohProvCombo.SelectedIndex = dohIdx >= 0 ? dohIdx : 0;
            TextBox shihabCustomDohBox = new TextBox { Location = new Point(345, 49), Width = 228, Text = _shihabConfig.ShihabCustomDohUrl ?? "", BackColor = mehediSurf, ForeColor = ForeColor };

            Label shihabPlainDnsLbl = new Label { Text = "Plain DNS IP:", Location = new Point(14, 82), AutoSize = true };
            TextBox shihabPlainDnsBox = new TextBox { Location = new Point(98, 79), Width = 145, Text = _shihabConfig.ShihabPlainDnsServer ?? "1.1.1.1", BackColor = mehediSurf, ForeColor = ForeColor };
            Label shihabNextDnsLbl = new Label { Text = "NextDNS ID:", Location = new Point(256, 82), AutoSize = true };
            TextBox shihabNextDnsBox = new TextBox { Location = new Point(335, 79), Width = 120, Text = _shihabConfig.ShihabNextDnsConfigId ?? "", BackColor = mehediSurf, ForeColor = ForeColor };

            shihabPacDnsGrp.Controls.AddRange(new Control[]
            {
                shihabPacUrlLbl, shihabPacUrlBox, shihabDnsModeLbl, shihabDnsModeCombo,
                shihabDohProvCombo, shihabCustomDohBox, shihabPlainDnsLbl, shihabPlainDnsBox,
                shihabNextDnsLbl, shihabNextDnsBox
            });

            shihabProxyTab.Controls.AddRange(new Control[] { mehediHttpPrxGrp, shihabSocksGrp, shihabPacDnsGrp });

            // ==================== 7. SITES LOGINS TAB ====================
            AddShihabSectionBanner(mehediLoginsTab, "Passwords for authenticated HTTP/HTTPS/FTP servers", shihabDark);

            ShihabModernListView mehediLoginsList = new ShihabModernListView
            {
                Location = new Point(18, 44),
                Size = new Size(588, 285),
                BorderStyle = BorderStyle.FixedSingle,
                MehediDarkMode = shihabDark,
                ShihabShowFileTypeBadges = false,
                BackColor = mehediSurf,
                ForeColor = ForeColor
            };
            mehediLoginsList.Columns.Add("Server / Path", 280);
            mehediLoginsList.Columns.Add("User", 180);
            mehediLoginsList.Columns.Add("Password", 105);

            Action refreshShihabLogins = delegate
            {
                mehediLoginsList.Items.Clear();
                for (int shihabI = 0; shihabI < _shihabConfig.ShihabSiteLogins.Count; shihabI++)
                {
                    ShihabSiteLoginCredential mehediC = _shihabConfig.ShihabSiteLogins[shihabI];
                    mehediLoginsList.Items.Add(new ListViewItem(new string[] { mehediC.ShihabServerPattern, mehediC.MehediUserName, "******" }));
                }
            };
            refreshShihabLogins();

            TextBox shihabNewSrv = new TextBox { Location = new Point(18, 344), Width = 212, Text = "example.com/members", BackColor = mehediSurf, ForeColor = ForeColor };
            TextBox mehediNewUsr = new TextBox { Location = new Point(238, 344), Width = 125, Text = "shihab", BackColor = mehediSurf, ForeColor = ForeColor };
            TextBox shihabNewPwd = new TextBox { Location = new Point(370, 344), Width = 110, UseSystemPasswordChar = true, BackColor = mehediSurf, ForeColor = ForeColor };
            Button mehediAddLoginBtn = ShihabThemePalette.CreateMehediStyledButton("New", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 56);
            mehediAddLoginBtn.Location = new Point(488, 340);
            mehediAddLoginBtn.Click += delegate
            {
                if (!string.IsNullOrWhiteSpace(shihabNewSrv.Text) && !string.IsNullOrWhiteSpace(mehediNewUsr.Text))
                {
                    _shihabConfig.ShihabSiteLogins.Add(new ShihabSiteLoginCredential
                    {
                        ShihabServerPattern = shihabNewSrv.Text.Trim(),
                        MehediUserName = mehediNewUsr.Text.Trim(),
                        ShihabPassword = shihabNewPwd.Text
                    });
                    refreshShihabLogins();
                }
            };
            Button shihabDelLoginBtn = ShihabThemePalette.CreateMehediStyledButton("Del", ShihabThemePalette.GetMehediAccentRed(), Color.White, 56);
            shihabDelLoginBtn.Location = new Point(550, 340);
            shihabDelLoginBtn.Click += delegate
            {
                if (mehediLoginsList.SelectedIndices.Count > 0)
                {
                    _shihabConfig.ShihabSiteLogins.RemoveAt(mehediLoginsList.SelectedIndices[0]);
                    refreshShihabLogins();
                }
            };
            mehediLoginsTab.Controls.AddRange(new Control[] { mehediLoginsList, shihabNewSrv, mehediNewUsr, shihabNewPwd, mehediAddLoginBtn, shihabDelLoginBtn });

            // ==================== 8. DIAL UP / VPN TAB ====================
            AddShihabSectionBanner(shihabDialTab, "Dial-Up Networking and VPN connection monitor", shihabDark);

            Label mehediDialConnLbl = new Label { Text = "Connection / VPN adapter profile:", Location = new Point(18, 52), AutoSize = true };
            TextBox shihabDialConnBox = new TextBox
            {
                Location = new Point(235, 49),
                Width = 365,
                Text = string.IsNullOrWhiteSpace(_shihabConfig.MehediDialUpConnectionName) ? "Default System Connection" : _shihabConfig.MehediDialUpConnectionName,
                BackColor = mehediSurf,
                ForeColor = ForeColor
            };
            CheckBox mehediRedialChk = new CheckBox
            {
                Text = "Automatically resume/reconnect queued transfers when network is restored",
                Location = new Point(18, 96),
                AutoSize = true,
                Checked = _shihabConfig.ShihabRedialIfDisconnected
            };
            CheckBox shihabHangUpChk = new CheckBox
            {
                Text = "Disconnect Dial-Up / VPN connection when scheduled queue finishes",
                Location = new Point(18, 128),
                AutoSize = true,
                Checked = _shihabConfig.MehediHangUpWhenDone
            };
            Label mehediDialNote = new Label
            {
                Text = "Note: Per-queue start/stop times and exit-on-complete actions can also be configured in the Scheduler window.",
                Location = new Point(18, 164),
                Size = new Size(580, 38),
                ForeColor = shihabMuted
            };

            GroupBox shihabVpnBindGrp = new GroupBox
            {
                Text = "VPN Kill-Switch (Bind Transfers to Specific Network Adapter / Interface)",
                Location = new Point(18, 210),
                Size = new Size(584, 118),
                ForeColor = ForeColor
            };
            Label shihabBindLbl = new Label { Text = "Bind outbound sockets to adapter (fail-closed if adapter drops):", Location = new Point(14, 26), AutoSize = true };
            ComboBox shihabBindAdapterCombo = new ComboBox
            {
                Location = new Point(14, 52),
                Width = 550,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = mehediSurf,
                ForeColor = ForeColor
            };
            shihabBindAdapterCombo.Items.Add("(Any / System Default Routing)");
            List<ShihabNetworkAdapterChoice> liveAdapters = ShihabDnsAndNetworkManager.GetAvailableNetworkAdapters();
            int selAdapterIdx = 0;
            for (int ai = 0; ai < liveAdapters.Count; ai++)
            {
                if (string.IsNullOrEmpty(liveAdapters[ai].AdapterId)) continue;
                string label = liveAdapters[ai].ToString();
                shihabBindAdapterCombo.Items.Add(label);
                if (!string.IsNullOrWhiteSpace(_shihabConfig.ShihabBoundNetworkAdapter) &&
                    label.StartsWith(_shihabConfig.ShihabBoundNetworkAdapter, StringComparison.OrdinalIgnoreCase))
                {
                    selAdapterIdx = shihabBindAdapterCombo.Items.Count - 1;
                }
            }
            shihabBindAdapterCombo.SelectedIndex = selAdapterIdx;
            CheckBox shihabNetwatchChk = new CheckBox
            {
                Text = "Auto-resume stalled downloads when network connectivity returns (netwatch)",
                Location = new Point(14, 86),
                AutoSize = true,
                Checked = _shihabConfig.ShihabAutoResumeOnNetworkRestore
            };
            shihabVpnBindGrp.Controls.AddRange(new Control[] { shihabBindLbl, shihabBindAdapterCombo, shihabNetwatchChk });

            shihabDialTab.Controls.AddRange(new Control[] { mehediDialConnLbl, shihabDialConnBox, mehediRedialChk, shihabHangUpChk, mehediDialNote, shihabVpnBindGrp });

            // ==================== 9. SOUNDS TAB ====================
            AddShihabSectionBanner(mehediSoundsTab, "Audio notifications for download events", shihabDark);

            GroupBox shihabSndCompGrp = new GroupBox
            {
                Text = "Download complete",
                Location = new Point(16, 46),
                Size = new Size(592, 96),
                ForeColor = ForeColor
            };
            CheckBox mehediSndCompChk = new CheckBox { Text = "Play sound when a download finishes (leave blank for default system chime):", Location = new Point(14, 24), AutoSize = true, Checked = _shihabConfig.MehediPlaySoundComplete };
            TextBox shihabSndCompBox = new TextBox { Location = new Point(14, 54), Width = 385, Text = _shihabConfig.ShihabSoundCompleteWav ?? "", BackColor = mehediSurf, ForeColor = ForeColor };
            Button mehediSndCompBrowse = ShihabThemePalette.CreateMehediStyledButton("Browse", mehediSurf, ForeColor, 88);
            mehediSndCompBrowse.Height = 25;
            mehediSndCompBrowse.Location = new Point(408, 52);
            mehediSndCompBrowse.Click += delegate
            {
                using (OpenFileDialog shihabOfd = new OpenFileDialog { Filter = "Wave Audio (*.wav)|*.wav" })
                {
                    if (shihabOfd.ShowDialog(this) == DialogResult.OK) shihabSndCompBox.Text = shihabOfd.FileName;
                }
            };
            Button shihabSndCompPlay = ShihabThemePalette.CreateMehediStyledButton("Play ▶", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 74);
            shihabSndCompPlay.Height = 25;
            shihabSndCompPlay.Location = new Point(504, 52);
            shihabSndCompPlay.Click += delegate { PreviewShihabSoundFile(shihabSndCompBox.Text, false); };
            shihabSndCompGrp.Controls.AddRange(new Control[] { mehediSndCompChk, shihabSndCompBox, mehediSndCompBrowse, shihabSndCompPlay });

            GroupBox mehediSndErrGrp = new GroupBox
            {
                Text = "Download failed / error",
                Location = new Point(16, 158),
                Size = new Size(592, 96),
                ForeColor = ForeColor
            };
            CheckBox shihabSndErrChk = new CheckBox { Text = "Play sound when a download encounters an error:", Location = new Point(14, 24), AutoSize = true, Checked = _shihabConfig.MehediPlaySoundError };
            TextBox mehediSndErrBox = new TextBox { Location = new Point(14, 54), Width = 385, Text = _shihabConfig.ShihabSoundErrorWav ?? "", BackColor = mehediSurf, ForeColor = ForeColor };
            Button shihabSndErrBrowse = ShihabThemePalette.CreateMehediStyledButton("Browse", mehediSurf, ForeColor, 88);
            shihabSndErrBrowse.Height = 25;
            shihabSndErrBrowse.Location = new Point(408, 52);
            shihabSndErrBrowse.Click += delegate
            {
                using (OpenFileDialog mehediOfd = new OpenFileDialog { Filter = "Wave Audio (*.wav)|*.wav" })
                {
                    if (mehediOfd.ShowDialog(this) == DialogResult.OK) mehediSndErrBox.Text = mehediOfd.FileName;
                }
            };
            Button mehediSndErrPlay = ShihabThemePalette.CreateMehediStyledButton("Play ▶", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 74);
            mehediSndErrPlay.Height = 25;
            mehediSndErrPlay.Location = new Point(504, 52);
            mehediSndErrPlay.Click += delegate { PreviewShihabSoundFile(mehediSndErrBox.Text, true); };
            mehediSndErrGrp.Controls.AddRange(new Control[] { shihabSndErrChk, mehediSndErrBox, shihabSndErrBrowse, mehediSndErrPlay });

            mehediSoundsTab.Controls.AddRange(new Control[] { shihabSndCompGrp, mehediSndErrGrp });

            // ==================== 10. DEBRID & VAULT TAB ====================
            AddShihabSectionBanner(shihabDebridTab, "Multi-Host Debrid Accounts & DPAPI Encrypted Secret Vault", shihabDark);

            ShihabModernListView shihabDebridView = new ShihabModernListView
            {
                Location = new Point(16, 44),
                Size = new Size(592, 165),
                BorderStyle = BorderStyle.FixedSingle,
                MehediDarkMode = shihabDark,
                ShihabShowFileTypeBadges = false,
                BackColor = mehediSurf,
                ForeColor = ForeColor
            };
            shihabDebridView.Columns.Add("Provider", 130);
            shihabDebridView.Columns.Add("Enabled", 75);
            shihabDebridView.Columns.Add("API Token", 150);
            shihabDebridView.Columns.Add("Account Status", 220);

            Action refreshDebridList = delegate
            {
                shihabDebridView.Items.Clear();
                if (_shihabConfig.ShihabDebridAccounts == null) return;
                for (int di = 0; di < _shihabConfig.ShihabDebridAccounts.Count; di++)
                {
                    ShihabDebridAccount acc = _shihabConfig.ShihabDebridAccounts[di];
                    string rawTok = ShihabSecretsVault.UnsealSecret(acc.ShihabApiToken);
                    string masked = string.IsNullOrEmpty(rawTok) ? "(not set)" : ("dpapi:****" + (rawTok.Length > 4 ? rawTok.Substring(rawTok.Length - 4) : ""));
                    ListViewItem row = new ListViewItem(new string[]
                    {
                        acc.ShihabProviderName,
                        acc.ShihabEnabled ? "Yes" : "No",
                        masked,
                        acc.ShihabAccountStatus ?? "Not configured"
                    });
                    row.Tag = acc;
                    shihabDebridView.Items.Add(row);
                }
            };
            refreshDebridList();

            Label shihabDebProvLbl = new Label { Text = "Provider:", Location = new Point(16, 222), AutoSize = true };
            ComboBox shihabDebProvCombo = new ComboBox { Location = new Point(80, 219), Width = 135, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = mehediSurf, ForeColor = ForeColor };
            shihabDebProvCombo.Items.AddRange(new object[] { "Real-Debrid", "AllDebrid", "Premiumize" });
            shihabDebProvCombo.SelectedIndex = 0;

            Label shihabDebTokLbl = new Label { Text = "API Token:", Location = new Point(225, 222), AutoSize = true };
            TextBox shihabDebTokBox = new TextBox { Location = new Point(295, 219), Width = 195, UseSystemPasswordChar = true, BackColor = mehediSurf, ForeColor = ForeColor };

            Button shihabDebSaveBtn = ShihabThemePalette.CreateMehediStyledButton("Save & Test", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 108);
            shihabDebSaveBtn.Height = 26;
            shihabDebSaveBtn.Location = new Point(500, 217);
            shihabDebSaveBtn.Click += delegate
            {
                string prov = shihabDebProvCombo.SelectedItem.ToString();
                string tok = shihabDebTokBox.Text.Trim();
                if (_shihabConfig.ShihabDebridAccounts == null) _shihabConfig.ShihabDebridAccounts = new List<ShihabDebridAccount>();
                ShihabDebridAccount target = null;
                for (int di = 0; di < _shihabConfig.ShihabDebridAccounts.Count; di++)
                {
                    if (string.Equals(_shihabConfig.ShihabDebridAccounts[di].ShihabProviderName, prov, StringComparison.OrdinalIgnoreCase))
                    {
                        target = _shihabConfig.ShihabDebridAccounts[di];
                        break;
                    }
                }
                if (target == null)
                {
                    target = new ShihabDebridAccount { ShihabProviderName = prov };
                    _shihabConfig.ShihabDebridAccounts.Add(target);
                }
                target.ShihabApiToken = ShihabSecretsVault.SealSecret(tok);
                target.ShihabEnabled = !string.IsNullOrEmpty(tok);
                target.ShihabAccountStatus = string.IsNullOrEmpty(tok) ? "Disabled" : ShihabDebridManager.CheckAccountStatus(prov, tok);
                refreshDebridList();
            };

            GroupBox shihabVaultGrp = new GroupBox
            {
                Text = "Windows DPAPI Secret Vault & Security Controls",
                Location = new Point(16, 260),
                Size = new Size(592, 130),
                ForeColor = ForeColor
            };
            Label shihabVaultDesc = new Label
            {
                Text = "All site login passwords, proxy credentials, and Debrid API tokens are encrypted at rest using Windows Data Protection API (DPAPI CurrentUser scope) with 'dpapi:' envelopes.\r\nClick 'Forget All Sign-Ins' to immediately purge all saved passwords, tokens, and remote sessions.",
                Location = new Point(14, 24),
                Size = new Size(560, 54),
                ForeColor = shihabMuted
            };
            Button shihabForgetAllBtn = ShihabThemePalette.CreateMehediStyledButton("Forget All Sign-Ins (Purge Vault)", ShihabThemePalette.GetMehediAccentRed(), Color.White, 245);
            shihabForgetAllBtn.Location = new Point(14, 84);
            shihabForgetAllBtn.Click += delegate
            {
                if (MessageBox.Show(this, "Purge all saved site logins, proxy passwords, Debrid API tokens, and paired remote devices?", "Forget All Sign-Ins", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    int count = ShihabSecretsVault.ForgetAllSignIns(_mehediStore);
                    _shihabConfig.ShihabSiteLogins.Clear();
                    refreshShihabLogins();
                    refreshDebridList();
                    MessageBox.Show(this, "Purged " + count + " credentials and tokens from the DPAPI vault.", "Vault Purged", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            shihabVaultGrp.Controls.AddRange(new Control[] { shihabVaultDesc, shihabForgetAllBtn });

            shihabDebridTab.Controls.AddRange(new Control[]
            {
                shihabDebridView, shihabDebProvLbl, shihabDebProvCombo, shihabDebTokLbl, shihabDebTokBox, shihabDebSaveBtn, shihabVaultGrp
            });

            // ==================== 11. UPDATES & P2P TAB ====================
            AddShihabSectionBanner(shihabUpdatesP2pTab, "GitHub Releases Auto-Updater, BitTorrent P2P & Watchdog", shihabDark);

            GroupBox shihabUpdGrp = new GroupBox
            {
                Text = "GitHub Releases Auto-Updater (" + (_shihabConfig.ShihabUpdateRepoSlug ?? "sshihabb007/MHS-DOWNLOAD-MANAGER") + ")",
                Location = new Point(16, 42),
                Size = new Size(592, 168),
                ForeColor = ForeColor
            };
            CheckBox shihabAutoUpdChk = new CheckBox
            {
                Text = "Automatically check GitHub Releases for software updates (startup + every 6 hours)",
                Location = new Point(14, 24),
                AutoSize = true,
                Checked = _shihabConfig.ShihabAutoCheckUpdates
            };
            CheckBox shihabPreRelChk = new CheckBox
            {
                Text = "Include pre-release / beta builds when checking for updates",
                Location = new Point(14, 50),
                AutoSize = true,
                Checked = _shihabConfig.ShihabUpdateIncludePrereleases
            };
            Label shihabCurVerLbl = new Label
            {
                Text = "Installed Version: v" + ShihabAutoUpdater.GetCurrentAppVersion() +
                       "  |  Last Check: " + (string.IsNullOrWhiteSpace(_shihabConfig.ShihabLastUpdateCheckUtc) ? "Never" : _shihabConfig.ShihabLastUpdateCheckUtc) +
                       (string.IsNullOrWhiteSpace(_shihabConfig.ShihabSkippedUpdateVersion) ? "" : ("  |  Skipped: v" + _shihabConfig.ShihabSkippedUpdateVersion)),
                Location = new Point(14, 78),
                Size = new Size(564, 34),
                ForeColor = shihabMuted
            };
            Button shihabCheckUpdNowBtn = ShihabThemePalette.CreateMehediStyledButton("Check for Updates Now...", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 205);
            shihabCheckUpdNowBtn.Location = new Point(14, 120);
            shihabCheckUpdNowBtn.Click += delegate
            {
                shihabCheckUpdNowBtn.Enabled = false;
                shihabCheckUpdNowBtn.Text = "Checking GitHub...";
                bool prevPre = _shihabConfig.ShihabUpdateIncludePrereleases;
                _shihabConfig.ShihabUpdateIncludePrereleases = shihabPreRelChk.Checked;
                Task.Run(delegate
                {
                    ShihabUpdateCheckResult updRes = ShihabAutoUpdater.CheckForUpdates(_shihabConfig, true);
                    _shihabConfig.ShihabLastUpdateCheckUtc = DateTime.UtcNow.ToString("u");
                    try
                    {
                        BeginInvoke((Action)delegate
                        {
                            if (IsDisposed) return;
                            shihabCheckUpdNowBtn.Enabled = true;
                            shihabCheckUpdNowBtn.Text = "Check for Updates Now...";
                            _shihabConfig.ShihabUpdateIncludePrereleases = prevPre;
                            shihabCurVerLbl.Text = "Installed Version: v" + ShihabAutoUpdater.GetCurrentAppVersion() +
                                "  |  Last Check: " + _shihabConfig.ShihabLastUpdateCheckUtc;
                            if (!string.IsNullOrEmpty(updRes.ErrorMessage))
                            {
                                MessageBox.Show(this, "Could not check GitHub Releases:\r\n" + updRes.ErrorMessage, "Update Check", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                            else if (updRes.UpdateAvailable)
                            {
                                using (ShihabAutoUpdateDialog dlg = new ShihabAutoUpdateDialog(updRes, _shihabConfig, _mehediStore, _shihabCleanExitCallback))
                                {
                                    dlg.ShowDialog(this);
                                }
                            }
                            else
                            {
                                MessageBox.Show(this, "You are running the latest version of MHS Download Manager (v" + ShihabAutoUpdater.GetCurrentAppVersion() + ").", "Up to Date", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        });
                    }
                    catch { }
                });
            };
            Button shihabResetSkipBtn = ShihabThemePalette.CreateMehediStyledButton("Reset Skipped Version", mehediSurf, ForeColor, 165);
            shihabResetSkipBtn.Location = new Point(230, 120);
            shihabResetSkipBtn.Click += delegate
            {
                _shihabConfig.ShihabSkippedUpdateVersion = "";
                _mehediStore.SaveMehediConfig(_shihabConfig);
                shihabCurVerLbl.Text = "Installed Version: v" + ShihabAutoUpdater.GetCurrentAppVersion() + "  |  Skipped version cleared.";
            };
            shihabUpdGrp.Controls.AddRange(new Control[] { shihabAutoUpdChk, shihabPreRelChk, shihabCurVerLbl, shihabCheckUpdNowBtn, shihabResetSkipBtn });

            GroupBox shihabP2pGrp = new GroupBox
            {
                Text = "BitTorrent P2P Defaults, IP Blocklist & Crash Watchdog",
                Location = new Point(16, 220),
                Size = new Size(592, 182),
                ForeColor = ForeColor
            };
            CheckBox shihabTorSeqDefChk = new CheckBox
            {
                Text = "Default to sequential piece download (stream media while downloading)",
                Location = new Point(14, 24),
                AutoSize = true,
                Checked = _shihabConfig.ShihabTorrentSequentialDefault
            };
            CheckBox shihabTorSeedDefChk = new CheckBox
            {
                Text = "Keep seeding completed torrents by default",
                Location = new Point(14, 50),
                AutoSize = true,
                Checked = _shihabConfig.ShihabTorrentKeepSeedingDefault
            };
            Label shihabBlkUrlLbl = new Label { Text = "P2P IP Blocklist URL / Path (P2P / CIDR):", Location = new Point(14, 82), AutoSize = true };
            TextBox shihabBlkUrlBox = new TextBox
            {
                Location = new Point(14, 104),
                Width = 560,
                Text = _shihabConfig.ShihabTorrentBlocklistUrl ?? "",
                BackColor = mehediSurf,
                ForeColor = ForeColor
            };
            CheckBox shihabCrashMonChk = new CheckBox
            {
                Text = "Enable out-of-process Crash Monitor & automatic interrupted-transfer recovery prompt",
                Location = new Point(14, 140),
                AutoSize = true,
                Checked = _shihabConfig.ShihabCrashMonitorEnabled
            };
            shihabP2pGrp.Controls.AddRange(new Control[] { shihabTorSeqDefChk, shihabTorSeedDefChk, shihabBlkUrlLbl, shihabBlkUrlBox, shihabCrashMonChk });

            shihabUpdatesP2pTab.Controls.AddRange(new Control[] { shihabUpdGrp, shihabP2pGrp });

            mehediTabs.TabPages.AddRange(new TabPage[]
            {
                shihabGenTab, mehediTypesTab, shihabSaveTab, mehediDownloadsTab,
                mehediConnTab, shihabProxyTab, mehediLoginsTab, shihabDialTab, mehediSoundsTab, shihabDebridTab, shihabUpdatesP2pTab
            });

            // ==================== BOTTOM BUTTONS ====================
            Button shihabSaveOk = ShihabThemePalette.CreateMehediStyledButton("OK", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 102);
            shihabSaveOk.Location = new Point(332, 478);
            shihabSaveOk.Click += delegate
            {
                _shihabConfig.MehediBrowserIntegrationEnabled = shihabBrowserChk.Checked;
                bool mehediStartupChanged = (_shihabConfig.ShihabLaunchOnStartup != mehediStartupChk.Checked);
                _shihabConfig.ShihabLaunchOnStartup = mehediStartupChk.Checked;
                if (mehediStartupChanged || mehediStartupChk.Checked)
                {
                    MehediAppConfiguration.SyncShihabWindowsStartupRegistry(mehediStartupChk.Checked);
                }
                _shihabConfig.ShihabMinimizeToTray = shihabMinTrayChk.Checked;
                _shihabConfig.ShihabCloseToTray = shihabCloseTrayChk.Checked;
                if (shihabLangCombo.SelectedItem != null)
                {
                    _shihabConfig.ShihabLanguageCode = ShihabThemeManager.ExtractLanguageIsoCode(shihabLangCombo.SelectedItem.ToString());
                }
                if (shihabScaleCombo.SelectedItem != null)
                {
                    int parsedScale;
                    if (int.TryParse(shihabScaleCombo.SelectedItem.ToString().TrimEnd('%'), out parsedScale))
                    {
                        _shihabConfig.ShihabUiScalePercent = Math.Max(100, Math.Min(200, parsedScale));
                    }
                }

                _shihabConfig.ShihabMonitorClipboard = shihabClipChk.Checked;
                List<string> shihabCheckedBrowsers = new List<string>();
                for (int mehediBi = 0; mehediBi < shihabBrowsersClb.CheckedItems.Count; mehediBi++)
                {
                    shihabCheckedBrowsers.Add(shihabBrowsersClb.CheckedItems[mehediBi].ToString());
                }
                _shihabConfig.MehediEnabledBrowsersCsv = string.Join(",", shihabCheckedBrowsers.ToArray());

                _shihabConfig.MehediPreventKeyName = shihabPendingPreventKey;
                _shihabConfig.ShihabForceKeyName = mehediPendingForceKey;
                _shihabConfig.MehediContextMenuItemsCsv = shihabPendingCtxMenuCsv;
                _shihabConfig.ShihabShowVideoFloatingPanel = mehediPendingShowPanel;
                _shihabConfig.ShihabPanelMiniMode = shihabPendingMiniMode;
                _shihabConfig.MehediPanelSkipAutoStart = mehediPendingSkipAuto;
                _shihabConfig.MehediMinVideoSizeKb = shihabPendingMinVidKb;

                _shihabConfig.MehediAutoCaptureExtensions = mehediTypesBox.Text.Trim();
                _shihabConfig.ShihabExcludedSitesSpaceSeparated = mehediExclBox.Text.Trim();
                _shihabConfig.MehediExcludedAddressesSpaceSeparated = mehediPendingExcludedAddr;
                _shihabConfig.ShihabCaptureOpenedFiles = shihabCapOpenedChk.Checked;
                _shihabConfig.ShihabCaptureMinSizeKb = (int)shihabMinSizeCapNum.Value;

                if (!string.IsNullOrWhiteSpace(shihabTmpBox.Text))
                {
                    _shihabConfig.MehediTemporaryDirectory = shihabTmpBox.Text.Trim();
                }
                _shihabConfig.ShihabSetServerCreationDate = shihabServerCreationChk.Checked;
                _shihabConfig.ShihabSetServerFileTimestamp = mehediServerTimeChk.Checked;

                _shihabConfig.ShihabShowStartDownloadDialog = mehediStartDlgChk.Checked;
                _shihabConfig.MehediShowDownloadProgressDialog = shihabProgDlgChk.Checked;
                _shihabConfig.ShihabShowDownloadCompleteDialog = mehediCompDlgChk.Checked;
                _shihabConfig.ShihabDarkMode = shihabDarkChk.Checked;
                if (shihabThemePresetCombo.SelectedItem != null)
                {
                    _shihabConfig.ShihabThemeName = shihabThemePresetCombo.SelectedItem.ToString();
                    ShihabCustomThemePreset selPreset = ShihabThemeManager.ResolveThemePalette(
                        _shihabConfig.ShihabThemeName,
                        _mehediStore != null ? _mehediStore.MehediBaseDirectory : "");
                    _shihabConfig.ShihabDarkMode = ShihabThemeManager.IsDarkThemePreset(selPreset);
                }
                if (shihabExtSubCombo.SelectedItem != null)
                {
                    _shihabConfig.ShihabExtractSubfolderMode = shihabExtSubCombo.SelectedItem.ToString();
                }
                _shihabConfig.ShihabAutoExtractArchives = shihabAutoExtGlobalChk.Checked;
                _shihabConfig.ShihabDeleteArchiveAfterExtract = shihabDelArchChk.Checked;
                _shihabConfig.ShihabWriteZoneIdentifier = shihabMotwChk.Checked;
                _shihabConfig.ShihabPreventSystemSleep = shihabSleepChk.Checked;
                _shihabConfig.MehediDuplicateFileMode = shihabDupCombo.SelectedIndex == 1 ? 1 : 0;
                _shihabConfig.ShihabEnableVirusCheck = shihabAvEnableChk.Checked;
                _shihabConfig.MehediVirusScannerExe = mehediAvExeBox.Text.Trim();
                _shihabConfig.ShihabVirusScannerArgs = shihabAvArgsBox.Text.Trim();

                _shihabConfig.MehediConnectionProfile = mehediConnTypeCombo.SelectedItem != null
                    ? mehediConnTypeCombo.SelectedItem.ToString()
                    : "High speed: Direct connection (Ethernet/Cable) / Wi-Fi / Mobile 4G/5G";
                _shihabConfig.ShihabAllowMoreThan32Connections = shihabUnlock128Chk.Checked;
                _shihabConfig.ShihabShedSlowConnections = shihabShedSlowChk.Checked;
                _shihabConfig.ShihabVerifyResumeOverlap = shihabOverlapChk.Checked;
                _shihabConfig.ShihabEnableSparseFiles = shihabSparseChk.Checked;
                _shihabConfig.ShihabDefaultMaxConnections = (int)mehediMaxConnNum.Value;
                _shihabConfig.ShihabAutoRetryIntervalSec = (int)shihabRetrySecNum.Value;
                _shihabConfig.MehediMaxRetryAttempts = (int)shihabMaxRetNum.Value;
                _shihabConfig.MehediGlobalSpeedLimiterEnabled = shihabGlobalLimChk.Checked;
                _shihabConfig.ShihabGlobalSpeedLimitKb = (int)shihabGlobSpdNum.Value;
                _shihabConfig.ShihabDefaultUserAgent = shihabUaBox.Text.Trim();
                _shihabConfig.ShihabQuotaEnabled = shihabQuotaChk.Checked;
                _shihabConfig.ShihabQuotaLimitMegabytes = (int)shihabQuotaMbNum.Value;
                _shihabConfig.ShihabQuotaWindowHours = (int)shihabQuotaHrsNum.Value;

                _shihabConfig.MehediUseHttpProxy = shihabUseProxyChk.Checked;
                _shihabConfig.ShihabProxyAddress = shihabPrxHostBox.Text.Trim();
                _shihabConfig.MehediProxyPort = (int)shihabPrxPortNum.Value;
                _shihabConfig.ShihabProxyUser = shihabPrxUserBox.Text.Trim();
                _shihabConfig.MehediProxyPassword = shihabPrxPassBox.Text;
                _shihabConfig.ShihabUseSocksProxy = mehediUseSocksChk.Checked;
                _shihabConfig.MehediSocksHost = mehediSocksHostBox.Text.Trim();
                _shihabConfig.ShihabSocksPort = (int)mehediSocksPortNum.Value;
                _shihabConfig.ShihabPacScriptUrl = shihabPacUrlBox.Text.Trim();
                _shihabConfig.ShihabProxyMode = !string.IsNullOrWhiteSpace(_shihabConfig.ShihabPacScriptUrl)
                    ? "PAC"
                    : (_shihabConfig.ShihabUseSocksProxy ? "SOCKS5" : (_shihabConfig.MehediUseHttpProxy ? "Manual" : "System"));
                _shihabConfig.ShihabDnsMode = shihabDnsModeCombo.SelectedItem != null ? shihabDnsModeCombo.SelectedItem.ToString() : "System";
                _shihabConfig.ShihabDnsProvider = shihabDohProvCombo.SelectedItem != null ? shihabDohProvCombo.SelectedItem.ToString() : "Cloudflare";
                _shihabConfig.ShihabCustomDohUrl = shihabCustomDohBox.Text.Trim();
                _shihabConfig.ShihabPlainDnsServer = string.IsNullOrWhiteSpace(shihabPlainDnsBox.Text) ? "1.1.1.1" : shihabPlainDnsBox.Text.Trim();
                _shihabConfig.ShihabNextDnsConfigId = shihabNextDnsBox.Text.Trim();

                _shihabConfig.MehediDialUpConnectionName = shihabDialConnBox.Text.Trim();
                _shihabConfig.ShihabRedialIfDisconnected = mehediRedialChk.Checked;
                _shihabConfig.MehediHangUpWhenDone = shihabHangUpChk.Checked;
                _shihabConfig.ShihabAutoResumeOnNetworkRestore = shihabNetwatchChk.Checked;
                if (shihabBindAdapterCombo.SelectedIndex <= 0)
                {
                    _shihabConfig.ShihabBoundNetworkAdapter = "";
                }
                else
                {
                    string rawSel = shihabBindAdapterCombo.SelectedItem.ToString();
                    int parenIdx = rawSel.IndexOf(" (", StringComparison.Ordinal);
                    _shihabConfig.ShihabBoundNetworkAdapter = parenIdx > 0 ? rawSel.Substring(0, parenIdx).Trim() : rawSel.Trim();
                }

                _shihabConfig.MehediPlaySoundComplete = mehediSndCompChk.Checked;
                _shihabConfig.ShihabSoundCompleteWav = shihabSndCompBox.Text.Trim();
                _shihabConfig.MehediPlaySoundError = shihabSndErrChk.Checked;
                _shihabConfig.ShihabSoundErrorWav = mehediSndErrBox.Text.Trim();

                _shihabConfig.ShihabAutoCheckUpdates = shihabAutoUpdChk.Checked;
                _shihabConfig.ShihabUpdateIncludePrereleases = shihabPreRelChk.Checked;
                _shihabConfig.ShihabTorrentSequentialDefault = shihabTorSeqDefChk.Checked;
                _shihabConfig.ShihabTorrentKeepSeedingDefault = shihabTorSeedDefChk.Checked;
                _shihabConfig.ShihabTorrentBlocklistUrl = shihabBlkUrlBox.Text.Trim();
                _shihabConfig.ShihabCrashMonitorEnabled = shihabCrashMonChk.Checked;

                ShihabSecretsVault.SealConfigurationSecrets(_shihabConfig);
                _mehediStore.SaveMehediConfig(_shihabConfig);
                ShihabSecretsVault.UnsealConfigurationSecrets(_shihabConfig);
                DialogResult = DialogResult.OK;
                Close();
            };

            Button mehediCancel = ShihabThemePalette.CreateMehediStyledButton("Cancel", mehediSurf, ForeColor, 102);
            mehediCancel.Location = new Point(442, 478);
            mehediCancel.Click += delegate { Close(); };

            Button shihabHelpBtn = ShihabThemePalette.CreateMehediStyledButton("Help", mehediSurf, ForeColor, 100);
            shihabHelpBtn.Location = new Point(552, 478);
            shihabHelpBtn.Click += delegate
            {
                MessageBox.Show(
                    this,
                    "MHS Download Manager 1.0.4 Configuration\nDeveloper: Mehedi Hasan Shihab (https://sshihabb007.github.io/)\n\n" +
                    "• General: Browser integration, startup launch, shortcut keys & web player panels\n" +
                    "• File types: Auto-captured extensions (wildcards like R0* supported), site & URL exclusions\n" +
                    "• Save to: Category folders, extensions, server creation/modification dates & temp directory\n" +
                    "• Downloads / Connection / Proxy / Sites Logins / Dial Up / Sounds: Full transfer control.",
                    "MHS Download Manager Help",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            };

            AcceptButton = shihabSaveOk;
            CancelButton = mehediCancel;
            Controls.AddRange(new Control[] { mehediTabs, shihabSaveOk, mehediCancel, shihabHelpBtn });
        }

        private static void AddShihabSectionBanner(TabPage mehediPage, string shihabHeaderText, bool mehediDark)
        {
            Label shihabHdr = new Label
            {
                Text = shihabHeaderText,
                Location = new Point(16, 10),
                Size = new Size(592, 20),
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
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

        private static void PreviewShihabSoundFile(string mehediWavPath, bool shihabIsError)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(mehediWavPath) && File.Exists(mehediWavPath))
                {
                    using (SoundPlayer shihabSp = new SoundPlayer(mehediWavPath))
                    {
                        shihabSp.Play();
                    }
                    return;
                }
                if (shihabIsError) SystemSounds.Hand.Play();
                else SystemSounds.Asterisk.Play();
            }
            catch { }
        }

        private void ShowShihabKeysModal(bool mehediDark, ref string shihabPreventKey, ref string mehediForceKey)
        {
            using (Form shihabDlg = CreateMehediSubModal("Customize Shortcut Keys — MHS Download Manager", new Size(440, 235), mehediDark))
            {
                Color mehediSurf = ShihabThemePalette.GetShihabSurfaceBg(mehediDark);
                Label shihabPrevLbl = new Label { Text = "Hold this key while clicking a link to PREVENT MHS capture:", Location = new Point(16, 20), AutoSize = true };
                ComboBox mehediPrevCombo = new ComboBox { Location = new Point(16, 44), Width = 390, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = mehediSurf, ForeColor = shihabDlg.ForeColor };
                mehediPrevCombo.Items.AddRange(new object[] { "Alt", "Ctrl", "Shift", "None" });
                int shihabPIdx = mehediPrevCombo.FindStringExact(shihabPreventKey);
                mehediPrevCombo.SelectedIndex = shihabPIdx >= 0 ? shihabPIdx : 0;

                Label mehediForceLbl = new Label { Text = "Hold this key while clicking a link to FORCE MHS capture:", Location = new Point(16, 86), AutoSize = true };
                ComboBox shihabForceCombo = new ComboBox { Location = new Point(16, 110), Width = 390, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = mehediSurf, ForeColor = shihabDlg.ForeColor };
                shihabForceCombo.Items.AddRange(new object[] { "Ins", "Ctrl+Alt", "Shift+Alt", "None" });
                int mehediFIdx = shihabForceCombo.FindStringExact(mehediForceKey);
                shihabForceCombo.SelectedIndex = mehediFIdx >= 0 ? mehediFIdx : 0;

                Button shihabOk = ShihabThemePalette.CreateMehediStyledButton("OK", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 92);
                shihabOk.Location = new Point(214, 152);
                shihabOk.DialogResult = DialogResult.OK;
                Button mehediCancel = ShihabThemePalette.CreateMehediStyledButton("Cancel", mehediSurf, shihabDlg.ForeColor, 92);
                mehediCancel.Location = new Point(314, 152);
                mehediCancel.DialogResult = DialogResult.Cancel;

                shihabDlg.Controls.AddRange(new Control[] { shihabPrevLbl, mehediPrevCombo, mehediForceLbl, shihabForceCombo, shihabOk, mehediCancel });
                if (shihabDlg.ShowDialog(this) == DialogResult.OK)
                {
                    shihabPreventKey = mehediPrevCombo.SelectedItem.ToString();
                    mehediForceKey = shihabForceCombo.SelectedItem.ToString();
                }
            }
        }

        private void ShowMehediContextMenuModal(bool shihabDark, ref string mehediCtxCsv)
        {
            using (Form mehediDlg = CreateMehediSubModal("Customize Browser Context Menu — MHS Download Manager", new Size(460, 240), shihabDark))
            {
                Color shihabSurf = ShihabThemePalette.GetShihabSurfaceBg(shihabDark);
                Label mehediInfo = new Label { Text = "Select items to display in browser right-click menu:", Location = new Point(16, 16), AutoSize = true };
                CheckedListBox shihabClb = new CheckedListBox { Location = new Point(16, 42), Size = new Size(412, 102), CheckOnClick = true, BackColor = shihabSurf, ForeColor = mehediDlg.ForeColor };
                string[] mehediItems = new string[] { "Download with MHS", "Download video with MHS", "Download all links with MHS" };
                for (int shihabI = 0; shihabI < mehediItems.Length; shihabI++)
                {
                    bool mehediHas = (mehediCtxCsv ?? "").IndexOf(mehediItems[shihabI], StringComparison.OrdinalIgnoreCase) >= 0;
                    shihabClb.Items.Add(mehediItems[shihabI], mehediHas);
                }

                Button shihabOk = ShihabThemePalette.CreateMehediStyledButton("OK", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 92);
                shihabOk.Location = new Point(236, 156);
                shihabOk.DialogResult = DialogResult.OK;
                Button mehediCancel = ShihabThemePalette.CreateMehediStyledButton("Cancel", shihabSurf, mehediDlg.ForeColor, 92);
                mehediCancel.Location = new Point(336, 156);
                mehediCancel.DialogResult = DialogResult.Cancel;

                mehediDlg.Controls.AddRange(new Control[] { mehediInfo, shihabClb, shihabOk, mehediCancel });
                if (mehediDlg.ShowDialog(this) == DialogResult.OK)
                {
                    List<string> shihabSel = new List<string>();
                    for (int mehediI = 0; mehediI < shihabClb.CheckedItems.Count; mehediI++)
                    {
                        shihabSel.Add(shihabClb.CheckedItems[mehediI].ToString());
                    }
                    mehediCtxCsv = string.Join(",", shihabSel.ToArray());
                }
            }
        }

        private void ShowShihabDownloadPanelsModal(
            bool mehediDark,
            ref bool shihabShowPanel,
            ref bool mehediMiniMode,
            ref bool shihabSkipAutoStart,
            ref int mehediMinVideoKb)
        {
            using (Form shihabDlg = CreateMehediSubModal("Customize Download Panels in Browsers", new Size(480, 265), mehediDark))
            {
                Color mehediSurf = ShihabThemePalette.GetShihabSurfaceBg(mehediDark);
                CheckBox shihabShowChk = new CheckBox { Text = "Show floating MHS video download icon on web players", Location = new Point(18, 20), AutoSize = true, Checked = shihabShowPanel };
                CheckBox mehediMiniChk = new CheckBox { Text = "Mini mode (compact corner download badge)", Location = new Point(18, 52), AutoSize = true, Checked = mehediMiniMode };
                CheckBox shihabSkipChk = new CheckBox { Text = "Don't start downloading from web players automatically", Location = new Point(18, 84), AutoSize = true, Checked = shihabSkipAutoStart };
                Label mehediMinLbl = new Label { Text = "Minimum video size to show panel (KB):", Location = new Point(18, 122), AutoSize = true };
                NumericUpDown shihabMinNum = new NumericUpDown { Location = new Point(280, 119), Width = 110, Minimum = 0, Maximum = 500000, Value = Math.Max(0, mehediMinVideoKb) };

                Button shihabOk = ShihabThemePalette.CreateMehediStyledButton("OK", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 92);
                shihabOk.Location = new Point(256, 178);
                shihabOk.DialogResult = DialogResult.OK;
                Button mehediCancel = ShihabThemePalette.CreateMehediStyledButton("Cancel", mehediSurf, shihabDlg.ForeColor, 92);
                mehediCancel.Location = new Point(356, 178);
                mehediCancel.DialogResult = DialogResult.Cancel;

                shihabDlg.Controls.AddRange(new Control[] { shihabShowChk, mehediMiniChk, shihabSkipChk, mehediMinLbl, shihabMinNum, shihabOk, mehediCancel });
                if (shihabDlg.ShowDialog(this) == DialogResult.OK)
                {
                    shihabShowPanel = shihabShowChk.Checked;
                    mehediMiniMode = mehediMiniChk.Checked;
                    shihabSkipAutoStart = shihabSkipChk.Checked;
                    mehediMinVideoKb = (int)shihabMinNum.Value;
                }
            }
        }

        private void ShowMehediExcludedAddressesModal(bool shihabDark, ref string mehediAddressesSpaceSeparated)
        {
            using (Form mehediDlg = CreateMehediSubModal("Addresses Excluded from Automatic Capture", new Size(520, 310), shihabDark))
            {
                Color shihabSurf = ShihabThemePalette.GetShihabSurfaceBg(shihabDark);
                Label mehediHint = new Label
                {
                    Text = "Enter URL patterns (wildcards * supported, one per line or space-separated):",
                    Location = new Point(16, 16),
                    AutoSize = true
                };
                TextBox shihabBox = new TextBox
                {
                    Location = new Point(16, 42),
                    Size = new Size(472, 170),
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    BackColor = shihabSurf,
                    ForeColor = mehediDlg.ForeColor,
                    Text = (mehediAddressesSpaceSeparated ?? "").Replace(" ", "\r\n")
                };

                Button shihabOk = ShihabThemePalette.CreateMehediStyledButton("OK", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 92);
                shihabOk.Location = new Point(296, 224);
                shihabOk.DialogResult = DialogResult.OK;
                Button mehediCancel = ShihabThemePalette.CreateMehediStyledButton("Cancel", shihabSurf, mehediDlg.ForeColor, 92);
                mehediCancel.Location = new Point(396, 224);
                mehediCancel.DialogResult = DialogResult.Cancel;

                mehediDlg.Controls.AddRange(new Control[] { mehediHint, shihabBox, shihabOk, mehediCancel });
                if (mehediDlg.ShowDialog(this) == DialogResult.OK)
                {
                    string[] shihabLines = shihabBox.Text.Split(new char[] { '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    mehediAddressesSpaceSeparated = string.Join(" ", shihabLines);
                }
            }
        }

        private void ShowShihabCategoryEditorModal(
            bool mehediDark,
            ShihabCategoryRule shihabExisting,
            Action mehediRefreshCallback,
            ComboBox shihabCatCombo)
        {
            bool mehediIsEdit = shihabExisting != null;
            using (Form shihabDlg = CreateMehediSubModal(mehediIsEdit ? "Edit Category" : "New Category", new Size(470, 260), mehediDark))
            {
                Color mehediSurf = ShihabThemePalette.GetShihabSurfaceBg(mehediDark);
                string shihabDownloadsRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

                Label mehediNameLbl = new Label { Text = "Category name:", Location = new Point(16, 18), AutoSize = true };
                TextBox shihabNameBox = new TextBox
                {
                    Location = new Point(140, 15),
                    Width = 298,
                    Text = mehediIsEdit ? shihabExisting.ShihabName : "Custom Category",
                    BackColor = mehediSurf,
                    ForeColor = shihabDlg.ForeColor
                };

                Label shihabExtLbl = new Label { Text = "File extensions:", Location = new Point(16, 56), AutoSize = true };
                TextBox mehediExtBox = new TextBox
                {
                    Location = new Point(140, 53),
                    Width = 298,
                    Text = mehediIsEdit ? (shihabExisting.MehediExtensionsSpaceSeparated ?? "") : "torrent iso",
                    BackColor = mehediSurf,
                    ForeColor = shihabDlg.ForeColor
                };

                Label mehediDirLbl = new Label { Text = "Default folder:", Location = new Point(16, 94), AutoSize = true };
                TextBox shihabDirBox = new TextBox
                {
                    Location = new Point(140, 91),
                    Width = 298,
                    Text = mehediIsEdit ? (shihabExisting.ShihabDefaultFolder ?? shihabDownloadsRoot) : Path.Combine(shihabDownloadsRoot, "Custom"),
                    BackColor = mehediSurf,
                    ForeColor = shihabDlg.ForeColor
                };

                Button mehediDelBtn = ShihabThemePalette.CreateMehediStyledButton("Delete", ShihabThemePalette.GetMehediAccentRed(), Color.White, 86);
                mehediDelBtn.Location = new Point(16, 168);
                mehediDelBtn.Visible = mehediIsEdit;
                mehediDelBtn.Click += delegate
                {
                    _shihabConfig.ShihabCategories.Remove(shihabExisting);
                    mehediRefreshCallback();
                    shihabDlg.DialogResult = DialogResult.Cancel;
                    shihabDlg.Close();
                };

                Button shihabOk = ShihabThemePalette.CreateMehediStyledButton("OK", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 92);
                shihabOk.Location = new Point(246, 168);
                shihabOk.Click += delegate
                {
                    string mehediNewName = shihabNameBox.Text.Trim();
                    if (string.IsNullOrEmpty(mehediNewName)) return;
                    if (mehediIsEdit)
                    {
                        shihabExisting.ShihabName = mehediNewName;
                        shihabExisting.MehediExtensionsSpaceSeparated = mehediExtBox.Text.Trim();
                        shihabExisting.ShihabDefaultFolder = shihabDirBox.Text.Trim();
                    }
                    else
                    {
                        _shihabConfig.ShihabCategories.Add(new ShihabCategoryRule
                        {
                            ShihabName = mehediNewName,
                            MehediExtensionsSpaceSeparated = mehediExtBox.Text.Trim(),
                            ShihabDefaultFolder = shihabDirBox.Text.Trim(),
                            MehediRememberLastSavePath = true
                        });
                    }
                    mehediRefreshCallback();
                    if (!mehediIsEdit && shihabCatCombo.Items.Count > 0)
                    {
                        shihabCatCombo.SelectedIndex = shihabCatCombo.Items.Count - 1;
                    }
                    shihabDlg.DialogResult = DialogResult.OK;
                    shihabDlg.Close();
                };

                Button mehediCancel = ShihabThemePalette.CreateMehediStyledButton("Cancel", mehediSurf, shihabDlg.ForeColor, 92);
                mehediCancel.Location = new Point(346, 168);
                mehediCancel.DialogResult = DialogResult.Cancel;

                shihabDlg.Controls.AddRange(new Control[] { mehediNameLbl, shihabNameBox, shihabExtLbl, mehediExtBox, mehediDirLbl, shihabDirBox, mehediDelBtn, shihabOk, mehediCancel });
                shihabDlg.ShowDialog(this);
            }
        }

        private void ShowShihabHostLimitsModal(bool shihabDark)
        {
            using (Form dlg = CreateMehediSubModal("Per-Host Connection Limits (1..128)", new Size(520, 340), shihabDark))
            {
                Color surf = ShihabThemePalette.GetShihabSurfaceBg(shihabDark);
                ShihabModernListView list = new ShihabModernListView
                {
                    Location = new Point(16, 16),
                    Size = new Size(472, 200),
                    BorderStyle = BorderStyle.FixedSingle,
                    MehediDarkMode = shihabDark,
                    ShihabShowFileTypeBadges = false,
                    BackColor = surf,
                    ForeColor = dlg.ForeColor
                };
                list.Columns.Add("Host Pattern (e.g. github.com)", 320);
                list.Columns.Add("Max Connections", 130);

                Action reload = delegate
                {
                    list.Items.Clear();
                    if (_shihabConfig.ShihabHostConnectionLimits == null) _shihabConfig.ShihabHostConnectionLimits = new List<ShihabHostConnectionLimit>();
                    for (int i = 0; i < _shihabConfig.ShihabHostConnectionLimits.Count; i++)
                    {
                        ShihabHostConnectionLimit lim = _shihabConfig.ShihabHostConnectionLimits[i];
                        list.Items.Add(new ListViewItem(new string[] { lim.ShihabHostPattern, lim.MehediMaxConnections.ToString() }));
                    }
                };
                reload();

                TextBox hostBox = new TextBox { Location = new Point(16, 228), Width = 240, Text = "cdn.example.com", BackColor = surf, ForeColor = dlg.ForeColor };
                NumericUpDown connNum = new NumericUpDown { Location = new Point(264, 227), Width = 75, Minimum = 1, Maximum = 128, Value = 16 };
                Button addBtn = ShihabThemePalette.CreateMehediStyledButton("Add", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 68);
                addBtn.Location = new Point(348, 224);
                addBtn.Click += delegate
                {
                    if (!string.IsNullOrWhiteSpace(hostBox.Text))
                    {
                        _shihabConfig.ShihabHostConnectionLimits.Add(new ShihabHostConnectionLimit
                        {
                            ShihabHostPattern = hostBox.Text.Trim(),
                            MehediMaxConnections = (int)connNum.Value
                        });
                        reload();
                    }
                };
                Button delBtn = ShihabThemePalette.CreateMehediStyledButton("Del", ShihabThemePalette.GetMehediAccentRed(), Color.White, 64);
                delBtn.Location = new Point(424, 224);
                delBtn.Click += delegate
                {
                    if (list.SelectedIndices.Count > 0)
                    {
                        _shihabConfig.ShihabHostConnectionLimits.RemoveAt(list.SelectedIndices[0]);
                        reload();
                    }
                };
                Button closeBtn = ShihabThemePalette.CreateMehediStyledButton("Done", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 100);
                closeBtn.Location = new Point(388, 262);
                closeBtn.DialogResult = DialogResult.OK;

                dlg.Controls.AddRange(new Control[] { list, hostBox, connNum, addBtn, delBtn, closeBtn });
                dlg.ShowDialog(this);
            }
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
                Font = new Font("Segoe UI", 9f),
                Icon = ShihabThemePalette.CreateShihabAppIcon()
            };
            ShihabThemePalette.ApplyMehediNativeDarkFrame(mehediModal, shihabDark);
            return mehediModal;
        }

        private TabPage CreateMehediPage(string shihabTitle, bool mehediDark)
        {
            return new TabPage(shihabTitle)
            {
                BackColor = ShihabThemePalette.GetMehediWindowBg(mehediDark),
                ForeColor = ShihabThemePalette.GetShihabPrimaryText(mehediDark)
            };
        }
    }

    public class MehediSchedulerQueuesForm : Form
    {
        private readonly MehediAppConfiguration _shihabConfig;
        private readonly MehediDynamicSegmentEngine _mehediEngine;
        private readonly ShihabStateStore _shihabStore;
        private ListBox _mehediQueueListBox;
        private CheckBox _shihabEnableStartChk;
        private TextBox _mehediStartHhMmBox;
        private TextBox _shihabOnceDateBox;
        private CheckBox _shihabEnableStopChk;
        private TextBox _mehediStopHhMmBox;
        private CheckBox _shihabStartOnStartupChk;
        private NumericUpDown _shihabMaxConcNum;
        private NumericUpDown _mehediRetryNum;
        private CheckBox _shihabExitDoneChk;
        private TextBox _shihabRunCmdDoneBox;
        private ComboBox _shihabPowerDoneCombo;
        private ListView _mehediFilesInQueueView;

        public MehediSchedulerQueuesForm(
            MehediAppConfiguration shihabConfig,
            MehediDynamicSegmentEngine mehediEngine,
            ShihabStateStore shihabStore)
        {
            _shihabConfig = shihabConfig;
            _mehediEngine = mehediEngine;
            _shihabStore = shihabStore;
            bool mehediDark = shihabConfig.ShihabDarkMode;
            Color surf = ShihabThemePalette.GetShihabSurfaceBg(mehediDark);

            Text = "Scheduler and Queues — " + ShihabThemePalette.ShihabMainHeaderTitle;
            Size = new Size(730, 505);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ShihabThemePalette.GetMehediWindowBg(mehediDark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(mehediDark);
            Font = new Font("Segoe UI", 9f);
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, mehediDark);

            Label shihabQLbl = new Label { Text = "Queues:", Location = new Point(14, 14), AutoSize = true, Font = new Font("Segoe UI Semibold", 9.5f) };
            _mehediQueueListBox = new ListBox
            {
                Location = new Point(14, 38),
                Size = new Size(195, 325),
                BackColor = surf,
                ForeColor = ForeColor
            };

            Button shihabNewQBtn = ShihabThemePalette.CreateMehediStyledButton("+ New Queue", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 94);
            shihabNewQBtn.Location = new Point(14, 372);
            shihabNewQBtn.Click += delegate
            {
                string mehediNewName = "Shihab Queue #" + (_shihabConfig.MehediQueues.Count + 1);
                MehediQueueConfig shihabQ = new MehediQueueConfig { ShihabQueueName = mehediNewName };
                _shihabConfig.MehediQueues.Add(shihabQ);
                PopulateShihabQueuesList();
                _mehediQueueListBox.SelectedIndex = _mehediQueueListBox.Items.Count - 1;
            };

            Button mehediDelQBtn = ShihabThemePalette.CreateMehediStyledButton("Delete", ShihabThemePalette.GetMehediAccentRed(), Color.White, 94);
            mehediDelQBtn.Location = new Point(115, 372);
            mehediDelQBtn.Click += delegate
            {
                int shihabIdx = _mehediQueueListBox.SelectedIndex;
                if (shihabIdx > 1 && shihabIdx < _shihabConfig.MehediQueues.Count)
                {
                    _shihabConfig.MehediQueues.RemoveAt(shihabIdx);
                    PopulateShihabQueuesList();
                }
            };

            ShihabDarkTabControl shihabRightTabs = new ShihabDarkTabControl
            {
                ShihabDarkMode = mehediDark,
                Location = new Point(222, 16),
                Size = new Size(478, 392)
            };
            TabPage mehediSchedTab = new TabPage("Schedule") { BackColor = BackColor, ForeColor = ForeColor };
            TabPage shihabFilesTab = new TabPage("Files in the queue") { BackColor = BackColor, ForeColor = ForeColor };

            _shihabEnableStartChk = new CheckBox { Text = "Start queue at (HH:mm):", Location = new Point(16, 18), AutoSize = true };
            _mehediStartHhMmBox = new TextBox { Location = new Point(195, 16), Width = 68, Text = "01:00", BackColor = surf, ForeColor = ForeColor };
            Label onceDateLbl = new Label { Text = "Once date (YYYY-MM-DD):", Location = new Point(272, 19), AutoSize = true };
            _shihabOnceDateBox = new TextBox { Location = new Point(358, 44), Width = 98, Text = "", BackColor = surf, ForeColor = ForeColor };
            onceDateLbl.Location = new Point(195, 47);

            _shihabEnableStopChk = new CheckBox { Text = "Stop queue at (HH:mm):", Location = new Point(16, 76), AutoSize = true };
            _mehediStopHhMmBox = new TextBox { Location = new Point(195, 74), Width = 68, Text = "07:00", BackColor = surf, ForeColor = ForeColor };
            _shihabStartOnStartupChk = new CheckBox { Text = "Start on app launch", Location = new Point(280, 76), AutoSize = true };

            Label mehediConcLbl = new Label { Text = "Download files at the same time:", Location = new Point(16, 112), AutoSize = true };
            _shihabMaxConcNum = new NumericUpDown { Location = new Point(265, 109), Width = 85, Minimum = 1, Maximum = 16, Value = 4 };

            Label shihabRetLbl = new Label { Text = "Number of retries per failed file:", Location = new Point(16, 146), AutoSize = true };
            _mehediRetryNum = new NumericUpDown { Location = new Point(265, 143), Width = 85, Minimum = 0, Maximum = 99, Value = 3 };

            _shihabExitDoneChk = new CheckBox { Text = "Exit MHS Download Manager when queue finishes", Location = new Point(16, 180), AutoSize = true };

            Label runCmdLbl = new Label { Text = "Run command when done:", Location = new Point(16, 212), AutoSize = true };
            _shihabRunCmdDoneBox = new TextBox { Location = new Point(185, 209), Width = 270, BackColor = surf, ForeColor = ForeColor };

            Label pwrDoneLbl = new Label { Text = "System power when done:", Location = new Point(16, 246), AutoSize = true };
            _shihabPowerDoneCombo = new ComboBox { Location = new Point(185, 243), Width = 165, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = surf, ForeColor = ForeColor };
            _shihabPowerDoneCombo.Items.AddRange(new object[] { "None", "Sleep", "Hibernate", "Shutdown" });
            _shihabPowerDoneCombo.SelectedIndex = 0;

            Button mehediApplyQBtn = ShihabThemePalette.CreateMehediStyledButton("Apply Schedule", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 135);
            mehediApplyQBtn.Location = new Point(16, 286);
            mehediApplyQBtn.Click += delegate { SaveSelectedMehediQueueSettings(); };

            mehediSchedTab.Controls.AddRange(new Control[]
            {
                _shihabEnableStartChk, _mehediStartHhMmBox, onceDateLbl, _shihabOnceDateBox,
                _shihabEnableStopChk, _mehediStopHhMmBox, _shihabStartOnStartupChk,
                mehediConcLbl, _shihabMaxConcNum, shihabRetLbl, _mehediRetryNum,
                _shihabExitDoneChk, runCmdLbl, _shihabRunCmdDoneBox, pwrDoneLbl, _shihabPowerDoneCombo, mehediApplyQBtn
            });

            _mehediFilesInQueueView = new ShihabModernListView
            {
                Location = new Point(12, 12),
                Size = new Size(444, 298),
                BorderStyle = BorderStyle.FixedSingle,
                MehediDarkMode = mehediDark,
                ShihabShowFileTypeBadges = true,
                BackColor = surf,
                ForeColor = ForeColor
            };
            _mehediFilesInQueueView.Columns.Add("File Name", 220);
            _mehediFilesInQueueView.Columns.Add("Size", 95);
            _mehediFilesInQueueView.Columns.Add("Status", 110);

            Button shihabRemoveFromQ = ShihabThemePalette.CreateMehediStyledButton("Remove from Queue", surf, ForeColor, 155);
            shihabRemoveFromQ.Location = new Point(12, 318);
            shihabRemoveFromQ.Click += delegate
            {
                if (_mehediFilesInQueueView.SelectedItems.Count > 0)
                {
                    ShihabDownloadItem mehediSel = _mehediFilesInQueueView.SelectedItems[0].Tag as ShihabDownloadItem;
                    if (mehediSel != null)
                    {
                        mehediSel.MehediQueueName = "";
                        _mehediEngine.PersistShihabState();
                        LoadSelectedShihabQueueDetails();
                    }
                }
            };
            shihabFilesTab.Controls.AddRange(new Control[] { _mehediFilesInQueueView, shihabRemoveFromQ });

            shihabRightTabs.TabPages.Add(mehediSchedTab);
            shihabRightTabs.TabPages.Add(shihabFilesTab);

            _mehediQueueListBox.SelectedIndexChanged += delegate { LoadSelectedShihabQueueDetails(); };

            Button shihabStartNowBtn = ShihabThemePalette.CreateMehediStyledButton("Start Now", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 120);
            shihabStartNowBtn.Location = new Point(332, 420);
            shihabStartNowBtn.Click += delegate
            {
                SaveSelectedMehediQueueSettings();
                MehediQueueConfig mehediQ = GetSelectedShihabQueue();
                if (mehediQ != null) _mehediEngine.StartShihabQueue(mehediQ.ShihabQueueName);
                Close();
            };

            Button mehediStopNowBtn = ShihabThemePalette.CreateMehediStyledButton("Stop Queue", ShihabThemePalette.GetShihabAccentAmber(), Color.White, 120);
            mehediStopNowBtn.Location = new Point(462, 420);
            mehediStopNowBtn.Click += delegate
            {
                MehediQueueConfig shihabQ = GetSelectedShihabQueue();
                if (shihabQ != null) _mehediEngine.StopShihabQueue(shihabQ.ShihabQueueName);
            };

            Button shihabCloseBtn = ShihabThemePalette.CreateMehediStyledButton("Close", surf, ForeColor, 108);
            shihabCloseBtn.Location = new Point(592, 420);
            shihabCloseBtn.Click += delegate
            {
                SaveSelectedMehediQueueSettings();
                Close();
            };

            Controls.AddRange(new Control[] { shihabQLbl, _mehediQueueListBox, shihabNewQBtn, mehediDelQBtn, shihabRightTabs, shihabStartNowBtn, mehediStopNowBtn, shihabCloseBtn });
            PopulateShihabQueuesList();
        }

        private void PopulateShihabQueuesList()
        {
            _mehediQueueListBox.Items.Clear();
            for (int shihabI = 0; shihabI < _shihabConfig.MehediQueues.Count; shihabI++)
            {
                _mehediQueueListBox.Items.Add(_shihabConfig.MehediQueues[shihabI].ShihabQueueName);
            }
            if (_mehediQueueListBox.Items.Count > 0) _mehediQueueListBox.SelectedIndex = 0;
        }

        private MehediQueueConfig GetSelectedShihabQueue()
        {
            int mehediIdx = _mehediQueueListBox.SelectedIndex;
            if (mehediIdx >= 0 && mehediIdx < _shihabConfig.MehediQueues.Count)
            {
                return _shihabConfig.MehediQueues[mehediIdx];
            }
            return null;
        }

        private void LoadSelectedShihabQueueDetails()
        {
            MehediQueueConfig shihabQ = GetSelectedShihabQueue();
            if (shihabQ == null) return;
            _shihabEnableStartChk.Checked = shihabQ.MehediEnableScheduledStart;
            _mehediStartHhMmBox.Text = shihabQ.ShihabStartTimeHhMm ?? "01:00";
            _shihabOnceDateBox.Text = shihabQ.ShihabOnceAtDateYyyyMmDd ?? "";
            _shihabEnableStopChk.Checked = shihabQ.MehediEnableScheduledStop;
            _mehediStopHhMmBox.Text = shihabQ.ShihabStopTimeHhMm ?? "07:00";
            _shihabStartOnStartupChk.Checked = shihabQ.ShihabStartOnStartup;
            _shihabMaxConcNum.Value = Math.Max(1, Math.Min(16, shihabQ.MehediMaxConcurrentFiles));
            _mehediRetryNum.Value = Math.Max(0, Math.Min(99, shihabQ.ShihabRetriesPerFile));
            _shihabExitDoneChk.Checked = shihabQ.MehediExitAppWhenDone;
            _shihabRunCmdDoneBox.Text = shihabQ.ShihabRunCommandWhenDone ?? "";
            int pIdx = _shihabPowerDoneCombo.FindStringExact(shihabQ.ShihabSystemPowerActionWhenDone ?? "None");
            _shihabPowerDoneCombo.SelectedIndex = pIdx >= 0 ? pIdx : 0;

            _mehediFilesInQueueView.Items.Clear();
            List<ShihabDownloadItem> mehediAll = _mehediEngine.GetShihabDownloadsSnapshot();
            for (int shihabI = 0; shihabI < mehediAll.Count; shihabI++)
            {
                ShihabDownloadItem mehediIt = mehediAll[shihabI];
                if (string.Equals(mehediIt.MehediQueueName, shihabQ.ShihabQueueName, StringComparison.OrdinalIgnoreCase))
                {
                    ListViewItem shihabRow = new ListViewItem(new string[]
                    {
                        mehediIt.ShihabFileName,
                        ShihabFormatHelpers.FormatMehediBytes(mehediIt.ShihabTotalBytes),
                        mehediIt.ShihabStatusText
                    });
                    shihabRow.Tag = mehediIt;
                    _mehediFilesInQueueView.Items.Add(shihabRow);
                }
            }
        }

        private void SaveSelectedMehediQueueSettings()
        {
            MehediQueueConfig shihabQ = GetSelectedShihabQueue();
            if (shihabQ == null) return;
            shihabQ.MehediEnableScheduledStart = _shihabEnableStartChk.Checked;
            shihabQ.ShihabStartTimeHhMm = _mehediStartHhMmBox.Text.Trim();
            shihabQ.ShihabOnceAtDateYyyyMmDd = _shihabOnceDateBox.Text.Trim();
            shihabQ.MehediEnableScheduledStop = _shihabEnableStopChk.Checked;
            shihabQ.ShihabStopTimeHhMm = _mehediStopHhMmBox.Text.Trim();
            shihabQ.ShihabStartOnStartup = _shihabStartOnStartupChk.Checked;
            shihabQ.MehediMaxConcurrentFiles = (int)_shihabMaxConcNum.Value;
            shihabQ.ShihabRetriesPerFile = (int)_mehediRetryNum.Value;
            shihabQ.MehediExitAppWhenDone = _shihabExitDoneChk.Checked;
            shihabQ.ShihabRunCommandWhenDone = _shihabRunCmdDoneBox.Text.Trim();
            shihabQ.ShihabSystemPowerActionWhenDone = _shihabPowerDoneCombo.SelectedItem != null ? _shihabPowerDoneCombo.SelectedItem.ToString() : "None";
            _shihabStore.SaveMehediConfig(_shihabConfig);
        }
    }

    public class ShihabBatchDownloadForm : Form
    {
        public List<string> ShihabGeneratedUrls { get; private set; }
        public bool MehediStartImmediately { get; private set; }

        public ShihabBatchDownloadForm(bool shihabDark)
        {
            ShihabGeneratedUrls = new List<string>();
            Text = "Add Batch Download (Wildcard *) — " + ShihabThemePalette.ShihabMainHeaderTitle;
            Size = new Size(620, 360);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ShihabThemePalette.GetMehediWindowBg(shihabDark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(shihabDark);
            Font = new Font("Segoe UI", 9f);
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, shihabDark);

            Label mehediHint = new Label
            {
                Text = "Use asterisk (*) wildcard in the URL (e.g. https://example.com/files/part*.zip):",
                Location = new Point(16, 16),
                AutoSize = true
            };
            TextBox shihabPatternBox = new TextBox
            {
                Location = new Point(16, 40),
                Width = 570,
                Text = "https://example.com/archive/part*.zip",
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark),
                ForeColor = ForeColor
            };

            RadioButton mehediNumbersRad = new RadioButton { Text = "Numbers", Location = new Point(16, 80), AutoSize = true, Checked = true };
            RadioButton shihabLettersRad = new RadioButton { Text = "Letters (a..z)", Location = new Point(120, 80), AutoSize = true };

            Label mehediFromLbl = new Label { Text = "From:", Location = new Point(16, 116), AutoSize = true };
            TextBox shihabFromBox = new TextBox { Location = new Point(65, 113), Width = 70, Text = "1", BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark), ForeColor = ForeColor };

            Label mehediToLbl = new Label { Text = "To:", Location = new Point(155, 116), AutoSize = true };
            TextBox shihabToBox = new TextBox { Location = new Point(185, 113), Width = 70, Text = "10", BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark), ForeColor = ForeColor };

            Label mehediPadLbl = new Label { Text = "Wildcard size (zero pad):", Location = new Point(280, 116), AutoSize = true };
            NumericUpDown shihabPadNum = new NumericUpDown { Location = new Point(445, 113), Width = 65, Minimum = 1, Maximum = 8, Value = 2 };

            Label mehediPreviewLbl = new Label
            {
                Location = new Point(16, 158),
                Size = new Size(570, 85),
                ForeColor = ShihabThemePalette.GetShihabAccentBlue(),
                Text = ""
            };

            Func<List<string>> buildShihabList = delegate
            {
                List<string> mehediOut = new List<string>();
                string shihabPat = shihabPatternBox.Text.Trim();
                if (shihabPat.IndexOf('*') < 0) return mehediOut;

                if (mehediNumbersRad.Checked)
                {
                    int mehediStart;
                    int shihabEnd;
                    if (int.TryParse(shihabFromBox.Text.Trim(), out mehediStart) &&
                        int.TryParse(shihabToBox.Text.Trim(), out shihabEnd) &&
                        shihabEnd >= mehediStart && (shihabEnd - mehediStart) < 5000)
                    {
                        int mehediWidth = (int)shihabPadNum.Value;
                        for (int shihabN = mehediStart; shihabN <= shihabEnd; shihabN++)
                        {
                            mehediOut.Add(shihabPat.Replace("*", shihabN.ToString().PadLeft(mehediWidth, '0')));
                        }
                    }
                }
                else
                {
                    string shihabA = shihabFromBox.Text.Trim();
                    string mehediB = shihabToBox.Text.Trim();
                    if (shihabA.Length == 1 && mehediB.Length == 1 && shihabBValidChar(shihabA[0], mehediB[0]))
                    {
                        for (char shihabC = shihabA[0]; shihabC <= mehediB[0]; shihabC++)
                        {
                            mehediOut.Add(shihabPat.Replace("*", shihabC.ToString()));
                        }
                    }
                }
                return mehediOut;
            };

            Action updateMehediPreview = delegate
            {
                List<string> shihabPreview = buildShihabList();
                if (shihabPreview.Count == 0)
                {
                    mehediPreviewLbl.Text = "Ensure URL contains '*' and valid range (1..5000 numbers, or a..z / A..Z letters).";
                }
                else
                {
                    string mehediSecond = shihabPreview.Count > 1 ? shihabPreview[1] : "(only 1 file)";
                    mehediPreviewLbl.Text = string.Format("First:   {0}\nSecond:  {1}\nLast:    {2}   (Total: {3} files)",
                        shihabPreview[0], mehediSecond, shihabPreview[shihabPreview.Count - 1], shihabPreview.Count);
                }
            };

            shihabPatternBox.TextChanged += delegate { updateMehediPreview(); };
            shihabFromBox.TextChanged += delegate { updateMehediPreview(); };
            shihabToBox.TextChanged += delegate { updateMehediPreview(); };
            shihabPadNum.ValueChanged += delegate { updateMehediPreview(); };
            mehediNumbersRad.CheckedChanged += delegate
            {
                if (mehediNumbersRad.Checked) { shihabFromBox.Text = "1"; shihabToBox.Text = "10"; }
                else { shihabFromBox.Text = "a"; shihabToBox.Text = "f"; }
                updateMehediPreview();
            };
            updateMehediPreview();

            Button shihabQueueBtn = ShihabThemePalette.CreateMehediStyledButton("Add to Main Queue", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 155);
            shihabQueueBtn.Location = new Point(185, 265);
            shihabQueueBtn.Click += delegate
            {
                ShihabGeneratedUrls = buildShihabList();
                if (ShihabGeneratedUrls.Count == 0) return;
                MehediStartImmediately = false;
                DialogResult = DialogResult.OK;
                Close();
            };

            Button mehediStartBtn = ShihabThemePalette.CreateMehediStyledButton("Download Now", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 135);
            mehediStartBtn.Location = new Point(348, 265);
            mehediStartBtn.Click += delegate
            {
                ShihabGeneratedUrls = buildShihabList();
                if (ShihabGeneratedUrls.Count == 0) return;
                MehediStartImmediately = true;
                DialogResult = DialogResult.OK;
                Close();
            };

            Button shihabCancel = ShihabThemePalette.CreateMehediStyledButton("Cancel", ShihabThemePalette.GetShihabSurfaceBg(shihabDark), ForeColor, 95);
            shihabCancel.Location = new Point(491, 265);
            shihabCancel.Click += delegate { Close(); };

            Controls.AddRange(new Control[]
            {
                mehediHint, shihabPatternBox, mehediNumbersRad, shihabLettersRad,
                mehediFromLbl, shihabFromBox, mehediToLbl, shihabToBox, mehediPadLbl, shihabPadNum,
                mehediPreviewLbl, shihabQueueBtn, mehediStartBtn, shihabCancel
            });
        }

        private static bool shihabBValidChar(char mehediStart, char shihabEnd)
        {
            if (shihabEnd < mehediStart) return false;
            return (mehediStart >= 'a' && shihabEnd <= 'z') || (mehediStart >= 'A' && shihabEnd <= 'Z');
        }
    }

    public class MehediDownloadAllLinksForm : Form
    {
        public List<MehediBatchLinkEntryDto> ShihabChosenLinks { get; private set; }
        public bool MehediStartNow { get; private set; }

        public MehediDownloadAllLinksForm(ShihabBatchCaptureDto shihabBatch, bool mehediDark)
        {
            if (shihabBatch == null) shihabBatch = new ShihabBatchCaptureDto { ShihabLinks = new List<MehediBatchLinkEntryDto>() };
            if (shihabBatch.ShihabLinks == null) shihabBatch.ShihabLinks = new List<MehediBatchLinkEntryDto>();
            ShihabChosenLinks = new List<MehediBatchLinkEntryDto>();
            Text = "Download All Links — " + ShihabThemePalette.ShihabMainHeaderTitle;
            Size = new Size(800, 495);
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            BackColor = ShihabThemePalette.GetMehediWindowBg(mehediDark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(mehediDark);
            Font = new Font("Segoe UI", 9f);
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, mehediDark);
            Icon = ShihabThemePalette.CreateShihabAppIcon();

            CheckBox shihabHideHtmlChk = new CheckBox { Text = "Hide HTML web pages", Location = new Point(14, 14), AutoSize = true, Checked = true };
            CheckBox mehediHideImgChk = new CheckBox { Text = "Hide images (.png/.jpg/.gif/.svg)", Location = new Point(185, 14), AutoSize = true, Checked = false };
            Label shihabExtFilterLbl = new Label { Text = "Filter ext/text:", Location = new Point(425, 16), AutoSize = true };
            TextBox mehediFilterBox = new TextBox { Location = new Point(515, 13), Width = 250, BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor = ForeColor };

            ShihabModernListView shihabLinksView = new ShihabModernListView
            {
                Location = new Point(14, 46),
                Size = new Size(754, 340),
                BorderStyle = BorderStyle.FixedSingle,
                CheckBoxes = true,
                MehediDarkMode = mehediDark,
                ShihabShowFileTypeBadges = true,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark),
                ForeColor = ForeColor
            };
            shihabLinksView.Columns.Add("File Name / URL", 345);
            shihabLinksView.Columns.Add("Type", 75);
            shihabLinksView.Columns.Add("Size", 90);
            shihabLinksView.Columns.Add("Link Text", 215);

            Action rebuildMehediTable = delegate
            {
                shihabLinksView.BeginUpdate();
                shihabLinksView.Items.Clear();
                HashSet<string> mehediSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string shihabNeedle = mehediFilterBox.Text.Trim().ToLowerInvariant();

                for (int mehediI = 0; shihabBatch.ShihabLinks != null && mehediI < shihabBatch.ShihabLinks.Count; mehediI++)
                {
                    MehediBatchLinkEntryDto shihabEntry = shihabBatch.ShihabLinks[mehediI];
                    if (shihabEntry == null || string.IsNullOrWhiteSpace(shihabEntry.ShihabUrl)) continue;
                    if (!mehediSeen.Add(shihabEntry.ShihabUrl)) continue;

                    string shihabExt = ClassifyBatchUrlExtension(shihabEntry.ShihabUrl);
                    if (shihabHideHtmlChk.Checked && (shihabExt == "HTML" || shihabExt == "HTM" || shihabExt == "PHP" || shihabExt == "ASP" || shihabExt == "ASPX" || shihabExt == "JSP"))
                    {
                        continue;
                    }
                    if (mehediHideImgChk.Checked && (shihabExt == "JPG" || shihabExt == "JPEG" || shihabExt == "PNG" || shihabExt == "GIF" || shihabExt == "SVG" || shihabExt == "WEBP" || shihabExt == "ICO"))
                    {
                        continue;
                    }
                    if (!string.IsNullOrEmpty(shihabNeedle) &&
                        shihabEntry.ShihabUrl.ToLowerInvariant().IndexOf(shihabNeedle) < 0 &&
                        (shihabEntry.MehediText ?? "").ToLowerInvariant().IndexOf(shihabNeedle) < 0)
                    {
                        continue;
                    }

                    ListViewItem mehediRow = new ListViewItem(new string[] { shihabEntry.ShihabUrl, shihabExt, "-", shihabEntry.MehediText ?? "" });
                    mehediRow.Checked = true;
                    mehediRow.Tag = shihabEntry;
                    shihabLinksView.Items.Add(mehediRow);
                }
                shihabLinksView.EndUpdate();
            };

            shihabHideHtmlChk.CheckedChanged += delegate { rebuildMehediTable(); };
            mehediHideImgChk.CheckedChanged += delegate { rebuildMehediTable(); };
            mehediFilterBox.TextChanged += delegate { rebuildMehediTable(); };
            rebuildMehediTable();

            Button shihabChkAll = ShihabThemePalette.CreateMehediStyledButton("Check All", ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor, 86);
            shihabChkAll.Location = new Point(14, 398);
            shihabChkAll.Click += delegate { foreach (ListViewItem mehediIt in shihabLinksView.Items) mehediIt.Checked = true; };

            Button mehediUnchkAll = ShihabThemePalette.CreateMehediStyledButton("Uncheck All", ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor, 94);
            mehediUnchkAll.Location = new Point(106, 398);
            mehediUnchkAll.Click += delegate { foreach (ListViewItem shihabIt in shihabLinksView.Items) shihabIt.Checked = false; };

            Button probeSizesBtn = ShihabThemePalette.CreateMehediStyledButton("Probe Sizes", ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor, 96);
            probeSizesBtn.Location = new Point(206, 398);
            probeSizesBtn.Click += delegate
            {
                probeSizesBtn.Enabled = false;
                List<ListViewItem> rows = new List<ListViewItem>();
                foreach (ListViewItem r in shihabLinksView.Items) if (r.Checked) rows.Add(r);
                Task.Run(delegate
                {
                    for (int i = 0; i < rows.Count && i < 40; i++)
                    {
                        ListViewItem rowRef = rows[i];
                        MehediBatchLinkEntryDto dto = rowRef.Tag as MehediBatchLinkEntryDto;
                        if (dto == null) continue;
                        try
                        {
                            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(dto.ShihabUrl);
                            req.Method = "HEAD";
                            req.Timeout = 5000;
                            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                            {
                                long len = resp.ContentLength;
                                string sz = len > 0 ? ShihabFormatHelpers.FormatMehediBytes(len) : "Unknown";
                                BeginInvoke((Action)delegate { if (!IsDisposed) rowRef.SubItems[2].Text = sz; });
                            }
                        }
                        catch { }
                    }
                    try { BeginInvoke((Action)delegate { if (!IsDisposed) probeSizesBtn.Enabled = true; }); } catch { }
                });
            };

            Button importFileBtn = ShihabThemePalette.CreateMehediStyledButton("Import .ef2/.txt", ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor, 114);
            importFileBtn.Location = new Point(308, 398);
            importFileBtn.Click += delegate
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Link Lists (*.ef2;*.txt)|*.ef2;*.txt|All Files (*.*)|*.*" })
                {
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        List<ShihabDownloadItem> imported = ShihabStateStore.ImportFromMehediEf2OrText(ofd.FileName);
                        for (int ii = 0; ii < imported.Count; ii++)
                        {
                            shihabBatch.ShihabLinks.Add(new MehediBatchLinkEntryDto
                            {
                                ShihabUrl = imported[ii].MehediUrl,
                                MehediText = imported[ii].ShihabFileName,
                                ShihabReferer = imported[ii].MehediReferer
                            });
                        }
                        rebuildMehediTable();
                    }
                }
            };

            Button shihabQueueBtn = ShihabThemePalette.CreateMehediStyledButton("Add to Queue", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 122);
            shihabQueueBtn.Location = new Point(430, 398);
            shihabQueueBtn.Click += delegate
            {
                CollectCheckedShihabLinks(shihabLinksView);
                if (ShihabChosenLinks.Count == 0) return;
                MehediStartNow = false;
                DialogResult = DialogResult.OK;
                Close();
            };

            Button mehediNowBtn = ShihabThemePalette.CreateMehediStyledButton("Download Now", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 126);
            mehediNowBtn.Location = new Point(558, 398);
            mehediNowBtn.Click += delegate
            {
                CollectCheckedShihabLinks(shihabLinksView);
                if (ShihabChosenLinks.Count == 0) return;
                MehediStartNow = true;
                DialogResult = DialogResult.OK;
                Close();
            };

            Button shihabCancel = ShihabThemePalette.CreateMehediStyledButton("Cancel", ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor, 78);
            shihabCancel.Location = new Point(690, 398);
            shihabCancel.Click += delegate { Close(); };

            Controls.AddRange(new Control[]
            {
                shihabHideHtmlChk, mehediHideImgChk, shihabExtFilterLbl, mehediFilterBox,
                shihabLinksView, shihabChkAll, mehediUnchkAll, probeSizesBtn, importFileBtn, shihabQueueBtn, mehediNowBtn, shihabCancel
            });
        }

        private static string ClassifyBatchUrlExtension(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return "OTHER";
            if (rawUrl.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase)) return "MAGNET";
            Uri u;
            if (Uri.TryCreate(rawUrl, UriKind.Absolute, out u))
            {
                string path = u.AbsolutePath ?? "";
                if (string.IsNullOrEmpty(path) || path.EndsWith("/", StringComparison.Ordinal))
                {
                    return string.IsNullOrEmpty(u.Query) ? "HTML" : "OTHER";
                }
                string ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
                if (!string.IsNullOrEmpty(ext)) return ext;
                return "OTHER";
            }
            string fallbackExt = Path.GetExtension(ShihabFormatHelpers.ExtractMehediFileNameFromUrl(rawUrl)).TrimStart('.').ToUpperInvariant();
            return string.IsNullOrEmpty(fallbackExt) ? "OTHER" : fallbackExt;
        }

        private void CollectCheckedShihabLinks(ListView mehediView)
        {
            ShihabChosenLinks.Clear();
            foreach (ListViewItem shihabItem in mehediView.Items)
            {
                if (shihabItem.Checked && shihabItem.Tag is MehediBatchLinkEntryDto)
                {
                    ShihabChosenLinks.Add((MehediBatchLinkEntryDto)shihabItem.Tag);
                }
            }
        }
    }

    public class ShihabSiteGrabberForm : Form
    {
        public List<string> ShihabSelectedGrabbedUrls { get; private set; }
        public bool MehediStartImmediately { get; private set; }
        private CancellationTokenSource _shihabCrawlCts;

        public ShihabSiteGrabberForm(bool shihabDark)
        {
            ShihabSelectedGrabbedUrls = new List<string>();
            Text = "Site Grabber — " + ShihabThemePalette.ShihabMainHeaderTitle;
            Size = new Size(760, 500);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ShihabThemePalette.GetMehediWindowBg(shihabDark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(shihabDark);
            Font = new Font("Segoe UI", 9f);
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, shihabDark);

            Label mehediStartLbl = new Label { Text = "Start page URL:", Location = new Point(14, 18), AutoSize = true };
            TextBox shihabUrlBox = new TextBox { Location = new Point(120, 15), Width = 445, Text = "https://", BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark), ForeColor = ForeColor };

            Label mehediDepthLbl = new Label { Text = "Depth:", Location = new Point(575, 18), AutoSize = true };
            NumericUpDown shihabDepthNum = new NumericUpDown { Location = new Point(622, 15), Width = 55, Minimum = 1, Maximum = 3, Value = 1 };

            Label mehediTplLbl = new Label { Text = "Filter Template:", Location = new Point(14, 52), AutoSize = true };
            ComboBox shihabTplCombo = new ComboBox { Location = new Point(120, 49), Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
            shihabTplCombo.Items.AddRange(new object[]
            {
                "All Files (except HTML)",
                "Video Files (.mp4/.mkv/.webm/.m3u8)",
                "Audio Files (.mp3/.flac/.wav/.m4a)",
                "Compressed & Archives (.zip/.rar/.7z/.tar.gz)",
                "Documents & PDF (.pdf/.docx/.xlsx/.pptx)",
                "All Pictures (.jpg/.png/.webp/.gif)"
            });
            shihabTplCombo.SelectedIndex = 0;

            CheckBox mehediSameHostChk = new CheckBox { Text = "Explore within start domain only", Location = new Point(385, 51), AutoSize = true, Checked = true };

            Button shihabExploreBtn = ShihabThemePalette.CreateMehediStyledButton("Start Explore", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 120);
            shihabExploreBtn.Location = new Point(608, 46);

            Label mehediStatsLbl = new Label { Text = "Status: Ready — enter a URL and click Start Explore.", Location = new Point(14, 86), AutoSize = true, ForeColor = ShihabThemePalette.GetMehediAccentGreen() };

            ShihabModernListView shihabFoundList = new ShihabModernListView
            {
                Location = new Point(14, 112),
                Size = new Size(714, 290),
                BorderStyle = BorderStyle.FixedSingle,
                CheckBoxes = true,
                MehediDarkMode = shihabDark,
                ShihabShowFileTypeBadges = true,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark),
                ForeColor = ForeColor
            };
            shihabFoundList.Columns.Add("Discovered File URL", 560);
            shihabFoundList.Columns.Add("Extension", 120);

            shihabExploreBtn.Click += delegate
            {
                if (_shihabCrawlCts != null)
                {
                    _shihabCrawlCts.Cancel();
                    _shihabCrawlCts = null;
                    shihabExploreBtn.Text = "Start Explore";
                    return;
                }
                Uri mehediRoot;
                if (!Uri.TryCreate(shihabUrlBox.Text.Trim(), UriKind.Absolute, out mehediRoot))
                {
                    MessageBox.Show(this, "Please enter a valid http:// or https:// start URL.", "Site Grabber", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                shihabFoundList.Items.Clear();
                _shihabCrawlCts = new CancellationTokenSource();
                CancellationToken shihabTok = _shihabCrawlCts.Token;
                int mehediMaxDepth = (int)shihabDepthNum.Value;
                int shihabFilterIdx = shihabTplCombo.SelectedIndex;
                bool mehediSameDomain = mehediSameHostChk.Checked;
                shihabExploreBtn.Text = "Stop Explore";

                Task.Run(delegate
                {
                    RunMehediGrabberSpider(mehediRoot, mehediMaxDepth, shihabFilterIdx, mehediSameDomain, shihabFoundList, mehediStatsLbl, shihabExploreBtn, shihabTok);
                });
            };

            Button mehediAddQueueBtn = ShihabThemePalette.CreateMehediStyledButton("Add Checked to Main List", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 185);
            mehediAddQueueBtn.Location = new Point(285, 414);
            mehediAddQueueBtn.Click += delegate
            {
                CollectGrabbedShihabSelection(shihabFoundList);
                if (ShihabSelectedGrabbedUrls.Count == 0) return;
                MehediStartImmediately = false;
                DialogResult = DialogResult.OK;
                Close();
            };

            Button shihabDownloadCheckedBtn = ShihabThemePalette.CreateMehediStyledButton("Download Checked Now", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 175);
            shihabDownloadCheckedBtn.Location = new Point(478, 414);
            shihabDownloadCheckedBtn.Click += delegate
            {
                CollectGrabbedShihabSelection(shihabFoundList);
                if (ShihabSelectedGrabbedUrls.Count == 0) return;
                MehediStartImmediately = true;
                DialogResult = DialogResult.OK;
                Close();
            };

            Button mehediClose = ShihabThemePalette.CreateMehediStyledButton("Close", ShihabThemePalette.GetShihabSurfaceBg(shihabDark), ForeColor, 70);
            mehediClose.Location = new Point(660, 414);
            mehediClose.Click += delegate { Close(); };

            FormClosing += delegate { if (_shihabCrawlCts != null) _shihabCrawlCts.Cancel(); };

            Controls.AddRange(new Control[]
            {
                mehediStartLbl, shihabUrlBox, mehediDepthLbl, shihabDepthNum,
                mehediTplLbl, shihabTplCombo, mehediSameHostChk, shihabExploreBtn,
                mehediStatsLbl, shihabFoundList, mehediAddQueueBtn, shihabDownloadCheckedBtn, mehediClose
            });
        }

        private void CollectGrabbedShihabSelection(ListView mehediList)
        {
            ShihabSelectedGrabbedUrls.Clear();
            foreach (ListViewItem shihabIt in mehediList.Items)
            {
                if (shihabIt.Checked) ShihabSelectedGrabbedUrls.Add(shihabIt.SubItems[0].Text);
            }
        }

        private void RunMehediGrabberSpider(
            Uri shihabStartUri,
            int mehediMaxDepth,
            int shihabTemplateIdx,
            bool mehediSameHostOnly,
            ListView shihabView,
            Label mehediStats,
            Button shihabBtn,
            CancellationToken mehediToken)
        {
            HashSet<string> shihabVisitedPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> mehediDiscoveredFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Queue<KeyValuePair<Uri, int>> shihabFrontier = new Queue<KeyValuePair<Uri, int>>();
            shihabFrontier.Enqueue(new KeyValuePair<Uri, int>(shihabStartUri, 0));

            int mehediPagesProcessed = 0;

            while (shihabFrontier.Count > 0 && !mehediToken.IsCancellationRequested && mehediPagesProcessed < 35)
            {
                KeyValuePair<Uri, int> shihabNode = shihabFrontier.Dequeue();
                if (!shihabVisitedPages.Add(shihabNode.Key.AbsoluteUri)) continue;

                string mehediHtml = "";
                try
                {
                    HttpWebRequest shihabReq = (HttpWebRequest)WebRequest.Create(shihabNode.Key);
                    shihabReq.UserAgent = "Mozilla/5.0 ShihabMehediIDM-Grabber/1.0.4";
                    shihabReq.Timeout = 10000;
                    using (HttpWebResponse mehediResp = (HttpWebResponse)shihabReq.GetResponse())
                    using (StreamReader shihabSr = new StreamReader(mehediResp.GetResponseStream(), Encoding.UTF8))
                    {
                        mehediHtml = shihabSr.ReadToEnd();
                    }
                    mehediPagesProcessed++;
                }
                catch
                {
                    continue;
                }

                MatchCollection shihabMatches = Regex.Matches(
                    mehediHtml,
                    @"(?:href|src)\s*=\s*[""']([^""'#\s]+)[""']",
                    RegexOptions.IgnoreCase);

                for (int mehediM = 0; mehediM < shihabMatches.Count; mehediM++)
                {
                    Uri shihabCandidate;
                    if (!Uri.TryCreate(shihabNode.Key, shihabMatches[mehediM].Groups[1].Value, out shihabCandidate)) continue;
                    if (shihabCandidate.Scheme != "http" && shihabCandidate.Scheme != "https") continue;
                    if (mehediSameHostOnly && !string.Equals(shihabCandidate.Host, shihabStartUri.Host, StringComparison.OrdinalIgnoreCase)) continue;

                    string mehediExt = Path.GetExtension(shihabCandidate.AbsolutePath).TrimStart('.').ToLowerInvariant();
                    bool shihabLooksPage = string.IsNullOrEmpty(mehediExt) || mehediExt == "html" || mehediExt == "htm" || mehediExt == "php" || mehediExt == "asp";

                    if (shihabLooksPage && shihabNode.Value + 1 < mehediMaxDepth)
                    {
                        shihabFrontier.Enqueue(new KeyValuePair<Uri, int>(shihabCandidate, shihabNode.Value + 1));
                    }
                    else if (MatchesGrabberShihabFilter(mehediExt, shihabTemplateIdx))
                    {
                        if (mehediDiscoveredFiles.Add(shihabCandidate.AbsoluteUri) && !IsDisposed)
                        {
                            string shihabFoundUrl = shihabCandidate.AbsoluteUri;
                            string mehediUpperExt = string.IsNullOrEmpty(mehediExt) ? "BIN" : mehediExt.ToUpperInvariant();
                            try
                            {
                                BeginInvoke((Action)delegate
                                {
                                    if (IsDisposed) return;
                                    ListViewItem shihabItem = new ListViewItem(new string[] { shihabFoundUrl, mehediUpperExt });
                                    shihabItem.Checked = true;
                                    shihabView.Items.Add(shihabItem);
                                });
                            }
                            catch { }
                        }
                    }
                }

                try
                {
                    int shihabPg = mehediPagesProcessed;
                    int mehediFoundCnt = mehediDiscoveredFiles.Count;
                    BeginInvoke((Action)delegate
                    {
                        if (!IsDisposed)
                        {
                            mehediStats.Text = string.Format("Exploring... Web pages processed: {0}  |  Files Found: {1}", shihabPg, mehediFoundCnt);
                        }
                    });
                }
                catch { }
            }

            try
            {
                BeginInvoke((Action)delegate
                {
                    if (!IsDisposed)
                    {
                        _shihabCrawlCts = null;
                        shihabBtn.Text = "Start Explore";
                        mehediStats.Text = string.Format("Complete — Web pages processed: {0}  |  Matched Files Found: {1}", mehediPagesProcessed, mehediDiscoveredFiles.Count);
                    }
                });
            }
            catch { }
        }

        private static bool MatchesGrabberShihabFilter(string mehediExt, int shihabIdx)
        {
            if (string.IsNullOrEmpty(mehediExt)) return false;
            if (shihabIdx == 1) return " mp4 mkv webm avi mov flv m3u8 ts ".Contains(" " + mehediExt + " ");
            if (shihabIdx == 2) return " mp3 flac wav aac ogg m4a opus ".Contains(" " + mehediExt + " ");
            if (shihabIdx == 3) return " zip rar 7z gz bz2 xz tar tgz ".Contains(" " + mehediExt + " ");
            if (shihabIdx == 4) return " pdf doc docx xls xlsx ppt pptx epub txt csv ".Contains(" " + mehediExt + " ");
            if (shihabIdx == 5) return " jpg jpeg png webp gif svg bmp ".Contains(" " + mehediExt + " ");
            return mehediExt != "html" && mehediExt != "htm" && mehediExt != "css" && mehediExt != "js";
        }
    }

    public class MehediFloatingDropTargetForm : Form
    {
        private readonly Action<string> _shihabOnUrlDropped;
        private readonly Action<int, int> _shihabOnMoved;
        private Point _mehediDragStart;
        private bool _shihabDragging;
        private string _mehediSpeedLabel = "Drop URL";

        public MehediFloatingDropTargetForm(
            Action<string> shihabOnUrlDropped,
            int initialX = -1,
            int initialY = -1,
            Action<int, int> onMoved = null)
        {
            _shihabOnUrlDropped = shihabOnUrlDropped;
            _shihabOnMoved = onMoved;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Rectangle mehediScreen = Screen.PrimaryScreen.WorkingArea;
            int posX = initialX >= 0 ? Math.Min(Math.Max(0, initialX), mehediScreen.Right - 88) : (mehediScreen.Right - 108);
            int posY = initialY >= 0 ? Math.Min(Math.Max(0, initialY), mehediScreen.Bottom - 88) : (mehediScreen.Bottom - 108);
            Location = new Point(posX, posY);
            Size = new Size(88, 88);
            TopMost = true;
            ShowInTaskbar = false;
            AllowDrop = true;
            Opacity = 0.90;
            BackColor = Color.FromArgb(15, 23, 42);
            DoubleBuffered = true;

            DragEnter += delegate(object shihabS, DragEventArgs mehediE)
            {
                if (mehediE.Data.GetDataPresent(DataFormats.FileDrop) ||
                    mehediE.Data.GetDataPresent(DataFormats.UnicodeText) ||
                    mehediE.Data.GetDataPresent(DataFormats.Text))
                {
                    mehediE.Effect = DragDropEffects.Copy;
                }
            };
            DragDrop += delegate(object shihabS, DragEventArgs mehediE)
            {
                if (_shihabOnUrlDropped == null) return;
                if (mehediE.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = mehediE.Data.GetData(DataFormats.FileDrop) as string[];
                    if (files != null)
                    {
                        for (int fi = 0; fi < files.Length; fi++)
                        {
                            if (!string.IsNullOrWhiteSpace(files[fi])) _shihabOnUrlDropped(files[fi]);
                        }
                    }
                    return;
                }
                string shihabDropped = (mehediE.Data.GetData(DataFormats.UnicodeText) ?? mehediE.Data.GetData(DataFormats.Text) ?? "").ToString().Trim();
                if (!string.IsNullOrEmpty(shihabDropped))
                {
                    _shihabOnUrlDropped(shihabDropped);
                }
            };

            MouseDown += delegate(object shihabS, MouseEventArgs mehediE)
            {
                if (mehediE.Button == MouseButtons.Left)
                {
                    _shihabDragging = true;
                    _mehediDragStart = mehediE.Location;
                }
            };
            MouseMove += delegate(object shihabS, MouseEventArgs mehediE)
            {
                if (_shihabDragging)
                {
                    Location = new Point(Left + mehediE.X - _mehediDragStart.X, Top + mehediE.Y - _mehediDragStart.Y);
                }
            };
            MouseUp += delegate
            {
                if (_shihabDragging)
                {
                    _shihabDragging = false;
                    if (_shihabOnMoved != null) _shihabOnMoved(Left, Top);
                }
            };

            ContextMenuStrip shihabCtx = new ContextMenuStrip();
            shihabCtx.Renderer = new MehediDarkToolStripRenderer(true, false);
            shihabCtx.Items.Add("Hide Drop Target", null, delegate { Hide(); });
            ContextMenuStrip = shihabCtx;
        }

        public void UpdateShihabLiveSpeed(double mehediTotalBps)
        {
            string shihabNext = mehediTotalBps > 5.0 ? ShihabFormatHelpers.FormatShihabSpeed(mehediTotalBps) : "Drop URL";
            if (shihabNext != _mehediSpeedLabel)
            {
                _mehediSpeedLabel = shihabNext;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs shihabE)
        {
            Graphics mehediG = shihabE.Graphics;
            mehediG.SmoothingMode = SmoothingMode.AntiAlias;
            using (LinearGradientBrush shihabBg = new LinearGradientBrush(ClientRectangle, Color.FromArgb(18, 19, 25), Color.FromArgb(26, 29, 44), 45f))
            {
                mehediG.FillRectangle(shihabBg, ClientRectangle);
            }
            using (Pen mehediBorder = new Pen(Color.FromArgb(124, 92, 255), 2f))
            {
                mehediG.DrawRectangle(mehediBorder, 1, 1, Width - 2, Height - 2);
            }
            using (Bitmap shihabLogo = ShihabThemePalette.CreateMehediLogoBitmap(34))
            using (Font shihabTitleFont = new Font("Segoe UI", 7.8f, FontStyle.Bold))
            using (Font mehediSubFont = new Font("Segoe UI Semibold", 7.3f))
            using (SolidBrush shihabBrush = new SolidBrush(Color.FromArgb(230, 234, 245)))
            using (SolidBrush mehediViolet = new SolidBrush(Color.FromArgb(196, 181, 253)))
            {
                mehediG.DrawImage(shihabLogo, (Width - 34) / 2, 7, 34, 34);
                mehediG.DrawString("MHS DM", shihabTitleFont, mehediViolet, 19, 44);
                mehediG.DrawString(_mehediSpeedLabel, mehediSubFont, shihabBrush, 14, 62);
            }
        }
    }

    // =========================================================================
    // 1. BITTORRENT / MAGNET ADD & CREATE .TORRENT DIALOG
    // =========================================================================
    public class ShihabTorrentAddDialog : Form
    {
        public ShihabDownloadItem ConfiguredTorrentItem { get; private set; }
        public bool StartImmediately { get; private set; }

        public ShihabTorrentAddDialog(MehediAppConfiguration config, string initialMagnetOrPath)
        {
            bool dark = config.ShihabDarkMode;
            Text = "BitTorrent / Magnet & .torrent Manager — " + ShihabThemePalette.ShihabMainHeaderTitle;
            Size = new Size(720, 510);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ShihabThemePalette.GetMehediWindowBg(dark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(dark);
            Font = new Font("Segoe UI", 9f);
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, dark);

            Color surf = ShihabThemePalette.GetShihabSurfaceBg(dark);

            Label srcLbl = new Label { Text = "Magnet URI or .torrent File:", Location = new Point(16, 18), AutoSize = true };
            TextBox srcBox = new TextBox { Location = new Point(185, 15), Width = 400, Text = initialMagnetOrPath ?? "", BackColor = surf, ForeColor = ForeColor };
            Button browseTorBtn = ShihabThemePalette.CreateMehediStyledButton("Browse .torrent", surf, ForeColor, 108);
            browseTorBtn.Height = 25;
            browseTorBtn.Location = new Point(592, 13);

            Label infoLbl = new Label
            {
                Text = "Info-Hash: (paste a magnet:? link or browse a .torrent file to inspect)",
                Location = new Point(16, 48),
                Size = new Size(680, 22),
                ForeColor = ShihabThemePalette.GetShihabAccentBlue(),
                Font = new Font("Segoe UI Semibold", 9f)
            };

            ShihabModernListView filesView = new ShihabModernListView
            {
                Location = new Point(16, 76),
                Size = new Size(680, 235),
                BorderStyle = BorderStyle.FixedSingle,
                CheckBoxes = true,
                MehediDarkMode = dark,
                ShihabShowFileTypeBadges = true,
                BackColor = surf,
                ForeColor = ForeColor
            };
            filesView.Columns.Add("#", 45);
            filesView.Columns.Add("File Path inside Torrent", 460);
            filesView.Columns.Add("Size", 140);

            ShihabTorrentMetadata currentMeta = null;

            Action parseCurrentSource = delegate
            {
                string raw = srcBox.Text.Trim();
                filesView.Items.Clear();
                if (string.IsNullOrEmpty(raw))
                {
                    currentMeta = null;
                    infoLbl.Text = "Info-Hash: (none)";
                    return;
                }
                if (raw.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
                {
                    currentMeta = ShihabTorrentManager.ParseMagnetUri(raw);
                }
                else if (File.Exists(raw))
                {
                    currentMeta = ShihabTorrentManager.InspectTorrentFile(raw);
                }
                else
                {
                    currentMeta = null;
                }

                if (currentMeta != null && currentMeta.ParsedSuccessfully)
                {
                    infoLbl.Text = string.Format("Name: {0}   |   Info-Hash: {1}   |   Total: {2}   |   Trackers: {3}   |   WebSeeds: {4}",
                        currentMeta.Name,
                        string.IsNullOrEmpty(currentMeta.InfoHashHex) ? "magnet" : currentMeta.InfoHashHex,
                        ShihabFormatHelpers.FormatMehediBytes(currentMeta.TotalSize),
                        currentMeta.Trackers.Count,
                        currentMeta.WebSeeds.Count);

                    for (int i = 0; i < currentMeta.Files.Count; i++)
                    {
                        ShihabTorrentFileEntry fe = currentMeta.Files[i];
                        ListViewItem row = new ListViewItem(new string[]
                        {
                            fe.Index.ToString(),
                            fe.RelativePath,
                            ShihabFormatHelpers.FormatMehediBytes(fe.Length)
                        });
                        row.Checked = true;
                        row.Tag = fe;
                        filesView.Items.Add(row);
                    }
                }
                else if (currentMeta != null)
                {
                    infoLbl.Text = "Error: " + currentMeta.ErrorMessage;
                }
            };

            browseTorBtn.Click += delegate
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "BitTorrent Files (*.torrent)|*.torrent|All Files (*.*)|*.*" })
                {
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        srcBox.Text = ofd.FileName;
                        parseCurrentSource();
                    }
                }
            };
            srcBox.TextChanged += delegate { parseCurrentSource(); };

            string defaultTorDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Torrents");
            Label saveLbl = new Label { Text = "Save Directory:", Location = new Point(16, 324), AutoSize = true };
            TextBox saveBox = new TextBox { Location = new Point(125, 321), Width = 475, Text = defaultTorDir, BackColor = surf, ForeColor = ForeColor };
            Button browseDirBtn = ShihabThemePalette.CreateMehediStyledButton("Browse", surf, ForeColor, 90);
            browseDirBtn.Height = 25;
            browseDirBtn.Location = new Point(606, 319);
            browseDirBtn.Click += delegate
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog { SelectedPath = saveBox.Text })
                {
                    if (fbd.ShowDialog(this) == DialogResult.OK) saveBox.Text = fbd.SelectedPath;
                }
            };

            CheckBox seqChk = new CheckBox { Text = "Sequential piece download (stream media)", Location = new Point(16, 354), AutoSize = true, Checked = config.ShihabTorrentSequentialDefault };
            CheckBox seedChk = new CheckBox { Text = "Keep seeding after completion", Location = new Point(292, 354), AutoSize = true, Checked = config.ShihabTorrentKeepSeedingDefault };
            CheckBox debridTorChk = new CheckBox { Text = "Accelerate via Debrid Cloud", Location = new Point(505, 354), AutoSize = true, Checked = false };

            Button createTorrentBtn = ShihabThemePalette.CreateMehediStyledButton("Create File .torrent...", surf, ForeColor, 145);
            createTorrentBtn.Location = new Point(16, 412);
            createTorrentBtn.Click += delegate
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Title = "Select Source File to Package into .torrent" })
                {
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        using (SaveFileDialog sfd = new SaveFileDialog { Filter = "Torrent File (*.torrent)|*.torrent", FileName = Path.GetFileName(ofd.FileName) + ".torrent" })
                        {
                            if (sfd.ShowDialog(this) == DialogResult.OK)
                            {
                                string infoHash;
                                string err;
                                List<string> defaultTrackers = new List<string> { "udp://tracker.opentrackr.org:1337/announce", "https://tracker.tamersunion.org:443/announce" };
                                if (ShihabTorrentManager.CreateTorrentFile(ofd.FileName, sfd.FileName, defaultTrackers, new List<string>(), false, 262144, out infoHash, out err))
                                {
                                    srcBox.Text = sfd.FileName;
                                    parseCurrentSource();
                                    MessageBox.Show(this, "Created .torrent file!\r\nInfo-Hash (SHA-1): " + infoHash, "Torrent Creator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                                else
                                {
                                    MessageBox.Show(this, "Failed to create .torrent:\r\n" + err, "Torrent Creator", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                        }
                    }
                }
            };

            Button createFolderTorBtn = ShihabThemePalette.CreateMehediStyledButton("Folder .torrent...", surf, ForeColor, 130);
            createFolderTorBtn.Location = new Point(167, 412);
            createFolderTorBtn.Click += delegate
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog { Description = "Select Folder to Package into Multi-File .torrent" })
                {
                    if (fbd.ShowDialog(this) == DialogResult.OK && Directory.Exists(fbd.SelectedPath))
                    {
                        using (SaveFileDialog sfd = new SaveFileDialog { Filter = "Torrent File (*.torrent)|*.torrent", FileName = Path.GetFileName(fbd.SelectedPath.TrimEnd('\\', '/')) + ".torrent" })
                        {
                            if (sfd.ShowDialog(this) == DialogResult.OK)
                            {
                                string infoHash;
                                string err;
                                List<string> defaultTrackers = new List<string> { "udp://tracker.opentrackr.org:1337/announce", "https://tracker.tamersunion.org:443/announce" };
                                if (ShihabTorrentManager.CreateTorrentFile(fbd.SelectedPath, sfd.FileName, defaultTrackers, new List<string>(), false, 262144, out infoHash, out err))
                                {
                                    srcBox.Text = sfd.FileName;
                                    parseCurrentSource();
                                    MessageBox.Show(this, "Created multi-file folder .torrent!\r\nInfo-Hash (SHA-1): " + infoHash, "Torrent Creator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                                else
                                {
                                    MessageBox.Show(this, "Failed to create folder .torrent:\r\n" + err, "Torrent Creator", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                        }
                    }
                }
            };

            Action<bool> commitAndClose = delegate(bool startNow)
            {
                string raw = srcBox.Text.Trim();
                if (string.IsNullOrEmpty(raw))
                {
                    MessageBox.Show(this, "Please enter a Magnet URI or select a .torrent file.", "BitTorrent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (currentMeta == null) parseCurrentSource();
                string itemName = (currentMeta != null && !string.IsNullOrEmpty(currentMeta.Name))
                    ? ShihabFormatHelpers.SanitizeShihabFileName(currentMeta.Name)
                    : "Torrent_Download";

                List<int> selectedIdx = new List<int>();
                for (int i = 0; i < filesView.Items.Count; i++)
                {
                    if (filesView.Items[i].Checked) selectedIdx.Add(i);
                }

                ConfiguredTorrentItem = new ShihabDownloadItem
                {
                    MehediUrl = raw,
                    ShihabFileName = itemName,
                    MehediSaveDirectory = saveBox.Text.Trim(),
                    ShihabCategoryName = "Torrents",
                    ShihabTotalBytes = currentMeta != null ? currentMeta.TotalSize : -1,
                    ShihabIsTorrent = true,
                    ShihabTorrentMagnetOrPath = raw,
                    ShihabTorrentSequential = seqChk.Checked,
                    ShihabTorrentKeepSeeding = seedChk.Checked,
                    ShihabUseDebridForTorrent = debridTorChk.Checked,
                    ShihabSelectedTorrentFiles = selectedIdx,
                    ShihabDescription = currentMeta != null && !string.IsNullOrEmpty(currentMeta.InfoHashHex)
                        ? ("BitTorrent InfoHash: " + currentMeta.InfoHashHex)
                        : "BitTorrent / Magnet Transfer"
                };
                StartImmediately = startNow;
                DialogResult = DialogResult.OK;
                Close();
            };

            Button addQueueBtn = ShihabThemePalette.CreateMehediStyledButton("Add to Queue", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 130);
            addQueueBtn.Location = new Point(315, 412);
            addQueueBtn.Click += delegate { commitAndClose(false); };

            Button startNowBtn = ShihabThemePalette.CreateMehediStyledButton("Download Now", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 145);
            startNowBtn.Location = new Point(453, 412);
            startNowBtn.Click += delegate { commitAndClose(true); };

            Button cancelBtn = ShihabThemePalette.CreateMehediStyledButton("Cancel", surf, ForeColor, 90);
            cancelBtn.Location = new Point(606, 412);
            cancelBtn.Click += delegate { Close(); };

            Controls.AddRange(new Control[]
            {
                srcLbl, srcBox, browseTorBtn, infoLbl, filesView,
                saveLbl, saveBox, browseDirBtn, seqChk, seedChk, debridTorChk,
                createTorrentBtn, createFolderTorBtn, addQueueBtn, startNowBtn, cancelBtn
            });

            if (!string.IsNullOrEmpty(initialMagnetOrPath)) parseCurrentSource();
        }
    }

    // =========================================================================
    // 2. VIDEO / PLAYLIST, GALLERY EXTRACTOR & MANAGED MEDIA HELPERS DIALOG
    // =========================================================================
    public class ShihabMediaAndGalleryDialog : Form
    {
        public List<string> DiscoveredDownloadUrls { get; private set; }
        public List<ShihabDownloadItem> ConfiguredMediaItems { get; private set; }
        public string SelectedSubtitlesLang { get; private set; }
        public bool EmbedSubtitles { get; private set; }
        public string AudioExtractFormat { get; private set; }

        private sealed class MediaRowPayload
        {
            public string Url;
            public string Title;
            public string FormatSelector;
            public bool IsYtDlpJob;
            public bool IsGalleryJob;
            public string CompanionAudioUrl;
            public long SizeBytes;
        }

        public ShihabMediaAndGalleryDialog(MehediAppConfiguration config, ShihabStateStore store)
        {
            DiscoveredDownloadUrls = new List<string>();
            ConfiguredMediaItems = new List<ShihabDownloadItem>();
            bool dark = config.ShihabDarkMode;
            Color surf = ShihabThemePalette.GetShihabSurfaceBg(dark);

            Text = "Video / Playlist, Image Gallery & Media Helpers — " + ShihabThemePalette.ShihabMainHeaderTitle;
            Size = new Size(760, 520);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ShihabThemePalette.GetMehediWindowBg(dark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(dark);
            Font = new Font("Segoe UI", 9f);
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, dark);

            ShihabDarkTabControl tabs = new ShihabDarkTabControl
            {
                ShihabDarkMode = dark,
                Location = new Point(12, 12),
                Size = new Size(720, 415)
            };

            TabPage ytTab = new TabPage("Video / Playlist & Subtitles") { BackColor = BackColor, ForeColor = ForeColor };
            TabPage galTab = new TabPage("Image Gallery Extractor") { BackColor = BackColor, ForeColor = ForeColor };
            TabPage helpersTab = new TabPage("Managed Helpers (yt-dlp / ffmpeg / gallery-dl)") { BackColor = BackColor, ForeColor = ForeColor };

            // --- Tab 1: Video / Playlist & Subtitles ---
            Label ytUrlLbl = new Label { Text = "Video or Playlist URL:", Location = new Point(16, 18), AutoSize = true };
            TextBox ytUrlBox = new TextBox { Location = new Point(155, 15), Width = 420, Text = "https://www.youtube.com/watch?v=", BackColor = surf, ForeColor = ForeColor };
            Button ytFetchBtn = ShihabThemePalette.CreateMehediStyledButton("Fetch Info", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 110);
            ytFetchBtn.Height = 26;
            ytFetchBtn.Location = new Point(584, 13);

            Label subLbl = new Label { Text = "Subtitles (lang):", Location = new Point(16, 52), AutoSize = true };
            TextBox subLangBox = new TextBox { Location = new Point(120, 49), Width = 90, Text = "en,bn", BackColor = surf, ForeColor = ForeColor };
            CheckBox embedSubChk = new CheckBox { Text = "Embed subtitles in MP4/MKV", Location = new Point(225, 51), AutoSize = true, Checked = true };
            Label audFmtLbl = new Label { Text = "Audio Extract:", Location = new Point(435, 52), AutoSize = true };
            ComboBox audFmtCombo = new ComboBox { Location = new Point(528, 49), Width = 165, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = surf, ForeColor = ForeColor };
            audFmtCombo.Items.AddRange(new object[] { "Keep Video+Audio (MP4)", "mp3", "m4a", "opus", "wav" });
            audFmtCombo.SelectedIndex = 0;

            ShihabModernListView ytResultsList = new ShihabModernListView
            {
                Location = new Point(16, 84),
                Size = new Size(678, 280),
                BorderStyle = BorderStyle.FixedSingle,
                CheckBoxes = true,
                MehediDarkMode = dark,
                ShihabShowFileTypeBadges = true,
                BackColor = surf,
                ForeColor = ForeColor
            };
            ytResultsList.Columns.Add("Stream / Playlist Item Title", 350);
            ytResultsList.Columns.Add("Format / Quality", 160);
            ytResultsList.Columns.Add("Size", 130);

            ytFetchBtn.Click += delegate
            {
                string rawUrl = ytUrlBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(rawUrl)) return;
                ytResultsList.Items.Clear();
                ytFetchBtn.Enabled = false;
                ytFetchBtn.Text = "Probing...";

                Task.Run(delegate
                {
                    string mediaTitle;
                    List<ShihabMediaFormatEntry> ytdlpFormats;
                    List<ShihabPlaylistEntryInfo> playlistItems;
                    string probeErr;
                    bool ytdlpOk = ShihabMediaHelpersManager.QueryVideoOrPlaylistInfo(
                        rawUrl,
                        store.MehediBaseDirectory,
                        config,
                        out mediaTitle,
                        out ytdlpFormats,
                        out playlistItems,
                        out probeErr);

                    ShihabYouTubeStreamsReplyDto innerTubeReply = null;
                    if (!ytdlpOk)
                    {
                        Match m = Regex.Match(rawUrl, @"(?:v=|youtu\.be/|shorts/)([A-Za-z0-9_-]{11})");
                        if (m.Success)
                        {
                            innerTubeReply = MehediYouTubeInnerTubeResolver.ResolveShihabYouTubeStreams(m.Groups[1].Value, "", false);
                        }
                    }

                    try
                    {
                        BeginInvoke((Action)delegate
                        {
                            if (IsDisposed) return;
                            ytFetchBtn.Enabled = true;
                            ytFetchBtn.Text = "Fetch Info";

                            if (ytdlpOk && playlistItems != null && playlistItems.Count > 1)
                            {
                                for (int pi = 0; pi < playlistItems.Count; pi++)
                                {
                                    ShihabPlaylistEntryInfo pe = playlistItems[pi];
                                    ListViewItem row = new ListViewItem(new string[]
                                    {
                                        string.Format("#{0} {1}", pe.Index, pe.Title),
                                        "Playlist Item (yt-dlp bestvideo+bestaudio)",
                                        string.IsNullOrEmpty(pe.DurationText) ? "Auto" : pe.DurationText
                                    });
                                    row.Checked = pe.Selected;
                                    row.Tag = new MediaRowPayload
                                    {
                                        Url = pe.Url,
                                        Title = ShihabFormatHelpers.SanitizeShihabFileName(pe.Title) + ".mp4",
                                        FormatSelector = "bv*+ba/b",
                                        IsYtDlpJob = true
                                    };
                                    ytResultsList.Items.Add(row);
                                }
                                return;
                            }

                            if (ytdlpOk && ytdlpFormats != null && ytdlpFormats.Count > 0)
                            {
                                string baseTitle = string.IsNullOrWhiteSpace(mediaTitle) ? "Media_Stream" : ShihabFormatHelpers.SanitizeShihabFileName(mediaTitle);
                                ListViewItem bestRow = new ListViewItem(new string[]
                                {
                                    baseTitle + " [Best Video + Audio Auto-Mux]",
                                    "bv*+ba/b (MP4/MKV)",
                                    "Auto"
                                });
                                bestRow.Checked = true;
                                bestRow.Tag = new MediaRowPayload
                                {
                                    Url = rawUrl,
                                    Title = baseTitle + ".mp4",
                                    FormatSelector = "bv*+ba/b",
                                    IsYtDlpJob = true
                                };
                                ytResultsList.Items.Add(bestRow);

                                for (int fi = ytdlpFormats.Count - 1; fi >= 0 && ytResultsList.Items.Count < 25; fi--)
                                {
                                    ShihabMediaFormatEntry fmt = ytdlpFormats[fi];
                                    string sel = fmt.HasVideo && !fmt.HasAudio ? (fmt.FormatId + "+bestaudio/" + fmt.FormatId) : fmt.FormatId;
                                    ListViewItem row = new ListViewItem(new string[]
                                    {
                                        baseTitle + " [" + fmt.ResolutionLabel + "]",
                                        fmt.Extension + " (" + sel + ")",
                                        ShihabFormatHelpers.FormatMehediBytes(fmt.FileSizeBytes)
                                    });
                                    row.Checked = false;
                                    row.Tag = new MediaRowPayload
                                    {
                                        Url = rawUrl,
                                        Title = baseTitle + "_" + fmt.FormatId + "." + fmt.Extension,
                                        FormatSelector = sel,
                                        IsYtDlpJob = true,
                                        SizeBytes = fmt.FileSizeBytes
                                    };
                                    ytResultsList.Items.Add(row);
                                }
                                return;
                            }

                            if (innerTubeReply != null && innerTubeReply.ShihabOk && innerTubeReply.MehediStreams != null && innerTubeReply.MehediStreams.Count > 0)
                            {
                                string ytTitle = ShihabFormatHelpers.SanitizeShihabFileName(innerTubeReply.ShihabTitle ?? "YouTube_Video");
                                for (int i = 0; i < innerTubeReply.MehediStreams.Count; i++)
                                {
                                    MehediYouTubeResolvedStreamDto fmt = innerTubeReply.MehediStreams[i];
                                    ListViewItem row = new ListViewItem(new string[]
                                    {
                                        ytTitle + " [" + fmt.ShihabResolution + "]",
                                        fmt.ShihabExt + (!string.IsNullOrEmpty(fmt.MehediAudioCompanionUrl) ? " (DASH Mux)" : ""),
                                        ShihabFormatHelpers.FormatMehediBytes(fmt.ShihabSizeBytes)
                                    });
                                    row.Checked = (i == 0);
                                    row.Tag = new MediaRowPayload
                                    {
                                        Url = fmt.ShihabUrl,
                                        Title = ytTitle + "_" + fmt.ShihabResolution + "." + (string.IsNullOrEmpty(fmt.ShihabExt) ? "mp4" : fmt.ShihabExt),
                                        CompanionAudioUrl = fmt.MehediAudioCompanionUrl ?? "",
                                        SizeBytes = fmt.ShihabSizeBytes,
                                        IsYtDlpJob = false
                                    };
                                    ytResultsList.Items.Add(row);
                                }
                                return;
                            }

                            bool hasYtdlpInstalled = File.Exists(ShihabMediaHelpersManager.GetHelperExecutablePath(store.MehediBaseDirectory, "yt-dlp"));
                            ListViewItem directRow = new ListViewItem(new string[]
                            {
                                rawUrl,
                                hasYtdlpInstalled ? "yt-dlp Managed Extraction" : "Direct HTTP / HLS Stream",
                                "Auto"
                            });
                            directRow.Checked = true;
                            directRow.Tag = new MediaRowPayload
                            {
                                Url = rawUrl,
                                Title = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(rawUrl),
                                FormatSelector = "bv*+ba/b",
                                IsYtDlpJob = hasYtdlpInstalled
                            };
                            ytResultsList.Items.Add(directRow);
                        });
                    }
                    catch { }
                });
            };

            ytTab.Controls.AddRange(new Control[] { ytUrlLbl, ytUrlBox, ytFetchBtn, subLbl, subLangBox, embedSubChk, audFmtLbl, audFmtCombo, ytResultsList });

            // --- Tab 2: Image Gallery Extractor ---
            Label galUrlLbl = new Label { Text = "Gallery Page URL:", Location = new Point(16, 18), AutoSize = true };
            TextBox galUrlBox = new TextBox { Location = new Point(135, 15), Width = 440, Text = "https://", BackColor = surf, ForeColor = ForeColor };
            Button galScanBtn = ShihabThemePalette.CreateMehediStyledButton("Extract Images", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 118);
            galScanBtn.Height = 26;
            galScanBtn.Location = new Point(582, 13);

            ShihabModernListView galList = new ShihabModernListView
            {
                Location = new Point(16, 52),
                Size = new Size(678, 312),
                BorderStyle = BorderStyle.FixedSingle,
                CheckBoxes = true,
                MehediDarkMode = dark,
                ShihabShowFileTypeBadges = true,
                BackColor = surf,
                ForeColor = ForeColor
            };
            galList.Columns.Add("High-Resolution Image / Gallery Job", 540);
            galList.Columns.Add("Type", 110);

            galScanBtn.Click += delegate
            {
                string gUrl = galUrlBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(gUrl) || gUrl == "https://") return;
                galList.Items.Clear();
                string gTitle;
                List<string> imgs = ShihabMediaHelpersManager.ExtractGalleryImageUrls(gUrl, store.MehediBaseDirectory, out gTitle);
                bool hasGalleryDl = File.Exists(ShihabMediaHelpersManager.GetHelperExecutablePath(store.MehediBaseDirectory, "gallery-dl"));
                if (hasGalleryDl)
                {
                    ListViewItem batchJobRow = new ListViewItem(new string[] { gUrl + " (Full Gallery Folder via gallery-dl)", "GALLERY-DL" });
                    batchJobRow.Checked = imgs.Count == 0;
                    batchJobRow.Tag = new MediaRowPayload
                    {
                        Url = gUrl,
                        Title = string.IsNullOrWhiteSpace(gTitle) ? "Gallery_Download" : gTitle,
                        IsGalleryJob = true
                    };
                    galList.Items.Add(batchJobRow);
                }
                for (int i = 0; i < imgs.Count; i++)
                {
                    string ext = Path.GetExtension(imgs[i]).TrimStart('.').ToUpperInvariant();
                    if (string.IsNullOrEmpty(ext)) ext = "IMG";
                    ListViewItem row = new ListViewItem(new string[] { imgs[i], ext });
                    row.Checked = true;
                    row.Tag = new MediaRowPayload
                    {
                        Url = imgs[i],
                        Title = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(imgs[i]),
                        IsGalleryJob = false
                    };
                    galList.Items.Add(row);
                }
            };
            galTab.Controls.AddRange(new Control[] { galUrlLbl, galUrlBox, galScanBtn, galList });

            // --- Tab 3: Managed Media Helpers ---
            Label hlpInfo = new Label
            {
                Text = "MHS Download Manager manages official standalone binaries in your AppData\\tools folder for 1000+ site video/playlist extraction (yt-dlp), stream muxing/audio conversion (ffmpeg), and multi-page image galleries (gallery-dl).",
                Location = new Point(16, 18),
                Size = new Size(675, 42)
            };

            ShihabModernListView hlpList = new ShihabModernListView
            {
                Location = new Point(16, 68),
                Size = new Size(678, 185),
                BorderStyle = BorderStyle.FixedSingle,
                MehediDarkMode = dark,
                ShihabShowFileTypeBadges = false,
                BackColor = surf,
                ForeColor = ForeColor
            };
            hlpList.Columns.Add("Helper Binary", 150);
            hlpList.Columns.Add("Status", 160);
            hlpList.Columns.Add("Local Path", 345);

            Action reloadHelpers = delegate
            {
                hlpList.Items.Clear();
                string[] names = new string[] { "yt-dlp", "ffmpeg", "gallery-dl" };
                for (int i = 0; i < names.Length; i++)
                {
                    string p = ShihabMediaHelpersManager.GetHelperExecutablePath(store.MehediBaseDirectory, names[i]);
                    bool exists = File.Exists(p);
                    hlpList.Items.Add(new ListViewItem(new string[]
                    {
                        names[i] + ".exe",
                        exists ? "Installed & Ready" : "Not Installed",
                        p
                    }));
                }
            };
            reloadHelpers();

            Label hlpProgLbl = new Label { Text = "Select a helper below to download/update from official GitHub Releases:", Location = new Point(16, 268), AutoSize = true };

            Button instYtdlpBtn = ShihabThemePalette.CreateMehediStyledButton("Install / Update yt-dlp", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 185);
            instYtdlpBtn.Location = new Point(16, 298);
            instYtdlpBtn.Click += delegate
            {
                ShihabMediaHelpersManager.InstallOrUpdateHelperAsync(store.MehediBaseDirectory, "yt-dlp", delegate(string st)
                {
                    try { BeginInvoke((Action)delegate { hlpProgLbl.Text = st; reloadHelpers(); }); } catch { }
                });
            };

            Button instGalBtn = ShihabThemePalette.CreateMehediStyledButton("Install / Update gallery-dl", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 195);
            instGalBtn.Location = new Point(212, 298);
            instGalBtn.Click += delegate
            {
                ShihabMediaHelpersManager.InstallOrUpdateHelperAsync(store.MehediBaseDirectory, "gallery-dl", delegate(string st)
                {
                    try { BeginInvoke((Action)delegate { hlpProgLbl.Text = st; reloadHelpers(); }); } catch { }
                });
            };

            Button instFfmpegBtn = ShihabThemePalette.CreateMehediStyledButton("Install / Update ffmpeg", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 185);
            instFfmpegBtn.Location = new Point(418, 298);
            instFfmpegBtn.Click += delegate
            {
                ShihabMediaHelpersManager.InstallOrUpdateHelperAsync(store.MehediBaseDirectory, "ffmpeg", delegate(string st)
                {
                    try { BeginInvoke((Action)delegate { hlpProgLbl.Text = st; reloadHelpers(); }); } catch { }
                });
            };

            helpersTab.Controls.AddRange(new Control[] { hlpInfo, hlpList, hlpProgLbl, instYtdlpBtn, instGalBtn, instFfmpegBtn });

            tabs.TabPages.AddRange(new TabPage[] { ytTab, galTab, helpersTab });

            Button dlCheckedBtn = ShihabThemePalette.CreateMehediStyledButton("Download Checked Items", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 190);
            dlCheckedBtn.Location = new Point(435, 438);
            dlCheckedBtn.Click += delegate
            {
                DiscoveredDownloadUrls.Clear();
                ConfiguredMediaItems.Clear();
                SelectedSubtitlesLang = subLangBox.Text.Trim();
                EmbedSubtitles = embedSubChk.Checked;
                AudioExtractFormat = audFmtCombo.SelectedIndex > 0 ? audFmtCombo.SelectedItem.ToString() : "";

                ListView activeList = tabs.SelectedIndex == 1 ? galList : ytResultsList;
                for (int i = 0; i < activeList.Items.Count; i++)
                {
                    if (!activeList.Items[i].Checked) continue;
                    MediaRowPayload payload = activeList.Items[i].Tag as MediaRowPayload;
                    if (payload != null && !string.IsNullOrWhiteSpace(payload.Url))
                    {
                        DiscoveredDownloadUrls.Add(payload.Url);
                        ShihabDownloadItem mediaIt = new ShihabDownloadItem
                        {
                            MehediUrl = payload.Url,
                            ShihabFileName = !string.IsNullOrWhiteSpace(payload.Title)
                                ? payload.Title
                                : ShihabFormatHelpers.ExtractMehediFileNameFromUrl(payload.Url),
                            ShihabCategoryName = !string.IsNullOrEmpty(AudioExtractFormat) ? "Music" : (payload.IsGalleryJob ? "General" : "Video"),
                            ShihabTotalBytes = payload.SizeBytes > 0 ? payload.SizeBytes : -1,
                            ShihabIsYtDlpJob = payload.IsYtDlpJob,
                            ShihabYtDlpFormatSelector = payload.FormatSelector ?? "",
                            ShihabIsGalleryJob = payload.IsGalleryJob,
                            MehediAudioCompanionUrl = payload.CompanionAudioUrl ?? "",
                            ShihabSubtitleLanguages = SelectedSubtitlesLang,
                            ShihabEmbedSubtitles = EmbedSubtitles,
                            ShihabAudioExtractFormat = AudioExtractFormat
                        };
                        ConfiguredMediaItems.Add(mediaIt);
                    }
                    else if (activeList.Items[i].Tag != null)
                    {
                        string u = activeList.Items[i].Tag.ToString();
                        DiscoveredDownloadUrls.Add(u);
                        ConfiguredMediaItems.Add(new ShihabDownloadItem
                        {
                            MehediUrl = u,
                            ShihabFileName = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(u),
                            ShihabCategoryName = "Video",
                            ShihabSubtitleLanguages = SelectedSubtitlesLang,
                            ShihabEmbedSubtitles = EmbedSubtitles,
                            ShihabAudioExtractFormat = AudioExtractFormat
                        });
                    }
                }
                if (ConfiguredMediaItems.Count == 0 && tabs.SelectedIndex == 0 && !string.IsNullOrWhiteSpace(ytUrlBox.Text))
                {
                    string rawU = ytUrlBox.Text.Trim();
                    bool hasYtdlp = File.Exists(ShihabMediaHelpersManager.GetHelperExecutablePath(store.MehediBaseDirectory, "yt-dlp"));
                    DiscoveredDownloadUrls.Add(rawU);
                    ConfiguredMediaItems.Add(new ShihabDownloadItem
                    {
                        MehediUrl = rawU,
                        ShihabFileName = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(rawU),
                        ShihabCategoryName = !string.IsNullOrEmpty(AudioExtractFormat) ? "Music" : "Video",
                        ShihabIsYtDlpJob = hasYtdlp,
                        ShihabYtDlpFormatSelector = "bv*+ba/b",
                        ShihabSubtitleLanguages = SelectedSubtitlesLang,
                        ShihabEmbedSubtitles = EmbedSubtitles,
                        ShihabAudioExtractFormat = AudioExtractFormat
                    });
                }
                if (ConfiguredMediaItems.Count == 0 && tabs.SelectedIndex == 1 && !string.IsNullOrWhiteSpace(galUrlBox.Text) && galUrlBox.Text.Trim() != "https://")
                {
                    string rawG = galUrlBox.Text.Trim();
                    DiscoveredDownloadUrls.Add(rawG);
                    ConfiguredMediaItems.Add(new ShihabDownloadItem
                    {
                        MehediUrl = rawG,
                        ShihabFileName = "Gallery_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                        ShihabCategoryName = "General",
                        ShihabIsGalleryJob = true
                    });
                }
                if (ConfiguredMediaItems.Count == 0) return;
                DialogResult = DialogResult.OK;
                Close();
            };

            Button closeBtn = ShihabThemePalette.CreateMehediStyledButton("Close", surf, ForeColor, 95);
            closeBtn.Location = new Point(635, 438);
            closeBtn.Click += delegate { Close(); };

            Controls.AddRange(new Control[] { tabs, dlCheckedBtn, closeBtn });
        }
    }

    // =========================================================================
    // 3. RSS / ATOM FEED READER & AUTO-DOWNLOADER DIALOG
    // =========================================================================
    public class ShihabRssFeedDialog : Form
    {
        public List<string> SelectedEnclosureUrls { get; private set; }

        public ShihabRssFeedDialog(MehediAppConfiguration config, ShihabStateStore store)
        {
            SelectedEnclosureUrls = new List<string>();
            bool dark = config.ShihabDarkMode;
            Color surf = ShihabThemePalette.GetShihabSurfaceBg(dark);

            Text = "RSS / Atom Feed Reader & Podcast / Torrent Auto-Downloader — " + ShihabThemePalette.ShihabMainHeaderTitle;
            Size = new Size(780, 510);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ShihabThemePalette.GetMehediWindowBg(dark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(dark);
            Font = new Font("Segoe UI", 9f);
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, dark);

            Label feedUrlLbl = new Label { Text = "Feed URL (RSS/Atom):", Location = new Point(16, 16), AutoSize = true };
            TextBox feedUrlBox = new TextBox { Location = new Point(142, 13), Width = 232, Text = "https://", BackColor = surf, ForeColor = ForeColor };
            Label regexLbl = new Label { Text = "Include:", Location = new Point(380, 16), AutoSize = true };
            TextBox regexBox = new TextBox { Location = new Point(430, 13), Width = 72, Text = "", BackColor = surf, ForeColor = ForeColor };
            Label exclLbl = new Label { Text = "Excl:", Location = new Point(508, 16), AutoSize = true };
            TextBox exclBox = new TextBox { Location = new Point(540, 13), Width = 64, Text = "", BackColor = surf, ForeColor = ForeColor };
            Label intvLbl = new Label { Text = "Min:", Location = new Point(608, 16), AutoSize = true };
            NumericUpDown intvNum = new NumericUpDown { Location = new Point(636, 13), Width = 44, Minimum = 5, Maximum = 1440, Value = 30 };
            Button addFeedBtn = ShihabThemePalette.CreateMehediStyledButton("+ Save", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 70);
            addFeedBtn.Height = 25;
            addFeedBtn.Location = new Point(684, 12);

            ListBox savedFeedsBox = new ListBox
            {
                Location = new Point(16, 50),
                Size = new Size(235, 355),
                BackColor = surf,
                ForeColor = ForeColor
            };

            ShihabModernListView entriesView = new ShihabModernListView
            {
                Location = new Point(262, 50),
                Size = new Size(490, 355),
                BorderStyle = BorderStyle.FixedSingle,
                CheckBoxes = true,
                MehediDarkMode = dark,
                ShihabShowFileTypeBadges = true,
                BackColor = surf,
                ForeColor = ForeColor
            };
            entriesView.Columns.Add("Episode / Release Title", 250);
            entriesView.Columns.Add("Enclosure / Download URL", 155);
            entriesView.Columns.Add("Published", 70);

            Action reloadFeeds = delegate
            {
                savedFeedsBox.Items.Clear();
                if (config.ShihabRssFeeds == null) config.ShihabRssFeeds = new List<ShihabRssFeedConfig>();
                for (int i = 0; i < config.ShihabRssFeeds.Count; i++)
                {
                    savedFeedsBox.Items.Add(config.ShihabRssFeeds[i].ShihabFeedTitle + " (" + config.ShihabRssFeeds[i].ShihabFeedUrl + ")");
                }
            };
            reloadFeeds();

            Action<string, string, string> fetchAndDisplayFeed = delegate(string url, string pattern, string exclPattern)
            {
                entriesView.Items.Clear();
                List<ShihabRssItemEntry> items = ShihabRssFeedManager.FetchFeedItems(url, config.ShihabDefaultUserAgent);
                for (int i = 0; i < items.Count; i++)
                {
                    ShihabRssItemEntry it = items[i];
                    if (!string.IsNullOrWhiteSpace(pattern) && pattern != "*")
                    {
                        try
                        {
                            if (!Regex.IsMatch(it.Title ?? "", pattern, RegexOptions.IgnoreCase) &&
                                !Regex.IsMatch(it.DownloadUrl ?? "", pattern, RegexOptions.IgnoreCase))
                            {
                                continue;
                            }
                        }
                        catch { }
                    }
                    if (!string.IsNullOrWhiteSpace(exclPattern))
                    {
                        try
                        {
                            if (Regex.IsMatch(it.Title ?? "", exclPattern, RegexOptions.IgnoreCase) ||
                                Regex.IsMatch(it.DownloadUrl ?? "", exclPattern, RegexOptions.IgnoreCase))
                            {
                                continue;
                            }
                        }
                        catch { }
                    }
                    ListViewItem row = new ListViewItem(new string[] { it.Title, it.DownloadUrl, it.PublishedDate });
                    row.Checked = true;
                    row.Tag = it.DownloadUrl;
                    entriesView.Items.Add(row);
                }
            };

            addFeedBtn.Click += delegate
            {
                string u = feedUrlBox.Text.Trim();
                if (string.IsNullOrEmpty(u) || u == "https://") return;
                if (config.ShihabRssFeeds == null) config.ShihabRssFeeds = new List<ShihabRssFeedConfig>();
                config.ShihabRssFeeds.Add(new ShihabRssFeedConfig
                {
                    ShihabFeedUrl = u,
                    ShihabFeedTitle = "Feed #" + (config.ShihabRssFeeds.Count + 1),
                    ShihabIncludeRegex = string.IsNullOrWhiteSpace(regexBox.Text) ? "*" : regexBox.Text.Trim(),
                    ShihabExcludePattern = exclBox.Text.Trim(),
                    MehediCheckIntervalMinutes = (int)intvNum.Value,
                    ShihabAutoDownload = true
                });
                store.SaveMehediConfig(config);
                reloadFeeds();
                fetchAndDisplayFeed(u, regexBox.Text.Trim(), exclBox.Text.Trim());
            };

            savedFeedsBox.SelectedIndexChanged += delegate
            {
                int idx = savedFeedsBox.SelectedIndex;
                if (idx >= 0 && idx < config.ShihabRssFeeds.Count)
                {
                    ShihabRssFeedConfig f = config.ShihabRssFeeds[idx];
                    feedUrlBox.Text = f.ShihabFeedUrl;
                    regexBox.Text = (f.ShihabIncludeRegex == "*") ? "" : (f.ShihabIncludeRegex ?? "");
                    exclBox.Text = f.ShihabExcludePattern ?? "";
                    intvNum.Value = Math.Max(5, Math.Min(1440, f.MehediCheckIntervalMinutes > 0 ? f.MehediCheckIntervalMinutes : 30));
                    fetchAndDisplayFeed(f.ShihabFeedUrl, f.ShihabIncludeRegex, f.ShihabExcludePattern);
                }
            };

            Button fetchNowBtn = ShihabThemePalette.CreateMehediStyledButton("Fetch Feed Now", surf, ForeColor, 130);
            fetchNowBtn.Location = new Point(16, 418);
            fetchNowBtn.Click += delegate { fetchAndDisplayFeed(feedUrlBox.Text.Trim(), regexBox.Text.Trim(), exclBox.Text.Trim()); };

            Button delFeedBtn = ShihabThemePalette.CreateMehediStyledButton("Delete Feed", ShihabThemePalette.GetMehediAccentRed(), Color.White, 100);
            delFeedBtn.Location = new Point(152, 418);
            delFeedBtn.Click += delegate
            {
                int idx = savedFeedsBox.SelectedIndex;
                if (idx >= 0 && idx < config.ShihabRssFeeds.Count)
                {
                    config.ShihabRssFeeds.RemoveAt(idx);
                    store.SaveMehediConfig(config);
                    reloadFeeds();
                }
            };

            Button dlCheckedBtn = ShihabThemePalette.CreateMehediStyledButton("Download Checked Enclosures", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 210);
            dlCheckedBtn.Location = new Point(430, 418);
            dlCheckedBtn.Click += delegate
            {
                SelectedEnclosureUrls.Clear();
                for (int i = 0; i < entriesView.Items.Count; i++)
                {
                    if (entriesView.Items[i].Checked && entriesView.Items[i].Tag != null)
                    {
                        SelectedEnclosureUrls.Add(entriesView.Items[i].Tag.ToString());
                    }
                }
                if (SelectedEnclosureUrls.Count == 0) return;
                DialogResult = DialogResult.OK;
                Close();
            };

            Button closeBtn = ShihabThemePalette.CreateMehediStyledButton("Close", surf, ForeColor, 100);
            closeBtn.Location = new Point(650, 418);
            closeBtn.Click += delegate { Close(); };

            Controls.AddRange(new Control[]
            {
                feedUrlLbl, feedUrlBox, regexLbl, regexBox, exclLbl, exclBox, intvLbl, intvNum, addFeedBtn,
                savedFeedsBox, entriesView, fetchNowBtn, delFeedBtn, dlCheckedBtn, closeBtn
            });
        }
    }

    // =========================================================================
    // 4. LAN REMOTE WEB CONTROL & 8-DIGIT QR PAIRING DIALOG
    // =========================================================================
    public class ShihabRemotePairingDialog : Form
    {
        public bool RemoteConfigChanged { get; private set; }

        public ShihabRemotePairingDialog(MehediAppConfiguration config, ShihabStateStore store)
        {
            bool dark = config.ShihabDarkMode;
            Color surf = ShihabThemePalette.GetShihabSurfaceBg(dark);

            if (string.IsNullOrWhiteSpace(config.ShihabRemotePairingCode))
            {
                config.ShihabRemotePairingCode = ShihabRemoteControlServer.GenerateEightDigitPairingCode();
            }

            Text = "LAN Remote Web UI & Mobile Pairing (QR / 8-Digit Code) — " + ShihabThemePalette.ShihabMainHeaderTitle;
            Size = new Size(640, 420);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ShihabThemePalette.GetMehediWindowBg(dark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(dark);
            Font = new Font("Segoe UI", 9f);
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, dark);

            CheckBox enableRemoteChk = new CheckBox
            {
                Text = "Enable LAN Remote Control Web Dashboard & REST API",
                Location = new Point(18, 18),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9.5f),
                Checked = config.ShihabRemoteWebEnabled
            };

            Label portLbl = new Label { Text = "Port:", Location = new Point(445, 20), AutoSize = true };
            NumericUpDown portNum = new NumericUpDown
            {
                Location = new Point(485, 17),
                Width = 95,
                Minimum = 1024,
                Maximum = 65535,
                Value = Math.Max(1024, Math.Min(65535, config.ShihabRemoteWebPort > 0 ? config.ShihabRemoteWebPort : 8463))
            };

            string lanUrl = ShihabRemoteControlServer.GetLocalLanDashboardUrl((int)portNum.Value);

            PictureBox qrBox = new PictureBox
            {
                Location = new Point(18, 56),
                Size = new Size(180, 180),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White,
                Image = ShihabQrCodeEncoder.RenderBitmap(lanUrl + "/?code=" + config.ShihabRemotePairingCode, 6, 4)
            };

            Label urlTitleLbl = new Label { Text = "LAN Dashboard URL:", Location = new Point(216, 56), AutoSize = true };
            TextBox urlBox = new TextBox { Location = new Point(216, 78), Width = 385, ReadOnly = true, Text = lanUrl, BackColor = surf, ForeColor = ShihabThemePalette.GetShihabAccentBlue(), Font = new Font("Consolas", 10f, FontStyle.Bold) };

            Label codeTitleLbl = new Label { Text = "8-Digit Pairing Code (scan QR or enter on phone):", Location = new Point(216, 114), AutoSize = true };
            Label codeBigLbl = new Label
            {
                Text = config.ShihabRemotePairingCode,
                Location = new Point(216, 136),
                AutoSize = true,
                Font = new Font("Consolas", 18f, FontStyle.Bold),
                ForeColor = ShihabThemePalette.GetMehediAccentGreen()
            };

            Action refreshQrImage = delegate
            {
                lanUrl = ShihabRemoteControlServer.GetLocalLanDashboardUrl((int)portNum.Value);
                urlBox.Text = lanUrl;
                Image oldImg = qrBox.Image;
                qrBox.Image = ShihabQrCodeEncoder.RenderBitmap(lanUrl + "/?code=" + config.ShihabRemotePairingCode, 6, 4);
                if (oldImg != null) oldImg.Dispose();
            };

            portNum.ValueChanged += delegate { refreshQrImage(); };

            Button regenBtn = ShihabThemePalette.CreateMehediStyledButton("New Code", surf, ForeColor, 105);
            regenBtn.Location = new Point(495, 140);
            regenBtn.Click += delegate
            {
                config.ShihabRemotePairingCode = ShihabRemoteControlServer.GenerateEightDigitPairingCode();
                codeBigLbl.Text = config.ShihabRemotePairingCode;
                refreshQrImage();
                RemoteConfigChanged = true;
            };

            Label mcpNoteLbl = new Label
            {
                Text = "AI Agent & Headless CLI Support:\r\n• Start MCP Server:  mdm.exe --mcp  (JSON-RPC 2.0 over stdio)\r\n• Headless CLI:      mdm.exe --cli status | list | add <url> | pause <id> | resume <id>",
                Location = new Point(18, 252),
                Size = new Size(585, 64),
                ForeColor = ShihabThemePalette.GetMehediMutedText(dark)
            };

            Button openBrowserBtn = ShihabThemePalette.CreateMehediStyledButton("Open Web UI", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 130);
            openBrowserBtn.Location = new Point(18, 332);
            openBrowserBtn.Click += delegate
            {
                try { Process.Start("http://127.0.0.1:" + (int)portNum.Value + "/"); } catch { }
            };

            Button saveBtn = ShihabThemePalette.CreateMehediStyledButton("Save & Apply", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 135);
            saveBtn.Location = new Point(355, 332);
            saveBtn.Click += delegate
            {
                config.ShihabRemoteWebEnabled = enableRemoteChk.Checked;
                config.ShihabRemoteWebPort = (int)portNum.Value;
                store.SaveMehediConfig(config);
                RemoteConfigChanged = true;
                DialogResult = DialogResult.OK;
                Close();
            };

            Button closeBtn = ShihabThemePalette.CreateMehediStyledButton("Close", surf, ForeColor, 100);
            closeBtn.Location = new Point(500, 332);
            closeBtn.Click += delegate { Close(); };

            Controls.AddRange(new Control[]
            {
                enableRemoteChk, portLbl, portNum, qrBox, urlTitleLbl, urlBox,
                codeTitleLbl, codeBigLbl, regenBtn, mcpNoteLbl,
                openBrowserBtn, saveBtn, closeBtn
            });
        }
    }

    // =========================================================================
    // 5. STANDALONE CHECKSUM VERIFIER & SAFE ARCHIVE EXTRACTOR DIALOG
    // =========================================================================
    public class ShihabChecksumAndExtractDialog : Form
    {
        public ShihabChecksumAndExtractDialog(bool dark)
        {
            Color surf = ShihabThemePalette.GetShihabSurfaceBg(dark);
            Text = "Multi-Algorithm Checksum Verifier & Safe Archive Extractor — " + ShihabThemePalette.ShihabMainHeaderTitle;
            Size = new Size(640, 420);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ShihabThemePalette.GetMehediWindowBg(dark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(dark);
            Font = new Font("Segoe UI", 9f);
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, dark);

            Label fileLbl = new Label { Text = "Target File / Archive:", Location = new Point(16, 18), AutoSize = true };
            TextBox fileBox = new TextBox { Location = new Point(145, 15), Width = 365, BackColor = surf, ForeColor = ForeColor };
            Button browseBtn = ShihabThemePalette.CreateMehediStyledButton("Browse...", surf, ForeColor, 92);
            browseBtn.Height = 25;
            browseBtn.Location = new Point(518, 13);
            browseBtn.Click += delegate
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "All Files (*.*)|*.*" })
                {
                    if (ofd.ShowDialog(this) == DialogResult.OK) fileBox.Text = ofd.FileName;
                }
            };

            Label algoLbl = new Label { Text = "Hash Algorithm:", Location = new Point(16, 54), AutoSize = true };
            ComboBox algoCombo = new ComboBox { Location = new Point(145, 51), Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = surf, ForeColor = ForeColor };
            algoCombo.Items.AddRange(new object[] { "SHA-256", "SHA-512", "SHA-1", "MD5", "CRC32" });
            algoCombo.SelectedIndex = 0;

            Label expLbl = new Label { Text = "Expected Digest:", Location = new Point(16, 88), AutoSize = true };
            TextBox expBox = new TextBox { Location = new Point(145, 85), Width = 465, BackColor = surf, ForeColor = ForeColor };

            TextBox resultBox = new TextBox
            {
                Location = new Point(16, 124),
                Size = new Size(594, 185),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = surf,
                ForeColor = ForeColor,
                Font = new Font("Consolas", 9f)
            };

            Button computeAllBtn = ShihabThemePalette.CreateMehediStyledButton("Compute Hash", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 140);
            computeAllBtn.Location = new Point(16, 326);
            computeAllBtn.Click += delegate
            {
                string f = fileBox.Text.Trim();
                if (!File.Exists(f))
                {
                    MessageBox.Show(this, "Please select an existing file first.", "Checksum Verifier", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                string algo = algoCombo.SelectedItem.ToString();
                string digest = ShihabChecksumVerifier.ComputeFileChecksumHex(f, algo);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("File:      " + f);
                sb.AppendLine("Algorithm: " + algo);
                sb.AppendLine("Digest:    " + digest);
                if (!string.IsNullOrWhiteSpace(expBox.Text))
                {
                    string parsedAlgo, comp, err;
                    bool ok = ShihabChecksumVerifier.VerifyExpectedChecksum(f, expBox.Text.Trim(), out parsedAlgo, out comp, out err);
                    sb.AppendLine("Status:    " + (ok ? "MATCH VERIFIED ✔" : ("MISMATCH ✘ — " + err)));
                }
                resultBox.Text = sb.ToString();
            };

            Button extractBtn = ShihabThemePalette.CreateMehediStyledButton("Safe Extract Archive", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 165);
            extractBtn.Location = new Point(166, 326);
            extractBtn.Click += delegate
            {
                string f = fileBox.Text.Trim();
                if (!File.Exists(f)) return;
                string destDir, err;
                if (ShihabSafeArchiveExtractor.ExtractArchiveSafely(f, "Smart", false, "", out destDir, out err))
                {
                    resultBox.Text = "Archive safely extracted (Zip-Slip & 200x Bomb Protected) to:\r\n" + destDir;
                }
                else
                {
                    resultBox.Text = "Archive extraction failed:\r\n" + err;
                }
            };

            Button closeBtn = ShihabThemePalette.CreateMehediStyledButton("Close", surf, ForeColor, 100);
            closeBtn.Location = new Point(510, 326);
            closeBtn.Click += delegate { Close(); };

            Controls.AddRange(new Control[]
            {
                fileLbl, fileBox, browseBtn, algoLbl, algoCombo,
                expLbl, expBox, resultBox, computeAllBtn, extractBtn, closeBtn
            });
        }
    }
}
