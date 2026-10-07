// Settings + needs, saved as simple key=value files in %APPDATA%\Clawd. Nothing leaves the PC.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace Clawd
{
    static class Store
    {
        public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clawd");
        public static Dictionary<string, string> Load(string name)
        {
            var d = new Dictionary<string, string>();
            try
            {
                string p = Path.Combine(Dir, name);
                if (!File.Exists(p)) return d;
                foreach (string line in File.ReadAllLines(p))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }
            }
            catch { }
            return d;
        }
        public static void Save(string name, Dictionary<string, string> d)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var lines = new List<string>();
                foreach (var kv in d) lines.Add(kv.Key + "=" + kv.Value);
                File.WriteAllLines(Path.Combine(Dir, name), lines.ToArray());
            }
            catch { }
        }
        public static double D(Dictionary<string, string> d, string k, double def)
        {
            string v; double r;
            return d.TryGetValue(k, out v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out r) ? r : def;
        }
        public static bool B(Dictionary<string, string> d, string k, bool def) { string v; return d.TryGetValue(k, out v) ? v == "1" : def; }
        public static string F(double v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
    }

    class Settings
    {
        // features
        public bool Gun = true, Tricks = true, WebSwing = true, Smash = true, MovieNight = true, PauseTantrum = true,
                    Commentary = true, TypeAlong = true, Climbing = true, Needs = true, PcReactions = true, Music = true;
        // sliders
        public double Size = 1.0;        // 0.5 .. 2
        public double Activity = 1.0;    // 0.3 (calm) .. 2 (hyper)
        public double Chatty = 1.0;      // 0 (quiet) .. 2
        public event Action Changed;

        static readonly string[] keys = { "Gun", "Tricks", "WebSwing", "Smash", "MovieNight", "PauseTantrum", "Commentary", "TypeAlong", "Climbing", "Needs", "PcReactions", "Music" };
        bool Get(string k)
        {
            switch (k) { case "Gun": return Gun; case "Tricks": return Tricks; case "WebSwing": return WebSwing; case "Smash": return Smash; case "MovieNight": return MovieNight;
                case "PauseTantrum": return PauseTantrum; case "Commentary": return Commentary; case "TypeAlong": return TypeAlong; case "Climbing": return Climbing;
                case "Needs": return Needs; case "PcReactions": return PcReactions; default: return Music; }
        }
        void Set(string k, bool v)
        {
            switch (k) { case "Gun": Gun = v; break; case "Tricks": Tricks = v; break; case "WebSwing": WebSwing = v; break; case "Smash": Smash = v; break;
                case "MovieNight": MovieNight = v; break; case "PauseTantrum": PauseTantrum = v; break; case "Commentary": Commentary = v; break;
                case "TypeAlong": TypeAlong = v; break; case "Climbing": Climbing = v; break; case "Needs": Needs = v; break; case "PcReactions": PcReactions = v; break; default: Music = v; break; }
        }
        public void Load()
        {
            var d = Store.Load("settings.ini");
            foreach (string k in keys) Set(k, Store.B(d, k, Get(k)));
            Size = Math.Max(0.5, Math.Min(2, Store.D(d, "Size", 1)));
            Activity = Math.Max(0.3, Math.Min(2, Store.D(d, "Activity", 1)));
            Chatty = Math.Max(0, Math.Min(2, Store.D(d, "Chatty", 1)));
        }
        public void Save()
        {
            var d = new Dictionary<string, string>();
            foreach (string k in keys) d[k] = Get(k) ? "1" : "0";
            d["Size"] = Store.F(Size); d["Activity"] = Store.F(Activity); d["Chatty"] = Store.F(Chatty);
            Store.Save("settings.ini", d);
            if (Changed != null) Changed();
        }

        // ---- the settings window ----
        public static readonly string[][] Labels = {
            new[] { "TypeAlong", "Types along when you type" }, new[] { "MovieNight", "Movie night (watches videos with you)" },
            new[] { "PauseTantrum", "Gets mad when you pause the video" }, new[] { "Commentary", "Comments while watching" },
            new[] { "Music", "Bops along to your music" }, new[] { "Climbing", "Climbs onto your windows" },
            new[] { "Needs", "Gets hungry, tired, bored (and needs love)" }, new[] { "PcReactions", "Reacts to your PC (battery, late nights, copying, busy CPU)" },
            new[] { "Gun", "Pellet gun" }, new[] { "Tricks", "Trickshots" }, new[] { "WebSwing", "Web-slinger suit" }, new[] { "Smash", "Smashes his laptop" } };

        public Form BuildForm(Func<bool> getStartup, Action<bool> setStartup, Action resetNeeds)
        {
            Form f = new Form();
            f.Text = "Clawd settings"; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MaximizeBox = false; f.MinimizeBox = false;
            f.StartPosition = FormStartPosition.CenterScreen; f.BackColor = Color.FromArgb(252, 248, 244); f.AutoScaleMode = AutoScaleMode.Dpi;
            f.Font = new Font("Segoe UI", 9.75f); f.ClientSize = new Size(440, 640); f.ShowIcon = true;
            try { f.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Panel head = new Panel(); head.Dock = DockStyle.Top; head.Height = 64; head.BackColor = Color.FromArgb(SpeciesList.Current.Accent);
            PictureBox pic = new PictureBox(); pic.Image = Sprite.IconArt(48); pic.SizeMode = PictureBoxSizeMode.Zoom; pic.SetBounds(14, 8, 48, 48); pic.BackColor = Color.Transparent;
            Label title = new Label(); title.Text = SpeciesList.Current.ShortName; title.Font = new Font("Segoe UI Semibold", 16f); title.ForeColor = Color.White; title.AutoSize = true; title.Location = new Point(70, 8);
            Label sub = new Label(); sub.Text = SpeciesList.Current.Tagline; sub.ForeColor = Color.FromArgb(255, 235, 225); sub.AutoSize = true; sub.Location = new Point(73, 38);
            head.Controls.Add(pic); head.Controls.Add(title); head.Controls.Add(sub);

            FlowLayoutPanel body = new FlowLayoutPanel(); body.Dock = DockStyle.Fill; body.FlowDirection = FlowDirection.TopDown; body.WrapContents = false;
            body.Padding = new Padding(18, 12, 18, 12); body.AutoScroll = true;

            Func<string, Label> section = t => { Label l = new Label(); l.Text = t; l.Font = new Font("Segoe UI Semibold", 10.5f); l.ForeColor = Color.FromArgb(SpeciesList.Current.Shade); l.AutoSize = true; l.Margin = new Padding(0, 10, 0, 4); return l; };
            body.Controls.Add(section("What he does"));
            foreach (string[] lab in Labels)
            {
                CheckBox cb = new CheckBox(); cb.Text = lab[1]; cb.AutoSize = true; cb.Checked = Get(lab[0]); cb.Margin = new Padding(2, 2, 0, 2);
                string key = lab[0];
                cb.CheckedChanged += (s, e) => { Set(key, ((CheckBox)s).Checked); Save(); };
                body.Controls.Add(cb);
            }
            body.Controls.Add(section("Personality"));
            body.Controls.Add(Slider("Size", 50, 200, (int)(Size * 100), v => { Size = v / 100.0; Save(); }, "small", "big"));
            body.Controls.Add(Slider("Activity", 30, 200, (int)(Activity * 100), v => { Activity = v / 100.0; Save(); }, "calm", "hyper"));
            body.Controls.Add(Slider("Chattiness", 0, 200, (int)(Chatty * 100), v => { Chatty = v / 100.0; Save(); }, "quiet", "chatty"));

            body.Controls.Add(section("General"));
            CheckBox st = new CheckBox(); st.Text = "Start with Windows"; st.AutoSize = true; st.Checked = getStartup();
            st.CheckedChanged += (s, e) => setStartup(((CheckBox)s).Checked);
            body.Controls.Add(st);
            Button reset = new Button(); reset.Text = "Reset his needs (full belly, rested, happy)"; reset.AutoSize = true; reset.Margin = new Padding(2, 8, 0, 0);
            reset.FlatStyle = FlatStyle.System; reset.Click += (s, e) => resetNeeds();
            body.Controls.Add(reset);
            Label note = new Label(); note.AutoSize = true; note.MaximumSize = new Size(390, 0); note.ForeColor = Color.Gray; note.Margin = new Padding(2, 14, 0, 0);
            note.Text = "Everything stays on this PC. He only counts key presses (never which keys), reads the title of the window in front to notice videos, and reads play/pause from Windows' media controls.";
            body.Controls.Add(note);

            f.Controls.Add(body); f.Controls.Add(head);
            return f;
        }
        static Control Slider(string name, int min, int max, int val, Action<int> changed, string lo, string hi)
        {
            TableLayoutPanel p = new TableLayoutPanel(); p.ColumnCount = 4; p.RowCount = 1; p.AutoSize = true; p.Margin = new Padding(0, 2, 0, 2);
            Label l = new Label(); l.Text = name; l.Width = 80; l.TextAlign = ContentAlignment.MiddleLeft; l.Anchor = AnchorStyles.Left;
            Label a = new Label(); a.Text = lo; a.AutoSize = true; a.ForeColor = Color.Gray; a.Anchor = AnchorStyles.Left;
            TrackBar t = new TrackBar(); t.Minimum = min; t.Maximum = max; t.Value = Math.Max(min, Math.Min(max, val)); t.TickStyle = TickStyle.None; t.Width = 190; t.AutoSize = false; t.Height = 30;
            Label b = new Label(); b.Text = hi; b.AutoSize = true; b.ForeColor = Color.Gray; b.Anchor = AnchorStyles.Left;
            t.ValueChanged += (s, e) => changed(((TrackBar)s).Value);
            p.Controls.Add(l, 0, 0); p.Controls.Add(a, 1, 0); p.Controls.Add(t, 2, 0); p.Controls.Add(b, 3, 0);
            return p;
        }
    }

    // How he's feeling. 100 = great, 0 = desperate. Slowly drains; you fill it up by caring for him.
    class Needs
    {
        public double Fullness = 80, Energy = 80, Fun = 70, Love = 70;
        public DateTime LastSave = DateTime.Now;
        public void Load()
        {
            var d = Store.Load("needs.ini");
            Fullness = Store.D(d, "Fullness", 80); Energy = Store.D(d, "Energy", 80); Fun = Store.D(d, "Fun", 70); Love = Store.D(d, "Love", 70);
            double ticks = Store.D(d, "Saved", 0);
            if (ticks > 0)
            {
                // time passed while he was off: he got a bit hungry but caught up on sleep
                double hours = Math.Max(0, Math.Min(24, (DateTime.Now - new DateTime((long)ticks)).TotalHours));
                Fullness -= hours * 4; Energy += hours * 15; Fun -= hours * 2;
            }
            Clamp();
        }
        public void Save()
        {
            var d = new Dictionary<string, string>();
            d["Fullness"] = Store.F(Fullness); d["Energy"] = Store.F(Energy); d["Fun"] = Store.F(Fun); d["Love"] = Store.F(Love);
            d["Saved"] = DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture);
            Store.Save("needs.ini", d); LastSave = DateTime.Now;
        }
        public void Clamp()
        {
            Fullness = Math.Max(0, Math.Min(100, Fullness)); Energy = Math.Max(0, Math.Min(100, Energy));
            Fun = Math.Max(0, Math.Min(100, Fun)); Love = Math.Max(0, Math.Min(100, Love));
        }
        public void Reset() { Fullness = Energy = 100; Fun = Love = 90; Save(); }
        // once a second
        public void Second(bool sleeping, bool night)
        {
            Fullness -= 100.0 / (90 * 60);                    // hungry again after ~1.5 h
            if (sleeping) Energy += 100.0 / (20 * 60);         // a 20-minute nap fills him up
            else Energy -= (night ? 2.0 : 1.0) * 100.0 / (180 * 60);
            Fun -= 100.0 / (60 * 60);
            Love -= 100.0 / (240 * 60);
            Clamp();
            if ((DateTime.Now - LastSave).TotalSeconds > 60) Save();
        }
    }
}
