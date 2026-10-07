// Developer tools. --dump-sprites <dir> renders a fixed set of poses to PNGs (1 px per cell, exact),
// plus the aiming anchors and a few full renderer frames, so art refactors can be pixel-diffed.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;

namespace Clawd
{
    static class DevTools
    {
        public static int DumpSprites(string dir, string species)
        {
            if (species != null) SpeciesList.Current = SpeciesList.Get(species);
            Directory.CreateDirectory(dir);
            foreach (string f in Directory.GetFiles(dir)) File.Delete(f);
            var states = new List<KeyValuePair<string, Action<Look>>>();
            Action<string, Action<Look>> add = (n, a) => states.Add(new KeyValuePair<string, Action<Look>>(n, a));

            foreach (string a in new[] { "out", "typeL", "typeR", "down", "up", "upL", "upR", "wave1", "wave2", "sip", "hold", "fan" })
            { string arms = a; add("arms_" + a, L => L.Arms = arms); }
            add("arms_fan_p1", L => { L.Arms = "fan"; L.Phase = 1; });
            foreach (string e in new[] { "normal", "up", "down", "wide", "blink", "sleep", "happy", "dizzy", "heart", "angry", "sad" })
                for (int eye = -1; eye <= 1; eye++) { string es = e; int ey = eye; add("eye_" + e + "_" + (eye + 1), L => { L.EyeStyle = es; L.Eye = ey; }); }
            foreach (string e in new[] { "normal", "up", "down", "sad" }) { string es = e; add("eyeblink_" + e, L => { L.EyeStyle = es; L.Blink = true; }); }
            foreach (string m in new[] { "o", "smile", "frown", "yawn", "chomp", "chew" }) { string mm = m; add("mouth_" + m, L => L.Mouth = mm); }
            add("blush", L => L.Blush = true);
            add("sit", L => L.Sit = true);
            add("squash", L => L.Squash = true);
            add("walk_p1", L => L.Phase = 1);
            add("walk_p3", L => L.Phase = 3);
            add("wob", L => L.Wob = 1);
            add("headphones", L => L.Headphones = true);
            add("nightcap_sit", L => { L.Nightcap = true; L.Sit = true; L.EyeStyle = "sleep"; });
            add("mug_hold", L => L.Mug = "hold");
            add("mug_sip", L => { L.Mug = "sip"; L.Arms = "sip"; L.EyeStyle = "sleep"; });
            add("laptop_blue", L => { L.Laptop = "open"; L.Glow = "blue"; L.EyeStyle = "down"; L.Arms = "typeL"; });
            add("laptop_red", L => { L.Laptop = "open"; L.Glow = "red"; L.EyeStyle = "angry"; L.Bang = true; });
            add("laptop_held", L => { L.Laptop = "held"; L.LapRow = Sprite.OY - 5; L.Arms = "up"; L.EyeStyle = "angry"; });
            foreach (string food in Food.Kinds) { string fd = food; add("held_" + food, L => { L.Held = fd; L.Arms = "hold"; }); }
            add("held_up_half", L => { L.Held = "cookie"; L.HeldUp = true; L.HeldLeft = 0.5; L.Arms = "hold"; L.Mouth = "chomp"; });
            add("suit", L => L.Suit = true);
            add("suit_blink", L => { L.Suit = true; L.Blink = true; });
            add("suit_up", L => { L.Suit = true; L.Arms = "up"; });
            add("suit_down", L => { L.Suit = true; L.Arms = "down"; });
            add("suit_sit_upR", L => { L.Suit = true; L.Sit = true; L.Arms = "upR"; });
            foreach (double deg in new[] { -110.0, -45, 0, 30 }) { double d = deg; add("aim_r_" + (int)deg, L => { L.Arms = "aim"; L.Gun = true; L.AimSide = 1; L.AimDeg = d; }); }
            foreach (double deg in new[] { 200.0, 180, 150 }) { double d = deg; add("aim_l_" + (int)deg, L => { L.Arms = "aim"; L.Gun = true; L.AimSide = -1; L.AimDeg = d; }); }
            add("aim_sit", L => { L.Arms = "aim"; L.Gun = true; L.AimSide = 1; L.AimDeg = -20; L.Sit = true; });
            foreach (string a in new[] { "rest", "reach", "eat" }) { string arms = a; add("back_" + a, L => { L.Back = true; L.Arms = arms; }); }
            add("back_phones", L => { L.Back = true; L.Headphones = true; });
            add("back_rim_pink", L => { L.Back = true; L.Rim = Pal.C(255, 178, 220); });
            add("back_wob", L => { L.Back = true; L.Wob = 1; });
            add("face_left", L => L.Face = -1);
            add("face_left_walk", L => { L.Face = -1; L.Phase = 1; L.Eye = -1; });
            add("back_face_left", L => { L.Back = true; L.Face = -1; });
            add("tuft_lift", L => { L.TuftLift = 1; L.Squash = true; L.EyeStyle = "happy"; });

            Cells c = new Cells(Sprite.CW, Sprite.CH);
            StringBuilder meta = new StringBuilder();
            foreach (var st in states)
            {
                Look L = new Look(); st.Value(L);
                c.Clear();
                if (L.Back) Sprite.DrawBack(c, L); else Sprite.DrawFront(c, L);
                c.ToBitmap().Save(Path.Combine(dir, st.Key + ".png"), ImageFormat.Png);
                if (L.Gun)
                {
                    double oy = Sprite.OY + ((L.Sit || L.Squash) ? 2 : 0), shx, shy, hx, hy, co, si;
                    Sprite.Aim(L, Sprite.OX + L.Wob, oy, out shx, out shy, out hx, out hy, out co, out si);
                    meta.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}: shoulder {1:0.###},{2:0.###} hand {3:0.###},{4:0.###}", st.Key, shx, shy, hx, hy));
                }
            }
            // a few full renderer frames (gun, rotation, bubble) at scale 4
            Renderer R = new Renderer(); R.Body.Offscreen = true; R.Fx.Offscreen = true;
            Native.RECT mon = new Native.RECT(); mon.Right = 800; mon.Bottom = 400;
            var parts = new List<Particle>(); Bubble bub = new Bubble();
            int i = 0;
            foreach (var st in states)
            {
                if (!st.Key.StartsWith("aim_") && st.Key != "laptop_held" && st.Key != "suit") continue;
                Look L = new Look(); st.Value(L);
                bub.Text = "hi!"; bub.Ticks = 10;
                R.Draw(L, 400, 300, 4, i % 2 == 0 ? 0 : 20, 1, 1, 300, parts, bub, mon, L.Back);
                Bitmap b = new Bitmap(R.Body.Surf.W, R.Body.Surf.H, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(b)) g.DrawImage(R.Body.Surf.Bmp, new Rectangle(0, 0, b.Width, b.Height), 0, 0, b.Width, b.Height, GraphicsUnit.Pixel);
                b.Save(Path.Combine(dir, "render_" + st.Key + ".png"), ImageFormat.Png);
                meta.AppendLine("render_" + st.Key + ": body at " + R.Body.SX + "," + R.Body.SY + " size " + R.Body.Surf.W + "x" + R.Body.Surf.H + "  fx at " + R.Fx.SX + "," + R.Fx.SY + " size " + R.Fx.Surf.W + "x" + R.Fx.Surf.H);
                i++;
            }
            File.WriteAllText(Path.Combine(dir, "anchors.txt"), meta.ToString());
            Console.WriteLine(states.Count + " states -> " + dir);
            return 0;
        }
    }
}
