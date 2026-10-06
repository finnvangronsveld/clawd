# Clawd - a little orange desktop pet
# Left-click: hop (click him fast and he gets dizzy) | Drag: pick him up | Rub him with the cursor: pets
# Right-click: menu

#
# -RenderFrames <dir> : don't run; play a scripted demo off-screen and save PNG frames (used for the README gif)
param([string]$RenderFrames)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$createdNew = $false
$mutexName = if ($RenderFrames) { "Local\ClawdRender_$PID" } else { 'Local\ClawdDesktopPet' }
$mutex = New-Object System.Threading.Mutex($true, $mutexName, [ref]$createdNew)
if (-not $createdNew) {
    # already running (e.g. launched again from Start): poke the running Clawd so he waves
    try { $ev = [System.Threading.EventWaitHandle]::OpenExisting('Local\ClawdPoke'); [void]$ev.Set() } catch {}
    return
}
$pokeName = if ($RenderFrames) { "Local\ClawdRenderPoke_$PID" } else { 'Local\ClawdPoke' }
$poke = New-Object System.Threading.EventWaitHandle($false, [System.Threading.EventResetMode]::AutoReset, $pokeName)

$logFile = Join-Path $PSScriptRoot 'clawd.log'
$script:errCount = 0
function Write-Log($m) { try { "$(Get-Date -Format s) $m" | Out-File -Append $logFile } catch {} }

Add-Type @"
using System; using System.Text; using System.Runtime.InteropServices;
public static class ClawdNative {
    [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO p);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern IntPtr GetTopWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h, uint f);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int s);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int k);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);

    // counts key presses since the last call (only how many - never which keys). Modifiers are ignored.
    static bool[] keyDown = new bool[256];
    public static int KeyPresses() {
        int n = 0;
        for (int k = 8; k < 256; k++) {
            if ((k >= 0x10 && k <= 0x12) || (k >= 0xA0 && k <= 0xA5) || k == 0x5B || k == 0x5C) continue;
            bool d = (GetAsyncKeyState(k) & 0x8000) != 0;
            if (d && !keyDown[k]) n++;
            keyDown[k] = d;
        }
        return n;
    }
    // click-through, never-focused overlay (used for bullets)
    public static void MakeOverlay(IntPtr h) { SetWindowLong(h, -20, GetWindowLong(h, -20) | 0x80 | 0x20 | 0x08000000); }
    public static void ShowNoActivate(IntPtr h) { ShowWindow(h, 4); }
    public static void HideWin(IntPtr h) { ShowWindow(h, 0); }

    // which window is in front, and its title (used to notice you're watching a video; checked locally only)
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
    public static IntPtr ForegroundWindow() { return GetForegroundWindow(); }
    public static string ForegroundTitle() { StringBuilder sb = new StringBuilder(512); GetWindowText(GetForegroundWindow(), sb, 512); return sb.ToString(); }

    // tool window = no taskbar button and not listed in Alt+Tab
    public static void HideFromAltTab(IntPtr h) {
        int ex = GetWindowLong(h, -20);
        int want = (ex | 0x80) & ~0x40000;
        if (want != ex) SetWindowLong(h, -20, want);
    }
    public static uint IdleMs() {
        LASTINPUTINFO l = new LASTINPUTINFO(); l.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
        if (!GetLastInputInfo(ref l)) return 0;
        return unchecked((uint)Environment.TickCount - l.dwTime);
    }
    static RECT R(IntPtr h) {
        RECT r;
        if (DwmGetWindowAttribute(h, 9, out r, Marshal.SizeOf(typeof(RECT))) != 0) GetWindowRect(h, out r);
        return r;
    }
    public static int[] Rect(IntPtr h) { RECT r = R(h); return new int[] { r.Left, r.Top, r.Right, r.Bottom }; }

    public static bool Usable(IntPtr h, IntPtr self) {
        if (h == IntPtr.Zero || h == self || !IsWindow(h) || !IsWindowVisible(h) || IsIconic(h) || IsZoomed(h)) return false;
        int cloaked; if (DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return false;
        int ex = GetWindowLong(h, -20);
        if ((ex & 0x20) != 0 || (ex & 0x80) != 0) return false;
        StringBuilder sb = new StringBuilder(128); GetClassName(h, sb, 128); string cn = sb.ToString();
        if (cn == "Progman" || cn == "WorkerW" || cn == "Shell_TrayWnd" || cn == "Shell_SecondaryTrayWnd") return false;
        RECT r = R(h); int w = r.Right - r.Left, hh = r.Bottom - r.Top;
        if (w < 200 || hh < 80) return false;
        if (w >= GetSystemMetrics(0) * 0.98 && hh >= GetSystemMetrics(1) * 0.9) return false;
        return true;
    }
    static bool TopVisible(IntPtr h, IntPtr self, int x, int top) {
        IntPtr at = WindowFromPoint(new POINT(x, top + 3));
        if (at == IntPtr.Zero) return false;
        IntPtr root = GetAncestor(at, 2);
        return root == h || root == self;
    }
    // topmost usable window whose visible top edge spans x and lies within [minTop, maxTop]
    public static IntPtr FindTop(IntPtr self, int x, int margin, int minTop, int maxTop) {
        for (IntPtr h = GetTopWindow(IntPtr.Zero); h != IntPtr.Zero; h = GetWindow(h, 2)) {
            if (!Usable(h, self)) continue;
            RECT r = R(h);
            if (x < r.Left + margin || x > r.Right - margin) continue;
            if (r.Top < minTop || r.Top > maxTop) continue;
            if (!TopVisible(h, self, x, r.Top)) continue;
            return h;
        }
        return IntPtr.Zero;
    }
    // any window top on this screen he could climb: returns { hwnd, x } (x = visible spot on its top edge
    // closest to him), or null. Topmost windows win.
    public static long[] FindClimbTarget(IntPtr self, int nearX, int left, int right, int minTop, int maxTop, int margin) {
        for (IntPtr h = GetTopWindow(IntPtr.Zero); h != IntPtr.Zero; h = GetWindow(h, 2)) {
            if (!Usable(h, self)) continue;
            RECT r = R(h);
            if (r.Top < minTop || r.Top > maxTop) continue;
            int a = Math.Max(r.Left + margin, left), b = Math.Min(r.Right - margin, right);
            if (b <= a) continue;
            int best = int.MinValue;
            for (int i = 0; i < 12; i++) {
                int x = a + (b - a) * i / 11;
                if (!TopVisible(h, self, x, r.Top)) continue;
                if (best == int.MinValue || Math.Abs(x - nearX) < Math.Abs(best - nearX)) best = x;
            }
            if (best != int.MinValue) return new long[] { h.ToInt64(), best };
        }
        return null;
    }
}
"@

[void][ClawdNative]::SetProcessDPIAware()

# pixel-art thought cloud (scalloped edges + trail of little bubbles), drawn on a cell grid
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System; using System.Drawing; using System.Collections.Generic;
public static class ClawdCloud {
    public static Bitmap Draw(int cw, int ch, int r, int cell, int trailRows, double trailToX, Color fill, Color shade, Color ink, Color key) {
        int x0 = r + 1, y0 = r + 1, x1 = x0 + cw, y1 = y0 + ch;
        int cols = cw + 2 * r + 2, rows = ch + 2 * r + 2 + trailRows;
        List<double[]> circ = new List<double[]>();
        int n = Math.Max(2, (int)Math.Round(cw / (1.4 * r))) + 1;
        for (int i = 0; i < n; i++) {
            double cx = x0 + (double)i * cw / (n - 1);
            circ.Add(new double[] { cx, y0 + 2, (i % 2 == 0) ? r : r - 2 });
            circ.Add(new double[] { cx, y1 - 2, (i % 2 == 0) ? r - 2 : r });
        }
        int m = Math.Max(1, (int)Math.Round(ch / (1.4 * r))) + 1;
        for (int j = 0; j < m; j++) {
            double cy = y0 + (double)j * ch / Math.Max(1, m - 1);
            circ.Add(new double[] { x0 + 2, cy, (j % 2 == 0) ? r - 2 : r });
            circ.Add(new double[] { x1 - 2, cy, (j % 2 == 0) ? r : r - 2 });
        }
        double sx = (x0 + x1) / 2.0, sy = y1 + r + 1.0, ex = trailToX, ey = rows - 1.6;
        double[] ts = { 0.08, 0.5, 0.88 }; double[] rs = { 3.2, 2.2, 1.4 };
        for (int i = 0; i < 3; i++) circ.Add(new double[] { sx + (ex - sx) * ts[i], sy + (ey - sy) * ts[i], rs[i] });

        bool[,] ins = new bool[cols, rows];
        for (int x = 0; x < cols; x++) for (int y = 0; y < rows; y++) {
            bool v = (x >= x0 && x < x1 && y >= y0 && y < y1);
            double px = x + 0.5, py = y + 0.5;
            if (!v) foreach (double[] c in circ) { double dx = px - c[0], dy = py - c[1]; if (dx * dx + dy * dy <= c[2] * c[2]) { v = true; break; } }
            ins[x, y] = v;
        }
        Bitmap bmp = new Bitmap(cols * cell, rows * cell);
        using (Graphics g = Graphics.FromImage(bmp))
        using (SolidBrush bf = new SolidBrush(fill)) using (SolidBrush bs = new SolidBrush(shade)) using (SolidBrush bi = new SolidBrush(ink)) {
            g.Clear(key);
            for (int x = 0; x < cols; x++) for (int y = 0; y < rows; y++) {
                if (ins[x, y]) {
                    bool edgeBelow = (y + 1 >= rows || !ins[x, y + 1]);
                    g.FillRectangle(edgeBelow ? bs : bf, x * cell, y * cell, cell, cell);
                } else {
                    bool nb = (x > 0 && ins[x - 1, y]) || (x + 1 < cols && ins[x + 1, y]) || (y > 0 && ins[x, y - 1]) || (y + 1 < rows && ins[x, y + 1]);
                    if (nb) g.FillRectangle(bi, x * cell, y * cell, cell, cell);
                }
            }
        }
        return bmp;
    }
}

// smooth rotation of chunky pixel art: every screen pixel is mapped back to a sprite pixel (nearest
// neighbour, no blending, so the transparency key colour never bleeds)
public static class ClawdSprite {
    static int[] Read(Bitmap bmp, out System.Drawing.Imaging.BitmapData bd) {
        bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite,
                          System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        int[] px = new int[bmp.Width * bmp.Height];
        System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, px, 0, px.Length);
        return px;
    }
    static void Write(Bitmap bmp, System.Drawing.Imaging.BitmapData bd, int[] px) {
        System.Runtime.InteropServices.Marshal.Copy(px, 0, bd.Scan0, px.Length);
        bmp.UnlockBits(bd);
    }
    // draw a char-map sprite (1 char = 1 cell) rotated by deg around its pivot; the pivot lands at (atX, atY) cells
    public static void DrawRotated(Bitmap bmp, string[] rows, string keys, int[] argb, double pivX, double pivY,
                                   double atX, double atY, double deg, bool flipY, int cell) {
        int h = rows.Length, w = rows[0].Length, W = bmp.Width, H = bmp.Height;
        double a = deg * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
        int rad = (int)Math.Ceiling(Math.Sqrt(w * w + h * h) * cell) + cell;
        int cxp = (int)(atX * cell), cyp = (int)(atY * cell);
        System.Drawing.Imaging.BitmapData bd; int[] px = Read(bmp, out bd);
        for (int Y = Math.Max(0, cyp - rad); Y < Math.Min(H, cyp + rad); Y++)
            for (int X = Math.Max(0, cxp - rad); X < Math.Min(W, cxp + rad); X++) {
                double dx = (X + 0.5) / cell - atX, dy = (Y + 0.5) / cell - atY;
                double sx = c * dx + s * dy, sy = -s * dx + c * dy;
                if (flipY) sy = -sy;
                int ix = (int)Math.Floor(sx + pivX), iy = (int)Math.Floor(sy + pivY);
                if (ix < 0 || iy < 0 || ix >= w || iy >= h) continue;
                int k = keys.IndexOf(rows[iy][ix]);
                if (k >= 0) px[Y * W + X] = argb[k];
            }
        Write(bmp, bd, px);
    }
    // rotate everything already drawn on the canvas around (cx, cy) cells
    public static void RotateCells(Bitmap bmp, int cell, double cx, double cy, double deg, Color key) {
        int W = bmp.Width, H = bmp.Height, keyArgb = key.ToArgb();
        double a = deg * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
        double ox = cx * cell, oy = cy * cell;
        System.Drawing.Imaging.BitmapData bd; int[] src = Read(bmp, out bd);
        int[] dst = new int[src.Length];
        for (int Y = 0; Y < H; Y++) for (int X = 0; X < W; X++) {
            double dx = X + 0.5 - ox, dy = Y + 0.5 - oy;
            int ix = (int)Math.Floor(c * dx + s * dy + ox), iy = (int)Math.Floor(-s * dx + c * dy + oy);
            dst[Y * W + X] = (ix < 0 || iy < 0 || ix >= W || iy >= H) ? keyArgb : src[iy * W + ix];
        }
        Write(bmp, bd, dst);
    }
}
"@

