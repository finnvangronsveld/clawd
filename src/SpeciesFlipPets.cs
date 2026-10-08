// Three "flip" pets, after Flipforward: a frog (hops and flips), a flip phone, and a pancake (you flip those).
// Each is a hand-authored pixel template; see TemplateSpecies for what gets added automatically.
using System;
using System.Collections.Generic;

namespace Flippy
{
    // ---------------- Hopper the frog ----------------
    class FrogSpecies : TemplateSpecies
    {
        public FrogSpecies()
        {
            Id = "frog"; Name = "Hopper"; ShortName = "Hopper"; Tagline = "a frog who flips";
            Hello = "ribbit! I'm Hopper"; SignatureMode = "flip"; SignatureLine = "frog flip!";
            Lines = new[] { "ribbit", "hop hop", "anyone seen a fly?", "flip-flop!", "it's not easy being green" };
            Rows = new[] {
                "..bbbbbb....bbbbbb..",
                ".bbbbbbbb..bbbbbbbb.",
                ".bbbbbbbbbbbbbbbbbb.",
                "bbbbbbbfbbbbfbbbbbbb",
                "bbbbbbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbbbbbb",
                "bbbbwwwwwwwwwwwwbbbb",
                "bbbwwwwwwwwwwwwwwbbb",
                "bbbwwwwwwwwwwwwwwbbb",
                ".bbbWWWWWWWWWWWWbbb.",
                "..bbbbbbbbbbbbbbbb..",
            };
            Colors['b'] = Pal.C(104, 196, 84); Colors['l'] = Pal.C(164, 230, 120); Colors['d'] = Pal.C(62, 146, 62);
            Colors['w'] = Pal.C(240, 244, 194); Colors['W'] = Pal.C(212, 222, 152); Colors['f'] = Pal.C(78, 162, 70);
            Outline = Pal.C(26, 72, 40); Accent = Pal.C(70, 168, 72); Back = Pal.C(22, 54, 30);
            TX = 1; TY = 1;
            ArmLX = 1; ArmRX = 20; ArmY = 8; ShoulderLX = 1; ShoulderRX = 21; ShoulderY = 9;
            FootLX = 3; FootRX = 14; FootW = 5; FootY = 13; FootH = 2;
            EyeKind = "big"; EyeLX = 3; EyeRX = 14; EyeY = 1;
            MouthX = 7; MouthW = 8; MouthY = 6;
            BlushLX = 3; BlushRX = 17; BlushY = 7;
            MaskLX = 3; MaskRX = 14; MaskY = 1; ChestX = 10; ChestY = 10; SuitSplitY = 8;
            WebX0 = 2; WebX1 = 19; WebY0 = 2; WebY1 = 7; WebCols = new double[] { 7, 10, 13 }; WebRows = new double[] { 4, 6 };
            HeldY = 9; HeldUpY = 4; MugSipX = 18; MugSipY = 4; MugHoldX = 22; MugHoldY = 3; BangY = -8; HeadTop = 1;
            Build();
        }
    }

