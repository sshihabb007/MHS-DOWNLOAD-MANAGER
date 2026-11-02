using System;
using System.Collections.Generic;
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

        public bool ShihabBridgeWasRestarted { get; private set; }
        public bool MehediBridgeRestartSucceeded { get; private set; }

        public ShihabOptionsConfigForm(
            MehediAppConfiguration shihabConfig,
            ShihabStateStore mehediStore,
            MehediBrowserBridgeServer shihabBridge = null)
        {
            _shihabConfig = shihabConfig;
            _mehediStore = mehediStore;
            _shihabBridge = shihabBridge;
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
                "Apple Safari", "Brave Browser", "Google Chrome", "Internet Explorer",
                "Microsoft Edge", "Microsoft Edge Legacy", "Mozilla Firefox", "Opera", "Vivaldi"
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

            shihabGenTab.Controls.AddRange(new Control[]
            {
                shihabBrowserChk, mehediRestartBridgeBtn, mehediStartupChk, shihabClipChk,
                mehediBrowsersGrp, shihabCtxMenuLbl, mehediCtxEditBtn, mehediPanelsLbl, shihabPanelsEditBtn
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

            mehediTypesTab.Controls.AddRange(new Control[]
            {
                shihabTypesLbl, mehediTypesBox, shihabTypesDefaultBtn,
                shihabExclLbl, mehediExclBox, shihabSepHintLbl, mehediSitesDefaultBtn,
                shihabAddrExclLbl, mehediAddrEditBtn
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

            CheckBox mehediStartDlgChk = new CheckBox { Text = "Show start download dialog ('Download File Info')", Location = new Point(18, 46), AutoSize = true, Checked = _shihabConfig.ShihabShowStartDownloadDialog };
            CheckBox shihabProgDlgChk = new CheckBox { Text = "Show download progress dialog during active transfers", Location = new Point(18, 74), AutoSize = true, Checked = _shihabConfig.MehediShowDownloadProgressDialog };
            CheckBox mehediCompDlgChk = new CheckBox { Text = "Show download complete dialog when finished", Location = new Point(18, 102), AutoSize = true, Checked = _shihabConfig.ShihabShowDownloadCompleteDialog };
            CheckBox shihabDarkChk = new CheckBox { Text = "Dark Mode UI Theme (uncheck for Classic Light Theme)", Location = new Point(18, 130), AutoSize = true, Checked = _shihabConfig.ShihabDarkMode };

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
                mehediDupLbl, shihabDupCombo, mehediAvGrp, mehediNativeGrp
            });

            // ==================== 5. CONNECTION TAB ====================
            AddShihabSectionBanner(mehediConnTab, "Connection type and Dynamic Segmentation", shihabDark);

            Label shihabConnTypeLbl = new Label { Text = "Connection Type / Speed:", Location = new Point(18, 46), AutoSize = true };
            ComboBox mehediConnTypeCombo = new ComboBox
            {
                Location = new Point(185, 43),
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

            Label shihabMaxConnLbl = new Label { Text = "Default max. conn. number (Dynamic Segmentation 1..32):", Location = new Point(18, 86), AutoSize = true };
            NumericUpDown mehediMaxConnNum = new NumericUpDown { Location = new Point(495, 83), Width = 95, Minimum = 1, Maximum = 32, Value = Math.Max(1, Math.Min(32, _shihabConfig.ShihabDefaultMaxConnections)) };

            Label mehediRetrySecLbl = new Label { Text = "Pause between auto-retry attempts (seconds):", Location = new Point(18, 124), AutoSize = true };
            NumericUpDown shihabRetrySecNum = new NumericUpDown { Location = new Point(335, 121), Width = 80, Minimum = 1, Maximum = 3600, Value = Math.Max(1, Math.Min(3600, _shihabConfig.ShihabAutoRetryIntervalSec > 0 ? _shihabConfig.ShihabAutoRetryIntervalSec : 10)) };
            Label mehediMaxRetLbl = new Label { Text = "Max retries:", Location = new Point(430, 124), AutoSize = true };
            NumericUpDown shihabMaxRetNum = new NumericUpDown { Location = new Point(510, 121), Width = 80, Minimum = 0, Maximum = 999, Value = Math.Max(0, Math.Min(999, _shihabConfig.MehediMaxRetryAttempts > 0 ? _shihabConfig.MehediMaxRetryAttempts : 99)) };

            CheckBox shihabGlobalLimChk = new CheckBox { Text = "Enable Global Speed Limiter across all active downloads", Location = new Point(18, 168), AutoSize = true, Checked = _shihabConfig.MehediGlobalSpeedLimiterEnabled };
            Label mehediGlobSpdLbl = new Label { Text = "Maximum global download transfer rate (KBytes/sec):", Location = new Point(18, 200), AutoSize = true };
            NumericUpDown shihabGlobSpdNum = new NumericUpDown { Location = new Point(365, 197), Width = 115, Minimum = 16, Maximum = 2000000, Value = Math.Max(16, _shihabConfig.ShihabGlobalSpeedLimitKb) };

            Label mehediUaLbl = new Label { Text = "User-Agent for manually added downloads:", Location = new Point(18, 246), AutoSize = true };
            TextBox shihabUaBox = new TextBox { Location = new Point(18, 270), Width = 585, Text = _shihabConfig.ShihabDefaultUserAgent, BackColor = mehediSurf, ForeColor = ForeColor };

            mehediConnTab.Controls.AddRange(new Control[]
            {
                shihabConnTypeLbl, mehediConnTypeCombo, shihabMaxConnLbl, mehediMaxConnNum,
                mehediRetrySecLbl, shihabRetrySecNum, mehediMaxRetLbl, shihabMaxRetNum,
                shihabGlobalLimChk, mehediGlobSpdLbl, shihabGlobSpdNum, mehediUaLbl, shihabUaBox
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

            shihabProxyTab.Controls.AddRange(new Control[] { mehediHttpPrxGrp, shihabSocksGrp });

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
                Location = new Point(18, 174),
                Size = new Size(580, 44),
                ForeColor = shihabMuted
            };
            shihabDialTab.Controls.AddRange(new Control[] { mehediDialConnLbl, shihabDialConnBox, mehediRedialChk, shihabHangUpChk, mehediDialNote });

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

            mehediTabs.TabPages.AddRange(new TabPage[]
            {
                shihabGenTab, mehediTypesTab, shihabSaveTab, mehediDownloadsTab,
                mehediConnTab, shihabProxyTab, mehediLoginsTab, shihabDialTab, mehediSoundsTab
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
                _shihabConfig.MehediDuplicateFileMode = shihabDupCombo.SelectedIndex == 1 ? 1 : 0;
                _shihabConfig.ShihabEnableVirusCheck = shihabAvEnableChk.Checked;
                _shihabConfig.MehediVirusScannerExe = mehediAvExeBox.Text.Trim();
                _shihabConfig.ShihabVirusScannerArgs = shihabAvArgsBox.Text.Trim();

                _shihabConfig.MehediConnectionProfile = mehediConnTypeCombo.SelectedItem != null
                    ? mehediConnTypeCombo.SelectedItem.ToString()
                    : "High speed: Direct connection (Ethernet/Cable) / Wi-Fi / Mobile 4G/5G";
                _shihabConfig.ShihabDefaultMaxConnections = (int)mehediMaxConnNum.Value;
                _shihabConfig.ShihabAutoRetryIntervalSec = (int)shihabRetrySecNum.Value;
                _shihabConfig.MehediMaxRetryAttempts = (int)shihabMaxRetNum.Value;
                _shihabConfig.MehediGlobalSpeedLimiterEnabled = shihabGlobalLimChk.Checked;
                _shihabConfig.ShihabGlobalSpeedLimitKb = (int)shihabGlobSpdNum.Value;
                _shihabConfig.ShihabDefaultUserAgent = shihabUaBox.Text.Trim();

                _shihabConfig.MehediUseHttpProxy = shihabUseProxyChk.Checked;
                _shihabConfig.ShihabProxyAddress = shihabPrxHostBox.Text.Trim();
                _shihabConfig.MehediProxyPort = (int)shihabPrxPortNum.Value;
                _shihabConfig.ShihabProxyUser = shihabPrxUserBox.Text.Trim();
                _shihabConfig.MehediProxyPassword = shihabPrxPassBox.Text;
                _shihabConfig.ShihabUseSocksProxy = mehediUseSocksChk.Checked;
                _shihabConfig.MehediSocksHost = mehediSocksHostBox.Text.Trim();
                _shihabConfig.ShihabSocksPort = (int)mehediSocksPortNum.Value;

                _shihabConfig.MehediDialUpConnectionName = shihabDialConnBox.Text.Trim();
                _shihabConfig.ShihabRedialIfDisconnected = mehediRedialChk.Checked;
                _shihabConfig.MehediHangUpWhenDone = shihabHangUpChk.Checked;

                _shihabConfig.MehediPlaySoundComplete = mehediSndCompChk.Checked;
                _shihabConfig.ShihabSoundCompleteWav = shihabSndCompBox.Text.Trim();
                _shihabConfig.MehediPlaySoundError = shihabSndErrChk.Checked;
                _shihabConfig.ShihabSoundErrorWav = mehediSndErrBox.Text.Trim();

                _mehediStore.SaveMehediConfig(_shihabConfig);
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
                    "MHS Download Manager 1.0.3 Configuration\nDeveloper: Mehedi Hasan Shihab (https://sshihabb007.github.io/)\n\n" +
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
        private CheckBox _shihabEnableStopChk;
        private TextBox _mehediStopHhMmBox;
        private NumericUpDown _shihabMaxConcNum;
        private NumericUpDown _mehediRetryNum;
        private CheckBox _shihabExitDoneChk;
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

            Text = "Scheduler and Queues — " + ShihabThemePalette.ShihabMainHeaderTitle;
            Size = new Size(710, 465);
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
                Size = new Size(195, 290),
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark),
                ForeColor = ForeColor
            };

            Button shihabNewQBtn = ShihabThemePalette.CreateMehediStyledButton("+ New Queue", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 94);
            shihabNewQBtn.Location = new Point(14, 336);
            shihabNewQBtn.Click += delegate
            {
                string mehediNewName = "Shihab Queue #" + (_shihabConfig.MehediQueues.Count + 1);
                MehediQueueConfig shihabQ = new MehediQueueConfig { ShihabQueueName = mehediNewName };
                _shihabConfig.MehediQueues.Add(shihabQ);
                PopulateShihabQueuesList();
                _mehediQueueListBox.SelectedIndex = _mehediQueueListBox.Items.Count - 1;
            };

            Button mehediDelQBtn = ShihabThemePalette.CreateMehediStyledButton("Delete", ShihabThemePalette.GetMehediAccentRed(), Color.White, 94);
            mehediDelQBtn.Location = new Point(115, 336);
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
                Size = new Size(460, 355)
            };
            TabPage mehediSchedTab = new TabPage("Schedule") { BackColor = BackColor, ForeColor = ForeColor };
            TabPage shihabFilesTab = new TabPage("Files in the queue") { BackColor = BackColor, ForeColor = ForeColor };

            _shihabEnableStartChk = new CheckBox { Text = "Start download queue at (HH:mm):", Location = new Point(18, 24), AutoSize = true };
            _mehediStartHhMmBox = new TextBox { Location = new Point(265, 22), Width = 85, Text = "01:00", BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor = ForeColor };

            _shihabEnableStopChk = new CheckBox { Text = "Stop download queue at (HH:mm):", Location = new Point(18, 60), AutoSize = true };
            _mehediStopHhMmBox = new TextBox { Location = new Point(265, 58), Width = 85, Text = "07:00", BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor = ForeColor };

            Label mehediConcLbl = new Label { Text = "Download files at the same time:", Location = new Point(18, 106), AutoSize = true };
            _shihabMaxConcNum = new NumericUpDown { Location = new Point(265, 103), Width = 85, Minimum = 1, Maximum = 16, Value = 4 };

            Label shihabRetLbl = new Label { Text = "Number of retries for each failed file:", Location = new Point(18, 142), AutoSize = true };
            _mehediRetryNum = new NumericUpDown { Location = new Point(265, 139), Width = 85, Minimum = 0, Maximum = 99, Value = 3 };

            _shihabExitDoneChk = new CheckBox { Text = "Exit MHS Download Manager when queue finishes", Location = new Point(18, 185), AutoSize = true };

            Button mehediApplyQBtn = ShihabThemePalette.CreateMehediStyledButton("Apply Schedule", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 135);
            mehediApplyQBtn.Location = new Point(18, 235);
            mehediApplyQBtn.Click += delegate { SaveSelectedMehediQueueSettings(); };

            mehediSchedTab.Controls.AddRange(new Control[]
            {
                _shihabEnableStartChk, _mehediStartHhMmBox, _shihabEnableStopChk, _mehediStopHhMmBox,
                mehediConcLbl, _shihabMaxConcNum, shihabRetLbl, _mehediRetryNum, _shihabExitDoneChk, mehediApplyQBtn
            });

            _mehediFilesInQueueView = new ShihabModernListView
            {
                Location = new Point(12, 12),
                Size = new Size(426, 265),
                BorderStyle = BorderStyle.FixedSingle,
                MehediDarkMode = mehediDark,
                ShihabShowFileTypeBadges = true,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark),
                ForeColor = ForeColor
            };
            _mehediFilesInQueueView.Columns.Add("File Name", 210);
            _mehediFilesInQueueView.Columns.Add("Size", 95);
            _mehediFilesInQueueView.Columns.Add("Status", 105);

            Button shihabRemoveFromQ = ShihabThemePalette.CreateMehediStyledButton("Remove from Queue", ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor, 155);
            shihabRemoveFromQ.Location = new Point(12, 284);
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
            shihabStartNowBtn.Location = new Point(315, 382);
            shihabStartNowBtn.Click += delegate
            {
                SaveSelectedMehediQueueSettings();
                MehediQueueConfig mehediQ = GetSelectedShihabQueue();
                if (mehediQ != null) _mehediEngine.StartShihabQueue(mehediQ.ShihabQueueName);
                Close();
            };

            Button mehediStopNowBtn = ShihabThemePalette.CreateMehediStyledButton("Stop Queue", ShihabThemePalette.GetShihabAccentAmber(), Color.White, 120);
            mehediStopNowBtn.Location = new Point(445, 382);
            mehediStopNowBtn.Click += delegate
            {
                MehediQueueConfig shihabQ = GetSelectedShihabQueue();
                if (shihabQ != null) _mehediEngine.StopShihabQueue(shihabQ.ShihabQueueName);
            };

            Button shihabCloseBtn = ShihabThemePalette.CreateMehediStyledButton("Close", ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor, 108);
            shihabCloseBtn.Location = new Point(574, 382);
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
            _shihabEnableStopChk.Checked = shihabQ.MehediEnableScheduledStop;
            _mehediStopHhMmBox.Text = shihabQ.ShihabStopTimeHhMm ?? "07:00";
            _shihabMaxConcNum.Value = Math.Max(1, Math.Min(16, shihabQ.MehediMaxConcurrentFiles));
            _mehediRetryNum.Value = Math.Max(0, Math.Min(99, shihabQ.ShihabRetriesPerFile));
            _shihabExitDoneChk.Checked = shihabQ.MehediExitAppWhenDone;

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
            shihabQ.MehediEnableScheduledStop = _shihabEnableStopChk.Checked;
            shihabQ.ShihabStopTimeHhMm = _mehediStopHhMmBox.Text.Trim();
            shihabQ.MehediMaxConcurrentFiles = (int)_shihabMaxConcNum.Value;
            shihabQ.ShihabRetriesPerFile = (int)_mehediRetryNum.Value;
            shihabQ.MehediExitAppWhenDone = _shihabExitDoneChk.Checked;
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
                        shihabEnd >= mehediStart && (shihabEnd - mehediStart) < 1000)
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
                    mehediPreviewLbl.Text = "Ensure URL contains '*' and valid range (< 1000 files).";
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
            return shihabEnd >= mehediStart && (shihabEnd - mehediStart) <= 52;
        }
    }

    public class MehediDownloadAllLinksForm : Form
    {
        public List<MehediBatchLinkEntryDto> ShihabChosenLinks { get; private set; }
        public bool MehediStartNow { get; private set; }

        public MehediDownloadAllLinksForm(ShihabBatchCaptureDto shihabBatch, bool mehediDark)
        {
            ShihabChosenLinks = new List<MehediBatchLinkEntryDto>();
            Text = "Download All Links — " + ShihabThemePalette.ShihabMainHeaderTitle;
            Size = new Size(760, 490);
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            BackColor = ShihabThemePalette.GetMehediWindowBg(mehediDark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(mehediDark);
            Font = new Font("Segoe UI", 9f);
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, mehediDark);
            Icon = ShihabThemePalette.CreateShihabAppIcon();

            CheckBox shihabHideHtmlChk = new CheckBox { Text = "Hide HTML web pages", Location = new Point(14, 14), AutoSize = true, Checked = true };
            CheckBox mehediHideImgChk = new CheckBox { Text = "Hide images (.png/.jpg/.gif/.svg)", Location = new Point(185, 14), AutoSize = true, Checked = false };
            Label shihabExtFilterLbl = new Label { Text = "Filter ext/text:", Location = new Point(415, 16), AutoSize = true };
            TextBox mehediFilterBox = new TextBox { Location = new Point(505, 13), Width = 220, BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor = ForeColor };

            ShihabModernListView shihabLinksView = new ShihabModernListView
            {
                Location = new Point(14, 46),
                Size = new Size(714, 340),
                BorderStyle = BorderStyle.FixedSingle,
                CheckBoxes = true,
                MehediDarkMode = mehediDark,
                ShihabShowFileTypeBadges = true,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark),
                ForeColor = ForeColor
            };
            shihabLinksView.Columns.Add("File Name / URL", 360);
            shihabLinksView.Columns.Add("Type", 85);
            shihabLinksView.Columns.Add("Link Text", 240);

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

                    string mehediName = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(shihabEntry.ShihabUrl);
                    string shihabExt = Path.GetExtension(mehediName).TrimStart('.').ToUpperInvariant();
                    if (string.IsNullOrEmpty(shihabExt)) shihabExt = "HTML";

                    if (shihabHideHtmlChk.Checked && (shihabExt == "HTML" || shihabExt == "HTM" || shihabExt == "PHP" || shihabExt == "ASP"))
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

                    ListViewItem mehediRow = new ListViewItem(new string[] { shihabEntry.ShihabUrl, shihabExt, shihabEntry.MehediText ?? "" });
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

            Button shihabChkAll = ShihabThemePalette.CreateMehediStyledButton("Check All", ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor, 105);
            shihabChkAll.Location = new Point(14, 398);
            shihabChkAll.Click += delegate { foreach (ListViewItem mehediIt in shihabLinksView.Items) mehediIt.Checked = true; };

            Button mehediUnchkAll = ShihabThemePalette.CreateMehediStyledButton("Uncheck All", ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor, 110);
            mehediUnchkAll.Location = new Point(126, 398);
            mehediUnchkAll.Click += delegate { foreach (ListViewItem shihabIt in shihabLinksView.Items) shihabIt.Checked = false; };

            Button shihabQueueBtn = ShihabThemePalette.CreateMehediStyledButton("Add Checked to Queue", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 175);
            shihabQueueBtn.Location = new Point(315, 398);
            shihabQueueBtn.Click += delegate
            {
                CollectCheckedShihabLinks(shihabLinksView);
                if (ShihabChosenLinks.Count == 0) return;
                MehediStartNow = false;
                DialogResult = DialogResult.OK;
                Close();
            };

            Button mehediNowBtn = ShihabThemePalette.CreateMehediStyledButton("Download Now", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 130);
            mehediNowBtn.Location = new Point(498, 398);
            mehediNowBtn.Click += delegate
            {
                CollectCheckedShihabLinks(shihabLinksView);
                if (ShihabChosenLinks.Count == 0) return;
                MehediStartNow = true;
                DialogResult = DialogResult.OK;
                Close();
            };

            Button shihabCancel = ShihabThemePalette.CreateMehediStyledButton("Cancel", ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor, 92);
            shihabCancel.Location = new Point(636, 398);
            shihabCancel.Click += delegate { Close(); };

            Controls.AddRange(new Control[]
            {
                shihabHideHtmlChk, mehediHideImgChk, shihabExtFilterLbl, mehediFilterBox,
                shihabLinksView, shihabChkAll, mehediUnchkAll, shihabQueueBtn, mehediNowBtn, shihabCancel
            });
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
                    shihabReq.UserAgent = "Mozilla/5.0 ShihabMehediIDM-Grabber/1.0.3";
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
        private Point _mehediDragStart;
        private bool _shihabDragging;
        private string _mehediSpeedLabel = "Drop URL";

        public MehediFloatingDropTargetForm(Action<string> shihabOnUrlDropped)
        {
            _shihabOnUrlDropped = shihabOnUrlDropped;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Rectangle mehediScreen = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(mehediScreen.Right - 108, mehediScreen.Bottom - 108);
            Size = new Size(88, 88);
            TopMost = true;
            ShowInTaskbar = false;
            AllowDrop = true;
            Opacity = 0.90;
            BackColor = Color.FromArgb(15, 23, 42);
            DoubleBuffered = true;

            DragEnter += delegate(object shihabS, DragEventArgs mehediE)
            {
                if (mehediE.Data.GetDataPresent(DataFormats.UnicodeText) || mehediE.Data.GetDataPresent(DataFormats.Text))
                {
                    mehediE.Effect = DragDropEffects.Copy;
                }
            };
            DragDrop += delegate(object shihabS, DragEventArgs mehediE)
            {
                string shihabDropped = (mehediE.Data.GetData(DataFormats.UnicodeText) ?? mehediE.Data.GetData(DataFormats.Text) ?? "").ToString().Trim();
                if (!string.IsNullOrEmpty(shihabDropped) && _shihabOnUrlDropped != null)
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
            MouseUp += delegate { _shihabDragging = false; };

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
}
