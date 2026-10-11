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

        public static World Instance { get; private set; }

        public SkySystem Sky { get; private set; }
        public TerrainSystem Terrain { get; private set; }
        public ObjectSystem Objects { get; private set; }
        public KitSystem Kits { get; private set; }
        public PropSystem Props { get; private set; }
        public WeatherSystem Weather { get; private set; }
        public AmbienceSystem Ambience { get; private set; }
        public RuleEngine Rules { get; private set; }
        public BlueprintSystem Blueprints { get; private set; }

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
        public bool Unbuilt => Sky.IsDark && Objects.Count == 0 && !Terrain.HasGround && !Terrain.Generating;

        internal void OnGiven(Prop prop) { Given?.Invoke(prop); }

        /// <summary>
        /// Records, for the session log, where something asked for actually ended up and what stood
        /// in for it: the first thing to read when "it said it made one but I can't see it".
        /// </summary>
        public void Placed(Statement s, string asked, AssetDef used, bool standIn, Vector3 position, string problem)
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
                "in_view", Mathf.Abs(bearing) < 45f && distance < 60f, "problem", problem);
        }

        /// <summary>True until the first sky statement brings light into the dark room.</summary>
        public bool IsDark => Sky.IsDark;

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
            Kits = new KitSystem(this);
            Props = new PropSystem(this);
            Weather = new WeatherSystem(this);
            Ambience = new AmbienceSystem(this);
            Rules = new RuleEngine(this);
            Blueprints = new BlueprintSystem(this);
            Terrain.Changed += OnGroundChanged;
        }

        void OnGroundChanged()
        {
            Kits.RebuildAll();
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
                    else Terrain.Apply(s);
                    break;
                case "weather": Weather.Apply(s); break;
                case "physics": Props.SetGravity(s.Num("gravity", 1f)); break;
                case "kit": Kits.Apply(s); _everLit = true; break;
                case "spawn": EnsureLight(); if(!Blueprints.Spawn(s)) Objects.Spawn(s); break;
                case "scatter": EnsureLight(); Objects.Scatter(s); break;
                case "give": EnsureLight(); if(!Blueprints.Spawn(s)) Props.Give(s); break;
                case "ride": EnsureLight(); if (Vehicle != null) Vehicle.Apply(s); break;
                case "goal": Objects.Goal(s); break;
                case "player": if (Vehicle != null) Vehicle.Resize(s.Num("scale", 1f)); break;
                case "modify": Objects.Modify(s); Props.Modify(s); Blueprints.Modify(s); break;
                case "remove": Rules.RemovingGroup(s.Str("id")); Objects.Remove(s); Props.Remove(s.Str("id")); break;
                case "ambience": Ambience.Apply(s); break;
                case "clear": Clear(s); break;
                case "rule": case "rule_remove": Rules.SyncEntities(); Rules.Install(s); break;
                case "blueprint": case "blueprint_remove": case "blueprint_edit": Blueprints.Apply(s); break;
            }
            Rules.SyncEntities();
        }

        static bool Builds(string op)
        {
            return op == "sky" || op == "terrain" || op == "kit" || op == "spawn" || op == "scatter" || op == "ride" || op == "goal";
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
            float t = s.T(1.2f);
            if (s.Flag("everything"))
            {
                // "reset everything": the dark room, and nothing left, not even what is in their hands
                ResetEverything(Mathf.Max(t, 1.5f));
                return;
            }
            List<string> keep = s.StrList("keep");
            Rules.Reset(keep.Contains("rules"));
            Blueprints.Reset();
            Objects.Clear(t);
            Kits.Forget();
            Props.ClearLoose();
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
            bool empty = Objects.Count == 0 && (_clearGroundAtTurnEnd || (!Terrain.HasGround && !Terrain.Generating));
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
            Blueprints.Reset();
            Rules.Reset();
            Rules.PlayerEffectsEnabled = false;
            Objects.Clear(seconds);
            Kits.Forget();
            Props.ClearLoose();
            Weather.Stop();
            Ambience.Stop(seconds);
            Props.SetGravity(1f);
            Terrain.Clear(seconds);
            Sky.Reset(seconds);
            _clearGroundAtTurnEnd = _clearSkyAtTurnEnd = false;
            if (Vehicle != null) { Vehicle.Dismount(); Vehicle.Resize(1f); Vehicle.Home(); }
            Rules.SyncEntities();
        }

        /// <summary>
        /// The dark room exactly as the app starts in it: nothing built, nothing given, nothing in
        /// the hands, no paint in the air, and (in a headset that can show it) the user's own room.
        /// </summary>
        public void ResetEverything(float seconds)
        {
            Blueprints.Reset(true);
            ResetToDark(seconds);
            Props.ClearAll();
            Rules.SyncEntities();
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
            Blueprints.Tick();
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
