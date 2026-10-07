// "Change pet...": a thought cloud with one tile per species - a live mini preview (blinks, waves when you
// hover it), the name and tagline, a highlight in that pet's colour and a check on the current one.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Flippy
{
    class Picker
    {
        readonly LayeredWindow win = new LayeredWindow(false);
        readonly List<RectangleF> tiles = new List<RectangleF>();
        readonly Cells cells = new Cells(Sprite.CW, Sprite.CH);
        readonly Random rng = new Random();
        int hover = -1, frame, S = 3; float k = 1;
        PointF head; Native.RECT work;
        int[] blinkAt;
        public bool Open;
        public int ClosedAt = -100;
        public Action<Species> Chosen;

        public Picker()
        {
            win.MouseMove += (s, e) => { int h = Hit(e.X, e.Y); if (h != hover) { hover = h; Draw(); } };
            win.MouseLeave += (s, e) => { if (hover != -1) { hover = -1; Draw(); } };
            win.MouseUp += (s, e) =>
            {
                int h = Hit(e.X, e.Y);
                if (e.Button == MouseButtons.Left && h >= 0) { Species sp = SpeciesList.All[h]; Close(0); if (Chosen != null) Chosen(sp); }
            };
            win.Cursor = Cursors.Hand;
        }
        public Rectangle Bounds { get { return new Rectangle(win.SX, win.SY, win.Surf.W, win.Surf.H); } }

        public void Show(PointF headPt, Native.RECT wk, int s)
        {
            head = headPt; work = wk; S = s; k = s / 3f; hover = -1; Open = true; frame = 0;
            if (Program.Verbose) Program.Log("picker opened");
            blinkAt = new int[SpeciesList.All.Count];
            for (int i = 0; i < blinkAt.Length; i++) blinkAt[i] = 40 + rng.Next(120);
            Draw();
        }
        public void Close(int tick) { if (Open) { if (Program.Verbose) Program.Log("picker closed at tick " + tick + "\n" + Environment.StackTrace); Open = false; ClosedAt = tick; win.Hide(); } }
        // called every tick while open: keeps the previews alive
        public void Tick()
        {
            if (!Open) return;
            frame++;
            bool redraw = frame % 6 == 0 && hover >= 0;
            for (int i = 0; i < blinkAt.Length; i++)
            {
                if (frame == blinkAt[i]) redraw = true;
                if (frame == blinkAt[i] + 8) { redraw = true; blinkAt[i] = frame + 120 + rng.Next(200); }
            }
            if (redraw) Draw();
        }
        int Hit(int x, int y) { for (int i = 0; i < tiles.Count; i++) if (tiles[i].Contains(x, y)) return i; return -1; }

        void Draw()
        {
            int n = SpeciesList.All.Count;
            int pp = Math.Max(2, (int)Math.Round(2 * k));              // screen px per cell in the previews
            const int PW = 30, PH = 25, PX0 = Sprite.OX - 4, PY0 = Sprite.OY - 9;
            float nameW = 0;
            using (Font nf = new Font("Segoe UI Semibold", 9.5f * k, GraphicsUnit.Pixel)) using (Bitmap tmp = new Bitmap(1, 1)) using (Graphics mg = Graphics.FromImage(tmp))
                foreach (Species sp in SpeciesList.All) nameW = Math.Max(nameW, mg.MeasureString(sp.Name, nf).Width);
            float tileW = Math.Max(PW * pp + 16 * k, nameW + 16 * k), tileH = PH * pp + 44 * k, gap = 8 * k, titleH = 22 * k;
            float contentW = n * tileW + (n - 1) * gap, contentH = titleH + tileH;
            float bump = 16 * k, trail = 34 * k;
            float W = contentW + 2 * bump + 8 * k, H = contentH + 2 * bump + trail + 8 * k;
            float left = Math.Max(work.Left + 2, Math.Min(work.Right - W - 2, head.X - W / 2));
            float top = Math.Max(work.Top + 2, head.Y - H - 2 * k);
            float cx0 = 4 * k + bump, cy0 = 4 * k + bump;

            win.Surf.Ensure((int)W + 1, (int)H + 1); win.Surf.Clear();
            Graphics g = win.Surf.G; g.SmoothingMode = SmoothingMode.AntiAlias;

            // the cloud (same look as the menu)
            List<RectangleF> blobs = new List<RectangleF>();
            blobs.Add(new RectangleF(cx0 - 6 * k, cy0 - 6 * k, contentW + 12 * k, contentH + 12 * k));
            int nx = Math.Max(2, (int)(contentW / (26 * k))), ny = Math.Max(1, (int)(contentH / (26 * k)));
            for (int i = 0; i <= nx; i++)
            {
                float x = cx0 + contentW * i / nx, r = (i % 2 == 0 ? 17 : 13) * k, r2 = (i % 2 == 1 ? 17 : 13) * k;
                blobs.Add(new RectangleF(x - r, cy0 - r + 4 * k, 2 * r, 2 * r));
                blobs.Add(new RectangleF(x - r2, cy0 + contentH - r2 - 4 * k, 2 * r2, 2 * r2));
            }
            for (int j = 0; j <= ny; j++)
            {
                float y = cy0 + contentH * j / ny, r = (j % 2 == 0 ? 15 : 12) * k;
                blobs.Add(new RectangleF(cx0 - r + 4 * k, y - r, 2 * r, 2 * r));
                blobs.Add(new RectangleF(cx0 + contentW - r - 4 * k, y - r, 2 * r, 2 * r));
            }
            float hx = head.X - left, hy = head.Y - top, sx = cx0 + contentW / 2, sy = cy0 + contentH + 14 * k;
            float[] ts = { 0.25f, 0.6f, 0.88f }, rs = { 7f, 5f, 3.2f };
            for (int i = 0; i < 3; i++) { float x = sx + (hx - sx) * ts[i], y = sy + (hy - sy) * ts[i], r = rs[i] * k; blobs.Add(new RectangleF(x - r, y - r, 2 * r, 2 * r)); }
            Action<Brush, float, float> fillAll = (b, grow, dy) =>
            {
                for (int i = 0; i < blobs.Count; i++)
                {
                    RectangleF r = blobs[i], q = new RectangleF(r.X - grow, r.Y - grow + dy, r.Width + 2 * grow, r.Height + 2 * grow);
                    if (i == 0) using (GraphicsPath p = Rounded(q, 14 * k)) g.FillPath(b, p); else g.FillEllipse(b, q);
                }
            };
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(28, 0, 0, 0))) { fillAll(sh, 1.5f * k, 4 * k); fillAll(sh, 0.5f * k, 2.5f * k); }
            using (SolidBrush ink = new SolidBrush(Color.FromArgb(Pal.Ink))) fillAll(ink, 1.6f * k, 0);
            using (SolidBrush wh = new SolidBrush(Color.FromArgb(255, 254, 252))) fillAll(wh, 0, 0);

            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (Font tf = new Font("Segoe UI Semibold", 10.5f * k, GraphicsUnit.Pixel)) using (SolidBrush ib = new SolidBrush(Color.FromArgb(Pal.Ink)))
                g.DrawString("Change pet", tf, ib, cx0 + 2 * k, cy0);

            tiles.Clear();
            for (int i = 0; i < n; i++)
            {
                Species sp = SpeciesList.All[i];
                bool current = sp == SpeciesList.Current, hot = i == hover;
                RectangleF t = new RectangleF(cx0 + i * (tileW + gap), cy0 + titleH, tileW, tileH);
                tiles.Add(t);
                using (GraphicsPath p = Rounded(t, 10 * k))
                {
                    Color bg = hot ? Color.FromArgb(sp.Accent) : Color.FromArgb(246, 243, 239);
                    using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, p);
                    if (current) using (Pen pn = new Pen(Color.FromArgb(sp.Accent), 2f * k)) g.DrawPath(pn, p);
                }
                // live preview: idle, blinking now and then, waving when hovered
                Look L = new Look();
                L.Blink = frame >= blinkAt[i] && frame < blinkAt[i] + 8;
                if (hot) { L.EyeStyle = "happy"; L.Mouth = "smile"; L.Arms = (frame / 12) % 2 == 0 ? "wave1" : "wave2"; }
                cells.Clear(); Sprite.DrawFront(cells, L, sp);
                g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(cells.ToBitmap(), new RectangleF(t.X + (t.Width - PW * pp) / 2, t.Y + 6 * k, PW * pp, PH * pp), new RectangleF(PX0, PY0, PW, PH), GraphicsUnit.Pixel);
                g.PixelOffsetMode = PixelOffsetMode.Default;
                Color tc = hot ? Color.White : Color.FromArgb(Pal.Ink), sc = hot ? Color.FromArgb(235, 255, 255, 255) : Color.FromArgb(130, 120, 115);
                using (Font nf = new Font("Segoe UI Semibold", 9.5f * k, GraphicsUnit.Pixel)) using (SolidBrush nb = new SolidBrush(tc))
                    g.DrawString(sp.Name, nf, nb, new RectangleF(t.X + 6 * k, t.Y + PH * pp + 8 * k, t.Width - 12 * k, 14 * k));
                using (Font lf = new Font("Segoe UI", 8f * k, GraphicsUnit.Pixel)) using (SolidBrush lb = new SolidBrush(sc))
                    g.DrawString(sp.Tagline, lf, lb, new RectangleF(t.X + 6 * k, t.Y + PH * pp + 22 * k, t.Width - 12 * k, 22 * k));
                if (current)
                {
                    float r = 7 * k, cx = t.Right - r - 5 * k, cy = t.Top + r + 5 * k;
                    using (SolidBrush cb = new SolidBrush(Color.FromArgb(sp.Accent))) g.FillEllipse(cb, cx - r, cy - r, 2 * r, 2 * r);
                    using (Pen ck = new Pen(Color.White, 1.8f * k)) g.DrawLines(ck, new[] { new PointF(cx - 3.5f * k, cy), new PointF(cx - 1 * k, cy + 2.5f * k), new PointF(cx + 3.5f * k, cy - 2.5f * k) });
                }
            }
            win.Present((int)left, (int)top);
            win.KeepOnTop();
        }

        static GraphicsPath Rounded(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            rad = Math.Min(rad, Math.Min(r.Width, r.Height) / 2);
            p.AddArc(r.Left, r.Top, rad * 2, rad * 2, 180, 90); p.AddArc(r.Right - rad * 2, r.Top, rad * 2, rad * 2, 270, 90);
            p.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90); p.AddArc(r.Left, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
            p.CloseFigure(); return p;
        }
    }
}
