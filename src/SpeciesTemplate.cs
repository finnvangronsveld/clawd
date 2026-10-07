// A pet drawn from a hand-authored pixel template. The template gives the body (any colours you like, one
// char per pixel); the outline, the top-left light and the bottom-right shade on the main colour are added
// automatically. Arms, feet and the face are drawn procedurally at anchor points, so every pose works.
using System;
using System.Collections.Generic;

namespace Flippy
{
    class TemplateSpecies : Species
    {
        // ---- the art ----
        public string[] Rows;                                            // '.' = empty
        public Dictionary<char, int> Colors = new Dictionary<char, int>();
        public int TX, TY;                                               // template (0,0) in box cells
        public string AutoShade = "b";                                   // chars that get edge light/shade
        // ---- limbs ----
        public double ArmLX, ArmRX, ArmY;                                // body's left/right edge column, shoulder row
        public double FootLX, FootRX, FootY = 13; public int FootW = 4, FootH = 2;
        public bool Feet = true;
        public int Limb, LimbLight, LimbShade;                           // arm/foot colours (default: body colours)
        // ---- face ----
        public string EyeKind = "big";                                   // "big" (white + pupil) or "bead" (black, with shine)
        public double EyeLX, EyeRX, EyeY;
        public int EyeBg = -1;                                           // colour round the eyes (for rounded corners)
        public int Ink = Pal.Eye, EyeWhite = Pal.C(252, 252, 250);
        public double MouthX, MouthY; public int MouthW = 4;              // MouthX = left of the mouth
        public bool DefaultSmile = true;
        public double BlushLX, BlushRX, BlushY;

        protected void Build()
        {
            Body = Colors['b']; Light = Colors.ContainsKey('l') ? Colors['l'] : Pal.Mix(Body, Pal.White, 0.4);
            Shade = Colors.ContainsKey('d') ? Colors['d'] : Pal.Mix(Body, Pal.Eye, 0.3);
            Specular = Colors.ContainsKey('h') ? Colors['h'] : Pal.Mix(Light, Pal.White, 0.5);
            if (Limb == 0) { Limb = Body; LimbLight = Light; LimbShade = Shade; }
            if (EyeBg < 0) EyeBg = Body;
            if (Accent == 0) Accent = Body;
            if (Back == 0) Back = Pal.Mix(Shade, Pal.Eye, 0.7);
        }

        int ColorOf(char ch) { int c; return Colors.TryGetValue(ch, out c) ? c : Body; }
        static bool In(bool[] m, Cells g, int x, int y) { return x >= 0 && y >= 0 && x < g.W && y < g.H && m[y * g.W + x]; }

        // template -> canvas, with auto outline and edge shading. backView: everything dark, rim-lit.
        void DrawTemplate(Cells g, double ox, double oy, bool backView, int rimLine, int band, bool[] bodyMask)
        {
            int x0 = (int)Math.Floor(ox + TX), y0 = (int)Math.Floor(oy + TY);
            for (int r = 0; r < Rows.Length; r++)
                for (int c = 0; c < Rows[r].Length; c++)
                {
                    int x = x0 + c, y = y0 + r;
                    if (Rows[r][c] != '.' && x >= 0 && y >= 0 && x < g.W && y < g.H) bodyMask[y * g.W + x] = true;
                }
            Func<int, int, char> at = (x, y) =>
            {
                int r = y - y0, c = x - x0;
                if (r < 0 || r >= Rows.Length || c < 0 || c >= Rows[r].Length) return '.';
                return Rows[r][c];
            };
            for (int y = Math.Max(0, y0 - 1); y < Math.Min(g.H, y0 + Rows.Length + 1); y++)
                for (int x = Math.Max(0, x0 - 1); x < Math.Min(g.W, x0 + 30); x++)
                {
                    char ch = at(x, y);
                    if (ch == '.')
                    {
                        if (In(bodyMask, g, x - 1, y) || In(bodyMask, g, x + 1, y) || In(bodyMask, g, x, y - 1) || In(bodyMask, g, x, y + 1))
                            g.Px[y * g.W + x] = backView ? rimLine : Outline;
                        continue;
                    }
                    if (backView)
                    {
                        bool edge = at(x, y - 1) == '.' || at(x - 1, y) == '.' || at(x + 1, y) == '.';
                        g.Px[y * g.W + x] = ch == 'o' ? Pal.Mix(Back, rimLine, 0.35) : (edge ? band : Back);
                        continue;
                    }
                    if (ch == 'o') { g.Px[y * g.W + x] = Outline; continue; }
                    int col = ColorOf(ch);
                    if (AutoShade.IndexOf(ch) >= 0)
                    {
                        char up = at(x, y - 1), lf = at(x - 1, y), dn = at(x, y + 1), rt = at(x + 1, y);
                        if (up == '.' || up == 'o' || lf == '.' || lf == 'o') col = Light;
                        if (dn == '.' || dn == 'o' || rt == '.' || rt == 'o') col = Shade;
                    }
                    g.Px[y * g.W + x] = col;
                }
        }

