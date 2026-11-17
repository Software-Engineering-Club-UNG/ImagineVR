using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// The game as the user sees it: a strip low in their view that follows their gaze lazily (what
    /// to do, the counters that have labels, their health), banners for moments, scoreboards that
    /// stand in the world, bubbles over people who speak, and fades.
    ///
    /// Nothing here is locked to the head: a panel glued to the face is uncomfortable in a headset.
    /// The strip drifts after the gaze the way the terminal does, and everything faces the user.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        public Transform Head;
        public TMP_FontAsset Font;
        public PlaySystem Play;
        public World World;

        GameObject _strip, _banner, _subtitle;
        TextMeshPro _stripText, _bannerText, _subtitleText;
        Material _stripPanel;
        Transform _stripBack;
        float _bannerUntil, _bannerShown, _subtitleUntil, _dirtyAt = -1f, _awaySince = -1f;
        bool _dirty = true;
        List<string> _only;
        Comfort _fade;
        float _fadeUntil, _fadeSeconds, _hurtUntil;
        readonly Dictionary<string, (GameObject root, TextMeshPro text, string title, List<string> show)> _boards = new Dictionary<string, (GameObject, TextMeshPro, string, List<string>)>();
        readonly Dictionary<string, Callout> _bubbles = new Dictionary<string, Callout>();

        public void Build(Transform head, TMP_FontAsset font, PlaySystem play, World world)
        {
            Head = head;
            Font = font;
            Play = play;
            World = world;
            _strip = Panel("HudStrip", 0.9f, out _stripText, out _stripPanel, out _stripBack);
            _stripText.fontSize = 0.55f;
            _strip.SetActive(false);
            _banner = new GameObject("HudBanner");
            _banner.transform.SetParent(transform, false);
            _bannerText = Text(_banner.transform, 1.6f, TextAlignmentOptions.Center);
            _bannerText.rectTransform.sizeDelta = new Vector2(3.2f, 1f);
            _bannerText.textWrappingMode = TextWrappingModes.Normal;
            _banner.SetActive(false);
            _subtitle = new GameObject("HudSubtitle");
            _subtitle.transform.SetParent(transform, false);
            _subtitleText = Text(_subtitle.transform, 0.55f, TextAlignmentOptions.Center);
            _subtitleText.rectTransform.sizeDelta = new Vector2(2.4f, 0.6f);
            _subtitleText.textWrappingMode = TextWrappingModes.Normal;
            _subtitleText.color = new Color(1f, 0.95f, 0.8f);
            _subtitle.SetActive(false);
            if (head != null) _fade = Comfort.Create(head);
        }

        TextMeshPro Text(Transform parent, float size, TextAlignmentOptions align)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.003f);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = Font;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.richText = true;
            tmp.color = Color.white;
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return tmp;
        }

        GameObject Panel(string name, float width, out TextMeshPro text, out Material panel, out Transform back)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            text = Text(go.transform, 0.5f, TextAlignmentOptions.Left);
            text.rectTransform.sizeDelta = new Vector2(width, 0.2f);
            text.textWrappingMode = TextWrappingModes.Normal;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Back";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(go.transform, false);
            back = quad.transform;
            panel = new Material(Shader.Find("ImagineVR/Panel")) { name = name };
            panel.SetFloat("_TitleHeight", 0f);
            panel.SetColor("_GlowColor", Pal.Hex(Terminal.Accent));
            var mr = quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial = panel;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        void SizePanel(Transform back, Material panel, Vector2 size)
        {
            float pad = 0.035f;
            back.localScale = new Vector3(size.x + pad * 2f, size.y + pad * 2f, 1f);
            panel.SetVector("_Size", new Vector4(size.x + pad * 2f, size.y + pad * 2f, 0f, 0f));
            panel.SetFloat("_Radius", 0.03f);
            panel.SetFloat("_Border", 0.0025f);
        }

        // ------------------------------------------------------------------ what the play layer says

        public void Changed() { _dirty = true; }

        public void ShowOnly(List<string> names) { _only = names != null && names.Count > 0 ? names : null; _dirty = true; }

        public void Banner(string text, float seconds, Color? color)
        {
            if (_bannerText == null) return;
            _bannerText.text = text;
            _bannerText.color = color ?? new Color(1f, 0.92f, 0.6f);
            _bannerUntil = Time.time + Mathf.Clamp(seconds, 0.5f, 15f);
            _bannerShown = Time.time;
            _banner.SetActive(true);
            PlaceInFront(_banner.transform, 2.2f, 0.25f, true);
        }

        public void Speech(string id, string text, Transform speaker)
        {
            if (speaker != null)
            {
                float lift = 0.5f;
                Creature c = speaker.GetComponent<Creature>();
                if (c != null) lift = c.Height + 0.35f;
                if (id != null && _bubbles.TryGetValue(id, out Callout old) && old != null) old.Dismiss();
                Callout bubble = Callout.Show(speaker, Vector3.zero, lift, Terminal.Wrap(text, 36), 0.6f, Mathf.Clamp(text.Length * 0.07f, 2.5f, 8f));
                if (id != null) _bubbles[id] = bubble;
                return;
            }
            // the narrator, or someone not to be seen: words low in the view, as subtitles
            if (_subtitleText == null) return;
            _subtitleText.text = "<i>" + text + "</i>";
            _subtitleUntil = Time.time + Mathf.Clamp(text.Length * 0.075f, 3f, 10f);
            _subtitle.SetActive(true);
            PlaceInFront(_subtitle.transform, 1.8f, -0.45f, true);
        }

        public void Board(string id, Vector3 at, Quaternion facing, float size, string title, List<string> show)
        {
            if (_boards.TryGetValue(id, out var old) && old.root != null) Destroy(old.root);
            var root = new GameObject("Board_" + id);
            root.transform.SetParent(World != null ? World.transform : transform, false);
            root.transform.SetPositionAndRotation(at, facing * Quaternion.Euler(0f, 180f, 0f));     // its face towards where the user stood
            float w = 1.6f * size, h = 0.9f * size;
            TextMeshPro text = Text(root.transform, 0.9f * size, TextAlignmentOptions.Center);
            text.rectTransform.sizeDelta = new Vector2(w, h);
            text.textWrappingMode = TextWrappingModes.Normal;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(root.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            quad.transform.localScale = new Vector3(w + 0.1f, h + 0.1f, 1f);
            var panel = new Material(Shader.Find("ImagineVR/Panel")) { name = "Board" };
            panel.SetVector("_Size", new Vector4(w + 0.1f, h + 0.1f, 0f, 0f));
            panel.SetFloat("_TitleHeight", 0f);
            panel.SetFloat("_Radius", 0.05f * size);
            panel.SetFloat("_Border", 0.01f * size);
            panel.SetColor("_GlowColor", Pal.Hex(Terminal.Accent));
            quad.GetComponent<MeshRenderer>().sharedMaterial = panel;
            _boards[id] = (root, text, title, show);
            _dirty = true;
        }

        public void Fade(float seconds, Color color)
        {
            if (_fade == null) return;
            _fadeSeconds = Mathf.Clamp(seconds, 0.2f, 6f);
            _fadeUntil = Time.time + _fadeSeconds;
        }

        public void Hurt(Vector3 from) { _hurtUntil = Time.time + 0.25f; }

        public void Clear()
        {
            foreach (var b in _boards.Values) if (b.root != null) Destroy(b.root);
            _boards.Clear();
            foreach (Callout c in _bubbles.Values) if (c != null) c.Dismiss();
            _bubbles.Clear();
            _only = null;
            if (_banner != null) _banner.SetActive(false);
            if (_subtitle != null) _subtitle.SetActive(false);
            _dirty = true;
        }

        // ------------------------------------------------------------------ every frame

        void PlaceInFront(Transform t, float distance, float up, bool face)
        {
            if (Head == null) return;
            Vector3 look = Head.forward;
            look.y = 0f;
            if (look.sqrMagnitude < 0.01f) look = Vector3.forward;
            look.Normalize();
            t.position = Head.position + look * distance + Vector3.up * up;
            if (face) t.rotation = Quaternion.LookRotation(t.position - Head.position);
        }

        void Update()
        {
            if (Play == null || Head == null) return;
            float now = Time.time;

            if (_banner.activeSelf)
            {
                float age = now - _bannerShown, left = _bannerUntil - now;
                float pop = Ease.OutBack(Mathf.Clamp01(age / 0.25f));
                _banner.transform.localScale = Vector3.one * (left < 0.4f ? Mathf.Clamp01(left / 0.4f) : pop);
                Follow(_banner.transform, 2.2f, 0.25f, 4f);
                if (left <= 0f) _banner.SetActive(false);
            }
            if (_subtitle.activeSelf)
            {
                Follow(_subtitle.transform, 1.8f, -0.45f, 3f);
                if (now > _subtitleUntil) _subtitle.SetActive(false);
            }

            if (_fade != null)
            {
                float amount = 0f;
                if (now < _fadeUntil) { float t = 1f - (_fadeUntil - now) / _fadeSeconds; amount = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI); }
                if (now < _hurtUntil) amount = Mathf.Max(amount, 0.35f * (_hurtUntil - now) / 0.25f);
                _fade.Fade(amount);
            }

            if (_dirty && now >= _dirtyAt)
            {
                _dirty = false;
                _dirtyAt = now + 0.1f;
                Redraw();
            }
            if (_strip.activeSelf) Drift(now);
        }

        void Follow(Transform t, float distance, float up, float rate)
        {
            Vector3 look = Head.forward;
            look.y = 0f;
            if (look.sqrMagnitude < 0.01f) return;
            Vector3 want = Head.position + look.normalized * distance + Vector3.up * up;
            t.position = Vector3.Lerp(t.position, want, 1f - Mathf.Exp(-Time.deltaTime * rate));
            t.rotation = Quaternion.LookRotation(t.position - Head.position);
        }

        /// <summary>The strip stays put while the user looks about, and comes round once they have settled on a new direction.</summary>
        void Drift(float now)
        {
            Vector3 to = _strip.transform.position - Head.position;
            Vector3 look = Head.forward;
            to.y = 0f;
            look.y = 0f;
            float off = to.sqrMagnitude > 0.001f && look.sqrMagnitude > 0.001f ? Vector3.Angle(to, look) : 180f;
            if (off > 35f) { if (_awaySince < 0f) _awaySince = now; }
            else _awaySince = -1f;
            bool far = (to.magnitude > 2.5f) || Mathf.Abs(_strip.transform.position.y - (Head.position.y - 0.42f)) > 0.6f;
            if ((_awaySince > 0f && now - _awaySince > 0.8f) || far)
            {
                Follow(_strip.transform, 1.25f, -0.42f, 5f);
                if (off < 5f) _awaySince = -1f;
            }
            else _strip.transform.rotation = Quaternion.LookRotation(_strip.transform.position - Head.position);
        }

        void Redraw()
        {
            var sb = new StringBuilder();
            if (Play.Objective.Length > 0) sb.Append("<color=#FFD98A>").Append(Play.Objective).Append("</color>");
            var chips = new List<string>();
            foreach (var (name, label, value, fill) in Play.Shown())
            {
                if (_only != null && !_only.Contains(name)) continue;
                chips.Add(fill >= 0f ? label + " " + Bar(fill) : label + " <b>" + value + "</b>");
            }
            if (Play.Gauge.Length > 0) chips.Add("<color=#8AD8FF>" + Play.Gauge + "</color>");
            if (Play.PlayerMax > 0f) chips.Insert(0, "<color=#FF7A7A>Health</color> " + Bar(Play.PlayerHealth / Play.PlayerMax) + " " + Mathf.CeilToInt(Play.PlayerHealth));
            if (chips.Count > 0)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(string.Join("   ", chips));
            }
            bool show = sb.Length > 0;
            if (show != _strip.activeSelf)
            {
                _strip.SetActive(show);
                if (show) PlaceInFront(_strip.transform, 1.25f, -0.42f, true);
            }
            if (show)
            {
                _stripText.text = sb.ToString();
                _stripText.ForceMeshUpdate();
                Vector2 size = _stripText.GetPreferredValues(_stripText.text, 0.9f, 0f);
                size.x = Mathf.Min(Mathf.Max(size.x, 0.3f), 0.9f);
                _stripText.rectTransform.sizeDelta = new Vector2(size.x, size.y);
                SizePanel(_stripBack, _stripPanel, size);
            }
            foreach (var board in _boards.Values)
            {
                if (board.text == null) continue;
                var lines = new StringBuilder();
                if (!string.IsNullOrEmpty(board.title)) lines.Append("<b><color=#FFD98A>").Append(board.title).Append("</color></b>\n");
                foreach (string name in board.show ?? new List<string>())
                {
                    object v = Play.Var(name);
                    string value = v is double d ? (Mathf.Abs((float)d - Mathf.Round((float)d)) < 0.01f ? Mathf.RoundToInt((float)d).ToString() : d.ToString("0.0")) : v.ToString();
                    lines.Append(name.Replace('_', ' ').ToUpperInvariant()).Append("  <b>").Append(value).Append("</b>\n");
                }
                board.text.text = lines.ToString().TrimEnd('\n');
            }
        }

        static string Bar(float fill)
        {
            int full = Mathf.Clamp(Mathf.RoundToInt(fill * 10f), 0, 10);
            return "<color=#7CFF8A>" + new string('|', full) + "</color><color=#555555>" + new string('|', 10 - full) + "</color>";
        }
    }
}
