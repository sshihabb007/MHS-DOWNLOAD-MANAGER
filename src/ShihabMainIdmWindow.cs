using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
        public bool ShihabDarkMode { get; set; }
        public string MehediActiveTag { get; set; }
        public event EventHandler OnShihabFilterSelected;

        private readonly List<ShihabSidebarNavEntry> _shihabEntries = new List<ShihabSidebarNavEntry>();
        private int _mehediHoveredIndex = -1;

        public ShihabTransfersNavControl()
        {
            ShihabDarkMode = true;
            MehediActiveTag = "ALL";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;

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
            bool shihabChanged = false;
            for (int mehediI = 0; mehediI < _shihabEntries.Count; mehediI++)
            {
                if (_shihabEntries[mehediI].ShihabCount != mehediCounts[mehediI])
                {
                    _shihabEntries[mehediI].ShihabCount = mehediCounts[mehediI];
                    shihabChanged = true;
                }
            }
            if (shihabChanged)
            {
                Invalidate();
            }
        }

        private int HitTestMehediIndex(int shihabY)
        {
            int mehediRowPitch = 34;
            int shihabIdx = (shihabY - 2) / mehediRowPitch;
            if (shihabIdx < 0 || shihabIdx >= _shihabEntries.Count) return -1;
            return shihabIdx;
        }

        protected override void OnMouseMove(MouseEventArgs shihabE)
        {
            base.OnMouseMove(shihabE);
            int mehediHit = HitTestMehediIndex(shihabE.Y);
            if (mehediHit != _mehediHoveredIndex)
            {
                _mehediHoveredIndex = mehediHit;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs mehediE)
        {
            base.OnMouseLeave(mehediE);
            if (_mehediHoveredIndex != -1)
            {
                _mehediHoveredIndex = -1;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs shihabE)
        {
            base.OnMouseDown(shihabE);
            if (shihabE.Button != MouseButtons.Left) return;
            int mehediHit = HitTestMehediIndex(shihabE.Y);
            if (mehediHit >= 0 && mehediHit < _shihabEntries.Count)
            {
                MehediActiveTag = _shihabEntries[mehediHit].ShihabTag;
                Invalidate();
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
                mehediG.FillRectangle(mehediBgBr, ClientRectangle);
            }

            int mehediRowPitch = 34;
            for (int shihabI = 0; shihabI < _shihabEntries.Count; shihabI++)
            {
                ShihabSidebarNavEntry mehediEntry = _shihabEntries[shihabI];
                int shihabTop = 2 + shihabI * mehediRowPitch;
                Rectangle mehediPillRect = new Rectangle(10, shihabTop, Math.Max(24, Width - 21), 30);

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
                using (Font mehediNavFont = new Font("Segoe UI", 9.1f, shihabActive ? FontStyle.Bold : FontStyle.Regular))
                {
                    TextRenderer.DrawText(
                        mehediG,
                        shihabDisplay,
                        mehediNavFont,
                        shihabTxtBounds,
                        mehediTxtCol,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
            }
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

        public ShihabLocalStorageCardControl()
        {
            ShihabDarkMode = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
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
            Cursor = _shihabSpeedHitRect.Contains(mehediE.Location) ? Cursors.Hand : Cursors.Default;
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

            using (Font mehediTitleFont = new Font("Segoe UI", 8.1f, FontStyle.Bold))
            using (Font shihabSubFont = new Font("Segoe UI", 7.5f, FontStyle.Regular))
            using (Font mehediSpeedFont = new Font("Segoe UI Semibold", 7.5f, FontStyle.Regular))
            {
                Size shihabBadgeMeasure = TextRenderer.MeasureText(mehediG, shihabCapBadgeTxt, shihabSubFont, new Size(200, 20), TextFormatFlags.NoPadding);
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
                    shihabSubFont,
                    shihabBadgeRect,
                    ShihabDarkMode ? Color.FromArgb(154, 163, 184) : Color.FromArgb(71, 85, 105),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                Rectangle mehediTitleRect = new Rectangle(shihabCard.X + 10, shihabCard.Y + 8, Math.Max(20, shihabBadgeRect.Left - shihabCard.X - 12), 18);
                TextRenderer.DrawText(
                    mehediG,
                    "Local Storage",
                    mehediTitleFont,
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
                Size mehediSpeedSz = TextRenderer.MeasureText(mehediG, _mehediSpeedLabel, mehediSpeedFont, new Size(160, 18), TextFormatFlags.NoPadding);
                int shihabSpeedW = Math.Min(shihabCard.Width - 78, mehediSpeedSz.Width + 6);

                _shihabSpeedHitRect = new Rectangle(shihabCard.Right - shihabSpeedW - 8, shihabCard.Bottom - 23, shihabSpeedW, 17);
                Rectangle mehediFreeRect = new Rectangle(shihabCard.X + 10, shihabCard.Bottom - 23, Math.Max(20, _shihabSpeedHitRect.Left - shihabCard.X - 12), 17);

                TextRenderer.DrawText(
                    mehediG,
                    shihabFreeTxt,
                    shihabSubFont,
                    mehediFreeRect,
                    ShihabDarkMode ? Color.FromArgb(124, 133, 152) : Color.FromArgb(100, 116, 139),
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                TextRenderer.DrawText(
                    mehediG,
                    _mehediSpeedLabel,
                    mehediSpeedFont,
                    _shihabSpeedHitRect,
                    Color.FromArgb(37, 132, 255),
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
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
            _shihabHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs mehediE)
        {
            base.OnMouseLeave(mehediE);
            _shihabHovered = false;
            _mehediPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs shihabE)
        {
            base.OnMouseDown(shihabE);
            if (shihabE.Button == MouseButtons.Left)
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
            using (Font shihabBtnFont = new Font("Segoe UI Semibold", 8.6f, FontStyle.Regular))
            {
                mehediG.FillPath(shihabBr, mehediPath);
                mehediG.DrawPath(mehediPen, mehediPath);
                TextRenderer.DrawText(
                    mehediG,
                    Text,
                    shihabBtnFont,
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

        public ShihabTransferFooterBarControl()
        {
            ShihabDarkMode = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
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

            using (Font mehediPillFont = new Font("Segoe UI Semibold", 8.1f, FontStyle.Regular))
            {
                int shihabLeftX = 12;
                shihabLeftX = DrawShihabFooterPill(mehediG, mehediPillFont, _mehediTotalText, shihabLeftX, false, shihabNeutralFill, mehediNeutralEdge, shihabNeutralFg) + 8;
                DrawShihabFooterPill(mehediG, mehediPillFont, _shihabDownloadedText, shihabLeftX, false, mehediGreenFill, shihabGreenEdge, mehediGreenFg);

                int mehediRightX = Width - 12;
                mehediRightX = DrawShihabFooterPill(mehediG, mehediPillFont, _shihabSocketsText, mehediRightX, true, shihabNeutralFill, mehediNeutralEdge, shihabNeutralFg) - 8;
                DrawShihabFooterPill(mehediG, mehediPillFont, _mehediSpeedText, mehediRightX, true, shihabBlueFill, mehediBlueEdge, shihabBlueFg);
            }
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
    }

    public class ShihabMainIdmWindow : Form
    {
        [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMehediCueBannerMessage(IntPtr shihabHwnd, int mehediMsg, IntPtr shihabWParam, string mehediLParam);

        private readonly ShihabStateStore _shihabStore;
        private readonly MehediAppConfiguration _mehediConfig;
        private readonly MehediDynamicSegmentEngine _shihabEngine;
        private readonly MehediBrowserBridgeServer _mehediBridge;
        private readonly ShihabCliParsedArguments _shihabStartupCli;
        private readonly Dictionary<string, ShihabDownloadProgressForm> _mehediOpenProgressForms;

        private MenuStrip _shihabMenuBar;
        private ContextMenuStrip _mehediOverflowAppMenu;
        private Panel _shihabSidebarPanel;
        private Panel _mehediSidebarTopHeading;
        private ShihabTransfersNavControl _shihabTransfersNav;
        private ShihabLocalStorageCardControl _mehediStorageCard;

        private Panel _shihabRightWorkspacePanel;
        private Panel _mehediTopActionHeaderBar;
        private string _shihabHeaderViewTitle = "All Downloads";
        private string _mehediHeaderItemsBadgeText = "0 items";

        private ShihabHeaderPillButton _shihabAddUrlPillBtn;
        private ShihabHeaderPillButton _shihabTorrentPillBtn;
        private ShihabHeaderPillButton _shihabMediaPillBtn;
        private ShihabHeaderPillButton _mehediStartPillBtn;
        private ShihabHeaderPillButton _shihabPausePillBtn;
        private ShihabHeaderPillButton _mehediFolderPillBtn;
        private ShihabHeaderPillButton _shihabRemotePillBtn;
        private ShihabHeaderPillButton _shihabUpdatePillBtn;
        private ShihabHeaderPillButton _shihabAboutPillBtn;
        private ShihabHeaderPillButton _mehediMenuPillBtn;
        private ShihabRemoteControlServer _shihabRemoteServer;

        private ShihabRoundedCardPanel _shihabSearchBarPanel;
        private TextBox _mehediSearchBox;
        private Button _shihabClearSearchBtn;

        private ShihabModernListView _shihabDownloadsListView;
        private ShihabTransferFooterBarControl _mehediFooterBar;

        private NotifyIcon _mehediTrayIcon;
        private ContextMenuStrip _shihabTrayMenuRef;
        private MehediFloatingDropTargetForm _shihabDropTargetForm;
        private Timer _mehediUiTickTimer;
        private Timer _shihabPeriodicUpdateTimer;
        private Timer _shihabRssPollTimer;
        private ShihabUpdateCheckResult _shihabLatestDetectedUpdate;
        private bool _shihabUpdateCheckInFlight = false;
        private bool _shihabUpdateDialogOpen = false;
        private bool _shihabRssPollInFlight = false;
        private bool _shihabClipboardPollInFlight = false;

        private string _shihabActiveTreeFilter = "ALL";
        private string _mehediSortColumnKey = "ORDER";
        private bool _shihabSortAscending = false;
        private string _shihabLastClipboardText = "";
        private bool _mehediBridgeListening = false;
        private bool _shihabExitingExplicitly = false;
        private DateTime _mehediLastDrivePollUtc = DateTime.MinValue;
        private double _shihabCachedUsedGb = 0.0;
        private double _mehediCachedTotalGb = 0.0;
        private double _shihabCachedFreeGb = 0.0;
        private readonly Queue<ShihabDownloadItem> _mehediPendingCaptureModalQueue = new Queue<ShihabDownloadItem>();
        private bool _shihabShowingCaptureModal = false;
        private readonly Dictionary<string, DateTime> _mehediRecentCapturedUrlsUtc = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        public ShihabMainIdmWindow(
            ShihabStateStore shihabStore,
            MehediAppConfiguration mehediConfig,
            MehediDynamicSegmentEngine shihabEngine,
            MehediBrowserBridgeServer mehediBridge,
            ShihabCliParsedArguments shihabStartupCli)
        {
            _shihabStore = shihabStore;
            _mehediConfig = mehediConfig;
            _shihabEngine = shihabEngine;
            _mehediBridge = mehediBridge;
            _shihabStartupCli = shihabStartupCli;
            _mehediOpenProgressForms = new Dictionary<string, ShihabDownloadProgressForm>();
            try
            {
                if (Clipboard.ContainsText())
                {
                    _shihabLastClipboardText = (Clipboard.GetText() ?? "").Trim();
                }
            }
            catch { }

            Text = ShihabThemePalette.ShihabMainHeaderTitle;
            Size = new Size(1180, 690);
            MinimumSize = new Size(930, 540);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            KeyPreview = true;
            AllowDrop = true;
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, _mehediConfig.ShihabDarkMode);

            BuildShihabLayoutControls();
            ApplyMehediTheme();
            PopulateShihabCategoryTree();
            RefreshMehediDownloadsGrid();

            _mehediBridgeListening = _mehediBridge.StartShihabBridge();
            _mehediBridge.OnShihabSingleCaptureReceived += HandleBrowserShihabSingleCapture;
            _mehediBridge.OnMehediBatchCaptureReceived += HandleBrowserMehediBatchCapture;
            _mehediBridge.OnShihabStartMainQueueRequested += delegate
            {
                SafeShihabUiInvoke(delegate { _shihabEngine.StartShihabQueue("Main Download Queue"); });
            };
            _mehediBridge.OnShihabShowMainWindowRequested += delegate
            {
                SafeShihabUiInvoke(delegate { RestoreAndActivateMainWindow(); });
            };
            _mehediBridge.OnShihabGracefulExitRequested += delegate
            {
                SafeShihabUiInvoke(delegate { ExitShihabApplicationCleanly(); });
            };

            _shihabEngine.OnShihabItemUpdated += delegate
            {
                SafeShihabUiInvoke(delegate { RefreshMehediDownloadsGrid(); });
            };
            _shihabEngine.OnMehediItemCompleted += HandleShihabItemCompleted;
            _shihabEngine.OnShihabItemFailed += HandleMehediItemFailed;
            _shihabEngine.OnQueueFinishedWithActions += HandleQueueFinishedWithActions;

            _shihabRemoteServer = new ShihabRemoteControlServer(_shihabStore, _mehediConfig, _shihabEngine);
            if (_mehediConfig.ShihabRemoteWebEnabled)
            {
                _shihabRemoteServer.Start();
            }

            ShihabDnsAndNetworkManager.OnNetworkRestored += delegate
            {
                if (_mehediConfig.ShihabAutoResumeOnNetworkRestore)
                {
                    SafeShihabUiInvoke(delegate
                    {
                        _shihabEngine.StartShihabQueue("Main Download Queue");
                    });
                }
            };

            if (_shihabStartupCli != null && _shihabStartupCli.ShihabStartMinimized)
            {
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
            }

            _mehediUiTickTimer = new Timer { Interval = 650 };
            _mehediUiTickTimer.Tick += delegate { OnMehediPeriodicTimerTick(); };
            _mehediUiTickTimer.Start();

            Resize += delegate
            {
                if (WindowState == FormWindowState.Minimized && _mehediConfig.ShihabMinimizeToTray)
                {
                    Hide();
                    ShowInTaskbar = false;
                    ShihabOsAndDiskSafety.TrimWorkingSetMemory();
                }
            };

            DragEnter += delegate(object s, DragEventArgs e)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop) ||
                    e.Data.GetDataPresent(DataFormats.UnicodeText) ||
                    e.Data.GetDataPresent(DataFormats.Text))
                {
                    e.Effect = DragDropEffects.Copy;
                }
            };
            DragDrop += delegate(object s, DragEventArgs e)
            {
                HandleDragDropDataObject(e.Data);
            };

            KeyDown += delegate(object shihabS, KeyEventArgs mehediE)
            {
                if ((mehediE.Control && mehediE.KeyCode == Keys.F) || mehediE.KeyCode == Keys.F3)
                {
                    _mehediSearchBox.Focus();
                    _mehediSearchBox.SelectAll();
                    mehediE.Handled = true;
                }
                else if (mehediE.Control && mehediE.KeyCode == Keys.N)
                {
                    PromptAndAddNewShihabUrl("");
                    mehediE.Handled = true;
                }
                else if (mehediE.Control && mehediE.KeyCode == Keys.U)
                {
                    TriggerShihabUpdateCheck(true);
                    mehediE.Handled = true;
                }
                else if (mehediE.Control && mehediE.KeyCode == Keys.A && !_mehediSearchBox.Focused)
                {
                    foreach (ListViewItem it in _shihabDownloadsListView.Items) it.Selected = true;
                    mehediE.Handled = true;
                }
                else if (mehediE.KeyCode == Keys.Delete && !_mehediSearchBox.Focused)
                {
                    DeleteSelectedShihabDownloads(mehediE.Shift);
                    mehediE.Handled = true;
                }
                else if (mehediE.Alt && mehediE.KeyCode == Keys.Enter && !_mehediSearchBox.Focused)
                {
                    OpenSelectedShihabProperties();
                    mehediE.Handled = true;
                }
                else if (mehediE.KeyCode == Keys.Enter && !_mehediSearchBox.Focused)
                {
                    HandleDoubleClickOnShihabRow();
                    mehediE.Handled = true;
                }
                else if (mehediE.KeyCode == Keys.F2 && !_mehediSearchBox.Focused)
                {
                    OpenSelectedShihabProperties();
                    mehediE.Handled = true;
                }
            };

            Shown += delegate
            {
                ResizeShihabFileNameColumn();
                RepositionShihabHeaderControls();
                OnShihabMainFormFirstShown();
            };
            FormClosing += HandleMehediFormClosing;
        }

        private void BuildShihabLayoutControls()
        {
            _shihabMenuBar = new MenuStrip { Visible = false };
            _mehediOverflowAppMenu = BuildMehediFullAppOverflowMenu();

            // Left Sidebar (224px wide, #121418, right 1px border #232730)
            _shihabSidebarPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 224,
                Padding = new Padding(0, 0, 1, 0),
                Visible = _mehediConfig.MehediShowCategoriesPanel
            };
            _shihabSidebarPanel.Paint += delegate(object shihabSender, PaintEventArgs mehediE)
            {
                Color shihabDiv = _mehediConfig.ShihabDarkMode ? Color.FromArgb(35, 39, 48) : Color.FromArgb(206, 214, 224);
                using (Pen mehediDivPen = new Pen(shihabDiv, 1f))
                {
                    mehediE.Graphics.DrawLine(mehediDivPen, _shihabSidebarPanel.Width - 1, 0, _shihabSidebarPanel.Width - 1, _shihabSidebarPanel.Height);
                }
            };

            _mehediSidebarTopHeading = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36
            };
            _mehediSidebarTopHeading.Paint += delegate(object shihabSender, PaintEventArgs mehediE)
            {
                mehediE.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                Color shihabHdrCol = _mehediConfig.ShihabDarkMode ? Color.FromArgb(104, 112, 131) : Color.FromArgb(100, 116, 139);
                using (Font mehediHdrFont = new Font("Segoe UI", 7.8f, FontStyle.Bold))
                {
                    TextRenderer.DrawText(
                        mehediE.Graphics,
                        "TRANSFERS",
                        mehediHdrFont,
                        new Rectangle(16, 11, _mehediSidebarTopHeading.Width - 24, 20),
                        shihabHdrCol,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                }
            };

            _mehediStorageCard = new ShihabLocalStorageCardControl
            {
                Dock = DockStyle.Bottom,
                Height = 92
            };
            _mehediStorageCard.OnMehediSpeedToggleClicked += delegate
            {
                _mehediConfig.MehediGlobalSpeedLimiterEnabled = !_mehediConfig.MehediGlobalSpeedLimiterEnabled;
                _shihabStore.SaveMehediConfig(_mehediConfig);
                UpdateShihabStatusBadges();
            };

            _shihabTransfersNav = new ShihabTransfersNavControl
            {
                Dock = DockStyle.Fill,
                MehediActiveTag = "ALL"
            };
            _shihabTransfersNav.OnShihabFilterSelected += delegate
            {
                _shihabActiveTreeFilter = _shihabTransfersNav.MehediActiveTag;
                _shihabHeaderViewTitle = _shihabTransfersNav.GetActiveDisplayTitle();
                RefreshMehediDownloadsGrid();
            };

            _shihabSidebarPanel.Controls.Add(_shihabTransfersNav);
            _shihabSidebarPanel.Controls.Add(_mehediStorageCard);
            _shihabSidebarPanel.Controls.Add(_mehediSidebarTopHeading);

            // Right Workspace (#16181d)
            _shihabRightWorkspacePanel = new Panel
            {
                Dock = DockStyle.Fill
            };

            // Top Action Header Bar (50px tall)
            _mehediTopActionHeaderBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50
            };
            _mehediTopActionHeaderBar.Paint += delegate(object shihabSender, PaintEventArgs mehediE)
            {
                Graphics shihabG = mehediE.Graphics;
                shihabG.SmoothingMode = SmoothingMode.AntiAlias;
                shihabG.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                Color mehediBotLine = _mehediConfig.ShihabDarkMode ? Color.FromArgb(35, 39, 48) : Color.FromArgb(206, 214, 224);
                using (Pen shihabBotPen = new Pen(mehediBotLine, 1f))
                {
                    shihabG.DrawLine(shihabBotPen, 0, _mehediTopActionHeaderBar.Height - 1, _mehediTopActionHeaderBar.Width, _mehediTopActionHeaderBar.Height - 1);
                }

                Color shihabTitleCol = _mehediConfig.ShihabDarkMode ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42);
                using (Font mehediTitleFont = new Font("Segoe UI", 11.5f, FontStyle.Bold))
                using (Font shihabBadgeFont = new Font("Segoe UI Semibold", 8.2f, FontStyle.Regular))
                {
                    Size mehediTitleSz = TextRenderer.MeasureText(shihabG, _shihabHeaderViewTitle, mehediTitleFont, new Size(340, 32), TextFormatFlags.NoPadding);
                    Rectangle shihabTitleRect = new Rectangle(14, 0, mehediTitleSz.Width + 4, _mehediTopActionHeaderBar.Height - 1);
                    TextRenderer.DrawText(
                        shihabG,
                        _shihabHeaderViewTitle,
                        mehediTitleFont,
                        shihabTitleRect,
                        shihabTitleCol,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);

                    Size shihabBadgeSz = TextRenderer.MeasureText(shihabG, _mehediHeaderItemsBadgeText, shihabBadgeFont, new Size(160, 24), TextFormatFlags.NoPadding);
                    int mehediBadgeW = shihabBadgeSz.Width + 16;
                    Rectangle shihabBadgeRect = new Rectangle(shihabTitleRect.Right + 8, (_mehediTopActionHeaderBar.Height - 24) / 2, mehediBadgeW, 24);
                    Color mehediBadgeFill = _mehediConfig.ShihabDarkMode ? Color.FromArgb(34, 38, 48) : Color.FromArgb(226, 232, 240);
                    Color shihabBadgeEdge = _mehediConfig.ShihabDarkMode ? Color.FromArgb(46, 52, 66) : Color.FromArgb(203, 213, 225);
                    Color mehediBadgeFg = _mehediConfig.ShihabDarkMode ? Color.FromArgb(154, 163, 181) : Color.FromArgb(71, 85, 105);

                    using (GraphicsPath mehediBadgePath = ShihabThemePalette.CreateShihabRoundedRect(shihabBadgeRect, 6))
                    using (SolidBrush shihabBadgeBr = new SolidBrush(mehediBadgeFill))
                    using (Pen mehediBadgePen = new Pen(shihabBadgeEdge, 1f))
                    {
                        shihabG.FillPath(shihabBadgeBr, mehediBadgePath);
                        shihabG.DrawPath(mehediBadgePen, mehediBadgePath);
                    }
                    TextRenderer.DrawText(
                        shihabG,
                        _mehediHeaderItemsBadgeText,
                        shihabBadgeFont,
                        shihabBadgeRect,
                        mehediBadgeFg,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            };

            _shihabAddUrlPillBtn = new ShihabHeaderPillButton("+ Add URL", 82, true);
            _shihabAddUrlPillBtn.Click += delegate { PromptAndAddNewShihabUrl(""); };

            _shihabTorrentPillBtn = new ShihabHeaderPillButton("Torrent", 64, false);
            _shihabTorrentPillBtn.Click += delegate { OpenShihabTorrentDialog(""); };

            _shihabMediaPillBtn = new ShihabHeaderPillButton("Media / YT", 78, false);
            _shihabMediaPillBtn.Click += delegate { OpenShihabMediaAndGalleryDialog(); };

            _mehediStartPillBtn = new ShihabHeaderPillButton("Start", 56, false);
            _mehediStartPillBtn.Click += delegate
            {
                if (_shihabDownloadsListView.SelectedItems.Count > 0)
                {
                    ResumeSelectedShihabDownloads();
                }
                else
                {
                    _shihabEngine.StartShihabQueue("Main Download Queue");
                }
            };

            _shihabPausePillBtn = new ShihabHeaderPillButton("Pause", 58, false);
            _shihabPausePillBtn.Click += delegate
            {
                if (_shihabDownloadsListView.SelectedItems.Count > 0)
                {
                    PauseSelectedMehediDownloads();
                }
                else
                {
                    _shihabEngine.StopAllMehediDownloads();
                }
            };

            _mehediFolderPillBtn = new ShihabHeaderPillButton("Folder", 62, false);
            _mehediFolderPillBtn.Click += delegate
            {
                if (_shihabDownloadsListView.SelectedItems.Count > 0)
                {
                    OpenSelectedMehediContainingFolder();
                }
                else
                {
                    OpenDefaultShihabDownloadsDirectory();
                }
            };

            _shihabRemotePillBtn = new ShihabHeaderPillButton("Remote QR", 80, false);
            _shihabRemotePillBtn.Click += delegate { OpenShihabRemotePairingDialog(); };

            _shihabUpdatePillBtn = new ShihabHeaderPillButton("Update!", 96, true);
            _shihabUpdatePillBtn.Visible = false;
            _shihabUpdatePillBtn.Click += delegate
            {
                if (_shihabLatestDetectedUpdate != null && _shihabLatestDetectedUpdate.UpdateAvailable)
                {
                    OpenShihabAutoUpdateDialog(_shihabLatestDetectedUpdate);
                }
                else
                {
                    TriggerShihabUpdateCheck(true);
                }
            };

            _shihabAboutPillBtn = new ShihabHeaderPillButton("About", 58, false);
            _shihabAboutPillBtn.Click += delegate { ShowShihabAboutDialog(); };

            _mehediMenuPillBtn = new ShihabHeaderPillButton("\u2630", 34, false);
            _mehediMenuPillBtn.Click += delegate
            {
                _mehediOverflowAppMenu.Show(_mehediMenuPillBtn, new Point(0, _mehediMenuPillBtn.Height + 2));
            };

            _shihabSearchBarPanel = new ShihabRoundedCardPanel
            {
                Size = new Size(224, 30),
                MehediCornerRadius = 6,
                Padding = new Padding(10, 5, 24, 4)
            };

            _mehediSearchBox = new TextBox
            {
                Location = new Point(10, 6),
                Width = 186,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 8.8f)
            };
            Action shihabApplySearchCue = delegate
            {
                try
                {
                    if (_mehediSearchBox.IsHandleCreated)
                    {
                        SendMehediCueBannerMessage(_mehediSearchBox.Handle, 0x1501, (IntPtr)1, "Filter transfers...");
                    }
                }
                catch { }
            };
            _mehediSearchBox.HandleCreated += delegate { shihabApplySearchCue(); };

            _shihabClearSearchBtn = new Button
            {
                Text = "\u00D7",
                Location = new Point(198, 4),
                Size = new Size(20, 20),
                FlatStyle = FlatStyle.Flat,
                Visible = false,
                Cursor = Cursors.Hand
            };
            _shihabClearSearchBtn.FlatAppearance.BorderSize = 0;
            _shihabClearSearchBtn.Click += delegate
            {
                _mehediSearchBox.Text = "";
                _mehediSearchBox.Focus();
            };
            _mehediSearchBox.TextChanged += delegate
            {
                _shihabClearSearchBtn.Visible = !string.IsNullOrEmpty(_mehediSearchBox.Text);
                RefreshMehediDownloadsGrid();
            };
            _shihabSearchBarPanel.Controls.Add(_mehediSearchBox);
            _shihabSearchBarPanel.Controls.Add(_shihabClearSearchBtn);

            _mehediTopActionHeaderBar.Controls.AddRange(new Control[]
            {
                _shihabAddUrlPillBtn,
                _shihabTorrentPillBtn,
                _shihabMediaPillBtn,
                _mehediStartPillBtn,
                _shihabPausePillBtn,
                _mehediFolderPillBtn,
                _shihabRemotePillBtn,
                _shihabUpdatePillBtn,
                _shihabAboutPillBtn,
                _mehediMenuPillBtn,
                _shihabSearchBarPanel
            });
            _mehediTopActionHeaderBar.Resize += delegate { RepositionShihabHeaderControls(); };

            // Main Transfers Grid (8 columns matching Screenshot 2)
            _shihabDownloadsListView = new ShihabModernListView
            {
                Dock = DockStyle.Fill,
                MultiSelect = true,
                AllowDrop = true,
                ShihabTransferGridMode = true,
                MehediSortedColumnIndex = -1,
                ShihabSortAscending = false
            };
            _shihabDownloadsListView.Columns.Add("#", 44);
            _shihabDownloadsListView.Columns.Add("FILE NAME", 350);
            _shihabDownloadsListView.Columns.Add("SIZE", 84);
            _shihabDownloadsListView.Columns.Add("PROGRESS", 92);
            _shihabDownloadsListView.Columns.Add("SPEED", 86);
            _shihabDownloadsListView.Columns.Add("ETA", 68);
            _shihabDownloadsListView.Columns.Add("STATUS", 96);
            _shihabDownloadsListView.Columns.Add("DATE ADDED", 96);

            _shihabDownloadsListView.Resize += delegate { ResizeShihabFileNameColumn(); };
            _shihabDownloadsListView.ColumnClick += delegate(object shihabS, ColumnClickEventArgs mehediE)
            {
                string shihabNextKey = "ORDER";
                if (mehediE.Column == 1) shihabNextKey = "NAME";
                else if (mehediE.Column == 2) shihabNextKey = "SIZE";
                else if (mehediE.Column == 3) shihabNextKey = "PROGRESS";
                else if (mehediE.Column == 4) shihabNextKey = "SPEED";
                else if (mehediE.Column == 6) shihabNextKey = "STATUS";
                else shihabNextKey = "ORDER";

                if (_mehediSortColumnKey == shihabNextKey)
                {
                    _shihabSortAscending = !_shihabSortAscending;
                }
                else
                {
                    _mehediSortColumnKey = shihabNextKey;
                    _shihabSortAscending = (shihabNextKey == "NAME" || shihabNextKey == "STATUS");
                }
                _shihabDownloadsListView.MehediSortedColumnIndex = mehediE.Column;
                _shihabDownloadsListView.ShihabSortAscending = _shihabSortAscending;
                RefreshMehediDownloadsGrid();
            };

            _shihabDownloadsListView.DoubleClick += delegate { HandleDoubleClickOnShihabRow(); };
            _shihabDownloadsListView.DragEnter += delegate(object shihabS, DragEventArgs mehediE)
            {
                if (mehediE.Data.GetDataPresent(DataFormats.FileDrop) ||
                    mehediE.Data.GetDataPresent(DataFormats.UnicodeText) ||
                    mehediE.Data.GetDataPresent(DataFormats.Text))
                {
                    mehediE.Effect = DragDropEffects.Copy;
                }
            };
            _shihabDownloadsListView.DragDrop += delegate(object shihabS, DragEventArgs mehediE)
            {
                HandleDragDropDataObject(mehediE.Data);
            };
            _shihabDownloadsListView.ContextMenuStrip = BuildMehediRowContextMenu();

            // Bottom Footer Bar (38px tall with 4 status pills)
            _mehediFooterBar = new ShihabTransferFooterBarControl
            {
                Dock = DockStyle.Bottom,
                Height = 38
            };

            _shihabRightWorkspacePanel.Controls.Add(_shihabDownloadsListView);
            _shihabRightWorkspacePanel.Controls.Add(_mehediFooterBar);
            _shihabRightWorkspacePanel.Controls.Add(_mehediTopActionHeaderBar);

            // System Tray Icon
            _shihabTrayMenuRef = new ContextMenuStrip();
            _shihabTrayMenuRef.Items.Add("Restore MHS Download Manager", null, delegate { RestoreAndActivateMainWindow(); });
            _shihabTrayMenuRef.Items.Add("Add new download...", null, delegate { RestoreAndActivateMainWindow(); PromptAndAddNewShihabUrl(""); });
            _shihabTrayMenuRef.Items.Add("Show drop target", null, delegate { ToggleMehediFloatingDropTarget(); });
            _shihabTrayMenuRef.Items.Add("Start Main Queue", null, delegate { _shihabEngine.StartShihabQueue("Main Download Queue"); });
            _shihabTrayMenuRef.Items.Add("Stop All Downloads", null, delegate { _shihabEngine.StopAllMehediDownloads(); });
            _shihabTrayMenuRef.Items.Add(new ToolStripSeparator());
            _shihabTrayMenuRef.Items.Add("Check for Updates... (Ctrl+U)", null, delegate { RestoreAndActivateMainWindow(); TriggerShihabUpdateCheck(true); });
            _shihabTrayMenuRef.Items.Add("About MHS Download Manager...", null, delegate { RestoreAndActivateMainWindow(); ShowShihabAboutDialog(); });
            _shihabTrayMenuRef.Items.Add("Exit", null, delegate { ExitShihabApplicationCleanly(); });

            _mehediTrayIcon = new NotifyIcon
            {
                Icon = Icon,
                Text = ShihabThemePalette.ShihabMainHeaderTitle,
                Visible = true,
                ContextMenuStrip = _shihabTrayMenuRef
            };
            _mehediTrayIcon.DoubleClick += delegate { RestoreAndActivateMainWindow(); };
            _mehediTrayIcon.BalloonTipClicked += delegate
            {
                if (_shihabLatestDetectedUpdate != null && _shihabLatestDetectedUpdate.UpdateAvailable)
                {
                    RestoreAndActivateMainWindow();
                    OpenShihabAutoUpdateDialog(_shihabLatestDetectedUpdate);
                }
            };

            Controls.Add(_shihabRightWorkspacePanel);
            Controls.Add(_shihabSidebarPanel);
            Controls.Add(_shihabMenuBar);
        }

        private ContextMenuStrip BuildMehediFullAppOverflowMenu()
        {
            ContextMenuStrip mehediMenu = new ContextMenuStrip();

            mehediMenu.Items.Add("Add New Download (Ctrl+N)...", null, delegate { PromptAndAddNewShihabUrl(""); });
            mehediMenu.Items.Add("Add BitTorrent / Magnet (.torrent)...", null, delegate { OpenShihabTorrentDialog(""); });
            mehediMenu.Items.Add("Video / Playlist, Subtitles & Gallery Downloader...", null, delegate { OpenShihabMediaAndGalleryDialog(); });
            mehediMenu.Items.Add("RSS / Atom Feed Reader & Auto-Downloader...", null, delegate { OpenShihabRssFeedDialog(); });
            mehediMenu.Items.Add("Add Batch Download (Wildcard *)...", null, delegate { OpenShihabBatchWildcardDialog(); });
            mehediMenu.Items.Add("Add Batch from Clipboard", null, delegate { ImportShihabClipboardUrlsBatch(); });
            mehediMenu.Items.Add("Run Site Grabber...", null, delegate { OpenShihabSiteGrabberDialog(); });
            mehediMenu.Items.Add(new ToolStripSeparator());

            mehediMenu.Items.Add("LAN Remote Web UI & Mobile Pairing (QR)...", null, delegate { OpenShihabRemotePairingDialog(); });
            mehediMenu.Items.Add("Checksum Verifier & Safe Archive Extractor...", null, delegate { OpenShihabChecksumAndExtractDialog(); });
            mehediMenu.Items.Add(new ToolStripSeparator());

            mehediMenu.Items.Add("Start Main Queue", null, delegate { _shihabEngine.StartShihabQueue("Main Download Queue"); });
            mehediMenu.Items.Add("Stop Main Queue", null, delegate { _shihabEngine.StopShihabQueue("Main Download Queue"); });
            mehediMenu.Items.Add("Scheduler & Queues...", null, delegate { OpenMehediSchedulerDialog(); });
            mehediMenu.Items.Add("Delete All Completed", null, delegate { DeleteAllCompletedMehediDownloads(); });
            mehediMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem shihabExpImp = new ToolStripMenuItem("Import / Export");
            shihabExpImp.DropDownItems.Add("Export to IDM (.ef2)...", null, delegate { ExportShihabList(true); });
            shihabExpImp.DropDownItems.Add("Export to Text (.txt)...", null, delegate { ExportShihabList(false); });
            shihabExpImp.DropDownItems.Add(new ToolStripSeparator());
            shihabExpImp.DropDownItems.Add("Import (.ef2 / .txt)...", null, delegate { ImportMehediList(); });
            mehediMenu.Items.Add(shihabExpImp);

            ToolStripMenuItem mehediViewSub = new ToolStripMenuItem("View & Theme");
            mehediViewSub.DropDownItems.Add("Toggle Transfers Sidebar", null, delegate
            {
                _mehediConfig.MehediShowCategoriesPanel = !_mehediConfig.MehediShowCategoriesPanel;
                _shihabSidebarPanel.Visible = _mehediConfig.MehediShowCategoriesPanel;
                _shihabStore.SaveMehediConfig(_mehediConfig);
                ResizeShihabFileNameColumn();
                RepositionShihabHeaderControls();
            });
            mehediViewSub.DropDownItems.Add("Toggle Dark / Light Theme", null, delegate
            {
                _mehediConfig.ShihabDarkMode = !_mehediConfig.ShihabDarkMode;
                _mehediConfig.ShihabThemeName = _mehediConfig.ShihabDarkMode ? "Obsidian Dark" : "Classic Light";
                _shihabStore.SaveMehediConfig(_mehediConfig);
                ApplyMehediTheme();
            });
            mehediViewSub.DropDownItems.Add("Show Floating Drop Target", null, delegate { ToggleMehediFloatingDropTarget(); });
            mehediMenu.Items.Add(mehediViewSub);

            mehediMenu.Items.Add("Options & Settings...", null, delegate { OpenShihabOptionsDialog(); });
            mehediMenu.Items.Add("Check for Updates (GitHub Releases)... (Ctrl+U)", null, delegate { TriggerShihabUpdateCheck(true); });
            mehediMenu.Items.Add(new ToolStripSeparator());

            mehediMenu.Items.Add("Open Browser Extension Folder...", null, delegate { OpenShihabInstalledExtensionFolder(); });
            mehediMenu.Items.Add("Browser Extension Setup Guide", null, delegate { ShowShihabExtensionInstructions(); });
            mehediMenu.Items.Add("CLI Switches Help", null, delegate { ShowMehediCliHelp(); });
            mehediMenu.Items.Add("About Mehedi Hasan Shihab...", null, delegate { ShowShihabAboutDialog(); });
            mehediMenu.Items.Add(new ToolStripSeparator());
            mehediMenu.Items.Add("Exit Application", null, delegate { ExitShihabApplicationCleanly(); });

            return mehediMenu;
        }

        private void RepositionShihabHeaderControls()
        {
            if (_mehediTopActionHeaderBar == null) return;
            int shihabBarW = _mehediTopActionHeaderBar.ClientSize.Width;
            int mehediBtnY = (_mehediTopActionHeaderBar.Height - 28) / 2;

            _shihabSearchBarPanel.Location = new Point(Math.Max(340, shihabBarW - _shihabSearchBarPanel.Width - 12), (_mehediTopActionHeaderBar.Height - _shihabSearchBarPanel.Height) / 2);

            int mehediTitleEnd = 210;
            using (Graphics shihabMeasureG = _mehediTopActionHeaderBar.CreateGraphics())
            using (Font mehediTitleFont = new Font("Segoe UI", 11.5f, FontStyle.Bold))
            using (Font shihabBadgeFont = new Font("Segoe UI Semibold", 8.2f, FontStyle.Regular))
            {
                Size mehediT = TextRenderer.MeasureText(shihabMeasureG, _shihabHeaderViewTitle, mehediTitleFont, new Size(340, 32), TextFormatFlags.NoPadding);
                Size shihabB = TextRenderer.MeasureText(shihabMeasureG, _mehediHeaderItemsBadgeText, shihabBadgeFont, new Size(160, 24), TextFormatFlags.NoPadding);
                mehediTitleEnd = 14 + mehediT.Width + 4 + 8 + shihabB.Width + 16 + 14;
            }

            bool hasUpdateBanner = _shihabLatestDetectedUpdate != null && _shihabLatestDetectedUpdate.UpdateAvailable;
            ShihabHeaderPillButton[] shihabBtns = new ShihabHeaderPillButton[]
            {
                _shihabAddUrlPillBtn,
                _shihabTorrentPillBtn,
                _shihabMediaPillBtn,
                _mehediStartPillBtn,
                _shihabPausePillBtn,
                _mehediFolderPillBtn,
                _shihabRemotePillBtn,
                _shihabUpdatePillBtn,
                _shihabAboutPillBtn,
                _mehediMenuPillBtn
            };

            int mehediCurX = mehediTitleEnd;
            for (int shihabI = 0; shihabI < shihabBtns.Length; shihabI++)
            {
                ShihabHeaderPillButton mehediBtn = shihabBtns[shihabI];
                if (mehediBtn == null) continue;
                if (mehediBtn == _shihabUpdatePillBtn && !hasUpdateBanner)
                {
                    mehediBtn.Visible = false;
                    continue;
                }
                bool shihabFits = (mehediCurX + mehediBtn.Width <= _shihabSearchBarPanel.Left - 8) || mehediBtn == _mehediMenuPillBtn;
                mehediBtn.Visible = shihabFits;
                if (shihabFits)
                {
                    mehediBtn.Location = new Point(mehediCurX, mehediBtnY);
                    mehediCurX += mehediBtn.Width + 6;
                }
            }
            _mehediTopActionHeaderBar.Invalidate();
        }

        private void ResizeShihabFileNameColumn()
        {
            if (_shihabDownloadsListView == null || _shihabDownloadsListView.Columns.Count < 8) return;
            int mehediAvail = _shihabDownloadsListView.ClientSize.Width;
            if (mehediAvail <= 200) return;

            int shihabFixedSum = 0;
            for (int mehediC = 0; mehediC < _shihabDownloadsListView.Columns.Count; mehediC++)
            {
                if (mehediC != 1)
                {
                    shihabFixedSum += _shihabDownloadsListView.Columns[mehediC].Width;
                }
            }
            int mehediTargetNameW = Math.Max(210, mehediAvail - shihabFixedSum);
            if (Math.Abs(_shihabDownloadsListView.Columns[1].Width - mehediTargetNameW) > 1)
            {
                _shihabDownloadsListView.Columns[1].Width = mehediTargetNameW;
            }
        }

        private void OpenDefaultShihabDownloadsDirectory()
        {
            try
            {
                string shihabDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (_shihabActiveTreeFilter.StartsWith("CAT:"))
                {
                    string mehediCatName = _shihabActiveTreeFilter.Substring(4);
                    for (int shihabI = 0; shihabI < _mehediConfig.ShihabCategories.Count; shihabI++)
                    {
                        if (string.Equals(_mehediConfig.ShihabCategories[shihabI].ShihabName, mehediCatName, StringComparison.OrdinalIgnoreCase))
                        {
                            shihabDir = _mehediConfig.ShihabCategories[shihabI].ShihabDefaultFolder;
                            break;
                        }
                    }
                }
                if (Directory.Exists(shihabDir))
                {
                    Process.Start("explorer.exe", "\"" + shihabDir + "\"");
                }
            }
            catch { }
        }

        private void UpdateMehediToolbarActionIcons()
        {
        }

        private ContextMenuStrip BuildMehediRowContextMenu()
        {
            ContextMenuStrip shihabCtx = new ContextMenuStrip();
            shihabCtx.Items.Add("Resume Download", null, delegate { ResumeSelectedShihabDownloads(); });
            shihabCtx.Items.Add("Stop Download", null, delegate { PauseSelectedMehediDownloads(); });
            shihabCtx.Items.Add("Show Live Progress Window", null, delegate { OpenSelectedMehediProgressWindow(); });
            shihabCtx.Items.Add(new ToolStripSeparator());
            shihabCtx.Items.Add("Open", null, delegate { OpenSelectedShihabCompletedFile(false); });
            shihabCtx.Items.Add("Open With...", null, delegate { OpenSelectedShihabCompletedFile(true); });
            shihabCtx.Items.Add("Open Folder", null, delegate { OpenSelectedMehediContainingFolder(); });
            shihabCtx.Items.Add(new ToolStripSeparator());
            shihabCtx.Items.Add("Refresh Download Address...", null, delegate { RefreshSelectedMehediAddress(); });
            shihabCtx.Items.Add("Redownload from Beginning", null, delegate { RedownloadSelectedShihabItem(); });

            ToolStripMenuItem mehediMoveQueue = new ToolStripMenuItem("Move to Queue");
            shihabCtx.Opening += delegate
            {
                mehediMoveQueue.DropDownItems.Clear();
                mehediMoveQueue.DropDownItems.Add("(None — Direct)", null, delegate { AssignSelectedShihabToQueue(""); });
                for (int shihabQ = 0; shihabQ < _mehediConfig.MehediQueues.Count; shihabQ++)
                {
                    string mehediQName = _mehediConfig.MehediQueues[shihabQ].ShihabQueueName;
                    mehediMoveQueue.DropDownItems.Add(mehediQName, null, delegate { AssignSelectedShihabToQueue(mehediQName); });
                }
            };
            shihabCtx.Items.Add(mehediMoveQueue);

            shihabCtx.Items.Add(new ToolStripSeparator());
            shihabCtx.Items.Add("Remove from List", null, delegate { DeleteSelectedShihabDownloads(false); });
            shihabCtx.Items.Add("Remove + Delete File from Disk", null, delegate { DeleteSelectedShihabDownloads(true); });
            shihabCtx.Items.Add(new ToolStripSeparator());
            shihabCtx.Items.Add("Properties...", null, delegate { OpenSelectedShihabProperties(); });
            return shihabCtx;
        }

        private void ApplyMehediTheme()
        {
            string themeName = !string.IsNullOrWhiteSpace(_mehediConfig.ShihabThemeName)
                ? _mehediConfig.ShihabThemeName
                : (_mehediConfig.ShihabDarkMode ? "Obsidian Dark" : "Classic Light");
            ShihabCustomThemePreset preset = ShihabThemeManager.ResolveThemePalette(themeName, _shihabStore.MehediBaseDirectory);
            ShihabThemePalette.ApplyThemePreset(preset);
            bool shihabDark = ShihabThemeManager.IsDarkThemePreset(preset);
            _mehediConfig.ShihabDarkMode = shihabDark;

            Color mehediWorkspaceBg = ShihabThemePalette.GetMehediWindowBg(shihabDark);
            Color shihabSideBg = ShihabThemePalette.GetShihabSidebarBg(shihabDark);
            Color shihabSurfBg = ShihabThemePalette.GetShihabSurfaceBg(shihabDark);
            Color shihabFg = ShihabThemePalette.GetShihabPrimaryText(shihabDark);
            Color mehediMuted = ShihabThemePalette.GetMehediMutedText(shihabDark);

            bool isRtl = ShihabThemeManager.IsRightToLeftLanguage(_mehediConfig.ShihabLanguageCode);
            RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;

            int uiScale = _mehediConfig.ShihabUiScalePercent > 0 ? Math.Max(80, Math.Min(150, _mehediConfig.ShihabUiScalePercent)) : 100;
            float scaledFontPt = 9.0f * (uiScale / 100.0f);
            if (Math.Abs(Font.Size - scaledFontPt) > 0.15f)
            {
                Font = new Font("Segoe UI", scaledFontPt);
            }

            if (_shihabAddUrlPillBtn != null)
            {
                _shihabAddUrlPillBtn.Text = "+ " + ShihabThemeManager.TranslateUiString(_mehediConfig.ShihabLanguageCode, "btn_add_url", "Add URL");
            }
            if (_mehediStartPillBtn != null)
            {
                _mehediStartPillBtn.Text = "\u25B6 " + ShihabThemeManager.TranslateUiString(_mehediConfig.ShihabLanguageCode, "btn_resume", "Start");
            }
            if (_shihabPausePillBtn != null)
            {
                _shihabPausePillBtn.Text = "\u275A\u275A " + ShihabThemeManager.TranslateUiString(_mehediConfig.ShihabLanguageCode, "btn_stop", "Pause");
            }

            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, shihabDark);
            BackColor = mehediWorkspaceBg;
            ForeColor = shihabFg;

            MehediDarkToolStripRenderer shihabMenuRenderer = new MehediDarkToolStripRenderer(shihabDark, false);
            _shihabMenuBar.Renderer = shihabMenuRenderer;
            if (_mehediOverflowAppMenu != null)
            {
                _mehediOverflowAppMenu.Renderer = shihabMenuRenderer;
            }
            if (_shihabDownloadsListView.ContextMenuStrip != null)
            {
                _shihabDownloadsListView.ContextMenuStrip.Renderer = shihabMenuRenderer;
            }
            if (_shihabTrayMenuRef != null)
            {
                _shihabTrayMenuRef.Renderer = shihabMenuRenderer;
            }

            _shihabSidebarPanel.BackColor = shihabSideBg;
            _mehediSidebarTopHeading.BackColor = shihabSideBg;
            _shihabTransfersNav.ShihabDarkMode = shihabDark;
            _shihabTransfersNav.BackColor = shihabSideBg;
            _mehediStorageCard.ShihabDarkMode = shihabDark;
            _mehediStorageCard.BackColor = shihabSideBg;
            _shihabSidebarPanel.Invalidate(true);

            _shihabRightWorkspacePanel.BackColor = mehediWorkspaceBg;
            _mehediTopActionHeaderBar.BackColor = mehediWorkspaceBg;

            ShihabHeaderPillButton[] mehediPills = new ShihabHeaderPillButton[]
            {
                _shihabAddUrlPillBtn,
                _shihabTorrentPillBtn,
                _shihabMediaPillBtn,
                _mehediStartPillBtn,
                _shihabPausePillBtn,
                _mehediFolderPillBtn,
                _shihabRemotePillBtn,
                _shihabUpdatePillBtn,
                _shihabAboutPillBtn,
                _mehediMenuPillBtn
            };
            for (int shihabI = 0; shihabI < mehediPills.Length; shihabI++)
            {
                if (mehediPills[shihabI] != null)
                {
                    mehediPills[shihabI].ShihabDarkMode = shihabDark;
                    mehediPills[shihabI].Invalidate();
                }
            }

            _shihabSearchBarPanel.BackColor = mehediWorkspaceBg;
            _shihabSearchBarPanel.MehediFillColor = shihabDark ? shihabSurfBg : Color.White;
            _shihabSearchBarPanel.ShihabBorderColor = ShihabThemePalette.GetShihabBorder(shihabDark);
            _mehediSearchBox.BackColor = _shihabSearchBarPanel.MehediFillColor;
            _mehediSearchBox.ForeColor = shihabFg;
            _shihabClearSearchBtn.BackColor = _shihabSearchBarPanel.MehediFillColor;
            _shihabClearSearchBtn.ForeColor = mehediMuted;
            _shihabSearchBarPanel.Invalidate();

            _shihabDownloadsListView.MehediDarkMode = shihabDark;
            _shihabDownloadsListView.BackColor = shihabDark ? mehediWorkspaceBg : Color.White;
            _shihabDownloadsListView.ForeColor = shihabFg;
            _shihabDownloadsListView.Invalidate();

            _mehediFooterBar.ShihabDarkMode = shihabDark;
            _mehediFooterBar.BackColor = shihabSideBg;
            _mehediFooterBar.Invalidate();

            ShihabThemePalette.ApplyShihabDarkThemeToControlTree(this, shihabDark);
            _mehediSearchBox.BackColor = _shihabSearchBarPanel.MehediFillColor;
            RepositionShihabHeaderControls();
            _mehediTopActionHeaderBar.Invalidate();
            UpdateShihabStatusBadges();
        }

        private void PopulateShihabCategoryTree()
        {
            if (_shihabTransfersNav == null) return;
            _shihabTransfersNav.MehediActiveTag = _shihabActiveTreeFilter;
            _shihabHeaderViewTitle = _shihabTransfersNav.GetActiveDisplayTitle();
            _shihabTransfersNav.Invalidate();
            RepositionShihabHeaderControls();
        }

        private static bool CategoryMatchesFilterName(string itemCategory, string filterCategory, bool isTorrentItem)
        {
            if (string.Equals(filterCategory, "Torrents", StringComparison.OrdinalIgnoreCase))
            {
                return isTorrentItem || string.Equals(itemCategory, "Torrents", StringComparison.OrdinalIgnoreCase);
            }
            if (string.Equals(filterCategory, "Audio", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filterCategory, "Music", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(itemCategory, "Audio", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(itemCategory, "Music", StringComparison.OrdinalIgnoreCase);
            }
            return string.Equals(itemCategory, filterCategory, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesShihabTreeAndSearch(ShihabDownloadItem mehediItem)
        {
            ShihabDownloadStatus shihabSt = mehediItem.GetShihabStatusEnum();
            bool mehediIsDone = shihabSt == ShihabDownloadStatus.ShihabCompleted;
            bool shihabIsActive = _shihabEngine.IsMehediItemActive(mehediItem.ShihabId) ||
                                  shihabSt == ShihabDownloadStatus.ShihabDownloading ||
                                  shihabSt == ShihabDownloadStatus.ShihabConnecting ||
                                  shihabSt == ShihabDownloadStatus.ShihabRebuilding;

            if (_shihabActiveTreeFilter == "DOWNLOADING" && !shihabIsActive) return false;
            if (_shihabActiveTreeFilter == "PAUSED" && (mehediIsDone || shihabIsActive || shihabSt != ShihabDownloadStatus.ShihabPaused)) return false;
            if (_shihabActiveTreeFilter == "UNFINISHED" && mehediIsDone) return false;
            if (_shihabActiveTreeFilter == "FINISHED" && !mehediIsDone) return false;
            if (_shihabActiveTreeFilter == "GRABBER_PROJECTS" && (mehediItem.ShihabDescription ?? "").IndexOf("Grabber", StringComparison.OrdinalIgnoreCase) < 0) return false;
            if (_shihabActiveTreeFilter == "QUEUES_ALL" && (mehediIsDone || (string.IsNullOrEmpty(mehediItem.MehediQueueName) && shihabSt != ShihabDownloadStatus.ShihabQueued))) return false;

            if (_shihabActiveTreeFilter.StartsWith("CAT:"))
            {
                string shihabC = _shihabActiveTreeFilter.Substring(4);
                if (!CategoryMatchesFilterName(mehediItem.ShihabCategoryName, shihabC, mehediItem.ShihabIsTorrent)) return false;
            }
            else if (_shihabActiveTreeFilter.StartsWith("UNFIN_CAT:"))
            {
                string mehediC = _shihabActiveTreeFilter.Substring(10);
                if (mehediIsDone || !CategoryMatchesFilterName(mehediItem.ShihabCategoryName, mehediC, mehediItem.ShihabIsTorrent)) return false;
            }
            else if (_shihabActiveTreeFilter.StartsWith("FIN_CAT:"))
            {
                string shihabC = _shihabActiveTreeFilter.Substring(8);
                if (!mehediIsDone || !CategoryMatchesFilterName(mehediItem.ShihabCategoryName, shihabC, mehediItem.ShihabIsTorrent)) return false;
            }
            else if (_shihabActiveTreeFilter.StartsWith("QUEUE:"))
            {
                string mehediQ = _shihabActiveTreeFilter.Substring(6);
                if (!string.Equals(mehediItem.MehediQueueName, mehediQ, StringComparison.OrdinalIgnoreCase)) return false;
            }

            string shihabSearch = _mehediSearchBox.Text.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(shihabSearch))
            {
                bool mehediHit =
                    (mehediItem.ShihabFileName ?? "").ToLowerInvariant().Contains(shihabSearch) ||
                    (mehediItem.MehediUrl ?? "").ToLowerInvariant().Contains(shihabSearch) ||
                    (mehediItem.ShihabDescription ?? "").ToLowerInvariant().Contains(shihabSearch) ||
                    (mehediItem.MehediReferer ?? "").ToLowerInvariant().Contains(shihabSearch);
                if (!mehediHit) return false;
            }
            return true;
        }

        private static string ExtractShihabCompactDate(ShihabDownloadItem mehediItem)
        {
            string shihabRaw = !string.IsNullOrWhiteSpace(mehediItem.ShihabDateAdded)
                ? mehediItem.ShihabDateAdded
                : (mehediItem.MehediLastTryDate ?? "");
            shihabRaw = shihabRaw.Trim();
            if (shihabRaw.Length >= 10)
            {
                return shihabRaw.Substring(0, 10);
            }
            return shihabRaw;
        }

        private static string ResolveCleanStatusWord(ShihabDownloadItem shihabItem, bool mehediRowActive)
        {
            ShihabDownloadStatus shihabSt = shihabItem.GetShihabStatusEnum();
            if (shihabSt == ShihabDownloadStatus.ShihabCompleted) return "Completed";
            if (mehediRowActive || shihabSt == ShihabDownloadStatus.ShihabDownloading) return "Downloading";
            if (shihabSt == ShihabDownloadStatus.ShihabConnecting) return "Connecting";
            if (shihabSt == ShihabDownloadStatus.ShihabRebuilding) return "Muxing";
            if (shihabSt == ShihabDownloadStatus.ShihabQueued) return "Scheduled";
            if (shihabSt == ShihabDownloadStatus.ShihabError) return "Error";
            return "Paused";
        }

        private void RefreshMehediDownloadsGrid()
        {
            if (IsDisposed) return;

            List<ShihabDownloadItem> shihabAll = _shihabEngine.GetShihabDownloadsSnapshot();
            Dictionary<string, int> mehediOrderMap = new Dictionary<string, int>(shihabAll.Count, StringComparer.Ordinal);

            int[] shihabSidebarCounts = new int[11];
            shihabSidebarCounts[0] = shihabAll.Count;

            long mehediSumTotalBytes = 0;
            long shihabSumDownloadedBytes = 0;
            double mehediGlobalSpeedBps = 0.0;
            int shihabActiveSockets = 0;

            for (int shihabI = 0; shihabI < shihabAll.Count; shihabI++)
            {
                ShihabDownloadItem mehediIt = shihabAll[shihabI];
                mehediOrderMap[mehediIt.ShihabId] = shihabI + 1;

                ShihabDownloadStatus shihabSt = mehediIt.GetShihabStatusEnum();
                bool mehediDone = shihabSt == ShihabDownloadStatus.ShihabCompleted;
                bool shihabLive = _shihabEngine.IsMehediItemActive(mehediIt.ShihabId) ||
                                  shihabSt == ShihabDownloadStatus.ShihabDownloading ||
                                  shihabSt == ShihabDownloadStatus.ShihabConnecting ||
                                  shihabSt == ShihabDownloadStatus.ShihabRebuilding;

                if (shihabLive) shihabSidebarCounts[1]++;
                if (mehediDone) shihabSidebarCounts[2]++;
                if (!mehediDone && !shihabLive && shihabSt == ShihabDownloadStatus.ShihabPaused) shihabSidebarCounts[3]++;
                if (!mehediDone && (!string.IsNullOrEmpty(mehediIt.MehediQueueName) || shihabSt == ShihabDownloadStatus.ShihabQueued)) shihabSidebarCounts[4]++;

                string mehediCat = mehediIt.ShihabCategoryName ?? "";
                if (string.Equals(mehediCat, "Video", StringComparison.OrdinalIgnoreCase)) shihabSidebarCounts[5]++;
                else if (string.Equals(mehediCat, "Audio", StringComparison.OrdinalIgnoreCase) || string.Equals(mehediCat, "Music", StringComparison.OrdinalIgnoreCase)) shihabSidebarCounts[6]++;
                else if (string.Equals(mehediCat, "Documents", StringComparison.OrdinalIgnoreCase)) shihabSidebarCounts[7]++;
                else if (string.Equals(mehediCat, "Compressed", StringComparison.OrdinalIgnoreCase)) shihabSidebarCounts[8]++;
                else if (string.Equals(mehediCat, "Programs", StringComparison.OrdinalIgnoreCase)) shihabSidebarCounts[9]++;
                else if (string.Equals(mehediCat, "Torrents", StringComparison.OrdinalIgnoreCase) || mehediIt.ShihabIsTorrent) shihabSidebarCounts[10]++;

                long shihabKnownTot = mehediIt.ShihabTotalBytes > 0 ? mehediIt.ShihabTotalBytes : Math.Max(0L, mehediIt.MehediDownloadedBytes);
                long mehediKnownDown = mehediDone ? shihabKnownTot : Math.Max(0L, mehediIt.MehediDownloadedBytes);
                mehediSumTotalBytes += shihabKnownTot;
                shihabSumDownloadedBytes += mehediKnownDown;

                if (shihabLive)
                {
                    mehediGlobalSpeedBps += mehediIt.ShihabCurrentSpeedBps;
                    int shihabItemWorkers = 0;
                    if (mehediIt.MehediSegments != null)
                    {
                        for (int mehediSegIdx = 0; mehediSegIdx < mehediIt.MehediSegments.Count; mehediSegIdx++)
                        {
                            MehediSegmentChunk shihabChunk = mehediIt.MehediSegments[mehediSegIdx];
                            if (shihabChunk != null && shihabChunk.ShihabIsActiveWorker)
                            {
                                shihabItemWorkers++;
                            }
                        }
                    }
                    shihabActiveSockets += Math.Max(1, shihabItemWorkers);
                }
            }

            if (_shihabTransfersNav != null)
            {
                _shihabTransfersNav.UpdateShihabCounts(shihabSidebarCounts);
            }

            List<ShihabDownloadItem> mehediFiltered = new List<ShihabDownloadItem>();
            for (int shihabI = 0; shihabI < shihabAll.Count; shihabI++)
            {
                if (MatchesShihabTreeAndSearch(shihabAll[shihabI]))
                {
                    mehediFiltered.Add(shihabAll[shihabI]);
                }
            }

            if (_mehediSortColumnKey == "ORDER")
            {
                if (!_shihabSortAscending)
                {
                    mehediFiltered.Reverse();
                }
            }
            else
            {
                int mehediDir = _shihabSortAscending ? 1 : -1;
                mehediFiltered.Sort(delegate(ShihabDownloadItem shihabA, ShihabDownloadItem mehediB)
                {
                    if (_mehediSortColumnKey == "NAME") return mehediDir * string.Compare(shihabA.ShihabFileName, mehediB.ShihabFileName, StringComparison.OrdinalIgnoreCase);
                    if (_mehediSortColumnKey == "SIZE") return mehediDir * shihabA.ShihabTotalBytes.CompareTo(mehediB.ShihabTotalBytes);
                    if (_mehediSortColumnKey == "PROGRESS") return mehediDir * shihabA.GetMehediPercent().CompareTo(mehediB.GetMehediPercent());
                    if (_mehediSortColumnKey == "STATUS") return mehediDir * string.Compare(shihabA.ShihabStatusText, mehediB.ShihabStatusText, StringComparison.OrdinalIgnoreCase);
                    if (_mehediSortColumnKey == "SPEED") return mehediDir * shihabA.ShihabCurrentSpeedBps.CompareTo(mehediB.ShihabCurrentSpeedBps);
                    return 0;
                });
            }

            string shihabNewBadgeTxt = mehediFiltered.Count == 1 ? "1 item" : string.Format("{0} items", mehediFiltered.Count);
            if (!string.Equals(_mehediHeaderItemsBadgeText, shihabNewBadgeTxt, StringComparison.Ordinal))
            {
                _mehediHeaderItemsBadgeText = shihabNewBadgeTxt;
                RepositionShihabHeaderControls();
            }

            HashSet<string> shihabSelectedIds = new HashSet<string>();
            foreach (ListViewItem mehediSel in _shihabDownloadsListView.SelectedItems)
            {
                ShihabDownloadItem shihabTag = mehediSel.Tag as ShihabDownloadItem;
                if (shihabTag != null) shihabSelectedIds.Add(shihabTag.ShihabId);
            }

            _shihabDownloadsListView.BeginUpdate();
            while (_shihabDownloadsListView.Items.Count < mehediFiltered.Count)
            {
                _shihabDownloadsListView.Items.Add(new ListViewItem(new string[] { "", "", "", "", "", "", "", "" }));
            }
            while (_shihabDownloadsListView.Items.Count > mehediFiltered.Count)
            {
                _shihabDownloadsListView.Items.RemoveAt(_shihabDownloadsListView.Items.Count - 1);
            }

            for (int mehediIdx = 0; mehediIdx < mehediFiltered.Count; mehediIdx++)
            {
                ShihabDownloadItem shihabIt = mehediFiltered[mehediIdx];
                ListViewItem mehediRow = _shihabDownloadsListView.Items[mehediIdx];
                mehediRow.Tag = shihabIt;

                int shihabOrderNum;
                if (!mehediOrderMap.TryGetValue(shihabIt.ShihabId, out shihabOrderNum))
                {
                    shihabOrderNum = mehediIdx + 1;
                }

                bool mehediRowActive = _shihabEngine.IsMehediItemActive(shihabIt.ShihabId) ||
                                       shihabIt.GetShihabStatusEnum() == ShihabDownloadStatus.ShihabDownloading;
                double shihabPctVal = shihabIt.GetMehediPercent();

                mehediRow.SubItems[0].Text = shihabOrderNum.ToString();
                mehediRow.SubItems[1].Text = shihabIt.ShihabFileName ?? "";
                mehediRow.SubItems[2].Text = shihabIt.ShihabTotalBytes > 0
                    ? ShihabFormatHelpers.FormatMehediBytes(shihabIt.ShihabTotalBytes)
                    : (shihabIt.MehediDownloadedBytes > 0 ? ShihabFormatHelpers.FormatMehediBytes(shihabIt.MehediDownloadedBytes) : "--");
                mehediRow.SubItems[3].Text = (shihabIt.GetShihabStatusEnum() == ShihabDownloadStatus.ShihabCompleted || shihabIt.ShihabTotalBytes > 0)
                    ? string.Format("{0:0}%", shihabPctVal)
                    : (mehediRowActive ? "Live" : "0%");
                mehediRow.SubItems[4].Text = (mehediRowActive && shihabIt.ShihabCurrentSpeedBps > 1.0)
                    ? ShihabFormatHelpers.FormatShihabSpeed(shihabIt.ShihabCurrentSpeedBps).Replace("/sec", "/s")
                    : "0 B/s";
                mehediRow.SubItems[5].Text = (mehediRowActive && shihabIt.MehediEtaSeconds > 0)
                    ? ShihabFormatHelpers.FormatMehediTimeLeft(shihabIt.MehediEtaSeconds)
                    : "0s";
                string geoPrefix = !string.IsNullOrWhiteSpace(shihabIt.ShihabServerCountryCode)
                    ? ("[" + shihabIt.ShihabServerCountryCode + "] ")
                    : "";
                string chkBadge = (!string.IsNullOrWhiteSpace(shihabIt.ShihabChecksumStatus) && shihabIt.ShihabChecksumStatus.StartsWith("Verified", StringComparison.OrdinalIgnoreCase))
                    ? " ✔"
                    : "";
                mehediRow.SubItems[6].Text = geoPrefix + ResolveCleanStatusWord(shihabIt, mehediRowActive) + chkBadge;
                mehediRow.SubItems[7].Text = ExtractShihabCompactDate(shihabIt);
                mehediRow.Selected = shihabSelectedIds.Contains(shihabIt.ShihabId);
            }
            _shihabDownloadsListView.EndUpdate();

            string shihabSpeedPill = mehediGlobalSpeedBps > 5.0
                ? ("Speed: " + ShihabFormatHelpers.FormatShihabSpeed(mehediGlobalSpeedBps).Replace("/sec", "/s"))
                : "Speed: 0 KB/s";
            if (_mehediFooterBar != null)
            {
                _mehediFooterBar.SetMehediFooterMetrics(
                    "Total: " + ShihabFormatHelpers.FormatMehediBytes(mehediSumTotalBytes),
                    "Downloaded: " + ShihabFormatHelpers.FormatMehediBytes(shihabSumDownloadedBytes),
                    shihabSpeedPill,
                    string.Format("Active Sockets: {0}", shihabActiveSockets));
            }

            if (_shihabDropTargetForm != null && !_shihabDropTargetForm.IsDisposed && _shihabDropTargetForm.Visible)
            {
                _shihabDropTargetForm.UpdateShihabLiveSpeed(mehediGlobalSpeedBps);
            }
            UpdateShihabStatusBadges();
        }

        private void UpdateShihabStatusBadges()
        {
            if (_mehediStorageCard == null) return;

            DateTime shihabNowUtc = DateTime.UtcNow;
            if ((shihabNowUtc - _mehediLastDrivePollUtc).TotalSeconds >= 5.0)
            {
                _mehediLastDrivePollUtc = shihabNowUtc;
                try
                {
                    string shihabSampleFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    if (_mehediConfig.ShihabCategories != null && _mehediConfig.ShihabCategories.Count > 0)
                    {
                        for (int mehediI = 0; mehediI < _mehediConfig.ShihabCategories.Count; mehediI++)
                        {
                            if (string.Equals(_mehediConfig.ShihabCategories[mehediI].ShihabName, "Video", StringComparison.OrdinalIgnoreCase) &&
                                !string.IsNullOrWhiteSpace(_mehediConfig.ShihabCategories[mehediI].ShihabDefaultFolder))
                            {
                                shihabSampleFolder = _mehediConfig.ShihabCategories[mehediI].ShihabDefaultFolder;
                                break;
                            }
                        }
                    }
                    string mehediRoot = Path.GetPathRoot(Path.GetFullPath(shihabSampleFolder));
                    if (!string.IsNullOrEmpty(mehediRoot))
                    {
                        DriveInfo shihabDrive = new DriveInfo(mehediRoot);
                        if (shihabDrive.IsReady)
                        {
                            double mehediBytesPerGb = 1024.0 * 1024.0 * 1024.0;
                            _mehediCachedTotalGb = shihabDrive.TotalSize / mehediBytesPerGb;
                            _shihabCachedFreeGb = shihabDrive.AvailableFreeSpace / mehediBytesPerGb;
                            _shihabCachedUsedGb = Math.Max(0.0, _mehediCachedTotalGb - _shihabCachedFreeGb);
                        }
                    }
                }
                catch { }
            }

            string mehediSpeedLimitLabel = _mehediConfig.MehediGlobalSpeedLimiterEnabled
                ? string.Format("Speed: {0} KB/s", _mehediConfig.ShihabGlobalSpeedLimitKb)
                : "Speed: Unlimited";
            _mehediStorageCard.SetShihabStorageAndSpeed(_shihabCachedUsedGb, _mehediCachedTotalGb, _shihabCachedFreeGb, mehediSpeedLimitLabel);
        }

        private void OnShihabMainFormFirstShown()
        {
            if (_mehediConfig.MehediShowFloatingDropTarget)
            {
                ToggleMehediFloatingDropTarget();
            }

            if (_shihabStartupCli != null && !string.IsNullOrWhiteSpace(_shihabStartupCli.ShihabUpdatedFromVersion))
            {
                try
                {
                    if (_mehediTrayIcon != null)
                    {
                        _mehediTrayIcon.ShowBalloonTip(
                            6000,
                            "MHS Download Manager Updated!",
                            string.Format("Successfully updated from v{0} to v{1}.",
                                _shihabStartupCli.ShihabUpdatedFromVersion,
                                ShihabAutoUpdater.GetCurrentAppVersion()),
                            ToolTipIcon.Info);
                    }
                }
                catch { }
            }

            string uncleanCrashLog;
            if (ShihabCrashMonitor.CheckAndClearUncleanShutdownMarker(_shihabStore.MehediBaseDirectory, out uncleanCrashLog))
            {
                List<ShihabDownloadItem> snap = _shihabEngine.GetShihabDownloadsSnapshot();
                List<ShihabDownloadItem> unfinished = new List<ShihabDownloadItem>();
                for (int i = 0; i < snap.Count; i++)
                {
                    if (snap[i].GetShihabStatusEnum() != ShihabDownloadStatus.ShihabCompleted)
                    {
                        unfinished.Add(snap[i]);
                    }
                }
                if (unfinished.Count > 0)
                {
                    DialogResult recoverAns = MessageBox.Show(
                        this,
                        string.Format("MHS Download Manager detected an unexpected exit in the previous session.\r\n\r\nWould you like to automatically resume your {0} unfinished download(s) from their exact verified byte checkpoints?", unfinished.Count),
                        "Crash Recovery — MHS Download Manager",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    if (recoverAns == DialogResult.Yes)
                    {
                        for (int i = 0; i < unfinished.Count; i++)
                        {
                            _shihabEngine.StartOrResumeMehediDownload(unfinished[i]);
                        }
                    }
                }
            }

            _shihabEngine.StartStartupQueues();

            if (_shihabStartupCli != null)
            {
                if (_shihabStartupCli.ShihabStartMinimized)
                {
                    Hide();
                }

                if (_shihabStartupCli.MehediAutoCloseMs > 0)
                {
                    Timer mehediAutoClose = new Timer { Interval = Math.Max(400, _shihabStartupCli.MehediAutoCloseMs) };
                    mehediAutoClose.Tick += delegate
                    {
                        mehediAutoClose.Stop();
                        ExitShihabApplicationCleanly();
                    };
                    mehediAutoClose.Start();
                }

                if (!string.IsNullOrWhiteSpace(_shihabStartupCli.ShihabDirectUrl))
                {
                    ShihabCapturedLinkDto shihabCliDto = new ShihabCapturedLinkDto
                    {
                        ShihabUrl = _shihabStartupCli.ShihabDirectUrl,
                        ShihabFileName = _shihabStartupCli.ShihabLocalFileName ?? "",
                        ShihabSaveDirectory = _shihabStartupCli.MehediLocalSavePath ?? "",
                        ShihabSilentMode = _shihabStartupCli.MehediSilentNoPrompt,
                        MehediAddToQueueOnly = _shihabStartupCli.MehediAddToQueueOnly,
                        MehediExitWhenDone = _shihabStartupCli.ShihabExitOnDone
                    };
                    HandleBrowserShihabSingleCapture(shihabCliDto);
                }

                if (_shihabStartupCli.ShihabStartMainQueue)
                {
                    _shihabEngine.StartShihabQueue("Main Download Queue");
                }
            }

            // Automatic GitHub Release Update Check: 5s after startup + every 6 hours
            Timer startupUpdateTimer = new Timer { Interval = 5000 };
            startupUpdateTimer.Tick += delegate
            {
                startupUpdateTimer.Stop();
                startupUpdateTimer.Dispose();
                if (!IsDisposed && _mehediConfig.ShihabAutoCheckUpdates)
                {
                    TriggerShihabUpdateCheck(false);
                }
            };
            startupUpdateTimer.Start();

            _shihabPeriodicUpdateTimer = new Timer { Interval = 6 * 60 * 60 * 1000 };
            _shihabPeriodicUpdateTimer.Tick += delegate
            {
                if (!IsDisposed && _mehediConfig.ShihabAutoCheckUpdates)
                {
                    TriggerShihabUpdateCheck(false);
                }
            };
            _shihabPeriodicUpdateTimer.Start();

            // Background RSS / Atom Feed Auto-Downloader Poller (every 60s)
            _shihabRssPollTimer = new Timer { Interval = 60000 };
            _shihabRssPollTimer.Tick += delegate { PollRssSubscriptionsInBackgroundAsync(); };
            _shihabRssPollTimer.Start();
        }

        private void RestoreAndActivateMainWindow()
        {
            if (IsDisposed) return;
            Show();
            ShowInTaskbar = true;
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }
            Activate();
            BringToFront();
        }

        private void TriggerShihabUpdateCheck(bool manualUserInitiated)
        {
            if (_shihabUpdateCheckInFlight || IsDisposed) return;
            _shihabUpdateCheckInFlight = true;

            Task.Run(delegate
            {
                ShihabUpdateCheckResult res = ShihabAutoUpdater.CheckForUpdates(_mehediConfig, manualUserInitiated);
                SafeShihabUiInvoke(delegate
                {
                    _shihabUpdateCheckInFlight = false;
                    if (IsDisposed || res == null) return;
                    _shihabStore.SaveMehediConfig(_mehediConfig);

                    if (res.Ok && res.UpdateAvailable)
                    {
                        _shihabLatestDetectedUpdate = res;
                        if (_shihabUpdatePillBtn != null)
                        {
                            _shihabUpdatePillBtn.Text = "Update v" + res.LatestVersion;
                        }
                        RepositionShihabHeaderControls();

                        try
                        {
                            if (_mehediTrayIcon != null && !manualUserInitiated)
                            {
                                _mehediTrayIcon.ShowBalloonTip(
                                    8000,
                                    "Update Available — MHS Download Manager v" + res.LatestVersion,
                                    string.Format("A new GitHub release (v{0}) is ready! Click to view release notes and install.", res.LatestVersion),
                                    ToolTipIcon.Info);
                            }
                        }
                        catch { }

                        PlayShihabConfiguredSound(_mehediConfig.MehediPlaySoundComplete, _mehediConfig.ShihabSoundCompleteWav, false);
                        OpenShihabAutoUpdateDialog(res);
                    }
                    else if (manualUserInitiated)
                    {
                        if (!res.Ok)
                        {
                            MessageBox.Show(
                                this,
                                string.IsNullOrWhiteSpace(res.ErrorMessage) ? "Unable to contact GitHub Releases API." : res.ErrorMessage,
                                "Check for Updates — MHS Download Manager",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                        else
                        {
                            MessageBox.Show(
                                this,
                                string.Format("You are running the latest version of MHS Download Manager (v{0}).\r\n\r\nRepository: {1}",
                                    res.CurrentVersion,
                                    string.IsNullOrWhiteSpace(_mehediConfig.ShihabUpdateRepoSlug) ? ShihabAutoUpdater.DefaultRepoSlug : _mehediConfig.ShihabUpdateRepoSlug),
                                "Up to Date — MHS Download Manager",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                    }
                });
            });
        }

        private void OpenShihabAutoUpdateDialog(ShihabUpdateCheckResult res)
        {
            if (res == null || !res.UpdateAvailable || _shihabUpdateDialogOpen || IsDisposed) return;
            _shihabUpdateDialogOpen = true;
            try
            {
                using (ShihabAutoUpdateDialog dlg = new ShihabAutoUpdateDialog(res, _mehediConfig, _shihabStore, ExitShihabApplicationCleanly))
                {
                    IWin32Window owner = GetShihabDialogOwnerOrNull();
                    DialogResult ans = owner != null ? dlg.ShowDialog(owner) : dlg.ShowDialog();
                    if (ans == DialogResult.Ignore)
                    {
                        _shihabLatestDetectedUpdate = null;
                        RepositionShihabHeaderControls();
                    }
                }
            }
            finally
            {
                _shihabUpdateDialogOpen = false;
            }
        }

        private void PollRssSubscriptionsInBackgroundAsync()
        {
            if (_shihabRssPollInFlight || IsDisposed) return;
            if (_mehediConfig.ShihabRssFeeds == null || _mehediConfig.ShihabRssFeeds.Count == 0) return;
            _shihabRssPollInFlight = true;

            Task.Run(delegate
            {
                try
                {
                    List<KeyValuePair<ShihabRssFeedConfig, ShihabRssEntryItem>> matched =
                        new List<KeyValuePair<ShihabRssFeedConfig, ShihabRssEntryItem>>();
                    bool dirty = ShihabRssFeedManager.PollDueFeedsInBackground(_mehediConfig, delegate(ShihabRssFeedConfig sub, ShihabRssEntryItem entry)
                    {
                        if (sub != null && sub.ShihabAutoDownload && entry != null && !string.IsNullOrWhiteSpace(entry.DownloadUrl))
                        {
                            matched.Add(new KeyValuePair<ShihabRssFeedConfig, ShihabRssEntryItem>(sub, entry));
                        }
                    });

                    if (dirty || matched.Count > 0)
                    {
                        SafeShihabUiInvoke(delegate
                        {
                            for (int i = 0; i < matched.Count; i++)
                            {
                                ShihabRssFeedConfig sub = matched[i].Key;
                                ShihabRssEntryItem entry = matched[i].Value;
                                string dlUrl = entry.DownloadUrl.Trim();
                                ShihabDownloadItem rssItem = new ShihabDownloadItem
                                {
                                    MehediUrl = dlUrl,
                                    ShihabDescription = "RSS Auto: " + (entry.Title ?? sub.ShihabFeedTitle ?? ""),
                                    ShihabFileName = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(dlUrl),
                                    MehediMaxConnections = _mehediConfig.ShihabDefaultMaxConnections
                                };
                                if (dlUrl.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase) ||
                                    dlUrl.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                                {
                                    rssItem.ShihabIsTorrent = true;
                                    rssItem.ShihabTorrentMagnetOrPath = dlUrl;
                                    rssItem.ShihabCategoryName = "Torrents";
                                }
                                else
                                {
                                    ShihabCategoryRule cat = _mehediConfig.ResolveMehediCategoryForFile(rssItem.ShihabFileName);
                                    rssItem.ShihabCategoryName = string.IsNullOrWhiteSpace(sub.MehediCategoryName) ? cat.ShihabName : sub.MehediCategoryName;
                                    rssItem.MehediSaveDirectory = !string.IsNullOrWhiteSpace(sub.ShihabSaveDirectory) ? sub.ShihabSaveDirectory : cat.ShihabDefaultFolder;
                                }
                                if (string.IsNullOrWhiteSpace(rssItem.MehediSaveDirectory))
                                {
                                    rssItem.MehediSaveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                                }
                                Directory.CreateDirectory(rssItem.MehediSaveDirectory);
                                rssItem.ShihabFileName = ShihabFormatHelpers.EnsureShihabUniqueFilePath(rssItem.MehediSaveDirectory, rssItem.ShihabFileName);
                                _shihabEngine.AddShihabDownloadItem(rssItem, true);
                            }
                            _shihabStore.SaveMehediConfig(_mehediConfig);
                        });
                    }
                }
                catch { }
                finally
                {
                    _shihabRssPollInFlight = false;
                }
            });
        }

        private void HandleQueueFinishedWithActions(MehediQueueConfig queue)
        {
            if (queue == null) return;
            SafeShihabUiInvoke(delegate
            {
                if (IsDisposed) return;
                if (queue.MehediExitAppWhenDone)
                {
                    ExitShihabApplicationCleanly();
                    return;
                }

                string pwr = (queue.ShihabSystemPowerActionWhenDone ?? "None").Trim();
                if (string.IsNullOrEmpty(pwr) || string.Equals(pwr, "None", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                int secondsLeft = 30;
                bool dark = _mehediConfig.ShihabDarkMode;
                using (Form prompt = new Form())
                {
                    prompt.Text = "Queue Post-Completion Action — MHS Download Manager";
                    prompt.ClientSize = new Size(440, 165);
                    prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                    prompt.MaximizeBox = false;
                    prompt.MinimizeBox = false;
                    prompt.StartPosition = FormStartPosition.CenterScreen;
                    prompt.TopMost = true;
                    prompt.BackColor = ShihabThemePalette.GetMehediWindowBg(dark);
                    prompt.ForeColor = ShihabThemePalette.GetShihabPrimaryText(dark);
                    prompt.Font = new Font("Segoe UI", 9.2f);
                    ShihabThemePalette.ApplyMehediNativeDarkFrame(prompt, dark);

                    Label msgLbl = new Label
                    {
                        Location = new Point(18, 18),
                        Size = new Size(404, 72),
                        Text = string.Format("Queue \"{0}\" has finished all downloads.\r\n\r\nSystem action [{1}] will execute in {2} seconds unless canceled.",
                            queue.ShihabQueueName, pwr, secondsLeft)
                    };
                    Button execNowBtn = ShihabThemePalette.CreateMehediStyledButton(pwr + " Now", ShihabThemePalette.GetMehediAccentRed(), Color.White, 130);
                    execNowBtn.Location = new Point(166, 110);
                    execNowBtn.DialogResult = DialogResult.OK;

                    Button cancelBtn = ShihabThemePalette.CreateMehediStyledButton("Cancel Action", ShihabThemePalette.GetShihabSurfaceBg(dark), prompt.ForeColor, 120);
                    cancelBtn.Location = new Point(302, 110);
                    cancelBtn.DialogResult = DialogResult.Cancel;

                    prompt.Controls.AddRange(new Control[] { msgLbl, execNowBtn, cancelBtn });
                    prompt.CancelButton = cancelBtn;

                    using (Timer countdown = new Timer { Interval = 1000 })
                    {
                        countdown.Tick += delegate
                        {
                            secondsLeft--;
                            if (secondsLeft <= 0)
                            {
                                countdown.Stop();
                                prompt.DialogResult = DialogResult.OK;
                                prompt.Close();
                                return;
                            }
                            msgLbl.Text = string.Format("Queue \"{0}\" has finished all downloads.\r\n\r\nSystem action [{1}] will execute in {2} seconds unless canceled.",
                                queue.ShihabQueueName, pwr, secondsLeft);
                        };
                        countdown.Start();

                        IWin32Window owner = GetShihabDialogOwnerOrNull();
                        DialogResult ans = owner != null ? prompt.ShowDialog(owner) : prompt.ShowDialog();
                        countdown.Stop();
                        if (ans != DialogResult.OK)
                        {
                            return;
                        }
                    }
                }

                if (string.Equals(pwr, "Sleep", StringComparison.OrdinalIgnoreCase))
                {
                    ShihabOsAndDiskSafety.EnterSystemSleepOrHibernate(false);
                }
                else if (string.Equals(pwr, "Hibernate", StringComparison.OrdinalIgnoreCase))
                {
                    ShihabOsAndDiskSafety.EnterSystemSleepOrHibernate(true);
                }
                else if (string.Equals(pwr, "Shutdown", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo("shutdown.exe", "/s /t 5 /c \"MHS Download Manager queue completed.\"");
                        psi.UseShellExecute = false;
                        psi.CreateNoWindow = true;
                        Process.Start(psi);
                        ExitShihabApplicationCleanly();
                    }
                    catch { }
                }
            });
        }

        private void HandleDragDropDataObject(IDataObject data)
        {
            if (data == null) return;
            try
            {
                if (data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = data.GetData(DataFormats.FileDrop) as string[];
                    if (files != null)
                    {
                        for (int i = 0; i < files.Length; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(files[i]))
                            {
                                HandleDroppedTextOrFilePath(files[i]);
                            }
                        }
                    }
                    return;
                }
                string droppedText = (data.GetData(DataFormats.UnicodeText) ?? data.GetData(DataFormats.Text) ?? "").ToString().Trim();
                if (!string.IsNullOrEmpty(droppedText))
                {
                    HandleDroppedTextOrFilePath(droppedText);
                }
            }
            catch { }
        }

        private void HandleDroppedTextOrFilePath(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            string trimmed = raw.Trim();
            if (File.Exists(trimmed))
            {
                string ext = (Path.GetExtension(trimmed) ?? "").ToLowerInvariant();
                if (ext == ".torrent")
                {
                    RestoreAndActivateMainWindow();
                    OpenShihabTorrentDialog(trimmed);
                    return;
                }
                if (ext == ".ef2" || ext == ".txt")
                {
                    List<ShihabDownloadItem> imported = ShihabStateStore.ImportFromMehediEf2OrText(trimmed);
                    for (int i = 0; i < imported.Count; i++)
                    {
                        ShihabDownloadItem it = imported[i];
                        ShihabCategoryRule cat = _mehediConfig.ResolveMehediCategoryForFile(it.ShihabFileName);
                        it.ShihabCategoryName = cat.ShihabName;
                        it.MehediSaveDirectory = cat.ShihabDefaultFolder;
                        it.MehediQueueName = "Main Download Queue";
                        it.SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued");
                        _shihabEngine.AddShihabDownloadItem(it, false);
                    }
                    RefreshMehediDownloadsGrid();
                    return;
                }
            }
            PromptAndAddNewShihabUrl(trimmed);
        }

        private void OnMehediPeriodicTimerTick()
        {
            _shihabEngine.TickMehediQueuesAndScheduler();
            RefreshMehediDownloadsGrid();

            if (_mehediConfig.ShihabMonitorClipboard)
            {
                PollShihabClipboardForDownloadUrl();
            }
        }

        private void PollShihabClipboardForDownloadUrl()
        {
            if (_shihabClipboardPollInFlight) return;
            _shihabClipboardPollInFlight = true;
            try
            {
                if (!Clipboard.ContainsText()) return;
                string mehediText = Clipboard.GetText().Trim();
                if (string.IsNullOrEmpty(mehediText) || mehediText == _shihabLastClipboardText || mehediText.Length > 2048)
                {
                    return;
                }
                _shihabLastClipboardText = mehediText;

                if (mehediText.StartsWith("magnet:?xt=urn:btih:", StringComparison.OrdinalIgnoreCase))
                {
                    OpenShihabTorrentDialog(mehediText);
                    return;
                }

                Uri shihabUri;
                if (!Uri.TryCreate(mehediText, UriKind.Absolute, out shihabUri)) return;
                if (shihabUri.Scheme != "http" && shihabUri.Scheme != "https" && shihabUri.Scheme != "ftp" && shihabUri.Scheme != "ftps") return;

                string mehediExt = Path.GetExtension(shihabUri.AbsolutePath).TrimStart('.');
                if (string.IsNullOrEmpty(mehediExt)) return;

                if (_mehediConfig.MatchesShihabAutoCaptureExtension(mehediExt))
                {
                    ShihabCapturedLinkDto shihabClipDto = new ShihabCapturedLinkDto
                    {
                        ShihabUrl = mehediText,
                        ShihabFileName = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(mehediText),
                        ShihabSilentMode = false
                    };
                    HandleBrowserShihabSingleCapture(shihabClipDto);
                }
            }
            catch { }
            finally
            {
                _shihabClipboardPollInFlight = false;
            }
        }

        private IWin32Window GetShihabDialogOwnerOrNull()
        {
            if (Visible && WindowState != FormWindowState.Minimized)
            {
                return this;
            }
            return null;
        }

        private static void PlayShihabConfiguredSound(bool mehediEnabled, string shihabWavPath, bool mehediIsError)
        {
            if (!mehediEnabled) return;
            try
            {
                if (!string.IsNullOrWhiteSpace(shihabWavPath) && File.Exists(shihabWavPath))
                {
                    using (SoundPlayer mehediPlayer = new SoundPlayer(shihabWavPath))
                    {
                        mehediPlayer.Play();
                    }
                    return;
                }
                if (mehediIsError) SystemSounds.Hand.Play();
                else SystemSounds.Asterisk.Play();
            }
            catch { }
        }

        private void HandleBrowserShihabSingleCapture(ShihabCapturedLinkDto mehediDto)
        {
            SafeShihabUiInvoke(delegate
            {
                if (mehediDto == null || string.IsNullOrWhiteSpace(mehediDto.ShihabUrl))
                {
                    return;
                }

                string shihabTrimmedUrl = mehediDto.ShihabUrl.Trim();
                DateTime mehediUtcNow = DateTime.UtcNow;

                List<string> shihabExpiredUrls = null;
                foreach (KeyValuePair<string, DateTime> mehediKv in _mehediRecentCapturedUrlsUtc)
                {
                    if ((mehediUtcNow - mehediKv.Value).TotalSeconds > 30.0)
                    {
                        if (shihabExpiredUrls == null) shihabExpiredUrls = new List<string>();
                        shihabExpiredUrls.Add(mehediKv.Key);
                    }
                }
                if (shihabExpiredUrls != null)
                {
                    for (int shihabExpIdx = 0; shihabExpIdx < shihabExpiredUrls.Count; shihabExpIdx++)
                    {
                        _mehediRecentCapturedUrlsUtc.Remove(shihabExpiredUrls[shihabExpIdx]);
                    }
                }

                DateTime shihabPrevSeenUtc;
                if (_mehediRecentCapturedUrlsUtc.TryGetValue(shihabTrimmedUrl, out shihabPrevSeenUtc) &&
                    (mehediUtcNow - shihabPrevSeenUtc).TotalSeconds < 4.0)
                {
                    return;
                }
                _mehediRecentCapturedUrlsUtc[shihabTrimmedUrl] = mehediUtcNow;

                ShihabDownloadItem shihabNewItem = new ShihabDownloadItem();
                shihabNewItem.MehediUrl = shihabTrimmedUrl;
                shihabNewItem.MehediReferer = mehediDto.MehediReferrer ?? "";
                shihabNewItem.ShihabCookies = mehediDto.ShihabCookies ?? "";
                shihabNewItem.MehediOriginHeader = mehediDto.ShihabOrigin ?? "";
                shihabNewItem.MehediOmitCookies = mehediDto.MehediOmitCookies;
                shihabNewItem.MehediAudioCompanionUrl = mehediDto.ShihabAudioUrl ?? "";
                shihabNewItem.ShihabAudioCompanionBytes = mehediDto.MehediAudioSize > 0 ? mehediDto.MehediAudioSize : -1L;
                shihabNewItem.ShihabSourceTabId = mehediDto.ShihabTabId >= 0 ? mehediDto.ShihabTabId : -1;
                shihabNewItem.ShihabQualityLabel = mehediDto.MehediQualityLabel ?? "";
                shihabNewItem.ShihabPostBody = mehediDto.ShihabPostBody ?? "";
                shihabNewItem.ShihabExpectedChecksum = mehediDto.ShihabExpectedChecksum ?? "";
                shihabNewItem.ShihabIsPrivateIncognito = mehediDto.ShihabIncognito;
                string dtoKind = (mehediDto.ShihabJobKind ?? "").Trim().ToLowerInvariant();
                if (dtoKind == "ytdlp" || dtoKind == "video" || dtoKind == "playlist")
                {
                    shihabNewItem.ShihabIsYtDlpJob = true;
                }
                else if (dtoKind == "gallery")
                {
                    shihabNewItem.ShihabIsGalleryJob = true;
                }
                if (!string.IsNullOrWhiteSpace(mehediDto.ShihabFormatSelector))
                {
                    shihabNewItem.ShihabYtDlpFormatSelector = mehediDto.ShihabFormatSelector.Trim();
                }
                if (mehediDto.ShihabAudioOnly)
                {
                    shihabNewItem.ShihabAudioExtractFormat = "mp3";
                }
                if (shihabTrimmedUrl.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase) ||
                    shihabTrimmedUrl.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                {
                    shihabNewItem.ShihabIsTorrent = true;
                    shihabNewItem.ShihabTorrentMagnetOrPath = shihabTrimmedUrl;
                }
                if (!string.IsNullOrWhiteSpace(mehediDto.MehediUserAgent))
                {
                    shihabNewItem.MehediUserAgent = mehediDto.MehediUserAgent;
                }
                shihabNewItem.ShihabDescription = mehediDto.MehediTabTitle ?? "";
                shihabNewItem.ShihabTotalBytes = mehediDto.ShihabFileSize > 0 ? mehediDto.ShihabFileSize : -1L;
                shihabNewItem.MehediMaxConnections = _mehediConfig.ShihabDefaultMaxConnections;
                shihabNewItem.MehediExitAppOnComplete = mehediDto.MehediExitWhenDone;
                shihabNewItem.ShihabShowCompleteDialog = _mehediConfig.ShihabShowDownloadCompleteDialog && !mehediDto.ShihabSilentMode;

                string mehediRawName = ShihabFormatHelpers.ResolveSmartMehediFileName(
                    mehediDto.ShihabFileName,
                    shihabNewItem.MehediUrl,
                    mehediDto.MehediTabTitle,
                    shihabNewItem.ShihabQualityLabel);
                shihabNewItem.ShihabFileName = mehediRawName;

                ShihabCategoryRule shihabCat = _mehediConfig.ResolveMehediCategoryForFile(mehediRawName);
                shihabNewItem.ShihabCategoryName = shihabCat.ShihabName;
                string mehediTargetDir = !string.IsNullOrWhiteSpace(mehediDto.ShihabSaveDirectory)
                    ? mehediDto.ShihabSaveDirectory
                    : shihabCat.ShihabDefaultFolder;
                Directory.CreateDirectory(mehediTargetDir);
                shihabNewItem.MehediSaveDirectory = mehediTargetDir;
                shihabNewItem.ShihabFileName = (_mehediConfig.MehediDuplicateFileMode == 1)
                    ? ShihabFormatHelpers.SanitizeShihabFileName(mehediRawName)
                    : ShihabFormatHelpers.EnsureShihabUniqueFilePath(mehediTargetDir, mehediRawName);

                if (mehediDto.MehediAddToQueueOnly)
                {
                    shihabNewItem.MehediQueueName = "Main Download Queue";
                    shihabNewItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued");
                    _shihabEngine.AddShihabDownloadItem(shihabNewItem, false);
                    return;
                }

                if (mehediDto.ShihabSilentMode || !_mehediConfig.ShihabShowStartDownloadDialog)
                {
                    _shihabEngine.AddShihabDownloadItem(shihabNewItem, true);
                    if (_mehediConfig.MehediShowDownloadProgressDialog && !mehediDto.ShihabSilentMode)
                    {
                        ShowProgressWindowForShihabItem(shihabNewItem);
                    }
                    return;
                }

                foreach (ShihabDownloadItem mehediQueued in _mehediPendingCaptureModalQueue)
                {
                    if (string.Equals(mehediQueued.MehediUrl, shihabNewItem.MehediUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }
                _mehediPendingCaptureModalQueue.Enqueue(shihabNewItem);
                PumpNextPendingShihabCaptureModal();
            });
        }

        private void PumpNextPendingShihabCaptureModal()
        {
            if (_shihabShowingCaptureModal || IsDisposed)
            {
                return;
            }
            _shihabShowingCaptureModal = true;
            try
            {
                while (_mehediPendingCaptureModalQueue.Count > 0 && !IsDisposed)
                {
                    ShihabDownloadItem shihabNextItem = _mehediPendingCaptureModalQueue.Dequeue();
                    using (MehediDownloadFileInfoForm shihabInfoDlg = new MehediDownloadFileInfoForm(shihabNextItem, _mehediConfig, _shihabEngine))
                    {
                        IWin32Window mehediOwner = GetShihabDialogOwnerOrNull();
                        if (mehediOwner == null) shihabInfoDlg.StartPosition = FormStartPosition.CenterScreen;
                        DialogResult shihabDlgRes = mehediOwner != null ? shihabInfoDlg.ShowDialog(mehediOwner) : shihabInfoDlg.ShowDialog();
                        if (shihabDlgRes == DialogResult.OK)
                        {
                            _shihabStore.SaveMehediConfig(_mehediConfig);
                            _shihabEngine.AddShihabDownloadItem(shihabNextItem, shihabInfoDlg.ShihabStartImmediatelySelected);
                            if (shihabInfoDlg.ShihabStartImmediatelySelected && _mehediConfig.MehediShowDownloadProgressDialog)
                            {
                                ShowProgressWindowForShihabItem(shihabNextItem);
                            }
                        }
                    }
                }
            }
            finally
            {
                _shihabShowingCaptureModal = false;
            }
        }

        private void HandleBrowserMehediBatchCapture(ShihabBatchCaptureDto shihabBatch)
        {
            SafeShihabUiInvoke(delegate
            {
                using (MehediDownloadAllLinksForm mehediDlg = new MehediDownloadAllLinksForm(shihabBatch, _mehediConfig.ShihabDarkMode))
                {
                    IWin32Window shihabOwner = GetShihabDialogOwnerOrNull();
                    DialogResult mehediRes = shihabOwner != null ? mehediDlg.ShowDialog(shihabOwner) : mehediDlg.ShowDialog();
                    if (mehediRes == DialogResult.OK)
                    {
                        for (int shihabI = 0; shihabI < mehediDlg.ShihabChosenLinks.Count; shihabI++)
                        {
                            MehediBatchLinkEntryDto mehediLink = mehediDlg.ShihabChosenLinks[shihabI];
                            EnqueueSimpleShihabUrl(
                                mehediLink.ShihabUrl,
                                shihabBatch.ShihabPageUrl ?? "",
                                shihabBatch.ShihabCookies ?? "",
                                shihabBatch.MehediUserAgent ?? "",
                                mehediLink.MehediText ?? "",
                                mehediDlg.MehediStartNow);
                        }
                    }
                }
            });
        }

        private void EnqueueSimpleShihabUrl(
            string mehediUrl,
            string shihabReferer,
            string mehediCookies,
            string shihabUserAgent,
            string mehediDesc,
            bool shihabStartNow)
        {
            ShihabDownloadItem mehediItem = new ShihabDownloadItem();
            mehediItem.MehediUrl = mehediUrl;
            mehediItem.MehediReferer = shihabReferer;
            mehediItem.ShihabCookies = mehediCookies;
            if (!string.IsNullOrWhiteSpace(shihabUserAgent)) mehediItem.MehediUserAgent = shihabUserAgent;
            mehediItem.ShihabDescription = mehediDesc;
            mehediItem.ShihabFileName = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(mehediUrl);
            ShihabCategoryRule shihabCat = _mehediConfig.ResolveMehediCategoryForFile(mehediItem.ShihabFileName);
            mehediItem.ShihabCategoryName = shihabCat.ShihabName;
            Directory.CreateDirectory(shihabCat.ShihabDefaultFolder);
            mehediItem.MehediSaveDirectory = shihabCat.ShihabDefaultFolder;
            mehediItem.ShihabFileName = ShihabFormatHelpers.EnsureShihabUniqueFilePath(shihabCat.ShihabDefaultFolder, mehediItem.ShihabFileName);
            mehediItem.MehediMaxConnections = _mehediConfig.ShihabDefaultMaxConnections;
            if (!shihabStartNow)
            {
                mehediItem.MehediQueueName = "Main Download Queue";
                mehediItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued");
            }
            _shihabEngine.AddShihabDownloadItem(mehediItem, shihabStartNow);
        }

        private void HandleShihabItemCompleted(ShihabDownloadItem mehediItem)
        {
            SafeShihabUiInvoke(delegate
            {
                RefreshMehediDownloadsGrid();
                PlayShihabConfiguredSound(_mehediConfig.MehediPlaySoundComplete, _mehediConfig.ShihabSoundCompleteWav, false);
                if (mehediItem.ShihabOpenWhenComplete)
                {
                    try
                    {
                        string shihabTarget = mehediItem.GetShihabFullTargetPath();
                        if (File.Exists(shihabTarget)) Process.Start(shihabTarget);
                    }
                    catch { }
                }
                if (mehediItem.MehediExitAppOnComplete)
                {
                    ExitShihabApplicationCleanly();
                    return;
                }
                if (mehediItem.ShihabShowCompleteDialog && _mehediConfig.ShihabShowDownloadCompleteDialog)
                {
                    MehediDownloadCompleteForm shihabDoneForm = new MehediDownloadCompleteForm(mehediItem, _mehediConfig);
                    IWin32Window mehediOwner = GetShihabDialogOwnerOrNull();
                    if (mehediOwner != null) shihabDoneForm.Show(mehediOwner);
                    else shihabDoneForm.Show();
                }
            });
        }

        private void HandleMehediItemFailed(ShihabDownloadItem shihabFailedItem)
        {
            SafeShihabUiInvoke(delegate
            {
                PlayShihabConfiguredSound(_mehediConfig.MehediPlaySoundError, _mehediConfig.ShihabSoundErrorWav, true);
            });
        }

        private void PromptAndAddNewShihabUrl(string mehediPrefillUrl)
        {
            if (!string.IsNullOrWhiteSpace(mehediPrefillUrl) &&
                (mehediPrefillUrl.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase) ||
                 mehediPrefillUrl.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase)))
            {
                OpenShihabTorrentDialog(mehediPrefillUrl);
                return;
            }
            using (MehediAddUrlAuthDialog shihabAddDlg = new MehediAddUrlAuthDialog(_mehediConfig.ShihabDarkMode, mehediPrefillUrl))
            {
                if (shihabAddDlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (shihabAddDlg.ShihabEnteredUrl.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase) ||
                        shihabAddDlg.ShihabEnteredUrl.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                    {
                        OpenShihabTorrentDialog(shihabAddDlg.ShihabEnteredUrl);
                        return;
                    }
                    ShihabDownloadItem mehediDraft = new ShihabDownloadItem
                    {
                        MehediUrl = shihabAddDlg.ShihabEnteredUrl,
                        MehediReferer = shihabAddDlg.MehediEnteredReferer,
                        ShihabAuthUser = shihabAddDlg.MehediEnteredUser,
                        MehediAuthPassword = shihabAddDlg.ShihabEnteredPassword,
                        ShihabFileName = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(shihabAddDlg.ShihabEnteredUrl),
                        MehediMaxConnections = _mehediConfig.ShihabDefaultMaxConnections
                    };
                    using (MehediDownloadFileInfoForm shihabInfo = new MehediDownloadFileInfoForm(mehediDraft, _mehediConfig, _shihabEngine))
                    {
                        if (shihabInfo.ShowDialog(this) == DialogResult.OK)
                        {
                            _shihabEngine.AddShihabDownloadItem(mehediDraft, shihabInfo.ShihabStartImmediatelySelected);
                            if (shihabInfo.ShihabStartImmediatelySelected && _mehediConfig.MehediShowDownloadProgressDialog)
                            {
                                ShowProgressWindowForShihabItem(mehediDraft);
                            }
                        }
                    }
                }
            }
        }

        private void ShowProgressWindowForShihabItem(ShihabDownloadItem mehediItem)
        {
            ShihabDownloadProgressForm shihabExisting;
            if (_mehediOpenProgressForms.TryGetValue(mehediItem.ShihabId, out shihabExisting) && !shihabExisting.IsDisposed)
            {
                shihabExisting.BringToFront();
                return;
            }
            ShihabDownloadProgressForm mehediProg = new ShihabDownloadProgressForm(mehediItem, _shihabEngine, _mehediConfig);
            _mehediOpenProgressForms[mehediItem.ShihabId] = mehediProg;
            mehediProg.FormClosed += delegate { _mehediOpenProgressForms.Remove(mehediItem.ShihabId); };
            IWin32Window shihabOwner = GetShihabDialogOwnerOrNull();
            if (shihabOwner != null) mehediProg.Show(shihabOwner);
            else mehediProg.Show();
            try { mehediProg.Activate(); mehediProg.BringToFront(); } catch { }
        }

        private List<ShihabDownloadItem> GetSelectedShihabItems()
        {
            List<ShihabDownloadItem> mehediList = new List<ShihabDownloadItem>();
            foreach (ListViewItem shihabRow in _shihabDownloadsListView.SelectedItems)
            {
                if (shihabRow.Tag is ShihabDownloadItem)
                {
                    mehediList.Add((ShihabDownloadItem)shihabRow.Tag);
                }
            }
            return mehediList;
        }

        private void ResumeSelectedShihabDownloads()
        {
            List<ShihabDownloadItem> mehediSel = GetSelectedShihabItems();
            for (int shihabI = 0; shihabI < mehediSel.Count; shihabI++)
            {
                if (mehediSel[shihabI].GetShihabStatusEnum() != ShihabDownloadStatus.ShihabCompleted)
                {
                    _shihabEngine.StartOrResumeMehediDownload(mehediSel[shihabI]);
                }
            }
        }

        private void PauseSelectedMehediDownloads()
        {
            List<ShihabDownloadItem> shihabSel = GetSelectedShihabItems();
            for (int mehediI = 0; mehediI < shihabSel.Count; mehediI++)
            {
                _shihabEngine.PauseShihabDownload(shihabSel[mehediI]);
            }
        }

        private void DeleteSelectedShihabDownloads(bool mehediAlsoDeleteDiskFile)
        {
            List<ShihabDownloadItem> shihabSel = GetSelectedShihabItems();
            if (shihabSel.Count == 0) return;
            for (int mehediI = 0; mehediI < shihabSel.Count; mehediI++)
            {
                _shihabEngine.RemoveShihabDownload(shihabSel[mehediI], mehediAlsoDeleteDiskFile);
            }
            RefreshMehediDownloadsGrid();
        }

        private void DeleteAllCompletedMehediDownloads()
        {
            List<ShihabDownloadItem> shihabAll = _shihabEngine.GetShihabDownloadsSnapshot();
            for (int mehediI = 0; mehediI < shihabAll.Count; mehediI++)
            {
                if (shihabAll[mehediI].GetShihabStatusEnum() == ShihabDownloadStatus.ShihabCompleted)
                {
                    _shihabEngine.RemoveShihabDownload(shihabAll[mehediI], false);
                }
            }
            RefreshMehediDownloadsGrid();
        }

        private void RedownloadSelectedShihabItem()
        {
            List<ShihabDownloadItem> mehediSel = GetSelectedShihabItems();
            if (mehediSel.Count == 0) return;
            ShihabDownloadItem shihabItem = mehediSel[0];
            _shihabEngine.PauseShihabDownload(shihabItem);
            shihabItem.MehediSegments.Clear();
            shihabItem.MehediDownloadedBytes = 0;
            shihabItem.MehediSha256Hex = "";
            _shihabEngine.StartOrResumeMehediDownload(shihabItem);
            if (_mehediConfig.MehediShowDownloadProgressDialog) ShowProgressWindowForShihabItem(shihabItem);
        }

        private void RefreshSelectedMehediAddress()
        {
            List<ShihabDownloadItem> shihabSel = GetSelectedShihabItems();
            if (shihabSel.Count == 0) return;
            ShihabDownloadItem mehediItem = shihabSel[0];
            using (MehediAddUrlAuthDialog shihabRefreshDlg = new MehediAddUrlAuthDialog(_mehediConfig.ShihabDarkMode, mehediItem.MehediUrl))
            {
                shihabRefreshDlg.Text = "Refresh Expired Download Address — " + mehediItem.ShihabFileName;
                if (shihabRefreshDlg.ShowDialog(this) == DialogResult.OK)
                {
                    mehediItem.MehediUrl = shihabRefreshDlg.ShihabEnteredUrl;
                    if (!string.IsNullOrEmpty(shihabRefreshDlg.MehediEnteredReferer))
                    {
                        mehediItem.MehediReferer = shihabRefreshDlg.MehediEnteredReferer;
                    }
                    _shihabEngine.PersistShihabState();
                    _shihabEngine.StartOrResumeMehediDownload(mehediItem);
                }
            }
        }

        private void AssignSelectedShihabToQueue(string mehediQueueName)
        {
            List<ShihabDownloadItem> shihabSel = GetSelectedShihabItems();
            for (int mehediI = 0; mehediI < shihabSel.Count; mehediI++)
            {
                shihabSel[mehediI].MehediQueueName = mehediQueueName;
            }
            _shihabEngine.PersistShihabState();
            RefreshMehediDownloadsGrid();
        }

        private void OpenSelectedMehediProgressWindow()
        {
            List<ShihabDownloadItem> shihabSel = GetSelectedShihabItems();
            if (shihabSel.Count > 0) ShowProgressWindowForShihabItem(shihabSel[0]);
        }

        private void OpenSelectedShihabCompletedFile(bool mehediOpenWith)
        {
            List<ShihabDownloadItem> shihabSel = GetSelectedShihabItems();
            if (shihabSel.Count == 0) return;
            string mehediPath = shihabSel[0].GetShihabFullTargetPath();
            if (!File.Exists(mehediPath)) return;
            try
            {
                if (mehediOpenWith) Process.Start("rundll32.exe", "shell32.dll,OpenAs_RunDLL " + mehediPath);
                else Process.Start(mehediPath);
            }
            catch { }
        }

        private void OpenSelectedMehediContainingFolder()
        {
            List<ShihabDownloadItem> shihabSel = GetSelectedShihabItems();
            if (shihabSel.Count == 0) return;
            string mehediPath = shihabSel[0].GetShihabFullTargetPath();
            try
            {
                if (File.Exists(mehediPath)) Process.Start("explorer.exe", "/select,\"" + mehediPath + "\"");
                else if (Directory.Exists(shihabSel[0].MehediSaveDirectory)) Process.Start("explorer.exe", "\"" + shihabSel[0].MehediSaveDirectory + "\"");
            }
            catch { }
        }

        private void OpenSelectedShihabProperties()
        {
            List<ShihabDownloadItem> mehediSel = GetSelectedShihabItems();
            if (mehediSel.Count == 0) return;
            using (ShihabFilePropertiesForm shihabProp = new ShihabFilePropertiesForm(mehediSel[0], _mehediConfig.ShihabDarkMode))
            {
                if (shihabProp.ShowDialog(this) == DialogResult.OK)
                {
                    _shihabEngine.PersistShihabState();
                    RefreshMehediDownloadsGrid();
                }
            }
        }

        private void HandleDoubleClickOnShihabRow()
        {
            List<ShihabDownloadItem> mehediSel = GetSelectedShihabItems();
            if (mehediSel.Count == 0) return;
            if (mehediSel[0].GetShihabStatusEnum() == ShihabDownloadStatus.ShihabCompleted)
            {
                OpenSelectedShihabCompletedFile(false);
            }
            else
            {
                ShowProgressWindowForShihabItem(mehediSel[0]);
            }
        }

        private void OpenShihabOptionsDialog()
        {
            using (ShihabOptionsConfigForm mehediOpts = new ShihabOptionsConfigForm(_mehediConfig, _shihabStore, _mehediBridge, ExitShihabApplicationCleanly))
            {
                if (mehediOpts.ShowDialog(this) == DialogResult.OK)
                {
                    if (mehediOpts.ShihabBridgeWasRestarted)
                    {
                        _mehediBridgeListening = mehediOpts.MehediBridgeRestartSucceeded;
                    }
                    ApplyMehediTheme();
                    PopulateShihabCategoryTree();
                    RefreshMehediDownloadsGrid();
                    UpdateShihabStatusBadges();
                }
            }
        }

        private void OpenMehediSchedulerDialog()
        {
            using (MehediSchedulerQueuesForm shihabSched = new MehediSchedulerQueuesForm(_mehediConfig, _shihabEngine, _shihabStore))
            {
                shihabSched.ShowDialog(this);
                PopulateShihabCategoryTree();
                RefreshMehediDownloadsGrid();
            }
        }

        private void OpenShihabBatchWildcardDialog()
        {
            using (ShihabBatchDownloadForm mehediBatch = new ShihabBatchDownloadForm(_mehediConfig.ShihabDarkMode))
            {
                if (mehediBatch.ShowDialog(this) == DialogResult.OK)
                {
                    for (int shihabI = 0; shihabI < mehediBatch.ShihabGeneratedUrls.Count; shihabI++)
                    {
                        EnqueueSimpleShihabUrl(mehediBatch.ShihabGeneratedUrls[shihabI], "", "", "", "Batch wildcard", mehediBatch.MehediStartImmediately);
                    }
                }
            }
        }

        private void ImportShihabClipboardUrlsBatch()
        {
            try
            {
                if (!Clipboard.ContainsText())
                {
                    MessageBox.Show(this, "Cannot find any web address in the clipboard!", "MHS Download Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                string[] mehediTokens = Clipboard.GetText().Split(new char[] { '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                ShihabBatchCaptureDto shihabDto = new ShihabBatchCaptureDto { ShihabLinks = new List<MehediBatchLinkEntryDto>() };
                for (int mehediI = 0; mehediI < mehediTokens.Length; mehediI++)
                {
                    if (mehediTokens[mehediI].StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        mehediTokens[mehediI].StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        shihabDto.ShihabLinks.Add(new MehediBatchLinkEntryDto { ShihabUrl = mehediTokens[mehediI], MehediText = "Clipboard URL" });
                    }
                }
                if (shihabDto.ShihabLinks.Count == 0)
                {
                    MessageBox.Show(this, "Cannot find any http:// or https:// web address in clipboard!", "MHS Download Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                HandleBrowserMehediBatchCapture(shihabDto);
            }
            catch { }
        }

        private void OpenShihabSiteGrabberDialog()
        {
            using (ShihabSiteGrabberForm mehediGrab = new ShihabSiteGrabberForm(_mehediConfig.ShihabDarkMode))
            {
                if (mehediGrab.ShowDialog(this) == DialogResult.OK)
                {
                    for (int shihabI = 0; shihabI < mehediGrab.ShihabSelectedGrabbedUrls.Count; shihabI++)
                    {
                        EnqueueSimpleShihabUrl(mehediGrab.ShihabSelectedGrabbedUrls[shihabI], "", "", "", "Site Grabber", mehediGrab.MehediStartImmediately);
                    }
                }
            }
        }

        private void OpenShihabTorrentDialog(string initialMagnetOrPath)
        {
            using (ShihabTorrentAddDialog dlg = new ShihabTorrentAddDialog(_mehediConfig, initialMagnetOrPath))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ConfiguredTorrentItem != null)
                {
                    _shihabEngine.AddShihabDownloadItem(dlg.ConfiguredTorrentItem, dlg.StartImmediately);
                    if (dlg.StartImmediately && _mehediConfig.MehediShowDownloadProgressDialog)
                    {
                        ShowProgressWindowForShihabItem(dlg.ConfiguredTorrentItem);
                    }
                }
            }
        }

        private void OpenShihabMediaAndGalleryDialog()
        {
            using (ShihabMediaAndGalleryDialog dlg = new ShihabMediaAndGalleryDialog(_mehediConfig, _shihabStore))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (dlg.ConfiguredMediaItems != null && dlg.ConfiguredMediaItems.Count > 0)
                    {
                        for (int i = 0; i < dlg.ConfiguredMediaItems.Count; i++)
                        {
                            ShihabDownloadItem item = dlg.ConfiguredMediaItems[i];
                            if (item == null) continue;
                            if (string.IsNullOrWhiteSpace(item.MehediSaveDirectory))
                            {
                                ShihabCategoryRule cat = _mehediConfig.ResolveMehediCategoryForFile(item.ShihabFileName);
                                item.ShihabCategoryName = cat.ShihabName;
                                item.MehediSaveDirectory = cat.ShihabDefaultFolder;
                            }
                            Directory.CreateDirectory(item.MehediSaveDirectory);
                            item.ShihabFileName = ShihabFormatHelpers.EnsureShihabUniqueFilePath(item.MehediSaveDirectory, item.ShihabFileName);
                            _shihabEngine.AddShihabDownloadItem(item, true);
                        }
                    }
                    else if (dlg.DiscoveredDownloadUrls.Count > 0)
                    {
                        for (int i = 0; i < dlg.DiscoveredDownloadUrls.Count; i++)
                        {
                            EnqueueSimpleShihabUrl(dlg.DiscoveredDownloadUrls[i], "", "", "", "Media / Gallery", true);
                        }
                    }
                }
            }
        }

        private void OpenShihabRssFeedDialog()
        {
            using (ShihabRssFeedDialog dlg = new ShihabRssFeedDialog(_mehediConfig, _shihabStore))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedEnclosureUrls.Count > 0)
                {
                    for (int i = 0; i < dlg.SelectedEnclosureUrls.Count; i++)
                    {
                        EnqueueSimpleShihabUrl(dlg.SelectedEnclosureUrls[i], "", "", "", "RSS Enclosure", true);
                    }
                }
            }
        }

        private void OpenShihabRemotePairingDialog()
        {
            using (ShihabRemotePairingDialog dlg = new ShihabRemotePairingDialog(_mehediConfig, _shihabStore))
            {
                dlg.ShowDialog(this);
                if (dlg.RemoteConfigChanged && _shihabRemoteServer != null)
                {
                    if (_mehediConfig.ShihabRemoteWebEnabled) _shihabRemoteServer.Start();
                    else _shihabRemoteServer.Stop();
                }
            }
        }

        private void OpenShihabChecksumAndExtractDialog()
        {
            using (ShihabChecksumAndExtractDialog dlg = new ShihabChecksumAndExtractDialog(_mehediConfig.ShihabDarkMode))
            {
                dlg.ShowDialog(this);
            }
        }

        private void ToggleMehediFloatingDropTarget()
        {
            if (_shihabDropTargetForm == null || _shihabDropTargetForm.IsDisposed)
            {
                _shihabDropTargetForm = new MehediFloatingDropTargetForm(
                    delegate(string shihabDropped)
                    {
                        HandleDroppedTextOrFilePath(shihabDropped);
                    },
                    _mehediConfig.ShihabDropTargetX,
                    _mehediConfig.ShihabDropTargetY,
                    delegate(int newX, int newY)
                    {
                        _mehediConfig.ShihabDropTargetX = newX;
                        _mehediConfig.ShihabDropTargetY = newY;
                        _shihabStore.SaveMehediConfig(_mehediConfig);
                    });
                _shihabDropTargetForm.Show();
                _mehediConfig.MehediShowFloatingDropTarget = true;
            }
            else
            {
                _shihabDropTargetForm.Visible = !_shihabDropTargetForm.Visible;
                _mehediConfig.MehediShowFloatingDropTarget = _shihabDropTargetForm.Visible;
            }
            _shihabStore.SaveMehediConfig(_mehediConfig);
        }

        private void ExportShihabList(bool mehediEf2Format)
        {
            using (SaveFileDialog shihabSfd = new SaveFileDialog())
            {
                shihabSfd.Filter = mehediEf2Format ? "IDM Export Files (*.ef2)|*.ef2" : "Text Files (*.txt)|*.txt";
                shihabSfd.FileName = mehediEf2Format ? "shihab_downloads.ef2" : "shihab_downloads.txt";
                if (shihabSfd.ShowDialog(this) == DialogResult.OK)
                {
                    List<ShihabDownloadItem> mehediItems = _shihabEngine.GetShihabDownloadsSnapshot();
                    if (mehediEf2Format)
                    {
                        ShihabStateStore.ExportToShihabEf2File(shihabSfd.FileName, mehediItems);
                    }
                    else
                    {
                        List<string> shihabLines = new List<string>();
                        for (int mehediI = 0; mehediI < mehediItems.Count; mehediI++) shihabLines.Add(mehediItems[mehediI].MehediUrl);
                        File.WriteAllLines(shihabSfd.FileName, shihabLines.ToArray());
                    }
                }
            }
        }

        private void ImportMehediList()
        {
            using (OpenFileDialog shihabOfd = new OpenFileDialog())
            {
                shihabOfd.Filter = "IDM Export & Text Files (*.ef2;*.txt)|*.ef2;*.txt|All Files (*.*)|*.*";
                if (shihabOfd.ShowDialog(this) == DialogResult.OK)
                {
                    List<ShihabDownloadItem> mehediImported = ShihabStateStore.ImportFromMehediEf2OrText(shihabOfd.FileName);
                    for (int shihabI = 0; shihabI < mehediImported.Count; shihabI++)
                    {
                        ShihabDownloadItem mehediIt = mehediImported[shihabI];
                        ShihabCategoryRule shihabCat = _mehediConfig.ResolveMehediCategoryForFile(mehediIt.ShihabFileName);
                        mehediIt.ShihabCategoryName = shihabCat.ShihabName;
                        mehediIt.MehediSaveDirectory = shihabCat.ShihabDefaultFolder;
                        mehediIt.MehediQueueName = "Main Download Queue";
                        mehediIt.SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued");
                        _shihabEngine.AddShihabDownloadItem(mehediIt, false);
                    }
                }
            }
        }

        private static string ResolveShihabExtensionDirectoryPath()
        {
            string shihabInstalledExt = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "extension"));
            if (Directory.Exists(shihabInstalledExt))
            {
                return shihabInstalledExt;
            }
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\extension"));
        }

        private void OpenShihabInstalledExtensionFolder()
        {
            string mehediExtFolder = ResolveShihabExtensionDirectoryPath();
            try
            {
                Clipboard.SetText(mehediExtFolder);
            }
            catch { }
            try
            {
                if (Directory.Exists(mehediExtFolder))
                {
                    Process.Start("explorer.exe", "\"" + mehediExtFolder + "\"");
                }
                else
                {
                    ShowShihabExtensionInstructions();
                }
            }
            catch { }
        }

        private void ShowShihabExtensionInstructions()
        {
            string mehediExtFolder = ResolveShihabExtensionDirectoryPath();
            try { Clipboard.SetText(mehediExtFolder); } catch { }
            DialogResult shihabOpenNow = MessageBox.Show(
                this,
                "MHS Download Manager Integration Module (Manifest V3 — Chrome / Brave / Edge / Opera):\n\n" +
                "1. Open chrome://extensions or brave://extensions (or edge://extensions)\n" +
                "2. Enable 'Developer mode' in the top-right corner\n" +
                "3. Click 'Load unpacked' and select:\n   " + mehediExtFolder + "\n   (Path copied to clipboard!)\n\n" +
                "Would you like to open the extension folder in File Explorer now?",
                ShihabThemePalette.ShihabMainHeaderTitle + " — Browser Extension",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (shihabOpenNow == DialogResult.Yes && Directory.Exists(mehediExtFolder))
            {
                try { Process.Start("explorer.exe", "\"" + mehediExtFolder + "\""); } catch { }
            }
        }

        private void ShowMehediCliHelp()
        {
            MessageBox.Show(
                this,
                "MHS Download Manager Command-Line, Auto-Updater & MCP Switches:\n\n" +
                "IDM-Compatible Switches:\n" +
                "  mdm.exe /d <URL> [/p Path] [/f FileName] [/n] [/q] [/a] [/s]\n\n" +
                "Subcommands & Automation:\n" +
                "  mdm.exe --cli list | add <url> | pause <id> | resume <id> | cancel <id>\n" +
                "  mdm.exe --cli check-update [--json]     Check GitHub Releases for updates\n" +
                "  mdm.exe --cli update [--verify-only]    Verify SHA256SUMS.txt & install update\n" +
                "  mdm.exe --cli pair                      Print LAN Web UI URL, PIN & QR code\n" +
                "  mdm.exe --mcp                           Start JSON-RPC 2.0 MCP server (15 tools)\n" +
                "  mdm.exe --headless                      Run daemon + browser bridge without GUI",
                ShihabThemePalette.ShihabMainHeaderTitle + " — CLI & MCP Help",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ShowShihabAboutDialog()
        {
            using (ShihabAboutDeveloperDialog mehediAboutDlg = new ShihabAboutDeveloperDialog(_mehediConfig.ShihabDarkMode))
            {
                mehediAboutDlg.ShowDialog(this);
            }
        }

        private void SafeShihabUiInvoke(Action mehediAction)
        {
            if (IsDisposed) return;
            try
            {
                if (InvokeRequired) BeginInvoke(mehediAction);
                else mehediAction();
            }
            catch { }
        }

        private void HandleMehediFormClosing(object shihabSender, FormClosingEventArgs mehediArgs)
        {
            if (!_shihabExitingExplicitly && mehediArgs.CloseReason == CloseReason.UserClosing)
            {
                if (_mehediConfig.ShihabCloseToTray)
                {
                    mehediArgs.Cancel = true;
                    Hide();
                    ShowInTaskbar = false;
                    ShihabOsAndDiskSafety.TrimWorkingSetMemory();
                    return;
                }
                _shihabExitingExplicitly = true;
            }
            CleanupShihabResources();
        }

        public void ExitShihabApplicationCleanly()
        {
            _shihabExitingExplicitly = true;
            CleanupShihabResources();
            Close();
            Application.Exit();
        }

        private void CleanupShihabResources()
        {
            try { if (_mehediUiTickTimer != null) _mehediUiTickTimer.Stop(); } catch { }
            try { if (_shihabPeriodicUpdateTimer != null) _shihabPeriodicUpdateTimer.Stop(); } catch { }
            try { if (_shihabRssPollTimer != null) _shihabRssPollTimer.Stop(); } catch { }
            try { _shihabEngine.StopAllMehediDownloads(); } catch { }
            try { _shihabEngine.PersistShihabState(); } catch { }
            try { _shihabStore.SaveMehediConfig(_mehediConfig); } catch { }
            try { _mehediBridge.Dispose(); } catch { }
            try { if (_shihabRemoteServer != null) _shihabRemoteServer.Dispose(); } catch { }
            try { ShihabLocalNetworkRelay.Stop(); } catch { }
            try
            {
                if (_shihabDropTargetForm != null && !_shihabDropTargetForm.IsDisposed) _shihabDropTargetForm.Close();
            }
            catch { }
            try
            {
                if (_mehediTrayIcon != null)
                {
                    _mehediTrayIcon.Visible = false;
                    _mehediTrayIcon.Dispose();
                }
            }
            catch { }
            try { ShihabCrashMonitor.MarkCleanShutdown(); } catch { }
        }
    }
}