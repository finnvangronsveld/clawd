// Moving over from the app's old name (it used to be called Clawd): the only place the old name lives.
//  - settings + needs are copied from the old settings folder once
//  - old shortcuts are removed (autostart survives as the new shortcut)
//  - an old instance that's still running is stopped, so you don't end up with two pets
using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading;

namespace Flippy
{
    static class Migration
    {
        const string OldName = "Clawd";
        public static readonly string OldDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), OldName);
        public const string OldLink = OldName + ".lnk";

        // first run under the new name: bring the old settings and hunger levels across (the old folder is left alone)
        public static bool CopySettings(string newDir, string oldDir)
        {
            try
            {
                if (Directory.Exists(newDir) || !Directory.Exists(oldDir)) return false;
                Directory.CreateDirectory(newDir);
                foreach (string f in new[] { "settings.ini", "needs.ini" })
                {
                    string src = Path.Combine(oldDir, f);
                    if (File.Exists(src)) File.Copy(src, Path.Combine(newDir, f));
                }
                Program.Log("migrated settings from " + oldDir);
                return true;
            }
            catch (Exception ex) { Program.Log("settings migration failed: " + ex.Message); return false; }
        }

        // an old pet still running (v2 exe, or the v1 PowerShell script)? close it
        public static void StopOldInstance()
        {
            try
            {
                Mutex m;
                bool running = Mutex.TryOpenExisting(@"Local\" + OldName + "DesktopPet", out m);
                if (m != null) m.Dispose();
                if (!running) return;
                foreach (Process p in Process.GetProcessesByName(OldName)) { try { p.Kill(); } catch { } }
                using (ManagementObjectSearcher q = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'powershell.exe'"))
                    foreach (ManagementObject o in q.Get())
                    {
                        string cl = (o["CommandLine"] as string) ?? "";
                        if (cl.IndexOf(OldName + ".ps1", StringComparison.OrdinalIgnoreCase) >= 0)
                            try { Process.GetProcessById(Convert.ToInt32(o["ProcessId"])).Kill(); } catch { }
                    }
                Program.Log("stopped an old instance");
            }
            catch (Exception ex) { Program.Log("could not stop old instance: " + ex.Message); }
        }

        // old shortcuts out; if he used to start with Windows, keep that under the new name
        public static void Shortcuts(string startMenuDir, string startupDir, Action<string> makeNewLink, string newLinkName)
        {
            try
            {
                string oldMenu = Path.Combine(startMenuDir, OldLink), oldStartup = Path.Combine(startupDir, OldLink);
                if (File.Exists(oldMenu)) File.Delete(oldMenu);
                if (File.Exists(oldStartup)) { File.Delete(oldStartup); makeNewLink(Path.Combine(startupDir, newLinkName)); }
            }
            catch (Exception ex) { Program.Log("shortcut migration failed: " + ex.Message); }
        }
    }
}
