using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    public static partial class Library
    {
        static void Flame(MeshBuilder mb, Vector3 at, float size)
        {
            mb.Emit(1f).Col(Pal.Fire).Lathe(at, Profile(size * 0.32f, 0f, size * 0.4f, size * 0.3f, size * 0.2f, size * 0.75f, 0f, size * 1.15f), 6);
            mb.Col(Pal.FireCore).Lathe(at, Profile(size * 0.18f, 0f, size * 0.2f, size * 0.25f, 0f, size * 0.7f), 5);
            mb.Emit().Glow(at + V(0, size * 0.5f, 0), size * 3.2f, new Color(1f, 0.55f, 0.2f) * 0.55f);
        }

        static void Legs(MeshBuilder mb, float halfX, float halfZ, float height, float thick, Color c)
        {
            mb.Col(c);
            foreach (float x in new[] { -halfX, halfX }) foreach (float z in new[] { -halfZ, halfZ }) mb.BoxOn(V(x, 0, z), V(thick, height, thick));
        }

        static partial void RegisterProps()
        {
            // ---- physics favourites: things to stack, push and knock over
            Add("crate", "props", 0.8f, "Wooden crate", "box,wood,cargo,stack,warehouse", "box,wooden box,packing crate",
                mb =>
                {
                    mb.Shade(0.06f).Col(Pal.Wood).BoxOn(Vector3.zero, V(0.8f, 0.8f, 0.8f));
                    mb.Col(Pal.WoodDark);
                    foreach (float y in new[] { 0.06f, 0.74f })
                    {
                        mb.Box(V(0, y, 0.405f), V(0.82f, 0.1f, 0.02f)).Box(V(0, y, -0.405f), V(0.82f, 0.1f, 0.02f));
                        mb.Box(V(0.405f, y, 0), V(0.02f, 0.1f, 0.82f)).Box(V(-0.405f, y, 0), V(0.02f, 0.1f, 0.82f));
                    }
                }).Solid(8f, 0.15f);
            Add("barrel", "props", 1f, "Wooden barrel", "wood,cask,pirate,tavern,stack", "cask,keg,drum barrel",
                mb =>
                {
                    mb.Shade(0.05f).Col(Pal.Wood).Lathe(Vector3.zero, Profile(0f, 0f, 0.3f, 0f, 0.37f, 0.3f, 0.38f, 0.65f, 0.3f, 0.95f, 0f, 0.95f), 10);
                    mb.Col(Pal.MetalDark).Cyl(V(0, 0.18f, 0), 0.345f, 0.36f, 0.05f, 10, false).Cyl(V(0, 0.74f, 0), 0.36f, 0.345f, 0.05f, 10, false);
                }).Solid(12f, 0.15f, ColliderShape.Capsule);
            Add("bowling_pin", "sports", 0.38f, "White bowling pin. Stand ten in a triangle and knock them down", "bowling,sport,game,knock over", "pin,skittle,ninepin",
                mb =>
                {
                    mb.Col(Pal.White).Lathe(Vector3.zero, Profile(0f, 0f, 0.05f, 0f, 0.06f, 0.1f, 0.045f, 0.22f, 0.025f, 0.29f, 0.032f, 0.34f, 0f, 0.38f), 8);
                    mb.Col(Pal.Red).Cyl(V(0, 0.26f, 0), 0.031f, 0.027f, 0.02f, 8, false);
                }).Solid(1.2f, 0.25f, ColliderShape.Capsule);
            Add("bowling_ball", "sports", 0.22f, "Heavy bowling ball", "bowling,sport,ball,heavy,roll", "",
                mb =>
                {
                    mb.Col(Pal.Hex("#2A3A8A")).Sphere(V(0, 0.11f, 0), 0.11f, 2);
                    mb.Col(Pal.Black).Sphere(V(0.03f, 0.2f, 0.03f), 0.014f, 0).Sphere(V(-0.03f, 0.2f, 0.03f), 0.014f, 0).Sphere(V(0, 0.2f, -0.03f), 0.014f, 0);
                }).Ball(6f, 0.15f);
            Add("domino", "toys", 0.3f, "Domino tile. Stand them in a row and tip the first", "game,toy,chain reaction,knock over", "dominoes,tile",
                mb =>
                {
                    mb.Col(Pal.White).BoxOn(Vector3.zero, V(0.15f, 0.3f, 0.04f));
                    mb.Col(Pal.Black).Box(V(0, 0.15f, 0.021f), V(0.13f, 0.01f, 0.002f)).Sphere(V(0, 0.225f, 0.02f), 0.014f, 0).Sphere(V(-0.03f, 0.075f, 0.02f), 0.014f, 0).Sphere(V(0.03f, 0.075f, 0.02f), 0.014f, 0);
                }).Solid(0.4f, 0.1f);
            Add("building_block", "toys", 0.3f, "Chunky toy building block", "toy,stack,build,play,kids", "block,toy block,brick,lego",
                mb =>
                {
                    mb.Col(Pal.White).BoxOn(Vector3.zero, V(0.3f, 0.3f, 0.3f));
                    mb.Cyl(V(0, 0.3f, 0), 0.08f, 0.08f, 0.04f, 8);
                }).Solid(0.5f, 0.2f);
            Add("tire", "props", 0.25f, "Rubber car tyre", "wheel,rubber,car,roll,garage", "tyre,wheel",
                mb => mb.Col(Pal.Rubber).Torus(V(0, 0.12f, 0), 0.24f, 0.12f, 14, 6)).Solid(8f, 0.6f);
            Add("trampoline", "props", 0.5f, "Round trampoline. Things that land on it bounce", "bounce,jump,play,garden,fun", "bouncy,jumping",
                mb =>
                {
                    mb.Col(Pal.Hex("#2A2C30")).Cyl(V(0, 0.42f, 0), 1.25f, 1.25f, 0.04f, 16);
                    mb.Col(Pal.Blue).Push(V(0, 0.44f, 0)).Torus(Vector3.zero, 1.32f, 0.09f, 18, 5).Pop();
                    mb.Col(Pal.Metal);
                    for (int i = 0; i < 6; i++) { float a = i / 6f * Mathf.PI * 2f; mb.Cyl(V(Mathf.Cos(a) * 1.3f, 0, Mathf.Sin(a) * 1.3f), 0.03f, 0.03f, 0.44f, 4); }
                }).Bounce = 1f;
            Add("ramp", "props", 1f, "Wooden ramp for rolling and launching things", "slope,jump,skate,roll,launch", "slope,jump ramp,skate ramp",
                mb => mb.Shade(0.05f).Col(Pal.WoodLight).Wedge(Vector3.zero, V(2f, 1f, 3f)));
            Add("seesaw", "props", 0.9f, "Playground seesaw", "playground,park,kids,play", "teeter totter,see-saw",
                mb =>
                {
                    mb.Col(Pal.Red).Lathe(Vector3.zero, Profile(0f, 0f, 0.3f, 0f, 0.05f, 0.5f, 0f, 0.5f), 4);
                    mb.Push(V(0, 0.52f, 0), V(0, 0, 10f)).Col(Pal.Yellow).Box(Vector3.zero, V(3.2f, 0.06f, 0.3f));
                    mb.Col(Pal.Blue).Box(V(1.35f, 0.2f, 0), V(0.05f, 0.34f, 0.3f)).Box(V(-1.35f, 0.2f, 0), V(0.05f, 0.34f, 0.3f)).Pop();
                });
            Add("swing_set", "props", 2.4f, "Playground swing set with two swings", "playground,park,kids,play", "swings,swing",
                mb =>
                {
                    mb.Col(Pal.Red);
                    foreach (float x in new[] { -1.6f, 1.6f }) { mb.Rod(V(x, 2.3f, 0), V(x, 0, 0.9f), 0.05f, 0.05f, 5).Rod(V(x, 2.3f, 0), V(x, 0, -0.9f), 0.05f, 0.05f, 5); }
                    mb.Rod(V(-1.7f, 2.3f, 0), V(1.7f, 2.3f, 0), 0.05f, 0.05f, 5);
                    foreach (float x in new[] { -0.7f, 0.7f })
                    {
                        mb.Col(Pal.MetalDark).Rod(V(x - 0.2f, 2.3f, 0), V(x - 0.2f, 0.55f, 0.1f), 0.01f, 0.01f, 3).Rod(V(x + 0.2f, 2.3f, 0), V(x + 0.2f, 0.55f, 0.1f), 0.01f, 0.01f, 3);
                        mb.Col(Pal.Blue).Box(V(x, 0.53f, 0.1f), V(0.5f, 0.04f, 0.2f));
                    }
                });
            Add("slide", "props", 2.2f, "Playground slide with a ladder", "playground,park,kids,play", "playground slide,chute",
                mb =>
                {
                    mb.Col(Pal.Yellow).Push(V(0, 0, 0)).Quad(V(-0.3f, 1.8f, 0), V(0.3f, 1.8f, 0), V(0.3f, 0.15f, 2.6f), V(-0.3f, 0.15f, 2.6f), V(0, 1, 0.6f)).Pop();
                    mb.QuadTwoSided(V(-0.3f, 1.8f, 0), V(-0.3f, 2.05f, 0), V(-0.3f, 0.4f, 2.6f), V(-0.3f, 0.15f, 2.6f));
                    mb.QuadTwoSided(V(0.3f, 1.8f, 0), V(0.3f, 2.05f, 0), V(0.3f, 0.4f, 2.6f), V(0.3f, 0.15f, 2.6f));
                    mb.Col(Pal.Red);
                    foreach (float x in new[] { -0.3f, 0.3f }) mb.Rod(V(x, 0, -0.7f), V(x, 2.1f, 0), 0.035f, 0.035f, 4);
                    for (int i = 1; i < 6; i++) mb.Rod(V(-0.3f, i * 0.33f, -0.7f + i * 0.11f), V(0.3f, i * 0.33f, -0.7f + i * 0.11f), 0.025f, 0.025f, 4);
                    mb.Col(Pal.Blue).Box(V(0, 1.82f, -0.05f), V(0.7f, 0.05f, 0.4f));
                });

            // ---- furniture
            Add("table", "furniture", 0.75f, "Wooden dining table", "furniture,home,room,dining", "dining table,desk",
                mb => { mb.Shade(0.04f).Col(Pal.Wood).Box(V(0, 0.72f, 0), V(1.6f, 0.06f, 0.9f)); Legs(mb, 0.7f, 0.35f, 0.7f, 0.07f, Pal.WoodDark); });
            Add("chair", "furniture", 0.95f, "Wooden chair", "furniture,home,room,seat", "seat,stool,dining chair",
                mb =>
                {
                    mb.Shade(0.04f).Col(Pal.Wood).Box(V(0, 0.45f, 0), V(0.45f, 0.05f, 0.45f)).Box(V(0, 0.75f, -0.2f), V(0.45f, 0.4f, 0.04f));
                    Legs(mb, 0.19f, 0.19f, 0.45f, 0.045f, Pal.WoodDark);
                }).Solid(4f, 0.1f);
            Add("sofa", "furniture", 0.85f, "Comfy three-seat sofa", "furniture,home,living room,seat", "couch,settee",
                mb =>
                {
                    mb.Shade(0.04f).Col(Pal.Hex("#6A7FA8")).Box(V(0, 0.28f, 0), V(2.1f, 0.3f, 0.9f)).Box(V(0, 0.62f, -0.36f), V(2.1f, 0.55f, 0.2f));
                    mb.Box(V(-0.98f, 0.45f, 0), V(0.2f, 0.4f, 0.9f)).Box(V(0.98f, 0.45f, 0), V(0.2f, 0.4f, 0.9f));
                    mb.Col(Pal.Hex("#8496BC"));
                    for (int i = -1; i <= 1; i++) mb.Box(V(i * 0.6f, 0.47f, 0.06f), V(0.56f, 0.1f, 0.62f));
                });
            Add("bed", "furniture", 0.9f, "Bed with a pillow and blanket", "furniture,home,bedroom,sleep", "mattress,cot",
                mb =>
                {
                    mb.Shade(0.04f).Col(Pal.WoodDark).Box(V(0, 0.2f, 0), V(1.5f, 0.25f, 2.1f)).Box(V(0, 0.6f, -1.04f), V(1.5f, 0.9f, 0.08f));
                    mb.Col(Pal.White).Box(V(0, 0.4f, 0), V(1.42f, 0.18f, 2.02f)).Box(V(0, 0.54f, -0.72f), V(0.8f, 0.1f, 0.4f));
                    mb.Col(Pal.Hex("#C0504A")).Box(V(0, 0.51f, 0.3f), V(1.46f, 0.08f, 1.4f));
                });
            Add("bookshelf", "furniture", 2f, "Tall bookshelf full of books", "furniture,home,library,books,study", "bookcase,shelf,shelves,library",
                mb =>
                {
                    mb.Shade(0.04f).Col(Pal.WoodDark).BoxOn(Vector3.zero, V(1.2f, 2f, 0.32f));
                    Color[] spines = { Pal.Red, Pal.Blue, Pal.Green, Pal.Yellow, Pal.Purple, Pal.Cream, Pal.Teal };
                    for (int shelf = 0; shelf < 4; shelf++)
                    {
                        mb.Col(Pal.Wood).Box(V(0, 0.12f + shelf * 0.48f, 0.02f), V(1.1f, 0.03f, 0.3f));
                        float x = -0.5f;
                        for (int b = 0; x < 0.45f; b++)
                        {
                            float w = 0.05f + Hash01(shelf, b, 3) * 0.05f, h = 0.28f + Hash01(shelf, b, 5) * 0.1f;
                            mb.Col(spines[(shelf * 3 + b) % spines.Length]).Box(V(x + w * 0.5f, 0.14f + shelf * 0.48f + h * 0.5f, 0.06f), V(w * 0.9f, h, 0.22f));
                            x += w;
                        }
                    }
                });
            Add("floor_lamp", "furniture", 1.7f, "Standing lamp with a warm shade", "furniture,home,light,room", "lamp,standing lamp,light",
                mb =>
                {
                    mb.Col(Pal.MetalDark).Cyl(Vector3.zero, 0.16f, 0.16f, 0.03f, 8).Cyl(Vector3.zero, 0.015f, 0.015f, 1.4f, 4);
                    mb.Emit(0.5f, 1f).Col(Pal.Cream).Cyl(V(0, 1.35f, 0), 0.24f, 0.15f, 0.34f, 8, false);
                    mb.Emit().Glow(V(0, 1.5f, 0), 1.3f, new Color(1f, 0.8f, 0.5f) * 0.6f, true);
                });
            Add("rug", "furniture", 0.03f, "Patterned floor rug", "furniture,home,carpet,floor,room", "carpet,mat,persian rug",
                mb =>
                {
                    mb.Col(Pal.Hex("#A8463A")).BoxOn(Vector3.zero, V(2.4f, 0.02f, 1.6f));
                    mb.Col(Pal.Cream).BoxOn(V(0, 0.005f, 0), V(2.0f, 0.02f, 1.2f));
                    mb.Col(Pal.Hex("#2E5A7A")).BoxOn(V(0, 0.01f, 0), V(1.6f, 0.02f, 0.8f));
                });
            Add("piano", "furniture", 1.2f, "Upright piano", "music,instrument,furniture,room", "upright piano,keyboard",
                mb =>
                {
                    mb.Shade(0.03f).Col(Pal.Hex("#2A2422")).BoxOn(V(0, 0, -0.1f), V(1.5f, 1.2f, 0.4f)).Box(V(0, 0.68f, 0.22f), V(1.5f, 0.08f, 0.3f));
                    mb.Col(Pal.White).Box(V(0, 0.725f, 0.26f), V(1.36f, 0.02f, 0.18f));
                    mb.Col(Pal.Black);
                    for (int i = 0; i < 16; i++) if (i % 7 != 2 && i % 7 != 6) mb.Box(V(-0.64f + i * 0.085f, 0.74f, 0.22f), V(0.04f, 0.02f, 0.1f));
                }).Does("bell", 1.2f);

            // ---- art studio
            Add("easel", "art", 1.8f, "Artist's easel holding a blank canvas", "art,painting,studio,creative,gallery", "canvas,painting stand,artist easel",
                mb =>
                {
                    mb.Shade(0.04f).Col(Pal.WoodLight);
                    mb.Rod(V(-0.4f, 0, 0.2f), V(-0.06f, 1.8f, 0), 0.025f, 0.02f, 4).Rod(V(0.4f, 0, 0.2f), V(0.06f, 1.8f, 0), 0.025f, 0.02f, 4).Rod(V(0, 0, -0.6f), V(0, 1.7f, 0), 0.025f, 0.02f, 4);
                    mb.Box(V(0, 0.75f, 0.13f), V(0.9f, 0.04f, 0.08f));
                    mb.Col(Pal.White).Push(V(0, 1.22f, 0.11f), V(-8f, 0, 0)).Box(Vector3.zero, V(0.9f, 0.9f, 0.03f)).Pop();
                });
            Add("painting", "art", 1.3f, "Framed abstract painting on a stand", "art,gallery,museum,picture,frame", "picture,artwork,framed picture,portrait",
                mb =>
                {
                    mb.Col(Pal.Gold).Box(V(0, 1.2f, 0), V(1.5f, 1.1f, 0.06f));
                    mb.Col(Pal.Cream).Box(V(0, 1.2f, 0.035f), V(1.32f, 0.92f, 0.01f));
                    Color[] paint = { Pal.Red, Pal.Blue, Pal.Yellow, Pal.Teal, Pal.Purple };
                    for (int i = 0; i < 7; i++)
                        mb.Col(paint[i % paint.Length]).Box(V(-0.45f + Hash01(i, 1, 9) * 0.9f, 0.9f + Hash01(i, 2, 9) * 0.6f, 0.042f + i * 0.0008f), V(0.15f + Hash01(i, 3, 9) * 0.35f, 0.1f + Hash01(i, 4, 9) * 0.3f, 0.004f));
                    mb.Col(Pal.MetalDark).Cyl(Vector3.zero, 0.02f, 0.02f, 0.66f, 4).Cyl(Vector3.zero, 0.2f, 0.2f, 0.02f, 8);
                });
            Add("paint_bucket", "art", 0.3f, "Open tin of paint", "art,paint,studio,colour", "paint can,paint pot,paint tin",
                mb =>
                {
                    mb.Col(Pal.Steel).Cyl(Vector3.zero, 0.13f, 0.13f, 0.26f, 10);
                    mb.Col(Pal.White).Cyl(V(0, 0.255f, 0), 0.12f, 0.12f, 0.01f, 10);
                    mb.Col(Pal.MetalDark).Push(V(0, 0.2f, 0), V(0, 0, 0), V(1f, 1f, 0.15f)).Torus(Vector3.zero, 0.14f, 0.008f, 10, 3).Pop();
                }).Solid(2f, 0.1f, ColliderShape.Capsule);
            Add("sculpture", "art", 2f, "Abstract stone sculpture on a plinth", "art,gallery,museum,statue,modern", "statue,modern art,monument,bust",
                mb =>
                {
                    mb.Shade(0.05f).Col(Pal.Concrete).BoxOn(Vector3.zero, V(0.7f, 0.5f, 0.7f));
                    mb.Col(Pal.OffWhite).Blob(V(0, 1.0f, 0), V(0.3f, 0.5f, 0.28f), 0.25f, 4, 1).Blob(V(0.12f, 1.6f, 0.05f), V(0.24f, 0.3f, 0.22f), 0.3f, 9, 1);
                    mb.Push(V(-0.05f, 1.25f, 0), V(70f, 20f, 0)).Torus(Vector3.zero, 0.36f, 0.06f, 12, 5).Pop();
                });

            // ---- camping and outdoors
            Add("campfire", "camp", 0.8f, "Crackling campfire ringed with stones", "fire,camp,night,warm,cozy", "bonfire,fire,fire pit",
                mb =>
                {
                    mb.Col(Pal.RockDark);
                    for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2f; mb.Blob(V(Mathf.Cos(a) * 0.5f, 0.07f, Mathf.Sin(a) * 0.5f), V(0.13f, 0.09f, 0.11f), 0.2f, i, 0); }
                    mb.Col(Pal.TrunkDark);
                    for (int i = 0; i < 4; i++) { float a = i / 4f * Mathf.PI * 2f + 0.4f; mb.Rod(V(Mathf.Cos(a) * 0.36f, 0.04f, Mathf.Sin(a) * 0.36f), V(-Mathf.Cos(a) * 0.08f, 0.34f, -Mathf.Sin(a) * 0.08f), 0.05f, 0.04f, 5); }
                    Flame(mb, V(0, 0.08f, 0), 0.55f);
                });
            Add("tent", "camp", 1.5f, "Small camping tent", "camp,outdoors,hike,sleep", "camping tent,teepee",
                mb =>
                {
                    Vector3 a = V(-1.1f, 0, -1.3f), b = V(1.1f, 0, -1.3f), c = V(1.1f, 0, 1.3f), d = V(-1.1f, 0, 1.3f), e = V(0, 1.5f, -1.3f), f = V(0, 1.5f, 1.3f);
                    mb.Shade(0.04f).Col(Pal.Orange).Quad(a, d, f, e, V(-1, 0.7f, 0)).Quad(b, c, f, e, V(1, 0.7f, 0));
                    mb.Col(Pal.Hex("#C8702A")).Tri(a, b, e, Vector3.back);
                    mb.Tri(d, V(-0.35f, 0, 1.3f), f, Vector3.forward); mb.Tri(c, V(0.35f, 0, 1.3f), f, Vector3.forward);
                    mb.Col(Pal.Black).Tri(V(-0.35f, 0, 1.29f), V(0.35f, 0, 1.29f), V(0, 1.45f, 1.29f), Vector3.forward);
                });
            Add("torch", "camp", 1.8f, "Standing torch with a live flame", "fire,light,medieval,tiki,night", "tiki torch,brazier,flame",
                mb =>
                {
                    mb.Col(Pal.WoodDark).Cyl(Vector3.zero, 0.04f, 0.035f, 1.5f, 5);
                    mb.Col(Pal.MetalDark).Cyl(V(0, 1.45f, 0), 0.06f, 0.11f, 0.14f, 6);
                    Flame(mb, V(0, 1.56f, 0), 0.26f);
                });
            Add("picnic_table", "camp", 0.8f, "Picnic table with benches", "park,camp,outdoors,seat", "picnic bench",
                mb =>
                {
                    mb.Shade(0.05f).Col(Pal.WoodLight).Box(V(0, 0.75f, 0), V(1.8f, 0.05f, 0.8f)).Box(V(0, 0.42f, 0.65f), V(1.8f, 0.05f, 0.28f)).Box(V(0, 0.42f, -0.65f), V(1.8f, 0.05f, 0.28f));
                    mb.Col(Pal.Wood);
                    foreach (float x in new[] { -0.7f, 0.7f }) { mb.Rod(V(x, 0.74f, 0.2f), V(x, 0, 0.75f), 0.04f, 0.04f, 4).Rod(V(x, 0.74f, -0.2f), V(x, 0, -0.75f), 0.04f, 0.04f, 4); }
                });
            Add("hay_bale", "farm", 0.9f, "Round bale of hay", "farm,field,straw,harvest", "hay,straw bale,haystack",
                mb => mb.Shade(0.08f).Col(Pal.Hex("#D8B85A")).Push(V(0, 0.55f, 0), V(0, 0, 90f)).Cyl(V(0, -0.6f, 0), 0.55f, 0.55f, 1.2f, 10).Pop()).Solid(30f, 0.1f);
            Add("pumpkin", "farm", 0.4f, "Orange pumpkin", "autumn,halloween,farm,harvest", "squash,gourd",
                mb =>
                {
                    mb.Shade(0.05f).Col(Pal.Orange);
                    for (int i = 0; i < 6; i++) { float a = i / 6f * Mathf.PI * 2f; mb.Sphere(V(Mathf.Cos(a) * 0.09f, 0.17f, Mathf.Sin(a) * 0.09f), V(0.13f, 0.17f, 0.13f), 0); }
                    mb.Col(Pal.LeafDark).Cyl(V(0, 0.3f, 0), 0.025f, 0.018f, 0.1f, 5);
                }).Hold().Solid(3f, 0.2f, ColliderShape.Sphere);
            Add("well", "farm", 2.2f, "Stone well with a little roof and bucket", "village,medieval,water,farm", "wishing well,water well",
                mb =>
                {
                    mb.Shade(0.06f).Col(Pal.Rock).Cyl(Vector3.zero, 0.75f, 0.75f, 0.8f, 10);
                    mb.Col(Pal.Black).Cyl(V(0, 0.79f, 0), 0.6f, 0.6f, 0.02f, 10);
                    mb.Col(Pal.WoodDark).BoxOn(V(-0.7f, 0, 0), V(0.1f, 1.9f, 0.1f)).BoxOn(V(0.7f, 0, 0), V(0.1f, 1.9f, 0.1f)).Rod(V(-0.7f, 1.5f, 0), V(0.7f, 1.5f, 0), 0.04f, 0.04f, 5);
                    mb.Col(Pal.Roof).Pyramid(V(0, 1.85f, 0), 2f, 0.6f);
                    mb.Col(Pal.Wood).Cyl(V(0.1f, 1.0f, 0), 0.12f, 0.14f, 0.2f, 7);
                });
            Add("windmill", "farm", 12f, "Old stone windmill with four sails", "farm,village,holland,countryside,landmark", "mill,dutch windmill",
                mb =>
                {
                    mb.Shade(0.05f).Grad(Pal.Hex("#C8BEA8"), Pal.Plaster, 0f, 8f).Cyl(Vector3.zero, 2.4f, 1.6f, 8f, 8, false);
                    mb.Col(Pal.Roof).Cone(V(0, 8f, 0), 2f, 2.2f, 8);
                    mb.Col(Pal.WoodDark).Push(V(0, 7.3f, 1.9f), V(90f, 0, 0)).Cyl(Vector3.zero, 0.2f, 0.2f, 0.5f, 6).Pop();
                    for (int i = 0; i < 4; i++)
                    {
                        mb.Push(V(0, 7.3f, 2.3f), V(0, 0, i * 90f + 20f));
                        mb.Col(Pal.WoodDark).Box(V(0, 2.4f, 0), V(0.12f, 4.8f, 0.08f));
                        mb.Col(Pal.Cream).Box(V(0.45f, 2.9f, 0), V(0.8f, 3.6f, 0.03f));
                        mb.Pop();
                    }
                    mb.Col(Pal.WoodDark).Quad(V(-0.5f, 0, 2.42f), V(0.5f, 0, 2.42f), V(0.5f, 1.9f, 2.3f), V(-0.5f, 1.9f, 2.3f), Vector3.forward);
                });
            Add("barn", "farm", 8f, "Big red barn", "farm,building,countryside,ranch", "farmhouse,stable,shed",
                mb =>
                {
                    mb.Shade(0.04f).Col(Pal.Hex("#A83A30")).Box(V(0, 2.5f - 0.5f, 0), V(9f, 6f, 12f));
                    Vector3 a = V(-4.8f, 5f, -6.3f), b = V(4.8f, 5f, -6.3f), c = V(4.8f, 5f, 6.3f), d = V(-4.8f, 5f, 6.3f);
                    Vector3 e = V(-2.6f, 7f, -6.3f), f = V(2.6f, 7f, -6.3f), g = V(2.6f, 7f, 6.3f), h = V(-2.6f, 7f, 6.3f), i = V(0, 8f, -6.3f), j = V(0, 8f, 6.3f);
                    mb.Col(Pal.RoofDark).Quad(a, d, h, e, V(-1, 1, 0)).Quad(b, c, g, f, V(1, 1, 0)).Quad(e, h, j, i, V(-0.4f, 1, 0)).Quad(f, g, j, i, V(0.4f, 1, 0));
                    mb.Col(Pal.Hex("#A83A30"));
                    foreach (float z in new[] { -6.3f, 6.3f })
                    {
                        Vector3 n = V(0, 0, Mathf.Sign(z));
                        mb.Quad(V(-4.5f, 5f, z), V(4.5f, 5f, z), V(2.6f, 7f, z), V(-2.6f, 7f, z), n); mb.Tri(V(-2.6f, 7f, z), V(2.6f, 7f, z), V(0, 8f, z), n);
                    }
                    mb.Col(Pal.White).Quad(V(-1.8f, 0, 6.02f), V(1.8f, 0, 6.02f), V(1.8f, 3.4f, 6.02f), V(-1.8f, 3.4f, 6.02f), Vector3.forward);
                    mb.Col(Pal.Hex("#7A2A24")).Quad(V(-1.6f, 0, 6.04f), V(1.6f, 0, 6.04f), V(1.6f, 3.2f, 6.04f), V(-1.6f, 3.2f, 6.04f), Vector3.forward);
                });

            // ---- party and food
            Add("gift_box", "party", 0.4f, "Wrapped present with a ribbon", "party,birthday,christmas,present", "present,gift,parcel",
                mb =>
                {
                    mb.Col(Pal.White).BoxOn(Vector3.zero, V(0.4f, 0.36f, 0.4f));
                    mb.Col(Pal.Red).Box(V(0, 0.18f, 0), V(0.42f, 0.37f, 0.07f)).Box(V(0, 0.18f, 0), V(0.07f, 0.37f, 0.42f));
                    mb.Push(V(0.07f, 0.4f, 0), V(0, 0, 60f), V(1f, 1f, 0.5f)).Torus(Vector3.zero, 0.06f, 0.02f, 8, 3).Pop();
                    mb.Push(V(-0.07f, 0.4f, 0), V(0, 0, -60f), V(1f, 1f, 0.5f)).Torus(Vector3.zero, 0.06f, 0.02f, 8, 3).Pop();
                }).Hold().Solid(1f, 0.2f);
            Add("cake", "party", 0.5f, "Three-tier birthday cake with candles", "party,birthday,food,dessert,celebration", "birthday cake,wedding cake",
                mb =>
                {
                    mb.Col(Pal.Cream).Cyl(Vector3.zero, 0.38f, 0.38f, 0.18f, 12).Cyl(V(0, 0.18f, 0), 0.27f, 0.27f, 0.16f, 12).Cyl(V(0, 0.34f, 0), 0.16f, 0.16f, 0.13f, 10);
                    mb.Col(Pal.Pink).Cyl(V(0, 0.16f, 0), 0.39f, 0.39f, 0.03f, 12, false).Cyl(V(0, 0.32f, 0), 0.28f, 0.28f, 0.03f, 12, false).Cyl(V(0, 0.45f, 0), 0.17f, 0.17f, 0.03f, 10);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i / 5f * Mathf.PI * 2f;
                        Vector3 p = V(Mathf.Cos(a) * 0.1f, 0.48f, Mathf.Sin(a) * 0.1f);
                        mb.Emit().Col(Pal.Blue).Cyl(p, 0.008f, 0.008f, 0.08f, 4);
                        mb.Emit(1f).Col(Pal.FireCore).Sphere(p + V(0, 0.095f, 0), V(0.01f, 0.018f, 0.01f), 0);
                    }
                    mb.Emit().Glow(V(0, 0.6f, 0), 0.5f, new Color(1f, 0.8f, 0.4f) * 0.5f);
                });
            Add("cupcake", "party", 0.14f, "Frosted cupcake", "food,dessert,sweet,party", "muffin,fairy cake",
                mb =>
                {
                    mb.Col(Pal.Hex("#C8A070")).Cyl(Vector3.zero, 0.04f, 0.055f, 0.06f, 8);
                    mb.Col(Pal.Pink).Lathe(V(0, 0.06f, 0), Profile(0.06f, 0f, 0.05f, 0.03f, 0.03f, 0.055f, 0f, 0.08f), 8);
                    mb.Col(Pal.Red).Sphere(V(0, 0.145f, 0), 0.014f, 0);
                }).Hold().Solid(0.1f, 0.1f);
            Add("donut", "party", 0.12f, "Pink glazed doughnut", "food,dessert,sweet,snack", "doughnut,bagel",
                mb =>
                {
                    mb.Col(Pal.Hex("#D8A868")).Torus(V(0, 0.035f, 0), 0.06f, 0.035f, 12, 6);
                    mb.Col(Pal.Pink).Push(V(0, 0.047f, 0), V(0, 0, 0), V(1f, 0.6f, 1f)).Torus(Vector3.zero, 0.06f, 0.034f, 12, 5).Pop();
                }).Hold().Solid(0.1f, 0.3f);
            Add("lollipop", "party", 2.4f, "Giant swirly lollipop", "candy,sweet,candyland,giant", "candy,sucker,sweet",
                mb =>
                {
                    mb.Col(Pal.White).Cyl(Vector3.zero, 0.05f, 0.05f, 1.6f, 6);
                    mb.Push(V(0, 1.9f, 0), V(90f, 0, 0));
                    mb.Col(Pal.Pink).Cyl(V(0, -0.06f, 0), 0.5f, 0.5f, 0.12f, 16);
                    mb.Col(Pal.White).Torus(Vector3.zero, 0.36f, 0.066f, 14, 4).Torus(Vector3.zero, 0.16f, 0.066f, 10, 4);
                    mb.Pop();
                });
            Add("candy_cane", "party", 1.6f, "Red and white striped candy cane", "candy,christmas,sweet,candyland", "peppermint stick",
                mb =>
                {
                    for (int i = 0; i < 10; i++) mb.Col(i % 2 == 0 ? Pal.White : Pal.Red).Cyl(V(0, i * 0.12f, 0), 0.06f, 0.06f, 0.12f, 7, false);
                    for (int i = 0; i < 6; i++)
                    {
                        float a0 = i / 6f * Mathf.PI, a1 = (i + 1) / 6f * Mathf.PI;
                        mb.Col(i % 2 == 0 ? Pal.Red : Pal.White).Rod(V(0.25f - Mathf.Cos(a0) * 0.25f, 1.2f + Mathf.Sin(a0) * 0.25f, 0), V(0.25f - Mathf.Cos(a1) * 0.25f, 1.2f + Mathf.Sin(a1) * 0.25f, 0), 0.06f, 0.06f, 7, i == 5);
                    }
                });
            Add("ice_cream", "party", 0.22f, "Ice cream cone with two scoops", "food,dessert,sweet,summer,beach", "ice cream cone,gelato",
                mb =>
                {
                    mb.Col(Pal.Hex("#D8A868")).Cone(V(0, 0.13f, 0), 0.045f, -0.14f, 8);
                    mb.Col(Pal.Pink).Sphere(V(0, 0.15f, 0), 0.05f, 1);
                    mb.Col(Pal.Cream).Sphere(V(0, 0.215f, 0), 0.043f, 1);
                }).AsTool().Solid(0.1f, 0.1f);
            Add("pizza", "party", 0.05f, "Whole pepperoni pizza", "food,dinner,italian,party", "pie,pizza pie",
                mb =>
                {
                    mb.Col(Pal.Hex("#D8A868")).Cyl(Vector3.zero, 0.2f, 0.2f, 0.02f, 14);
                    mb.Col(Pal.Hex("#F2D060")).Cyl(V(0, 0.02f, 0), 0.175f, 0.175f, 0.006f, 14);
                    mb.Col(Pal.Red);
                    for (int i = 0; i < 8; i++) { float a = i * 2.4f, r = 0.05f + (i % 3) * 0.045f; mb.Cyl(V(Mathf.Cos(a) * r, 0.026f, Mathf.Sin(a) * r), 0.022f, 0.022f, 0.004f, 6); }
                }).Hold().Solid(0.4f, 0.1f);

            // ---- sports
            Add("basketball_hoop", "sports", 3.6f, "Basketball hoop on a pole. Throw a basketball through it", "basketball,sport,court,game,score", "hoop,basket,net",
                mb =>
                {
                    mb.Col(Pal.MetalDark).Cyl(V(0, 0, -0.9f), 0.09f, 0.09f, 3.3f, 6).Rod(V(0, 3.2f, -0.9f), V(0, 3.4f, -0.1f), 0.07f, 0.07f, 5);
                    mb.Col(Pal.White).Box(V(0, 3.5f, -0.05f), V(1.8f, 1.05f, 0.05f));
                    mb.Col(Pal.Red).Box(V(0, 3.3f, -0.02f), V(0.6f, 0.45f, 0.01f));
                    mb.Col(Pal.Orange).Torus(V(0, 3.05f, 0.28f), 0.23f, 0.018f, 14, 4);
                    mb.Col(Pal.White);
                    for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2f; mb.Rod(V(Mathf.Cos(a) * 0.23f, 3.05f, 0.28f + Mathf.Sin(a) * 0.23f), V(Mathf.Cos(a) * 0.13f, 2.65f, 0.28f + Mathf.Sin(a) * 0.13f), 0.006f, 0.006f, 3, false); }
                });
            Add("soccer_goal", "sports", 2.44f, "Football goal with a net", "soccer,football,sport,pitch,game,score", "goal,football goal,net",
                mb =>
                {
                    mb.Col(Pal.White);
                    foreach (float x in new[] { -3.66f, 3.66f }) { mb.Cyl(V(x, 0, 0), 0.06f, 0.06f, 2.44f, 6); mb.Rod(V(x, 2.44f, 0), V(x, 0, -1.8f), 0.03f, 0.03f, 4); }
                    mb.Rod(V(-3.66f, 2.44f, 0), V(3.66f, 2.44f, 0), 0.06f, 0.06f, 6).Rod(V(-3.66f, 0.03f, -1.8f), V(3.66f, 0.03f, -1.8f), 0.03f, 0.03f, 4);
                    mb.Col(Pal.Hex("#DDE4EA"));
                    for (int i = 1; i < 10; i++) mb.Rod(V(-3.66f + i * 0.732f, 2.44f, 0), V(-3.66f + i * 0.732f, 0.03f, -1.8f), 0.008f, 0.008f, 3, false);
                    for (int i = 1; i < 5; i++) mb.Rod(V(-3.66f, 2.44f - i * 0.49f, -i * 0.36f), V(3.66f, 2.44f - i * 0.49f, -i * 0.36f), 0.008f, 0.008f, 3, false);
                });
            Add("target", "sports", 1.6f, "Archery target on a stand", "archery,aim,shoot,sport,game,score", "bullseye,dartboard,archery target",
                mb =>
                {
                    mb.Col(Pal.WoodDark).Rod(V(-0.4f, 0, -0.3f), V(0, 1.2f, 0), 0.03f, 0.03f, 4).Rod(V(0.4f, 0, -0.3f), V(0, 1.2f, 0), 0.03f, 0.03f, 4).Rod(V(0, 0, 0.3f), V(0, 1.1f, 0.05f), 0.03f, 0.03f, 4);
                    mb.Push(V(0, 1.05f, 0.12f), V(80f, 0, 0));
                    Color[] rings = { Pal.White, Pal.Black, Pal.Blue, Pal.Red, Pal.Yellow };
                    for (int i = 0; i < 5; i++) mb.Col(rings[i]).Cyl(V(0, i * 0.006f, 0), 0.55f - i * 0.1f, 0.55f - i * 0.1f, 0.05f, 16);
                    mb.Pop();
                });
            Add("skateboard", "sports", 0.12f, "Skateboard that rolls", "skate,sport,roll,street", "board,longboard",
                mb =>
                {
                    mb.Col(Pal.Hex("#2A2C30")).Box(V(0, 0.1f, 0), V(0.2f, 0.015f, 0.72f));
                    mb.Col(Pal.Orange).Box(V(0, 0.09f, 0), V(0.19f, 0.01f, 0.7f));
                    foreach (float z in new[] { -0.25f, 0.25f }) { mb.Col(Pal.Metal).Box(V(0, 0.07f, z), V(0.14f, 0.03f, 0.04f)); foreach (float x in new[] { -0.09f, 0.09f }) mb.Col(Pal.White).Rod(V(x - 0.015f, 0.03f, z), V(x + 0.015f, 0.03f, z), 0.03f, 0.03f, 6); }
                }).Hold().Solid(2f, 0.1f).Drag = 0.02f;
        }
    }
}
