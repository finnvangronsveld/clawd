// What Clawd does: triggers (things that grab his attention) and every behaviour, one tick at a time.
// Timings are in ticks at 60 per second.
using System;
using System.Drawing;
using System.IO;

namespace Clawd
{
    partial class Pet
    {
        static readonly string[] thinkWords = { "Thinking", "Pondering", "Clauding", "Noodling", "Percolating", "Cogitating", "Brewing", "Musing", "Ruminating", "Vibing", "Simmering", "Spelunking", "Wrangling", "Schlepping" };
        static readonly string[] commentLines = { "ooh", "wait, what?", "no way!", "plot twist!", "this part is so good", "called it.", "lol", "*crunch crunch*",
            "pass the popcorn", "who is this guy?", "nooo!", "hehe", "same tbh", "10/10", "rewind that!", "wait for it...", "I knew it", "classic", "chills.",
            "bro what", "I could do that", "this is peak", "okay okay okay", "smooth.", "did you see that?", "love this part", "oh no no no", "hmm, suspicious" };
        static readonly string[] complainLines = { "hey! I was watching!", "press play!!", "UNPAUSE IT", "ugh, seriously?", "my popcorn is cold", "rude.", "don't leave me hanging", "I need to know!" };
        static readonly string[] sulkLines = { "...", "*sigh*", "still waiting.", "*crunch*" };
        static readonly string[] resumeLines = { "finally!", "yesss", "that's better.", "shh, it started" };
        static readonly string[] hungryLines = { "I'm hungry...", "snack? please?", "*stomach rumbles*", "is it lunch yet?", "feed me :(" };
        static readonly string[] boredLines = { "I'm bored...", "play with me?", "hellooo?", "pay attention to meee", "*taps screen*" };
        static readonly string[] tiredLines = { "so sleepy...", "*yawn*", "nap time?", "can't... keep... eyes open" };
        string word = "Thinking";
        int nextComment, laughT, rimEvery = 80, swings, maxSwings, freeT, anchT, flip, travel = 1, trickKind, hits0;
        double ax, ay, ropeL, ropeTarget, jy, jv, watchX, eatGain; bool air, anchored, anchorCursor, resumed;
        FoodItem eating;

        // ======================= triggers =======================
        void Triggers(double ground)
        {
            bool free = Free;

            if (dropped != null) { if (free || Mode == "sniff" || Mode == "sleep") { Set("filechew"); } else dropped = null; free = false; }
            if (fileHover && (free || Mode == "sleep")) { Set("sniff"); free = false; }
            if (clipPing) { clipPing = false; if (free && cfg.PcReactions && rng.NextDouble() < 0.35 * Math.Max(0.2, cfg.Chatty)) { Set("catch"); free = false; } }

            // a snack nearby? go get it
            if ((free || Mode == "sleep") && foods.Count > 0 && !drag)
            {
                FoodItem f = NearestFood();
                if (f != null && (needs.Fullness < 92 || f.Age > 600)) { Set("gofood"); free = false; }
            }
            if (cfg.MovieNight && videoOn && (free || Mode == "sleep" || Mode == "yawn") && !napping) { Set("gowatch"); free = false; }
            if (free && idleMs >= 60000) { Set("yawn"); free = false; }
            // morning greeting after a long time away
            if (idleMs < 2000 && longestIdle > 4 * 3600 * 1000u) { longestIdle = 0; if (free || Mode == "sleep") { Set("morning"); free = false; } }
            if (idleMs < 2000) longestIdle = Math.Min(longestIdle, idleMs);

            if (free || Mode == "wave")
            {
                if (overBody && curMoved) hover++; else if (!overBody) hover = 0;
                if (hover >= 30) { Set("pet"); hover = 50; free = false; }
            }
            if (cdist > 350 * K) wasFar = true;
            if (free && wasFar && cdist < 160 * K && Tick > waveCD) { Set("wave"); wasFar = false; waveCD = Tick + 1200; free = false; }

            bool canType = free || Mode == "wave" || Mode == "think" || Mode == "dance" || Mode == "coffee" || Mode == "bop";
            if (cfg.TypeAlong && canType && keyCount >= 3) { Set("typing"); free = false; }

            if (cfg.Climbing && free && Mode != "chase" && Tick % 120 == 60 && Tick > climbCD) { if (TryClimb()) free = false; }

            if (cfg.PcReactions && free)
            {
                if (cpuHot >= 5 && Tick > hotCD) { hotCD = Tick + 60 * 60 * 10; Set("hot"); free = false; }
                else if (IsNight() && Tick > lateCD && rng.NextDouble() < 0.002) { lateCD = Tick + 60 * 60 * 15; Chat(Pick(new[] { "it's late... bed?", "go to sleep, human", "*yawn* it's so late" }), 120); }
            }
            if (cfg.Needs && free && Tick > 1800)
            {
                if (needs.Fullness < 30 && Tick > hungryCD) { hungryCD = Tick + 60 * 90; Set("hungry"); free = false; }
                else if (needs.Energy < 20 && rng.NextDouble() < 0.003) { napping = true; Set("yawn"); free = false; }
                else if (needs.Fun < 30 && Tick > boredCD) { boredCD = Tick + 60 * 120; Chat(Pick(boredLines), 110); Set("chase", 400); free = false; }
            }
            if (cfg.Music && musicOn && free && rng.NextDouble() < 0.004 * cfg.Activity) { Set("bop", 420 + rng.Next(300)); }
        }

        FoodItem NearestFood()
        {
            FoodItem best = null; double bd = double.MaxValue;
            foreach (FoodItem f in foods) { if (f.Dragging) continue; double d = Math.Abs(f.X - X) + Math.Abs(f.Y - Y) * 0.5; if (d < bd) { bd = d; best = f; } }
            return best;
        }

