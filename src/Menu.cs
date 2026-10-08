// The right-click menu: a hologram that the pet projects above his head. A beam of light comes out of his
// head and the panel flickers on: name, mood bars, two button grids (Play, Tricks), then rows for the
// Pocket, Change pet, Settings and Bye. Pocket and Change pet are pages inside the same window, so there
// is only ever one menu window and nothing can get left behind.
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
        public string Text, Hint, Target, Group; public Action Act; public bool Sep, Danger;
        public bool Sub { get { return Target != null; } }
        public MenuItem(string t, Action a) { Text = t; Act = a; }
        public static MenuItem Separator() { MenuItem m = new MenuItem("", null); m.Sep = true; return m; }
        public static MenuItem Page(string t, string hint, string target) { MenuItem m = new MenuItem(t, null); m.Hint = hint; m.Target = target; return m; }
    }

    class PetMenu
    {
        readonly LayeredWindow win = new LayeredWindow(false);
        List<MenuItem> items = new List<MenuItem>();
        double[] needs;
        // hit areas in window coords. main: one per item (a grid button or a row). pets: 0 = back, 1.. = pets.
        // pocket: 0 = back, 1..n = files, n+1 = "empty the pocket"; the little x buttons are XBase + i.
        readonly List<RectangleF> rows = new List<RectangleF>();
        readonly List<RectangleF> xs = new List<RectangleF>();
        const int XBase = 100;
        RectangleF panel;
        Point anchor; Native.RECT work; float sc = 1;
        int hover = -1, pressed = -1, frame, openT;
        Point downAt; bool dragging;
        int[] blinkAt;
        readonly Random rng = new Random();
        readonly Cells cells = new Cells(Sprite.CW, Sprite.CH);
        readonly Dictionary<string, Bitmap> icons = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        public bool Open;
        public string Page = "main";
        public int ClosedAtMs = -100000;
        public bool Dragging { get { return dragging; } }
        public Action<Species> PetChosen;
        public Pocket Pocket;
        public Action<int> PocketCopy;           // clicked a file
        public Action<string> PocketDropped;     // dragged a file out and dropped it somewhere
        public Action<int> PocketRemoved;

        // hologram colours
        static readonly Color Holo = Color.FromArgb(110, 228, 255), TextC = Color.FromArgb(230, 250, 255), Dim = Color.FromArgb(132, 196, 222),
                              DangerC = Color.FromArgb(255, 122, 122), BgA = Color.FromArgb(246, 10, 28, 46), BgB = Color.FromArgb(250, 6, 16, 30);
        static readonly string[] needNames = { "food", "energy", "fun", "love" };

        public PetMenu()
        {
            win.MouseMove += (s, e) =>
            {
                int h = Hit(e.X, e.Y);
                if (h != hover) { hover = h; Draw(); }
                // drag a file out of the pocket
                if (Page == "pocket" && !dragging && pressed >= 1 && pressed <= PocketCount() && (e.Button & MouseButtons.Left) != 0
                    && Math.Abs(e.X - downAt.X) + Math.Abs(e.Y - downAt.Y) > 6)
                    DragOut(pressed - 1);
            };
            win.MouseLeave += (s, e) => { if (!dragging && (hover != -1 || pressed != -1)) { hover = -1; pressed = -1; Draw(); } };
            win.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { pressed = Hit(e.X, e.Y); downAt = e.Location; Draw(); } };
            win.MouseUp += (s, e) =>
            {
                int h = Hit(e.X, e.Y), p = pressed; pressed = -1;
                if (e.Button != MouseButtons.Left || h < 0 || h != p) { if (Open) Draw(); return; }
                Activate(h);
            };
            win.Cursor = Cursors.Hand;
        }

        public IntPtr Handle { get { return win.Handle; } }
        public Rectangle Bounds { get { return new Rectangle(win.SX, win.SY, win.Surf.W, win.Surf.H); } }
        // is this screen point on the panel itself (not on the glow or the beam)?
        public bool PanelContains(int x, int y) { return Open && panel.Contains(x - win.SX, y - win.SY); }
        int PocketCount() { return Pocket != null ? Pocket.Files.Count : 0; }

        // at = the top of his head; the hologram goes above it
        public void Show(List<MenuItem> it, double[] nd, Point at, Native.RECT wk, float scale)
        {
            items = it; needs = nd; anchor = at; work = wk; sc = scale;
            hover = -1; pressed = -1; Page = "main"; Open = true; openT = 0;
            Draw();
            win.KeepOnTop();
            if (Program.Verbose) Program.Log("menu opened" + RowsForLog());
        }
        public void ShowPage(string page)
        {
            if (!Open) return;
            Page = page; hover = -1; pressed = -1; frame = 0; openT = Math.Min(openT, 6);
            blinkAt = new int[SpeciesList.All.Count];
            for (int i = 0; i < blinkAt.Length; i++) blinkAt[i] = 40 + rng.Next(120);
            foreach (Bitmap b in icons.Values) if (b != null) b.Dispose();
            icons.Clear();
            Draw();
            if (Program.Verbose) Program.Log("menu: " + page + " page" + RowsForLog());
        }
        public void Close()
        {
            if (!Open) return;
            Open = false; Page = "main"; hover = -1; pressed = -1; ClosedAtMs = Environment.TickCount;
            win.Hide();
        }

        void Activate(int h)
        {
            if (Page == "main")
            {
                MenuItem m = items[h];
                if (m.Sub) { ShowPage(m.Target); return; }
                Close();
                if (m.Act != null) m.Act();
            }
            else if (h == 0) { Page = "main"; hover = -1; openT = 6; Draw(); }      // back
            else if (Page == "pets")
            {
                Species sp = SpeciesList.All[h - 1];
                Close();
                if (PetChosen != null) PetChosen(sp);
            }
            else if (Page == "pocket")
            {
                int n = PocketCount();
                if (h >= XBase) { if (PocketRemoved != null) PocketRemoved(h - XBase); hover = -1; Draw(); }
                else if (h >= 1 && h <= n) { Close(); if (PocketCopy != null) PocketCopy(h - 1); }
                else if (h == n + 1) { Pocket.Clear(); hover = -1; Draw(); }
            }
        }

        void DragOut(int i)
        {
            string path = Pocket.Files[i];
            if (!Pocket.Exists(path)) return;
            dragging = true;
            DragDropEffects r = DragDropEffects.None;
            try
            {
                DataObject data = new DataObject(DataFormats.FileDrop, new[] { path });
                r = win.DoDragDrop(data, DragDropEffects.Copy | DragDropEffects.Link);
            }
            catch { }
            dragging = false; pressed = -1;
            if (Program.Verbose) Program.Log("pocket drag out: " + r);
            if (r != DragDropEffects.None) { Close(); if (PocketDropped != null) PocketDropped(path); }
            else if (Open) Draw();
        }

        // every tick while open: the flicker-on, and the pet previews
        public void Tick()
        {
            if (!Open) return;
            frame++;
            bool redraw = false;
            if (openT < 14) { openT++; redraw = true; }
            if (Page == "pets")
            {
                if (frame % 6 == 0 && hover >= 0) redraw = true;
                for (int i = 0; i < blinkAt.Length; i++)
                {
                    if (frame == blinkAt[i]) redraw = true;
                    if (frame == blinkAt[i] + 8) { redraw = true; blinkAt[i] = frame + 120 + rng.Next(200); }
                }
            }
            if (redraw) Draw();
        }

        int Hit(int x, int y)
        {
            if (!panel.Contains(x, y)) return -1;
            for (int i = 0; i < xs.Count; i++) if (xs[i].Contains(x, y)) return XBase + i;
            for (int i = 0; i < rows.Count; i++)
            {
                if (Page == "main" && items[i].Sep) continue;
                if (rows[i].Contains(x, y)) return i;
            }
            return -1;
        }

        // ---------- drawing ----------
        void Draw()
        {
            float s = sc, glow = 16 * s, pad = 12 * s, rowH = 30 * s, btnH = 31 * s, gap = 6 * s, headH = 18 * s, sepH = 13 * s, backH = 34 * s;
            using (Font fItem = new Font("Segoe UI Semibold", 12.5f * s, GraphicsUnit.Pixel))
            using (Font fName = new Font("Segoe UI Semibold", 15f * s, GraphicsUnit.Pixel))
            using (Font fSmall = new Font("Segoe UI", 11f * s, GraphicsUnit.Pixel))
            using (Font fHead = new Font("Segoe UI", 9.5f * s, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                Species cur = SpeciesList.Current;
                int n = PocketCount();
                float PW = 300 * s, PH;
                // ---- measure ----
                if (Page == "main")
                {
                    PH = 44 * s + (needs != null ? 40 * s : 0) + sepH;
                    string g0 = null; int inGroup = 0;
                    foreach (MenuItem m in items)
                    {
                        if (m.Group != null)
                        {
                            if (m.Group != g0) { if (inGroup > 0) PH += ((inGroup + 1) / 2) * (btnH + gap); PH += headH; g0 = m.Group; inGroup = 0; }
                            inGroup++;
                        }
                    }
                    if (inGroup > 0) PH += ((inGroup + 1) / 2) * (btnH + gap);
                    PH += sepH - gap;
                    foreach (MenuItem m in items) if (m.Group == null) PH += m.Sep ? sepH : rowH;
                }
                else if (Page == "pets") PH = backH + sepH + SpeciesList.All.Count * 54 * s;
                else PH = backH + sepH + n * 50 * s + (n > 0 ? sepH + rowH : 0) + 46 * s;
                PH += 2 * pad;

                // ---- place: above his head, with room for the beam ----
                float beam = 34 * s;
                float pl = Math.Max(work.Left + 6, Math.Min(work.Right - 6 - PW, anchor.X - PW / 2));
                float pb = anchor.Y - beam, pt = pb - PH;
                if (pt < work.Top + 6) { pt = work.Top + 6; pb = pt + PH; }
                bool showBeam = pb < anchor.Y - 8 * s;
                float wl = Math.Min(pl - glow, anchor.X - 12 * s), wr = Math.Max(pl + PW + glow, anchor.X + 12 * s);
                float wt = pt - glow, wb = Math.Max(pb + glow, showBeam ? anchor.Y + 4 * s : 0);
                int W = (int)Math.Ceiling(wr - wl), H = (int)Math.Ceiling(wb - wt);

                win.Surf.Ensure(W, H); win.Surf.Clear();
                Graphics g = win.Surf.G; g.SmoothingMode = SmoothingMode.AntiAlias;
                panel = new RectangleF(pl - wl, pt - wt, PW, PH);
                float ax = anchor.X - wl, ay = anchor.Y - wt, rad = 12 * s;

                // flicker-on: the beam first, then the panel unrolls upwards from it
                float e = Math.Min(1f, Math.Max(0f, (openT - 2) / 9f)); e = 1 - (1 - e) * (1 - e);
                bool flick = openT == 4 || openT == 7;
                if (showBeam)
                {
                    float spread = Math.Min(PW * 0.36f, 110 * s), bx0 = Math.Max(panel.Left + 16 * s, ax - spread), bx1 = Math.Min(panel.Right - 16 * s, ax + spread);
                    if (bx1 - bx0 < 20 * s) { bx0 = ax - 10 * s; bx1 = ax + 10 * s; }
                    PointF[] cone = { new PointF(ax - 2.5f * s, ay), new PointF(ax + 2.5f * s, ay), new PointF(bx1, panel.Bottom), new PointF(bx0, panel.Bottom) };
                    using (LinearGradientBrush lb = new LinearGradientBrush(new PointF(0, ay + 1), new PointF(0, panel.Bottom - 1), Color.FromArgb(flick ? 40 : 110, Holo), Color.FromArgb(18, Holo)))
                        g.FillPolygon(lb, cone);
                    using (Pen bp = new Pen(Color.FromArgb(60, Holo), 1f * s))
                        for (int i = 1; i < 4; i++) { float t = i / 4f; g.DrawLine(bp, ax, ay, bx0 + (bx1 - bx0) * t, panel.Bottom); }
                    // the emitter on his head
                    for (int i = 4; i >= 1; i--)
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(i == 1 ? 230 : 40, i == 1 ? Color.White : Holo))) { float r = i * 1.8f * s; g.FillEllipse(b, ax - r, ay - r, 2 * r, 2 * r); }
                }
                // (everything is laid out even while hidden, so the rows are always clickable and known)
                RectangleF vis = new RectangleF(panel.X - glow, panel.Bottom - (panel.Height + glow) * e, panel.Width + 2 * glow, e > 0 ? (panel.Height + glow) * e + glow : 0);
                g.SetClip(vis);

                // glow, glass, scanlines, border, corner brackets
                using (GraphicsPath p = Rounded(panel, rad))
                {
                    for (int i = 7; i >= 1; i--)
                        using (Pen gp = new Pen(Color.FromArgb(flick ? 4 : 9, Holo), i * 2.2f * s)) g.DrawPath(gp, p);
                    using (LinearGradientBrush lb = new LinearGradientBrush(panel, BgA, BgB, LinearGradientMode.Vertical)) g.FillPath(lb, p);
                    Region keep = g.Clip; g.SetClip(p, CombineMode.Intersect);
                    using (Pen sp = new Pen(Color.FromArgb(13, Holo), 1f))
                        for (float yy = panel.Top + 2; yy < panel.Bottom; yy += 3 * s) g.DrawLine(sp, panel.Left, yy, panel.Right, yy);
                    if (e < 1) using (Pen edge = new Pen(Color.FromArgb(200, Holo), 2f * s)) g.DrawLine(edge, panel.Left, vis.Top + glow, panel.Right, vis.Top + glow);
                    g.Clip = keep;
                    using (Pen bp = new Pen(Color.FromArgb(flick ? 90 : 190, Holo), 1.3f * s)) g.DrawPath(bp, p);
                }
                using (Pen cp = new Pen(Color.FromArgb(240, Holo), 2.2f * s))
                {
                    float c = 14 * s, o = 3 * s; RectangleF r = RectangleF.Inflate(panel, o, o);
                    g.DrawLines(cp, new[] { new PointF(r.Left, r.Top + c), new PointF(r.Left, r.Top), new PointF(r.Left + c, r.Top) });
                    g.DrawLines(cp, new[] { new PointF(r.Right - c, r.Top), new PointF(r.Right, r.Top), new PointF(r.Right, r.Top + c) });
                    g.DrawLines(cp, new[] { new PointF(r.Left, r.Bottom - c), new PointF(r.Left, r.Bottom), new PointF(r.Left + c, r.Bottom) });
                    g.DrawLines(cp, new[] { new PointF(r.Right - c, r.Bottom), new PointF(r.Right, r.Bottom), new PointF(r.Right, r.Bottom - c) });
                }
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                float x0 = panel.X, y = panel.Y + pad, iw = PW - 2 * pad, ix = x0 + pad;
                rows.Clear(); xs.Clear();
                if (Page == "main")
                {
                    // header: name and tagline, mood bars
                    using (SolidBrush b = new SolidBrush(TextC)) g.DrawString(cur.Name, fName, b, ix, y - 2 * s);
                    using (SolidBrush b = new SolidBrush(Dim)) g.DrawString(cur.Tagline, fSmall, b, ix + 1 * s, y + 20 * s);
                    y += 44 * s;
                    if (needs != null)
                    {
                        int[] cols = { Pal.C(255, 176, 80), Pal.C(110, 190, 255), Pal.C(190, 150, 255), Pal.C(255, 110, 150) };
                        float colW = (iw - 12 * s) / 2;
                        for (int i = 0; i < 4; i++)
                        {
                            float cx = ix + (i % 2) * (colW + 12 * s), cy = y + (i / 2) * 18 * s;
                            using (SolidBrush b = new SolidBrush(Dim)) g.DrawString(needNames[i], fSmall, b, cx - 2 * s, cy - 1 * s);
                            RectangleF bar = new RectangleF(cx + 48 * s, cy + 5 * s, colW - 48 * s, 5 * s);
                            using (GraphicsPath p = Rounded(bar, 2.5f * s)) using (SolidBrush b = new SolidBrush(Color.FromArgb(40, Holo))) g.FillPath(b, p);
                            float f = (float)Math.Max(0, Math.Min(1, needs[i] / 100));
                            RectangleF fill = new RectangleF(bar.X, bar.Y, Math.Max(5 * s, bar.Width * f), bar.Height);
                            using (GraphicsPath p = Rounded(fill, 2.5f * s)) using (SolidBrush b = new SolidBrush(Color.FromArgb(needs[i] < 25 ? Pal.Red : cols[i]))) g.FillPath(b, p);
                        }
                        y += 40 * s;
                    }
                    Divider(g, x0, PW, y + sepH / 2, s); y += sepH;

                    // button grids, one per group
                    string group = null; int col = 0;
                    for (int i = 0; i < items.Count; i++)
                    {
                        MenuItem m = items[i];
                        if (m.Group == null) { rows.Add(RectangleF.Empty); continue; }
                        if (m.Group != group)
                        {
                            if (group != null && col == 1) y += btnH + gap;
                            group = m.Group; col = 0;
                            using (SolidBrush b = new SolidBrush(Dim)) g.DrawString(group.ToUpperInvariant(), fHead, b, ix + 1 * s, y);
                            y += headH;
                        }
                        float bw = (iw - gap) / 2;
                        RectangleF r = new RectangleF(ix + col * (bw + gap), y, bw, btnH);
                        rows.Add(r);
                        Button(g, r, m.Text, fItem, i == hover, i == pressed, false, s);
                        col++; if (col == 2) { col = 0; y += btnH + gap; }
                    }
                    if (col == 1) y += btnH + gap;
                    y += sepH - gap;
                    Divider(g, x0, PW, y - sepH / 2, s);

                    // the rows
                    for (int i = 0; i < items.Count; i++)
                    {
                        MenuItem m = items[i];
                        if (m.Group != null) continue;
                        float h = m.Sep ? sepH : rowH;
                        RectangleF r = new RectangleF(x0 + 6 * s, y, PW - 12 * s, h);
                        rows[i] = r;
                        if (m.Sep) { Divider(g, x0, PW, y + h / 2, s); y += h; continue; }
                        Color hi = m.Danger ? DangerC : Holo;
                        bool hot = i == hover;
                        if (hot || i == pressed) Highlight(g, r, hi, i == pressed, s);
                        using (SolidBrush b = new SolidBrush(m.Danger ? (hot ? DangerC : Color.FromArgb(255, 170, 170)) : TextC)) g.DrawString(m.Text, fItem, b, r.X + 8 * s, r.Y + (h - 17 * s) / 2);
                        if (m.Sub)
                        {
                            string hint = (m.Hint ?? "") + "  >";
                            SizeF hs = g.MeasureString(hint, fSmall);
                            using (SolidBrush b = new SolidBrush(Dim)) g.DrawString(hint, fSmall, b, r.Right - hs.Width - 6 * s, r.Y + (h - 15 * s) / 2);
                        }
                        y += h;
                    }
                }
                else
                {
                    // back row with the page title
                    RectangleF back = new RectangleF(x0 + 6 * s, y, PW - 12 * s, backH);
                    rows.Add(back);
                    if (hover == 0 || pressed == 0) Highlight(g, back, Holo, pressed == 0, s);
                    using (SolidBrush b = new SolidBrush(Dim)) g.DrawString("<", fName, b, back.X + 8 * s, back.Y + 6 * s);
                    using (SolidBrush b = new SolidBrush(TextC)) g.DrawString(Page == "pets" ? "Change pet" : "Pocket", fName, b, back.X + 24 * s, back.Y + 6 * s);
                    if (Page == "pocket")
                        using (SolidBrush b = new SolidBrush(Dim)) { string c = n + " / " + Pocket.Max; SizeF cs = g.MeasureString(c, fSmall); g.DrawString(c, fSmall, b, back.Right - cs.Width - 8 * s, back.Y + 10 * s); }
                    y += backH;
                    Divider(g, x0, PW, y + sepH / 2, s); y += sepH;
                    if (Page == "pets") DrawPets(g, x0, ref y, PW, s, fItem, fSmall, cur);
                    else DrawPocket(g, x0, ref y, PW, rowH, sepH, s, fItem, fSmall, cur, n);
                }
                g.ResetClip();
                win.Present((int)wl, (int)wt);
            }
        }

        void Button(Graphics g, RectangleF r, string text, Font f, bool hot, bool down, bool danger, float s)
        {
            Color c = danger ? DangerC : Holo;
            using (GraphicsPath p = Rounded(r, 7 * s))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(down ? 110 : hot ? 62 : 20, c))) g.FillPath(b, p);
                using (Pen pn = new Pen(Color.FromArgb(hot ? 235 : 95, c), (hot ? 1.5f : 1f) * s)) g.DrawPath(pn, p);
            }
            using (StringFormat sf = new StringFormat()) using (SolidBrush b = new SolidBrush(hot ? Color.White : TextC))
            {
                sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; sf.Trimming = StringTrimming.EllipsisCharacter; sf.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(text, f, b, r, sf);
            }
        }

        void DrawPets(Graphics g, float x0, ref float y, float PW, float s, Font fItem, Font fSmall, Species cur)
        {
            for (int i = 0; i < SpeciesList.All.Count; i++)
            {
                Species sp = SpeciesList.All[i];
                int idx = i + 1;
                bool hot = idx == hover, isCur = sp == cur;
                RectangleF r = new RectangleF(x0 + 6 * s, y, PW - 12 * s, 52 * s);
                rows.Add(r);
                if (hot || idx == pressed) Highlight(g, r, Holo, idx == pressed, s);
                Look L = new Look();
                L.Blink = blinkAt != null && frame >= blinkAt[i] && frame < blinkAt[i] + 8;
                if (hot) { L.EyeStyle = "happy"; L.Mouth = "smile"; L.Arms = (frame / 12) % 2 == 0 ? "wave1" : "wave2"; }
                DrawThumb(g, sp, L, new RectangleF(r.X + 6 * s, r.Y + 5 * s, 42 * s, 42 * s));
                using (SolidBrush b = new SolidBrush(TextC)) g.DrawString(sp.Name, fItem, b, r.X + 58 * s, r.Y + 8 * s);
                using (SolidBrush b = new SolidBrush(Dim)) g.DrawString(sp.Tagline, fSmall, b, r.X + 58 * s, r.Y + 27 * s);
                if (isCur)
                {
                    float cr = 8 * s, cx = r.Right - cr - 10 * s, cy = r.Y + r.Height / 2;
                    using (SolidBrush b = new SolidBrush(Holo)) g.FillEllipse(b, cx - cr, cy - cr, 2 * cr, 2 * cr);
                    using (Pen ck = new Pen(Color.FromArgb(8, 24, 40), 2f * s)) g.DrawLines(ck, new[] { new PointF(cx - 4 * s, cy), new PointF(cx - 1 * s, cy + 3 * s), new PointF(cx + 4 * s, cy - 3 * s) });
                }
                y += 54 * s;
            }
        }

        void DrawPocket(Graphics g, float x0, ref float y, float PW, float rowH, float sepH, float s, Font fItem, Font fSmall, Species cur, int n)
        {
            using (StringFormat end = new StringFormat(StringFormatFlags.NoWrap)) using (StringFormat mid = new StringFormat(StringFormatFlags.NoWrap))
            {
                end.Trimming = StringTrimming.EllipsisCharacter; mid.Trimming = StringTrimming.EllipsisPath;
                for (int i = 0; i < n; i++)
                {
                    string path = Pocket.Files[i];
                    bool there = Pocket.Exists(path);
                    int idx = i + 1;
                    RectangleF r = new RectangleF(x0 + 6 * s, y, PW - 12 * s, 48 * s);
                    rows.Add(r);
                    if (idx == hover || idx == pressed || hover == XBase + i) Highlight(g, r, Holo, idx == pressed, s);
                    DrawFileIcon(g, path, new RectangleF(r.X + 10 * s, r.Y + 10 * s, 28 * s, 28 * s));
                    float tx = r.X + 48 * s, tw = r.Width - 48 * s - 34 * s;
                    using (SolidBrush b = new SolidBrush(there ? TextC : Dim)) g.DrawString(Pocket.Name(path), fItem, b, new RectangleF(tx, r.Y + 6 * s, tw, 18 * s), end);
                    using (SolidBrush b = new SolidBrush(Dim)) g.DrawString(there ? Pocket.Where(path) : "not there anymore", fSmall, b, new RectangleF(tx, r.Y + 26 * s, tw, 16 * s), mid);
                    // the little x
                    float xr = 9 * s, xc = r.Right - xr - 10 * s, yc = r.Y + r.Height / 2;
                    xs.Add(new RectangleF(xc - xr - 3 * s, yc - xr - 3 * s, 2 * xr + 6 * s, 2 * xr + 6 * s));
                    bool xh = hover == XBase + i;
                    if (xh) using (SolidBrush b = new SolidBrush(Color.FromArgb(70, DangerC))) g.FillEllipse(b, xc - xr, yc - xr, 2 * xr, 2 * xr);
                    using (Pen p = new Pen(xh ? DangerC : Dim, 1.6f * s)) { float a = 3.5f * s; g.DrawLine(p, xc - a, yc - a, xc + a, yc + a); g.DrawLine(p, xc - a, yc + a, xc + a, yc - a); }
                    y += 50 * s;
                }
                if (n > 0)
                {
                    Divider(g, x0, PW, y + sepH / 2, s); y += sepH;
                    RectangleF r = new RectangleF(x0 + 6 * s, y, PW - 12 * s, rowH);
                    rows.Add(r);
                    bool hot = hover == n + 1;
                    if (hot || pressed == n + 1) Highlight(g, r, DangerC, pressed == n + 1, s);
                    using (SolidBrush b = new SolidBrush(hot ? DangerC : Color.FromArgb(255, 170, 170))) g.DrawString("Empty the pocket", fItem, b, r.X + 8 * s, r.Y + (rowH - 17 * s) / 2);
                    y += rowH;
                }
                string tip = n == 0 ? "Drop files on " + cur.ShortName + " to keep them here (up to " + Pocket.Max + ")."
                                    : "Click to copy (then paste anywhere), or drag a file out.";
                using (SolidBrush b = new SolidBrush(Dim)) g.DrawString(tip, fSmall, b, new RectangleF(x0 + 16 * s, y + 8 * s, PW - 32 * s, 34 * s));
            }
        }

        void DrawFileIcon(Graphics g, string path, RectangleF box)
        {
            Bitmap bmp;
            if (!icons.TryGetValue(path, out bmp))
            {
                bmp = null;
                try { if (System.IO.File.Exists(path)) using (Icon ic = Icon.ExtractAssociatedIcon(path)) bmp = ic.ToBitmap(); } catch { }
                icons[path] = bmp;
            }
            if (bmp != null) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(bmp, box); return; }
            // a folder (or a file that's gone): a simple drawn folder
            bool folder = System.IO.Directory.Exists(path);
            Color c = folder ? Color.FromArgb(236, 186, 76) : Color.FromArgb(120, 140, 160);
            using (SolidBrush b = new SolidBrush(c))
            {
                g.FillRectangle(b, box.X + 1, box.Y + box.Height * 0.18f, box.Width * 0.45f, box.Height * 0.2f);
                using (GraphicsPath p = Rounded(new RectangleF(box.X + 1, box.Y + box.Height * 0.28f, box.Width - 2, box.Height * 0.62f), 3 * sc)) g.FillPath(b, p);
            }
        }

        static void Highlight(Graphics g, RectangleF r, Color c, bool down, float s)
        {
            using (GraphicsPath p = Rounded(r, 8 * s))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(down ? 90 : 42, c))) g.FillPath(b, p);
                using (Pen pn = new Pen(Color.FromArgb(150, c), 1f * s)) g.DrawPath(pn, p);
            }
        }

        // for tests: the screen centre of every clickable row
        string RowsForLog()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(" rows:");
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Width > 0 && (Page != "main" || !items[i].Sep))
                    sb.Append(" " + i + "@" + (int)(win.SX + rows[i].X + rows[i].Width / 2) + "," + (int)(win.SY + rows[i].Y + rows[i].Height / 2));
            for (int i = 0; i < xs.Count; i++) sb.Append(" x" + i + "@" + (int)(win.SX + xs[i].X + xs[i].Width / 2) + "," + (int)(win.SY + xs[i].Y + xs[i].Height / 2));
            return sb.ToString();
        }

        static void Divider(Graphics g, float x0, float w, float y, float s)
        {
            using (LinearGradientBrush lb = new LinearGradientBrush(new PointF(x0, y), new PointF(x0 + w, y), Color.FromArgb(0, Holo), Color.FromArgb(0, Holo)))
            {
                ColorBlend cb = new ColorBlend();
                cb.Colors = new[] { Color.FromArgb(0, Holo), Color.FromArgb(110, Holo), Color.FromArgb(0, Holo) };
                cb.Positions = new[] { 0f, 0.5f, 1f };
                lb.InterpolationColors = cb;
                using (Pen p = new Pen(lb, Math.Max(1f, s))) g.DrawLine(p, x0 + 10 * s, y, x0 + w - 10 * s, y);
            }
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
