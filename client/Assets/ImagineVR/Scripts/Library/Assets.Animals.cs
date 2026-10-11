using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    public static partial class Library
    {
        // ------------------------------------------------------------------ generators

        /// <summary>Where a quadruped's head ended up, so recipes can add ears, horns, manes and trunks.</summary>
        struct Head { public Vector3 Pos, Dir, NeckBase; public float Size; }

        /// <summary>
        /// A four-legged animal facing +Z, standing on the ground. Everything is proportions:
        /// body length/height/width, leg height, neck length and angle, head size, tail length.
        /// </summary>
        static Head Quadruped(MeshBuilder mb, float bodyL, float bodyH, float bodyW, float legH, float legR,
            float neckL, float neckDeg, float headL, float headS, float tailL, Color body, Color dark, float tailDrop = 0.6f)
        {
            float cy = legH + bodyH * 0.5f;
            mb.Shade(0.05f).Grad(Pal.Dark(body, 0.18f), body, legH, legH + bodyH);
            mb.Sphere(V(0, cy, 0), V(bodyW * 0.5f, bodyH * 0.5f, bodyL * 0.5f), 1);

            mb.Col(dark);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    mb.Rod(V(sx * bodyW * 0.3f, cy - bodyH * 0.1f, sz * bodyL * 0.32f), V(sx * bodyW * 0.3f, 0f, sz * bodyL * 0.34f), legR * 1.2f, legR, 6);

            float a = neckDeg * Mathf.Deg2Rad;
            var dir = new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a));
            Vector3 neckBase = V(0, cy + bodyH * 0.22f, bodyL * 0.36f);
            Vector3 neckEnd = neckBase + dir * neckL;
            mb.Col(body).Rod(neckBase - dir * (bodyH * 0.15f), neckEnd, bodyW * 0.26f, bodyW * 0.19f, 6);

            Vector3 headPos = neckEnd + V(0, headS * 0.1f, headL * 0.28f);
            mb.Sphere(headPos, V(headS * 0.5f, headS * 0.5f, headL * 0.5f), 1);
            mb.Col(Pal.Black);
            mb.Sphere(headPos + V(headS * 0.36f, headS * 0.14f, headL * 0.12f), headS * 0.07f, 0);
            mb.Sphere(headPos + V(-headS * 0.36f, headS * 0.14f, headL * 0.12f), headS * 0.07f, 0);

            if (tailL > 0f)
            {
                Vector3 t0 = V(0, cy + bodyH * 0.2f, -bodyL * 0.46f);
                mb.Col(dark).Rod(t0, t0 + V(0, -tailL * tailDrop, -tailL * (1f - tailDrop * 0.6f)), bodyW * 0.08f, bodyW * 0.03f, 4);
            }
            mb.Col(body);
            return new Head { Pos = headPos, Dir = dir, NeckBase = neckBase, Size = headS };
        }

        static void Ears(MeshBuilder mb, Head h, float size, Color color, bool pointed = true)
        {
            mb.Col(color);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 b = h.Pos + V(s * h.Size * 0.3f, h.Size * 0.38f, -h.Size * 0.1f);
                if (pointed) mb.Cone(b, size * 0.4f, size, 4);
                else mb.Sphere(b + V(0, size * 0.3f, 0), V(size * 0.5f, size * 0.5f, size * 0.2f), 0);
            }
        }

        /// <summary>
        /// A simple standing person, facing +Z. Returns the centre of the head.
        /// `top` is the shirt or coat, `bottom` the trousers or skirt.
        /// </summary>
        static Vector3 Figure(MeshBuilder mb, float height, Color skin, Color top, Color bottom, Color hair, bool skirt = false, float bulk = 1f, bool reach = false)
        {
            float leg = height * 0.47f, torso = height * 0.31f, head = height * 0.13f, w = height * 0.19f * bulk;
            mb.Shade(0.04f);
            mb.Col(bottom);
            if (skirt)
            {
                mb.Cyl(V(0, leg * 0.42f, 0), w * 0.95f, w * 0.52f, leg * 0.6f, 8);
                mb.Col(skin).Rod(V(-w * 0.22f, leg * 0.45f, 0), V(-w * 0.22f, 0.04f, 0), w * 0.16f, w * 0.13f, 5).Rod(V(w * 0.22f, leg * 0.45f, 0), V(w * 0.22f, 0.04f, 0), w * 0.16f, w * 0.13f, 5);
            }
            else
            {
                mb.Rod(V(-w * 0.26f, leg, 0), V(-w * 0.28f, 0.05f, 0), w * 0.24f, w * 0.19f, 5).Rod(V(w * 0.26f, leg, 0), V(w * 0.28f, 0.05f, 0), w * 0.24f, w * 0.19f, 5);
            }
            mb.Col(Pal.Black).Box(V(-w * 0.28f, 0.035f, w * 0.12f), V(w * 0.3f, 0.07f, w * 0.6f)).Box(V(w * 0.28f, 0.035f, w * 0.12f), V(w * 0.3f, 0.07f, w * 0.6f));

            mb.Col(top).Lathe(V(0, leg - 0.02f, 0), Profile(w * 0.5f, 0f, w * 0.56f, torso * 0.3f, w * 0.62f, torso * 0.8f, w * 0.4f, torso, 0f, torso * 1.03f), 7);
            float shoulder = leg + torso * 0.86f;
            // arms hang at the sides, or reach straight out in front
            Vector3 handL = reach ? V(-w * 0.5f, shoulder - torso * 0.12f, torso * 0.95f) : V(-w * 0.78f, shoulder - torso * 0.95f, w * 0.1f);
            Vector3 handR = reach ? V(w * 0.5f, shoulder + torso * 0.04f, torso * 0.95f) : V(w * 0.78f, shoulder - torso * 0.95f, w * 0.1f);
            mb.Rod(V(-w * 0.62f, shoulder, 0), handL, w * 0.17f, w * 0.14f, 5);
            mb.Rod(V(w * 0.62f, shoulder, 0), handR, w * 0.17f, w * 0.14f, 5);
            mb.Col(skin).Sphere(handL, w * 0.15f, 0).Sphere(handR, w * 0.15f, 0);

            Vector3 headPos = V(0, leg + torso + head * 0.62f, 0);
            mb.Cyl(V(0, leg + torso - 0.01f, 0), w * 0.16f, w * 0.15f, head * 0.3f, 6, false);
            mb.Sphere(headPos, V(head * 0.48f, head * 0.56f, head * 0.5f), 1);
            mb.Col(hair).Sphere(headPos + V(0, head * 0.14f, -head * 0.06f), V(head * 0.52f, head * 0.5f, head * 0.54f), 1);
            mb.Col(Pal.Black).Sphere(headPos + V(-head * 0.17f, head * 0.02f, head * 0.45f), head * 0.05f, 0).Sphere(headPos + V(head * 0.17f, head * 0.02f, head * 0.45f), head * 0.05f, 0);
            return headPos;
        }

        static partial void RegisterAnimals()
        {
            // ---- farm and pets
            Add("horse", "animals", 2.2f, "Brown horse", "animal,farm,ranch,ride,western", "pony,stallion,mare,steed",
                mb =>
                {
                    Color c = Pal.Hex("#8A5A3A"), d = Pal.Hex("#4A3020");
                    Head h = Quadruped(mb, 1.9f, 0.75f, 0.6f, 1.0f, 0.07f, 0.85f, 52f, 0.6f, 0.3f, 0.9f, c, d, 0.9f);
                    Ears(mb, h, 0.14f, c);
                    mb.Col(d).Box(h.NeckBase + V(0, 0.5f, 0.12f), V(0.06f, 0.75f, 0.3f));
                });
            Add("cow", "animals", 1.6f, "Black and white dairy cow", "animal,farm,cattle,field", "cattle,bull,ox,dairy cow",
                mb =>
                {
                    Head h = Quadruped(mb, 1.9f, 0.9f, 0.75f, 0.7f, 0.08f, 0.4f, 12f, 0.5f, 0.36f, 0.8f, Pal.White, Pal.Hex("#3A3A3A"), 0.95f);
                    mb.Col(Pal.Black).Sphere(V(0.2f, 1.25f, 0.3f), V(0.3f, 0.3f, 0.36f), 0).Sphere(V(-0.25f, 1.1f, -0.4f), V(0.3f, 0.36f, 0.4f), 0);
                    mb.Col(Pal.Pink).Sphere(h.Pos + V(0, -0.08f, 0.22f), V(0.14f, 0.1f, 0.08f), 0);
                    mb.Col(Pal.Cream).Rod(h.Pos + V(0.14f, 0.16f, 0), h.Pos + V(0.26f, 0.32f, 0.02f), 0.035f, 0.01f, 4).Rod(h.Pos + V(-0.14f, 0.16f, 0), h.Pos + V(-0.26f, 0.32f, 0.02f), 0.035f, 0.01f, 4);
                });
            Add("sheep", "animals", 1.0f, "Woolly sheep: a thick cream fleece, dark face and legs", "animal,farm,field,wool,flock,pasture", "lamb,ewe,ram",
                mb =>
                {
                    Color face = Pal.Hex("#3B3531"), leg = Pal.Hex("#2B2623"), wool = Pal.Hex("#F6EEDC"), woolLow = Pal.Hex("#D9CCB0");
                    // four dark legs, each ending in a hoof
                    foreach (float sx in new[] { -1f, 1f })
                        foreach (float sz in new[] { -1f, 1f })
                        {
                            Vector3 foot = V(sx * 0.15f, 0f, sz * 0.27f + 0.02f);
                            mb.Shade(0.04f).Col(leg).Rod(foot + V(0, 0.44f, 0), foot + V(0, 0.05f, 0), 0.045f, 0.03f, 6);
                            mb.Col(Pal.Hex("#151312")).Cyl(foot, 0.04f, 0.033f, 0.055f, 6);
                        }
                    // the fleece: one big lumpy mass, with more piled on the back, flanks, chest and rump
                    mb.Shade(0.07f).Grad(woolLow, wool, 0.4f, 0.95f);
                    mb.Blob(V(0, 0.67f, 0f), V(0.28f, 0.26f, 0.45f), 0.1f, 4, 2);
                    mb.Blob(V(0, 0.84f, -0.2f), V(0.2f, 0.15f, 0.22f), 0.2f, 7, 1).Blob(V(0, 0.86f, 0.1f), V(0.2f, 0.15f, 0.2f), 0.2f, 9, 1);
                    mb.Blob(V(0.19f, 0.64f, 0.02f), V(0.13f, 0.18f, 0.3f), 0.2f, 11, 1).Blob(V(-0.19f, 0.64f, 0.02f), V(0.13f, 0.18f, 0.3f), 0.2f, 12, 1);
                    mb.Blob(V(0, 0.62f, -0.4f), V(0.19f, 0.18f, 0.12f), 0.2f, 13, 1).Blob(V(0, 0.66f, 0.38f), V(0.2f, 0.21f, 0.14f), 0.2f, 15, 1);
                    mb.Blob(V(0, 0.7f, -0.52f), V(0.055f, 0.08f, 0.05f), 0.2f, 3, 0);
                    // the head reaches out of the wool: a long dark face, ears held out sideways
                    mb.Shade(0.04f).Col(face);
                    mb.Rod(V(0, 0.76f, 0.4f), V(0, 0.83f, 0.56f), 0.1f, 0.08f, 7);
                    mb.Sphere(V(0, 0.85f, 0.6f), V(0.09f, 0.1f, 0.13f), 1).Sphere(V(0, 0.79f, 0.71f), V(0.062f, 0.06f, 0.085f), 1);
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        mb.Col(face).Push(V(sx * 0.1f, 0.89f, 0.54f), V(0, 0, sx * -24f)).Sphere(V(sx * 0.06f, 0, 0), V(0.075f, 0.022f, 0.04f), 0).Pop();
                        mb.Col(Pal.Hex("#0E0D0C")).Sphere(V(sx * 0.078f, 0.875f, 0.645f), 0.017f, 0);
                        mb.Col(Pal.White).Sphere(V(sx * 0.086f, 0.882f, 0.654f), 0.006f, 0);
                    }
                    mb.Col(Pal.Hex("#191615")).Sphere(V(0, 0.785f, 0.79f), V(0.032f, 0.022f, 0.014f), 0);
                    mb.Shade(0.07f).Col(wool).Blob(V(0, 0.95f, 0.53f), V(0.11f, 0.07f, 0.1f), 0.25f, 5, 1);
                });
            Add("pig", "animals", 0.8f, "Pink pig", "animal,farm,mud", "hog,piglet,swine,boar",
                mb =>
                {
                    Color c = Pal.Hex("#F2A8B4");
                    Head h = Quadruped(mb, 1.1f, 0.6f, 0.55f, 0.28f, 0.06f, 0.12f, 5f, 0.36f, 0.36f, 0.15f, c, Pal.Dark(c, 0.15f), 0.2f);
                    mb.Col(Pal.Dark(c, 0.2f)).Cyl(h.Pos + V(0, -0.04f, 0.17f), 0.09f, 0.09f, 0.01f, 6);
                    mb.Push(h.Pos + V(0, -0.04f, 0.14f), V(90f, 0, 0)).Cyl(Vector3.zero, 0.09f, 0.08f, 0.08f, 6).Pop();
                    Ears(mb, h, 0.12f, c);
                });
            Add("chicken", "animals", 0.45f, "White hen", "animal,farm,bird,coop", "hen,rooster,chick",
                mb =>
                {
                    mb.Shade(0.05f).Col(Pal.White).Sphere(V(0, 0.22f, 0), V(0.13f, 0.14f, 0.18f), 1);
                    mb.Sphere(V(0, 0.38f, 0.12f), 0.075f, 0).Cone(V(0, 0.2f, -0.16f), 0.07f, 0.18f, 4);
                    mb.Col(Pal.Red).Box(V(0, 0.46f, 0.12f), V(0.02f, 0.06f, 0.08f)).Sphere(V(0, 0.33f, 0.17f), 0.025f, 0);
                    mb.Col(Pal.Orange).Push(V(0, 0.38f, 0.18f), V(90f, 0, 0)).Cone(Vector3.zero, 0.025f, 0.06f, 4).Pop();
                    mb.Rod(V(-0.05f, 0.1f, 0), V(-0.05f, 0, 0.02f), 0.012f, 0.012f, 3).Rod(V(0.05f, 0.1f, 0), V(0.05f, 0, 0.02f), 0.012f, 0.012f, 3);
                });
            Add("dog", "animals", 0.7f, "Friendly tan dog", "animal,pet,puppy,park", "puppy,hound,labrador,retriever,pup",
                mb =>
                {
                    Color c = Pal.Hex("#C89A5C");
                    Head h = Quadruped(mb, 0.75f, 0.32f, 0.28f, 0.36f, 0.035f, 0.22f, 50f, 0.3f, 0.2f, 0.35f, c, Pal.Dark(c, 0.15f), -0.4f);
                    Ears(mb, h, 0.12f, Pal.Dark(c, 0.3f), false);
                    mb.Col(Pal.Black).Sphere(h.Pos + V(0, -0.01f, 0.15f), 0.03f, 0);
                });
            Add("cat", "animals", 0.35f, "Orange tabby cat", "animal,pet,kitten", "kitten,kitty,tabby",
                mb =>
                {
                    Color c = Pal.Hex("#E0903A");
                    Head h = Quadruped(mb, 0.42f, 0.17f, 0.15f, 0.17f, 0.02f, 0.08f, 55f, 0.14f, 0.14f, 0.34f, c, Pal.Dark(c, 0.12f), -1.3f);
                    Ears(mb, h, 0.07f, c);
                });
            Add("rabbit", "animals", 0.35f, "Small rabbit with long ears", "animal,pet,bunny,meadow", "bunny,hare",
                mb =>
                {
                    Color c = Pal.Hex("#C8BCAE");
                    mb.Shade(0.05f).Col(c).Sphere(V(0, 0.13f, 0), V(0.1f, 0.12f, 0.15f), 1).Sphere(V(0, 0.24f, 0.12f), 0.075f, 1);
                    mb.Col(Pal.White).Sphere(V(0, 0.12f, -0.15f), 0.04f, 0);
                    mb.Col(c).Lathe(V(-0.03f, 0.29f, 0.1f), Profile(0.015f, 0f, 0.025f, 0.08f, 0f, 0.17f), 4).Lathe(V(0.03f, 0.29f, 0.1f), Profile(0.015f, 0f, 0.025f, 0.08f, 0f, 0.17f), 4);
                    mb.Col(Pal.Black).Sphere(V(0.055f, 0.26f, 0.16f), 0.012f, 0).Sphere(V(-0.055f, 0.26f, 0.16f), 0.012f, 0);
                });

            // ---- wild
            Add("deer", "animals", 1.7f, "Deer with antlers", "animal,forest,wild,stag", "stag,buck,doe,reindeer,elk,moose",
                mb =>
                {
                    Color c = Pal.Hex("#A8743E");
                    Head h = Quadruped(mb, 1.25f, 0.5f, 0.4f, 0.85f, 0.035f, 0.6f, 62f, 0.34f, 0.2f, 0.15f, c, Pal.Dark(c, 0.25f), 0.2f);
                    Ears(mb, h, 0.12f, c);
                    mb.Col(Pal.Cream);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Vector3 b = h.Pos + V(s * 0.06f, 0.1f, -0.03f), m = b + V(s * 0.16f, 0.3f, -0.05f);
                        mb.Rod(b, m, 0.02f, 0.015f, 4).Rod(m, m + V(s * 0.12f, 0.22f, 0.05f), 0.015f, 0.006f, 4).Rod(m, m + V(-s * 0.02f, 0.2f, 0.1f), 0.014f, 0.006f, 4);
                    }
                });
            Add("fox", "animals", 0.6f, "Red fox with a bushy tail", "animal,forest,wild", "wolf,coyote,jackal",
                mb =>
                {
                    Color c = Pal.Hex("#D9682A");
                    Head h = Quadruped(mb, 0.7f, 0.26f, 0.22f, 0.3f, 0.025f, 0.16f, 40f, 0.28f, 0.17f, 0f, c, Pal.Hex("#3A2A20"));
                    Ears(mb, h, 0.1f, c);
                    mb.Col(c).Sphere(V(0, 0.42f, -0.55f), V(0.1f, 0.1f, 0.28f), 0);
                    mb.Col(Pal.White).Sphere(V(0, 0.42f, -0.78f), V(0.07f, 0.07f, 0.1f), 0);
                });
            Add("bear", "animals", 1.5f, "Big brown bear", "animal,forest,wild,grizzly", "grizzly,grizzly bear,black bear",
                mb =>
                {
                    Color c = Pal.Hex("#5E4030");
                    Head h = Quadruped(mb, 1.7f, 1.0f, 0.95f, 0.55f, 0.13f, 0.3f, 15f, 0.5f, 0.44f, 0f, c, Pal.Dark(c, 0.15f));
                    Ears(mb, h, 0.14f, c, false);
                    mb.Col(Pal.Hex("#8A6A50")).Sphere(h.Pos + V(0, -0.06f, 0.22f), V(0.13f, 0.11f, 0.12f), 0);
                    mb.Col(Pal.Black).Sphere(h.Pos + V(0, -0.02f, 0.33f), 0.04f, 0);
                });
            Add("lion", "animals", 1.3f, "Lion with a golden mane", "animal,africa,safari,savanna,wild,king", "lioness,big cat,tiger",
                mb =>
                {
                    Color c = Pal.Hex("#D8A858");
                    Head h = Quadruped(mb, 1.5f, 0.6f, 0.5f, 0.6f, 0.07f, 0.3f, 30f, 0.4f, 0.36f, 0.9f, c, Pal.Dark(c, 0.1f), 0.7f);
                    mb.Shade(0.08f).Col(Pal.Hex("#9A5A2A")).Blob(h.Pos + V(0, 0, -0.2f), V(0.36f, 0.4f, 0.3f), 0.2f, 6, 1);
                    mb.Sphere(V(0, 0.85f, -1.45f), 0.08f, 0);
                    Ears(mb, h, 0.1f, c, false);
                });
            Add("elephant", "animals", 3f, "Grey elephant with tusks", "animal,africa,safari,savanna,wild,big", "mammoth",
                mb =>
                {
                    Color c = Pal.Hex("#8E9098");
                    Head h = Quadruped(mb, 3.0f, 1.8f, 1.6f, 1.3f, 0.26f, 0.5f, 10f, 1.0f, 1.0f, 0.9f, c, Pal.Dark(c, 0.1f), 1f);
                    mb.Col(c);
                    Vector3 t = h.Pos + V(0, -0.15f, 0.45f);
                    mb.Rod(t, t + V(0, -0.8f, 0.25f), 0.2f, 0.14f, 6).Rod(t + V(0, -0.8f, 0.25f), t + V(0, -1.6f, 0.15f), 0.14f, 0.09f, 6).Rod(t + V(0, -1.6f, 0.15f), t + V(0, -1.75f, 0.45f), 0.09f, 0.07f, 5);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        mb.Col(Pal.Dark(c, 0.08f)).Sphere(h.Pos + V(s * 0.75f, 0.1f, -0.25f), V(0.5f, 0.65f, 0.1f), 0);
                        mb.Col(Pal.Cream).Rod(h.Pos + V(s * 0.28f, -0.4f, 0.35f), h.Pos + V(s * 0.4f, -0.75f, 1.0f), 0.07f, 0.02f, 5);
                    }
                });
            Add("giraffe", "animals", 5f, "Tall spotted giraffe", "animal,africa,safari,savanna,wild,tall", "",
                mb =>
                {
                    Color c = Pal.Hex("#E8C060"), spot = Pal.Hex("#9A6230");
                    Head h = Quadruped(mb, 1.9f, 0.9f, 0.7f, 1.9f, 0.08f, 2.3f, 72f, 0.6f, 0.3f, 0.9f, c, Pal.Hex("#C09848"), 1f);
                    mb.Col(spot);
                    for (int i = 0; i < 9; i++) mb.Sphere(V(Mathf.Sin(i * 2.4f) * 0.33f, 2.25f + Mathf.Cos(i * 1.7f) * 0.25f, -0.7f + i * 0.17f), V(0.09f, 0.12f, 0.12f), 0);
                    for (int i = 0; i < 5; i++) mb.Sphere(h.NeckBase + h.Dir * (0.35f + i * 0.42f) + V(i % 2 == 0 ? 0.13f : -0.13f, 0, 0), V(0.07f, 0.11f, 0.09f), 0);
                    mb.Col(spot).Rod(h.Pos + V(0.08f, 0.12f, -0.05f), h.Pos + V(0.09f, 0.32f, -0.06f), 0.025f, 0.03f, 4).Rod(h.Pos + V(-0.08f, 0.12f, -0.05f), h.Pos + V(-0.09f, 0.32f, -0.06f), 0.025f, 0.03f, 4);
                    Ears(mb, h, 0.12f, c, false);
                });
            Add("penguin", "animals", 0.8f, "Black and white penguin", "animal,arctic,ice,snow,antarctic,bird", "penguins",
                mb =>
                {
                    mb.Shade(0.04f).Col(Pal.Hex("#22262E")).Lathe(Vector3.zero, Profile(0f, 0.02f, 0.2f, 0.06f, 0.23f, 0.3f, 0.17f, 0.56f, 0.13f, 0.7f, 0f, 0.8f), 8);
                    mb.Col(Pal.White).Sphere(V(0, 0.33f, 0.1f), V(0.16f, 0.25f, 0.14f), 1);
                    mb.Col(Pal.Orange).Push(V(0, 0.66f, 0.12f), V(90f, 0, 0)).Cone(Vector3.zero, 0.035f, 0.1f, 4).Pop();
                    mb.Box(V(-0.09f, 0.02f, 0.08f), V(0.12f, 0.04f, 0.18f)).Box(V(0.09f, 0.02f, 0.08f), V(0.12f, 0.04f, 0.18f));
                    mb.Col(Pal.Hex("#22262E")).Rod(V(-0.2f, 0.52f, 0), V(-0.3f, 0.22f, 0), 0.05f, 0.025f, 4).Rod(V(0.2f, 0.52f, 0), V(0.3f, 0.22f, 0), 0.05f, 0.025f, 4);
                    mb.Col(Pal.White).Sphere(V(-0.06f, 0.69f, 0.1f), 0.022f, 0).Sphere(V(0.06f, 0.69f, 0.1f), 0.022f, 0);
                });

            // ---- prehistoric and mythical
            Add("brontosaurus", "animals", 9f, "Enormous long-necked dinosaur", "dinosaur,prehistoric,jurassic,giant", "dinosaur,brachiosaurus,sauropod,diplodocus,long neck",
                mb =>
                {
                    Color c = Pal.Hex("#6E9A68");
                    Quadruped(mb, 7f, 3.2f, 2.8f, 2.6f, 0.5f, 6.5f, 55f, 1.3f, 0.75f, 0f, c, Pal.Dark(c, 0.15f));
                    mb.Col(c).Rod(V(0, 4.2f, -3.2f), V(0, 2.8f, -6.5f), 0.9f, 0.4f, 6).Rod(V(0, 2.8f, -6.5f), V(0, 1.4f, -10f), 0.4f, 0.08f, 5);
                });
            Add("t_rex", "animals", 5.5f, "Fearsome Tyrannosaurus rex", "dinosaur,prehistoric,jurassic,giant,monster", "tyrannosaurus,dinosaur,rex,trex,raptor,velociraptor",
                mb =>
                {
                    Color c = Pal.Hex("#7A8A4A"), d = Pal.Hex("#4E5A30");
                    mb.Shade(0.06f).Grad(d, c, 1.5f, 4.5f);
                    mb.Sphere(V(0, 3.2f, 0), V(1.0f, 1.2f, 2.1f), 1);
                    mb.Rod(V(0, 3.5f, 1.5f), V(0, 4.7f, 2.8f), 0.8f, 0.6f, 6);
                    mb.Sphere(V(0, 4.9f, 3.5f), V(0.6f, 0.6f, 1.1f), 1);
                    mb.Col(d).Box(V(0, 4.55f, 3.75f), V(0.9f, 0.3f, 1.3f));
                    mb.Col(Pal.Cream);
                    for (int i = 0; i < 5; i++) { mb.Cone(V(0.36f, 4.42f, 3.3f + i * 0.2f), 0.05f, -0.16f, 3); mb.Cone(V(-0.36f, 4.42f, 3.3f + i * 0.2f), 0.05f, -0.16f, 3); }
                    mb.Emit(0.6f).Col(Pal.Yellow).Sphere(V(0.48f, 5.15f, 3.5f), 0.08f, 0).Sphere(V(-0.48f, 5.15f, 3.5f), 0.08f, 0);
                    mb.Emit().Col(c).Rod(V(0, 3.2f, -1.8f), V(0, 2.6f, -4.2f), 0.75f, 0.35f, 6).Rod(V(0, 2.6f, -4.2f), V(0, 2.2f, -6.4f), 0.35f, 0.06f, 5);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        mb.Col(d).Rod(V(s * 0.75f, 2.9f, -0.3f), V(s * 0.9f, 1.4f, 0.4f), 0.55f, 0.32f, 6).Rod(V(s * 0.9f, 1.4f, 0.4f), V(s * 0.9f, 0.15f, -0.1f), 0.3f, 0.2f, 6);
                        mb.Box(V(s * 0.9f, 0.1f, 0.35f), V(0.5f, 0.2f, 1.0f));
                        mb.Col(c).Rod(V(s * 0.75f, 3.3f, 1.6f), V(s * 0.85f, 2.8f, 2.1f), 0.12f, 0.08f, 4);
                    }
                });
            Add("dragon", "animals", 4.5f, "Red winged dragon", "fantasy,monster,myth,fire,medieval", "wyvern,drake,fire dragon",
                mb =>
                {
                    Color c = Pal.Hex("#B8382E"), d = Pal.Hex("#6E1E1A");
                    Head h = Quadruped(mb, 4.2f, 1.5f, 1.4f, 1.3f, 0.2f, 2.2f, 50f, 1.2f, 0.7f, 0f, c, d);
                    mb.Col(c).Rod(V(0, 2.3f, -1.9f), V(0, 1.6f, -4.5f), 0.5f, 0.22f, 6).Rod(V(0, 1.6f, -4.5f), V(0, 1.0f, -6.6f), 0.22f, 0.04f, 5);
                    mb.Col(d).Cone(V(0, 0.95f, -6.9f), 0.3f, 0.6f, 4);
                    mb.Col(Pal.Hex("#3A2A28"));
                    mb.Rod(h.Pos + V(0.22f, 0.3f, -0.2f), h.Pos + V(0.4f, 0.85f, -0.6f), 0.08f, 0.02f, 4).Rod(h.Pos + V(-0.22f, 0.3f, -0.2f), h.Pos + V(-0.4f, 0.85f, -0.6f), 0.08f, 0.02f, 4);
                    for (int i = 0; i < 6; i++) mb.Cone(V(0, 2.75f, 1.2f - i * 0.6f), 0.12f, 0.3f, 3);
                    mb.Emit(0.7f).Col(Pal.Yellow).Sphere(h.Pos + V(0.28f, 0.12f, 0.25f), 0.07f, 0).Sphere(h.Pos + V(-0.28f, 0.12f, 0.25f), 0.07f, 0);
                    mb.Emit();
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Vector3 root = V(s * 0.5f, 2.7f, 0.6f), elbow = root + V(s * 2.2f, 1.8f, -0.3f), tip = elbow + V(s * 2.6f, -0.5f, -1.2f);
                        mb.Col(d).Rod(root, elbow, 0.12f, 0.08f, 4).Rod(elbow, tip, 0.08f, 0.03f, 4);
                        mb.Col(Pal.Hex("#D8604A"));
                        mb.TriTwoSided(root, elbow, root + V(s * 0.6f, -0.3f, -2.2f));
                        mb.TriTwoSided(elbow, tip, root + V(s * 0.6f, -0.3f, -2.2f));
                        mb.TriTwoSided(elbow, tip + V(-s * 1.0f, -0.2f, -1.3f), tip);
                    }
                }).Wings();
            Add("unicorn", "animals", 2.3f, "White unicorn with a golden horn", "fantasy,magic,myth,horse,fairy tale", "pegasus,magical horse",
                mb =>
                {
                    Head h = Quadruped(mb, 1.9f, 0.75f, 0.6f, 1.0f, 0.07f, 0.85f, 52f, 0.6f, 0.3f, 0.9f, Pal.White, Pal.Hex("#E8E0F0"), 0.9f);
                    Ears(mb, h, 0.14f, Pal.White);
                    mb.Col(Pal.Pink).Box(h.NeckBase + V(0, 0.5f, 0.12f), V(0.06f, 0.75f, 0.3f));
                    mb.Emit(0.5f).Col(Pal.Gold).Push(h.Pos + V(0, 0.12f, 0.12f), V(35f, 0, 0)).Cone(Vector3.zero, 0.04f, 0.45f, 5).Pop();
                    mb.Emit();
                });

            // ---- people and characters
            Add("person", "people", 1.75f, "A person standing, in a blue shirt", "people,human,crowd,pedestrian", "man,human,guy,people,pedestrian,someone",
                mb => Figure(mb, 1.75f, Pal.Skin, Pal.Blue, Pal.Hex("#3A4252"), Pal.Hex("#3A2A20")));
            Add("woman", "people", 1.68f, "A woman standing, in a red dress", "people,human,crowd,pedestrian", "lady,girl,female",
                mb =>
                {
                    Vector3 head = Figure(mb, 1.68f, Pal.Hex("#D9A078"), Pal.Red, Pal.Red, Pal.Hex("#5A3420"), true);
                    mb.Col(Pal.Hex("#5A3420")).Sphere(head + V(0, -0.14f, -0.08f), V(0.11f, 0.2f, 0.09f), 0);
                });
            Add("child", "people", 1.15f, "A child in a yellow top", "people,human,kid,family", "kid,boy,toddler",
                mb => Figure(mb, 1.15f, Pal.Skin, Pal.Yellow, Pal.Hex("#3E7BFA"), Pal.Hex("#6A4A2A"), false, 1.15f));
            Add("astronaut", "people", 1.85f, "Astronaut in a white spacesuit", "space,moon,nasa,explorer,sci-fi", "spaceman,cosmonaut,space suit",
                mb =>
                {
                    Vector3 head = Figure(mb, 1.85f, Pal.White, Pal.White, Pal.OffWhite, Pal.White, false, 1.3f);
                    mb.Col(Pal.White).Sphere(head, 0.2f, 1);
                    mb.Col(Pal.Gold).Sphere(head + V(0, 0, 0.09f), V(0.15f, 0.13f, 0.14f), 1);
                    mb.Col(Pal.Concrete).Box(head + V(0, -0.45f, -0.25f), V(0.42f, 0.55f, 0.2f));
                    mb.Col(Pal.Red).Box(head + V(0, -0.38f, 0.21f), V(0.14f, 0.1f, 0.02f));
                });
            Add("robot", "people", 1.9f, "Boxy metal robot with glowing eyes", "sci-fi,machine,future,android", "android,droid,bot,mech,cyborg",
                mb =>
                {
                    mb.Shade(0.04f).Col(Pal.Metal).Box(V(0, 1.15f, 0), V(0.75f, 0.8f, 0.45f)).Box(V(0, 1.82f, 0), V(0.5f, 0.42f, 0.42f));
                    mb.Col(Pal.MetalDark).Box(V(0, 0.68f, 0), V(0.5f, 0.16f, 0.34f)).Cyl(V(0, 1.55f, 0), 0.1f, 0.1f, 0.08f, 6);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        mb.Col(Pal.MetalDark).Box(V(s * 0.19f, 0.32f, 0), V(0.2f, 0.62f, 0.24f)).Box(V(s * 0.19f, 0.04f, 0.06f), V(0.24f, 0.08f, 0.4f));
                        mb.Rod(V(s * 0.46f, 1.45f, 0), V(s * 0.56f, 0.85f, 0.1f), 0.08f, 0.07f, 5);
                        mb.Col(Pal.Metal).Sphere(V(s * 0.56f, 0.8f, 0.1f), 0.1f, 0);
                        mb.Emit(1f).Col(Pal.Neon).Box(V(s * 0.12f, 1.86f, 0.215f), V(0.1f, 0.07f, 0.01f));
                        mb.Emit();
                    }
                    mb.Col(Pal.MetalDark).Cyl(V(0, 2.03f, 0), 0.015f, 0.015f, 0.2f, 4);
                    mb.Emit(1f).Col(Pal.Red).Sphere(V(0, 2.25f, 0), 0.04f, 0);
                    mb.Emit(0.6f).Col(Pal.Orange).Box(V(0, 1.2f, 0.23f), V(0.3f, 0.18f, 0.01f));
                    mb.Emit();
                });
            Add("knight", "people", 1.9f, "Knight in steel armour with a shield", "medieval,castle,fantasy,armor,guard", "soldier,warrior,guard,paladin",
                mb =>
                {
                    Vector3 head = Figure(mb, 1.85f, Pal.Steel, Pal.Steel, Pal.Metal, Pal.Steel, false, 1.2f);
                    mb.Col(Pal.MetalDark).Box(head + V(0, 0.02f, 0.11f), V(0.2f, 0.04f, 0.02f));
                    mb.Col(Pal.Red).Cone(head + V(0, 0.13f, -0.02f), 0.04f, 0.26f, 4);
                    mb.Col(Pal.Blue).Push(V(-0.38f, 1.05f, 0.18f), V(90f, 0, 0)).Cyl(Vector3.zero, 0.3f, 0.3f, 0.04f, 6).Pop();
                    mb.Col(Pal.Gold).Box(V(-0.38f, 1.05f, 0.225f), V(0.06f, 0.36f, 0.01f)).Box(V(-0.38f, 1.1f, 0.225f), V(0.28f, 0.06f, 0.01f));
                });
            Add("wizard", "people", 2.1f, "Wizard in a purple robe and pointed hat", "fantasy,magic,medieval,sorcerer", "sorcerer,mage,warlock,witch,merlin",
                mb =>
                {
                    Vector3 head = Figure(mb, 1.8f, Pal.Skin, Pal.Purple, Pal.Purple, Pal.OffWhite, true, 1.1f);
                    mb.Col(Pal.Purple).Cyl(head + V(0, 0.09f, 0), 0.26f, 0.24f, 0.02f, 8).Cone(head + V(0, 0.1f, 0), 0.15f, 0.5f, 7);
                    mb.Col(Pal.OffWhite).Cone(head + V(0, -0.08f, 0.1f), 0.09f, -0.34f, 5);
                    mb.Col(Pal.WoodDark).Cyl(V(0.42f, 0f, 0.15f), 0.02f, 0.025f, 1.9f, 5);
                    mb.Emit(1f).Col(Pal.Cyan).Sphere(V(0.42f, 1.96f, 0.15f), 0.08f, 0);
                    mb.Emit().Glow(V(0.42f, 1.96f, 0.15f), 0.45f, new Color(0.3f, 0.85f, 1f) * 0.7f);
                });
            Add("pirate", "people", 1.85f, "Pirate with a hat and eye patch", "sea,ship,treasure,adventure", "buccaneer,sailor,captain",
                mb =>
                {
                    Vector3 head = Figure(mb, 1.8f, Pal.Skin, Pal.Hex("#8A2A2A"), Pal.Hex("#2A2A30"), Pal.Black);
                    mb.Col(Pal.Black).Box(head + V(0, 0.13f, 0), V(0.4f, 0.06f, 0.2f)).Box(head + V(0, 0.2f, 0), V(0.26f, 0.12f, 0.16f));
                    mb.Box(head + V(0.045f, 0.01f, 0.115f), V(0.07f, 0.05f, 0.01f));
                    mb.Col(Pal.White).Sphere(head + V(0, 0.2f, 0.085f), 0.03f, 0);
                });
            Add("alien", "people", 1.5f, "Green alien with a big head and black eyes", "space,ufo,sci-fi,martian", "martian,extraterrestrial,little green man",
                mb =>
                {
                    Color c = Pal.Hex("#7ED070");
                    mb.Shade(0.04f).Col(c).Lathe(Vector3.zero, Profile(0f, 0.55f, 0.13f, 0.58f, 0.17f, 0.85f, 0.1f, 1.05f, 0f, 1.08f), 7);
                    mb.Sphere(V(0, 1.28f, 0), V(0.24f, 0.28f, 0.24f), 1);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        mb.Col(c).Rod(V(s * 0.08f, 0.6f, 0), V(s * 0.1f, 0.03f, 0), 0.05f, 0.04f, 5).Rod(V(s * 0.16f, 0.98f, 0), V(s * 0.34f, 0.5f, 0.06f), 0.035f, 0.03f, 4);
                        mb.Col(Pal.Black).Sphere(V(s * 0.11f, 1.3f, 0.19f), V(0.07f, 0.1f, 0.04f), 0);
                    }
                });
            Add("ghost", "people", 1.6f, "Pale glowing ghost that floats", "spooky,halloween,haunted,spirit", "spirit,phantom,spectre,spook",
                mb =>
                {
                    mb.Emit(0.35f, 0.6f).Col(Pal.Hex("#E8F4FF"));
                    mb.Lathe(Vector3.zero, Profile(0.42f, 0.25f, 0.4f, 0.8f, 0.34f, 1.25f, 0.2f, 1.52f, 0f, 1.62f), 9);
                    for (int i = 0; i < 9; i++)
                    {
                        float a = i / 9f * Mathf.PI * 2f + 0.35f;
                        mb.Cone(V(Mathf.Cos(a) * 0.34f, 0.27f, Mathf.Sin(a) * 0.34f), 0.11f, -0.26f, 4);
                    }
                    mb.Emit().Col(Pal.Black).Sphere(V(-0.12f, 1.2f, 0.3f), V(0.05f, 0.08f, 0.04f), 0).Sphere(V(0.12f, 1.2f, 0.3f), V(0.05f, 0.08f, 0.04f), 0).Sphere(V(0, 1.03f, 0.34f), V(0.06f, 0.09f, 0.04f), 0);
                    mb.Glow(V(0, 0.9f, 0), 1.3f, new Color(0.6f, 0.8f, 1f) * 0.25f, true);
                }).Flies();
            Add("skeleton", "people", 1.75f, "Bony white skeleton", "spooky,halloween,haunted,undead,pirate", "bones,skull,skeletons",
                mb =>
                {
                    Color b = Pal.Hex("#E8E4D4");
                    mb.Col(b).Sphere(V(0, 1.62f, 0), V(0.1f, 0.12f, 0.11f), 1).Box(V(0, 1.5f, 0.03f), V(0.1f, 0.06f, 0.09f));
                    mb.Rod(V(0, 1.47f, 0), V(0, 0.85f, 0), 0.025f, 0.025f, 4);
                    for (int i = 0; i < 4; i++) mb.Push(V(0, 1.32f - i * 0.1f, 0), V(0, 0, 0), V(1f, 0.5f, 0.7f)).Torus(Vector3.zero, 0.15f - i * 0.012f, 0.014f, 8, 3).Pop();
                    mb.Box(V(0, 0.85f, 0), V(0.26f, 0.1f, 0.12f));
                    foreach (float s in new[] { -1f, 1f })
                    {
                        mb.Rod(V(s * 0.1f, 0.82f, 0), V(s * 0.11f, 0.42f, 0.02f), 0.022f, 0.02f, 4).Rod(V(s * 0.11f, 0.42f, 0.02f), V(s * 0.11f, 0.03f, 0), 0.02f, 0.018f, 4);
                        mb.Rod(V(s * 0.18f, 1.4f, 0), V(s * 0.25f, 1.08f, 0), 0.02f, 0.018f, 4).Rod(V(s * 0.25f, 1.08f, 0), V(s * 0.28f, 0.76f, 0.05f), 0.018f, 0.016f, 4);
                        mb.Box(V(s * 0.11f, 0.02f, 0.05f), V(0.07f, 0.04f, 0.18f));
                    }
                    mb.Col(Pal.Black).Sphere(V(-0.04f, 1.64f, 0.095f), 0.026f, 0).Sphere(V(0.04f, 1.64f, 0.095f), 0.026f, 0);
                });
            Add("zombie", "people", 1.75f, "Green-skinned zombie that shambles towards you and falls over when shot or hit, then another rises", "undead,monster,horror,halloween,spooky,enemy", "zombies,walker,ghoul,undead,walking dead,the undead",
                mb =>
                {
                    Color skin = Pal.Hex("#7FA45C"), rags = Pal.Hex("#5C5346");
                    Vector3 head = Figure(mb, 1.75f, skin, rags, Pal.Hex("#39404A"), Pal.Hex("#2C3324"), false, 1f, true);
                    // torn shirt, a dark mouth, and eyes that glow in the dark
                    mb.Col(Pal.Hex("#3E382F")).Box(V(-0.09f, 1.12f, 0.17f), V(0.12f, 0.16f, 0.03f)).Box(V(0.1f, 0.95f, 0.17f), V(0.09f, 0.1f, 0.03f));
                    mb.Col(Pal.Hex("#2A1616")).Box(head + V(0, -0.065f, 0.1f), V(0.07f, 0.025f, 0.03f));
                    mb.Emit(1f).Col(Pal.Hex("#FFE14A")).Sphere(head + V(-0.039f, 0.006f, 0.105f), 0.021f, 0).Sphere(head + V(0.039f, 0.006f, 0.105f), 0.021f, 0);
                    mb.Emit();
                }).Does("zombie");
            Add("snowman", "people", 1.7f, "Snowman with a carrot nose, scarf and top hat", "winter,snow,christmas,holiday", "frosty,snow man",
                mb =>
                {
                    mb.Shade(0.03f).Col(Pal.Snow).Sphere(V(0, 0.45f, 0), 0.48f, 1).Sphere(V(0, 1.05f, 0), 0.34f, 1).Sphere(V(0, 1.5f, 0), 0.24f, 1);
                    mb.Col(Pal.Orange).Push(V(0, 1.5f, 0.22f), V(90f, 0, 0)).Cone(Vector3.zero, 0.04f, 0.22f, 5).Pop();
                    mb.Col(Pal.Black).Sphere(V(-0.09f, 1.58f, 0.2f), 0.028f, 0).Sphere(V(0.09f, 1.58f, 0.2f), 0.028f, 0);
                    for (int i = 0; i < 3; i++) mb.Sphere(V(0, 0.9f + i * 0.14f, 0.33f - Mathf.Abs(i - 1) * 0.03f), 0.03f, 0);
                    mb.Cyl(V(0, 1.7f, 0), 0.26f, 0.26f, 0.03f, 8).Cyl(V(0, 1.72f, 0), 0.17f, 0.17f, 0.26f, 8);
                    mb.Col(Pal.Red).Torus(V(0, 1.3f, 0), 0.25f, 0.05f, 10, 4).Box(V(0.18f, 1.14f, 0.2f), V(0.1f, 0.3f, 0.04f));
                    mb.Col(Pal.TrunkDark).Rod(V(0.3f, 1.1f, 0), V(0.78f, 1.38f, 0.05f), 0.02f, 0.012f, 4).Rod(V(-0.3f, 1.1f, 0), V(-0.78f, 1.3f, 0.05f), 0.02f, 0.012f, 4);
                });
            Add("scarecrow", "people", 2.1f, "Straw scarecrow on a pole", "farm,field,autumn,halloween", "strawman",
                mb =>
                {
                    mb.Col(Pal.WoodDark).Cyl(Vector3.zero, 0.04f, 0.04f, 2f, 5).Rod(V(-0.8f, 1.5f, 0), V(0.8f, 1.5f, 0), 0.035f, 0.035f, 5);
                    mb.Col(Pal.Hex("#7A5A3A")).Lathe(V(0, 0.85f, 0), Profile(0.24f, 0f, 0.22f, 0.5f, 0.14f, 0.75f, 0f, 0.78f), 6);
                    mb.Rod(V(-0.2f, 1.5f, 0), V(-0.75f, 1.5f, 0), 0.08f, 0.07f, 5).Rod(V(0.2f, 1.5f, 0), V(0.75f, 1.5f, 0), 0.08f, 0.07f, 5);
                    mb.Col(Pal.Hex("#E8D08A")).Sphere(V(0, 1.82f, 0), 0.17f, 1).Sphere(V(-0.8f, 1.5f, 0), 0.07f, 0).Sphere(V(0.8f, 1.5f, 0), 0.07f, 0);
                    mb.Col(Pal.Hex("#8A6A3A")).Cyl(V(0, 1.95f, 0), 0.3f, 0.28f, 0.02f, 8).Cone(V(0, 1.96f, 0), 0.17f, 0.25f, 7);
                    mb.Col(Pal.Black).Sphere(V(-0.06f, 1.84f, 0.15f), 0.02f, 0).Sphere(V(0.06f, 1.84f, 0.15f), 0.02f, 0);
                });
        }
    }
}
