using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    // =========================================================================
    // 3. RSS / ATOM FEED READER & AUTO-DOWNLOADER DIALOG
    // =========================================================================
    public class ShihabRssFeedDialog : Form
    {
        public List<string> SelectedEnclosureUrls { get; private set; }
        private int _fetchGeneration;

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
            Font = ShihabThemePalette.UiRegular9Font;
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

            Button fetchNowBtn = ShihabThemePalette.CreateMehediStyledButton("Fetch Feed Now", surf, ForeColor, 130);
            fetchNowBtn.Location = new Point(16, 418);

            Action reloadFeeds = delegate
            {
                savedFeedsBox.BeginUpdate();
                try
                {
                    savedFeedsBox.Items.Clear();
                    if (config.ShihabRssFeeds == null) config.ShihabRssFeeds = new List<ShihabRssFeedConfig>();
                    for (int i = 0; i < config.ShihabRssFeeds.Count; i++)
                    {
                        savedFeedsBox.Items.Add(config.ShihabRssFeeds[i].ShihabFeedTitle + " (" + config.ShihabRssFeeds[i].ShihabFeedUrl + ")");
                    }
                }
                finally
                {
                    savedFeedsBox.EndUpdate();
                }
            };
            reloadFeeds();

            Action<string, string, string> fetchAndDisplayFeed = delegate(string url, string pattern, string exclPattern)
            {
                Uri parsedFeedUri;
                if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(url, true, out parsedFeedUri))
                {
                    MessageBox.Show(this, "Please enter a valid HTTP or HTTPS feed URL.", "RSS Feed Reader", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int myGen = Interlocked.Increment(ref _fetchGeneration);
                entriesView.Items.Clear();
                fetchNowBtn.Enabled = false;
                fetchNowBtn.Text = "Fetching...";
                string ua = config.ShihabDefaultUserAgent;

                Task.Run(delegate
                {
                    List<ShihabRssItemEntry> items = ShihabRssFeedManager.FetchFeedItems(url, ua);
                    List<ShihabRssItemEntry> filtered = new List<ShihabRssItemEntry>();
                    TimeSpan rxTimeout = TimeSpan.FromMilliseconds(150);

                    for (int i = 0; i < items.Count; i++)
                    {
                        ShihabRssItemEntry it = items[i];
                        if (!string.IsNullOrWhiteSpace(pattern) && pattern != "*")
                        {
                            try
                            {
                                if (!Regex.IsMatch(it.Title ?? "", pattern, RegexOptions.IgnoreCase, rxTimeout) &&
                                    !Regex.IsMatch(it.DownloadUrl ?? "", pattern, RegexOptions.IgnoreCase, rxTimeout))
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
                                if (Regex.IsMatch(it.Title ?? "", exclPattern, RegexOptions.IgnoreCase, rxTimeout) ||
                                    Regex.IsMatch(it.DownloadUrl ?? "", exclPattern, RegexOptions.IgnoreCase, rxTimeout))
                                {
                                    continue;
                                }
                            }
                            catch { }
                        }
                        filtered.Add(it);
                    }

                    try
                    {
                        if (IsDisposed || !IsHandleCreated) return;
                        BeginInvoke((Action)delegate
                        {
                            if (IsDisposed || myGen != Volatile.Read(ref _fetchGeneration)) return;
                            fetchNowBtn.Enabled = true;
                            fetchNowBtn.Text = "Fetch Feed Now";

                            entriesView.BeginUpdate();
                            try
                            {
                                entriesView.Items.Clear();
                                for (int i = 0; i < filtered.Count; i++)
                                {
                                    ShihabRssItemEntry it = filtered[i];
                                    ListViewItem row = new ListViewItem(new string[] { it.Title, it.DownloadUrl, it.PublishedDate });
                                    row.Checked = true;
                                    row.Tag = it.DownloadUrl;
                                    entriesView.Items.Add(row);
                                }
                            }
                            finally
                            {
                                entriesView.EndUpdate();
                            }
                        });
                    }
                    catch { }
                });
            };

            addFeedBtn.Click += delegate
            {
                string u = feedUrlBox.Text.Trim();
                if (string.IsNullOrEmpty(u) || u == "https://") return;
                Uri parsedFeedUri;
                if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(u, true, out parsedFeedUri))
                {
                    MessageBox.Show(this, "Please enter a valid HTTP or HTTPS feed URL.", "RSS Feed Reader", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
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
        private Font _enableFont;
        private Font _urlFont;
        private Font _codeFont;
        private PictureBox _qrBox;

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
            Font = ShihabThemePalette.UiRegular9Font;
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, dark);

            _enableFont = new Font("Segoe UI Semibold", 9.5f);
            _urlFont = new Font("Consolas", 10f, FontStyle.Bold);
            _codeFont = new Font("Consolas", 18f, FontStyle.Bold);

            CheckBox enableRemoteChk = new CheckBox
            {
                Text = "Enable LAN Remote Control Web Dashboard & REST API",
                Location = new Point(18, 18),
                AutoSize = true,
                Font = _enableFont,
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

            _qrBox = new PictureBox
            {
                Location = new Point(18, 56),
                Size = new Size(180, 180),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White,
                Image = ShihabQrCodeEncoder.RenderBitmap(lanUrl + "/?code=" + config.ShihabRemotePairingCode, 6, 4)
            };

            Label urlTitleLbl = new Label { Text = "LAN Dashboard URL:", Location = new Point(216, 56), AutoSize = true };
            TextBox urlBox = new TextBox { Location = new Point(216, 78), Width = 385, ReadOnly = true, Text = lanUrl, BackColor = surf, ForeColor = ShihabThemePalette.GetShihabAccentBlue(), Font = _urlFont };

            Label codeTitleLbl = new Label { Text = "8-Digit Pairing Code (scan QR or enter on phone):", Location = new Point(216, 114), AutoSize = true };
            Label codeBigLbl = new Label
            {
                Text = config.ShihabRemotePairingCode,
                Location = new Point(216, 136),
                AutoSize = true,
                Font = _codeFont,
                ForeColor = ShihabThemePalette.GetMehediAccentGreen()
            };

            Action refreshQrImage = delegate
            {
                lanUrl = ShihabRemoteControlServer.GetLocalLanDashboardUrl((int)portNum.Value);
                urlBox.Text = lanUrl;
                Image oldImg = _qrBox.Image;
                _qrBox.Image = ShihabQrCodeEncoder.RenderBitmap(lanUrl + "/?code=" + config.ShihabRemotePairingCode, 6, 4);
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
                ShihabThemePalette.SafeLaunchExternalUrl("http://127.0.0.1:" + (int)portNum.Value + "/");
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
                enableRemoteChk, portLbl, portNum, _qrBox, urlTitleLbl, urlBox,
                codeTitleLbl, codeBigLbl, regenBtn, mcpNoteLbl,
                openBrowserBtn, saveBtn, closeBtn
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_qrBox != null && _qrBox.Image != null)
                {
                    _qrBox.Image.Dispose();
                    _qrBox.Image = null;
                }
                if (_enableFont != null) { _enableFont.Dispose(); _enableFont = null; }
                if (_urlFont != null) { _urlFont.Dispose(); _urlFont = null; }
                if (_codeFont != null) { _codeFont.Dispose(); _codeFont = null; }
            }
            base.Dispose(disposing);
        }
    }

    // =========================================================================
    // 5. STANDALONE CHECKSUM VERIFIER & SAFE ARCHIVE EXTRACTOR DIALOG
    // =========================================================================
    public class ShihabChecksumAndExtractDialog : Form
    {
        private Font _monoFont;

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
            Font = ShihabThemePalette.UiRegular9Font;
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, dark);

            _monoFont = new Font("Consolas", 9f);

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
                Font = _monoFont
            };

            Button computeAllBtn = ShihabThemePalette.CreateMehediStyledButton("Compute Hash", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 140);
            computeAllBtn.Location = new Point(16, 326);

            Button extractBtn = ShihabThemePalette.CreateMehediStyledButton("Safe Extract Archive", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 165);
            extractBtn.Location = new Point(166, 326);

            computeAllBtn.Click += delegate
            {
                string f = fileBox.Text.Trim();
                if (ShihabProcessAndPathSafety.ContainsControlOrNewline(f) || !File.Exists(f))
                {
                    MessageBox.Show(this, "Please select an existing file first.", "Checksum Verifier", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                string algo = algoCombo.SelectedItem.ToString();
                string expectedText = expBox.Text.Trim();

                computeAllBtn.Enabled = false;
                extractBtn.Enabled = false;
                resultBox.Text = "Computing " + algo + " checksum...";

                Task.Run(delegate
                {
                    string digest = ShihabChecksumVerifier.ComputeFileChecksumHex(f, algo);
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("File:      " + f);
                    sb.AppendLine("Algorithm: " + algo);
                    sb.AppendLine("Digest:    " + digest);
                    if (!string.IsNullOrWhiteSpace(expectedText))
                    {
                        string parsedAlgo, comp, err;
                        bool ok = ShihabChecksumVerifier.VerifyExpectedChecksum(f, expectedText, out parsedAlgo, out comp, out err);
                        sb.AppendLine("Status:    " + (ok ? "MATCH VERIFIED ✔" : ("MISMATCH ✘ — " + err)));
                    }
                    string output = sb.ToString();

                    try
                    {
                        if (IsDisposed || !IsHandleCreated) return;
                        BeginInvoke((Action)delegate
                        {
                            if (IsDisposed) return;
                            computeAllBtn.Enabled = true;
                            extractBtn.Enabled = true;
                            resultBox.Text = output;
                        });
                    }
                    catch { }
                });
            };

            extractBtn.Click += delegate
            {
                string f = fileBox.Text.Trim();
                if (ShihabProcessAndPathSafety.ContainsControlOrNewline(f) || !File.Exists(f)) return;

                computeAllBtn.Enabled = false;
                extractBtn.Enabled = false;
                resultBox.Text = "Extracting archive safely (Zip-Slip & Bomb protection active)...";

                Task.Run(delegate
                {
                    string destDir, err;
                    bool ok = ShihabSafeArchiveExtractor.ExtractArchiveSafely(f, "Smart", false, "", out destDir, out err);
                    string output = ok
                        ? ("Archive safely extracted (Zip-Slip & 200x Bomb Protected) to:\r\n" + destDir)
                        : ("Archive extraction failed:\r\n" + err);

                    try
                    {
                        if (IsDisposed || !IsHandleCreated) return;
                        BeginInvoke((Action)delegate
                        {
                            if (IsDisposed) return;
                            computeAllBtn.Enabled = true;
                            extractBtn.Enabled = true;
                            resultBox.Text = output;
                        });
                    }
                    catch { }
                });
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

        protected override void Dispose(bool disposing)
        {
            if (disposing && _monoFont != null)
            {
                _monoFont.Dispose();
                _monoFont = null;
            }
            base.Dispose(disposing);
        }
    }

    // =========================================================================
    // 6. TELL A FRIEND / SHARE MHS DOWNLOAD MANAGER DIALOG
    // =========================================================================
    public class ShihabTellAFriendDialog : Form
    {
        private static readonly Regex SafeRepoSlugRegex = new Regex("^[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
        private Font _titleFont;
        private Font _linkFont;
        private PictureBox _qrBox;

        public ShihabTellAFriendDialog(MehediAppConfiguration config)
        {
            bool dark = config != null ? config.ShihabDarkMode : true;
            Color surf = ShihabThemePalette.GetShihabSurfaceBg(dark);
            string rawSlug = (config != null && !string.IsNullOrWhiteSpace(config.ShihabUpdateRepoSlug))
                ? config.ShihabUpdateRepoSlug.Trim()
                : ShihabAutoUpdater.DefaultRepoSlug;
            string repoSlug = SafeRepoSlugRegex.IsMatch(rawSlug) ? rawSlug : ShihabAutoUpdater.DefaultRepoSlug;
            string releaseUrl = "https://github.com/" + repoSlug + "/releases/latest";
            string repoUrl = "https://github.com/" + repoSlug;
            string inviteMessage =
                "Hey! Check out MHS Download Manager (v" + ShihabAutoUpdater.GetCurrentAppVersion() + ") — a super-fast free & open-source Windows download accelerator with multi-segment HTTP/HTTPS/FTP, YouTube 4K/8K, BitTorrent/Magnet, Scheduler & Queues, Browser Extension, and LAN Remote QR:\r\n" +
                releaseUrl;

            Text = "Tell a Friend — " + ShihabThemePalette.ShihabMainHeaderTitle;
            ClientSize = new Size(650, 388);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ShihabThemePalette.GetMehediWindowBg(dark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(dark);
            Font = ShihabThemePalette.UiRegular9Font;
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, dark);

            _titleFont = new Font("Segoe UI Semibold", 12f);
            _linkFont = new Font("Consolas", 9.5f, FontStyle.Bold);

            Label titleLbl = new Label
            {
                Text = "Share MHS Download Manager with a Friend",
                Location = new Point(18, 16),
                AutoSize = true,
                Font = _titleFont
            };
            Label subLbl = new Label
            {
                Text = "Scan the QR code, copy the direct installer link, or share via Email, X, WhatsApp, Telegram, or LinkedIn.",
                Location = new Point(18, 42),
                AutoSize = true,
                ForeColor = ShihabThemePalette.GetMehediMutedText(dark)
            };

            _qrBox = new PictureBox
            {
                Location = new Point(18, 74),
                Size = new Size(176, 176),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White,
                Image = ShihabQrCodeEncoder.RenderBitmap(releaseUrl, 5, 4)
            };

            Label linkLbl = new Label { Text = "Latest Installer Release URL:", Location = new Point(210, 74), AutoSize = true };
            TextBox linkBox = new TextBox
            {
                Location = new Point(210, 96),
                Width = 420,
                ReadOnly = true,
                Text = releaseUrl,
                BackColor = surf,
                ForeColor = ShihabThemePalette.GetShihabAccentBlue(),
                Font = _linkFont
            };

            Label msgLbl = new Label { Text = "Invite Message (editable):", Location = new Point(210, 128), AutoSize = true };
            TextBox msgBox = new TextBox
            {
                Location = new Point(210, 150),
                Size = new Size(420, 100),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Text = inviteMessage,
                BackColor = surf,
                ForeColor = ForeColor
            };

            Label statusToastLbl = new Label
            {
                Location = new Point(18, 260),
                Size = new Size(612, 22),
                ForeColor = ShihabThemePalette.GetMehediAccentGreen(),
                Font = ShihabThemePalette.UiSemibold9Font,
                Text = ""
            };

            Button copyLinkBtn = ShihabThemePalette.CreateMehediStyledButton("Copy Link", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 112);
            copyLinkBtn.Location = new Point(18, 288);
            copyLinkBtn.Click += delegate
            {
                try
                {
                    Clipboard.SetText(releaseUrl);
                    statusToastLbl.Text = "✔ Copied release download URL to clipboard!";
                }
                catch { }
            };

            Button copyMsgBtn = ShihabThemePalette.CreateMehediStyledButton("Copy Message", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 126);
            copyMsgBtn.Location = new Point(138, 288);
            copyMsgBtn.Click += delegate
            {
                try
                {
                    Clipboard.SetText(msgBox.Text);
                    statusToastLbl.Text = "✔ Copied invite message to clipboard!";
                }
                catch { }
            };

            Button emailBtn = ShihabThemePalette.CreateMehediStyledButton("Email", surf, ForeColor, 82);
            emailBtn.Location = new Point(272, 288);
            emailBtn.Click += delegate
            {
                string subj = Uri.EscapeDataString("Check out MHS Download Manager");
                string body = Uri.EscapeDataString(msgBox.Text);
                ShihabThemePalette.SafeLaunchExternalUrl("mailto:?subject=" + subj + "&body=" + body);
            };

            Button xShareBtn = ShihabThemePalette.CreateMehediStyledButton("X / Twitter", surf, ForeColor, 94);
            xShareBtn.Location = new Point(362, 288);
            xShareBtn.Click += delegate
            {
                string txt = Uri.EscapeDataString("MHS Download Manager — Free & Open-Source Windows Download Accelerator!");
                ShihabThemePalette.SafeLaunchExternalUrl("https://twitter.com/intent/tweet?text=" + txt + "&url=" + Uri.EscapeDataString(releaseUrl));
            };

            Button waShareBtn = ShihabThemePalette.CreateMehediStyledButton("WhatsApp", surf, ForeColor, 92);
            waShareBtn.Location = new Point(464, 288);
            waShareBtn.Click += delegate
            {
                ShihabThemePalette.SafeLaunchExternalUrl("https://wa.me/?text=" + Uri.EscapeDataString(msgBox.Text));
            };

            Button tgShareBtn = ShihabThemePalette.CreateMehediStyledButton("Telegram", surf, ForeColor, 86);
            tgShareBtn.Location = new Point(564, 288);
            tgShareBtn.Width = 68;
            tgShareBtn.Click += delegate
            {
                ShihabThemePalette.SafeLaunchExternalUrl("https://t.me/share/url?url=" + Uri.EscapeDataString(releaseUrl) + "&text=" + Uri.EscapeDataString("MHS Download Manager — Fast Windows Download Accelerator"));
            };

            Button liShareBtn = ShihabThemePalette.CreateMehediStyledButton("Share on LinkedIn", surf, ForeColor, 140);
            liShareBtn.Location = new Point(18, 334);
            liShareBtn.Click += delegate
            {
                ShihabThemePalette.SafeLaunchExternalUrl("https://www.linkedin.com/sharing/share-offsite/?url=" + Uri.EscapeDataString(releaseUrl));
            };

            Button starRepoBtn = ShihabThemePalette.CreateMehediStyledButton("★ Star on GitHub", surf, ForeColor, 140);
            starRepoBtn.Location = new Point(166, 334);
            starRepoBtn.Click += delegate
            {
                ShihabThemePalette.SafeLaunchExternalUrl(repoUrl);
            };

            Button closeBtn = ShihabThemePalette.CreateMehediStyledButton("Close", surf, ForeColor, 108);
            closeBtn.Location = new Point(522, 334);
            closeBtn.DialogResult = DialogResult.Cancel;
            closeBtn.Click += delegate { Close(); };

            CancelButton = closeBtn;
            Shown += delegate
            {
                try
                {
                    copyLinkBtn.Focus();
                    linkBox.SelectionStart = 0;
                    linkBox.SelectionLength = 0;
                }
                catch { }
            };
            Controls.AddRange(new Control[]
            {
                titleLbl, subLbl, _qrBox, linkLbl, linkBox, msgLbl, msgBox, statusToastLbl,
                copyLinkBtn, copyMsgBtn, emailBtn, xShareBtn, waShareBtn, tgShareBtn,
                liShareBtn, starRepoBtn, closeBtn
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_qrBox != null && _qrBox.Image != null)
                {
                    _qrBox.Image.Dispose();
                    _qrBox.Image = null;
                }
                if (_titleFont != null) { _titleFont.Dispose(); _titleFont = null; }
                if (_linkFont != null) { _linkFont.Dispose(); _linkFont = null; }
            }
            base.Dispose(disposing);
        }
    }

    // =========================================================================
    // 7. REGISTRATION & OPEN-SOURCE LICENSE STATUS DIALOG
    // =========================================================================
    public class ShihabRegistrationStatusDialog : Form
    {
        private static readonly Regex SafeRepoSlugRegex = new Regex("^[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
        public bool CheckUpdatesRequested { get; private set; }
        private Font _badgeFont;

        public ShihabRegistrationStatusDialog(MehediAppConfiguration config)
        {
            bool dark = config != null ? config.ShihabDarkMode : true;
            Color surf = ShihabThemePalette.GetShihabSurfaceBg(dark);
            string rawSlug = (config != null && !string.IsNullOrWhiteSpace(config.ShihabUpdateRepoSlug))
                ? config.ShihabUpdateRepoSlug.Trim()
                : ShihabAutoUpdater.DefaultRepoSlug;
            string repoSlug = SafeRepoSlugRegex.IsMatch(rawSlug) ? rawSlug : ShihabAutoUpdater.DefaultRepoSlug;
            string repoUrl = "https://github.com/" + repoSlug;
            string licenseUrl = repoUrl + "/blob/main/LICENSE";

            Text = "Registration & License — " + ShihabThemePalette.ShihabMainHeaderTitle;
            ClientSize = new Size(540, 290);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ShihabThemePalette.GetMehediWindowBg(dark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(dark);
            Font = ShihabThemePalette.UiRegular9Font;
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, dark);

            _badgeFont = new Font("Segoe UI", 11.5f, FontStyle.Bold);

            Label badgeLbl = new Label
            {
                Text = "✔ REGISTERED — FREE & OPEN-SOURCE EDITION",
                Location = new Point(20, 18),
                AutoSize = true,
                UseMnemonic = false,
                Font = _badgeFont,
                ForeColor = ShihabThemePalette.GetMehediAccentGreen()
            };

            ShihabRoundedCardPanel infoCard = new ShihabRoundedCardPanel
            {
                Location = new Point(20, 52),
                Size = new Size(500, 168),
                MehediFillColor = surf,
                ShihabBorderColor = ShihabThemePalette.GetShihabBorder(dark),
                MehediCornerRadius = 8
            };

            string userName = "Desktop User";
            try { userName = Environment.UserName; } catch { }

            Label detailsLbl = new Label
            {
                Location = new Point(16, 14),
                Size = new Size(468, 140),
                BackColor = surf,
                ForeColor = ForeColor,
                UseMnemonic = false,
                Text =
                    "Product:         MHS Download Manager (v" + ShihabAutoUpdater.GetCurrentAppVersion() + ")\r\n" +
                    "Licensed To:     " + userName + " (Unlimited Personal & Commercial Use)\r\n" +
                    "Edition / Key:   MIT Open-Source Perpetual License — No Trial Limits\r\n" +
                    "Author:          Mehedi Hasan Shihab\r\n" +
                    "Repository:      " + repoUrl + "\r\n\r\n" +
                    "All features — Multi-Segment Acceleration, YouTube 4K/8K, BitTorrent, Scheduler & Queues, Browser Integration, and Auto-Updates — are permanently unlocked."
            };
            infoCard.Controls.Add(detailsLbl);

            Button licenseBtn = ShihabThemePalette.CreateMehediStyledButton("View MIT License", surf, ForeColor, 134);
            licenseBtn.Location = new Point(20, 236);
            licenseBtn.Click += delegate
            {
                ShihabThemePalette.SafeLaunchExternalUrl(licenseUrl);
            };

            Button checkUpdBtn = ShihabThemePalette.CreateMehediStyledButton("Check for Updates", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 144);
            checkUpdBtn.Location = new Point(162, 236);
            checkUpdBtn.Click += delegate
            {
                CheckUpdatesRequested = true;
                DialogResult = DialogResult.OK;
                Close();
            };

            Button repoBtn = ShihabThemePalette.CreateMehediStyledButton("GitHub Repo", surf, ForeColor, 108);
            repoBtn.Location = new Point(314, 236);
            repoBtn.Click += delegate
            {
                ShihabThemePalette.SafeLaunchExternalUrl(repoUrl);
            };

            Button closeBtn = ShihabThemePalette.CreateMehediStyledButton("Close", surf, ForeColor, 90);
            closeBtn.Location = new Point(430, 236);
            closeBtn.DialogResult = DialogResult.Cancel;
            closeBtn.Click += delegate { Close(); };

            CancelButton = closeBtn;
            Controls.AddRange(new Control[] { badgeLbl, infoCard, licenseBtn, checkUpdBtn, repoBtn, closeBtn });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _badgeFont != null)
            {
                _badgeFont.Dispose();
                _badgeFont = null;
            }
            base.Dispose(disposing);
        }
    }

    // =========================================================================
    // 8. DELETE / REMOVE TRANSFER CONFIRMATION DIALOG
    // =========================================================================
    public class ShihabDeleteConfirmDialog : Form
    {
        public bool AlsoDeleteDiskFiles { get; private set; }

        public ShihabDeleteConfirmDialog(List<ShihabDownloadItem> selectedItems, bool initialDeleteFromDisk, bool dark)
        {
            Color surf = ShihabThemePalette.GetShihabSurfaceBg(dark);
            int count = selectedItems != null ? selectedItems.Count : 0;
            string subject = count == 1 && selectedItems[0] != null
                ? ("\"" + selectedItems[0].ShihabFileName + "\"")
                : (count + " selected transfers");

            Text = "Delete Download — " + ShihabThemePalette.ShihabMainHeaderTitle;
            ClientSize = new Size(460, 174);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ShihabThemePalette.GetMehediWindowBg(dark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(dark);
            Font = ShihabThemePalette.UiRegular9Font;
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, dark);

            Label promptLbl = new Label
            {
                Location = new Point(18, 18),
                Size = new Size(424, 48),
                Text = "Are you sure you want to remove " + subject + " from the transfer list?"
            };

            CheckBox deleteDiskChk = new CheckBox
            {
                Location = new Point(18, 72),
                AutoSize = true,
                Checked = initialDeleteFromDisk,
                ForeColor = ShihabThemePalette.GetMehediAccentRed(),
                Font = ShihabThemePalette.UiSemibold9Font,
                Text = "Also permanently delete downloaded file(s) from hard disk"
            };

            Button deleteBtn = ShihabThemePalette.CreateMehediStyledButton("Delete", ShihabThemePalette.GetMehediAccentRed(), Color.White, 110);
            deleteBtn.Location = new Point(220, 120);
            deleteBtn.Click += delegate
            {
                AlsoDeleteDiskFiles = deleteDiskChk.Checked;
                DialogResult = DialogResult.OK;
                Close();
            };

            Button cancelBtn = ShihabThemePalette.CreateMehediStyledButton("Cancel", surf, ForeColor, 108);
            cancelBtn.Location = new Point(336, 120);
            cancelBtn.DialogResult = DialogResult.Cancel;
            cancelBtn.Click += delegate { Close(); };

            AcceptButton = deleteBtn;
            CancelButton = cancelBtn;
            Controls.AddRange(new Control[] { promptLbl, deleteDiskChk, deleteBtn, cancelBtn });
        }
    }
}

