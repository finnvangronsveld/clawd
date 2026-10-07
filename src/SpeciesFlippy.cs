// Flippy: a round teal "arrow blob" with a forward-arrow tuft that points the way he's going.
// The body is built as a mask; outline, top-left light and bottom-right shade are derived from it,
// so every arm pose gets clean outlines and lighting for free.
using System;
using System.Collections.Generic;

namespace Flippy
{
    class FlippySpecies : Species
    {
        static readonly int TuftBody = Pal.C(255, 204, 51), TuftLight = Pal.C(255, 234, 150), TuftShade = Pal.C(214, 158, 22);
        static readonly int EyeWhite = Pal.C(252, 252, 250);
        // body outline per row (relative x from..to, inclusive): a chunky rounded blob
        static readonly int[][] rows = {
            new[] { 7, 14 }, new[] { 5, 16 }, new[] { 4, 17 }, new[] { 3, 18 }, new[] { 3, 18 }, new[] { 3, 18 }, new[] { 3, 18 },
            new[] { 3, 18 }, new[] { 3, 18 }, new[] { 3, 18 }, new[] { 4, 17 }, new[] { 5, 16 }, new[] { 7, 14 } };
        // the arrow tuft (5 wide, 7 tall, chunky strokes), pointing right; mirrored when he faces left
        static readonly string[] tuft = { "XX...", "XXX..", ".XXX.", "..XXX", ".XXX.", "XXX..", "XX..." };

        public FlippySpecies()
        {
            Id = "flippy"; Name = "Flippy"; ShortName = "Flippy"; Tagline = "always moving forward";
            Hello = "hi, I'm Flippy!"; SignatureMode = "flip"; SignatureLine = "front flip!";
            Lines = new[] { "onward!", "flip it forward!", "let's gooo", "forward is the only direction", "today feels like a flip day" };
            Body = Pal.C(47, 184, 176); Light = Pal.C(127, 224, 216); Shade = Pal.C(29, 127, 122); Back = Pal.C(20, 56, 58);
            Accent = Pal.C(40, 170, 162); Specular = Pal.C(200, 246, 240); Outline = Pal.C(16, 64, 68);
            MaskLX = 5; MaskRX = 13; MaskY = 3;
            ChestX = 10; ChestY = 8; SuitSplitY = 9;
            WebX0 = 4; WebX1 = 17; WebY0 = 1; WebY1 = 8; WebCols = new double[] { 7, 11, 14 }; WebRows = new double[] { 2, 5 };
            HeldY = 10; HeldUpY = 6; MugSipX = 18; MugSipY = 5; MugHoldY = 2;
            BangY = -16;
        }

