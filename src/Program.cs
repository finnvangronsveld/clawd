// Flippy entry point: single instance, the 60 Hz loop, shortcuts, settings window.
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Flippy")]
[assembly: AssemblyProduct("Flippy")]
[assembly: AssemblyDescription("A little desktop buddy")]
[assembly: AssemblyVersion("2.3.0.0")]
[assembly: AssemblyFileVersion("2.3.0.0")]

namespace Flippy
{
    static class Program
    {
        public const string Version = "2.3";
        static Pet pet;
        static Settings cfg;
        static Needs needs;
        static Form settingsForm;

        [STAThread]
        static int Main(string[] args)
        {
            Native.MakeDpiAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length >= 2 && args[0] == "--render-frames") { return Demo.Render(args[1]); }
            if (args.Length >= 2 && args[0] == "--make-icon") { return DevTools.MakeIcon(args[1]); }
            if (args.Length >= 2 && args[0] == "--dump-sprites") { return DevTools.DumpSprites(args[1], args.Length >= 4 && args[2] == "--species" ? args[3] : null); }

            // test switches (for development): --test runs a second instance, --mode X forces a behaviour, --verbose logs
            bool test = Array.IndexOf(args, "--test") >= 0;
            Verbose = Array.IndexOf(args, "--verbose") >= 0;
            int mi = Array.IndexOf(args, "--mode"); string forceMode = mi >= 0 && mi + 1 < args.Length ? args[mi + 1] : null;
            string suffix = test ? "Test" : "";
            if (test)
            {
                LogFile = "flippy-test.log";
                // test instances never touch the real settings: --data <dir>, or a temp folder
                int di = Array.IndexOf(args, "--data");
                Store.Dir = di >= 0 && di + 1 < args.Length ? args[di + 1] : Path.Combine(Path.GetTempPath(), "FlippyTestData");
            }

            bool created;
            Mutex mutex = new Mutex(true, @"Local\FlippyDesktopPet" + suffix, out created);
            if (!created)
            {
                try { EventWaitHandle.OpenExisting(@"Local\FlippyPoke" + suffix).Set(); } catch { }
                return 0;
            }
            EventWaitHandle poke = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\FlippyPoke" + suffix);
            // coming from the old name: settings across, old copy stopped (test runs only migrate a fake --old-data folder)
            int od = Array.IndexOf(args, "--old-data");
            if (test) { if (od >= 0 && od + 1 < args.Length) Migration.CopySettings(Store.Dir, args[od + 1]); }
            else { Migration.StopOldInstance(); Migration.CopySettings(Store.Dir, Migration.OldDataDir); }
            Native.timeBeginPeriod(1);

            cfg = new Settings(); cfg.Load();
            needs = new Needs(); needs.Load();
            int pi = Array.IndexOf(args, "--pet"); if (pi >= 0 && pi + 1 < args.Length) cfg.Pet = SpeciesList.Get(args[pi + 1]).Id;
            SpeciesList.Current = SpeciesList.Get(cfg.Pet);
            if (Verbose) Log("pet " + SpeciesList.Current.Id + " (settings said " + cfg.Pet + ")");
            if (!test)
                try
                {
                    Migration.Shortcuts(Path.GetDirectoryName(StartMenuLink), Path.GetDirectoryName(StartupLink), p => EnsureShortcut(p, true), LinkName);
                    EnsureShortcut(StartMenuLink, false);
                    if (File.Exists(StartupLink)) EnsureShortcut(StartupLink, false);
                }
                catch { }

            pet = new Pet(cfg, needs);
            if (test) pet.TestX = Array.IndexOf(args, "--x") >= 0 ? int.Parse(args[Array.IndexOf(args, "--x") + 1]) : -1;
            pet.ForceMode = forceMode;
            pet.OpenSettings = ShowSettings;
            pet.Quit = () => { pet.SaveAll(); Application.Exit(); };

            // fixed 60 Hz simulation, drawn once per timer tick
            Stopwatch clock = Stopwatch.StartNew();
            double acc = 0, last = 0, step = 1000.0 / 60;
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 15;
            timer.Tick += (s, e) =>
            {
                try
                {
                    double now = clock.Elapsed.TotalMilliseconds;
                    acc += now - last; last = now;
                    if (acc > step * 5) acc = step * 5;
                    bool stepped = false;
                    while (acc >= step) { pet.Step(); acc -= step; stepped = true; }
                    if (poke.WaitOne(0)) pet.Poked();
                    if (stepped) pet.Draw();
                }
                catch (Exception ex) { Log(ex.ToString()); }
            };
            timer.Start();
            Application.ApplicationExit += (s, e) => { try { pet.SaveAll(); } catch { } };
            Application.Run();
            GC.KeepAlive(mutex);
            return 0;
        }

        static int logged;
        public static bool Verbose;
        public static string LogFile = "flippy.log";
        public static void Log(string s)
        {
            if (logged++ > (Verbose ? 5000 : 20)) return;
            try { Directory.CreateDirectory(Store.Dir); File.AppendAllText(Path.Combine(Store.Dir, LogFile), DateTime.Now.ToString("s") + " " + s + Environment.NewLine); } catch { }
        }

        static void ShowSettings()
        {
            if (settingsForm != null && !settingsForm.IsDisposed) { settingsForm.Activate(); return; }
            settingsForm = cfg.BuildForm(() => File.Exists(StartupLink), on => { try { if (on) EnsureShortcut(StartupLink, true); else File.Delete(StartupLink); } catch { } }, () => needs.Reset(), id => pet.SwitchTo(SpeciesList.Get(id)));
            settingsForm.Show();
        }

        // ---------- shortcuts ----------
        const string LinkName = "Flippy.lnk";
        static string StartMenuLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), LinkName); } }
        static string StartupLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), LinkName); } }
        public static void EnsureShortcut(string path, bool force)
        {
            string exe = Application.ExecutablePath;
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object sh = Activator.CreateInstance(t);
            object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { path });
            Type lt = lnk.GetType();
            string current = (string)lt.InvokeMember("TargetPath", BindingFlags.GetProperty, null, lnk, null);
            if (!force && File.Exists(path) && string.Equals(current, exe, StringComparison.OrdinalIgnoreCase)) return;
            lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { exe });
            lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { Path.GetDirectoryName(exe) });
            lt.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { "Flippy, your little desktop buddy" });
            lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk, new object[] { exe + ",0" });
            lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
        }
    }
}