        void PickActivity()
        {
            // weighted, respecting settings and how he feels
            var opts = new System.Collections.Generic.List<string>(); var wts = new System.Collections.Generic.List<double>();
            Action<string, double> add = (m, w) => { if (w > 0) { opts.Add(m); wts.Add(w); } };
            add("dance", musicOn ? 20 : 9); add("coffee", needs.Energy < 40 ? 16 : 8); add("think", 8);
            if (cfg.Smash) add("smash", 4);
            if (cfg.Gun) add("gun", 3);
            if (cfg.Tricks) add("trick", 4);
            if (cfg.WebSwing) add("suitup", 4);
            if (cfg.Needs && needs.Energy < 30) add("nap", 14);
            add("signature", 7);
            add("walk", 45);
            double sum = 0; foreach (double w in wts) sum += w;
            double r = rng.NextDouble() * sum; string pick = "walk";
            for (int i = 0; i < opts.Count; i++) { r -= wts[i]; if (r <= 0) { pick = opts[i]; break; } }
            switch (pick)
            {
                case "dance": Set("dance", 340); break;
                case "think": Set("think"); word = Pick(thinkWords); break;
                case "smash": parts.Clear(); Set("smash"); break;
                case "nap": napping = true; Set("yawn"); break;
                case "signature": DoSignature(); break;
                case "walk": if (rng.Next(3) == 0) Dir = -Dir; Set("walk"); break;
                default: Set(pick); break;
            }
        }