        // ---- shared mask helpers ----
        static bool[] NewMask(Cells g) { return new bool[g.W * g.H]; }
        static void Fill(Cells g, bool[] m, double x, double y, int w, int h)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            for (int yy = Math.Max(0, y0); yy < Math.Min(g.H, y0 + h); yy++)
                for (int xx = Math.Max(0, x0); xx < Math.Min(g.W, x0 + w); xx++) m[yy * g.W + xx] = true;
        }
        static bool In(Cells g, bool[] m, int x, int y) { return x >= 0 && y >= 0 && x < g.W && y < g.H && m[y * g.W + x]; }
        // outline (4-neighbour) around the mask, then fill, then edge lighting
        static void Paint(Cells g, bool[] m, int outline, int body, int light, int shade, bool[] keep)
        {
            for (int y = 0; y < g.H; y++)
                for (int x = 0; x < g.W; x++)
                {
                    if (m[y * g.W + x] || (keep != null && keep[y * g.W + x])) continue;
                    if (In(g, m, x - 1, y) || In(g, m, x + 1, y) || In(g, m, x, y - 1) || In(g, m, x, y + 1)) g.Px[y * g.W + x] = outline;
                }
            for (int y = 0; y < g.H; y++)
                for (int x = 0; x < g.W; x++)
                {
                    if (!m[y * g.W + x]) continue;
                    int c = body;
                    if (!In(g, m, x, y - 1) || !In(g, m, x - 1, y)) c = light;
                    if (!In(g, m, x, y + 1) || !In(g, m, x + 1, y)) c = shade;
                    g.Px[y * g.W + x] = c;
                }
        }
        static void AddBody(Cells g, bool[] m, double ox, double oy)
        {
            for (int r = 0; r < rows.Length; r++) Fill(g, m, ox + rows[r][0], oy + r, rows[r][1] - rows[r][0] + 1, 1);
        }
        void AddArms(Cells g, bool[] m, Look L, double ox, double oy)
        {
            Action<double, double, int, int> add = (x, y, w, h) => Fill(g, m, x, y, w, h);
            switch (L.Arms)
            {
                case "out": add(ox, oy + 6, 4, 2); add(ox + 18, oy + 6, 4, 2); break;
                case "typeL": add(ox, oy + 8, 4, 2); add(ox + 18, oy + 6, 4, 2); break;
                case "typeR": add(ox, oy + 6, 4, 2); add(ox + 18, oy + 8, 4, 2); break;
                case "down": add(ox + 1, oy + 8, 3, 2); add(ox + 18, oy + 8, 3, 2); break;
                case "up": add(ox + 1, oy - 3, 2, 10); add(ox + 19, oy - 3, 2, 10); break;
                case "upL": add(ox + 1, oy - 3, 2, 10); add(ox + 18, oy + 6, 4, 2); break;
                case "upR": add(ox, oy + 6, 4, 2); add(ox + 19, oy - 3, 2, 10); break;
                case "wave1": add(ox, oy + 6, 4, 2); add(ox + 19, oy - 3, 2, 10); break;
                case "wave2": add(ox, oy + 6, 4, 2); add(ox + 19, oy + 3, 2, 4); add(ox + 21, oy - 2, 2, 6); break;
                case "sip": add(ox, oy + 6, 4, 2); add(ox + 18, oy + 8, 3, 2); break;
                case "hold": add(ox + 1, oy + 7, 3, 2); add(ox + 18, oy + 7, 3, 2); add(ox + 4, oy + 9, 2, 2); add(ox + 16, oy + 9, 2, 2); break;
                case "fan": add(ox, oy + 6, 4, 2); add(ox + 19, oy + 1 + (L.Phase % 2) * 2, 2, 7); break;
                case "reach": add(ox, oy + 6, 4, 2); add(ox + 18, oy + 6, 5, 2); break;
                case "eat": add(ox, oy + 6, 4, 2); add(ox + 18, oy + 1, 2, 7); break;
                case "rest": add(ox, oy + 6, 4, 2); add(ox + 18, oy + 6, 4, 2); break;
                case "aim":
                    {
                        double shx, shy, hx, hy, c, s; Sprite.Aim(L, ox, oy, out shx, out shy, out hx, out hy, out c, out s);
                        if (L.AimSide < 0) add(ox + 18, oy + 6, 4, 2); else add(ox, oy + 6, 4, 2);
                        for (double d = 0; d <= Sprite.ARM; d += 0.5) add(shx + c * d - 1, shy + s * d - 1, 2, 2);
                        break;
                    }
            }
        }
        static void AddFeet(Cells g, bool[] m, Look L, double ox, double oy, bool low)
        {
            // two stubby feet; walking lifts one at a time
            int lh = low ? 1 : ((L.Phase == 1) ? 1 : 2), rh = low ? 1 : ((L.Phase == 3) ? 1 : 2);
            Fill(g, m, ox + 5, oy + 13, 4, lh); Fill(g, m, ox + 13, oy + 13, 4, rh);
        }
        void DrawTuft(Cells g, Look L, double ox, double oy, int body, int light, int shade, int outline)
        {
            bool[] m = NewMask(g);
            double tx = ox + 9 - (L.Face < 0 ? 1 : 0), ty = oy - 6 - L.TuftLift;
            for (int r = 0; r < tuft.Length; r++)
                for (int c = 0; c < 5; c++)
                {
                    int cc = L.Face < 0 ? 4 - c : c;
                    if (tuft[r][cc] == 'X') Fill(g, m, tx + c, ty + r, 1, 1);
                }
            // don't let the tuft's outline cut into his head
            bool[] keep = new bool[g.W * g.H];
            for (int i = 0; i < keep.Length; i++) { int p = g.Px[i]; keep[i] = p == Body || p == Light || p == Shade || p == Specular || p == Back; }
            Paint(g, m, outline, body, light, shade, keep);
        }

