using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ShihabMehediIDM
{
    public class ShihabChromeForm : Form
    {
        private const int WM_SIZE = 0x0005;
        private const int WM_SETTEXT = 0x000C;
        private const int WM_SETTINGCHANGE = 0x001A;
        private const int WM_GETMINMAXINFO = 0x0024;
        private const int WM_WINDOWPOSCHANGED = 0x0047;
        private const int WM_SETICON = 0x0080;
        private const int WM_NCCALCSIZE = 0x0083;
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_NCPAINT = 0x0085;
        private const int WM_NCACTIVATE = 0x0086;
        private const int WM_THEMECHANGED = 0x031A;
        private const int WM_DWMCOMPOSITIONCHANGED = 0x031E;
        private const int WM_DPICHANGED = 0x02E0;
        private const int GWL_STYLE = -16;
        private const long WS_VISIBLE_STYLE = 0x10000000L;
        private const int HTCAPTION = 2;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MinMaxInfo
        {
            public NativePoint ptReserved;
            public NativePoint ptMaxSize;
            public NativePoint ptMaxPosition;
            public NativePoint ptMinTrackSize;
            public NativePoint ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DwmMargins
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
        }

        [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
        private static extern int DwmSetIntAttr(IntPtr hwnd, int attr, ref int val, int size);

        [DllImport("dwmapi.dll", EntryPoint = "DwmExtendFrameIntoClientArea")]
        private static extern int DwmExtendFrame(IntPtr hwnd, ref DwmMargins margins);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr GetNativeWindowStyle(IntPtr hwnd)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, GWL_STYLE) : GetWindowLong32(hwnd, GWL_STYLE);
        }

        private static void SetNativeWindowStyle(IntPtr hwnd, IntPtr style)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr64(hwnd, GWL_STYLE, style);
            else SetWindowLong32(hwnd, GWL_STYLE, style);
        }

        private bool _shihabDarkChrome = true;
        private bool _shihabWindowActive = true;
        private Color _shihabChromeBorderColor = Color.FromArgb(46, 52, 66);
        private Rectangle _shihabNormalBounds = Rectangle.Empty;
        private bool _shihabInRestoreTransition = false;
        private FormWindowState _shihabLastWindowState = FormWindowState.Normal;

        protected virtual bool ShihabResizableChrome
        {
            get { return true; }
        }

        public ShihabChromeForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            Padding = new Padding(1);
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x00020000; // WS_MINIMIZEBOX
                cp.Style |= 0x00080000; // WS_SYSMENU
                if (ShihabResizableChrome)
                {
                    cp.Style |= 0x00010000; // WS_MAXIMIZEBOX
                    cp.Style |= 0x00040000; // WS_THICKFRAME
                }
                return cp;
            }
        }

        protected override Size SizeFromClientSize(Size clientSize)
        {
            return clientSize;
        }

        protected override void SetClientSizeCore(int x, int y)
        {
            Size = new Size(x, y);
        }

        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            // Prevent Form.RestoreWindowBoundsIfNecessary from re-inflating chromeless bounds
            // by WS_THICKFRAME non-client insets (~14x14) after Minimize/Maximize -> Normal restore.
            if (_shihabInRestoreTransition &&
                specified == BoundsSpecified.All &&
                WindowState == FormWindowState.Normal &&
                !_shihabNormalBounds.IsEmpty)
            {
                width = _shihabNormalBounds.Width;
                height = _shihabNormalBounds.Height;
            }
            base.SetBoundsCore(x, y, width, height, specified);
            if (IsHandleCreated && WindowState == FormWindowState.Normal && !_shihabInRestoreTransition && width > 0 && height > 0)
            {
                _shihabNormalBounds = Bounds;
            }
        }

        public void ApplyShihabChromeTheme(bool dark, Color? customBorderColor)
        {
            _shihabDarkChrome = dark;
            _shihabChromeBorderColor = customBorderColor.HasValue
                ? customBorderColor.Value
                : ShihabThemePalette.GetShihabBorder(dark);

            ApplyDwmChromeAttributes();
            Invalidate(true);
        }

        public void RefreshShihabChromeSurface()
        {
            ApplyDwmChromeAttributes();
            Invalidate(true);
        }

        private void ApplyDwmChromeAttributes()
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                int darkFlag = _shihabDarkChrome ? 1 : 0;
                DwmSetIntAttr(Handle, 20, ref darkFlag, sizeof(int));
                DwmSetIntAttr(Handle, 19, ref darkFlag, sizeof(int));

                int roundPref = 2; // DWMWCP_ROUND on Windows 11
                DwmSetIntAttr(Handle, 33, ref roundPref, sizeof(int));

                int bgr = (_shihabChromeBorderColor.B << 16) |
                          (_shihabChromeBorderColor.G << 8) |
                          _shihabChromeBorderColor.R;
                DwmSetIntAttr(Handle, 34, ref bgr, sizeof(int));

                Color capCol = _shihabDarkChrome ? Color.FromArgb(22, 24, 30) : Color.FromArgb(240, 244, 249);
                int capBgr = (capCol.B << 16) | (capCol.G << 8) | capCol.R;
                DwmSetIntAttr(Handle, 35, ref capBgr, sizeof(int));

                Color textCol = _shihabDarkChrome ? Color.FromArgb(226, 232, 240) : Color.FromArgb(22, 28, 38);
                int textBgr = (textCol.B << 16) | (textCol.G << 8) | textCol.R;
                DwmSetIntAttr(Handle, 36, ref textBgr, sizeof(int));

                DwmMargins margins = new DwmMargins { Left = 1, Right = 1, Top = 1, Bottom = 1 };
                DwmExtendFrame(Handle, ref margins);
            }
            catch { }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyDwmChromeAttributes();
            ShihabThemePalette.ApplyShihabWindowIcons(this);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_shihabNormalBounds.IsEmpty && WindowState == FormWindowState.Normal)
            {
                _shihabNormalBounds = Bounds;
            }
            ApplyDwmChromeAttributes();
            ShihabThemePalette.ApplyShihabWindowIcons(this);
            Invalidate(true);
        }

        protected override void OnActivated(EventArgs e)
        {
            _shihabWindowActive = true;
            base.OnActivated(e);
            ApplyDwmChromeAttributes();
            Invalidate(false);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            _shihabWindowActive = false;
            base.OnDeactivate(e);
            ApplyDwmChromeAttributes();
            Invalidate(false);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            Padding targetPad = (WindowState == FormWindowState.Maximized)
                ? Padding.Empty
                : new Padding(1);
            if (Padding != targetPad)
            {
                Padding = targetPad;
            }
            if (WindowState != FormWindowState.Minimized)
            {
                ApplyDwmChromeAttributes();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (WindowState != FormWindowState.Maximized && ClientSize.Width > 2 && ClientSize.Height > 2)
            {
                Color drawBorder = _shihabWindowActive
                    ? _shihabChromeBorderColor
                    : ShihabThemePalette.BlendThemeColor(_shihabChromeBorderColor, BackColor, 0.35);
                using (Pen borderPen = new Pen(drawBorder, 1f))
                {
                    e.Graphics.DrawRectangle(borderPen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
                }
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCCALCSIZE)
            {
                // Keep 0px non-client insets on both WParam == 0 and WParam != 0 so WinForms
                // restore/minimize cycles never re-introduce a 7px WS_THICKFRAME border.
                m.Result = IntPtr.Zero;
                return;
            }
            if (m.Msg == WM_NCPAINT)
            {
                m.Result = IntPtr.Zero;
                return;
            }
            if (m.Msg == WM_NCACTIVATE)
            {
                // Suppress DefWindowProc classic caption/border painting on activation changes.
                _shihabWindowActive = (m.WParam != IntPtr.Zero);
                m.Result = new IntPtr(1);
                Invalidate(false);
                return;
            }
            if ((m.Msg == WM_SETTEXT || m.Msg == WM_SETICON) && IsHandleCreated)
            {
                long styleVal = GetNativeWindowStyle(Handle).ToInt64();
                if ((styleVal & WS_VISIBLE_STYLE) != 0)
                {
                    SetNativeWindowStyle(Handle, new IntPtr(styleVal & ~WS_VISIBLE_STYLE));
                    try
                    {
                        base.WndProc(ref m);
                    }
                    finally
                    {
                        SetNativeWindowStyle(Handle, new IntPtr(styleVal));
                    }
                    return;
                }
            }
            if (m.Msg == WM_DWMCOMPOSITIONCHANGED || m.Msg == WM_THEMECHANGED || m.Msg == WM_SETTINGCHANGE || m.Msg == WM_DPICHANGED)
            {
                base.WndProc(ref m);
                ApplyDwmChromeAttributes();
                Invalidate(true);
                return;
            }
            if (m.Msg == WM_GETMINMAXINFO)
            {
                base.WndProc(ref m);
                try
                {
                    MinMaxInfo mmi = (MinMaxInfo)Marshal.PtrToStructure(m.LParam, typeof(MinMaxInfo));
                    Screen scr = IsHandleCreated ? Screen.FromHandle(Handle) : Screen.FromPoint(Location);
                    if (scr != null)
                    {
                        Rectangle wa = scr.WorkingArea;
                        Rectangle mon = scr.Bounds;
                        mmi.ptMaxPosition.X = wa.Left - mon.Left;
                        mmi.ptMaxPosition.Y = wa.Top - mon.Top;
                        mmi.ptMaxSize.X = wa.Width;
                        mmi.ptMaxSize.Y = wa.Height;
                        Marshal.StructureToPtr(mmi, m.LParam, true);
                    }
                }
                catch { }
                return;
            }
            if (m.Msg == WM_NCHITTEST && ShihabResizableChrome && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                long lp = m.LParam.ToInt64();
                int sx = unchecked((short)(lp & 0xFFFF));
                int sy = unchecked((short)((lp >> 16) & 0xFFFF));
                Point pt = PointToClient(new Point(sx, sy));
                const int grip = 6;
                bool l = pt.X < grip;
                bool r = pt.X >= ClientSize.Width - grip;
                bool t = pt.Y < grip;
                bool b = pt.Y >= ClientSize.Height - grip;
                if (l && t) { m.Result = (IntPtr)HTTOPLEFT; return; }
                if (r && t) { m.Result = (IntPtr)HTTOPRIGHT; return; }
                if (l && b) { m.Result = (IntPtr)HTBOTTOMLEFT; return; }
                if (r && b) { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }
                if (l) { m.Result = (IntPtr)HTLEFT; return; }
                if (r) { m.Result = (IntPtr)HTRIGHT; return; }
                if (t) { m.Result = (IntPtr)HTTOP; return; }
                if (b) { m.Result = (IntPtr)HTBOTTOM; return; }
                return;
            }
            if (m.Msg == WM_WINDOWPOSCHANGED)
            {
                bool wasMinOrMax = (_shihabLastWindowState != FormWindowState.Normal);
                _shihabInRestoreTransition = wasMinOrMax;
                try
                {
                    base.WndProc(ref m);
                }
                finally
                {
                    _shihabInRestoreTransition = false;
                }
                FormWindowState curState = WindowState;
                if (curState == FormWindowState.Normal && IsHandleCreated && Visible && !wasMinOrMax)
                {
                    _shihabNormalBounds = Bounds;
                }
                _shihabLastWindowState = curState;
                return;
            }
            base.WndProc(ref m);
            if (m.Msg == WM_SIZE)
            {
                _shihabLastWindowState = WindowState;
            }
        }

        public static void BeginChromeWindowDrag(Form form)
        {
            if (form == null || form.IsDisposed || !form.IsHandleCreated) return;
            try
            {
                ReleaseCapture();
                SendMessage(form.Handle, 0x00A1, (IntPtr)HTCAPTION, IntPtr.Zero);
            }
            catch { }
        }
    }

    public sealed class ShihabChromeTitleBar : Control
    {
        private const int CaptionBtnWidth = 44;
        private const int TitleBarHeight = 32;

        private readonly Form _hostForm;
        private readonly bool _canMaximize;
        private readonly bool _showDisabledMaximizeSlot;
        private readonly Action _onBackgroundMinimize;
        private readonly string _bgTooltipText;
        private readonly ToolTip _toolTip;
        private Bitmap _cachedTitleIconBmp;

        private bool _dark = true;
        private Color _barBg = Color.FromArgb(24, 26, 32);
        private Color _barBottomLine = Color.FromArgb(38, 42, 52);
        private Color _titleFg = Color.FromArgb(226, 232, 240);
        private Color _mutedFg = Color.FromArgb(100, 110, 128);

        // Button slots in left-to-right order: 0=BackgroundMinimize, 1=Minimize, 2=Maximize, 3=Close
        private Rectangle _bgBtnRect = Rectangle.Empty;
        private Rectangle _minBtnRect = Rectangle.Empty;
        private Rectangle _maxBtnRect = Rectangle.Empty;
        private Rectangle _closeBtnRect = Rectangle.Empty;
        private int _hoverSlot = -1;
        private int _pressedSlot = -1;
        private int _lastTooltipSlot = -1;

        public Rectangle BackgroundMinimizeButtonBounds { get { return _bgBtnRect; } }
        public Rectangle MinimizeButtonBounds { get { return _minBtnRect; } }
        public Rectangle MaximizeButtonBounds { get { return _maxBtnRect; } }
        public Rectangle CloseButtonBounds { get { return _closeBtnRect; } }

        public ShihabChromeTitleBar(
            Form hostForm,
            bool canMaximize,
            bool showDisabledMaximizeSlot,
            Action onBackgroundMinimize,
            string bgTooltipText)
        {
            _hostForm = hostForm;
            _canMaximize = canMaximize;
            _showDisabledMaximizeSlot = showDisabledMaximizeSlot;
            _onBackgroundMinimize = onBackgroundMinimize;
            _bgTooltipText = string.IsNullOrEmpty(bgTooltipText)
                ? "Minimize to Background (System Tray)"
                : bgTooltipText;

            Dock = DockStyle.Top;
            Height = TitleBarHeight;
            TabStop = false;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            _toolTip = new ToolTip
            {
                InitialDelay = 350,
                ReshowDelay = 120,
                AutoPopDelay = 5000
            };

            SyncHostIconBitmap();
            if (_hostForm != null)
            {
                _hostForm.TextChanged += OnHostTextOrSizeChanged;
                _hostForm.SizeChanged += OnHostTextOrSizeChanged;
            }
            RecalculateButtonRects();
        }

        public void ApplyTheme(bool dark, Color? customBg, Color? customDivider)
        {
            _dark = dark;
            _barBg = customBg.HasValue
                ? customBg.Value
                : (dark ? Color.FromArgb(24, 26, 32) : Color.FromArgb(248, 250, 252));
            _barBottomLine = customDivider.HasValue
                ? customDivider.Value
                : (dark ? Color.FromArgb(38, 42, 52) : Color.FromArgb(218, 226, 236));
            _titleFg = dark ? Color.FromArgb(232, 236, 244) : Color.FromArgb(30, 41, 59);
            _mutedFg = dark ? Color.FromArgb(95, 104, 120) : Color.FromArgb(165, 175, 190);
            BackColor = _barBg;
            Invalidate();
        }

        public void InvokeBackgroundMinimize()
        {
            if (_onBackgroundMinimize != null)
            {
                _onBackgroundMinimize();
            }
        }

        public void InvokeNormalMinimize()
        {
            if (_hostForm != null && !_hostForm.IsDisposed)
            {
                _hostForm.WindowState = FormWindowState.Minimized;
            }
        }

        private void OnHostTextOrSizeChanged(object sender, EventArgs e)
        {
            Invalidate();
        }

        private void SyncHostIconBitmap()
        {
            if (_cachedTitleIconBmp != null)
            {
                _cachedTitleIconBmp.Dispose();
                _cachedTitleIconBmp = null;
            }
            try
            {
                // Prefer the embedded PNG logo downscaled with HighQualityBicubic over Icon.ToBitmap(),
                // which corrupts PNG-compressed ICO frames on .NET Framework 4.8.
                _cachedTitleIconBmp = ShihabThemePalette.CreateMehediLogoBitmap(16);
                if (_cachedTitleIconBmp != null) return;

                Icon src = (_hostForm != null && _hostForm.Icon != null)
                    ? _hostForm.Icon
                    : ShihabThemePalette.CreateShihabAppIcon();
                if (src != null)
                {
                    Bitmap bmp = new Bitmap(16, 16);
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawIcon(src, new Rectangle(0, 0, 16, 16));
                    }
                    _cachedTitleIconBmp = bmp;
                }
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_hostForm != null)
                {
                    _hostForm.TextChanged -= OnHostTextOrSizeChanged;
                    _hostForm.SizeChanged -= OnHostTextOrSizeChanged;
                }
                if (_cachedTitleIconBmp != null)
                {
                    _cachedTitleIconBmp.Dispose();
                    _cachedTitleIconBmp = null;
                }
                _toolTip.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            RecalculateButtonRects();
        }

        private void RecalculateButtonRects()
        {
            int x = Width;
            int h = Height;

            x -= CaptionBtnWidth;
            _closeBtnRect = new Rectangle(x, 0, CaptionBtnWidth, h);

            if (_canMaximize || _showDisabledMaximizeSlot)
            {
                x -= CaptionBtnWidth;
                _maxBtnRect = new Rectangle(x, 0, CaptionBtnWidth, h);
            }
            else
            {
                _maxBtnRect = Rectangle.Empty;
            }

            x -= CaptionBtnWidth;
            _minBtnRect = new Rectangle(x, 0, CaptionBtnWidth, h);

            if (_onBackgroundMinimize != null)
            {
                x -= CaptionBtnWidth;
                _bgBtnRect = new Rectangle(x, 0, CaptionBtnWidth, h);
            }
            else
            {
                _bgBtnRect = Rectangle.Empty;
            }
        }

        private int HitTestSlot(Point p)
        {
            if (_closeBtnRect.Contains(p)) return 3;
            if (!_maxBtnRect.IsEmpty && _maxBtnRect.Contains(p)) return _canMaximize ? 2 : -1;
            if (_minBtnRect.Contains(p)) return 1;
            if (!_bgBtnRect.IsEmpty && _bgBtnRect.Contains(p)) return 0;
            return -1;
        }

        private Rectangle GetSlotRect(int slot)
        {
            if (slot == 0) return _bgBtnRect;
            if (slot == 1) return _minBtnRect;
            if (slot == 2) return _maxBtnRect;
            if (slot == 3) return _closeBtnRect;
            return Rectangle.Empty;
        }

        private int GetButtonsLeftEdge()
        {
            if (!_bgBtnRect.IsEmpty) return _bgBtnRect.Left;
            if (!_minBtnRect.IsEmpty) return _minBtnRect.Left;
            if (!_maxBtnRect.IsEmpty) return _maxBtnRect.Left;
            return _closeBtnRect.Left;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int slot = HitTestSlot(e.Location);
            if (slot != _hoverSlot)
            {
                _hoverSlot = slot;
                UpdateHoverToolTip(slot);
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverSlot != -1 || _lastTooltipSlot != -1)
            {
                _hoverSlot = -1;
                UpdateHoverToolTip(-1);
                Invalidate();
            }
        }

        private void UpdateHoverToolTip(int slot)
        {
            if (slot == _lastTooltipSlot) return;
            _lastTooltipSlot = slot;
            if (slot == 0) _toolTip.SetToolTip(this, _bgTooltipText);
            else if (slot == 1) _toolTip.SetToolTip(this, "Minimize");
            else if (slot == 2 && _canMaximize)
            {
                bool maxed = _hostForm != null && _hostForm.WindowState == FormWindowState.Maximized;
                _toolTip.SetToolTip(this, maxed ? "Restore" : "Maximize");
            }
            else if (slot == 3) _toolTip.SetToolTip(this, "Close");
            else _toolTip.SetToolTip(this, null);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            int slot = HitTestSlot(e.Location);
            if (slot >= 0)
            {
                _pressedSlot = slot;
                Invalidate();
                return;
            }

            if (e.X < GetButtonsLeftEdge() && _hostForm != null)
            {
                if (e.Clicks == 2 && _canMaximize)
                {
                    ToggleHostMaximize();
                }
                else
                {
                    ShihabChromeForm.BeginChromeWindowDrag(_hostForm);
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;

            int downSlot = _pressedSlot;
            _pressedSlot = -1;
            Invalidate();

            if (downSlot < 0) return;
            if (HitTestSlot(e.Location) != downSlot) return;

            if (downSlot == 0)
            {
                InvokeBackgroundMinimize();
            }
            else if (downSlot == 1)
            {
                InvokeNormalMinimize();
            }
            else if (downSlot == 2 && _canMaximize)
            {
                ToggleHostMaximize();
            }
            else if (downSlot == 3 && _hostForm != null && !_hostForm.IsDisposed)
            {
                _hostForm.Close();
            }
        }

        private void ToggleHostMaximize()
        {
            if (_hostForm == null || _hostForm.IsDisposed || !_canMaximize) return;
            _hostForm.WindowState = (_hostForm.WindowState == FormWindowState.Maximized)
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(_barBg);

            using (Pen divPen = new Pen(_barBottomLine, 1f))
            {
                g.DrawLine(divPen, 0, Height - 1, Width, Height - 1);
            }

            int textLeft = 10;
            if (_cachedTitleIconBmp != null)
            {
                int iy = (Height - 16) / 2;
                g.DrawImage(_cachedTitleIconBmp, new Rectangle(10, iy, 16, 16));
                textLeft = 32;
            }

            int textRight = Math.Max(textLeft + 10, GetButtonsLeftEdge() - 8);
            string title = (_hostForm != null && !string.IsNullOrEmpty(_hostForm.Text))
                ? _hostForm.Text
                : "";
            Rectangle titleRect = new Rectangle(textLeft, 0, textRight - textLeft, Height);
            TextRenderer.DrawText(
                g,
                title,
                Font ?? ShihabThemePalette.UiRegular9Font,
                titleRect,
                _titleFg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            PaintCaptionButtons(g);
        }

        private void PaintCaptionButtons(Graphics g)
        {
            // Slot 0: Background Minimize (Tray arrow + bowl)
            if (!_bgBtnRect.IsEmpty)
            {
                bool hot = (_hoverSlot == 0);
                bool down = (_pressedSlot == 0);
                if (hot || down)
                {
                    Color fill = down
                        ? (_dark ? Color.FromArgb(44, 62, 92) : Color.FromArgb(196, 214, 238))
                        : (_dark ? Color.FromArgb(36, 48, 70) : Color.FromArgb(220, 232, 246));
                    using (SolidBrush b = new SolidBrush(fill))
                    {
                        g.FillRectangle(b, _bgBtnRect);
                    }
                }
                Color bowlCol = _titleFg;
                Color arrowCol = (hot || down)
                    ? (_dark ? Color.FromArgb(96, 165, 250) : Color.FromArgb(29, 78, 216))
                    : (_dark ? Color.FromArgb(125, 185, 252) : Color.FromArgb(37, 99, 235));
                ShihabThemePalette.DrawShihabTrayMinimizeGlyph(g, _bgBtnRect, bowlCol, arrowCol);
            }

            // Slot 1: Normal Minimize (-)
            PaintStandardHover(g, _minBtnRect, _hoverSlot == 1, _pressedSlot == 1);
            using (Pen minPen = new Pen(_titleFg, 1.2f))
            {
                int cx = _minBtnRect.Left + _minBtnRect.Width / 2;
                int cy = _minBtnRect.Top + _minBtnRect.Height / 2;
                g.DrawLine(minPen, cx - 5, cy, cx + 5, cy);
            }

            // Slot 2: Maximize / Restore ([])
            if (!_maxBtnRect.IsEmpty)
            {
                if (_canMaximize)
                {
                    PaintStandardHover(g, _maxBtnRect, _hoverSlot == 2, _pressedSlot == 2);
                }
                Color maxCol = _canMaximize ? _titleFg : _mutedFg;
                using (Pen maxPen = new Pen(maxCol, 1.1f))
                {
                    int cx = _maxBtnRect.Left + _maxBtnRect.Width / 2;
                    int cy = _maxBtnRect.Top + _maxBtnRect.Height / 2;
                    bool isMax = _canMaximize && _hostForm != null && _hostForm.WindowState == FormWindowState.Maximized;
                    if (isMax)
                    {
                        g.DrawRectangle(maxPen, cx - 3, cy - 5, 7, 7);
                        using (SolidBrush bgFill = new SolidBrush(_barBg))
                        {
                            g.FillRectangle(bgFill, cx - 5, cy - 3, 7, 7);
                        }
                        g.DrawRectangle(maxPen, cx - 5, cy - 3, 7, 7);
                    }
                    else
                    {
                        g.DrawRectangle(maxPen, cx - 5, cy - 5, 9, 9);
                    }
                }
            }

            // Slot 3: Close (X)
            bool closeHot = (_hoverSlot == 3);
            bool closeDown = (_pressedSlot == 3);
            if (closeHot || closeDown)
            {
                Color redFill = closeDown
                    ? Color.FromArgb(168, 34, 24)
                    : Color.FromArgb(196, 43, 28);
                using (SolidBrush rb = new SolidBrush(redFill))
                {
                    g.FillRectangle(rb, _closeBtnRect);
                }
            }
            Color closeFg = (closeHot || closeDown) ? Color.White : _titleFg;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen closePen = new Pen(closeFg, 1.25f))
            {
                int cx = _closeBtnRect.Left + _closeBtnRect.Width / 2;
                int cy = _closeBtnRect.Top + _closeBtnRect.Height / 2;
                g.DrawLine(closePen, cx - 4, cy - 4, cx + 4, cy + 4);
                g.DrawLine(closePen, cx + 4, cy - 4, cx - 4, cy + 4);
            }
            g.SmoothingMode = SmoothingMode.Default;
        }

        private void PaintStandardHover(Graphics g, Rectangle rect, bool hot, bool down)
        {
            if (!hot && !down) return;
            Color fill = down
                ? (_dark ? Color.FromArgb(46, 52, 66) : Color.FromArgb(208, 216, 226))
                : (_dark ? Color.FromArgb(35, 39, 49) : Color.FromArgb(228, 234, 242));
            using (SolidBrush b = new SolidBrush(fill))
            {
                g.FillRectangle(b, rect);
            }
        }
    }
}
