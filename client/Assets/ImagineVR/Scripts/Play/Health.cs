using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// `health` on a thing: blows take it away, and at nothing it raises `die`. Without it, a
    /// creature still goes down at one hit and gets up again, as it always did; with it, hits
    /// before the last only make it flinch, and the last keeps it down.
    /// </summary>
    public sealed class Health : MonoBehaviour
    {
        public float Hp = 1f, Max = 1f;
        public string Team = "";
        public bool Dead;
        public string Breaks;       // shatters into pieces at nothing: glass, wood, stone, metal, crate
        float _flashUntil;

        public static bool IsDead(GameObject go)
        {
            Health h = go != null ? go.GetComponent<Health>() : null;
            return h != null && h.Dead;
        }

        public static Health Give(GameObject go, float hp, string team = null)
        {
            Health h = go.GetComponent<Health>() ?? go.AddComponent<Health>();
            h.Max = h.Hp = Mathf.Max(hp, 0.01f);
            h.Dead = false;
            if (team != null) h.Team = team;
            return h;
        }

        /// <summary>
        /// A blow to something. True when its health took it (it flinched, or died and is dealt
        /// with here), so that whatever would ordinarily happen to a struck thing does not.
        /// </summary>
        public static bool Strike(World world, Rules.Subject subject)
        {
            GameObject go = subject.Object;
            Health h = go != null ? go.GetComponent<Health>() : null;
            if (h == null || h.Dead) return false;
            float amount = subject.Amount > 0f ? subject.Amount : 1f;
            h.Hp = Mathf.Max(0f, h.Hp - amount);
            Number(world, go.transform.position + Vector3.up * Height(go), amount, false);
            h._flashUntil = Time.time + 0.12f;
            world.Rules.Changed();
            if (h.Hp > 0f)
            {
                Sfx.Play("thunk", go.transform.position, 0.6f, Random.Range(0.9f, 1.15f));
                if (subject.Creature != null) subject.Creature.Flinch();
                world.Rules.Fire("hurt", subject);
                return true;
            }
            h.Dead = true;
            SessionLog.Write("die", "asset", subject.Asset, "group", subject.Group);
            world.Rules.Fire("die", subject);
            if (go == null) return true;      // a `die` rule removed it
            if (subject.Creature != null) { subject.Creature.StayDown = true; subject.Creature.Knock(go.transform.position - (world.Head != null ? world.Head.position : Vector3.zero)); }
            else if (!string.IsNullOrEmpty(h.Breaks)) Shatter(world, go, h.Breaks, subject);
            return true;
        }

        public static void Heal(Rules.Subject subject, float amount)
        {
            Health h = subject.Object != null ? subject.Object.GetComponent<Health>() : null;
            if (h == null || h.Dead) return;
            h.Hp = Mathf.Min(h.Max, h.Hp + amount);
        }

        /// <summary>Everything with health within a radius takes the blow, falling off towards the edge. The user too, if they can be hurt.</summary>
        public static void Area(World world, Vector3 at, float radius, float amount, GameObject except = null)
        {
            foreach (Creature c in new List<Creature>(Creature.All))
            {
                if (c == null || c.gameObject == except) continue;
                float d = Vector3.Distance(c.transform.position, at);
                if (d > radius) continue;
                Rules.Subject s = Rules.Of(c);
                s.Amount = amount * (1f - 0.6f * d / radius);
                if (c.GetComponent<Health>() != null) Strike(world, s); else c.Knock(c.transform.position - at);
            }
            foreach (Prop p in new List<Prop>(world.Props.All))
            {
                if (p == null || p.gameObject == except || p.GetComponent<Health>() == null) continue;
                float d = Vector3.Distance(p.transform.position, at);
                if (d > radius) continue;
                Rules.Subject s = Rules.Of(p);
                s.Amount = amount * (1f - 0.6f * d / radius);
                Strike(world, s);
            }
            foreach (Hittable h in new List<Hittable>(Hittable.All))
            {
                if (h == null || h.gameObject == except || h.GetComponent<Health>() == null) continue;
                float d = Vector3.Distance(h.transform.position, at);
                if (d > radius) continue;
                Rules.Subject s = Rules.Of(h);
                s.Amount = amount * (1f - 0.6f * d / radius);
                Strike(world, s);
            }
            if (world.Head != null)
            {
                float d = Vector3.Distance(world.Head.position, at);
                if (d < radius) world.Play.HurtPlayer(amount * (1f - 0.6f * d / radius), at);
            }
        }

        static float Height(GameObject go)
        {
            Creature c = go.GetComponent<Creature>();
            if (c != null) return c.Height + 0.25f;
            Collider col = go.GetComponentInChildren<Collider>();
            return col != null ? col.bounds.extents.y + 0.3f : 0.6f;
        }

        /// <summary>A number that rises off what was hit and fades: how much that blow took.</summary>
        static void Number(World world, Vector3 at, float amount, bool heal)
        {
            if (world.Font == null) return;
            var go = new GameObject("Damage");
            go.transform.position = at;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = world.Font;
            tmp.text = (heal ? "+" : "") + (amount >= 10f ? Mathf.RoundToInt(amount).ToString() : amount.ToString("0.#"));
            tmp.fontSize = 2.4f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = heal ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.85f, 0.35f);
            tmp.rectTransform.sizeDelta = new Vector2(2f, 0.6f);
            go.AddComponent<FaceUser>();
            go.AddComponent<Rise>();
        }

        /// <summary>Breaks into pieces that tumble away: a crate into planks, a window into shards, a wall into rubble.</summary>
        static void Shatter(World world, GameObject go, string kind, Rules.Subject subject)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.one);
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) b.Encapsulate(c.bounds);
            Color tint = kind == "glass" ? new Color(0.75f, 0.9f, 1f) : kind == "stone" ? new Color(0.6f, 0.58f, 0.55f) : kind == "metal" ? new Color(0.55f, 0.58f, 0.62f) : new Color(0.6f, 0.42f, 0.25f);
            int pieces = Mathf.Clamp(Mathf.RoundToInt(b.size.magnitude * 3f), 4, 10);
            AssetDef chunk = Library.Asset("cube");
            for (int i = 0; i < pieces && chunk != null; i++)
            {
                Vector3 p = b.center + Vector3.Scale(Random.insideUnitSphere * 0.5f, b.size);
                float s = Mathf.Clamp(b.size.magnitude * Random.Range(0.08f, 0.18f), 0.05f, 0.8f);
                Prop piece = world.Props.SpawnLoose("debris", chunk, p, Random.rotation, new Vector3(s, s * (kind == "glass" ? 0.15f : 0.6f), s), tint);
                if (piece != null && piece.Body != null) piece.Body.linearVelocity = (p - b.center).normalized * Random.Range(2f, 5f) + Vector3.up * 2f;
                if (piece != null) Destroy(piece.gameObject, 8f);
            }
            Rules.Burst("dust", b.center);
            Sfx.Play(kind == "glass" ? "glass" : "crash", b.center, 0.9f);
            if (subject.Hit != null) subject.Hit.Remove();
            else if (subject.Prop != null) world.Props.Destroy(subject.Prop);
        }
    }

    /// <summary>Floats up and fades, then is gone. For damage numbers.</summary>
    public sealed class Rise : MonoBehaviour
    {
        float _t;
        TextMeshPro _text;
        void Start() { _text = GetComponent<TextMeshPro>(); }
        void Update()
        {
            _t += Time.deltaTime;
            transform.position += Vector3.up * Time.deltaTime * 0.8f;
            if (_text != null) { Color c = _text.color; c.a = Mathf.Clamp01(1.2f - _t); _text.color = c; }
            if (_t > 1.2f) Destroy(gameObject);
        }
    }

    /// <summary>
    /// Plain scenery that can be hit, pressed and counted: a treasure chest, a lever, a target on a
    /// wall, a building to knock down. Scenery is drawn combined and has no object of its own, so
    /// this sits on the body it is given (ObjectSystem.Solidify) and says which thing it is.
    /// </summary>
    public sealed class Hittable : MonoBehaviour
    {
        public static readonly List<Hittable> All = new List<Hittable>();
        public string Group, Asset;
        float _last = -1f;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void OnCollisionEnter(Collision collision)
        {
            Prop source = collision.collider.GetComponentInParent<Prop>();
            if (source == null || World.Instance == null || Time.time - _last < 0.2f) return;
            float speed = collision.relativeVelocity.magnitude;
            bool shot = source.GroupId == "shot";
            if (!shot && speed < 2.5f && !source.Held) return;
            _last = Time.time;
            Rules.Subject s = Rules.Of(this);
            s.Amount = shot ? source.Damage : source.Held ? Mathf.Max(1f, speed * 0.4f) : speed * 0.25f;
            World.Instance.Rules.Fire("hit", s);
            if (shot && source != null) Destroy(source.gameObject);
        }

        /// <summary>This one thing goes, not the whole group it was drawn with.</summary>
        public void Remove()
        {
            World world = World.Instance;
            if (world != null)
            {
                Group g = world.Objects.Get(Group);
                if (g != null)
                {
                    // a travelling thing is one of the group's extras; a standing one is one of its placements
                    if (GetComponent<Mover>() != null) { g.Extras.Remove(gameObject); Destroy(gameObject); return; }
                    Vector3 here = transform.position;
                    int best = -1;
                    float bestD = 0.25f;
                    for (int i = 0; i < g.Items.Count; i++)
                    {
                        Vector3 p = g.Items[i].Position;
                        float d = new Vector2(p.x - here.x, p.z - here.z).magnitude;
                        if (d < bestD) { bestD = d; best = i; }
                    }
                    if (best >= 0) { world.Objects.Take(g, best); return; }
                }
            }
            Destroy(gameObject);
        }
    }
}
