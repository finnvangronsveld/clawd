// The pets' pixel art. Everything is drawn into a small grid of "cells" (one cell = one sprite pixel);
// the renderer scales that up crisply and adds the soft stuff (shadow, glow, particles, bubbles).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Flippy
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

    // Everything about how the pet looks this frame (reset every tick, then set by the current behaviour).
    class Look
    {
        public string Arms = "out", EyeStyle = "normal", Mouth = "none", Laptop = "none", Glow = "blue", Mug = "none", Held = "none";
        public double HeldLeft = 1; public bool HeldUp;
        public int Eye, Phase, Face = 1, TuftLift;      // Face: which way he points (1 right, -1 left)
        public double Wob, LapRow;
        public bool Blush, Sit, Squash, Bang, Suit, Back, Headphones, Nightcap, Blink, Spin;
        public bool Gun, Recoil, Flash; public double AimDeg; public int AimSide = 1;
        public int Rim = Pal.C(165, 212, 255);
        // everything that affects the picture, as one string (so unchanged frames can be skipped)
        public string Key()
        {
            return Arms + EyeStyle + Mouth + Laptop + Glow + Mug + Held + HeldLeft + HeldUp + Eye + Phase + Wob + LapRow + Blush + Sit + Squash + Bang + Suit + Back
                   + Face + TuftLift + Headphones + Nightcap + Blink + Gun + Recoil + Flash + (int)AimDeg + AimSide + Rim;
        }
        public void Reset()
        {
            Arms = "out"; EyeStyle = "normal"; Mouth = "none"; Laptop = "none"; Mug = "none"; Held = "none";
            Phase = 0; Wob = 0; Blush = Sit = Squash = Bang = Back = Spin = false; Gun = Recoil = Flash = false;
            Headphones = Nightcap = HeldUp = false; HeldLeft = 1; TuftLift = 0;
        }
    }

    static class Sprite
    {
        // the pet's 22x16 box sits at (OX, OY) in the cell canvas; his feet are on row OY+16.
        public const int CW = 60, CH = 34, OX = 19, OY = 16;
        public const double ARM = 6.0;

        // shoulder + hand of the aiming arm (cells)
        public static void Aim(Look L, double ox, double oy, out double shx, out double shy, out double hx, out double hy, out double c, out double s)
        {
            Species sp = SpeciesList.Current;
            bool left = L.AimSide < 0;
            shx = left ? ox + sp.ShoulderLX : ox + sp.ShoulderRX; shy = oy + sp.ShoulderY;
            double a = L.AimDeg * Math.PI / 180; c = Math.Cos(a); s = Math.Sin(a);
            hx = shx + c * ARM; hy = shy + s * ARM;
        }

        public static void DrawFront(Cells g, Look L) { DrawFront(g, L, SpeciesList.Current); }
        public static void DrawFront(Cells g, Look L, Species sp)
        {
            double ox = OX + L.Wob, oy = OY;
            bool low = L.Sit || L.Squash;
            if (low) oy += 2;

            sp.DrawBody(g, L, ox, oy, low);
            if (L.Suit) DrawSuit(g, L, sp, ox, oy);
            else sp.DrawFace(g, L, ox, oy, L.Eye);

            // ---- props (shared by every pet, placed with the species' anchors) ----
            double top = oy + sp.HeadTop;
            if (L.Headphones && !L.Suit)
            {
                g.Rect(ox + 4, top - 2, 14, 1, Pal.Phones); g.Rect(ox + 3, top - 1, 1, 3, Pal.Phones); g.Rect(ox + 18, top - 1, 1, 3, Pal.Phones);
                g.Rect(ox + 2, top + 2, 3, 4, Pal.Phones); g.Rect(ox + 17, top + 2, 3, 4, Pal.Phones);
                g.Rect(ox + 2, top + 2, 1, 4, Pal.PhonesHi); g.Rect(ox + 17, top + 2, 1, 4, Pal.PhonesHi);
            }
            if (L.Nightcap && !L.Suit)
            {
                g.Rect(ox + 5, top - 1, 12, 2, Pal.Cap); g.Rect(ox + 7, top - 3, 9, 2, Pal.Cap); g.Rect(ox + 10, top - 5, 7, 2, Pal.Cap);
                g.Rect(ox + 14, top - 6, 4, 1, Pal.Cap); g.Rect(ox + 5, top - 1, 12, 1, Pal.CapHi);
                g.Rect(ox + 17, top - 7, 2, 2, Pal.Pom);
            }
            if (L.Mug != "none")
            {
                double mx, my;
                if (L.Mug == "sip") { mx = ox + sp.MugSipX; my = oy + sp.MugSipY; } else { mx = ox + sp.MugHoldX; my = oy + sp.MugHoldY; }
                g.Rect(mx - 1, my - 1, 6, 7, Pal.Outline); g.Rect(mx + 5, my + 1, 1, 3, Pal.Outline);
                g.Rect(mx, my, 4, 5, Pal.White); g.Rect(mx, my, 4, 1, Pal.Coffee);
            }
            if (L.Held != "none") Food.DrawInto(g, L.Held, ox + sp.HeldX, oy + (L.HeldUp ? sp.HeldUpY : sp.HeldY), L.HeldLeft);
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
            if (L.Bang) { g.Rect(ox + sp.BangX, oy + sp.BangY, 2, 4, Pal.Red); g.Rect(ox + sp.BangX, oy + sp.BangY + 5, 2, 2, Pal.Red); }
        }

        // web-slinger suit, for any pet: recolour his body-coloured pixels (red on top, blue below),
        // add web lines on the red part, the chest spider and the mask eyes
        static void DrawSuit(Cells g, Look L, Species sp, double ox, double oy)
        {
            int split = (int)Math.Floor(oy + sp.SuitSplitY);
            for (int y = 0; y < g.H; y++)
                for (int x = 0; x < g.W; x++)
                {
                    int c = g.Px[y * g.W + x];
                    if (c != sp.Body && c != sp.Light && c != sp.Shade && c != sp.Specular) continue;
                    g.Px[y * g.W + x] = y >= split ? Pal.SuitBlue : (c == sp.Light ? Pal.SuitLight : Pal.SuitRed);
                }
            int wx0 = (int)Math.Floor(ox + sp.WebX0), wx1 = (int)Math.Floor(ox + sp.WebX1), wy0 = (int)Math.Floor(oy + sp.WebY0), wy1 = (int)Math.Floor(oy + sp.WebY1);
            Action<int, int> web = (x, y) => { if (g.Get(x, y) == Pal.SuitRed) g.Px[y * g.W + x] = Pal.WebLine; };
            foreach (double cx in sp.WebCols) for (int y = wy0; y <= wy1; y++) web((int)Math.Floor(ox + cx), y);
            foreach (double ry in sp.WebRows) for (int x = wx0; x <= wx1; x++) web(x, (int)Math.Floor(oy + ry));
            double chx = ox + sp.ChestX, chy = oy + sp.ChestY;
            g.Rect(chx, chy, 2, 2, Pal.Eye);
            g.Dot(chx - 1, chy - 1, Pal.Eye); g.Dot(chx + 2, chy - 1, Pal.Eye); g.Dot(chx - 1, chy + 2, Pal.Eye); g.Dot(chx + 2, chy + 2, Pal.Eye);
            foreach (double mx in new[] { ox + sp.MaskLX, ox + sp.MaskRX })
            {
                double my = oy + sp.MaskY;
                g.Rect(mx, my, 4, 4, Pal.Eye);
                if (L.Blink) g.Rect(mx + 1, my + 2, 2, 1, Pal.White);
                else { g.Rect(mx + 1, my + 1, 2, 2, Pal.White); g.Rect(mx + 1, my, 2, 1, Pal.Eye); }
            }
        }

        // movie night, seen from behind: dark body (the renderer adds the screen-coloured rim glow)
        public static void DrawBack(Cells g, Look L)
        {
            Species sp = SpeciesList.Current;
            double ox = OX + L.Wob, oy = OY + 2;
            int rim = L.Rim;
            double bx2 = ox + 22, by2 = oy + 7;
            g.Rect(bx2 - 1, by2 - 3, 8, 11, Pal.Outline); g.Rect(bx2, by2 - 4, 6, 1, Pal.Outline);
            for (int col = 0; col < 6; col++) g.Rect(bx2 + col, by2, 1, 7, col % 2 == 0 ? Pal.BucketRed : Pal.BucketWhite);
            g.Rect(bx2, by2, 6, 1, Pal.Mix(Pal.BucketWhite, rim, 0.5));
            g.Rect(bx2, by2 - 2, 6, 2, Pal.Kernel); g.Rect(bx2 + 1, by2 - 3, 4, 1, Pal.Kernel);
            g.Dot(bx2 + 1, by2 - 1, Pal.KernelShade); g.Dot(bx2 + 4, by2 - 2, Pal.KernelShade);

            sp.DrawBack(g, L, ox, oy);
            double top = oy + sp.HeadTop;
            if (L.Headphones) { g.Rect(ox + 4, top - 2, 14, 1, Pal.Phones); g.Rect(ox + 3, top - 1, 1, 3, Pal.Phones); g.Rect(ox + 18, top - 1, 1, 3, Pal.Phones); }
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
        public static Bitmap IconArt(int size) { return IconArt(size, SpeciesList.Current); }
        public static Bitmap IconArt(int size, Species sp)
        {
            // draw the pet, then centre whatever he covers (tuft included) in a square
            Look L = new Look();
            Cells big = new Cells(CW, CH); DrawFront(big, L, sp);
            int x0 = CW, y0 = CH, x1 = -1, y1 = -1;
            for (int y = 0; y < CH; y++) for (int x = 0; x < CW; x++) if (big.Get(x, y) != 0) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
            int side = Math.Max(x1 - x0 + 1, y1 - y0 + 1) + 2;
            Cells g = new Cells(side, side);
            int dx = (side - (x1 - x0 + 1)) / 2, dy = (side - (y1 - y0 + 1)) / 2;
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) g.Px[(y - y0 + dy) * side + (x - x0 + dx)] = big.Get(x, y);
            Bitmap src = g.ToBitmap();
            Bitmap dst = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics gr = Graphics.FromImage(dst))
            {
                gr.InterpolationMode = size >= side ? System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor : System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                gr.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                gr.DrawImage(src, new Rectangle(0, 0, size, size), new Rectangle(0, 0, side, side), GraphicsUnit.Pixel);
            }
            return dst;
        }
    }
}
