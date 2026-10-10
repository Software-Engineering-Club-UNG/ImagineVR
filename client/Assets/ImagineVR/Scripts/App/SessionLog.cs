using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// A record of one run of the app, kept on the device so that a test in the headset can be
    /// read afterwards: what was pressed, said, sent, received, built, grabbed, and what went wrong.
    /// One JSON object per line, written as it happens (so a crash loses nothing):
    ///   {"t": seconds since start, "clock": time of day, "kind": "...", ...}
    ///
    /// On the Quest the files are in /sdcard/Android/data/com.hacklanta.imaginevr/files/logs/ ;
    /// plug the headset in and run:  adb pull /sdcard/Android/data/com.hacklanta.imaginevr/files/logs
    /// The app token is never written. What the user said is.
    /// </summary>
    public static class SessionLog
    {
        const int KeepFiles = 20;
        const int MaxText = 1200;

        static StreamWriter _out;
        static readonly object Gate = new object();
        static readonly Stopwatch Clock = new Stopwatch();
        static bool _writing;

        public static string Folder => System.IO.Path.Combine(Application.persistentDataPath, "logs");
        /// <summary>The file this run is writing to, or null when it could not be opened.</summary>
        public static string Path { get; private set; }

        public static void Open()
        {
            if (_out != null) return;
            try
            {
                Directory.CreateDirectory(Folder);
                Prune();
                Path = System.IO.Path.Combine(Folder, "session-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".ndjson");
                _out = new StreamWriter(Path, false, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
                Clock.Restart();
                Application.logMessageReceivedThreaded += OnUnityLog;
            }
            catch (Exception e)
            {
                _out = null;
                Path = null;
                UnityEngine.Debug.LogWarning("ImagineVR: no session log: " + e.Message);
            }
        }

        public static void Close()
        {
            Application.logMessageReceivedThreaded -= OnUnityLog;
            lock (Gate)
            {
                if (_out != null) _out.Dispose();
                _out = null;
            }
        }

        /// <summary>Only the newest runs are kept, so the headset never fills up with logs.</summary>
        static void Prune()
        {
            string[] files = Directory.GetFiles(Folder, "session-*.ndjson");
            Array.Sort(files, string.CompareOrdinal);   // the names sort by date
            for (int i = 0; i < files.Length - (KeepFiles - 1); i++) File.Delete(files[i]);
        }

        /// <summary>Writes one line. Fields are given as name, value, name, value.</summary>
        public static void Write(string kind, params object[] keyValues)
        {
            if (_out == null) return;
            var line = new Dictionary<string, object>
            {
                ["t"] = Math.Round(Clock.Elapsed.TotalSeconds, 2),
                ["clock"] = DateTime.Now.ToString("HH:mm:ss.fff"),
                ["kind"] = kind,
            };
            for (int i = 0; i + 1 < keyValues.Length; i += 2) line[(string)keyValues[i]] = Clean(keyValues[i + 1]);
            lock (Gate)
            {
                if (_out == null || _writing) return;
                _writing = true;
                try { _out.WriteLine(MiniJson.Serialize(line)); }
                catch (Exception) { /* a full disk must not stop the app */ }
                finally { _writing = false; }
            }
        }

        static object Clean(object value)
        {
            switch (value)
            {
                case Vector3 v: return Vec(v);
                case Vector2 v: return new List<object> { Round(v.x), Round(v.y) };
                case string s: return s.Length > MaxText ? s.Substring(0, MaxText) + "…" : s;
                case float f: return float.IsNaN(f) || float.IsInfinity(f) ? null : (object)f;
                default: return value;
            }
        }

        static double Round(float v) => Math.Round(v, 2);

        public static List<object> Vec(Vector3 v) => new List<object> { Round(v.x), Round(v.y), Round(v.z) };

        /// <summary>Everything Unity itself reports (warnings, errors, exceptions with their stack) goes in too.</summary>
        static void OnUnityLog(string message, string stack, LogType type)
        {
            if (type == LogType.Log) Write("unity", "level", "info", "text", message);
            else if (type == LogType.Warning) Write("unity", "level", "warning", "text", message);
            else Write("unity", "level", type == LogType.Exception ? "exception" : "error", "text", message, "stack", stack);
        }
    }
}