# ---------- media sessions (is the video playing or paused?) ----------
# Windows' own media session API - the thing behind the volume-key media overlay. Local only.
$script:mediaMgr = $null
if (-not $RenderFrames) {
    try {
        Add-Type -AssemblyName System.Runtime.WindowsRuntime
        $null = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media.Control, ContentType = WindowsRuntime]
        $asTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
            $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1
        $op = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager]::RequestAsync()
        $task = $asTask.MakeGenericMethod([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager]).Invoke($null, @($op))
        if ($task.Wait(3000)) { $script:mediaMgr = $task.Result }
    } catch { Write-Log "media sessions unavailable: $_" }
}
# 'playing' / 'paused' / 'none' (unknown). Music apps don't count.
function Get-MediaStatus {
    if (-not $script:mediaMgr) { return 'none' }
    try {
        $paused = $false
        foreach ($s in $script:mediaMgr.GetSessions()) {
            if ($s.SourceAppUserModelId -match 'Spotify') { continue }
            $ps = $s.GetPlaybackInfo().PlaybackStatus.ToString()
            if ($ps -eq 'Playing') { return 'playing' }
            if ($ps -eq 'Paused') { $paused = $true }
        }
        if ($paused) { 'paused' } else { 'none' }
    } catch { 'none' }
}

# ---------- canvas / palette ----------
$gr0 = [System.Drawing.Graphics]::FromHwnd([IntPtr]::Zero); $dpi = $gr0.DpiX; $gr0.Dispose()
$S  = [int][Math]::Max(3, [Math]::Round(3 * $dpi / 96.0))   # screen pixels per sprite pixel
if ($RenderFrames) { $S = 4 }
$k  = $S / 3.0                                                # motion scale for high-DPI screens
$GW = 56; $GH = 32          # canvas in sprite pixels (wide enough for speech bubbles)
$OX = 17; $OY = $GH - 16    # Clawd's top-left (he is 22 x 16), centred
$GRAV = 0.8 * $k            # gravity
$SPD = 1.6 * $k             # walk speed

function New-Brush($r, $g, $b) { New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($r, $g, $b)) }
$keyCol   = [System.Drawing.Color]::FromArgb(255, 0, 255)
$bOrange  = New-Brush 217 119 87
$bLight   = New-Brush 235 150 117
$bShade   = New-Brush 186 94 64
$bOutline = New-Brush 112 50 32
$bEye     = New-Brush 28 22 20
$bShine   = New-Brush 235 235 240
$bBlush   = New-Brush 240 128 128
$bTongue  = New-Brush 230 100 110
$bInk     = New-Brush 40 32 30
$bWhite   = New-Brush 250 250 250
$bSilver  = New-Brush 190 195 204
$bDark    = New-Brush 78 82 92
$bBlue    = New-Brush 110 175 255
$bRed     = New-Brush 235 70 70
$bSpark   = New-Brush 255 210 70
$bCoffee  = New-Brush 110 68 42
$bSteam   = New-Brush 200 200 210
$bHeart   = New-Brush 240 80 115
$bPurple  = New-Brush 160 120 255
$bTeal    = New-Brush 50 195 175
$bGun     = New-Brush 132 138 150
$bGrip    = New-Brush 96 62 40
$bGunHi   = New-Brush 205 210 220
# pistol pointing right; pivot (hand) at grip centre (2, 4.5), muzzle tip at (11, 2)
$gunRows   = @('oooooooooo...', 'ohhhhhhhhho..', 'ogggggggggo..', 'obbgoooooo...', 'obbo.........', 'obbo.........', 'oooo.........')
$flashRows = @('............f..', '...........fff.', '..........fwwff', '...........fff.', '............f..', '...............', '...............')
$gunPal    = [int[]]@($bOutline.Color.ToArgb(), $bGunHi.Color.ToArgb(), $bGun.Color.ToArgb(), $bGrip.Color.ToArgb())
$flashPal  = [int[]]@($bSpark.Color.ToArgb(), $bWhite.Color.ToArgb())
$ARM = 6.0
# movie night: his back in the dark, rim-lit by whatever is on screen
$bBack        = New-Brush 88 44 33
$bKernel      = New-Brush 250 238 195
$bKernelShade = New-Brush 214 188 128
$bBucketRed   = New-Brush 122 34 36
$bBucketWhite = New-Brush 148 148 158
$rimSet = foreach ($c in @(@(165, 212, 255), @(236, 242, 255), @(255, 212, 165), @(185, 255, 215), @(255, 178, 220), @(200, 190, 255))) {
    @{ rim  = New-Brush $c[0] $c[1] $c[2]
       band = New-Brush ([int](88 + ($c[0] - 88) * 0.4)) ([int](44 + ($c[1] - 44) * 0.4)) ([int](33 + ($c[2] - 33) * 0.4)) }
}
# his running commentary (kept short so the bubble fits)
$commentLines = @('ooh', 'wait, what?', 'no way!', 'plot twist!', 'this part is so good', 'called it.', 'lol', '*crunch crunch*',
    'pass the popcorn', 'who is this guy?', 'nooo!', 'hehe', 'same tbh', '10/10', 'rewind that!', 'wait for it...',
    'I knew it', 'classic', 'chills.', 'bro what', 'I could do that', 'this is peak', 'okay okay okay', 'smooth.',
    'did you see that?', 'love this part', 'oh no no no', 'hmm, suspicious')
$siteLines = @{
    'YouTube'               = @('like and subscribe!', 'skip the ad...', 'the algorithm gets me', 'read the comments!')
    'Netflix|Prime Video'   = @('one more episode?', 'yes, still watching.', 'skip intro!')
    'Twitch'                = @('chat is wild', 'pog', 'gg')
    'Disney\+'              = @('magical.')
    'VLC|Media Player|\bmpv' = @('good movie pick')
}
$complainLines = @('hey! I was watching!', 'press play!!', 'UNPAUSE IT', 'ugh, seriously?', 'my popcorn is cold', 'rude.', "don't leave me hanging", 'I need to know!')
$sulkLines     = @('...', '*sigh*', 'still waiting.', '*crunch*')
$resumeLines   = @('finally!', 'yesss', "that's better.", 'shh, it started')

$videoRx = ' - YouTube|Netflix|Twitch|Prime Video|Disney\+|Crunchyroll|Vimeo|Plex|VLC media player|Media Player|\bmpv\b|HBO Max|VRT MAX|Streamz|GoPlay|Videoland|Dailymotion'
$noteBrushes =@($bPurple, $bTeal, $bOrange, $bHeart)

$canvas = New-Object System.Drawing.Bitmap ($GW * $S), ($GH * $S)
$g = [System.Drawing.Graphics]::FromImage($canvas)
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::SingleBitPerPixelGridFit
$font = New-Object System.Drawing.Font('Segoe UI', [float](11 * $k), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)

function Px([double]$c, [double]$r, [int]$w, [int]$h, $b) {
    $g.FillRectangle($b, [int][Math]::Floor($c) * $S, [int][Math]::Floor($r) * $S, $w * $S, $h * $S)
}

# little pixel glyphs
$glyphDefs = @{
    heart = @('.X.X.', 'XXXXX', 'XXXXX', '.XXX.', '..X..')
    note  = @('.XXXX', '.X..X', '.X..X', 'XX.XX', 'XX.XX')
    z     = @('XXXX', '..X.', '.X..', 'XXXX')
    star  = @('.X.', 'XXX', '.X.')
    dot   = @('X')
    sp0   = @('.....', '.....', '..X..', '.....', '.....')
    sp1   = @('.....', '..X..', '.XXX.', '..X..', '.....')
    sp2   = @('X.X.X', '.XXX.', 'XXXXX', '.XXX.', 'X.X.X')
    sp3   = @('..X..', 'X.X.X', '.XXX.', 'X.X.X', '..X..')
}
$glyphPts = @{}
foreach ($name in @($glyphDefs.Keys)) {
    $pts = New-Object System.Collections.ArrayList
    $rows = $glyphDefs[$name]
    for ($i = 0; $i -lt $rows.Count; $i++) { for ($j = 0; $j -lt $rows[$i].Length; $j++) { if ($rows[$i][$j] -eq 'X') { [void]$pts.Add(@($j, $i)) } } }
    $glyphPts[$name] = $pts
}
function Glyph([string]$name, [int]$xpx, [int]$ypx, [int]$cell, $b) {
    foreach ($p in $glyphPts[$name]) { $g.FillRectangle($b, $xpx + $p[0] * $cell, $ypx + $p[1] * $cell, $cell, $cell) }
}

$thinkWords = @('Thinking', 'Pondering', 'Clauding', 'Noodling', 'Percolating', 'Cogitating', 'Brewing', 'Musing', 'Ruminating', 'Vibing', 'Simmering', 'Spelunking', 'Wrangling', 'Schlepping')

# ---------- startup shortcut ----------
$startupLnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'Clawd.lnk'
$launcher   = Join-Path $PSScriptRoot 'launch.vbs'
$iconPath     = Join-Path $PSScriptRoot 'clawd.ico'
$startMenuLnk = Join-Path ([Environment]::GetFolderPath('Programs')) 'Clawd.lnk'

# Clawd icon (.ico with PNG frames), drawn from the same palette
function Make-Icon([string]$path) {
    $entries = @()
    foreach ($size in 256, 48, 32) {
        $c = [int][Math]::Max(1, [Math]::Floor($size / 24))
        $bmp = New-Object System.Drawing.Bitmap $size, $size
        $ig = [System.Drawing.Graphics]::FromImage($bmp)
        $ig.Clear([System.Drawing.Color]::Transparent)
        $x0 = [int](($size - 24 * $c) / 2); $y0 = [int](($size - 18 * $c) / 2)
        $R = { param($x, $y, $w, $h, $b) $ig.FillRectangle($b, $x0 + $x * $c, $y0 + $y * $c, $w * $c, $h * $c) }
        & $R 4 0 16 14 $bOutline; & $R 0 6 24 4 $bOutline
        & $R 4 13 1 5 $bOutline; & $R 19 13 1 5 $bOutline
        foreach ($lx in 5, 9, 13, 17) { & $R $lx 17 2 1 $bOutline }
        & $R 5 1 14 12 $bOrange; & $R 1 7 4 2 $bOrange; & $R 19 7 4 2 $bOrange
        foreach ($lx in 5, 9, 13, 17) { & $R $lx 13 2 4 $bOrange; & $R ($lx + 1) 13 1 4 $bShade }
        & $R 5 1 14 1 $bLight; & $R 5 2 1 10 $bLight; & $R 5 12 14 1 $bShade; & $R 18 2 1 11 $bShade
        & $R 7 3 2 4 $bEye; & $R 15 3 2 4 $bEye; & $R 8 3 1 1 $bShine; & $R 16 3 1 1 $bShine
        $ig.Dispose()
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $entries += , @($size, $ms.ToArray())
    }
    $fs = [System.IO.File]::Create($path)
    $wr = New-Object System.IO.BinaryWriter($fs)
    $wr.Write([UInt16]0); $wr.Write([UInt16]1); $wr.Write([UInt16]$entries.Count)
    $off = 6 + 16 * $entries.Count
    foreach ($en in $entries) {
        $dim = if ($en[0] -ge 256) { 0 } else { $en[0] }
        $wr.Write([byte]$dim); $wr.Write([byte]$dim); $wr.Write([byte]0); $wr.Write([byte]0)
        $wr.Write([UInt16]1); $wr.Write([UInt16]32); $wr.Write([UInt32]$en[1].Length); $wr.Write([UInt32]$off)
        $off += $en[1].Length
    }
    foreach ($en in $entries) { $wr.Write([byte[]]$en[1]) }
    $wr.Close()
}

function Ensure-Launcher {
    $cmd = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -STA -File ""' + $PSCommandPath + '""'
    $content = 'CreateObject("WScript.Shell").Run "' + $cmd + '", 0, False'
    if (-not (Test-Path $launcher) -or (Get-Content $launcher -Raw).Trim() -ne $content) {
        $content | Set-Content -Path $launcher -Encoding ASCII
    }
}
function New-Shortcut([string]$lnkPath) {
    Ensure-Launcher
    $sh = New-Object -ComObject WScript.Shell
    $l = $sh.CreateShortcut($lnkPath)
    $l.TargetPath = Join-Path $env:WINDIR 'System32\wscript.exe'
    $l.Arguments = '"' + $launcher + '"'
    $l.WorkingDirectory = $PSScriptRoot
    $l.Description = 'Clawd, the little orange desktop pet'
    if (Test-Path $iconPath) { $l.IconLocation = "$iconPath,0" }
    $l.Save()
}
function Set-Startup([bool]$on) {
    if ($on) { New-Shortcut $startupLnk }
    elseif (Test-Path $startupLnk) { Remove-Item -LiteralPath $startupLnk -Force }
}
if (-not $RenderFrames) { try {
    if (-not (Test-Path $iconPath)) { Make-Icon $iconPath }
    New-Shortcut $startMenuLnk                                   # Start menu entry, kept pointing here
    if (Test-Path $startupLnk) { New-Shortcut $startupLnk }      # refresh path + icon
} catch { Write-Log "shortcut setup: $_" } }

# ---------- window ----------
$form = New-Object System.Windows.Forms.Form
$form.FormBorderStyle = 'None'
$form.ShowInTaskbar = $false
$form.TopMost = $true
$form.StartPosition = 'Manual'
$form.BackColor = $keyCol
$form.TransparencyKey = $keyCol
$form.ClientSize = New-Object System.Drawing.Size ($GW * $S), ($GH * $S)
$form.Text = 'Clawd'

$pb = New-Object System.Windows.Forms.PictureBox
$pb.Dock = 'Fill'
$pb.BackColor = $keyCol
$pb.Image = $canvas
$pb.Cursor = [System.Windows.Forms.Cursors]::Hand
$form.Controls.Add($pb)
$self = $form.Handle
[ClawdNative]::HideFromAltTab($self)

