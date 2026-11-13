using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ShihabMehediIDM
{
    internal sealed class ShihabSidebarNavEntry
    {
        public string ShihabTag;
        public string MehediTitle;
        public int ShihabCount;

        public ShihabSidebarNavEntry(string shihabTag, string mehediTitle)
        {
            ShihabTag = shihabTag;
            MehediTitle = mehediTitle;
            ShihabCount = 0;
        }
    }

    internal sealed class ShihabTransfersNavControl : Control
    {
        private const int RowPitch = 34;

        public bool ShihabDarkMode { get; set; }
        public string MehediActiveTag { get; set; }
        public event EventHandler OnShihabFilterSelected;

        private readonly List<ShihabSidebarNavEntry> _shihabEntries = new List<ShihabSidebarNavEntry>();
        private int _mehediHoveredIndex = -1;
        private Font _navRegularFont;
        private Font _navBoldFont;

        public ShihabTransfersNavControl()
        {
            ShihabDarkMode = true;
            MehediActiveTag = "ALL";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;

            _navRegularFont = new Font("Segoe UI", 9.1f, FontStyle.Regular);
            _navBoldFont = new Font("Segoe UI", 9.1f, FontStyle.Bold);

            _shihabEntries.Add(new ShihabSidebarNavEntry("ALL", "All Downloads"));
            _shihabEntries.Add(new ShihabSidebarNavEntry("DOWNLOADING", "Downloading"));
            _shihabEntries.Add(new ShihabSidebarNavEntry("FINISHED", "Completed"));
            _shihabEntries.Add(new ShihabSidebarNavEntry("PAUSED", "Paused"));
            _shihabEntries.Add(new ShihabSidebarNavEntry("QUEUES_ALL", "Scheduled"));
            _shihabEntries.Add(new ShihabSidebarNavEntry("CAT:Video", "Video"));
            _shihabEntries.Add(new ShihabSidebarNavEntry("CAT:Audio", "Audio"));
            _shihabEntries.Add(new ShihabSidebarNavEntry("CAT:Documents", "Documents"));
            _shihabEntries.Add(new ShihabSidebarNavEntry("CAT:Compressed", "Compressed"));
            _shihabEntries.Add(new ShihabSidebarNavEntry("CAT:Programs", "Programs"));
            _shihabEntries.Add(new ShihabSidebarNavEntry("CAT:Torrents", "Torrents"));
        }

        public string GetActiveDisplayTitle()
        {
            for (int shihabI = 0; shihabI < _shihabEntries.Count; shihabI++)
            {
                if (string.Equals(_shihabEntries[shihabI].ShihabTag, MehediActiveTag, StringComparison.OrdinalIgnoreCase))
                {
                    return _shihabEntries[shihabI].MehediTitle;
                }
            }
            if (MehediActiveTag != null && MehediActiveTag.StartsWith("QUEUE:"))
            {
                return MehediActiveTag.Substring(6);
            }
            if (MehediActiveTag != null && MehediActiveTag.StartsWith("CAT:"))
            {
                return MehediActiveTag.Substring(4);
            }
            return "All Downloads";
        }

        public void UpdateShihabCounts(int[] mehediCounts)
        {
            if (mehediCounts == null || mehediCounts.Length < _shihabEntries.Count) return;
            for (int mehediI = 0; mehediI < _shihabEntries.Count; mehediI++)
            {
                if (_shihabEntries[mehediI].ShihabCount != mehediCounts[mehediI])
                {
                    _shihabEntries[mehediI].ShihabCount = mehediCounts[mehediI];
                    Invalidate(GetRowDirtyRect(mehediI));
                }
            }
        }

        private Rectangle GetRowDirtyRect(int idx)
        {
            if (idx < 0 || idx >= _shihabEntries.Count) return Rectangle.Empty;
            return new Rectangle(6, 2 + idx * RowPitch, Math.Max(32, Width - 12), RowPitch);
        }

        private int HitTestMehediIndex(int shihabY)
        {
            int shihabIdx = (shihabY - 2) / RowPitch;
            if (shihabIdx < 0 || shihabIdx >= _shihabEntries.Count) return -1;
            return shihabIdx;
        }

        protected override void OnMouseMove(MouseEventArgs shihabE)
        {
            base.OnMouseMove(shihabE);
            int mehediHit = HitTestMehediIndex(shihabE.Y);
            if (mehediHit != _mehediHoveredIndex)
            {
                int prev = _mehediHoveredIndex;
                _mehediHoveredIndex = mehediHit;
                if (prev >= 0) Invalidate(GetRowDirtyRect(prev));
                if (mehediHit >= 0) Invalidate(GetRowDirtyRect(mehediHit));
            }
        }

        protected override void OnMouseLeave(EventArgs mehediE)
        {
            base.OnMouseLeave(mehediE);
            if (_mehediHoveredIndex != -1)
            {
                int prev = _mehediHoveredIndex;
                _mehediHoveredIndex = -1;
                Invalidate(GetRowDirtyRect(prev));
            }
        }

        protected override void OnMouseDown(MouseEventArgs shihabE)
        {
            base.OnMouseDown(shihabE);
            if (shihabE.Button != MouseButtons.Left) return;
            int mehediHit = HitTestMehediIndex(shihabE.Y);
            if (mehediHit >= 0 && mehediHit < _shihabEntries.Count)
            {
                string nextTag = _shihabEntries[mehediHit].ShihabTag;
                if (!string.Equals(MehediActiveTag, nextTag, StringComparison.OrdinalIgnoreCase))
                {
                    MehediActiveTag = nextTag;
                    Invalidate();
                }
                if (OnShihabFilterSelected != null)
                {
                    OnShihabFilterSelected(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs shihabE)
        {
            Graphics mehediG = shihabE.Graphics;
            mehediG.SmoothingMode = SmoothingMode.AntiAlias;
            mehediG.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Color shihabSideBg = ShihabThemePalette.GetShihabSidebarBg(ShihabDarkMode);
            using (SolidBrush mehediBgBr = new SolidBrush(shihabSideBg))
            {
                mehediG.FillRectangle(mehediBgBr, shihabE.ClipRectangle);
            }

            Rectangle clip = shihabE.ClipRectangle;
            Font regFont = _navRegularFont ?? Font;
            Font bldFont = _navBoldFont ?? Font;

            for (int shihabI = 0; shihabI < _shihabEntries.Count; shihabI++)
            {
                int shihabTop = 2 + shihabI * RowPitch;
                Rectangle mehediPillRect = new Rectangle(10, shihabTop, Math.Max(24, Width - 21), 30);
                if (!clip.IsEmpty && !mehediPillRect.IntersectsWith(clip)) continue;

                ShihabSidebarNavEntry mehediEntry = _shihabEntries[shihabI];
                bool shihabActive = string.Equals(mehediEntry.ShihabTag, MehediActiveTag, StringComparison.OrdinalIgnoreCase);
                bool mehediHot = shihabI == _mehediHoveredIndex && !shihabActive;

                if (shihabActive || mehediHot)
                {
                    Color shihabPillFill = shihabActive
                        ? ShihabThemePalette.GetShihabAccentBlue()
                        : ShihabThemePalette.GetShihabSurfaceBg(ShihabDarkMode);
                    using (GraphicsPath mehediPath = ShihabThemePalette.CreateShihabRoundedRect(mehediPillRect, 7))
                    using (SolidBrush shihabFillBr = new SolidBrush(shihabPillFill))
                    {
                        mehediG.FillPath(shihabFillBr, mehediPath);
                    }
                }

                string shihabDisplay = string.Format("{0} ({1})", mehediEntry.MehediTitle, mehediEntry.ShihabCount);
                Color mehediTxtCol = shihabActive
                    ? Color.White
                    : (mehediHot ? ShihabThemePalette.GetShihabPrimaryText(ShihabDarkMode) : ShihabThemePalette.GetMehediMutedText(ShihabDarkMode));

                Rectangle shihabTxtBounds = new Rectangle(mehediPillRect.X + 12, mehediPillRect.Y, Math.Max(12, mehediPillRect.Width - 20), mehediPillRect.Height);
                TextRenderer.DrawText(
                    mehediG,
                    shihabDisplay,
                    shihabActive ? bldFont : regFont,
                    shihabTxtBounds,
                    mehediTxtCol,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_navRegularFont != null) { _navRegularFont.Dispose(); _navRegularFont = null; }
                if (_navBoldFont != null) { _navBoldFont.Dispose(); _navBoldFont = null; }
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class ShihabLocalStorageCardControl : Control
    {
        public bool ShihabDarkMode { get; set; }
        public event EventHandler OnMehediSpeedToggleClicked;

        private double _shihabUsedGb = 0.0;
        private double _mehediTotalGb = 0.0;
        private double _shihabFreeGb = 0.0;
        private string _mehediSpeedLabel = "Speed: Unlimited";
        private Rectangle _shihabSpeedHitRect = Rectangle.Empty;

        private Font _mehediTitleFont;
        private Font _shihabSubFont;
        private Font _mehediSpeedFont;

        public ShihabLocalStorageCardControl()
        {
            ShihabDarkMode = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _mehediTitleFont = new Font("Segoe UI", 8.1f, FontStyle.Bold);
            _shihabSubFont = new Font("Segoe UI", 7.5f, FontStyle.Regular);
            _mehediSpeedFont = new Font("Segoe UI Semibold", 7.5f, FontStyle.Regular);
        }

        public void SetShihabStorageAndSpeed(double shihabUsedGb, double mehediTotalGb, double shihabFreeGb, string mehediSpeedText)
        {
            bool shihabDiff = Math.Abs(_shihabUsedGb - shihabUsedGb) > 0.05 ||
                              Math.Abs(_mehediTotalGb - mehediTotalGb) > 0.05 ||
                              Math.Abs(_shihabFreeGb - shihabFreeGb) > 0.05 ||
                              !string.Equals(_mehediSpeedLabel, mehediSpeedText, StringComparison.Ordinal);
            _shihabUsedGb = shihabUsedGb;
            _mehediTotalGb = mehediTotalGb;
            _shihabFreeGb = shihabFreeGb;
            _mehediSpeedLabel = mehediSpeedText ?? "Speed: Unlimited";
            if (shihabDiff)
            {
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs mehediE)
        {
            base.OnMouseMove(mehediE);
            Cursor next = _shihabSpeedHitRect.Contains(mehediE.Location) ? Cursors.Hand : Cursors.Default;
            if (Cursor != next) Cursor = next;
        }

        protected override void OnMouseDown(MouseEventArgs shihabE)
        {
            base.OnMouseDown(shihabE);
            if (shihabE.Button == MouseButtons.Left && _shihabSpeedHitRect.Contains(shihabE.Location))
            {
                if (OnMehediSpeedToggleClicked != null)
                {
                    OnMehediSpeedToggleClicked(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs shihabE)
        {
            Graphics mehediG = shihabE.Graphics;
            mehediG.SmoothingMode = SmoothingMode.AntiAlias;
            mehediG.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Color shihabOuterBg = ShihabDarkMode ? Color.FromArgb(18, 20, 24) : Color.FromArgb(241, 245, 249);
            using (SolidBrush mehediOuterBr = new SolidBrush(shihabOuterBg))
            {
                mehediG.FillRectangle(mehediOuterBr, ClientRectangle);
            }

            Rectangle shihabCard = new Rectangle(10, 4, Math.Max(20, Width - 21), Math.Max(20, Height - 14));
            Color mehediCardFill = ShihabDarkMode ? Color.FromArgb(12, 14, 18) : Color.White;
            Color shihabCardBorder = ShihabDarkMode ? Color.FromArgb(34, 38, 48) : Color.FromArgb(203, 213, 225);

            using (GraphicsPath mehediCardPath = ShihabThemePalette.CreateShihabRoundedRect(shihabCard, 9))
            using (SolidBrush shihabCardBr = new SolidBrush(mehediCardFill))
            using (Pen mehediCardPen = new Pen(shihabCardBorder, 1f))
            {
                mehediG.FillPath(shihabCardBr, mehediCardPath);
                mehediG.DrawPath(mehediCardPen, mehediCardPath);
            }

            string shihabCapBadgeTxt = _mehediTotalGb > 0.1
                ? string.Format("{0:0.0} / {1:0} GB", _shihabUsedGb, _mehediTotalGb)
                : "-- / -- GB";

            Font titleFont = _mehediTitleFont ?? Font;
            Font subFont = _shihabSubFont ?? Font;
            Font speedFont = _mehediSpeedFont ?? Font;

            Size shihabBadgeMeasure = TextRenderer.MeasureText(mehediG, shihabCapBadgeTxt, subFont, new Size(200, 20), TextFormatFlags.NoPadding);
            int mehediBadgeW = Math.Min(shihabCard.Width - 82, shihabBadgeMeasure.Width + 10);
            Rectangle shihabBadgeRect = new Rectangle(shihabCard.Right - mehediBadgeW - 8, shihabCard.Y + 8, Math.Max(30, mehediBadgeW), 18);

            using (GraphicsPath mehediBadgePath = ShihabThemePalette.CreateShihabRoundedRect(shihabBadgeRect, 5))
            using (SolidBrush shihabBadgeBr = new SolidBrush(ShihabDarkMode ? Color.FromArgb(23, 27, 36) : Color.FromArgb(241, 245, 249)))
            {
                mehediG.FillPath(shihabBadgeBr, mehediBadgePath);
            }

            TextRenderer.DrawText(
                mehediG,
                shihabCapBadgeTxt,
                subFont,
                shihabBadgeRect,
                ShihabDarkMode ? Color.FromArgb(154, 163, 184) : Color.FromArgb(71, 85, 105),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            Rectangle mehediTitleRect = new Rectangle(shihabCard.X + 10, shihabCard.Y + 8, Math.Max(20, shihabBadgeRect.Left - shihabCard.X - 12), 18);
            TextRenderer.DrawText(
                mehediG,
                "Local Storage",
                titleFont,
                mehediTitleRect,
                ShihabDarkMode ? Color.FromArgb(232, 237, 246) : Color.FromArgb(15, 23, 42),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            Rectangle shihabTrack = new Rectangle(shihabCard.X + 10, shihabCard.Y + 34, Math.Max(20, shihabCard.Width - 20), 5);
            using (GraphicsPath mehediTrackPath = ShihabThemePalette.CreateShihabRoundedRect(shihabTrack, 2))
            using (SolidBrush shihabTrackBr = new SolidBrush(ShihabDarkMode ? Color.FromArgb(30, 36, 48) : Color.FromArgb(220, 226, 236)))
            {
                mehediG.FillPath(shihabTrackBr, mehediTrackPath);
            }

            double mehediRatio = _mehediTotalGb > 0.1 ? Math.Max(0.0, Math.Min(1.0, _shihabUsedGb / _mehediTotalGb)) : 0.0;
            if (mehediRatio > 0.01)
            {
                int shihabFillW = Math.Max(6, (int)Math.Round(shihabTrack.Width * mehediRatio));
                Rectangle mehediFillRect = new Rectangle(shihabTrack.X, shihabTrack.Y, Math.Min(shihabTrack.Width, shihabFillW), shihabTrack.Height);
                using (GraphicsPath shihabFillPath = ShihabThemePalette.CreateShihabRoundedRect(mehediFillRect, 2))
                using (SolidBrush mehediFillBr = new SolidBrush(Color.FromArgb(24, 119, 242)))
                {
                    mehediG.FillPath(mehediFillBr, shihabFillPath);
                }
            }

            string shihabFreeTxt = string.Format("Free: {0:0.0} GB", _shihabFreeGb);
            Size mehediSpeedSz = TextRenderer.MeasureText(mehediG, _mehediSpeedLabel, speedFont, new Size(160, 18), TextFormatFlags.NoPadding);
            int shihabSpeedW = Math.Min(shihabCard.Width - 78, mehediSpeedSz.Width + 6);

            _shihabSpeedHitRect = new Rectangle(shihabCard.Right - shihabSpeedW - 8, shihabCard.Bottom - 23, shihabSpeedW, 17);
            Rectangle mehediFreeRect = new Rectangle(shihabCard.X + 10, shihabCard.Bottom - 23, Math.Max(20, _shihabSpeedHitRect.Left - shihabCard.X - 12), 17);

            TextRenderer.DrawText(
                mehediG,
                shihabFreeTxt,
                subFont,
                mehediFreeRect,
                ShihabDarkMode ? Color.FromArgb(124, 133, 152) : Color.FromArgb(100, 116, 139),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(
                mehediG,
                _mehediSpeedLabel,
                speedFont,
                _shihabSpeedHitRect,
                Color.FromArgb(37, 132, 255),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_mehediTitleFont != null) { _mehediTitleFont.Dispose(); _mehediTitleFont = null; }
                if (_shihabSubFont != null) { _shihabSubFont.Dispose(); _shihabSubFont = null; }
                if (_mehediSpeedFont != null) { _mehediSpeedFont.Dispose(); _mehediSpeedFont = null; }
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class ShihabHeaderPillButton : Control
    {
        public bool ShihabDarkMode { get; set; }
        public bool MehediPrimaryAccent { get; set; }
        private bool _shihabHovered;
        private bool _mehediPressed;

        public ShihabHeaderPillButton(string shihabText, int mehediWidth, bool shihabPrimary)
        {
            Text = shihabText;
            Size = new Size(mehediWidth, 28);
            ShihabDarkMode = true;
            MehediPrimaryAccent = shihabPrimary;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs shihabE)
        {
            base.OnMouseEnter(shihabE);
            if (!_shihabHovered)
            {
                _shihabHovered = true;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs mehediE)
        {
            base.OnMouseLeave(mehediE);
            if (_shihabHovered || _mehediPressed)
            {
                _shihabHovered = false;
                _mehediPressed = false;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs shihabE)
        {
            base.OnMouseDown(shihabE);
            if (shihabE.Button == MouseButtons.Left && !_mehediPressed)
            {
                _mehediPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs mehediE)
        {
            base.OnMouseUp(mehediE);
            if (_mehediPressed)
            {
                _mehediPressed = false;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs shihabE)
        {
            Graphics mehediG = shihabE.Graphics;
            mehediG.SmoothingMode = SmoothingMode.AntiAlias;
            mehediG.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Color shihabParentBg = Parent != null ? Parent.BackColor : Color.FromArgb(22, 24, 29);
            using (SolidBrush mehediOuter = new SolidBrush(shihabParentBg))
            {
                mehediG.FillRectangle(mehediOuter, ClientRectangle);
            }

            Rectangle shihabRect = new Rectangle(0, 0, Math.Max(6, Width - 1), Math.Max(6, Height - 1));
            Color mehediFill;
            Color shihabBorder;
            Color mehediFg;

            if (MehediPrimaryAccent)
            {
                mehediFill = _mehediPressed
                    ? Color.FromArgb(10, 96, 196)
                    : (_shihabHovered ? Color.FromArgb(24, 130, 248) : Color.FromArgb(13, 116, 231));
                shihabBorder = Color.FromArgb(37, 138, 250);
                mehediFg = Color.White;
            }
            else if (ShihabDarkMode)
            {
                mehediFill = _mehediPressed
                    ? Color.FromArgb(26, 30, 38)
                    : (_shihabHovered ? Color.FromArgb(42, 48, 60) : Color.FromArgb(33, 37, 46));
                shihabBorder = _shihabHovered ? Color.FromArgb(64, 73, 92) : Color.FromArgb(48, 54, 68);
                mehediFg = Color.FromArgb(229, 234, 243);
            }
            else
            {
                mehediFill = _shihabHovered ? Color.FromArgb(226, 232, 240) : Color.White;
                shihabBorder = Color.FromArgb(196, 206, 220);
                mehediFg = Color.FromArgb(15, 23, 42);
            }

            using (GraphicsPath mehediPath = ShihabThemePalette.CreateShihabRoundedRect(shihabRect, 6))
            using (SolidBrush shihabBr = new SolidBrush(mehediFill))
            using (Pen mehediPen = new Pen(shihabBorder, 1f))
            {
                mehediG.FillPath(shihabBr, mehediPath);
                mehediG.DrawPath(mehediPen, mehediPath);
                TextRenderer.DrawText(
                    mehediG,
                    Text,
                    ShihabThemePalette.UiSemibold86Font,
                    shihabRect,
                    mehediFg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }
    }

    internal sealed class ShihabTransferFooterBarControl : Control
    {
        public bool ShihabDarkMode { get; set; }
        private string _mehediTotalText = "Total: 0 B";
        private string _shihabDownloadedText = "Downloaded: 0 B";
        private string _mehediSpeedText = "Speed: 0 KB/s";
        private string _shihabSocketsText = "Active Sockets: 0";
        private Font _mehediPillFont;

        public ShihabTransferFooterBarControl()
        {
            ShihabDarkMode = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _mehediPillFont = new Font("Segoe UI Semibold", 8.1f, FontStyle.Regular);
        }

        public void SetMehediFooterMetrics(string shihabTotal, string mehediDownloaded, string shihabSpeed, string mehediSockets)
        {
            if (string.Equals(_mehediTotalText, shihabTotal, StringComparison.Ordinal) &&
                string.Equals(_shihabDownloadedText, mehediDownloaded, StringComparison.Ordinal) &&
                string.Equals(_mehediSpeedText, shihabSpeed, StringComparison.Ordinal) &&
                string.Equals(_shihabSocketsText, mehediSockets, StringComparison.Ordinal))
            {
                return;
            }
            _mehediTotalText = shihabTotal;
            _shihabDownloadedText = mehediDownloaded;
            _mehediSpeedText = shihabSpeed;
            _shihabSocketsText = mehediSockets;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs shihabE)
        {
            Graphics mehediG = shihabE.Graphics;
            mehediG.SmoothingMode = SmoothingMode.AntiAlias;
            mehediG.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Color shihabFooterBg = ShihabDarkMode ? Color.FromArgb(18, 20, 24) : Color.FromArgb(241, 245, 249);
            Color mehediTopBorder = ShihabDarkMode ? Color.FromArgb(35, 39, 48) : Color.FromArgb(206, 214, 224);

            using (SolidBrush shihabBgBr = new SolidBrush(shihabFooterBg))
            using (Pen mehediTopPen = new Pen(mehediTopBorder, 1f))
            {
                mehediG.FillRectangle(shihabBgBr, ClientRectangle);
                mehediG.DrawLine(mehediTopPen, 0, 0, Width, 0);
            }

            Color shihabNeutralFill = ShihabDarkMode ? Color.FromArgb(24, 27, 34) : Color.White;
            Color mehediNeutralEdge = ShihabDarkMode ? Color.FromArgb(38, 43, 54) : Color.FromArgb(203, 213, 225);
            Color shihabNeutralFg = ShihabDarkMode ? Color.FromArgb(154, 163, 181) : Color.FromArgb(71, 85, 105);

            Color mehediGreenFill = ShihabDarkMode ? Color.FromArgb(16, 35, 29) : Color.FromArgb(220, 252, 231);
            Color shihabGreenEdge = ShihabDarkMode ? Color.FromArgb(27, 67, 53) : Color.FromArgb(134, 239, 172);
            Color mehediGreenFg = ShihabDarkMode ? Color.FromArgb(16, 185, 129) : Color.FromArgb(21, 128, 61);

            Color shihabBlueFill = ShihabDarkMode ? Color.FromArgb(17, 30, 51) : Color.FromArgb(219, 234, 254);
            Color mehediBlueEdge = ShihabDarkMode ? Color.FromArgb(29, 56, 97) : Color.FromArgb(147, 197, 253);
            Color shihabBlueFg = ShihabDarkMode ? Color.FromArgb(59, 130, 246) : Color.FromArgb(29, 78, 216);

            Font pillFont = _mehediPillFont ?? Font;
            int shihabLeftX = 12;
            shihabLeftX = DrawShihabFooterPill(mehediG, pillFont, _mehediTotalText, shihabLeftX, false, shihabNeutralFill, mehediNeutralEdge, shihabNeutralFg) + 8;
            DrawShihabFooterPill(mehediG, pillFont, _shihabDownloadedText, shihabLeftX, false, mehediGreenFill, shihabGreenEdge, mehediGreenFg);

            int mehediRightX = Width - 12;
            mehediRightX = DrawShihabFooterPill(mehediG, pillFont, _shihabSocketsText, mehediRightX, true, shihabNeutralFill, mehediNeutralEdge, shihabNeutralFg) - 8;
            DrawShihabFooterPill(mehediG, pillFont, _mehediSpeedText, mehediRightX, true, shihabBlueFill, mehediBlueEdge, shihabBlueFg);
        }

        private int DrawShihabFooterPill(
            Graphics mehediG,
            Font shihabFont,
            string mehediText,
            int shihabAnchorX,
            bool mehediAlignRight,
            Color shihabFill,
            Color mehediEdge,
            Color shihabFg)
        {
            Size mehediMeasure = TextRenderer.MeasureText(mehediG, mehediText, shihabFont, new Size(320, 24), TextFormatFlags.NoPadding);
            int shihabPillW = mehediMeasure.Width + 20;
            int mehediPillH = 22;
            int shihabPillX = mehediAlignRight ? (shihabAnchorX - shihabPillW) : shihabAnchorX;
            int mehediPillY = (Height - mehediPillH) / 2 + 1;

            Rectangle shihabRect = new Rectangle(shihabPillX, mehediPillY, shihabPillW, mehediPillH);
            using (GraphicsPath mehediPath = ShihabThemePalette.CreateShihabRoundedRect(shihabRect, 9))
            using (SolidBrush shihabBr = new SolidBrush(shihabFill))
            using (Pen mehediPen = new Pen(mehediEdge, 1f))
            {
                mehediG.FillPath(shihabBr, mehediPath);
                mehediG.DrawPath(mehediPen, mehediPath);
                TextRenderer.DrawText(
                    mehediG,
                    mehediText,
                    shihabFont,
                    shihabRect,
                    shihabFg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
            return mehediAlignRight ? shihabPillX : (shihabPillX + shihabPillW);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _mehediPillFont != null)
            {
                _mehediPillFont.Dispose();
                _mehediPillFont = null;
            }
            base.Dispose(disposing);
        }
    }
}

