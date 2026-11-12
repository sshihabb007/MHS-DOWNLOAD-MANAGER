using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShihabMehediIDM
{
    public sealed class ShihabDarkMenuColorTable : ProfessionalColorTable
    {
        private readonly bool _shihabDark;

        public ShihabDarkMenuColorTable(bool shihabDark)
        {
            _shihabDark = shihabDark;
            UseSystemColors = false;
        }

        public override Color MenuStripGradientBegin { get { return _shihabDark ? Color.FromArgb(18, 19, 25) : Color.FromArgb(240, 243, 248); } }
        public override Color MenuStripGradientEnd { get { return _shihabDark ? Color.FromArgb(18, 19, 25) : Color.FromArgb(240, 243, 248); } }
        public override Color ToolStripDropDownBackground { get { return _shihabDark ? Color.FromArgb(24, 26, 36) : Color.White; } }
        public override Color ImageMarginGradientBegin { get { return _shihabDark ? Color.FromArgb(24, 26, 36) : Color.FromArgb(245, 247, 250); } }
        public override Color ImageMarginGradientMiddle { get { return _shihabDark ? Color.FromArgb(24, 26, 36) : Color.FromArgb(245, 247, 250); } }
        public override Color ImageMarginGradientEnd { get { return _shihabDark ? Color.FromArgb(24, 26, 36) : Color.FromArgb(245, 247, 250); } }
        public override Color MenuBorder { get { return _shihabDark ? Color.FromArgb(44, 48, 68) : Color.FromArgb(195, 203, 214); } }
        public override Color MenuItemBorder { get { return _shihabDark ? Color.FromArgb(92, 98, 138) : Color.FromArgb(124, 92, 255); } }
        public override Color MenuItemSelected { get { return _shihabDark ? Color.FromArgb(35, 39, 56) : Color.FromArgb(228, 232, 252); } }
        public override Color MenuItemSelectedGradientBegin { get { return _shihabDark ? Color.FromArgb(35, 39, 56) : Color.FromArgb(228, 232, 252); } }
        public override Color MenuItemSelectedGradientEnd { get { return _shihabDark ? Color.FromArgb(35, 39, 56) : Color.FromArgb(228, 232, 252); } }
        public override Color MenuItemPressedGradientBegin { get { return _shihabDark ? Color.FromArgb(29, 32, 46) : Color.FromArgb(214, 222, 248); } }
        public override Color MenuItemPressedGradientEnd { get { return _shihabDark ? Color.FromArgb(29, 32, 46) : Color.FromArgb(214, 222, 248); } }
        public override Color SeparatorDark { get { return _shihabDark ? Color.FromArgb(38, 42, 58) : Color.FromArgb(210, 216, 224); } }
        public override Color SeparatorLight { get { return Color.Transparent; } }
    }

    public sealed class MehediDarkToolStripRenderer : ToolStripProfessionalRenderer
    {
        private readonly bool _mehediDark;
        private readonly bool _shihabIsLargeToolbar;

        public MehediDarkToolStripRenderer(bool mehediDark, bool shihabIsLargeToolbar)
            : base(new ShihabDarkMenuColorTable(mehediDark))
        {
            _mehediDark = mehediDark;
            _shihabIsLargeToolbar = shihabIsLargeToolbar;
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs shihabArgs)
        {
            Color mehediFill;
            if (shihabArgs.ToolStrip is ToolStripDropDown)
            {
                mehediFill = _mehediDark ? Color.FromArgb(24, 26, 36) : Color.White;
            }
            else if (_shihabIsLargeToolbar)
            {
                mehediFill = _mehediDark ? Color.FromArgb(18, 19, 25) : Color.FromArgb(242, 244, 250);
            }
            else if (shihabArgs.ToolStrip is StatusStrip)
            {
                mehediFill = _mehediDark ? Color.FromArgb(16, 17, 23) : Color.FromArgb(236, 239, 246);
            }
            else if (shihabArgs.ToolStrip is MenuStrip)
            {
                mehediFill = _mehediDark ? Color.FromArgb(27, 29, 35) : Color.FromArgb(244, 246, 250);
            }
            else
            {
                mehediFill = _mehediDark ? Color.FromArgb(18, 19, 25) : Color.FromArgb(244, 246, 250);
            }
            using (SolidBrush shihabBrush = new SolidBrush(mehediFill))
            {
                shihabArgs.Graphics.FillRectangle(shihabBrush, shihabArgs.AffectedBounds);
            }
            if (shihabArgs.ToolStrip is MenuStrip)
            {
                Color menuBotLine = _mehediDark ? Color.FromArgb(62, 66, 76) : Color.FromArgb(208, 215, 226);
                using (Pen menuBotPen = new Pen(menuBotLine, 1f))
                {
                    shihabArgs.Graphics.DrawLine(menuBotPen, 0, shihabArgs.ToolStrip.Height - 1, shihabArgs.ToolStrip.Width, shihabArgs.ToolStrip.Height - 1);
                }
            }
            else if (_shihabIsLargeToolbar && !(shihabArgs.ToolStrip is ToolStripDropDown))
            {
                Color shihabBotLine = _mehediDark ? Color.FromArgb(30, 33, 45) : Color.FromArgb(212, 218, 228);
                using (Pen shihabBotPen = new Pen(shihabBotLine))
                {
                    shihabArgs.Graphics.DrawLine(shihabBotPen, 0, shihabArgs.ToolStrip.Height - 1, shihabArgs.ToolStrip.Width, shihabArgs.ToolStrip.Height - 1);
                }
            }
            else if (shihabArgs.ToolStrip is StatusStrip)
            {
                Color mehediTopLine = _mehediDark ? Color.FromArgb(30, 33, 45) : Color.FromArgb(212, 218, 228);
                using (Pen mehediTopPen = new Pen(mehediTopLine))
                {
                    shihabArgs.Graphics.DrawLine(mehediTopPen, 0, 0, shihabArgs.ToolStrip.Width, 0);
                }
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs shihabSepArgs)
        {
            if (shihabSepArgs.Vertical)
            {
                Color mehediLine = _mehediDark ? Color.FromArgb(34, 38, 52) : Color.FromArgb(208, 214, 226);
                int shihabX = shihabSepArgs.Item.Width / 2;
                using (Pen mehediPen = new Pen(mehediLine, 1f))
                {
                    shihabSepArgs.Graphics.DrawLine(mehediPen, shihabX, 11, shihabX, shihabSepArgs.Item.Height - 12);
                }
                return;
            }
            base.OnRenderSeparator(shihabSepArgs);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs mehediArgs)
        {
            if (mehediArgs.ToolStrip is ToolStripDropDown)
            {
                Color shihabBorder = _mehediDark ? Color.FromArgb(44, 48, 68) : Color.FromArgb(195, 203, 214);
                using (Pen mehediPen = new Pen(shihabBorder))
                {
                    Rectangle shihabR = new Rectangle(0, 0, mehediArgs.ToolStrip.Width - 1, mehediArgs.ToolStrip.Height - 1);
                    mehediArgs.Graphics.DrawRectangle(mehediPen, shihabR);
                }
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs shihabTextArgs)
        {
            if (shihabTextArgs.Item != null)
            {
                if (!shihabTextArgs.Item.Enabled)
                {
                    Color disabledCol = _mehediDark ? Color.FromArgb(108, 114, 128) : Color.FromArgb(148, 163, 184);
                    TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis;
                    if (shihabTextArgs.TextFormat != TextFormatFlags.Default)
                    {
                        flags = shihabTextArgs.TextFormat;
                    }
                    TextRenderer.DrawText(
                        shihabTextArgs.Graphics,
                        shihabTextArgs.Text,
                        shihabTextArgs.TextFont,
                        shihabTextArgs.TextRectangle,
                        disabledCol,
                        flags);
                    return;
                }
                if (shihabTextArgs.ToolStrip is MenuStrip)
                {
                    shihabTextArgs.TextColor = _mehediDark ? Color.FromArgb(238, 242, 248) : Color.FromArgb(20, 26, 36);
                    shihabTextArgs.TextFormat &= ~TextFormatFlags.HidePrefix;
                }
                else if (_shihabIsLargeToolbar && !(shihabTextArgs.ToolStrip is ToolStripDropDown))
                {
                    bool mehediPrimaryAdd = Equals(shihabTextArgs.Item.Tag, "add");
                    bool shihabHovered = shihabTextArgs.Item.Selected || shihabTextArgs.Item.Pressed;
                    if (_mehediDark)
                    {
                        shihabTextArgs.TextColor = (mehediPrimaryAdd || shihabHovered)
                            ? Color.FromArgb(240, 242, 250)
                            : Color.FromArgb(182, 188, 208);
                    }
                    else
                    {
                        shihabTextArgs.TextColor = Color.FromArgb(28, 34, 46);
                    }
                }
                else
                {
                    shihabTextArgs.TextColor = _mehediDark ? Color.FromArgb(218, 222, 236) : Color.FromArgb(24, 30, 38);
                }
            }
            base.OnRenderItemText(shihabTextArgs);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs mehediArrowArgs)
        {
            mehediArrowArgs.ArrowColor = _mehediDark ? Color.FromArgb(156, 163, 184) : Color.FromArgb(60, 70, 82);
            base.OnRenderArrow(mehediArrowArgs);
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs shihabBtnArgs)
        {
            bool mehediIsAddPill = Equals(shihabBtnArgs.Item.Tag, "add");
            if (mehediIsAddPill)
            {
                Graphics mehediG = shihabBtnArgs.Graphics;
                mehediG.SmoothingMode = SmoothingMode.AntiAlias;
                mehediG.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                Rectangle mehediPillRect = new Rectangle(2, 6, shihabBtnArgs.Item.Width - 5, shihabBtnArgs.Item.Height - 12);
                bool shihabHot = shihabBtnArgs.Item.Selected || shihabBtnArgs.Item.Pressed;
                Color mehediCardFill = _mehediDark
                    ? (shihabHot ? Color.FromArgb(31, 35, 50) : Color.FromArgb(24, 26, 37))
                    : (shihabHot ? Color.FromArgb(228, 232, 252) : Color.White);
                Color shihabCardEdge = _mehediDark
                    ? (shihabHot ? Color.FromArgb(124, 92, 255) : Color.FromArgb(42, 47, 66))
                    : Color.FromArgb(180, 188, 212);
                using (GraphicsPath mehediPath = ShihabThemePalette.CreateShihabRoundedRect(mehediPillRect, 14))
                using (SolidBrush shihabFillBr = new SolidBrush(mehediCardFill))
                using (Pen mehediEdgePen = new Pen(shihabCardEdge, 1f))
                {
                    mehediG.FillPath(shihabFillBr, mehediPath);
                    mehediG.DrawPath(mehediEdgePen, mehediPath);
                }

                if (shihabBtnArgs.Item.DisplayStyle == ToolStripItemDisplayStyle.None)
                {
                    int shihabCy = mehediPillRect.Y + mehediPillRect.Height / 2;
                    Color mehediLinkCol = _mehediDark ? Color.FromArgb(204, 212, 235) : Color.FromArgb(51, 65, 85);
                    using (Pen shihabLinkPen = new Pen(mehediLinkCol, 1.6f))
                    {
                        shihabLinkPen.StartCap = LineCap.Round;
                        shihabLinkPen.EndCap = LineCap.Round;
                        mehediG.DrawArc(shihabLinkPen, mehediPillRect.X + 10, shihabCy - 5, 8, 10, 115, 210);
                        mehediG.DrawArc(shihabLinkPen, mehediPillRect.X + 15, shihabCy - 5, 8, 10, 295, 210);
                        mehediG.DrawLine(shihabLinkPen, mehediPillRect.X + 14, shihabCy, mehediPillRect.X + 19, shihabCy);
                    }

                    Color shihabTxtCol = _mehediDark ? Color.FromArgb(234, 238, 248) : Color.FromArgb(24, 30, 42);
                    Rectangle mehediLblRect = new Rectangle(mehediPillRect.X + 28, mehediPillRect.Y, Math.Max(20, mehediPillRect.Width - 62), mehediPillRect.Height);
                    using (Font shihabAddFont = new Font("Segoe UI Semibold", 9f, FontStyle.Regular))
                    {
                        TextRenderer.DrawText(
                            mehediG,
                            "Add URL",
                            shihabAddFont,
                            mehediLblRect,
                            shihabTxtCol,
                            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                    }

                    int mehediBadgeSz = 24;
                    Rectangle shihabBadgeRect = new Rectangle(
                        mehediPillRect.Right - mehediBadgeSz - 7,
                        shihabCy - mehediBadgeSz / 2,
                        mehediBadgeSz,
                        mehediBadgeSz);
                    using (GraphicsPath mehediBadgePath = ShihabThemePalette.CreateShihabRoundedRect(shihabBadgeRect, 8))
                    using (LinearGradientBrush shihabVioletGrad = new LinearGradientBrush(
                        shihabBadgeRect,
                        Color.FromArgb(139, 92, 246),
                        Color.FromArgb(99, 102, 241),
                        45f))
                    using (Pen mehediPlusPen = new Pen(Color.White, 1.8f))
                    {
                        mehediPlusPen.StartCap = LineCap.Round;
                        mehediPlusPen.EndCap = LineCap.Round;
                        mehediG.FillPath(shihabVioletGrad, mehediBadgePath);
                        int shihabBx = shihabBadgeRect.X + mehediBadgeSz / 2;
                        int mehediBy = shihabBadgeRect.Y + mehediBadgeSz / 2;
                        mehediG.DrawLine(mehediPlusPen, shihabBx - 4, mehediBy, shihabBx + 4, mehediBy);
                        mehediG.DrawLine(mehediPlusPen, shihabBx, mehediBy - 4, shihabBx, mehediBy + 4);
                    }
                }
                return;
            }
            if (shihabBtnArgs.Item.Selected || shihabBtnArgs.Item.Pressed)
            {
                PaintMehediHoverPill(shihabBtnArgs.Graphics, new Rectangle(1, 3, shihabBtnArgs.Item.Width - 3, shihabBtnArgs.Item.Height - 7), shihabBtnArgs.Item.Pressed);
            }
        }

        protected override void OnRenderSplitButtonBackground(ToolStripItemRenderEventArgs mehediSplitArgs)
        {
            ToolStripSplitButton shihabSplit = mehediSplitArgs.Item as ToolStripSplitButton;
            if (shihabSplit == null) return;
            if (shihabSplit.Selected || shihabSplit.Pressed || shihabSplit.ButtonPressed)
            {
                PaintMehediHoverPill(mehediSplitArgs.Graphics, new Rectangle(1, 3, shihabSplit.Width - 3, shihabSplit.Height - 7), shihabSplit.Pressed || shihabSplit.ButtonPressed);
            }
            DrawArrow(new ToolStripArrowRenderEventArgs(
                mehediSplitArgs.Graphics,
                shihabSplit,
                shihabSplit.DropDownButtonBounds,
                _mehediDark ? Color.FromArgb(152, 158, 180) : Color.FromArgb(70, 80, 92),
                ArrowDirection.Down));
        }

        private void PaintMehediHoverPill(Graphics shihabG, Rectangle mehediRect, bool shihabPressed)
        {
            shihabG.SmoothingMode = SmoothingMode.AntiAlias;
            Color mehediFill = _mehediDark
                ? (shihabPressed ? Color.FromArgb(24, 27, 40) : Color.FromArgb(30, 33, 48))
                : (shihabPressed ? Color.FromArgb(214, 222, 248) : Color.FromArgb(228, 234, 250));
            Color shihabEdge = _mehediDark
                ? Color.FromArgb(52, 57, 80)
                : Color.FromArgb(175, 185, 216);
            using (GraphicsPath mehediPath = ShihabThemePalette.CreateShihabRoundedRect(mehediRect, 8))
            using (SolidBrush shihabBr = new SolidBrush(mehediFill))
            using (Pen mehediPen = new Pen(shihabEdge, 1f))
            {
                shihabG.FillPath(shihabBr, mehediPath);
                shihabG.DrawPath(mehediPen, mehediPath);
            }
        }
    }

    public sealed class ShihabRoundedCardPanel : Panel
    {
        public Color MehediFillColor { get; set; }
        public Color ShihabBorderColor { get; set; }
        public int MehediCornerRadius { get; set; }

        public ShihabRoundedCardPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            MehediFillColor = Color.FromArgb(22, 24, 33);
            ShihabBorderColor = Color.FromArgb(36, 40, 56);
            MehediCornerRadius = 9;
        }

        protected override void OnPaint(PaintEventArgs shihabE)
        {
            Graphics mehediG = shihabE.Graphics;
            mehediG.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush shihabOuter = new SolidBrush(Parent != null ? Parent.BackColor : BackColor))
            {
                mehediG.FillRectangle(shihabOuter, ClientRectangle);
            }
            Rectangle shihabCardRect = new Rectangle(0, 0, Math.Max(4, Width - 1), Math.Max(4, Height - 1));
            using (GraphicsPath mehediPath = ShihabThemePalette.CreateShihabRoundedRect(shihabCardRect, MehediCornerRadius))
            using (SolidBrush shihabFill = new SolidBrush(MehediFillColor))
            using (Pen mehediBorder = new Pen(ShihabBorderColor, 1f))
            {
                mehediG.FillPath(shihabFill, mehediPath);
                mehediG.DrawPath(mehediBorder, mehediPath);
            }
        }
    }

    public sealed class ShihabDarkTabControl : TabControl
    {
        public bool ShihabDarkMode { get; set; }
        private int _mehediHoveredTabIndex = -1;

        public ShihabDarkTabControl()
        {
            ShihabDarkMode = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            ItemSize = new Size(102, 28);
            Padding = new Point(12, 5);
        }

        protected override void OnMouseMove(MouseEventArgs shihabE)
        {
            base.OnMouseMove(shihabE);
            int mehediHit = -1;
            for (int shihabI = 0; shihabI < TabCount; shihabI++)
            {
                if (GetTabRect(shihabI).Contains(shihabE.Location))
                {
                    mehediHit = shihabI;
                    break;
                }
            }
            if (mehediHit != _mehediHoveredTabIndex)
            {
                _mehediHoveredTabIndex = mehediHit;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs mehediE)
        {
            base.OnMouseLeave(mehediE);
            if (_mehediHoveredTabIndex != -1)
            {
                _mehediHoveredTabIndex = -1;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs shihabE)
        {
            base.OnMouseDown(shihabE);
            if (shihabE.Button == MouseButtons.Left)
            {
                for (int mehediI = 0; mehediI < TabCount; mehediI++)
                {
                    if (GetTabRect(mehediI).Contains(shihabE.Location))
                    {
                        SelectedIndex = mehediI;
                        Invalidate();
                        break;
                    }
                }
            }
        }

        protected override void OnSelectedIndexChanged(EventArgs mehediE)
        {
            base.OnSelectedIndexChanged(mehediE);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs shihabE)
        {
            Graphics mehediG = shihabE.Graphics;
            mehediG.SmoothingMode = SmoothingMode.AntiAlias;
            mehediG.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Color shihabBg = ShihabDarkMode ? Color.FromArgb(18, 19, 25) : Color.FromArgb(245, 248, 252);
            Color mehediPageBg = ShihabDarkMode ? Color.FromArgb(22, 24, 33) : Color.White;
            Color shihabBorderCol = ShihabDarkMode ? Color.FromArgb(38, 42, 59) : Color.FromArgb(203, 213, 225);

            using (SolidBrush mehediCanvas = new SolidBrush(shihabBg))
            {
                mehediG.FillRectangle(mehediCanvas, ClientRectangle);
            }

            if (TabCount > 0)
            {
                Rectangle shihabPageFrame = Rectangle.Inflate(DisplayRectangle, 2, 2);
                using (SolidBrush mehediPageFill = new SolidBrush(mehediPageBg))
                using (Pen shihabFramePen = new Pen(shihabBorderCol, 1f))
                {
                    mehediG.FillRectangle(mehediPageFill, shihabPageFrame);
                    mehediG.DrawRectangle(shihabFramePen, shihabPageFrame.X, shihabPageFrame.Y, shihabPageFrame.Width - 1, shihabPageFrame.Height - 1);
                }
            }

            for (int shihabI = 0; shihabI < TabCount; shihabI++)
            {
                Rectangle mehediTabRect = GetTabRect(shihabI);
                Rectangle shihabPill = new Rectangle(mehediTabRect.X + 1, mehediTabRect.Y + 2, Math.Max(10, mehediTabRect.Width - 3), Math.Max(10, mehediTabRect.Height - 3));
                bool mehediActive = (shihabI == SelectedIndex);
                bool shihabHot = (shihabI == _mehediHoveredTabIndex);

                Color mehediTabFill = shihabBg;
                if (mehediActive)
                {
                    mehediTabFill = ShihabDarkMode ? Color.FromArgb(31, 34, 49) : Color.White;
                }
                else if (shihabHot)
                {
                    mehediTabFill = ShihabDarkMode ? Color.FromArgb(25, 27, 38) : Color.FromArgb(232, 238, 248);
                }

                using (GraphicsPath shihabPath = ShihabThemePalette.CreateShihabRoundedRect(shihabPill, 6))
                using (SolidBrush mehediTabBr = new SolidBrush(mehediTabFill))
                {
                    mehediG.FillPath(mehediTabBr, shihabPath);
                    if (mehediActive)
                    {
                        using (Pen shihabEdge = new Pen(ShihabDarkMode ? Color.FromArgb(52, 57, 82) : shihabBorderCol, 1f))
                        {
                            mehediG.DrawPath(shihabEdge, shihabPath);
                        }
                        using (SolidBrush mehediAccent = new SolidBrush(Color.FromArgb(124, 92, 255)))
                        {
                            mehediG.FillRectangle(mehediAccent, shihabPill.X + 8, shihabPill.Bottom - 2, Math.Max(8, shihabPill.Width - 16), 2);
                        }
                    }
                }

                Color shihabTxtCol = mehediActive
                    ? (ShihabDarkMode ? Color.FromArgb(242, 244, 252) : Color.FromArgb(24, 30, 42))
                    : (ShihabDarkMode ? Color.FromArgb(148, 155, 178) : Color.FromArgb(95, 108, 126));

                TextRenderer.DrawText(
                    mehediG,
                    TabPages[shihabI].Text,
                    Font,
                    shihabPill,
                    shihabTxtCol,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }
    }

    public sealed class MehediModernTreeView : TreeView
    {
        public bool ShihabDarkMode { get; set; }
        private TreeNode _mehediHoveredNode;

        public MehediModernTreeView()
        {
            ShihabDarkMode = true;
            DoubleBuffered = true;
            DrawMode = TreeViewDrawMode.OwnerDrawAll;
            FullRowSelect = true;
            HideSelection = false;
            ShowLines = false;
            ShowPlusMinus = false;
            ShowRootLines = false;
            ItemHeight = 29;
            BorderStyle = BorderStyle.None;
        }

        protected override void OnMouseMove(MouseEventArgs shihabE)
        {
            base.OnMouseMove(shihabE);
            TreeNode mehediHit = GetNodeAt(shihabE.Location);
            if (!ReferenceEquals(mehediHit, _mehediHoveredNode))
            {
                _mehediHoveredNode = mehediHit;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs mehediE)
        {
            base.OnMouseLeave(mehediE);
            if (_mehediHoveredNode != null)
            {
                _mehediHoveredNode = null;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs shihabE)
        {
            base.OnMouseDown(shihabE);
            TreeNode mehediNode = GetNodeAt(shihabE.Location);
            if (mehediNode != null)
            {
                int shihabChevronZoneStart = ClientSize.Width - 28;
                if (mehediNode.Nodes.Count > 0 && (shihabE.X >= shihabChevronZoneStart || shihabE.X <= 22))
                {
                    if (mehediNode.IsExpanded) mehediNode.Collapse();
                    else mehediNode.Expand();
                }
                SelectedNode = mehediNode;
            }
        }

        protected override void OnDrawNode(DrawTreeNodeEventArgs mehediArgs)
        {
            TreeNode shihabNode = mehediArgs.Node;
            if (shihabNode == null || mehediArgs.Bounds.Height <= 0) return;

            Graphics shihabG = mehediArgs.Graphics;
            shihabG.SmoothingMode = SmoothingMode.AntiAlias;
            shihabG.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Rectangle mehediRowRect = new Rectangle(0, mehediArgs.Bounds.Y, ClientSize.Width, mehediArgs.Bounds.Height);
            Color shihabCanvas = ShihabDarkMode ? Color.FromArgb(22, 24, 33) : Color.White;
            using (SolidBrush mehediCanvasBr = new SolidBrush(shihabCanvas))
            {
                shihabG.FillRectangle(mehediCanvasBr, mehediRowRect);
            }

            bool shihabSelected = ReferenceEquals(shihabNode, SelectedNode);
            bool mehediHovered = ReferenceEquals(shihabNode, _mehediHoveredNode);

            if (shihabSelected || mehediHovered)
            {
                Rectangle shihabPillRect = new Rectangle(6, mehediRowRect.Y + 2, Math.Max(20, ClientSize.Width - 12), Math.Max(10, mehediRowRect.Height - 4));
                Color mehediPillFill = shihabSelected
                    ? (ShihabDarkMode ? Color.FromArgb(34, 38, 54) : Color.FromArgb(226, 232, 252))
                    : (ShihabDarkMode ? Color.FromArgb(27, 30, 42) : Color.FromArgb(240, 244, 252));
                using (GraphicsPath shihabPath = ShihabThemePalette.CreateShihabRoundedRect(shihabPillRect, 7))
                using (SolidBrush mehediBr = new SolidBrush(mehediPillFill))
                {
                    shihabG.FillPath(mehediBr, shihabPath);
                    if (shihabSelected && ShihabDarkMode)
                    {
                        using (Pen shihabEdge = new Pen(Color.FromArgb(52, 58, 84), 1f))
                        {
                            shihabG.DrawPath(shihabEdge, shihabPath);
                        }
                    }
                }
            }

            int shihabBaseX = 14 + shihabNode.Level * 16;
            Rectangle mehediIconRect = new Rectangle(shihabBaseX, mehediRowRect.Y + (mehediRowRect.Height - 16) / 2, 16, 16);
            string shihabTagStr = shihabNode.Tag != null ? shihabNode.Tag.ToString() : shihabNode.Text;
            ShihabThemePalette.DrawMehediTreeCategoryIcon(shihabG, mehediIconRect, shihabTagStr, shihabNode.Text);

            if (shihabNode.Nodes.Count > 0)
            {
                int mehediChevX = ClientSize.Width - 20;
                int shihabCy = mehediRowRect.Y + mehediRowRect.Height / 2;
                using (Pen mehediChevPen = new Pen(ShihabDarkMode ? Color.FromArgb(148, 155, 178) : Color.FromArgb(80, 90, 102), 1.5f))
                {
                    if (shihabNode.IsExpanded)
                    {
                        shihabG.DrawLines(mehediChevPen, new Point[]
                        {
                            new Point(mehediChevX - 4, shihabCy - 2),
                            new Point(mehediChevX, shihabCy + 2),
                            new Point(mehediChevX + 4, shihabCy - 2)
                        });
                    }
                    else
                    {
                        shihabG.DrawLines(mehediChevPen, new Point[]
                        {
                            new Point(mehediChevX - 4, shihabCy + 2),
                            new Point(mehediChevX, shihabCy - 2),
                            new Point(mehediChevX + 4, shihabCy + 2)
                        });
                    }
                }
            }

            Rectangle shihabTextRect = new Rectangle(
                mehediIconRect.Right + 8,
                mehediRowRect.Y,
                Math.Max(20, mehediRowRect.Width - mehediIconRect.Right - 32),
                mehediRowRect.Height);

            bool mehediIsRoot = shihabNode.Level == 0;
            Color mehediTextCol = shihabSelected
                ? (ShihabDarkMode ? Color.FromArgb(245, 247, 255) : Color.FromArgb(20, 26, 38))
                : (ShihabDarkMode
                    ? (mehediIsRoot ? Color.FromArgb(208, 214, 232) : Color.FromArgb(156, 163, 184))
                    : Color.FromArgb(40, 48, 60));

            using (Font shihabNodeFont = new Font("Segoe UI", mehediIsRoot ? 9.4f : 8.9f, (shihabSelected || mehediIsRoot) ? FontStyle.Bold : FontStyle.Regular))
            {
                TextRenderer.DrawText(
                    shihabG,
                    shihabNode.Text,
                    shihabNodeFont,
                    shihabTextRect,
                    mehediTextCol,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }
    }

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
                _shihabRowHeightScaler.ImageSize = new Size(1, shihabRowH);
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

                using (Font mehediGridHdrFont = new Font("Segoe UI", 7.8f, FontStyle.Bold))
                {
                    TextRenderer.DrawText(mehediG, mehediUpperTitle, mehediGridHdrFont, mehediCellTxtRect, shihabHdrTextCol, shihabHdrFlags);
                }
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
            using (Font shihabHdrFont = new Font("Segoe UI Semibold", 8.6f, FontStyle.Regular))
            {
                TextRenderer.DrawText(
                    mehediG,
                    shihabHeaderText,
                    shihabHdrFont,
                    mehediTextBounds,
                    mehediFg,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
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
                Region shihabSavedClip = mehediG.Clip;
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
                shihabSavedClip.Dispose();
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
                    using (Font shihabTitleFont = new Font("Segoe UI Semibold", 9f, FontStyle.Regular))
                    using (Font mehediSubFont = new Font("Segoe UI", 7.6f, FontStyle.Regular))
                    {
                        TextRenderer.DrawText(
                            mehediG,
                            mehediText,
                            shihabTitleFont,
                            shihabNameRect,
                            shihabTextColor,
                            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                        TextRenderer.DrawText(
                            mehediG,
                            shihabSubCat,
                            mehediSubFont,
                            mehediSubRect,
                            mehediSubColor,
                            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    }
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
                using (Font shihabIdxFont = new Font("Segoe UI", 8.6f, FontStyle.Regular))
                {
                    TextRenderer.DrawText(
                        mehediG,
                        mehediCellText,
                        shihabIdxFont,
                        shihabCell,
                        mehediIdxCol,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
                return;
            }

            if (shihabCol == 1)
            {
                Color mehediNameCol = ShihabThemePalette.GetShihabPrimaryText(MehediDarkMode);
                Rectangle shihabNameBounds = new Rectangle(shihabCell.X + 10, shihabCell.Y, Math.Max(4, shihabCell.Width - 16), shihabCell.Height);
                using (Font mehediNameFont = new Font("Segoe UI", 9f, FontStyle.Bold))
                {
                    TextRenderer.DrawText(
                        mehediG,
                        mehediCellText,
                        mehediNameFont,
                        shihabNameBounds,
                        mehediNameCol,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
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
                using (Font mehediPctFont = new Font("Segoe UI", 8.2f, FontStyle.Regular))
                {
                    TextRenderer.DrawText(
                        mehediG,
                        mehediCellText,
                        mehediPctFont,
                        mehediPctTxtRect,
                        shihabPctCol,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
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
                using (Font mehediStatFont = new Font("Segoe UI", 8.8f, FontStyle.Bold))
                {
                    TextRenderer.DrawText(
                        mehediG,
                        mehediCellText,
                        mehediStatFont,
                        shihabStatRect,
                        mehediStatusCol,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
                return;
            }

            Color shihabDefaultCol = MehediDarkMode ? Color.FromArgb(136, 145, 165) : Color.FromArgb(71, 85, 105);
            if (shihabCol == 4 && shihabDl != null && shihabDl.ShihabCurrentSpeedBps > 5.0)
            {
                shihabDefaultCol = Color.FromArgb(56, 189, 248);
            }
            Rectangle mehediGenRect = new Rectangle(shihabCell.X + 10, shihabCell.Y, Math.Max(4, shihabCell.Width - 14), shihabCell.Height);
            using (Font shihabGenFont = new Font("Segoe UI", 8.6f, FontStyle.Regular))
            {
                TextRenderer.DrawText(
                    mehediG,
                    mehediCellText,
                    shihabGenFont,
                    mehediGenRect,
                    shihabDefaultCol,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }
    }

    public static class ShihabThemePalette
    {
        public const string ShihabMainHeaderTitle = "MHS Download Manager 1.0.5 (Mehedi Hasan Shihab)";

        [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
        private static extern int SetMehediDwmWindowAttribute(IntPtr shihabHwnd, int mehediAttr, ref int shihabAttrValue, int mehediAttrSize);

        [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true)]
        private static extern int SetMehediPreferredAppMode(int shihabPreferredMode);

        [DllImport("uxtheme.dll", EntryPoint = "#136", SetLastError = true)]
        private static extern void FlushShihabMenuThemes();

        [DllImport("uxtheme.dll", EntryPoint = "SetWindowTheme", CharSet = CharSet.Unicode)]
        private static extern int SetShihabWindowTheme(IntPtr shihabHwnd, string mehediSubAppName, string shihabSubIdList);

        public static void EnableShihabProcessDarkScrollbars()
        {
            try
            {
                SetMehediPreferredAppMode(1);
                FlushShihabMenuThemes();
            }
            catch { }
        }

        public static void ApplyMehediNativeDarkFrame(Form shihabForm, bool mehediDark)
        {
            if (shihabForm == null) return;
            EventHandler shihabApplyDwm = delegate
            {
                try
                {
                    if (!shihabForm.IsHandleCreated) return;
                    int mehediUseDark = mehediDark ? 1 : 0;
                    SetMehediDwmWindowAttribute(shihabForm.Handle, 20, ref mehediUseDark, sizeof(int));
                    SetMehediDwmWindowAttribute(shihabForm.Handle, 19, ref mehediUseDark, sizeof(int));
                    if (mehediDark)
                    {
                        int shihabObsidianCaptionRef = (25 << 16) | (19 << 8) | 18;
                        SetMehediDwmWindowAttribute(shihabForm.Handle, 35, ref shihabObsidianCaptionRef, sizeof(int));
                        int mehediCaptionTextRef = (245 << 16) | (234 << 8) | 228;
                        SetMehediDwmWindowAttribute(shihabForm.Handle, 36, ref mehediCaptionTextRef, sizeof(int));
                        int shihabFrameBorderRef = (56 << 16) | (40 << 8) | 36;
                        SetMehediDwmWindowAttribute(shihabForm.Handle, 34, ref shihabFrameBorderRef, sizeof(int));
                    }
                    else
                    {
                        int mehediLightCaptionRef = (252 << 16) | (248 << 8) | 245;
                        SetMehediDwmWindowAttribute(shihabForm.Handle, 35, ref mehediLightCaptionRef, sizeof(int));
                        int shihabLightTextRef = (42 << 16) | (30 << 8) | 24;
                        SetMehediDwmWindowAttribute(shihabForm.Handle, 36, ref shihabLightTextRef, sizeof(int));
                    }
                }
                catch { }
            };
            if (shihabForm.IsHandleCreated) shihabApplyDwm(shihabForm, EventArgs.Empty);
            shihabForm.HandleCreated += shihabApplyDwm;
            shihabForm.Shown += delegate
            {
                shihabApplyDwm(shihabForm, EventArgs.Empty);
                ApplyShihabDarkThemeToControlTree(shihabForm, mehediDark);
            };
        }

        public static void ApplyShihabDarkThemeToControlTree(Control shihabRoot, bool mehediDark)
        {
            if (shihabRoot == null) return;
            BindMehediControlNativeTheme(shihabRoot, mehediDark);

            ShihabDarkTabControl mehediDarkTabs = shihabRoot as ShihabDarkTabControl;
            if (mehediDarkTabs != null)
            {
                mehediDarkTabs.ShihabDarkMode = mehediDark;
                mehediDarkTabs.Invalidate();
            }

            TextBox shihabTxt = shihabRoot as TextBox;
            if (shihabTxt != null && shihabTxt.BorderStyle != BorderStyle.None)
            {
                shihabTxt.BorderStyle = BorderStyle.FixedSingle;
                shihabTxt.BackColor = GetShihabSurfaceBg(mehediDark);
                shihabTxt.ForeColor = GetShihabPrimaryText(mehediDark);
            }

            ComboBox mehediCombo = shihabRoot as ComboBox;
            if (mehediCombo != null)
            {
                mehediCombo.FlatStyle = FlatStyle.Flat;
                mehediCombo.BackColor = GetShihabSurfaceBg(mehediDark);
                mehediCombo.ForeColor = GetShihabPrimaryText(mehediDark);
            }

            NumericUpDown shihabNum = shihabRoot as NumericUpDown;
            if (shihabNum != null)
            {
                shihabNum.BorderStyle = BorderStyle.FixedSingle;
                shihabNum.BackColor = GetShihabSurfaceBg(mehediDark);
                shihabNum.ForeColor = GetShihabPrimaryText(mehediDark);
            }

            ListBox mehediListBox = shihabRoot as ListBox;
            if (mehediListBox != null)
            {
                mehediListBox.BorderStyle = BorderStyle.FixedSingle;
                mehediListBox.BackColor = GetShihabSurfaceBg(mehediDark);
                mehediListBox.ForeColor = GetShihabPrimaryText(mehediDark);
            }

            GroupBox shihabGroup = shihabRoot as GroupBox;
            if (shihabGroup != null)
            {
                shihabGroup.ForeColor = GetShihabPrimaryText(mehediDark);
            }

            TabPage mehediTabPage = shihabRoot as TabPage;
            if (mehediTabPage != null)
            {
                mehediTabPage.BackColor = GetMehediWindowBg(mehediDark);
                mehediTabPage.ForeColor = GetShihabPrimaryText(mehediDark);
            }

            for (int shihabI = 0; shihabI < shihabRoot.Controls.Count; shihabI++)
            {
                ApplyShihabDarkThemeToControlTree(shihabRoot.Controls[shihabI], mehediDark);
            }
        }

        private static void BindMehediControlNativeTheme(Control shihabCtrl, bool mehediDark)
        {
            string shihabSubApp = null;
            if (shihabCtrl is ComboBox)
            {
                shihabSubApp = mehediDark ? "DarkMode_CFD" : "Explorer";
            }
            else if (shihabCtrl is ListView || shihabCtrl is TreeView || shihabCtrl is TextBoxBase || shihabCtrl is ListBox || shihabCtrl is ScrollableControl)
            {
                shihabSubApp = mehediDark ? "DarkMode_Explorer" : "Explorer";
            }
            if (shihabSubApp == null) return;

            Action mehediApplyHandle = delegate
            {
                try
                {
                    if (shihabCtrl.IsHandleCreated)
                    {
                        SetShihabWindowTheme(shihabCtrl.Handle, shihabSubApp, null);
                    }
                }
                catch { }
            };
            mehediApplyHandle();
            shihabCtrl.HandleCreated += delegate { mehediApplyHandle(); };
        }

        public static GraphicsPath CreateShihabRoundedRect(Rectangle mehediBounds, int shihabRadius)
        {
            GraphicsPath mehediPath = new GraphicsPath();
            int shihabDiam = shihabRadius * 2;
            if (shihabDiam <= 0 || mehediBounds.Width <= shihabDiam || mehediBounds.Height <= shihabDiam)
            {
                mehediPath.AddRectangle(mehediBounds);
                return mehediPath;
            }
            mehediPath.AddArc(mehediBounds.X, mehediBounds.Y, shihabDiam, shihabDiam, 180, 90);
            mehediPath.AddArc(mehediBounds.Right - shihabDiam, mehediBounds.Y, shihabDiam, shihabDiam, 270, 90);
            mehediPath.AddArc(mehediBounds.Right - shihabDiam, mehediBounds.Bottom - shihabDiam, shihabDiam, shihabDiam, 0, 90);
            mehediPath.AddArc(mehediBounds.X, mehediBounds.Bottom - shihabDiam, shihabDiam, shihabDiam, 90, 90);
            mehediPath.CloseFigure();
            return mehediPath;
        }

        private static ShihabCustomThemePreset _activeThemePreset;
        private static bool _activeThemeIsDark = true;

        public static void ApplyThemePreset(ShihabCustomThemePreset preset)
        {
            _activeThemePreset = preset;
            _activeThemeIsDark = preset == null || ShihabThemeManager.IsDarkThemePreset(preset);
        }

        public static Color BlendThemeColor(Color a, Color b, double weightB)
        {
            double w = Math.Max(0.0, Math.Min(1.0, weightB));
            int r = (int)Math.Round(a.R * (1.0 - w) + b.R * w);
            int g = (int)Math.Round(a.G * (1.0 - w) + b.G * w);
            int bl = (int)Math.Round(a.B * (1.0 - w) + b.B * w);
            return Color.FromArgb(Math.Max(0, Math.Min(255, r)), Math.Max(0, Math.Min(255, g)), Math.Max(0, Math.Min(255, bl)));
        }

        public static Color GetMehediWindowBg(bool shihabDark)
        {
            if (_activeThemePreset != null && _activeThemeIsDark == shihabDark)
            {
                return _activeThemePreset.BackgroundMain;
            }
            return shihabDark ? Color.FromArgb(18, 19, 25) : Color.FromArgb(245, 248, 252);
        }

        public static Color GetShihabSidebarBg(bool mehediDark)
        {
            if (_activeThemePreset != null && _activeThemeIsDark == mehediDark)
            {
                return _activeThemePreset.BackgroundSidebar;
            }
            return mehediDark ? Color.FromArgb(22, 24, 33) : Color.White;
        }

        public static Color GetShihabSurfaceBg(bool mehediDark)
        {
            if (_activeThemePreset != null && _activeThemeIsDark == mehediDark)
            {
                return _activeThemePreset.BackgroundCard;
            }
            return mehediDark ? Color.FromArgb(25, 28, 39) : Color.White;
        }

        public static Color GetMehediHeaderBg(bool shihabDark)
        {
            if (_activeThemePreset != null && _activeThemeIsDark == shihabDark)
            {
                return _activeThemePreset.GridAlternate;
            }
            return shihabDark ? Color.FromArgb(22, 24, 34) : Color.FromArgb(232, 240, 250);
        }

        public static Color GetShihabPrimaryText(bool mehediDark)
        {
            if (_activeThemePreset != null && _activeThemeIsDark == mehediDark)
            {
                return _activeThemePreset.TextPrimary;
            }
            return mehediDark ? Color.FromArgb(230, 234, 245) : Color.FromArgb(22, 28, 38);
        }

        public static Color GetMehediMutedText(bool shihabDark)
        {
            if (_activeThemePreset != null && _activeThemeIsDark == shihabDark)
            {
                return _activeThemePreset.TextSecondary;
            }
            return shihabDark ? Color.FromArgb(132, 140, 166) : Color.FromArgb(95, 110, 128);
        }

        public static Color GetShihabAccentBlue()
        {
            if (_activeThemePreset != null)
            {
                return _activeThemePreset.AccentPrimary;
            }
            return Color.FromArgb(124, 92, 255);
        }

        public static Color GetMehediAccentCyan()
        {
            return Color.FromArgb(56, 189, 248);
        }

        public static Color GetMehediAccentGreen()
        {
            return Color.FromArgb(16, 185, 129);
        }

        public static Color GetShihabAccentAmber()
        {
            return Color.FromArgb(245, 158, 11);
        }

        public static Color GetMehediAccentRed()
        {
            return Color.FromArgb(239, 68, 68);
        }

        public static Color GetShihabBorder(bool mehediDark)
        {
            if (_activeThemePreset != null && _activeThemeIsDark == mehediDark)
            {
                return BlendThemeColor(_activeThemePreset.BackgroundCard, _activeThemePreset.TextSecondary, mehediDark ? 0.22 : 0.35);
            }
            return mehediDark ? Color.FromArgb(38, 43, 60) : Color.FromArgb(203, 213, 225);
        }

        public static Button CreateMehediStyledButton(string shihabText, Color mehediBg, Color shihabFg, int mehediWidth)
        {
            Button shihabBtn = new Button();
            shihabBtn.Text = shihabText;
            shihabBtn.Width = mehediWidth;
            shihabBtn.Height = 32;
            shihabBtn.FlatStyle = FlatStyle.Flat;
            shihabBtn.FlatAppearance.BorderSize = 1;
            shihabBtn.FlatAppearance.BorderColor = Color.FromArgb(52, 58, 82);
            shihabBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(
                Math.Min(255, mehediBg.R + 14),
                Math.Min(255, mehediBg.G + 14),
                Math.Min(255, mehediBg.B + 18));
            shihabBtn.FlatAppearance.MouseDownBackColor = Color.FromArgb(
                Math.Max(0, mehediBg.R - 10),
                Math.Max(0, mehediBg.G - 10),
                Math.Max(0, mehediBg.B - 10));
            shihabBtn.BackColor = mehediBg;
            shihabBtn.ForeColor = shihabFg;
            shihabBtn.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Regular);
            shihabBtn.Cursor = Cursors.Hand;
            return shihabBtn;
        }

        private static Icon _shihabCachedOfficialIcon;
        private static Bitmap _mehediCachedOfficialPng;
        private static Bitmap _shihabCachedAuthorPortrait;

        public static Icon CreateShihabAppIcon()
        {
            if (_shihabCachedOfficialIcon != null)
            {
                return _shihabCachedOfficialIcon;
            }
            try
            {
                Stream mehediIcoStream = typeof(ShihabThemePalette).Assembly.GetManifestResourceStream("ShihabMehediIDM.mdm_icon.ico");
                if (mehediIcoStream != null)
                {
                    using (mehediIcoStream)
                    {
                        _shihabCachedOfficialIcon = new Icon(mehediIcoStream);
                        return _shihabCachedOfficialIcon;
                    }
                }
            }
            catch { }

            try
            {
                Icon shihabExeIco = Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
                if (shihabExeIco != null)
                {
                    _shihabCachedOfficialIcon = shihabExeIco;
                    return _shihabCachedOfficialIcon;
                }
            }
            catch { }
            return SystemIcons.Application;
        }

        public static Bitmap CreateMehediLogoBitmap(int shihabPixelSize)
        {
            if (_mehediCachedOfficialPng == null)
            {
                try
                {
                    Stream shihabPngStream = typeof(ShihabThemePalette).Assembly.GetManifestResourceStream("ShihabMehediIDM.mdm_icon.png");
                    if (shihabPngStream != null)
                    {
                        using (shihabPngStream)
                        using (Image mehediLoaded = Image.FromStream(shihabPngStream))
                        {
                            _mehediCachedOfficialPng = new Bitmap(mehediLoaded);
                        }
                    }
                }
                catch { }
            }

            int mehediSz = Math.Max(16, shihabPixelSize);
            Bitmap shihabScaled = new Bitmap(mehediSz, mehediSz);
            using (Graphics mehediG = Graphics.FromImage(shihabScaled))
            {
                mehediG.SmoothingMode = SmoothingMode.HighQuality;
                mehediG.InterpolationMode = InterpolationMode.HighQualityBicubic;
                mehediG.PixelOffsetMode = PixelOffsetMode.HighQuality;
                if (_mehediCachedOfficialPng != null)
                {
                    mehediG.DrawImage(_mehediCachedOfficialPng, new Rectangle(0, 0, mehediSz, mehediSz));
                }
                else
                {
                    Icon shihabFallback = CreateShihabAppIcon();
                    mehediG.DrawIcon(shihabFallback, new Rectangle(0, 0, mehediSz, mehediSz));
                }
            }
            return shihabScaled;
        }

        public static Bitmap CreateShihabCircularAuthorBitmap(int shihabDiameterPx)
        {
            if (_shihabCachedAuthorPortrait == null)
            {
                try
                {
                    Stream mehediJpgStream = typeof(ShihabThemePalette).Assembly.GetManifestResourceStream("ShihabMehediIDM.author_shihab.jpg");
                    if (mehediJpgStream != null)
                    {
                        using (mehediJpgStream)
                        using (Image shihabLoaded = Image.FromStream(mehediJpgStream))
                        {
                            _shihabCachedAuthorPortrait = new Bitmap(shihabLoaded);
                        }
                    }
                }
                catch { }

                if (_shihabCachedAuthorPortrait == null)
                {
                    try
                    {
                        string mehediLocalAsset = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\assets\\author_shihab.jpg");
                        if (File.Exists(mehediLocalAsset))
                        {
                            using (Image shihabDiskImg = Image.FromFile(mehediLocalAsset))
                            {
                                _shihabCachedAuthorPortrait = new Bitmap(shihabDiskImg);
                            }
                        }
                    }
                    catch { }
                }
            }

            int mehediDiam = Math.Max(32, shihabDiameterPx);
            Bitmap shihabResult = new Bitmap(mehediDiam, mehediDiam);
            if (_shihabCachedAuthorPortrait == null)
            {
                return CreateMehediLogoBitmap(mehediDiam);
            }

            using (Bitmap mehediScaledSquare = new Bitmap(mehediDiam, mehediDiam))
            {
                using (Graphics shihabScaleG = Graphics.FromImage(mehediScaledSquare))
                {
                    shihabScaleG.SmoothingMode = SmoothingMode.HighQuality;
                    shihabScaleG.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    shihabScaleG.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    int mehediSrcW = _shihabCachedAuthorPortrait.Width;
                    int shihabSrcH = _shihabCachedAuthorPortrait.Height;
                    int mehediCrop = Math.Min(mehediSrcW, shihabSrcH);
                    int shihabSrcX = (mehediSrcW - mehediCrop) / 2;
                    int mehediSrcY = (shihabSrcH - mehediCrop) / 2;

                    shihabScaleG.DrawImage(
                        _shihabCachedAuthorPortrait,
                        new Rectangle(0, 0, mehediDiam, mehediDiam),
                        new Rectangle(shihabSrcX, mehediSrcY, mehediCrop, mehediCrop),
                        GraphicsUnit.Pixel);
                }

                using (Graphics mehediOutG = Graphics.FromImage(shihabResult))
                using (TextureBrush shihabTexBrush = new TextureBrush(mehediScaledSquare, WrapMode.Clamp))
                using (Pen mehediOuterRing = new Pen(Color.FromArgb(46, 62, 88), 2.2f))
                using (Pen shihabInnerGlow = new Pen(Color.FromArgb(75, 130, 210, 255), 1f))
                {
                    mehediOutG.SmoothingMode = SmoothingMode.AntiAlias;
                    mehediOutG.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    mehediOutG.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    RectangleF shihabCircleRect = new RectangleF(1.5f, 1.5f, mehediDiam - 3f, mehediDiam - 3f);
                    mehediOutG.FillEllipse(shihabTexBrush, shihabCircleRect);
                    mehediOutG.DrawEllipse(mehediOuterRing, shihabCircleRect);

                    RectangleF mehediInnerRect = new RectangleF(2.8f, 2.8f, mehediDiam - 5.6f, mehediDiam - 5.6f);
                    mehediOutG.DrawEllipse(shihabInnerGlow, mehediInnerRect);
                }
            }
            return shihabResult;
        }

        public static Bitmap DrawMehediLargeToolbarIcon(string shihabKind, bool mehediActive)
        {
            Bitmap shihabBmp = new Bitmap(28, 28);
            using (Graphics mehediG = Graphics.FromImage(shihabBmp))
            {
                mehediG.SmoothingMode = SmoothingMode.AntiAlias;
                mehediG.PixelOffsetMode = PixelOffsetMode.HighQuality;

                Color shihabStroke = mehediActive ? Color.FromArgb(214, 222, 242) : Color.FromArgb(100, 108, 134);
                Color mehediMaskBg = Color.FromArgb(18, 19, 25);

                if (shihabKind == "add")
                {
                    Rectangle shihabBadge = new Rectangle(3, 3, 22, 22);
                    using (GraphicsPath mehediPath = CreateShihabRoundedRect(shihabBadge, 7))
                    using (LinearGradientBrush shihabGrad = new LinearGradientBrush(
                        shihabBadge,
                        Color.FromArgb(139, 92, 246),
                        Color.FromArgb(99, 102, 241),
                        45f))
                    using (Pen mehediPlus = new Pen(Color.White, 2f))
                    {
                        mehediPlus.StartCap = LineCap.Round;
                        mehediPlus.EndCap = LineCap.Round;
                        mehediG.FillPath(shihabGrad, mehediPath);
                        mehediG.DrawLine(mehediPlus, 9, 14, 19, 14);
                        mehediG.DrawLine(mehediPlus, 14, 9, 14, 19);
                    }
                }
                else if (shihabKind == "resume")
                {
                    Color mehediCol = mehediActive ? Color.FromArgb(52, 211, 153) : shihabStroke;
                    using (Pen shihabPen = new Pen(mehediCol, 1.9f))
                    {
                        shihabPen.StartCap = LineCap.Round;
                        shihabPen.EndCap = LineCap.Round;
                        shihabPen.LineJoin = LineJoin.Round;
                        mehediG.DrawEllipse(shihabPen, 4, 4, 20, 20);
                        mehediG.DrawPolygon(shihabPen, new PointF[]
                        {
                            new PointF(12f, 9.5f),
                            new PointF(18.5f, 14f),
                            new PointF(12f, 18.5f)
                        });
                    }
                }
                else if (shihabKind == "stop")
                {
                    Color mehediCol = mehediActive ? Color.FromArgb(251, 191, 36) : shihabStroke;
                    using (Pen shihabPen = new Pen(mehediCol, 2f))
                    {
                        shihabPen.StartCap = LineCap.Round;
                        shihabPen.EndCap = LineCap.Round;
                        mehediG.DrawEllipse(shihabPen, 4, 4, 20, 20);
                        mehediG.DrawLine(shihabPen, 11.5f, 10f, 11.5f, 18f);
                        mehediG.DrawLine(shihabPen, 16.5f, 10f, 16.5f, 18f);
                    }
                }
                else if (shihabKind == "stopall")
                {
                    Color mehediCol = mehediActive ? Color.FromArgb(248, 113, 113) : shihabStroke;
                    using (Pen shihabPen = new Pen(mehediCol, 1.85f))
                    using (GraphicsPath mehediSq = CreateShihabRoundedRect(new Rectangle(7, 7, 14, 14), 3))
                    {
                        mehediG.DrawPath(shihabPen, mehediSq);
                    }
                }
                else if (shihabKind == "startq")
                {
                    Color mehediCol = Color.FromArgb(218, 224, 242);
                    using (Pen shihabPen = new Pen(mehediCol, 1.9f))
                    {
                        shihabPen.LineJoin = LineJoin.Round;
                        mehediG.DrawPolygon(shihabPen, new PointF[]
                        {
                            new PointF(9f, 6.5f),
                            new PointF(20.5f, 14f),
                            new PointF(9f, 21.5f)
                        });
                    }
                }
                else if (shihabKind == "stopq")
                {
                    Color mehediCol = Color.FromArgb(164, 172, 196);
                    using (Pen shihabPen = new Pen(mehediCol, 1.85f))
                    using (GraphicsPath mehediSq = CreateShihabRoundedRect(new Rectangle(7, 7, 14, 14), 3))
                    {
                        mehediG.DrawPath(shihabPen, mehediSq);
                    }
                }
                else if (shihabKind == "scheduler")
                {
                    using (Pen shihabPen = new Pen(Color.FromArgb(214, 222, 242), 1.8f))
                    using (GraphicsPath mehediBack = CreateShihabRoundedRect(new Rectangle(5, 8, 14, 14), 3))
                    using (GraphicsPath shihabFront = CreateShihabRoundedRect(new Rectangle(9, 5, 14, 14), 3))
                    using (SolidBrush mehediMask = new SolidBrush(mehediMaskBg))
                    {
                        shihabPen.StartCap = LineCap.Round;
                        shihabPen.EndCap = LineCap.Round;
                        mehediG.DrawPath(shihabPen, mehediBack);
                        mehediG.FillPath(mehediMask, shihabFront);
                        mehediG.DrawPath(shihabPen, shihabFront);
                        mehediG.DrawLine(shihabPen, 16, 9, 16, 15);
                        mehediG.DrawLine(shihabPen, 13, 12, 19, 12);
                    }
                }
                else if (shihabKind == "options")
                {
                    using (Pen mehediGearPen = new Pen(Color.FromArgb(214, 222, 242), 1.75f))
                    {
                        mehediGearPen.LineJoin = LineJoin.Round;
                        List<PointF> shihabGearPts = new List<PointF>();
                        int mehediTeeth = 8;
                        for (int shihabI = 0; shihabI < mehediTeeth * 4; shihabI++)
                        {
                            double mehediAng = (Math.PI * 2.0 * shihabI) / (mehediTeeth * 4);
                            float shihabRad = (shihabI % 4 == 0 || shihabI % 4 == 1) ? 10.2f : 7.8f;
                            shihabGearPts.Add(new PointF(14f + (float)(Math.Cos(mehediAng) * shihabRad), 14f + (float)(Math.Sin(mehediAng) * shihabRad)));
                        }
                        mehediG.DrawPolygon(mehediGearPen, shihabGearPts.ToArray());
                        mehediG.DrawEllipse(mehediGearPen, 10.5f, 10.5f, 7f, 7f);
                    }
                }
                else if (shihabKind == "delete")
                {
                    Color mehediCol = mehediActive ? Color.FromArgb(251, 113, 133) : shihabStroke;
                    DrawShihabTrashCanIcon(mehediG, mehediCol, false);
                }
                else if (shihabKind == "clean")
                {
                    DrawShihabTrashCanIcon(mehediG, Color.FromArgb(196, 181, 253), true);
                }
                else if (shihabKind == "grabber")
                {
                    using (Pen shihabPen = new Pen(Color.FromArgb(165, 180, 252), 1.75f))
                    {
                        mehediG.DrawEllipse(shihabPen, 5, 5, 18, 18);
                        mehediG.DrawEllipse(shihabPen, 10, 5, 8, 18);
                        mehediG.DrawLine(shihabPen, 5, 14, 23, 14);
                    }
                }
            }
            return shihabBmp;
        }

        private static void DrawShihabTrashCanIcon(Graphics mehediG, Color shihabColor, bool mehediWithCheckmark)
        {
            using (Pen shihabPen = new Pen(shihabColor, 1.75f))
            {
                shihabPen.StartCap = LineCap.Round;
                shihabPen.EndCap = LineCap.Round;
                shihabPen.LineJoin = LineJoin.Round;
                mehediG.DrawLine(shihabPen, 6, 8, 22, 8);
                mehediG.DrawArc(shihabPen, 11, 4, 6, 5, 180, 180);
                PointF[] mehediBody = new PointF[]
                {
                    new PointF(8, 8),
                    new PointF(9.5f, 23),
                    new PointF(18.5f, 23),
                    new PointF(20, 8)
                };
                mehediG.DrawLines(shihabPen, mehediBody);
                if (mehediWithCheckmark)
                {
                    mehediG.DrawLines(shihabPen, new PointF[]
                    {
                        new PointF(11f, 15.5f),
                        new PointF(13.5f, 18f),
                        new PointF(17.5f, 13f)
                    });
                }
                else
                {
                    mehediG.DrawLine(shihabPen, 12, 12, 12, 19);
                    mehediG.DrawLine(shihabPen, 16, 12, 16, 19);
                }
            }
        }

        public static Bitmap DrawMehediToolbarGlyph(string shihabKind, Color mehediTint)
        {
            return DrawMehediLargeToolbarIcon(shihabKind, true);
        }

        public static void DrawMehediMiniQueueFolder(Graphics shihabG, Rectangle mehediRect)
        {
            shihabG.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen shihabPen = new Pen(Color.FromArgb(165, 174, 202), 1.4f))
            {
                shihabPen.LineJoin = LineJoin.Round;
                PointF[] mehediFolder = new PointF[]
                {
                    new PointF(mehediRect.X + 1, mehediRect.Y + 4),
                    new PointF(mehediRect.X + 1, mehediRect.Bottom - 2),
                    new PointF(mehediRect.Right - 2, mehediRect.Bottom - 2),
                    new PointF(mehediRect.Right - 2, mehediRect.Y + 5),
                    new PointF(mehediRect.X + 8, mehediRect.Y + 5),
                    new PointF(mehediRect.X + 6, mehediRect.Y + 2.5f),
                    new PointF(mehediRect.X + 1, mehediRect.Y + 2.5f)
                };
                shihabG.DrawPolygon(shihabPen, mehediFolder);
            }
        }

        public static void DrawShihabFileTypeBadge(Graphics mehediG, Rectangle shihabRect, string mehediFileName)
        {
            mehediG.SmoothingMode = SmoothingMode.AntiAlias;
            string shihabExt = "";
            if (!string.IsNullOrEmpty(mehediFileName))
            {
                try { shihabExt = Path.GetExtension(mehediFileName).TrimStart('.').ToUpperInvariant(); } catch { }
            }

            Color mehediStrokeCol = Color.FromArgb(162, 171, 198);

            if (shihabExt == "EXE" || shihabExt == "MSI" || shihabExt == "APK" || shihabExt == "DMG")
            {
                using (SolidBrush shihabAppBr = new SolidBrush(Color.FromArgb(174, 183, 210)))
                using (GraphicsPath mehediSq1 = CreateShihabRoundedRect(new Rectangle(shihabRect.X + 2, shihabRect.Y + 2, 6, 6), 2))
                using (GraphicsPath mehediSq2 = CreateShihabRoundedRect(new Rectangle(shihabRect.X + 10, shihabRect.Y + 2, 6, 6), 2))
                using (GraphicsPath mehediSq3 = CreateShihabRoundedRect(new Rectangle(shihabRect.X + 2, shihabRect.Y + 10, 6, 6), 2))
                using (GraphicsPath mehediSq4 = CreateShihabRoundedRect(new Rectangle(shihabRect.X + 10, shihabRect.Y + 10, 6, 6), 2))
                {
                    mehediG.FillPath(shihabAppBr, mehediSq1);
                    mehediG.FillPath(shihabAppBr, mehediSq2);
                    mehediG.FillPath(shihabAppBr, mehediSq3);
                    mehediG.FillPath(shihabAppBr, mehediSq4);
                }
                return;
            }

            bool shihabIsVideo = shihabExt == "MP4" || shihabExt == "MKV" || shihabExt == "WEBM" ||
                                 shihabExt == "MOV" || shihabExt == "AVI" || shihabExt == "FLV" ||
                                 shihabExt == "M4V" || shihabExt == "TS" || shihabExt == "M3U8";
            if (shihabIsVideo)
            {
                using (Pen shihabCamPen = new Pen(mehediStrokeCol, 1.5f))
                using (GraphicsPath mehediCamBody = CreateShihabRoundedRect(new Rectangle(shihabRect.X + 1, shihabRect.Y + 4, 11, 10), 3))
                {
                    shihabCamPen.LineJoin = LineJoin.Round;
                    mehediG.DrawPath(shihabCamPen, mehediCamBody);
                    mehediG.DrawPolygon(shihabCamPen, new PointF[]
                    {
                        new PointF(shihabRect.X + 12, shihabRect.Y + 7.5f),
                        new PointF(shihabRect.X + 16.5f, shihabRect.Y + 5f),
                        new PointF(shihabRect.X + 16.5f, shihabRect.Y + 13f),
                        new PointF(shihabRect.X + 12, shihabRect.Y + 10.5f)
                    });
                }
                return;
            }

            if (shihabExt == "MP3" || shihabExt == "M4A" || shihabExt == "WAV" || shihabExt == "FLAC" || shihabExt == "AAC" || shihabExt == "OGG")
            {
                using (Pen shihabNotePen = new Pen(mehediStrokeCol, 1.5f))
                {
                    shihabNotePen.LineJoin = LineJoin.Round;
                    mehediG.DrawLine(shihabNotePen, shihabRect.X + 6, shihabRect.Y + 13, shihabRect.X + 6, shihabRect.Y + 4);
                    mehediG.DrawLine(shihabNotePen, shihabRect.X + 6, shihabRect.Y + 4, shihabRect.X + 14, shihabRect.Y + 3);
                    mehediG.DrawLine(shihabNotePen, shihabRect.X + 14, shihabRect.Y + 3, shihabRect.X + 14, shihabRect.Y + 12);
                    mehediG.DrawEllipse(shihabNotePen, shihabRect.X + 2, shihabRect.Y + 11, 4, 4);
                    mehediG.DrawEllipse(shihabNotePen, shihabRect.X + 10, shihabRect.Y + 10, 4, 4);
                }
                return;
            }

            if (shihabExt == "PNG" || shihabExt == "JPG" || shihabExt == "JPEG" || shihabExt == "WEBP" || shihabExt == "GIF" || shihabExt == "SVG" || shihabExt == "ICO")
            {
                using (Pen mehediImgPen = new Pen(mehediStrokeCol, 1.45f))
                using (SolidBrush shihabDot = new SolidBrush(mehediStrokeCol))
                using (GraphicsPath shihabFrame = CreateShihabRoundedRect(new Rectangle(shihabRect.X + 2, shihabRect.Y + 2, 14, 14), 3))
                {
                    mehediImgPen.LineJoin = LineJoin.Round;
                    mehediG.DrawPath(mehediImgPen, shihabFrame);
                    mehediG.FillEllipse(shihabDot, shihabRect.X + 5, shihabRect.Y + 5, 2.6f, 2.6f);
                    mehediG.DrawLines(mehediImgPen, new PointF[]
                    {
                        new PointF(shihabRect.X + 3, shihabRect.Y + 14),
                        new PointF(shihabRect.X + 8.5f, shihabRect.Y + 9.5f),
                        new PointF(shihabRect.X + 12, shihabRect.Y + 12.5f),
                        new PointF(shihabRect.X + 15, shihabRect.Y + 10.5f)
                    });
                }
                return;
            }

            if (shihabExt == "ZIP" || shihabExt == "RAR" || shihabExt == "7Z" || shihabExt == "GZ" || shihabExt == "TAR" || shihabExt == "ISO")
            {
                using (Pen shihabZipPen = new Pen(mehediStrokeCol, 1.45f))
                using (GraphicsPath mehediBox = CreateShihabRoundedRect(new Rectangle(shihabRect.X + 3, shihabRect.Y + 2, 12, 14), 2))
                {
                    mehediG.DrawPath(shihabZipPen, mehediBox);
                    mehediG.DrawLine(shihabZipPen, shihabRect.X + 9, shihabRect.Y + 3, shihabRect.X + 9, shihabRect.Y + 12);
                    mehediG.DrawLine(shihabZipPen, shihabRect.X + 7, shihabRect.Y + 5, shihabRect.X + 11, shihabRect.Y + 5);
                    mehediG.DrawLine(shihabZipPen, shihabRect.X + 7, shihabRect.Y + 8, shihabRect.X + 11, shihabRect.Y + 8);
                    mehediG.DrawLine(shihabZipPen, shihabRect.X + 7, shihabRect.Y + 11, shihabRect.X + 11, shihabRect.Y + 11);
                }
                return;
            }

            using (Pen mehediDocPen = new Pen(mehediStrokeCol, 1.45f))
            using (GraphicsPath shihabDoc = CreateShihabRoundedRect(new Rectangle(shihabRect.X + 3, shihabRect.Y + 2, 12, 14), 2))
            {
                mehediG.DrawPath(mehediDocPen, shihabDoc);
                mehediG.DrawLine(mehediDocPen, shihabRect.X + 6, shihabRect.Y + 7, shihabRect.X + 12, shihabRect.Y + 7);
                mehediG.DrawLine(mehediDocPen, shihabRect.X + 6, shihabRect.Y + 10, shihabRect.X + 12, shihabRect.Y + 10);
                mehediG.DrawLine(mehediDocPen, shihabRect.X + 6, shihabRect.Y + 13, shihabRect.X + 10, shihabRect.Y + 13);
            }
        }

        public static void DrawMehediTreeCategoryIcon(Graphics shihabG, Rectangle mehediRect, string shihabTag, string mehediLabel)
        {
            shihabG.SmoothingMode = SmoothingMode.AntiAlias;
            string shihabUpper = (shihabTag ?? "").ToUpperInvariant();
            string mehediNameUpper = (mehediLabel ?? "").ToUpperInvariant();
            Rectangle shihabIconBox = new Rectangle(mehediRect.X - 1, mehediRect.Y - 1, 18, 18);

            if (mehediNameUpper.Contains("COMPRESSED"))
            {
                DrawShihabFileTypeBadge(shihabG, shihabIconBox, "archive.zip");
                return;
            }
            if (mehediNameUpper.Contains("DOCUMENT"))
            {
                DrawShihabFileTypeBadge(shihabG, shihabIconBox, "readme.pdf");
                return;
            }
            if (mehediNameUpper.Contains("MUSIC"))
            {
                DrawShihabFileTypeBadge(shihabG, shihabIconBox, "track.mp3");
                return;
            }
            if (mehediNameUpper.Contains("PROGRAM") || mehediNameUpper.Contains("APP"))
            {
                DrawShihabFileTypeBadge(shihabG, shihabIconBox, "app.exe");
                return;
            }
            if (mehediNameUpper.Contains("VIDEO"))
            {
                DrawShihabFileTypeBadge(shihabG, shihabIconBox, "clip.mp4");
                return;
            }
            if (mehediNameUpper.Contains("IMAGE"))
            {
                DrawShihabFileTypeBadge(shihabG, shihabIconBox, "photo.png");
                return;
            }
            if (mehediNameUpper.Contains("GENERAL") || mehediNameUpper.Contains("OTHER"))
            {
                DrawShihabFileTypeBadge(shihabG, shihabIconBox, "file.dat");
                return;
            }
            if (shihabUpper == "GRABBER_PROJECTS")
            {
                using (Pen mehediGlobePen = new Pen(Color.FromArgb(162, 171, 198), 1.4f))
                {
                    shihabG.DrawEllipse(mehediGlobePen, mehediRect.X + 2, mehediRect.Y + 2, 12, 12);
                    shihabG.DrawLine(mehediGlobePen, mehediRect.X + 2, mehediRect.Y + 8, mehediRect.X + 14, mehediRect.Y + 8);
                }
                return;
            }

            DrawMehediMiniQueueFolder(shihabG, mehediRect);
        }
    }

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
            Font = new Font("Segoe UI", 9f);
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
                string mehediU = _shihabUrlBox.Text.Trim();
                bool isMagnet = mehediU.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase);
                Uri shihabParsed;
                if (!isMagnet && (!Uri.TryCreate(mehediU, UriKind.Absolute, out shihabParsed) ||
                    (shihabParsed.Scheme != "http" && shihabParsed.Scheme != "https" && shihabParsed.Scheme != "ftp" && shihabParsed.Scheme != "ftps")))
                {
                    MessageBox.Show(this, "Invalid URL entered. Please enter a valid http://, https://, ftp://, ftps://, or magnet:? address.", "MHS Download Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                ShihabEnteredUrl = mehediU;
                MehediEnteredReferer = _mehediRefererBox.Text.Trim();
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

    public class MehediDownloadFileInfoForm : Form
    {
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
            Size = new Size(640, 415);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            BackColor = ShihabThemePalette.GetMehediWindowBg(shihabDark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(shihabDark);
            Font = new Font("Segoe UI", 9f);
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, shihabDark);
            Icon = ShihabThemePalette.CreateShihabAppIcon();

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
                Font = new Font("Segoe UI Semibold", 9f),
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
                    if (Directory.Exists(shihabCurDir)) mehediSfd.InitialDirectory = shihabCurDir;
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

            Controls.AddRange(new Control[]
            {
                mehediUrlLbl, _shihabUrlBox, shihabCatLbl, _mehediCategoryCombo, _shihabSizeBadgeLabel,
                mehediSaveLbl, _shihabSaveAsBox, shihabBrowseBtn, _mehediRememberPathCheck,
                shihabDescLbl, _mehediDescBox, mehediQueueLbl, _shihabQueueCombo,
                _shihabAutoExtractCheck, shihabChkLbl, _shihabExpectedChecksumBox,
                _shihabPrivateIncognitoCheck, _shihabBypassDebridCheck,
                mehediLaterBtn, shihabStartBtn, mehediCancelBtn
            });

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
                if (IsDisposed) return;
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
            string shihabFullSave = _shihabSaveAsBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(shihabFullSave))
            {
                MessageBox.Show(this, "Please specify a valid Save As file path.", "MHS Download Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string mehediFolder = Path.GetDirectoryName(shihabFullSave);
            string shihabLeaf = ShihabFormatHelpers.SanitizeShihabFileName(Path.GetFileName(shihabFullSave));
            if (string.IsNullOrWhiteSpace(mehediFolder))
            {
                mehediFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            }
            Directory.CreateDirectory(mehediFolder);

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

    public class ShihabSegmentMapPanel : Panel
    {
        public ShihabDownloadItem MehediBoundItem { get; set; }
        public bool ShihabIsOverallBar { get; set; }

        public ShihabSegmentMapPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs shihabArgs)
        {
            Graphics mehediG = shihabArgs.Graphics;
            mehediG.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle shihabRect = ClientRectangle;
            using (SolidBrush mehediBg = new SolidBrush(Color.FromArgb(24, 27, 38)))
            {
                mehediG.FillRectangle(mehediBg, shihabRect);
            }

            if (MehediBoundItem != null)
            {
                if (ShihabIsOverallBar)
                {
                    double mehediPct = MehediBoundItem.GetMehediPercent();
                    int shihabFillW = (int)Math.Round((shihabRect.Width * mehediPct) / 100.0);
                    if (shihabFillW > 1)
                    {
                        using (LinearGradientBrush mehediGrad = new LinearGradientBrush(
                            new Rectangle(0, 0, Math.Max(2, shihabFillW), Math.Max(2, shihabRect.Height)),
                            Color.FromArgb(56, 189, 248),
                            Color.FromArgb(139, 92, 246),
                            0f))
                        {
                            mehediG.FillRectangle(mehediGrad, 0, 0, shihabFillW, shihabRect.Height);
                        }
                    }
                    string shihabText = string.Format("{0:0.0}%", mehediPct);
                    using (Font mehediFont = new Font("Segoe UI Semibold", 8.5f))
                    using (SolidBrush shihabWhite = new SolidBrush(Color.White))
                    {
                        SizeF mehediSz = mehediG.MeasureString(shihabText, mehediFont);
                        mehediG.DrawString(
                            shihabText,
                            mehediFont,
                            shihabWhite,
                            (shihabRect.Width - mehediSz.Width) / 2f,
                            (shihabRect.Height - mehediSz.Height) / 2f);
                    }
                }
                else if (MehediBoundItem.ShihabTotalBytes > 0 && MehediBoundItem.MehediSegments != null)
                {
                    long shihabTotal = MehediBoundItem.ShihabTotalBytes;
                    List<MehediSegmentChunk> mehediSnap = new List<MehediSegmentChunk>(MehediBoundItem.MehediSegments);
                    using (SolidBrush shihabFillBrush = new SolidBrush(Color.FromArgb(124, 92, 255)))
                    using (Pen mehediMarkerPen = new Pen(Color.FromArgb(56, 189, 248), 1.5f))
                    {
                        for (int shihabI = 0; shihabI < mehediSnap.Count; shihabI++)
                        {
                            MehediSegmentChunk mehediSeg = mehediSnap[shihabI];
                            if (mehediSeg == null) continue;
                            int shihabX0 = (int)((mehediSeg.MehediStartByte * (long)shihabRect.Width) / shihabTotal);
                            int mehediFillW = (int)((mehediSeg.MehediDownloadedBytes * (long)shihabRect.Width) / shihabTotal);
                            if (mehediSeg.MehediDownloadedBytes > 0 && mehediFillW < 2) mehediFillW = 2;
                            if (mehediFillW > 0)
                            {
                                mehediG.FillRectangle(shihabFillBrush, shihabX0, 1, mehediFillW, shihabRect.Height - 2);
                            }
                            mehediG.DrawLine(mehediMarkerPen, shihabX0, 0, shihabX0, shihabRect.Height);
                        }
                    }
                }
            }

            using (Pen shihabBorder = new Pen(Color.FromArgb(42, 48, 68), 1f))
            {
                mehediG.DrawRectangle(shihabBorder, 0, 0, shihabRect.Width - 1, shihabRect.Height - 1);
            }
        }
    }

    public class ShihabDownloadProgressForm : Form
    {
        private readonly ShihabDownloadItem _shihabItem;
        private readonly MehediDynamicSegmentEngine _mehediEngine;
        private readonly MehediAppConfiguration _shihabConfig;

        private Label _mehediUrlTitleLabel;
        private Label _shihabStatusVal;
        private Label _mehediFileSizeVal;
        private Label _shihabDownloadedVal;
        private Label _mehediRateVal;
        private Label _shihabEtaVal;
        private Label _mehediResumeVal;
        private ShihabSegmentMapPanel _shihabOverallBar;
        private ShihabSegmentMapPanel _mehediSegmentsMapBar;
        private ListView _shihabConnectionsList;
        private Button _mehediPauseResumeBtn;
        private Button _shihabHideDetailsBtn;
        private CheckBox _mehediItemSpeedCheck;
        private NumericUpDown _shihabItemSpeedNum;
        private CheckBox _mehediShowCompleteCheck;
        private CheckBox _shihabOpenOnDoneCheck;
        private CheckBox _mehediExitOnDoneCheck;
        private Timer _shihabUiRefreshTimer;
        private bool _mehediDetailsExpanded = true;

        public ShihabDownloadProgressForm(
            ShihabDownloadItem shihabItem,
            MehediDynamicSegmentEngine mehediEngine,
            MehediAppConfiguration shihabConfig)
        {
            _shihabItem = shihabItem;
            _mehediEngine = mehediEngine;
            _shihabConfig = shihabConfig;
            bool mehediDark = shihabConfig.ShihabDarkMode;

            Text = string.Format("{0:0.0}% — {1}", shihabItem.GetMehediPercent(), shihabItem.ShihabFileName);
            Size = new Size(610, 485);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = ShihabThemePalette.GetMehediWindowBg(mehediDark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(mehediDark);
            Font = new Font("Segoe UI", 9f);
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, mehediDark);
            Icon = ShihabThemePalette.CreateShihabAppIcon();

            ShihabDarkTabControl shihabTabs = new ShihabDarkTabControl
            {
                ShihabDarkMode = mehediDark,
                Location = new Point(10, 10),
                Size = new Size(574, 426)
            };

            TabPage mehediStatusTab = new TabPage("Download status")
            {
                BackColor = ShihabThemePalette.GetMehediWindowBg(mehediDark),
                ForeColor = ForeColor
            };
            TabPage shihabLimiterTab = new TabPage("Speed Limiter")
            {
                BackColor = ShihabThemePalette.GetMehediWindowBg(mehediDark),
                ForeColor = ForeColor
            };
            TabPage mehediCompletionTab = new TabPage("Options on completion")
            {
                BackColor = ShihabThemePalette.GetMehediWindowBg(mehediDark),
                ForeColor = ForeColor
            };

            BuildMehediStatusTab(mehediStatusTab, mehediDark);
            BuildShihabLimiterTab(shihabLimiterTab);
            BuildMehediCompletionTab(mehediCompletionTab);

            shihabTabs.TabPages.Add(mehediStatusTab);
            shihabTabs.TabPages.Add(shihabLimiterTab);
            shihabTabs.TabPages.Add(mehediCompletionTab);
            Controls.Add(shihabTabs);

            _shihabUiRefreshTimer = new Timer { Interval = 250 };
            _shihabUiRefreshTimer.Tick += delegate { RefreshShihabProgressView(); };
            _shihabUiRefreshTimer.Start();

            FormClosed += delegate
            {
                _shihabUiRefreshTimer.Stop();
                _shihabUiRefreshTimer.Dispose();
            };

            RefreshShihabProgressView();
        }

        private void BuildMehediStatusTab(TabPage shihabPage, bool mehediDark)
        {
            _mehediUrlTitleLabel = new Label
            {
                Text = _shihabItem.MehediUrl,
                Location = new Point(14, 12),
                Size = new Size(535, 20),
                AutoEllipsis = true,
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = ShihabThemePalette.GetShihabAccentBlue()
            };

            int shihabLeftX = 14;
            int mehediValX = 155;
            _shihabStatusVal = AddShihabMetricRow(shihabPage, "Status:", shihabLeftX, mehediValX, 38);
            _mehediFileSizeVal = AddShihabMetricRow(shihabPage, "File size:", shihabLeftX, mehediValX, 60);
            _shihabDownloadedVal = AddShihabMetricRow(shihabPage, "Downloaded:", shihabLeftX, mehediValX, 82);
            _mehediRateVal = AddShihabMetricRow(shihabPage, "Transfer rate:", shihabLeftX, mehediValX, 104);
            _shihabEtaVal = AddShihabMetricRow(shihabPage, "Time left:", shihabLeftX, mehediValX, 126);
            _mehediResumeVal = AddShihabMetricRow(shihabPage, "Resume capability:", shihabLeftX, mehediValX, 148);

            _shihabOverallBar = new ShihabSegmentMapPanel
            {
                MehediBoundItem = _shihabItem,
                ShihabIsOverallBar = true,
                Location = new Point(14, 174),
                Size = new Size(536, 22)
            };

            _shihabHideDetailsBtn = ShihabThemePalette.CreateMehediStyledButton("<< Hide details", ShihabThemePalette.GetShihabSurfaceBg(mehediDark), ForeColor, 125);
            _shihabHideDetailsBtn.Location = new Point(14, 204);
            _shihabHideDetailsBtn.Click += delegate
            {
                _mehediDetailsExpanded = !_mehediDetailsExpanded;
                _shihabHideDetailsBtn.Text = _mehediDetailsExpanded ? "<< Hide details" : "Show details >>";
                _mehediSegmentsMapBar.Visible = _mehediDetailsExpanded;
                _shihabConnectionsList.Visible = _mehediDetailsExpanded;
                Height = _mehediDetailsExpanded ? 485 : 318;
            };

            _mehediPauseResumeBtn = ShihabThemePalette.CreateMehediStyledButton("Pause", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 110);
            _mehediPauseResumeBtn.Location = new Point(318, 204);
            _mehediPauseResumeBtn.Click += delegate
            {
                if (_mehediEngine.IsMehediItemActive(_shihabItem.ShihabId))
                {
                    _mehediEngine.PauseShihabDownload(_shihabItem);
                }
                else if (_shihabItem.GetShihabStatusEnum() != ShihabDownloadStatus.ShihabCompleted)
                {
                    _mehediEngine.StartOrResumeMehediDownload(_shihabItem);
                }
                RefreshShihabProgressView();
            };

            Button shihabCancelBtn = ShihabThemePalette.CreateMehediStyledButton("Cancel", ShihabThemePalette.GetMehediAccentRed(), Color.White, 110);
            shihabCancelBtn.Location = new Point(440, 204);
            shihabCancelBtn.Click += delegate
            {
                if (_mehediEngine.IsMehediItemActive(_shihabItem.ShihabId))
                {
                    _mehediEngine.PauseShihabDownload(_shihabItem);
                }
                Close();
            };

            Label mehediConnHeader = new Label
            {
                Text = "Start positions and download progress by connections (Dynamic Segmentation):",
                Location = new Point(14, 244),
                AutoSize = true,
                ForeColor = ShihabThemePalette.GetMehediMutedText(mehediDark)
            };

            _mehediSegmentsMapBar = new ShihabSegmentMapPanel
            {
                MehediBoundItem = _shihabItem,
                ShihabIsOverallBar = false,
                Location = new Point(14, 265),
                Size = new Size(536, 18)
            };

            _shihabConnectionsList = new ShihabModernListView
            {
                Location = new Point(14, 289),
                Size = new Size(536, 98),
                MehediDarkMode = mehediDark,
                ShihabShowFileTypeBadges = false,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(mehediDark),
                ForeColor = ForeColor
            };
            _shihabConnectionsList.Columns.Add("N.", 50);
            _shihabConnectionsList.Columns.Add("Byte Range", 160);
            _shihabConnectionsList.Columns.Add("Downloaded", 115);
            _shihabConnectionsList.Columns.Add("Info", 190);

            shihabPage.Controls.AddRange(new Control[]
            {
                _mehediUrlTitleLabel, _shihabOverallBar, _shihabHideDetailsBtn,
                _mehediPauseResumeBtn, shihabCancelBtn, mehediConnHeader,
                _mehediSegmentsMapBar, _shihabConnectionsList
            });
        }

        private Label AddShihabMetricRow(TabPage mehediPage, string shihabTitle, int mehediX1, int shihabX2, int mehediY)
        {
            Label shihabKey = new Label { Text = shihabTitle, Location = new Point(mehediX1, mehediY), AutoSize = true };
            Label mehediVal = new Label
            {
                Text = "-",
                Location = new Point(shihabX2, mehediY),
                Size = new Size(390, 18),
                AutoEllipsis = true,
                Font = new Font("Segoe UI Semibold", 9f)
            };
            mehediPage.Controls.Add(shihabKey);
            mehediPage.Controls.Add(mehediVal);
            return mehediVal;
        }

        private void BuildShihabLimiterTab(TabPage mehediPage)
        {
            _mehediItemSpeedCheck = new CheckBox
            {
                Text = "Use Speed Limiter for this download",
                Location = new Point(24, 30),
                AutoSize = true,
                Checked = _shihabItem.ShihabUseItemSpeedLimit
            };

            Label shihabMaxLbl = new Label
            {
                Text = "Maximum download speed for this file (KBytes/sec):",
                Location = new Point(24, 68),
                AutoSize = true
            };

            _shihabItemSpeedNum = new NumericUpDown
            {
                Location = new Point(330, 65),
                Width = 110,
                Minimum = 8,
                Maximum = 1000000,
                Value = Math.Max(8, _shihabItem.MehediItemSpeedLimitKb)
            };

            _mehediItemSpeedCheck.CheckedChanged += delegate
            {
                _shihabItem.ShihabUseItemSpeedLimit = _mehediItemSpeedCheck.Checked;
            };
            _shihabItemSpeedNum.ValueChanged += delegate
            {
                _shihabItem.MehediItemSpeedLimitKb = (int)_shihabItemSpeedNum.Value;
            };

            Label mehediNote = new Label
            {
                Text = "Note: Per-download Speed Limiter throttles all active segments of this download smoothly using Shihab Mehedi token-bucket pacing.",
                Location = new Point(24, 112),
                Size = new Size(510, 50)
            };

            mehediPage.Controls.AddRange(new Control[] { _mehediItemSpeedCheck, shihabMaxLbl, _shihabItemSpeedNum, mehediNote });
        }

        private void BuildMehediCompletionTab(TabPage shihabPage)
        {
            _mehediShowCompleteCheck = new CheckBox
            {
                Text = "Show Download Complete dialog",
                Location = new Point(24, 30),
                AutoSize = true,
                Checked = _shihabItem.ShihabShowCompleteDialog
            };
            _shihabOpenOnDoneCheck = new CheckBox
            {
                Text = "Open downloaded file automatically when done",
                Location = new Point(24, 64),
                AutoSize = true,
                Checked = _shihabItem.ShihabOpenWhenComplete
            };
            _mehediExitOnDoneCheck = new CheckBox
            {
                Text = "Exit MHS Download Manager when this download completes",
                Location = new Point(24, 98),
                AutoSize = true,
                Checked = _shihabItem.MehediExitAppOnComplete
            };

            _mehediShowCompleteCheck.CheckedChanged += delegate { _shihabItem.ShihabShowCompleteDialog = _mehediShowCompleteCheck.Checked; };
            _shihabOpenOnDoneCheck.CheckedChanged += delegate { _shihabItem.ShihabOpenWhenComplete = _shihabOpenOnDoneCheck.Checked; };
            _mehediExitOnDoneCheck.CheckedChanged += delegate { _shihabItem.MehediExitAppOnComplete = _mehediExitOnDoneCheck.Checked; };

            shihabPage.Controls.AddRange(new Control[] { _mehediShowCompleteCheck, _shihabOpenOnDoneCheck, _mehediExitOnDoneCheck });
        }

        private void RefreshShihabProgressView()
        {
            if (IsDisposed) return;

            ShihabDownloadStatus mehediSt = _shihabItem.GetShihabStatusEnum();
            Text = string.Format("{0:0.0}% — {1}", _shihabItem.GetMehediPercent(), _shihabItem.ShihabFileName);
            _shihabStatusVal.Text = _shihabItem.ShihabStatusText;
            _mehediFileSizeVal.Text = ShihabFormatHelpers.FormatMehediBytes(_shihabItem.ShihabTotalBytes);
            _shihabDownloadedVal.Text = string.Format("{0} ({1:0.0}%)",
                ShihabFormatHelpers.FormatMehediBytes(_shihabItem.MehediDownloadedBytes),
                _shihabItem.GetMehediPercent());

            string shihabSpd = ShihabFormatHelpers.FormatShihabSpeed(_shihabItem.ShihabCurrentSpeedBps);
            if (!string.IsNullOrEmpty(shihabSpd) && (_shihabItem.ShihabUseItemSpeedLimit || _shihabConfig.MehediGlobalSpeedLimiterEnabled))
            {
                shihabSpd += " (Limited)";
            }
            bool mehediActive = _mehediEngine.IsMehediItemActive(_shihabItem.ShihabId);
            _mehediRateVal.Text = string.IsNullOrEmpty(shihabSpd) ? "-" : shihabSpd;
            _shihabEtaVal.Text = (mehediActive && _shihabItem.MehediEtaSeconds > 0)
                ? ShihabFormatHelpers.FormatMehediTimeLeft(_shihabItem.MehediEtaSeconds)
                : "-";
            string shihabGeoInfo = ShihabGeoIpLookup.FormatCountryBadgeText(_shihabItem.ShihabServerCountryCode, _shihabItem.ShihabServerIp);
            _mehediResumeVal.Text = (_shihabItem.ShihabResumeSupported ? "Yes" : "No") +
                (!string.IsNullOrEmpty(shihabGeoInfo) ? ("   •   Server: " + shihabGeoInfo) : "");
            _mehediResumeVal.ForeColor = _shihabItem.ShihabResumeSupported
                ? ShihabThemePalette.GetMehediAccentGreen()
                : ShihabThemePalette.GetShihabAccentAmber();

            _mehediPauseResumeBtn.Text = mehediActive ? "Pause" : "Start";
            _mehediPauseResumeBtn.Enabled = mehediSt != ShihabDownloadStatus.ShihabCompleted;

            _shihabOverallBar.Invalidate();
            _mehediSegmentsMapBar.Invalidate();

            List<MehediSegmentChunk> shihabSegs = _shihabItem.MehediSegments != null
                ? new List<MehediSegmentChunk>(_shihabItem.MehediSegments)
                : new List<MehediSegmentChunk>();

            _shihabConnectionsList.BeginUpdate();
            while (_shihabConnectionsList.Items.Count < shihabSegs.Count)
            {
                _shihabConnectionsList.Items.Add(new ListViewItem(new string[] { "", "", "", "" }));
            }
            while (_shihabConnectionsList.Items.Count > shihabSegs.Count)
            {
                _shihabConnectionsList.Items.RemoveAt(_shihabConnectionsList.Items.Count - 1);
            }
            for (int mehediI = 0; mehediI < shihabSegs.Count; mehediI++)
            {
                MehediSegmentChunk shihabSeg = shihabSegs[mehediI];
                ListViewItem mehediLvi = _shihabConnectionsList.Items[mehediI];
                mehediLvi.SubItems[0].Text = shihabSeg.ShihabSegmentIndex.ToString();
                mehediLvi.SubItems[1].Text = shihabSeg.ShihabEndByte >= 0
                    ? string.Format("{0} - {1}", ShihabFormatHelpers.FormatMehediBytes(shihabSeg.MehediStartByte), ShihabFormatHelpers.FormatMehediBytes(shihabSeg.ShihabEndByte))
                    : "Stream";
                mehediLvi.SubItems[2].Text = ShihabFormatHelpers.FormatMehediBytes(shihabSeg.MehediDownloadedBytes);
                mehediLvi.SubItems[3].Text = shihabSeg.ShihabSegmentState ?? "";
            }
            _shihabConnectionsList.EndUpdate();

            if (mehediSt == ShihabDownloadStatus.ShihabCompleted)
            {
                _shihabUiRefreshTimer.Stop();
                Close();
            }
        }
    }

    public class MehediDownloadCompleteForm : Form
    {
        public MehediDownloadCompleteForm(ShihabDownloadItem shihabItem, MehediAppConfiguration mehediConfig)
        {
            bool shihabDark = mehediConfig.ShihabDarkMode;
            Text = "Download complete — MHS Download Manager";
            Size = new Size(590, 285);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            BackColor = ShihabThemePalette.GetMehediWindowBg(shihabDark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(shihabDark);
            Font = new Font("Segoe UI", 9f);
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, shihabDark);
            Icon = ShihabThemePalette.CreateShihabAppIcon();

            string mehediFullFile = shihabItem.GetShihabFullTargetPath();

            Label shihabBanner = new Label
            {
                Text = "Download Complete — " + ShihabFormatHelpers.FormatMehediBytes(shihabItem.MehediDownloadedBytes),
                Location = new Point(18, 16),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 11f),
                ForeColor = ShihabThemePalette.GetMehediAccentGreen()
            };

            Label mehediAddrLbl = new Label { Text = "Address:", Location = new Point(18, 52), AutoSize = true };
            TextBox shihabAddrBox = new TextBox
            {
                Location = new Point(105, 49),
                Width = 450,
                ReadOnly = true,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark),
                ForeColor = ForeColor,
                Text = shihabItem.MehediUrl
            };

            Label shihabSavedLbl = new Label { Text = "Saved as:", Location = new Point(18, 88), AutoSize = true };
            TextBox mehediSavedBox = new TextBox
            {
                Location = new Point(105, 85),
                Width = 450,
                ReadOnly = true,
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(shihabDark),
                ForeColor = ForeColor,
                Text = mehediFullFile
            };

            Label shihabHashLbl = new Label
            {
                Text = "SHA-256: " + (string.IsNullOrEmpty(shihabItem.MehediSha256Hex) ? "-" : shihabItem.MehediSha256Hex),
                Location = new Point(18, 124),
                Size = new Size(540, 20),
                AutoEllipsis = true,
                ForeColor = ShihabThemePalette.GetMehediMutedText(shihabDark)
            };

            CheckBox mehediDontShow = new CheckBox
            {
                Text = "Don't show this dialog again",
                Location = new Point(18, 155),
                AutoSize = true
            };
            mehediDontShow.CheckedChanged += delegate
            {
                mehediConfig.ShihabShowDownloadCompleteDialog = !mehediDontShow.Checked;
            };

            Button shihabOpenBtn = ShihabThemePalette.CreateMehediStyledButton("Open", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 115);
            shihabOpenBtn.Location = new Point(18, 195);
            shihabOpenBtn.Click += delegate
            {
                try { if (File.Exists(mehediFullFile)) Process.Start(mehediFullFile); } catch { }
                Close();
            };

            Button mehediOpenWithBtn = ShihabThemePalette.CreateMehediStyledButton("Open With...", ShihabThemePalette.GetShihabSurfaceBg(shihabDark), ForeColor, 125);
            mehediOpenWithBtn.Location = new Point(145, 195);
            mehediOpenWithBtn.Click += delegate
            {
                try
                {
                    if (File.Exists(mehediFullFile))
                    {
                        Process.Start("rundll32.exe", "shell32.dll,OpenAs_RunDLL " + mehediFullFile);
                    }
                }
                catch { }
                Close();
            };

            Button shihabFolderBtn = ShihabThemePalette.CreateMehediStyledButton("Open Folder", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 130);
            shihabFolderBtn.Location = new Point(282, 195);
            shihabFolderBtn.Click += delegate
            {
                try
                {
                    if (File.Exists(mehediFullFile))
                    {
                        Process.Start("explorer.exe", "/select,\"" + mehediFullFile + "\"");
                    }
                    else if (Directory.Exists(shihabItem.MehediSaveDirectory))
                    {
                        Process.Start("explorer.exe", "\"" + shihabItem.MehediSaveDirectory + "\"");
                    }
                }
                catch { }
                Close();
            };

            Button mehediCloseBtn = ShihabThemePalette.CreateMehediStyledButton("Close", ShihabThemePalette.GetShihabSurfaceBg(shihabDark), ForeColor, 115);
            mehediCloseBtn.Location = new Point(440, 195);
            mehediCloseBtn.Click += delegate { Close(); };

            Controls.AddRange(new Control[]
            {
                shihabBanner, mehediAddrLbl, shihabAddrBox, shihabSavedLbl, mehediSavedBox,
                shihabHashLbl, mehediDontShow, shihabOpenBtn, mehediOpenWithBtn, shihabFolderBtn, mehediCloseBtn
            });
        }
    }

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
            Font = new Font("Segoe UI", 9f);
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
                if (string.IsNullOrEmpty(spec))
                {
                    string sha256 = ShihabChecksumVerifier.ComputeFileChecksumHex(fullFile, "SHA-256");
                    shihabItem.MehediSha256Hex = sha256;
                    shihabHashBox.Text = sha256;
                    MessageBox.Show(this, "Computed SHA-256:\r\n" + sha256, "Checksum Computed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    string algo;
                    string comp;
                    string err;
                    bool ok = ShihabChecksumVerifier.VerifyExpectedChecksum(fullFile, spec, out algo, out comp, out err);
                    shihabItem.ShihabComputedChecksum = algo + ":" + comp;
                    shihabItem.ShihabChecksumStatus = ok ? ("Verified (" + algo + ")") : ("Mismatch (" + algo + ")");
                    shihabHashBox.Text = comp + " [" + shihabItem.ShihabChecksumStatus + "]";
                    MessageBox.Show(this, ok ? ("Checksum VERIFIED (" + algo + "):\r\n" + comp) : err, "Checksum Verification", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                }
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
                string destDir;
                string err;
                if (ShihabSafeArchiveExtractor.ExtractArchiveSafely(fullFile, "Smart", false, shihabArchPassBox.Text, out destDir, out err))
                {
                    MessageBox.Show(this, "Archive extracted safely to:\r\n" + destDir, "Extract Archive", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(this, "Archive extraction failed:\r\n" + err, "Extract Archive", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            Button shihabSaveBtn = ShihabThemePalette.CreateMehediStyledButton("Save Changes", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 130);
            shihabSaveBtn.Location = new Point(335, 390);
            shihabSaveBtn.Click += delegate
            {
                if (!string.IsNullOrWhiteSpace(shihabUrlBox.Text))
                {
                    shihabItem.MehediUrl = shihabUrlBox.Text.Trim();
                }
                shihabItem.MehediReferer = shihabRefBox.Text.Trim();
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
            Font = new Font("Segoe UI", 9.2f);
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, shihabDark);

            _shihabRoundAuthorImg = ShihabThemePalette.CreateShihabCircularAuthorBitmap(128);

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
                using (Font shihabRoleFont = new Font("Segoe UI Semibold", 9.2f, FontStyle.Regular))
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
                        shihabRoleFont,
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
                Font = new Font("Segoe UI", 8.3f, FontStyle.Bold),
                ForeColor = shihabDark ? Color.FromArgb(77, 184, 255) : Color.FromArgb(29, 78, 216)
            };

            Label mehediDevNameLbl = new Label
            {
                Text = "Developer:",
                Location = new Point(18, 42),
                AutoSize = true,
                BackColor = mehediDevCard.MehediFillColor,
                Font = new Font("Segoe UI Semibold", 9.2f),
                ForeColor = ShihabThemePalette.GetMehediMutedText(shihabDark)
            };
            Label shihabDevNameVal = new Label
            {
                Text = "Mehedi Hasan Shihab  (Software & Systems Engineer)",
                UseMnemonic = false,
                Location = new Point(128, 42),
                AutoSize = true,
                BackColor = mehediDevCard.MehediFillColor,
                Font = new Font("Segoe UI", 9.6f, FontStyle.Bold),
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
                Font = new Font("Segoe UI", 8.2f),
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
            if (shihabDisposing && _shihabRoundAuthorImg != null)
            {
                _shihabRoundAuthorImg.Dispose();
                _shihabRoundAuthorImg = null;
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
                Font = new Font("Segoe UI Semibold", 9.2f)
            };
            mehediLink.LinkClicked += delegate
            {
                OpenMehediExternalUri(shihabTargetUri);
            };
            return mehediLink;
        }

        private static void OpenMehediExternalUri(string shihabUri)
        {
            try
            {
                Process.Start(new ProcessStartInfo(shihabUri) { UseShellExecute = true });
            }
            catch { }
        }
    }
}