        // ======================= behaviours =======================
        void Behave(double ground)
        {
            int t = T;
            switch (Mode)
            {
                case "fall":
                    {
                        double prevFeet = Y;
                        VY = Math.Min(VY + G, 16 * K);
                        Y += VY; X += VX;
                        if (thrown) { rot += spinV; spinV *= 0.985; VX *= 0.995; }
                        L.EyeStyle = thrown ? "wide" : (VY < 0 ? "up" : "normal");
                        L.Arms = VY < 0 ? "up" : (thrown ? ((Tick / 5) % 2 == 0 ? "up" : "out") : "out");
                        if (thrown) L.Mouth = "o";
                        if (Y - 16 * S < W.Mon.Top) { Y = W.Mon.Top + 16 * S; VY = Math.Abs(VY) * 0.5; }
                        if (VY > 0)
                        {
                            bool landed = false;
                            IntPtr h = Native.FindTop((int)X, 4, (int)prevFeet - 1, (int)Y);
                            if (h != IntPtr.Zero) { Y = Native.Rect(h).Top; plat = h; platR = Native.Rect(h); havePlatR = true; landed = true; climbCD = Tick + 1800; }
                            else if (Y >= ground) { Y = ground; landed = true; }
                            if (landed)
                            {
                                double impact = VY;
                                if (thrown && impact > 7 * K && Math.Abs(VX) + impact > 9 * K) { VY = -impact * 0.35; VX *= 0.6; squashT = 14; Bit(Sprite.OX + 11, Sprite.OY + 16, -0.5, -0.3, 20, Pal.Steam, 2, 0.02); Bit(Sprite.OX + 11, Sprite.OY + 16, 0.5, -0.3, 20, Pal.Steam, 2, 0.02); plat = IntPtr.Zero; }
                                else
                                {
                                    if (impact > 4 * K) squashT = 14;
                                    bool dizzy = thrown && (Math.Abs(spinV) > 8 || impact > 6 * K);
                                    VY = 0; VX = 0; rot = 0; spinV = 0; walkOff = false;
                                    if (dizzy) { thrown = false; Set("dizzy", 200); }
                                    else { thrown = false; Set("idle", 50); }
                                }
                            }
                        }
                        break;
                    }
                case "walk":
                    {
                        double spd = (cfg.Needs && needs.Fullness < 15 ? 0.5 : 0.85) * K;
                        X += spd * Dir; Y = ground; L.Eye = Dir;
                        if (Tick % 10 == 0) L.Phase = (Tick / 10) % 4; else L.Phase = (Tick / 10) % 4;
                        if (cfg.Needs && needs.Fullness < 15) L.EyeStyle = "sad";
                        if (cdist < 220 * K) { L.Eye = lookEye; if (lookUp) L.EyeStyle = "up"; if (Chance(400)) Set("idle", 120 + rng.Next(160)); }
                        if (havePlatR && !walkOff)
                        {
                            if ((Dir < 0 && X < platR.Left + 15 * K) || (Dir > 0 && X > platR.Right - 15 * K)) { if (rng.Next(5) == 0) walkOff = true; else Dir = -Dir; }
                        }
                        if (Chance(600)) Set("idle", 80 + rng.Next(320));
                        else if (Chance(2400)) Set("chase", 300 + rng.Next(300));
                        else if (Chance(2400)) Hop(4.6, 0.9);
                        break;
                    }
                case "idle":
                    Y = ground;
                    if (cdist < 900 * K) { L.Eye = lookEye; if (lookUp) L.EyeStyle = "up"; if (cfg.Needs && needs.Love > 85 && cdist < 300 * K && (Tick / 90) % 6 == 0) L.EyeStyle = "heart"; }
                    else if (rng.Next(90) == 0) L.Eye = rng.Next(3) - 1;
                    if (SpeciesList.Current.Lines.Length > 0 && Chance(5000)) Chat(Pick(SpeciesList.Current.Lines), 110);
                    if (cfg.Needs && needs.Fullness < 15) L.EyeStyle = "sad";
                    if (--Timer <= 0) PickActivity();
                    break;
                case "chase":
                    Y = ground;
                    if (Math.Abs(cdx) > 12 * K) { Dir = cdx > 0 ? 1 : -1; X += 1.6 * K * Dir; L.Phase = (Tick / 6) % 4; L.Eye = Dir; }
                    if (lookUp) L.EyeStyle = "up";
                    if (t == 1) Chat("gonna get it!", 80);
                    if (Math.Abs(cdx) < 40 * K && cdy < -20 * K && cdy > -220 * K && rng.Next(50) == 0) { Hop(6, 0); needs.Fun += 1; }
                    else if (t >= Timer) Set("idle", 100);
                    break;
                case "goclimb":
                    Y = ground;
                    if (!Native.Usable(climbTarget) || t > 1200) { climbCD = Tick + 600; Set("idle", 60); break; }
                    if (t == 1) Chat(Pick(new[] { "ooh, a window!", "up there!", "I can make that", "climb time!" }), 90);
                    if (Math.Abs(climbX - X) > 6 * K) { Dir = climbX > X ? 1 : -1; X += 1.4 * K * Dir; L.Eye = Dir; L.EyeStyle = "up"; L.Phase = (Tick / 6) % 4; }
                    else Set("crouch");
                    break;
                case "crouch":
                    Y = ground; L.Sit = true; L.EyeStyle = "up"; L.Arms = "down";
                    if (t >= 24)
                    {
                        if (Native.Usable(climbTarget))
                        {
                            double dy = Y - Native.Rect(climbTarget).Top;
                            if (dy > 0) { climbCD = Tick + 480; LeavePlatform(); Set("fall"); VY = -(Math.Sqrt(2 * G * dy) + 1.0 * K); VX = 0; squashT = 8; }
                            else Set("idle", 40);
                        }
                        else Set("idle", 40);
                    }
                    break;
                case "wave":
                    Y = ground; L.Eye = lookEye; L.EyeStyle = "happy"; L.Mouth = "smile";
                    L.Arms = (t / 12) % 2 == 0 ? "wave1" : "wave2";
                    if (t == 1) { if (poked) { Say("I'm right here!", 120); poked = false; } else Chat("hi!", 100); needs.Love += 1; }
                    if (t >= 120) Set("idle", 80);
                    break;
                case "pet":
                    Y = ground; L.EyeStyle = "happy"; L.Blush = true; L.Mouth = "smile";
                    if (overBody) hover = 50; else hover--;
                    if (t % 20 == 1) Float("heart", Sprite.OX + 9 + rng.Next(-8, 9), Sprite.OY - 4, (rng.NextDouble() - 0.5) * 0.05, -0.12, 80, Pal.Heart);
                    if (t == 1) Chat(needs.Love < 40 ? "finally some love" : "hehe", 90);
                    needs.Love += 0.05; needs.Fun += 0.02; needs.Clamp();
                    if (hover <= 0) Set("idle", 60);
                    break;
                case "dizzy":
                    Y = ground; L.EyeStyle = "dizzy"; L.Mouth = "o"; L.Arms = "down"; L.Wob = (t / 10) % 2 == 0 ? 1 : -1;
                    if (t % 6 == 0) { double a = Tick * 0.12; Float("star", Sprite.OX + 10 + Math.Cos(a) * 10, Sprite.OY - 3 + Math.Sin(a) * 2, 0, 0, 6, Pal.Spark); }
                    if (t == 1) Say("@_@", 100);
                    if (t >= Timer) Set("idle", 60);
                    break;
                case "dance":
                    {
                        Y = ground; L.EyeStyle = "happy"; L.Mouth = "smile";
                        int beat = t / 16; L.Sit = beat % 2 == 1; L.Arms = beat % 2 == 0 ? "upL" : "upR";
                        X += (beat % 4 < 2 ? 0.3 : -0.3) * K;
                        if (t % 28 == 1) Float("note", Sprite.OX + 9 + rng.Next(-10, 11), Sprite.OY - 4, (rng.NextDouble() - 0.5) * 0.06, -0.15, 90, Pick(Pal.Notes));
                        needs.Fun += 0.01;
                        if (t >= Timer) Hop(3, 0);
                        break;
                    }
                case "bop":     // music playing: headphones on, head bobbing
                    {
                        Y = ground; L.EyeStyle = (t / 120) % 3 == 2 ? "happy" : "sleep"; L.Mouth = "smile";
                        L.Sit = (t / 15) % 2 == 1; L.Arms = (t / 30) % 2 == 0 ? "out" : "down";
                        if (t % 40 == 1) Float("note", Sprite.OX + 9 + rng.Next(-10, 11), Sprite.OY - 4, (rng.NextDouble() - 0.5) * 0.06, -0.15, 90, Pick(Pal.Notes));
                        if (t == 1) Chat(Pick(new[] { "ooh I love this song", "tunes!", "*bops*" }), 90);
                        needs.Fun += 0.01;
                        if (t >= Timer || !musicOn) Set("idle", 60);
                        break;
                    }
                case "coffee":
                    {
                        Y = ground;
                        bool sip = (t >= 60 && t < 110) || (t >= 190 && t < 240) || (t >= 320 && t < 370);
                        if (sip) { L.Mug = "sip"; L.Arms = "sip"; L.EyeStyle = "sleep"; }
                        else { L.Mug = "hold"; if (t > 110) L.EyeStyle = "happy"; else if (cdist < 900 * K) L.Eye = lookEye; }
                        if (t > 110) L.Blush = true;
                        if (t % 14 == 0) Float("dot", (sip ? Sprite.OX + 18 : Sprite.OX + 22) + rng.Next(2), Sprite.OY + (sip ? 2 : -1), 0, -0.1, 32, Pal.Steam);
                        if (t == 1) Chat("coffee time", 80);
                        if (t == 380) { Chat("ahh.", 90); needs.Energy += 15; needs.Clamp(); }
                        if (t >= 450) Set("idle", 60);
                        break;
                    }
                case "think":
                    Y = ground; L.Eye = 1; L.EyeStyle = "up";
                    if (t < 300) { if (t % 100 == 0) word = Pick(thinkWords); Say(word + "...", 2); bub.Spinner = true; }
                    else
                    {
                        L.Bang = t < 330; L.EyeStyle = "normal"; L.Eye = 0; L.Mouth = "o";
                        if (t == 300) Say("got it!", 100);
                        if (t >= 336) Hop(3.6, 0);
                    }
                    break;
                case "yawn":
                    Y = ground; L.Arms = "up"; L.EyeStyle = "sleep";
                    if (t < 64) L.Mouth = "yawn";
                    if (t == 2 && napping) Chat(Pick(tiredLines), 100);
                    if (idleMs < 1500 && !napping) Set("idle", 60);
                    else if (t >= 90) Set("sleep");
                    break;
                case "sleep":
                    Y = ground; L.Sit = true; L.EyeStyle = "sleep"; L.Arms = "down";
                    if (IsNight()) L.Nightcap = true;
                    if (t % 70 == 1) Float("z", Sprite.OX + 17, Sprite.OY - 2, 0.03, -0.09, 120, Pal.Blue);
                    bool wake = napping ? (needs.Energy >= 95 || t > 60 * 60 * 20) : idleMs < 1500;
                    if (wake) { napping = false; L.Sit = false; L.EyeStyle = "up"; Say(Pick(new[] { "huh?!", "*yawn* morning", "I'm up! I'm up!" }), 80); Hop(3.6, 0); }
                    break;
                case "morning":
                    Y = ground; L.EyeStyle = "happy"; L.Mouth = "smile"; L.Arms = (t / 12) % 2 == 0 ? "wave1" : "wave2";
                    if (t == 1) Say(DateTime.Now.Hour < 12 ? "good morning!" : (DateTime.Now.Hour < 18 ? "welcome back!" : "good evening!"), 120);
                    if (t >= 140) Set("coffee");
                    break;
                case "hungry":
                    Y = ground; L.EyeStyle = "sad"; L.Mouth = "frown"; L.Arms = "hold"; L.Wob = t < 40 ? ((t / 4) % 2 == 0 ? 0.5 : -0.5) : 0;
                    if (t == 1) Say(Pick(hungryLines), 120);
                    if (t >= 160) Set("idle", 100);
                    break;
                case "gofood":
                    {
                        Y = ground;
                        FoodItem f = NearestFood();
                        if (f == null || t > 900) { Set("idle", 60); break; }
                        if (t == 1) Chat(Pick(new[] { "FOOD!", "mine!", "ooh what's that" }), 70);
                        double dx = f.X - X;
                        bool reach = Math.Abs(dx) < 14 * K && Math.Abs(f.Y - Y) < 40 * K;
                        if (reach && !f.Dragging) { eating = f; Set("eat"); break; }
                        if (Math.Abs(dx) > 6 * K) { Dir = dx > 0 ? 1 : -1; X += 1.5 * K * Dir; L.Eye = Dir; L.Phase = (Tick / 5) % 4; L.Mouth = "o"; }
                        else if (f.Y < Y - 40 * K) { L.EyeStyle = "up"; L.Arms = (t / 10) % 2 == 0 ? "up" : "out"; }   // it's up there, out of reach
                        break;
                    }
                case "eat":
                    {
                        Y = ground;
                        if (t == 1 && eating != null)
                        {
                            L.Held = eating.Kind;
                            eatGain = eating.Kind == "pizza" ? 40 : (eating.Kind == "apple" ? 22 : 30);
                            heldKind = eating.Kind; eating.Remove(); foods.Remove(eating); eating = null;
                        }
                        L.Held = heldKind; L.Arms = "hold"; L.EyeStyle = "happy";
                        int bite = t / 50;                      // 3 bites
                        L.HeldLeft = 1 - Math.Min(3, bite) / 3.0;
                        bool chomp = t % 50 < 14;
                        L.HeldUp = chomp; L.Mouth = chomp ? "chomp" : "chew";
                        if (t % 50 == 14) for (int i = 0; i < 4; i++) Bit(Sprite.OX + 11, Sprite.OY + 8, (rng.NextDouble() - 0.5) * 0.4, -0.3, 40, Pal.C(214, 160, 92), 1, 0.03, true);
                        if (t >= 150) { L.Held = "none"; L.Mouth = "smile"; L.Blush = true; }
                        if (t == 152)
                        {
                            needs.Fullness += eatGain; needs.Love += 4; needs.Fun += 2; needs.Clamp();
                            Say(Pick(new[] { "yum!", "delicious!", "*burp*", "more please", "thank you!!" }), 100);
                            for (int i = 0; i < 3; i++) Float("heart", Sprite.OX + 6 + rng.Next(10), Sprite.OY - 2, (rng.NextDouble() - 0.5) * 0.05, -0.12, 70, Pal.Heart);
                        }
                        if (t >= 230) Set("idle", 60);
                        break;
                    }
                case "sniff":      // you're dragging a file over him
                    Y = ground; L.EyeStyle = "wide"; L.Mouth = "o"; L.Arms = "up";
                    if (t == 1) Chat(Pick(new[] { "ooh, for me?", "is that a file?", "gimme gimme" }), 100);
                    if (!fileHover && dropped == null) Set("idle", 40);
                    break;
                case "filechew":
                    {
                        Y = ground;
                        if (t == 1) Say(FileComment(dropped), 150);
                        L.Mouth = (t / 8) % 2 == 0 ? "chomp" : "chew"; L.EyeStyle = t < 90 ? "happy" : "normal"; L.Arms = "hold";
                        if (t % 12 == 0 && t < 90) Bit(Sprite.OX + 11, Sprite.OY + 9, (rng.NextDouble() - 0.5) * 0.5, -0.4, 40, Pal.White, 1, 0.03, true);
                        if (t >= 160) { dropped = null; needs.Fun += 3; Set("idle", 60); }
                        break;
                    }
                case "catch":      // you copied something: a little paper falls and he catches it
                    Y = ground; L.Arms = "up"; L.EyeStyle = "up";
                    if (t == 1) Float("paper", Sprite.OX + 9, Sprite.OY - 16, 0, 0.12, 70, Pal.White);
                    if (t == 70) { Chat(Pick(new[] { "copied!", "got it!", "ctrl+c, nice", "I'll hold onto that" }), 80); }
                    if (t > 70) { L.Arms = "hold"; L.EyeStyle = "happy"; }
                    if (t >= 140) Set("idle", 40);
                    break;
                case "hot":        // the CPU is working hard
                    Y = ground; L.Arms = "fan"; L.Phase = (t / 6) % 2; L.EyeStyle = "sleep"; L.Mouth = "o";
                    if (t == 1) Chat(Pick(new[] { "phew, your PC is working hard", "it's getting hot in here", "*fans self*" }), 120);
                    if (t % 24 == 0) Bit(Sprite.OX + (rng.Next(2) == 0 ? 5 : 17), Sprite.OY + 1, (rng.NextDouble() - 0.5) * 0.2, 0.05, 40, Pal.Blue, 1, 0.03);
                    if (t >= 300) Set("idle", 60);
                    break;
                case "lowbatt":
                    Y = ground; L.EyeStyle = "sad"; L.Mouth = "frown"; L.Arms = "hold";
                    if (t == 1) Say("battery's low... plug me in?", 160);
                    if (t % 30 == 1) Float("bolt", Sprite.OX + 11, Sprite.OY - 6, 0, -0.08, 50, Pal.Red);
                    if (t >= 220) Set("idle", 60);
                    break;
                case "charged":
                    Y = ground; L.EyeStyle = "happy"; L.Mouth = "smile"; L.Arms = (t / 10) % 2 == 0 ? "upL" : "upR";
                    if (t == 1) Say("ahh, power!", 100);
                    if (t % 12 == 1) Float("bolt", Sprite.OX + 4 + rng.Next(14), Sprite.OY - 4, (rng.NextDouble() - 0.5) * 0.05, -0.12, 50, Pal.Spark);
                    if (t >= 140) Set("idle", 60);
                    break;
                case "typing":
                    {
                        Y = ground; L.Laptop = "open"; L.Glow = "blue";
                        int since = Tick - lastKeyTick;
                        if (since < 60)
                        {
                            L.EyeStyle = "down"; L.Eye = 0;
                            L.Arms = since < 10 ? (typeHand ? "typeL" : "typeR") : "down";
                            if (kp > 0 && rng.Next(3) == 0) Bit(rng.Next(2) == 0 ? Sprite.OX + 1 + rng.Next(3) : Sprite.OX + 18 + rng.Next(3), Sprite.OY + 5, (rng.NextDouble() - 0.5) * 0.15, -0.15, 28, Pal.Blue, 1, 0);
                        }
                        else if (since < 720)
                        {
                            if (since == 60 || since % 120 == 0) word = Pick(thinkWords);
                            L.EyeStyle = "up"; L.Eye = 1; L.Arms = "down";
                            Say(word + "...", 2); bub.Spinner = true;
                        }
                        else Set("idle", 60);
                        break;
                    }
                case "smash": Y = ground; StepSmash(); break;
                case "gun":
                    Y = ground;
                    if (t < 280) { AimAt(cur.X, cur.Y); L.EyeStyle = "angry"; }
                    else if (fx.Hits > hits0) { L.EyeStyle = "happy"; L.Mouth = "smile"; }
                    else L.Mouth = "o";
                    if (t == 1) { Say("hold still...", 90); hits0 = fx.Hits; }
                    if (t >= 60 && t <= 204 && (t - 60) % 36 == 0) FireGun(cur.X, cur.Y, false, 0, 0);
                    if (t == 280) { if (fx.Hits > hits0) Say("gotcha!", 100); else Say("dang it!", 100); needs.Fun += 4; }
                    if (t >= 360) Set("idle", 60);
                    break;
                case "trick": StepTrick(ground); break;
                case "gowatch":
                    {
                        Y = ground;
                        if (!videoOn) { Set("idle", 60); break; }
                        if (plat != IntPtr.Zero) { LeavePlatform(); Set("fall"); VY = 0; VX = 0; break; }
                        if (t == 1)
                        {
                            Chat("ooh, a video!", 90); resumed = false;
                            double lo = Math.Max(W.Work.Left + 80 * K, videoRect.Left + 60 * K), hi = Math.Min(W.Work.Right - 80 * K, videoRect.Right - 60 * K);
                            if (hi <= lo) { lo = W.Work.Left + 80 * K; hi = W.Work.Right - 80 * K; }
                            watchX = (X >= lo && X <= hi) ? X : lo + rng.NextDouble() * (hi - lo);
                        }
                        double dx = watchX - X;
                        if (Math.Abs(dx) > 8 * K) { Dir = dx > 0 ? 1 : -1; X += 1.2 * K * Dir; L.Eye = Dir; L.Phase = (Tick / 8) % 4; }
                        else { Set("watch"); nextComment = (int)(500 + rng.Next(400)); }
                        break;
                    }
                case "watch":
                    Y = ground;
                    if (!videoOn) { Chat("good show.", 100); Set("idle", 80); break; }
                    if (cfg.PauseTantrum && pausedPolls >= 2) { Set("paused"); break; }
                    StepWatch();
                    if (cfg.Commentary && cfg.Chatty > 0.05 && t >= nextComment)
                    {
                        string line = Comment(); Say(line, 160);
                        if (line == "lol" || line == "hehe") laughT = 48;
                        nextComment = t + (int)((660 + rng.Next(840)) / Math.Max(0.3, cfg.Chatty));
                    }
                    needs.Fun += 0.004;
                    break;
                case "paused":
                    Y = ground;
                    if (!videoOn) { Chat("fine, be that way.", 100); Set("idle", 80); break; }
                    if (pausedPolls < 2) { Say(Pick(resumeLines), 100); Set("watch"); resumed = true; nextComment = 600 + rng.Next(600); break; }
                    if (t < 480)
                    {
                        L.Eye = 0; L.EyeStyle = "angry"; L.Mouth = "o";
                        L.Bang = t < 90 && t % 16 < 10;
                        if (t < 180) { L.Arms = (t / 8) % 2 == 0 ? "up" : "down"; L.Phase = (t / 8) % 2 == 0 ? 1 : 3; L.Wob = t % 8 < 4 ? 1 : -1; if (t % 16 == 0) shake = 3; }
                        else L.Arms = "down";
                        if (t == 1 || t % 150 == 0) Say(Pick(complainLines), 120);
                    }
                    else
                    {
                        L.Back = true; L.Sit = true; L.Arms = "rest";
                        if (t % 440 == 0) Chat(Pick(sulkLines), 100);
                    }
                    break;
                case "suitup":
                    Y = ground; rot = t < 48 ? t * 7.5 : 0;
                    if (t == 24) { Poof(); suit = true; Say("suit up!", 80); }
                    if (t >= 60) { rot = 0; Set("swing"); }
                    break;
                case "swing": StepSwing(ground); break;
                case "heropose":
                    Y = ground; rot = 0;
                    if (t < 100) { L.Sit = true; L.Arms = travel > 0 ? "upR" : "upL"; }
                    if (t == 16) Chat(Pick(new[] { "nailed it.", "stuck the landing", "your friendly neighbourhood " + SpeciesList.Current.ShortName }), 110);
                    if (t == 140) { Poof(); suit = false; }
                    if (t >= 180) Set("idle", 60);
                    break;
                case "switch":       // front flip + poof, and a different pet lands
                    {
                        if (t == 1) { jv = 5.4 * K; jy = 0; air = true; Say("ta-da!", 50); }
                        if (air) { jy += jv; jv -= G; }
                        rot = Math.Min(360, t * 360.0 / 50) * Dir;
                        L.Arms = "up"; L.EyeStyle = "happy";
                        if (t == 25 && pending != null) { Poof(); Poof(); SpeciesList.Current = pending; pending = null; }
                        if (air && jy <= 0 && t > 2) { jy = 0; air = false; rot = 0; squashT = 14; tuftT = 18; Say(SpeciesList.Current.Hello, 120); Set("idle", 90); }
                        Y = ground - jy;
                        break;
                    }
                case "flip":         // Flippy's thing: a front flip forward, landing with a squash and a bouncy tuft
                    {
                        if (t == 1) Chat(SpeciesList.Current.SignatureLine, 80);
                        if (t < 18) { L.Sit = true; L.EyeStyle = "happy"; Y = ground; break; }
                        if (t == 18) { jv = 5.6 * K; jy = 0; air = true; squashT = 6; }
                        if (air)
                        {
                            jy += jv; jv -= G; X += 1.5 * K * Dir;
                            rot = Math.Min(360, (t - 18) * 360.0 / 52) * Dir; L.Arms = "up"; L.EyeStyle = "happy";
                            if (jy <= 0 && t > 20) { jy = 0; air = false; rot = 0; squashT = 14; tuftT = 18; needs.Fun += 3; }
                        }
                        else { L.EyeStyle = "happy"; L.Mouth = "smile"; if (t > 140) Set("idle", 60); }
                        Y = ground - jy;
                        break;
                    }
                default: Set("idle", 60); break;
            }
        }
        string heldKind = "cookie";

