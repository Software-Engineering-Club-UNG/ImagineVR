using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    public static partial class Library
    {
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static List<Vector2> Profile(params float[] radiusHeightPairs)
        {
            var list = new List<Vector2>();
            for (int i = 0; i + 1 < radiusHeightPairs.Length; i += 2) list.Add(new Vector2(radiusHeightPairs[i], radiusHeightPairs[i + 1]));
            return list;
        }

        // ------------------------------------------------------------------ shared pieces

        static void Trunk(MeshBuilder mb, float height, float r0, float r1, Color bark)
        {
            mb.NoSway().Shade(0.08f).Grad(Pal.Dark(bark, 0.25f), bark, 0f, height);
            mb.Cyl(Vector3.zero, r0, r1, height, 7, false);
        }

        /// <summary>A few lumpy blobs of leaves around a point.</summary>
        static void Canopy(MeshBuilder mb, Vector3 center, float radius, Color dark, Color light, int seed, int blobs = 4, float flat = 0.8f)
        {
            mb.Shade(0.1f);
            var rng = new System.Random(seed);
            for (int i = 0; i < blobs; i++)
            {
                float a = i / (float)blobs * Mathf.PI * 2f + (float)rng.NextDouble();
                float d = i == 0 ? 0f : radius * (0.35f + 0.3f * (float)rng.NextDouble());
                float r = radius * (i == 0 ? 0.78f : 0.5f + 0.2f * (float)rng.NextDouble());
                Vector3 c = center + new Vector3(Mathf.Cos(a) * d, ((float)rng.NextDouble() - 0.35f) * radius * 0.5f, Mathf.Sin(a) * d);
                mb.Grad(dark, light, c.y - r * flat, c.y + r * flat);
                mb.Blob(c, new Vector3(r, r * flat, r), 0.18f, seed + i * 7, 1);
            }
        }

        static void LeafyTree(MeshBuilder mb, float height, Color bark, Color dark, Color light, int seed, float spread = 0.36f)
        {
            float trunk = height * 0.42f;
            Trunk(mb, trunk + 0.4f, height * 0.045f, height * 0.03f, bark);
            mb.Rod(V(0, trunk * 0.75f, 0), V(height * 0.14f, trunk + 0.5f, height * 0.05f), height * 0.02f, height * 0.012f, 5);
            mb.Rod(V(0, trunk * 0.85f, 0), V(-height * 0.12f, trunk + 0.6f, -height * 0.08f), height * 0.018f, height * 0.011f, 5);
            mb.Sway(trunk, height);
            Canopy(mb, V(0, height * 0.72f, 0), height * spread, dark, light, seed, 5);
        }

        static void Conifer(MeshBuilder mb, float height, Color bark, Color dark, Color light, int tiers, float width, bool snow)
        {
            Trunk(mb, height * 0.3f, height * 0.03f, height * 0.022f, bark);
            mb.Sway(height * 0.2f, height);
            mb.Shade(0.07f);
            for (int i = 0; i < tiers; i++)
            {
                float f = i / (float)tiers;
                float y0 = Mathf.Lerp(height * 0.16f, height * 0.72f, f);
                float h = height * (0.5f - 0.22f * f);
                float r = width * (1f - 0.62f * f);
                mb.Grad(dark, light, y0, y0 + h);
                mb.Cone(V(0, y0, 0), r, h, 7);
                if (snow)
                {
                    mb.Col(Pal.Snow);
                    mb.Cone(V(0, y0 + h * 0.42f, 0), r * 0.62f, h * 0.6f, 7);
                }
            }
        }

        static partial void RegisterNature()
        {
            // ---- trees
            Add("pine_tree", "nature", 7f, "Tall evergreen pine", "tree,forest,evergreen,woods", "fir,conifer,spruce,evergreen,redwood,christmas tree shape",
                mb => Conifer(mb, 7f, Pal.Trunk, Pal.PineDark, Pal.Pine, 4, 1.7f, false));
            Add("snowy_pine", "nature", 7f, "Pine tree heavy with snow", "tree,winter,snow,forest", "snow tree,winter tree,frosted pine",
                mb => Conifer(mb, 7f, Pal.TrunkDark, Pal.PineDark, Pal.Pine, 4, 1.7f, true));
            Add("oak_tree", "nature", 6.5f, "Broad leafy tree", "tree,forest,park,shade,green", "tree,leafy tree,deciduous,maple,elm",
                mb => LeafyTree(mb, 6.5f, Pal.Trunk, Pal.LeafDark, Pal.LeafLight, 11));
            Add("birch_tree", "nature", 6f, "Slender white-barked tree with light leaves", "tree,forest,pale,slender", "aspen,silver birch",
                mb =>
                {
                    Trunk(mb, 3.6f, 0.11f, 0.07f, Pal.Birch);
                    mb.Col(Pal.TrunkDark);
                    for (int i = 0; i < 5; i++) mb.Box(V(0.09f * (i % 2 == 0 ? 1 : -1), 0.5f + i * 0.6f, 0), V(0.07f, 0.05f, 0.14f));
                    mb.Sway(2.6f, 6f);
                    Canopy(mb, V(0, 4.5f, 0), 1.5f, Pal.Hex("#6FAF4E"), Pal.Hex("#B4DA6A"), 21, 4, 1.05f);
                });
            Add("autumn_tree", "nature", 6f, "Tree in orange and red autumn colours", "tree,fall,autumn,orange,red", "fall tree,maple,orange tree",
                mb => LeafyTree(mb, 6f, Pal.TrunkDark, Pal.AutumnRed, Pal.AutumnGold, 31));
            Add("cherry_tree", "nature", 5f, "Cherry tree covered in pink blossom", "tree,blossom,pink,spring,japan", "sakura,blossom tree,pink tree",
                mb => LeafyTree(mb, 5f, Pal.TrunkDark, Pal.BlossomDeep, Pal.Hex("#FFE3EE"), 41, 0.4f));
            Add("dead_tree", "nature", 5f, "Bare leafless tree with twisted branches", "tree,spooky,bare,winter,dead", "bare tree,leafless tree,spooky tree",
                mb =>
                {
                    Color bark = Pal.Hex("#4A4038");
                    Trunk(mb, 2.6f, 0.2f, 0.12f, bark);
                    mb.Col(bark).Sway(2f, 5f);
                    mb.Rod(V(0, 2.4f, 0), V(0.9f, 3.6f, 0.2f), 0.1f, 0.05f, 5); mb.Rod(V(0.9f, 3.6f, 0.2f), V(1.2f, 4.6f, -0.2f), 0.05f, 0.015f, 4);
                    mb.Rod(V(0, 2.2f, 0), V(-0.8f, 3.3f, -0.3f), 0.09f, 0.045f, 5); mb.Rod(V(-0.8f, 3.3f, -0.3f), V(-1.4f, 3.9f, 0.1f), 0.045f, 0.012f, 4);
                    mb.Rod(V(0, 2.5f, 0), V(0.1f, 4.2f, -0.5f), 0.09f, 0.04f, 5); mb.Rod(V(0.1f, 4.2f, -0.5f), V(-0.3f, 5f, -0.7f), 0.04f, 0.012f, 4);
                    mb.Rod(V(0.5f, 3.1f, 0.1f), V(0.9f, 3.2f, 0.9f), 0.04f, 0.012f, 4);
                });
            Add("palm_tree", "nature", 7f, "Leaning tropical palm with coconuts", "tree,tropical,beach,island", "palm,coconut tree,coconut palm",
                mb =>
                {
                    mb.Shade(0.06f).Col(Pal.Hex("#8A6F4A"));
                    Vector3 p = Vector3.zero;
                    for (int i = 0; i < 6; i++)
                    {
                        Vector3 n = V(0.12f * (i + 1) * (i + 1) * 0.3f, (i + 1) * 1.05f, 0f);
                        mb.Rod(p, n, 0.2f - i * 0.018f, 0.185f - i * 0.018f, 6, i == 0);
                        p = n;
                    }
                    mb.Sway(4f, 7.5f);
                    mb.Col(Pal.Hex("#5A4030")).Sphere(p + V(0.12f, -0.15f, 0.1f), 0.14f, 0).Sphere(p + V(-0.12f, -0.18f, -0.08f), 0.14f, 0);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * Mathf.PI * 2f;
                        Vector3 dir = V(Mathf.Cos(a), 0, Mathf.Sin(a)), side = V(-Mathf.Sin(a), 0, Mathf.Cos(a)) * 0.38f;
                        Vector3 m = p + dir * 1.5f + V(0, 0.35f, 0), e = p + dir * 2.9f + V(0, -0.9f, 0);
                        mb.Grad(Pal.Hex("#3F8A4A"), Pal.Hex("#6FBF5A"), p.y - 1f, p.y + 0.5f);
                        mb.QuadTwoSided(p - side * 0.4f, p + side * 0.4f, m + side, m - side);
                        mb.TriTwoSided(m - side, m + side, e);
                    }
                });
            Add("cypress_tree", "nature", 8f, "Tall narrow dark-green tree", "tree,tall,narrow,italy,garden", "cypress,poplar,column tree",
                mb =>
                {
                    Trunk(mb, 1.2f, 0.14f, 0.1f, Pal.TrunkDark);
                    mb.Sway(1f, 8f).Shade(0.08f).Grad(Pal.PineDark, Pal.Hex("#3C7A4E"), 0.8f, 8f);
                    mb.Lathe(Vector3.zero, Profile(0f, 0.8f, 0.7f, 1.4f, 0.85f, 3.2f, 0.6f, 5.6f, 0f, 8f), 7);
                });
            Add("bamboo", "nature", 5f, "Cluster of tall bamboo stalks", "plant,japan,garden,green,asia", "bamboo grove,bamboo stalks",
                mb =>
                {
                    mb.Sway(0.5f, 5f);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 1.1f, d = 0.15f + (i % 3) * 0.13f, h = 3.8f + (i % 4) * 0.4f;
                        Vector3 b = V(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
                        mb.Grad(Pal.Hex("#6E9A3A"), Pal.Hex("#A8C860"), 0f, h);
                        mb.Cyl(b, 0.045f, 0.035f, h, 5, true);
                        mb.Col(Pal.Hex("#7FB24A"));
                        for (int k = 0; k < 3; k++)
                        {
                            Vector3 t = b + V(0, h - 0.3f - k * 0.5f, 0);
                            Vector3 dir = V(Mathf.Cos(a + k * 2.1f), -0.25f, Mathf.Sin(a + k * 2.1f)) * 0.6f;
                            mb.TriTwoSided(t, t + dir + V(0, 0.12f, 0), t + dir * 1.15f - V(0, 0.08f, 0));
                        }
                    }
                });

            // ---- low plants
            Add("bush", "nature", 1.2f, "Round green bush", "plant,shrub,garden,hedge", "shrub,hedge,bushes",
                mb => { mb.Sway(0.2f, 3f); Canopy(mb, V(0, 0.55f, 0), 0.75f, Pal.LeafDark, Pal.Leaf, 5, 4, 0.75f); });
            Add("flower_patch", "nature", 0.4f, "Small cluster of mixed wildflowers", "flowers,meadow,garden,colourful", "flowers,wildflowers,daisies,tulips,flower bed",
                mb =>
                {
                    Color[] petals = { Pal.Red, Pal.Yellow, Pal.White, Pal.Purple, Pal.Pink, Pal.Orange };
                    mb.Sway(0f, 1.2f);
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i * 2.4f, d = 0.08f + (i % 3) * 0.12f, h = 0.22f + (i % 4) * 0.05f;
                        Vector3 b = V(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
                        mb.Col(Pal.Hex("#4E9A4A")).Rod(b, b + V(0, h, 0), 0.008f, 0.006f, 3, false);
                        mb.TriTwoSided(b, b + V(0.07f, 0.1f, 0.02f), b + V(0.02f, 0.02f, 0.06f));
                        mb.Col(petals[i % petals.Length]).Sphere(b + V(0, h, 0), V(0.045f, 0.025f, 0.045f), 0);
                        mb.Col(Pal.Yellow).Sphere(b + V(0, h + 0.015f, 0), 0.016f, 0);
                    }
                });
            Add("tall_grass", "nature", 0.6f, "Tuft of tall grass", "grass,meadow,field,plant", "grass,grass tuft,weeds,reeds",
                mb =>
                {
                    mb.Sway(0f, 1.1f);
                    for (int i = 0; i < 9; i++)
                    {
                        float a = i * 0.7f, d = 0.05f + (i % 3) * 0.05f, h = 0.35f + (i % 4) * 0.08f;
                        Vector3 b = V(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d), w = V(-Mathf.Sin(a), 0, Mathf.Cos(a)) * 0.035f;
                        mb.Grad(Pal.Hex("#4E8A3E"), Pal.Hex("#9CCB5A"), 0f, 0.6f);
                        mb.TriTwoSided(b - w, b + w, b + V(Mathf.Cos(a) * 0.12f, h, Mathf.Sin(a) * 0.12f));
                    }
                });
            Add("fern", "nature", 0.7f, "Low spreading fern", "plant,forest,jungle,green", "ferns,undergrowth,frond",
                mb =>
                {
                    mb.Sway(0.1f, 1.5f).Grad(Pal.LeafDark, Pal.LeafLight, 0f, 0.6f);
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i / 7f * Mathf.PI * 2f;
                        Vector3 dir = V(Mathf.Cos(a), 0, Mathf.Sin(a)), side = V(-Mathf.Sin(a), 0, Mathf.Cos(a)) * 0.12f;
                        Vector3 m = dir * 0.4f + V(0, 0.45f, 0), e = dir * 0.85f + V(0, 0.18f, 0);
                        mb.QuadTwoSided(-side * 0.3f, side * 0.3f, m + side, m - side);
                        mb.TriTwoSided(m - side, m + side, e);
                    }
                });
            Add("cactus", "nature", 2.6f, "Tall desert cactus with arms", "desert,plant,western,dry", "saguaro,cacti",
                mb =>
                {
                    mb.Shade(0.06f).Grad(Pal.Hex("#4A8448"), Pal.Cactus, 0f, 2.6f);
                    mb.Capsule(Vector3.zero, 0.24f, 2.6f, 8);
                    mb.Rod(V(0.2f, 1.1f, 0), V(0.62f, 1.1f, 0), 0.13f, 0.13f, 6); mb.Push(V(0.62f, 1.0f, 0)).Capsule(Vector3.zero, 0.13f, 0.95f, 6).Pop();
                    mb.Rod(V(-0.2f, 1.5f, 0), V(-0.56f, 1.5f, 0), 0.12f, 0.12f, 6); mb.Push(V(-0.56f, 1.4f, 0)).Capsule(Vector3.zero, 0.12f, 0.7f, 6).Pop();
                    mb.Col(Pal.Pink).Sphere(V(0, 2.62f, 0), 0.07f, 0);
                });
            Add("mushroom", "nature", 0.35f, "Red toadstool with white spots", "forest,fungus,fairy,small", "toadstool,fungus",
                mb =>
                {
                    mb.Col(Pal.Cream).Cyl(Vector3.zero, 0.06f, 0.045f, 0.22f, 7, false);
                    mb.Col(Pal.Red).Lathe(Vector3.zero, Profile(0f, 0.18f, 0.2f, 0.19f, 0.16f, 0.28f, 0f, 0.34f), 9);
                    mb.Col(Pal.White);
                    for (int i = 0; i < 5; i++) mb.Sphere(V(Mathf.Cos(i * 1.3f) * 0.11f, 0.285f, Mathf.Sin(i * 1.3f) * 0.11f), 0.022f, 0);
                });
            Add("giant_mushroom", "nature", 5f, "Towering mushroom with a softly glowing cap", "fantasy,fairy,alien,glow,forest", "huge mushroom,fantasy mushroom,glowing mushroom",
                mb =>
                {
                    mb.Shade(0.05f).Grad(Pal.Hex("#C8BFB0"), Pal.Cream, 0f, 3.6f).Cyl(Vector3.zero, 0.5f, 0.3f, 3.7f, 9, false);
                    mb.Grad(Pal.Hex("#7A3FB0"), Pal.Hex("#D07AE0"), 3.2f, 5f);
                    mb.Lathe(Vector3.zero, Profile(0f, 3.5f, 2.3f, 3.45f, 2.0f, 4.3f, 1.0f, 4.9f, 0f, 5.05f), 12);
                    mb.Emit(0.2f, 1f).Col(Pal.Hex("#9FF5E0"));
                    for (int i = 0; i < 9; i++) mb.Sphere(V(Mathf.Cos(i * 0.7f) * 1.5f, 4.1f + (i % 3) * 0.25f, Mathf.Sin(i * 0.7f) * 1.5f), 0.14f, 0);
                    mb.Emit().Glow(V(0, 3.3f, 0), 2.6f, new Color(0.35f, 0.9f, 0.8f, 1f) * 0.35f, true);
                });

            // ---- rocks
            Add("rock", "nature", 0.7f, "Small grey rock", "stone,boulder,grey", "stone,pebble,rocks",
                mb => { mb.Shade(0.12f).Grad(Pal.RockDark, Pal.Rock, 0f, 0.7f).Blob(V(0, 0.28f, 0), V(0.5f, 0.36f, 0.42f), 0.28f, 3, 1); }).Solid(30f, 0.1f);
            Add("boulder", "nature", 2.4f, "Large weathered boulder", "stone,rock,big,grey", "big rock,large rock",
                mb => { mb.Shade(0.12f).Grad(Pal.RockDark, Pal.Rock, 0f, 2.4f).Blob(V(0, 1.0f, 0), V(1.6f, 1.25f, 1.4f), 0.3f, 8, 1); });
            Add("rock_spire", "nature", 7f, "Tall jagged spire of rock", "stone,cliff,desert,alien,canyon", "rock pillar,stone spire,hoodoo,stalagmite",
                mb =>
                {
                    mb.Shade(0.12f).Jitter(0.12f).Grad(Pal.Hex("#7A5A48"), Pal.RockWarm, 0f, 7f);
                    mb.Lathe(Vector3.zero, Profile(1.5f, -0.5f, 1.2f, 1.2f, 0.95f, 3f, 0.7f, 4.6f, 0.35f, 6.1f, 0f, 7f), 6);
                    mb.Jitter(0f);
                });
            Add("log", "nature", 0.5f, "Fallen tree trunk lying on the ground", "wood,forest,camp,seat", "fallen log,tree trunk,timber",
                mb =>
                {
                    mb.Shade(0.08f).Col(Pal.Trunk).Rod(V(-1.2f, 0.24f, 0), V(1.2f, 0.24f, 0), 0.25f, 0.22f, 8, false);
                    mb.Col(Pal.WoodLight).Push(V(1.2f, 0.24f, 0), V(0, 0, -90f)).Disc(Vector3.zero, 0.22f, 8, true).Pop();
                    mb.Push(V(-1.2f, 0.24f, 0), V(0, 0, 90f)).Disc(Vector3.zero, 0.25f, 8, true).Pop();
                    mb.Col(Pal.TrunkDark).Rod(V(0.3f, 0.4f, 0), V(0.5f, 0.75f, 0.1f), 0.05f, 0.03f, 4);
                }).Solid(40f, 0.1f);
            Add("tree_stump", "nature", 0.5f, "Cut tree stump", "wood,forest,seat", "stump",
                mb =>
                {
                    mb.Shade(0.08f).Col(Pal.Trunk).Cyl(Vector3.zero, 0.38f, 0.3f, 0.45f, 8, false);
                    mb.Col(Pal.WoodLight).Disc(V(0, 0.45f, 0), 0.3f, 8);
                });

            // ---- sky things
            Add("cloud", "sky", 4f, "Puffy white cloud", "sky,weather,white,fluffy", "clouds,puffy cloud",
                mb =>
                {
                    mb.Grad(Pal.Hex("#C8D4E4"), Color.white, -2f, 2.5f);
                    mb.Blob(V(0, 0, 0), V(4.5f, 2.2f, 3f), 0.15f, 1, 1).Blob(V(4.2f, -0.3f, 0.5f), V(3.2f, 1.6f, 2.4f), 0.15f, 2, 1);
                    mb.Blob(V(-4f, -0.4f, -0.4f), V(3f, 1.5f, 2.2f), 0.15f, 3, 1).Blob(V(1f, 1.4f, -0.3f), V(2.6f, 1.7f, 2f), 0.15f, 4, 1);
                }).Flies();
            Add("bird", "sky", 0.4f, "Small bird in flight", "sky,animal,flying,wings", "birds,seagull,gull,flock",
                mb =>
                {
                    mb.Col(Pal.Hex("#3A3F4A")).Sphere(V(0, 0, 0), V(0.09f, 0.08f, 0.2f), 0);
                    mb.Col(Pal.Hex("#555B68"));
                    mb.TriTwoSided(V(0.05f, 0.02f, 0.08f), V(0.05f, 0.02f, -0.1f), V(0.6f, 0.16f, -0.05f));
                    mb.TriTwoSided(V(-0.05f, 0.02f, 0.08f), V(-0.05f, 0.02f, -0.1f), V(-0.6f, 0.16f, -0.05f));
                    mb.Col(Pal.Orange).Cone(V(0, 0, 0.19f), 0.02f, 0.06f, 4);
                }).Flies();
        }
    }
}
