using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ImagineVR
{
    public enum TerrainShape { None, Flat, Rolling, Hills, Mountains, Dunes, Canyon, Valley, Island, Beach, Lake, Ocean, Craters, Plateau, Wetlands, Archipelago, Cliffs, Mesas }

    /// <summary>
    /// Things any ground can be given on top of its shape, so that two meadows are not the same
    /// meadow: water running through it, a trail to follow, rock breaking the surface, steps.
    /// </summary>
    [Flags]
    public enum TerrainFeature { None = 0, River = 1, Pond = 2, Path = 4, Terraces = 8, Outcrops = 16 }

    /// <summary>Everything that decides the form of one piece of ground. Plain values: safe on a worker thread.</summary>
    public struct TerrainRecipe
    {
        public TerrainShape Shape;
        public float Amp;
        public int Seed;
        public TerrainFeature Features;
        public float Roughness;     // 0 smooth, 1 broken, lumpy ground
        public float WaterLevel;    // of the shape itself, relative to its zero; NaN when it has none

        public bool Has(TerrainFeature f) => (Features & f) != 0;
        public bool Watered => !float.IsNaN(WaterLevel) || Has(TerrainFeature.River) || Has(TerrainFeature.Pond);

        /// <summary>Feature names as written in a statement; unknown ones are ignored.</summary>
        public static TerrainFeature Parse(List<string> names)
        {
            TerrainFeature f = TerrainFeature.None;
            foreach (string name in names)
            {
                switch (name)
                {
                    case "river": case "stream": case "creek": f |= TerrainFeature.River; break;
                    case "pond": case "pool": f |= TerrainFeature.Pond; break;
                    case "path": case "trail": case "track": f |= TerrainFeature.Path; break;
                    case "terraces": case "terraced": case "steps": f |= TerrainFeature.Terraces; break;
                    case "outcrops": case "rocks": case "boulders": case "crags": f |= TerrainFeature.Outcrops; break;
                }
            }
            return f;
        }
    }

    public sealed class TerrainType : Entry
    {
        public TerrainShape Shape;
        public string Material = "grass";
        public float Height = 0.3f;
        public float WaterLevel = float.NaN;   // metres relative to the shape's zero; NaN = no water
    }

    public sealed class GroundMaterial : Entry
    {
        public Color Low, High, Steep, Shore;
        public float Patch;
        public Color Water = new Color(0.05f, 0.3f, 0.45f);
    }

    public static partial class Library
    {
        public static readonly List<TerrainType> TerrainTypes = new List<TerrainType>();
        public static readonly List<GroundMaterial> GroundMaterials = new List<GroundMaterial>();

        static void Ground(string id, string desc, string tags, string aliases, string low, string high, string steep, string shore, float patch, string water = null)
        {
            var m = new GroundMaterial
            {
                Id = id, Desc = desc, Tags = Split(tags), Aliases = Split(aliases),
                Low = Pal.Hex(low), High = Pal.Hex(high), Steep = Pal.Hex(steep), Shore = Pal.Hex(shore), Patch = patch,
            };
            if (water != null) m.Water = Pal.Hex(water);
            GroundMaterials.Add(m);
        }

        static void Terrain(string id, TerrainShape shape, string desc, string tags, string aliases, string material, float height, float water = float.NaN)
        {
            TerrainTypes.Add(new TerrainType
            {
                Id = id, Shape = shape, Desc = desc, Tags = Split(tags), Aliases = Split(aliases),
                Material = material, Height = height, WaterLevel = water,
            });
        }

        static void RegisterGround()
        {
            Ground("grass", "Green lawn, dirt on the slopes", "lawn,turf,meadow,field,green", "lawn,turf,meadow", "#5FA84E", "#7DB85A", "#7A6A50", "#C9B98A", 0.18f);
            Ground("dry_grass", "Yellowed savanna grass", "savanna,prairie,straw,yellow", "savanna,prairie,hay", "#B8A85A", "#C8B870", "#8A7A5A", "#CDB888", 0.2f);
            Ground("forest_floor", "Dark mossy forest ground", "moss,woods,jungle,dark green", "moss,jungle floor,undergrowth", "#3F6B3A", "#55803F", "#5A4A38", "#6A6A4A", 0.25f);
            Ground("sand", "Pale beach or desert sand", "beach,desert,dune,yellow", "beach sand,desert sand", "#E3CC9A", "#EED9AA", "#C4A870", "#CDB888", 0.08f, "#1E88A8");
            Ground("snow", "Fresh white snow, grey rock on the slopes", "winter,white,ice,arctic", "snowy,snowfield", "#F2F6FA", "#FFFFFF", "#9AA4B0", "#DDE8F0", 0.05f, "#3A6A8A");
            Ground("rock", "Bare grey stone", "stone,cliff,mountain,grey", "stone,bedrock", "#7C7C80", "#9A9A9E", "#5E6066", "#6A6C70", 0.2f);
            Ground("dirt", "Brown earth", "soil,earth,mud,brown,path", "soil,earth", "#7A5B3E", "#8C6A48", "#5A4330", "#6A5338", 0.2f);
            Ground("asphalt", "Dark road surface", "road,street,tarmac,city,urban", "tarmac,pavement,road", "#3A3C40", "#44464A", "#2C2E32", "#34363A", 0.06f);
            Ground("concrete", "Pale grey concrete", "plaza,pavement,slab,grey", "cement,plaza", "#9A9C9F", "#A8AAAD", "#7A7C80", "#8A8C90", 0.05f);
            Ground("marble", "Polished pale stone floor", "floor,gallery,museum,palace,white", "tile,stone floor,polished floor", "#E8E4DC", "#F4F0E8", "#BDB8AE", "#D8D4CC", 0.04f);
            Ground("moon", "Grey lunar dust", "lunar,regolith,space,grey", "lunar,regolith,moon dust", "#8A8A8E", "#A4A4A8", "#5C5C62", "#6C6C72", 0.12f);
            Ground("mars", "Rust-red planet dust", "red,planet,rust,alien", "martian,red planet,rust", "#B5553A", "#C8694A", "#7A3A2A", "#8A4A3A", 0.15f);
            Ground("lava_rock", "Black volcanic rock", "volcano,basalt,black,burnt,hell", "basalt,volcanic,obsidian", "#2A2224", "#3A2A28", "#1A1416", "#4A2018", 0.2f, "#E0440C");
            Ground("candy", "Pink and mint pastel ground", "pastel,sweet,cute,pink", "pastel,sugar,frosting", "#FFC8E0", "#C8F0E0", "#FFE8A0", "#FFE0F0", 0.1f, "#FF9AC8");
            Ground("neon", "Near-black ground for glowing grids", "dark,grid,retro,tron,black", "tron,grid floor,cyber", "#0A0A1A", "#12122A", "#0A0A1A", "#0A0A1A", 0f, "#200838");
            Ground("ice", "Pale blue ice", "frozen,glacier,arctic,blue", "frozen,glacier", "#BFE3F2", "#E6F6FC", "#8FC0DA", "#A8D4E8", 0.08f, "#2A6A9A");
            Ground("swamp", "Dark wet mud and moss", "mud,marsh,bog,wet", "marsh,bog,mud", "#4A4A30", "#5A5A38", "#3A3626", "#3A4028", 0.2f, "#3A4A2A");
            Ground("seabed", "Pale sea floor sand and silt", "ocean floor,underwater,reef", "sea floor,ocean floor", "#C8B890", "#A8B8A0", "#6A7A70", "#B8A880", 0.2f);

            Terrain("flat", TerrainShape.Flat, "Perfectly level ground", "level,plain,plaza,floor", "level,plain,paved", "grass", 0f);
            Terrain("rolling", TerrainShape.Rolling, "Gentle rolling ground", "gentle,meadow,park,field", "gentle hills,meadow,fairway", "grass", 0.3f);
            Terrain("hills", TerrainShape.Hills, "Big rounded hills", "hilly,highlands,countryside", "hilly,highlands,downs", "grass", 0.4f);
            Terrain("mountains", TerrainShape.Mountains, "Open ground ringed by tall peaks", "peaks,alps,range,epic", "mountain range,alps,peaks", "rock", 0.5f);
            Terrain("dunes", TerrainShape.Dunes, "Rippling sand dunes", "desert,sahara,sand", "sand dunes,desert", "sand", 0.4f);
            Terrain("canyon", TerrainShape.Canyon, "A canyon floor between stepped rock walls", "gorge,ravine,cliffs,western", "gorge,ravine,grand canyon", "mars", 0.5f);
            Terrain("valley", TerrainShape.Valley, "A wide valley rising to slopes on both sides", "vale,glen,basin", "vale,glen", "grass", 0.4f);
            Terrain("island", TerrainShape.Island, "A small island surrounded by sea", "tropical,sea,ocean,atoll", "tropical island,atoll,desert island", "sand", 0.3f, -0.6f);
            Terrain("beach", TerrainShape.Beach, "Shoreline: land behind, sea ahead", "coast,shore,seaside,ocean", "shore,coast,seaside", "sand", 0.3f, -0.45f);
            Terrain("lake", TerrainShape.Lake, "Rolling land with a lake ahead", "pond,water,lakeside", "pond,lakeside,loch", "grass", 0.3f, -0.9f);
            Terrain("ocean", TerrainShape.Ocean, "Open water in every direction, a tiny sandbar underfoot", "sea,water,open water", "open sea,sea,open water", "sand", 0.2f, -0.5f);
            Terrain("craters", TerrainShape.Craters, "Dusty plain pocked with impact craters", "moon,lunar,asteroid,space", "lunar surface,moonscape", "moon", 0.4f);
            Terrain("plateau", TerrainShape.Plateau, "The top of a high mesa with the land far below", "mesa,cliff,summit,overlook,epic", "mesa,cliff top,summit,mountain top", "rock", 0.5f);
            Terrain("wetlands", TerrainShape.Wetlands, "Low marshy ground riddled with pools and winding channels", "marsh,swamp,bog,bayou,fen,delta", "marsh,marshland,bayou,fen,everglades", "swamp", 0.3f, 0f);
            Terrain("archipelago", TerrainShape.Archipelago, "A scatter of small islands across a shallow sea, you on the largest", "islands,sea,tropical,lagoon,keys", "islands,island chain,lagoon,keys", "sand", 0.3f, -0.6f);
            Terrain("cliffs", TerrainShape.Cliffs, "A clifftop meadow that drops sheer to the sea ahead", "coast,cliff,headland,sea,dramatic", "sea cliff,clifftop,headland,cliff edge", "grass", 0.4f, -10f);
            Terrain("mesas", TerrainShape.Mesas, "A wide desert floor studded with flat-topped buttes", "buttes,badlands,western,monument valley,desert", "buttes,badlands,monument valley,mesa country", "mars", 0.4f);
            Terrain("none", TerrainShape.None, "No ground at all: floating in open space or sky", "void,space,floating,empty", "no ground,nothing,floating", "neon", 0f);
        }
    }

    /// <summary>Pure height maths. No Unity objects, so it can run on a worker thread.</summary>
    public static class TerrainMath
    {
        public const int N = 129;            // vertices per side
        public const float Extent = 125f;    // half-size of the mesh in metres

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        public static float Noise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
            fy = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        public static float Fbm(float x, float y, int seed, int octaves = 4)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(x, y, seed + i * 31) * amp;
                norm += amp;
                x = x * 2.03f + 11.7f; y = y * 2.03f + 5.3f; amp *= 0.5f;
            }
            return sum / norm;
        }

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Height before it is flattened under the user.</summary>
        public static float Raw(TerrainShape shape, float amp, int seed, float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            switch (shape)
            {
                case TerrainShape.Rolling:
                    return (Fbm(x / 45f, z / 45f, seed) - 0.5f) * 2f * (2f + 10f * amp);
                case TerrainShape.Hills:
                    return (Fbm(x / 70f, z / 70f, seed) - 0.45f) * (10f + 40f * amp);
                case TerrainShape.Mountains:
                {
                    float near = (Fbm(x / 40f, z / 40f, seed) - 0.5f) * 3f;
                    float ring = Smooth(35f, 95f, r);
                    float ridge = 1f - Mathf.Abs(2f * Fbm(x / 60f, z / 60f, seed + 7, 5) - 1f);
                    return near + Mathf.Pow(ridge, 1.6f) * (30f + 60f * amp) * ring;
                }
                case TerrainShape.Dunes:
                {
                    float warp = Fbm(x / 40f, z / 40f, seed) * 4f;
                    float crest = 1f - Mathf.Abs(Mathf.Sin(x / 14f + z / 37f + warp));
                    return crest * (2f + 6f * amp) * (0.5f + Fbm(x / 80f, z / 80f, seed + 3));
                }
                case TerrainShape.Canyon:
                {
                    float side = Mathf.Abs(x + Mathf.Sin(z / 25f + seed) * 6f);
                    float wall = Smooth(9f, 26f, side);
                    float h = wall * (14f + 30f * amp) * (0.7f + 0.6f * Fbm(x / 30f, z / 30f, seed));
                    float stepped = Mathf.Round(h / 4f) * 4f;
                    return Mathf.Lerp(h, stepped, 0.6f) + (Fbm(x / 12f, z / 12f, seed + 5) - 0.5f) * 0.8f;
                }
                case TerrainShape.Valley:
                {
                    float side = Mathf.Abs(x + (Fbm(z / 60f, 0.3f, seed) - 0.5f) * 30f);
                    return Mathf.Pow(Smooth(6f, 95f, side), 1.3f) * (20f + 40f * amp) + (Fbm(x / 35f, z / 35f, seed + 2) - 0.5f) * 3f;
                }
                case TerrainShape.Island:
                {
                    float land = Smooth(44f, 18f, r + (Fbm(x / 20f, z / 20f, seed) - 0.5f) * 16f);
                    return land * (1.2f + Fbm(x / 30f, z / 30f, seed + 4) * (2f + 6f * amp)) - (1f - land) * 3f;
                }
                case TerrainShape.Beach:
                {
                    float shore = 9f + Mathf.Sin(x / 22f + seed) * 3f;
                    float s = z - shore;   // positive = seaward
                    if (s > 0f) return Mathf.Max(-s * 0.07f, -4f);
                    float inland = Mathf.Min(-s * 0.09f, 1.6f);
                    float dunes = Smooth(12f, 40f, -s) * (Fbm(x / 25f, z / 25f, seed) - 0.3f) * (2f + 8f * amp);
                    return inland + Mathf.Max(dunes, 0f);
                }
                case TerrainShape.Lake:
                {
                    float dx = x, dz = z - 28f;
                    float d = Mathf.Sqrt(dx * dx + dz * dz) + (Fbm(x / 15f, z / 15f, seed) - 0.5f) * 8f;
                    float basin = Smooth(21f, 10f, d);
                    return (Fbm(x / 45f, z / 45f, seed + 1) - 0.4f) * (2f + 8f * amp) * (1f - basin) - basin * 3.5f;
                }
                case TerrainShape.Ocean:
                {
                    float land = Smooth(9f, 4f, r);
                    return Mathf.Lerp(-4f + (Fbm(x / 30f, z / 30f, seed) - 0.5f), 0.3f, land);
                }
                case TerrainShape.Craters:
                {
                    float h = (Fbm(x / 60f, z / 60f, seed) - 0.5f) * (2f + 8f * amp);
                    for (int i = 0; i < 16; i++)
                    {
                        float ang = Hash(i, 1, seed) * Mathf.PI * 2f;
                        float dist = 14f + Hash(i, 2, seed) * 85f;
                        float radius = 5f + Hash(i, 3, seed) * 13f;
                        float cx = Mathf.Cos(ang) * dist, cz = Mathf.Sin(ang) * dist;
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz)) / radius;
                        if (d > 1.6f) continue;
                        float bowl = d < 1f ? (d * d - 1f) : 0f;
                        float rim = Mathf.Exp(-((d - 1f) / 0.2f) * ((d - 1f) / 0.2f)) * 0.45f;
                        h += radius * 0.2f * (bowl + rim);
                    }
                    return h;
                }
                case TerrainShape.Plateau:
                {
                    float edge = r + (Fbm(x / 12f, z / 12f, seed) - 0.5f) * 7f;
                    float drop = Smooth(16f, 25f, edge) * (22f + 30f * amp);
                    float below = (Fbm(x / 50f, z / 50f, seed + 9) - 0.5f) * 10f * Smooth(25f, 60f, r);
                    return -drop + below;
                }
                case TerrainShape.Wetlands:
                {
                    // the user's own tussock is always dry; the low places between the others hold water
                    float n = (Fbm(x / 20f, z / 20f, seed) - 0.45f) * (2.5f + 5f * amp);
                    return Mathf.Lerp(0.45f, n, Smooth(2f, 16f, r));
                }
                case TerrainShape.Archipelago:
                {
                    float home = Smooth(30f, 11f, r + (Fbm(x / 14f, z / 14f, seed) - 0.5f) * 10f);
                    float others = Smooth(0.54f, 0.64f, Fbm(x / 34f, z / 34f, seed + 3)) * Smooth(26f, 44f, r);
                    float land = Mathf.Max(home, others);
                    return land * (1f + Fbm(x / 24f, z / 24f, seed + 4) * (2f + 9f * amp)) - (1f - land) * 3f;
                }
                case TerrainShape.Cliffs:
                {
                    float edge = 15f + (Fbm(x / 18f, 0.7f, seed) - 0.5f) * 14f + Mathf.Sin(x / 37f + seed) * 5f;
                    float s = z - edge;   // positive = out over the sea
                    float top = (Fbm(x / 40f, z / 40f, seed + 2) - 0.5f) * (1.5f + 5f * amp) + Mathf.Max(-s - 20f, 0f) * 0.05f * amp;
                    float fall = Smooth(-1.5f, 4f, s);
                    float seabed = -14f - Smooth(0f, 60f, s) * 4f + (Fbm(x / 9f, z / 9f, seed + 5) - 0.5f) * 3f * (1f - Smooth(6f, 14f, s));
                    return Mathf.Lerp(top, seabed, fall);
                }
                case TerrainShape.Mesas:
                {
                    float floor = (Fbm(x / 50f, z / 50f, seed) - 0.5f) * 2f;
                    float v = Fbm(x / 42f, z / 42f, seed + 6, 3);
                    float butte = Smooth(0.55f, 0.6f, v) * Smooth(16f, 34f, r);
                    float tall = (9f + 26f * amp) * (0.55f + 0.9f * Noise(x / 85f + 3.1f, z / 85f + 7.7f, seed + 8));
                    float skirt = Smooth(0.47f, 0.56f, v) * Smooth(16f, 34f, r) * 2.5f;
                    return floor + skirt + butte * (Mathf.Round(tall / 5f) * 5f);
                }
                default:
                    return 0f;
            }
        }

        /// <summary>How far below the user's feet the water lies when a river or pond is asked of ground that has no water of its own.</summary>
        public const float FeatureWater = -0.8f;

        /// <summary>The middle of the river, as a distance in front of the middle of the world, at this x.</summary>
        static float RiverZ(int seed, float x)
        {
            return 21f + Mathf.Sin(x / 31f + seed * 1.7f) * 6f + (Fbm(x / 55f, 0.4f, seed + 71) - 0.5f) * 10f;
        }

        /// <summary>Where the trail runs, as a distance to the side, at this z. It starts under the user.</summary>
        static float PathX(int seed, float z)
        {
            return Mathf.Sin(z / 23f + seed) * 7f * Smooth(0f, 26f, Mathf.Abs(z)) + (Fbm(z / 60f, 2.3f, seed + 81) - Fbm(0f, 2.3f, seed + 81)) * 24f;
        }

        /// <summary>
        /// The finished height at a point, measured from the ground under the user, with the
        /// recipe's features worked in. `water` is the level of the water plane (NaN for none).
        /// `rock` and `trail` say how much of each shows there, for the colouring.
        /// </summary>
        public static float Height(in TerrainRecipe k, float h0, float water, float x, float z, out float rock, out float trail)
        {
            rock = trail = 0f;
            float h = Raw(k.Shape, k.Amp, k.Seed, x, z) - h0;
            if (k.Shape == TerrainShape.None) return h;
            float r = Mathf.Sqrt(x * x + z * z);

            if (k.Roughness > 0f)
            {
                h += ((Fbm(x / 11f, z / 11f, k.Seed + 41, 3) - 0.5f) * 3.2f + (Noise(x / 3.3f, z / 3.3f, k.Seed + 43) - 0.5f) * 0.7f) * k.Roughness;
            }
            if (k.Has(TerrainFeature.Outcrops))
            {
                float m = Smooth(0.61f, 0.68f, Fbm(x / 13f, z / 13f, k.Seed + 61, 3)) * Smooth(7f, 16f, r);
                float crag = 1f - Mathf.Abs(2f * Fbm(x / 5f, z / 5f, k.Seed + 63, 3) - 1f);
                h += m * (0.5f + crag * 3.2f);
                rock = m;
            }
            if (k.Has(TerrainFeature.Terraces))
            {
                const float step = 2.2f;
                float f = h / step, whole = Mathf.Floor(f), part = f - whole;
                h = Mathf.Lerp(h, (whole + Smooth(0.35f, 0.65f, part)) * step, Smooth(8f, 20f, r));
            }
            if (k.Has(TerrainFeature.Path))
            {
                float wide = 1.3f + r * 0.03f;
                trail = 1f - Smooth(wide, wide + 1.3f, Mathf.Abs(x - PathX(k.Seed, z)));
                rock *= 1f - trail;
            }
            if (!float.IsNaN(water))
            {
                // A river or a pond asked of dry ground is the only water there: the rest of the
                // land is kept above it, or every hollow for a mile would fill and drown the place.
                if (float.IsNaN(k.WaterLevel))
                {
                    float dry = water + 1.4f;
                    if (h < dry) h = dry - (dry - h) / (1f + (dry - h));
                }
                float bed = water - 1.1f;
                if (k.Has(TerrainFeature.River))
                {
                    // across the view in front of the user; along the floor where the shape has one
                    float d;
                    if (k.Shape == TerrainShape.Canyon) d = Mathf.Abs(x + Mathf.Sin(z / 25f + k.Seed) * 6f - 5f) + 1.5f;
                    else if (k.Shape == TerrainShape.Valley) d = Mathf.Abs(x + (Fbm(z / 60f, 0.3f, k.Seed) - 0.5f) * 30f - 9f);
                    else d = Mathf.Abs(z - RiverZ(k.Seed, x));
                    float wide = 1.4f + Fbm(x / 20f, z / 20f, k.Seed + 73) * 1.6f;
                    float cut = 1f - Smooth(wide, wide + 3.5f + Mathf.Max(h - water, 0f) * 0.7f, d);
                    if (h > bed) h = Mathf.Lerp(h, bed, cut);
                    trail *= 1f - Smooth(0.2f, 0.7f, cut);
                }
                if (k.Has(TerrainFeature.Pond))
                {
                    float side = (k.Seed & 1) == 0 ? 1f : -1f;
                    float px = x - side * 11f, pz = z - 15f;
                    float d = Mathf.Sqrt(px * px + pz * pz) + (Fbm(x / 7f, z / 7f, k.Seed + 75) - 0.5f) * 4f;
                    float cut = 1f - Smooth(5f, 10f + Mathf.Max(h - water, 0f) * 0.9f, d);
                    if (h > bed) h = Mathf.Lerp(h, bed, cut);
                    trail *= 1f - Smooth(0.2f, 0.7f, cut);
                }
            }
            return h;
        }

        /// <summary>World coordinate of grid line i. Cells are small near the user and large far away.</summary>
        public static float Coord(int i)
        {
            float u = (i / (float)(N - 1)) * 2f - 1f;
            float a = Mathf.Abs(u);
            return Mathf.Sign(u) * Extent * (0.25f * a + 0.75f * Mathf.Pow(a, 2.4f));
        }
    }

    /// <summary>The result of generating one terrain: plain arrays, ready to become a mesh.</summary>
    public sealed class TerrainData
    {
        public TerrainShape Shape;
        public float[] Coords;      // N grid coordinates, shared by X and Z
        public float[] Heights;     // N * N, index = z * N + x
        public Vector3[] Vertices, Normals;
        public Color[] Colors;
        public int[] Triangles;
        public float WaterY = float.NaN;
        public Color WaterColor;
        public float Min, Max;      // lowest and highest ground

        /// <param name="patches">A second ground that shows through the first in large blotches, or null.</param>
        public static TerrainData Generate(TerrainRecipe recipe, GroundMaterial mat, GroundMaterial patches, bool linear, float safeRadius)
        {
            int n = TerrainMath.N;
            TerrainShape shape = recipe.Shape;
            int seed = recipe.Seed;
            var data = new TerrainData { Shape = shape, Coords = new float[n], Heights = new float[n * n], WaterColor = mat.Water };
            for (int i = 0; i < n; i++) data.Coords[i] = TerrainMath.Coord(i);

            float h0 = TerrainMath.Raw(shape, recipe.Amp, seed, 0f, 0f);
            if (!float.IsNaN(recipe.WaterLevel)) data.WaterY = recipe.WaterLevel - h0;
            else if (recipe.Watered && shape != TerrainShape.None) data.WaterY = TerrainMath.FeatureWater;
            var rocks = new float[n * n];
            var trails = new float[n * n];
            float min = float.MaxValue, max = float.MinValue;
            for (int j = 0; j < n; j++)
            {
                float z = data.Coords[j];
                for (int i = 0; i < n; i++)
                {
                    float x = data.Coords[i];
                    float r = Mathf.Sqrt(x * x + z * z);
                    float t = Mathf.Clamp01((r - safeRadius) / 8f);
                    t = t * t * (3f - 2f * t);
                    float h = TerrainMath.Height(recipe, h0, data.WaterY, x, z, out float rock, out float trail) * t;
                    data.Heights[j * n + i] = h;
                    rocks[j * n + i] = rock * t;
                    trails[j * n + i] = trail;
                    if (h < min) min = h;
                    if (h > max) max = h;
                }
            }

            data.Vertices = new Vector3[n * n];
            data.Normals = new Vector3[n * n];
            data.Colors = new Color[n * n];
            float span = Mathf.Max(max - min, 1f);
            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    int k = j * n + i;
                    float x = data.Coords[i], z = data.Coords[j], y = data.Heights[k];
                    data.Vertices[k] = new Vector3(x, y, z);

                    int il = Mathf.Max(i - 1, 0), ir = Mathf.Min(i + 1, n - 1), jd = Mathf.Max(j - 1, 0), ju = Mathf.Min(j + 1, n - 1);
                    float dx = (data.Heights[j * n + ir] - data.Heights[j * n + il]) / (data.Coords[ir] - data.Coords[il]);
                    float dz = (data.Heights[ju * n + i] - data.Heights[jd * n + i]) / (data.Coords[ju] - data.Coords[jd]);
                    Vector3 normal = new Vector3(-dx, 1f, -dz).normalized;
                    data.Normals[k] = normal;

                    float hn = (y - min) / span + (TerrainMath.Noise(x / 9f, z / 9f, seed + 17) - 0.5f) * 0.3f;
                    float th = Mathf.Clamp01((hn - 0.35f) / 0.5f);
                    th = th * th * (3f - 2f * th);
                    Color c = Color.Lerp(mat.Low, mat.High, th);
                    float slope = Mathf.Clamp01((1f - normal.y - 0.12f) / 0.22f);
                    Color steep = mat.Steep;
                    if (patches != null)
                    {
                        // the second ground lies in broad, soft-edged blotches, a little ragged at the rim
                        float blot = TerrainMath.Fbm(x / 48f, z / 48f, seed + 91, 3) + (TerrainMath.Noise(x / 6f, z / 6f, seed + 93) - 0.5f) * 0.07f;
                        float mix = Mathf.Clamp01((blot - 0.53f) / 0.07f);
                        mix = mix * mix * (3f - 2f * mix);
                        c = Color.Lerp(c, Color.Lerp(patches.Low, patches.High, th), mix);
                        steep = Color.Lerp(steep, patches.Steep, mix);
                    }
                    c = Color.Lerp(c, steep, slope);
                    c = Color.Lerp(c, Color.Lerp(steep, new Color(0.5f, 0.5f, 0.52f), 0.45f), rocks[k]);   // bare rock is greyer than a worn slope
                    // a trail is the bare ground showing through, a shade darker for being trodden
                    c = Color.Lerp(c, mat.Shore * 0.86f, trails[k] * (1f - slope));
                    if (!float.IsNaN(data.WaterY))
                    {
                        float wet = Mathf.Clamp01((y - (data.WaterY - 0.2f)) / 1.1f);
                        c = Color.Lerp(mat.Shore, c, wet);
                    }
                    float patch = 1f + (TerrainMath.Noise(x / 3.1f, z / 3.1f, seed + 5) - 0.5f) * mat.Patch
                                     + (TerrainMath.Noise(x / 21f, z / 21f, seed + 9) - 0.5f) * mat.Patch;
                    c.r *= patch; c.g *= patch; c.b *= patch; c.a = 1f;
                    data.Colors[k] = linear ? new Color(ToLinear(c.r), ToLinear(c.g), ToLinear(c.b), 1f) : c;
                }
            }

            data.Min = min;
            data.Max = max;
            data.Triangles = new int[(n - 1) * (n - 1) * 6];
            int ti = 0;
            for (int j = 0; j < n - 1; j++)
            {
                for (int i = 0; i < n - 1; i++)
                {
                    int a = j * n + i, b = a + 1, c = a + n, d = c + 1;   // a=(i,j) b=(i+1,j) c=(i,j+1) d=(i+1,j+1)
                    data.Triangles[ti++] = a; data.Triangles[ti++] = c; data.Triangles[ti++] = d;
                    data.Triangles[ti++] = a; data.Triangles[ti++] = d; data.Triangles[ti++] = b;
                }
            }
            return data;
        }

        // Done by hand because this runs on a worker thread, away from Unity's own conversion.
        static float ToLinear(float v)
        {
            v = Mathf.Clamp01(v);
            return v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
        }

        int Cell(float v)
        {
            int lo = 0, hi = Coords.Length - 1;
            if (v <= Coords[0]) return 0;
            if (v >= Coords[hi]) return hi - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (Coords[mid] <= v) lo = mid; else hi = mid;
            }
            return lo;
        }

        /// <summary>Exact height of the rendered surface at a point.</summary>
        public float HeightAt(float x, float z)
        {
            int n = TerrainMath.N;
            int i = Cell(x), j = Cell(z);
            float fx = Mathf.Clamp01((x - Coords[i]) / (Coords[i + 1] - Coords[i]));
            float fz = Mathf.Clamp01((z - Coords[j]) / (Coords[j + 1] - Coords[j]));
            float h00 = Heights[j * n + i], h10 = Heights[j * n + i + 1], h01 = Heights[(j + 1) * n + i], h11 = Heights[(j + 1) * n + i + 1];
            if (fz >= fx) return h00 + (h11 - h01) * fx + (h01 - h00) * fz;
            return h00 + (h10 - h00) * fx + (h11 - h10) * fz;
        }
    }

    /// <summary>
    /// Owns the ground. A change spreads outward from the user as a glowing ring: inside it the
    /// new ground has risen, outside it the old ground remains.
    /// </summary>
    public sealed class TerrainSystem
    {
        static readonly int RevealId = Shader.PropertyToID("_Reveal"), ModeId = Shader.PropertyToID("_RevealMode");
        static readonly int TintId = Shader.PropertyToID("_Tint"), EdgeId = Shader.PropertyToID("_EdgeColor");
        static readonly int GridColorId = Shader.PropertyToID("_GridColor"), GridAmountId = Shader.PropertyToID("_GridAmount");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        const float Far = 100000f, RingMax = 190f;

        sealed class Layer
        {
            public GameObject Root, Water;
            public Material Mat, WaterMat;
            public TerrainData Data;
            public Color Tint = Color.white, GridColor = new Color(0f, 1f, 1f);
            public float Grid;
        }

        readonly Transform _parent;
        readonly Shader _terrainShader, _waterShader;
        readonly float _safeRadius;
        Layer _current, _old, _pending;
        Task<TerrainData> _job;
        Statement _jobStatement;
        TerrainType _jobType;
        float _ring = -1f, _ringDuration = 3f;
        MeshCollider _collider;
        Mesh _colliderMesh;

        // remembered so a partial statement (just a tint, say) can rebuild with the rest unchanged
        TerrainType _type;
        string _material;
        float _amp;
        int _seed = 1;
        TerrainFeature _features;
        float _roughness;
        string _patches;

        public event Action Changed;   // heights changed: things standing on the ground should re-settle
        public Color EdgeColor = new Color(1f, 0.55f, 0.25f, 1f);

        public bool HasGround
        {
            get
            {
                Layer layer = _pending ?? _current;
                return layer != null && layer.Data != null && layer.Data.Shape != TerrainShape.None;
            }
        }
        public float WaterY => _current != null && _current.Data != null ? _current.Data.WaterY : float.NaN;
        public bool Busy => _job != null || _ring >= 0f;
        public bool Generating => _job != null;
        public string TypeId => _type != null ? _type.Id : null;

        public TerrainSystem(Transform parent, float safeRadius)
        {
            _parent = parent;
            _safeRadius = safeRadius;
            _terrainShader = Shader.Find("ImagineVR/Terrain");
            _waterShader = Shader.Find("ImagineVR/Water");
            var go = new GameObject("TerrainCollider");
            go.transform.SetParent(parent, false);
            _collider = go.AddComponent<MeshCollider>();
            _collider.enabled = false;
        }

        public float HeightAt(float x, float z)
        {
            Layer layer = _pending ?? _current;
            return layer != null && layer.Data != null && layer.Data.Shape != TerrainShape.None ? layer.Data.HeightAt(x, z) : 0f;
        }

        public void Apply(Statement s, bool synchronous = false)
        {
            Library.EnsureLoaded();
            bool rebuild = _current == null && _pending == null;
            TerrainType type = _type;
            if (s.Has("type"))
            {
                TerrainType wanted = Library.Resolve(Library.TerrainTypes, s.Str("type"));
                if (wanted != null && wanted != _type) { type = wanted; rebuild = true; _material = wanted.Material; _amp = wanted.Height; NewGround(s); }
            }
            if (type == null) { type = Library.TerrainTypes[0]; _material = type.Material; _amp = type.Height; rebuild = true; NewGround(s); }
            if (s.Has("material"))
            {
                GroundMaterial m = Library.Resolve(Library.GroundMaterials, s.Str("material"));
                if (m != null && m.Id != _material) { _material = m.Id; rebuild = true; }
            }
            if (s.Has("height")) { float a = Mathf.Clamp01(s.Num("height")); if (!Mathf.Approximately(a, _amp)) { _amp = a; rebuild = true; } }
            if (s.Has("seed")) { int sd = s.Int("seed"); if (sd != _seed) { _seed = sd; rebuild = true; } }
            if (s.Has("features")) { TerrainFeature f = TerrainRecipe.Parse(s.StrList("features")); if (f != _features) { _features = f; rebuild = true; } }
            if (s.Has("roughness")) { float ro = Mathf.Clamp01(s.Num("roughness")); if (!Mathf.Approximately(ro, _roughness)) { _roughness = ro; rebuild = true; } }
            if (s.Has("patches"))
            {
                GroundMaterial m = Library.Resolve(Library.GroundMaterials, s.Str("patches"));
                string id = m != null && m.Id != _material ? m.Id : null;
                if (id != _patches) { _patches = id; rebuild = true; }
            }
            _type = type;

            if (!rebuild)
            {
                ApplyLook(_pending ?? _current, s);
                return;
            }

            FinishTransition();
            GroundMaterial mat = Library.Resolve(Library.GroundMaterials, _material) ?? Library.GroundMaterials[0];
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            GroundMaterial patches = _patches != null ? Library.Resolve(Library.GroundMaterials, _patches) : null;
            TerrainShape shape = type.Shape;
            float safe = _safeRadius;
            var recipe = new TerrainRecipe { Shape = shape, Amp = _amp, Seed = _seed, Features = _features, Roughness = _roughness, WaterLevel = type.WaterLevel };
            _jobStatement = s;
            _jobType = type;
            if (synchronous || shape == TerrainShape.None)
            {
                _job = Task.FromResult(TerrainData.Generate(recipe, mat, patches, linear, safe));
                Tick(0f);
            }
            else
            {
                _job = Task.Run(() => TerrainData.Generate(recipe, mat, patches, linear, safe));
            }
        }

        /// <summary>
        /// A different kind of ground starts from nothing: no features carried over from the last
        /// place, and a form of its own. Without a `seed` in the statement the same type used to
        /// come out as the very same hills every time it was asked for.
        /// </summary>
        void NewGround(Statement s)
        {
            _features = TerrainFeature.None;
            _roughness = 0f;
            _patches = null;
            if (!s.Has("seed")) _seed = 1 + (int)(DateTime.UtcNow.Ticks % 99989);
        }

        /// <summary>The lowest and highest the ground goes, for placing things by how high they stand.</summary>
        public void Span(out float min, out float max)
        {
            Layer layer = _pending ?? _current;
            min = layer != null && layer.Data != null ? layer.Data.Min : 0f;
            max = layer != null && layer.Data != null ? layer.Data.Max : 0f;
        }

        /// <summary>Removes the ground, with the ring closing over it.</summary>
        public void Clear(float seconds)
        {
            _job = null;
            FinishTransition();
            if (_current == null) return;
            _type = null;
            _old = _current;
            _current = null;
            _old.Mat.SetFloat(ModeId, 1f);
            if (_old.WaterMat != null) _old.WaterMat.SetFloat(ModeId, 1f);
            _collider.enabled = false;
            StartRing(seconds);
            Changed?.Invoke();
        }

        void ApplyLook(Layer layer, Statement s)
        {
            if (layer == null) return;
            if (s.TryColor("tint", out Color c)) layer.Tint = c;
            if (s.Has("grid")) layer.Grid = Mathf.Clamp01(s.Num("grid"));
            if (s.TryColor("grid_color", out c)) layer.GridColor = c;
            if (layer.Mat != null)
            {
                layer.Mat.SetColor(TintId, layer.Tint);
                layer.Mat.SetFloat(GridAmountId, layer.Grid);
                layer.Mat.SetColor(GridColorId, layer.GridColor);
            }
            if (layer.WaterMat != null && s.TryColor("water_color", out c)) layer.WaterMat.SetColor(ColorId, c);
        }

        void StartRing(float seconds)
        {
            _ring = 0f;
            _ringDuration = Mathf.Max(0.3f, seconds);
        }

        void FinishTransition()
        {
            if (_old != null) { Destroy(_old); _old = null; }
            if (_pending != null) { _current = _pending; _pending = null; }
            if (_current != null)
            {
                _current.Mat.SetFloat(RevealId, Far);
                _current.Mat.SetColor(EdgeId, Color.clear);
                if (_current.WaterMat != null) _current.WaterMat.SetFloat(RevealId, Far);
            }
            _ring = -1f;
        }

        static void Destroy(Layer layer)
        {
            if (layer.Root != null)
            {
                var mf = layer.Root.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) UnityEngine.Object.Destroy(mf.sharedMesh);
                UnityEngine.Object.Destroy(layer.Root);
            }
            if (layer.Mat != null) UnityEngine.Object.Destroy(layer.Mat);
            if (layer.WaterMat != null) UnityEngine.Object.Destroy(layer.WaterMat);
        }

        Layer Build(TerrainData data, Statement s)
        {
            var layer = new Layer { Data = data };
            layer.Root = new GameObject("Terrain");
            layer.Root.transform.SetParent(_parent, false);
            layer.Mat = new Material(_terrainShader) { name = "Terrain" };
            layer.Mat.SetFloat(RevealId, 0f);
            layer.Mat.SetFloat(ModeId, 0f);
            layer.Mat.SetColor(EdgeId, EdgeColor);
            if (data.Shape != TerrainShape.None)
            {
                var mesh = new Mesh { name = "TerrainMesh" };
                mesh.vertices = data.Vertices;
                mesh.normals = data.Normals;
                mesh.colors = data.Colors;
                mesh.triangles = data.Triangles;
                mesh.bounds = new Bounds(Vector3.zero, new Vector3(TerrainMath.Extent * 2f, 300f, TerrainMath.Extent * 2f));
                layer.Root.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = layer.Root.AddComponent<MeshRenderer>();
                mr.sharedMaterial = layer.Mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            if (!float.IsNaN(data.WaterY))
            {
                layer.Water = GameObject.CreatePrimitive(PrimitiveType.Quad);
                layer.Water.name = "Water";
                UnityEngine.Object.Destroy(layer.Water.GetComponent<Collider>());
                layer.Water.transform.SetParent(layer.Root.transform, false);
                layer.Water.transform.localPosition = new Vector3(0f, data.WaterY, 0f);
                layer.Water.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                layer.Water.transform.localScale = new Vector3(900f, 900f, 1f);
                layer.WaterMat = new Material(_waterShader) { name = "Water" };
                layer.WaterMat.SetColor(ColorId, data.WaterColor);
                layer.WaterMat.SetFloat(RevealId, 0f);
                layer.WaterMat.SetFloat(ModeId, 0f);
                var wr = layer.Water.GetComponent<MeshRenderer>();
                wr.sharedMaterial = layer.WaterMat;
                wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            ApplyLook(layer, s);
            return layer;
        }

        void BuildCollider(TerrainData data)
        {
            if (data.Shape == TerrainShape.None) { _collider.enabled = false; return; }
            const int n = 41;
            const float half = 50f;
            var verts = new Vector3[n * n];
            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    float x = -half + i * (2f * half / (n - 1)), z = -half + j * (2f * half / (n - 1));
                    verts[j * n + i] = new Vector3(x, data.HeightAt(x, z), z);
                }
            }
            var tris = new int[(n - 1) * (n - 1) * 6];
            int ti = 0;
            for (int j = 0; j < n - 1; j++)
            {
                for (int i = 0; i < n - 1; i++)
                {
                    int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = d;
                    tris[ti++] = a; tris[ti++] = d; tris[ti++] = b;
                }
            }
            if (_colliderMesh == null) _colliderMesh = new Mesh { name = "TerrainCollider" };
            _colliderMesh.Clear();
            _colliderMesh.vertices = verts;
            _colliderMesh.triangles = tris;
            _collider.sharedMesh = null;
            _collider.sharedMesh = _colliderMesh;
            _collider.enabled = true;
        }

        public void Tick(float dt)
        {
            if (_job != null && _job.IsCompleted)
            {
                Task<TerrainData> job = _job;
                _job = null;
                if (job.IsFaulted || job.Result == null)
                {
                    Debug.LogError("ImagineVR: terrain generation failed: " + job.Exception);
                }
                else
                {
                    if (_current != null)
                    {
                        _old = _current;
                        _old.Mat.SetFloat(ModeId, 1f);
                        _old.Mat.SetFloat(RevealId, 0f);
                        _old.Mat.SetColor(EdgeId, Color.clear);
                        if (_old.WaterMat != null) { _old.WaterMat.SetFloat(ModeId, 1f); _old.WaterMat.SetFloat(RevealId, 0f); }
                        _current = null;
                    }
                    _pending = Build(job.Result, _jobStatement);
                    BuildCollider(job.Result);
                    StartRing(_jobStatement != null ? _jobStatement.T(3f) : 3f);
                    Changed?.Invoke();
                }
            }

            if (_ring >= 0f)
            {
                _ring += dt / _ringDuration;
                float p = Mathf.Clamp01(_ring);
                float radius = RingMax * (0.15f * p + 0.85f * p * p);
                if (_pending != null)
                {
                    _pending.Mat.SetFloat(RevealId, radius);
                    if (_pending.WaterMat != null) _pending.WaterMat.SetFloat(RevealId, radius);
                }
                if (_old != null)
                {
                    _old.Mat.SetFloat(RevealId, radius);
                    if (_old.WaterMat != null) _old.WaterMat.SetFloat(RevealId, radius);
                }
                if (p >= 1f) FinishTransition();
            }
        }
    }
}
