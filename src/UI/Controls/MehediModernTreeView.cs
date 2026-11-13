using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ShihabMehediIDM
{
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

        private void InvalidateNodeBand(TreeNode node)
        {
            if (node == null || !IsHandleCreated) return;
            try
            {
                Rectangle nb = node.Bounds;
                if (nb.Height > 0)
                {
                    Invalidate(new Rectangle(0, nb.Y, ClientSize.Width, nb.Height));
                }
            }
            catch { }
        }

        protected override void OnMouseMove(MouseEventArgs shihabE)
        {
            base.OnMouseMove(shihabE);
            TreeNode mehediHit = GetNodeAt(shihabE.Location);
            if (!ReferenceEquals(mehediHit, _mehediHoveredNode))
            {
                TreeNode prev = _mehediHoveredNode;
                _mehediHoveredNode = mehediHit;
                InvalidateNodeBand(prev);
                InvalidateNodeBand(_mehediHoveredNode);
            }
        }

        protected override void OnMouseLeave(EventArgs mehediE)
        {
            base.OnMouseLeave(mehediE);
            if (_mehediHoveredNode != null)
            {
                TreeNode prev = _mehediHoveredNode;
                _mehediHoveredNode = null;
                InvalidateNodeBand(prev);
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

            Font shihabNodeFont = mehediIsRoot
                ? ShihabThemePalette.UiBold94Font
                : ((shihabSelected || mehediIsRoot) ? ShihabThemePalette.UiBold89Font : ShihabThemePalette.UiRegular89Font);
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

