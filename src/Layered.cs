// Per-pixel-alpha windows: GDI+ draws straight into a DIB section, UpdateLayeredWindow puts it on screen.
// Fully transparent pixels are click-through for free; nothing here ever takes focus.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Flippy
{
    // A drawing surface backed by a 32bpp premultiplied DIB section (grows as needed, never shrinks).
    class Surface : IDisposable
    {
        IntPtr memDC, hbmp, oldBmp, bits;
        int capW, capH;
        public Bitmap Bmp;
        public Graphics G;
        public int W, H;
        public IntPtr DC { get { return memDC; } }

        public void Ensure(int w, int h)
        {
            w = Math.Max(1, w); h = Math.Max(1, h);
            if (Bmp == null || w > capW || h > capH)
            {
                Free();
                capW = Math.Max(capW, ((w + 63) / 64) * 64);
                capH = Math.Max(capH, ((h + 63) / 64) * 64);
                Native.BITMAPINFOHEADER bi = new Native.BITMAPINFOHEADER();
                bi.biSize = 40; bi.biWidth = capW; bi.biHeight = -capH; bi.biPlanes = 1; bi.biBitCount = 32;
                memDC = Native.CreateCompatibleDC(IntPtr.Zero);
                hbmp = Native.CreateDIBSection(memDC, ref bi, 0, out bits, IntPtr.Zero, 0);
                oldBmp = Native.SelectObject(memDC, hbmp);
                Bmp = new Bitmap(capW, capH, capW * 4, PixelFormat.Format32bppPArgb, bits);
                G = Graphics.FromImage(Bmp);
            }
            W = w; H = h;
            G.ResetTransform(); G.ResetClip();
        }

        public void Clear()
        {
            CompositingMode old = G.CompositingMode;
            G.CompositingMode = CompositingMode.SourceCopy;
            using (SolidBrush b = new SolidBrush(Color.Transparent)) G.FillRectangle(b, 0, 0, W, H);
            G.CompositingMode = old;
        }

        void Free()
        {
            if (G != null) { G.Dispose(); G = null; }
            if (Bmp != null) { Bmp.Dispose(); Bmp = null; }
            if (memDC != IntPtr.Zero) { Native.SelectObject(memDC, oldBmp); Native.DeleteObject(hbmp); Native.DeleteDC(memDC); memDC = IntPtr.Zero; }
        }
        public void Dispose() { Free(); }
    }

    // A borderless, topmost, never-activating, taskbar/Alt+Tab-free window that shows a Surface.
    class LayeredWindow : Form
    {
        readonly bool clickThrough;
        public readonly Surface Surf = new Surface();
        public int SX, SY;     // where the surface's top-left is on screen

        public LayeredWindow(bool clickThrough)
        {
            this.clickThrough = clickThrough;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "Flippy";
        }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST;
                if (clickThrough) cp.ExStyle |= Native.WS_EX_TRANSPARENT;
                return cp;
            }
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        public event Action<int> Message;
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_MOUSEACTIVATE) { m.Result = (IntPtr)Native.MA_NOACTIVATE; return; }
            if (Message != null) Message(m.Msg);
            base.WndProc(ref m);
        }
        // push the surface to the screen with its top-left at (x, y)
        public bool Offscreen;      // demo rendering: keep the picture, never show the window
        public void Present(int x, int y)
        {
            SX = x; SY = y;
            Surf.G.Flush();
            if (Offscreen) return;
            if (!Visible) { Show(); }
            Native.Present(Handle, Surf.DC, x, y, Surf.W, Surf.H);
        }
        public void KeepOnTop() { if (IsHandleCreated) Native.KeepOnTop(Handle); }
    }
}
