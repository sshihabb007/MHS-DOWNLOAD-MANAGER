using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ShihabMehediIDM
{
    internal sealed class ShihabTopRibbonButtonSpec
    {
        public string Id;
        public string LocalizationKey;
        public string DefaultText;
        public string DisplayText;
        public string ToolTipText;
        public bool Enabled;
        public bool HasSplitDropDown;
        public Action OnPrimaryClick;
        public Action<Control, Point> OnSplitDropDownClick;
        public Rectangle Bounds;
        public Rectangle SplitDropBounds;
    }

    internal static class ShihabTopRibbonIconRenderer
    {
        public static Bitmap CreateCachedVectorBitmap(int size, string iconId, bool enabled, bool darkMode)
        {
            int px = Math.Max(12, size);
            Bitmap bmp = new Bitmap(px, px, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                DrawVectorIcon(g, new Rectangle(0, 0, px, px), iconId, enabled, darkMode);
            }
            return bmp;
        }

        public static void DrawVectorIcon(Graphics g, Rectangle bounds, string iconId, bool enabled, bool darkMode)
        {
            if (g == null || bounds.Width < 12 || bounds.Height < 12) return;

            SmoothingMode prevSmooth = g.SmoothingMode;
            PixelOffsetMode prevOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float size = Math.Min(bounds.Width, bounds.Height);
            float ox = bounds.X + (bounds.Width - size) * 0.5f;
            float oy = bounds.Y + (bounds.Height - size) * 0.5f;
            float s = size / 36f;
            float strokeW = Math.Max(1.85f, 2.25f * s);

            RectangleF gradRect = new RectangleF(ox, oy, Math.Max(16f, size), Math.Max(16f, size));
            Color cTop, cMid, cBot;
            ResolveIconColors(iconId, enabled, darkMode, out cTop, out cMid, out cBot);

            using (LinearGradientBrush gradBrush = new LinearGradientBrush(gradRect, cTop, cBot, LinearGradientMode.Vertical))
            {
                ColorBlend blend = new ColorBlend(3);
                blend.Colors = new Color[] { cTop, cMid, cBot };
                blend.Positions = new float[] { 0f, 0.52f, 1f };
                gradBrush.InterpolationColors = blend;

                using (Pen pen = new Pen(gradBrush, strokeW))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;

                    if (iconId == "add_url")
                    {
                        DrawHollowPlusIcon(g, pen, ox, oy, s);
                    }
                    else if (iconId == "resume")
                    {
                        DrawCircleArrowDownIcon(g, pen, ox, oy, s);
                    }
                    else if (iconId == "stop")
                    {
                        DrawCircleStopSquareIcon(g, pen, ox, oy, s);
                    }
                    else if (iconId == "stop_all")
                    {
                        DrawRaisedHandIcon(g, pen, ox, oy, s);
                    }
                    else if (iconId == "delete")
                    {
                        DrawTrashCanIcon(g, pen, ox, oy, s, false);
                    }
                    else if (iconId == "delete_completed")
                    {
                        DrawTrashCanIcon(g, pen, ox, oy, s, true);
                    }
                    else if (iconId == "options")
                    {
                        DrawGearIcon(g, pen, ox, oy, s);
                    }
                    else if (iconId == "scheduler")
                    {
                        DrawAlarmClockIcon(g, pen, gradBrush, ox, oy, s);
                    }
                    else if (iconId == "start_queue")
                    {
                        DrawQueueCardsIcon(g, pen, ox, oy, s, true);
                    }
                    else if (iconId == "stop_queue")
                    {
                        DrawQueueCardsIcon(g, pen, ox, oy, s, false);
                    }
                    else if (iconId == "tell_friend")
                    {
                        DrawThumbsUpIcon(g, pen, ox, oy, s);
                    }
                }
            }

            g.SmoothingMode = prevSmooth;
            g.PixelOffsetMode = prevOffset;
        }

        private static void ResolveIconColors(string iconId, bool enabled, bool darkMode, out Color cTop, out Color cMid, out Color cBot)
        {
            if (!enabled)
            {
                Color dim = darkMode ? Color.FromArgb(124, 129, 139) : Color.FromArgb(156, 163, 175);
                cTop = dim;
                cMid = dim;
                cBot = dim;
                return;
            }

            if (iconId == "add_url")
            {
                cTop = darkMode ? Color.FromArgb(124, 232, 190) : Color.FromArgb(16, 185, 129);
                cMid = darkMode ? Color.FromArgb(94, 224, 200) : Color.FromArgb(20, 184, 166);
                cBot = darkMode ? Color.FromArgb(96, 196, 228) : Color.FromArgb(14, 165, 233);
            }
            else if (iconId == "resume")
            {
                cTop = darkMode ? Color.FromArgb(110, 231, 183) : Color.FromArgb(16, 185, 129);
                cMid = darkMode ? Color.FromArgb(56, 189, 248) : Color.FromArgb(14, 165, 233);
                cBot = darkMode ? Color.FromArgb(52, 211, 153) : Color.FromArgb(5, 150, 105);
            }
            else if (iconId == "stop")
            {
                cTop = darkMode ? Color.FromArgb(251, 113, 133) : Color.FromArgb(225, 29, 72);
                cMid = darkMode ? Color.FromArgb(244, 114, 182) : Color.FromArgb(219, 39, 119);
                cBot = darkMode ? Color.FromArgb(251, 191, 36) : Color.FromArgb(217, 119, 6);
            }
            else if (iconId == "stop_all")
            {
                cTop = darkMode ? Color.FromArgb(251, 113, 133) : Color.FromArgb(225, 29, 72);
                cMid = darkMode ? Color.FromArgb(249, 115, 22) : Color.FromArgb(234, 88, 12);
                cBot = darkMode ? Color.FromArgb(251, 191, 36) : Color.FromArgb(202, 138, 4);
            }
            else if (iconId == "delete")
            {
                cTop = darkMode ? Color.FromArgb(244, 63, 94) : Color.FromArgb(225, 29, 72);
                cMid = darkMode ? Color.FromArgb(217, 70, 239) : Color.FromArgb(168, 85, 247);
                cBot = darkMode ? Color.FromArgb(139, 92, 246) : Color.FromArgb(99, 102, 241);
            }
            else if (iconId == "delete_completed")
            {
                cTop = darkMode ? Color.FromArgb(236, 72, 153) : Color.FromArgb(219, 39, 119);
                cMid = darkMode ? Color.FromArgb(168, 85, 247) : Color.FromArgb(147, 51, 234);
                cBot = darkMode ? Color.FromArgb(129, 140, 248) : Color.FromArgb(79, 70, 229);
            }
            else if (iconId == "options")
            {
                cTop = darkMode ? Color.FromArgb(192, 132, 252) : Color.FromArgb(147, 51, 234);
                cMid = darkMode ? Color.FromArgb(167, 139, 250) : Color.FromArgb(124, 58, 237);
                cBot = darkMode ? Color.FromArgb(129, 140, 248) : Color.FromArgb(79, 70, 229);
            }
            else if (iconId == "scheduler")
            {
                cTop = darkMode ? Color.FromArgb(236, 72, 153) : Color.FromArgb(219, 39, 119);
                cMid = darkMode ? Color.FromArgb(168, 85, 247) : Color.FromArgb(139, 92, 246);
                cBot = darkMode ? Color.FromArgb(245, 180, 96) : Color.FromArgb(217, 119, 6);
            }
            else if (iconId == "start_queue")
            {
                cTop = darkMode ? Color.FromArgb(96, 165, 250) : Color.FromArgb(37, 99, 235);
                cMid = darkMode ? Color.FromArgb(94, 234, 212) : Color.FromArgb(13, 148, 136);
                cBot = darkMode ? Color.FromArgb(110, 231, 183) : Color.FromArgb(16, 185, 129);
            }
            else if (iconId == "stop_queue")
            {
                cTop = darkMode ? Color.FromArgb(129, 140, 248) : Color.FromArgb(79, 70, 229);
                cMid = darkMode ? Color.FromArgb(192, 132, 252) : Color.FromArgb(147, 51, 234);
                cBot = darkMode ? Color.FromArgb(244, 114, 182) : Color.FromArgb(219, 39, 119);
            }
            else
            {
                cTop = darkMode ? Color.FromArgb(167, 139, 250) : Color.FromArgb(124, 58, 237);
                cMid = darkMode ? Color.FromArgb(129, 140, 248) : Color.FromArgb(79, 70, 229);
                cBot = darkMode ? Color.FromArgb(147, 175, 250) : Color.FromArgb(59, 130, 246);
            }
        }

        private static PointF Pt(float ox, float oy, float s, float x, float y)
        {
            return new PointF(ox + x * s, oy + y * s);
        }

        private static void DrawHollowPlusIcon(Graphics g, Pen pen, float ox, float oy, float s)
        {
            float cx = 18f, cy = 18f, arm = 13.2f, halfT = 3.7f, r = 1.8f;
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddArc(ox + (cx - halfT) * s, oy + (cy - arm) * s, 2 * r * s, 2 * r * s, 180f, 90f);
                p.AddArc(ox + (cx + halfT - 2 * r) * s, oy + (cy - arm) * s, 2 * r * s, 2 * r * s, 270f, 90f);
                p.AddLine(Pt(ox, oy, s, cx + halfT, cy - arm + r), Pt(ox, oy, s, cx + halfT, cy - halfT));
                p.AddArc(ox + (cx + arm - 2 * r) * s, oy + (cy - halfT) * s, 2 * r * s, 2 * r * s, 270f, 90f);
                p.AddArc(ox + (cx + arm - 2 * r) * s, oy + (cy + halfT - 2 * r) * s, 2 * r * s, 2 * r * s, 0f, 90f);
                p.AddLine(Pt(ox, oy, s, cx + arm - r, cy + halfT), Pt(ox, oy, s, cx + halfT, cy + halfT));
                p.AddArc(ox + (cx + halfT - 2 * r) * s, oy + (cy + arm - 2 * r) * s, 2 * r * s, 2 * r * s, 0f, 90f);
                p.AddArc(ox + (cx - halfT) * s, oy + (cy + arm - 2 * r) * s, 2 * r * s, 2 * r * s, 90f, 90f);
                p.AddLine(Pt(ox, oy, s, cx - halfT, cy + arm - r), Pt(ox, oy, s, cx - halfT, cy + halfT));
                p.AddArc(ox + (cx - arm) * s, oy + (cy + halfT - 2 * r) * s, 2 * r * s, 2 * r * s, 90f, 90f);
                p.AddArc(ox + (cx - arm) * s, oy + (cy - halfT) * s, 2 * r * s, 2 * r * s, 180f, 90f);
                p.AddLine(Pt(ox, oy, s, cx - arm + r, cy - halfT), Pt(ox, oy, s, cx - halfT, cy - halfT));
                p.CloseFigure();
                g.DrawPath(pen, p);
            }
        }

        private static void DrawCircleArrowDownIcon(Graphics g, Pen pen, float ox, float oy, float s)
        {
            g.DrawEllipse(pen, ox + 4.5f * s, oy + 4.5f * s, 27f * s, 27f * s);
            g.DrawLine(pen, Pt(ox, oy, s, 18f, 11.2f), Pt(ox, oy, s, 18f, 24.4f));
            g.DrawLines(pen, new PointF[]
            {
                Pt(ox, oy, s, 12.8f, 19.2f),
                Pt(ox, oy, s, 18f, 24.6f),
                Pt(ox, oy, s, 23.2f, 19.2f)
            });
        }

        private static void DrawCircleStopSquareIcon(Graphics g, Pen pen, float ox, float oy, float s)
        {
            g.DrawEllipse(pen, ox + 4.5f * s, oy + 4.5f * s, 27f * s, 27f * s);
            Rectangle sq = new Rectangle(
                (int)Math.Round(ox + 13.4f * s),
                (int)Math.Round(oy + 13.4f * s),
                Math.Max(6, (int)Math.Round(9.2f * s)),
                Math.Max(6, (int)Math.Round(9.2f * s)));
            using (GraphicsPath sqPath = ShihabThemePalette.CreateShihabRoundedRect(sq, Math.Max(1, (int)Math.Round(1.6f * s))))
            {
                g.DrawPath(pen, sqPath);
            }
        }

        private static void DrawRaisedHandIcon(Graphics g, Pen pen, float ox, float oy, float s)
        {
            using (GraphicsPath hand = new GraphicsPath())
            {
                hand.AddLine(Pt(ox, oy, s, 8.2f, 22.5f), Pt(ox, oy, s, 8.2f, 16.8f));
                hand.AddArc(ox + 8.2f * s, oy + 15.0f * s, 3.4f * s, 3.4f * s, 180f, 180f);
                hand.AddLine(Pt(ox, oy, s, 11.6f, 16.8f), Pt(ox, oy, s, 11.6f, 20.0f));

                hand.AddLine(Pt(ox, oy, s, 11.6f, 20.0f), Pt(ox, oy, s, 11.6f, 8.8f));
                hand.AddArc(ox + 11.6f * s, oy + 6.8f * s, 3.8f * s, 3.8f * s, 180f, 180f);
                hand.AddLine(Pt(ox, oy, s, 15.4f, 8.8f), Pt(ox, oy, s, 15.4f, 17.6f));

                hand.AddLine(Pt(ox, oy, s, 15.4f, 17.6f), Pt(ox, oy, s, 15.4f, 6.8f));
                hand.AddArc(ox + 15.4f * s, oy + 4.8f * s, 3.8f * s, 3.8f * s, 180f, 180f);
                hand.AddLine(Pt(ox, oy, s, 19.2f, 6.8f), Pt(ox, oy, s, 19.2f, 17.6f));

                hand.AddLine(Pt(ox, oy, s, 19.2f, 17.6f), Pt(ox, oy, s, 19.2f, 8.4f));
                hand.AddArc(ox + 19.2f * s, oy + 6.4f * s, 3.8f * s, 3.8f * s, 180f, 180f);
                hand.AddLine(Pt(ox, oy, s, 23.0f, 8.4f), Pt(ox, oy, s, 23.0f, 18.4f));

                hand.AddLine(Pt(ox, oy, s, 23.0f, 18.4f), Pt(ox, oy, s, 23.0f, 11.8f));
                hand.AddArc(ox + 23.0f * s, oy + 9.8f * s, 3.6f * s, 3.6f * s, 180f, 180f);
                hand.AddLine(Pt(ox, oy, s, 26.6f, 11.8f), Pt(ox, oy, s, 26.6f, 23.0f));
                hand.AddArc(ox + 8.2f * s, oy + 16.2f * s, 18.4f * s, 15.0f * s, 0f, 180f);
                hand.CloseFigure();
                g.DrawPath(pen, hand);
            }
        }

        private static void DrawTrashCanIcon(Graphics g, Pen pen, float ox, float oy, float s, bool withCheckmark)
        {
            using (GraphicsPath handle = new GraphicsPath())
            {
                handle.AddArc(ox + 14.5f * s, oy + 5.0f * s, 7.0f * s, 4.4f * s, 180f, 180f);
                g.DrawPath(pen, handle);
            }

            Rectangle lidRect = new Rectangle(
                (int)Math.Round(ox + 8.0f * s),
                (int)Math.Round(oy + 8.0f * s),
                Math.Max(10, (int)Math.Round(20.0f * s)),
                Math.Max(3, (int)Math.Round(3.6f * s)));
            using (GraphicsPath lid = ShihabThemePalette.CreateShihabRoundedRect(lidRect, Math.Max(1, (int)Math.Round(1.5f * s))))
            {
                g.DrawPath(pen, lid);
            }

            using (GraphicsPath bin = new GraphicsPath())
            {
                bin.AddLine(Pt(ox, oy, s, 10.2f, 11.8f), Pt(ox, oy, s, 11.5f, 28.4f));
                bin.AddArc(ox + 11.5f * s, oy + 26.6f * s, 4.0f * s, 4.0f * s, 180f, -90f);
                bin.AddLine(Pt(ox, oy, s, 13.5f, 30.6f), Pt(ox, oy, s, 22.5f, 30.6f));
                bin.AddArc(ox + 20.5f * s, oy + 26.6f * s, 4.0f * s, 4.0f * s, 90f, -90f);
                bin.AddLine(Pt(ox, oy, s, 24.5f, 28.4f), Pt(ox, oy, s, 25.8f, 11.8f));
                g.DrawPath(pen, bin);
            }

            if (withCheckmark)
            {
                g.DrawLines(pen, new PointF[]
                {
                    Pt(ox, oy, s, 13.8f, 21.0f),
                    Pt(ox, oy, s, 16.9f, 24.2f),
                    Pt(ox, oy, s, 22.4f, 17.8f)
                });
            }
            else
            {
                g.DrawLine(pen, Pt(ox, oy, s, 15.8f, 16.0f), Pt(ox, oy, s, 15.8f, 25.4f));
                g.DrawLine(pen, Pt(ox, oy, s, 20.2f, 16.0f), Pt(ox, oy, s, 20.2f, 25.4f));
            }
        }

        private static void DrawGearIcon(Graphics g, Pen pen, float ox, float oy, float s)
        {
            int teeth = 8;
            float cx = ox + 18f * s;
            float cy = oy + 18f * s;
            float rOuter = 13.6f * s;
            float rInner = 10.2f * s;
            List<PointF> pts = new List<PointF>(teeth * 4);
            double step = (Math.PI * 2.0) / teeth;

            for (int i = 0; i < teeth; i++)
            {
                double baseAng = i * step - Math.PI / 2.0;
                double a0 = baseAng - step * 0.24;
                double a1 = baseAng - step * 0.14;
                double a2 = baseAng + step * 0.14;
                double a3 = baseAng + step * 0.24;

                pts.Add(new PointF((float)(cx + Math.Cos(a0) * rInner), (float)(cy + Math.Sin(a0) * rInner)));
                pts.Add(new PointF((float)(cx + Math.Cos(a1) * rOuter), (float)(cy + Math.Sin(a1) * rOuter)));
                pts.Add(new PointF((float)(cx + Math.Cos(a2) * rOuter), (float)(cy + Math.Sin(a2) * rOuter)));
                pts.Add(new PointF((float)(cx + Math.Cos(a3) * rInner), (float)(cy + Math.Sin(a3) * rInner)));
            }

            using (GraphicsPath gear = new GraphicsPath())
            {
                gear.AddPolygon(pts.ToArray());
                g.DrawPath(pen, gear);
            }
            g.DrawEllipse(pen, cx - 5.0f * s, cy - 5.0f * s, 10.0f * s, 10.0f * s);
        }

        private static void DrawAlarmClockIcon(Graphics g, Pen pen, Brush dotBrush, float ox, float oy, float s)
        {
            g.DrawArc(pen, ox + 5.5f * s, oy + 5.0f * s, 8.5f * s, 8.5f * s, 150f, 175f);
            g.DrawArc(pen, ox + 22.0f * s, oy + 5.0f * s, 8.5f * s, 8.5f * s, 215f, 175f);
            g.DrawEllipse(pen, ox + 6.5f * s, oy + 7.5f * s, 23.0f * s, 23.0f * s);
            g.DrawLine(pen, Pt(ox, oy, s, 11.2f, 28.8f), Pt(ox, oy, s, 8.8f, 31.6f));
            g.DrawLine(pen, Pt(ox, oy, s, 24.8f, 28.8f), Pt(ox, oy, s, 27.2f, 31.6f));

            float d = Math.Max(1.6f, 2.0f * s);
            g.FillEllipse(dotBrush, ox + 18f * s - d * 0.5f, oy + 10.6f * s - d * 0.5f, d, d);
            g.FillEllipse(dotBrush, ox + 18f * s - d * 0.5f, oy + 27.4f * s - d * 0.5f, d, d);
            g.FillEllipse(dotBrush, ox + 9.6f * s - d * 0.5f, oy + 19.0f * s - d * 0.5f, d, d);
            g.FillEllipse(dotBrush, ox + 26.4f * s - d * 0.5f, oy + 19.0f * s - d * 0.5f, d, d);

            g.DrawLines(pen, new PointF[]
            {
                Pt(ox, oy, s, 14.5f, 18.8f),
                Pt(ox, oy, s, 17.6f, 21.6f),
                Pt(ox, oy, s, 22.4f, 15.6f)
            });
        }

        private static void DrawQueueCardsIcon(Graphics g, Pen pen, float ox, float oy, float s, bool startArrow)
        {
            using (GraphicsPath backCard = new GraphicsPath())
            {
                backCard.AddLine(Pt(ox, oy, s, 9.8f, 23.2f), Pt(ox, oy, s, 7.6f, 23.2f));
                backCard.AddArc(ox + 6.0f * s, oy + 20.0f * s, 3.2f * s, 3.2f * s, 90f, 90f);
                backCard.AddLine(Pt(ox, oy, s, 6.0f, 21.6f), Pt(ox, oy, s, 6.0f, 7.6f));
                backCard.AddArc(ox + 6.0f * s, oy + 6.0f * s, 3.2f * s, 3.2f * s, 180f, 90f);
                backCard.AddLine(Pt(ox, oy, s, 7.6f, 6.0f), Pt(ox, oy, s, 21.2f, 6.0f));
                backCard.AddArc(ox + 19.6f * s, oy + 6.0f * s, 3.2f * s, 3.2f * s, 270f, 90f);
                backCard.AddLine(Pt(ox, oy, s, 22.8f, 7.6f), Pt(ox, oy, s, 22.8f, 9.6f));
                g.DrawPath(pen, backCard);
            }

            using (GraphicsPath frontCard = new GraphicsPath())
            {
                frontCard.AddLine(Pt(ox, oy, s, 18.2f, 27.6f), Pt(ox, oy, s, 12.2f, 27.6f));
                frontCard.AddArc(ox + 10.4f * s, oy + 24.0f * s, 3.6f * s, 3.6f * s, 90f, 90f);
                frontCard.AddLine(Pt(ox, oy, s, 10.4f, 25.8f), Pt(ox, oy, s, 10.4f, 12.0f));
                frontCard.AddArc(ox + 10.4f * s, oy + 10.2f * s, 3.6f * s, 3.6f * s, 180f, 90f);
                frontCard.AddLine(Pt(ox, oy, s, 12.2f, 10.2f), Pt(ox, oy, s, 25.6f, 10.2f));
                frontCard.AddArc(ox + 23.8f * s, oy + 10.2f * s, 3.6f * s, 3.6f * s, 270f, 90f);
                frontCard.AddLine(Pt(ox, oy, s, 27.4f, 12.0f), Pt(ox, oy, s, 27.4f, 17.6f));
                g.DrawPath(pen, frontCard);
            }

            if (startArrow)
            {
                g.DrawLine(pen, Pt(ox, oy, s, 25.0f, 18.2f), Pt(ox, oy, s, 25.0f, 30.2f));
                g.DrawLines(pen, new PointF[]
                {
                    Pt(ox, oy, s, 20.6f, 25.8f),
                    Pt(ox, oy, s, 25.0f, 30.4f),
                    Pt(ox, oy, s, 29.4f, 25.8f)
                });
            }
            else
            {
                Rectangle stopSq = new Rectangle(
                    (int)Math.Round(ox + 20.4f * s),
                    (int)Math.Round(oy + 21.0f * s),
                    Math.Max(6, (int)Math.Round(8.8f * s)),
                    Math.Max(6, (int)Math.Round(8.8f * s)));
                using (GraphicsPath sq = ShihabThemePalette.CreateShihabRoundedRect(stopSq, Math.Max(1, (int)Math.Round(1.4f * s))))
                {
                    g.DrawPath(pen, sq);
                }
            }
        }

        private static void DrawThumbsUpIcon(Graphics g, Pen pen, float ox, float oy, float s)
        {
            Rectangle cuffRect = new Rectangle(
                (int)Math.Round(ox + 23.4f * s),
                (int)Math.Round(oy + 15.2f * s),
                Math.Max(4, (int)Math.Round(5.2f * s)),
                Math.Max(8, (int)Math.Round(13.8f * s)));
            using (GraphicsPath cuff = ShihabThemePalette.CreateShihabRoundedRect(cuffRect, Math.Max(1, (int)Math.Round(1.4f * s))))
            {
                g.DrawPath(pen, cuff);
            }

            using (GraphicsPath hand = new GraphicsPath())
            {
                hand.AddLine(Pt(ox, oy, s, 23.2f, 16.2f), Pt(ox, oy, s, 20.4f, 13.8f));
                hand.AddLine(Pt(ox, oy, s, 20.4f, 13.8f), Pt(ox, oy, s, 20.0f, 8.2f));
                hand.AddArc(ox + 16.4f * s, oy + 6.4f * s, 3.8f * s, 3.8f * s, 0f, -180f);
                hand.AddLine(Pt(ox, oy, s, 16.4f, 8.2f), Pt(ox, oy, s, 15.4f, 14.6f));
                hand.AddLine(Pt(ox, oy, s, 15.4f, 14.6f), Pt(ox, oy, s, 10.6f, 14.6f));

                hand.AddArc(ox + 8.6f * s, oy + 14.6f * s, 3.6f * s, 3.5f * s, 270f, -180f);
                hand.AddArc(ox + 8.2f * s, oy + 18.1f * s, 3.6f * s, 3.5f * s, 270f, -180f);
                hand.AddArc(ox + 8.6f * s, oy + 21.6f * s, 3.6f * s, 3.5f * s, 270f, -180f);
                hand.AddArc(ox + 9.4f * s, oy + 25.1f * s, 3.6f * s, 3.5f * s, 270f, -180f);

                hand.AddLine(Pt(ox, oy, s, 11.2f, 28.6f), Pt(ox, oy, s, 23.2f, 28.4f));
                g.DrawPath(pen, hand);
            }
        }
    }

    internal sealed class ShihabTopIconRibbonControl : Control
    {
        public bool ShihabDarkMode { get; set; }
        public bool CompactMode { get; set; }
        public int UiScalePercent { get; set; }

        private readonly List<ShihabTopRibbonButtonSpec> _items = new List<ShihabTopRibbonButtonSpec>();
        private readonly ToolTip _toolTip = new ToolTip();
        private readonly Dictionary<string, Bitmap> _iconBitmapCache = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
        private Font _cachedLabelFont;
        private float _cachedLabelFontPt;

        private int _hoveredIndex = -1;
        private bool _hoveredSplitZone = false;
        private int _pressedIndex = -1;
        private bool _pressedSplitZone = false;
        private int _lastTipIndex = -1;

        public ShihabTopIconRibbonControl()
        {
            ShihabDarkMode = true;
            CompactMode = false;
            UiScalePercent = 100;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 72;
            _toolTip.InitialDelay = 350;
            _toolTip.ReshowDelay = 150;
        }

        public List<ShihabTopRibbonButtonSpec> Items
        {
            get { return _items; }
        }

        public void UpdateMetricsForScale()
        {
            float scale = Math.Max(80, Math.Min(150, UiScalePercent > 0 ? UiScalePercent : 100)) / 100f;
            Height = CompactMode ? (int)Math.Round(52 * scale) : (int)Math.Round(72 * scale);
            ClearIconBitmapCache();
            RecalculateItemBounds();
            Invalidate();
        }

        public void SetButtonEnabled(string id, bool enabled)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    if (_items[i].Enabled != enabled)
                    {
                        _items[i].Enabled = enabled;
                        InvalidateButtonIndex(i);
                    }
                    return;
                }
            }
        }

        private void InvalidateButtonIndex(int idx)
        {
            if (idx >= 0 && idx < _items.Count)
            {
                Invalidate(Rectangle.Inflate(_items[idx].Bounds, 2, 2));
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalculateItemBounds();
        }

        public void RecalculateItemBounds()
        {
            if (_items.Count == 0) return;
            float scale = Math.Max(80, Math.Min(150, UiScalePercent > 0 ? UiScalePercent : 100)) / 100f;
            int availW = Math.Max(320, ClientSize.Width - 16);
            int maxSlotW = (int)Math.Round(102 * scale);
            int minSlotW = 62;
            bool wideMode = availW >= (int)Math.Round(1140 * scale);
            int longLabelExtra = wideMode ? (int)Math.Round(18 * scale) : 0;

            int totalExtra = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].HasSplitDropDown) totalExtra += (int)Math.Round(12 * scale);
                if (wideMode && string.Equals(_items[i].Id, "delete_completed", StringComparison.OrdinalIgnoreCase))
                {
                    totalExtra += longLabelExtra;
                }
            }

            int baseSlotW = Math.Max(minSlotW, Math.Min(maxSlotW, (availW - totalExtra - _items.Count) / _items.Count));
            int curX = 8;
            int btnTop = 3;
            int btnH = Math.Max(36, Height - 6);

            for (int i = 0; i < _items.Count; i++)
            {
                ShihabTopRibbonButtonSpec item = _items[i];
                int splitW = item.HasSplitDropDown ? (int)Math.Round(15 * scale) : 0;
                int w = baseSlotW + (item.HasSplitDropDown ? (int)Math.Round(12 * scale) : 0);
                if (wideMode && string.Equals(item.Id, "delete_completed", StringComparison.OrdinalIgnoreCase))
                {
                    w += longLabelExtra;
                }
                item.Bounds = new Rectangle(curX, btnTop, w, btnH);
                item.SplitDropBounds = item.HasSplitDropDown
                    ? new Rectangle(curX + w - splitW - 2, btnTop + 4, splitW + 2, Math.Max(24, btnH - 8))
                    : Rectangle.Empty;
                curX += w + 1;
            }
        }

        private int HitTestItem(Point pt, out bool inSplitZone)
        {
            inSplitZone = false;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Bounds.Contains(pt))
                {
                    if (_items[i].HasSplitDropDown && _items[i].SplitDropBounds.Contains(pt))
                    {
                        inSplitZone = true;
                    }
                    return i;
                }
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool split;
            int idx = HitTestItem(e.Location, out split);
            if (idx != _hoveredIndex || split != _hoveredSplitZone)
            {
                int prevHovered = _hoveredIndex;
                _hoveredIndex = idx;
                _hoveredSplitZone = split;
                Cursor nextCursor = (idx >= 0 && _items[idx].Enabled) ? Cursors.Hand : Cursors.Default;
                if (Cursor != nextCursor) Cursor = nextCursor;

                if (idx != _lastTipIndex)
                {
                    _lastTipIndex = idx;
                    if (idx >= 0 && !string.IsNullOrEmpty(_items[idx].ToolTipText))
                    {
                        _toolTip.SetToolTip(this, _items[idx].ToolTipText);
                    }
                    else
                    {
                        _toolTip.SetToolTip(this, "");
                    }
                }
                if (prevHovered != idx)
                {
                    InvalidateButtonIndex(prevHovered);
                    InvalidateButtonIndex(idx);
                }
                else
                {
                    InvalidateButtonIndex(idx);
                }
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredIndex != -1 || _pressedIndex != -1)
            {
                int prevH = _hoveredIndex;
                int prevP = _pressedIndex;
                _hoveredIndex = -1;
                _hoveredSplitZone = false;
                _pressedIndex = -1;
                _pressedSplitZone = false;
                _lastTipIndex = -1;
                Cursor = Cursors.Default;
                InvalidateButtonIndex(prevH);
                if (prevP != prevH) InvalidateButtonIndex(prevP);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            bool split;
            int idx = HitTestItem(e.Location, out split);
            if (idx >= 0 && _items[idx].Enabled)
            {
                int prevPressed = _pressedIndex;
                _pressedIndex = idx;
                _pressedSplitZone = split;
                InvalidateButtonIndex(prevPressed);
                InvalidateButtonIndex(idx);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            int prevPressed = _pressedIndex;
            bool prevSplit = _pressedSplitZone;
            _pressedIndex = -1;
            _pressedSplitZone = false;
            InvalidateButtonIndex(prevPressed);

            bool nowSplit;
            int nowIdx = HitTestItem(e.Location, out nowSplit);
            if (prevPressed >= 0 && nowIdx == prevPressed && _items[nowIdx].Enabled)
            {
                ShihabTopRibbonButtonSpec item = _items[nowIdx];
                if (item.HasSplitDropDown && prevSplit && nowSplit && item.OnSplitDropDownClick != null)
                {
                    item.OnSplitDropDownClick(this, new Point(item.Bounds.Left, item.Bounds.Bottom));
                }
                else if (item.OnPrimaryClick != null)
                {
                    item.OnPrimaryClick();
                }
            }
        }

        private Bitmap GetOrCreateCachedIconBitmap(string iconId, int iconSz, bool enabled, bool darkMode)
        {
            string key = string.Format("{0}|{1}|{2}|{3}", iconId, iconSz, enabled ? 1 : 0, darkMode ? 1 : 0);
            Bitmap bmp;
            if (!_iconBitmapCache.TryGetValue(key, out bmp))
            {
                if (_iconBitmapCache.Count > 64) ClearIconBitmapCache();
                bmp = ShihabTopRibbonIconRenderer.CreateCachedVectorBitmap(iconSz, iconId, enabled, darkMode);
                _iconBitmapCache[key] = bmp;
            }
            return bmp;
        }

        private void ClearIconBitmapCache()
        {
            foreach (KeyValuePair<string, Bitmap> kv in _iconBitmapCache)
            {
                if (kv.Value != null) kv.Value.Dispose();
            }
            _iconBitmapCache.Clear();
        }

        private Font GetOrCreateLabelFont(float fontPt)
        {
            if (_cachedLabelFont == null || Math.Abs(_cachedLabelFontPt - fontPt) > 0.05f)
            {
                if (_cachedLabelFont != null) _cachedLabelFont.Dispose();
                _cachedLabelFontPt = fontPt;
                _cachedLabelFont = new Font("Segoe UI", fontPt, FontStyle.Regular);
            }
            return _cachedLabelFont;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Color barBg = ShihabDarkMode ? Color.FromArgb(43, 45, 51) : Color.FromArgb(241, 245, 249);
            Color botLine = ShihabDarkMode ? Color.FromArgb(62, 66, 76) : Color.FromArgb(203, 213, 225);

            using (SolidBrush bgBr = new SolidBrush(barBg))
            using (Pen botPen = new Pen(botLine, 1f))
            {
                g.FillRectangle(bgBr, e.ClipRectangle);
                g.DrawLine(botPen, 0, Height - 1, Width, Height - 1);
            }

            if (_items.Count == 0) return;
            if (_items[0].Bounds.Width == 0) RecalculateItemBounds();

            float scale = Math.Max(80, Math.Min(150, UiScalePercent > 0 ? UiScalePercent : 100)) / 100f;
            int iconSz = CompactMode ? (int)Math.Round(24 * scale) : (int)Math.Round(35 * scale);
            float fontPt = (CompactMode ? 8.0f : 8.6f) * scale;
            Font lblFont = GetOrCreateLabelFont(fontPt);
            Rectangle clip = e.ClipRectangle;

            for (int i = 0; i < _items.Count; i++)
            {
                ShihabTopRibbonButtonSpec item = _items[i];
                Rectangle r = item.Bounds;
                if (!clip.IsEmpty && !Rectangle.Inflate(r, 2, 2).IntersectsWith(clip)) continue;

                bool hot = (i == _hoveredIndex) && item.Enabled;
                bool pressed = (i == _pressedIndex) && item.Enabled;

                if (hot || pressed)
                {
                    Color hoverFill = ShihabDarkMode
                        ? (pressed ? Color.FromArgb(56, 60, 70) : Color.FromArgb(52, 56, 65))
                        : (pressed ? Color.FromArgb(219, 228, 243) : Color.FromArgb(228, 235, 247));
                    Color hoverEdge = ShihabDarkMode
                        ? Color.FromArgb(78, 84, 98)
                        : Color.FromArgb(186, 198, 218);

                    using (GraphicsPath hp = ShihabThemePalette.CreateShihabRoundedRect(r, 6))
                    using (SolidBrush hBr = new SolidBrush(hoverFill))
                    using (Pen hPen = new Pen(hoverEdge, 1f))
                    {
                        g.FillPath(hBr, hp);
                        g.DrawPath(hPen, hp);
                    }

                    if (item.HasSplitDropDown && !item.SplitDropBounds.IsEmpty)
                    {
                        Color sepCol = ShihabDarkMode ? Color.FromArgb(78, 84, 98) : Color.FromArgb(186, 198, 218);
                        using (Pen sepPen = new Pen(sepCol, 1f))
                        {
                            g.DrawLine(sepPen, item.SplitDropBounds.Left, r.Top + 6, item.SplitDropBounds.Left, r.Bottom - 6);
                        }
                    }
                }

                int contentRight = item.HasSplitDropDown ? (r.Right - item.SplitDropBounds.Width + 2) : r.Right;
                int contentW = Math.Max(24, contentRight - r.Left);
                int iconX = r.Left + (contentW - iconSz) / 2;
                int iconY = r.Top + (CompactMode ? 3 : 5);
                Rectangle iconRect = new Rectangle(iconX, iconY, iconSz, iconSz);

                Bitmap cachedBmp = GetOrCreateCachedIconBitmap(item.Id, iconSz, item.Enabled, ShihabDarkMode);
                if (cachedBmp != null)
                {
                    g.DrawImageUnscaled(cachedBmp, iconRect.Location);
                }

                if (item.HasSplitDropDown && !item.SplitDropBounds.IsEmpty)
                {
                    Color arrowCol = item.Enabled
                        ? (ShihabDarkMode
                            ? ((hot && _hoveredSplitZone) ? Color.FromArgb(236, 240, 248) : Color.FromArgb(150, 156, 168))
                            : Color.FromArgb(71, 85, 105))
                        : (ShihabDarkMode ? Color.FromArgb(100, 105, 114) : Color.FromArgb(156, 163, 175));
                    int ax = item.SplitDropBounds.Left + item.SplitDropBounds.Width / 2;
                    int ay = iconRect.Top + iconRect.Height / 2 + 2;
                    using (SolidBrush arrBr = new SolidBrush(arrowCol))
                    {
                        g.FillPolygon(arrBr, new PointF[]
                        {
                            new PointF(ax - 3.6f, ay - 2.0f),
                            new PointF(ax + 3.6f, ay - 2.0f),
                            new PointF(ax, ay + 2.4f)
                        });
                    }
                }

                Color txtCol = item.Enabled
                    ? (ShihabDarkMode ? Color.FromArgb(242, 244, 248) : Color.FromArgb(15, 23, 42))
                    : (ShihabDarkMode ? Color.FromArgb(145, 150, 160) : Color.FromArgb(148, 163, 184));

                Rectangle txtRect = new Rectangle(
                    r.Left + 2,
                    iconRect.Bottom + 2,
                    Math.Max(20, r.Width - 4),
                    Math.Max(14, r.Bottom - (iconRect.Bottom + 2) - 1));
                string textToDraw = !string.IsNullOrEmpty(item.DisplayText) ? item.DisplayText : item.DefaultText;
                TextRenderer.DrawText(
                    g,
                    textToDraw,
                    lblFont,
                    txtRect,
                    txtCol,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearIconBitmapCache();
                if (_cachedLabelFont != null)
                {
                    _cachedLabelFont.Dispose();
                    _cachedLabelFont = null;
                }
                try { _toolTip.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}

