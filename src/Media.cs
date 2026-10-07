// Play/pause state from Windows' own media controls (the volume-key overlay), via WinRT reflection
// so it builds with the plain .NET Framework compiler. Local only.
using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace Clawd
{
    class Media
    {
        object mgr;
        MethodInfo getSessions;
        public bool Available { get { return mgr != null; } }

        public Media()
        {
            try
            {
                Assembly.Load("System.Runtime.WindowsRuntime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                Type mt = Type.GetType("Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media.Control, ContentType=WindowsRuntime");
                if (mt == null) return;
                object op = mt.GetMethod("RequestAsync").Invoke(null, null);
                Type ext = Type.GetType("System.WindowsRuntimeSystemExtensions, System.Runtime.WindowsRuntime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                MethodInfo asTask = ext.GetMethods().First(m => m.Name == "AsTask" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.Name == "IAsyncOperation`1");
                object task = asTask.MakeGenericMethod(mt).Invoke(null, new[] { op });
                var t = (System.Threading.Tasks.Task)task;
                if (!t.Wait(3000)) return;
                mgr = task.GetType().GetProperty("Result").GetValue(task, null);
                getSessions = mgr.GetType().GetMethod("GetSessions");
            }
            catch { mgr = null; }
        }

        // anything (other than music apps) playing / paused?  plus: is a music app playing?
        public void Poll(out string video, out bool musicPlaying)
        {
            video = "none"; musicPlaying = false;
            if (mgr == null) return;
            try
            {
                bool paused = false;
                foreach (object s in (IEnumerable)getSessions.Invoke(mgr, null))
                {
                    string app = (string)s.GetType().GetProperty("SourceAppUserModelId").GetValue(s, null) ?? "";
                    object info = s.GetType().GetMethod("GetPlaybackInfo").Invoke(s, null);
                    string st = info.GetType().GetProperty("PlaybackStatus").GetValue(info, null).ToString();
                    bool music = app.IndexOf("Spotify", StringComparison.OrdinalIgnoreCase) >= 0 || app.IndexOf("AppleMusic", StringComparison.OrdinalIgnoreCase) >= 0
                                 || app.IndexOf("Deezer", StringComparison.OrdinalIgnoreCase) >= 0 || app.IndexOf("Tidal", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (music) { if (st == "Playing") musicPlaying = true; continue; }
                    if (st == "Playing") video = "playing";
                    else if (st == "Paused") paused = true;
                }
                if (video == "none" && paused) video = "paused";
            }
            catch { }
        }
    }
}