        // ---------------- gun ----------------
        void AimAt(double tx, double ty)
        {
            int side = tx >= X ? 1 : -1;
            L.AimSide = side;
            double shx = CX(side < 0 ? Sprite.OX + 3 : Sprite.OX + 19), shy = CY(Sprite.OY + 7);
            double deg = Math.Atan2(ty - shy, tx - shx) * 180 / Math.PI;
            double m = side > 0 ? deg : 180 - deg;
            while (m > 180) m -= 360; while (m <= -180) m += 360;
            m = Math.Max(-115, Math.Min(40, m));
            L.AimDeg = side > 0 ? m : 180 - m;
            L.Eye = side; L.Arms = "aim"; L.Gun = true;
        }
        int flashT, recoilT;
        void FireGun(double tx, double ty, bool viaFloor, double vx, double vy)
        {
            double shx, shy, hx, hy, c, s;
            Sprite.Aim(L, Sprite.OX, Sprite.OY, out shx, out shy, out hx, out hy, out c, out s);
            double mv = L.AimSide < 0 ? 2.5 : -2.5;
            double mx = hx + c * 9 - s * mv, my = hy + s * 9 + c * mv;
            // account for his current rotation around his middle (trickshots)
            double sx = CX(mx), sy = CY(my);
            if (rot != 0)
            {
                double ccx = X, ccy = Y - 8 * S, a = rot * Math.PI / 180, dx = sx - ccx, dy = sy - ccy;
                sx = ccx + dx * Math.Cos(a) - dy * Math.Sin(a); sy = ccy + dx * Math.Sin(a) + dy * Math.Cos(a);
            }
            fx.Fire(sx, sy, tx, ty, viaFloor, vx, vy, K);
            flashT = 6; recoilT = 6;
            Bit(hx, hy - 1, -0.25 * L.AimSide, -0.8, 44, Pal.Spark, 1, 0.04, true);
        }

