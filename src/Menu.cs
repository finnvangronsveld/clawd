// The right-click menu: one clean panel that opens at the cursor, like a normal context menu.
// Main page: the pet (name, tagline, mood bars), actions in groups, "Change pet" (a second page in the
// same window, with live previews), Settings and Bye. One window for everything, so nothing can get left behind.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Flippy
{
    class MenuItem
    {
        public string Text, Hint; public Action Act; public bool Sep, Sub, Danger;
        public MenuItem(string t, Action a) { Text = t; Act = a; }
        public static MenuItem Separator() { MenuItem m = new MenuItem("", null); m.Sep = true; return m; }
        public static MenuItem Page(string t, string hint) { MenuItem m = new MenuItem(t, null); m.Sub = true; m.Hint = hint; return m; }
    }

    class PetMenu
    {
        readonly LayeredWindow win = new LayeredWindow(false);
        List<MenuItem> items = new List<MenuItem>();
        double[] needs;
        readonly List<RectangleF> rows = new List<RectangleF>();     // hit areas (window coords); main page: per item, pets page: 0 = back, 1.. = pets
        RectangleF panel;                                            // the panel inside the window (the rest is shadow)
        Point anchor; Native.RECT work; float sc = 1;
        int hover = -1, pressed = -1, frame;
        int[] blinkAt;
        readonly Random rng = new Random();
        readonly Cells cells = new Cells(Sprite.CW, Sprite.CH);
        public bool Open, PetsPage;
        public int ClosedAtMs = -100000;
        public Action<Species> PetChosen;

        static readonly Color Bg = Color.FromArgb(252, 252, 251), InkC = Color.FromArgb(34, 32, 38), Grey = Color.FromArgb(118, 116, 124),
                              Line = Color.FromArgb(232, 230, 228), DangerC = Color.FromArgb(206, 56, 56);
        static readonly string[] needNames = { "Food", "Energy", "Fun", "Love" };

        public PetMenu()
        {
            win.MouseMove += (s, e) => { int h = Hit(e.X, e.Y); if (h != hover) { hover = h; Draw(); } };
            win.MouseLeave += (s, e) => { if (hover != -1 || pressed != -1) { hover = -1; pressed = -1; Draw(); } };
            win.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { pressed = Hit(e.X, e.Y); Draw(); } };
            win.MouseUp += (s, e) =>
            {
                int h = Hit(e.X, e.Y), p = pressed; pressed = -1;
                if (e.Button != MouseButtons.Left || h < 0 || h != p) { Draw(); return; }
                Activate(h);
            };
            win.Cursor = Cursors.Hand;
        }

        public IntPtr Handle { get { return win.Handle; } }
        public Rectangle Bounds { get { return new Rectangle(win.SX, win.SY, win.Surf.W, win.Surf.H); } }
        // is this screen point on the panel itself (not on the soft shadow around it)?
        public bool PanelContains(int x, int y) { return Open && panel.Contains(x - win.SX, y - win.SY); }

        public void Show(List<MenuItem> it, double[] nd, Point at, Native.RECT wk, float scale)
        {
            items = it; needs = nd; anchor = at; work = wk; sc = scale;
            hover = -1; pressed = -1; PetsPage = false; Open = true;
            Draw();
            win.KeepOnTop();
            if (Program.Verbose) Program.Log("menu opened" + RowsForLog());
        }
        public void ShowPets()
        {
            if (!Open) return;
            PetsPage = true; hover = -1; pressed = -1; frame = 0;
            blinkAt = new int[SpeciesList.All.Count];
            for (int i = 0; i < blinkAt.Length; i++) blinkAt[i] = 40 + rng.Next(120);
            Draw();
            if (Program.Verbose) Program.Log("menu: pets page" + RowsForLog());
        }
        public void Close()
        {
            if (!Open) return;
            Open = false; PetsPage = false; hover = -1; pressed = -1; ClosedAtMs = Environment.TickCount;
            win.Hide();
        }

        void Activate(int h)
        {
            if (!PetsPage)
            {
                MenuItem m = items[h];
                if (m.Sub) { ShowPets(); return; }
                Close();
                if (m.Act != null) m.Act();
            }
            else if (h == 0) { PetsPage = false; hover = -1; Draw(); }      // back
            else
            {
                Species sp = SpeciesList.All[h - 1];
                Close();
                if (PetChosen != null) PetChosen(sp);
            }
        }

        // every tick while open: keeps the pet previews alive
        public void Tick()
        {
            if (!Open) return;
            frame++;
            bool redraw = frame % 6 == 0 && hover >= 0;
            if (PetsPage)
                for (int i = 0; i < blinkAt.Length; i++)
                {
                    if (frame == blinkAt[i]) redraw = true;
                    if (frame == blinkAt[i] + 8) { redraw = true; blinkAt[i] = frame + 120 + rng.Next(200); }
                }
            if (redraw) Draw();
        }

        int Hit(int x, int y)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (!PetsPage && items[i].Sep) continue;
                if (rows[i].Contains(x, y)) return i;
            }
            return -1;
        }

        // ---------- drawing ----------
        void Draw()
        {
            float s = sc, shadow = 12 * s, pad = 6 * s, rowH = 28 * s, sepH = 11 * s;
            using (Font fItem = new Font("Segoe UI", 12.5f * s, GraphicsUnit.Pixel))
            using (Font fBold = new Font("Segoe UI Semibold", 13.5f * s, GraphicsUnit.Pixel))
            using (Font fSmall = new Font("Segoe UI", 11f * s, GraphicsUnit.Pixel))
            using (Bitmap tmp = new Bitmap(1, 1)) using (Graphics mg = Graphics.FromImage(tmp))
            {
                Species cur = SpeciesList.Current;
                // ---- measure ----
                float contentW = 230 * s, contentH;
                if (!PetsPage)
                {
                    foreach (MenuItem m in items) if (!m.Sep)
                        contentW = Math.Max(contentW, mg.MeasureString(m.Text, fItem).Width + (m.Hint != null ? mg.MeasureString(m.Hint + "  >", fSmall).Width + 16 * s : 0) + 40 * s);
                    contentW = Math.Max(contentW, mg.MeasureString(cur.Name, fBold).Width + 70 * s);
                    contentH = HeaderH(s) + (needs != null ? 50 * s : 0) + sepH;
                    foreach (MenuItem m in items) contentH += m.Sep ? sepH : rowH;
                }
                else
                {
                    foreach (Species sp in SpeciesList.All)
                        contentW = Math.Max(contentW, Math.Max(mg.MeasureString(sp.Name, fBold).Width, mg.MeasureString(sp.Tagline, fSmall).Width) + 110 * s);
                    contentH = 34 * s + sepH + SpeciesList.All.Count * 54 * s;
                }
                contentH += 2 * pad;
                float PW = contentW, PH = contentH, W = PW + 2 * shadow, H = PH + 2 * shadow;

                // ---- place: like a context menu, at the cursor, flipped to stay on screen ----
                float left = anchor.X + 2 - shadow, top = anchor.Y + 2 - shadow;
                if (left + shadow + PW > work.Right - 4) left = anchor.X - PW - 2 - shadow;
                if (top + shadow + PH > work.Bottom - 4) top = anchor.Y - PH - 2 - shadow;
                left = Math.Max(work.Left + 4 - shadow, Math.Min(work.Right - 4 - PW - shadow, left));
                top = Math.Max(work.Top + 4 - shadow, Math.Min(work.Bottom - 4 - PH - shadow, top));

                win.Surf.Ensure((int)Math.Ceiling(W), (int)Math.Ceiling(H)); win.Surf.Clear();
                Graphics g = win.Surf.G; g.SmoothingMode = SmoothingMode.AntiAlias;
                panel = new RectangleF(shadow, shadow, PW, PH);
                float rad = 10 * s;

                // soft shadow, then the panel with a hairline border
                for (int i = 1; i <= 10; i++)
                {
                    float gr = i * s * 1.1f;
                    using (GraphicsPath p = Rounded(new RectangleF(panel.X - gr, panel.Y - gr + 3 * s, panel.Width + 2 * gr, panel.Height + 2 * gr), rad + gr))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(Math.Max(1, 9 - i * 4 / 5), 0, 0, 0))) g.FillPath(b, p);
                }
                using (GraphicsPath p = Rounded(panel, rad))
                {
                    using (SolidBrush b = new SolidBrush(Bg)) g.FillPath(b, p);
                    using (Pen pn = new Pen(Color.FromArgb(40, 0, 0, 0), 1f)) g.DrawPath(pn, p);
                }
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                float x0 = panel.X, y = panel.Y + pad;
                Color accent = Color.FromArgb(cur.Accent);
                rows.Clear();
                if (!PetsPage)
                {
                    // header: the pet, name and tagline
                    float th = 38 * s;
                    DrawThumb(g, cur, new Look(), new RectangleF(x0 + 12 * s, y + 6 * s, th, th));
                    using (SolidBrush b = new SolidBrush(InkC)) g.DrawString(cur.Name, fBold, b, x0 + 58 * s, y + 6 * s);
                    using (SolidBrush b = new SolidBrush(Grey)) g.DrawString(cur.Tagline, fSmall, b, x0 + 58 * s, y + 25 * s);
                    y += HeaderH(s);
                    if (needs != null)
                    {
                        int[] cols = { Pal.C(240, 150, 50), Pal.C(80, 150, 235), Pal.C(150, 105, 225), Pal.Heart };
                        float colW = (PW - 36 * s) / 2;
                        for (int i = 0; i < 4; i++)
                        {
                            float cx = x0 + 14 * s + (i % 2) * (colW + 8 * s), cy = y + (i / 2) * 24 * s;
                            using (SolidBrush b = new SolidBrush(Grey)) g.DrawString(needNames[i], fSmall, b, cx - 2 * s, cy);
                            RectangleF bar = new RectangleF(cx + 46 * s, cy + 6 * s, colW - 46 * s, 6 * s);
                            using (GraphicsPath p = Rounded(bar, 3 * s)) using (SolidBrush b = new SolidBrush(Color.FromArgb(234, 232, 230))) g.FillPath(b, p);
                            float f = (float)Math.Max(0, Math.Min(1, needs[i] / 100));
                            RectangleF fill = new RectangleF(bar.X, bar.Y, Math.Max(6 * s, bar.Width * f), bar.Height);
                            using (GraphicsPath p = Rounded(fill, 3 * s)) using (SolidBrush b = new SolidBrush(Color.FromArgb(needs[i] < 25 ? Pal.Red : cols[i]))) g.FillPath(b, p);
                        }
                        y += 50 * s;
                    }
                    Divider(g, x0, PW, y + sepH / 2, s); y += sepH;

                    for (int i = 0; i < items.Count; i++)
                    {
                        MenuItem m = items[i];
                        float h = m.Sep ? sepH : rowH;
                        RectangleF r = new RectangleF(x0 + pad, y, PW - 2 * pad, h);
                        rows.Add(r);
                        if (m.Sep) Divider(g, x0, PW, y + h / 2, s);
                        else
                        {
                            Color hi = m.Danger ? DangerC : accent;
                            if (i == hover || i == pressed)
                                using (GraphicsPath p = Rounded(r, 6 * s)) using (SolidBrush b = new SolidBrush(Color.FromArgb(i == pressed ? 70 : 38, hi))) g.FillPath(b, p);
                            using (SolidBrush b = new SolidBrush(m.Danger && i == hover ? DangerC : InkC)) g.DrawString(m.Text, fItem, b, r.X + 10 * s, r.Y + (h - 17 * s) / 2);
                            if (m.Sub)
                            {
                                string hint = (m.Hint ?? "") + "  >";
                                SizeF hs = g.MeasureString(hint, fSmall);
                                using (SolidBrush b = new SolidBrush(Grey)) g.DrawString(hint, fSmall, b, r.Right - hs.Width - 8 * s, r.Y + (h - 15 * s) / 2);
                            }
                        }
                        y += h;
                    }
                }
                else
                {
                    // back row
                    RectangleF back = new RectangleF(x0 + pad, y, PW - 2 * pad, 34 * s);
                    rows.Add(back);
                    if (hover == 0 || pressed == 0) using (GraphicsPath p = Rounded(back, 6 * s)) using (SolidBrush b = new SolidBrush(Color.FromArgb(pressed == 0 ? 70 : 38, accent))) g.FillPath(b, p);
                    using (SolidBrush b = new SolidBrush(Grey)) g.DrawString("<", fBold, b, back.X + 10 * s, back.Y + 7 * s);
                    using (SolidBrush b = new SolidBrush(InkC)) g.DrawString("Change pet", fBold, b, back.X + 26 * s, back.Y + 7 * s);
                    y += 34 * s;
                    Divider(g, x0, PW, y + sepH / 2, s); y += sepH;

                    for (int i = 0; i < SpeciesList.All.Count; i++)
                    {
                        Species sp = SpeciesList.All[i];
                        int idx = i + 1;
                        bool hot = idx == hover, isCur = sp == cur;
                        RectangleF r = new RectangleF(x0 + pad, y, PW - 2 * pad, 52 * s);
                        rows.Add(r);
                        if (hot || idx == pressed)
                            using (GraphicsPath p = Rounded(r, 8 * s)) using (SolidBrush b = new SolidBrush(Color.FromArgb(idx == pressed ? 70 : 42, Color.FromArgb(sp.Accent)))) g.FillPath(b, p);
                        Look L = new Look();
                        L.Blink = blinkAt != null && frame >= blinkAt[i] && frame < blinkAt[i] + 8;
                        if (hot) { L.EyeStyle = "happy"; L.Mouth = "smile"; L.Arms = (frame / 12) % 2 == 0 ? "wave1" : "wave2"; }
                        DrawThumb(g, sp, L, new RectangleF(r.X + 6 * s, r.Y + 5 * s, 42 * s, 42 * s));
                        using (SolidBrush b = new SolidBrush(InkC)) g.DrawString(sp.Name, fBold, b, r.X + 58 * s, r.Y + 8 * s);
                        using (SolidBrush b = new SolidBrush(Grey)) g.DrawString(sp.Tagline, fSmall, b, r.X + 58 * s, r.Y + 27 * s);
                        if (isCur)
                        {
                            float cr = 8 * s, cx = r.Right - cr - 10 * s, cy = r.Y + r.Height / 2;
                            using (SolidBrush b = new SolidBrush(Color.FromArgb(sp.Accent))) g.FillEllipse(b, cx - cr, cy - cr, 2 * cr, 2 * cr);
                            using (Pen ck = new Pen(Color.White, 2f * s)) g.DrawLines(ck, new[] { new PointF(cx - 4 * s, cy), new PointF(cx - 1 * s, cy + 3 * s), new PointF(cx + 4 * s, cy - 3 * s) });
                        }
                        y += 54 * s;
                    }
                }
                win.Present((int)left, (int)top);
            }
        }

        // for tests: the screen centre of every clickable row
        string RowsForLog()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(" rows:");
            for (int i = 0; i < rows.Count; i++) if (PetsPage || !items[i].Sep) sb.Append(" " + i + "@" + (int)(win.SX + rows[i].X + rows[i].Width / 2) + "," + (int)(win.SY + rows[i].Y + rows[i].Height / 2));
            return sb.ToString();
        }
        static float HeaderH(float s) { return 50 * s; }

        static void Divider(Graphics g, float x0, float w, float y, float s)
        {
            using (Pen p = new Pen(Line, Math.Max(1f, s))) g.DrawLine(p, x0 + 10 * s, y, x0 + w - 10 * s, y);
        }

        // the pet, pixel-crisp, centred in a box
        void DrawThumb(Graphics g, Species sp, Look L, RectangleF box)
        {
            const int PW = 30, PH = 26, PX0 = Sprite.OX - 4, PY0 = Sprite.OY - 10;
            cells.Clear(); Sprite.DrawFront(cells, L, sp);
            float k = Math.Min(box.Width / PW, box.Height / PH);
            RectangleF dst = new RectangleF(box.X + (box.Width - PW * k) / 2, box.Y + (box.Height - PH * k) / 2, PW * k, PH * k);
            InterpolationMode im = g.InterpolationMode; PixelOffsetMode pm = g.PixelOffsetMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(cells.ToBitmap(), dst, new RectangleF(PX0, PY0, PW, PH), GraphicsUnit.Pixel);
            g.InterpolationMode = im; g.PixelOffsetMode = pm;
        }

        static GraphicsPath Rounded(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            rad = Math.Max(0.5f, Math.Min(rad, Math.Min(r.Width, r.Height) / 2));
            p.AddArc(r.Left, r.Top, rad * 2, rad * 2, 180, 90); p.AddArc(r.Right - rad * 2, r.Top, rad * 2, rad * 2, 270, 90);
            p.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90); p.AddArc(r.Left, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
            p.CloseFigure(); return p;
        }
    }
}