$rng = New-Object System.Random
$parts = New-Object System.Collections.ArrayList
$orangeList = New-Object System.Collections.ArrayList
$clickTicks = New-Object System.Collections.ArrayList
$wa0 = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$st = @{
    x = [double]($wa0.Left + $rng.Next([Math]::Max(1, $wa0.Width - $form.Width)))
    y = [double]($wa0.Bottom - $form.Height)
    vx = 0.0; vy = 0.0; dir = 1; eye = 1
    mode = 'walk'; timer = 0; t = 0; phase = 0; tick = 0
    blink = 0; nextBlink = 100; shake = 0; squash = 0
    drag = $false; moved = $false; dx = 0; dy = 0; downX = 0; downY = 0
    arms = 'out'; eyeStyle = 'normal'; mouth = 'none'; blush = $false; sit = $false; wob = 0; mug = 'none'
    laptop = 'none'; lapRow = 0.0; glow = 'blue'; bang = $false
    say = ''; sayT = 0; spin = $false; word = 'Thinking'
    idleMs = 0; hover = 0; wasFar = $true; waveCD = 0; lastCurX = 0; lastCurY = 0
    plat = [IntPtr]::Zero; platRect = $null; walkOff = $false; target = [IntPtr]::Zero; menuOpen = $false
    gun = 0; flash = 0; recoil = 0; hits = 0; poked = $false
    keyCount = 0.0; lastKeyTick = -10000; typeHand = $false
    back = $false; rim = $rimSet[0]; rimEvery = 40; videoSeen = -10000; videoRect = $null
    videoTitle = ''; media = 'none'; pausedPolls = 0; nextComment = 200; resumed = $false; laughT = 0; watchX = 0.0
    aimDeg = 0.0; aimSide = 1; rot = 0; jy = 0.0; jv = 0.0; air = $false; trick = 'noscope'; climbX = 0.0; climbCD = 150
}

function Set-Mode([string]$m, [int]$timer = 0) { $st.mode = $m; $st.timer = $timer; $st.t = 0; $st.hover = 0 }
function Say([string]$text, [int]$ticks = 45) { $st.say = $text; $st.sayT = $ticks }
function Leave-Platform { $st.plat = [IntPtr]::Zero; $st.platRect = $null; $st.walkOff = $false }

function Start-Hop([double]$power = (9 * $k), [double]$vx = (1.8 * $k)) {
    Leave-Platform
    Set-Mode 'fall'
    $st.vy = -$power; $st.vx = $vx * $st.dir
}
function Start-Chase { Set-Mode 'chase' (150 + $rng.Next(150)) }
function Start-Smash { $parts.Clear(); Set-Mode 'smash' }

function Spawn-Float([string]$glyph, [double]$c, [double]$r, [double]$vx, [double]$vy, [int]$life, $b) {
    [void]$parts.Add(@{ kind = 'float'; glyph = $glyph; x = $c; y = $r; vx = $vx; vy = $vy; life = $life; b = $b; seed = $rng.Next(100) })
}
function Spawn-Debris {
    $parts.Clear()
    $pool = @($bSilver, $bSilver, $bSilver, $bDark, $bDark, $bBlue, $bBlue, $bSpark, $bSpark, $bSpark, $bWhite)
    for ($i = 0; $i -lt 32; $i++) {
        $b = $pool[$rng.Next($pool.Count)]
        $spark = [object]::ReferenceEquals($b, $bSpark)
        [void]$parts.Add(@{
            kind = if ($spark) { 'spark' } else { 'debris' }
            x = $OX + 11 + ($rng.NextDouble() - 0.5) * 10; y = [double]($GH - 4)
            vx = ($rng.NextDouble() - 0.5) * 3.6; vy = -1.0 - $rng.NextDouble() * 2.6
            b = $b; sz = if ($spark) { 1 } else { 2 }; life = 12 + $rng.Next(25)
        })
    }
}

# Any window top on this screen he can reach? He runs over to it, crouches and jumps up.
function Try-Climb($w, $h, $wa) {
    $cx = [int]($st.x + $w / 2); $feet = [int]($st.y + $h)
    $res = [ClawdNative]::FindClimbTarget($self, $cx, $wa.Left + 60, $wa.Right - 60, $wa.Top + 60, $feet - 60, 110)
    if ($null -eq $res) { return $false }
    $st.target = [IntPtr]([long]$res[0]); $st.climbX = [double]$res[1]
    Set-Mode 'goclimb'
    return $true
}

# ---- aiming: one arm + the pistol rotate toward a point ----
function Get-Aim($ox, $oy) {
    $left = $st.aimSide -lt 0
    $shx = if ($left) { $ox + 3 } else { $ox + 19 }; $shy = $oy + 7
    $a = $st.aimDeg * [Math]::PI / 180; $c = [Math]::Cos($a); $s = [Math]::Sin($a)
    @{ left = $left; shx = $shx; shy = $shy; c = $c; s = $s; hx = $shx + $c * $ARM; hy = $shy + $s * $ARM }
}
function Aim-At([double]$tx, [double]$ty) {
    $side = if ($tx -ge $st.x + ($OX + 11) * $S) { 1 } else { -1 }
    $st.aimSide = $side
    $shx = $st.x + $(if ($side -lt 0) { $OX + 3 } else { $OX + 19 }) * $S
    $shy = $st.y + ($OY + 7) * $S
    $deg = [Math]::Atan2($ty - $shy, $tx - $shx) * 180 / [Math]::PI
    # work in "right-hand" angles so both sides share one clamp (no aiming through his own body)
    $m = if ($side -gt 0) { $deg } else { 180 - $deg }
    while ($m -gt 180) { $m -= 360 }; while ($m -le -180) { $m += 360 }
    $m = [Math]::Max(-115, [Math]::Min(40, $m))
    $st.aimDeg = if ($side -gt 0) { $m } else { 180 - $m }
    $st.eye = $side; $st.arms = 'aim'; $st.gun = 1
}
# muzzle tip in screen pixels (+ the hand in canvas cells, for shell casings)
function Get-Muzzle {
    $ag = Get-Aim $OX $OY
    $mv = if ($ag.left) { 2.5 } else { -2.5 }
    $mx = $ag.hx + $ag.c * 9 - $ag.s * $mv; $my = $ag.hy + $ag.s * 9 + $ag.c * $mv
    @(($st.x + $mx * $S), ($st.y + $my * $S), $ag.hx, $ag.hy)
}
function Fire-Gun($target, $via = $null) {
    $mz = Get-Muzzle
    Fire-Bullet $mz[0] $mz[1] $target $via
    $st.flash = 3; $st.recoil = 3
    [void]$parts.Add(@{ kind = 'spark'; x = [double]$mz[2]; y = [double]$mz[3] - 1; vx = -0.5 * $st.aimSide; vy = -1.6; b = $bSpark; sz = 1; life = 22 })
}

function Get-Ground($w, $h, $wa) {
    if ($st.plat -ne [IntPtr]::Zero) {
        if ([ClawdNative]::Usable($st.plat, $self)) {
            $r = [ClawdNative]::Rect($st.plat)
            if ($st.platRect) { $st.x += $r[0] - $st.platRect[0] }   # ride along when the window moves
            $st.platRect = $r
            $cx = $st.x + $w / 2
            if ($r[1] -ge $wa.Top + 30 -and $cx -ge $r[0] -and $cx -le $r[2]) { return [double]($r[1] - $h) }
        }
        # window gone, maximized, or he walked off the edge: fall
        Leave-Platform
        if ($st.mode -ne 'fall') {
            if ($st.mode -eq 'smash') { $parts.Clear() }
            Set-Mode 'fall'; $st.vy = 0.0; $st.vx = $st.dir * $SPD
        }
    }
    return [double]($wa.Bottom - $h)
}

function Pick-Activity($w, $h, $wa) {
    $r = $rng.Next(100)
    if     ($r -lt 9)  { Set-Mode 'dance' 170 }
    elseif ($r -lt 18) { Set-Mode 'coffee' }
    elseif ($r -lt 27) { Set-Mode 'think'; $st.word = $thinkWords[$rng.Next($thinkWords.Count)] }
    elseif ($r -lt 32) { Start-Smash }
    elseif ($r -lt 35) { Set-Mode 'gun' }
    elseif ($r -lt 40) { Set-Mode 'trick' }
    elseif ($r -lt 52 -and (Try-Climb $w $h $wa)) { }
    else {
        if ($rng.Next(3) -eq 0) { $st.dir = -$st.dir }
        Set-Mode 'walk'
    }
}

# ---------- drawing ----------
function Add-O($x, $y, $w, $h) { [void]$orangeList.Add(@($x, $y, $w, $h)) }

function Draw-Bubble([string]$text, [bool]$spin, [int]$headTopPx) {
    $sz = $g.MeasureString($text, $font)
    $tw = [int][Math]::Ceiling($sz.Width); $th = [int][Math]::Ceiling($sz.Height)
    $cell = [int][Math]::Max(2, [Math]::Round(2 * $k))
    $u = [int][Math]::Max(1, [Math]::Round($k))
    $sw = if ($spin) { 5 * $cell + 3 } else { 0 }
    $bw = $tw + 6 + $sw; $bh = $th + 2
    $cx = [int](($OX + 11) * $S)
    $bx = [int][Math]::Max(1, [Math]::Min($canvas.Width - $bw - 1, $cx - [int]($bw / 2)))
    $by = [int][Math]::Max(1, $headTopPx - 4 * $S - $bh)
    $g.FillRectangle($bInk, $bx - $u, $by - $u, $bw + 2 * $u, $bh + 2 * $u)
    $g.FillRectangle($bWhite, $bx, $by, $bw, $bh)
    # tail
    $g.FillRectangle($bWhite, $cx - 2 * $u, $by + $bh, 4 * $u, $u)
    $g.FillRectangle($bInk, $cx - 2 * $u, $by + $bh + $u, 4 * $u, $u)
    $g.FillRectangle($bWhite, $cx - $u, $by + $bh + $u, 2 * $u, $u)
    $g.FillRectangle($bInk, $cx - $u, $by + $bh + 2 * $u, 2 * $u, $u)
    if ($spin) {
        $f = @(0, 1, 2, 3, 2, 1)[[int][Math]::Floor($st.tick / 4) % 6]
        Glyph "sp$f" ($bx + 3) ($by + [int](($bh - 5 * $cell) / 2)) $cell $bOrange
    }
    $g.DrawString($text, $font, $bInk, [float]($bx + 3 + $sw), [float]($by + 1))
}

