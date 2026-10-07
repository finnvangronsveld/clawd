// Pets are data: each species has a palette, anchor points for props, a few lines, a signature move,
// and draws its own body, face and back. Every behaviour works for every species.
using System;
using System.Collections.Generic;

namespace Clawd
{
    abstract class Species
    {
        public string Id, Name, ShortName, Tagline;     // Name for the picker, ShortName in sentences
        public string Hello = "hi!", SignatureMode = "dance", SignatureLine = "watch this!";
        public string[] Lines = new string[0];       // personality lines he says now and then

        // palette
        public int Body, Light, Shade, Outline = Pal.Outline, Back, Accent, Specular;

        // anchors, in cells relative to the top-left of the 22x16 box (the box shifts down 2 when sitting)
        public double ShoulderLX = 3, ShoulderRX = 19, ShoulderY = 7;         // aiming arm
        public double MaskLX = 5, MaskRX = 13, MaskY = 2;                     // web-slinger mask eyes (4x4)
        public double ChestX = 10, ChestY = 6;                                // web-slinger emblem (2x2 centre)
        public double SuitSplitY = 8;                                         // suit: red above this row, blue below
        public double WebX0 = 5, WebX1 = 16, WebY0 = 1, WebY1 = 7;            // suit: area that gets web lines
        public double[] WebCols = { 7, 11, 14 }, WebRows = { 3, 6 };
        public double MugHoldX = 21, MugHoldY = 1, MugSipX = 17, MugSipY = 4;
        public double HeldX = 8, HeldY = 8, HeldUpY = 5;
        public double BangX = 10, BangY = -12;
        public double HeadTop = 0;                                            // row of the top of his head (hats, headphones)

        public abstract void DrawBody(Cells g, Look L, double ox, double oy, bool low);
        public abstract void DrawFace(Cells g, Look L, double ox, double oy, int e);
        public abstract void DrawBack(Cells g, Look L, double ox, double oy);
    }

    static class SpeciesList
    {
        public static readonly List<Species> All = new List<Species> { new FlippySpecies(), new ClawdSpecies() };
        public static Species Current = All[0];
        public static Species Default { get { return All[0]; } }
        public static Species Get(string id)
        {
            foreach (Species s in All) if (string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase)) return s;
            return Default;
        }
    }

    // ---------------- the classic orange one ----------------
    class ClawdSpecies : Species
    {
        public ClawdSpecies()
        {
            Id = "clawd"; Name = "Clawd (classic)"; ShortName = "Clawd"; Tagline = "the original orange buddy";
            Hello = "hi!"; SignatureMode = "smash"; SignatureLine = "watch this.";
            Body = Pal.C(217, 119, 87); Light = Pal.C(235, 150, 117); Shade = Pal.C(186, 94, 64); Back = Pal.C(88, 44, 33);
            Accent = Body; Specular = Pal.Mix(Light, Pal.White, 0.35);
        }

        public override void DrawBody(Cells g, Look L, double ox, double oy, bool low)
        {
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
                        double shx, shy, hx, hy, c, s; Sprite.Aim(L, ox, oy, out shx, out shy, out hx, out hy, out c, out s);
                        if (L.AimSide < 0) add(ox + 18, oy + 6, 4, 2); else add(ox, oy + 6, 4, 2);
                        for (double d = 0; d <= Sprite.ARM; d += 0.5) add(shx + c * d - 1, shy + s * d - 1, 2, 2);
                        break;
                    }
            }
            int[] legH = new int[4]; int[] legX = { 4, 8, 12, 16 };
            for (int i = 0; i < 4; i++)
            {
                bool lifted = (L.Phase == 1 && (legX[i] == 8 || legX[i] == 16)) || (L.Phase == 3 && (legX[i] == 4 || legX[i] == 12));
                legH[i] = low ? 2 : (lifted ? 3 : 4);
            }
            foreach (double[] q in parts) g.Rect(q[0] - 1, q[1] - 1, (int)q[2] + 2, (int)q[3] + 2, Outline);
            for (int i = 0; i < 4; i++) g.Rect(ox + legX[i], oy + 12 + legH[i], 2, 1, Outline);
            g.Rect(ox + 3, oy + 12, 1, legH[0] + 1, Outline); g.Rect(ox + 18, oy + 12, 1, legH[3] + 1, Outline);
            foreach (double[] q in parts) g.Rect(q[0], q[1], (int)q[2], (int)q[3], Body);
            for (int i = 0; i < 4; i++) { g.Rect(ox + legX[i], oy + 12, 2, legH[i], Body); g.Rect(ox + legX[i] + 1, oy + 12, 1, legH[i], Shade); }
            // shading: lit from the top-left
            g.Rect(bx, oy, bw, 1, Light); g.Rect(bx, oy + 1, 1, 10, Light);
            g.Rect(bx, oy + 11, bw, 1, Shade); g.Rect(bx + bw - 1, oy + 1, 1, 11, Shade);
            g.Rect(bx + 1, oy + 1, 2, 1, Specular);
        }

        public override void DrawFace(Cells g, Look L, double ox, double oy, int e)
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

        public override void DrawBack(Cells g, Look L, double ox, double oy)
        {
            int band = Pal.Mix(Back, L.Rim, 0.4);
            var parts = new List<double[]>();
            parts.Add(new double[] { ox + 4, oy, 14, 12 });
            parts.Add(new double[] { ox, oy + 6, 4, 2 });
            if (L.Arms == "reach") parts.Add(new double[] { ox + 18, oy + 6, 5, 2 });
            else if (L.Arms == "eat") parts.Add(new double[] { ox + 18, oy + 1, 2, 7 });
            else parts.Add(new double[] { ox + 18, oy + 6, 4, 2 });
            int rimLine = Pal.Mix(Outline, L.Rim, 0.8);       // backlit: his outline glows in the screen's colour
            foreach (double[] q in parts) g.Rect(q[0] - 1, q[1] - 1, (int)q[2] + 2, (int)q[3] + 2, rimLine);
            g.Rect(ox + 3, oy + 12, 1, 3, rimLine); g.Rect(ox + 18, oy + 12, 1, 3, rimLine);
            foreach (double[] q in parts) g.Rect(q[0], q[1], (int)q[2], (int)q[3], Back);
            g.Rect(ox + 4, oy + 12, 14, 1, Back);
            foreach (int c in new[] { 4, 8, 12, 16 }) g.Rect(ox + c, oy + 12, 2, 2, Back);
            // screen light wrapping round his edges
            g.Rect(ox + 4, oy, 14, 1, band); g.Rect(ox + 4, oy + 1, 1, 11, band); g.Rect(ox + 17, oy + 1, 1, 11, band);
            g.Rect(ox, oy + 6, 4, 1, band);
            if (L.Arms == "eat") g.Rect(ox + 18, oy + 1, 1, 7, band); else g.Rect(ox + 18, oy + 6, 4, 1, band);
        }
    }
}
