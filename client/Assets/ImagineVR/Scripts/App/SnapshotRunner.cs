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
        public string Sheet = "";      // "category" or "category:page": lay the library out for inspection
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
            r.Sheet = UnityEditor.SessionState.GetString("ImagineSnap.Sheet", "");
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
            if (UnityEditor.SessionState.GetBool("ImagineSnap.Bare", false))
            {
                // pictures of the world alone: no terminal across the middle, no hands in the corners
                app.Terminal.gameObject.SetActive(false);
                app.Rig.ShowHand(true, false);
                app.Rig.ShowHand(false, false);
            }

            if (Sheet.Length > 0)
            {
                foreach (string sheet in Sheet.Split(';')) yield return ShootSheet(app, sheet);
                Debug.Log("Snapshot: done");
                UnityEditor.EditorApplication.Exit(0);
                yield break;
            }
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

        /// <summary>
        /// Stands every asset of a category in rows on a plain floor, each scaled to the same
        /// size and labelled with its id, and takes one picture. A quick way to look over the library.
        /// </summary>
        IEnumerator ShootSheet(ImagineApp app, string spec)
        {
            string[] parts = spec.Split(':');
            string category = parts[0];
            int page = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            const int perRow = 7, rows = 3, perPage = perRow * rows;
            var picked = new List<AssetDef>();
            foreach (AssetDef a in Library.AssetList) if (category == "all" || a.Category == category) picked.Add(a);

            app.Terminal.gameObject.SetActive(false);
            app.Rig.ShowHand(true, false);
            app.Rig.ShowHand(false, false);
            app.World.ResetToDark(0.2f);
            yield return new WaitForSeconds(0.6f);
            app.World.Apply(Statement.Make("sky", "preset", parts.Length > 2 ? parts[2] : "morning", "t", 0.2));
            app.World.Apply(Statement.Make("terrain", "type", "flat", "material", "concrete", "t", 0.2));
            yield return new WaitForSeconds(0.8f);
            for (int i = page * perPage; i < Mathf.Min(picked.Count, (page + 1) * perPage); i++)
            {
                AssetDef def = picked[i];
                int slot = i - page * perPage, row = slot / perRow, col = slot % perRow;
                Bounds b = def.Bounds;
                float biggest = Mathf.Max(b.size.x, b.size.y, b.size.z, 0.01f);
                float scale = 2.3f / biggest;
                float x = (col - (perRow - 1) * 0.5f) * 3.1f, z = 15f + row * 3f, y = row * 3.1f - b.min.y * scale;
                Statement s = Statement.Make("spawn", "id", "sheet" + i, "asset", def.Id, "scale", (double)scale, "t", 0.2,
                    "at", new List<object> { (double)x, (double)y, (double)z });
                Group g = app.World.Objects.Spawn(s);
                if (g != null) app.World.AddLabel(g, def.Id, new Vector3(x, row * 3.1f - 0.4f, z - 1.2f), 0f, 3.2f);
            }
            yield return new WaitForSeconds(1.6f);
            app.Rig.DesktopLookAt(0f, -6.5f);
            float fov = app.Rig.Cam.fieldOfView;
            app.Rig.Cam.fieldOfView = 38f;
            yield return null;
            yield return null;
            Capture(app, Path.Combine(OutputDir, "sheet_" + category + "_" + page + ".png"));
            app.Rig.Cam.fieldOfView = fov;
            app.Terminal.gameObject.SetActive(true);
            Debug.Log("Snapshot: sheet " + category + " page " + page + " shows " + Mathf.Max(0, Mathf.Min(picked.Count - page * perPage, perPage)) + " of " + picked.Count);
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
