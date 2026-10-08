// His pocket: up to 3 files (or folders) you dropped on him, to grab again later from his menu.
// Only the paths are kept (in %APPDATA%\Flippy\pocket.txt); the files themselves are never opened or copied.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Flippy
{
    class Pocket
    {
        public const int Max = 3;
        public readonly List<string> Files = new List<string>();
        string Path0 { get { return Path.Combine(Store.Dir, "pocket.txt"); } }

        public void Load()
        {
            Files.Clear();
            try { if (File.Exists(Path0)) foreach (string l in File.ReadAllLines(Path0)) if (l.Trim().Length > 0 && Files.Count < Max) Files.Add(l.Trim()); } catch { }
        }
        public void Save()
        {
            try { Directory.CreateDirectory(Store.Dir); File.WriteAllLines(Path0, Files.ToArray()); } catch { }
        }

        // newest last; returns how many old ones fell out to make room
        public int Add(IEnumerable<string> paths)
        {
            int dropped = 0;
            foreach (string p in paths)
            {
                if (string.IsNullOrEmpty(p)) continue;
                Files.RemoveAll(f => string.Equals(f, p, StringComparison.OrdinalIgnoreCase));
                Files.Add(p);
                while (Files.Count > Max) { Files.RemoveAt(0); dropped++; }
            }
            Save();
            return dropped;
        }
        public void Remove(int i) { if (i >= 0 && i < Files.Count) { Files.RemoveAt(i); Save(); } }
        public void Clear() { Files.Clear(); Save(); }

        public static bool Exists(string p) { return File.Exists(p) || Directory.Exists(p); }
        public static string Name(string p) { string n = Path.GetFileName(p.TrimEnd('\\', '/')); return n.Length > 0 ? n : p; }
        public static string Where(string p)
        {
            try { string d = Path.GetDirectoryName(p.TrimEnd('\\', '/')); return d == null ? "" : d; } catch { return ""; }
        }
    }
}