        void StepTrick(double ground)
        {
            int t = T, resultT = 220;
            if (t == 1)
            {
                trickKind = rng.Next(4); hits0 = fx.Hits; jy = 0; jv = 0; air = false;
                Say(trickKind <= 1 ? "360 no-scope. watch." : (trickKind == 2 ? "don't even need to look" : "bank shot. watch this."), 100);
            }
            if (t < resultT)
            {
                if (trickKind <= 1)
                {
                    if (t < 44) { L.Sit = true; L.EyeStyle = "happy"; }
                    if (t == 44) { jv = 7.6 * K; air = true; squashT = 6; }
                    if (air) { jy += jv; jv -= G; if (jy <= 0) { jy = 0; air = false; squashT = 14; } }
                    AimAt(cur.X, cur.Y);
                    if (t >= 46 && t < 82) { rot = (t - 46) * 10; L.EyeStyle = "happy"; } else { rot = 0; if (t >= 82) L.EyeStyle = "angry"; }
                    if (t == 82) FireGun(cur.X, cur.Y, false, 0, 0);
                }
                else if (trickKind == 2)
                {
                    AimAt(cur.X, cur.Y); L.Eye = 0; L.EyeStyle = "sleep"; L.Mouth = "smile";
                    if (t == 90) FireGun(cur.X, cur.Y, false, 0, 0);
                }
                else
                {
                    double fxp = (X + cur.X) / 2, fyp = Y - 2;
                    AimAt(fxp, fyp); L.EyeStyle = "angry";
                    if (t == 90) FireGun(cur.X, cur.Y, true, fxp, fyp);
                }
            }
            else if (t == resultT)
            {
                if (fx.Hits > hits0)
                {
                    Say("TRICKSHOT!!", 140); needs.Fun += 8;
                    for (int i = 0; i < 8; i++) Float("star", Sprite.OX + 2 + rng.Next(18), Sprite.OY - 2 - rng.Next(6), (rng.NextDouble() - 0.5) * 0.1, -0.15, 70, Pal.Spark);
                }
                else Say(Pick(new[] { "meant to do that.", "that counts.", "wind, obviously.", "lag." }), 120);
            }
            else
            {
                L.EyeStyle = fx.Hits > hits0 ? "happy" : "normal"; if (fx.Hits > hits0) L.Mouth = "smile";
                if (t >= resultT + 120) Set("idle", 60);
            }
            if (flashT > 0) L.Flash = true;
            if (recoilT > 0) L.Recoil = true;
            Y = ground - jy;
        }

