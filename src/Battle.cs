// The anime battle: his shadow clone appears, they power up (auras, rising sparks, the ground shakes),
// dash in and clash (flash + shockwave), charge energy balls, and fire beams that meet in the middle.
// The beam struggle wobbles back and forth until his beam wins and the shadow blows up.
// Everything except the pet himself is drawn in one click-through window that sits behind him.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Flippy
{
    class BattlePose
    {
        public string Arms = "out", Eye = "angry", Mouth = "none", Say;
        public bool Sit, Done; public double DX; public int Shake;
    }

    class Battle
    {
        class Spark { public double X, Y, VX, VY; public int Life, Max; public Color C; public float Size; }

        readonly LayeredWindow win = new LayeredWindow(true);
        readonly Cells cells = new Cells(Sprite.CW, Sprite.CH);
        readonly List<Spark> sparks = new List<Spark>();
        readonly Random rng = new Random();
        public bool Active;
        int T, dir;                 // dir: which way the rival is from him
        double homeX, rivalX, gy, dist;
        double petDX, rivalDX;
        static readonly Color Gold = Color.FromArgb(255, 220, 90), Cyan = Color.FromArgb(120, 230, 255), Violet = Color.FromArgb(190, 90, 255), White = Color.White;

        public void Start(double x, double ground, int towards, double distance)
        {
            Active = true; T = 0; dir = towards; homeX = x; gy = ground; dist = distance; rivalX = x + dir * dist;
            petDX = rivalDX = 0; sparks.Clear();
        }
        public void Stop() { Active = false; win.Hide(); sparks.Clear(); }

        static double Ease(double s) { s = Math.Max(0, Math.Min(1, s)); return s < 0.5 ? 2 * s * s : 1 - 2 * (1 - s) * (1 - s); }

        // one tick: returns how the pet should look, and draws the rest
        public BattlePose Step(double petX, int S, double K, string name)
        {
            BattlePose p = new BattlePose();
            T++;
            int t = T;
            double mid = homeX + dir * dist / 2;
            // ---- the script ----
            if (t < 40) { p.Eye = "wide"; p.Arms = "out"; if (t == 6) p.Say = "you again?!"; }
            else if (t < 120) { p.Sit = true; p.Arms = "down"; p.Eye = "angry"; p.Mouth = "yawn"; if (t == 50) p.Say = "HAAAAAAA!"; p.Shake = t > 80 ? 2 : 0; }
            else if (t < 150)
            {
                double s = Ease((t - 120) / 26.0);
                petDX = dir * (dist / 2 - 14 * S) * s; rivalDX = -dir * (dist / 2 - 14 * S) * s;
                p.Arms = dir > 0 ? "upR" : "upL"; p.Eye = "angry";
            }
            else if (t < 176)
            {
                if (t == 150) { Burst(mid, gy - 9 * S, 40, White, 3.2 * K, S); Burst(mid, gy - 9 * S, 25, Gold, 2.2 * K, S); }
                double s = Ease((t - 150) / 26.0);
                petDX = dir * (dist / 2 - 14 * S) * (1 - s); rivalDX = -dir * (dist / 2 - 14 * S) * (1 - s);
                p.Shake = t < 162 ? 6 : 2; p.Arms = "out"; p.Eye = "wide"; if (t == 151) p.Say = "!!";
            }
            else if (t < 216) { petDX = rivalDX = 0; p.Arms = "hold"; p.Eye = "angry"; p.Sit = true; if (t == 180) p.Say = name.ToUpperInvariant() + "... BEEEAM!"; p.Shake = 1; }
            else if (t < 330) { p.Arms = dir > 0 ? "upR" : "upL"; p.Eye = "angry"; p.Mouth = "yawn"; p.Shake = 3; }
            else if (t < 350) { p.Arms = dir > 0 ? "upR" : "upL"; p.Eye = "wide"; p.Shake = 8; if (t == 330) { double rx = rivalX; Burst(rx, gy - 9 * S, 70, Violet, 3.5 * K, S); Burst(rx, gy - 9 * S, 50, White, 2.5 * K, S); Burst(rx, gy - 9 * S, 40, Gold, 4.5 * K, S); } }
            else if (t < 440) { p.Arms = (t / 12) % 2 == 0 ? "up" : "wave1"; p.Eye = "happy"; p.Mouth = "smile"; if (t == 360) p.Say = "too easy."; }
            else { p.Done = true; }
            p.DX = petDX;

            // sparks
            if (t >= 40 && t < 120)
            {
                for (int i = 0; i < 2; i++)
                {
                    AddSpark(petX + (rng.NextDouble() - 0.5) * 22 * S, gy - rng.NextDouble() * 6 * S, (rng.NextDouble() - 0.5) * 0.4 * K, -(1.5 + rng.NextDouble() * 2) * K, 30, Gold, 1.2f * S / 3);
                    AddSpark(rivalX + rivalDX + (rng.NextDouble() - 0.5) * 22 * S, gy - rng.NextDouble() * 6 * S, (rng.NextDouble() - 0.5) * 0.4 * K, -(1.5 + rng.NextDouble() * 2) * K, 30, Violet, 1.2f * S / 3);
                }
                if (t % 9 == 0) AddSpark(petX + (rng.NextDouble() - 0.5) * 60 * S, gy, 0, -0.4 * K, 26, Color.FromArgb(200, 180, 150), 2f * S / 3);   // the ground crumbles
            }
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                Spark s = sparks[i]; s.X += s.VX; s.Y += s.VY; s.VY += 0.02 * K; s.VX *= 0.97; s.VY *= 0.97;
                if (--s.Life <= 0) sparks.RemoveAt(i);
            }
            Draw(petX, S, K, t);
            return p;
        }

        void AddSpark(double x, double y, double vx, double vy, int life, Color c, float size)
        {
            if (sparks.Count > 400) return;
            Spark s = new Spark(); s.X = x; s.Y = y; s.VX = vx; s.VY = vy; s.Life = s.Max = life; s.C = c; s.Size = size; sparks.Add(s);
        }
        void Burst(double x, double y, int n, Color c, double speed, int S)
        {
            for (int i = 0; i < n; i++)
            {
                double a = rng.NextDouble() * Math.PI * 2, v = speed * (0.3 + rng.NextDouble());
                AddSpark(x, y, Math.Cos(a) * v, Math.Sin(a) * v, 30 + rng.Next(30), c, (float)(S * (0.5 + rng.NextDouble() * 0.6)));
            }
        }

        void Draw(double petX, int S, double K, int t)
        {
            double rx = rivalX + rivalDX;
            double left = Math.Min(homeX, rivalX) - 80 * S, right = Math.Max(homeX, rivalX) + 80 * S;
            double top = gy - 60 * S, bottom = gy + 10 * S;
            int W = (int)(right - left), H = (int)(bottom - top);
            win.Surf.Ensure(W, H); win.Surf.Clear();
            Graphics g = win.Surf.G; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform((float)-left, (float)-top);
            float cy = (float)(gy - 8 * S);

            // auras while powering up and during the beams
            if ((t >= 40 && t < 176) || (t >= 176 && t < 330))
            {
                double pw = t < 120 ? Math.Min(1, (t - 40) / 50.0) : 0.8;
                Aura(g, (float)petX, cy, S, Gold, pw, t);
                if (t < 330) Aura(g, (float)rx, cy, S, Violet, pw, t + 7);
            }
            // the shadow clone
            if (t < 335)
            {
                double alpha = t < 40 ? t / 40.0 : (t >= 330 ? (t % 2 == 0 ? 1 : 0.2) : 1);
                DrawRival(g, rx, S, alpha, t >= 330);
            }
            // afterimages during the dash
            if (t >= 120 && t < 150)
                for (int i = 1; i <= 3; i++) DrawRival(g, rx + dir * i * 9 * S, S, 0.25 / i, false);
            // the clash: flash and a shockwave ring
            if (t >= 150 && t < 172)
            {
                double s = (t - 150) / 22.0;
                double mid = homeX + dir * dist / 2;
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(200 * (1 - s) * (1 - s)), 255, 255, 240))) { float r = (float)(8 * S * (1 - s) + 3 * S); g.FillEllipse(b, (float)mid - r, cy - r, 2 * r, 2 * r); }
                using (Pen pn = new Pen(Color.FromArgb((int)(200 * (1 - s)), 255, 230, 160), 3f * S / 3)) { float r = (float)(60 * S * s); g.DrawEllipse(pn, (float)mid - r, cy - r * 0.5f, 2 * r, r); }
            }
            // charging energy balls
            if (t >= 176 && t < 216)
            {
                float r = (float)(1 + (t - 176) / 40.0 * 4) * S;
                Ball(g, (float)(petX + dir * 13 * S), cy, r, Cyan, t);
                Ball(g, (float)(rx - dir * 13 * S), cy, r, Violet, t);
            }
            // the beam struggle
            if (t >= 216 && t < 335)
            {
                float hx = (float)(petX + dir * 13 * S), rhx = (float)(rx - dir * 13 * S);
                double struggle = Math.Sin((t - 216) * 0.11) * 0.18 + Math.Sin((t - 216) * 0.37) * 0.05;
                double push = t < 285 ? 0.5 + struggle : 0.5 + struggle * (1 - (t - 285) / 45.0) + (t - 285) / 45.0 * 0.5;    // he wins
                float clash = (float)(hx + (rhx - hx) * Math.Min(1.02, push));
                if (t < 330) Beam(g, rhx, clash, cy, S, Violet, t + 3);
                Beam(g, hx, clash, cy, S, Cyan, t);
                Ball(g, clash, cy, (float)(5 + Math.Sin(t * 0.9) * 1.5) * S, White, t);
                if (t % 2 == 0) for (int i = 0; i < 3; i++) AddSpark(clash, cy, (rng.NextDouble() - 0.5) * 6 * K, (rng.NextDouble() - 0.5) * 6 * K, 16, rng.Next(2) == 0 ? Cyan : Violet, S * 0.6f);
                // little lightning around the clash point
                using (Pen ln = new Pen(Color.FromArgb(220, 255, 255, 255), 1.2f * S / 3))
                    for (int k = 0; k < 2; k++)
                    {
                        PointF[] pts = new PointF[5]; double a = rng.NextDouble() * Math.PI * 2;
                        for (int i = 0; i < 5; i++) pts[i] = new PointF(clash + (float)(Math.Cos(a) * i * 3 * S + (rng.NextDouble() - 0.5) * 3 * S), cy + (float)(Math.Sin(a) * i * 3 * S + (rng.NextDouble() - 0.5) * 3 * S));
                        g.DrawLines(ln, pts);
                    }
            }
            // the explosion
            if (t >= 330 && t < 380)
            {
                double s = (t - 330) / 50.0;
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(200 * (1 - s)), 255, 240, 200))) { float r = (float)(30 * S * Math.Sqrt(s) + 6 * S); g.FillEllipse(b, (float)rx - r, cy - r, 2 * r, 2 * r); }
                using (Pen pn = new Pen(Color.FromArgb((int)(180 * (1 - s)), 200, 140, 255), 2.5f * S / 3)) { float r = (float)(70 * S * s); g.DrawEllipse(pn, (float)rx - r, cy - r * 0.6f, 2 * r, r * 1.2f); }
            }
            // sparks on top
            foreach (Spark s in sparks)
            {
                double a = (double)s.Life / s.Max;
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * Math.Min(1, a * 1.6)), s.C))) g.FillRectangle(b, (float)s.X - s.Size / 2, (float)s.Y - s.Size / 2, s.Size, s.Size);
            }
            g.ResetTransform();
            win.Present((int)left, (int)top);
        }

        void Aura(Graphics g, float x, float cy, int S, Color c, double power, int t)
        {
            if (power <= 0) return;
            for (int i = 4; i >= 1; i--)
            {
                float w = (float)((13 + i * 3) * S * (0.7 + 0.3 * power) + Math.Sin(t * 0.5 + i) * S), h = (float)((14 + i * 4) * S * (0.7 + 0.3 * power) + Math.Sin(t * 0.7 + i * 2) * 1.5 * S);
                using (GraphicsPath p = new GraphicsPath())
                {
                    // a flame shape: an ellipse with a pointed top
                    p.AddBezier(x - w, cy + 8 * S, x - w * 1.1f, cy - h * 0.4f, x - w * 0.3f, cy - h * 0.8f, x, cy - h - (float)(Math.Sin(t * 0.9 + i) * 2 * S));
                    p.AddBezier(x, cy - h, x + w * 0.3f, cy - h * 0.8f, x + w * 1.1f, cy - h * 0.4f, x + w, cy + 8 * S);
                    p.CloseFigure();
                    using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(28 * power) + i * 4, c))) g.FillPath(b, p);
                }
            }
        }
        void Ball(Graphics g, float x, float y, float r, Color c, int t)
        {
            for (int i = 4; i >= 1; i--)
            {
                float rr = r * (1 + i * 0.45f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(40 + (4 - i) * 30, c))) g.FillEllipse(b, x - rr, y - rr, 2 * rr, 2 * rr);
            }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 255, 255, 255))) g.FillEllipse(b, x - r * 0.7f, y - r * 0.7f, r * 1.4f, r * 1.4f);
        }
        void Beam(Graphics g, float x0, float x1, float y, int S, Color c, int t)
        {
            float a = Math.Min(x0, x1), b = Math.Max(x0, x1);
            for (int i = 3; i >= 0; i--)
            {
                float h = (float)((3 + i * 2.2) * S + Math.Sin(t * 1.3 + i) * 0.8 * S);
                using (SolidBrush br = new SolidBrush(i == 0 ? Color.FromArgb(250, 255, 255, 255) : Color.FromArgb(70 + (3 - i) * 50, c)))
                using (GraphicsPath p = new GraphicsPath()) { p.AddRectangle(new RectangleF(a, y - h / 2, b - a, h)); g.FillPath(br, p); }
            }
        }

        // his shadow: the same pet, mirrored, in dark purple with red eyes
        void DrawRival(Graphics g, double x, int S, double alpha, bool flashWhite)
        {
            Species sp = SpeciesList.Current;
            Look L = new Look(); L.EyeStyle = "angry";
            cells.Clear(); Sprite.DrawFront(cells, L, sp);
            int[] px = cells.Px;
            for (int i = 0; i < px.Length; i++)
            {
                int c = px[i]; if (c == 0) continue;
                int r = (c >> 16) & 255, gg = (c >> 8) & 255, bb = c & 255;
                double lum = (r * 0.3 + gg * 0.59 + bb * 0.11) / 255.0;
                px[i] = flashWhite ? Pal.C(255, 255, 255) : Pal.C((int)(24 + 96 * lum), (int)(10 + 50 * lum), (int)(40 + 130 * lum));
            }
            double ex = Sprite.OX + sp.MaskLX + 1, ex2 = Sprite.OX + sp.MaskRX + 1, ey = Sprite.OY + sp.MaskY + 1;
            if (!flashWhite) { cells.Rect(ex, ey, 2, 1, Pal.C(255, 60, 80)); cells.Rect(ex2, ey, 2, 1, Pal.C(255, 60, 80)); }
            Bitmap bmp = cells.ToBitmap();
            float w = Sprite.CW * S, h = Sprite.CH * S;
            float left = (float)(x - (Sprite.OX + 11) * S), top = (float)(gy - (Sprite.OY + 16) * S);
            System.Drawing.Imaging.ColorMatrix cm = new System.Drawing.Imaging.ColorMatrix(); cm.Matrix33 = (float)alpha;
            using (System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes())
            {
                ia.SetColorMatrix(cm);
                InterpolationMode im = g.InterpolationMode; PixelOffsetMode pm = g.PixelOffsetMode;
                g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
                GraphicsState st = g.Save();
                if (dir > 0) { g.TranslateTransform(left + w, top); g.ScaleTransform(-1, 1); }     // face him
                else g.TranslateTransform(left, top);
                g.DrawImage(bmp, new Rectangle(0, 0, (int)w, (int)h), 0, 0, Sprite.CW, Sprite.CH, GraphicsUnit.Pixel, ia);
                g.Restore(st);
                g.InterpolationMode = im; g.PixelOffsetMode = pm;
            }
        }

        public void KeepOnTop() { win.KeepOnTop(); }
    }
}
