using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// Plays one or more scripts and saves pictures of each result from several directions, so
    /// worlds can be checked without a headset. In the editor it is started by
    /// ImagineVR.EditorTools.Snapshot; a built player starts it itself when run with -snapOut.
    ///
    /// Run it against a BUILD before trusting a change to how things are drawn. The editor keeps
    /// every shader variant and a build does not: for a whole day the editor's pictures showed
    /// forests that the headset never drew.
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

        bool _bare, _probe;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            string scripts, output, times, yaws, sheet = "";
            float pitch;
            bool welcome, bare, probe;
#if UNITY_EDITOR
            if (!UnityEditor.SessionState.GetBool("ImagineSnap.Active", false)) return;
            UnityEditor.SessionState.SetBool("ImagineSnap.Active", false);
            scripts = UnityEditor.SessionState.GetString("ImagineSnap.Scripts", "");
            output = UnityEditor.SessionState.GetString("ImagineSnap.Out", "Snapshots");
            times = UnityEditor.SessionState.GetString("ImagineSnap.Times", "");
            yaws = UnityEditor.SessionState.GetString("ImagineSnap.Yaws", "");
            pitch = UnityEditor.SessionState.GetFloat("ImagineSnap.Pitch", -4f);
            sheet = UnityEditor.SessionState.GetString("ImagineSnap.Sheet", "");
            welcome = UnityEditor.SessionState.GetBool("ImagineSnap.Welcome", false);
            bare = UnityEditor.SessionState.GetBool("ImagineSnap.Bare", false);
            probe = UnityEditor.SessionState.GetBool("ImagineSnap.Probe", false);
#else
            output = Arg("-snapOut", "");
            if (output.Length == 0) return;
            scripts = Arg("-snapScripts", "");
            times = Arg("-snapTimes", "");
            yaws = Arg("-snapYaws", "");
            pitch = ParseList(Arg("-snapPitch", ""), new[] { -4f })[0];
            welcome = Arg("-snapWelcome", "0") == "1";
            bare = Arg("-snapBare", "0") == "1";
            probe = Arg("-snapProbe", "0") == "1";
