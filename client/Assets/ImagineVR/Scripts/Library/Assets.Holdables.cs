using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    public static partial class Library
    {
        /// <summary>
        /// Things made to be held. Tools are modelled standing upright with the grip at the origin:
        /// in the hand, "up" becomes the direction the controller points.
        /// </summary>
        static partial void RegisterHoldables()
        {
            // ---- golf
            Add("golf_club", "sports", 1.05f, "Iron golf club. Swing it at a golf ball", "golf,sport,club,swing", "club,iron,putter,driver,nine iron",
                mb =>
                {
                    mb.Col(Pal.Black).Cyl(V(0, -0.1f, 0), 0.016f, 0.014f, 0.28f, 6);
                    mb.Col(Pal.Steel).Cyl(V(0, 0.18f, 0), 0.008f, 0.006f, 0.82f, 5, false);
                    mb.Col(Pal.Metal).Push(V(0, 1.0f, 0), V(0, 0, -18f)).Box(V(0.055f, 0.02f, 0), V(0.13f, 0.055f, 0.03f)).Pop();
                }).AsTool().Solid(0.6f, 0.3f);
            Add("golf_ball", "sports", 0.045f, "White golf ball", "golf,sport,ball,small", "ball",
                mb => mb.Col(Pal.White).Sphere(V(0, 0.022f, 0), 0.0225f, 1)).Ball(0.08f, 0.55f);

            // ---- art
            Add("paintbrush", "art", 0.3f, "Paintbrush that paints ribbons of colour in the air. Trigger paints, B changes colour", "art,paint,draw,brush,creative,colour", "brush,paint brush,art brush,pen,pencil,marker,crayon",
                mb =>
                {
                    mb.Col(Pal.Wood).Cyl(V(0, -0.08f, 0), 0.01f, 0.013f, 0.22f, 6);
                    mb.Col(Pal.Steel).Cyl(V(0, 0.14f, 0), 0.014f, 0.016f, 0.05f, 6, false);
                    mb.Col(Pal.White).Lathe(V(0, 0.19f, 0), Profile(0.016f, 0f, 0.02f, 0.03f, 0.012f, 0.08f, 0f, 0.11f), 6);
                }).AsTool().Solid(0.1f, 0.1f).Does("paint");

            // ---- toys with physics
            Add("beach_ball", "toys", 0.5f, "Big light striped beach ball", "ball,beach,toy,bounce,play", "ball,inflatable ball",
                mb =>
                {
                    Color[] stripes = { Pal.Red, Pal.White, Pal.Blue, Pal.White, Pal.Yellow, Pal.White };
                    for (int i = 0; i < 6; i++)
                    {
                        mb.Col(stripes[i]);
                        float a0 = i / 6f * Mathf.PI * 2f, a1 = (i + 1) / 6f * Mathf.PI * 2f;
                        for (int j = 0; j < 4; j++)
                        {
                            float p0 = j / 4f * Mathf.PI, p1 = (j + 1) / 4f * Mathf.PI;
                            Vector3 P(float a, float p) => V(Mathf.Sin(p) * Mathf.Cos(a), Mathf.Cos(p), Mathf.Sin(p) * Mathf.Sin(a)) * 0.25f + V(0, 0.25f, 0);
                            Vector3 q0 = P(a0, p0), q1 = P(a1, p0), q2 = P(a1, p1), q3 = P(a0, p1);
                            Vector3 outside = (q0 + q1 + q2 + q3) * 0.25f - V(0, 0.25f, 0);
                            if (j == 0) mb.Tri(q0, q2, q3, outside); else if (j == 3) mb.Tri(q0, q1, q2, outside); else mb.Quad(q0, q1, q2, q3, outside);
                        }
                    }
                }).Ball(0.12f, 0.8f).Drag = 0.6f;
            Add("balloon", "toys", 0.4f, "Party balloon that floats away and pops when hit", "party,float,toy,birthday,celebration", "balloons,party balloon",
                mb =>
                {
                    mb.Col(Pal.White).Lathe(Vector3.zero, Profile(0f, 0.06f, 0.03f, 0.08f, 0.13f, 0.2f, 0.17f, 0.32f, 0.13f, 0.44f, 0f, 0.5f), 9);
                    mb.Cone(V(0, 0.02f, 0), 0.025f, 0.05f, 5);
                    mb.Col(Pal.OffWhite).Cyl(V(0, -0.5f, 0), 0.003f, 0.003f, 0.52f, 3, false);
                }).Hold().Solid(0.05f, 0.5f, ColliderShape.Sphere).Does("balloon").Drag = 1.2f;

            // ---- magic and gadgets
            Add("magic_wand", "magic", 0.4f, "Magic wand. Hold the trigger and point at loose things to levitate them", "magic,wizard,spell,fantasy,levitate", "wand,wizard wand,fairy wand",
                mb =>
                {
                    mb.Col(Pal.Hex("#3A2A20")).Cyl(V(0, -0.08f, 0), 0.012f, 0.008f, 0.44f, 6);
                    mb.Col(Pal.Gold).Cyl(V(0, 0.02f, 0), 0.014f, 0.014f, 0.02f, 6);
                    mb.Emit(1f).Col(Pal.Hex("#FFE9A0")).Sphere(V(0, 0.38f, 0), 0.02f, 0);
                    mb.Emit().Glow(V(0, 0.38f, 0), 0.12f, new Color(1f, 0.9f, 0.55f) * 0.9f);
                }).AsTool().Solid(0.1f, 0.2f).Does("wand");
            Add("blaster", "toys", 0.28f, "Toy ray gun that fires bouncing balls of light. Pull the trigger", "toy,gun,sci-fi,shoot,laser,weapon", "gun,ray gun,laser gun,toy gun,zapper,space gun,pistol,handgun,rifle,shotgun,weapon",
                mb =>
                {
                    mb.Col(Pal.Hex("#3C4654")).Box(V(0, -0.05f, -0.02f), V(0.035f, 0.11f, 0.05f));
                    mb.Col(Pal.Orange).Box(V(0, 0.08f, 0), V(0.05f, 0.2f, 0.06f));
                    mb.Col(Pal.Hex("#3C4654")).Cyl(V(0, 0.18f, 0), 0.022f, 0.03f, 0.08f, 8);
                    mb.Emit(1f).Col(Pal.Neon).Cyl(V(0, 0.26f, 0), 0.016f, 0.016f, 0.012f, 8);
                    mb.Torus(V(0, 0.12f, 0), 0.034f, 0.006f, 10, 4).Torus(V(0, 0.16f, 0), 0.034f, 0.006f, 10, 4);
                    mb.Emit();
                }).AsTool().Solid(0.3f, 0.2f).Does("blaster");
            Add("energy_ball", "magic", 0.12f, "Small glowing ball of light that bounces", "glow,light,magic,ball,orb", "orb,glowing orb,light ball,plasma ball",
                mb =>
                {
                    mb.Emit(1f).Col(Pal.White).Sphere(V(0, 0.06f, 0), 0.06f, 1);
                    mb.Emit().Glow(V(0, 0.06f, 0), 0.28f, Color.white * 0.6f);
                }).Ball(0.15f, 0.85f);
            Add("bubble_wand", "toys", 0.3f, "Bubble wand. Hold the trigger and wave it to blow bubbles", "bubbles,toy,play,soap", "bubble blower,bubbles",
                mb =>
                {
                    mb.Col(Pal.Pink).Cyl(V(0, -0.08f, 0), 0.01f, 0.01f, 0.3f, 6);
                    mb.Push(V(0, 0.27f, 0), V(90f, 0, 0)).Torus(Vector3.zero, 0.05f, 0.007f, 12, 4).Pop();
                }).AsTool().Solid(0.05f, 0.2f).Does("bubbles");
            Add("confetti_popper", "toys", 0.22f, "Party popper. Pull the trigger for a burst of confetti", "party,celebrate,birthday,confetti", "party popper,confetti cannon,popper",
                mb =>
                {
                    mb.Col(Pal.Purple).Cyl(V(0, -0.06f, 0), 0.018f, 0.045f, 0.24f, 8);
                    mb.Col(Pal.Yellow).Cyl(V(0, 0.02f, 0), 0.03f, 0.033f, 0.02f, 8, false).Cyl(V(0, 0.1f, 0), 0.039f, 0.042f, 0.02f, 8, false);
                }).AsTool().Solid(0.1f, 0.2f).Does("confetti");
            Add("sword", "fantasy", 1f, "Knight's sword", "weapon,knight,medieval,fantasy,blade", "blade,longsword,katana,saber",
                mb =>
                {
                    mb.Col(Pal.WoodDark).Cyl(V(0, -0.1f, 0), 0.016f, 0.016f, 0.2f, 6);
                    mb.Col(Pal.Gold).Sphere(V(0, -0.11f, 0), 0.024f, 0).Box(V(0, 0.11f, 0), V(0.2f, 0.022f, 0.03f));
                    mb.Col(Pal.Steel).Lathe(V(0, 0.12f, 0), Profile(0.03f, 0f, 0.028f, 0.68f, 0f, 0.8f), 4, Mathf.PI / 4f);
                }).AsTool().Solid(0.9f, 0.15f);
        }
    }
}
