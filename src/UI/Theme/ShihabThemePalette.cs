using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ShihabMehediShared;

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
                    TextRenderer.DrawText(
                        mehediG,
                        "Add URL",
                        ShihabThemePalette.UiSemibold9Font,
                        mehediLblRect,
                        shihabTxtCol,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);

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

    public static class ShihabThemePalette
    {
        public const string ShihabMainHeaderTitle = "MHS Download Manager 1.0.6 (Mehedi Hasan Shihab)";

        internal static readonly Font UiRegular76Font = new Font("Segoe UI", 7.6f, FontStyle.Regular);
        internal static readonly Font UiBold78Font = new Font("Segoe UI", 7.8f, FontStyle.Bold);
        internal static readonly Font UiRegular82Font = new Font("Segoe UI", 8.2f, FontStyle.Regular);
        internal static readonly Font UiSemibold85Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Regular);
        internal static readonly Font UiSemibold86Font = new Font("Segoe UI Semibold", 8.6f, FontStyle.Regular);
        internal static readonly Font UiRegular86Font = new Font("Segoe UI", 8.6f, FontStyle.Regular);
        internal static readonly Font UiBold88Font = new Font("Segoe UI", 8.8f, FontStyle.Bold);
        internal static readonly Font UiRegular89Font = new Font("Segoe UI", 8.9f, FontStyle.Regular);
        internal static readonly Font UiBold89Font = new Font("Segoe UI", 8.9f, FontStyle.Bold);
        internal static readonly Font UiRegular9Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        internal static readonly Font UiSemibold9Font = new Font("Segoe UI Semibold", 9f, FontStyle.Regular);
        internal static readonly Font UiBold9Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        internal static readonly Font UiSemibold92Font = new Font("Segoe UI Semibold", 9.2f, FontStyle.Regular);
        internal static readonly Font UiRegular94Font = new Font("Segoe UI", 9.4f, FontStyle.Regular);
        internal static readonly Font UiBold94Font = new Font("Segoe UI", 9.4f, FontStyle.Bold);

        private sealed class FormDarkHookState
        {
            public bool Dark;
        }

        private sealed class ControlThemeHookState
        {
            public string SubApp;
        }

        private static readonly ConditionalWeakTable<Form, FormDarkHookState> _boundDarkForms = new ConditionalWeakTable<Form, FormDarkHookState>();
        private static readonly ConditionalWeakTable<Control, ControlThemeHookState> _boundThemeControls = new ConditionalWeakTable<Control, ControlThemeHookState>();

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

        private static void ApplyDwmAttributesToForm(Form form, bool dark)
        {
            try
            {
                if (form == null || form.IsDisposed || !form.IsHandleCreated) return;
                int mehediUseDark = dark ? 1 : 0;
                SetMehediDwmWindowAttribute(form.Handle, 20, ref mehediUseDark, sizeof(int));
                SetMehediDwmWindowAttribute(form.Handle, 19, ref mehediUseDark, sizeof(int));
                if (dark)
                {
                    int shihabObsidianCaptionRef = (25 << 16) | (19 << 8) | 18;
                    SetMehediDwmWindowAttribute(form.Handle, 35, ref shihabObsidianCaptionRef, sizeof(int));
                    int mehediCaptionTextRef = (245 << 16) | (234 << 8) | 228;
                    SetMehediDwmWindowAttribute(form.Handle, 36, ref mehediCaptionTextRef, sizeof(int));
                    int shihabFrameBorderRef = (56 << 16) | (40 << 8) | 36;
                    SetMehediDwmWindowAttribute(form.Handle, 34, ref shihabFrameBorderRef, sizeof(int));
                }
                else
                {
                    int mehediLightCaptionRef = (252 << 16) | (248 << 8) | 245;
                    SetMehediDwmWindowAttribute(form.Handle, 35, ref mehediLightCaptionRef, sizeof(int));
                    int shihabLightTextRef = (42 << 16) | (30 << 8) | 24;
                    SetMehediDwmWindowAttribute(form.Handle, 36, ref shihabLightTextRef, sizeof(int));
                }
            }
            catch { }
        }

        public static void ApplyMehediNativeDarkFrame(Form shihabForm, bool mehediDark)
        {
            if (shihabForm == null || shihabForm.IsDisposed) return;

            FormDarkHookState state;
            if (_boundDarkForms.TryGetValue(shihabForm, out state))
            {
                state.Dark = mehediDark;
                if (shihabForm.IsHandleCreated) ApplyDwmAttributesToForm(shihabForm, mehediDark);
                return;
            }

            state = new FormDarkHookState { Dark = mehediDark };
            _boundDarkForms.Add(shihabForm, state);

            shihabForm.HandleCreated += delegate
            {
                ApplyDwmAttributesToForm(shihabForm, state.Dark);
            };
            shihabForm.Shown += delegate
            {
                ApplyDwmAttributesToForm(shihabForm, state.Dark);
                ApplyShihabDarkThemeToControlTree(shihabForm, state.Dark);
            };

            if (shihabForm.IsHandleCreated)
            {
                ApplyDwmAttributesToForm(shihabForm, mehediDark);
            }
        }

        public static void ApplyShihabDarkThemeToControlTree(Control shihabRoot, bool mehediDark)
        {
            if (shihabRoot == null || shihabRoot.IsDisposed) return;
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

            ControlThemeHookState state;
            if (_boundThemeControls.TryGetValue(shihabCtrl, out state))
            {
                state.SubApp = shihabSubApp;
                try
                {
                    if (shihabCtrl.IsHandleCreated) SetShihabWindowTheme(shihabCtrl.Handle, shihabSubApp, null);
                }
                catch { }
                return;
            }

            state = new ControlThemeHookState { SubApp = shihabSubApp };
            _boundThemeControls.Add(shihabCtrl, state);
            shihabCtrl.HandleCreated += delegate
            {
                try
                {
                    if (!shihabCtrl.IsDisposed && shihabCtrl.IsHandleCreated)
                    {
                        SetShihabWindowTheme(shihabCtrl.Handle, state.SubApp, null);
                    }
                }
                catch { }
            };
            try
            {
                if (shihabCtrl.IsHandleCreated) SetShihabWindowTheme(shihabCtrl.Handle, shihabSubApp, null);
            }
            catch { }
        }

        public static bool SafeLaunchExternalUrl(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return false;
            Uri parsed;
            if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out parsed)) return false;
            bool allowed = parsed.Scheme == Uri.UriSchemeHttp ||
                           parsed.Scheme == Uri.UriSchemeHttps ||
                           string.Equals(parsed.Scheme, Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase);
            if (!allowed) return false;
            try
            {
                using (Process p = Process.Start(new ProcessStartInfo(parsed.AbsoluteUri) { UseShellExecute = true }))
                {
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void SafeOpenContainingFolderOrFile(string fullFilePath, string saveDirectory)
        {
            try
            {
                string explorerExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
                if (!File.Exists(explorerExe)) return;
                if (!string.IsNullOrWhiteSpace(fullFilePath) && File.Exists(fullFilePath))
                {
                    string safe = fullFilePath.Replace("\"", "").Replace("\0", "").Replace("\r", "").Replace("\n", "");
                    ProcessStartInfo psi = new ProcessStartInfo(explorerExe, "/select,\"" + safe + "\"");
                    psi.UseShellExecute = false;
                    using (Process p = Process.Start(psi)) { }
                }
                else if (!string.IsNullOrWhiteSpace(saveDirectory) && Directory.Exists(saveDirectory))
                {
                    string safeDir = saveDirectory.Replace("\"", "").Replace("\0", "").Replace("\r", "").Replace("\n", "");
                    ProcessStartInfo psi = new ProcessStartInfo(explorerExe, "\"" + safeDir + "\"");
                    psi.UseShellExecute = false;
                    using (Process p = Process.Start(psi)) { }
                }
            }
            catch { }
        }

        public static void SafeOpenWithDialog(string fullFilePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fullFilePath) || !File.Exists(fullFilePath)) return;
                string rundll = ShihabProcessAndPathSafety.ResolveSystem32ToolPath("rundll32.exe");
                if (string.IsNullOrEmpty(rundll)) return;
                string cleanPath = fullFilePath.Replace("\0", "").Replace("\r", "").Replace("\n", "");
                ProcessStartInfo psi = new ProcessStartInfo(rundll, "shell32.dll,OpenAs_RunDLL " + cleanPath);
                psi.UseShellExecute = false;
                using (Process p = Process.Start(psi)) { }
            }
            catch { }
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
            shihabBtn.Font = UiSemibold9Font;
            shihabBtn.Cursor = Cursors.Hand;
            return shihabBtn;
        }

        public static void DrawShihabTrayMinimizeGlyph(Graphics g, Rectangle bounds, Color trayColor, Color arrowColor)
        {
            if (g == null || bounds.IsEmpty) return;
            SmoothingMode prevSmooth = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                float cx = bounds.Left + bounds.Width / 2.0f;
                float cy = bounds.Top + bounds.Height / 2.0f;
                using (Pen arrowPen = new Pen(arrowColor, 1.45f))
                using (Pen trayPen = new Pen(trayColor, 1.35f))
                {
                    arrowPen.StartCap = LineCap.Round;
                    arrowPen.EndCap = LineCap.Round;
                    arrowPen.LineJoin = LineJoin.Round;
                    trayPen.StartCap = LineCap.Round;
                    trayPen.EndCap = LineCap.Round;
                    trayPen.LineJoin = LineJoin.Round;

                    // Downward minimize arrow into tray
                    g.DrawLine(arrowPen, cx, cy - 5.2f, cx, cy + 1.4f);
                    g.DrawLine(arrowPen, cx - 2.6f, cy - 1.2f, cx, cy + 1.6f);
                    g.DrawLine(arrowPen, cx + 2.6f, cy - 1.2f, cx, cy + 1.6f);

                    // Tray cradle at bottom
                    g.DrawLines(trayPen, new PointF[]
                    {
                        new PointF(cx - 5.2f, cy + 2.0f),
                        new PointF(cx - 5.2f, cy + 4.8f),
                        new PointF(cx + 5.2f, cy + 4.8f),
                        new PointF(cx + 5.2f, cy + 2.0f)
                    });
                }
            }
            finally
            {
                g.SmoothingMode = prevSmooth;
            }
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
                using (Process current = Process.GetCurrentProcess())
                {
                    Icon shihabExeIco = Icon.ExtractAssociatedIcon(current.MainModule.FileName);
                    if (shihabExeIco != null)
                    {
                        _shihabCachedOfficialIcon = shihabExeIco;
                        return _shihabCachedOfficialIcon;
                    }
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
            if (_shihabCachedAuthorPortrait == null)
            {
                return CreateMehediLogoBitmap(mehediDiam);
            }

            Bitmap shihabResult = new Bitmap(mehediDiam, mehediDiam);
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
}

