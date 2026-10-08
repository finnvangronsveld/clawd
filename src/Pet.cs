// The pet's body and senses: position, physics, input, needs, and the per-tick loop.
// Behaviours (the big mode switch) live in Behaviours.cs.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Flippy
{
    partial class Pet
    {
        public readonly World W = new World();
        public readonly Renderer R = new Renderer();
        readonly Look L = new Look();
        readonly Random rng = new Random();
        readonly List<Particle> parts = new List<Particle>();
        readonly Bubble bub = new Bubble();
        readonly Settings cfg; readonly Needs needs; readonly Media media; readonly Effects fx = new Effects(); readonly PetMenu menu = new PetMenu();
        readonly List<FoodItem> foods = new List<FoodItem>();
        public Action OpenSettings, Quit;

        // body (X, Y = feet, screen px)
        double X, Y, VX, VY, rot, spinV, sqx = 1, sqy = 1;
        int squashT, Dir = 1;
        string Mode = "walk"; int T, Timer; public int Tick;
        bool suit, thrown;
        // platform (a window he's standing on)
        IntPtr plat = IntPtr.Zero; Native.RECT platR; bool havePlatR, walkOff;
        // looks
        int blink, nextBlink = 200, shake, tuftT;
        IntPtr menuFg; int menuAway; bool menuInit;   // to close the menus: the app in front when they opened, how long the cursor has been away
        // input
        bool drag, moved; double grabDX, grabDY, dvx, dvy; Point downPt, lastCur; readonly List<int> clicks = new List<int>();
        Point cur; double cdx, cdy, cdist; int lookEye; bool lookUp, overBody, curMoved;
        uint idleMs, longestIdle; int kp; double keyCount; int lastKeyTick = -100000; bool typeHand;
        int hover, waveCD; bool wasFar = true;
        // world sensing
        bool videoOn; int videoSeen = -100000; Native.RECT videoRect; string videoTitle = ""; string mediaState = "none"; int pausedPolls; bool musicOn;
        int climbCD = 300; IntPtr climbTarget; double climbX;
        bool fileHover; string[] dropped;
        double cpuHot; int hotCD, lateCD, hungryCD, boredCD; bool lowWarned; PowerLineStatus lastLine = PowerLineStatus.Unknown;
        bool clipPing; bool napping;

        static readonly Regex videoRx = new Regex(@" - YouTube|Netflix|Twitch|Prime Video|Disney\+|Crunchyroll|Vimeo|Plex|VLC media player|Media Player|\bmpv\b|HBO Max|VRT MAX|Streamz|GoPlay|Videoland|Dailymotion", RegexOptions.Compiled);

        int S { get { return W.S; } }
        double K { get { return W.K; } }
        double G { get { return 0.21 * K; } }
        bool Free { get { return Mode == "walk" || Mode == "idle" || Mode == "chase"; } }

        public Pet(Settings settings, Needs n)
        {
            cfg = settings; needs = n;
            media = new Media();
            W.UserScale = cfg.Size;
            Point c = Cursor.Position;
            W.Update(c.X, c.Y);
            X = W.Work.Left + 120 + rng.Next(Math.Max(1, W.Work.Right - W.Work.Left - 240));
            Y = W.Floor;
            cfg.Changed += () => { W.UserScale = cfg.Size; };

            R.Body.MouseDown += (s, e) => MouseDownH(e);
            R.Body.MouseMove += (s, e) => MouseMoveH();
            R.Body.MouseUp += (s, e) => MouseUpH(e);
            R.Body.Cursor = Cursors.Hand;
            menu.PetChosen = sp => SwitchTo(sp);
            R.Body.AllowDrop = true;
            R.Body.DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effect = DragDropEffects.Copy; fileHover = true; }
            };
            R.Body.DragLeave += (s, e) => { fileHover = false; };
            R.Body.DragDrop += (s, e) =>
            {
                fileHover = false;
                string[] f = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (f != null && f.Length > 0) dropped = f;
            };
            R.Body.Message += m => { if (m == Native.WM_CLIPBOARDUPDATE) clipPing = true; };
            R.Body.HandleCreated += (s, e) => { try { Native.AddClipboardFormatListener(R.Body.Handle); } catch { } };
        }

        // ---------- small helpers ----------
        bool Chance(double perTicks) { return rng.NextDouble() < cfg.Activity / perTicks; }
        T1 Pick<T1>(T1[] a) { return a[rng.Next(a.Length)]; }
        void Set(string m, int timer = 0)
        {
            if (Program.Verbose && m != Mode) Program.Log("mode " + m + " tick " + Tick + " at " + (int)X + "," + (int)Y);
            Mode = m; Timer = timer; T = 0; hover = 0;
        }
        public string ForceMode; public int TestX = -1;
        void Say(string text, int ticks = 90) { bub.Text = text; bub.Ticks = ticks; bub.Spinner = false; bub.Thought = false; }
        // chatter respects the chattiness slider
        void Chat(string text, int ticks = 100) { if (cfg.Chatty > 0.05 && rng.NextDouble() < Math.Min(1, cfg.Chatty)) Say(text, ticks); }
        double CX(double cx) { return X + (cx - (Sprite.OX + 11)) * S; }     // cell -> screen
        double CY(double cy) { return Y + (cy - (Sprite.OY + 16)) * S; }

        void Float(string glyph, double cx, double cy, double vx, double vy, int life, int color)
        {
            Particle p = new Particle(); p.Glyph = glyph; p.X = CX(cx); p.Y = CY(cy); p.VX = vx * S; p.VY = vy * S; p.Life = p.Max = life; p.Color = color; p.Wobble = 1; p.Seed = rng.Next(100);
            parts.Add(p);
        }
        void Bit(double cx, double cy, double vx, double vy, int life, int color, double size = 1, double grav = 0.06, bool bounce = false)
        {
            Particle p = new Particle(); p.X = CX(cx); p.Y = CY(cy); p.VX = vx * S; p.VY = vy * S; p.Life = p.Max = life; p.Color = color; p.Size = size; p.Gravity = grav * S; p.Bounce = bounce;
            parts.Add(p);
        }
        void Poof()
        {
            int[] pool = { Pal.White, Pal.Silver, Pal.Steam, Pal.Spark };
            for (int i = 0; i < 18; i++) Bit(Sprite.OX + 11 + (rng.NextDouble() - 0.5) * 18, Sprite.OY + 4 + rng.Next(12), (rng.NextDouble() - 0.5) * 0.8, -0.3 - rng.NextDouble() * 0.7, 30 + rng.Next(20), Pick(pool), 2, 0.02);
        }
        void Hop(double power, double vx)
        {
            LeavePlatform(); Set("fall"); VY = -power * K; VX = vx * K * Dir; thrown = false;
        }

        // ---------- ground / platforms ----------
        void LeavePlatform() { plat = IntPtr.Zero; havePlatR = false; walkOff = false; }
        double Ground()
        {
            if (plat != IntPtr.Zero)
            {
                if (Native.Usable(plat))
                {
                    Native.RECT r = Native.Rect(plat);
                    if (havePlatR) X += r.Left - platR.Left;      // ride along when the window moves
                    platR = r; havePlatR = true;
                    bool covered = Tick % 6 == 0 && !drag && !Native.TopVisibleAt(plat, (int)X, r.Top);   // another window is in front of it now
                    if (r.Top >= W.Work.Top + 30 && X >= r.Left && X <= r.Right && !covered) return r.Top;
                    if (covered && Mode != "fall") Chat("whoa!", 50);
                }
                LeavePlatform();
                if (Mode != "fall" && !drag) { if (Mode == "smash") parts.Clear(); Set("fall"); VY = 0; VX = Dir * 0.8 * K; }
            }
            return W.Floor;
        }

        // ---------- mouse ----------
        void MouseDownH(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            drag = true; moved = false; downPt = Cursor.Position; lastCur = downPt;
            grabDX = X - downPt.X; grabDY = Y - downPt.Y; dvx = dvy = 0;
        }
        void MouseMoveH()
        {
            if (!drag) return;
            Point p = Cursor.Position;
            if (!moved && Math.Abs(p.X - downPt.X) + Math.Abs(p.Y - downPt.Y) > 4)
            {
                moved = true; CloseMenus(); fx.WebOff();
                if (suit) { suit = false; Poof(); }
                LeavePlatform(); rot = 0;
                Say(Pick(new[] { "wheee!", "whoa!", "put me down!", "where are we going?" }), 70);
            }
        }
        void MouseUpH(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // right-click toggles (the click itself already closed an open menu a moment ago)
                if (menu.Open || Environment.TickCount - menu.ClosedAtMs < 300) CloseMenus(); else OpenMenu(Cursor.Position);
                return;
            }
            if (e.Button != MouseButtons.Left || !drag) return;
            drag = false;
            if (moved)
            {
                // let go: fly with the speed you threw him at
                Set("fall"); VX = dvx * 0.9; VY = dvy * 0.9; thrown = true;
                spinV = Math.Max(-25, Math.Min(25, VX * 1.6 / Math.Max(1, K)));
                double sp = Math.Sqrt(VX * VX + VY * VY);
                if (sp > 12 * K) { Say(Pick(new[] { "AAAAA!", "wheeeeee!", "noooo!" }), 60); needs.Fun += 4; }
                bub.Ticks = Math.Min(bub.Ticks, 60);
            }
            else if (Mode == "sleep" || Mode == "yawn") { napping = false; Say("huh?!", 70); Hop(3.6, 0); }
            else if (Mode == "eat" || Mode == "smash" || Mode == "dizzy" || Mode == "swing") { }
            else
            {
                clicks.Add(Tick); clicks.RemoveAll(t => Tick - t > 100);
                if (clicks.Count >= 4) { clicks.Clear(); Set("dizzy", 260); }
                else { Hop(4.6, 0.9); needs.Fun += 0.5; }
            }
        }

        void OpenMenu(Point at)
        {
            var items = new List<MenuItem>();
            items.Add(new MenuItem("Hop!", () => Hop(4.6, 0.9)));
            items.Add(new MenuItem("Chase my cursor", () => Set("chase", 300 + rng.Next(300))));
            items.Add(new MenuItem("Dance", () => Set("dance", 340)));
            items.Add(new MenuItem("Coffee break", () => Set("coffee")));
            items.Add(new MenuItem("Think hard", () => { Set("think"); word = Pick(thinkWords); }));
            items.Add(new MenuItem("Give him a snack", () => SpawnFood(true)));
            if (cfg.Climbing) items.Add(new MenuItem("Climb a window", () => { if (!TryClimb()) Say("no window to climb!", 90); }));
            items.Add(MenuItem.Separator());
            if (cfg.Gun) items.Add(new MenuItem("Shoot my cursor", () => Set("gun")));
            if (cfg.Tricks) items.Add(new MenuItem("Trickshot!", () => Set("trick")));
            if (cfg.WebSwing) items.Add(new MenuItem("Web-swing!", () => Set("suitup")));
            if (cfg.Smash) items.Add(new MenuItem("Smash the laptop", () => { parts.Clear(); Set("smash"); }));
            items.Add(MenuItem.Separator());
            items.Add(new MenuItem("Do your thing!", DoSignature));
            items.Add(MenuItem.Separator());
            items.Add(MenuItem.Page("Change pet", SpeciesList.Current.ShortName));
            items.Add(new MenuItem("Settings...", () => { if (OpenSettings != null) OpenSettings(); }));
            MenuItem bye = new MenuItem("Bye, " + SpeciesList.Current.ShortName, () => { if (Quit != null) Quit(); }); bye.Danger = true;
            items.Add(bye);
            double[] nd = cfg.Needs ? new[] { needs.Fullness, needs.Energy, needs.Fun, needs.Love } : null;
            menu.Show(items, nd, at, W.Work, W.Dpi / 96f);
            menuInit = false;
        }

        public void SpawnFood(bool atCursor)
        {
            Point c = Cursor.Position;
            double fx0 = atCursor ? c.X : X + Dir * 80 * K, fy0 = atCursor ? c.Y : CY(Sprite.OY - 20);
            FoodItem f = new FoodItem(Food.Pick(rng), fx0, fy0);
            foods.Add(f);
            Chat(Pick(new[] { "ooh, food!", "for me?!", "snack!!" }), 70);
        }
        // ---------- changing pets ----------
        Species pending;
        public void SwitchTo(Species sp)
        {
            cfg.Pet = sp.Id; cfg.Save();
            if (sp == SpeciesList.Current) { Say("that's me!", 80); return; }
            // cancel whatever he was doing, cleanly
            CloseMenus(); fx.WebOff();
            suit = false; rot = 0; eating = null; napping = false; thrown = false; drag = false;
            if (Mode == "smash") parts.Clear();
            pending = sp; Set("switch");
        }
        void DoSignature()
        {
            string m = SpeciesList.Current.SignatureMode;
            if (m == "smash") parts.Clear();
            Set(m, m == "dance" ? 340 : 0);
        }
        public bool Busy;
        Point HeadPoint() { return new Point((int)X, (int)CY(Sprite.OY - 2)); }
        void CloseMenus() { menu.Close(); ClickWatch.Disarm(); menuInit = false; }
        public void Poked() { CloseMenus(); if (!drag) { Set("wave"); poked = true; } }
        bool poked;

        // ---------- one tick (60 per second) ----------
        public void Step()
        {
            Tick++; T++;
            if (Busy && Tick % 30 == 0) System.Threading.Thread.Sleep(400);   // test only: a stalled UI thread
            if (menuInit && !menu.Open) { ClickWatch.Disarm(); menuInit = false; }   // closed by picking an item
            if (Tick == 1 && TestX >= 0) X = TestX;
            if (Tick == 90 && ForceMode != null) { if (ForceMode == "food") SpawnFood(false); else if (ForceMode == "menu") OpenMenu(HeadPoint()); else if (ForceMode == "picker") { OpenMenu(HeadPoint()); menu.ShowPets(); } else if (ForceMode == "switch") SwitchTo(SpeciesList.All[SpeciesList.All.IndexOf(SpeciesList.Current) == 0 ? 1 : 0]); else if (ForceMode == "signature") DoSignature(); else Set(ForceMode, 600); }
            if (ForceMode == "watch" || ForceMode == "paused") { videoSeen = Tick; videoRect = W.Work; }
            if (Tick % 10 == 0 || drag) W.Update(X, Y);

            SenseCursor();
            L.Reset();
            if (blink > 0) blink--; else if (--nextBlink <= 0) { blink = 8; nextBlink = 160 + rng.Next(300); }
            L.Blink = blink > 0;
            if (bub.Ticks > 0) bub.Ticks--;
            if (squashT > 0) squashT--;
            if (shake > 0) shake--;

            kp = Native.KeyPresses();
            keyCount = keyCount * 0.985 + kp;
            if (kp > 0) { lastKeyTick = Tick; typeHand = !typeHand; }
            if (Tick % 10 == 0) idleMs = Native.IdleMs();
            if (idleMs > longestIdle) longestIdle = idleMs;
            SenseWorld();
            if (ForceMode == "paused") pausedPolls = 5;
            if (Tick % 60 == 0 && cfg.Needs) needs.Second(Mode == "sleep", IsNight());

            double ground = drag ? W.Floor : Ground();

            if (drag)
            {
                Point p = Cursor.Position;
                dvx = dvx * 0.4 + (p.X - lastCur.X) * 0.6; dvy = dvy * 0.4 + (p.Y - lastCur.Y) * 0.6; lastCur = p;
                if (moved)
                {
                    X = p.X + grabDX; Y = p.Y + grabDY;
                    L.Phase = (Tick / 4) % 4; L.Arms = (Tick / 6) % 2 == 0 ? "up" : "down"; L.Mouth = "o"; L.EyeStyle = "wide";
                    rot = Math.Max(-35, Math.Min(35, -dvx * 2.2));       // dangles like he's being carried
                }
            }
            else if (menu.Open)
            {
                L.EyeStyle = "up"; rot = 0;
                if (Mode == "sleep") Set("idle", 60);
                Rectangle body = new Rectangle(R.Body.SX, R.Body.SY, R.Body.Surf.W, R.Body.Surf.H);
                bool near = body.Contains(cur) || menu.Bounds.Contains(cur);
                if (!menuInit) { menuInit = true; menuFg = Native.Foreground(); menuAway = 0; ClickWatch.Arm(); }   // just opened
                menuAway = near ? 0 : menuAway + 1;
                string why = ClickWatch.ClickedOutside(menu.PanelContains) ? "click"     // any click that isn't on the menu (his body too)
                           : Native.EscDown() ? "esc"
                           : Native.Foreground() != menuFg ? "focus"                     // Alt+Tab / another window came to the front
                           : menuAway > 240 ? "away" : null;                               // cursor wandered off for 4 seconds
                if (why != null) { if (Program.Verbose) Program.Log("menu closed: " + why); CloseMenus(); }
                menu.Tick();
            }
            else
            {
                if (fx != null && Mode != "swing") fx.WebOff();
                if (suit && Mode != "suitup" && Mode != "swing" && Mode != "heropose") { suit = false; Poof(); }
                Triggers(ground);
                Behave(ground);
                // keep him on his screen
                double minX = W.Work.Left + 7 * S, maxX = W.Work.Right - 7 * S;
                if (X < minX) { X = minX; Dir = 1; VX = Math.Abs(VX) * (thrown ? 0.6 : 1); spinV = -spinV * 0.6; }
                if (X > maxX) { X = maxX; Dir = -1; VX = -Math.Abs(VX) * (thrown ? 0.6 : 1); spinV = -spinV * 0.6; }
                if (Mode != "fall" && Mode != "swing" && Mode != "trick" && Y > ground) Y = ground;
            }
            L.Suit = suit; L.Face = Dir; if (tuftT > 0) { tuftT--; L.TuftLift = tuftT > 10 ? 2 : (tuftT > 4 ? 1 : 0); }
            if (flashT > 0) flashT--;
            if (recoilT > 0) recoilT--;
            if (L.Gun) { L.Flash = flashT > 0; L.Recoil = recoilT > 0; }
            if (musicOn && cfg.Music && !L.Back && !suit) L.Headphones = true;

            UpdateParticles(ground);
            fx.Tick(cur, K, S);
            for (int i = foods.Count - 1; i >= 0; i--) { foods[i].Tick(W); if (foods[i].Gone) foods.RemoveAt(i); }

            // squash & stretch
            double tx = 1, ty = 1;
            if (squashT > 0) { double a = squashT / 14.0; tx = 1 + 0.16 * a; ty = 1 - 0.2 * a; }
            else if (Mode == "fall" && !drag) { double st = Math.Min(0.14, Math.Abs(VY) / (40 * K)); tx = 1 - st * 0.6; ty = 1 + st; }
            else if (Mode == "idle" || Mode == "watch" || Mode == "sleep") ty = 1 + 0.02 * Math.Round(Math.Sin(Tick * (Mode == "sleep" ? 0.04 : 0.07)));   // breathing (in steps)
            sqx += (tx - sqx) * 0.5; sqy += (ty - sqy) * 0.5;

            if (Tick % 120 == 0) { R.KeepOnTop(); foreach (FoodItem f in foods) f.KeepOnTop(); }
        }

        string lastKey = ""; int lastDX, lastDY;
        public void Draw()
        {
            double gy = (Mode == "fall" || Mode == "swing" || drag) ? ShadowGround() : Y;
            double sx = shake > 0 ? (rng.Next(5) - 2) * S * 0.6 : 0;
            int dx = (int)(X + sx), dy = (int)Y;
            // skip frames where nothing visible changed, and just move the windows when only his position did
            string key = L.Key() + "," + (int)(gy - Y) + "," + Math.Round(rot) + "," + Math.Round(sqx * 100) + "," + Math.Round(sqy * 100) + "," + S
                         + (bub.Ticks > 0 ? bub.Text + (bub.Spinner ? (R.Frame / 6).ToString() : "") : "") + parts.Count;
            if (parts.Count > 0 || key != lastKey) { R.Draw(L, X + sx, Y, S, rot, sqx, sqy, gy, parts, bub, W.Mon, L.Back); lastKey = key; }
            else { R.Frame++; if (dx != lastDX || dy != lastDY) R.Move(dx - lastDX, dy - lastDY); }
            lastDX = dx; lastDY = dy;
            foreach (FoodItem f in foods) if (!f.Grounded || f.Dragging || f.Age < 3 || f.Left < 1) f.Draw(S);
        }
        int shadowCacheTick = -100; double shadowCache;
        double ShadowGround()
        {
            if (Tick - shadowCacheTick > 8) { shadowCache = W.GroundAt(X, Y, W.Floor); shadowCacheTick = Tick; }
            return shadowCache;
        }

        // ---------- senses ----------
        void SenseCursor()
        {
            Point c = Cursor.Position;
            curMoved = Math.Abs(c.X - cur.X) + Math.Abs(c.Y - cur.Y) > 1;
            cur = c;
            cdx = cur.X - X; cdy = cur.Y - CY(Sprite.OY + 3);
            cdist = Math.Sqrt(cdx * cdx + cdy * cdy);
            lookEye = cdx > 25 * K ? 1 : (cdx < -25 * K ? -1 : 0);
            lookUp = cdy < -40 * K && Math.Abs(cdx) < 220 * K;
            overBody = Math.Abs(cur.X - X) < 8 * S && cur.Y > CY(Sprite.OY - 1) && cur.Y < Y;
        }

        void SenseWorld()
        {
            // a video in front?  (window title, checked locally)
            if (Tick % 30 == 0)
            {
                IntPtr fg = Native.Foreground();
                if (Native.Ours(fg)) { if (Tick - videoSeen < 120) videoSeen = Tick; }
                else
                {
                    string title = Native.Title(fg);
                    if (cfg.MovieNight && videoRx.IsMatch(title)) { videoSeen = Tick; videoRect = Native.Rect(fg); videoTitle = title; }
                }
            }
            videoOn = Tick - videoSeen < 120;
            if (Tick % 30 == 15)
            {
                string v; bool m; media.Poll(out v, out m);
                mediaState = v; musicOn = m && !videoOn;
                if (mediaState == "paused") pausedPolls++; else pausedPolls = 0;
            }
            // the PC itself
            if (cfg.PcReactions && Tick % 120 == 60)
            {
                double load = Native.CpuLoad();
                cpuHot = load > 0.85 ? cpuHot + 1 : Math.Max(0, cpuHot - 1);
                PowerStatus ps = SystemInformation.PowerStatus;
                if (ps.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery && ps.BatteryChargeStatus != BatteryChargeStatus.Unknown)
                {
                    if (ps.BatteryLifePercent < 0.15 && ps.PowerLineStatus == PowerLineStatus.Offline && !lowWarned && FreeOrIdle()) { lowWarned = true; Set("lowbatt"); }
                    if (ps.PowerLineStatus == PowerLineStatus.Online && lastLine == PowerLineStatus.Offline && FreeOrIdle()) Set("charged");
                    if (ps.BatteryLifePercent > 0.3) lowWarned = false;
                    lastLine = ps.PowerLineStatus;
                }
            }
        }
        bool FreeOrIdle() { return Free || Mode == "sleep" || Mode == "watch"; }
        static bool IsNight() { int h = DateTime.Now.Hour; return h >= 0 && h < 5; }

        // ---------- particles ----------
        void UpdateParticles(double ground)
        {
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                Particle p = parts[i];
                p.VY += p.Gravity;
                p.X += p.VX + (p.Wobble > 0 ? Math.Sin((Tick + p.Seed) * 0.1) * 0.25 * K : 0);
                p.Y += p.VY;
                if (p.Bounce && p.Y >= ground) { p.Y = ground; p.VY = -p.VY * 0.3; p.VX *= 0.6; if (Math.Abs(p.VY) < 0.4 * K) p.VY = 0; }
                if (--p.Life <= 0) parts.RemoveAt(i);
            }
            if (parts.Count > 260) parts.RemoveRange(0, parts.Count - 260);
        }

        // ---------- climbing ----------
        bool TryClimb()
        {
            IntPtr h; int x;
            if (!Native.FindClimbTarget((int)X, W.Work.Left + 60, W.Work.Right - 60, W.Work.Top + 60, (int)Y - 60, (int)(55 * K + 40), out h, out x)) return false;
            climbTarget = h; climbX = x; Set("goclimb");
            return true;
        }

        public void SaveAll() { needs.Save(); }
    }
}