function Render-Frame {
    if ($st.back) { Render-Back; return }
    $g.Clear($keyCol)
    $ox = $OX + $st.wob; $oy = $OY
    $low = $st.sit -or $st.squash -gt 0
    if ($low) { $oy += 2 }
    $e = $st.eye

    # ---- silhouette: body, arms, legs (outlined as one shape) ----
    $orangeList.Clear()
    $bx = $ox + 4; $bw = 14
    if ($st.squash -gt 0) { $bx -= 1; $bw = 16 }
    Add-O $bx $oy $bw 12
    switch ($st.arms) {
        'out'   { Add-O $ox ($oy + 6) 4 2;        Add-O ($ox + 18) ($oy + 6) 4 2 }
        'typeL' { Add-O $ox ($oy + 8) 4 2;        Add-O ($ox + 18) ($oy + 6) 4 2 }
        'typeR' { Add-O $ox ($oy + 6) 4 2;        Add-O ($ox + 18) ($oy + 8) 4 2 }
        'down'  { Add-O $ox ($oy + 8) 4 2;        Add-O ($ox + 18) ($oy + 8) 4 2 }
        'up'    { Add-O ($ox + 2) ($oy - 3) 2 10; Add-O ($ox + 18) ($oy - 3) 2 10 }
        'upL'   { Add-O ($ox + 2) ($oy - 3) 2 10; Add-O ($ox + 18) ($oy + 6) 4 2 }
        'upR'   { Add-O $ox ($oy + 6) 4 2;        Add-O ($ox + 18) ($oy - 3) 2 10 }
        'wave1' { Add-O $ox ($oy + 6) 4 2;        Add-O ($ox + 18) ($oy - 3) 2 10 }
        'wave2' { Add-O $ox ($oy + 6) 4 2;        Add-O ($ox + 18) ($oy + 3) 2 4; Add-O ($ox + 20) ($oy - 2) 2 6 }
        'sip'   { Add-O $ox ($oy + 6) 4 2;        Add-O ($ox + 18) ($oy + 8) 3 2 }
        'aim'   {
            $ag = Get-Aim $ox $oy
            if ($ag.left) { Add-O ($ox + 18) ($oy + 6) 4 2 } else { Add-O $ox ($oy + 6) 4 2 }
            for ($sd = 0.0; $sd -le $ARM; $sd += 0.5) { Add-O ($ag.shx + $ag.c * $sd - 1) ($ag.shy + $ag.s * $sd - 1) 2 2 }
        }
    }
    $legs = @()
    foreach ($c in 4, 8, 12, 16) {
        $lifted = ($st.phase -eq 1 -and ($c -eq 8 -or $c -eq 16)) -or ($st.phase -eq 3 -and ($c -eq 4 -or $c -eq 12))
        $lh = if ($low) { 2 } elseif ($lifted) { 3 } else { 4 }
        $legs += , @(($ox + $c), $lh)
    }
    foreach ($q in $orangeList) { Px ($q[0] - 1) ($q[1] - 1) ($q[2] + 2) ($q[3] + 2) $bOutline }
    # legs: outline only under the feet and on the outer sides, so the gaps between them stay see-through
    foreach ($l in $legs) { Px $l[0] ($oy + 12 + $l[1]) 2 1 $bOutline }
    Px ($ox + 3) ($oy + 12) 1 ($legs[0][1] + 1) $bOutline
    Px ($ox + 18) ($oy + 12) 1 ($legs[3][1] + 1) $bOutline
    foreach ($q in $orangeList) { Px $q[0] $q[1] $q[2] $q[3] $bOrange }
    foreach ($l in $legs) { Px $l[0] ($oy + 12) 2 $l[1] $bOrange }
    # shading
    Px $bx $oy $bw 1 $bLight
    Px $bx ($oy + 1) 1 10 $bLight
    Px $bx ($oy + 11) $bw 1 $bShade
    Px ($bx + $bw - 1) ($oy + 1) 1 11 $bShade
    foreach ($l in $legs) { Px ($l[0] + 1) ($oy + 12) 1 $l[1] $bShade }

    # ---- face ----
    $style = $st.eyeStyle
    if ($st.blink -gt 0 -and ($style -eq 'normal' -or $style -eq 'up' -or $style -eq 'down')) { $style = 'blink' }
    $ex1 = $ox + 6 + 2 * $e; $ex2 = $ox + 14 + 2 * $e
    if ($style -eq 'angry') {
        Px $ex1 ($oy + 3) 2 3 $bEye; Px $ex2 ($oy + 3) 2 3 $bEye
        Px ($ex1 - 1) ($oy + 1) 2 1 $bEye; Px ($ex1 + 1) ($oy + 2) 2 1 $bEye
        Px ($ex2 + 1) ($oy + 1) 2 1 $bEye; Px ($ex2 - 1) ($oy + 2) 2 1 $bEye
    } else {
        foreach ($ex in $ex1, $ex2) {
            switch ($style) {
                'normal' { Px $ex ($oy + 2) 2 4 $bEye; Px ($ex + 1) ($oy + 2) 1 1 $bShine }
                'up'     { Px $ex ($oy + 1) 2 4 $bEye; Px ($ex + 1) ($oy + 1) 1 1 $bShine }
                'down'   { Px $ex ($oy + 3) 2 4 $bEye }
                'blink'  { Px $ex ($oy + 4) 2 1 $bEye }
                'sleep'  { Px ($ex - 1) ($oy + 3) 1 1 $bEye; Px $ex ($oy + 4) 2 1 $bEye; Px ($ex + 2) ($oy + 3) 1 1 $bEye }
                'happy'  { Px ($ex - 1) ($oy + 4) 1 1 $bEye; Px $ex ($oy + 3) 2 1 $bEye; Px ($ex + 2) ($oy + 4) 1 1 $bEye }
                'dizzy'  {
                    Px $ex ($oy + 2) 1 1 $bEye; Px ($ex + 2) ($oy + 2) 1 1 $bEye; Px ($ex + 1) ($oy + 3) 1 1 $bEye
                    Px $ex ($oy + 4) 1 1 $bEye; Px ($ex + 2) ($oy + 4) 1 1 $bEye
                }
            }
        }
    }
    if ($st.blush) { Px ($ox + 5) ($oy + 7) 2 1 $bBlush; Px ($ox + 15) ($oy + 7) 2 1 $bBlush }
    switch ($st.mouth) {
        'o'     { Px ($ox + 10) ($oy + 7) 2 2 $bEye }
        'smile' { Px ($ox + 9) ($oy + 7) 1 1 $bEye; Px ($ox + 10) ($oy + 8) 2 1 $bEye; Px ($ox + 12) ($oy + 7) 1 1 $bEye }
        'yawn'  { Px ($ox + 9) ($oy + 7) 4 3 $bEye; Px ($ox + 10) ($oy + 9) 2 1 $bTongue }
    }

    # ---- props ----
    if ($st.mug -ne 'none') {
        if ($st.mug -eq 'sip') { $mx = $ox + 17; $my = $oy + 4 } else { $mx = $ox + 21; $my = $oy + 1 }
        Px ($mx - 1) ($my - 1) 6 7 $bOutline
        Px ($mx + 5) ($my + 1) 1 3 $bOutline
        Px $mx $my 4 5 $bWhite
        Px $mx $my 4 1 $bCoffee
        if ($st.tick % 7 -eq 0) { Spawn-Float 'dot' ($mx + 1 + $rng.Next(2)) ($my - 2) 0.0 -0.2 16 $bSteam }
    }
    switch ($st.laptop) {
        'open' {
            $glowB = if ($st.glow -eq 'red') { $bRed } else { $bBlue }
            Px ($ox + 3) ($oy + 7) 16 7 $bOutline
            Px ($ox + 4) ($oy + 8) 14 1 $glowB
            Px ($ox + 4) ($oy + 9) 14 4 $bSilver
            Px ($ox + 10) ($oy + 10) 2 2 $bWhite
            Px ($ox + 1) ($oy + 13) 20 3 $bOutline
            Px ($ox + 2) ($oy + 14) 18 1 $bDark
        }
        'held' {
            Px ($ox - 1) ($st.lapRow - 1) 24 4 $bOutline
            Px $ox $st.lapRow 22 1 $bSilver
            Px $ox ($st.lapRow + 1) 22 1 $bDark
        }
    }
    if ($st.gun -ne 0) {
        # pistol in his hand, rotated to wherever he's aiming (kicks back a cell on recoil)
        $ag = Get-Aim $ox $oy
        $rb = if ($st.recoil -gt 0) { 1.0 } else { 0.0 }
        $hx = $ag.hx - $ag.c * $rb; $hy = $ag.hy - $ag.s * $rb
        $g.Flush()
        [ClawdSprite]::DrawRotated($canvas, $gunRows, 'ohgb', $gunPal, 2.0, 4.5, $hx, $hy, $st.aimDeg, $ag.left, $S)
        if ($st.flash -gt 0) { [ClawdSprite]::DrawRotated($canvas, $flashRows, 'fw', $flashPal, 2.0, 4.5, $hx, $hy, $st.aimDeg, $ag.left, $S) }
    }
    if ($st.bang) { Px ($ox + 10) ($oy - 12) 2 4 $bRed; Px ($ox + 10) ($oy - 7) 2 2 $bRed }
    if ($st.mode -eq 'dizzy') {
        for ($i = 0; $i -lt 3; $i++) {
            $a = $st.tick * 0.25 + $i * 2.094
            Glyph 'star' ([int][Math]::Floor($ox + 10 + [Math]::Cos($a) * 10) * $S) ([int][Math]::Floor($oy - 3 + [Math]::Sin($a) * 2) * $S) $S $bSpark
        }
    }

    # ---- spinning (trickshots): rotate everything drawn so far, on the pixel grid ----
    if ($st.rot -ne 0) { $g.Flush(); [ClawdSprite]::RotateCells($canvas, $S, $ox + 11, $oy + 8, $st.rot, $keyCol) }

    # ---- particles ----
    foreach ($p in $parts) {
        if ($p.kind -eq 'float') { Glyph $p.glyph ([int][Math]::Floor($p.x) * $S) ([int][Math]::Floor($p.y) * $S) $S $p.b }
        else { Px $p.x $p.y $p.sz $p.sz $p.b }
    }

    # ---- speech bubble ----
    if ($st.sayT -gt 0 -and $st.say -and $st.laptop -ne 'held') { Draw-Bubble $st.say $st.spin ($oy * $S) }

    $pb.Invalidate()
}

# ---------- movie night ----------
# seen from behind: dark body, bright rim light from the screen, popcorn bucket at his side
function Render-Back {
    $g.Clear($keyCol)
    $ox = $OX + $st.wob; $oy = $OY + 2
    $rimB = $st.rim.rim; $bandB = $st.rim.band

    # popcorn bucket (drawn first so his arm reaches over it)
    $bx2 = $ox + 22; $by2 = $oy + 7
    Px ($bx2 - 1) ($by2 - 3) 8 11 $rimB
    Px $bx2 ($by2 - 4) 6 1 $rimB
    for ($col = 0; $col -lt 6; $col++) { Px ($bx2 + $col) $by2 1 7 $(if ($col % 2 -eq 0) { $bBucketRed } else { $bBucketWhite }) }
    Px $bx2 $by2 6 1 $bandB
    Px $bx2 ($by2 - 2) 6 2 $bKernel; Px ($bx2 + 1) ($by2 - 3) 4 1 $bKernel
    Px ($bx2 + 1) ($by2 - 1) 1 1 $bKernelShade; Px ($bx2 + 4) ($by2 - 2) 1 1 $bKernelShade

    # silhouette
    $orangeList.Clear()
    Add-O ($ox + 4) $oy 14 12
    Add-O $ox ($oy + 6) 4 2
    switch ($st.arms) {
        'reach' { Add-O ($ox + 18) ($oy + 6) 5 2 }
        'eat'   { Add-O ($ox + 18) ($oy + 1) 2 7 }
        default { Add-O ($ox + 18) ($oy + 6) 4 2 }
    }
    foreach ($q in $orangeList) { Px ($q[0] - 1) ($q[1] - 1) ($q[2] + 2) ($q[3] + 2) $rimB }
    Px ($ox + 3) ($oy + 12) 1 3 $rimB; Px ($ox + 18) ($oy + 12) 1 3 $rimB
    foreach ($q in $orangeList) { Px $q[0] $q[1] $q[2] $q[3] $bBack }
    Px ($ox + 4) ($oy + 12) 14 1 $bBack
    foreach ($c in 4, 8, 12, 16) { Px ($ox + $c) ($oy + 12) 2 2 $bBack }
    # light wrapping round the edges
    Px ($ox + 4) $oy 14 1 $bandB
    Px ($ox + 4) ($oy + 1) 1 11 $bandB
    Px ($ox + 17) ($oy + 1) 1 11 $bandB
    Px $ox ($oy + 6) 4 1 $bandB
    if ($st.arms -eq 'eat') { Px ($ox + 18) ($oy + 1) 1 7 $bandB } else { Px ($ox + 18) ($oy + 6) 4 1 $bandB }

    foreach ($p in $parts) {
        if ($p.kind -eq 'float') { Glyph $p.glyph ([int][Math]::Floor($p.x) * $S) ([int][Math]::Floor($p.y) * $S) $S $p.b }
        else { Px $p.x $p.y $p.sz $p.sz $p.b }
    }
    if ($st.sayT -gt 0 -and $st.say) { Draw-Bubble $st.say $false ($oy * $S) }
    $pb.Invalidate()
}

function Step-Watch {
    $st.back = $true; $st.sit = $true; $st.phase = 0
    # the screen's light keeps changing, like a real video
    if ($st.t -eq 1 -or $st.t % $st.rimEvery -eq 0) { $st.rim = $rimSet[$rng.Next($rimSet.Count)]; $st.rimEvery = 20 + $rng.Next(50) }
    $c = $st.t % 100
    $st.arms = if ($c -ge 60 -and $c -lt 70) { 'reach' } elseif ($c -ge 70 -and $c -lt 88) { 'eat' } else { 'rest' }
    if ($c -eq 80) {
        for ($i = 0; $i -lt 3; $i++) {
            [void]$parts.Add(@{ kind = 'spark'; x = [double]($OX + 16 + $rng.Next(3)); y = [double]($OY + 4); vx = ($rng.NextDouble() - 0.5) * 0.6; vy = -0.6; b = $bKernel; sz = 1; life = 25 })
        }
    }
    if ($c -eq 30 -and $rng.Next(3) -eq 0) {
        [void]$parts.Add(@{ kind = 'spark'; x = [double]($OX + 24); y = [double]($OY + 5); vx = -0.2; vy = -2.3; b = $bKernel; sz = 1; life = 30 })
    }
    if ($st.t -eq 1 -and -not $st.resumed) { Say 'popcorn time!' 50 }
    if ($st.laughT -gt 0) { $st.laughT--; $st.wob = if ($st.laughT % 4 -lt 2) { 1 } else { -1 } }
}

# something to say about what you're watching
function Get-Comment {
    $r = $rng.Next(100)
    # a short bit of the video title, e.g. "Minecraft Hardcore - YouTube" -> "Minecraft Hardcore"
    $title = ($st.videoTitle -replace '^\(\d+\)\s*', '') -split ' - | \| ' | Select-Object -First 1
    $short = ''
    foreach ($wd in ($title -split '\s+')) { if (($short + ' ' + $wd).Trim().Length -gt 14) { break }; $short = ($short + ' ' + $wd).Trim() }
    if ($r -lt 18 -and $short -and $short -notmatch '^(YouTube|Netflix|Twitch|Prime Video)$') {
        $opts = @("I love $short", "$short? classic", "ah, $short", "more $short pls")
        return $opts[$rng.Next($opts.Count)]
    }
    if ($r -lt 40) {
        foreach ($site in $siteLines.Keys) {
            if ($st.videoTitle -match $site) { $l = $siteLines[$site]; return $l[$rng.Next($l.Count)] }
        }
    }
    return $commentLines[$rng.Next($commentLines.Count)]
}