        // arms + feet: built as a mask, outlined (never cutting into the body), lit at the edges
        void DrawLimbs(Cells g, Look L, double ox, double oy, bool low, bool backView, int rimLine, int band, bool[] bodyMask)
        {
            bool[] m = new bool[g.W * g.H];
            bool[] front = new bool[g.W * g.H];                   // limb parts that sit in front of the body (holding things)
            Action<double, double, int, int, bool> add = (x, y, w, h, fr) =>
            {
                int xa = (int)Math.Floor(x), ya = (int)Math.Floor(y);
                for (int yy = Math.Max(0, ya); yy < Math.Min(g.H, ya + h); yy++)
                    for (int xx = Math.Max(0, xa); xx < Math.Min(g.W, xa + w); xx++) { m[yy * g.W + xx] = true; if (fr) front[yy * g.W + xx] = true; }
            };
            double lx = ox + ArmLX, rx = ox + ArmRX, ay = oy + ArmY;
            switch (L.Arms)
            {
                case "out": case "rest": add(lx - 3, ay, 4, 2, false); add(rx, ay, 4, 2, false); break;
                case "typeL": add(lx - 3, ay + 2, 4, 2, false); add(rx, ay, 4, 2, false); break;
                case "typeR": add(lx - 3, ay, 4, 2, false); add(rx, ay + 2, 4, 2, false); break;
                case "down": add(lx - 2, ay + 2, 3, 2, false); add(rx, ay + 2, 3, 2, false); break;
                case "up": add(lx - 2, ay - 9, 2, 10, false); add(rx + 1, ay - 9, 2, 10, false); break;
                case "upL": add(lx - 2, ay - 9, 2, 10, false); add(rx, ay, 4, 2, false); break;
                case "upR": case "wave1": add(lx - 3, ay, 4, 2, false); add(rx + 1, ay - 9, 2, 10, false); break;
                case "wave2": add(lx - 3, ay, 4, 2, false); add(rx + 1, ay - 3, 2, 4, false); add(rx + 3, ay - 8, 2, 6, false); break;
                case "sip": add(lx - 3, ay, 4, 2, false); add(rx - 1, ay + 2, 3, 2, false); break;
                case "hold": add(lx - 2, ay + 1, 3, 2, false); add(rx, ay + 1, 3, 2, false); add(lx + 1, ay + 3, 2, 2, true); add(rx - 2, ay + 3, 2, 2, true); break;
                case "fan": add(lx - 3, ay, 4, 2, false); add(rx + 1, ay - 5 + (L.Phase % 2) * 2, 2, 7, false); break;
                case "reach": add(lx - 3, ay, 4, 2, false); add(rx, ay, 5, 2, false); break;
                case "eat": add(lx - 3, ay, 4, 2, false); add(rx + 1, ay - 5, 2, 7, false); break;
                case "aim":
                    {
                        double shx, shy, hx, hy, c, s; Sprite.Aim(L, ox, oy, out shx, out shy, out hx, out hy, out c, out s);
                        if (L.AimSide < 0) add(rx, ay, 4, 2, false); else add(lx - 3, ay, 4, 2, false);
                        for (double d = 0; d <= Sprite.ARM; d += 0.5) add(shx + c * d - 1, shy + s * d - 1, 2, 2, false);
                        break;
                    }
            }
            if (Feet)
            {
                int lh = low ? 1 : (L.Phase == 1 ? FootH - 1 : FootH), rh = low ? 1 : (L.Phase == 3 ? FootH - 1 : FootH);
                add(ox + FootLX, oy + FootY + (FootH - lh), FootW, Math.Max(1, lh), false);
                add(ox + FootRX, oy + FootY + (FootH - rh), FootW, Math.Max(1, rh), false);
            }
            int outline = backView ? rimLine : Outline;
            for (int y = 0; y < g.H; y++)
                for (int x = 0; x < g.W; x++)
                {
                    int i = y * g.W + x;
                    if (m[i] || bodyMask[i]) continue;
                    if (In(m, g, x - 1, y) || In(m, g, x + 1, y) || In(m, g, x, y - 1) || In(m, g, x, y + 1)) g.Px[i] = outline;
                }
            for (int y = 0; y < g.H; y++)
                for (int x = 0; x < g.W; x++)
                {
                    int i = y * g.W + x;
                    if (!m[i]) continue;
                    if (bodyMask[i] && !front[i] && !IsBodyEdge(g, bodyMask, x, y)) continue;   // tucked behind the body
                    int c = backView ? Back : Limb;
                    bool topLeft = !In(m, g, x, y - 1) || !In(m, g, x - 1, y), botRight = !In(m, g, x, y + 1) || !In(m, g, x + 1, y);
                    if (backView) { if (topLeft || botRight) c = band; }
                    else { if (topLeft) c = LimbLight; if (botRight) c = LimbShade; }
                    g.Px[i] = c;
                }
        }
        // the body's outer edge pixels may be covered by a limb so it joins seamlessly
        static bool IsBodyEdge(Cells g, bool[] body, int x, int y)
        {
            return !In(body, g, x - 1, y) || !In(body, g, x + 1, y) || !In(body, g, x, y - 1) || !In(body, g, x, y + 1);
        }

