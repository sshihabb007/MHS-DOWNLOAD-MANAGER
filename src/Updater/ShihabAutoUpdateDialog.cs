using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public sealed class ShihabAutoUpdateDialog : Form
    {
        private readonly ShihabUpdateCheckResult _updateInfo;
        private readonly MehediAppConfiguration _config;
        private readonly ShihabStateStore _store;
        private readonly Action _cleanExitCallback;
        private CancellationTokenSource _downloadCts;
        private bool _isDownloading;

        private readonly Panel _progressFillPanel;
        private readonly Panel _progressTrackPanel;
        private readonly Label _statusLabel;
        private readonly Button _updateNowBtn;
        private readonly Button _laterBtn;
        private readonly Button _skipBtn;
        private readonly PictureBox _iconBox;
        private readonly Font _baseFont;
        private readonly Font _titleFont;
        private readonly Font _subFont;
        private readonly Font _notesFont;
        private readonly Icon _ownedIcon;

        public ShihabAutoUpdateDialog(
            ShihabUpdateCheckResult updateInfo,
            MehediAppConfiguration config,
            ShihabStateStore store,
            Action cleanExitCallback)
        {
            _updateInfo = updateInfo;
            _config = config;
            _store = store;
            _cleanExitCallback = cleanExitCallback;

            bool dark = config == null || config.ShihabDarkMode;
            Text = "Software Update Available \u2014 MHS Download Manager";
            ClientSize = new Size(620, 445);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            BackColor = ShihabThemePalette.GetMehediWindowBg(dark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(dark);
            _baseFont = new Font("Segoe UI", 9.2f);
            _titleFont = new Font("Segoe UI Semibold", 11.5f);
            _subFont = new Font("Segoe UI Semibold", 9f);
            _notesFont = new Font("Consolas", 9f);
            _ownedIcon = ShihabThemePalette.CreateShihabAppIcon();
            Font = _baseFont;
            Icon = _ownedIcon;
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, dark);
            Shown += delegate { try { Activate(); BringToFront(); } catch { } };

            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 76,
                BackColor = ShihabThemePalette.GetShihabSidebarBg(dark)
            };
            _iconBox = new PictureBox
            {
                Image = ShihabThemePalette.CreateMehediLogoBitmap(46),
                Location = new Point(18, 15),
                Size = new Size(46, 46),
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            Label titleLbl = new Label
            {
                Text = "A New Version of MHS Download Manager is Ready!",
                Location = new Point(76, 13),
                AutoSize = true,
                Font = _titleFont,
                ForeColor = ShihabThemePalette.GetMehediAccentGreen()
            };
            string modeBadge = updateInfo.InstallMode == ShihabAppInstallMode.InstalledSetup ? "Installed Setup" : "Portable x64";
            string sizeBadge = (updateInfo.PrimaryAsset != null && updateInfo.PrimaryAsset.Size > 0)
                ? ("  \u2022  " + ShihabFormatHelpers.FormatMehediBytes(updateInfo.PrimaryAsset.Size))
                : "";
            Label verSubLbl = new Label
            {
                Text = string.Format("Installed: v{0}   \u2192   Latest: v{1}   ({2}{3})",
                    updateInfo.CurrentVersion, updateInfo.LatestVersion, modeBadge, sizeBadge),
                Location = new Point(77, 41),
                AutoSize = true,
                Font = _subFont,
                ForeColor = ShihabThemePalette.GetMehediMutedText(dark)
            };
            header.Controls.AddRange(new Control[] { _iconBox, titleLbl, verSubLbl });

            Label notesHdr = new Label
            {
                Text = "What's New in " + (string.IsNullOrWhiteSpace(updateInfo.ReleaseTitle) ? ("v" + updateInfo.LatestVersion) : updateInfo.ReleaseTitle) + ":",
                Location = new Point(18, 88),
                AutoSize = true,
                Font = _subFont
            };
            LinkLabel ghLink = new LinkLabel
            {
                Text = "View Release on GitHub \u2197",
                Location = new Point(455, 88),
                AutoSize = true,
                LinkColor = ShihabThemePalette.GetMehediAccentCyan()
            };
            ghLink.LinkClicked += delegate
            {
                try
                {
                    Uri safeUri;
                    if (ShihabProcessAndPathSafety.TryValidateHttpUrl(_updateInfo.HtmlUrl, true, out safeUri))
                    {
                        Process.Start(new ProcessStartInfo(safeUri.AbsoluteUri) { UseShellExecute = true });
                    }
                }
                catch { }
            };

            TextBox notesBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(18, 114),
                Size = new Size(584, 200),
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(dark),
                ForeColor = ForeColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = _notesFont,
                Text = string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes)
                    ? "Performance improvements, bug fixes, and verified SHA-256 GitHub release package."
                    : updateInfo.ReleaseNotes.Replace("\r\n", "\n").Replace("\n", "\r\n")
            };

            _progressTrackPanel = new Panel
            {
                Location = new Point(18, 326),
                Size = new Size(584, 18),
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(dark),
                BorderStyle = BorderStyle.FixedSingle
            };
            _progressFillPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(0, 18),
                BackColor = ShihabThemePalette.GetShihabAccentBlue()
            };
            _progressTrackPanel.Controls.Add(_progressFillPanel);

            _statusLabel = new Label
            {
                Text = updateInfo.Sha256SumsAsset != null
                    ? "Cryptographic SHA-256 verification (SHA256SUMS.txt) will run automatically before installing."
                    : "Ready to download and install automatically.",
                Location = new Point(18, 350),
                Size = new Size(584, 22),
                AutoEllipsis = true,
                ForeColor = ShihabThemePalette.GetMehediMutedText(dark)
            };

            _skipBtn = ShihabThemePalette.CreateMehediStyledButton("Skip This Version", ShihabThemePalette.GetShihabSurfaceBg(dark), ForeColor, 138);
            _skipBtn.Location = new Point(18, 388);
            _skipBtn.Click += delegate
            {
                if (_isDownloading) return;
                if (_config != null && _store != null)
                {
                    _config.ShihabSkippedUpdateVersion = _updateInfo.LatestVersion;
                    _store.SaveMehediConfig(_config);
                }
                DialogResult = DialogResult.Ignore;
                Close();
            };

            _laterBtn = ShihabThemePalette.CreateMehediStyledButton("Remind Me Later", ShihabThemePalette.GetShihabSurfaceBg(dark), ForeColor, 138);
            _laterBtn.Location = new Point(268, 388);
            _laterBtn.Click += delegate
            {
                if (_isDownloading && _downloadCts != null)
                {
                    _downloadCts.Cancel();
                    return;
                }
                DialogResult = DialogResult.Cancel;
                Close();
            };

            _updateNowBtn = ShihabThemePalette.CreateMehediStyledButton("Update & Restart Now", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 186);
            _updateNowBtn.Location = new Point(416, 388);
            _updateNowBtn.Click += delegate { StartOneClickUpdate(); };

            Controls.AddRange(new Control[]
            {
                header, notesHdr, ghLink, notesBox, _progressTrackPanel, _statusLabel,
                _skipBtn, _laterBtn, _updateNowBtn
            });
        }

        private void SafeInvokeUi(Action action)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            try
            {
                BeginInvoke((Action)delegate
                {
                    if (!IsDisposed && !Disposing)
                    {
                        action();
                    }
                });
            }
            catch { }
        }

        private void StartOneClickUpdate()
        {
            if (_isDownloading) return;
            _isDownloading = true;
            _updateNowBtn.Enabled = false;
            _skipBtn.Enabled = false;
            _laterBtn.Text = "Cancel";
            if (_downloadCts != null)
            {
                try { _downloadCts.Dispose(); } catch { }
            }
            _downloadCts = new CancellationTokenSource();
            CancellationToken token = _downloadCts.Token;
            string dataDir = _store != null
                ? _store.MehediBaseDirectory
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShihabMehediIDM");

            Task.Run(delegate
            {
                string primaryFile;
                string extZip;
                string err;
                bool ok = ShihabAutoUpdater.DownloadAndVerifyReleaseUpdate(
                    _updateInfo,
                    _config,
                    dataDir,
                    delegate(long got, long total, string msg)
                    {
                        SafeInvokeUi(delegate
                        {
                            int pct = total > 0 ? (int)Math.Min(100L, (got * 100L) / total) : 50;
                            _progressFillPanel.Width = (_progressTrackPanel.ClientSize.Width * pct) / 100;
                            _statusLabel.Text = msg;
                        });
                    },
                    token,
                    out primaryFile,
                    out extZip,
                    out err);

                SafeInvokeUi(delegate
                {
                    _isDownloading = false;
                    if (!ok)
                    {
                        _updateNowBtn.Enabled = true;
                        _skipBtn.Enabled = true;
                        _laterBtn.Text = "Remind Me Later";
                        _statusLabel.ForeColor = ShihabThemePalette.GetMehediAccentRed();
                        _statusLabel.Text = err;
                        MessageBox.Show(this, err, "MHS Auto-Updater", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    _progressFillPanel.Width = _progressTrackPanel.ClientSize.Width;
                    _statusLabel.ForeColor = ShihabThemePalette.GetMehediAccentGreen();
                    _statusLabel.Text = "SHA-256 verified! Applying update and restarting MHS Download Manager...";

                    string applyErr;
                    if (!ShihabAutoUpdater.ApplyVerifiedUpdateAndRestart(_updateInfo, primaryFile, extZip, _cleanExitCallback, out applyErr))
                    {
                        _updateNowBtn.Enabled = true;
                        _skipBtn.Enabled = true;
                        _laterBtn.Text = "Remind Me Later";
                        _statusLabel.ForeColor = ShihabThemePalette.GetMehediAccentRed();
                        _statusLabel.Text = applyErr;
                        MessageBox.Show(this, applyErr, "MHS Auto-Updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                });
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_downloadCts != null) { _downloadCts.Cancel(); _downloadCts.Dispose(); } } catch { }
                try { if (_iconBox != null && _iconBox.Image != null) { _iconBox.Image.Dispose(); _iconBox.Image = null; } } catch { }
                try { if (_ownedIcon != null) _ownedIcon.Dispose(); } catch { }
                try { if (_notesFont != null) _notesFont.Dispose(); } catch { }
                try { if (_titleFont != null) _titleFont.Dispose(); } catch { }
                try { if (_subFont != null) _subFont.Dispose(); } catch { }
                try { if (_baseFont != null) _baseFont.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}