        // ---------------- web-slinging ----------------
        void NewAnchor()
        {
            double px = X, py = Y - 8 * S;
            double cdx2 = cur.X - px;
            if (cur.Y < py - 120 * K && cur.Y > W.Work.Top && Math.Abs(cdx2) < 700 * K && cdx2 * travel > -150 * K && rng.Next(2) == 0)
            {
                anchorCursor = true; ax = cur.X; ay = cur.Y;
                if (rng.Next(2) == 0) Chat("thanks for the hand!", 80);
            }
            else
            {
                anchorCursor = false;
                double wallX = travel > 0 ? W.Work.Right - 2 : W.Work.Left + 2;
                if (Math.Abs(wallX - px) < 380 * K) { ax = wallX; ay = Math.Max(W.Work.Top + 20, py - 280 * K); travel = -travel; }
                else { ax = Math.Max(W.Work.Left + 20, Math.Min(W.Work.Right - 20, px + travel * (240 + rng.Next(220)) * K)); ay = W.Work.Top + 2; }
            }
            double dx = px - ax, dy = py - ay;
            ropeL = Math.Sqrt(dx * dx + dy * dy); ropeTarget = Math.Min(ropeL * 0.85, 420 * K); anchT = 0; anchored = true;
        }
        void StepSwing(double ground)
        {
            int t = T;
            // physics on his middle point
            double px = X, py = Y - 8 * S;
            if (t == 1)
            {
                LeavePlatform(); VX = 0; VY = 0; swings = 0; maxSwings = 4 + rng.Next(4); flip = 0;
                travel = X < (W.Work.Left + W.Work.Right) / 2 ? 1 : -1; anchored = false; freeT = 99;
                Say("THWIP!", 70);
            }
            if (!anchored && swings < maxSwings && freeT >= 12 && VY >= -1 * K) NewAnchor();
            freeT++;
            VY += G;
            if (anchored && anchorCursor) { ax = cur.X; ay = cur.Y; }
            px += VX; py += VY;
            if (anchored)
            {
                ropeL = Math.Max(ropeTarget, ropeL - 11 * K);
                double dx = px - ax, dy = py - ay, d = Math.Sqrt(dx * dx + dy * dy);
                if (d > ropeL && d > 0)
                {
                    double nx = dx / d, ny = dy / d;
                    px = ax + nx * ropeL; py = ay + ny * ropeL;
                    double vr = VX * nx + VY * ny;
                    if (vr > 0) { VX -= vr * nx; VY -= vr * ny; }
                }
                anchT++;
                bool forward = (px - ax) * travel > 0;
                if ((anchT > 36 && VX * travel > 1 * K && VY < 0 && forward) || anchT > 280)
                {
                    anchored = false; freeT = 0; swings++; VX *= 1.1; VY -= 1.5 * K;
                    if (rng.Next(3) == 0) flip = 36;
                    if (rng.Next(3) == 0) Chat(Pick(new[] { "thwip!", "wheee!", "whoo!", "parkour!" }), 70);
                }
            }
            if (px < W.Work.Left + 40 * K) { px = W.Work.Left + 40 * K; VX = Math.Abs(VX) * 0.4; travel = 1; }
            if (px > W.Work.Right - 40 * K) { px = W.Work.Right - 40 * K; VX = -Math.Abs(VX) * 0.4; travel = -1; }
            if (py < W.Work.Top + 60 * K) { py = W.Work.Top + 60 * K; VY = Math.Abs(VY) * 0.3; }

            if (anchored) rot = Math.Atan2(py - ay, px - ax) * 180 / Math.PI - 90;
            else if (flip > 0) { rot = (36 - flip) * 10 * travel; flip--; }
            else rot = Math.Max(-35, Math.Min(35, VX * 3));
            L.Arms = "up";

            if (anchored)
            {
                double a = rot * Math.PI / 180;
                fx.Web(px + Math.Sin(a) * 11 * S, py - Math.Cos(a) * 11 * S, ax, ay, S);
            }
            else fx.WebOff();

            X = px; Y = py + 8 * S;
            if ((!anchored && VY > 0 && Y >= ground) || t > 3000)
            {
                fx.WebOff(); Y = ground; rot = 0; squashT = 14; needs.Fun += 6; Set("heropose");
            }
        }