    // ---------------- Flip the flip phone ----------------
    class PhoneSpecies : TemplateSpecies
    {
        public PhoneSpecies()
        {
            Id = "phone"; Name = "Flip"; ShortName = "Flip"; Tagline = "a flip phone with feelings";
            Hello = "*ring ring* hi, I'm Flip!"; SignatureMode = "flip"; SignatureLine = "flip open!";
            Lines = new[] { "new message: you're awesome", "beep boop", "I still have snake on me", "can you hear me now?", "1 bar... 2 bars..." };
            Rows = new[] {
                "............aa..",
                "............aa..",
                ".hbbbbbbbbbbbbb.",
                ".bSSSSSSSSSSSSb.",
                ".bSssssssssssSb.",
                ".bSssssssssssSb.",
                ".bSssssssssssSb.",
                ".bSssssssssssSb.",
                ".bSssssssssssSb.",
                ".bSSSSSSSSSSSSb.",
                ".bbbbbbbbbbbbbb.",
                "..cccccccccccc..",
                ".bbbbbbbbbbbbbb.",
                ".bbkkbbkkbbkkbb.",
                ".bbbbbbbbbbbbbb.",
                ".bbkkbbkkbbkkbb.",
                ".bbbbbbbbbbbbbb.",
                "..bbbbbbbbbbbb..",
            };
            Colors['b'] = Pal.C(98, 112, 222); Colors['l'] = Pal.C(150, 166, 248); Colors['d'] = Pal.C(62, 74, 164); Colors['h'] = Pal.C(214, 222, 255);
            Colors['S'] = Pal.C(40, 44, 66); Colors['s'] = Pal.C(178, 226, 152); Colors['c'] = Pal.C(198, 202, 222);
            Colors['k'] = Pal.C(230, 234, 250); Colors['a'] = Pal.C(70, 74, 104);
            Outline = Pal.C(26, 28, 54); Accent = Pal.C(90, 104, 214); Back = Pal.C(26, 30, 64);
            Ink = Pal.C(34, 70, 44);
            TX = 3; TY = -5;
            ArmLX = 4; ArmRX = 17; ArmY = 7; ShoulderLX = 4; ShoulderRX = 18; ShoulderY = 8;
            FootLX = 5; FootRX = 12; FootW = 4; FootY = 13; FootH = 2;
            EyeKind = "bead"; EyeLX = 8; EyeRX = 12; EyeY = -1; EyeBg = Colors['s'];
            MouthX = 9; MouthW = 4; MouthY = 2; MouthRows = 2;
            BlushLX = 6; BlushRX = 14; BlushY = 2;
            MaskLX = 7; MaskRX = 11; MaskY = -1; ChestX = 10; ChestY = 9; SuitSplitY = 6;
            WebX0 = 4; WebX1 = 17; WebY0 = -3; WebY1 = 5; WebCols = new double[] { 5, 16 }; WebRows = new double[] { -3 };
            HeldY = 6; HeldUpY = 0; MugSipX = 16; MugSipY = 0; MugHoldX = 20; MugHoldY = 2; BangY = -14; HeadTop = -3;
            Build();
        }
        // a little red light on the antenna
        protected override void DrawExtras(Cells g, Look L, double ox, double oy, bool backView)
        {
            if (!backView && !L.Suit) g.Dot(ox + TX + 12, oy + TY, Pal.C(255, 90, 90));
        }
    }

    // ---------------- Flapjack the pancake stack ----------------
    class PancakeSpecies : TemplateSpecies
    {
        public PancakeSpecies()
        {
            Id = "pancake"; Name = "Flapjack"; ShortName = "Flapjack"; Tagline = "golden, fluffy, flippable";
            Hello = "hi, I'm Flapjack!"; SignatureMode = "flip"; SignatureLine = "pancake flip!";
            Lines = new[] { "flip me!", "syrup is life", "golden brown and proud", "butter me up", "stacked and happy" };
            Rows = new[] {
                "........yyyy........",
                ".......yyyyyy.......",
                "....SSSYYYYYYSSS....",
                "..SSsSSssssssSSsSS..",
                ".bsssssssssssssssss.",
                ".bbsbbbbbbbbsbbbbsb.",
                "bbbsbbbbbbbbsbbbbsbb",
                "bbbbbbbbbbbbsbbbbbbb",
                "bbbbbbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbbbbbb",
                ".dddddddddddddddddd.",
                "oooooooooooooooooooo",
                "bbbbbbbbbbbbbbbbbbbb",
                ".dddddddddddddddddd.",
            };
            Colors['b'] = Pal.C(236, 172, 92); Colors['l'] = Pal.C(252, 214, 144); Colors['d'] = Pal.C(200, 130, 60);
            Colors['s'] = Pal.C(186, 92, 30); Colors['S'] = Pal.C(232, 146, 64); Colors['y'] = Pal.C(255, 242, 164); Colors['Y'] = Pal.C(242, 210, 102);
            Outline = Pal.C(96, 50, 22); Accent = Pal.C(222, 146, 62); Back = Pal.C(70, 40, 18);
            TX = 1; TY = -1;
            ArmLX = 1; ArmRX = 20; ArmY = 6; ShoulderLX = 1; ShoulderRX = 21; ShoulderY = 7;
            FootLX = 5; FootRX = 13; FootW = 4; FootY = 13; FootH = 2;
            EyeKind = "bead"; EyeLX = 6; EyeRX = 13; EyeY = 5;
            MouthX = 9; MouthW = 4; MouthY = 8;
            BlushLX = 3; BlushRX = 17; BlushY = 8;
            MaskLX = 5; MaskRX = 12; MaskY = 4; ChestX = 10; ChestY = 11; SuitSplitY = 10;
            WebX0 = 1; WebX1 = 20; WebY0 = 5; WebY1 = 9; WebCols = new double[] { 4, 10, 16 }; WebRows = new double[] { 7 };
            HeldY = 10; HeldUpY = 6; MugSipX = 18; MugSipY = 6; MugHoldX = 22; MugHoldY = 4; BangY = -9; HeadTop = -1;
            Build();
        }
    }
}
