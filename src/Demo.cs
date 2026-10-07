// Off-screen demo for the README: a scripted show on a tiny fake desktop, drawn by the real renderer.
//   Clawd.exe --render-frames <dir>      then   python tools/make_gif.py <dir> docs/clawd.gif
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Clawd
{
    static class Demo
    {
        const int SW = 800, SH = 270, TASK = 30, S = 4;
        static readonly Rectangle Win = new Rectangle(500, 120, 270, 112);     // the app window he climbs / watches
        static Renderer R; static Look L; static List<Particle> parts; static Bubble bub;
        static double X, Y, rot, sqx = 1, sqy = 1; static int tick, frame; static string dir;
        static Bitmap scene; static Graphics sg; static Random rng = new Random(7);
        static Native.RECT mon;
        static string food; static double foodX, foodY, foodVY, foodLeft = 1;
        static int screenCol = -1; static bool paused;
        static int squashT;

        static double K { get { return S / 3.0; } }
        static double CX(double cx) { return X + (cx - (Sprite.OX + 11)) * S; }
        static double CY(double cy) { return Y + (cy - (Sprite.OY + 16)) * S; }
        static double Ground { get { return SH - TASK; } }

        public static int Render(string outDir)
        {
            dir = outDir; Directory.CreateDirectory(dir);
            foreach (string f in Directory.GetFiles(dir, "f*.png")) File.Delete(f);
            R = new Renderer(); R.Body.Offscreen = true; R.Fx.Offscreen = true;
            L = new Look(); parts = new List<Particle>(); bub = new Bubble();
            mon = new Native.RECT(); mon.Right = SW; mon.Bottom = SH;
            scene = new Bitmap(SW, SH, PixelFormat.Format32bppArgb); sg = Graphics.FromImage(scene);
            X = -40; Y = Ground;

            // walk in, wave
            Run(170, t => { X += 0.85 * K * 1.2; L.Eye = 1; L.Phase = (tick / 10) % 4; });
            Say("hi!", 90);
            Run(100, t => { L.EyeStyle = "happy"; L.Mouth = "smile"; L.Arms = (t / 12) % 2 == 0 ? "wave1" : "wave2"; });
            // types along, thinks when you pause
            bool hand = false;
            Run(110, t => { Typing(t, ref hand); });
            Run(110, t => { L.Laptop = "open"; L.Arms = "down"; L.Eye = 1; L.EyeStyle = "up"; bub.Text = "Clauding..."; bub.Ticks = 2; bub.Spinner = true; });
            Run(50, t => { Typing(t, ref hand); });
            // a snack drops in
            food = "cookie"; foodX = X + 110; foodY = 0; foodVY = 0;
            Say("ooh, food!", 60);
            Run(150, t =>
            {
                if (foodY < Ground) { foodVY += 0.22 * K; foodY = Math.Min(Ground, foodY + foodVY); }
                if (t > 40 && Math.Abs(foodX - X) > 6) { X += 1.5 * K; L.Eye = 1; L.Phase = (tick / 5) % 4; L.Mouth = "o"; }
            });
            food = null;
            Run(170, t =>
            {
                L.Held = "cookie"; L.Arms = "hold"; L.EyeStyle = "happy";
                int bite = t / 45; L.HeldLeft = 1 - Math.Min(3, bite) / 3.0;
                bool chomp = t % 45 < 12; L.HeldUp = chomp; L.Mouth = chomp ? "chomp" : "chew";
                if (t % 45 == 12) for (int i = 0; i < 4; i++) Bit(Sprite.OX + 11, Sprite.OY + 8, (rng.NextDouble() - 0.5) * 0.4, -0.3, 40, Pal.C(214, 160, 92), 1, 0.03, true);
                if (t >= 135) { L.Held = "none"; L.Mouth = "smile"; L.Blush = true; }
                if (t == 136) { Say("yum!", 60); for (int i = 0; i < 3; i++) Float("heart", Sprite.OX + 6 + rng.Next(10), Sprite.OY - 2, 0, -0.12, 60, Pal.Heart); }
            });
            // dance
            Run(130, t =>
            {
                L.EyeStyle = "happy"; L.Mouth = "smile"; int beat = t / 16; L.Sit = beat % 2 == 1; L.Arms = beat % 2 == 0 ? "upL" : "upR";
                if (t % 28 == 1) Float("note", Sprite.OX + 9 + rng.Next(-10, 11), Sprite.OY - 4, 0, -0.15, 80, Pal.Notes[rng.Next(4)]);
            });
            // gun: aims all over, then a 360 no-scope
            Say("hold still...", 70);
            Run(110, t => { L.Gun = true; L.Arms = "aim"; L.AimSide = 1; L.Eye = 1; L.EyeStyle = "angry"; L.AimDeg = -100 + t * 1.15; L.Flash = t % 30 < 5 && t > 20; L.Recoil = L.Flash; });
            Say("360 no-scope. watch.", 70);
            double jy = 0, jv = 0;
            Run(150, t =>
            {
                L.Gun = true; L.Arms = "aim"; L.AimSide = 1; L.Eye = 1; L.AimDeg = -25;
                if (t < 30) { L.Sit = true; L.EyeStyle = "happy"; }
                if (t == 30) jv = 7.6 * K;
                if (t >= 30) { jy += jv; jv -= 0.21 * K; if (jy < 0) { jy = 0; jv = 0; if (t < 120) squashT = 14; } }
                rot = (t >= 32 && t < 68) ? (t - 32) * 10 : 0;
                if (t >= 68) L.EyeStyle = "angry";
                L.Flash = t >= 68 && t < 74; L.Recoil = L.Flash;
                if (t == 100) { Say("TRICKSHOT!!", 60); for (int i = 0; i < 6; i++) Float("star", Sprite.OX + 2 + rng.Next(18), Sprite.OY - 2 - rng.Next(6), 0, -0.15, 50, Pal.Spark); }
                if (t > 100) { L.EyeStyle = "happy"; L.Mouth = "smile"; }
                Y = Ground - jy;
            });
            rot = 0; Y = Ground;
            // climbs the window
            Say("ooh, a window!", 60);
            double target = Win.Left + 70;
            Run(200, t => { if (X < target) { X += 1.4 * K; L.Eye = 1; L.EyeStyle = "up"; L.Phase = (tick / 6) % 4; } });
            double vy = -(Math.Sqrt(2 * 0.21 * K * (Ground - Win.Top)) + 1.0 * K);
            Run(24, t => { L.Sit = true; L.EyeStyle = "up"; L.Arms = "down"; });
            bool landed = false;
            Run(110, t =>
            {
                if (!landed) { vy += 0.21 * K; Y += vy; L.Arms = vy < 0 ? "up" : "out"; L.EyeStyle = vy < 0 ? "up" : "normal"; if (vy > 0 && Y >= Win.Top) { Y = Win.Top; landed = true; squashT = 14; } sqy = vy < 0 ? 1.1 : 1; }
                else if (t > 60) { X += 0.85 * K; L.Phase = (tick / 10) % 4; L.Eye = 1; }
            });
            Say("made it!", 60);
            Run(40, t => { L.EyeStyle = "happy"; L.Arms = "upR"; });
            // hop down and watch a video on the window
            vy = -3.6 * K; double vx = 1.2 * K;
            Run(80, t => { if (Y < Ground || vy < 0) { vy += 0.21 * K; Y = Math.Min(Ground, Y + vy); X += vx; } else { Y = Ground; } });
            Y = Ground; screenCol = 0;
            int[] rims = { Pal.C(165, 212, 255), Pal.C(255, 212, 165), Pal.C(185, 255, 215), Pal.C(255, 178, 220) };
            Say("popcorn time!", 80);
            Run(230, t =>
            {
                L.Back = true; L.Sit = true; screenCol = (t / 50) % 4; L.Rim = rims[screenCol];
                int c = t % 200; L.Arms = c >= 120 && c < 140 ? "reach" : (c >= 140 && c < 176 ? "eat" : "rest");
                if (t == 150) Say("plot twist!", 70);
            });
            paused = true;
            Say("hey! I was watching!", 120);
            Run(150, t => { L.EyeStyle = "angry"; L.Mouth = "o"; L.Bang = t < 60 && t % 16 < 10; if (t < 100) { L.Arms = (t / 8) % 2 == 0 ? "up" : "down"; L.Phase = (t / 8) % 2 == 0 ? 1 : 3; L.Wob = t % 8 < 4 ? 1 : -1; } else L.Arms = "down"; });
            paused = false;
            Say("finally!", 60);
            Run(60, t => { L.Back = true; L.Sit = true; L.Rim = rims[1]; screenCol = 1; L.Arms = "rest"; });
            screenCol = -1;
            // the laptop smash (from the frustrated part)
            Run(470, t => Smash(100 + t / 2, t % 2 == 0));
            // nap
            Run(150, t => { L.Sit = true; L.EyeStyle = "sleep"; L.Arms = "down"; if (t % 70 == 1) Float("z", Sprite.OX + 17, Sprite.OY - 2, 0.03, -0.09, 110, Pal.Blue); });
            Console.WriteLine(frame + " frames -> " + dir);
            return 0;
        }

        static void Typing(int t, ref bool hand)
        {
            L.Laptop = "open"; L.EyeStyle = "down";
            if (t % 8 == 0) { hand = !hand; if (rng.Next(2) == 0) Bit(rng.Next(2) == 0 ? Sprite.OX + 2 : Sprite.OX + 19, Sprite.OY + 5, (rng.NextDouble() - 0.5) * 0.15, -0.15, 26, Pal.Blue, 1, 0, false); }
            L.Arms = t % 8 < 4 ? (hand ? "typeL" : "typeR") : "down";
        }

        static void Smash(int t, bool step)
        {
            if (t < 125) { L.Laptop = "open"; L.Glow = "blue"; L.EyeStyle = "down"; L.Arms = (t / 3) % 2 == 0 ? "typeL" : "typeR"; }
            else if (t < 160) { L.Laptop = "open"; L.Glow = "red"; L.EyeStyle = "angry"; L.Arms = t % 2 == 0 ? "typeL" : "typeR"; L.Bang = t % 8 < 5; }
            else if (t < 180) { L.Laptop = "open"; L.Glow = "red"; L.EyeStyle = "angry"; L.Bang = true; }
            else if (t < 194) { L.Laptop = "held"; L.Arms = "up"; L.EyeStyle = "angry"; L.LapRow = (Sprite.OY + 9) - (t - 180) / 14.0 * 14; }
            else if (t < 212) { L.Laptop = "held"; L.Arms = "up"; L.EyeStyle = "angry"; L.LapRow = t % 4 < 2 ? Sprite.OY - 5 : Sprite.OY - 6; }
            else if (t < 216) { L.Laptop = "held"; L.Arms = "down"; L.EyeStyle = "angry"; L.LapRow = (Sprite.OY - 6) + (t - 212) / 4.0 * 18; }
            else if (t == 216 && step)
            {
                squashT = 10;
                int[] pool = { Pal.Silver, Pal.Silver, Pal.Dark, Pal.Blue, Pal.Spark, Pal.Spark, Pal.White };
                for (int i = 0; i < 34; i++)
                {
                    int col = pool[rng.Next(pool.Length)]; bool spark = col == Pal.Spark;
                    Bit(Sprite.OX + 11 + (rng.NextDouble() - 0.5) * 10, Sprite.OY + 13, (rng.NextDouble() - 0.5) * 1.8, -0.5 - rng.NextDouble() * 1.3, spark ? 50 : 300, col, spark ? 1 : 2, 0.07, !spark);
                }
            }
            else if (t < 250) { L.Arms = "down"; L.EyeStyle = "angry"; }
            else { L.EyeStyle = "happy"; L.Mouth = "smile"; if (t == 250 && step) Say("fixed it.", 90); }
        }

        static void Say(string s, int ticks) { bub.Text = s; bub.Ticks = ticks; bub.Spinner = false; }
        static void Float(string glyph, double cx, double cy, double vx, double vy, int life, int color)
        {
            Particle p = new Particle(); p.Glyph = glyph; p.X = CX(cx); p.Y = CY(cy); p.VX = vx * S; p.VY = vy * S; p.Life = p.Max = life; p.Color = color; p.Wobble = 1; p.Seed = rng.Next(100);
            parts.Add(p);
        }
        static void Bit(double cx, double cy, double vx, double vy, int life, int color, double size, double grav, bool bounce)
        {
            Particle p = new Particle(); p.X = CX(cx); p.Y = CY(cy); p.VX = vx * S; p.VY = vy * S; p.Life = p.Max = life; p.Color = color; p.Size = size; p.Gravity = grav * S; p.Bounce = bounce;
            parts.Add(p);
        }

        static void Run(int ticks, Action<int> body)
        {
            for (int t = 0; t < ticks; t++)
            {
                tick++;
                L.Reset();
                L.Blink = (tick % 230) < 8;
                body(t);
                if (bub.Ticks > 0) bub.Ticks--;
                if (squashT > 0) squashT--;
                double tx = 1, ty = 1;
                if (squashT > 0) { double a = squashT / 14.0; tx = 1 + 0.16 * a; ty = 1 - 0.2 * a; }
                sqx += (tx - sqx) * 0.5; sqy += (ty - sqy) * 0.5;
                for (int i = parts.Count - 1; i >= 0; i--)
                {
                    Particle p = parts[i]; p.VY += p.Gravity; p.X += p.VX + (p.Wobble > 0 ? Math.Sin((tick + p.Seed) * 0.1) * 0.3 : 0); p.Y += p.VY;
                    if (p.Bounce && p.Y >= Ground) { p.Y = Ground; p.VY = -p.VY * 0.3; p.VX *= 0.6; if (Math.Abs(p.VY) < 0.5) p.VY = 0; }
                    if (--p.Life <= 0) parts.RemoveAt(i);
                }
                if (tick % 2 == 0) Snap();
            }
        }

        static void Snap()
        {
            // the fake desktop
            using (LinearGradientBrush wb = new LinearGradientBrush(new Rectangle(0, 0, SW, SH), Color.FromArgb(30, 36, 62), Color.FromArgb(70, 52, 96), 70f)) sg.FillRectangle(wb, 0, 0, SW, SH);
            sg.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath p = RoundRect(Win, 8))
            {
                using (SolidBrush sh = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) { sg.TranslateTransform(0, 4); sg.FillPath(sh, p); sg.ResetTransform(); }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(242, 240, 236))) sg.FillPath(b, p);
            }
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(222, 218, 212))) sg.FillRectangle(tb, Win.Left + 1, Win.Top + 1, Win.Width - 2, 18);
            int[] dots = { Pal.C(255, 95, 86), Pal.C(255, 189, 46), Pal.C(39, 201, 63) };
            for (int i = 0; i < 3; i++) using (SolidBrush db = new SolidBrush(Color.FromArgb(dots[i]))) sg.FillEllipse(db, Win.Left + 10 + i * 14, Win.Top + 5, 9, 9);
            Rectangle content = new Rectangle(Win.Left + 10, Win.Top + 26, Win.Width - 20, Win.Height - 36);
            if (screenCol >= 0)
            {
                int[] cols = { Pal.C(60, 120, 220), Pal.C(230, 140, 60), Pal.C(60, 190, 130), Pal.C(220, 90, 160) };
                using (SolidBrush vb = new SolidBrush(Color.FromArgb(cols[screenCol]))) sg.FillRectangle(vb, content);
                using (SolidBrush ib = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
                {
                    int cx = content.Left + content.Width / 2, cy = content.Top + content.Height / 2;
                    if (paused) { sg.FillRectangle(ib, cx - 11, cy - 12, 8, 24); sg.FillRectangle(ib, cx + 3, cy - 12, 8, 24); }
                    else sg.FillPolygon(ib, new[] { new Point(cx - 8, cy - 12), new Point(cx + 12, cy), new Point(cx - 8, cy + 12) });
                }
            }
            else
                using (SolidBrush lb = new SolidBrush(Color.FromArgb(214, 210, 204)))
                    for (int i = 0; i < 5; i++) sg.FillRectangle(lb, content.Left, content.Top + 4 + i * 14, content.Width - (i % 2) * 60 - 20, 6);
            using (SolidBrush tk = new SolidBrush(Color.FromArgb(235, 22, 24, 34))) sg.FillRectangle(tk, 0, SH - TASK, SW, TASK);
            for (int i = 0; i < 6; i++) using (SolidBrush ic = new SolidBrush(Color.FromArgb(i == 2 ? Pal.Orange : Pal.C(70, 76, 96)))) sg.FillEllipse(ic, SW / 2 - 90 + i * 30, SH - TASK + 6, 18, 18);
            sg.SmoothingMode = SmoothingMode.None;

            if (food != null)
            {
                Cells c = new Cells(8, 8); Food.DrawInto(c, food, 1, 1, foodLeft);
                sg.InterpolationMode = InterpolationMode.NearestNeighbor; sg.PixelOffsetMode = PixelOffsetMode.Half;
                sg.DrawImage(c.ToBitmap(), new Rectangle((int)foodX - 16, (int)foodY - 28, 32, 32), 0, 0, 8, 8, GraphicsUnit.Pixel);
            }
            double gy = Y < Win.Top + 2 && X > Win.Left && X < Win.Right ? Win.Top : Ground;
            R.Draw(L, X, Y, S, rot, sqx, sqy, Y > gy ? Y : gy, parts, bub, mon, L.Back);
            sg.DrawImage(R.Fx.Surf.Bmp, new Rectangle(R.Fx.SX, R.Fx.SY, R.Fx.Surf.W, R.Fx.Surf.H), 0, 0, R.Fx.Surf.W, R.Fx.Surf.H, GraphicsUnit.Pixel);
            sg.DrawImage(R.Body.Surf.Bmp, new Rectangle(R.Body.SX, R.Body.SY, R.Body.Surf.W, R.Body.Surf.H), 0, 0, R.Body.Surf.W, R.Body.Surf.H, GraphicsUnit.Pixel);
            scene.Save(Path.Combine(dir, "f" + (frame++).ToString("D4") + ".png"), ImageFormat.Png);
        }

        static GraphicsPath RoundRect(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.Left, r.Top, rad * 2, rad * 2, 180, 90); p.AddArc(r.Right - rad * 2, r.Top, rad * 2, rad * 2, 270, 90);
            p.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90); p.AddArc(r.Left, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
            p.CloseFigure(); return p;
        }
    }
}