        // ---------------- movie night ----------------
        void StepWatch()
        {
            L.Back = true; L.Sit = true;
            if (T == 1 || T % rimEvery == 0)
            {
                int[] rims = { Pal.C(165, 212, 255), Pal.C(236, 242, 255), Pal.C(255, 212, 165), Pal.C(185, 255, 215), Pal.C(255, 178, 220), Pal.C(200, 190, 255) };
                L.Rim = Pick(rims); rimEvery = 40 + rng.Next(100);
            }
            int c = T % 200;
            L.Arms = c >= 120 && c < 140 ? "reach" : (c >= 140 && c < 176 ? "eat" : "rest");
            if (c == 160) for (int i = 0; i < 3; i++) Bit(Sprite.OX + 16 + rng.Next(3), Sprite.OY + 4, (rng.NextDouble() - 0.5) * 0.3, -0.3, 50, Pal.Kernel, 1, 0.04, true);
            if (c == 60 && rng.Next(3) == 0) Bit(Sprite.OX + 24, Sprite.OY + 5, -0.1, -1.15, 60, Pal.Kernel, 1, 0.04, true);
            if (T == 1 && !resumed) Chat("popcorn time!", 100);
            if (laughT > 0) { laughT--; L.Wob = laughT % 8 < 4 ? 1 : -1; }
        }
        string Comment()
        {
            int r = rng.Next(100);
            string title = videoTitle;
            title = System.Text.RegularExpressions.Regex.Replace(title, @"^\(\d+\)\s*", "");
            title = System.Text.RegularExpressions.Regex.Split(title, @" - | \| ")[0];
            string shortT = "";
            foreach (string wd in title.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) { if ((shortT + " " + wd).Trim().Length > 16) break; shortT = (shortT + " " + wd).Trim(); }
            if (r < 18 && shortT.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(shortT, "^(YouTube|Netflix|Twitch|Prime Video)$"))
                return Pick(new[] { "I love " + shortT, shortT + "? classic", "ah, " + shortT, "more " + shortT + " pls" });
            if (r < 40)
            {
                if (videoTitle.Contains("YouTube")) return Pick(new[] { "like and subscribe!", "skip the ad...", "the algorithm gets me", "read the comments!" });
                if (videoTitle.Contains("Netflix") || videoTitle.Contains("Prime Video")) return Pick(new[] { "one more episode?", "yes, still watching.", "skip intro!" });
                if (videoTitle.Contains("Twitch")) return Pick(new[] { "chat is wild", "pog", "gg" });
            }
            return Pick(commentLines);
        }

