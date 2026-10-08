// Things that fly across the whole screen: pellets and the web line. Each lives in its own small,
// click-through window that's only as big as it needs to be (no full-screen boxes).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Flippy
{
    class Pellet
    {
        public LayeredWindow Win = new LayeredWindow(true);
        public bool Active; public double X, Y, VX, VY, TX, TY; public int Left, Impact;
        public bool HasNext; public double NX, NY;
        public List<PointF> Trail = new List<PointF>();
    }

    class Effects
    {
        readonly List<Pellet> pellets = new List<Pellet>();
        public int Hits;
        readonly LayeredWindow web = new LayeredWindow(true);
        bool webOn;

        public Effects() { for (int i = 0; i < 6; i++) pellets.Add(new Pellet()); }

        void Aim(Pellet p, double tx, double ty, double speed)
        {
            double dx = tx - p.X, dy = ty - p.Y;
            int steps = (int)Math.Max(1, Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) / speed));
            p.TX = tx; p.TY = ty; p.VX = dx / steps; p.VY = dy / steps; p.Left = steps;
        }
        // fire from (sx, sy) at (tx, ty); optional bounce point first (ricochet)
        public void Fire(double sx, double sy, double tx, double ty, bool viaFloor, double vx, double vy, double k)
        {
            foreach (Pellet p in pellets)
            {
                if (p.Active) continue;
                p.X = sx; p.Y = sy; p.Impact = 0; p.Trail.Clear(); p.Active = true;
                if (viaFloor) { Aim(p, vx, vy, 18 * k); p.HasNext = true; p.NX = tx; p.NY = ty; }
                else { Aim(p, tx, ty, 18 * k); p.HasNext = false; }
                return;
            }
        }

        public void Tick(Point cursor, double k, int S)
        {
            foreach (Pellet p in pellets)
            {
                if (!p.Active) continue;
                if (p.Impact > 0)
                {
                    p.Impact--;
                    if (p.Impact == 0) { p.Active = false; p.Win.Hide(); continue; }
                }
                else
                {
                    p.Trail.Add(new PointF((float)p.X, (float)p.Y)); if (p.Trail.Count > 6) p.Trail.RemoveAt(0);
                    p.X += p.VX; p.Y += p.VY; p.Left--;
                    if (p.Left <= 0)
                    {
                        p.X = p.TX; p.Y = p.TY;
                        if (p.HasNext) { p.HasNext = false; Aim(p, p.NX, p.NY, 18 * k); }
                        else
                        {
                            p.Impact = 16;
                            double hx = cursor.X - p.TX, hy = cursor.Y - p.TY;
                            if (Math.Sqrt(hx * hx + hy * hy) < 30 * k) Hits++;
                        }
                    }
                }
                DrawPellet(p, S);
            }
        }

        void DrawPellet(Pellet p, int S)
        {
            int pad = 14 * S;
            float x0 = (float)p.X, y0 = (float)p.Y, x1 = x0, y1 = y0;
            foreach (PointF t in p.Trail) { x0 = Math.Min(x0, t.X); y0 = Math.Min(y0, t.Y); x1 = Math.Max(x1, t.X); y1 = Math.Max(y1, t.Y); }
            int wx = (int)x0 - pad, wy = (int)y0 - pad;
            p.Win.Surf.Ensure((int)(x1 - x0) + 2 * pad, (int)(y1 - y0) + 2 * pad); p.Win.Surf.Clear();
            Graphics g = p.Win.Surf.G; g.TranslateTransform(-wx, -wy); g.SmoothingMode = SmoothingMode.AntiAlias;
            if (p.Impact > 0)
            {
                double t = p.Impact / 16.0, r = (1 - t) * 10 * S + 2 * S;
                using (Pen pn = new Pen(Color.FromArgb((int)(220 * t), 255, 210, 70), 1.5f * S / 3 * 2))
                    for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; g.DrawLine(pn, (float)(p.X + Math.Cos(a) * r * 0.4), (float)(p.Y + Math.Sin(a) * r * 0.4), (float)(p.X + Math.Cos(a) * r), (float)(p.Y + Math.Sin(a) * r)); }
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * t), 255, 250, 220))) g.FillEllipse(b, (float)p.X - S, (float)p.Y - S, 2 * S, 2 * S);
            }
            else
            {
                for (int i = 0; i < p.Trail.Count; i++)
                {
                    float a = (i + 1f) / (p.Trail.Count + 1), r = S * 0.6f * a;
                    using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(120 * a), 255, 200, 80))) g.FillEllipse(b, p.Trail[i].X - r, p.Trail[i].Y - r, 2 * r, 2 * r);
                }
                using (SolidBrush glow = new SolidBrush(Color.FromArgb(90, 255, 210, 90))) g.FillEllipse(glow, (float)p.X - 1.6f * S, (float)p.Y - 1.6f * S, 3.2f * S, 3.2f * S);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 255, 236, 140))) g.FillEllipse(b, (float)p.X - 0.8f * S, (float)p.Y - 0.8f * S, 1.6f * S, 1.6f * S);
            }
            p.Win.Present(wx, wy);
            p.Win.KeepOnTop();
        }

        // ---- web line ----
        // The line isn't a metal rod: it can wobble (a standing wave that travels along it), sag, and while
        // it's being shot it only reaches part of the way (reach 0..1).
        public void Web(double x1, double y1, double x2, double y2, int S) { Web(x1, y1, x2, y2, S, 0, 0, 0, 1); }
        public void Web(double x1, double y1, double x2, double y2, int S, double wobble, double sag, double phase, double reach)
        {
            float k = S / 3f;
            const int N = 28;
            double dx = x2 - x1, dy = y2 - y1, len = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
            double nx = -dy / len, ny = dx / len;
            if (ny < 0) { nx = -nx; ny = -ny; }              // sag points down
            PointF[] pts = new PointF[N + 1];
            double minx = double.MaxValue, miny = double.MaxValue, maxx = double.MinValue, maxy = double.MinValue;
            for (int i = 0; i <= N; i++)
            {
                double u = (double)i / N * reach;
                double env = Math.Sin(Math.PI * u / Math.Max(0.001, reach));
                double off = sag * env + wobble * env * Math.Sin(u * Math.PI * 3 + phase) + (reach < 1 ? wobble * 0.6 * Math.Sin(u * 18 + phase * 2) * (u / reach) : 0);
                double px = x1 + dx * u + nx * off, py = y1 + dy * u + ny * off;
                pts[i] = new PointF((float)px, (float)py);
                minx = Math.Min(minx, px); miny = Math.Min(miny, py); maxx = Math.Max(maxx, px); maxy = Math.Max(maxy, py);
            }
            int pad = 8 * S;
            int wx = (int)minx - pad, wy = (int)miny - pad;
            web.Surf.Ensure((int)(maxx - minx) + 2 * pad, (int)(maxy - miny) + 2 * pad); web.Surf.Clear();
            Graphics g = web.Surf.G; g.TranslateTransform(-wx, -wy); g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen o = new Pen(Color.FromArgb(110, 60, 60, 80), 3.2f * k)) { o.LineJoin = LineJoin.Round; g.DrawLines(o, pts); }
            using (Pen w = new Pen(Color.FromArgb(245, 250, 250, 255), 1.6f * k)) { w.LineJoin = LineJoin.Round; g.DrawLines(w, pts); }
            PointF end = pts[N];
            if (reach >= 1)
                using (Pen w = new Pen(Color.FromArgb(230, 250, 250, 255), 1.2f * k))     // splat where it sticks
                    for (int i = 0; i < 6; i++) { double a = i * Math.PI / 3 + 0.3; g.DrawLine(w, end.X, end.Y, (float)(end.X + Math.Cos(a) * 5 * k), (float)(end.Y + Math.Sin(a) * 5 * k)); }
            web.Present(wx, wy); web.KeepOnTop();
            webOn = true;
        }
        public void WebOff() { if (webOn) { web.Hide(); webOn = false; } }

        // ---- the shield hero's thrown shield (spinning, with a little motion blur) ----
        readonly LayeredWindow shield = new LayeredWindow(true);
        readonly Cells shieldCells = new Cells(11, 11);
        bool shieldOn;
        public void Shield(double x, double y, int S, double spin)
        {
            shieldCells.Clear(); Sprite.DrawShield(shieldCells, 5, 5);
            int size = 11 * S, pad = 4 * S;
            shield.Surf.Ensure(size + 2 * pad, size + 2 * pad); shield.Surf.Clear();
            Graphics g = shield.Surf.G;
            g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
            float c = size / 2f + pad;
            // a squashed ellipse that "spins" (the shield turning on its edge)
            float sx = (float)Math.Max(0.25, Math.Abs(Math.Cos(spin * Math.PI / 180)));
            using (SolidBrush gl = new SolidBrush(Color.FromArgb(50, 255, 255, 255))) g.FillEllipse(gl, c - size * 0.62f, c - size * 0.62f, size * 1.24f, size * 1.24f);
            g.TranslateTransform(c, c); g.ScaleTransform(sx, 1);
            g.DrawImage(shieldCells.ToBitmap(), new RectangleF(-size / 2f, -size / 2f, size, size), new RectangleF(0, 0, 11, 11), GraphicsUnit.Pixel);
            g.ResetTransform();
            shield.Present((int)(x - c), (int)(y - c)); shield.KeepOnTop();
            shieldOn = true;
        }
        public void ShieldOff() { if (shieldOn) { shield.Hide(); shieldOn = false; } }
    }
}
