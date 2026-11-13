using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ShihabMehediIDM
{
    public sealed class ShihabModernListView : ListView
    {
        public bool MehediDarkMode { get; set; }
        public bool ShihabShowFileTypeBadges { get; set; }
        public int MehediSortedColumnIndex { get; set; }
        public bool ShihabSortAscending { get; set; }

        private bool _shihabSpaciousRows;
        private bool _mehediTransferGridMode;
        private int _mehediHoveredRowIndex = -1;
        private readonly ImageList _shihabRowHeightScaler;

        public bool ShihabSpaciousRows
        {
            get { return _shihabSpaciousRows; }
            set
            {
                _shihabSpaciousRows = value;
                UpdateMehediRowScalerHeight();
            }
        }

        public bool ShihabTransferGridMode
        {
            get { return _mehediTransferGridMode; }
            set
            {
                _mehediTransferGridMode = value;
                UpdateMehediRowScalerHeight();
            }
        }

        private void UpdateMehediRowScalerHeight()
        {
            if (_shihabRowHeightScaler != null)
            {
                int shihabRowH = _mehediTransferGridMode ? 34 : (_shihabSpaciousRows ? 38 : 23);
                if (_shihabRowHeightScaler.ImageSize.Height != shihabRowH)
                {
                    _shihabRowHeightScaler.ImageSize = new Size(1, shihabRowH);
                }
            }
        }

        public ShihabModernListView()
        {
            MehediDarkMode = true;
            ShihabShowFileTypeBadges = true;
            MehediSortedColumnIndex = -1;
            ShihabSortAscending = false;
            DoubleBuffered = true;
            OwnerDraw = true;
            FullRowSelect = true;
            View = View.Details;
            BorderStyle = BorderStyle.None;
            HideSelection = false;
            _shihabRowHeightScaler = new ImageList { ImageSize = new Size(1, 23), ColorDepth = ColorDepth.Depth32Bit };
            SmallImageList = _shihabRowHeightScaler;
        }

        protected override void Dispose(bool shihabDisposing)
        {
            if (shihabDisposing && _shihabRowHeightScaler != null)
            {
                _shihabRowHeightScaler.Dispose();
            }
            base.Dispose(shihabDisposing);
        }

        protected override void OnMouseMove(MouseEventArgs mehediE)
        {
            base.OnMouseMove(mehediE);
            ListViewItem shihabHit = GetItemAt(mehediE.X, mehediE.Y);
            int mehediNextIdx = shihabHit != null ? shihabHit.Index : -1;
            if (mehediNextIdx != _mehediHoveredRowIndex)
            {
                int shihabPrev = _mehediHoveredRowIndex;
                _mehediHoveredRowIndex = mehediNextIdx;
                if (shihabPrev >= 0 && shihabPrev < Items.Count)
                {
                    Invalidate(Items[shihabPrev].Bounds);
                }
                if (_mehediHoveredRowIndex >= 0 && _mehediHoveredRowIndex < Items.Count)
                {
                    Invalidate(Items[_mehediHoveredRowIndex].Bounds);
                }
            }
        }

        protected override void OnMouseLeave(EventArgs shihabE)
        {
            base.OnMouseLeave(shihabE);
            if (_mehediHoveredRowIndex >= 0)
            {
                int mehediPrev = _mehediHoveredRowIndex;
                _mehediHoveredRowIndex = -1;
                if (mehediPrev >= 0 && mehediPrev < Items.Count)
                {
                    Invalidate(Items[mehediPrev].Bounds);
                }
            }
        }

        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs shihabHdrArgs)
        {
            Graphics mehediG = shihabHdrArgs.Graphics;
            mehediG.SmoothingMode = SmoothingMode.AntiAlias;
            mehediG.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            Rectangle shihabBounds = shihabHdrArgs.Bounds;

            if (_mehediTransferGridMode)
            {
                Color shihabGridHdrBg = MehediDarkMode ? Color.FromArgb(24, 27, 33) : Color.FromArgb(238, 242, 247);
                Color mehediGridBorder = MehediDarkMode ? Color.FromArgb(36, 40, 50) : Color.FromArgb(206, 214, 224);
                using (SolidBrush shihabHdrFill = new SolidBrush(shihabGridHdrBg))
                using (Pen mehediHdrLine = new Pen(mehediGridBorder))
                {
                    mehediG.FillRectangle(shihabHdrFill, shihabBounds);
                    mehediG.DrawLine(mehediHdrLine, shihabBounds.Left, shihabBounds.Bottom - 1, shihabBounds.Right, shihabBounds.Bottom - 1);
                    mehediG.DrawLine(mehediHdrLine, shihabBounds.Right - 1, shihabBounds.Top, shihabBounds.Right - 1, shihabBounds.Bottom - 1);
                }

                string mehediUpperTitle = (shihabHdrArgs.Header != null && shihabHdrArgs.Header.Text != null)
                    ? shihabHdrArgs.Header.Text.ToUpperInvariant()
                    : "";
                bool shihabSorted = shihabHdrArgs.ColumnIndex == MehediSortedColumnIndex;
                if (shihabSorted && shihabHdrArgs.ColumnIndex != 0)
                {
                    mehediUpperTitle = mehediUpperTitle + (ShihabSortAscending ? " \u2191" : " \u2193");
                }
                Color shihabHdrTextCol = MehediDarkMode
                    ? (shihabSorted ? Color.FromArgb(226, 232, 244) : Color.FromArgb(124, 133, 153))
                    : Color.FromArgb(71, 85, 105);

                Rectangle mehediCellTxtRect = shihabHdrArgs.ColumnIndex == 0
                    ? shihabBounds
                    : new Rectangle(shihabBounds.X + 10, shihabBounds.Y, Math.Max(4, shihabBounds.Width - 14), shihabBounds.Height);
                TextFormatFlags shihabHdrFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                    (shihabHdrArgs.ColumnIndex == 0 ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left);

                TextRenderer.DrawText(mehediG, mehediUpperTitle, ShihabThemePalette.UiBold78Font, mehediCellTxtRect, shihabHdrTextCol, shihabHdrFlags);
                return;
            }

            Color mehediHdrBg = MehediDarkMode ? Color.FromArgb(24, 26, 36) : Color.FromArgb(236, 240, 246);
            Color shihabBottomLine = MehediDarkMode ? Color.FromArgb(38, 42, 58) : Color.FromArgb(204, 212, 222);
            Color mehediTickCol = MehediDarkMode ? Color.FromArgb(44, 49, 68) : Color.FromArgb(208, 214, 224);

            using (SolidBrush shihabBgBrush = new SolidBrush(mehediHdrBg))
            using (Pen mehediBotPen = new Pen(shihabBottomLine))
            using (Pen shihabTickPen = new Pen(mehediTickCol))
            {
                mehediG.FillRectangle(shihabBgBrush, shihabBounds);
                mehediG.DrawLine(mehediBotPen, shihabBounds.Left, shihabBounds.Bottom - 1, shihabBounds.Right, shihabBounds.Bottom - 1);
                mehediG.DrawLine(shihabTickPen, shihabBounds.Right - 1, shihabBounds.Top + 6, shihabBounds.Right - 1, shihabBounds.Bottom - 7);
            }

            string shihabHeaderText = shihabHdrArgs.Header != null ? shihabHdrArgs.Header.Text : "";
            bool mehediIsSortedCol = shihabHdrArgs.ColumnIndex == MehediSortedColumnIndex;
            Color mehediFg = MehediDarkMode
                ? (mehediIsSortedCol ? Color.FromArgb(228, 234, 248) : Color.FromArgb(148, 156, 180))
                : Color.FromArgb(28, 34, 44);

            int shihabHdrPadX = 8;
            if (_shihabSpaciousRows && shihabHdrArgs.ColumnIndex == 0 && !CheckBoxes)
            {
                Rectangle mehediHdrChk = new Rectangle(shihabBounds.X + 8, shihabBounds.Y + (shihabBounds.Height - 13) / 2, 13, 13);
                using (GraphicsPath shihabHdrChkPath = ShihabThemePalette.CreateShihabRoundedRect(mehediHdrChk, 3))
                using (Pen mehediHdrChkPen = new Pen(MehediDarkMode ? Color.FromArgb(64, 70, 92) : Color.FromArgb(148, 163, 184), 1.1f))
                {
                    mehediG.DrawPath(mehediHdrChkPen, shihabHdrChkPath);
                }
                shihabHdrPadX = 28;
            }

            if (_shihabSpaciousRows && shihabBounds.Width > 54 && shihabHdrArgs.ColumnIndex != 1)
            {
                string mehediGlyph = mehediIsSortedCol
                    ? (ShihabSortAscending ? "\u2191 " : "\u2193 ")
                    : "\u21C5 ";
                shihabHeaderText = mehediGlyph + shihabHeaderText;
            }
            else if (mehediIsSortedCol)
            {
                shihabHeaderText += ShihabSortAscending ? " \u2191" : " \u2193";
            }

            Rectangle mehediTextBounds = new Rectangle(shihabBounds.X + shihabHdrPadX, shihabBounds.Y, Math.Max(4, shihabBounds.Width - shihabHdrPadX - 6), shihabBounds.Height);
            TextRenderer.DrawText(
                mehediG,
                shihabHeaderText,
                ShihabThemePalette.UiSemibold86Font,
                mehediTextBounds,
                mehediFg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        protected override void OnDrawItem(DrawListViewItemEventArgs mehediItemArgs)
        {
        }

        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs shihabSubArgs)
        {
            Graphics mehediG = shihabSubArgs.Graphics;
            mehediG.SmoothingMode = SmoothingMode.AntiAlias;
            mehediG.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Rectangle shihabCell = shihabSubArgs.Bounds;
            bool mehediSelected = shihabSubArgs.Item != null && shihabSubArgs.Item.Selected;
            bool shihabHovered = shihabSubArgs.ItemIndex == _mehediHoveredRowIndex && !mehediSelected;
            bool shihabEven = (shihabSubArgs.ItemIndex % 2) == 0;

            if (_mehediTransferGridMode)
            {
                DrawShihabTransferGridCell(mehediG, shihabSubArgs, shihabCell, mehediSelected, shihabHovered);
                return;
            }

            Color mehediCanvasBg = MehediDarkMode
                ? (_shihabSpaciousRows ? Color.FromArgb(18, 19, 25) : (shihabEven ? Color.FromArgb(20, 22, 30) : Color.FromArgb(23, 25, 34)))
                : (shihabEven ? Color.White : Color.FromArgb(248, 250, 252));

            using (SolidBrush mehediBaseFill = new SolidBrush(mehediCanvasBg))
            {
                mehediG.FillRectangle(mehediBaseFill, shihabCell);
            }

            if ((mehediSelected || shihabHovered) && shihabSubArgs.Item != null)
            {
                using (Region shihabSavedClip = mehediG.Clip)
                {
                    mehediG.SetClip(shihabCell);
                    Rectangle mehediRowBounds = shihabSubArgs.Item.Bounds;
                    Rectangle shihabCardBounds = new Rectangle(
                        mehediRowBounds.X + 3,
                        mehediRowBounds.Y + (_shihabSpaciousRows ? 2 : 1),
                        Math.Max(10, mehediRowBounds.Width - 6),
                        Math.Max(10, mehediRowBounds.Height - (_shihabSpaciousRows ? 4 : 2)));
                    Color mehediCardFill = mehediSelected
                        ? (MehediDarkMode ? Color.FromArgb(30, 34, 50) : Color.FromArgb(224, 231, 255))
                        : (MehediDarkMode ? Color.FromArgb(23, 26, 38) : Color.FromArgb(241, 245, 252));
                    Color shihabCardEdge = mehediSelected
                        ? (MehediDarkMode ? Color.FromArgb(56, 63, 92) : Color.FromArgb(129, 140, 248))
                        : (MehediDarkMode ? Color.FromArgb(34, 38, 54) : Color.FromArgb(214, 222, 238));
                    using (GraphicsPath mehediCardPath = ShihabThemePalette.CreateShihabRoundedRect(shihabCardBounds, _shihabSpaciousRows ? 8 : 5))
                    using (SolidBrush shihabCardBr = new SolidBrush(mehediCardFill))
                    using (Pen mehediCardPen = new Pen(shihabCardEdge, 1f))
                    {
                        mehediG.FillPath(shihabCardBr, mehediCardPath);
                        mehediG.DrawPath(mehediCardPen, mehediCardPath);
                    }
                    mehediG.Clip = shihabSavedClip;
                }
            }
            else if (!_shihabSpaciousRows)
            {
                Color shihabSubtleDivider = MehediDarkMode ? Color.FromArgb(30, 33, 45) : Color.FromArgb(226, 232, 240);
                using (Pen shihabDivPen = new Pen(shihabSubtleDivider))
                {
                    mehediG.DrawLine(shihabDivPen, shihabCell.Left, shihabCell.Bottom - 1, shihabCell.Right, shihabCell.Bottom - 1);
                }
            }
            else
            {
                Color shihabHairline = MehediDarkMode ? Color.FromArgb(23, 25, 34) : Color.FromArgb(236, 240, 246);
                using (Pen mehediHairPen = new Pen(shihabHairline))
                {
                    mehediG.DrawLine(mehediHairPen, shihabCell.Left, shihabCell.Bottom - 1, shihabCell.Right, shihabCell.Bottom - 1);
                }
            }

            string mehediText = shihabSubArgs.SubItem != null ? (shihabSubArgs.SubItem.Text ?? "") : "";
            Color shihabTextColor = MehediDarkMode ? Color.FromArgb(228, 233, 245) : Color.FromArgb(22, 28, 36);
            int mehediLeftPad = 8;

            if ((CheckBoxes || _shihabSpaciousRows) && shihabSubArgs.ColumnIndex == 0 && shihabSubArgs.Item != null)
            {
                int shihabBoxSz = _shihabSpaciousRows ? 15 : 13;
                Rectangle mehediChkRect = new Rectangle(
                    shihabCell.X + 8,
                    shihabCell.Y + (shihabCell.Height - shihabBoxSz) / 2,
                    shihabBoxSz,
                    shihabBoxSz);
                bool shihabIsChecked = CheckBoxes ? shihabSubArgs.Item.Checked : mehediSelected;
                Color mehediChkFill = shihabIsChecked
                    ? Color.FromArgb(124, 92, 255)
                    : Color.Transparent;
                Color shihabChkEdge = shihabIsChecked
                    ? Color.FromArgb(139, 92, 246)
                    : (MehediDarkMode ? Color.FromArgb(62, 68, 92) : Color.FromArgb(148, 163, 184));
                using (GraphicsPath shihabChkPath = ShihabThemePalette.CreateShihabRoundedRect(mehediChkRect, 4))
                using (Pen shihabBoxPen = new Pen(shihabChkEdge, 1.3f))
                {
                    if (shihabIsChecked)
                    {
                        using (SolidBrush mehediBoxBr = new SolidBrush(mehediChkFill))
                        {
                            mehediG.FillPath(mehediBoxBr, shihabChkPath);
                        }
                    }
                    mehediG.DrawPath(shihabBoxPen, shihabChkPath);
                }
                if (shihabIsChecked)
                {
                    using (Pen mehediTickPen = new Pen(Color.White, 1.8f))
                    {
                        mehediTickPen.StartCap = LineCap.Round;
                        mehediTickPen.EndCap = LineCap.Round;
                        mehediTickPen.LineJoin = LineJoin.Round;
                        mehediG.DrawLines(mehediTickPen, new Point[]
                        {
                            new Point(mehediChkRect.X + 3, mehediChkRect.Y + shihabBoxSz / 2),
                            new Point(mehediChkRect.X + shihabBoxSz / 2 - 1, mehediChkRect.Bottom - 4),
                            new Point(mehediChkRect.Right - 3, mehediChkRect.Y + 4)
                        });
                    }
                }
                mehediLeftPad = _shihabSpaciousRows ? 30 : 25;
            }

            ShihabDownloadItem shihabDlItem = shihabSubArgs.Item != null ? (shihabSubArgs.Item.Tag as ShihabDownloadItem) : null;

            if (ShihabShowFileTypeBadges && shihabSubArgs.ColumnIndex == 0)
            {
                Rectangle mehediBadgeRect = new Rectangle(shihabCell.X + mehediLeftPad, shihabCell.Y + (shihabCell.Height - 18) / 2, 18, 18);
                ShihabThemePalette.DrawShihabFileTypeBadge(mehediG, mehediBadgeRect, mehediText);
                mehediLeftPad += 24;

                if (_shihabSpaciousRows && shihabDlItem != null)
                {
                    Rectangle shihabNameRect = new Rectangle(
                        shihabCell.X + mehediLeftPad,
                        shihabCell.Y + 4,
                        Math.Max(4, shihabCell.Width - mehediLeftPad - 6),
                        18);
                    Rectangle mehediSubRect = new Rectangle(
                        shihabCell.X + mehediLeftPad,
                        shihabCell.Y + 21,
                        Math.Max(4, shihabCell.Width - mehediLeftPad - 6),
                        15);
                    string shihabSubCat = string.IsNullOrWhiteSpace(shihabDlItem.ShihabCategoryName)
                        ? "General"
                        : shihabDlItem.ShihabCategoryName;
                    Color mehediSubColor = MehediDarkMode ? Color.FromArgb(118, 126, 154) : Color.FromArgb(100, 116, 139);
                    TextRenderer.DrawText(
                        mehediG,
                        mehediText,
                        ShihabThemePalette.UiSemibold9Font,
                        shihabNameRect,
                        shihabTextColor,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    TextRenderer.DrawText(
                        mehediG,
                        shihabSubCat,
                        ShihabThemePalette.UiRegular76Font,
                        mehediSubRect,
                        mehediSubColor,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    return;
                }
            }
            else if (ShihabShowFileTypeBadges && shihabSubArgs.ColumnIndex == 1 && !string.IsNullOrEmpty(mehediText))
            {
                Rectangle shihabQRect = new Rectangle(shihabCell.X + 6, shihabCell.Y + (shihabCell.Height - 14) / 2, 16, 14);
                ShihabThemePalette.DrawMehediMiniQueueFolder(mehediG, shihabQRect);
                if (shihabCell.Width <= 36)
                {
                    return;
                }
                mehediLeftPad = 25;
            }
            else if (shihabDlItem != null && shihabSubArgs.ColumnIndex == 3)
            {
                ShihabDownloadStatus mehediSt = shihabDlItem.GetShihabStatusEnum();
                bool shihabShowBar = (mehediSt == ShihabDownloadStatus.ShihabDownloading || mehediSt == ShihabDownloadStatus.ShihabPaused)
                    && (shihabDlItem.ShihabTotalBytes > 0 || shihabDlItem.MehediDownloadedBytes > 0);

                if (shihabShowBar && _shihabSpaciousRows)
                {
                    double shihabPct = shihabDlItem.ShihabTotalBytes > 0
                        ? Math.Max(0.0, Math.Min(1.0, (double)shihabDlItem.MehediDownloadedBytes / shihabDlItem.ShihabTotalBytes))
                        : 0.35;
                    string mehediStatusHead = shihabDlItem.ShihabTotalBytes > 0
                        ? string.Format("{0:0}% {1}", shihabPct * 100.0, mehediSt == ShihabDownloadStatus.ShihabDownloading ? "Downloading" : "Paused")
                        : mehediText;
                    Color shihabStatusCol = mehediSt == ShihabDownloadStatus.ShihabDownloading
                        ? (MehediDarkMode ? Color.FromArgb(224, 232, 248) : Color.FromArgb(15, 23, 42))
                        : Color.FromArgb(251, 191, 36);

                    Rectangle mehediTopTxtRect = new Rectangle(shihabCell.X + 8, shihabCell.Y + 4, Math.Max(8, shihabCell.Width - 14), 17);
                    TextRenderer.DrawText(
                        mehediG,
                        mehediStatusHead,
                        Font,
                        mehediTopTxtRect,
                        shihabStatusCol,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                    Rectangle mehediBarTrack = new Rectangle(shihabCell.X + 8, shihabCell.Y + 24, Math.Max(16, shihabCell.Width - 18), 5);
                    int shihabFillW = Math.Max(4, (int)Math.Round(mehediBarTrack.Width * shihabPct));
                    using (GraphicsPath mehediTrackPath = ShihabThemePalette.CreateShihabRoundedRect(mehediBarTrack, 2))
                    using (SolidBrush shihabTrackBr = new SolidBrush(MehediDarkMode ? Color.FromArgb(37, 42, 60) : Color.FromArgb(216, 224, 236)))
                    {
                        mehediG.FillPath(shihabTrackBr, mehediTrackPath);
                    }
                    if (shihabPct > 0.005)
                    {
                        Rectangle shihabFillRect = new Rectangle(mehediBarTrack.X, mehediBarTrack.Y, Math.Min(mehediBarTrack.Width, shihabFillW), mehediBarTrack.Height);
                        Color mehediC1 = mehediSt == ShihabDownloadStatus.ShihabDownloading ? Color.FromArgb(56, 189, 248) : Color.FromArgb(245, 158, 11);
                        Color shihabC2 = mehediSt == ShihabDownloadStatus.ShihabDownloading ? Color.FromArgb(139, 92, 246) : Color.FromArgb(251, 146, 60);
                        using (GraphicsPath mehediFillPath = ShihabThemePalette.CreateShihabRoundedRect(shihabFillRect, 2))
                        using (LinearGradientBrush shihabProgBr = new LinearGradientBrush(
                            new Rectangle(mehediBarTrack.X, mehediBarTrack.Y, Math.Max(8, mehediBarTrack.Width), mehediBarTrack.Height),
                            mehediC1,
                            shihabC2,
                            0f))
                        {
                            mehediG.FillPath(shihabProgBr, mehediFillPath);
                        }
                    }
                    return;
                }

                if (mehediSt == ShihabDownloadStatus.ShihabError)
                {
                    shihabTextColor = MehediDarkMode ? Color.FromArgb(253, 164, 175) : Color.FromArgb(185, 28, 28);
                }
                else if (mehediSt == ShihabDownloadStatus.ShihabCompleted)
                {
                    shihabTextColor = MehediDarkMode ? Color.FromArgb(186, 196, 220) : Color.FromArgb(22, 101, 52);
                }
                else if (mehediSt == ShihabDownloadStatus.ShihabPaused)
                {
                    shihabTextColor = Color.FromArgb(251, 191, 36);
                }
                else if (mehediSt == ShihabDownloadStatus.ShihabQueued)
                {
                    shihabTextColor = Color.FromArgb(196, 181, 253);
                }
            }
            else if (_shihabSpaciousRows && shihabSubArgs.ColumnIndex >= 2)
            {
                shihabTextColor = MehediDarkMode ? Color.FromArgb(182, 190, 214) : Color.FromArgb(51, 65, 85);
            }

            Rectangle shihabTxtRect = new Rectangle(
                shihabCell.X + mehediLeftPad,
                shihabCell.Y,
                Math.Max(4, shihabCell.Width - mehediLeftPad - 6),
                shihabCell.Height);

            TextFormatFlags mehediFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Left;
            TextRenderer.DrawText(mehediG, mehediText, Font, shihabTxtRect, shihabTextColor, mehediFlags);
        }

        private void DrawShihabTransferGridCell(
            Graphics mehediG,
            DrawListViewSubItemEventArgs shihabSubArgs,
            Rectangle shihabCell,
            bool mehediSelected,
            bool shihabHovered)
        {
            Color baseBg = ShihabThemePalette.GetMehediWindowBg(MehediDarkMode);
            Color surfBg = ShihabThemePalette.GetShihabSurfaceBg(MehediDarkMode);
            Color accentCol = ShihabThemePalette.GetShihabAccentBlue();
            Color shihabRowFill;
            if (mehediSelected)
            {
                shihabRowFill = MehediDarkMode
                    ? ShihabThemePalette.BlendThemeColor(baseBg, accentCol, 0.22)
                    : Color.FromArgb(219, 234, 254);
            }
            else if (shihabHovered)
            {
                shihabRowFill = MehediDarkMode ? surfBg : Color.FromArgb(241, 245, 249);
            }
            else
            {
                shihabRowFill = baseBg;
            }

            Color borderCol = ShihabThemePalette.GetShihabBorder(MehediDarkMode);
            Color mehediHorizLine = mehediSelected
                ? ShihabThemePalette.BlendThemeColor(borderCol, accentCol, 0.45)
                : borderCol;
            Color shihabVertLine = ShihabThemePalette.BlendThemeColor(baseBg, borderCol, 0.65);

            using (SolidBrush mehediFillBr = new SolidBrush(shihabRowFill))
            using (Pen shihabRowPen = new Pen(mehediHorizLine))
            using (Pen mehediColPen = new Pen(shihabVertLine))
            {
                mehediG.FillRectangle(mehediFillBr, shihabCell);
                mehediG.DrawLine(shihabRowPen, shihabCell.Left, shihabCell.Bottom - 1, shihabCell.Right, shihabCell.Bottom - 1);
                mehediG.DrawLine(mehediColPen, shihabCell.Right - 1, shihabCell.Top, shihabCell.Right - 1, shihabCell.Bottom - 1);
            }

            string mehediCellText = shihabSubArgs.SubItem != null ? (shihabSubArgs.SubItem.Text ?? "") : "";
            ShihabDownloadItem shihabDl = shihabSubArgs.Item != null ? (shihabSubArgs.Item.Tag as ShihabDownloadItem) : null;
            int shihabCol = shihabSubArgs.ColumnIndex;

            if (shihabCol == 0)
            {
                Color mehediIdxCol = ShihabThemePalette.GetMehediMutedText(MehediDarkMode);
                TextRenderer.DrawText(
                    mehediG,
                    mehediCellText,
                    ShihabThemePalette.UiRegular86Font,
                    shihabCell,
                    mehediIdxCol,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                return;
            }

            if (shihabCol == 1)
            {
                Color mehediNameCol = ShihabThemePalette.GetShihabPrimaryText(MehediDarkMode);
                Rectangle shihabNameBounds = new Rectangle(shihabCell.X + 10, shihabCell.Y, Math.Max(4, shihabCell.Width - 16), shihabCell.Height);
                TextRenderer.DrawText(
                    mehediG,
                    mehediCellText,
                    ShihabThemePalette.UiBold9Font,
                    shihabNameBounds,
                    mehediNameCol,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                return;
            }

            if (shihabCol == 3 && shihabDl != null)
            {
                ShihabDownloadStatus mehediSt = shihabDl.GetShihabStatusEnum();
                double shihabPct = shihabDl.GetMehediPercent() / 100.0;
                if (mehediSt == ShihabDownloadStatus.ShihabDownloading && shihabDl.ShihabTotalBytes <= 0 && shihabDl.MehediDownloadedBytes > 0)
                {
                    shihabPct = 0.45;
                }

                Rectangle mehediTrack = new Rectangle(shihabCell.X + 10, shihabCell.Y + shihabCell.Height - 10, Math.Max(14, shihabCell.Width - 20), 4);
                Color shihabTrackCol = MehediDarkMode ? Color.FromArgb(35, 40, 50) : Color.FromArgb(220, 226, 236);
                using (GraphicsPath mehediTrackPath = ShihabThemePalette.CreateShihabRoundedRect(mehediTrack, 2))
                using (SolidBrush shihabTrackBr = new SolidBrush(shihabTrackCol))
                {
                    mehediG.FillPath(shihabTrackBr, mehediTrackPath);
                }

                if (shihabPct > 0.005)
                {
                    int mehediFillW = Math.Max(4, (int)Math.Round(mehediTrack.Width * Math.Min(1.0, shihabPct)));
                    Rectangle shihabBarFillRect = new Rectangle(mehediTrack.X, mehediTrack.Y, Math.Min(mehediTrack.Width, mehediFillW), mehediTrack.Height);
                    Color mehediBarCol = mehediSt == ShihabDownloadStatus.ShihabCompleted
                        ? Color.FromArgb(16, 185, 129)
                        : (mehediSt == ShihabDownloadStatus.ShihabPaused ? Color.FromArgb(245, 158, 11) : Color.FromArgb(13, 116, 231));
                    using (GraphicsPath shihabFillPath = ShihabThemePalette.CreateShihabRoundedRect(shihabBarFillRect, 2))
                    using (SolidBrush mehediProgBr = new SolidBrush(mehediBarCol))
                    {
                        mehediG.FillPath(mehediProgBr, shihabFillPath);
                    }
                }

                Rectangle mehediPctTxtRect = new Rectangle(shihabCell.X + 10, shihabCell.Y + 3, Math.Max(8, shihabCell.Width - 16), 17);
                Color shihabPctCol = MehediDarkMode ? Color.FromArgb(146, 155, 174) : Color.FromArgb(71, 85, 105);
                TextRenderer.DrawText(
                    mehediG,
                    mehediCellText,
                    ShihabThemePalette.UiRegular82Font,
                    mehediPctTxtRect,
                    shihabPctCol,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                return;
            }

            if (shihabCol == 6 && shihabDl != null)
            {
                ShihabDownloadStatus shihabState = shihabDl.GetShihabStatusEnum();
                Color mehediStatusCol;
                if (shihabState == ShihabDownloadStatus.ShihabCompleted)
                {
                    mehediStatusCol = Color.FromArgb(16, 185, 129);
                }
                else if (shihabState == ShihabDownloadStatus.ShihabDownloading || shihabState == ShihabDownloadStatus.ShihabConnecting || shihabState == ShihabDownloadStatus.ShihabRebuilding)
                {
                    mehediStatusCol = Color.FromArgb(59, 130, 246);
                }
                else if (shihabState == ShihabDownloadStatus.ShihabPaused)
                {
                    mehediStatusCol = Color.FromArgb(245, 158, 11);
                }
                else if (shihabState == ShihabDownloadStatus.ShihabQueued)
                {
                    mehediStatusCol = Color.FromArgb(167, 139, 250);
                }
                else if (shihabState == ShihabDownloadStatus.ShihabError)
                {
                    mehediStatusCol = Color.FromArgb(239, 68, 68);
                }
                else
                {
                    mehediStatusCol = MehediDarkMode ? Color.FromArgb(156, 163, 178) : Color.FromArgb(51, 65, 85);
                }

                Rectangle shihabStatRect = new Rectangle(shihabCell.X + 10, shihabCell.Y, Math.Max(4, shihabCell.Width - 14), shihabCell.Height);
                TextRenderer.DrawText(
                    mehediG,
                    mehediCellText,
                    ShihabThemePalette.UiBold88Font,
                    shihabStatRect,
                    mehediStatusCol,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                return;
            }

            Color shihabDefaultCol = MehediDarkMode ? Color.FromArgb(136, 145, 165) : Color.FromArgb(71, 85, 105);
            if (shihabCol == 4 && shihabDl != null && shihabDl.ShihabCurrentSpeedBps > 5.0)
            {
                shihabDefaultCol = Color.FromArgb(56, 189, 248);
            }
            Rectangle mehediGenRect = new Rectangle(shihabCell.X + 10, shihabCell.Y, Math.Max(4, shihabCell.Width - 14), shihabCell.Height);
            TextRenderer.DrawText(
                mehediG,
                mehediCellText,
                ShihabThemePalette.UiRegular86Font,
                mehediGenRect,
                shihabDefaultCol,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}

