using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Media;
using System.Windows.Forms;

namespace ShihabMehediIDM
{
    public partial class ShihabOptionsConfigForm : Form
    {
        private void BuildBottomActionButtons(ShihabDarkTabControl mehediTabs)
        {
            Button shihabSaveOk = ShihabThemePalette.CreateMehediStyledButton("OK", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 102);
            shihabSaveOk.Location = new Point(332, 478);
            shihabSaveOk.Click += delegate
            {
                _shihabConfig.MehediBrowserIntegrationEnabled = _shihabBrowserChk.Checked;
                bool mehediStartupChanged = (_shihabConfig.ShihabLaunchOnStartup != _mehediStartupChk.Checked);
                _shihabConfig.ShihabLaunchOnStartup = _mehediStartupChk.Checked;
                if (mehediStartupChanged || _mehediStartupChk.Checked)
                {
                    MehediAppConfiguration.SyncShihabWindowsStartupRegistry(_mehediStartupChk.Checked);
                }
                _shihabConfig.ShihabMinimizeToTray = _shihabMinTrayChk.Checked;
                _shihabConfig.ShihabCloseToTray = _shihabCloseTrayChk.Checked;
                if (_shihabLangCombo.SelectedItem != null)
                {
                    _shihabConfig.ShihabLanguageCode = ShihabThemeManager.ExtractLanguageIsoCode(_shihabLangCombo.SelectedItem.ToString());
                }
                if (_shihabScaleCombo.SelectedItem != null)
                {
                    int parsedScale;
                    if (int.TryParse(_shihabScaleCombo.SelectedItem.ToString().TrimEnd('%'), out parsedScale))
                    {
                        _shihabConfig.ShihabUiScalePercent = Math.Max(100, Math.Min(200, parsedScale));
                    }
                }

                _shihabConfig.ShihabMonitorClipboard = _shihabClipChk.Checked;
                List<string> shihabCheckedBrowsers = new List<string>();
                for (int mehediBi = 0; mehediBi < _shihabBrowsersClb.CheckedItems.Count; mehediBi++)
                {
                    shihabCheckedBrowsers.Add(_shihabBrowsersClb.CheckedItems[mehediBi].ToString());
                }
                _shihabConfig.MehediEnabledBrowsersCsv = string.Join(",", shihabCheckedBrowsers.ToArray());

                _shihabConfig.MehediPreventKeyName = _shihabPendingPreventKey;
                _shihabConfig.ShihabForceKeyName = _mehediPendingForceKey;
                _shihabConfig.MehediContextMenuItemsCsv = _shihabPendingCtxMenuCsv;
                _shihabConfig.ShihabShowVideoFloatingPanel = _mehediPendingShowPanel;
                _shihabConfig.ShihabPanelMiniMode = _shihabPendingMiniMode;
                _shihabConfig.MehediPanelSkipAutoStart = _mehediPendingSkipAuto;
                _shihabConfig.MehediMinVideoSizeKb = _shihabPendingMinVidKb;

                _shihabConfig.MehediAutoCaptureExtensions = _mehediTypesBox.Text.Trim();
                _shihabConfig.ShihabExcludedSitesSpaceSeparated = _mehediExclBox.Text.Trim();
                _shihabConfig.MehediExcludedAddressesSpaceSeparated = _mehediPendingExcludedAddr;
                _shihabConfig.ShihabCaptureOpenedFiles = _shihabCapOpenedChk.Checked;
                _shihabConfig.ShihabCaptureMinSizeKb = (int)_shihabMinSizeCapNum.Value;

                if (!string.IsNullOrWhiteSpace(_shihabTmpBox.Text))
                {
                    _shihabConfig.MehediTemporaryDirectory = _shihabTmpBox.Text.Trim();
                }
                _shihabConfig.ShihabSetServerCreationDate = _shihabServerCreationChk.Checked;
                _shihabConfig.ShihabSetServerFileTimestamp = _mehediServerTimeChk.Checked;

                _shihabConfig.ShihabShowStartDownloadDialog = _mehediStartDlgChk.Checked;
                _shihabConfig.MehediShowDownloadProgressDialog = _shihabProgDlgChk.Checked;
                _shihabConfig.ShihabShowDownloadCompleteDialog = _mehediCompDlgChk.Checked;
                _shihabConfig.ShihabDarkMode = _shihabDarkChk.Checked;
                if (_shihabThemePresetCombo.SelectedItem != null)
                {
                    _shihabConfig.ShihabThemeName = _shihabThemePresetCombo.SelectedItem.ToString();
                    ShihabCustomThemePreset selPreset = ShihabThemeManager.ResolveThemePalette(
                        _shihabConfig.ShihabThemeName,
                        _mehediStore != null ? _mehediStore.MehediBaseDirectory : "");
                    _shihabConfig.ShihabDarkMode = ShihabThemeManager.IsDarkThemePreset(selPreset);
                }
                if (_shihabExtSubCombo.SelectedItem != null)
                {
                    _shihabConfig.ShihabExtractSubfolderMode = _shihabExtSubCombo.SelectedItem.ToString();
                }
                _shihabConfig.ShihabAutoExtractArchives = _shihabAutoExtGlobalChk.Checked;
                _shihabConfig.ShihabDeleteArchiveAfterExtract = _shihabDelArchChk.Checked;
                _shihabConfig.ShihabWriteZoneIdentifier = _shihabMotwChk.Checked;
                _shihabConfig.ShihabPreventSystemSleep = _shihabSleepChk.Checked;
                _shihabConfig.MehediDuplicateFileMode = _shihabDupCombo.SelectedIndex == 1 ? 1 : 0;
                _shihabConfig.ShihabEnableVirusCheck = _shihabAvEnableChk.Checked;
                _shihabConfig.MehediVirusScannerExe = _mehediAvExeBox.Text.Trim();
                _shihabConfig.ShihabVirusScannerArgs = _shihabAvArgsBox.Text.Trim();

                _shihabConfig.MehediConnectionProfile = _mehediConnTypeCombo.SelectedItem != null
                    ? _mehediConnTypeCombo.SelectedItem.ToString()
                    : "High speed: Direct connection (Ethernet/Cable) / Wi-Fi / Mobile 4G/5G";
                _shihabConfig.ShihabAllowMoreThan32Connections = _shihabUnlock128Chk.Checked;
                _shihabConfig.ShihabShedSlowConnections = _shihabShedSlowChk.Checked;
                _shihabConfig.ShihabVerifyResumeOverlap = _shihabOverlapChk.Checked;
                _shihabConfig.ShihabEnableSparseFiles = _shihabSparseChk.Checked;
                _shihabConfig.ShihabDefaultMaxConnections = (int)_mehediMaxConnNum.Value;
                _shihabConfig.ShihabAutoRetryIntervalSec = (int)_shihabRetrySecNum.Value;
                _shihabConfig.MehediMaxRetryAttempts = (int)_shihabMaxRetNum.Value;
                _shihabConfig.MehediGlobalSpeedLimiterEnabled = _shihabGlobalLimChk.Checked;
                _shihabConfig.ShihabGlobalSpeedLimitKb = (int)_shihabGlobSpdNum.Value;
                _shihabConfig.ShihabDefaultUserAgent = _shihabUaBox.Text.Trim();
                _shihabConfig.ShihabQuotaEnabled = _shihabQuotaChk.Checked;
                _shihabConfig.ShihabQuotaLimitMegabytes = (int)_shihabQuotaMbNum.Value;
                _shihabConfig.ShihabQuotaWindowHours = (int)_shihabQuotaHrsNum.Value;

                _shihabConfig.MehediUseHttpProxy = _shihabUseProxyChk.Checked;
                _shihabConfig.ShihabProxyAddress = _shihabPrxHostBox.Text.Trim();
                _shihabConfig.MehediProxyPort = (int)_shihabPrxPortNum.Value;
                _shihabConfig.ShihabProxyUser = _shihabPrxUserBox.Text.Trim();
                _shihabConfig.MehediProxyPassword = _shihabPrxPassBox.Text;
                _shihabConfig.ShihabUseSocksProxy = _mehediUseSocksChk.Checked;
                _shihabConfig.MehediSocksHost = _mehediSocksHostBox.Text.Trim();
                _shihabConfig.ShihabSocksPort = (int)_mehediSocksPortNum.Value;
                _shihabConfig.ShihabPacScriptUrl = _shihabPacUrlBox.Text.Trim();
                _shihabConfig.ShihabProxyMode = !string.IsNullOrWhiteSpace(_shihabConfig.ShihabPacScriptUrl)
                    ? "PAC"
                    : (_shihabConfig.ShihabUseSocksProxy ? "SOCKS5" : (_shihabConfig.MehediUseHttpProxy ? "Manual" : "System"));
                _shihabConfig.ShihabDnsMode = _shihabDnsModeCombo.SelectedItem != null ? _shihabDnsModeCombo.SelectedItem.ToString() : "System";
                _shihabConfig.ShihabDnsProvider = _shihabDohProvCombo.SelectedItem != null ? _shihabDohProvCombo.SelectedItem.ToString() : "Cloudflare";
                _shihabConfig.ShihabCustomDohUrl = _shihabCustomDohBox.Text.Trim();
                _shihabConfig.ShihabPlainDnsServer = string.IsNullOrWhiteSpace(_shihabPlainDnsBox.Text) ? "1.1.1.1" : _shihabPlainDnsBox.Text.Trim();
                _shihabConfig.ShihabNextDnsConfigId = _shihabNextDnsBox.Text.Trim();

                _shihabConfig.MehediDialUpConnectionName = _shihabDialConnBox.Text.Trim();
                _shihabConfig.ShihabRedialIfDisconnected = _mehediRedialChk.Checked;
                _shihabConfig.MehediHangUpWhenDone = _shihabHangUpChk.Checked;
                _shihabConfig.ShihabAutoResumeOnNetworkRestore = _shihabNetwatchChk.Checked;
                if (_shihabBindAdapterCombo.SelectedIndex <= 0)
                {
                    _shihabConfig.ShihabBoundNetworkAdapter = "";
                }
                else
                {
                    string rawSel = _shihabBindAdapterCombo.SelectedItem.ToString();
                    int parenIdx = rawSel.IndexOf(" (", StringComparison.Ordinal);
                    _shihabConfig.ShihabBoundNetworkAdapter = parenIdx > 0 ? rawSel.Substring(0, parenIdx).Trim() : rawSel.Trim();
                }

                _shihabConfig.MehediPlaySoundComplete = _mehediSndCompChk.Checked;
                _shihabConfig.ShihabSoundCompleteWav = _shihabSndCompBox.Text.Trim();
                _shihabConfig.MehediPlaySoundError = _shihabSndErrChk.Checked;
                _shihabConfig.ShihabSoundErrorWav = _mehediSndErrBox.Text.Trim();

                _shihabConfig.ShihabAutoCheckUpdates = _shihabAutoUpdChk.Checked;
                _shihabConfig.ShihabUpdateIncludePrereleases = _shihabPreRelChk.Checked;
                _shihabConfig.ShihabTorrentSequentialDefault = _shihabTorSeqDefChk.Checked;
                _shihabConfig.ShihabTorrentKeepSeedingDefault = _shihabTorSeedDefChk.Checked;
                _shihabConfig.ShihabTorrentBlocklistUrl = _shihabBlkUrlBox.Text.Trim();
                _shihabConfig.ShihabCrashMonitorEnabled = _shihabCrashMonChk.Checked;

                ShihabSecretsVault.SealConfigurationSecrets(_shihabConfig);
                _mehediStore.SaveMehediConfig(_shihabConfig);
                ShihabSecretsVault.UnsealConfigurationSecrets(_shihabConfig);
                _committedOk = true;
                DialogResult = DialogResult.OK;
                Close();
            };

            Button mehediCancel = ShihabThemePalette.CreateMehediStyledButton("Cancel", _surfColor, ForeColor, 102);
            mehediCancel.Location = new Point(442, 478);
            mehediCancel.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            Button shihabHelpBtn = ShihabThemePalette.CreateMehediStyledButton("Help", _surfColor, ForeColor, 100);
            shihabHelpBtn.Location = new Point(552, 478);
            shihabHelpBtn.Click += delegate
            {
                MessageBox.Show(
                    this,
                    "MHS Download Manager 1.0.6 Configuration\nDeveloper: Mehedi Hasan Shihab (https://sshihabb007.github.io/)\n\n" +
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
                    list.BeginUpdate();
                    list.Items.Clear();
                    if (_shihabConfig.ShihabHostConnectionLimits == null) _shihabConfig.ShihabHostConnectionLimits = new List<ShihabHostConnectionLimit>();
                    for (int i = 0; i < _shihabConfig.ShihabHostConnectionLimits.Count; i++)
                    {
                        ShihabHostConnectionLimit lim = _shihabConfig.ShihabHostConnectionLimits[i];
                        list.Items.Add(new ListViewItem(new string[] { lim.ShihabHostPattern, lim.MehediMaxConnections.ToString() }));
                    }
                    list.EndUpdate();
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
    }
}

