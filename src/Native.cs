// Win32 plumbing: layered windows, window queries, input, DPI, power, clipboard.
// Compiled with the C# 5 compiler that ships with Windows (no string interpolation, no ?. etc).
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Clawd
{
    static class Native
    {
        // ---- structs ----
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int W, H; public SIZE(int w, int h) { W = w; H = h; } }
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)] public struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight; public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }
        [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public int dwFlags; }
        [StructLayout(LayoutKind.Sequential)] public struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage, uEdge; public RECT rc; public IntPtr lParam; }
        [StructLayout(LayoutKind.Sequential)] struct FILETIME { public uint Low, High; public ulong Value { get { return ((ulong)High << 32) | Low; } } }

        // ---- constants ----
        public const int WS_EX_LAYERED = 0x80000, WS_EX_TOOLWINDOW = 0x80, WS_EX_TRANSPARENT = 0x20, WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOPMOST = 0x8, WS_EX_APPWINDOW = 0x40000;
        public const int WM_MOUSEACTIVATE = 0x21, MA_NOACTIVATE = 3, WM_CLIPBOARDUPDATE = 0x031D, WM_NCHITTEST = 0x84;
        const int ULW_ALPHA = 2; const byte AC_SRC_OVER = 0, AC_SRC_ALPHA = 1;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOACTIVATE = 0x10, SWP_NOOWNERZORDER = 0x200;

        // ---- imports ----
        [DllImport("user32.dll", SetLastError = true)] public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int i, int v);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO p);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int k);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
        [DllImport("user32.dll")] static extern IntPtr GetTopWindow(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h, uint f);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT p, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr mon, ref MONITORINFO mi);
        [DllImport("user32.dll")] public static extern bool AddClipboardFormatListener(IntPtr h);
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr mon, int type, out uint dx, out uint dy);
        [DllImport("shell32.dll")] static extern uint SHAppBarMessage(uint msg, ref APPBARDATA data);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int s);
        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
        [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

        // ---- process / DPI ----
        public static void MakeDpiAware()
        {
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; } catch { }   // per-monitor v2
            try { SetProcessDPIAware(); } catch { }
        }
        public static int DpiAt(int x, int y)
        {
            try
            {
                IntPtr mon = MonitorFromPoint(new POINT(x, y), 2);
                uint dx, dy;
                if (GetDpiForMonitor(mon, 0, out dx, out dy) == 0) return (int)dx;
            }
            catch { }
            return 96;
        }

        // ---- layered window helpers ----
        public static void Present(IntPtr hwnd, IntPtr memDC, int x, int y, int w, int h)
        {
            POINT dst = new POINT(x, y), src = new POINT(0, 0);
            SIZE sz = new SIZE(Math.Max(1, w), Math.Max(1, h));
            BLENDFUNCTION bf = new BLENDFUNCTION(); bf.BlendOp = AC_SRC_OVER; bf.SourceConstantAlpha = 255; bf.AlphaFormat = AC_SRC_ALPHA;
            UpdateLayeredWindow(hwnd, IntPtr.Zero, ref dst, ref sz, memDC, ref src, 0, ref bf, ULW_ALPHA);
        }
        // keep a window above everything, including docks and an auto-hide taskbar
        public static void KeepOnTop(IntPtr h) { SetWindowPos(h, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER); }

        // ---- input ----
        public static POINT Cursor() { POINT p; GetCursorPos(out p); return p; }
        public static uint IdleMs()
        {
            LASTINPUTINFO l = new LASTINPUTINFO(); l.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
            if (!GetLastInputInfo(ref l)) return 0;
            return unchecked((uint)Environment.TickCount - l.dwTime);
        }
        public static bool EscDown() { return (GetAsyncKeyState(0x1B) & 0x8000) != 0; }
        public static bool LeftButtonDown() { return (GetAsyncKeyState(1) & 0x8000) != 0; }
        public static bool AnyMouseButtonDown() { return ((GetAsyncKeyState(1) | GetAsyncKeyState(2) | GetAsyncKeyState(4)) & 0x8000) != 0; }

        // counts key presses since the last call - only how many, never which keys. Modifiers ignored.
        static bool[] keyDown = new bool[256];
        public static int KeyPresses()
        {
            int n = 0;
            for (int k = 8; k < 256; k++)
            {
                if ((k >= 0x10 && k <= 0x12) || (k >= 0xA0 && k <= 0xA5) || k == 0x5B || k == 0x5C) continue;
                bool d = (GetAsyncKeyState(k) & 0x8000) != 0;
                if (d && !keyDown[k]) n++;
                keyDown[k] = d;
            }
            return n;
        }

        // ---- monitors ----
        public static void MonitorAt(int x, int y, out RECT bounds, out RECT work)
        {
            IntPtr mon = MonitorFromPoint(new POINT(x, y), 2);
            MONITORINFO mi = new MONITORINFO(); mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            GetMonitorInfo(mon, ref mi);
            bounds = mi.rcMonitor; work = mi.rcWork;
        }
        // is the taskbar on auto-hide? (then the work area is the whole screen and he'd sit behind it)
        public static bool TaskbarAutoHide()
        {
            APPBARDATA d = new APPBARDATA(); d.cbSize = Marshal.SizeOf(typeof(APPBARDATA));
            return (SHAppBarMessage(4, ref d) & 1) != 0;
        }

        // ---- windows ----
        public static IntPtr Foreground() { return GetForegroundWindow(); }
        public static string Title(IntPtr h) { StringBuilder sb = new StringBuilder(512); GetWindowText(h, sb, 512); return sb.ToString(); }
        static RECT R(IntPtr h)
        {
            RECT r;
            if (DwmGetWindowAttribute(h, 9, out r, Marshal.SizeOf(typeof(RECT))) != 0) GetWindowRect(h, out r);
            return r;
        }
        public static RECT Rect(IntPtr h) { return R(h); }
        static readonly uint myPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        public static bool Ours(IntPtr h) { uint pid; GetWindowThreadProcessId(h, out pid); return pid == myPid; }

        // a normal, visible, non-maximized app window he could stand on
        public static bool Usable(IntPtr h)
        {
            if (h == IntPtr.Zero || !IsWindow(h) || !IsWindowVisible(h) || IsIconic(h) || IsZoomed(h) || Ours(h)) return false;
            int cloaked; if (DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return false;
            int ex = GetWindowLong(h, -20);
            if ((ex & WS_EX_TRANSPARENT) != 0 || (ex & WS_EX_TOOLWINDOW) != 0) return false;
            StringBuilder sb = new StringBuilder(128); GetClassName(h, sb, 128); string cn = sb.ToString();
            if (cn == "Progman" || cn == "WorkerW" || cn == "Shell_TrayWnd" || cn == "Shell_SecondaryTrayWnd") return false;
            RECT r = R(h); int w = r.Right - r.Left, hh = r.Bottom - r.Top;
            if (w < 200 || hh < 80) return false;
            if (w >= GetSystemMetrics(0) * 0.98 && hh >= GetSystemMetrics(1) * 0.9) return false;
            return true;
        }
        static bool TopVisible(IntPtr h, int x, int top)
        {
            IntPtr at = WindowFromPoint(new POINT(x, top + 3));
            if (at == IntPtr.Zero) return false;
            IntPtr root = GetAncestor(at, 2);
            return root == h || Ours(root);
        }
        // topmost usable window whose visible top edge spans x and lies within [minTop, maxTop]
        public static IntPtr FindTop(int x, int margin, int minTop, int maxTop)
        {
            for (IntPtr h = GetTopWindow(IntPtr.Zero); h != IntPtr.Zero; h = GetWindow(h, 2))
            {
                if (!Usable(h)) continue;
                RECT r = R(h);
                if (x < r.Left + margin || x > r.Right - margin) continue;
                if (r.Top < minTop || r.Top > maxTop) continue;
                if (!TopVisible(h, x, r.Top)) continue;
                return h;
            }
            return IntPtr.Zero;
        }
        // any reachable window top on this screen: hwnd + a visible x on its top edge, closest to nearX
        public static bool FindClimbTarget(int nearX, int left, int right, int minTop, int maxTop, int margin, out IntPtr hwnd, out int x)
        {
            hwnd = IntPtr.Zero; x = 0;
            for (IntPtr h = GetTopWindow(IntPtr.Zero); h != IntPtr.Zero; h = GetWindow(h, 2))
            {
                if (!Usable(h)) continue;
                RECT r = R(h);
                if (r.Top < minTop || r.Top > maxTop) continue;
                int a = Math.Max(r.Left + margin, left), b = Math.Min(r.Right - margin, right);
                if (b <= a) continue;
                int best = int.MinValue;
                for (int i = 0; i < 12; i++)
                {
                    int xx = a + (b - a) * i / 11;
                    if (!TopVisible(h, xx, r.Top)) continue;
                    if (best == int.MinValue || Math.Abs(xx - nearX) < Math.Abs(best - nearX)) best = xx;
                }
                if (best != int.MinValue) { hwnd = h; x = best; return true; }
            }
            return false;
        }

        // ---- CPU load (0..1), from successive GetSystemTimes calls ----
        static ulong lastIdle, lastTotal;
        public static double CpuLoad()
        {
            FILETIME i, k, u;
            if (!GetSystemTimes(out i, out k, out u)) return 0;
            ulong idle = i.Value, total = k.Value + u.Value;   // kernel time includes idle
            double load = 0;
            if (lastTotal != 0 && total > lastTotal) load = 1.0 - (double)(idle - lastIdle) / (total - lastTotal);
            lastIdle = idle; lastTotal = total;
            return Math.Max(0, Math.Min(1, load));
        }
    }
}
