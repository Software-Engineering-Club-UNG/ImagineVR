using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    public static partial class Library
    {
        // ------------------------------------------------------------------ shared pieces

        static float Hash01(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093 ^ b * 19349663 ^ c * 83492791);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        /// <summary>
        /// A grid of windows on the four sides of a box standing on the ground. About two in three
        /// are "lit": plain glass by day, glowing warm at night.
        /// </summary>
        static void Windows(MeshBuilder mb, Vector3 baseCenter, float w, float d, float y0, float y1, float floorHeight, float bay, int seed, float lit = 0.62f)
        {
            int floors = Mathf.Max(1, Mathf.FloorToInt((y1 - y0) / floorHeight));
            float winH = floorHeight * 0.55f, winW = bay * 0.62f;
            for (int side = 0; side < 4; side++)
            {
                float span = side % 2 == 0 ? w : d;
                float depth = (side % 2 == 0 ? d : w) * 0.5f + 0.03f;
                int cols = Mathf.Max(1, Mathf.FloorToInt((span - 0.8f) / bay));
                Vector3 normal = side == 0 ? Vector3.forward : side == 1 ? Vector3.right : side == 2 ? Vector3.back : Vector3.left;
                Vector3 along = side % 2 == 0 ? Vector3.right : Vector3.forward;
                for (int f = 0; f < floors; f++)
                {
                    float cy = y0 + (f + 0.5f) * ((y1 - y0) / floors);
                    for (int c = 0; c < cols; c++)
                    {
                        float cx = (c - (cols - 1) * 0.5f) * (span - 0.8f) / cols;
                        bool on = Hash01(side * 31 + seed, f, c) < lit;
                        // lit rooms come in three brightnesses, so a facade is not one flat sheet of light
                        float room = Hash01(seed + 77, f * 13 + side, c);
                        if (on) mb.Window().Col(room < 0.4f ? Pal.Hex("#A8CCE4") : room < 0.75f ? Pal.Hex("#7FA6C4") : Pal.Hex("#5C7E9C"));
                        else mb.Emit().Col(Pal.GlassDark);
                        Vector3 center = baseCenter + normal * depth + along * cx + Vector3.up * cy;
                        Vector3 hx = along * (winW * 0.5f), hy = Vector3.up * (winH * 0.5f);
                        mb.Quad(center - hx - hy, center + hx - hy, center + hx + hy, center - hx + hy, normal);
                    }
                }
            }
            mb.Emit();
        }

        /// <summary>
        /// What a building has at the level people walk past it: a darker base, a band of shop glass
        /// with a doorway on each street side, and awnings in colours of its own.
        /// </summary>
        static void StreetLevel(MeshBuilder mb, float w, float d, Color wall, int seed)
        {
            const float top = 4.2f;
            mb.Emit().Col(Pal.Dark(wall, 0.45f)).Box(V(0, top * 0.5f - 0.75f, 0), V(w + 0.3f, top + 1.5f, d + 0.3f));
            mb.Col(Pal.Dark(wall, 0.2f)).Box(V(0, top + 0.15f, 0), V(w + 0.7f, 0.3f, d + 0.7f));
            Color[] cloth = { Pal.Red, Pal.Hex("#2F6F4F"), Pal.Hex("#2A4E86"), Pal.Hex("#C98A2B"), Pal.Hex("#6A3A6E") };
            for (int side = 0; side < 4; side++)
            {
                float span = side % 2 == 0 ? w : d, depth = (side % 2 == 0 ? d : w) * 0.5f + 0.17f;
                Vector3 normal = side == 0 ? Vector3.forward : side == 1 ? Vector3.right : side == 2 ? Vector3.back : Vector3.left;
                Vector3 along = side % 2 == 0 ? Vector3.right : Vector3.forward;
                int bays = Mathf.Max(1, Mathf.FloorToInt((span - 1f) / 4.5f));
                float bay = (span - 1f) / bays;
                for (int k = 0; k < bays; k++)
                {
                    Vector3 mid = normal * depth + along * ((k - (bays - 1) * 0.5f) * bay);
                    Vector3 hx = along * (bay * 0.42f);
                    bool door = Hash01(seed + side, k, 3) < 0.4f;
                    if (door)
                    {
                        mb.Emit().Col(Pal.WoodDark).Quad(mid - along * 0.7f, mid + along * 0.7f, mid + along * 0.7f + Vector3.up * 2.4f, mid - along * 0.7f + Vector3.up * 2.4f, normal);
                        mb.Window().Col(Pal.Cream).Quad(mid - along * 0.7f + Vector3.up * 2.5f, mid + along * 0.7f + Vector3.up * 2.5f, mid + along * 0.7f + Vector3.up * 3.1f, mid - along * 0.7f + Vector3.up * 3.1f, normal);
                    }
                    else
                    {
                        mb.Window().Col(Pal.Hex("#9CC4DE")).Quad(mid - hx + Vector3.up * 0.7f, mid + hx + Vector3.up * 0.7f, mid + hx + Vector3.up * 3f, mid - hx + Vector3.up * 3f, normal);
                        if (Hash01(seed + 9, side, k) < 0.6f)
                        {
                            mb.Emit().Col(cloth[(int)(Hash01(seed, side * 7 + k, 5) * cloth.Length) % cloth.Length]);
                            mb.QuadTwoSided(mid - hx + Vector3.up * 3.5f, mid + hx + Vector3.up * 3.5f, mid + hx + Vector3.up * 3.05f + normal * 1.2f, mid - hx + Vector3.up * 3.05f + normal * 1.2f);
                        }
                    }
                }
            }
            mb.Emit();
        }

        /// <summary>What stands on a flat roof: a parapet, air handlers, and by its seed a water tank, a mast or a stair head.</summary>
        static void Rooftop(MeshBuilder mb, float w, float d, float y, Color wall, int seed)
        {
            Color trim = Pal.Dark(wall, 0.3f);
            mb.Emit().Col(trim);
            mb.Box(V(0, y + 0.3f, d * 0.5f - 0.15f), V(w, 0.6f, 0.3f)).Box(V(0, y + 0.3f, -d * 0.5f + 0.15f), V(w, 0.6f, 0.3f));
            mb.Box(V(w * 0.5f - 0.15f, y + 0.3f, 0), V(0.3f, 0.6f, d)).Box(V(-w * 0.5f + 0.15f, y + 0.3f, 0), V(0.3f, 0.6f, d));
            mb.Col(Pal.Metal);
            for (int k = 0; k < 3; k++)
            {
                float x = (Hash01(seed, k, 1) - 0.5f) * (w - 4f), z = (Hash01(seed, k, 2) - 0.5f) * (d - 4f);
                mb.BoxOn(V(x, y, z), V(1.6f, 0.9f, 1.2f));
            }
            float kind = Hash01(seed, 4, 4);
            Vector3 at = V((Hash01(seed, 5, 1) - 0.5f) * (w - 5f), y, (Hash01(seed, 5, 2) - 0.5f) * (d - 5f));
            if (kind < 0.4f)
            {
                // a water tank on legs
                mb.Col(Pal.MetalDark);
                foreach (float x in new[] { -0.8f, 0.8f }) foreach (float z in new[] { -0.8f, 0.8f }) mb.BoxOn(at + V(x, 0, z), V(0.12f, 1.6f, 0.12f));
                mb.Col(Pal.Wood).Cyl(at + V(0, 1.6f, 0), 1.25f, 1.25f, 2.2f, 10);
                mb.Col(Pal.WoodDark).Cone(at + V(0, 3.8f, 0), 1.35f, 0.8f, 10);
            }
            else if (kind < 0.7f)
            {
                mb.Col(Pal.MetalDark).Cyl(at, 0.12f, 0.04f, 7f, 5);
                mb.Emit(1f).Col(Pal.Red).Sphere(at + V(0, 7.1f, 0), 0.18f, 0);
                mb.Emit();
            }
            else mb.Col(trim).BoxOn(at, V(3f, 2.6f, 3f));
            mb.Emit();
        }

        /// <summary>Thin courses standing proud of the wall every few floors, and piers up the corners: what stops a tower being a box.</summary>
        static void Courses(MeshBuilder mb, float w, float d, float y0, float y1, Color wall, int seed)
        {
            mb.Emit().Col(Pal.Dark(wall, 0.18f));
            float every = 3.4f * (3 + seed % 3);
            for (float y = y0 + every; y < y1 - 2f; y += every) mb.Box(V(0, y, 0), V(w + 0.36f, 0.3f, d + 0.36f));
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                mb.Box(V(x * w * 0.5f, (y0 + y1) * 0.5f, z * d * 0.5f), V(0.7f, y1 - y0, 0.7f));
        }

        static void Tower(MeshBuilder mb, float w, float d, float h, Color wall, int seed, bool setbacks)
        {
            mb.Shade(0.03f).Grad(Pal.Dark(wall, 0.2f), wall, 0f, h);
            if (!setbacks)
            {
                mb.Box(V(0, h * 0.5f - 1.5f, 0), V(w, h + 3f, d));
                Windows(mb, Vector3.zero, w, d, 4.8f, h - 1.2f, 3.4f, 3f, seed);
                Courses(mb, w, d, 4.4f, h, wall, seed);
                StreetLevel(mb, w, d, wall, seed);
                Rooftop(mb, w, d, h, wall, seed);
            }
            else
            {
                float h1 = h * 0.55f, h2 = h * 0.85f;
                mb.Box(V(0, h1 * 0.5f - 1.5f, 0), V(w, h1 + 3f, d));
                Windows(mb, Vector3.zero, w, d, 4.8f, h1 - 0.8f, 3.4f, 3f, seed);
                Courses(mb, w, d, 4.4f, h1, wall, seed);
                StreetLevel(mb, w, d, wall, seed);
                mb.Shade(0.03f);
                mb.Grad(Pal.Dark(wall, 0.1f), wall, h1, h);
                mb.BoxOn(V(0, h1, 0), V(w * 0.74f, h2 - h1, d * 0.74f));
                Windows(mb, V(0, 0, 0), w * 0.74f, d * 0.74f, h1 + 0.8f, h2 - 0.6f, 3.4f, 3f, seed + 5);
                mb.Grad(Pal.Dark(wall, 0.1f), wall, h1, h);
                mb.BoxOn(V(0, h2, 0), V(w * 0.46f, h - h2, d * 0.46f));
                Windows(mb, V(0, 0, 0), w * 0.46f, d * 0.46f, h2 + 0.6f, h - 0.5f, 3.4f, 3f, seed + 9);
                mb.Col(Pal.MetalDark).Cyl(V(0, h, 0), 0.25f, 0.06f, h * 0.16f, 5);
                mb.Emit(1f).Col(Pal.Red).Sphere(V(0, h * 1.16f, 0), 0.35f, 0);
                mb.Emit().Glow(V(0, h * 1.16f, 0), 2.5f, new Color(1f, 0.15f, 0.1f) * 0.8f, true);
            }
        }

        static void Wheel(MeshBuilder mb, Vector3 center, float radius, float width)
        {
            mb.Col(Pal.Rubber).Rod(center - V(width * 0.5f, 0, 0), center + V(width * 0.5f, 0, 0), radius, radius, 8, true);
            mb.Col(Pal.Metal).Rod(center - V(width * 0.52f, 0, 0), center + V(width * 0.52f, 0, 0), radius * 0.5f, radius * 0.5f, 6, true);
        }

        static partial void RegisterCity()
        {
            // ---- buildings (white-ish walls, so a tint recolours them)
            Add("tower", "buildings", 44f, "Tall office tower with rows of windows", "building,city,skyscraper,urban,downtown", "office tower,high rise,highrise",
                mb => Tower(mb, 13f, 13f, 44f, Pal.Hex("#C9CED6"), 3, false));
            Add("skyscraper", "buildings", 78f, "Very tall stepped skyscraper with a spire", "building,city,urban,downtown,landmark", "tall building,super tall,empire state",
                mb => Tower(mb, 18f, 18f, 68f, Pal.Hex("#BFC6D2"), 7, true));
            Add("office_block", "buildings", 26f, "Wide mid-rise office building", "building,city,urban,office", "office,office building,mid rise",
                mb => Tower(mb, 22f, 15f, 26f, Pal.Hex("#D2CFC6"), 11, false));
            Add("apartment_block", "buildings", 20f, "Brick apartment building", "building,city,urban,housing,flats", "apartments,flats,tenement,condo",
                mb => Tower(mb, 14f, 12f, 20f, Pal.Hex("#D8B8A2"), 17, false));
            Add("shop", "buildings", 5f, "Small street-level shop with an awning", "building,city,store,street,market", "store,storefront,cafe,bakery,market stall",
                mb =>
                {
                    mb.Shade(0.03f).Col(Pal.Plaster).Box(V(0, 2.5f - 0.75f, 0), V(8f, 6.5f, 7f));
                    mb.Col(Pal.Dark(Pal.Plaster, 0.25f)).BoxOn(V(0, 5f, 0), V(8.4f, 0.4f, 7.4f));
                    mb.Window().Col(Pal.Hex("#9CC4DE")).Quad(V(-3.2f, 0.6f, 3.53f), V(-0.6f, 0.6f, 3.53f), V(-0.6f, 2.7f, 3.53f), V(-3.2f, 2.7f, 3.53f), Vector3.forward);
                    mb.Quad(V(0.9f, 0.6f, 3.53f), V(3.2f, 0.6f, 3.53f), V(3.2f, 2.7f, 3.53f), V(0.9f, 2.7f, 3.53f), Vector3.forward);
                    mb.Emit().Col(Pal.WoodDark).Quad(V(-0.45f, 0f, 3.53f), V(0.75f, 0f, 3.53f), V(0.75f, 2.5f, 3.53f), V(-0.45f, 2.5f, 3.53f), Vector3.forward);
                    mb.Col(Pal.Red);
                    mb.QuadTwoSided(V(-3.8f, 3.3f, 3.5f), V(3.8f, 3.3f, 3.5f), V(3.8f, 2.8f, 4.9f), V(-3.8f, 2.8f, 4.9f));
                    mb.Emit(0.6f).Col(Pal.Cream).Box(V(0, 4.1f, 3.56f), V(4.6f, 0.8f, 0.1f));
                    mb.Emit();
                });
            Add("house", "buildings", 6.5f, "Family house with a pitched roof and chimney", "building,home,village,suburb,cottage", "home,cottage,suburban house,village house",
                mb =>
                {
                    mb.Shade(0.03f).Col(Pal.Plaster).Box(V(0, 2f - 0.75f, 0), V(8f, 5.5f, 7f));
                    // gable roof: a triangular prism along X
                    mb.Col(Pal.Roof);
                    Vector3 a = V(-4.4f, 4f, -3.9f), b = V(4.4f, 4f, -3.9f), c = V(4.4f, 4f, 3.9f), d = V(-4.4f, 4f, 3.9f), e = V(-4.4f, 6.5f, 0), f = V(4.4f, 6.5f, 0);
                    mb.Quad(a, b, f, e, V(0, 1, -1)); mb.Quad(d, c, f, e, V(0, 1, 1));
                    mb.Col(Pal.Plaster).Tri(a, d, e, Vector3.left); mb.Tri(b, c, f, Vector3.right);
                    mb.Col(Pal.Brick).BoxOn(V(2.2f, 4.6f, -1.2f), V(0.8f, 2.4f, 0.8f));
                    mb.Col(Pal.WoodDark).Quad(V(-0.55f, 0f, 3.53f), V(0.55f, 0f, 3.53f), V(0.55f, 2.2f, 3.53f), V(-0.55f, 2.2f, 3.53f), Vector3.forward);
                    mb.Window().Col(Pal.Hex("#9CC4DE"));
                    mb.Quad(V(-3.1f, 1.2f, 3.53f), V(-1.6f, 1.2f, 3.53f), V(-1.6f, 2.6f, 3.53f), V(-3.1f, 2.6f, 3.53f), Vector3.forward);
                    mb.Quad(V(1.6f, 1.2f, 3.53f), V(3.1f, 1.2f, 3.53f), V(3.1f, 2.6f, 3.53f), V(1.6f, 2.6f, 3.53f), Vector3.forward);
                    mb.Quad(V(4.03f, 1.2f, -1.5f), V(4.03f, 1.2f, 0.5f), V(4.03f, 2.6f, 0.5f), V(4.03f, 2.6f, -1.5f), Vector3.right);
                    mb.Quad(V(-4.03f, 1.2f, -1.5f), V(-4.03f, 1.2f, 0.5f), V(-4.03f, 2.6f, 0.5f), V(-4.03f, 2.6f, -1.5f), Vector3.left);
                    mb.Emit();
                });

            // ---- street furniture
            Add("street_lamp", "city", 5.2f, "Street lamp that glows at night", "light,city,street,night,road", "lamp post,streetlight,lamppost,light pole",
                mb =>
                {
                    mb.Col(Pal.MetalDark).Cyl(Vector3.zero, 0.12f, 0.07f, 5f, 6);
                    mb.Rod(V(0, 4.9f, 0), V(0, 5.1f, 1.1f), 0.05f, 0.05f, 5);
                    mb.Box(V(0, 5.08f, 1.25f), V(0.35f, 0.12f, 0.6f));
                    mb.Window().Col(Pal.Cream).Box(V(0, 5.0f, 1.25f), V(0.28f, 0.06f, 0.5f));
                    mb.Emit().Glow(V(0, 4.95f, 1.25f), 1.5f, new Color(1f, 0.82f, 0.5f) * 0.9f, true);
                });
            Add("traffic_light", "city", 4f, "Traffic light on a pole", "city,street,road,signal", "stoplight,traffic signal,signal",
                mb =>
                {
                    mb.Col(Pal.MetalDark).Cyl(Vector3.zero, 0.09f, 0.07f, 3.2f, 6);
                    mb.Col(Pal.Black).Box(V(0, 3.6f, 0), V(0.34f, 0.95f, 0.3f));
                    mb.Emit(1f).Col(Pal.Red).Sphere(V(0, 3.9f, 0.16f), 0.1f, 0);
                    mb.Emit(0.15f).Col(Pal.Yellow).Sphere(V(0, 3.6f, 0.16f), 0.1f, 0);
                    mb.Col(Pal.Green).Sphere(V(0, 3.3f, 0.16f), 0.1f, 0);
                    mb.Emit();
                });
            Add("bench", "city", 0.9f, "Wooden park bench", "seat,park,street,garden", "park bench,seat",
                mb =>
                {
                    mb.Shade(0.05f).Col(Pal.Wood);
                    for (int i = 0; i < 3; i++) mb.Box(V(0, 0.45f, -0.18f + i * 0.18f), V(1.7f, 0.05f, 0.15f));
                    for (int i = 0; i < 2; i++) mb.Box(V(0, 0.68f + i * 0.2f, -0.3f), V(1.7f, 0.15f, 0.05f));
                    mb.Col(Pal.MetalDark);
                    foreach (float x in new[] { -0.75f, 0.75f }) { mb.Box(V(x, 0.22f, 0), V(0.06f, 0.45f, 0.5f)); mb.Box(V(x, 0.68f, -0.3f), V(0.06f, 0.5f, 0.06f)); }
                });
            Add("fire_hydrant", "city", 0.8f, "Red fire hydrant", "city,street,red", "hydrant",
                mb =>
                {
                    mb.Col(Pal.Red).Lathe(Vector3.zero, Profile(0f, 0f, 0.17f, 0f, 0.17f, 0.08f, 0.12f, 0.1f, 0.12f, 0.58f, 0.15f, 0.6f, 0.1f, 0.74f, 0f, 0.8f), 8);
                    mb.Rod(V(-0.2f, 0.42f, 0), V(0.2f, 0.42f, 0), 0.055f, 0.055f, 6);
                }).Solid(30f, 0.1f);
            Add("trash_can", "city", 1f, "Metal street bin", "city,street,bin,garbage", "bin,garbage can,rubbish bin,dustbin",
                mb => { mb.Shade(0.05f).Col(Pal.MetalDark).Cyl(Vector3.zero, 0.26f, 0.32f, 0.9f, 8); mb.Col(Pal.Metal).Cyl(V(0, 0.9f, 0), 0.34f, 0.3f, 0.08f, 8); }).Solid(8f, 0.15f);
            Add("road_cone", "city", 0.7f, "Orange traffic cone", "road,street,construction,orange", "traffic cone,cone,pylon",
                mb =>
                {
                    mb.Col(Pal.Black).BoxOn(Vector3.zero, V(0.4f, 0.04f, 0.4f));
                    mb.Col(Pal.Orange).Cyl(V(0, 0.04f, 0), 0.15f, 0.03f, 0.64f, 8);
                    mb.Col(Pal.White).Cyl(V(0, 0.3f, 0), 0.096f, 0.076f, 0.1f, 8, false);
                }).Solid(1.5f, 0.3f);
            Add("stop_sign", "city", 2.4f, "Red stop sign on a post", "road,street,sign", "sign,road sign,street sign",
                mb =>
                {
                    mb.Col(Pal.Metal).Cyl(Vector3.zero, 0.035f, 0.035f, 2.1f, 5);
                    mb.Col(Pal.Red).Push(V(0, 2.1f, 0.04f), V(90f, 22.5f, 0)).Cyl(V(0, -0.02f, 0), 0.34f, 0.34f, 0.04f, 8).Pop();
                    mb.Col(Pal.White).Box(V(0, 2.1f, 0.065f), V(0.42f, 0.1f, 0.01f));
                });
            Add("fence", "city", 1.1f, "Section of wooden picket fence, 3 m long", "garden,farm,yard,wood,boundary", "picket fence,railing,fencing",
                mb =>
                {
                    mb.Shade(0.05f).Col(Pal.OffWhite);
                    for (int i = 0; i < 9; i++) { float x = -1.4f + i * 0.35f; mb.BoxOn(V(x, 0, 0), V(0.12f, 1f, 0.04f)); mb.Pyramid(V(x, 1f, 0), 0.12f, 0.12f); }
                    mb.Box(V(0, 0.35f, -0.04f), V(3f, 0.08f, 0.04f)); mb.Box(V(0, 0.8f, -0.04f), V(3f, 0.08f, 0.04f));
                });

            // ---- the street itself
            Add("road", "city", 0.05f, "Stretch of road 8 m wide and 12 m long, with kerbs and a dashed centre line. Lay a street with `repeat` and `step` [0,0,12]", "street,city,asphalt,lane,highway,avenue", "street,road section,highway,avenue,lane",
                mb =>
                {
                    mb.Col(Pal.Hex("#2E3034")).Box(V(0, 0.02f, 0), V(8f, 0.04f, 12f));
                    mb.Col(Pal.Hex("#9A9A94")).Box(V(-4.3f, 0.07f, 0), V(0.6f, 0.14f, 12f)).Box(V(4.3f, 0.07f, 0), V(0.6f, 0.14f, 12f));
                    mb.Col(Pal.Hex("#E8E2C0"));
                    for (int i = 0; i < 3; i++) mb.Box(V(0, 0.045f, -4f + i * 4f), V(0.16f, 0.01f, 2f));
                    mb.Col(Pal.White).Box(V(-3.7f, 0.045f, 0), V(0.1f, 0.01f, 12f)).Box(V(3.7f, 0.045f, 0), V(0.1f, 0.01f, 12f));
                });
            Add("crossroads", "city", 0.05f, "Road junction 8 m square with zebra crossings on all four sides: joins `road`s", "street,city,junction,intersection,crossing", "intersection,junction,crossing,zebra crossing",
                mb =>
                {
                    mb.Col(Pal.Hex("#2E3034")).Box(V(0, 0.02f, 0), V(8f, 0.04f, 8f));
                    mb.Col(Pal.White);
                    for (int side = 0; side < 4; side++)
                    {
                        mb.Push(Vector3.zero, V(0, side * 90f, 0));
                        for (int i = 0; i < 6; i++) mb.Box(V(-3.1f + i * 1.24f, 0.045f, 3.3f), V(0.6f, 0.01f, 1.2f));
                        mb.Pop();
                    }
                });
            Add("bus_stop", "city", 2.6f, "Bus shelter with a bench and a lit sign", "city,street,shelter,transit,stop", "bus shelter,shelter,tram stop",
                mb =>
                {
                    mb.Shade(0.03f).Col(Pal.MetalDark);
                    foreach (float x in new[] { -1.6f, 1.6f }) mb.BoxOn(V(x, 0, -0.6f), V(0.08f, 2.4f, 0.08f)).BoxOn(V(x, 0, 0.6f), V(0.08f, 2.4f, 0.08f));
                    mb.Box(V(0, 2.45f, 0), V(3.5f, 0.1f, 1.5f));
                    mb.Col(Pal.GlassDark).Box(V(0, 1.3f, -0.62f), V(3.2f, 1.9f, 0.04f));
                    mb.Col(Pal.Wood).Box(V(0, 0.45f, -0.3f), V(2.4f, 0.06f, 0.4f));
                    mb.Col(Pal.MetalDark).BoxOn(V(-1f, 0, -0.3f), V(0.06f, 0.45f, 0.3f)).BoxOn(V(1f, 0, -0.3f), V(0.06f, 0.45f, 0.3f));
                    mb.Window().Col(Pal.Hex("#7FC8FF")).Box(V(1.62f, 1.5f, 0), V(0.06f, 1.5f, 1f));
                    mb.Emit();
                });
            Add("mailbox", "city", 1.2f, "Blue street mailbox", "city,street,post,letters", "postbox,post box,letterbox",
                mb =>
                {
                    mb.Col(Pal.MetalDark);
                    foreach (float x in new[] { -0.2f, 0.2f }) foreach (float z in new[] { -0.2f, 0.2f }) mb.BoxOn(V(x, 0, z), V(0.05f, 0.2f, 0.05f));
                    mb.Shade(0.04f).Col(Pal.Hex("#2A4E86")).BoxOn(V(0, 0.2f, 0), V(0.55f, 0.75f, 0.5f));
                    mb.Push(V(0, 0.95f, 0), V(0, 0, 90f)).Cyl(V(0, -0.275f, 0), 0.25f, 0.25f, 0.55f, 10).Pop();
                    mb.Col(Pal.Black).Box(V(0, 0.9f, 0.255f), V(0.36f, 0.04f, 0.01f));
                }).Solid(25f, 0.1f);
            Add("billboard", "city", 9f, "Big lit advertising board on a steel post: tint it any colour", "city,street,sign,advert,roadside,highway", "hoarding,advertising board,ad board",
                mb =>
                {
                    mb.Col(Pal.MetalDark).Cyl(Vector3.zero, 0.3f, 0.22f, 6f, 8);
                    mb.Box(V(0, 7.5f, -0.15f), V(8.4f, 3.4f, 0.2f));
                    mb.Window().Col(Pal.White).Box(V(0, 7.5f, 0f), V(8f, 3f, 0.08f));
                    mb.Emit().Col(Pal.MetalDark).Box(V(0, 5.7f, 0.5f), V(8.4f, 0.06f, 1f));
                    foreach (float x in new[] { -3f, 0f, 3f }) mb.Emit(1f).Col(Pal.Cream).Box(V(x, 5.75f, 0.9f), V(0.3f, 0.1f, 0.2f));
                    mb.Emit();
                });

            // ---- vehicles (white body, so a tint paints the car)
            Add("car", "vehicles", 1.5f, "Compact car with headlights", "vehicle,city,road,street,traffic", "automobile,sedan,taxi,vehicle,cars",
                mb =>
                {
                    mb.Shade(0.03f).Col(Pal.White);
                    mb.Box(V(0, 0.55f, 0), V(1.8f, 0.6f, 4.2f));
                    mb.Prism(new List<Vector2> { new Vector2(-0.85f, -1.3f), new Vector2(0.85f, -1.3f), new Vector2(0.85f, 0.9f), new Vector2(-0.85f, 0.9f) }, 0.85f, 1.4f);
                    mb.Col(Pal.GlassDark);
                    mb.Quad(V(-0.8f, 0.9f, 0.92f), V(0.8f, 0.9f, 0.92f), V(0.75f, 1.36f, 0.86f), V(-0.75f, 1.36f, 0.86f), Vector3.forward);
                    mb.Quad(V(-0.8f, 0.9f, -1.32f), V(0.8f, 0.9f, -1.32f), V(0.75f, 1.36f, -1.26f), V(-0.75f, 1.36f, -1.26f), Vector3.back);
                    mb.Quad(V(0.87f, 0.9f, -1.2f), V(0.87f, 0.9f, 0.8f), V(0.87f, 1.33f, 0.75f), V(0.87f, 1.33f, -1.15f), Vector3.right);
                    mb.Quad(V(-0.87f, 0.9f, -1.2f), V(-0.87f, 0.9f, 0.8f), V(-0.87f, 1.33f, 0.75f), V(-0.87f, 1.33f, -1.15f), Vector3.left);
                    foreach (float x in new[] { -0.9f, 0.9f }) foreach (float z in new[] { -1.3f, 1.35f }) Wheel(mb, V(x, 0.32f, z), 0.32f, 0.24f);
                    mb.Window().Col(Pal.Cream).Box(V(-0.6f, 0.62f, 2.11f), V(0.32f, 0.16f, 0.04f)).Box(V(0.6f, 0.62f, 2.11f), V(0.32f, 0.16f, 0.04f));
                    mb.Emit(0.5f).Col(Pal.Red).Box(V(-0.6f, 0.62f, -2.11f), V(0.32f, 0.14f, 0.04f)).Box(V(0.6f, 0.62f, -2.11f), V(0.32f, 0.14f, 0.04f));
                    mb.Emit().Glow(V(-0.6f, 0.62f, 2.2f), 0.7f, new Color(1f, 0.95f, 0.75f) * 0.7f, true).Glow(V(0.6f, 0.62f, 2.2f), 0.7f, new Color(1f, 0.95f, 0.75f) * 0.7f, true);
                });
            Add("bus", "vehicles", 3.1f, "City bus", "vehicle,city,road,transport", "coach,school bus,transit",
                mb =>
                {
                    mb.Shade(0.03f).Col(Pal.White).Box(V(0, 1.75f, 0), V(2.5f, 2.7f, 10f));
                    mb.Col(Pal.GlassDark);
                    foreach (float s in new[] { -1f, 1f })
                        mb.Quad(V(1.27f * s, 1.7f, -4.4f), V(1.27f * s, 1.7f, 4.4f), V(1.27f * s, 2.7f, 4.4f), V(1.27f * s, 2.7f, -4.4f), V(s, 0, 0));
                    mb.Quad(V(-1.1f, 1.5f, 5.02f), V(1.1f, 1.5f, 5.02f), V(1.1f, 2.8f, 5.02f), V(-1.1f, 2.8f, 5.02f), Vector3.forward);
                    foreach (float x in new[] { -1.2f, 1.2f }) foreach (float z in new[] { -3.2f, 3.4f }) Wheel(mb, V(x, 0.48f, z), 0.48f, 0.3f);
                    mb.Window().Col(Pal.Cream).Box(V(-0.85f, 0.8f, 5.02f), V(0.4f, 0.2f, 0.04f)).Box(V(0.85f, 0.8f, 5.02f), V(0.4f, 0.2f, 0.04f));
                    mb.Emit();
                });

            // ---- golf and flags
            Add("flag", "props", 4f, "Flag flying on a tall pole", "pole,banner,marker", "flagpole,banner,pennant",
                mb =>
                {
                    mb.Col(Pal.Metal).Cyl(Vector3.zero, 0.05f, 0.035f, 4f, 6);
                    mb.Col(Pal.Gold).Sphere(V(0, 4.05f, 0), 0.07f, 0);
                    mb.Col(Pal.White).Sway(-6f, 10f);
                    mb.QuadTwoSided(V(0.04f, 3.0f, 0), V(1.5f, 3.05f, 0.1f), V(1.5f, 3.9f, 0.05f), V(0.04f, 3.95f, 0));
                });
            Add("golf_flag", "sports", 2.2f, "Golf pin with a red flag, marks the hole", "golf,hole,green,pin", "pin,golf pin,hole flag",
                mb =>
                {
                    mb.Col(Pal.OffWhite).Cyl(Vector3.zero, 0.018f, 0.014f, 2.2f, 5);
                    mb.Col(Pal.Red).Sway(-4f, 6f);
                    mb.TriTwoSided(V(0.015f, 2.18f, 0), V(0.015f, 1.72f, 0), V(0.62f, 1.95f, 0.05f));
                });
            Add("golf_cart", "vehicles", 1.8f, "Golf buggy with a canopy", "golf,vehicle,buggy", "golf buggy,buggy,cart",
                mb =>
                {
                    mb.Shade(0.03f).Col(Pal.White).Box(V(0, 0.5f, 0), V(1.2f, 0.4f, 2.3f));
                    mb.Box(V(0, 0.75f, 0.85f), V(1.15f, 0.25f, 0.5f));
                    mb.Col(Pal.Cream).Box(V(0, 0.82f, -0.25f), V(1.1f, 0.12f, 0.5f)).Box(V(0, 1.1f, -0.52f), V(1.1f, 0.5f, 0.1f));
                    mb.Col(Pal.White).Box(V(0, 1.8f, -0.1f), V(1.3f, 0.06f, 1.9f));
                    mb.Col(Pal.MetalDark);
                    foreach (float x in new[] { -0.58f, 0.58f }) { mb.Rod(V(x, 0.7f, 0.75f), V(x, 1.78f, 0.7f), 0.025f, 0.025f, 4); mb.Rod(V(x, 0.7f, -0.9f), V(x, 1.78f, -0.95f), 0.025f, 0.025f, 4); }
                    foreach (float x in new[] { -0.62f, 0.62f }) foreach (float z in new[] { -0.8f, 0.85f }) Wheel(mb, V(x, 0.22f, z), 0.22f, 0.16f);
                });
        }
    }
}
