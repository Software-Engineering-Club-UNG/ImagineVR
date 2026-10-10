using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    public static partial class Library
    {
        static void Fish(MeshBuilder mb, float length, Color body, Color fin)
        {
            mb.Shade(0.04f).Col(body).Sphere(Vector3.zero, V(length * 0.16f, length * 0.24f, length * 0.5f), 1);
            mb.Col(fin).TriTwoSided(V(0, 0, -length * 0.42f), V(0, length * 0.26f, -length * 0.75f), V(0, -length * 0.26f, -length * 0.75f));
            mb.TriTwoSided(V(0, length * 0.2f, length * 0.1f), V(0, length * 0.42f, -length * 0.12f), V(0, length * 0.18f, -length * 0.2f));
            mb.Col(Pal.Black).Sphere(V(length * 0.12f, length * 0.06f, length * 0.32f), length * 0.035f, 0).Sphere(V(-length * 0.12f, length * 0.06f, length * 0.32f), length * 0.035f, 0);
        }

        static void Hull(MeshBuilder mb, float length, float width, float height, Color c)
        {
            float hl = length * 0.5f, hw = width * 0.5f;
            Vector3 bowTop = V(0, height, hl), bowBot = V(0, 0, hl * 0.75f);
            Vector3 tl = V(-hw, height, hl * 0.3f), tr = V(hw, height, hl * 0.3f), bl = V(-hw * 0.6f, 0, hl * 0.25f), br = V(hw * 0.6f, 0, hl * 0.25f);
            Vector3 stl = V(-hw * 0.85f, height, -hl), str = V(hw * 0.85f, height, -hl), sbl = V(-hw * 0.5f, 0, -hl * 0.9f), sbr = V(hw * 0.5f, 0, -hl * 0.9f);
            mb.Shade(0.04f).Col(c);
            mb.Tri(bowTop, tr, br, V(1, -0.3f, 1)).Tri(bowTop, br, bowBot, V(1, -0.3f, 1));
            mb.Tri(bowTop, tl, bl, V(-1, -0.3f, 1)).Tri(bowTop, bl, bowBot, V(-1, -0.3f, 1));
            mb.Quad(tr, str, sbr, br, Vector3.right).Quad(tl, stl, sbl, bl, Vector3.left).Quad(stl, str, sbr, sbl, Vector3.back);
            mb.Quad(bl, br, sbr, sbl, Vector3.down).Tri(bowBot, br, bl, Vector3.down);
            mb.Col(Pal.Dark(c, 0.3f)).Quad(tl, tr, str, stl, Vector3.up).Tri(bowTop, tr, tl, Vector3.up);
        }

        static partial void RegisterThemes()
        {
            // ================================================================ sea
            Add("fish", "sea", 0.4f, "Bright tropical fish", "ocean,underwater,reef,swim,sea", "fishes,tropical fish,goldfish,clownfish",
                mb => Fish(mb, 0.4f, Pal.Orange, Pal.White)).Flies();
            Add("shark", "sea", 1.2f, "Grey shark", "ocean,underwater,predator,sea,danger", "great white,sharks",
                mb =>
                {
                    Color c = Pal.Hex("#6E7E8E");
                    mb.Shade(0.04f).Grad(Pal.White, c, -0.4f, 0.1f).Sphere(Vector3.zero, V(0.5f, 0.5f, 2.0f), 1);
                    mb.Col(c).TriTwoSided(V(0, 0.4f, 0.3f), V(0, 1.2f, -0.3f), V(0, 0.4f, -0.5f));
                    mb.TriTwoSided(V(0, 0, -1.8f), V(0, 0.95f, -2.9f), V(0, -0.5f, -2.6f));
                    mb.TriTwoSided(V(0.4f, -0.1f, 0.5f), V(1.3f, -0.35f, -0.1f), V(0.4f, -0.1f, -0.1f));
                    mb.TriTwoSided(V(-0.4f, -0.1f, 0.5f), V(-1.3f, -0.35f, -0.1f), V(-0.4f, -0.1f, -0.1f));
                    mb.Col(Pal.Black).Sphere(V(0.36f, 0.12f, 1.3f), 0.05f, 0).Sphere(V(-0.36f, 0.12f, 1.3f), 0.05f, 0);
                }).Flies();
            Add("whale", "sea", 4f, "Huge blue whale", "ocean,underwater,giant,sea", "blue whale,humpback,orca",
                mb =>
                {
                    Color c = Pal.Hex("#3E5E8A");
                    mb.Shade(0.04f).Grad(Pal.Hex("#C8D8E8"), c, -1.5f, 0.5f).Sphere(Vector3.zero, V(2.2f, 2f, 7f), 1);
                    mb.Col(c).Rod(V(0, 0.2f, -6f), V(0, 0.6f, -9f), 1.2f, 0.4f, 6);
                    mb.TriTwoSided(V(0, 0.6f, -8.8f), V(2.8f, 0.8f, -10.5f), V(0, 0.6f, -10f));
                    mb.TriTwoSided(V(0, 0.6f, -8.8f), V(-2.8f, 0.8f, -10.5f), V(0, 0.6f, -10f));
                    mb.TriTwoSided(V(1.8f, -0.8f, 2f), V(4.2f, -1.6f, 0.5f), V(1.8f, -0.8f, 0.3f));
                    mb.TriTwoSided(V(-1.8f, -0.8f, 2f), V(-4.2f, -1.6f, 0.5f), V(-1.8f, -0.8f, 0.3f));
                    mb.Col(Pal.Black).Sphere(V(1.75f, 0.1f, 4.2f), 0.14f, 0).Sphere(V(-1.75f, 0.1f, 4.2f), 0.14f, 0);
                }).Flies();
            Add("dolphin", "sea", 0.9f, "Leaping dolphin", "ocean,sea,playful,swim", "porpoise,dolphins",
                mb =>
                {
                    Color c = Pal.Hex("#6E94B8");
                    mb.Shade(0.04f).Grad(Pal.White, c, -0.3f, 0.1f).Sphere(Vector3.zero, V(0.32f, 0.34f, 1.2f), 1);
                    mb.Col(c).Rod(V(0, 0, 1.0f), V(0, -0.05f, 1.5f), 0.12f, 0.05f, 5);
                    mb.TriTwoSided(V(0, 0.28f, 0.1f), V(0, 0.75f, -0.3f), V(0, 0.28f, -0.35f));
                    mb.TriTwoSided(V(0, 0, -1.1f), V(0.6f, 0.05f, -1.7f), V(0, 0, -1.5f));
                    mb.TriTwoSided(V(0, 0, -1.1f), V(-0.6f, 0.05f, -1.7f), V(0, 0, -1.5f));
                }).Flies();
            Add("jellyfish", "sea", 0.8f, "Softly glowing jellyfish", "ocean,underwater,glow,deep,sea", "jelly,medusa",
                mb =>
                {
                    mb.Emit(0.5f).Col(Pal.Hex("#E8A0F0")).Lathe(Vector3.zero, Profile(0.3f, 0f, 0.32f, 0.1f, 0.22f, 0.28f, 0f, 0.36f), 9);
                    mb.Emit(0.3f).Col(Pal.Hex("#C8B0FF"));
                    for (int i = 0; i < 7; i++) { float a = i / 7f * Mathf.PI * 2f; mb.Rod(V(Mathf.Cos(a) * 0.2f, 0f, Mathf.Sin(a) * 0.2f), V(Mathf.Cos(a + 0.5f) * 0.14f, -0.7f, Mathf.Sin(a + 0.5f) * 0.14f), 0.015f, 0.005f, 3, false); }
                    mb.Emit().Glow(V(0, 0.1f, 0), 0.9f, new Color(0.8f, 0.5f, 1f) * 0.4f);
                }).Flies();
            Add("sea_turtle", "sea", 0.5f, "Sea turtle", "ocean,underwater,reef,beach,sea", "turtle,tortoise",
                mb =>
                {
                    mb.Shade(0.06f).Col(Pal.Hex("#5A7A3A")).Sphere(V(0, 0.18f, 0), V(0.45f, 0.2f, 0.55f), 1);
                    mb.Col(Pal.Hex("#8AAA5A")).Sphere(V(0, 0.16f, 0.62f), V(0.13f, 0.11f, 0.17f), 0);
                    foreach (float s in new[] { -1f, 1f }) { mb.Sphere(V(s * 0.5f, 0.1f, 0.3f), V(0.3f, 0.04f, 0.14f), 0).Sphere(V(s * 0.4f, 0.1f, -0.4f), V(0.16f, 0.04f, 0.12f), 0); }
                });
            Add("crab", "sea", 0.25f, "Red crab", "beach,ocean,shore,sea", "lobster",
                mb =>
                {
                    mb.Col(Pal.Red).Sphere(V(0, 0.12f, 0), V(0.2f, 0.09f, 0.14f), 1);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        for (int i = 0; i < 3; i++) mb.Rod(V(s * 0.15f, 0.12f, -0.08f + i * 0.08f), V(s * 0.32f, 0f, -0.12f + i * 0.12f), 0.018f, 0.01f, 3);
                        mb.Rod(V(s * 0.14f, 0.14f, 0.1f), V(s * 0.26f, 0.2f, 0.24f), 0.025f, 0.02f, 4).Sphere(V(s * 0.28f, 0.22f, 0.28f), V(0.06f, 0.05f, 0.07f), 0);
                    }
                    mb.Col(Pal.Black).Sphere(V(0.06f, 0.22f, 0.1f), 0.018f, 0).Sphere(V(-0.06f, 0.22f, 0.1f), 0.018f, 0);
                });
            Add("coral", "sea", 1f, "Branching pink coral", "reef,underwater,ocean,sea,colourful", "coral reef,sea fan",
                mb =>
                {
                    mb.Shade(0.06f).Col(Pal.Coral);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2f, d = 0.12f;
                        Vector3 b = V(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d), m = b + V(Mathf.Cos(a) * 0.25f, 0.5f, Mathf.Sin(a) * 0.25f);
                        mb.Rod(b, m, 0.07f, 0.05f, 5).Rod(m, m + V(Mathf.Cos(a + 0.6f) * 0.2f, 0.4f, Mathf.Sin(a + 0.6f) * 0.2f), 0.05f, 0.025f, 4).Rod(m, m + V(Mathf.Cos(a - 0.7f) * 0.22f, 0.3f, Mathf.Sin(a - 0.7f) * 0.22f), 0.045f, 0.02f, 4);
                    }
                });
            Add("kelp", "sea", 4f, "Tall swaying seaweed", "underwater,ocean,sea,plant,green", "seaweed,sea grass,algae",
                mb =>
                {
                    mb.Sway(0f, 3f).Grad(Pal.Hex("#2E6A48"), Pal.Hex("#6FB06A"), 0f, 4f);
                    for (int s = 0; s < 3; s++)
                    {
                        float ox = (s - 1) * 0.2f;
                        for (int i = 0; i < 6; i++)
                        {
                            float y0 = i * 0.65f, y1 = y0 + 0.65f, w0 = 0.14f - i * 0.015f, w1 = 0.14f - (i + 1) * 0.015f;
                            float x0 = ox + Mathf.Sin(i * 1.3f + s) * 0.12f, x1 = ox + Mathf.Sin((i + 1) * 1.3f + s) * 0.12f;
                            mb.QuadTwoSided(V(x0 - w0, y0, s * 0.1f), V(x0 + w0, y0, s * 0.1f), V(x1 + w1, y1, s * 0.1f), V(x1 - w1, y1, s * 0.1f));
                        }
                    }
                });
            Add("seashell", "sea", 0.15f, "Spiral seashell", "beach,shore,sand,ocean", "shell,conch",
                mb => mb.Col(Pal.Hex("#F4D8C0")).Lathe(Vector3.zero, Profile(0f, 0f, 0.07f, 0.03f, 0.05f, 0.08f, 0.025f, 0.12f, 0f, 0.15f), 7)).Hold().Solid(0.1f, 0.2f);
            Add("starfish", "sea", 0.06f, "Orange starfish", "beach,shore,ocean,reef", "sea star",
                mb =>
                {
                    mb.Col(Pal.Orange);
                    for (int i = 0; i < 5; i++) { float a = i / 5f * Mathf.PI * 2f; mb.Rod(V(0, 0.03f, 0), V(Mathf.Cos(a) * 0.2f, 0.015f, Mathf.Sin(a) * 0.2f), 0.045f, 0.012f, 4); }
                });
            Add("boat", "sea", 1f, "Small wooden rowing boat", "water,lake,sea,row,fishing", "rowboat,dinghy,canoe,rowing boat",
                mb =>
                {
                    Hull(mb, 3.6f, 1.4f, 0.6f, Pal.Wood);
                    mb.Col(Pal.WoodLight).Box(V(0, 0.45f, 0.2f), V(1.2f, 0.05f, 0.3f)).Box(V(0, 0.45f, -0.9f), V(1.1f, 0.05f, 0.3f));
                    mb.Col(Pal.WoodDark).Rod(V(0.7f, 0.6f, 0), V(1.8f, 0.2f, -0.6f), 0.025f, 0.025f, 4).Rod(V(-0.7f, 0.6f, 0), V(-1.8f, 0.2f, -0.6f), 0.025f, 0.025f, 4);
                }).Floats();
            Add("sailboat", "sea", 6f, "Sailing boat with white sails", "water,sea,lake,sail,harbor", "yacht,sailing boat,sloop",
                mb =>
                {
                    Hull(mb, 6f, 2f, 0.9f, Pal.White);
                    mb.Col(Pal.WoodDark).Cyl(V(0, 0.8f, 0.3f), 0.06f, 0.04f, 5.5f, 5);
                    mb.Col(Pal.White).TriTwoSided(V(0, 1.3f, 0.2f), V(0, 6.1f, 0.3f), V(0, 1.3f, -2.6f));
                    mb.TriTwoSided(V(0, 1.2f, 0.45f), V(0, 5.6f, 0.4f), V(0, 1.0f, 2.8f));
                    mb.Col(Pal.Blue).Box(V(0, 0.45f, 0), V(2.02f, 0.14f, 5.2f));
                }).Floats();
            Add("pirate_ship", "sea", 14f, "Three-masted pirate ship with black sails", "sea,pirate,galleon,adventure,ocean", "galleon,ship,sailing ship,tall ship",
                mb =>
                {
                    Hull(mb, 20f, 6f, 3.2f, Pal.Hex("#5A3E2A"));
                    mb.Col(Pal.Hex("#4A3020")).Box(V(0, 4f, -7.5f), V(5f, 1.8f, 4.5f)).Box(V(0, 3.7f, 7.5f), V(3.6f, 1.2f, 3f));
                    mb.Col(Pal.Gold).Box(V(0, 3.2f, 0), V(6.04f, 0.2f, 16f));
                    float[] zs = { -5f, 0.5f, 6f }; float[] hs = { 9f, 11.5f, 8.5f };
                    for (int i = 0; i < 3; i++)
                    {
                        mb.Col(Pal.WoodDark).Cyl(V(0, 3f, zs[i]), 0.2f, 0.12f, hs[i], 6);
                        mb.Col(Pal.Hex("#2A2A2E"));
                        mb.QuadTwoSided(V(-3.2f, 5f, zs[i] + 0.3f), V(3.2f, 5f, zs[i] + 0.3f), V(2.6f, 3f + hs[i] * 0.62f, zs[i] + 0.6f), V(-2.6f, 3f + hs[i] * 0.62f, zs[i] + 0.6f));
                        mb.QuadTwoSided(V(-2.2f, 3.3f + hs[i] * 0.66f, zs[i] + 0.3f), V(2.2f, 3.3f + hs[i] * 0.66f, zs[i] + 0.3f), V(1.6f, 2.6f + hs[i], zs[i] + 0.5f), V(-1.6f, 2.6f + hs[i], zs[i] + 0.5f));
                    }
                    mb.Col(Pal.Black).QuadTwoSided(V(0, 14.6f, 0.5f), V(1.6f, 14.5f, 0.5f), V(1.6f, 13.7f, 0.5f), V(0, 13.8f, 0.5f));
                    mb.Col(Pal.WoodDark).Rod(V(0, 3.4f, 9.5f), V(0, 5.2f, 14f), 0.14f, 0.06f, 5);
                }).Floats();
            Add("lighthouse", "sea", 18f, "Striped lighthouse with a turning lamp", "coast,sea,beacon,landmark,harbor", "beacon,light house",
                mb =>
                {
                    for (int i = 0; i < 5; i++)
                        mb.Col(i % 2 == 0 ? Pal.White : Pal.Red).Cyl(V(0, i * 2.8f, 0), 2.6f - i * 0.3f, 2.3f - i * 0.3f, 2.8f, 10, false);
                    mb.Col(Pal.MetalDark).Cyl(V(0, 14f, 0), 1.9f, 1.9f, 0.3f, 10);
                    mb.Emit(1f).Col(Pal.FireCore).Cyl(V(0, 14.3f, 0), 0.9f, 0.9f, 1.6f, 8, false);
                    mb.Emit().Col(Pal.Red).Cone(V(0, 15.9f, 0), 1.5f, 1.6f, 10);
                    mb.Glow(V(0, 15.1f, 0), 9f, new Color(1f, 0.9f, 0.6f) * 0.7f, true);
                });
            Add("beach_umbrella", "sea", 2.3f, "Striped beach parasol", "beach,summer,shade,holiday", "parasol,sunshade,umbrella",
                mb =>
                {
                    mb.Col(Pal.White).Cyl(Vector3.zero, 0.03f, 0.03f, 2.2f, 5);
                    for (int i = 0; i < 8; i++)
                    {
                        float a0 = i / 8f * Mathf.PI * 2f, a1 = (i + 1) / 8f * Mathf.PI * 2f;
                        mb.Col(i % 2 == 0 ? Pal.Red : Pal.White);
                        mb.TriTwoSided(V(0, 2.3f, 0), V(Mathf.Cos(a0) * 1.4f, 1.85f, Mathf.Sin(a0) * 1.4f), V(Mathf.Cos(a1) * 1.4f, 1.85f, Mathf.Sin(a1) * 1.4f));
                    }
                });
            Add("beach_chair", "sea", 0.9f, "Reclining deck chair", "beach,summer,relax,holiday", "deck chair,lounger,sun lounger",
                mb =>
                {
                    mb.Col(Pal.WoodLight);
                    foreach (float x in new[] { -0.3f, 0.3f }) mb.Rod(V(x, 0, 0.7f), V(x, 0.85f, -0.6f), 0.025f, 0.025f, 4).Rod(V(x, 0, -0.5f), V(x, 0.5f, 0.3f), 0.025f, 0.025f, 4);
                    mb.Col(Pal.Hex("#3E9AD8")).QuadTwoSided(V(-0.3f, 0.82f, -0.55f), V(0.3f, 0.82f, -0.55f), V(0.3f, 0.2f, 0.4f), V(-0.3f, 0.2f, 0.4f));
                });
            Add("sandcastle", "sea", 0.6f, "Sandcastle with towers and a flag", "beach,sand,play,summer", "sand castle",
                mb =>
                {
                    mb.Shade(0.05f).Col(Pal.Sand).BoxOn(Vector3.zero, V(0.8f, 0.3f, 0.8f));
                    foreach (float x in new[] { -0.36f, 0.36f }) foreach (float z in new[] { -0.36f, 0.36f }) { mb.Cyl(V(x, 0, z), 0.13f, 0.11f, 0.45f, 8); mb.Cone(V(x, 0.45f, z), 0.13f, 0.14f, 8); }
                    mb.Cyl(V(0, 0.3f, 0), 0.16f, 0.14f, 0.3f, 8).Cone(V(0, 0.6f, 0), 0.16f, 0.16f, 8);
                    mb.Col(Pal.Red).TriTwoSided(V(0, 0.9f, 0), V(0, 0.8f, 0), V(0.14f, 0.85f, 0));
                    mb.Col(Pal.WoodDark).Cyl(V(0, 0.75f, 0), 0.006f, 0.006f, 0.15f, 3);
                });
            Add("treasure_chest", "sea", 0.7f, "Open chest overflowing with gold", "pirate,treasure,gold,loot,adventure", "chest,treasure,loot",
                mb =>
                {
                    mb.Shade(0.05f).Col(Pal.Wood).BoxOn(Vector3.zero, V(0.9f, 0.45f, 0.55f));
                    mb.Col(Pal.Gold).Box(V(0, 0.22f, 0.28f), V(0.92f, 0.08f, 0.02f)).Box(V(0, 0.3f, 0.285f), V(0.1f, 0.14f, 0.02f));
                    mb.Col(Pal.WoodDark).Push(V(0, 0.45f, -0.27f), V(-65f, 0, 0)).Box(V(0, 0.14f, 0.26f), V(0.9f, 0.28f, 0.52f)).Pop();
                    mb.Emit(0.3f).Col(Pal.Gold);
                    for (int i = 0; i < 12; i++) mb.Sphere(V(-0.32f + Hash01(i, 1, 4) * 0.64f, 0.46f + Hash01(i, 2, 4) * 0.08f, -0.15f + Hash01(i, 3, 4) * 0.3f), V(0.07f, 0.035f, 0.07f), 0);
                    mb.Emit().Glow(V(0, 0.55f, 0), 0.9f, new Color(1f, 0.8f, 0.3f) * 0.35f);
                });
            Add("pier", "sea", 1.2f, "Wooden jetty reaching out over the water", "dock,harbor,lake,sea,fishing", "dock,jetty,boardwalk,wharf",
                mb =>
                {
                    mb.Shade(0.06f).Col(Pal.Wood);
                    for (int i = 0; i < 16; i++) mb.Box(V(0, 0.9f, i * 0.75f), V(2.4f, 0.08f, 0.68f));
                    mb.Col(Pal.WoodDark);
                    for (int i = 0; i < 5; i++) foreach (float x in new[] { -1.1f, 1.1f }) mb.Cyl(V(x, -2f, i * 2.9f), 0.12f, 0.12f, 3.3f, 6);
                });

            // ================================================================ space
            Add("planet", "space", 2f, "A planet to hang in the sky. Tint it any colour and scale it up", "space,sky,world,orbit,giant", "earth,world,globe,mars,venus,moon,exoplanet",
                mb =>
                {
                    mb.Emit(0.25f).Shade(0.03f).Col(Pal.Hex("#4A7FD0")).Sphere(V(0, 1f, 0), 1f, 2);
                    mb.Col(Pal.Hex("#5AA860"));
                    for (int i = 0; i < 7; i++)
                    {
                        Vector3 d = V(Mathf.Sin(i * 1.9f), Mathf.Cos(i * 2.7f) * 0.7f, Mathf.Cos(i * 1.3f)).normalized;
                        mb.Blob(V(0, 1f, 0) + d * 0.86f, V(0.34f, 0.26f, 0.3f), 0.3f, i, 0);
                    }
                    mb.Emit();
                }).Flies();
            Add("ringed_planet", "space", 2f, "A gas giant with wide rings", "space,sky,saturn,orbit,giant", "saturn,gas giant,jupiter",
                mb =>
                {
                    mb.Emit(0.25f).Grad(Pal.Hex("#C89860"), Pal.Hex("#F0D8A8"), 0f, 2f).Sphere(V(0, 1f, 0), 1f, 2);
                    mb.Col(Pal.Hex("#E8D8B8")).Push(V(0, 1f, 0), V(18f, 0, 8f), V(1f, 0.02f, 1f)).Torus(Vector3.zero, 1.75f, 0.32f, 28, 4).Pop();
                    mb.Emit();
                }).Flies();
            Add("asteroid", "space", 1.5f, "Tumbling chunk of space rock", "space,rock,meteor,belt", "meteor,space rock,comet,meteorite",
                mb => mb.Shade(0.14f).Col(Pal.Hex("#6A625C")).Blob(V(0, 0.7f, 0), V(0.9f, 0.7f, 0.75f), 0.4f, 13, 1)).Flies().Solid(40f, 0.2f, ColliderShape.Sphere);
            Add("rocket", "space", 7f, "Red and white rocket ready for launch", "space,launch,nasa,ship,sci-fi", "spaceship,space rocket,missile,spacecraft",
                mb =>
                {
                    mb.Shade(0.03f).Col(Pal.White).Lathe(V(0, 1f, 0), Profile(0f, 0f, 0.6f, 0f, 0.75f, 0.6f, 0.75f, 3.6f, 0.5f, 4.8f, 0f, 6f), 12);
                    mb.Col(Pal.Red).Cyl(V(0, 4.6f, 0), 0.76f, 0.73f, 0.2f, 12, false).Cone(V(0, 5.8f, 0), 0.24f, 0.5f, 10);
                    mb.Col(Pal.GlassDark).Sphere(V(0, 3.9f, 0.7f), 0.26f, 1);
                    mb.Col(Pal.Red);
                    for (int i = 0; i < 3; i++)
                    {
                        mb.Push(Vector3.zero, V(0, i * 120f, 0));
                        mb.TriTwoSided(V(0, 2.2f, 0.72f), V(0, 0.2f, 0.72f), V(0, 0f, 1.7f));
                        mb.Col(Pal.MetalDark).Rod(V(0, 1.1f, 0.6f), V(0, 0f, 1.5f), 0.05f, 0.05f, 4);
                        mb.Col(Pal.Red).Pop();
                    }
                    mb.Col(Pal.MetalDark).Cyl(V(0, 0.6f, 0), 0.3f, 0.5f, 0.45f, 10);
                });
            Add("ufo", "space", 1.5f, "Flying saucer with a glowing dome", "space,alien,sci-fi,flying", "flying saucer,spaceship,alien ship",
                mb =>
                {
                    mb.Shade(0.03f).Col(Pal.Metal).Lathe(Vector3.zero, Profile(0f, 0f, 1.2f, 0.1f, 2.2f, 0.4f, 1.2f, 0.7f, 0f, 0.75f), 16);
                    mb.Emit(0.6f).Col(Pal.Hex("#9FF5E0")).Sphere(V(0, 0.75f, 0), V(0.85f, 0.65f, 0.85f), 1);
                    mb.Emit(1f).Col(Pal.Yellow);
                    for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2f; mb.Sphere(V(Mathf.Cos(a) * 1.8f, 0.36f, Mathf.Sin(a) * 1.8f), 0.1f, 0); }
                    mb.Emit().Glow(V(0, -0.2f, 0), 2.5f, new Color(0.4f, 1f, 0.85f) * 0.5f);
                }).Flies();
            Add("satellite", "space", 2f, "Satellite with solar panels", "space,orbit,sci-fi,technology", "space station,probe,sputnik",
                mb =>
                {
                    mb.Col(Pal.Gold).Box(V(0, 1f, 0), V(0.8f, 0.9f, 0.8f));
                    mb.Col(Pal.Hex("#2A4A8A")).Box(V(1.9f, 1f, 0), V(2.6f, 0.9f, 0.04f)).Box(V(-1.9f, 1f, 0), V(2.6f, 0.9f, 0.04f));
                    mb.Col(Pal.Metal).Rod(V(-0.6f, 1f, 0), V(0.6f, 1f, 0), 0.04f, 0.04f, 4).Lathe(V(0, 1.45f, 0), Profile(0f, 0f, 0.35f, 0.25f, 0.4f, 0.3f), 8);
                }).Flies();
            Add("moon_lander", "space", 3.5f, "Lunar landing craft on four legs", "space,moon,apollo,nasa", "lunar module,lander,lunar lander",
                mb =>
                {
                    mb.Col(Pal.Gold).Cyl(V(0, 1.1f, 0), 1.3f, 1.3f, 0.9f, 8);
                    mb.Col(Pal.Metal).Lathe(V(0, 2f, 0), Profile(1f, 0f, 1.1f, 0.7f, 0.6f, 1.4f, 0f, 1.5f), 8);
                    mb.Col(Pal.MetalDark).Cone(V(0, 1.1f, 0), 0.6f, -0.6f, 8);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i / 4f * Mathf.PI * 2f + 0.78f;
                        Vector3 foot = V(Mathf.Cos(a) * 2.2f, 0.05f, Mathf.Sin(a) * 2.2f);
                        mb.Col(Pal.Metal).Rod(V(Mathf.Cos(a) * 1.2f, 1.6f, Mathf.Sin(a) * 1.2f), foot, 0.06f, 0.05f, 4).Cyl(foot - V(0, 0.05f, 0), 0.25f, 0.25f, 0.06f, 7);
                    }
                });
            Add("crystal", "fantasy", 1.4f, "Cluster of glowing crystals", "gem,cave,magic,alien,glow,mineral", "crystals,gem,gems,diamond,quartz,amethyst",
                mb =>
                {
                    mb.Emit(0.55f).Shade(0.1f).Col(Pal.Hex("#7AD0FF"));
                    float[] ang = { 0f, 1.3f, 2.6f, 3.9f, 5.1f }; float[] h = { 1.4f, 0.9f, 1.1f, 0.7f, 1.0f };
                    for (int i = 0; i < 5; i++)
                    {
                        mb.Push(V(Mathf.Cos(ang[i]) * 0.2f, 0, Mathf.Sin(ang[i]) * 0.2f), V(Mathf.Sin(ang[i]) * 20f, ang[i] * 40f, -Mathf.Cos(ang[i]) * 20f));
                        mb.Lathe(Vector3.zero, Profile(0.12f, 0f, 0.14f, h[i] * 0.75f, 0f, h[i]), 5);
                        mb.Pop();
                    }
                    mb.Emit().Glow(V(0, 0.6f, 0), 1.4f, new Color(0.4f, 0.8f, 1f) * 0.4f);
                });

            // ================================================================ medieval and fantasy
            Add("castle_tower", "fantasy", 12f, "Round stone castle tower with battlements", "castle,medieval,fortress,knight,kingdom", "tower,keep,turret,castle",
                mb =>
                {
                    mb.Shade(0.07f).Grad(Pal.RockDark, Pal.Hex("#A8A49C"), 0f, 10f).Cyl(V(0, -1f, 0), 2.6f, 2.4f, 11f, 12, false);
                    mb.Col(Pal.Hex("#A8A49C")).Cyl(V(0, 10f, 0), 2.9f, 2.9f, 0.7f, 12);
                    for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2f; mb.Box(V(Mathf.Cos(a) * 2.6f, 11.1f, Mathf.Sin(a) * 2.6f), V(0.8f, 0.8f, 0.8f)); }
                    mb.Col(Pal.Roof).Cone(V(0, 10.7f, 0), 2.2f, 3.2f, 10);
                    mb.Window().Col(Pal.Hex("#8FB6D4"));
                    for (int i = 0; i < 3; i++) mb.Quad(V(-0.25f, 3f + i * 2.4f, 2.52f - i * 0.03f), V(0.25f, 3f + i * 2.4f, 2.52f - i * 0.03f), V(0.25f, 4f + i * 2.4f, 2.5f - i * 0.03f), V(-0.25f, 4f + i * 2.4f, 2.5f - i * 0.03f), Vector3.forward);
                    mb.Emit();
                });
            Add("castle_wall", "fantasy", 6f, "Section of crenellated castle wall, 10 m long", "castle,medieval,fortress,rampart", "wall,rampart,battlement,fortification",
                mb =>
                {
                    mb.Shade(0.07f).Grad(Pal.RockDark, Pal.Hex("#A8A49C"), 0f, 6f).Box(V(0, 2.5f, 0), V(10f, 7f, 1.6f));
                    mb.Col(Pal.Hex("#A8A49C"));
                    for (int i = 0; i < 6; i++) mb.Box(V(-4.5f + i * 1.8f, 6.4f, 0.55f), V(0.9f, 0.8f, 0.5f));
                });
            Add("castle_gate", "fantasy", 8f, "Castle gatehouse with an arched doorway", "castle,medieval,fortress,entrance", "gatehouse,gate,drawbridge,portcullis",
                mb =>
                {
                    mb.Shade(0.07f).Grad(Pal.RockDark, Pal.Hex("#A8A49C"), 0f, 8f);
                    mb.Box(V(-2.6f, 3.5f, 0), V(2.2f, 9f, 2.4f)).Box(V(2.6f, 3.5f, 0), V(2.2f, 9f, 2.4f)).Box(V(0, 6f, 0), V(3.2f, 4f, 2.4f));
                    mb.Col(Pal.Hex("#A8A49C"));
                    for (int i = 0; i < 5; i++) mb.Box(V(-3.2f + i * 1.6f, 8.4f, 0.9f), V(0.8f, 0.8f, 0.5f));
                    mb.Col(Pal.WoodDark).Quad(V(-1.5f, 0, -0.2f), V(1.5f, 0, -0.2f), V(1.5f, 4f, -0.2f), V(-1.5f, 4f, -0.2f), Vector3.forward);
                    mb.Col(Pal.MetalDark);
                    for (int i = 0; i < 5; i++) mb.Box(V(-1.2f + i * 0.6f, 2f, 1.1f), V(0.08f, 4f, 0.08f));
                });
            Add("medieval_house", "fantasy", 7f, "Timber-framed village house", "medieval,village,tudor,fantasy,town", "tudor house,village house,inn,tavern,cottage",
                mb =>
                {
                    mb.Shade(0.04f).Col(Pal.Plaster).Box(V(0, 2.2f, 0), V(6f, 5.4f, 5f));
                    mb.Col(Pal.WoodDark);
                    foreach (float x in new[] { -3f, -1f, 1f, 3f }) mb.Box(V(x, 2.2f, 2.52f), V(0.2f, 5.4f, 0.06f));
                    mb.Box(V(0, 2.6f, 2.52f), V(6.2f, 0.2f, 0.06f)).Box(V(0, 4.8f, 2.52f), V(6.2f, 0.2f, 0.06f));
                    Vector3 a = V(-3.4f, 4.9f, -2.9f), b = V(3.4f, 4.9f, -2.9f), c = V(3.4f, 4.9f, 2.9f), d = V(-3.4f, 4.9f, 2.9f), e = V(-3.4f, 7.2f, 0), f = V(3.4f, 7.2f, 0);
                    mb.Col(Pal.Hex("#6E4A3A")).Quad(a, b, f, e, V(0, 1, -1)).Quad(d, c, f, e, V(0, 1, 1));
                    mb.Col(Pal.Plaster).Tri(a, d, e, Vector3.left).Tri(b, c, f, Vector3.right);
                    mb.Col(Pal.WoodDark).Quad(V(-0.5f, 0, 2.54f), V(0.5f, 0, 2.54f), V(0.5f, 2.1f, 2.54f), V(-0.5f, 2.1f, 2.54f), Vector3.forward);
                    mb.Window().Col(Pal.Hex("#A8CCE4")).Quad(V(1.3f, 1.2f, 2.54f), V(2.5f, 1.2f, 2.54f), V(2.5f, 2.2f, 2.54f), V(1.3f, 2.2f, 2.54f), Vector3.forward);
                    mb.Quad(V(-2.5f, 3.2f, 2.54f), V(-1.3f, 3.2f, 2.54f), V(-1.3f, 4.2f, 2.54f), V(-2.5f, 4.2f, 2.54f), Vector3.forward);
                    mb.Emit();
                });
            Add("throne", "fantasy", 2.2f, "Golden royal throne", "king,queen,royal,castle,palace", "royal chair,king's chair",
                mb =>
                {
                    mb.Col(Pal.Gold).BoxOn(Vector3.zero, V(1f, 0.6f, 0.9f)).Box(V(0, 1.4f, -0.4f), V(1f, 1.6f, 0.12f)).Box(V(-0.5f, 0.85f, 0), V(0.12f, 0.5f, 0.9f)).Box(V(0.5f, 0.85f, 0), V(0.12f, 0.5f, 0.9f));
                    mb.Col(Pal.Hex("#8A1E2A")).Box(V(0, 0.63f, 0.03f), V(0.84f, 0.08f, 0.8f)).Box(V(0, 1.4f, -0.33f), V(0.8f, 1.3f, 0.03f));
                    mb.Col(Pal.Gold).Cone(V(-0.4f, 2.2f, -0.4f), 0.1f, 0.3f, 4).Cone(V(0.4f, 2.2f, -0.4f), 0.1f, 0.3f, 4).Cone(V(0, 2.2f, -0.4f), 0.13f, 0.45f, 4);
                });
            Add("portal", "fantasy", 3.2f, "Glowing magical gateway", "magic,gate,sci-fi,dimension,teleport", "gateway,stargate,magic door,wormhole",
                mb =>
                {
                    mb.Shade(0.08f).Col(Pal.RockDark).Push(V(0, 1.7f, 0), V(90f, 0, 0)).Torus(Vector3.zero, 1.5f, 0.24f, 20, 6).Pop();
                    mb.Emit(1f).Col(Pal.Hex("#8A5BFF")).Push(V(0, 1.7f, 0), V(90f, 0, 0)).Cyl(V(0, -0.02f, 0), 1.32f, 1.32f, 0.04f, 20).Pop();
                    mb.Col(Pal.Hex("#C8B0FF")).Push(V(0, 1.7f, 0), V(90f, 0, 0)).Cyl(V(0, -0.03f, 0), 0.7f, 0.7f, 0.06f, 14).Pop();
                    mb.Emit().Glow(V(0, 1.7f, 0), 3.5f, new Color(0.55f, 0.35f, 1f) * 0.6f);
                });
            Add("fairy_lights", "fantasy", 0.2f, "Cluster of tiny floating lights", "magic,glow,fairy,sparkle,night", "will o wisp,wisps,floating lights,fairies,lanterns",
                mb =>
                {
                    for (int i = 0; i < 5; i++)
                    {
                        Vector3 p = V(Hash01(i, 1, 2) - 0.5f, Hash01(i, 2, 2) * 0.6f, Hash01(i, 3, 2) - 0.5f) * 0.8f;
                        mb.Emit(1f).Col(Pal.FireCore).Sphere(p, 0.025f, 0);
                        mb.Emit().Glow(p, 0.3f, new Color(1f, 0.9f, 0.5f) * 0.7f);
                    }
                }).Flies();

            // ================================================================ spooky
            Add("gravestone", "spooky", 1f, "Weathered headstone", "graveyard,cemetery,halloween,haunted", "tombstone,headstone,grave",
                mb =>
                {
                    mb.Shade(0.08f).Col(Pal.Hex("#7A7E84")).Jitter(0.012f).BoxOn(Vector3.zero, V(0.6f, 0.75f, 0.14f));
                    mb.Push(V(0, 0.75f, 0), V(90f, 0, 0)).Cyl(V(0, -0.07f, 0), 0.3f, 0.3f, 0.14f, 10).Pop().Jitter(0f);
                    mb.Col(Pal.RockDark).Box(V(0, 0.55f, 0.072f), V(0.3f, 0.04f, 0.005f)).Box(V(0, 0.45f, 0.072f), V(0.22f, 0.03f, 0.005f));
                });
            Add("jack_o_lantern", "spooky", 0.45f, "Carved pumpkin with a glowing face", "halloween,pumpkin,spooky,autumn", "carved pumpkin,halloween pumpkin",
                mb =>
                {
                    mb.Shade(0.05f).Col(Pal.Orange);
                    for (int i = 0; i < 6; i++) { float a = i / 6f * Mathf.PI * 2f; mb.Sphere(V(Mathf.Cos(a) * 0.1f, 0.19f, Mathf.Sin(a) * 0.1f), V(0.14f, 0.19f, 0.14f), 0); }
                    mb.Col(Pal.LeafDark).Cyl(V(0, 0.34f, 0), 0.025f, 0.018f, 0.1f, 5);
                    mb.Emit(1f).Col(Pal.FireCore);
                    mb.Tri(V(-0.1f, 0.22f, 0.235f), V(-0.04f, 0.22f, 0.24f), V(-0.07f, 0.28f, 0.235f), Vector3.forward).Tri(V(0.1f, 0.22f, 0.235f), V(0.04f, 0.22f, 0.24f), V(0.07f, 0.28f, 0.235f), Vector3.forward);
                    mb.Quad(V(-0.1f, 0.14f, 0.24f), V(0.1f, 0.14f, 0.24f), V(0.07f, 0.09f, 0.24f), V(-0.07f, 0.09f, 0.24f), Vector3.forward);
                    mb.Emit().Glow(V(0, 0.18f, 0.2f), 0.7f, new Color(1f, 0.6f, 0.15f) * 0.6f);
                }).Hold().Solid(3f, 0.2f, ColliderShape.Sphere);
            Add("cauldron", "spooky", 0.8f, "Bubbling witch's cauldron", "witch,magic,halloween,potion", "pot,witch pot,potion",
                mb =>
                {
                    mb.Shade(0.05f).Col(Pal.Hex("#2A2A2E")).Lathe(V(0, 0.12f, 0), Profile(0f, 0f, 0.3f, 0.03f, 0.45f, 0.3f, 0.42f, 0.58f, 0.46f, 0.62f), 10);
                    for (int i = 0; i < 3; i++) { float a = i / 3f * Mathf.PI * 2f; mb.Cyl(V(Mathf.Cos(a) * 0.28f, 0, Mathf.Sin(a) * 0.28f), 0.04f, 0.05f, 0.16f, 5); }
                    mb.Emit(0.9f).Col(Pal.Lime).Cyl(V(0, 0.66f, 0), 0.41f, 0.41f, 0.02f, 10);
                    mb.Sphere(V(0.12f, 0.7f, 0.05f), 0.06f, 0).Sphere(V(-0.14f, 0.69f, -0.1f), 0.045f, 0).Sphere(V(0, 0.71f, 0.2f), 0.035f, 0);
                    mb.Emit().Glow(V(0, 0.8f, 0), 1.2f, new Color(0.5f, 1f, 0.3f) * 0.45f);
                });
            Add("haunted_house", "spooky", 11f, "Crooked old mansion with glowing windows", "halloween,haunted,mansion,creepy,ghost", "mansion,spooky house,manor",
                mb =>
                {
                    Color wall = Pal.Hex("#4A4450");
                    mb.Shade(0.06f).Col(wall).Box(V(0, 3f, 0), V(9f, 7f, 7f)).Box(V(3.2f, 5.5f, 1f), V(3f, 12f, 3f));
                    Vector3 a = V(-4.9f, 6.5f, -3.9f), b = V(4.9f, 6.5f, -3.9f), c = V(4.9f, 6.5f, 3.9f), d = V(-4.9f, 6.5f, 3.9f), e = V(-4.9f, 9.5f, 0), f = V(4.9f, 9.5f, 0);
                    mb.Col(Pal.Hex("#2A2630")).Quad(a, b, f, e, V(0, 1, -1)).Quad(d, c, f, e, V(0, 1, 1));
                    mb.Col(wall).Tri(a, d, e, Vector3.left).Tri(b, c, f, Vector3.right);
                    mb.Col(Pal.Hex("#2A2630")).Pyramid(V(3.2f, 11.5f, 1f), 3.6f, 3.4f);
                    mb.Col(Pal.Black).Quad(V(-0.7f, 0, 3.52f), V(0.7f, 0, 3.52f), V(0.7f, 2.6f, 3.52f), V(-0.7f, 2.6f, 3.52f), Vector3.forward);
                    mb.Emit(0.5f, 1f).Col(Pal.Hex("#C8E060"));
                    foreach (float x in new[] { -3f, -1.6f, 1.6f }) { mb.Quad(V(x - 0.4f, 1.2f, 3.52f), V(x + 0.4f, 1.2f, 3.52f), V(x + 0.4f, 2.6f, 3.52f), V(x - 0.4f, 2.6f, 3.52f), Vector3.forward); mb.Quad(V(x - 0.4f, 4f, 3.52f), V(x + 0.4f, 4f, 3.52f), V(x + 0.4f, 5.4f, 3.52f), V(x - 0.4f, 5.4f, 3.52f), Vector3.forward); }
                    mb.Quad(V(2.8f, 9f, 2.52f), V(3.6f, 9f, 2.52f), V(3.6f, 10.4f, 2.52f), V(2.8f, 10.4f, 2.52f), Vector3.forward);
                    mb.Emit();
                });
            Add("bat", "spooky", 0.3f, "Small black bat", "halloween,cave,night,flying,vampire", "bats,vampire bat",
                mb =>
                {
                    mb.Col(Pal.Hex("#2A2630")).Sphere(Vector3.zero, V(0.06f, 0.07f, 0.1f), 0);
                    mb.TriTwoSided(V(0.04f, 0.02f, 0.05f), V(0.42f, 0.1f, 0f), V(0.04f, 0f, -0.08f));
                    mb.TriTwoSided(V(-0.04f, 0.02f, 0.05f), V(-0.42f, 0.1f, 0f), V(-0.04f, 0f, -0.08f));
                    mb.TriTwoSided(V(0.2f, 0.06f, -0.03f), V(0.3f, 0.04f, -0.16f), V(0.42f, 0.1f, 0f));
                    mb.TriTwoSided(V(-0.2f, 0.06f, -0.03f), V(-0.3f, 0.04f, -0.16f), V(-0.42f, 0.1f, 0f));
                }).Flies();
            Add("spider_web", "spooky", 1.6f, "Big cobweb", "halloween,spider,haunted,creepy", "cobweb,web",
                mb =>
                {
                    mb.Emit(0.3f).Col(Pal.OffWhite);
                    for (int i = 0; i < 8; i++)
                    {
                        float a0 = i / 8f * Mathf.PI * 2f, a1 = (i + 1) / 8f * Mathf.PI * 2f;
                        mb.Rod(V(0, 0.8f, 0), V(Mathf.Cos(a0) * 0.8f, 0.8f + Mathf.Sin(a0) * 0.8f, 0), 0.006f, 0.006f, 3, false);
                        for (int r = 1; r <= 3; r++) mb.Rod(V(Mathf.Cos(a0) * r * 0.25f, 0.8f + Mathf.Sin(a0) * r * 0.25f, 0), V(Mathf.Cos(a1) * r * 0.25f, 0.8f + Mathf.Sin(a1) * r * 0.25f, 0), 0.005f, 0.005f, 3, false);
                    }
                    mb.Emit();
                });

            // ================================================================ winter
            Add("igloo", "winter", 1.7f, "Dome of snow blocks with a tunnel door", "snow,arctic,ice,inuit,winter", "ice house,snow house",
                mb =>
                {
                    mb.Shade(0.05f).Col(Pal.Snow).Lathe(Vector3.zero, Profile(1.7f, 0f, 1.6f, 0.6f, 1.2f, 1.25f, 0.6f, 1.6f, 0f, 1.7f), 12);
                    mb.Push(V(0, 0, 1.7f), V(90f, 0, 0)).Cyl(V(0, -0.5f, 0), 0.6f, 0.6f, 1f, 8, false).Pop();
                    mb.Col(Pal.Black).Push(V(0, 0.02f, 2.19f), V(90f, 0, 0)).Cyl(Vector3.zero, 0.5f, 0.5f, 0.02f, 8).Pop();
                });
            Add("ice_crystal", "winter", 2.5f, "Tall shard of blue ice", "ice,frozen,winter,arctic,glacier", "iceberg,ice spike,icicle,glacier",
                mb =>
                {
                    mb.Emit(0.25f).Shade(0.1f).Grad(Pal.Hex("#7AB8E0"), Pal.Hex("#E6F6FF"), 0f, 2.5f);
                    mb.Lathe(Vector3.zero, Profile(0.5f, -0.3f, 0.55f, 0.8f, 0.3f, 1.9f, 0f, 2.5f), 5);
                    mb.Push(V(0.5f, 0, 0.2f), V(0, 30f, 15f)).Lathe(Vector3.zero, Profile(0.3f, -0.2f, 0.32f, 0.6f, 0f, 1.4f), 5).Pop();
                    mb.Emit();
                });
            Add("log_cabin", "winter", 5f, "Log cabin with a smoking chimney", "cabin,woods,mountain,cozy,winter", "cabin,hut,lodge,chalet,shack",
                mb =>
                {
                    mb.Shade(0.06f).Col(Pal.Trunk);
                    for (int i = 0; i < 8; i++) mb.Box(V(0, 0.2f + i * 0.4f, 0), V(6.2f, 0.36f, 5.2f));
                    Vector3 a = V(-3.5f, 3.2f, -3f), b = V(3.5f, 3.2f, -3f), c = V(3.5f, 3.2f, 3f), d = V(-3.5f, 3.2f, 3f), e = V(-3.5f, 5f, 0), f = V(3.5f, 5f, 0);
                    mb.Col(Pal.Hex("#5A3A2A")).Quad(a, b, f, e, V(0, 1, -1)).Quad(d, c, f, e, V(0, 1, 1));
                    mb.Col(Pal.Trunk).Tri(a, d, e, Vector3.left).Tri(b, c, f, Vector3.right);
                    mb.Col(Pal.Rock).BoxOn(V(-2f, 3.6f, -1f), V(0.9f, 2.2f, 0.9f));
                    mb.Col(Pal.WoodDark).Quad(V(-0.5f, 0, 2.62f), V(0.5f, 0, 2.62f), V(0.5f, 2.1f, 2.62f), V(-0.5f, 2.1f, 2.62f), Vector3.forward);
                    mb.Window().Col(Pal.Hex("#A8CCE4")).Quad(V(1.4f, 1.2f, 2.62f), V(2.5f, 1.2f, 2.62f), V(2.5f, 2.1f, 2.62f), V(1.4f, 2.1f, 2.62f), Vector3.forward);
                    mb.Emit().Glow(V(1.95f, 1.65f, 2.8f), 1.4f, new Color(1f, 0.75f, 0.4f) * 0.5f, true);
                });
            Add("christmas_tree", "winter", 3f, "Decorated Christmas tree with lights and a star", "christmas,holiday,winter,festive,xmas", "xmas tree,holiday tree",
                mb =>
                {
                    Conifer(mb, 3f, Pal.TrunkDark, Pal.PineDark, Pal.Pine, 4, 1.1f, false);
                    mb.NoSway();
                    Color[] lights = { Pal.Red, Pal.Yellow, Pal.Blue, Pal.Pink, Pal.Cyan };
                    for (int i = 0; i < 16; i++)
                    {
                        float y = 0.7f + i * 0.13f, r = 1.0f * (1f - (y - 0.5f) / 2.6f) + 0.05f, a = i * 1.9f;
                        Vector3 p = V(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                        mb.Emit(1f).Col(lights[i % lights.Length]).Sphere(p, 0.05f, 0);
                    }
                    mb.Emit(1f).Col(Pal.Gold).Sphere(V(0, 3.05f, 0), V(0.14f, 0.14f, 0.04f), 0);
                    mb.Emit().Glow(V(0, 3.05f, 0), 0.7f, new Color(1f, 0.85f, 0.4f) * 0.7f);
                });

            // ================================================================ landmarks and far scenery
            Add("mountain", "landmarks", 60f, "A distant snow-capped mountain. Place it far away as a backdrop", "peak,backdrop,alps,far,scenery", "peak,mount,summit,mount everest,alp",
                mb =>
                {
                    mb.Shade(0.1f).Jitter(2.5f).Grad(Pal.RockDark, Pal.Rock, 0f, 40f);
                    mb.Lathe(Vector3.zero, Profile(55f, -4f, 40f, 12f, 24f, 30f, 12f, 46f), 9);
                    mb.Col(Pal.Snow).Lathe(Vector3.zero, Profile(12f, 46f, 6f, 55f, 0f, 61f), 9);
                    mb.Jitter(0f);
                });
            Add("volcano", "landmarks", 45f, "A smoking volcano with a glowing crater. Place it far away", "lava,eruption,fire,backdrop,far", "volcanic mountain,crater",
                mb =>
                {
                    mb.Shade(0.1f).Jitter(2f).Grad(Pal.Hex("#2A2224"), Pal.Hex("#5A4640"), 0f, 40f);
                    mb.Lathe(Vector3.zero, Profile(60f, -4f, 38f, 12f, 20f, 30f, 11f, 42f, 9f, 40f), 10);
                    mb.Jitter(0f).Emit(1f).Col(Pal.Fire).Cyl(V(0, 39.5f, 0), 9.5f, 9.5f, 0.6f, 10);
                    for (int i = 0; i < 4; i++) { float a = i * 1.7f; mb.Rod(V(Mathf.Cos(a) * 9f, 40f, Mathf.Sin(a) * 9f), V(Mathf.Cos(a) * 30f, 16f, Mathf.Sin(a) * 30f), 1.2f, 0.5f, 4, false); }
                    mb.Emit().Glow(V(0, 44f, 0), 30f, new Color(1f, 0.4f, 0.1f) * 0.5f);
                });
            Add("pyramid_tomb", "landmarks", 40f, "A great stepped desert pyramid", "egypt,desert,ancient,pharaoh,tomb", "pyramid,great pyramid,egyptian pyramid,ziggurat",
                mb =>
                {
                    mb.Shade(0.05f);
                    for (int i = 0; i < 10; i++) mb.Col(Color.Lerp(Pal.SandDark, Pal.Sand, i / 9f)).BoxOn(V(0, i * 4f, 0), V(56f - i * 5.4f, 4f, 56f - i * 5.4f));
                    mb.Col(Pal.Gold).Pyramid(V(0, 40f, 0), 3f, 2.4f);
                });
            Add("greek_column", "landmarks", 5f, "Fluted marble column", "ancient,greece,rome,ruins,temple", "pillar,column,ionic column",
                mb =>
                {
                    mb.Shade(0.05f).Col(Pal.Hex("#E8E2D4")).BoxOn(Vector3.zero, V(1f, 0.3f, 1f)).Cyl(V(0, 0.3f, 0), 0.38f, 0.33f, 4.3f, 10, false).BoxOn(V(0, 4.6f, 0), V(1f, 0.35f, 1f));
                });
            Add("ruined_arch", "landmarks", 5f, "Crumbling stone archway", "ancient,ruins,temple,stone", "arch,ruins,archway,broken arch",
                mb =>
                {
                    mb.Shade(0.08f).Jitter(0.04f).Col(Pal.Hex("#B8B0A0"));
                    mb.BoxOn(V(-1.6f, 0, 0), V(0.9f, 3.4f, 0.9f)).BoxOn(V(1.6f, 0, 0), V(0.9f, 2.6f, 0.9f));
                    for (int i = 0; i < 5; i++) { float a = Mathf.PI - i / 6f * Mathf.PI; mb.Push(V(Mathf.Cos(a) * 1.6f, 3.4f + Mathf.Sin(a) * 1.5f, 0), V(0, 0, a * Mathf.Rad2Deg - 90f)).Box(Vector3.zero, V(0.95f, 0.75f, 0.9f)).Pop(); }
                    mb.Jitter(0f);
                });
            Add("obelisk", "landmarks", 9f, "Tall stone obelisk", "monument,egypt,ancient,landmark", "monolith,monument,needle",
                mb => mb.Shade(0.05f).Col(Pal.Hex("#C8B890")).BoxOn(Vector3.zero, V(1.8f, 0.6f, 1.8f)).Lathe(V(0, 0.6f, 0), Profile(0.75f, 0f, 0.5f, 7.6f, 0f, 8.4f), 4, Mathf.PI / 4f));
            Add("standing_stone", "landmarks", 4f, "Ancient standing stone", "stonehenge,druid,ancient,monolith", "menhir,megalith,stonehenge",
                mb => mb.Shade(0.1f).Jitter(0.08f).Grad(Pal.RockDark, Pal.Rock, 0f, 4f).BoxOn(V(0, -0.3f, 0), V(1.3f, 4.3f, 0.8f)).Jitter(0f));
            Add("statue", "landmarks", 4f, "Stone statue of a figure on a plinth", "monument,park,memorial,ancient", "monument,memorial,stone figure",
                mb =>
                {
                    mb.Shade(0.05f).Col(Pal.Hex("#8E9A94")).BoxOn(Vector3.zero, V(1.4f, 1.3f, 1.4f));
                    mb.Push(V(0, 1.3f, 0));
                    Figure(mb, 2.6f, Pal.Hex("#9AA8A0"), Pal.Hex("#9AA8A0"), Pal.Hex("#9AA8A0"), Pal.Hex("#9AA8A0"), true);
                    mb.Pop();
                });
            Add("fountain", "landmarks", 2.4f, "Tiered stone fountain with water", "park,plaza,water,garden,square", "water fountain,water feature",
                mb =>
                {
                    mb.Shade(0.05f).Col(Pal.Concrete).Cyl(Vector3.zero, 2.2f, 2.2f, 0.5f, 14).Cyl(V(0, 0.5f, 0), 0.3f, 0.25f, 1.2f, 8).Lathe(V(0, 1.6f, 0), Profile(0.2f, 0f, 1f, 0.2f, 1.05f, 0.3f), 12).Cyl(V(0, 1.7f, 0), 0.14f, 0.1f, 0.6f, 6);
                    mb.Emit(0.2f).Col(Pal.Hex("#6EC6E8")).Cyl(V(0, 0.48f, 0), 2.0f, 2.0f, 0.04f, 14).Cyl(V(0, 1.88f, 0), 0.95f, 0.95f, 0.02f, 12);
                    mb.Emit();
                });
            Add("bridge", "landmarks", 3f, "Arched stone footbridge", "river,park,garden,crossing", "footbridge,stone bridge,arch bridge",
                mb =>
                {
                    mb.Shade(0.07f).Col(Pal.Hex("#A8A090"));
                    for (int i = 0; i < 9; i++)
                    {
                        float t0 = i / 9f, t1 = (i + 1) / 9f;
                        float z0 = -5f + t0 * 10f, z1 = -5f + t1 * 10f, y0 = Mathf.Sin(t0 * Mathf.PI) * 2.2f, y1 = Mathf.Sin(t1 * Mathf.PI) * 2.2f;
                        mb.Quad(V(-1.4f, y0, z0), V(1.4f, y0, z0), V(1.4f, y1, z1), V(-1.4f, y1, z1), Vector3.up);
                        mb.Quad(V(-1.4f, y0 - 0.5f, z0), V(1.4f, y0 - 0.5f, z0), V(1.4f, y1 - 0.5f, z1), V(-1.4f, y1 - 0.5f, z1), Vector3.down);
                        foreach (float x in new[] { -1.4f, 1.4f }) mb.Quad(V(x, y0 - 0.5f, z0), V(x, y0 + 0.7f, z0), V(x, y1 + 0.7f, z1), V(x, y1 - 0.5f, z1), V(x, 0, 0));
                    }
                });
            Add("ferris_wheel", "landmarks", 30f, "Giant Ferris wheel with coloured cabins", "fair,carnival,amusement park,funfair,landmark", "big wheel,observation wheel",
                mb =>
                {
                    mb.Col(Pal.Metal);
                    foreach (float z in new[] { -1.4f, 1.4f }) { mb.Rod(V(-7f, 0, z), V(0, 16f, z), 0.3f, 0.2f, 5).Rod(V(7f, 0, z), V(0, 16f, z), 0.3f, 0.2f, 5); }
                    mb.Push(V(0, 16f, 0), V(90f, 0, 0)).Torus(Vector3.zero, 13f, 0.25f, 28, 4).Torus(Vector3.zero, 6.5f, 0.15f, 20, 4).Pop();
                    Color[] cabins = { Pal.Red, Pal.Yellow, Pal.Blue, Pal.Green, Pal.Pink, Pal.Orange };
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i / 12f * Mathf.PI * 2f;
                        Vector3 p = V(Mathf.Cos(a) * 13f, 16f + Mathf.Sin(a) * 13f, 0);
                        mb.Col(Pal.Metal).Rod(V(0, 16f, 0), p, 0.08f, 0.08f, 3, false);
                        mb.Emit(0.2f, 0.8f).Col(cabins[i % cabins.Length]).Box(p - V(0, 1.1f, 0), V(1.5f, 1.4f, 1.6f));
                    }
                    mb.Emit();
                });
            Add("hot_air_balloon", "sky", 14f, "Striped hot air balloon with a basket", "sky,flying,fair,adventure,float", "balloon,airship,blimp",
                mb =>
                {
                    Color[] stripes = { Pal.Red, Pal.Yellow, Pal.Blue, Pal.Orange };
                    for (int i = 0; i < 8; i++)
                    {
                        mb.Col(stripes[i % 4]);
                        float a0 = i / 8f * Mathf.PI * 2f, a1 = (i + 1) / 8f * Mathf.PI * 2f;
                        float[] rs = { 1.2f, 3.6f, 5f, 4.6f, 2.6f, 0f }; float[] ys = { 4f, 6.5f, 9.5f, 12f, 13.6f, 14f };
                        for (int j = 0; j < 5; j++)
                        {
                            Vector3 q0 = V(Mathf.Cos(a0) * rs[j], ys[j], Mathf.Sin(a0) * rs[j]), q1 = V(Mathf.Cos(a1) * rs[j], ys[j], Mathf.Sin(a1) * rs[j]);
                            Vector3 q2 = V(Mathf.Cos(a1) * rs[j + 1], ys[j + 1], Mathf.Sin(a1) * rs[j + 1]), q3 = V(Mathf.Cos(a0) * rs[j + 1], ys[j + 1], Mathf.Sin(a0) * rs[j + 1]);
                            Vector3 outside = (q0 + q1 + q2 + q3) * 0.25f - V(0, 9f, 0);
                            if (j == 4) mb.Tri(q0, q1, q2, outside); else mb.Quad(q0, q1, q2, q3, outside);
                        }
                    }
                    mb.Col(Pal.Wood).BoxOn(V(0, 0, 0), V(1.6f, 1.2f, 1.6f));
                    mb.Col(Pal.WoodDark);
                    foreach (float x in new[] { -0.75f, 0.75f }) foreach (float z in new[] { -0.75f, 0.75f }) mb.Rod(V(x, 1.2f, z), V(x * 1.4f, 4.1f, z * 1.4f), 0.03f, 0.03f, 3, false);
                }).Flies();
            Add("airplane", "sky", 4f, "Passenger jet flying overhead", "sky,flying,plane,travel,airport", "plane,jet,aeroplane,aircraft,airliner",
                mb =>
                {
                    mb.Shade(0.03f).Col(Pal.White).Sphere(Vector3.zero, V(1.6f, 1.6f, 13f), 1);
                    mb.Col(Pal.Hex("#C8D0D8")).TriTwoSided(V(1.2f, -0.3f, 2f), V(13f, 0.4f, -3f), V(1.2f, -0.3f, -3f));
                    mb.TriTwoSided(V(-1.2f, -0.3f, 2f), V(-13f, 0.4f, -3f), V(-1.2f, -0.3f, -3f));
                    mb.TriTwoSided(V(0, 1f, -9f), V(0, 5f, -13f), V(0, 1f, -12.5f));
                    mb.TriTwoSided(V(0.8f, 0.5f, -10f), V(5f, 0.8f, -12.8f), V(0.8f, 0.5f, -12.5f));
                    mb.TriTwoSided(V(-0.8f, 0.5f, -10f), V(-5f, 0.8f, -12.8f), V(-0.8f, 0.5f, -12.5f));
                    mb.Col(Pal.Blue).Box(V(0, 0.2f, 1f), V(3.24f, 0.4f, 18f));
                    mb.Col(Pal.MetalDark).Rod(V(5f, -0.8f, -1f), V(5f, -0.8f, 2f), 0.6f, 0.6f, 6).Rod(V(-5f, -0.8f, -1f), V(-5f, -0.8f, 2f), 0.6f, 0.6f, 6);
                }).Flies();
            Add("rainbow", "sky", 40f, "A huge rainbow arching across the sky", "sky,colour,weather,magic,happy", "arc,rainbow arch",
                mb =>
                {
                    Color[] bands = { Pal.Hex("#E5484D"), Pal.Hex("#F76B15"), Pal.Hex("#F5D90A"), Pal.Hex("#46A758"), Pal.Hex("#3E7BFA"), Pal.Hex("#8E4EC6") };
                    mb.Emit(0.8f);
                    for (int b = 0; b < 6; b++)
                    {
                        float r0 = 40f - b * 1.4f, r1 = r0 - 1.4f;
                        mb.Col(bands[b]);
                        for (int i = 0; i < 20; i++)
                        {
                            float a0 = i / 20f * Mathf.PI, a1 = (i + 1) / 20f * Mathf.PI;
                            mb.QuadTwoSided(V(Mathf.Cos(a0) * r0, Mathf.Sin(a0) * r0, 0), V(Mathf.Cos(a1) * r0, Mathf.Sin(a1) * r0, 0), V(Mathf.Cos(a1) * r1, Mathf.Sin(a1) * r1, 0), V(Mathf.Cos(a0) * r1, Mathf.Sin(a0) * r1, 0));
                        }
                    }
                    mb.Emit();
                });

            // ================================================================ japan and gardens
            Add("torii_gate", "japan", 5f, "Red Japanese shrine gate", "japan,shrine,temple,asia,zen", "torii,shrine gate,japanese gate",
                mb =>
                {
                    Color r = Pal.Hex("#D8382A");
                    mb.Shade(0.03f).Col(r).Cyl(V(-1.8f, 0, 0), 0.22f, 0.18f, 4.4f, 8).Cyl(V(1.8f, 0, 0), 0.22f, 0.18f, 4.4f, 8);
                    mb.Box(V(0, 3.5f, 0), V(4.6f, 0.26f, 0.26f));
                    mb.Col(Pal.Black).Box(V(0, 4.5f, 0), V(5.6f, 0.3f, 0.5f));
                    mb.Col(r).Box(V(0, 4.25f, 0), V(5.2f, 0.22f, 0.36f)).Box(V(0, 3.9f, 0), V(0.3f, 0.6f, 0.2f));
                    mb.Col(Pal.Black).Cyl(V(-1.8f, 0, 0), 0.26f, 0.26f, 0.3f, 8).Cyl(V(1.8f, 0, 0), 0.26f, 0.26f, 0.3f, 8);
                });
            Add("pagoda", "japan", 14f, "Five-tiered pagoda", "japan,temple,asia,china,zen,landmark", "temple,japanese temple,shrine,chinese temple",
                mb =>
                {
                    for (int i = 0; i < 5; i++)
                    {
                        float w = 6f - i * 0.9f, y = i * 2.5f;
                        mb.Shade(0.03f).Col(Pal.Hex("#B83A2E")).BoxOn(V(0, y, 0), V(w * 0.62f, 1.7f, w * 0.62f));
                        mb.Col(Pal.Hex("#2A2E34")).Lathe(V(0, y + 1.7f, 0), Profile(w * 0.82f, 0f, w * 0.5f, 0.45f, w * 0.3f, 0.8f), 4, Mathf.PI / 4f);
                    }
                    mb.Col(Pal.Gold).Cyl(V(0, 12.7f, 0), 0.12f, 0.04f, 2f, 5);
                });
            Add("stone_lantern", "japan", 1.5f, "Japanese stone garden lantern", "japan,garden,zen,light,temple", "toro,garden lantern,japanese lantern",
                mb =>
                {
                    mb.Shade(0.06f).Col(Pal.Hex("#8E9088")).BoxOn(Vector3.zero, V(0.5f, 0.15f, 0.5f)).Cyl(V(0, 0.15f, 0), 0.1f, 0.09f, 0.6f, 6).BoxOn(V(0, 0.75f, 0), V(0.44f, 0.08f, 0.44f));
                    mb.Window().Col(Pal.Cream).BoxOn(V(0, 0.83f, 0), V(0.3f, 0.3f, 0.3f));
                    mb.Emit().Col(Pal.Hex("#6E7068")).Pyramid(V(0, 1.13f, 0), 0.6f, 0.3f);
                    mb.Glow(V(0, 0.98f, 0), 0.9f, new Color(1f, 0.8f, 0.5f) * 0.6f, true);
                });
            Add("paper_lantern", "japan", 0.5f, "Glowing paper lantern that floats", "festival,light,japan,china,night,glow", "sky lantern,chinese lantern,floating lantern",
                mb =>
                {
                    mb.Emit(0.8f).Col(Pal.Hex("#FF9A5A")).Lathe(Vector3.zero, Profile(0.1f, 0f, 0.2f, 0.12f, 0.22f, 0.3f, 0.14f, 0.45f), 8);
                    mb.Emit().Glow(V(0, 0.2f, 0), 0.8f, new Color(1f, 0.6f, 0.3f) * 0.7f);
                }).Flies();
            Add("zen_rock", "japan", 1f, "Smooth dark garden rock", "japan,garden,zen,stone", "garden rock,river stone",
                mb => mb.Shade(0.06f).Col(Pal.Hex("#5A5E62")).Blob(V(0, 0.4f, 0), V(0.7f, 0.5f, 0.55f), 0.12f, 21, 1));

            // ================================================================ desert and west
            Add("cow_skull", "desert", 0.3f, "Bleached cattle skull", "desert,western,bones,dry", "skull,bones",
                mb =>
                {
                    mb.Col(Pal.Hex("#E8E0CC")).Sphere(V(0, 0.12f, 0), V(0.14f, 0.1f, 0.22f), 1);
                    mb.Rod(V(0.1f, 0.16f, -0.1f), V(0.42f, 0.3f, -0.14f), 0.035f, 0.01f, 4).Rod(V(-0.1f, 0.16f, -0.1f), V(-0.42f, 0.3f, -0.14f), 0.035f, 0.01f, 4);
                    mb.Col(Pal.Black).Sphere(V(0.07f, 0.15f, 0.05f), 0.035f, 0).Sphere(V(-0.07f, 0.15f, 0.05f), 0.035f, 0);
                });
            Add("tumbleweed", "desert", 0.7f, "Dry rolling tumbleweed", "desert,western,wind,dry", "tumble weed",
                mb =>
                {
                    mb.Col(Pal.Hex("#A8905A"));
                    for (int i = 0; i < 14; i++)
                    {
                        Vector3 a = V(Mathf.Sin(i * 2.1f), Mathf.Cos(i * 1.3f), Mathf.Sin(i * 0.7f + 1f)).normalized * 0.33f, b = V(Mathf.Cos(i * 1.7f), Mathf.Sin(i * 2.9f), Mathf.Cos(i * 0.9f)).normalized * 0.33f;
                        mb.Rod(a + V(0, 0.35f, 0), b + V(0, 0.35f, 0), 0.012f, 0.012f, 3, false);
                    }
                }).Solid(0.3f, 0.5f, ColliderShape.Sphere);
            Add("saloon", "desert", 8f, "Wild West saloon with a porch", "western,cowboy,town,frontier", "western building,old west,tavern,frontier town",
                mb =>
                {
                    mb.Shade(0.05f).Col(Pal.Hex("#A8804A")).Box(V(0, 2.4f, 0), V(10f, 5.8f, 7f));
                    mb.Col(Pal.Hex("#8A6638")).Box(V(0, 6.2f, 3.4f), V(10f, 2.8f, 0.2f));
                    mb.Col(Pal.WoodDark).Box(V(0, 3.2f, 4.5f), V(10.4f, 0.14f, 2.2f));
                    for (int i = 0; i < 4; i++) mb.Cyl(V(-4.6f + i * 3.07f, 0, 5.4f), 0.1f, 0.1f, 3.2f, 5);
                    mb.Emit(0.4f).Col(Pal.Cream).Box(V(0, 6.3f, 3.52f), V(5f, 1f, 0.04f));
                    mb.Emit().Col(Pal.WoodDark).Quad(V(-0.8f, 0, 3.52f), V(0.8f, 0, 3.52f), V(0.8f, 2.2f, 3.52f), V(-0.8f, 2.2f, 3.52f), Vector3.forward);
                    mb.Window().Col(Pal.Hex("#A8CCE4")).Quad(V(2f, 1.2f, 3.52f), V(3.8f, 1.2f, 3.52f), V(3.8f, 2.6f, 3.52f), V(2f, 2.6f, 3.52f), Vector3.forward).Quad(V(-3.8f, 1.2f, 3.52f), V(-2f, 1.2f, 3.52f), V(-2f, 2.6f, 3.52f), V(-3.8f, 2.6f, 3.52f), Vector3.forward);
                    mb.Emit();
                });

            // ================================================================ neon and stage
            Add("neon_sign", "city", 3f, "Glowing neon sign on a pole. Tint it any colour", "neon,city,night,glow,cyberpunk,bar", "sign,billboard,neon",
                mb =>
                {
                    mb.Col(Pal.MetalDark).Cyl(Vector3.zero, 0.07f, 0.07f, 2.2f, 5).Box(V(0, 2.9f, 0), V(2.2f, 1.4f, 0.12f));
                    mb.Emit(1f).Col(Pal.White).Box(V(0, 2.9f, 0.07f), V(1.9f, 0.14f, 0.02f)).Box(V(0, 3.3f, 0.07f), V(1.5f, 0.14f, 0.02f)).Box(V(0, 2.5f, 0.07f), V(1.5f, 0.14f, 0.02f));
                    mb.Push(V(0, 2.9f, 0.07f), V(90f, 0, 0), V(1f, 1f, 0.62f)).Torus(Vector3.zero, 1.02f, 0.035f, 4, 3).Pop();
                    mb.Emit().Glow(V(0, 2.9f, 0.2f), 2.6f, Color.white * 0.4f);
                });
            Add("disco_ball", "party", 0.6f, "Mirror ball that spins and sparkles", "party,dance,club,music,sparkle", "mirror ball,glitter ball",
                mb =>
                {
                    mb.Emit(0.5f).Shade(0.35f).Col(Pal.Steel).Sphere(V(0, 0.3f, 0), 0.3f, 2);
                    mb.Emit().Col(Pal.MetalDark).Cyl(V(0, 0.6f, 0), 0.01f, 0.01f, 0.5f, 3);
                    mb.Glow(V(0, 0.3f, 0), 1.6f, new Color(0.9f, 0.9f, 1f) * 0.4f);
                }).Flies();
            Add("stage_light", "party", 2.6f, "Coloured spotlight on a stand", "stage,concert,party,show,club", "spotlight,light,strobe",
                mb =>
                {
                    mb.Col(Pal.MetalDark).Cyl(Vector3.zero, 0.3f, 0.3f, 0.04f, 8).Cyl(Vector3.zero, 0.035f, 0.035f, 2.3f, 5);
                    mb.Push(V(0, 2.4f, 0), V(60f, 0, 0)).Cyl(V(0, -0.2f, 0), 0.16f, 0.2f, 0.4f, 8);
                    mb.Emit(1f).Col(Pal.White).Cyl(V(0, 0.2f, 0), 0.18f, 0.18f, 0.01f, 8).Pop();
                    mb.Emit().Glow(V(0, 2.5f, 0.18f), 1.6f, Color.white * 0.6f);
                });
            Add("speaker", "party", 1.2f, "Big loudspeaker stack", "music,party,concert,stage,sound", "loudspeaker,amp,amplifier,boombox",
                mb =>
                {
                    mb.Col(Pal.Hex("#22242A")).BoxOn(Vector3.zero, V(0.7f, 1.2f, 0.6f));
                    mb.Col(Pal.Hex("#3A3E48")).Push(V(0, 0.38f, 0.3f), V(90f, 0, 0)).Cyl(Vector3.zero, 0.26f, 0.2f, 0.03f, 12).Pop();
                    mb.Push(V(0, 0.92f, 0.3f), V(90f, 0, 0)).Cyl(Vector3.zero, 0.15f, 0.11f, 0.03f, 10).Pop();
                });
        }
    }
}