        public override void DrawBody(Cells g, Look L, double ox, double oy, bool low)
        {
            bool[] body = new bool[g.W * g.H];
            DrawTemplate(g, ox, oy, false, 0, 0, body);
            DrawLimbs(g, L, ox, oy, low, false, 0, 0, body);
            DrawExtras(g, L, ox, oy, false);
        }
        public override void DrawBack(Cells g, Look L, double ox, double oy)
        {
            int rimLine = Pal.Mix(Outline, L.Rim, 0.8), band = Pal.Mix(Back, L.Rim, 0.45);
            bool[] body = new bool[g.W * g.H];
            DrawTemplate(g, ox, oy, true, rimLine, band, body);
            DrawLimbs(g, L, ox, oy, true, true, rimLine, band, body);
            DrawExtras(g, L, ox, oy, true);
        }
        // species can add things that move (a tuft, an antenna light...)
        protected virtual void DrawExtras(Cells g, Look L, double ox, double oy, bool backView) { }

        // ---------------- face ----------------
        public override void DrawFace(Cells g, Look L, double ox, double oy, int e)
        {
            string style = L.EyeStyle;
            if (L.Blink && (style == "normal" || style == "up" || style == "down" || style == "sad" || style == "wide")) style = "blink";
            foreach (double exr in new[] { EyeLX, EyeRX })
            {
                double ex = ox + exr, y = oy + EyeY;
                bool left = exr == EyeLX;
                if (EyeKind == "big") BigEye(g, style, ex, y, e, left);
                else BeadEye(g, style, ex, y, e, left);
            }
            if (L.Blush) { g.Rect(ox + BlushLX, oy + BlushY, 2, 1, Pal.Blush); g.Rect(ox + BlushRX, oy + BlushY, 2, 1, Pal.Blush); }
            double mx = ox + MouthX, my = oy + MouthY; int w = MouthW;
            switch (L.Mouth)
            {
                case "none":
                    if (DefaultSmile) { g.Dot(mx, my, Ink); g.Rect(mx + 1, my + 1, w - 2, 1, Ink); g.Dot(mx + w - 1, my, Ink); }
                    break;
                case "smile": g.Dot(mx - 1, my, Ink); g.Rect(mx, my + 1, w, 1, Ink); g.Dot(mx + w, my, Ink); g.Rect(mx + w / 2 - 1, my + 2, 2, 1, Pal.Tongue); break;
                case "o": g.Rect(mx + w / 2 - 1, my, 2, 2, Ink); break;
                case "frown": g.Dot(mx, my + 1, Ink); g.Rect(mx + 1, my, w - 2, 1, Ink); g.Dot(mx + w - 1, my + 1, Ink); break;
                case "yawn": g.Rect(mx, my, w, 3, Ink); g.Rect(mx + 1, my + 2, w - 2, 1, Pal.Tongue); break;
                case "chomp": g.Rect(mx - 1, my, w + 2, 3, Ink); g.Rect(mx, my + 2, w, 1, Pal.Tongue); break;
                case "chew": g.Rect(mx, my + 1, w, 1, Ink); break;
            }
        }
        void BigEye(Cells g, string style, double ex, double y, int e, bool left)
        {
            int px = (int)ex + 1 + e;
            switch (style)
            {
                case "normal": case "up": case "down":
                    {
                        g.Rect(ex, y, 4, 4, EyeWhite); g.Dot(ex, y, EyeBg); g.Dot(ex + 3, y, EyeBg); g.Dot(ex, y + 3, EyeBg); g.Dot(ex + 3, y + 3, EyeBg);
                        double py = style == "up" ? y : (style == "down" ? y + 2 : y + 1);
                        g.Rect(px, py, 2, 2, Ink); g.Dot(px + 1, py, Pal.Shine);
                        break;
                    }
                case "wide": g.Rect(ex, y - 1, 4, 5, EyeWhite); g.Dot(ex, y - 1, EyeBg); g.Dot(ex + 3, y - 1, EyeBg); g.Dot(ex + 1 + (e > 0 ? 1 : 0), y + 1, Ink); break;
                case "blink": g.Rect(ex, y + 2, 4, 1, Ink); break;
                case "sleep": g.Dot(ex, y + 1, Ink); g.Rect(ex + 1, y + 2, 2, 1, Ink); g.Dot(ex + 3, y + 1, Ink); break;
                case "happy": g.Dot(ex, y + 2, Ink); g.Rect(ex + 1, y + 1, 2, 1, Ink); g.Dot(ex + 3, y + 2, Ink); break;
                case "dizzy": g.Dot(ex, y, Ink); g.Dot(ex + 3, y, Ink); g.Rect(ex + 1, y + 1, 2, 2, Ink); g.Dot(ex, y + 3, Ink); g.Dot(ex + 3, y + 3, Ink); break;
                case "heart": g.Glyph("heart", ex - 0.5, y - 1, Pal.Heart); break;
                case "angry":
                    g.Rect(ex, y + 1, 4, 3, EyeWhite); g.Rect(px, y + 2, 2, 2, Ink);
                    if (left) { g.Rect(ex, y - 1, 2, 1, Ink); g.Rect(ex + 2, y, 2, 1, Ink); } else { g.Rect(ex + 2, y - 1, 2, 1, Ink); g.Rect(ex, y, 2, 1, Ink); }
                    break;
                case "sad":
                    g.Rect(ex, y + 1, 4, 3, EyeWhite); g.Rect(px, y + 2, 2, 2, Ink); g.Dot(px + 1, y + 2, Pal.Shine);
                    if (left) { g.Rect(ex + 2, y - 1, 2, 1, Ink); g.Rect(ex, y, 2, 1, Ink); } else { g.Rect(ex, y - 1, 2, 1, Ink); g.Rect(ex + 2, y, 2, 1, Ink); }
                    break;
            }
        }
        // small shiny black eyes, 2x3 (drawn on body colour, or on a screen)
        void BeadEye(Cells g, string style, double ex, double y, int e, bool left)
        {
            double x = ex + (e < 0 ? 0 : (e > 0 ? 1 : 0.5));
            x = Math.Floor(x);
            switch (style)
            {
                case "normal": g.Rect(x, y, 2, 3, Ink); g.Dot(x + 1, y, Pal.Shine); break;
                case "up": g.Rect(x, y - 1, 2, 3, Ink); g.Dot(x + 1, y - 1, Pal.Shine); break;
                case "down": g.Rect(x, y + 1, 2, 3, Ink); break;
                case "wide": g.Rect(x - 1, y - 1, 3, 4, Ink); g.Rect(x, y - 1, 1, 2, Pal.Shine); break;
                case "blink": g.Rect(x, y + 2, 2, 1, Ink); break;
                case "sleep": g.Dot(x - 1, y + 1, Ink); g.Rect(x, y + 2, 2, 1, Ink); g.Dot(x + 2, y + 1, Ink); break;
                case "happy": g.Dot(x - 1, y + 2, Ink); g.Rect(x, y + 1, 2, 1, Ink); g.Dot(x + 2, y + 2, Ink); break;
                case "dizzy": g.Dot(x - 1, y, Ink); g.Dot(x + 2, y, Ink); g.Rect(x, y + 1, 2, 1, Ink); g.Dot(x - 1, y + 2, Ink); g.Dot(x + 2, y + 2, Ink); break;
                case "heart": g.Glyph("heart", x - 1.5, y - 1, Pal.Heart); break;
                case "angry":
                    g.Rect(x, y + 1, 2, 2, Ink);
                    if (left) { g.Dot(x - 1, y - 1, Ink); g.Rect(x, y, 2, 1, Ink); } else { g.Dot(x + 2, y - 1, Ink); g.Rect(x, y, 2, 1, Ink); }
                    break;
                case "sad":
                    g.Rect(x, y + 1, 2, 2, Ink); g.Dot(x + 1, y + 1, Pal.Shine);
                    if (left) { g.Dot(x + 2, y - 1, Ink); g.Rect(x, y, 2, 1, Ink); } else { g.Dot(x - 1, y - 1, Ink); g.Rect(x, y, 2, 1, Ink); }
                    break;
            }
        }
    }
}
