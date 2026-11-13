using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    // =========================================================================
    // 1. BIT-TORRENT / MAGNET & .TORRENT CREATOR / FILE SELECTOR DIALOG
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
            Font = ShihabThemePalette.UiRegular9Font;
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
                Font = ShihabThemePalette.UiSemibold9Font
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
                filesView.BeginUpdate();
                try
                {
                    filesView.Items.Clear();
                    if (string.IsNullOrEmpty(raw))
                    {
                        currentMeta = null;
                        infoLbl.Text = "Info-Hash: (none)";
                        return;
                    }
                    if (ShihabProcessAndPathSafety.ContainsControlOrNewline(raw))
                    {
                        currentMeta = null;
                        infoLbl.Text = "Error: Invalid control characters in torrent source.";
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
                }
                finally
                {
                    filesView.EndUpdate();
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

            Button createFolderTorBtn = ShihabThemePalette.CreateMehediStyledButton("Folder .torrent...", surf, ForeColor, 130);
            createFolderTorBtn.Location = new Point(167, 412);

            Action<string, string, bool> startAsyncCreateTorrent = delegate(string sourcePath, string destTorrentPath, bool isFolder)
            {
                createTorrentBtn.Enabled = false;
                createFolderTorBtn.Enabled = false;
                infoLbl.Text = "Creating .torrent file (hashing pieces)...";

                Task.Run(delegate
                {
                    string infoHash;
                    string err;
                    List<string> defaultTrackers = new List<string>
                    {
                        "udp://tracker.opentrackr.org:1337/announce",
                        "https://tracker.tamersunion.org:443/announce"
                    };
                    bool ok = ShihabTorrentManager.CreateTorrentFile(sourcePath, destTorrentPath, defaultTrackers, new List<string>(), false, 262144, out infoHash, out err);

                    try
                    {
                        if (IsDisposed || !IsHandleCreated) return;
                        BeginInvoke((Action)delegate
                        {
                            if (IsDisposed) return;
                            createTorrentBtn.Enabled = true;
                            createFolderTorBtn.Enabled = true;
                            if (ok)
                            {
                                srcBox.Text = destTorrentPath;
                                parseCurrentSource();
                                MessageBox.Show(this,
                                    (isFolder ? "Created multi-file folder .torrent!\r\nInfo-Hash (SHA-1): " : "Created .torrent file!\r\nInfo-Hash (SHA-1): ") + infoHash,
                                    "Torrent Creator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                infoLbl.Text = "Failed to create .torrent.";
                                MessageBox.Show(this,
                                    (isFolder ? "Failed to create folder .torrent:\r\n" : "Failed to create .torrent:\r\n") + err,
                                    "Torrent Creator", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        });
                    }
                    catch { }
                });
            };

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
                                startAsyncCreateTorrent(ofd.FileName, sfd.FileName, false);
                            }
                        }
                    }
                }
            };

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
                                startAsyncCreateTorrent(fbd.SelectedPath, sfd.FileName, true);
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
                if (ShihabProcessAndPathSafety.ContainsControlOrNewline(raw))
                {
                    MessageBox.Show(this, "Invalid characters in Magnet URI or .torrent path.", "BitTorrent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                string saveDir = saveBox.Text.Trim();
                if (string.IsNullOrEmpty(saveDir) || ShihabProcessAndPathSafety.ContainsControlOrNewline(saveDir))
                {
                    saveDir = defaultTorDir;
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
                    MehediSaveDirectory = saveDir,
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
                if (!startNow)
                {
                    ConfiguredTorrentItem.MehediQueueName = "Main Download Queue";
                    ConfiguredTorrentItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued");
                }
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
        private static readonly Regex YouTubeVideoIdRegex = new Regex(
            @"(?:v=|youtu\.be/|shorts/)([A-Za-z0-9_-]{11})",
            RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(250));

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
            Font = ShihabThemePalette.UiRegular9Font;
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
                Uri parsedUrl;
                if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(rawUrl, true, out parsedUrl))
                {
                    MessageBox.Show(this, "Please enter a valid HTTP or HTTPS video/playlist URL.", "Media & Video Probe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
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
                        try
                        {
                            Match m = YouTubeVideoIdRegex.Match(rawUrl);
                            if (m.Success)
                            {
                                innerTubeReply = MehediYouTubeInnerTubeResolver.ResolveShihabYouTubeStreams(m.Groups[1].Value, "", false);
                            }
                        }
                        catch { }
                    }

                    try
                    {
                        if (IsDisposed || !IsHandleCreated) return;
                        BeginInvoke((Action)delegate
                        {
                            if (IsDisposed) return;
                            ytFetchBtn.Enabled = true;
                            ytFetchBtn.Text = "Fetch Info";

                            ytResultsList.BeginUpdate();
                            try
                            {
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
                            }
                            finally
                            {
                                ytResultsList.EndUpdate();
                            }
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
                Uri parsedGal;
                if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(gUrl, true, out parsedGal))
                {
                    MessageBox.Show(this, "Please enter a valid HTTP or HTTPS gallery URL.", "Gallery Extractor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                galList.Items.Clear();
                galScanBtn.Enabled = false;
                galScanBtn.Text = "Extracting...";

                Task.Run(delegate
                {
                    string gTitle;
                    List<string> imgs = ShihabMediaHelpersManager.ExtractGalleryImageUrls(gUrl, store.MehediBaseDirectory, out gTitle);
                    bool hasGalleryDl = File.Exists(ShihabMediaHelpersManager.GetHelperExecutablePath(store.MehediBaseDirectory, "gallery-dl"));

                    try
                    {
                        if (IsDisposed || !IsHandleCreated) return;
                        BeginInvoke((Action)delegate
                        {
                            if (IsDisposed) return;
                            galScanBtn.Enabled = true;
                            galScanBtn.Text = "Extract Images";

                            galList.BeginUpdate();
                            try
                            {
                                if (hasGalleryDl)
                                {
                                    ListViewItem batchJobRow = new ListViewItem(new string[] { gUrl + " (Full Gallery Folder via gallery-dl)", "GALLERY-DL" });
                                    batchJobRow.Checked = imgs == null || imgs.Count == 0;
                                    batchJobRow.Tag = new MediaRowPayload
                                    {
                                        Url = gUrl,
                                        Title = string.IsNullOrWhiteSpace(gTitle) ? "Gallery_Download" : gTitle,
                                        IsGalleryJob = true
                                    };
                                    galList.Items.Add(batchJobRow);
                                }
                                if (imgs != null)
                                {
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
                                }
                            }
                            finally
                            {
                                galList.EndUpdate();
                            }
                        });
                    }
                    catch { }
                });
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
                hlpList.BeginUpdate();
                try
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
                }
                finally
                {
                    hlpList.EndUpdate();
                }
            };
            reloadHelpers();

            Label hlpProgLbl = new Label { Text = "Select a helper below to download/update from official GitHub Releases:", Location = new Point(16, 268), AutoSize = true };

            Action<string> installHelper = delegate(string helperName)
            {
                ShihabMediaHelpersManager.InstallOrUpdateHelperAsync(store.MehediBaseDirectory, helperName, delegate(string st)
                {
                    try
                    {
                        if (IsDisposed || !IsHandleCreated) return;
                        BeginInvoke((Action)delegate
                        {
                            if (IsDisposed) return;
                            hlpProgLbl.Text = st;
                            reloadHelpers();
                        });
                    }
                    catch { }
                });
            };

            Button instYtdlpBtn = ShihabThemePalette.CreateMehediStyledButton("Install / Update yt-dlp", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 185);
            instYtdlpBtn.Location = new Point(16, 298);
            instYtdlpBtn.Click += delegate { installHelper("yt-dlp"); };

            Button instGalBtn = ShihabThemePalette.CreateMehediStyledButton("Install / Update gallery-dl", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 195);
            instGalBtn.Location = new Point(212, 298);
            instGalBtn.Click += delegate { installHelper("gallery-dl"); };

            Button instFfmpegBtn = ShihabThemePalette.CreateMehediStyledButton("Install / Update ffmpeg", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 185);
            instFfmpegBtn.Location = new Point(418, 298);
            instFfmpegBtn.Click += delegate { installHelper("ffmpeg"); };

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
                    Uri parsedRaw;
                    if (ShihabProcessAndPathSafety.TryValidateHttpUrl(rawU, true, out parsedRaw))
                    {
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
                }
                if (ConfiguredMediaItems.Count == 0 && tabs.SelectedIndex == 1 && !string.IsNullOrWhiteSpace(galUrlBox.Text) && galUrlBox.Text.Trim() != "https://")
                {
                    string rawG = galUrlBox.Text.Trim();
                    Uri parsedGal;
                    if (ShihabProcessAndPathSafety.TryValidateHttpUrl(rawG, true, out parsedGal))
                    {
                        DiscoveredDownloadUrls.Add(rawG);
                        ConfiguredMediaItems.Add(new ShihabDownloadItem
                        {
                            MehediUrl = rawG,
                            ShihabFileName = "Gallery_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                            ShihabCategoryName = "General",
                            ShihabIsGalleryJob = true
                        });
                    }
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
}