        // ---------------- files dropped on him ----------------
        static string FileComment(string[] files)
        {
            if (files == null || files.Length == 0) return "nom";
            if (files.Length > 3) return "a whole buffet?!";
            string f = files[0];
            if (Directory.Exists(f)) return "a whole folder?!";
            string ext = Path.GetExtension(f).ToLowerInvariant();
            switch (ext)
            {
                case ".png": case ".jpg": case ".jpeg": case ".gif": case ".webp": case ".bmp": return "ooh, pretty picture";
                case ".pdf": return "a pdf... very official";
                case ".exe": case ".msi": return "I'm NOT running that";
                case ".zip": case ".rar": case ".7z": return "a present!";
                case ".mp3": case ".wav": case ".flac": return "music! can I hear it?";
                case ".mp4": case ".mkv": case ".mov": return "movie night?";
                case ".txt": case ".md": return "reading material";
                case ".ps1": case ".cs": case ".py": case ".js": case ".ts": case ".java": case ".cpp": return "code! my favourite snack";
                case ".docx": case ".doc": return "homework?";
                case ".xlsx": case ".csv": return "numbers... yum?";
                case ".pptx": return "a presentation! good luck";
                default: return "nom nom nom";
            }
        }

        // ---------------- laptop smash ----------------
        void StepSmash()
        {
            int t = T / 2;      // the sequence was authored at 30 steps per second
            bool step = T % 2 == 0;
            L.Eye = 0;
            if (t < 15) { L.Laptop = "open"; L.Glow = "blue"; L.EyeStyle = "down"; }
            else if (t < 110)
            {
                L.Laptop = "open"; L.Glow = "blue"; L.EyeStyle = "down";
                L.Arms = (t / 3) % 2 == 0 ? "typeL" : "typeR";
                if (step && t % 9 == 0) Bit(rng.Next(2) == 0 ? Sprite.OX + 1 + rng.Next(3) : Sprite.OX + 18 + rng.Next(3), Sprite.OY + 5, (rng.NextDouble() - 0.5) * 0.15, -0.15, 28, Pal.Blue, 1, 0);
            }
            else if (t < 160) { L.Laptop = "open"; L.Glow = "red"; L.EyeStyle = "angry"; L.Arms = t % 2 == 0 ? "typeL" : "typeR"; L.Bang = t % 8 < 5; }
            else if (t < 180) { L.Laptop = "open"; L.Glow = "red"; L.EyeStyle = "angry"; L.Bang = true; }
            else if (t < 194) { L.Laptop = "held"; L.Arms = "up"; L.EyeStyle = "angry"; L.LapRow = (Sprite.OY + 9) - (T / 2.0 - 180) / 14.0 * 14; }
            else if (t < 212) { L.Laptop = "held"; L.Arms = "up"; L.EyeStyle = "angry"; L.LapRow = t % 4 < 2 ? Sprite.OY - 5 : Sprite.OY - 6; }
            else if (t < 216) { L.Laptop = "held"; L.Arms = "down"; L.EyeStyle = "angry"; L.LapRow = (Sprite.OY - 6) + (T / 2.0 - 212) / 4.0 * 18; }
            else if (t == 216)
            {
                L.Arms = "down"; L.EyeStyle = "angry";
                if (step)
                {
                    shake = 24; squashT = 10;
                    int[] pool = { Pal.Silver, Pal.Silver, Pal.Dark, Pal.Dark, Pal.Blue, Pal.Spark, Pal.Spark, Pal.White };
                    for (int i = 0; i < 36; i++)
                    {
                        int col = Pick(pool); bool spark = col == Pal.Spark;
                        Bit(Sprite.OX + 11 + (rng.NextDouble() - 0.5) * 10, Sprite.OY + 13, (rng.NextDouble() - 0.5) * 1.8, -0.5 - rng.NextDouble() * 1.3,
                            spark ? 40 + rng.Next(30) : 400 + rng.Next(80), col, spark ? 1 : 2, 0.07, !spark);
                    }
                }
            }
            else if (t < 250) { L.Arms = "down"; L.EyeStyle = "angry"; }
            else if (t < 310) { L.EyeStyle = "happy"; L.Mouth = "smile"; if (t == 250 && step) Say("fixed it.", 110); }
            else if (t >= 330) { Set("walk"); needs.Fun += 5; }
        }
    }
}