# ---------- thought-cloud menu ----------
$menuItems = @(
    @{ text = 'Hop!';             act = { Start-Hop } }
    @{ text = 'Chase my cursor';  act = { Start-Chase } }
    @{ text = 'Dance';            act = { Set-Mode 'dance' 170 } }
    @{ text = 'Coffee break';     act = { Set-Mode 'coffee' } }
    @{ text = 'Think hard';       act = { Set-Mode 'think'; $st.word = $thinkWords[$rng.Next($thinkWords.Count)] } }
    @{ text = 'Climb a window';   act = {
        $wa = [System.Windows.Forms.Screen]::FromControl($form).WorkingArea
        if (-not (Try-Climb $form.Width $form.Height $wa)) { Say 'no window to climb!' 60 }
    } }
    @{ text = 'Smash the laptop'; act = { Start-Smash } }
    @{ text = 'Shoot my cursor';  act = { Set-Mode 'gun' } }
    @{ text = 'Trickshot!';       act = { Set-Mode 'trick' } }
    @{ text = '-' }
    @{ text = 'startup';          act = { Set-Startup (-not (Test-Path $startupLnk)) } }
    @{ text = '-' }
    @{ text = 'Bye, Clawd';       act = { $form.Close() } }
)
function Item-Text($it) {
    if ($it.text -eq 'startup') { if (Test-Path $startupLnk) { '[x] Start with Windows' } else { '[ ] Start with Windows' } }
    else { $it.text }
}

