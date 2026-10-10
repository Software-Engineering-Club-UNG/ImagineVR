#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// Editor-only helper used by the snapshot tool: plays one or more scripts in Play mode and
    /// saves pictures of each result from several directions, so worlds can be checked without a
    /// headset. Started by ImagineVR.EditorTools.Snapshot through SessionState.
    /// </summary>
    public sealed class SnapshotRunner : MonoBehaviour
    {
        public string[] ScriptFiles = new string[0];
        public string OutputDir;
        public float[] Times = { 6f };
        public float[] Yaws = { 0f };
        public float Pitch = -4f;
        public int Width = 1280, Height = 720;
        float _deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            if (!UnityEditor.SessionState.GetBool("ImagineSnap.Active", false)) return;
            UnityEditor.SessionState.SetBool("ImagineSnap.Active", false);
            ImagineApp.Quiet = !UnityEditor.SessionState.GetBool("ImagineSnap.Welcome", false);
            var go = new GameObject("SnapshotRunner");
            DontDestroyOnLoad(go);
            SnapshotRunner r = go.AddComponent<SnapshotRunner>();
            string scripts = UnityEditor.SessionState.GetString("ImagineSnap.Scripts", "");
            r.ScriptFiles = scripts.Length > 0 ? scripts.Split(';') : new string[0];
            r.OutputDir = UnityEditor.SessionState.GetString("ImagineSnap.Out", "Snapshots");
            r.Times = ParseList(UnityEditor.SessionState.GetString("ImagineSnap.Times", ""), new[] { 6f });
            r.Yaws = ParseList(UnityEditor.SessionState.GetString("ImagineSnap.Yaws", ""), new[] { 0f });
            r.Pitch = UnityEditor.SessionState.GetFloat("ImagineSnap.Pitch", -4f);
        }

        IEnumerator Start()
        {
            float longest = 0f;
            foreach (float t in Times) longest = Mathf.Max(longest, t);
            _deadline = Time.realtimeSinceStartup + (longest + 20f) * Mathf.Max(1, ScriptFiles.Length) + 60f;

            // let the app finish building itself
            yield return null;
            yield return null;
            ImagineApp app = ImagineApp.Instance;
            if (app == null)
            {
                Debug.LogError("Snapshot: no ImagineApp in the scene");
                UnityEditor.EditorApplication.Exit(1);
                yield break;
            }
            Directory.CreateDirectory(OutputDir);

            if (ScriptFiles.Length == 0)
            {
                yield return Shoot(app, "welcome", null);
            }
            foreach (string file in ScriptFiles)
            {
                if (!File.Exists(file)) { Debug.LogError("Snapshot: missing script " + file); continue; }
                yield return Shoot(app, Path.GetFileNameWithoutExtension(file), File.ReadAllText(file));
                app.World.ResetToDark(0.25f);
                app.Conductor.ResetFirstLight();
                yield return new WaitForSeconds(1.2f);
            }
            Debug.Log("Snapshot: done");
            UnityEditor.EditorApplication.Exit(0);
        }

        IEnumerator Shoot(ImagineApp app, string name, string script)
        {
            if (script != null) app.Conductor.SubmitScript(script, name.Replace('-', ' '));
            float start = Time.time;
            foreach (float t in Times)
            {
                while (Time.time - start < t) yield return null;
                foreach (float yaw in Yaws)
                {
                    app.Rig.DesktopLookAt(yaw, Pitch);
                    yield return null;   // (WaitForEndOfFrame never fires in batch mode)
                    yield return null;
                    string file = Path.Combine(OutputDir, string.Format(CultureInfo.InvariantCulture, "{0}_t{1:0.#}_y{2:0}.png", name, t, yaw));
                    Capture(app, file);
                }
                app.Rig.DesktopLookAt(0f, Pitch);   // face the terminal again so it does not wander off
            }
        }

        void Update()
        {
            if (Time.realtimeSinceStartup > _deadline && _deadline > 0f)
            {
                Debug.LogError("Snapshot: timed out");
                UnityEditor.EditorApplication.Exit(2);
            }
        }

        void Capture(ImagineApp app, string file)
        {
            Camera cam = app.Rig.Cam;
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;
            app.World.Objects.Render();   // instanced draws are queued per frame: queue them for this extra render
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = previous;
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Destroy(tex);
            rt.Release();
            Destroy(rt);
            Debug.Log("Snapshot: wrote " + file);
        }

        public static float[] ParseList(string text, float[] fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            var values = new List<float>();
            foreach (string part in text.Split(','))
            {
                if (float.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) values.Add(v);
            }
            return values.Count > 0 ? values.ToArray() : fallback;
        }
    }
}
#endif
