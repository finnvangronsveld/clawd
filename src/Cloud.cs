// The right-click menu: a thought cloud above his head with his mood bars on top.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Clawd
{
    class MenuItem
    {
        public string Text; public Action Act; public bool Sep;
        public MenuItem(string t, Action a) { Text = t; Act = a; }
        public static MenuItem Separator() { MenuItem m = new MenuItem("", null); m.Sep = true; return m; }
    }

    class Cloud
    {
        readonly LayeredWindow win = new LayeredWindow(false);
        List<MenuItem> items = new List<MenuItem>();
        readonly List<RectangleF> rows = new List<RectangleF>();
        double[] needs; string[] needNames = { "food", "energy", "fun", "love" };
        int hover = -1, S = 3; float k = 1;
        PointF head; Native.RECT work;
        public bool Open;
        public int ClosedAt = -100;

        public Cloud()
        {
            win.MouseMove += (s, e) => { int h = HitRow(e.X, e.Y); if (h != hover) { hover = h; Draw(); } };
            win.MouseLeave += (s, e) => { if (hover != -1) { hover = -1; Draw(); } };
            win.MouseUp += (s, e) =>
            {
                int h = HitRow(e.X, e.Y);
                if (e.Button == MouseButtons.Left && h >= 0 && items[h].Act != null) { Action a = items[h].Act; Close(0); a(); }
            };
            win.Cursor = Cursors.Hand;
        }
        public Rectangle Bounds { get { return new Rectangle(win.SX, win.SY, win.Surf.W, win.Surf.H); } }

        public void Show(List<MenuItem> it, double[] nd, PointF headPt, Native.RECT wk, int s)
        {
            items = it; needs = nd; head = headPt; work = wk; S = s; k = s / 3f; hover = -1; Open = true;
            Draw();
        }
        public void Close(int tick) { if (Open) { Open = false; ClosedAt = tick; win.Hide(); } }

        int HitRow(int x, int y)
        {
            for (int i = 0; i < rows.Count; i++) if (!items[i].Sep && rows[i].Contains(x, y)) return i;
            return -1;
        }

        void Draw()
        {
            float rowH = 19 * k, sepH = 8 * k, padX = 14 * k, barsH = needs != null ? 4 * 12 * k + 10 * k : 0;
            float textW = 0;
            using (Font ft = new Font("Segoe UI Semibold", 9.5f * k, GraphicsUnit.Pixel))
            using (Bitmap tmp = new Bitmap(1, 1)) using (Graphics mg = Graphics.FromImage(tmp))
                foreach (MenuItem m in items) if (!m.Sep) textW = Math.Max(textW, mg.MeasureString(m.Text, ft).Width);
            float contentW = Math.Max(textW + 2 * padX, 150 * k), contentH = barsH;
            foreach (MenuItem m in items) contentH += m.Sep ? sepH : rowH;
            float bump = 16 * k, trail = 34 * k;
            float W = contentW + 2 * bump + 8 * k, H = contentH + 2 * bump + trail + 8 * k;
            float left = Math.Max(work.Left + 2, Math.Min(work.Right - W - 2, head.X - W / 2));
            float top = Math.Max(work.Top + 2, head.Y - H - 2 * k);
            float cx0 = 4 * k + bump, cy0 = 4 * k + bump;      // content origin inside the window

            win.Surf.Ensure((int)W + 1, (int)H + 1); win.Surf.Clear();
            Graphics g = win.Surf.G; g.SmoothingMode = SmoothingMode.AntiAlias;

            // cloud = a rounded core + scallops along the edges + three little bubbles trailing to his head
            List<RectangleF> blobs = new List<RectangleF>();
            blobs.Add(new RectangleF(cx0 - 6 * k, cy0 - 6 * k, contentW + 12 * k, contentH + 12 * k));
            int nx = Math.Max(2, (int)(contentW / (26 * k))), ny = Math.Max(1, (int)(contentH / (26 * k)));
            for (int i = 0; i <= nx; i++)
            {
                float x = cx0 + contentW * i / nx, r = (i % 2 == 0 ? 17 : 13) * k;
                blobs.Add(new RectangleF(x - r, cy0 - r + 4 * k, 2 * r, 2 * r));
                float r2 = (i % 2 == 1 ? 17 : 13) * k;
                blobs.Add(new RectangleF(x - r2, cy0 + contentH - r2 - 4 * k, 2 * r2, 2 * r2));
            }
            for (int j = 0; j <= ny; j++)
            {
                float y = cy0 + contentH * j / ny, r = (j % 2 == 0 ? 15 : 12) * k;
                blobs.Add(new RectangleF(cx0 - r + 4 * k, y - r, 2 * r, 2 * r));
                blobs.Add(new RectangleF(cx0 + contentW - r - 4 * k, y - r, 2 * r, 2 * r));
            }
            float hx = head.X - left, hy = head.Y - top;
            float sx = cx0 + contentW / 2, sy = cy0 + contentH + 14 * k;
            float[] ts = { 0.25f, 0.6f, 0.88f }, rs = { 7f, 5f, 3.2f };
            for (int i = 0; i < 3; i++)
            {
                float x = sx + (hx - sx) * ts[i], y = sy + (hy - sy) * ts[i], r = rs[i] * k;
                blobs.Add(new RectangleF(x - r, y - r, 2 * r, 2 * r));
            }
            Action<Brush, float, float, float> fillAll = (b, grow, dx, dy) =>
            {
                foreach (RectangleF r in blobs)
                {
                    RectangleF q = new RectangleF(r.X - grow + dx, r.Y - grow + dy, r.Width + 2 * grow, r.Height + 2 * grow);
                    if (r == blobs[0]) { using (GraphicsPath p = Rounded(q, 14 * k)) g.FillPath(b, p); }
                    else g.FillEllipse(b, q);
                }
            };
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(28, 0, 0, 0))) { fillAll(sh, 1.5f * k, 0, 4 * k); fillAll(sh, 0.5f * k, 0, 2.5f * k); }
            using (SolidBrush ink = new SolidBrush(Color.FromArgb(Pal.Ink))) fillAll(ink, 1.6f * k, 0, 0);
            using (SolidBrush wh = new SolidBrush(Color.FromArgb(255, 254, 252))) fillAll(wh, 0, 0, 0);

            // mood bars
            float y0 = cy0;
            if (needs != null)
            {
                int[] cols = { Pal.C(240, 160, 60), Pal.C(90, 160, 240), Pal.C(150, 110, 230), Pal.Heart };
                using (Font sf = new Font("Segoe UI", 8f * k, GraphicsUnit.Pixel)) using (SolidBrush lb = new SolidBrush(Color.FromArgb(120, 110, 105)))
                    for (int i = 0; i < 4; i++)
                    {
                        float y = y0 + i * 12 * k;
                        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                        g.DrawString(needNames[i], sf, lb, cx0 + 2 * k, y - 1 * k);
                        RectangleF bar = new RectangleF(cx0 + 48 * k, y + 3 * k, contentW - 54 * k, 6 * k);
                        using (GraphicsPath p = Rounded(bar, 3 * k)) using (SolidBrush bg = new SolidBrush(Color.FromArgb(236, 230, 225))) g.FillPath(bg, p);
                        RectangleF fill = new RectangleF(bar.X, bar.Y, Math.Max(3 * k, bar.Width * (float)(needs[i] / 100)), bar.Height);
                        using (GraphicsPath p = Rounded(fill, 3 * k)) using (SolidBrush fb = new SolidBrush(Color.FromArgb(needs[i] < 25 ? Pal.Red : cols[i]))) g.FillPath(fb, p);
                    }
                y0 += barsH;
                using (Pen sp = new Pen(Color.FromArgb(225, 218, 212), 1 * k)) g.DrawLine(sp, cx0, y0 - 5 * k, cx0 + contentW, y0 - 5 * k);
            }

            rows.Clear();
            using (Font ft = new Font("Segoe UI Semibold", 9.5f * k, GraphicsUnit.Pixel))
                for (int i = 0; i < items.Count; i++)
                {
                    MenuItem m = items[i];
                    float h = m.Sep ? sepH : rowH;
                    RectangleF r = new RectangleF(cx0, y0, contentW, h);
                    rows.Add(r);
                    if (m.Sep)
                        using (Pen sp = new Pen(Color.FromArgb(225, 218, 212), 1 * k)) { sp.DashPattern = new[] { 2f, 2f }; g.DrawLine(sp, cx0 + padX / 2, y0 + h / 2, cx0 + contentW - padX / 2, y0 + h / 2); }
                    else
                    {
                        bool hot = i == hover;
                        if (hot) using (GraphicsPath p = Rounded(new RectangleF(r.X + 2 * k, r.Y + 1 * k, r.Width - 4 * k, r.Height - 2 * k), 6 * k)) using (SolidBrush hb = new SolidBrush(Color.FromArgb(SpeciesList.Current.Accent))) g.FillPath(hb, p);
                        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                        using (SolidBrush tb = new SolidBrush(hot ? Color.White : Color.FromArgb(Pal.Ink))) g.DrawString(m.Text, ft, tb, r.X + padX, r.Y + 2.5f * k);
                    }
                    y0 += h;
                }
            win.Present((int)left, (int)top);
            win.KeepOnTop();
        }

        static GraphicsPath Rounded(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            rad = Math.Min(rad, Math.Min(r.Width, r.Height) / 2);
            if (rad < 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.Left, r.Top, rad * 2, rad * 2, 180, 90); p.AddArc(r.Right - rad * 2, r.Top, rad * 2, rad * 2, 270, 90);
            p.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90); p.AddArc(r.Left, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
            p.CloseFigure(); return p;
        }
    }
}
