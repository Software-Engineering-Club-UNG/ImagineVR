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

        /// <summary>The user's head. Used to place things in reach and to aim sounds.</summary>
        public Transform Head;
        public TMP_FontAsset Font;

        /// <summary>Dim terminal lines, e.g. a substitution or a limit being applied.</summary>
        public event Action<string> Noted;
        /// <summary>Things that happen in the world and deserve a line in the terminal.</summary>
        public event Action<string> Happened;

        bool _clearGroundAtTurnEnd;
        bool _everLit;

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
            Terrain.Changed += OnGroundChanged;
        }

        void OnGroundChanged()
        {
            Kits.RebuildAll();
            Objects.Resettle();
        }

        public void Note(string text) { Noted?.Invoke(text); }
        public void Emit(string text) { Happened?.Invoke(text); }

        /// <summary>Applies one world statement. Unknown operations are ignored.</summary>
        public void Apply(Statement s)
        {
            switch (s.Op)
            {
                case "sky":
                    if (Sky.IsDark && !s.Has("t")) s.Fields["t"] = 4.0;   // the first light takes its time
                    Sky.ApplySky(s);
                    _everLit = true;
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
                case "spawn": EnsureLight(); Objects.Spawn(s); break;
                case "scatter": EnsureLight(); Objects.Scatter(s); break;
                case "give": Props.Give(s); break;
                case "modify": Objects.Modify(s); Props.Modify(s); break;
                case "remove": Objects.Remove(s); Props.Remove(s.Str("id")); break;
                case "ambience": Ambience.Apply(s); break;
                case "clear": Clear(s); break;
            }
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
            List<string> keep = s.StrList("keep");
            float t = s.T(1.2f);
            Objects.Clear(t);
            Kits.Forget();
            Props.ClearLoose();
            if (!keep.Contains("weather")) Weather.Stop();
            if (!keep.Contains("ambience")) Ambience.Stop(t);
            Props.SetGravity(1f);
            // The ground is only removed if nothing replaces it before the turn ends, so that a
            // new terrain statement can sweep over the old ground instead of popping it away.
            if (!keep.Contains("terrain")) _clearGroundAtTurnEnd = true;
        }

        /// <summary>Called when a turn's `done` arrives.</summary>
        public void EndTurn()
        {
            if (_clearGroundAtTurnEnd)
            {
                _clearGroundAtTurnEnd = false;
                Terrain.Clear(1.5f);
            }
        }

        /// <summary>Back to the dark room.</summary>
        public void ResetToDark(float seconds)
        {
            Objects.Clear(seconds);
            Kits.Forget();
            Props.ClearLoose();
            Weather.Stop();
            Ambience.Stop(seconds);
            Props.SetGravity(1f);
            Terrain.Clear(seconds);
            Sky.Reset(seconds);
            _clearGroundAtTurnEnd = false;
        }

        /// <summary>A floating name tag, used above stand-in shapes.</summary>
        public void AddLabel(Group group, string text, Vector3 position, float height)
        {
            if (Font == null) return;
            var go = new GameObject("Label");
            go.transform.SetParent(transform, false);
            float ground = Terrain.HeightAt(position.x, position.z);
            go.transform.position = new Vector3(position.x, ground + position.y + height, position.z);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = Font;
            tmp.text = text;
            tmp.fontSize = 2.2f;
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
        }

        void LateUpdate()
        {
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
