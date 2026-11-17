using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// One generator for the face of any building: floors of windows with frames and sills, a door
    /// (or a shopfront with an awning), a cornice, and a roof. Windows glow warm after dark, a few
    /// left dark so a street of them looks lived in. Used by the `building` part shape of `make` and
    /// by the library's town houses, towers and warehouses. Stands on y = 0, front to +Z.
    /// </summary>
    public static class Architecture
    {
        public static Color StyleColor(string style)
        {
            switch (style)
            {
                case "brick": return Pal.Brick;
                case "glass": return new Color(0.32f, 0.45f, 0.55f);
                case "concrete": return Pal.Concrete;
                case "stone": return new Color(0.72f, 0.68f, 0.6f);
                case "wood": return Pal.Wood;
                case "metal": return new Color(0.55f, 0.58f, 0.62f);
                case "neon": return new Color(0.16f, 0.14f, 0.22f);
                case "adobe": return new Color(0.82f, 0.66f, 0.48f);
                default: return Pal.Plaster;
            }
        }

        /// <summary>The `building` shape of a `make` part.</summary>
        public static void Facade(MeshBuilder mb, Vector3 size, Statement part)
        {
            string style = part.Str("style", "plain");
            Color wall = part.TryColor("color", out Color c) ? c : StyleColor(style);
            Color trim = part.TryColor("trim", out Color t) ? t : Pal.Dark(wall, 0.35f);
            int floors = part.Has("floors") ? Mathf.Clamp(part.Int("floors"), 1, 90) : Mathf.Max(1, Mathf.RoundToInt(size.y / 3.2f));
            Build(mb, size, floors, style, part.Str("roof", "flat"), wall, trim, part.Flag("shop"), part.Num("lit", 0.75f), part.Int("seed", 1));
        }

        public static void Build(MeshBuilder mb, Vector3 size, int floors, string style, string roof, Color wall, Color trim, bool shop, float lit, int seed)
        {
            float w = Mathf.Max(size.x, 1f), h = Mathf.Max(size.y, 2f), d = Mathf.Max(size.z, 1f);
            float storey = h / floors;
            bool glass = style == "glass";
            bool neon = style == "neon";

            // the mass of it
            mb.Col(wall).BoxOn(Vector3.zero, new Vector3(w, h, d));
            // a plinth, and a cornice band at the top
            mb.Col(Pal.Dark(wall, 0.25f)).BoxOn(Vector3.zero, new Vector3(w + 0.12f, Mathf.Min(0.5f, storey * 0.18f), d + 0.12f));
            mb.Col(trim).BoxOn(new Vector3(0f, h - 0.25f, 0f), new Vector3(w + 0.3f, 0.3f, d + 0.3f));

            // windows on all four faces
            int columnsFront = Mathf.Max(1, Mathf.RoundToInt(w / (glass ? 1.6f : 2.4f)));
            int columnsSide = Mathf.Max(1, Mathf.RoundToInt(d / (glass ? 1.6f : 2.4f)));
            Color pane = glass ? new Color(0.35f, 0.55f, 0.7f) : neon ? new Color(0.2f, 0.25f, 0.35f) : Pal.GlassDark;
            uint hash = (uint)(seed * 2654435761u);
            for (int face = 0; face < 4; face++)
            {
                bool frontBack = face == 0 || face == 2;
                int columns = frontBack ? columnsFront : columnsSide;
                float span = frontBack ? w : d, depth = frontBack ? d : w;
                Quaternion turn = Quaternion.Euler(0f, face * 90f, 0f);
                for (int f = 0; f < floors; f++)
                {
                    if (f == 0 && face == 0 && (shop || floors > 1)) continue;   // the ground floor of the front is a door or a shop
                    for (int i = 0; i < columns; i++)
                    {
                        hash = hash * 1664525u + 1013904223u;
                        bool dark = (hash >> 24) / 255f > lit;
                        float x = -span * 0.5f + span * (i + 0.5f) / columns;
                        float y = storey * f + storey * 0.55f;
                        float ww = span / columns * (glass ? 0.9f : 0.55f), wh = storey * (glass ? 0.82f : 0.55f);
                        Vector3 at = turn * new Vector3(x, y, depth * 0.5f + 0.02f);
                        Vector3 sizeW = turn * new Vector3(ww, wh, 0.06f);
                        sizeW = new Vector3(Mathf.Abs(sizeW.x), Mathf.Abs(sizeW.y), Mathf.Abs(sizeW.z));
                        if (!glass) mb.Emit(0f).Col(trim).Box(at - turn * new Vector3(0f, wh * 0.5f + 0.05f, -0.03f), Abs(turn * new Vector3(ww + 0.18f, 0.1f, 0.14f)));   // sill
                        if (dark) mb.Emit(0f).Col(Pal.Dark(pane, 0.4f)); else mb.Col(pane).Window();
                        mb.Box(at, sizeW);
                        mb.Emit(0f);
                    }
                }
            }

            // the way in
            if (shop)
            {
                float sh = Mathf.Min(storey * 0.75f, 3.2f);
                mb.Col(new Color(0.75f, 0.85f, 0.9f)).Window().Box(new Vector3(0f, sh * 0.5f + 0.25f, d * 0.5f + 0.03f), new Vector3(w * 0.8f, sh, 0.06f));
                mb.Emit(0f);
                hash = hash * 1664525u + 1013904223u;
                Color awning = Awnings[(hash >> 26) & 3];
                mb.Col(awning).Push(new Vector3(0f, sh + 0.45f, d * 0.5f + 0.55f), new Vector3(-20f, 0f, 0f)).BoxOn(Vector3.zero, new Vector3(w * 0.86f, 0.08f, 1.1f)).Pop();
                mb.Col(Pal.Dark(awning, 0.3f)).Box(new Vector3(0f, sh + 0.15f, d * 0.5f + 1.05f), new Vector3(w * 0.86f, 0.3f, 0.04f));
                mb.Col(trim).Box(new Vector3(0f, sh + 0.85f, d * 0.5f + 0.06f), new Vector3(w * 0.7f, 0.5f, 0.06f));   // where the shop's name goes
            }
            else if (floors > 1)
            {
                float dh = Mathf.Min(2.3f, storey * 0.8f);
                mb.Col(Pal.WoodDark).Box(new Vector3(0f, dh * 0.5f + 0.2f, d * 0.5f + 0.03f), new Vector3(1.1f, dh, 0.08f));
                mb.Col(trim).Box(new Vector3(0f, dh + 0.3f, d * 0.5f + 0.06f), new Vector3(1.5f, 0.15f, 0.12f));
                mb.Col(Pal.WindowLit).Emit(0f, 1f).Box(new Vector3(0.75f, dh * 0.75f, d * 0.5f + 0.1f), new Vector3(0.12f, 0.18f, 0.12f));
                mb.Emit(0f);
            }

            if (neon)
            {
                // signs that glow in colour down the corners
                mb.Col(Pal.Magenta).Emit(2f).Box(new Vector3(w * 0.5f + 0.06f, h * 0.55f, d * 0.5f - 0.3f), new Vector3(0.1f, h * 0.5f, 0.4f));
                mb.Col(Pal.Neon).Emit(2f).Box(new Vector3(-w * 0.5f - 0.06f, h * 0.4f, d * 0.5f - 0.3f), new Vector3(0.1f, h * 0.35f, 0.4f));
                mb.Emit(0f);
            }

            Roof(mb, roof, w, h, d, trim, wall);
        }

        static readonly Color[] Awnings = { Pal.Red, Pal.Green, Pal.Blue, Pal.Orange };

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static void Roof(MeshBuilder mb, string roof, float w, float h, float d, Color trim, Color wall)
        {
            Color tiles = Pal.Roof;
            switch (roof)
            {
                case "gable":
                {
                    // the ridge runs across the front: a triangle seen from the side, as wide as the house
                    float rh = Mathf.Min(d, 8f) * 0.45f;
                    mb.Col(tiles).Push(new Vector3(0f, h, 0f)).Slab(new[] { new Vector2(-d * 0.55f, 0f), new Vector2(d * 0.55f, 0f), new Vector2(0f, rh) }, w * 1.06f).Pop();
                    break;
                }
                case "hip": mb.Col(tiles).Pyramid(new Vector3(0f, h, 0f), Mathf.Max(w, d) * 1.08f, Mathf.Min(w, d) * 0.4f); break;
                case "spire":
                    mb.Col(trim).BoxOn(new Vector3(0f, h, 0f), new Vector3(w * 0.4f, h * 0.08f, d * 0.4f));
                    mb.Col(Pal.Metal).Cone(new Vector3(0f, h * 1.08f, 0f), Mathf.Min(w, d) * 0.18f, h * 0.25f, 8);
                    break;
                case "dome": mb.Col(new Color(0.55f, 0.7f, 0.65f)).Dome(new Vector3(0f, h, 0f), Mathf.Min(w, d) * 0.45f, Mathf.Min(w, d) * 0.4f, 16, 6); break;
                case "stepped":
                    for (int i = 1; i <= 3; i++)
                    {
                        float k = 1f - i * 0.22f;
                        mb.Col(i % 2 == 0 ? trim : wall).BoxOn(new Vector3(0f, h + (i - 1) * h * 0.06f, 0f), new Vector3(w * k, h * 0.06f, d * k));
                    }
                    mb.Col(Pal.Metal).Cone(new Vector3(0f, h * 1.18f, 0f), Mathf.Min(w, d) * 0.1f, h * 0.12f, 8);
                    break;
                case "shed":
                    mb.Col(Pal.RoofDark).Push(new Vector3(0f, h, 0f), new Vector3(-8f, 0f, 0f)).BoxOn(Vector3.zero, new Vector3(w * 1.06f, 0.15f, d * 1.1f)).Pop();
                    break;
                default:
                    // a flat roof: a parapet round it, and the machinery that lives up there
                    mb.Col(trim).BoxOn(new Vector3(0f, h, d * 0.5f - 0.1f), new Vector3(w, 0.6f, 0.2f));
                    mb.Col(trim).BoxOn(new Vector3(0f, h, -d * 0.5f + 0.1f), new Vector3(w, 0.6f, 0.2f));
                    mb.Col(trim).BoxOn(new Vector3(w * 0.5f - 0.1f, h, 0f), new Vector3(0.2f, 0.6f, d));
                    mb.Col(trim).BoxOn(new Vector3(-w * 0.5f + 0.1f, h, 0f), new Vector3(0.2f, 0.6f, d));
                    if (w > 6f) mb.Col(Pal.MetalDark).BoxOn(new Vector3(w * 0.2f, h, -d * 0.15f), new Vector3(Mathf.Min(2.4f, w * 0.25f), 1.4f, Mathf.Min(1.8f, d * 0.2f)));
                    break;
            }
        }
    }
}