        // ---------------- front ----------------
        public override void DrawBody(Cells g, Look L, double ox, double oy, bool low)
        {
            bool[] m = NewMask(g);
            AddBody(g, m, ox, oy); AddArms(g, m, L, ox, oy); AddFeet(g, m, L, ox, oy, low);
            Paint(g, m, Outline, Body, Light, Shade, null);
            // a soft second highlight band and a specular glint, top-left
            for (int x = 6; x <= 9; x++) if (g.Get((int)(ox + x), (int)(oy + 2)) == Body) g.Dot(ox + x, oy + 2, Pal.Mix(Body, Light, 0.5));
            g.Rect(ox + 6, oy + 1, 2, 1, Specular);
            // belly shade line
            for (int x = 6; x <= 15; x++) if (g.Get((int)(ox + x), (int)(oy + 11)) == Body) g.Dot(ox + x, oy + 11, Pal.Mix(Body, Shade, 0.5));
            DrawTuft(g, L, ox, oy, TuftBody, TuftLight, TuftShade, Outline);
        }

        public override void DrawFace(Cells g, Look L, double ox, double oy, int e)
        {
            string style = L.EyeStyle;
            if (L.Blink && (style == "normal" || style == "up" || style == "down" || style == "sad" || style == "wide")) style = "blink";
            double y = oy + 3;
            foreach (double ex in new[] { ox + 5, ox + 13 })
            {
                bool left = ex < ox + 10;
                int px = (int)ex + 1 + e;              // pupil follows where he's looking
                switch (style)
                {
                    case "normal": case "up": case "down":
                        {
                            g.Rect(ex, y, 4, 4, EyeWhite); g.Dot(ex, y, Body); g.Dot(ex + 3, y, Body);       // rounded corners
                            double py = style == "up" ? y : (style == "down" ? y + 2 : y + 1);
                            g.Rect(px, py, 2, 2, Pal.Eye); g.Dot(px + 1, py, Pal.Shine);
                            break;
                        }
                    case "wide":
                        g.Rect(ex, y - 1, 4, 5, EyeWhite); g.Dot(ex, y - 1, Body); g.Dot(ex + 3, y - 1, Body);
                        g.Dot(px + (e < 0 ? 0 : 1) - (e == 0 ? 0 : 0), y + 1, Pal.Eye); break;
                    case "blink": g.Rect(ex, y + 2, 4, 1, Pal.Eye); break;
                    case "sleep": g.Dot(ex, y + 1, Pal.Eye); g.Rect(ex + 1, y + 2, 2, 1, Pal.Eye); g.Dot(ex + 3, y + 1, Pal.Eye); break;
                    case "happy": g.Dot(ex, y + 2, Pal.Eye); g.Rect(ex + 1, y + 1, 2, 1, Pal.Eye); g.Dot(ex + 3, y + 2, Pal.Eye); break;
                    case "dizzy":
                        g.Dot(ex, y, Pal.Eye); g.Dot(ex + 3, y, Pal.Eye); g.Rect(ex + 1, y + 1, 2, 2, Pal.Eye); g.Dot(ex, y + 3, Pal.Eye); g.Dot(ex + 3, y + 3, Pal.Eye); break;
                    case "heart": g.Glyph("heart", ex - 0.5, y - 1, Pal.Heart); break;
                    case "angry":
                        g.Rect(ex, y + 1, 4, 3, EyeWhite); g.Rect(px, y + 2, 2, 2, Pal.Eye);
                        if (left) { g.Rect(ex, y - 1, 2, 1, Pal.Eye); g.Rect(ex + 2, y, 2, 1, Pal.Eye); }
                        else { g.Rect(ex + 2, y - 1, 2, 1, Pal.Eye); g.Rect(ex, y, 2, 1, Pal.Eye); }
                        break;
                    case "sad":
                        g.Rect(ex, y + 1, 4, 3, EyeWhite); g.Rect(px, y + 2, 2, 2, Pal.Eye); g.Dot(px + 1, y + 2, Pal.Shine);
                        if (left) { g.Rect(ex + 2, y - 1, 2, 1, Pal.Eye); g.Rect(ex, y, 2, 1, Pal.Eye); }
                        else { g.Rect(ex, y - 1, 2, 1, Pal.Eye); g.Rect(ex + 2, y, 2, 1, Pal.Eye); }
                        break;
                }
            }
            if (L.Blush) { g.Rect(ox + 4, oy + 8, 2, 1, Pal.Blush); g.Rect(ox + 16, oy + 8, 2, 1, Pal.Blush); }
            double my = oy + 8;
            switch (L.Mouth)
            {
                case "none": g.Dot(ox + 9, my, Pal.Eye); g.Rect(ox + 10, my + 1, 2, 1, Pal.Eye); g.Dot(ox + 12, my, Pal.Eye); break;   // a little default smile
                case "o": g.Rect(ox + 10, my, 2, 2, Pal.Eye); break;
                case "smile": g.Dot(ox + 8, my, Pal.Eye); g.Rect(ox + 9, my + 1, 4, 1, Pal.Eye); g.Dot(ox + 13, my, Pal.Eye); g.Dot(ox + 10, my + 2, Pal.Tongue); g.Dot(ox + 11, my + 2, Pal.Tongue); break;
                case "frown": g.Dot(ox + 9, my + 1, Pal.Eye); g.Rect(ox + 10, my, 2, 1, Pal.Eye); g.Dot(ox + 12, my + 1, Pal.Eye); break;
                case "yawn": g.Rect(ox + 9, my, 4, 3, Pal.Eye); g.Rect(ox + 10, my + 2, 2, 1, Pal.Tongue); break;
                case "chomp": g.Rect(ox + 8, my, 6, 3, Pal.Eye); g.Rect(ox + 9, my + 2, 4, 1, Pal.Tongue); break;
                case "chew": g.Rect(ox + 9, my + 1, 4, 1, Pal.Eye); break;
            }
        }

        // ---------------- back (movie night) ----------------
        public override void DrawBack(Cells g, Look L, double ox, double oy)
        {
            int band = Pal.Mix(Back, L.Rim, 0.4), rimLine = Pal.Mix(Outline, L.Rim, 0.8);
            bool[] m = NewMask(g);
            AddBody(g, m, ox, oy); AddArms(g, m, L, ox, oy);
            Fill(g, m, ox + 5, oy + 13, 4, 1); Fill(g, m, ox + 13, oy + 13, 4, 1);
            Paint(g, m, rimLine, Back, band, Back, null);
            // light also wraps round his right edge (the screen is in front of him)
            for (int y = 0; y < g.H; y++) for (int x = 0; x < g.W; x++) if (m[y * g.W + x] && !In(g, m, x + 1, y)) g.Px[y * g.W + x] = band;
            DrawTuft(g, L, ox, oy, Pal.Mix(Back, TuftBody, 0.35), Pal.Mix(Back, L.Rim, 0.6), Back, rimLine);
        }
    }
}
