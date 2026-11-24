using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ShihabMehediIDM
{
    public sealed class ShihabDarkTabControl : TabControl
    {
        private const int WM_SETCURSOR = 0x0020;
        private const int HTCLIENT = 1;

        [DllImport("user32.dll")]
        private static extern IntPtr SetCursor(IntPtr hCursor);

        public bool ShihabDarkMode { get; set; }
        private int _mehediHoveredTabIndex = -1;

        public ShihabDarkTabControl()
        {
            ShihabDarkMode = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            ItemSize = new Size(102, 28);
            Padding = new Point(12, 5);
        }

        private void InvalidateTabIndex(int idx)
        {
            if (idx >= 0 && idx < TabCount)
            {
                Rectangle r = GetTabRect(idx);
                r.Inflate(4, 4);
                Invalidate(r);
            }
        }

        private int HitTestTabIndex(Point clientPoint)
        {
            if (!IsHandleCreated || TabCount <= 0) return -1;
            for (int shihabI = 0; shihabI < TabCount; shihabI++)
            {
                if (GetTabRect(shihabI).Contains(clientPoint))
                {
                    return shihabI;
                }
            }
            return -1;
        }

        private void UpdateHoverStateFromPoint(Point clientPoint, bool invalidateChanged)
        {
            int mehediHit = ClientRectangle.Contains(clientPoint) ? HitTestTabIndex(clientPoint) : -1;
            if (mehediHit != _mehediHoveredTabIndex)
            {
                int prev = _mehediHoveredTabIndex;
                _mehediHoveredTabIndex = mehediHit;
                if (invalidateChanged)
                {
                    InvalidateTabIndex(prev);
                    InvalidateTabIndex(_mehediHoveredTabIndex);
                }
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SETCURSOR && (m.LParam.ToInt64() & 0xFFFFL) == HTCLIENT)
            {
                if (m.WParam == Handle && HitTestTabIndex(PointToClient(Cursor.Position)) >= 0)
                {
                    SetCursor(Cursors.Hand.Handle);
                    m.Result = new IntPtr(1);
                    return;
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnMouseMove(MouseEventArgs shihabE)
        {
            base.OnMouseMove(shihabE);
            UpdateHoverStateFromPoint(shihabE.Location, true);
        }

        protected override void OnMouseLeave(EventArgs mehediE)
        {
            base.OnMouseLeave(mehediE);
            if (_mehediHoveredTabIndex != -1)
            {
                int prev = _mehediHoveredTabIndex;
                _mehediHoveredTabIndex = -1;
                InvalidateTabIndex(prev);
            }
        }

        // NOTE: Do NOT override OnMouseDown to re-hit-test GetTabRect() and set SelectedIndex.
        // Win32 SysTabControl32 handles WM_LBUTTONDOWN in DefWndProc BEFORE OnMouseDown fires;
        // on Multiline TabControls, SysTabControl32 swaps the selected tab's row to the bottom row
        // during DefWndProc, so re-hit-testing shihabE.Location against post-swap GetTabRect()
        // immediately overwrites SelectedIndex back onto the tab that shifted into the top row.
        protected override void OnSelectedIndexChanged(EventArgs mehediE)
        {
            base.OnSelectedIndexChanged(mehediE);
            if (IsHandleCreated)
            {
                UpdateHoverStateFromPoint(PointToClient(Cursor.Position), false);
            }
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
}

