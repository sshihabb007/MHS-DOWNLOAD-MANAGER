using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
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
            Font = ShihabThemePalette.UiRegular9Font;
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
            Font = ShihabThemePalette.UiRegular9Font;
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
                        if (IsDisposed) break;
                        ListViewItem rowRef = rows[i];
                        MehediBatchLinkEntryDto dto = rowRef.Tag as MehediBatchLinkEntryDto;
                        if (dto == null) continue;
                        Uri targetUri;
                        if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(dto.ShihabUrl, false, out targetUri))
                        {
                            continue;
                        }
                        try
                        {
                            long len = ProbePublicUrlContentLength(targetUri);
                            string sz = len > 0 ? ShihabFormatHelpers.FormatMehediBytes(len) : "Unknown";
                            if (!IsDisposed && IsHandleCreated)
                            {
                                BeginInvoke((Action)delegate { if (!IsDisposed) rowRef.SubItems[2].Text = sz; });
                            }
                        }
                        catch { }
                    }
                    try
                    {
                        if (!IsDisposed && IsHandleCreated)
                        {
                            BeginInvoke((Action)delegate { if (!IsDisposed) probeSizesBtn.Enabled = true; });
                        }
                    }
                    catch { }
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

        private static long ProbePublicUrlContentLength(Uri initialUri)
        {
            Uri cur = initialUri;
            for (int hop = 0; hop < 4; hop++)
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(cur);
                req.Method = "HEAD";
                req.AllowAutoRedirect = false;
                req.Timeout = 4500;
                req.ReadWriteTimeout = 4500;
                req.UserAgent = "Mozilla/5.0 ShihabMehediIDM/1.0.6";
                try
                {
                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    {
                        int code = (int)resp.StatusCode;
                        if (code >= 300 && code < 400)
                        {
                            string loc = resp.Headers["Location"];
                            Uri next;
                            if (!string.IsNullOrEmpty(loc) &&
                                Uri.TryCreate(cur, loc, out next) &&
                                ShihabProcessAndPathSafety.TryValidateHttpUrl(next.AbsoluteUri, false, out next))
                            {
                                cur = next;
                                continue;
                            }
                            return -1;
                        }
                        return resp.ContentLength;
                    }
                }
                catch (WebException wex)
                {
                    HttpWebResponse errResp = wex.Response as HttpWebResponse;
                    if (errResp != null)
                    {
                        using (errResp)
                        {
                            int code = (int)errResp.StatusCode;
                            if (code >= 300 && code < 400)
                            {
                                string loc = errResp.Headers["Location"];
                                Uri next;
                                if (!string.IsNullOrEmpty(loc) &&
                                    Uri.TryCreate(cur, loc, out next) &&
                                    ShihabProcessAndPathSafety.TryValidateHttpUrl(next.AbsoluteUri, false, out next))
                                {
                                    cur = next;
                                    continue;
                                }
                            }
                        }
                    }
                    return -1;
                }
            }
            return -1;
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
        private static readonly Regex GrabberLinkRegex = new Regex(
            @"(?:href|src)\s*=\s*[""']([^""'#\s]{1,1024})[""']",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(250));

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
            Font = ShihabThemePalette.UiRegular9Font;
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
                    try { _shihabCrawlCts.Cancel(); _shihabCrawlCts.Dispose(); } catch { }
                    _shihabCrawlCts = null;
                    shihabExploreBtn.Text = "Start Explore";
                    return;
                }
                Uri mehediRoot;
                if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(shihabUrlBox.Text.Trim(), false, out mehediRoot))
                {
                    MessageBox.Show(this, "Please enter a valid public http:// or https:// start URL.", "Site Grabber", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

            FormClosing += delegate
            {
                if (_shihabCrawlCts != null)
                {
                    try { _shihabCrawlCts.Cancel(); _shihabCrawlCts.Dispose(); } catch { }
                    _shihabCrawlCts = null;
                }
            };

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

        private static string FetchPublicHtmlPageBounded(Uri pageUri)
        {
            const int maxBytes = 2 * 1024 * 1024;
            Uri cur = pageUri;
            for (int hop = 0; hop < 4; hop++)
            {
                HttpWebRequest shihabReq = (HttpWebRequest)WebRequest.Create(cur);
                shihabReq.AllowAutoRedirect = false;
                shihabReq.UserAgent = "Mozilla/5.0 ShihabMehediIDM-Grabber/1.0.6";
                shihabReq.Timeout = 10000;
                shihabReq.ReadWriteTimeout = 10000;
                using (HttpWebResponse mehediResp = (HttpWebResponse)shihabReq.GetResponse())
                {
                    int code = (int)mehediResp.StatusCode;
                    if (code >= 300 && code < 400)
                    {
                        string loc = mehediResp.Headers["Location"];
                        Uri next;
                        if (!string.IsNullOrEmpty(loc) &&
                            Uri.TryCreate(cur, loc, out next) &&
                            ShihabProcessAndPathSafety.TryValidateHttpUrl(next.AbsoluteUri, false, out next))
                        {
                            cur = next;
                            continue;
                        }
                        return "";
                    }
                    using (Stream stream = mehediResp.GetResponseStream())
                    {
                        if (stream == null) return "";
                        using (MemoryStream ms = new MemoryStream())
                        {
                            byte[] buf = new byte[16384];
                            int total = 0;
                            while (total < maxBytes)
                            {
                                int read = stream.Read(buf, 0, Math.Min(buf.Length, maxBytes - total));
                                if (read <= 0) break;
                                ms.Write(buf, 0, read);
                                total += read;
                            }
                            return Encoding.UTF8.GetString(ms.ToArray());
                        }
                    }
                }
            }
            return "";
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
                Uri verifiedPage;
                if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(shihabNode.Key.AbsoluteUri, false, out verifiedPage))
                {
                    continue;
                }

                string mehediHtml = "";
                try
                {
                    mehediHtml = FetchPublicHtmlPageBounded(verifiedPage);
                    mehediPagesProcessed++;
                }
                catch
                {
                    continue;
                }

                List<KeyValuePair<string, string>> pageBatch = new List<KeyValuePair<string, string>>();
                try
                {
                    MatchCollection shihabMatches = GrabberLinkRegex.Matches(mehediHtml);
                    for (int mehediM = 0; mehediM < shihabMatches.Count; mehediM++)
                    {
                        if (mehediToken.IsCancellationRequested) break;
                        Uri shihabCandidate;
                        if (!Uri.TryCreate(shihabNode.Key, shihabMatches[mehediM].Groups[1].Value, out shihabCandidate)) continue;
                        if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(shihabCandidate.AbsoluteUri, false, out shihabCandidate)) continue;
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
                                string mehediUpperExt = string.IsNullOrEmpty(mehediExt) ? "BIN" : mehediExt.ToUpperInvariant();
                                pageBatch.Add(new KeyValuePair<string, string>(shihabCandidate.AbsoluteUri, mehediUpperExt));
                            }
                        }
                    }
                }
                catch (RegexMatchTimeoutException) { }

                try
                {
                    int shihabPg = mehediPagesProcessed;
                    int mehediFoundCnt = mehediDiscoveredFiles.Count;
                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke((Action)delegate
                        {
                            if (IsDisposed) return;
                            if (pageBatch.Count > 0)
                            {
                                shihabView.BeginUpdate();
                                for (int bi = 0; bi < pageBatch.Count; bi++)
                                {
                                    ListViewItem shihabItem = new ListViewItem(new string[] { pageBatch[bi].Key, pageBatch[bi].Value });
                                    shihabItem.Checked = true;
                                    shihabView.Items.Add(shihabItem);
                                }
                                shihabView.EndUpdate();
                            }
                            mehediStats.Text = string.Format("Exploring... Web pages processed: {0}  |  Files Found: {1}", shihabPg, mehediFoundCnt);
                        });
                    }
                }
                catch { }
            }

            try
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)delegate
                    {
                        if (!IsDisposed)
                        {
                            if (_shihabCrawlCts != null)
                            {
                                try { _shihabCrawlCts.Dispose(); } catch { }
                                _shihabCrawlCts = null;
                            }
                            shihabBtn.Text = "Start Explore";
                            mehediStats.Text = string.Format("Complete — Web pages processed: {0}  |  Matched Files Found: {1}", mehediPagesProcessed, mehediDiscoveredFiles.Count);
                        }
                    });
                }
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
        private Bitmap _dropLogoBitmap;
        private Font _dropSubFont;

        public MehediFloatingDropTargetForm(
            Action<string> shihabOnUrlDropped,
            int initialX = -1,
            int initialY = -1,
            Action<int, int> onMoved = null)
        {
            _shihabOnUrlDropped = shihabOnUrlDropped;
            _shihabOnMoved = onMoved;
            _dropLogoBitmap = ShihabThemePalette.CreateMehediLogoBitmap(34);
            _dropSubFont = new Font("Segoe UI Semibold", 7.3f);

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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_dropLogoBitmap != null) { _dropLogoBitmap.Dispose(); _dropLogoBitmap = null; }
                if (_dropSubFont != null) { _dropSubFont.Dispose(); _dropSubFont = null; }
            }
            base.Dispose(disposing);
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
            using (SolidBrush shihabBrush = new SolidBrush(Color.FromArgb(230, 234, 245)))
            using (SolidBrush mehediViolet = new SolidBrush(Color.FromArgb(196, 181, 253)))
            {
                if (_dropLogoBitmap != null)
                {
                    mehediG.DrawImage(_dropLogoBitmap, (Width - 34) / 2, 7, 34, 34);
                }
                mehediG.DrawString("MHS DM", ShihabThemePalette.UiBold78Font, mehediViolet, 19, 44);
                mehediG.DrawString(_mehediSpeedLabel, _dropSubFont, shihabBrush, 14, 62);
            }
        }
    }
}

