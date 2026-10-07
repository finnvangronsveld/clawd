// Pets are data: each species has a palette, anchor points for props, a few lines, a signature move,
// and draws its own body, face and back. Every behaviour works for every species.
using System;
using System.Collections.Generic;

namespace Flippy
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
        public static readonly List<Species> All = new List<Species> { new FlippySpecies(), new FrogSpecies(), new PhoneSpecies(), new PancakeSpecies(), new ClassicSpecies() };
        public static Species Current = All[0];
        public static Species Default { get { return All[0]; } }
        public static Species Get(string id)
        {
            foreach (Species s in All) if (string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase)) return s;
            return Default;
        }
    }

}
