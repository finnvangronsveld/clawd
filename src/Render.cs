// Turns the cell art into what you see: Clawd crisp and scaled up (with squash/stretch and rotation) in one
// window, and the soft stuff (shadow, screen glow, particles, speech bubble) in a click-through window behind him.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace Clawd
{
    class Particle
    {
        public double X, Y, VX, VY, Gravity, Size = 1, Wobble;   // screen px; Size in sprite pixels
        public int Life, Max, Color; public string Glyph; public bool Bounce, Fade = true;
        public int Seed;
    }

    class Bubble
    {
        public string Text = ""; public int Ticks; public bool Spinner; public bool Thought;
    }

    class Renderer
    {
        public readonly LayeredWindow Body = new LayeredWindow(false);
        public readonly LayeredWindow Fx = new LayeredWindow(true);
        public readonly Cells Cells = new Cells(Sprite.CW, Sprite.CH);
        readonly Font font9;
        readonly ImageAttributes glowAttr = new ImageAttributes();
        int glowColor = -1;
        public int Frame;

        public Renderer()
        {
            font9 = new Font("Segoe UI Semibold", 9f, FontStyle.Regular, GraphicsUnit.Point);
        }

        // where cell (cx, cy) lands on screen
        static PointF Map(Matrix m, double cx, double cy)
        {
            PointF[] p = { new PointF((float)cx, (float)cy) };
            m.TransformPoints(p);
            return p[0];
        }

        public void Draw(Look L, double fx, double fy, int S, double rot, double sx, double sy, double groundY,
                         List<Particle> parts, Bubble bub, Native.RECT mon, bool glow)
        {
            Frame++;
            // --- sprite in cells ---
            Cells.Clear();
            if (L.Back) Sprite.DrawBack(Cells, L); else Sprite.DrawFront(Cells, L);
            int minX = Sprite.CW, minY = Sprite.CH, maxX = -1, maxY = -1;
            for (int y = 0; y < Sprite.CH; y++) for (int x = 0; x < Sprite.CW; x++)
                    if (Cells.Px[y * Sprite.CW + x] != 0) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
            if (maxX < 0) { minX = minY = 0; maxX = maxY = 1; }

            // cell space -> screen: feet at (fx, fy), rotate about his middle, squash/stretch from the feet
            Matrix m = new Matrix();
            m.Translate(-(Sprite.OX + 11), -(Sprite.OY + 16), MatrixOrder.Append);
            m.Scale((float)(S * sx), (float)(S * sy), MatrixOrder.Append);
            m.Translate(0, (float)(8 * S * sy), MatrixOrder.Append);
            m.Rotate((float)rot, MatrixOrder.Append);
            m.Translate((float)fx, (float)(fy - 8 * S * sy), MatrixOrder.Append);

            // gun: its own bitmap rotated at the hand
            Matrix gm = null, fm = null;
            if (L.Gun)
            {
                double shx, shy, hx, hy, c, s;
                double oy = Sprite.OY + ((L.Sit || L.Squash) ? 2 : 0);
                Sprite.Aim(L, Sprite.OX + L.Wob, oy, out shx, out shy, out hx, out hy, out c, out s);
                if (L.Recoil) { hx -= c; hy -= s; }
                gm = new Matrix();
                gm.Translate(-2f, -4.5f, MatrixOrder.Append);
                if (L.AimSide < 0) gm.Scale(1, -1, MatrixOrder.Append);
                gm.Rotate((float)L.AimDeg, MatrixOrder.Append);
                gm.Translate((float)hx, (float)hy, MatrixOrder.Append);
                gm.Multiply(m, MatrixOrder.Append);
            }

            // body window bounds = transformed bbox (+ gun)
            List<PointF> pts = new List<PointF>();
            foreach (double[] c in new[] { new double[] { minX, minY }, new double[] { maxX + 1, minY }, new double[] { minX, maxY + 1 }, new double[] { maxX + 1, maxY + 1 } })
                pts.Add(Map(m, c[0], c[1]));
            if (gm != null) foreach (double[] c in new[] { new double[] { 0, 0 }, new double[] { 15, 0 }, new double[] { 0, 7 }, new double[] { 15, 7 } }) pts.Add(Map(gm, c[0], c[1]));
            RectangleF bb = Bounds(pts, 2);
            int bx = (int)Math.Floor(bb.X), by = (int)Math.Floor(bb.Y);
            Surface bs = Body.Surf;
            bs.Ensure((int)Math.Ceiling(bb.Width) + 1, (int)Math.Ceiling(bb.Height) + 1);
            bs.Clear();
            Graphics g = bs.G;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.SmoothingMode = SmoothingMode.None;
            Bitmap cb = Cells.ToBitmap();
            Matrix bm = m.Clone(); bm.Translate(-bx, -by, MatrixOrder.Append);
            g.Transform = bm;
            g.DrawImage(cb, new Rectangle(0, 0, Sprite.CW, Sprite.CH), 0, 0, Sprite.CW, Sprite.CH, GraphicsUnit.Pixel);
            if (gm != null)
            {
                Matrix gm2 = gm.Clone(); gm2.Translate(-bx, -by, MatrixOrder.Append);
                g.Transform = gm2;
                g.DrawImage(Sprite.GunBitmap(false), new Rectangle(0, 0, 13, 7), 0, 0, 13, 7, GraphicsUnit.Pixel);
                if (L.Flash) g.DrawImage(Sprite.GunBitmap(true), new Rectangle(0, 0, 15, 7), 0, 0, 15, 7, GraphicsUnit.Pixel);
            }
            g.ResetTransform();

            // --- FX window: shadow, glow, particles, bubble ---
            List<PointF> fp = new List<PointF>(pts);
            double hover = Math.Max(0, groundY - fy);
            double shAlpha = Math.Max(0, 1 - hover / (160.0 * S / 3));
            float shW = (float)(18 * S * (1 - 0.4 * (1 - shAlpha))), shH = (float)(3.5 * S);
            if (shAlpha > 0.02) { fp.Add(new PointF((float)fx - shW / 2, (float)groundY - shH)); fp.Add(new PointF((float)fx + shW / 2, (float)groundY + shH)); }
            foreach (Particle p in parts) { fp.Add(new PointF((float)p.X - 6 * S, (float)p.Y - 6 * S)); fp.Add(new PointF((float)p.X + 6 * S, (float)p.Y + 6 * S)); }
            RectangleF bubRect = RectangleF.Empty; PointF tail = PointF.Empty;
            if (bub != null && bub.Ticks > 0 && bub.Text.Length > 0)
            {
                PointF head = Map(m, Sprite.OX + 11, Sprite.OY - 1);
                head.Y = Math.Min(head.Y, bb.Top);       // above anything he's holding up (laptop, "!", gun)
                bubRect = LayoutBubble(bub, head, S, mon, out tail);
                fp.Add(new PointF(bubRect.Left - 4, bubRect.Top - 4)); fp.Add(new PointF(bubRect.Right + 4, bubRect.Bottom + 4)); fp.Add(tail);
            }
            if (glow) { fp.Add(new PointF(bb.Left - 4 * S, bb.Top - 4 * S)); fp.Add(new PointF(bb.Right + 4 * S, bb.Bottom + 4 * S)); }
            RectangleF fb = Bounds(fp, 2);
            int fxx = (int)Math.Floor(fb.X), fyy = (int)Math.Floor(fb.Y);
            Surface fs = Fx.Surf;
            fs.Ensure(Math.Min(4000, (int)Math.Ceiling(fb.Width) + 1), Math.Min(4000, (int)Math.Ceiling(fb.Height) + 1));
            fs.Clear();
            Graphics f = fs.G;
            f.TranslateTransform(-fxx, -fyy);
            f.SmoothingMode = SmoothingMode.AntiAlias;

            if (shAlpha > 0.02)
            {
                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddEllipse((float)fx - shW / 2, (float)groundY - shH / 2, shW, shH);
                    using (PathGradientBrush pb = new PathGradientBrush(gp))
                    {
                        pb.CenterColor = Color.FromArgb((int)(90 * shAlpha), 0, 0, 0);
                        pb.SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) };
                        f.FillPath(pb, gp);
                    }
                }
            }
            if (glow)
            {
                // soft halo in the screen's light: the silhouette, tinted and smeared outwards
                if (glowColor != L.Rim)
                {
                    glowColor = L.Rim; Color c = Color.FromArgb(L.Rim);
                    ColorMatrix cm = new ColorMatrix(new[] {
                        new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 },
                        new float[] { 0, 0, 0, 0.16f, 0 }, new float[] { c.R / 255f, c.G / 255f, c.B / 255f, 0, 1 } });
                    glowAttr.SetColorMatrix(cm);
                }
                f.InterpolationMode = InterpolationMode.Bilinear;
                for (int i = 0; i < 12; i++)
                {
                    double a = i * Math.PI / 6, r = (i % 2 == 0 ? 2.2 : 1.2) * S;
                    Matrix gmx = m.Clone(); gmx.Translate((float)(Math.Cos(a) * r - fxx), (float)(Math.Sin(a) * r - fyy), MatrixOrder.Append);
                    f.Transform = gmx;
                    f.DrawImage(cb, new Rectangle(0, 0, Sprite.CW, Sprite.CH), 0, 0, Sprite.CW, Sprite.CH, GraphicsUnit.Pixel, glowAttr);
                }
                f.ResetTransform(); f.TranslateTransform(-fxx, -fyy);
            }
            foreach (Particle p in parts) DrawParticle(f, p, S);
            if (!bubRect.IsEmpty) DrawBubble(f, bub, bubRect, tail, S);

            // FX first (behind), then body on top
            Fx.Present(fxx, fyy);
            Body.Present(bx, by);
        }

        static RectangleF Bounds(List<PointF> pts, float pad)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (PointF p in pts) { x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); }
            return RectangleF.FromLTRB(x0 - pad, y0 - pad, x1 + pad, y1 + pad);
        }

        void DrawParticle(Graphics f, Particle p, int S)
        {
            double t = p.Max > 0 ? (double)p.Life / p.Max : 1;
            int alpha = p.Fade ? (int)(255 * Math.Min(1, t * 2.5)) : 255;
            Color c = Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), Color.FromArgb(p.Color));
            using (SolidBrush b = new SolidBrush(c))
            {
                SmoothingMode old = f.SmoothingMode; f.SmoothingMode = SmoothingMode.None;
                float cs = (float)(S * p.Size);
                if (p.Glyph == null) f.FillRectangle(b, (float)p.X - cs / 2, (float)p.Y - cs / 2, cs, cs);
                else
                {
                    string[] rows = Glyphs.Get(p.Glyph); float px = S;
                    float ox = (float)p.X - rows[0].Length * px / 2, oy = (float)p.Y - rows.Length * px / 2;
                    for (int i = 0; i < rows.Length; i++) for (int j = 0; j < rows[i].Length; j++) if (rows[i][j] == 'X') f.FillRectangle(b, ox + j * px, oy + i * px, px, px);
                }
                f.SmoothingMode = old;
            }
        }

        RectangleF LayoutBubble(Bubble bub, PointF head, int S, Native.RECT mon, out PointF tail)
        {
            float k = S / 3f;
            using (Font ft = new Font(font9.FontFamily, 9f * k, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                SizeF sz;
                using (Bitmap tmp = new Bitmap(1, 1)) using (Graphics g = Graphics.FromImage(tmp)) sz = g.MeasureString(bub.Text, ft, (int)(170 * k));
                float spin = bub.Spinner ? 10 * k : 0;
                float w = sz.Width + 12 * k + spin, h = sz.Height + 8 * k;
                float x = head.X - w / 2, y = head.Y - h - 9 * k;
                x = Math.Max(mon.Left + 4, Math.Min(mon.Right - 4 - w, x));
                if (y < mon.Top + 4) y = mon.Top + 4;
                tail = new PointF(Math.Max(x + 8 * k, Math.Min(x + w - 8 * k, head.X)), y + h + 6 * k);
                return new RectangleF(x, y, w, h);
            }
        }

        void DrawBubble(Graphics f, Bubble bub, RectangleF r, PointF tail, int S)
        {
            float k = S / 3f, rad = 6 * k;
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddArc(r.Left, r.Top, rad * 2, rad * 2, 180, 90);
                gp.AddArc(r.Right - rad * 2, r.Top, rad * 2, rad * 2, 270, 90);
                gp.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
                if (!bub.Thought)
                {
                    float tx = tail.X;
                    gp.AddLine(Math.Min(r.Right - rad, tx + 5 * k), r.Bottom, tx, tail.Y);
                    gp.AddLine(tx, tail.Y, Math.Max(r.Left + rad, tx - 3 * k), r.Bottom);
                }
                gp.AddArc(r.Left, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
                gp.CloseFigure();
                // soft drop shadow
                using (Matrix sh = new Matrix()) using (GraphicsPath sp = (GraphicsPath)gp.Clone())
                {
                    sh.Translate(0, 2 * k); sp.Transform(sh);
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) f.FillPath(sb, sp);
                }
                using (SolidBrush wb = new SolidBrush(Color.FromArgb(250, 255, 253, 250))) f.FillPath(wb, gp);
                using (Pen pn = new Pen(Color.FromArgb(Pal.Ink), 1.5f * k)) f.DrawPath(pn, gp);
            }
            if (bub.Thought)
            {
                using (SolidBrush wb = new SolidBrush(Color.FromArgb(250, 255, 253, 250))) using (Pen pn = new Pen(Color.FromArgb(Pal.Ink), 1.2f * k))
                {
                    float d1 = 5 * k, d2 = 3 * k;
                    f.FillEllipse(wb, tail.X - d1 / 2, r.Bottom + 1 * k, d1, d1); f.DrawEllipse(pn, tail.X - d1 / 2, r.Bottom + 1 * k, d1, d1);
                    f.FillEllipse(wb, tail.X + 2 * k, r.Bottom + 7 * k, d2, d2); f.DrawEllipse(pn, tail.X + 2 * k, r.Bottom + 7 * k, d2, d2);
                }
            }
            float textX = r.Left + 6 * k;
            if (bub.Spinner)
            {
                int[] seq = { 0, 1, 2, 3, 2, 1 };
                string[] rows = Glyphs.Get("sp" + seq[(Frame / 6) % 6]);
                float px = 1.6f * k, gy = r.Top + (r.Height - 5 * px) / 2;
                using (SolidBrush ob = new SolidBrush(Color.FromArgb(Pal.Orange)))
                    for (int i = 0; i < 5; i++) for (int j = 0; j < 5; j++) if (rows[i][j] == 'X') f.FillRectangle(ob, textX + j * px, gy + i * px, px, px);
                textX += 10 * k;
            }
            f.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (Font ft = new Font(font9.FontFamily, 9f * k, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(Pal.Ink)))
                f.DrawString(bub.Text, ft, tb, new RectangleF(textX, r.Top + 4 * k, r.Width - (textX - r.Left) - 4 * k, r.Height));
        }

        public void KeepOnTop() { Fx.KeepOnTop(); Body.KeepOnTop(); }

        // same picture, new place: just move both windows (much cheaper than redrawing)
        public void Move(int dx, int dy)
        {
            if (Fx.Surf.Bmp == null || Body.Surf.Bmp == null) return;
            Fx.Present(Fx.SX + dx, Fx.SY + dy);
            Body.Present(Body.SX + dx, Body.SY + dy);
        }
    }
}