#endif
            ImagineApp.Quiet = !welcome;
            var go = new GameObject("SnapshotRunner");
            DontDestroyOnLoad(go);
            SnapshotRunner r = go.AddComponent<SnapshotRunner>();
            r.ScriptFiles = scripts.Length > 0 ? scripts.Split(';') : new string[0];
            r.OutputDir = output;
            r.Times = ParseList(times, new[] { 6f });
            r.Yaws = ParseList(yaws, new[] { 0f });
            r.Pitch = pitch;
            r.Sheet = sheet;
            r._bare = bare;
            r._probe = probe;
        }

        static string Arg(string name, string fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return fallback;
        }

        static void Quit(int code)
        {
#if UNITY_EDITOR
            Quit(code);
#else
            Application.Quit(code);
#endif
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
                Quit(1);
                yield break;
            }
            Directory.CreateDirectory(OutputDir);
            if (_bare)
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
                Quit(0);
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
                Census(app, Path.GetFileNameWithoutExtension(file));
                app.World.ResetEverything(0.25f);
                app.Conductor.ResetFirstLight();
                yield return new WaitForSeconds(1.2f);
            }
            if (_probe) yield return Probe(app);
            Debug.Log("Snapshot: done");
            Quit(0);
        }

        /// <summary>What is really in the renderers after a script, written beside its pictures.</summary>
        void Census(ImagineApp app, string name)
        {
            app.World.Objects.Census(out int renderers, out int items, out int triangles);
            app.World.Structures.Census(out int structureRenderers, out int structureTriangles);
            string line = "{\"scene\":\"" + name + "\",\"groups\":" + app.World.Objects.Count + ",\"renderers\":" + renderers + ",\"items\":" + items +
                ",\"triangles\":" + triangles + ",\"alive\":" + Creature.All.Count + ",\"props\":" + app.World.Props.All.Count +
                ",\"rules\":" + app.World.Rules.Count + ",\"riding\":\"" + (app.World.Vehicle != null ? app.World.Vehicle.Asset : null) + "\",\"editor\":" + (Application.isEditor ? "true" : "false") +
                ",\"structure_zones\":" + app.World.Structures.ZoneCount + ",\"structure_doors\":" + app.World.Structures.DoorCount +
                ",\"structure_renderers\":" + structureRenderers + ",\"structure_triangles\":" + structureTriangles + "}";
            File.AppendAllText(Path.Combine(OutputDir, "census.ndjson"), line + "\n");
            Debug.Log("Snapshot: " + line);
        }

        /// <summary>
        /// The experiment behind the way scenery is drawn. Two magenta cubes are put in front of the
        /// camera: the left one through GPU instancing with a material made at runtime (how scenery
        /// used to be drawn), the right one as an ordinary renderer (how it is drawn now). In the
        /// editor both appear. In a build, the left one is missing if instancing variants are stripped.
        /// </summary>
        IEnumerator Probe(ImagineApp app)
        {
            app.World.ResetEverything(0.1f);
            yield return new WaitForSeconds(0.5f);
            app.World.Apply(Statement.Make("sky", "preset", "white_room", "t", 0.2));
            yield return new WaitForSeconds(1f);
            app.Rig.DesktopLookAt(0f, 0f);
            Mesh cube = Library.Asset("cube").Mesh;
            var instanced = new Material(Shader.Find("ImagineVR/Lit")) { enableInstancing = true };
            var block = new MaterialPropertyBlock();
            block.SetColor("_Tint", Color.magenta);
            block.SetFloat("_Appear", 1f);
            var matrices = new[] { Matrix4x4.TRS(new Vector3(-1.2f, 1f, 4f), Quaternion.identity, Vector3.one) };
            var ordinary = new GameObject("ProbeOrdinary");
            ordinary.transform.position = new Vector3(1.2f, 1f, 4f);
            ordinary.AddComponent<MeshFilter>().sharedMesh = cube;
            MeshRenderer mr = ordinary.AddComponent<MeshRenderer>();
            mr.sharedMaterial = app.World.Objects.LitMaterial;
            mr.SetPropertyBlock(block);
            for (int i = 0; i < 4; i++)
            {
                Graphics.RenderMeshInstanced(new RenderParams(instanced) { matProps = block, worldBounds = new Bounds(Vector3.zero, Vector3.one * 100f) }, cube, 0, matrices, 1);
                yield return null;
            }
            Graphics.RenderMeshInstanced(new RenderParams(instanced) { matProps = block, worldBounds = new Bounds(Vector3.zero, Vector3.one * 100f) }, cube, 0, matrices, 1);
            string file = Path.Combine(OutputDir, "probe.png");
            Texture2D tex = Capture(app, file, true);
            bool Magenta(int x) { Color c = tex.GetPixel(x, Height / 2 - 20); return c.r > 0.5f && c.b > 0.5f && c.g < 0.45f; }
            bool left = false, right = false;
            for (int x = 0; x < Width / 2; x += 4) left |= Magenta(x);
            for (int x = Width / 2; x < Width; x += 4) right |= Magenta(x);
            string line = "{\"probe\":true,\"instanced_drew\":" + (left ? "true" : "false") + ",\"ordinary_drew\":" + (right ? "true" : "false") + ",\"editor\":" + (Application.isEditor ? "true" : "false") + "}";
            File.AppendAllText(Path.Combine(OutputDir, "census.ndjson"), line + "\n");
            Debug.Log("Snapshot: " + line);
            Destroy(tex);
            Destroy(ordinary);
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
                Quit(2);
            }
        }

        void Capture(ImagineApp app, string file) { Destroy(Capture(app, file, true)); }

        Texture2D Capture(ImagineApp app, string file, bool keep)
        {
            Camera cam = app.Rig.Cam;
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = previous;
            File.WriteAllBytes(file, tex.EncodeToPNG());
            rt.Release();
            Destroy(rt);
            Debug.Log("Snapshot: wrote " + file);
            return tex;
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
