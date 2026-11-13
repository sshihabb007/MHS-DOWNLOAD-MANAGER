using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public class ShihabFilePropertiesForm : Form
    {
        public ShihabFilePropertiesForm(ShihabDownloadItem shihabItem, bool mehediDark)
        {
            Text = "File Properties — " + shihabItem.ShihabFileName;
            Size = new Size(620, 485);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ShihabThemePalette.GetMehediWindowBg(mehediDark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(mehediDark);
            Font = ShihabThemePalette.UiRegular9Font;
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, mehediDark);

            Label mehediL1 = new Label { Text = "Address (URL):", Location = new Point(16, 18), AutoSize = true };
            TextBox shihabUrlBox = new TextBox { Location = new Point(135, 15), Width = 450, Text = shihabItem.MehediUrl, BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor = ForeColor };

            Label mehediL2 = new Label { Text = "Referer:", Location = new Point(16, 50), AutoSize = true };
            TextBox shihabRefBox = new TextBox { Location = new Point(135, 47), Width = 450, Text = shihabItem.MehediReferer ?? "", BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor = ForeColor };

            Label mehediL3 = new Label { Text = "Save To:", Location = new Point(16, 82), AutoSize = true };
            TextBox shihabSaveBox = new TextBox { Location = new Point(135, 79), Width = 450, ReadOnly = true, Text = shihabItem.GetShihabFullTargetPath(), BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor = ForeColor };

            string shihabGeoBadge = ShihabGeoIpLookup.FormatCountryBadgeText(shihabItem.ShihabServerCountryCode, shihabItem.ShihabServerIp);
            Label mehediL4 = new Label { Text = "Size / Server:", Location = new Point(16, 114), AutoSize = true };
            Label shihabSizeInfo = new Label
            {
                Location = new Point(135, 114),
                AutoSize = true,
                Text = string.Format("{0}  |  Downloaded: {1}  |  Resume: {2}{3}",
                    ShihabFormatHelpers.FormatMehediBytes(shihabItem.ShihabTotalBytes),
                    ShihabFormatHelpers.FormatMehediBytes(shihabItem.MehediDownloadedBytes),
                    shihabItem.ShihabResumeSupported ? "Yes" : "No",
                    !string.IsNullOrEmpty(shihabGeoBadge) ? ("  |  Server: " + shihabGeoBadge) : "")
            };

            Label mehediL5 = new Label { Text = "SHA-256 / Status:", Location = new Point(16, 144), AutoSize = true };
            string shihabHashDisplay = !string.IsNullOrEmpty(shihabItem.MehediSha256Hex)
                ? (shihabItem.MehediSha256Hex + (!string.IsNullOrEmpty(shihabItem.ShihabChecksumStatus) ? (" [" + shihabItem.ShihabChecksumStatus + "]") : ""))
                : "(computed on completion)";
            TextBox shihabHashBox = new TextBox { Location = new Point(135, 141), Width = 450, ReadOnly = true, Text = shihabHashDisplay, BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor = ForeColor };

            Label mehediL5B = new Label { Text = "Expected Hash:", Location = new Point(16, 176), AutoSize = true };
            TextBox shihabExpectedBox = new TextBox { Location = new Point(135, 173), Width = 450, Text = shihabItem.ShihabExpectedChecksum ?? "", BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor = ForeColor };

            Label mehediL5C = new Label { Text = "ETag Validator:", Location = new Point(16, 208), AutoSize = true };
            TextBox shihabETagBox = new TextBox { Location = new Point(135, 205), Width = 450, ReadOnly = true, Text = string.IsNullOrEmpty(shihabItem.ShihabETag) ? "(none)" : (shihabItem.ShihabETag + " @ " + (shihabItem.ShihabETagHost ?? "")), BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor = ForeColor };

            Label mehediL6 = new Label { Text = "Description:", Location = new Point(16, 240), AutoSize = true };
            TextBox shihabDescBox = new TextBox { Location = new Point(135, 237), Width = 450, Text = shihabItem.ShihabDescription ?? "", BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor = ForeColor };

            Label mehediL7 = new Label { Text = "Login / Pass:", Location = new Point(16, 272), AutoSize = true };
            TextBox shihabUserBox = new TextBox { Location = new Point(135, 269), Width = 200, Text = shihabItem.ShihabAuthUser ?? "", BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor = ForeColor };
            TextBox mehediPassBox = new TextBox { Location = new Point(350, 269), Width = 235, UseSystemPasswordChar = true, Text = shihabItem.MehediAuthPassword ?? "", BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor = ForeColor };

            Label mehediL8 = new Label { Text = "Max Conns (1-128):", Location = new Point(16, 308), AutoSize = true };
            NumericUpDown shihabConnNum = new NumericUpDown { Location = new Point(135, 305), Width = 80, Minimum = 1, Maximum = 128, Value = Math.Max(1, Math.Min(128, shihabItem.MehediMaxConnections)) };

            CheckBox shihabAutoExtChk = new CheckBox
            {
                Text = "Auto-extract archive",
                Location = new Point(235, 307),
                AutoSize = true,
                Checked = shihabItem.ShihabAutoExtract
            };

            CheckBox shihabBypassDebridChk = new CheckBox
            {
                Text = "Bypass Debrid",
                Location = new Point(415, 307),
                AutoSize = true,
                Checked = shihabItem.ShihabBypassDebrid
            };

            Label mehediL9 = new Label { Text = "Archive Pass:", Location = new Point(16, 344), AutoSize = true };
            TextBox shihabArchPassBox = new TextBox { Location = new Point(135, 341), Width = 220, UseSystemPasswordChar = true, Text = shihabItem.ShihabArchivePassword ?? "", BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor = ForeColor };
            CheckBox shihabPrivateChk = new CheckBox { Text = "Private / Incognito", Location = new Point(375, 343), AutoSize = true, Checked = shihabItem.ShihabIsPrivateIncognito };

            Button shihabVerifyNowBtn = ShihabThemePalette.CreateMehediStyledButton("Verify Hash Now", ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor, 135);
            shihabVerifyNowBtn.Location = new Point(16, 390);
            shihabVerifyNowBtn.Click += delegate
            {
                string fullFile = shihabItem.GetShihabFullTargetPath();
                if (!File.Exists(fullFile))
                {
                    MessageBox.Show(this, "Target file does not exist on disk yet:\r\n" + fullFile, "Verify Checksum", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                string spec = shihabExpectedBox.Text.Trim();
                shihabVerifyNowBtn.Enabled = false;
                shihabVerifyNowBtn.Text = "Verifying...";
                Task.Run(delegate
                {
                    string algo = "";
                    string comp = "";
                    string err = "";
                    bool ok = false;
                    bool emptySpec = string.IsNullOrEmpty(spec);
                    string sha256 = "";
                    try
                    {
                        if (emptySpec)
                        {
                            sha256 = ShihabChecksumVerifier.ComputeFileChecksumHex(fullFile, "SHA-256");
                        }
                        else
                        {
                            ok = ShihabChecksumVerifier.VerifyExpectedChecksum(fullFile, spec, out algo, out comp, out err);
                        }
                    }
                    catch (Exception ex)
                    {
                        err = ex.Message;
                    }
                    if (IsDisposed || !IsHandleCreated) return;
                    try
                    {
                        BeginInvoke((Action)delegate
                        {
                            if (IsDisposed) return;
                            shihabVerifyNowBtn.Enabled = true;
                            shihabVerifyNowBtn.Text = "Verify Hash Now";
                            if (emptySpec)
                            {
                                if (!string.IsNullOrEmpty(err))
                                {
                                    MessageBox.Show(this, err, "Checksum Computed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    return;
                                }
                                shihabItem.MehediSha256Hex = sha256;
                                shihabHashBox.Text = sha256;
                                MessageBox.Show(this, "Computed SHA-256:\r\n" + sha256, "Checksum Computed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                shihabItem.ShihabComputedChecksum = algo + ":" + comp;
                                shihabItem.ShihabChecksumStatus = ok ? ("Verified (" + algo + ")") : ("Mismatch (" + algo + ")");
                                shihabHashBox.Text = comp + " [" + shihabItem.ShihabChecksumStatus + "]";
                                MessageBox.Show(this, ok ? ("Checksum VERIFIED (" + algo + "):\r\n" + comp) : err, "Checksum Verification", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                            }
                        });
                    }
                    catch { }
                });
            };

            Button shihabExtractNowBtn = ShihabThemePalette.CreateMehediStyledButton("Extract Archive", ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor, 130);
            shihabExtractNowBtn.Location = new Point(160, 390);
            shihabExtractNowBtn.Click += delegate
            {
                string fullFile = shihabItem.GetShihabFullTargetPath();
                if (!File.Exists(fullFile) || !ShihabSafeArchiveExtractor.IsSupportedArchiveFile(fullFile))
                {
                    MessageBox.Show(this, "Target file is not an existing supported archive (.zip, .tar, .tar.gz, .tgz, .gz, .7z, .rar).", "Extract Archive", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                string archPass = shihabArchPassBox.Text;
                shihabExtractNowBtn.Enabled = false;
                shihabExtractNowBtn.Text = "Extracting...";
                Task.Run(delegate
                {
                    string destDir = "";
                    string err = "";
                    bool ok = false;
                    try
                    {
                        ok = ShihabSafeArchiveExtractor.ExtractArchiveSafely(fullFile, "Smart", false, archPass, out destDir, out err);
                    }
                    catch (Exception ex)
                    {
                        err = ex.Message;
                    }
                    if (IsDisposed || !IsHandleCreated) return;
                    try
                    {
                        BeginInvoke((Action)delegate
                        {
                            if (IsDisposed) return;
                            shihabExtractNowBtn.Enabled = true;
                            shihabExtractNowBtn.Text = "Extract Archive";
                            if (ok)
                            {
                                MessageBox.Show(this, "Archive extracted safely to:\r\n" + destDir, "Extract Archive", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show(this, "Archive extraction failed:\r\n" + err, "Extract Archive", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        });
                    }
                    catch { }
                });
            };

            Button shihabSaveBtn = ShihabThemePalette.CreateMehediStyledButton("Save Changes", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 130);
            shihabSaveBtn.Location = new Point(335, 390);
            shihabSaveBtn.Click += delegate
            {
                string editedUrl = (shihabUrlBox.Text ?? "").Trim();
                if (!string.IsNullOrEmpty(editedUrl))
                {
                    bool hasCtrl = ShihabProcessAndPathSafety.ContainsControlOrNewline(editedUrl);
                    bool isMagnet = editedUrl.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase);
                    Uri parsedUrl;
                    bool okUrl = !hasCtrl && (isMagnet ||
                        (Uri.TryCreate(editedUrl, UriKind.Absolute, out parsedUrl) &&
                         parsedUrl != null &&
                         (string.Equals(parsedUrl.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(parsedUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(parsedUrl.Scheme, Uri.UriSchemeFtp, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(parsedUrl.Scheme, "ftps", StringComparison.OrdinalIgnoreCase))));
                    if (!okUrl)
                    {
                        MessageBox.Show(this, "Address (URL) must be a valid http://, https://, ftp://, ftps://, or magnet:? URL.", "File Properties", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    shihabItem.MehediUrl = editedUrl;
                }
                string editedRef = (shihabRefBox.Text ?? "").Trim();
                if (!string.IsNullOrEmpty(editedRef))
                {
                    Uri refUri;
                    if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(editedRef, true, out refUri))
                    {
                        MessageBox.Show(this, "Referer must be a valid http:// or https:// URL.", "File Properties", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                shihabItem.MehediReferer = editedRef;
                shihabItem.ShihabExpectedChecksum = shihabExpectedBox.Text.Trim();
                shihabItem.ShihabDescription = shihabDescBox.Text.Trim();
                shihabItem.ShihabAuthUser = shihabUserBox.Text.Trim();
                shihabItem.MehediAuthPassword = mehediPassBox.Text;
                shihabItem.MehediMaxConnections = (int)shihabConnNum.Value;
                shihabItem.ShihabAutoExtract = shihabAutoExtChk.Checked;
                shihabItem.ShihabBypassDebrid = shihabBypassDebridChk.Checked;
                shihabItem.ShihabArchivePassword = shihabArchPassBox.Text;
                shihabItem.ShihabIsPrivateIncognito = shihabPrivateChk.Checked;
                DialogResult = DialogResult.OK;
                Close();
            };

            Button mehediCancelBtn = ShihabThemePalette.CreateMehediStyledButton("Close", ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor, 110);
            mehediCancelBtn.Location = new Point(475, 390);
            mehediCancelBtn.Click += delegate { Close(); };

            Controls.AddRange(new Control[]
            {
                mehediL1, shihabUrlBox, mehediL2, shihabRefBox, mehediL3, shihabSaveBox,
                mehediL4, shihabSizeInfo, mehediL5, shihabHashBox, mehediL5B, shihabExpectedBox,
                mehediL5C, shihabETagBox, mehediL6, shihabDescBox,
                mehediL7, shihabUserBox, mehediPassBox, mehediL8, shihabConnNum, shihabAutoExtChk,
                shihabBypassDebridChk, mehediL9, shihabArchPassBox, shihabPrivateChk,
                shihabVerifyNowBtn, shihabExtractNowBtn, shihabSaveBtn, mehediCancelBtn
            });
        }
    }

    public class ShihabAboutDeveloperDialog : Form
    {
        private Bitmap _shihabRoundAuthorImg;
        private readonly Font _cardHdrFont;
        private readonly Font _devValFont;

        public ShihabAboutDeveloperDialog(bool shihabDark)
        {
            Text = "About — " + ShihabThemePalette.ShihabMainHeaderTitle;
            ClientSize = new Size(660, 520);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = shihabDark ? Color.FromArgb(14, 17, 24) : ShihabThemePalette.GetMehediWindowBg(false);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(shihabDark);
            Font = ShihabThemePalette.UiSemibold92Font.FontFamily != null ? ShihabThemePalette.UiRegular9Font : SystemFonts.DefaultFont;
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, shihabDark);

            _shihabRoundAuthorImg = ShihabThemePalette.CreateShihabCircularAuthorBitmap(128);
            _cardHdrFont = new Font("Segoe UI", 8.3f, FontStyle.Bold);
            _devValFont = new Font("Segoe UI", 9.6f, FontStyle.Bold);

            Panel mehediHeroBanner = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(660, 224),
                BackColor = shihabDark ? Color.FromArgb(11, 16, 29) : Color.FromArgb(232, 240, 252)
            };
            mehediHeroBanner.Paint += delegate(object shihabSender, PaintEventArgs mehediE)
            {
                Graphics shihabG = mehediE.Graphics;
                shihabG.SmoothingMode = SmoothingMode.AntiAlias;
                shihabG.InterpolationMode = InterpolationMode.HighQualityBicubic;
                shihabG.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                Rectangle mehediHeroRect = mehediHeroBanner.ClientRectangle;
                Color shihabC1 = shihabDark ? Color.FromArgb(9, 13, 24) : Color.FromArgb(238, 244, 255);
                Color mehediC2 = shihabDark ? Color.FromArgb(16, 25, 44) : Color.FromArgb(219, 234, 254);
                using (LinearGradientBrush shihabHeroGrad = new LinearGradientBrush(mehediHeroRect, shihabC1, mehediC2, 35f))
                using (Pen mehediBotBorder = new Pen(shihabDark ? Color.FromArgb(32, 40, 58) : Color.FromArgb(203, 213, 225)))
                {
                    shihabG.FillRectangle(shihabHeroGrad, mehediHeroRect);
                    shihabG.DrawLine(mehediBotBorder, 0, mehediHeroRect.Height - 1, mehediHeroRect.Width, mehediHeroRect.Height - 1);
                }

                if (_shihabRoundAuthorImg != null)
                {
                    int mehediImgX = (mehediHeroRect.Width - 128) / 2;
                    shihabG.DrawImage(_shihabRoundAuthorImg, new Rectangle(mehediImgX, 16, 128, 128));
                }

                Color shihabAuthorTitleCol = shihabDark ? Color.FromArgb(244, 247, 254) : Color.FromArgb(15, 23, 42);
                Color mehediRoleCol = shihabDark ? Color.FromArgb(96, 165, 250) : Color.FromArgb(29, 78, 216);
                Color shihabSubTagCol = shihabDark ? Color.FromArgb(142, 152, 175) : Color.FromArgb(71, 85, 105);

                using (Font mehediAuthorFont = new Font("Segoe UI", 13.5f, FontStyle.Bold))
                using (Font mehediAppSubFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                {
                    TextRenderer.DrawText(
                        shihabG,
                        "Mehedi Hasan Shihab",
                        mehediAuthorFont,
                        new Rectangle(20, 150, mehediHeroRect.Width - 40, 26),
                        shihabAuthorTitleCol,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                    TextRenderer.DrawText(
                        shihabG,
                        "Senior Software Engineer  \u2022  Author of " + ShihabThemePalette.ShihabMainHeaderTitle,
                        ShihabThemePalette.UiSemibold92Font,
                        new Rectangle(20, 176, mehediHeroRect.Width - 40, 20),
                        mehediRoleCol,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                    TextRenderer.DrawText(
                        shihabG,
                        "Executable: mdm.exe (x64)  \u2022  Dynamic 32-Segment Engine & MV3 Stream Sniffer",
                        mehediAppSubFont,
                        new Rectangle(20, 196, mehediHeroRect.Width - 40, 18),
                        shihabSubTagCol,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            };

            ShihabRoundedCardPanel mehediDevCard = new ShihabRoundedCardPanel
            {
                Location = new Point(20, 240),
                Size = new Size(620, 214),
                MehediFillColor = shihabDark ? Color.FromArgb(20, 23, 31) : Color.White,
                ShihabBorderColor = shihabDark ? Color.FromArgb(36, 41, 54) : Color.FromArgb(203, 213, 225),
                MehediCornerRadius = 10
            };

            Label shihabCardHdr = new Label
            {
                Text = "DEVELOPER & OFFICIAL LINKS",
                UseMnemonic = false,
                Location = new Point(18, 14),
                AutoSize = true,
                BackColor = mehediDevCard.MehediFillColor,
                Font = _cardHdrFont,
                ForeColor = shihabDark ? Color.FromArgb(77, 184, 255) : Color.FromArgb(29, 78, 216)
            };

            Label mehediDevNameLbl = new Label
            {
                Text = "Developer:",
                Location = new Point(18, 42),
                AutoSize = true,
                BackColor = mehediDevCard.MehediFillColor,
                Font = ShihabThemePalette.UiSemibold92Font,
                ForeColor = ShihabThemePalette.GetMehediMutedText(shihabDark)
            };
            Label shihabDevNameVal = new Label
            {
                Text = "Mehedi Hasan Shihab  (Software & Systems Engineer)",
                UseMnemonic = false,
                Location = new Point(128, 42),
                AutoSize = true,
                BackColor = mehediDevCard.MehediFillColor,
                Font = _devValFont,
                ForeColor = ForeColor
            };

            Color mehediLinkCol = shihabDark ? Color.FromArgb(96, 165, 250) : Color.FromArgb(29, 78, 216);

            Label shihabPortLbl = new Label { Text = "Portfolio:", Location = new Point(18, 74), AutoSize = true, BackColor = mehediDevCard.MehediFillColor, ForeColor = ShihabThemePalette.GetMehediMutedText(shihabDark) };
            LinkLabel mehediPortLink = CreateShihabUrlLink("https://sshihabb007.github.io/", "https://sshihabb007.github.io/", new Point(128, 74), mehediLinkCol);
            mehediPortLink.BackColor = mehediDevCard.MehediFillColor;

            Label shihabMailLbl = new Label { Text = "Email Me:", Location = new Point(18, 103), AutoSize = true, BackColor = mehediDevCard.MehediFillColor, ForeColor = ShihabThemePalette.GetMehediMutedText(shihabDark) };
            LinkLabel mehediMailLink = CreateShihabUrlLink("sshihabb007@gmail.com", "mailto:sshihabb007@gmail.com?subject=MHS%20Download%20Manager", new Point(128, 103), mehediLinkCol);
            mehediMailLink.BackColor = mehediDevCard.MehediFillColor;

            Label shihabGhLbl = new Label { Text = "GitHub:", Location = new Point(18, 132), AutoSize = true, BackColor = mehediDevCard.MehediFillColor, ForeColor = ShihabThemePalette.GetMehediMutedText(shihabDark) };
            LinkLabel mehediGhLink = CreateShihabUrlLink("https://github.com/sshihabb007", "https://github.com/sshihabb007", new Point(128, 132), mehediLinkCol);
            mehediGhLink.BackColor = mehediDevCard.MehediFillColor;

            Label shihabLiLbl = new Label { Text = "LinkedIn:", Location = new Point(18, 161), AutoSize = true, BackColor = mehediDevCard.MehediFillColor, ForeColor = ShihabThemePalette.GetMehediMutedText(shihabDark) };
            LinkLabel mehediLiLink = CreateShihabUrlLink("https://www.linkedin.com/in/mehedi-hasan-shihab", "https://www.linkedin.com/in/mehedi-hasan-shihab", new Point(128, 161), mehediLinkCol);
            mehediLiLink.BackColor = mehediDevCard.MehediFillColor;

            Label shihabCopyLbl = new Label
            {
                Text = "Copyright \u00A9 2026 Mehedi Hasan Shihab (sshihabb007). All rights reserved.",
                Location = new Point(18, 188),
                AutoSize = true,
                BackColor = mehediDevCard.MehediFillColor,
                Font = ShihabThemePalette.UiRegular82Font,
                ForeColor = ShihabThemePalette.GetMehediMutedText(shihabDark)
            };

            mehediDevCard.Controls.AddRange(new Control[]
            {
                shihabCardHdr, mehediDevNameLbl, shihabDevNameVal,
                shihabPortLbl, mehediPortLink,
                shihabMailLbl, mehediMailLink,
                shihabGhLbl, mehediGhLink,
                shihabLiLbl, mehediLiLink,
                shihabCopyLbl
            });

            Button shihabPortfolioBtn = ShihabThemePalette.CreateMehediStyledButton("Visit Portfolio", Color.FromArgb(13, 116, 231), Color.White, 138);
            shihabPortfolioBtn.Location = new Point(20, 470);
            shihabPortfolioBtn.Click += delegate { OpenMehediExternalUri("https://sshihabb007.github.io/"); };

            Button mehediEmailBtn = ShihabThemePalette.CreateMehediStyledButton("Email Me", Color.FromArgb(33, 39, 52), Color.White, 118);
            mehediEmailBtn.Location = new Point(168, 470);
            mehediEmailBtn.Click += delegate { OpenMehediExternalUri("mailto:sshihabb007@gmail.com?subject=MHS%20Download%20Manager"); };

            Button shihabGithubBtn = ShihabThemePalette.CreateMehediStyledButton("GitHub", Color.FromArgb(33, 39, 52), Color.White, 110);
            shihabGithubBtn.Location = new Point(296, 470);
            shihabGithubBtn.Click += delegate { OpenMehediExternalUri("https://github.com/sshihabb007"); };

            Button mehediOkBtn = ShihabThemePalette.CreateMehediStyledButton("Close", ShihabThemePalette.GetShihabSurfaceBg(shihabDark), ForeColor, 110);
            mehediOkBtn.Location = new Point(530, 470);
            mehediOkBtn.Click += delegate { Close(); };

            AcceptButton = mehediOkBtn;
            CancelButton = mehediOkBtn;

            Controls.AddRange(new Control[]
            {
                mehediHeroBanner,
                mehediDevCard, shihabPortfolioBtn, mehediEmailBtn, shihabGithubBtn, mehediOkBtn
            });
        }

        protected override void Dispose(bool shihabDisposing)
        {
            if (shihabDisposing)
            {
                if (_shihabRoundAuthorImg != null)
                {
                    _shihabRoundAuthorImg.Dispose();
                    _shihabRoundAuthorImg = null;
                }
                if (_cardHdrFont != null) _cardHdrFont.Dispose();
                if (_devValFont != null) _devValFont.Dispose();
            }
            base.Dispose(shihabDisposing);
        }

        private static LinkLabel CreateShihabUrlLink(string mehediText, string shihabTargetUri, Point mehediLoc, Color shihabCol)
        {
            LinkLabel mehediLink = new LinkLabel
            {
                Text = mehediText,
                Location = mehediLoc,
                AutoSize = true,
                LinkColor = shihabCol,
                ActiveLinkColor = Color.FromArgb(244, 114, 182),
                VisitedLinkColor = shihabCol,
                Font = ShihabThemePalette.UiSemibold92Font
            };
            mehediLink.LinkClicked += delegate
            {
                OpenMehediExternalUri(shihabTargetUri);
            };
            return mehediLink;
        }

        private static void OpenMehediExternalUri(string shihabUri)
        {
            ShihabThemePalette.SafeLaunchExternalUrl(shihabUri);
        }
    }
}

