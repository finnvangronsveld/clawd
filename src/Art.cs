// Clawd's pixel art. Everything is drawn into a small grid of "cells" (one cell = one sprite pixel);
// the renderer scales that up crisply and adds the soft stuff (shadow, glow, particles, bubbles).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Clawd
{
    static class Pal
    {
        public static int C(int r, int g, int b) { return unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b; }
        public static Color Col(int argb) { return Color.FromArgb(argb); }
        public static readonly int Orange = C(217, 119, 87), Light = C(235, 150, 117), Shade = C(186, 94, 64), Outline = C(112, 50, 32);
        public static readonly int Eye = C(28, 22, 20), Shine = C(235, 235, 240), Blush = C(240, 128, 128), Tongue = C(230, 100, 110), Ink = C(40, 32, 30);
        public static readonly int White = C(250, 250, 250), Silver = C(190, 195, 204), Dark = C(78, 82, 92), Blue = C(110, 175, 255), Red = C(235, 70, 70);
        public static readonly int Spark = C(255, 210, 70), Coffee = C(110, 68, 42), Steam = C(200, 200, 210), Heart = C(240, 80, 115);
        public static readonly int Purple = C(160, 120, 255), Teal = C(50, 195, 175);
        public static readonly int Gun = C(132, 138, 150), GunHi = C(205, 210, 220), Grip = C(96, 62, 40);
        public static readonly int Back = C(88, 44, 33), Kernel = C(250, 238, 195), KernelShade = C(214, 188, 128), BucketRed = C(122, 34, 36), BucketWhite = C(148, 148, 158);
        public static readonly int SuitRed = C(200, 32, 42), SuitLight = C(232, 82, 88), SuitBlue = C(36, 76, 186), WebLine = C(118, 16, 26);
        public static readonly int Phones = C(52, 56, 66), PhonesHi = C(98, 104, 118), Cap = C(70, 90, 190), CapHi = C(110, 130, 225), Pom = C(245, 245, 250);
        public static readonly int[] Notes = { Purple, Teal, Orange, Heart };
        public static int Mix(int a, int b, double t)
        {
            int ar = (a >> 16) & 255, ag = (a >> 8) & 255, ab = a & 255, br = (b >> 16) & 255, bg = (b >> 8) & 255, bb = b & 255;
            return C((int)(ar + (br - ar) * t), (int)(ag + (bg - ag) * t), (int)(ab + (bb - ab) * t));
        }
    }

    // A tiny pixel canvas. Px == 0 means transparent.
    class Cells
    {
        public readonly int W, H;
        public readonly int[] Px;
        readonly Bitmap bmp;
        public Cells(int w, int h) { W = w; H = h; Px = new int[w * h]; bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb); }
        public void Clear() { Array.Clear(Px, 0, Px.Length); }
        public void Rect(double x, double y, int w, int h, int c)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            int x1 = Math.Min(W, x0 + w), y1 = Math.Min(H, y0 + h);
            for (int yy = Math.Max(0, y0); yy < y1; yy++)
                for (int xx = Math.Max(0, x0); xx < x1; xx++) Px[yy * W + xx] = c;
        }
        public void Dot(double x, double y, int c) { Rect(x, y, 1, 1, c); }
        public int Get(int x, int y) { return (x < 0 || y < 0 || x >= W || y >= H) ? 0 : Px[y * W + x]; }
        public void Glyph(string name, double x, double y, int c)
        {
            string[] rows = Glyphs.Get(name);
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            for (int i = 0; i < rows.Length; i++) for (int j = 0; j < rows[i].Length; j++) if (rows[i][j] == 'X') Dot(x0 + j, y0 + i, c);
        }
        public Bitmap ToBitmap()
        {
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(Px, 0, bd.Scan0, Px.Length);
            bmp.UnlockBits(bd);
            return bmp;
        }
    }

    static class Glyphs
    {
        static readonly Dictionary<string, string[]> map = new Dictionary<string, string[]>
        {
            { "heart", new[] { ".X.X.", "XXXXX", "XXXXX", ".XXX.", "..X.." } },
            { "note",  new[] { ".XXXX", ".X..X", ".X..X", "XX.XX", "XX.XX" } },
            { "z",     new[] { "XXXX", "..X.", ".X..", "XXXX" } },
            { "star",  new[] { ".X.", "XXX", ".X." } },
            { "dot",   new[] { "X" } },
            { "drop",  new[] { ".X.", "XXX", "XXX", ".X." } },
            { "paper", new[] { "XXX.", "X.XX", "X..X", "XXXX" } },
            { "bolt",  new[] { "..XX", ".XX.", "XXXX", ".XX.", "XX.." } },
            { "sp0",   new[] { ".....", ".....", "..X..", ".....", "....." } },
            { "sp1",   new[] { ".....", "..X..", ".XXX.", "..X..", "....." } },
            { "sp2",   new[] { "X.X.X", ".XXX.", "XXXXX", ".XXX.", "X.X.X" } },
            { "sp3",   new[] { "..X..", "X.X.X", ".XXX.", "X.X.X", "..X.." } },
        };
        public static string[] Get(string n) { string[] r; return map.TryGetValue(n, out r) ? r : map["dot"]; }
    }

    // Everything about how Clawd looks this frame (reset every tick, then set by the current behaviour).
    class Look
    {
        public string Arms = "out", EyeStyle = "normal", Mouth = "none", Laptop = "none", Glow = "blue", Mug = "none", Held = "none";
        public double HeldLeft = 1; public bool HeldUp;
        public int Eye, Phase;
        public double Wob, LapRow;
        public bool Blush, Sit, Squash, Bang, Suit, Back, Headphones, Nightcap, Blink, Spin;
        public bool Gun, Recoil, Flash; public double AimDeg; public int AimSide = 1;
        public int Rim = Pal.C(165, 212, 255);
        // everything that affects the picture, as one string (so unchanged frames can be skipped)
        public string Key()
        {
            return Arms + EyeStyle + Mouth + Laptop + Glow + Mug + Held + HeldLeft + HeldUp + Eye + Phase + Wob + LapRow + Blush + Sit + Squash + Bang + Suit + Back
                   + Headphones + Nightcap + Blink + Gun + Recoil + Flash + (int)AimDeg + AimSide + Rim;
        }
        public void Reset()
        {
            Arms = "out"; EyeStyle = "normal"; Mouth = "none"; Laptop = "none"; Mug = "none"; Held = "none";
            Phase = 0; Wob = 0; Blush = Sit = Squash = Bang = Back = Spin = false; Gun = Recoil = Flash = false;
            Headphones = Nightcap = HeldUp = false; HeldLeft = 1;
        }
    }

    static class Sprite
    {
        // Clawd's 22x16 box sits at (OX, OY) in the cell canvas; his feet are on row OY+16.
        public const int CW = 60, CH = 34, OX = 19, OY = 16;
        public const double ARM = 6.0;

        // shoulder + hand of the aiming arm (cells)
        public static void Aim(Look L, double ox, double oy, out double shx, out double shy, out double hx, out double hy, out double c, out double s)
        {
            bool left = L.AimSide < 0;
            shx = left ? ox + 3 : ox + 19; shy = oy + 7;
            double a = L.AimDeg * Math.PI / 180; c = Math.Cos(a); s = Math.Sin(a);
            hx = shx + c * ARM; hy = shy + s * ARM;
        }

        public static void DrawFront(Cells g, Look L)
        {
            double ox = OX + L.Wob, oy = OY;
            bool low = L.Sit || L.Squash;
            if (low) oy += 2;
            int e = L.Eye;
            var parts = new List<double[]>();
            Action<double, double, int, int> add = (x, y, w, h) => parts.Add(new double[] { x, y, w, h });

            double bx = ox + 4; int bw = 14;
            add(bx, oy, bw, 12);
            switch (L.Arms)
            {
                case "out": add(ox, oy + 6, 4, 2); add(ox + 18, oy + 6, 4, 2); break;
                case "typeL": add(ox, oy + 8, 4, 2); add(ox + 18, oy + 6, 4, 2); break;
                case "typeR": add(ox, oy + 6, 4, 2); add(ox + 18, oy + 8, 4, 2); break;
                case "down": add(ox, oy + 8, 4, 2); add(ox + 18, oy + 8, 4, 2); break;
                case "up": add(ox + 2, oy - 3, 2, 10); add(ox + 18, oy - 3, 2, 10); break;
                case "upL": add(ox + 2, oy - 3, 2, 10); add(ox + 18, oy + 6, 4, 2); break;
                case "upR": add(ox, oy + 6, 4, 2); add(ox + 18, oy - 3, 2, 10); break;
                case "wave1": add(ox, oy + 6, 4, 2); add(ox + 18, oy - 3, 2, 10); break;
                case "wave2": add(ox, oy + 6, 4, 2); add(ox + 18, oy + 3, 2, 4); add(ox + 20, oy - 2, 2, 6); break;
                case "sip": add(ox, oy + 6, 4, 2); add(ox + 18, oy + 8, 3, 2); break;
                case "hold": add(ox + 1, oy + 6, 3, 2); add(ox + 18, oy + 6, 3, 2); add(ox + 4, oy + 8, 2, 2); add(ox + 16, oy + 8, 2, 2); break;
                case "fan": add(ox, oy + 6, 4, 2); add(ox + 18, oy + 1 + (L.Phase % 2) * 2, 2, 7); break;
                case "aim":
                    {
                        double shx, shy, hx, hy, c, s; Aim(L, ox, oy, out shx, out shy, out hx, out hy, out c, out s);
                        if (L.AimSide < 0) add(ox + 18, oy + 6, 4, 2); else add(ox, oy + 6, 4, 2);
                        for (double d = 0; d <= ARM; d += 0.5) add(shx + c * d - 1, shy + s * d - 1, 2, 2);
                        break;
                    }
            }
            int[] legH = new int[4]; int[] legX = { 4, 8, 12, 16 };
            for (int i = 0; i < 4; i++)
            {
                bool lifted = (L.Phase == 1 && (legX[i] == 8 || legX[i] == 16)) || (L.Phase == 3 && (legX[i] == 4 || legX[i] == 12));
                legH[i] = low ? 2 : (lifted ? 3 : 4);
            }
            foreach (double[] q in parts) g.Rect(q[0] - 1, q[1] - 1, (int)q[2] + 2, (int)q[3] + 2, Pal.Outline);
            for (int i = 0; i < 4; i++) g.Rect(ox + legX[i], oy + 12 + legH[i], 2, 1, Pal.Outline);
            g.Rect(ox + 3, oy + 12, 1, legH[0] + 1, Pal.Outline); g.Rect(ox + 18, oy + 12, 1, legH[3] + 1, Pal.Outline);
            foreach (double[] q in parts) g.Rect(q[0], q[1], (int)q[2], (int)q[3], Pal.Orange);
            for (int i = 0; i < 4; i++) { g.Rect(ox + legX[i], oy + 12, 2, legH[i], Pal.Orange); g.Rect(ox + legX[i] + 1, oy + 12, 1, legH[i], Pal.Shade); }
            // shading: lit from the top-left
            g.Rect(bx, oy, bw, 1, Pal.Light); g.Rect(bx, oy + 1, 1, 10, Pal.Light);
            g.Rect(bx, oy + 11, bw, 1, Pal.Shade); g.Rect(bx + bw - 1, oy + 1, 1, 11, Pal.Shade);
            g.Rect(bx + 1, oy + 1, 2, 1, Pal.Mix(Pal.Light, Pal.White, 0.35));   // tiny specular

            if (L.Suit)
            {
                for (int i = 1; i < parts.Count; i++) { double[] q = parts[i]; g.Rect(q[0], q[1], (int)q[2], (int)q[3], Pal.SuitRed); }
                g.Rect(bx, oy, bw, 8, Pal.SuitRed); g.Rect(bx, oy + 8, bw, 4, Pal.SuitBlue);
                for (int i = 0; i < 4; i++) g.Rect(ox + legX[i], oy + 12, 2, legH[i], Pal.SuitBlue);
                foreach (int wx in new[] { 3, 7, 10 }) g.Rect(bx + wx, oy + 1, 1, 7, Pal.WebLine);
                g.Rect(bx + 1, oy + 3, bw - 2, 1, Pal.WebLine); g.Rect(bx + 1, oy + 6, bw - 2, 1, Pal.WebLine);
                g.Rect(bx, oy, bw, 1, Pal.SuitLight); g.Rect(bx, oy + 1, 1, 7, Pal.SuitLight);
                g.Rect(ox + 10, oy + 6, 2, 2, Pal.Eye);
                g.Dot(ox + 9, oy + 5, Pal.Eye); g.Dot(ox + 12, oy + 5, Pal.Eye); g.Dot(ox + 9, oy + 8, Pal.Eye); g.Dot(ox + 12, oy + 8, Pal.Eye);
                foreach (double mx in new[] { ox + 5, ox + 13 })
                {
                    g.Rect(mx, oy + 2, 4, 4, Pal.Eye);
                    if (L.Blink) g.Rect(mx + 1, oy + 4, 2, 1, Pal.White);
                    else { g.Rect(mx + 1, oy + 3, 2, 2, Pal.White); g.Rect(mx + 1, oy + 2, 2, 1, Pal.Eye); }
                }
            }
            else DrawFace(g, L, ox, oy, e);

            // ---- props ----
            if (L.Headphones && !L.Suit)
            {
                g.Rect(ox + 4, oy - 2, 14, 1, Pal.Phones); g.Rect(ox + 3, oy - 1, 1, 3, Pal.Phones); g.Rect(ox + 18, oy - 1, 1, 3, Pal.Phones);
                g.Rect(ox + 2, oy + 2, 3, 4, Pal.Phones); g.Rect(ox + 17, oy + 2, 3, 4, Pal.Phones);
                g.Rect(ox + 2, oy + 2, 1, 4, Pal.PhonesHi); g.Rect(ox + 17, oy + 2, 1, 4, Pal.PhonesHi);
            }
            if (L.Nightcap && !L.Suit)
            {
                g.Rect(ox + 5, oy - 1, 12, 2, Pal.Cap); g.Rect(ox + 7, oy - 3, 9, 2, Pal.Cap); g.Rect(ox + 10, oy - 5, 7, 2, Pal.Cap);
                g.Rect(ox + 14, oy - 6, 4, 1, Pal.Cap); g.Rect(ox + 5, oy - 1, 12, 1, Pal.CapHi);
                g.Rect(ox + 17, oy - 7, 2, 2, Pal.Pom);
            }
            if (L.Mug != "none")
            {
                double mx, my;
                if (L.Mug == "sip") { mx = ox + 17; my = oy + 4; } else { mx = ox + 21; my = oy + 1; }
                g.Rect(mx - 1, my - 1, 6, 7, Pal.Outline); g.Rect(mx + 5, my + 1, 1, 3, Pal.Outline);
                g.Rect(mx, my, 4, 5, Pal.White); g.Rect(mx, my, 4, 1, Pal.Coffee);
            }
            if (L.Held != "none") Food.DrawInto(g, L.Held, ox + 8, oy + (L.HeldUp ? 5 : 8), L.HeldLeft);
            if (L.Laptop == "open")
            {
                int glow = L.Glow == "red" ? Pal.Red : Pal.Blue;
                g.Rect(ox + 3, oy + 7, 16, 7, Pal.Outline); g.Rect(ox + 4, oy + 8, 14, 1, glow);
                g.Rect(ox + 4, oy + 9, 14, 4, Pal.Silver); g.Rect(ox + 10, oy + 10, 2, 2, Pal.White);
                g.Rect(ox + 1, oy + 13, 20, 3, Pal.Outline); g.Rect(ox + 2, oy + 14, 18, 1, Pal.Dark);
            }
            else if (L.Laptop == "held")
            {
                g.Rect(ox - 1, L.LapRow - 1, 24, 4, Pal.Outline); g.Rect(ox, L.LapRow, 22, 1, Pal.Silver); g.Rect(ox, L.LapRow + 1, 22, 1, Pal.Dark);
            }
            if (L.Bang) { g.Rect(ox + 10, oy - 12, 2, 4, Pal.Red); g.Rect(ox + 10, oy - 7, 2, 2, Pal.Red); }
        }

        static void DrawFace(Cells g, Look L, double ox, double oy, int e)
        {
            string style = L.EyeStyle;
            if (L.Blink && (style == "normal" || style == "up" || style == "down" || style == "sad")) style = "blink";
            double ex1 = ox + 6 + 2 * e, ex2 = ox + 14 + 2 * e;
            if (style == "angry")
            {
                g.Rect(ex1, oy + 3, 2, 3, Pal.Eye); g.Rect(ex2, oy + 3, 2, 3, Pal.Eye);
                g.Rect(ex1 - 1, oy + 1, 2, 1, Pal.Eye); g.Rect(ex1 + 1, oy + 2, 2, 1, Pal.Eye);
                g.Rect(ex2 + 1, oy + 1, 2, 1, Pal.Eye); g.Rect(ex2 - 1, oy + 2, 2, 1, Pal.Eye);
            }
            else if (style == "sad")
            {
                g.Rect(ex1, oy + 3, 2, 3, Pal.Eye); g.Rect(ex2, oy + 3, 2, 3, Pal.Eye);
                g.Dot(ex1 + 1, oy + 3, Pal.Shine); g.Dot(ex2 + 1, oy + 3, Pal.Shine);
                g.Rect(ex1 + 1, oy + 1, 2, 1, Pal.Eye); g.Rect(ex1 - 1, oy + 2, 2, 1, Pal.Eye);
                g.Rect(ex2 - 1, oy + 1, 2, 1, Pal.Eye); g.Rect(ex2 + 1, oy + 2, 2, 1, Pal.Eye);
            }
            else
            {
                foreach (double ex in new[] { ex1, ex2 })
                {
                    switch (style)
                    {
                        case "normal": g.Rect(ex, oy + 2, 2, 4, Pal.Eye); g.Dot(ex + 1, oy + 2, Pal.Shine); break;
                        case "up": g.Rect(ex, oy + 1, 2, 4, Pal.Eye); g.Dot(ex + 1, oy + 1, Pal.Shine); break;
                        case "down": g.Rect(ex, oy + 3, 2, 4, Pal.Eye); break;
                        case "wide": g.Rect(ex - 1, oy + 2, 3, 4, Pal.Eye); g.Rect(ex, oy + 2, 1, 2, Pal.Shine); break;
                        case "blink": g.Rect(ex, oy + 4, 2, 1, Pal.Eye); break;
                        case "sleep": g.Dot(ex - 1, oy + 3, Pal.Eye); g.Rect(ex, oy + 4, 2, 1, Pal.Eye); g.Dot(ex + 2, oy + 3, Pal.Eye); break;
                        case "happy": g.Dot(ex - 1, oy + 4, Pal.Eye); g.Rect(ex, oy + 3, 2, 1, Pal.Eye); g.Dot(ex + 2, oy + 4, Pal.Eye); break;
                        case "dizzy":
                            g.Dot(ex, oy + 2, Pal.Eye); g.Dot(ex + 2, oy + 2, Pal.Eye); g.Dot(ex + 1, oy + 3, Pal.Eye);
                            g.Dot(ex, oy + 4, Pal.Eye); g.Dot(ex + 2, oy + 4, Pal.Eye); break;
                        case "heart": g.Glyph("heart", ex - 1, oy + 2, Pal.Heart); break;
                    }
                }
            }
            if (L.Blush) { g.Rect(ox + 5, oy + 7, 2, 1, Pal.Blush); g.Rect(ox + 15, oy + 7, 2, 1, Pal.Blush); }
            switch (L.Mouth)
            {
                case "o": g.Rect(ox + 10, oy + 7, 2, 2, Pal.Eye); break;
                case "smile": g.Dot(ox + 9, oy + 7, Pal.Eye); g.Rect(ox + 10, oy + 8, 2, 1, Pal.Eye); g.Dot(ox + 12, oy + 7, Pal.Eye); break;
                case "frown": g.Dot(ox + 9, oy + 8, Pal.Eye); g.Rect(ox + 10, oy + 7, 2, 1, Pal.Eye); g.Dot(ox + 12, oy + 8, Pal.Eye); break;
                case "yawn": g.Rect(ox + 9, oy + 7, 4, 3, Pal.Eye); g.Rect(ox + 10, oy + 9, 2, 1, Pal.Tongue); break;
                case "chomp": g.Rect(ox + 8, oy + 7, 6, 3, Pal.Eye); g.Rect(ox + 9, oy + 9, 4, 1, Pal.Tongue); break;
                case "chew": g.Rect(ox + 9, oy + 8, 4, 1, Pal.Eye); break;
            }
        }

        // movie night, seen from behind: dark body (the renderer adds the screen-coloured rim glow)
        public static void DrawBack(Cells g, Look L)
        {
            double ox = OX + L.Wob, oy = OY + 2;
            int rim = L.Rim, band = Pal.Mix(Pal.Back, L.Rim, 0.4);
            double bx2 = ox + 22, by2 = oy + 7;
            g.Rect(bx2 - 1, by2 - 3, 8, 11, Pal.Outline); g.Rect(bx2, by2 - 4, 6, 1, Pal.Outline);
            for (int col = 0; col < 6; col++) g.Rect(bx2 + col, by2, 1, 7, col % 2 == 0 ? Pal.BucketRed : Pal.BucketWhite);
            g.Rect(bx2, by2, 6, 1, Pal.Mix(Pal.BucketWhite, rim, 0.5));
            g.Rect(bx2, by2 - 2, 6, 2, Pal.Kernel); g.Rect(bx2 + 1, by2 - 3, 4, 1, Pal.Kernel);
            g.Dot(bx2 + 1, by2 - 1, Pal.KernelShade); g.Dot(bx2 + 4, by2 - 2, Pal.KernelShade);

            var parts = new List<double[]>();
            parts.Add(new double[] { ox + 4, oy, 14, 12 });
            parts.Add(new double[] { ox, oy + 6, 4, 2 });
            if (L.Arms == "reach") parts.Add(new double[] { ox + 18, oy + 6, 5, 2 });
            else if (L.Arms == "eat") parts.Add(new double[] { ox + 18, oy + 1, 2, 7 });
            else parts.Add(new double[] { ox + 18, oy + 6, 4, 2 });
            int rimLine = Pal.Mix(Pal.Outline, rim, 0.8);       // backlit: his outline glows in the screen's colour
            foreach (double[] q in parts) g.Rect(q[0] - 1, q[1] - 1, (int)q[2] + 2, (int)q[3] + 2, rimLine);
            g.Rect(ox + 3, oy + 12, 1, 3, rimLine); g.Rect(ox + 18, oy + 12, 1, 3, rimLine);
            foreach (double[] q in parts) g.Rect(q[0], q[1], (int)q[2], (int)q[3], Pal.Back);
            g.Rect(ox + 4, oy + 12, 14, 1, Pal.Back);
            foreach (int c in new[] { 4, 8, 12, 16 }) g.Rect(ox + c, oy + 12, 2, 2, Pal.Back);
            // screen light wrapping round his edges
            g.Rect(ox + 4, oy, 14, 1, band); g.Rect(ox + 4, oy + 1, 1, 11, band); g.Rect(ox + 17, oy + 1, 1, 11, band);
            g.Rect(ox, oy + 6, 4, 1, band);
            if (L.Arms == "eat") g.Rect(ox + 18, oy + 1, 1, 7, band); else g.Rect(ox + 18, oy + 6, 4, 1, band);
            if (L.Headphones) { g.Rect(ox + 4, oy - 2, 14, 1, Pal.Phones); g.Rect(ox + 3, oy - 1, 1, 3, Pal.Phones); g.Rect(ox + 18, oy - 1, 1, 3, Pal.Phones); }
        }

        // the pistol, pointing right; pivot (hand) at grip centre (2, 4.5), muzzle tip at (11, 2)
        public static readonly string[] GunRows = { "oooooooooo...", "ohhhhhhhhho..", "ogggggggggo..", "obbgoooooo...", "obbo.........", "obbo.........", "oooo........." };
        public static readonly string[] FlashRows = { "............f..", "...........fff.", "..........fwwff", "...........fff.", "............f..", "...............", "..............." };
        static Bitmap gunBmp, flashBmp;
        public static Bitmap GunBitmap(bool flash)
        {
            if (gunBmp == null)
            {
                gunBmp = RowsToBitmap(GunRows, "ohgb", new[] { Pal.Outline, Pal.GunHi, Pal.Gun, Pal.Grip });
                flashBmp = RowsToBitmap(FlashRows, "fw", new[] { Pal.Spark, Pal.White });
            }
            return flash ? flashBmp : gunBmp;
        }
        public static Bitmap RowsToBitmap(string[] rows, string keys, int[] cols)
        {
            Cells c = new Cells(rows[0].Length, rows.Length);
            for (int y = 0; y < rows.Length; y++) for (int x = 0; x < rows[y].Length; x++) { int k = keys.IndexOf(rows[y][x]); if (k >= 0) c.Dot(x, y, cols[k]); }
            return (Bitmap)c.ToBitmap().Clone();
        }

        // the app icon (also used for the .ico)
        public static Bitmap IconArt(int size)
        {
            Cells g = new Cells(24, 24);
            Look L = new Look();
            // draw a mini Clawd centred: reuse DrawFront on a temp canvas and copy his box
            Cells big = new Cells(CW, CH); DrawFront(big, L);
            for (int y = 0; y < 18; y++) for (int x = 0; x < 24; x++) g.Px[(y + 3) * 24 + x] = big.Get(OX - 1 + x, OY - 1 + y);
            Bitmap src = g.ToBitmap();
            Bitmap dst = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics gr = Graphics.FromImage(dst))
            {
                gr.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                gr.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                gr.DrawImage(src, new Rectangle(0, 0, size, size), new Rectangle(0, 0, 24, 24), GraphicsUnit.Pixel);
            }
            return dst;
        }
    }
}
