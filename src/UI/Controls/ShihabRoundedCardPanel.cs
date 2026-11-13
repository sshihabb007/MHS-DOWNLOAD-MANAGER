using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShihabMehediIDM
{
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
}