$menuFont = New-Object System.Drawing.Font('Segoe UI', [float](12 * $k), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$bCloudShade = New-Brush 222 226 234
$bSep = New-Brush 200 204 212

$cloud = New-Object System.Windows.Forms.Form
$cloud.FormBorderStyle = 'None'
$cloud.ShowInTaskbar = $false
$cloud.TopMost = $true
$cloud.StartPosition = 'Manual'
$cloud.BackColor = $keyCol
$cloud.TransparencyKey = $keyCol
$cloud.KeyPreview = $true
$cloud.Text = 'Clawd menu'
$cpb = New-Object System.Windows.Forms.PictureBox
$cpb.Dock = 'Fill'
$cpb.BackColor = $keyCol
$cpb.Cursor = [System.Windows.Forms.Cursors]::Hand
$cloud.Controls.Add($cpb)
[ClawdNative]::HideFromAltTab($cloud.Handle)
$cl = @{ base = $null; img = $null; hover = -1; rows = @(); ox = 0; oy = 0; cwpx = 0; padX = 0; hiddenAt = -100 }

function Render-Cloud {
    $u = [int][Math]::Max(1, [Math]::Round($k))
    $cg = [System.Drawing.Graphics]::FromImage($cl.img)
    $cg.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::SingleBitPerPixelGridFit
    $cg.DrawImage($cl.base, 0, 0, $cl.base.Width, $cl.base.Height)
    for ($i = 0; $i -lt $cl.rows.Count; $i++) {
        $row = $cl.rows[$i]
        $y = $cl.oy + $row[0]
        if ($null -eq $row[2]) {
            for ($xx = $cl.ox + $cl.padX; $xx -lt $cl.ox + $cl.cwpx - $cl.padX; $xx += 3 * $u) {
                $cg.FillRectangle($bSep, $xx, $y + [int]($row[1] / 2), 2 * $u, $u)
            }
        } else {
            $hot = ($i -eq $cl.hover)
            if ($hot) { $cg.FillRectangle($bOrange, $cl.ox + [int]($cl.padX / 2), $y, $cl.cwpx - $cl.padX, $row[1]) }
            $tb = if ($hot) { $bWhite } else { $bInk }
            $cg.DrawString((Item-Text $row[2]), $menuFont, $tb, [float]($cl.ox + $cl.padX), [float]($y + 2 * $u))
        }
    }
    $cg.Dispose()
    $cpb.Image = $cl.img
    $cpb.Invalidate()
}

function Show-Cloud {
    $rowH = [int][Math]::Ceiling(20 * $k); $sepH = [int][Math]::Ceiling(9 * $k); $padX = [int][Math]::Ceiling(10 * $k)
    $mw = 0; $mh = 0; $rows = New-Object System.Collections.ArrayList
    foreach ($it in $menuItems) {
        if ($it.text -eq '-') { [void]$rows.Add(@($mh, $sepH, $null)); $mh += $sepH }
        else {
            $tw = $g.MeasureString((Item-Text $it), $menuFont).Width
            if ($tw -gt $mw) { $mw = $tw }
            [void]$rows.Add(@($mh, $rowH, $it)); $mh += $rowH
        }
    }
    $rad = 8; $trail = 17
    $cw = [int][Math]::Ceiling(([int]$mw + 2 * $padX) / $S); $ch = [int][Math]::Ceiling($mh / $S)
    $cloudW = ($cw + 2 * $rad + 2) * $S; $cloudH = ($ch + 2 * $rad + 2 + $trail) * $S
    $headX = $st.x + $form.Width / 2
    $headTop = $st.y + $OY * $S
    $wa = [System.Windows.Forms.Screen]::FromControl($form).WorkingArea
    $left = [int][Math]::Max($wa.Left, [Math]::Min($wa.Right - $cloudW, $headX - $cloudW / 2))
    $top = [int][Math]::Max($wa.Top, $headTop - $cloudH - $S)
    $cl.base = [ClawdCloud]::Draw($cw, $ch, $rad, $S, $trail, ($headX - $left) / $S, `
        [System.Drawing.Color]::FromArgb(252, 252, 252), $bCloudShade.Color, $bInk.Color, $keyCol)
    $cl.img = New-Object System.Drawing.Bitmap $cl.base.Width, $cl.base.Height
    $cl.ox = ($rad + 1) * $S; $cl.oy = ($rad + 1) * $S; $cl.cwpx = $cw * $S; $cl.padX = $padX
    $cl.rows = $rows; $cl.hover = -1
    $cloud.ClientSize = New-Object System.Drawing.Size $cloudW, $cloudH
    $cloud.Location = New-Object System.Drawing.Point $left, $top
    Render-Cloud
    $st.menuOpen = $true
    $cloud.Show(); $cloud.Activate()
}

function Hide-Cloud {
    if ($st.menuOpen -or $cloud.Visible) { $cl.hiddenAt = $st.tick }
    $cloud.Hide()
    $st.menuOpen = $false
}

$cpb.Add_MouseMove({ param($sender, $e)
    $idx = -1
    for ($i = 0; $i -lt $cl.rows.Count; $i++) {
        $row = $cl.rows[$i]
        if ($null -ne $row[2] -and $e.Y -ge $cl.oy + $row[0] -and $e.Y -lt $cl.oy + $row[0] + $row[1] -and $e.X -ge $cl.ox -and $e.X -lt $cl.ox + $cl.cwpx) { $idx = $i }
    }
    if ($idx -ne $cl.hover) { $cl.hover = $idx; Render-Cloud }
})
$cpb.Add_MouseLeave({ if ($cl.hover -ne -1) { $cl.hover = -1; Render-Cloud } })
$cpb.Add_MouseUp({ param($sender, $e)
    if ($e.Button -eq 'Left' -and $cl.hover -ge 0) {
        $it = $cl.rows[$cl.hover][2]
        Hide-Cloud
        & $it.act
    }
})
$cloud.Add_Deactivate({ Hide-Cloud })
$cloud.Add_KeyDown({ param($sender, $e) if ($e.KeyCode -eq 'Escape') { Hide-Cloud } })

# ---------- bullets: tiny click-through overlay windows so they can fly across the whole screen ----------
$BC = 7
function New-ShotBmp([string[]]$rows, $fill, $core) {
    $bm = New-Object System.Drawing.Bitmap ($BC * $S), ($BC * $S)
    $bgr = [System.Drawing.Graphics]::FromImage($bm); $bgr.Clear($keyCol)
    for ($i = 0; $i -lt $rows.Count; $i++) { for ($j = 0; $j -lt $rows[$i].Length; $j++) {
        $ch = $rows[$i][$j]
        $br = if ($ch -eq 'o') { $bOutline } elseif ($ch -eq 'X') { $fill } elseif ($ch -eq 'w') { $core } else { $null }
        if ($br) { $bgr.FillRectangle($br, $j * $S, $i * $S, $S, $S) }
    } }
    $bgr.Dispose(); return $bm
}
$bulletBmp = New-ShotBmp @('.......', '.......', '..ooo..', '..oXo..', '..ooo..', '.......', '.......') $bSpark $bWhite
$impactBmp = New-ShotBmp @('X..X..X', '.X.X.X.', '..XXX..', 'XXXwXXX', '..XXX..', '.X.X.X.', 'X..X..X') $bSpark $bWhite
$bullets = @()
for ($i = 0; $i -lt 5; $i++) {
    $bf = New-Object System.Windows.Forms.Form
    $bf.FormBorderStyle = 'None'; $bf.ShowInTaskbar = $false; $bf.TopMost = $true; $bf.StartPosition = 'Manual'
    $bf.BackColor = $keyCol; $bf.TransparencyKey = $keyCol
    $bf.ClientSize = New-Object System.Drawing.Size ($BC * $S), ($BC * $S)
    $bpx = New-Object System.Windows.Forms.PictureBox
    $bpx.Dock = 'Fill'; $bpx.BackColor = $keyCol; $bpx.Image = $bulletBmp
    $bf.Controls.Add($bpx)
    [ClawdNative]::MakeOverlay($bf.Handle)
    $bullets += @{ form = $bf; pb = $bpx; active = $false; x = 0.0; y = 0.0; vx = 0.0; vy = 0.0; tx = 0; ty = 0; left = 0; impact = 0 }
}
function Move-Bullet($bl) {
    $half = [int]($BC * $S / 2)
    $bl.form.Location = New-Object System.Drawing.Point ([int]$bl.x - $half), ([int]$bl.y - $half)
}
function Aim-Bullet($bl, $tx, $ty) {
    $dx = $tx - $bl.x; $dy = $ty - $bl.y
    $steps = [int][Math]::Max(1, [Math]::Ceiling([Math]::Sqrt($dx * $dx + $dy * $dy) / (32 * $k)))
    $bl.tx = $tx; $bl.ty = $ty; $bl.vx = $dx / $steps; $bl.vy = $dy / $steps; $bl.left = $steps
}
# $via: optional bounce point (ricochet) before heading for the target
function Fire-Bullet([double]$sx, [double]$sy, $target, $via = $null) {
    foreach ($bl in $bullets) {
        if (-not $bl.active) {
            $bl.x = $sx; $bl.y = $sy; $bl.impact = 0
            if ($via) { Aim-Bullet $bl $via.X $via.Y; $bl.next = $target } else { Aim-Bullet $bl $target.X $target.Y; $bl.next = $null }
            $bl.pb.Image = $bulletBmp
            Move-Bullet $bl
            [ClawdNative]::ShowNoActivate($bl.form.Handle)
            $bl.active = $true
            return
        }
    }
}
function Update-Bullets($cur) {
    foreach ($bl in $bullets) {
        if (-not $bl.active) { continue }
        if ($bl.impact -gt 0) {
            $bl.impact--
            if ($bl.impact -le 0) { [ClawdNative]::HideWin($bl.form.Handle); $bl.active = $false }
        } else {
            $bl.x += $bl.vx; $bl.y += $bl.vy; $bl.left--
            if ($bl.left -le 0 -and $bl.next) {
                # ricochet: bounce off and head for the real target
                $bl.x = [double]$bl.tx; $bl.y = [double]$bl.ty
                $nx = $bl.next; $bl.next = $null
                Aim-Bullet $bl $nx.X $nx.Y
            } elseif ($bl.left -le 0) {
                $bl.x = [double]$bl.tx; $bl.y = [double]$bl.ty
                $bl.pb.Image = $impactBmp; $bl.impact = 9
                $hx = $cur.X - $bl.tx; $hy = $cur.Y - $bl.ty
                if ([Math]::Sqrt($hx * $hx + $hy * $hy) -lt 30 * $k) { $st.hits++ }
            }
            Move-Bullet $bl
        }
    }
}

# ---------- mouse ----------
$pb.Add_MouseDown({ param($sender, $e)
    if ($e.Button -eq 'Left') {
        $p = [System.Windows.Forms.Cursor]::Position
        $st.drag = $true; $st.moved = $false
        $st.dx = $e.X; $st.dy = $e.Y; $st.downX = $p.X; $st.downY = $p.Y
    }
})
$pb.Add_MouseMove({ param($sender, $e)
    if ($st.drag) {
        $p = [System.Windows.Forms.Cursor]::Position
        if (-not $st.moved -and [Math]::Abs($p.X - $st.downX) + [Math]::Abs($p.Y - $st.downY) -gt 4) {
            $st.moved = $true
            Hide-Cloud
            $st.mug = 'none'; $parts.Clear(); Leave-Platform
            Say 'wheee!' 60
        }
        if ($st.moved) {
            $st.x = [double]($p.X - $st.dx); $st.y = [double]($p.Y - $st.dy)
            $form.Location = New-Object System.Drawing.Point ([int]$st.x), ([int]$st.y)
        }
    }
})
$pb.Add_MouseUp({ param($sender, $e)
    if ($e.Button -eq 'Right') {
        # (clicking him while the cloud is open closes it via Deactivate first, so don't instantly reopen)
        if (-not $cloud.Visible -and $st.tick - $cl.hiddenAt -gt 5) { Show-Cloud } else { Hide-Cloud }
        return
    }
    if ($e.Button -eq 'Left' -and $st.drag) {
        $st.drag = $false
        if ($st.moved) { Set-Mode 'fall'; $st.vy = 0.0; $st.vx = 0.0; $st.sayT = 0 }
        elseif ($st.mode -eq 'sleep' -or $st.mode -eq 'yawn') { Say 'huh?!' 40; Start-Hop (7 * $k) 0 }
        elseif ($st.mode -ne 'smash' -and $st.mode -ne 'dizzy') {
            [void]$clickTicks.Add($st.tick)
            while ($clickTicks.Count -gt 0 -and $st.tick - $clickTicks[0] -gt 50) { $clickTicks.RemoveAt(0) }
            if ($clickTicks.Count -ge 4) { $clickTicks.Clear(); Set-Mode 'dizzy' 130 }
            else { Start-Hop }
        }
    }
})

# ---------- brain ----------
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 30
$timer.Add_Tick({
  try {
    $st.tick++; $st.t++
    $w = $form.Width; $h = $form.Height
    $cxw = $st.x + $w / 2
    $wa = [System.Windows.Forms.Screen]::FromPoint((New-Object System.Drawing.Point ([int]$cxw), ([int]($st.y + $h - 20)))).WorkingArea
    $ground = if ($st.drag) { [double]($wa.Bottom - $h) } else { Get-Ground $w $h $wa }
    $cxw = $st.x + $w / 2

    # cursor relative to Clawd
    $cur = [System.Windows.Forms.Cursor]::Position
    $cdx = $cur.X - $cxw
    $cdy = $cur.Y - ($st.y + ($OY + 3) * $S)
    $cdist = [Math]::Sqrt($cdx * $cdx + $cdy * $cdy)
    $lookEye = if ($cdx -gt 25) { 1 } elseif ($cdx -lt -25) { -1 } else { 0 }
    $lookUp = ($cdy -lt -40 -and [Math]::Abs($cdx) -lt 220)
    $relX = ($cur.X - $st.x) / $S; $relY = ($cur.Y - $st.y) / $S
    $overBody = ($relX -ge $OX + 3 -and $relX -lt $OX + 19 -and $relY -ge $OY - 1 -and $relY -lt $OY + 16)
    $curMoved = ([Math]::Abs($cur.X - $st.lastCurX) + [Math]::Abs($cur.Y - $st.lastCurY)) -gt 1
    $st.lastCurX = $cur.X; $st.lastCurY = $cur.Y

    # per-frame visual defaults
    $st.arms = 'out'; $st.eyeStyle = 'normal'; $st.mouth = 'none'; $st.blush = $false; $st.sit = $false
    $st.wob = 0; $st.mug = 'none'; $st.laptop = 'none'; $st.bang = $false; $st.spin = $false; $st.gun = 0; $st.back = $false; $st.rot = 0

    if ($st.tick % 10 -eq 0) { $st.idleMs = [ClawdNative]::IdleMs() }
    if ($st.blink -gt 0) { $st.blink-- }
    else { $st.nextBlink--; if ($st.nextBlink -le 0) { $st.blink = 4; $st.nextBlink = 80 + $rng.Next(150) } }
    if ($st.sayT -gt 0) { $st.sayT-- }
    if ($st.squash -gt 0) { $st.squash-- }
    if ($st.flash -gt 0) { $st.flash-- }
    if ($st.recoil -gt 0) { $st.recoil-- }

    # is a video in front? (window title check, local only)
    if ($st.tick % 15 -eq 0) {
        $fgw = [ClawdNative]::ForegroundWindow()
        if ($fgw -eq $self -or $fgw -eq $cloud.Handle) {
            if ($st.tick - $st.videoSeen -lt 60) { $st.videoSeen = $st.tick }   # clicking him doesn't end movie night
        } else {
            $fgTitle = [ClawdNative]::ForegroundTitle()
            if ($fgTitle -match $videoRx) { $st.videoSeen = $st.tick; $st.videoRect = [ClawdNative]::Rect($fgw); $st.videoTitle = $fgTitle }
        }
    }
    $videoOn = ($st.tick - $st.videoSeen) -lt 60
    # playing or paused? (only checked while a video is around)
    if ($st.tick % 15 -eq 7 -and ($videoOn -or $st.mode -eq 'watch' -or $st.mode -eq 'paused')) {
        $st.media = Get-MediaStatus
        if ($st.media -eq 'paused') { $st.pausedPolls++ } else { $st.pausedPolls = 0 }
    }
    $mediaPaused = $st.pausedPolls -ge 2

    # keyboard activity (counts only)
    $kp = [ClawdNative]::KeyPresses()
    $st.keyCount = $st.keyCount * 0.97 + $kp
    if ($kp -gt 0) { $st.lastKeyTick = $st.tick; $st.typeHand = -not $st.typeHand }

    # launched again from Start while already running
    if ($poke.WaitOne(0) -and -not $st.drag) { Hide-Cloud; Set-Mode 'wave'; $st.poked = $true }

    Update-Bullets $cur

    if ($st.drag) {
        if ($st.moved) {
            $st.phase = [int][Math]::Floor($st.tick / 2) % 4
            $st.arms = if ([Math]::Floor($st.tick / 3) % 2 -eq 0) { 'up' } else { 'down' }
            $st.mouth = 'o'
        }
    } elseif ($st.menuOpen) {
        # pondering what to do next while the thought cloud is open
        $st.phase = 0; $st.eye = 0; $st.eyeStyle = 'up'
        if ($st.mode -eq 'sleep') { Set-Mode 'idle' 30 }
        # click anywhere else (or the cloud vanished) -> close it
        if (-not $cloud.Visible) { $st.menuOpen = $false }
        elseif ([System.Windows.Forms.Control]::MouseButtons -ne 'None' -and -not $cloud.Bounds.Contains($cur) -and -not $form.Bounds.Contains($cur)) { Hide-Cloud }
    } else {
        $free = ($st.mode -eq 'walk' -or $st.mode -eq 'idle' -or $st.mode -eq 'chase')

        # you're watching something: he comes over with popcorn (and doesn't fall asleep)
        if ($videoOn -and ($free -or $st.mode -eq 'sleep' -or $st.mode -eq 'yawn')) { Set-Mode 'gowatch'; $free = $false }

        # sleepy after a minute of no input
        if ($free -and $st.idleMs -ge 60000) { Set-Mode 'yawn'; $free = $false }

        # being petted (rub the cursor over him)
        if ($free -or $st.mode -eq 'wave') {
            if ($overBody -and $curMoved) { $st.hover++ } elseif (-not $overBody) { $st.hover = 0 }
            if ($st.hover -ge 15) { Set-Mode 'pet'; $st.hover = 25; $free = $false }
        }

        # wave when the cursor comes to visit
        if ($cdist -gt 350) { $st.wasFar = $true }
        if ($free -and $st.wasFar -and $cdist -lt 160 -and $st.tick -gt $st.waveCD) {
            Set-Mode 'wave'; $st.wasFar = $false; $st.waveCD = $st.tick + 600
        }

        # you're typing -> he gets his laptop out and types along
        $canType = $free -or $st.mode -eq 'wave' -or $st.mode -eq 'think' -or $st.mode -eq 'dance' -or $st.mode -eq 'coffee'
        if ($canType -and $st.keyCount -ge 3) { Set-Mode 'typing'; $free = $false }

        # windows are for climbing! (looks around every ~2 s while he's free)
        if ($free -and $st.mode -ne 'chase' -and $st.tick % 60 -eq 30 -and $st.tick -gt $st.climbCD) {
            if (Try-Climb $w $h $wa) { $free = $false }
        }

        switch ($st.mode) {
            'fall' {
                $prevFeet = $st.y + $h
                $st.vy = [Math]::Min($st.vy + $GRAV, 30 * $k)
                $st.y += $st.vy; $st.x += $st.vx
                $feet = $st.y + $h
                $st.phase = 0; $st.eyeStyle = if ($st.vy -lt 0) { 'up' } else { 'normal' }
                $st.arms = if ($st.vy -lt 0) { 'up' } else { 'out' }
                if ($st.vy -gt 0) {
                    $landed = $false
                    $hw = [ClawdNative]::FindTop($self, [int]($st.x + $w / 2), 4, [int]$prevFeet - 1, [int]$feet)
                    if ($hw -ne [IntPtr]::Zero) {
                        $r = [ClawdNative]::Rect($hw)
                        $st.y = [double]($r[1] - $h); $st.plat = $hw; $st.platRect = $r; $landed = $true
                        $st.climbCD = $st.tick + 900          # enjoy this window for a bit before eyeing the next one
                    } elseif ($st.y -ge $ground) { $st.y = $ground; $landed = $true }
                    if ($landed) {
                        if ($st.vy -gt 8 * $k) { $st.squash = 6 }
                        $st.vy = 0.0; $st.vx = 0.0; $st.walkOff = $false
                        Set-Mode 'idle' 25
                    }
                }
            }
            'walk' {
                $st.x += $SPD * $st.dir
                $st.y = $ground
                $st.eye = $st.dir
                if ($st.tick % 5 -eq 0) { $st.phase = ($st.phase + 1) % 4 }
                if ($cdist -lt 220) {
                    $st.eye = $lookEye; if ($lookUp) { $st.eyeStyle = 'up' }
                    if ($rng.Next(200) -eq 0) { Set-Mode 'idle' (60 + $rng.Next(80)) }
                }
                # at the edge of a window: usually turn around, sometimes walk right off
                if ($st.platRect -and -not $st.walkOff) {
                    $r = $st.platRect
                    if (($st.dir -lt 0 -and $cxw -lt $r[0] + 15) -or ($st.dir -gt 0 -and $cxw -gt $r[2] - 15)) {
                        if ($rng.Next(5) -eq 0) { $st.walkOff = $true } else { $st.dir = -$st.dir }
                    }
                }
                if ($rng.Next(300) -eq 0) { Set-Mode 'idle' (40 + $rng.Next(160)) }
                elseif ($rng.Next(1000) -eq 0) { Start-Chase }
                elseif ($rng.Next(1200) -eq 0) { Start-Hop }
            }
            'idle' {
                $st.y = $ground; $st.phase = 0
                if ($cdist -lt 900) { $st.eye = $lookEye; if ($lookUp) { $st.eyeStyle = 'up' } }
                elseif ($rng.Next(45) -eq 0) { $st.eye = $rng.Next(3) - 1 }
                $st.timer--
                if ($st.timer -le 0) { Pick-Activity $w $h $wa }
            }
            'chase' {
                $st.y = $ground
                if ([Math]::Abs($cdx) -gt 12) {
                    $st.dir = if ($cdx -gt 0) { 1 } else { -1 }
                    $st.x += 3.0 * $k * $st.dir
                    if ($st.tick % 3 -eq 0) { $st.phase = ($st.phase + 1) % 4 }
                    $st.eye = $st.dir
                } else { $st.phase = 0; $st.eye = 0 }
                if ($lookUp) { $st.eyeStyle = 'up' }
                if ($st.t -eq 1) { Say 'gonna get it!' 40 }
                if ([Math]::Abs($cdx) -lt 40 -and $cdy -lt -20 -and $cdy -gt -220 -and $rng.Next(25) -eq 0) { Start-Hop (12 * $k) 0 }
                elseif ($st.t -ge $st.timer) { Set-Mode 'idle' 50 }
            }
            'crouch' {
                $st.y = $ground; $st.phase = 0; $st.sit = $true; $st.eyeStyle = 'up'; $st.arms = 'down'
                if ($st.t -ge 12) {
                    if ([ClawdNative]::Usable($st.target, $self)) {
                        $r = [ClawdNative]::Rect($st.target)
                        $dy = ($st.y + $h) - $r[1]
                        if ($dy -gt 0) {
                            $st.climbCD = $st.tick + 240
                            Leave-Platform; Set-Mode 'fall'
                            $st.vy = -([Math]::Sqrt(2 * $GRAV * $dy) + 2 * $k); $st.vx = 0.0
                        } else { Set-Mode 'idle' 20 }
                    } else { Set-Mode 'idle' 20 }
                }
            }
            'gowatch' {
                $st.y = $ground
                if (-not $videoOn) { Set-Mode 'idle' 30 }
                elseif ($st.plat -ne [IntPtr]::Zero) { Leave-Platform; Set-Mode 'fall'; $st.vy = 0.0; $st.vx = 0.0 }
                else {
                    if ($st.t -eq 1) {
                        Say 'ooh, a video!' 45; $st.resumed = $false
                        # find a seat: right here if he's already under the video, otherwise some random spot under it
                        $r = $st.videoRect
                        $lo = [Math]::Max($wa.Left + 80, $r[0] + 60); $hi = [Math]::Min($wa.Right - 80, $r[2] - 60)
                        if ($hi -le $lo) { $lo = $wa.Left + 80; $hi = $wa.Right - 80 }
                        $st.watchX = if ($cxw -ge $lo -and $cxw -le $hi) { $cxw } else { $lo + $rng.NextDouble() * ($hi - $lo) }
                    }
                    $dx = $st.watchX - $cxw
                    if ([Math]::Abs($dx) -gt 8 * $k) {
                        $st.dir = if ($dx -gt 0) { 1 } else { -1 }
                        $st.x += 2.4 * $k * $st.dir; $st.eye = $st.dir
                        if ($st.tick % 4 -eq 0) { $st.phase = ($st.phase + 1) % 4 }
                    } else { Set-Mode 'watch'; $st.nextComment = 250 + $rng.Next(200) }
                }
            }
            'watch' {
                $st.y = $ground
                if (-not $videoOn) { Say 'good show.' 50; Set-Mode 'idle' 40 }
                elseif ($mediaPaused) { Set-Mode 'paused' }
                else {
                    Step-Watch
                    # running commentary
                    if ($st.t -ge $st.nextComment) {
                        $line = Get-Comment
                        Say $line 80
                        if ($line -match 'lol|hehe') { $st.laughT = 24 }
                        $st.nextComment = $st.t + 330 + $rng.Next(420)
                    }
                }
            }
            'paused' {
                # you paused the video. he is NOT happy about it.
                $st.y = $ground; $t = $st.t
                if (-not $videoOn) { Say 'fine, be that way.' 50; Set-Mode 'idle' 40 }
                elseif (-not $mediaPaused) {
                    Say $resumeLines[$rng.Next($resumeLines.Count)] 50
                    Set-Mode 'watch'; $st.resumed = $true; $st.nextComment = 300 + $rng.Next(300)
                } elseif ($t -lt 240) {
                    # turn around and stomp
                    $st.eye = 0; $st.eyeStyle = 'angry'; $st.mouth = 'o'
                    $st.bang = ($t -lt 45 -and $t % 8 -lt 5)
                    if ($t -lt 90) {
                        $st.arms = if ([Math]::Floor($t / 4) % 2 -eq 0) { 'up' } else { 'down' }
                        $st.phase = if ([Math]::Floor($t / 4) % 2 -eq 0) { 1 } else { 3 }
                        $st.wob = if ($t % 4 -lt 2) { 1 } else { -1 }
                    } else { $st.arms = 'down'; $st.phase = 0 }
                    if ($t -eq 1 -or $t % 75 -eq 0) { Say $complainLines[$rng.Next($complainLines.Count)] 60 }
                } else {
                    # sulk: sits back down facing the (frozen) screen, waiting
                    $st.back = $true; $st.sit = $true; $st.arms = 'rest'; $st.phase = 0
                    if ($t % 220 -eq 0) { Say $sulkLines[$rng.Next($sulkLines.Count)] 50 }
                }
            }
            'typing' {
                $st.y = $ground; $st.phase = 0; $st.laptop = 'open'; $st.glow = 'blue'
                $since = $st.tick - $st.lastKeyTick
                if ($since -lt 30) {
                    # typing along with you, one hand per key press
                    $st.eyeStyle = 'down'; $st.eye = 0
                    $st.arms = if ($since -lt 5) { if ($st.typeHand) { 'typeL' } else { 'typeR' } } else { 'down' }
                    if ($kp -gt 0 -and $rng.Next(3) -eq 0) {
                        Spawn-Float 'dot' $(if ($rng.Next(2) -eq 0) { $OX + 1 + $rng.Next(3) } else { $OX + 18 + $rng.Next(3) }) ($OY + 5) (($rng.NextDouble() - 0.5) * 0.3) -0.3 14 $bBlue
                    }
                } elseif ($since -lt 360) {
                    # you paused: he thinks
                    if ($since -eq 30 -or $since % 60 -eq 0) { $st.word = $thinkWords[$rng.Next($thinkWords.Count)] }
                    $st.eyeStyle = 'up'; $st.eye = 1; $st.arms = 'down'
                    $st.spin = $true; Say ($st.word + '...') 2
                } else {
                    Set-Mode 'idle' 30   # laptop away
                }
            }
            'goclimb' {
                $st.y = $ground
                if (-not [ClawdNative]::Usable($st.target, $self) -or $st.t -gt 600) { $st.climbCD = $st.tick + 300; Set-Mode 'idle' 30 }
                else {
                    if ($st.t -eq 1) { $l = @('ooh, a window!', 'up there!', 'I can make that', 'climb time!'); Say $l[$rng.Next($l.Count)] 45 }
                    $dx = $st.climbX - $cxw
                    if ([Math]::Abs($dx) -gt 6 * $k) {
                        $st.dir = if ($dx -gt 0) { 1 } else { -1 }
                        $st.x += 2.8 * $k * $st.dir; $st.eye = $st.dir; $st.eyeStyle = 'up'
                        if ($st.tick % 3 -eq 0) { $st.phase = ($st.phase + 1) % 4 }
                    } else { Set-Mode 'crouch' }
                }
            }
            'gun' {
                $st.y = $ground; $st.phase = 0; $t = $st.t
                if ($t -lt 140) { Aim-At $cur.X $cur.Y; $st.eyeStyle = 'angry' }
                elseif ($st.hits -gt 0) { $st.eyeStyle = 'happy'; $st.mouth = 'smile' }
                else { $st.mouth = 'o' }
                if ($t -eq 1) { Say 'hold still...' 45; $st.hits = 0 }
                if ($t -ge 30 -and $t -le 102 -and ($t - 30) % 18 -eq 0) { Fire-Gun $cur }
                if ($t -eq 140) { if ($st.hits -gt 0) { Say 'gotcha!' 50 } else { Say 'dang it!' 50 } }
                if ($t -ge 180) { Set-Mode 'idle' 30 }
            }
            'trick' {
                $t = $st.t; $st.phase = 0; $resultT = 110
                if ($t -eq 1) {
                    $st.trick = @('noscope', 'noscope', 'nolook', 'ricochet')[$rng.Next(4)]
                    $st.hits = 0; $st.jy = 0.0; $st.jv = 0.0; $st.air = $false
                    switch ($st.trick) {
                        'noscope'  { Say '360 no-scope. watch.' 50 }
                        'nolook'   { Say "don't even need to look" 50 }
                        'ricochet' { Say 'bank shot. watch this.' 50 }
                    }
                }
                if ($t -lt $resultT) {
                    switch ($st.trick) {
                        'noscope' {
                            # crouch, jump, full spin in the air, fire at the top
                            if ($t -lt 22) { $st.sit = $true; $st.eyeStyle = 'happy' }
                            if ($t -eq 22) { $st.jv = 15 * $k; $st.air = $true }
                            if ($st.air) {
                                $st.jy += $st.jv; $st.jv -= $GRAV
                                if ($st.jy -le 0) { $st.jy = 0.0; $st.air = $false; $st.squash = 6 }
                            }
                            Aim-At $cur.X $cur.Y
                            if ($t -ge 23 -and $t -lt 41) { $st.rot = ($t - 23) * 20; $st.eyeStyle = 'happy' }
                            elseif ($t -ge 41) { $st.eyeStyle = 'angry' }
                            if ($t -eq 41) { Fire-Gun $cur }
                        }
                        'nolook' {
                            # eyes shut, smug, still on target
                            Aim-At $cur.X $cur.Y; $st.eye = 0; $st.eyeStyle = 'sleep'; $st.mouth = 'smile'
                            if ($t -eq 45) { Fire-Gun $cur }
                        }
                        'ricochet' {
                            # shoot the floor halfway to the cursor; it bounces up into it
                            $fx = ($cxw + $cur.X) / 2; $fy = $st.y + $h - 2
                            Aim-At $fx $fy; $st.eyeStyle = 'angry'
                            if ($t -eq 45) { Fire-Gun $cur (New-Object System.Drawing.Point ([int]$fx), ([int]$fy)) }
                        }
                    }
                } elseif ($t -eq $resultT) {
                    if ($st.hits -gt 0) {
                        Say 'TRICKSHOT!!' 70
                        for ($i = 0; $i -lt 6; $i++) { Spawn-Float 'star' ($OX + 4 + $rng.Next(14)) ($OY - 2 - $rng.Next(4)) (($rng.NextDouble() - 0.5) * 0.4) -0.3 35 $bSpark }
                    } else { $l = @('meant to do that.', 'that counts.', 'wind, obviously.', 'lag.'); Say $l[$rng.Next($l.Count)] 60 }
                } else {
                    $st.eyeStyle = if ($st.hits -gt 0) { 'happy' } else { 'normal' }
                    if ($st.hits -gt 0) { $st.mouth = 'smile' }
                    if ($t -ge $resultT + 60) { Set-Mode 'idle' 30 }
                }
                $st.y = $ground - $st.jy
            }
            'wave' {
                $st.y = $ground; $st.phase = 0; $st.eye = $lookEye
                $st.eyeStyle = 'happy'; $st.mouth = 'smile'
                $st.arms = if ([Math]::Floor($st.t / 6) % 2 -eq 0) { 'wave1' } else { 'wave2' }
                if ($st.t -eq 1) {
                    if ($st.poked) { Say "I'm right here!" 60; $st.poked = $false } else { Say 'hi!' 50 }
                }
                if ($st.t -ge 60) { Set-Mode 'idle' 40 }
            }
            'pet' {
                $st.y = $ground; $st.phase = 0; $st.eye = 0
                $st.eyeStyle = 'happy'; $st.blush = $true; $st.mouth = 'smile'
                if ($overBody) { $st.hover = 25 } else { $st.hover-- }
                if ($st.t % 10 -eq 1) { Spawn-Float 'heart' ($OX + 9 + $rng.Next(-8, 9)) ($OY - 4) (($rng.NextDouble() - 0.5) * 0.2) -0.25 40 $bHeart }
                if ($st.t -eq 1) { Say 'hehe' 45 }
                if ($st.hover -le 0) { Set-Mode 'idle' 30 }
            }
            'dizzy' {
                $st.y = $ground; $st.phase = 0; $st.eye = 0
                $st.eyeStyle = 'dizzy'; $st.mouth = 'o'; $st.arms = 'down'
                $st.wob = if ([Math]::Floor($st.t / 5) % 2 -eq 0) { 1 } else { -1 }
                if ($st.t -eq 1) { Say '@_@' 50 }
                if ($st.t -ge $st.timer) { Set-Mode 'idle' 30 }
            }
            'dance' {
                $st.y = $ground; $st.phase = 0; $st.eye = 0
                $st.eyeStyle = 'happy'; $st.mouth = 'smile'
                $beat = [int][Math]::Floor($st.t / 8)
                $st.sit = ($beat % 2 -eq 1)
                $st.arms = if ($beat % 2 -eq 0) { 'upL' } else { 'upR' }
                $st.x += if ($beat % 4 -lt 2) { 0.6 * $k } else { -0.6 * $k }
                if ($st.t % 14 -eq 1) {
                    Spawn-Float 'note' ($OX + 9 + $rng.Next(-10, 11)) ($OY - 4) (($rng.NextDouble() - 0.5) * 0.25) -0.3 45 $noteBrushes[$rng.Next($noteBrushes.Count)]
                }
                if ($st.t -ge $st.timer) { Start-Hop (6 * $k) 0 }
            }
            'coffee' {
                $st.y = $ground; $st.phase = 0
                $t = $st.t
                $sipping = ($t -ge 30 -and $t -lt 55) -or ($t -ge 95 -and $t -lt 120) -or ($t -ge 160 -and $t -lt 185)
                if ($sipping) { $st.mug = 'sip'; $st.arms = 'sip'; $st.eyeStyle = 'sleep'; $st.eye = 0 }
                else {
                    $st.mug = 'hold'
                    if ($t -gt 55) { $st.eyeStyle = 'happy' } elseif ($cdist -lt 900) { $st.eye = $lookEye }
                }
                if ($t -gt 55) { $st.blush = $true }
                if ($t -eq 1) { Say 'coffee time' 40 }
                if ($t -eq 190) { Say 'ahh.' 45 }
                if ($t -ge 225) { Set-Mode 'idle' 30 }
            }
            'think' {
                $st.y = $ground; $st.phase = 0; $st.eye = 1; $st.eyeStyle = 'up'
                $t = $st.t
                if ($t -lt 150) {
                    if ($t % 50 -eq 0) { $st.word = $thinkWords[$rng.Next($thinkWords.Count)] }
                    $st.spin = $true; Say ($st.word + '...') 2
                } else {
                    $st.bang = ($t -lt 165); $st.eyeStyle = 'normal'; $st.eye = 0; $st.mouth = 'o'
                    if ($t -eq 150) { Say 'got it!' 50 }
                    if ($t -ge 168) { Start-Hop (7 * $k) 0 }
                }
            }
            'yawn' {
                $st.y = $ground; $st.phase = 0; $st.eye = 0
                $st.arms = 'up'; $st.eyeStyle = 'sleep'
                if ($st.t -lt 32) { $st.mouth = 'yawn' }
                if ($st.idleMs -lt 1500) { Set-Mode 'idle' 30 }
                elseif ($st.t -ge 45) { Set-Mode 'sleep' }
            }
            'sleep' {
                $st.y = $ground; $st.phase = 0; $st.eye = 0
                $st.sit = $true; $st.eyeStyle = 'sleep'; $st.arms = 'down'
                if ($st.t % 35 -eq 1) { Spawn-Float 'z' ($OX + 17) ($OY - 2) 0.12 -0.18 60 $bBlue }
                if ($st.idleMs -lt 1500) {
                    $st.sit = $false; $st.eyeStyle = 'up'
                    Say 'huh?!' 40; Start-Hop (7 * $k) 0
                }
            }
            'smash' { $st.y = $ground; Step-Smash }
        }

        # keep him on screen
        $minX = $wa.Left - ($OX + 4) * $S
        $maxX = $wa.Right - ($OX + 18) * $S
        if ($st.x -le $minX) { $st.x = [double]$minX; $st.dir = 1; $st.vx = [Math]::Abs($st.vx) }
        if ($st.x -ge $maxX) { $st.x = [double]$maxX; $st.dir = -1; $st.vx = -[Math]::Abs($st.vx) }
        if ($st.mode -ne 'fall' -and $st.y -gt $ground) { $st.y = $ground }

        $shakeX = 0; $shakeY = 0
        if ($st.shake -gt 0) { $st.shake--; $shakeX = ($rng.Next(5) - 2) * $S; $shakeY = ($rng.Next(3) - 1) * $S }
        $form.Location = New-Object System.Drawing.Point ([int]$st.x + $shakeX), ([int]$st.y + $shakeY)
    }

    Update-Particles
    Render-Frame
    if ($st.tick % 300 -eq 0) {
        $form.TopMost = $true; $form.BringToFront()
        [ClawdNative]::HideFromAltTab($form.Handle)
    }
  } catch {
    if ($script:errCount -lt 5) { $script:errCount++; Write-Log "$_ @ line $($_.InvocationInfo.ScriptLineNumber)" }
  }
})

# the laptop-smash sequence, one tick at a time (driven by $st.t)
function Step-Smash {
                $t = $st.t
                $st.phase = 0; $st.eye = 0
                if ($t -lt 15) {
                    $st.laptop = 'open'; $st.glow = 'blue'; $st.eyeStyle = 'down'
                } elseif ($t -lt 110) {
                    $st.laptop = 'open'; $st.glow = 'blue'; $st.eyeStyle = 'down'
                    $st.arms = if ([Math]::Floor($t / 3) % 2 -eq 0) { 'typeL' } else { 'typeR' }
                    if ($t % 9 -eq 0) { Spawn-Float 'dot' $(if ($rng.Next(2) -eq 0) { $OX + 1 + $rng.Next(3) } else { $OX + 18 + $rng.Next(3) }) ($OY + 5) (($rng.NextDouble() - 0.5) * 0.3) -0.3 14 $bBlue }
                } elseif ($t -lt 160) {
                    $st.laptop = 'open'; $st.glow = 'red'; $st.eyeStyle = 'angry'
                    $st.arms = if ($t % 2 -eq 0) { 'typeL' } else { 'typeR' }
                    $st.bang = ($t % 8 -lt 5)
                } elseif ($t -lt 180) {
                    $st.laptop = 'open'; $st.glow = 'red'; $st.eyeStyle = 'angry'; $st.bang = $true
                } elseif ($t -lt 194) {
                    $st.laptop = 'held'; $st.arms = 'up'; $st.eyeStyle = 'angry'
                    $st.lapRow = ($OY + 9) - ($t - 180) / 14.0 * 14
                } elseif ($t -lt 212) {
                    $st.laptop = 'held'; $st.arms = 'up'; $st.eyeStyle = 'angry'
                    $st.lapRow = if ($t % 4 -lt 2) { $OY - 5 } else { $OY - 6 }
                } elseif ($t -lt 216) {
                    $st.laptop = 'held'; $st.arms = 'down'; $st.eyeStyle = 'angry'
                    $st.lapRow = ($OY - 6) + ($t - 212) / 4.0 * 18
                } elseif ($t -eq 216) {
                    Spawn-Debris; $st.shake = 12; $st.squash = 5; $st.arms = 'down'; $st.eyeStyle = 'angry'
                } elseif ($t -lt 250) {
                    $st.arms = 'down'; $st.eyeStyle = 'angry'
                } elseif ($t -lt 310) {
                    $st.eyeStyle = 'happy'; $st.mouth = 'smile'
                    if ($t -eq 250) { Say 'fixed it.' 55 }
                } elseif ($t -lt 380) {
                    if ($t % 2 -eq 0 -and $parts.Count -gt 0) { $parts.RemoveAt($rng.Next($parts.Count)) }
                } else {
                    $parts.Clear(); Set-Mode 'walk'
                }
}

# floating glyphs, debris and sparks
function Update-Particles {
    for ($i = $parts.Count - 1; $i -ge 0; $i--) {
        $p = $parts[$i]
        if ($p.kind -eq 'float') {
            $p.x += $p.vx + [Math]::Sin(($st.tick + $p.seed) * 0.2) * 0.08
            $p.y += $p.vy; $p.life--
            if ($p.life -le 0 -or $p.y -lt -6) { $parts.RemoveAt($i) }
        } else {
            $p.vy += 0.25; $p.x += $p.vx; $p.y += $p.vy
            if ($p.x -lt 0) { $p.x = 0.0; $p.vx = -$p.vx * 0.5 }
            if ($p.x -gt $GW - $p.sz) { $p.x = [double]($GW - $p.sz); $p.vx = -$p.vx * 0.5 }
            if ($p.y -ge $GH - $p.sz) {
                $p.y = [double]($GH - $p.sz); $p.vy = -$p.vy * 0.3; $p.vx *= 0.6
                if ([Math]::Abs($p.vy) -lt 0.4) { $p.vy = 0.0 }
            }
            if ($p.kind -eq 'spark') { $p.life--; if ($p.life -le 0) { $parts.RemoveAt($i) } }
        }
    }
}

$form.Add_Shown({
    [ClawdNative]::HideFromAltTab($form.Handle)
    $form.Location = New-Object System.Drawing.Point ([int]$st.x), ([int]$st.y)
    $timer.Start()
})
$form.Add_FormClosed({ $timer.Stop(); $mutex.ReleaseMutex() })

# ---------- demo renderer (for the README gif) ----------
function Invoke-Demo([string]$outDir) {
    New-Item -ItemType Directory -Force $outDir | Out-Null
    $stage = New-Object System.Drawing.Bitmap ($canvas.Width * 3), $canvas.Height
    $sg = [System.Drawing.Graphics]::FromImage($stage)
    $script:cx = [double]($canvas.Width * 0.15)
    $script:frameNo = 0

    function Tick-Base {
        $st.tick++; $st.t++
        $st.arms = 'out'; $st.eyeStyle = 'normal'; $st.mouth = 'none'; $st.blush = $false; $st.sit = $false
        $st.wob = 0; $st.mug = 'none'; $st.laptop = 'none'; $st.bang = $false; $st.spin = $false; $st.gun = 0; $st.back = $false; $st.rot = 0
        if ($st.blink -gt 0) { $st.blink-- } else { $st.nextBlink--; if ($st.nextBlink -le 0) { $st.blink = 4; $st.nextBlink = 80 + $rng.Next(150) } }
        if ($st.sayT -gt 0) { $st.sayT-- }
        if ($st.squash -gt 0) { $st.squash-- }
    }
    function Snap {
        Update-Particles
        if ($st.tick % 2 -ne 0) { return }      # one gif frame per two ticks
        Render-Frame
        $sx = 0
        if ($st.shake -gt 0) { $st.shake--; $sx = ($rng.Next(5) - 2) * $S }
        $sg.Clear($keyCol)
        $sg.DrawImage($canvas, [int]$script:cx + $sx, 0, $canvas.Width, $canvas.Height)
        $stage.Save((Join-Path $outDir ('f{0:D4}.png' -f $script:frameNo)), [System.Drawing.Imaging.ImageFormat]::Png)
        $script:frameNo++
    }
    function Typing-Frame([bool]$keys) {
        $st.laptop = 'open'; $st.glow = 'blue'; $st.eyeStyle = 'down'; $st.eye = 0
        if ($keys -and $st.t % 4 -eq 0) { $st.typeHand = -not $st.typeHand; $st.lastKeyTick = $st.tick
            if ($rng.Next(2) -eq 0) { Spawn-Float 'dot' $(if ($rng.Next(2) -eq 0) { $OX + 1 + $rng.Next(3) } else { $OX + 18 + $rng.Next(3) }) ($OY + 5) (($rng.NextDouble() - 0.5) * 0.3) -0.3 14 $bBlue } }
        $st.arms = if ($st.tick - $st.lastKeyTick -lt 3) { if ($st.typeHand) { 'typeL' } else { 'typeR' } } else { 'down' }
    }

    # walk in
    Set-Mode 'walk'; $st.dir = 1
    for ($i = 0; $i -lt 90; $i++) { Tick-Base; $st.eye = 1; $script:cx += $SPD; if ($st.tick % 5 -eq 0) { $st.phase = ($st.phase + 1) % 4 }; Snap }
    $st.phase = 0
    # wave
    Set-Mode 'wave'; Say 'hi!' 50
    for ($i = 0; $i -lt 56; $i++) { Tick-Base; $st.eyeStyle = 'happy'; $st.mouth = 'smile'; $st.arms = if ([Math]::Floor($st.t / 6) % 2 -eq 0) { 'wave1' } else { 'wave2' }; Snap }
    # types along with you...
    Set-Mode 'typing'
    for ($i = 0; $i -lt 70; $i++) { Tick-Base; Typing-Frame $true; Snap }
    # ...thinks when you pause...
    $st.word = 'Clauding'
    for ($i = 0; $i -lt 64; $i++) { Tick-Base; $st.laptop = 'open'; $st.glow = 'blue'; $st.arms = 'down'; $st.eye = 1; $st.eyeStyle = 'up'; $st.spin = $true; Say ($st.word + '...') 2; Snap }
    # ...and types again
    for ($i = 0; $i -lt 30; $i++) { Tick-Base; Typing-Frame $true; Snap }
    # dance
    Set-Mode 'dance'
    for ($i = 0; $i -lt 96; $i++) {
        Tick-Base; $st.eyeStyle = 'happy'; $st.mouth = 'smile'
        $beat = [int][Math]::Floor($st.t / 8); $st.sit = ($beat % 2 -eq 1); $st.arms = if ($beat % 2 -eq 0) { 'upL' } else { 'upR' }
        if ($st.t % 14 -eq 1) { Spawn-Float 'note' ($OX + 9 + $rng.Next(-10, 11)) ($OY - 4) (($rng.NextDouble() - 0.5) * 0.25) -0.3 45 $noteBrushes[$rng.Next($noteBrushes.Count)] }
        Snap
    }
    # gun: aim sweeps from straight up, round to the front and down, shooting (no real bullets in the demo)
    Set-Mode 'gun'; Say 'hold still...' 45
    for ($i = 0; $i -lt 96; $i++) {
        Tick-Base; if ($st.flash -gt 0) { $st.flash-- }; if ($st.recoil -gt 0) { $st.recoil-- }
        $st.gun = 1; $st.arms = 'aim'; $st.aimSide = 1; $st.eye = 1; $st.eyeStyle = 'angry'
        $st.aimDeg = -105 + $i * 1.45
        if ($i % 16 -eq 8) { $st.flash = 3; $st.recoil = 3 }
        Snap
    }
    # ...other side, then a 360 spin shot
    for ($i = 0; $i -lt 40; $i++) {
        Tick-Base; if ($st.flash -gt 0) { $st.flash-- }; if ($st.recoil -gt 0) { $st.recoil-- }
        $st.gun = 1; $st.arms = 'aim'; $st.aimSide = -1; $st.eye = -1; $st.eyeStyle = 'angry'
        $st.aimDeg = 180 + 30 - $i * 1.5
        if ($i % 16 -eq 8) { $st.flash = 3; $st.recoil = 3 }
        Snap
    }
    Set-Mode 'trick'; Say '360 no-scope. watch.' 45
    for ($i = 0; $i -lt 70; $i++) {
        Tick-Base; if ($st.flash -gt 0) { $st.flash-- }; if ($st.recoil -gt 0) { $st.recoil-- }
        $st.gun = 1; $st.arms = 'aim'; $st.aimSide = 1; $st.eye = 1; $st.aimDeg = -20
        if ($i -lt 14) { $st.sit = $true; $st.eyeStyle = 'happy' }
        elseif ($i -lt 32) { $st.rot = ($i - 14) * 20; $st.eyeStyle = 'happy' }
        else { $st.eyeStyle = 'angry' }
        if ($i -eq 32) { $st.flash = 4; $st.recoil = 3 }
        if ($i -eq 44) { Say 'TRICKSHOT!!' 40; for ($j = 0; $j -lt 6; $j++) { Spawn-Float 'star' ($OX + 4 + $rng.Next(14)) ($OY - 2 - $rng.Next(4)) (($rng.NextDouble() - 0.5) * 0.4) -0.3 35 $bSpark } }
        Snap
    }
    # laptop smash
    $parts.Clear(); Set-Mode 'smash'
    for ($i = 0; $i -lt 330; $i++) { Tick-Base; if ($st.t -lt 15) { $st.t = 15 }; Step-Smash; Snap }
    while ($parts.Count -gt 0) { Tick-Base; $parts.RemoveAt($rng.Next($parts.Count)); if ($parts.Count -gt 0) { $parts.RemoveAt($rng.Next($parts.Count)) }; Snap }
    # movie night
    Set-Mode 'watch'
    for ($i = 0; $i -lt 200; $i++) { Tick-Base; Step-Watch; if ($i -eq 100) { Say 'plot twist!' 70 }; Snap }
    # ...and you paused it
    Set-Mode 'paused'
    for ($i = 0; $i -lt 110; $i++) {
        Tick-Base; $t = $st.t
        $st.eyeStyle = 'angry'; $st.mouth = 'o'; $st.bang = ($t -lt 45 -and $t % 8 -lt 5)
        if ($t -lt 90) {
            $st.arms = if ([Math]::Floor($t / 4) % 2 -eq 0) { 'up' } else { 'down' }
            $st.phase = if ([Math]::Floor($t / 4) % 2 -eq 0) { 1 } else { 3 }
            $st.wob = if ($t % 4 -lt 2) { 1 } else { -1 }
        } else { $st.arms = 'down'; $st.phase = 0 }
        if ($t -eq 1) { Say 'hey! I was watching!' 70 }
        Snap
    }
    $st.phase = 0
    Set-Mode 'watch'; $st.resumed = $true; Say 'finally!' 40
    for ($i = 0; $i -lt 60; $i++) { Tick-Base; Step-Watch; Snap }
    $st.back = $false
    # nap
    Set-Mode 'sleep'
    for ($i = 0; $i -lt 110; $i++) { Tick-Base; $st.sit = $true; $st.eyeStyle = 'sleep'; $st.arms = 'down'; if ($st.t % 35 -eq 1) { Spawn-Float 'z' ($OX + 17) ($OY - 2) 0.12 -0.18 60 $bBlue }; Snap }
    $sg.Dispose()
    "$script:frameNo frames -> $outDir"
}

if ($RenderFrames) { Invoke-Demo $RenderFrames; $mutex.ReleaseMutex(); return }
[System.Windows.Forms.Application]::Run($form)
