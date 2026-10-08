// The sandcastle he builds in a corner: pixel art in six stages (a pile, a base, towers, a keep with a door,
// battlements and a flag, a shell). When it's done it stays a few minutes, then crumbles.
// Its own small click-through window, so it stays put while he walks off.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Flippy
{
    class Castle
    {
        public const int CWd = 28, CHt = 22, MaxStage = 6;
        readonly LayeredWindow win = new LayeredWindow(true);
        readonly Cells cells = new Cells(CWd, CHt);
        readonly Random rng = new Random();
        public bool Active;
        public int Stage;           // 0..6
        public double X, Y;         // centre of its base, on the ground (screen px)
        int life, crumble, lastS;
        string lastKey = "";

        static readonly int Sand = Pal.C(236, 206, 140), SandHi = Pal.C(250, 230, 176), SandLo = Pal.C(204, 166, 98), SandLine = Pal.C(128, 94, 52),
                            Door = Pal.C(110, 80, 46), Flag = Pal.C(230, 60, 70), Pole = Pal.C(120, 100, 80), Bucket = Pal.C(220, 70, 60), BucketHi = Pal.C(250, 120, 100),
                            Shell = Pal.C(250, 190, 200), Spade = Pal.C(70, 140, 220);

        public void Begin(double x, double y) { Active = true; Stage = 0; X = x; Y = y; life = 0; crumble = 0; lastKey = ""; }
        public void Finish() { life = 60 * 180; }            // stays three minutes, then crumbles
        public void Hide() { Active = false; win.Hide(); }
        // does something at (x, feetY), about `half` wide, touch the castle? (only once it's more than a pile)
        public bool Hits(double x, double feetY, double half, int S)
        {
            if (!Active || Stage < 2) return false;
            double w = CWd * S / 2.0, top = Y - (Stage >= 5 ? CHt : Stage >= 4 ? 15 : Stage >= 3 ? 12 : 5) * S;
            return Math.Abs(x - X) < w * 0.8 + half && feetY > top + 2 * S;
        }

        public void Tick(int S)
        {
            if (!Active) return;
            if (life > 0 && --life == 0) crumble = 1;
            if (crumble > 0) { crumble++; if (crumble > 70) { Hide(); return; } }
            string key = Stage + "," + crumble / 4 + "," + S;
            if (key == lastKey && S == lastS) return;
            lastKey = key; lastS = S;
            Draw(S);
        }

        void Draw(int S)
        {
            cells.Clear();
            int gy = CHt - 1, cx = CWd / 2;
            Action<int, int, int, int> blockSand = (x, y, w, h) =>
            {
                cells.Rect(x - 1, y - 1, w + 2, h + 2, SandLine);
                cells.Rect(x, y, w, h, Sand); cells.Rect(x, y, w, 1, SandHi); cells.Rect(x + w - 1, y + 1, 1, h - 1, SandLo);
            };
            // the bucket and spade are there from the start
            cells.Rect(0, gy - 5, 6, 6, SandLine); cells.Rect(1, gy - 4, 4, 5, Bucket); cells.Rect(1, gy - 4, 1, 5, BucketHi); cells.Rect(0, gy - 6, 6, 1, SandLine);
            cells.Rect(CWd - 3, gy - 8, 1, 6, Pole); cells.Rect(CWd - 4, gy - 3, 3, 3, Spade);
            if (Stage >= 1 && Stage < 2) { blockSand(cx - 6, gy - 2, 12, 2); cells.Rect(cx - 4, gy - 3, 8, 1, Sand); }       // a pile
            if (Stage >= 2) blockSand(cx - 9, gy - 4, 18, 4);                                                                 // the base
            if (Stage >= 3) { blockSand(cx - 9, gy - 11, 5, 7); blockSand(cx + 4, gy - 11, 5, 7); }                           // two towers
            if (Stage >= 4)
            {
                blockSand(cx - 3, gy - 14, 6, 10);                                                                          // the keep
                cells.Rect(cx - 1, gy - 4, 2, 3, Door); cells.Dot(cx - 1, gy - 5, Door);
                cells.Rect(cx - 8, gy - 9, 1, 2, Door); cells.Rect(cx + 6, gy - 9, 1, 2, Door);                             // windows
            }
            if (Stage >= 5)
            {
                // battlements and a flag
                foreach (int bx in new[] { cx - 9, cx - 7, cx - 5, cx + 4, cx + 6, cx + 8 }) { cells.Rect(bx - 1, gy - 13, 3, 3, SandLine); cells.Rect(bx, gy - 12, 1, 2, SandHi); }
                foreach (int bx in new[] { cx - 3, cx, cx + 2 }) { cells.Rect(bx - 1, gy - 16, 3, 3, SandLine); cells.Rect(bx, gy - 15, 1, 2, SandHi); }
                cells.Rect(cx, gy - 21, 1, 6, Pole); cells.Rect(cx + 1, gy - 21, 4, 1, Flag); cells.Rect(cx + 1, gy - 20, 3, 1, Flag); cells.Rect(cx + 1, gy - 19, 1, 1, Flag);
            }
            if (Stage >= 6) { cells.Rect(cx + 10, gy - 1, 3, 2, Shell); cells.Dot(cx + 11, gy - 2, Shell); cells.Dot(cx - 12, gy, Shell); }
            // crumbling: knock pixels off from the top and let them slump
            if (crumble > 0)
            {
                int gone = crumble / 4;
                for (int y = 0; y < Math.Min(CHt, gone); y++) for (int x = 0; x < CWd; x++) if (cells.Get(x, y) != 0 && rng.Next(3) > 0) cells.Px[y * CWd + x] = 0;
                for (int x = 0; x < CWd; x++) if (rng.Next(4) == 0 && cells.Get(x, gy) == 0) cells.Px[gy * CWd + x] = Sand;
            }
            int pw = CWd * S, ph = CHt * S;
            win.Surf.Ensure(pw, ph); win.Surf.Clear();
            Graphics g = win.Surf.G;
            g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(cells.ToBitmap(), new Rectangle(0, 0, pw, ph), 0, 0, CWd, CHt, GraphicsUnit.Pixel);
            win.Present((int)(X - pw / 2), (int)(Y - ph + S));
        }
        public void KeepOnTop() { if (Active) win.KeepOnTop(); }
    }
}
