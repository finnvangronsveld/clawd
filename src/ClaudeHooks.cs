// Claude Code alerts. Claude Code can run a command when it finishes an answer (Stop), when it needs your OK
// (Notification: permission_prompt) and when it asks you something (PreToolUse: AskUserQuestion).
// Connect() adds "Flippy.exe --notify <event>" for those to %USERPROFILE%\.claude\settings.json.
// The --notify copy reads the hook's JSON from stdin, writes one line to the inbox and pokes the running Flippy.
// Local only: just the project folder name and Claude's short status message are passed along.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace Flippy
{
    static class ClaudeHooks
    {
        public static string SettingsPath
        {
            get
            {
                string t = Environment.GetEnvironmentVariable("FLIPPY_CLAUDE_SETTINGS");     // tests only: work on a copy
                return !string.IsNullOrEmpty(t) ? t : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");
            }
        }
        static string Inbox { get { return Path.Combine(Store.Dir, "claude-inbox.txt"); } }
        public static string EventName(string suffix) { return @"Local\FlippyNotify" + suffix; }

        // ---------------- the --notify side (a short-lived second copy started by Claude Code) ----------------
        public static int Notify(string evName, string suffix)
        {
            EventWaitHandle ev;
            try { ev = EventWaitHandle.OpenExisting(EventName(suffix)); } catch { return 0; }     // Flippy isn't running: nothing to do
            string json = ReadStdin();
            if (Program.Verbose) Program.Log("notify " + evName + ": " + json.Length + " chars of hook input");
            Dictionary<string, object> d = null;
            try { d = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>; } catch (Exception ex) { if (Program.Verbose) Program.Log("notify json: " + ex.GetType().Name + " " + ex.Message + " | " + json); }
            if (d == null) d = new Dictionary<string, object>();

            string ev0 = Str(d, "hook_event_name"); if (ev0.Length == 0) ev0 = evName;
            string project = "";
            try { string cwd = Str(d, "cwd").TrimEnd('\\', '/'); if (cwd.Length > 0) project = Path.GetFileName(cwd); } catch { }
            string kind = "done", text = "";
            if (ev0.Equals("Stop", StringComparison.OrdinalIgnoreCase) || evName == "stop")
            {
                string last = Str(d, "last_assistant_message").Trim();
                if (last.EndsWith("?")) { kind = "ask"; text = "Claude has a question for you"; }
                else text = "Claude is done!";
            }
            else if (ev0.Equals("Notification", StringComparison.OrdinalIgnoreCase) || evName == "notification")
            {
                if (Str(d, "notification_type") == "idle_prompt") return 0;      // the Stop alert already covered it
                kind = "ask"; text = Str(d, "message"); if (text.Length == 0) text = "Claude needs you";
            }
            else    // PreToolUse: AskUserQuestion
            {
                kind = "ask"; text = "Claude has a question for you";
                try
                {
                    var ti = d.ContainsKey("tool_input") ? d["tool_input"] as Dictionary<string, object> : null;
                    var qs = ti != null && ti.ContainsKey("questions") ? ti["questions"] as object[] : null;
                    var q0 = qs != null && qs.Length > 0 ? qs[0] as Dictionary<string, object> : null;
                    string q = q0 != null ? Str(q0, "question") : "";
                    if (q.Length > 0) text = q;
                }
                catch { }
            }
            string line = Clean(kind) + "\t" + Clean(project) + "\t" + Clean(text);
            for (int i = 0; i < 10; i++)
            {
                try { Directory.CreateDirectory(Store.Dir); File.AppendAllText(Inbox, line + Environment.NewLine, Encoding.UTF8); break; }
                catch { Thread.Sleep(30); }
            }
            ev.Set();
            return 0;
        }
        static string ReadStdin()
        {
            string s = "";
            Thread t = new Thread(() =>
            {
                try
                {
                    // a GUI exe has no Console streams, so read the inherited stdin handle directly
                    IntPtr h = GetStdHandle(-10);
                    if (h == IntPtr.Zero || h == new IntPtr(-1)) return;
                    using (FileStream fs = new FileStream(new Microsoft.Win32.SafeHandles.SafeFileHandle(h, false), FileAccess.Read))
                    using (StreamReader r = new StreamReader(fs, Encoding.UTF8)) s = r.ReadToEnd();
                }
                catch (Exception ex) { if (Program.Verbose) Program.Log("notify stdin: " + ex.Message); }
            });
            t.IsBackground = true; t.Start(); t.Join(1500);      // never hold Claude up for long
            return s ?? "";
        }
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int n);
        static string Str(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v != null ? v.ToString() : ""; }
        static string Clean(string s)
        {
            s = (s ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
            return s.Length > 140 ? s.Substring(0, 137) + "..." : s;
        }

        // ---------------- the running Flippy: read what came in ----------------
        public static List<string[]> TakeInbox()
        {
            var res = new List<string[]>();
            string taken = Inbox + ".reading";
            try
            {
                if (!File.Exists(Inbox)) return res;
                if (File.Exists(taken)) File.Delete(taken);
                File.Move(Inbox, taken);
                foreach (string l in File.ReadAllLines(taken, Encoding.UTF8))
                {
                    string[] p = l.Split('\t');
                    if (p.Length >= 3) res.Add(new[] { p[0], p[1], p[2] });
                }
                File.Delete(taken);
            }
            catch { }
            return res;
        }

        // ---------------- connecting / disconnecting ----------------
        static readonly string[][] Wanted = {           // event, matcher, our --notify arg
            new[] { "Stop", null, "stop" },
            new[] { "Notification", "permission_prompt", "notification" },
            new[] { "PreToolUse", "AskUserQuestion", "question" } };

        static bool Ours(object hook)
        {
            var h = hook as Dictionary<string, object>;
            string c = h != null ? Str(h, "command") : "";
            return IsOurCommand(c);
        }
        // ours = "<some exe>" --notify stop|notification|question (works even if the exe was renamed or moved)
        static readonly System.Text.RegularExpressions.Regex OurCmd = new System.Text.RegularExpressions.Regex(@"\.exe""?\s+--notify\s+(stop|notification|question)\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static bool IsOurCommand(string c) { return c != null && OurCmd.IsMatch(c); }
        public static bool IsConnected()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return false;
                var root = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(File.ReadAllText(SettingsPath)) as Dictionary<string, object>;
                var hooks = root != null && root.ContainsKey("hooks") ? root["hooks"] as Dictionary<string, object> : null;
                if (hooks == null) return false;
                foreach (var ev in hooks.Values)
                    foreach (object g in (ev as object[] ?? new object[0]))
                    {
                        var gd = g as Dictionary<string, object>;
                        if (gd != null && gd.ContainsKey("hooks")) foreach (object h in (gd["hooks"] as object[] ?? new object[0])) if (Ours(h)) return true;
                    }
                return false;
            }
            catch { return false; }
        }
        // the command Claude Code runs; forward slashes and quotes work in Git Bash and cmd
        public static string Command(string exe, string arg) { return "\"" + exe.Replace('\\', '/') + "\" --notify " + arg; }

        public static void Connect(string exe) { Edit(exe); }
        public static void Disconnect() { Edit(null); }

        static void Edit(string exe)
        {
            string path = SettingsPath;
            Dictionary<string, object> root = null;
            if (File.Exists(path))
            {
                string txt = File.ReadAllText(path);
                if (txt.Trim().Length > 0)
                {
                    root = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(txt) as Dictionary<string, object>;
                    if (root == null) throw new InvalidDataException("settings.json isn't a JSON object");
                }
                File.Copy(path, path + ".flippy-backup", true);
            }
            if (root == null) root = new Dictionary<string, object>();
            var hooks = root.ContainsKey("hooks") ? root["hooks"] as Dictionary<string, object> : null;
            if (hooks == null) hooks = new Dictionary<string, object>();

            // take out any of ours first (so connecting twice, or from another folder, doesn't duplicate)
            foreach (string evName in hooks.Keys.ToList())
            {
                var groups = (hooks[evName] as object[] ?? new object[0]).ToList();
                var keep = new List<object>();
                foreach (object g in groups)
                {
                    var gd = g as Dictionary<string, object>;
                    if (gd == null || !gd.ContainsKey("hooks")) { keep.Add(g); continue; }
                    var hs = (gd["hooks"] as object[] ?? new object[0]).Where(h => !Ours(h)).ToArray();
                    if (hs.Length == 0) continue;
                    gd["hooks"] = hs; keep.Add(gd);
                }
                if (keep.Count == 0) hooks.Remove(evName); else hooks[evName] = keep.ToArray();
            }
            if (exe != null)
                foreach (string[] w in Wanted)
                {
                    var hook = new Dictionary<string, object>();
                    hook["type"] = "command"; hook["command"] = Command(exe, w[2]); hook["timeout"] = 10;
                    var group = new Dictionary<string, object>();
                    if (w[1] != null) group["matcher"] = w[1];
                    group["hooks"] = new object[] { hook };
                    var list = hooks.ContainsKey(w[0]) ? (hooks[w[0]] as object[] ?? new object[0]).ToList() : new List<object>();
                    list.Add(group);
                    hooks[w[0]] = list.ToArray();
                }
            if (hooks.Count > 0) root["hooks"] = hooks; else root.Remove("hooks");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            StringBuilder sb = new StringBuilder();
            Write(sb, root, 0); sb.Append('\n');
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        // a small pretty-printing JSON writer (JavaScriptSerializer only writes one long line)
        static void Write(StringBuilder sb, object v, int ind)
        {
            string pad = new string(' ', ind * 2), pad1 = new string(' ', (ind + 1) * 2);
            if (v == null) sb.Append("null");
            else if (v is string) Quote(sb, (string)v);
            else if (v is bool) sb.Append((bool)v ? "true" : "false");
            else if (v is IDictionary<string, object>)
            {
                var d = (IDictionary<string, object>)v;
                if (d.Count == 0) { sb.Append("{}"); return; }
                sb.Append("{\n"); int i = 0;
                foreach (var kv in d) { sb.Append(pad1); Quote(sb, kv.Key); sb.Append(": "); Write(sb, kv.Value, ind + 1); if (++i < d.Count) sb.Append(','); sb.Append('\n'); }
                sb.Append(pad).Append('}');
            }
            else if (v is IEnumerable)
            {
                var a = ((IEnumerable)v).Cast<object>().ToList();
                if (a.Count == 0) { sb.Append("[]"); return; }
                sb.Append("[\n");
                for (int i = 0; i < a.Count; i++) { sb.Append(pad1); Write(sb, a[i], ind + 1); if (i < a.Count - 1) sb.Append(','); sb.Append('\n'); }
                sb.Append(pad).Append(']');
            }
            else if (v is IFormattable) sb.Append(((IFormattable)v).ToString(null, CultureInfo.InvariantCulture));
            else Quote(sb, v.ToString());
        }
        static void Quote(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c); break;
                }
            }
            sb.Append('"');
        }
    }
}
