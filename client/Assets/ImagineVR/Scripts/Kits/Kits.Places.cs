using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    public static partial class Library
    {
        static float DistToPath(IList<Vector2> path, float x, float z)
        {
            var p = new Vector2(x, z);
            float best = float.MaxValue;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                Vector2 a = path[i], b = path[i + 1], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        static partial void RegisterKits()
        {
            Kit("golf_course", "A golf hole: tee at your feet, mown fairway, green with a flag and a real hole, bunkers, trees",
                "golf,sport,course,fairway,green,links", "golf,golf hole,links,country club,driving range", GolfCourse)
                .Defaults(sky: "clear_noon", terrain: "rolling", material: "grass", height: 0.25f, ambience: "birds_breeze")
                .Greet("Welcome to the first tee. The green is straight ahead.");

            Kit("city", "A downtown street grid: you stand at a crossroads between blocks of towers, with lamps, cars and lit windows at night",
                "urban,downtown,street,buildings,skyscrapers,metropolis", "city,town,downtown,metropolis,new york,tokyo,urban,streets", City)
                .Defaults(sky: "night_city", terrain: "flat", material: "asphalt", ambience: "city_night")
                .Greet("The city never sleeps. Look up.");

            RegisterMoreKits();
        }

        static partial void RegisterMoreKits();

        // ------------------------------------------------------------------ golf

        static void GolfCourse(KitContext k)
        {
            float length = Mathf.Lerp(46f, 88f, k.Size);
            float bend = k.Range(-10f, 10f);
            var fairway = new List<Vector2>
            {
                new Vector2(0f, 3f), new Vector2(bend * 0.4f, length * 0.35f), new Vector2(bend, length * 0.7f), new Vector2(bend * 0.8f, length),
            };
            Vector2 green = fairway[fairway.Count - 1];
            Color mown = Pal.Hex("#74C25A"), putting = Pal.Hex("#93DA6E"), sand = Pal.Hex("#EAD9A6");

            k.Ribbon(fairway, 17f, mown, 0.05f, 0f, "fairway");
            k.Patch(0f, 2.2f, 3.4f, 2.4f, Pal.Hex("#5FB04E"), 0.07f, 0.04f, 0f, 16, "tee");
            k.Patch(green.x, green.y, 10f, 9f, putting, 0.09f, 0.1f, 0f, 26, "green");

            // the hole: a dark disc, a pin, and a zone that notices a ball dropping in
            float hx = green.x + k.Range(-3f, 3f), hz = green.y + k.Range(-2.5f, 2.5f);
            k.Patch(hx, hz, 0.16f, 0.16f, Pal.Black, 0.11f, 0f, 0f, 10, "hole");
            k.Put("golf_flag", hx, hz, 0f);
            k.Zone(new Vector3(hx, k.Ground(hx, hz) + 0.05f, hz), 0.4f, "golf_ball", "In the hole! Lovely shot.");

            // bunkers guard the green and pinch the fairway
            int bunkers = k.N(3);
            for (int i = 0; i < bunkers; i++)
            {
                float t = k.Range(0.45f, 1f);
                Vector2 on = Vector2.Lerp(fairway[1], green, t);
                float side = k.Chance(0.5f) ? 1f : -1f;
                float bx = on.x + side * k.Range(10f, 15f), bz = on.y + k.Range(-5f, 5f);
                k.Patch(bx, bz, k.Range(3f, 5f), k.Range(2.4f, 3.6f), sand, 0.07f, 0.2f, 0f, 16, "bunker");
            }

            // tee markers
            k.Put("sphere", -1.6f, 3.2f, 0f, 0.14f, Pal.Red);
            k.Put("sphere", 1.6f, 3.2f, 0f, 0.14f, Pal.Red);

            // trees line the hole but never stand on it
            float reach = k.Reach;
            System.Func<float, float, bool> offFairway = (x, z) =>
                DistToPath(fairway, x, z) > 13f && Vector2.Distance(new Vector2(x, z), green) > 15f;
            k.Scatter("pine_tree", k.N(46), 14f, reach, 0.8f, 1.5f, null, offFairway);
            k.Scatter("oak_tree", k.N(26), 14f, reach, 0.8f, 1.3f, null, offFairway);
            k.Scatter("bush", k.N(22), 10f, reach * 0.8f, 0.7f, 1.3f, null, offFairway);
            k.Scatter("flower_patch", k.N(34), 6f, 45f, 0.8f, 1.4f, null, (x, z) => DistToPath(fairway, x, z) > 9.5f);
            k.Scatter("tall_grass", k.N(70), 9f, reach * 0.7f, 0.8f, 1.5f, null, (x, z) => DistToPath(fairway, x, z) > 10f);

            k.Put("golf_cart", 4.5f, 0.5f, 200f);
            k.Put("bench", -4.2f, 1f, 90f);
        }

        // ------------------------------------------------------------------ city

        static void City(KitContext k)
        {
            const float pitch = 44f, street = 11f;
            float block = pitch - street;
            int n = Mathf.Clamp(Mathf.CeilToInt(k.Reach / pitch), 1, 3);
            Color[] walls =
            {
                Pal.Hex("#FFFFFF"), Pal.Hex("#E6D6C4"), Pal.Hex("#C8D2E0"), Pal.Hex("#D8C0AE"), Pal.Hex("#B8C4C0"),
            };
            Color kerb = Pal.Hex("#8E9196");

            // road markings: one mesh for every dash and crossing
            var mb = new MeshBuilder();
            float extent = n * pitch + street;
            for (int i = -n; i <= n; i++)
            {
                float line = i * pitch;
                mb.Col(Pal.Hex("#E8C84A"));
                for (float t = -extent; t < extent; t += 6f)
                {
                    if (Mathf.Abs(Mathf.Repeat(t + pitch * 0.5f + 1.5f, pitch) - pitch * 0.5f) < street * 0.5f + 1f) continue;   // skip junctions
                    AddMark(k, mb, line - 0.1f, t, line + 0.1f, t + 3f);
                    AddMark(k, mb, t, line - 0.1f, t + 3f, line + 0.1f);
                }
            }
            mb.Col(Pal.Hex("#E8E8E8"));
            for (int s = 0; s < 4; s++)   // zebra crossings on the four arms of the central junction
            {
                for (int stripe = -3; stripe <= 3; stripe++)
                {
                    float o = stripe * 1.3f, d = street * 0.5f + 1.6f;
                    if (s == 0) AddMark(k, mb, o - 0.35f, d, o + 0.35f, d + 3f);
                    else if (s == 1) AddMark(k, mb, o - 0.35f, -d - 3f, o + 0.35f, -d);
                    else if (s == 2) AddMark(k, mb, d, o - 0.35f, d + 3f, o + 0.35f);
                    else AddMark(k, mb, -d - 3f, o - 0.35f, -d, o + 0.35f);
                }
            }
            k.AddOwn(mb.Build("road_markings"));

            for (int bx = -n; bx < n; bx++)
            {
                for (int bz = -n; bz < n; bz++)
                {
                    float cx = (bx + 0.5f) * pitch, cz = (bz + 0.5f) * pitch;
                    k.Put("cube", cx, cz, 0f, new Vector3(block, 0.16f, block), kerb);   // pavement slab

                    // each block holds four lots
                    for (int lx = 0; lx < 2; lx++)
                    {
                        for (int lz = 0; lz < 2; lz++)
                        {
                            if (!k.Chance(Mathf.Lerp(0.6f, 1f, k.Density))) continue;
                            float x = cx + (lx - 0.5f) * block * 0.5f, z = cz + (lz - 0.5f) * block * 0.5f;
                            float dist = Mathf.Sqrt(x * x + z * z);
                            float yaw = Mathf.Abs(x) > Mathf.Abs(z) ? (x > 0 ? 270f : 90f) : (z > 0 ? 180f : 0f);   // face the nearest street towards the centre
                            string type;
                            float roll = k.Rand();
                            if (dist < 34f) type = roll < 0.4f ? "shop" : roll < 0.8f ? "apartment_block" : "office_block";
                            else if (dist < 75f) type = roll < 0.35f ? "office_block" : roll < 0.7f ? "tower" : roll < 0.9f ? "apartment_block" : "skyscraper";
                            else type = roll < 0.5f ? "tower" : roll < 0.85f ? "skyscraper" : "office_block";
                            float fit = type == "office_block" ? 0.62f : type == "skyscraper" ? 0.72f : type == "shop" ? 1.3f : 0.95f;
                            var scale = new Vector3(fit, k.Range(0.75f, 1.35f), fit);
                            k.Put(type, x, z, yaw, scale, k.Pick(walls), 0.16f);
                        }
                    }

                    // a street tree or two on the pavement
                    if (k.Chance(0.5f * k.Density + 0.2f)) k.Put("oak_tree", cx + block * 0.46f, cz + k.Range(-10f, 10f), -1f, 0.7f, null, 0.16f);
                }
            }

            // lamps at every corner, facing the junction they light
            for (int ix = -n; ix <= n; ix++)
            {
                for (int iz = -n; iz <= n; iz++)
                {
                    float jx = ix * pitch, jz = iz * pitch;
                    if (Mathf.Sqrt(jx * jx + jz * jz) > k.Reach + 20f) continue;
                    for (int c = 0; c < 4; c++)
                    {
                        float sx = c < 2 ? 1f : -1f, sz = c % 2 == 0 ? 1f : -1f;
                        float x = jx + sx * (street * 0.5f + 0.7f), z = jz + sz * (street * 0.5f + 0.7f);
                        if (c % 2 == 0 || (ix == 0 && iz == 0)) k.Put("street_lamp", x, z, KitContext.Facing(x, z, jx, jz), 1f, null, 0.16f);
                    }
                }
            }
            k.Put("traffic_light", street * 0.5f + 0.9f, street * 0.5f + 2.2f, 180f, 1f, null, 0.16f);
            k.Put("traffic_light", -street * 0.5f - 0.9f, -street * 0.5f - 2.2f, 0f, 1f, null, 0.16f);

            // parked and passing cars, kept to their lanes
            Color[] paint = { Pal.Red, Pal.Blue, Pal.Hex("#F5D90A"), Pal.White, Pal.Hex("#2B2D31"), Pal.Teal, Pal.Hex("#C0C4C8") };
            int cars = k.N(22);
            for (int i = 0; i < cars; i++)
            {
                int lane = k.RangeInt(-n, n + 1);
                float along = k.Range(-extent + 8f, extent - 8f);
                if (Mathf.Abs(Mathf.Repeat(along + pitch * 0.5f, pitch) - pitch * 0.5f) < street * 0.5f + 3f) continue;
                bool alongZ = k.Chance(0.5f);
                float side = k.Chance(0.5f) ? 1f : -1f;
                float x = alongZ ? lane * pitch + side * 2.8f : along, z = alongZ ? along : lane * pitch + side * 2.8f;
                if (Mathf.Sqrt(x * x + z * z) < 7f) continue;
                float yaw = alongZ ? (side > 0 ? 0f : 180f) : (side > 0 ? 270f : 90f);
                k.Put(k.Chance(0.1f) ? "bus" : "car", x, z, yaw, 1f, k.Pick(paint));
            }

            // things on the pavement near the user
            k.Put("bench", street * 0.5f + 2.2f, -street * 0.5f - 6f, 270f, 1f, null, 0.16f);
            k.Put("fire_hydrant", -street * 0.5f - 1f, street * 0.5f + 5f, 0f, 1f, null, 0.16f);
            k.Put("trash_can", street * 0.5f + 1.2f, street * 0.5f + 6f, 0f, 1f, null, 0.16f);
            k.Put("stop_sign", -street * 0.5f - 1f, -street * 0.5f - 1.4f, 90f, 1f, null, 0.16f);
        }

        static void AddMark(KitContext k, MeshBuilder mb, float x0, float z0, float x1, float z1)
        {
            const float lift = 0.03f;
            var a = new Vector3(x0, k.Ground(x0, z0) + lift, z0);
            var b = new Vector3(x1, k.Ground(x1, z0) + lift, z0);
            var c = new Vector3(x1, k.Ground(x1, z1) + lift, z1);
            var d = new Vector3(x0, k.Ground(x0, z1) + lift, z1);
            mb.Quad(a, b, c, d, Vector3.up);
        }
    }
}
