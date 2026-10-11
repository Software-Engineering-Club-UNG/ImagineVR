using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// The user's surroundings. Applies ImagineScript world statements by handing them to the
    /// system that owns that part of the world. With every system at zero, this is the dark room.
    /// </summary>
    public sealed class World : MonoBehaviour
    {
        public const float SafeRadius = 3f;
        public const float Radius = 120f;
        public StructureSystem Structures { get; private set; }
        public float SurfaceHeight(float x, float z)
        {
            float floor = Structures != null ? Structures.FloorAt(new Vector3(x, 0, z)) : float.NaN;
            return float.IsNaN(floor) ? Terrain.HeightAt(x, z) : floor;
        }

        public static World Instance { get; private set; }

        public SkySystem Sky { get; private set; }
        public TerrainSystem Terrain { get; private set; }
        public ObjectSystem Objects { get; private set; }
        public PropSystem Props { get; private set; }
        public WeatherSystem Weather { get; private set; }
        public AmbienceSystem Ambience { get; private set; }
        public Rules Rules { get; private set; }

        /// <summary>The user's head. Used to place things in reach and to aim sounds.</summary>
        public Transform Head;
        public TMP_FontAsset Font;
        /// <summary>What carries the user about. Null when the world is drawn without a rig.</summary>
        public Vehicle Vehicle;

        /// <summary>Dim terminal lines, e.g. a substitution or a limit being applied.</summary>
        public event Action<string> Noted;
        /// <summary>Things that happen in the world and deserve a line in the terminal.</summary>
        public event Action<string> Happened;
        /// <summary>Something was put in reach for the user to take.</summary>
        public event Action<Prop> Given;
        /// <summary>The world went back to the dark room with nothing left in it.</summary>
        public event Action Emptied;

        bool _clearGroundAtTurnEnd, _clearSkyAtTurnEnd;
        bool _everLit;

        /// <summary>True in the dark room before anything has been built: the next place is built around wherever the user is looking.</summary>
        public bool Unbuilt => Sky.IsDark && !Structures.Active && Objects.Count == 0 && !Terrain.HasGround && !Terrain.Generating;

        internal void OnGiven(Prop prop) { Given?.Invoke(prop); }

        /// <summary>
        /// Records, for the session log, where something asked for actually ended up and what stood
        /// in for it: the first thing to read when "it said it made one but I can't see it".
        /// </summary>
        public void Placed(Statement s, string asked, AssetDef used, bool standIn, Vector3 position, string problem, int still = -1, int alive = -1)
        {
            float distance = 0f, bearing = 0f;
            if (Head != null)
            {
                Vector3 to = position - Head.position, look = Head.forward;
                to.y = 0f;
                look.y = 0f;
                distance = to.magnitude;
                if (to.sqrMagnitude > 0.0001f && look.sqrMagnitude > 0.0001f) bearing = Vector3.SignedAngle(look, to, Vector3.up);
            }
            SessionLog.Write("placed", "op", s.Op, "id", s.Str("id"), "asked", asked, "used", used != null ? used.Id : null,
                "stand_in", standIn, "at", position, "metres_from_user", Math.Round(distance, 2), "degrees_from_gaze", Math.Round(bearing, 1),
                "in_view", Mathf.Abs(bearing) < 45f && distance < 60f, "problem", problem,
                "still", still < 0 ? (object)null : still, "alive", alive < 0 ? (object)null : alive, "made", used != null && used.Made);
        }

        /// <summary>True until the first sky statement brings light into the dark room.</summary>
        public bool IsDark => Sky.IsDark && !Structures.Active;

        public void Init(Transform head, TMP_FontAsset font)
        {
            Instance = this;
            Head = head;
            Font = font;
            Library.EnsureLoaded();
            Sky = new SkySystem();
            Sky.SetWorldRadius(Radius);
            Terrain = new TerrainSystem(transform, SafeRadius);
            Objects = new ObjectSystem(this);
            Structures = new StructureSystem(this);
            Props = new PropSystem(this);
            Weather = new WeatherSystem(this);
            Ambience = new AmbienceSystem(this);
            Rules = new Rules(this);
            Terrain.Changed += OnGroundChanged;
        }

        void OnGroundChanged()
        {
            Objects.Resettle();
        }

        public void Note(string text) { SessionLog.Write("note", "text", text); Noted?.Invoke(text); }
        public void Emit(string text) { Happened?.Invoke(text); }

        /// <summary>Applies one world statement. Unknown operations are ignored.</summary>
        public void Apply(Statement s)
        {
            // The first thing built in the dark room is built around the user as they stand: they
            // are brought to the middle of the world facing along it, whichever way they were turned.
            if (Unbuilt && Vehicle != null && Builds(s.Op)) Vehicle.Home();
            switch (s.Op)
            {
                case "sky":
                    if (Sky.IsDark && !s.Has("t")) s.Fields["t"] = 4.0;   // the first light takes its time
                    Sky.ApplySky(s);
                    if (s.Has("fog") || s.Has("fog_color"))
                    {
                        // `fog` on a sky is the same thing as a `fog` statement after it
                        Statement fog = Statement.Make("fog", "t", (double)s.T(1.5f));
                        if (s.Has("fog")) fog.Fields["density"] = (double)s.Num("fog");
                        if (s.Has("fog_color")) fog.Fields["color"] = s.Str("fog_color");
                        Sky.ApplyFog(fog);
                    }
                    _everLit = true;
                    _clearSkyAtTurnEnd = false;
                    break;
                case "light": Sky.ApplyLight(s); break;
                case "fog": Sky.ApplyFog(s); break;
                case "terrain":
                    _clearGroundAtTurnEnd = false;
                    EnsureLight();
                    TerrainType type = s.Has("type") ? Library.Resolve(Library.TerrainTypes, s.Str("type")) : null;
                    if (type != null && type.Shape == TerrainShape.None) Terrain.Clear(s.T(2f));
                    else { Terrain.Apply(s); DressGround(); }
                    break;
                case "weather": Weather.Apply(s); break;
                case "physics": Props.SetGravity(s.Num("gravity", 1f)); break;
                case "make": Maker.Make(s); break;
                case "structure": if (Structures.Build(s)) { EnsureLight(); _clearSkyAtTurnEnd = false; } break;
                case "door": Structures.Door(s); break;
                case "spawn": EnsureLight(); Objects.Spawn(s); Structures.TrackFurnishing(s); break;
                case "scatter": EnsureLight(); Objects.Scatter(s); break;
                case "give": EnsureLight(); Props.Give(s); break;
                case "ride": EnsureLight(); if (Vehicle != null) Vehicle.Apply(s); break;
                case "player": if (Vehicle != null) Vehicle.Resize(s.Num("scale", 1f)); break;
                case "modify": Objects.Modify(s); Props.Modify(s); break;
                case "remove": Structures.Remove(s.Str("id")); Objects.Remove(s); Props.Remove(s.Str("id")); Rules.Remove(s.Str("id")); break;
                case "ambience": Ambience.Apply(s); break;
                case "clear": Clear(s); break;
                case "when": Rules.Install(s); break;
            }
        }

        /// <summary>Ids of what grows on the ground by itself. Not the model's: it never sees, counts or removes them.</summary>
        public const string Cover = "ground.";

        /// <summary>
        /// Bare ground looks unfinished, and the model cannot be relied on to cover it: asked for a
        /// pasture in the 20:45 session it scattered thirty tufts over half a hectare. So ground that
        /// would have something growing or lying on it gets it, thick underfoot and thinning away,
        /// whatever else is built. Level ground is left clear: that is what people build on.
        /// </summary>
        void DressGround()
        {
            foreach (string id in new[] { "near", "far", "extra", "stones" }) Objects.Remove(Cover + id, 0.4f);
            if (Terrain.Shape == TerrainShape.None || Terrain.Shape == TerrainShape.Flat) return;
            void Lay(string id, string asset, int count, float from, float to, string tint = null, float small = 0.7f, float big = 1.3f, int clusters = 0)
            {
                Statement cover = Statement.Make("scatter", "id", Cover + id, "asset", asset, "count", (double)count, "area", "around", "t", 2.5,
                    "radius", new List<object> { (double)from, (double)to }, "scale", new List<object> { (double)small, (double)big }, "seed", 7.0);
                if (tint != null) cover.Fields["tint"] = tint;
                if (clusters > 0) cover.Fields["clusters"] = (double)clusters;
                Objects.Scatter(cover);
            }
            switch (Terrain.MaterialId)
            {
                case "grass":
                    Lay("near", "tall_grass", 170, 1.5f, 16f); Lay("far", "tall_grass", 150, 16f, 55f, null, 0.9f, 1.6f);
                    Lay("extra", "flower_patch", 40, 3f, 45f, null, 0.7f, 1.2f, 9); Lay("stones", "rock", 14, 5f, 55f, null, 0.2f, 0.5f);
                    break;
                case "dry_grass":
                    Lay("near", "tall_grass", 150, 1.5f, 16f, "#D2C078"); Lay("far", "tall_grass", 130, 16f, 55f, "#C8B468", 0.9f, 1.6f);
                    Lay("stones", "rock", 22, 4f, 55f, "#B0A080", 0.2f, 0.6f);
                    break;
                case "forest_floor":
                    Lay("near", "fern", 110, 2f, 18f); Lay("far", "fern", 110, 18f, 55f, null, 0.9f, 1.5f);
                    Lay("extra", "mushroom", 36, 2f, 30f, null, 0.6f, 1.1f, 8); Lay("stones", "rock", 18, 5f, 50f, "#6A7060", 0.25f, 0.7f);
                    break;
                case "swamp":
                    Lay("near", "tall_grass", 130, 2f, 18f, "#6A7A3A", 0.9f, 1.5f); Lay("far", "tall_grass", 110, 18f, 55f, "#5A6A34", 1f, 1.8f);
                    Lay("extra", "mushroom", 24, 2f, 28f, null, 0.6f, 1.1f, 6);
                    break;
                case "sand":
                    Lay("stones", "rock", 26, 3f, 55f, "#D8C8A0", 0.15f, 0.45f); Lay("extra", "seashell", 22, 2f, 28f, null, 0.8f, 1.4f);
                    break;
                case "snow":
                    Lay("stones", "rock", 22, 4f, 55f, "#C6CED8", 0.25f, 0.8f); Lay("extra", "ice_crystal", 12, 5f, 40f, null, 0.4f, 0.9f, 4);
                    break;
                case "dirt": case "rock":
                    Lay("near", "rock", 60, 2f, 20f, null, 0.12f, 0.4f); Lay("stones", "rock", 50, 20f, 60f, null, 0.3f, 1f); Lay("extra", "tall_grass", 50, 3f, 45f, "#8A9060", 0.6f, 1f, 10);
                    break;
                case "mars":
                    Lay("near", "rock", 60, 2f, 20f, "#A85A40", 0.12f, 0.4f); Lay("stones", "rock", 50, 20f, 60f, "#9A4E38", 0.3f, 1.1f);
                    break;
                case "moon":
                    Lay("near", "rock", 60, 2f, 20f, "#9A9AA0", 0.1f, 0.35f); Lay("stones", "rock", 50, 20f, 60f, "#8A8A90", 0.3f, 1.1f);
                    break;
                case "lava_rock":
                    Lay("near", "rock", 60, 2f, 20f, "#3A2E30", 0.15f, 0.45f); Lay("stones", "rock", 50, 20f, 60f, "#2E2426", 0.4f, 1.3f);
                    break;
            }
        }

        static bool Builds(string op)
        {
            return op == "sky" || op == "terrain" || op == "spawn" || op == "scatter" || op == "ride" || op == "structure";
        }

        /// <summary>Things should never be built in total darkness: give them a default sky to be seen by.</summary>
        void EnsureLight()
        {
            if (!Sky.IsDark) return;
            Sky.ApplySky(Statement.Make("sky", "preset", "morning", "t", 3.0));
            _everLit = true;
        }

        void Clear(Statement s)
        {
            Structures.Clear();
            float t = s.T(1.2f);
            if (s.Flag("everything"))
            {
                // "reset everything": the dark room, and nothing left, not even what is in their hands
                ResetEverything(Mathf.Max(t, 1.5f));
                return;
            }
            List<string> keep = s.StrList("keep");
            Rules.Clear();
            Objects.Clear(t);
            Props.ClearLoose();
            // A new place is a new start on foot. (The ride used to survive: asked for a maze after a
            // drive round New York, the user found themselves still sitting in the taxi.)
            if (Vehicle != null) Vehicle.Dismount();
            if (!keep.Contains("weather")) Weather.Stop();
            if (!keep.Contains("ambience")) Ambience.Stop(t);
            Props.SetGravity(1f);
            // The ground is only removed if nothing replaces it before the turn ends, so that a
            // new terrain statement can sweep over the old ground instead of popping it away.
            if (!keep.Contains("terrain")) _clearGroundAtTurnEnd = true;
            // Likewise the sky: it stays if anything is built under it this turn, and goes dark
            // (which is the user's own room again, in a headset that shows it) if nothing is.
            if (!keep.Contains("sky")) _clearSkyAtTurnEnd = true;
            if (Vehicle != null) Vehicle.Home();   // the next place is built around the middle of the world
        }

        /// <summary>Called when a turn's `done` arrives.</summary>
        public void EndTurn()
        {
            bool empty = !Structures.Active && Objects.Count == 0 && (_clearGroundAtTurnEnd || (!Terrain.HasGround && !Terrain.Generating));
            if (_clearGroundAtTurnEnd)
            {
                _clearGroundAtTurnEnd = false;
                Terrain.Clear(1.5f);
            }
            if (_clearSkyAtTurnEnd)
            {
                _clearSkyAtTurnEnd = false;
                if (empty && !Sky.IsDark) Sky.Reset(2f);
            }
        }

        /// <summary>Back to the dark room.</summary>
        public void ResetToDark(float seconds)
        {
            Structures.Clear();
            Rules.Clear();
            Objects.Clear(seconds);
            Props.ClearLoose();
            Weather.Stop();
            Ambience.Stop(seconds);
            Props.SetGravity(1f);
            Terrain.Clear(seconds);
            Sky.Reset(seconds);
            _clearGroundAtTurnEnd = _clearSkyAtTurnEnd = false;
            if (Vehicle != null) { Vehicle.Dismount(); Vehicle.Resize(1f); Vehicle.Home(); }
        }

        /// <summary>
        /// The dark room exactly as the app starts in it: nothing built, nothing given, nothing in
        /// the hands, no paint in the air, and (in a headset that can show it) the user's own room.
        /// </summary>
        public void ResetEverything(float seconds)
        {
            ResetToDark(seconds);
            Props.ClearAll();
            Library.ForgetMade();
            SessionLog.Write("reset", "what", "world");
            Emptied?.Invoke();
        }

        /// <summary>A floating name tag, used above stand-in shapes.</summary>
        public void AddLabel(Group group, string text, Vector3 position, float height, float size = 2.2f)
        {
            if (Font == null) return;
            var go = new GameObject("Label");
            go.transform.SetParent(transform, false);
            float ground = Terrain.HeightAt(position.x, position.z);
            go.transform.position = new Vector3(position.x, ground + position.y + height, position.z);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = Font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.85f, 0.6f);
            tmp.rectTransform.sizeDelta = new Vector2(4f, 0.6f);
            go.AddComponent<FaceUser>();
            group.Extras.Add(go);
        }

        void Update()
        {
            Structures.Tick();
            float dt = Time.deltaTime;
            Sky.Tick(dt);
            Terrain.Tick(dt);
            Objects.Tick(dt);
            Props.Tick(dt);
            Weather.Tick(dt);
            Ambience.Tick(dt);

            // In a headset that can show the real room, the dark room is the user's own room.
            _real = Mathf.MoveTowards(_real, QuestFeature.Passthrough && IsDark ? 1f : 0f, dt / 2.5f);
            Shader.SetGlobalFloat(RealId, _real);
            Rules.Tick(dt);
        }

        static readonly int RealId = Shader.PropertyToID("_ImReal");
        float _real;

        void LateUpdate()
        {
            Shadows.Render(this, Time.deltaTime);
            Objects.Render();
        }

        void OnDestroy()
        {
            Structures?.Dispose();
            if (Instance == this) Instance = null;
        }
    }

    /// <summary>Keeps a label turned towards the user.</summary>
    public sealed class FaceUser : MonoBehaviour
    {
        void LateUpdate()
        {
            Transform head = World.Instance != null ? World.Instance.Head : null;
            if (head == null) return;
            Vector3 away = transform.position - head.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(away);
        }
    }
}
