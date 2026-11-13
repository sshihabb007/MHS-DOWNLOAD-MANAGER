using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public class MehediAddUrlAuthDialog : Form
    {
        public string ShihabEnteredUrl { get; private set; }
        public string MehediEnteredUser { get; private set; }
        public string ShihabEnteredPassword { get; private set; }
        public string MehediEnteredReferer { get; private set; }

        private TextBox _shihabUrlBox;
        private TextBox _mehediRefererBox;
        private CheckBox _shihabAuthCheck;
        private TextBox _mehediUserBox;
        private TextBox _shihabPassBox;

        public MehediAddUrlAuthDialog(bool shihabDark, string mehediInitialUrl)
        {
            Text = "Add URL — " + ShihabThemePalette.ShihabMainHeaderTitle;
            Size = new Size(580, 275);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ShihabThemePalette.GetMehediWindowBg(shihabDark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(shihabDark);
            Font = ShihabThemePalette.UiRegular9Font;
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, shihabDark);

            Label shihabAddrLbl = new Label { Text = "Address (URL):", Location = new Point(16, 18), AutoSize = true };
            _shihabUrlBox = new TextBox
            {
                Location = new Point(115, 15),
                Width = 430,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark),
                ForeColor = ForeColor,
                Text = mehediInitialUrl ?? ""
            };

            if (string.IsNullOrWhiteSpace(_shihabUrlBox.Text))
            {
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        string mehediClip = Clipboard.GetText().Trim();
                        if (mehediClip.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            mehediClip.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                            mehediClip.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                            mehediClip.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase) ||
                            mehediClip.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
                        {
                            _shihabUrlBox.Text = mehediClip;
                        }
                    }
                }
                catch { }
            }

            Label mehediRefLbl = new Label { Text = "Referer (opt.):", Location = new Point(16, 54), AutoSize = true };
            _mehediRefererBox = new TextBox
            {
                Location = new Point(115, 51),
                Width = 430,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark),
                ForeColor = ForeColor
            };

            _shihabAuthCheck = new CheckBox
            {
                Text = "Use authorization (HTTP Basic Login)",
                Location = new Point(115, 86),
                AutoSize = true
            };

            Label shihabUserLbl = new Label { Text = "Login:", Location = new Point(115, 118), AutoSize = true };
            _mehediUserBox = new TextBox
            {
                Location = new Point(170, 115),
                Width = 145,
                Enabled = false,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark),
                ForeColor = ForeColor
            };

            Label mehediPassLbl = new Label { Text = "Password:", Location = new Point(328, 118), AutoSize = true };
            _shihabPassBox = new TextBox
            {
                Location = new Point(398, 115),
                Width = 147,
                Enabled = false,
                UseSystemPasswordChar = true,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark),
                ForeColor = ForeColor
            };

            _shihabAuthCheck.CheckedChanged += delegate
            {
                _mehediUserBox.Enabled = _shihabAuthCheck.Checked;
                _shihabPassBox.Enabled = _shihabAuthCheck.Checked;
            };

            Button shihabOkBtn = ShihabThemePalette.CreateMehediStyledButton("OK", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 105);
            shihabOkBtn.Location = new Point(328, 178);
            shihabOkBtn.Click += delegate
            {
                string mehediU = (_shihabUrlBox.Text ?? "").Trim();
                bool isMagnet = mehediU.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase);
                bool hasControl = ShihabProcessAndPathSafety.ContainsControlOrNewline(mehediU);
                Uri shihabParsed;
                bool isValidScheme = !hasControl && !isMagnet &&
                    Uri.TryCreate(mehediU, UriKind.Absolute, out shihabParsed) &&
                    shihabParsed != null &&
                    (string.Equals(shihabParsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(shihabParsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(shihabParsed.Scheme, Uri.UriSchemeFtp, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(shihabParsed.Scheme, "ftps", StringComparison.OrdinalIgnoreCase)) &&
                    !string.IsNullOrWhiteSpace(shihabParsed.Host);
                if (hasControl || (!isMagnet && !isValidScheme))
                {
                    MessageBox.Show(this, "Invalid URL entered. Please enter a valid http://, https://, ftp://, ftps://, or magnet:? address.", "MHS Download Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                string enteredReferer = (_mehediRefererBox.Text ?? "").Trim();
                if (!string.IsNullOrEmpty(enteredReferer))
                {
                    Uri refUri;
                    if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(enteredReferer, true, out refUri))
                    {
                        MessageBox.Show(this, "Referer must be a valid http:// or https:// URL without control characters.", "MHS Download Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                ShihabEnteredUrl = mehediU;
                MehediEnteredReferer = enteredReferer;
                MehediEnteredUser = _shihabAuthCheck.Checked ? _mehediUserBox.Text.Trim() : "";
                ShihabEnteredPassword = _shihabAuthCheck.Checked ? _shihabPassBox.Text : "";
                DialogResult = DialogResult.OK;
                Close();
            };

            Button mehediCancelBtn = ShihabThemePalette.CreateMehediStyledButton("Cancel", ShihabThemePalette.GetShihabSurfaceBg(shihabDark), ForeColor, 105);
            mehediCancelBtn.Location = new Point(440, 178);
            mehediCancelBtn.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            AcceptButton = shihabOkBtn;
            CancelButton = mehediCancelBtn;
            Controls.AddRange(new Control[]
            {
                shihabAddrLbl, _shihabUrlBox, mehediRefLbl, _mehediRefererBox,
                _shihabAuthCheck, shihabUserLbl, _mehediUserBox, mehediPassLbl, _shihabPassBox,
                shihabOkBtn, mehediCancelBtn
            });
        }
    }

    public class MehediDownloadFileInfoForm : ShihabChromeForm
    {
        protected override bool ShihabResizableChrome
        {
            get { return false; }
        }

        public bool ShihabStartImmediatelySelected { get; private set; }
        public ShihabDownloadItem MehediConfiguredItem { get; private set; }

        private readonly MehediAppConfiguration _shihabConfig;
        private readonly MehediDynamicSegmentEngine _mehediEngine;
        private TextBox _shihabUrlBox;
        private ComboBox _mehediCategoryCombo;
        private TextBox _shihabSaveAsBox;
        private TextBox _mehediDescBox;
        private ComboBox _shihabQueueCombo;
        private CheckBox _mehediRememberPathCheck;
        private Label _shihabSizeBadgeLabel;
        private TextBox _shihabExpectedChecksumBox;
        private CheckBox _shihabAutoExtractCheck;
        private CheckBox _shihabPrivateIncognitoCheck;
        private CheckBox _shihabBypassDebridCheck;

        public MehediDownloadFileInfoForm(
            ShihabDownloadItem shihabDraftItem,
            MehediAppConfiguration mehediConfig,
            MehediDynamicSegmentEngine shihabEngine)
        {
            MehediConfiguredItem = shihabDraftItem;
            _shihabConfig = mehediConfig;
            _mehediEngine = shihabEngine;
            bool shihabDark = mehediConfig.ShihabDarkMode;

            Text = "Download File Info — " + ShihabThemePalette.ShihabMainHeaderTitle;
            Size = new Size(626, 402);
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            Resize += delegate
            {
                try
                {
                    bool minimized = WindowState == FormWindowState.Minimized;
                    if (TopMost == minimized)
                    {
                        TopMost = !minimized;
                    }
                }
                catch { }
            };
            BackColor = ShihabThemePalette.GetMehediWindowBg(shihabDark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(shihabDark);
            Font = ShihabThemePalette.UiRegular9Font;
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, shihabDark);
            ApplyShihabChromeTheme(shihabDark, shihabDark ? Color.FromArgb(46, 52, 66) : Color.FromArgb(182, 193, 208));
            Icon = ShihabThemePalette.CreateShihabAppIcon();

            ShihabChromeTitleBar mehediInfoTitleBar = new ShihabChromeTitleBar(this, false, true, null, null);
            mehediInfoTitleBar.ApplyTheme(
                shihabDark,
                shihabDark ? Color.FromArgb(22, 24, 30) : Color.FromArgb(240, 244, 249),
                shihabDark ? Color.FromArgb(36, 40, 50) : Color.FromArgb(214, 222, 234));
            Panel shihabInfoContentHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BackColor,
                ForeColor = ForeColor
            };

            Label mehediUrlLbl = new Label { Text = "URL:", Location = new Point(18, 18), AutoSize = true };
            _shihabUrlBox = new TextBox
            {
                Location = new Point(110, 15),
                Width = 495,
                ReadOnly = true,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark),
                ForeColor = ForeColor,
                Text = shihabDraftItem.MehediUrl
            };

            Label shihabCatLbl = new Label { Text = "Category:", Location = new Point(18, 54), AutoSize = true };
            _mehediCategoryCombo = new ComboBox
            {
                Location = new Point(110, 51),
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark),
                ForeColor = ForeColor
            };

            ShihabCategoryRule mehediAutoCat = _shihabConfig.ResolveMehediCategoryForFile(shihabDraftItem.ShihabFileName);
            int shihabSelIdx = 0;
            for (int mehediI = 0; mehediI < _shihabConfig.ShihabCategories.Count; mehediI++)
            {
                string shihabCName = _shihabConfig.ShihabCategories[mehediI].ShihabName;
                _mehediCategoryCombo.Items.Add(shihabCName);
                if (string.Equals(shihabCName, mehediAutoCat.ShihabName, StringComparison.OrdinalIgnoreCase))
                {
                    shihabSelIdx = mehediI;
                }
            }
            if (_mehediCategoryCombo.Items.Count > 0)
            {
                _mehediCategoryCombo.SelectedIndex = shihabSelIdx;
            }

            long mehediInitDisplayBytes = shihabDraftItem.ShihabTotalBytes > 0
                ? (shihabDraftItem.ShihabTotalBytes + (shihabDraftItem.ShihabAudioCompanionBytes > 0 ? shihabDraftItem.ShihabAudioCompanionBytes : 0L))
                : -1L;
            string shihabInitMuxSuffix = !string.IsNullOrWhiteSpace(shihabDraftItem.MehediAudioCompanionUrl) ? "  [Video + Audio]" : "";

            _shihabSizeBadgeLabel = new Label
            {
                Text = mehediInitDisplayBytes > 0
                    ? ("Size: " + ShihabFormatHelpers.FormatMehediBytes(mehediInitDisplayBytes) + shihabInitMuxSuffix)
                    : "Probing server size & resume...",
                Location = new Point(345, 54),
                AutoSize = true,
                Font = ShihabThemePalette.UiSemibold9Font,
                ForeColor = ShihabThemePalette.GetShihabAccentBlue()
            };

            Label mehediSaveLbl = new Label { Text = "Save As:", Location = new Point(18, 92), AutoSize = true };
            string shihabInitFolder = string.IsNullOrWhiteSpace(shihabDraftItem.MehediSaveDirectory)
                ? mehediAutoCat.ShihabDefaultFolder
                : shihabDraftItem.MehediSaveDirectory;
            string mehediUniqueFile = ShihabFormatHelpers.EnsureShihabUniqueFilePath(shihabInitFolder, shihabDraftItem.ShihabFileName);

            _shihabSaveAsBox = new TextBox
            {
                Location = new Point(110, 89),
                Width = 405,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark),
                ForeColor = ForeColor,
                Text = Path.Combine(shihabInitFolder, mehediUniqueFile)
            };

            Button shihabBrowseBtn = ShihabThemePalette.CreateMehediStyledButton("Browse...", ShihabThemePalette.GetShihabSurfaceBg(shihabDark), ForeColor, 82);
            shihabBrowseBtn.Height = 26;
            shihabBrowseBtn.Location = new Point(523, 87);
            shihabBrowseBtn.Click += delegate
            {
                using (SaveFileDialog mehediSfd = new SaveFileDialog())
                {
                    mehediSfd.FileName = Path.GetFileName(_shihabSaveAsBox.Text);
                    string shihabCurDir = Path.GetDirectoryName(_shihabSaveAsBox.Text);
                    if (!string.IsNullOrEmpty(shihabCurDir) && Directory.Exists(shihabCurDir)) mehediSfd.InitialDirectory = shihabCurDir;
                    mehediSfd.Filter = "All Files (*.*)|*.*";
                    if (mehediSfd.ShowDialog(this) == DialogResult.OK)
                    {
                        _shihabSaveAsBox.Text = mehediSfd.FileName;
                    }
                }
            };

            _mehediRememberPathCheck = new CheckBox
            {
                Text = "Change folder for \"" + mehediAutoCat.ShihabName + "\" category on last selected",
                Location = new Point(110, 122),
                AutoSize = true,
                Checked = mehediAutoCat.MehediRememberLastSavePath
            };

            _mehediCategoryCombo.SelectedIndexChanged += delegate
            {
                int mehediIdx = _mehediCategoryCombo.SelectedIndex;
                if (mehediIdx >= 0 && mehediIdx < _shihabConfig.ShihabCategories.Count)
                {
                    ShihabCategoryRule shihabChosenCat = _shihabConfig.ShihabCategories[mehediIdx];
                    string shihabNewDir = shihabChosenCat.ShihabDefaultFolder;
                    string mehediCurLeaf = Path.GetFileName(_shihabSaveAsBox.Text);
                    _shihabSaveAsBox.Text = Path.Combine(shihabNewDir, mehediCurLeaf);
                    _mehediRememberPathCheck.Text = "Change folder for \"" + shihabChosenCat.ShihabName + "\" category on last selected";
                    _mehediRememberPathCheck.Checked = shihabChosenCat.MehediRememberLastSavePath;
                }
            };

            Label shihabDescLbl = new Label { Text = "Description:", Location = new Point(18, 156), AutoSize = true };
            _mehediDescBox = new TextBox
            {
                Location = new Point(110, 153),
                Width = 495,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark),
                ForeColor = ForeColor,
                Text = shihabDraftItem.ShihabDescription ?? ""
            };

            Label mehediQueueLbl = new Label { Text = "Queue (Later):", Location = new Point(18, 194), AutoSize = true };
            _shihabQueueCombo = new ComboBox
            {
                Location = new Point(110, 191),
                Width = 240,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark),
                ForeColor = ForeColor
            };
            for (int shihabQ = 0; shihabQ < _shihabConfig.MehediQueues.Count; shihabQ++)
            {
                _shihabQueueCombo.Items.Add(_shihabConfig.MehediQueues[shihabQ].ShihabQueueName);
            }
            if (_shihabQueueCombo.Items.Count > 0) _shihabQueueCombo.SelectedIndex = 0;

            _shihabAutoExtractCheck = new CheckBox
            {
                Text = "Auto-extract archive on completion",
                Location = new Point(365, 193),
                AutoSize = true,
                Checked = shihabDraftItem.ShihabAutoExtract || _shihabConfig.ShihabAutoExtractArchives
            };

            Label shihabChkLbl = new Label { Text = "Checksum (opt):", Location = new Point(18, 230), AutoSize = true };
            _shihabExpectedChecksumBox = new TextBox
            {
                Location = new Point(110, 227),
                Width = 495,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark),
                ForeColor = ForeColor,
                Text = shihabDraftItem.ShihabExpectedChecksum ?? ""
            };

            _shihabPrivateIncognitoCheck = new CheckBox
            {
                Text = "Private / Incognito (ephemeral — omit from saved history)",
                Location = new Point(110, 264),
                AutoSize = true,
                Checked = shihabDraftItem.ShihabIsPrivateIncognito
            };

            _shihabBypassDebridCheck = new CheckBox
            {
                Text = "Bypass Debrid",
                Location = new Point(470, 264),
                AutoSize = true,
                Checked = shihabDraftItem.ShihabBypassDebrid
            };

            Button mehediLaterBtn = ShihabThemePalette.CreateMehediStyledButton("Download Later", ShihabThemePalette.GetShihabSurfaceBg(shihabDark), ForeColor, 138);
            mehediLaterBtn.Location = new Point(180, 312);
            mehediLaterBtn.Click += delegate { CommitShihabChoicesAndClose(false); };

            Button shihabStartBtn = ShihabThemePalette.CreateMehediStyledButton("Start Download", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 155);
            shihabStartBtn.Location = new Point(328, 312);
            shihabStartBtn.Click += delegate { CommitShihabChoicesAndClose(true); };

            Button mehediCancelBtn = ShihabThemePalette.CreateMehediStyledButton("Cancel", ShihabThemePalette.GetShihabSurfaceBg(shihabDark), ForeColor, 110);
            mehediCancelBtn.Location = new Point(495, 312);
            mehediCancelBtn.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            AcceptButton = shihabStartBtn;
            CancelButton = mehediCancelBtn;

            shihabInfoContentHost.Controls.AddRange(new Control[]
            {
                mehediUrlLbl, _shihabUrlBox, shihabCatLbl, _mehediCategoryCombo, _shihabSizeBadgeLabel,
                mehediSaveLbl, _shihabSaveAsBox, shihabBrowseBtn, _mehediRememberPathCheck,
                shihabDescLbl, _mehediDescBox, mehediQueueLbl, _shihabQueueCombo,
                _shihabAutoExtractCheck, shihabChkLbl, _shihabExpectedChecksumBox,
                _shihabPrivateIncognitoCheck, _shihabBypassDebridCheck,
                mehediLaterBtn, shihabStartBtn, mehediCancelBtn
            });
            Controls.Add(shihabInfoContentHost);
            Controls.Add(mehediInfoTitleBar);

            Shown += delegate
            {
                try
                {
                    TopMost = true;
                    Activate();
                    BringToFront();
                }
                catch { }
                BeginBackgroundShihabProbe();
            };
        }

        private void BeginBackgroundShihabProbe()
        {
            Task.Run(delegate
            {
                ShihabProbeMetadata mehediProbe = _mehediEngine.ProbeMehediUrl(MehediConfiguredItem.MehediUrl, MehediConfiguredItem);
                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        if (IsDisposed) return;
                        if (mehediProbe.MehediSuccess)
                        {
                            if (mehediProbe.ShihabContentLength > 0)
                            {
                                MehediConfiguredItem.ShihabTotalBytes = mehediProbe.ShihabContentLength;
                            }
                            MehediConfiguredItem.ShihabResumeSupported = mehediProbe.MehediAcceptsRanges;
                            long mehediCombinedBytes = MehediConfiguredItem.ShihabTotalBytes > 0
                                ? (MehediConfiguredItem.ShihabTotalBytes + (MehediConfiguredItem.ShihabAudioCompanionBytes > 0 ? MehediConfiguredItem.ShihabAudioCompanionBytes : 0L))
                                : -1L;
                            string shihabMuxTag = !string.IsNullOrWhiteSpace(MehediConfiguredItem.MehediAudioCompanionUrl) ? " · Video+Audio" : "";
                            string shihabSizeStr = mehediCombinedBytes > 0
                                ? ShihabFormatHelpers.FormatMehediBytes(mehediCombinedBytes)
                                : (mehediProbe.MehediIsHlsPlaylist ? "HLS Video Stream (.m3u8)" : "Stream / Unknown");
                            string mehediResStr = mehediProbe.MehediAcceptsRanges ? "Resume: Yes" : "Resume: No";
                            string shihabGeoTag = !string.IsNullOrWhiteSpace(mehediProbe.ShihabServerCountryCode)
                                ? (" · [" + mehediProbe.ShihabServerCountryCode + "]")
                                : "";
                            _shihabSizeBadgeLabel.Text = string.Format("Size: {0}  [{1}{2}{3}]", shihabSizeStr, mehediResStr, shihabMuxTag, shihabGeoTag);

                            string shihabCurLeaf = Path.GetFileName(_shihabSaveAsBox.Text);
                            if ((shihabCurLeaf == "download.bin" || shihabCurLeaf == "shihab_download.bin") &&
                                !string.IsNullOrWhiteSpace(mehediProbe.MehediSuggestedFileName))
                            {
                                ShihabCategoryRule mehediResolvedCat = _shihabConfig.ResolveMehediCategoryForFile(mehediProbe.MehediSuggestedFileName);
                                for (int shihabI = 0; shihabI < _mehediCategoryCombo.Items.Count; shihabI++)
                                {
                                    if (string.Equals(_mehediCategoryCombo.Items[shihabI].ToString(), mehediResolvedCat.ShihabName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        _mehediCategoryCombo.SelectedIndex = shihabI;
                                        break;
                                    }
                                }
                                string mehediDir = mehediResolvedCat.ShihabDefaultFolder;
                                _shihabSaveAsBox.Text = Path.Combine(
                                    mehediDir,
                                    ShihabFormatHelpers.EnsureShihabUniqueFilePath(mehediDir, mehediProbe.MehediSuggestedFileName));
                            }
                        }
                        else if (MehediConfiguredItem.ShihabTotalBytes > 0)
                        {
                            long mehediKnownTotal = MehediConfiguredItem.ShihabTotalBytes +
                                (MehediConfiguredItem.ShihabAudioCompanionBytes > 0 ? MehediConfiguredItem.ShihabAudioCompanionBytes : 0L);
                            string shihabMuxTag = !string.IsNullOrWhiteSpace(MehediConfiguredItem.MehediAudioCompanionUrl) ? " · Video+Audio" : "";
                            _shihabSizeBadgeLabel.Text = string.Format("Size: {0}  [Browser Bridge{1}]", ShihabFormatHelpers.FormatMehediBytes(mehediKnownTotal), shihabMuxTag);
                        }
                        else
                        {
                            _shihabSizeBadgeLabel.Text = "Size: Resolving via Browser Bridge...";
                        }
                    });
                }
                catch { }
            });
        }

        private void CommitShihabChoicesAndClose(bool mehediStartNow)
        {
            string shihabFullSave = (_shihabSaveAsBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(shihabFullSave))
            {
                MessageBox.Show(this, "Please specify a valid Save As file path.", "MHS Download Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string mehediFolder;
            string shihabLeaf;
            try
            {
                mehediFolder = Path.GetDirectoryName(shihabFullSave);
                shihabLeaf = ShihabFormatHelpers.SanitizeShihabFileName(Path.GetFileName(shihabFullSave));
                if (string.IsNullOrWhiteSpace(mehediFolder))
                {
                    mehediFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                }
                Directory.CreateDirectory(mehediFolder);
            }
            catch (Exception pathEx)
            {
                MessageBox.Show(this, "Invalid Save As path:\r\n" + pathEx.Message, "MHS Download Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MehediConfiguredItem.MehediSaveDirectory = mehediFolder;
            MehediConfiguredItem.ShihabFileName = shihabLeaf;
            MehediConfiguredItem.ShihabCategoryName = _mehediCategoryCombo.SelectedItem != null
                ? _mehediCategoryCombo.SelectedItem.ToString()
                : "General";
            MehediConfiguredItem.ShihabDescription = _mehediDescBox.Text.Trim();
            MehediConfiguredItem.ShihabExpectedChecksum = _shihabExpectedChecksumBox != null ? _shihabExpectedChecksumBox.Text.Trim() : "";
            MehediConfiguredItem.ShihabAutoExtract = _shihabAutoExtractCheck != null && _shihabAutoExtractCheck.Checked;
            MehediConfiguredItem.ShihabIsPrivateIncognito = _shihabPrivateIncognitoCheck != null && _shihabPrivateIncognitoCheck.Checked;
            MehediConfiguredItem.ShihabBypassDebrid = _shihabBypassDebridCheck != null && _shihabBypassDebridCheck.Checked;
            MehediConfiguredItem.MehediMaxConnections = _shihabConfig.ShihabDefaultMaxConnections;

            if (!mehediStartNow)
            {
                MehediConfiguredItem.MehediQueueName = _shihabQueueCombo.SelectedItem != null
                    ? _shihabQueueCombo.SelectedItem.ToString()
                    : "Main Download Queue";
                MehediConfiguredItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued");
            }

            if (_mehediRememberPathCheck.Checked)
            {
                for (int shihabI = 0; shihabI < _shihabConfig.ShihabCategories.Count; shihabI++)
                {
                    if (string.Equals(_shihabConfig.ShihabCategories[shihabI].ShihabName, MehediConfiguredItem.ShihabCategoryName, StringComparison.OrdinalIgnoreCase))
                    {
                        _shihabConfig.ShihabCategories[shihabI].ShihabDefaultFolder = mehediFolder;
                        break;
                    }
                }
            }

            ShihabStartImmediatelySelected = mehediStartNow;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

