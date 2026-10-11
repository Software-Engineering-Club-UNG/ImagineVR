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
        /// <summary>
        /// Guns are drawn the way one looks at them: x across, y up, z along the barrel, the middle
        /// of the grip at the origin. This turns that into a tool's own space, where "up" is the way
        /// the controller points. Pair it with a Pop.
        /// </summary>
        static void GunSpace(MeshBuilder mb) { mb.Push(Vector3.zero, V(-90f, 0, 0)); }

        /// <summary>The raked grip every gun here is held by, its guard and its trigger.</summary>
        static void GunGrip(MeshBuilder mb, Color body, Color panel, float width, float length)
        {
            mb.Push(V(0, 0.012f, 0), V(14f, 0, 0));
            mb.Shade(0.03f).Col(body).BevelBox(V(0, -length * 0.5f, -0.012f), V(width, length, 0.046f), 0.008f);
            mb.Col(panel).BevelBox(V(0, -length * 0.52f, -0.012f), V(width + 0.005f, length * 0.68f, 0.034f), 0.006f);
            mb.Pop();
            mb.Col(Pal.Hex("#1F2024")).Push(V(0, -0.006f, 0.046f), V(0, 0, 90f)).Torus(Vector3.zero, 0.024f, 0.0035f, 12, 4).Pop();
            mb.Push(V(0, -0.002f, 0.04f), V(-20f, 0, 0)).BevelBox(V(0, -0.006f, 0), V(0.007f, 0.024f, 0.007f), 0.002f).Pop();
        }

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
            // ---- guns: toys that fire balls of light, each with its own shape, voice and pace
            Add("blaster", "toys", 0.3f, "Toy sci-fi ray gun with a glowing barrel. Pull the trigger: it fires bouncing balls of light", "toy,gun,sci-fi,shoot,laser,weapon,space", "ray gun,laser gun,toy gun,zapper,space gun,phaser,laser pistol,gun,weapon",
                mb =>
                {
                    Color shell = Pal.Orange, dark = Pal.Hex("#2E3644"), mid = Pal.Hex("#46556A");
                    GunSpace(mb);
                    GunGrip(mb, dark, Pal.Hex("#1F2530"), 0.034f, 0.115f);
                    // the body: a rounded shell with a darker spine and a cap at the back
                    mb.Shade(0.04f).Grad(Pal.Dark(shell, 0.18f), Pal.Light(shell, 0.08f), 0f, 0.075f).BevelBox(V(0, 0.038f, 0.028f), V(0.052f, 0.07f, 0.17f), 0.016f);
                    mb.Col(dark).BevelBox(V(0, 0.08f, 0.015f), V(0.018f, 0.02f, 0.13f), 0.006f);
                    mb.Slab(new[] { new Vector2(-0.05f, 0.088f), new Vector2(-0.075f, 0.118f), new Vector2(-0.02f, 0.088f) }, 0.012f);
                    mb.Push(V(0, 0.038f, -0.057f), V(-90f, 0, 0)).Col(mid).Dome(Vector3.zero, 0.028f, 0.024f, 10, 3).Pop();
                    mb.Col(mid).BevelBox(V(0.028f, 0.04f, 0.02f), V(0.006f, 0.03f, 0.09f), 0.002f).BevelBox(V(-0.028f, 0.04f, 0.02f), V(0.006f, 0.03f, 0.09f), 0.002f);
                    // the barrel: flared, ringed, with the light it fires showing at the tip
                    mb.Push(V(0, 0.04f, 0.108f), V(90f, 0, 0));
                    mb.Col(dark).Lathe(Vector3.zero, Profile(0.027f, 0f, 0.031f, 0.018f, 0.021f, 0.03f, 0.021f, 0.085f, 0.031f, 0.097f, 0.031f, 0.115f, 0.019f, 0.125f, 0f, 0.125f), 12);
                    mb.Emit(1f).Col(Pal.Neon).Torus(V(0, 0.045f, 0), 0.026f, 0.0055f, 14, 5).Torus(V(0, 0.066f, 0), 0.026f, 0.0055f, 14, 5);
                    mb.Sphere(V(0, 0.126f, 0), 0.015f, 1);
                    mb.Emit().Glow(V(0, 0.13f, 0), 0.07f, new Color(0.3f, 1f, 0.92f) * 0.7f);
                    mb.Pop();
                    // the power cell slung underneath
                    mb.Emit(0.7f).Col(Pal.Hex("#7CF7E8")).Rod(V(0, -0.006f, 0.05f), V(0, -0.006f, 0.105f), 0.013f, 0.013f, 8);
                    mb.Emit().Col(dark).Rod(V(0, -0.006f, 0.04f), V(0, -0.006f, 0.052f), 0.016f, 0.016f, 8).Rod(V(0, -0.006f, 0.103f), V(0, -0.006f, 0.115f), 0.016f, 0.016f, 8);
                    mb.Pop();
                }).AsTool().Solid(0.3f, 0.2f).Gun(V(0, 0.04f, 0.25f), "zap", 0.15f);

            Add("pistol", "toys", 0.2f, "Black toy handgun with a slide and sights. Pull the trigger: it fires balls of light", "toy,gun,handgun,shoot,weapon,police,spy,western", "handgun,glock,revolver,sidearm,9mm,desert eagle,colt,magnum,toy pistol",
                mb =>
                {
                    Color steel = Pal.Hex("#2B2D33"), frame = Pal.Hex("#3A3D45");
                    GunSpace(mb);
                    GunGrip(mb, frame, Pal.Hex("#5A3B24"), 0.03f, 0.11f);
                    mb.Shade(0.03f).Col(frame).BevelBox(V(0, 0.022f, 0.04f), V(0.029f, 0.026f, 0.15f), 0.005f);
                    mb.Grad(steel, Pal.Light(steel, 0.12f), 0.03f, 0.066f).BevelBox(V(0, 0.049f, 0.03f), V(0.032f, 0.034f, 0.195f), 0.006f);
                    // cuts in the back of the slide to rack it by, the port a spent case leaves through
                    for (int i = 0; i < 5; i++) mb.Col(Pal.Hex("#18191D")).Box(V(0, 0.05f, -0.056f + i * 0.008f), V(0.034f, 0.024f, 0.003f));
                    mb.Col(Pal.Hex("#6A6E78")).Box(V(0.0165f, 0.055f, 0.04f), V(0.002f, 0.014f, 0.035f));
                    mb.Col(Pal.Hex("#15161A")).Rod(V(0, 0.049f, 0.12f), V(0, 0.049f, 0.134f), 0.009f, 0.009f, 8);
                    mb.Col(steel).Box(V(0, 0.07f, 0.116f), V(0.005f, 0.009f, 0.009f)).Box(V(-0.008f, 0.07f, -0.058f), V(0.005f, 0.009f, 0.007f)).Box(V(0.008f, 0.07f, -0.058f), V(0.005f, 0.009f, 0.007f));
                    mb.Col(frame).BevelBox(V(0, 0.012f, 0.1f), V(0.022f, 0.012f, 0.03f), 0.003f);
                    mb.Pop();
                }).AsTool().Solid(0.3f, 0.2f).Gun(V(0, 0.049f, 0.14f), "gunshot", 0.22f, false, 26f, 0.7f).Shoots(new Color(1f, 0.85f, 0.4f));

            Add("rifle", "toys", 0.9f, "Toy assault rifle in the AK-47 pattern: wooden stock and handguard, curved magazine. Hold the trigger for a stream of shots", "toy,gun,rifle,ak47,shoot,weapon,army,soldier,military", "ak47,ak 47,ak,assault rifle,machine gun,kalashnikov,m16,m4,carbine,automatic rifle,sniper rifle,sniper,toy rifle,long gun,smg,submachine gun",
                mb =>
                {
                    Color steel = Pal.Hex("#2A2B30"), worn = Pal.Hex("#3B3D44"), wood = Pal.Hex("#8A5630"), woodDark = Pal.Hex("#6B4023");
                    GunSpace(mb);
                    GunGrip(mb, wood, woodDark, 0.032f, 0.105f);
                    // receiver and the rounded cover on top of it
                    mb.Shade(0.035f).Grad(steel, worn, 0.02f, 0.09f).BevelBox(V(0, 0.055f, 0.055f), V(0.044f, 0.07f, 0.31f), 0.007f);
                    mb.Col(worn).BevelBox(V(0, 0.097f, 0.03f), V(0.04f, 0.024f, 0.25f), 0.011f);
                    mb.Col(steel).Rod(V(0.02f, 0.078f, 0.09f), V(0.042f, 0.078f, 0.09f), 0.005f, 0.005f, 6).Box(V(0.0225f, 0.06f, 0.0f), V(0.002f, 0.012f, 0.06f));
                    mb.Col(steel).BevelBox(V(0, 0.115f, 0.18f), V(0.022f, 0.012f, 0.06f), 0.003f);
                    // the stock, cut from a board, with a steel plate on its end
                    mb.Grad(woodDark, wood, -0.03f, 0.09f).Slab(new[]
                    {
                        new Vector2(-0.1f, 0.088f), new Vector2(-0.1f, 0.03f), new Vector2(-0.19f, 0.012f), new Vector2(-0.37f, -0.032f),
                        new Vector2(-0.385f, -0.02f), new Vector2(-0.385f, 0.074f), new Vector2(-0.36f, 0.084f),
                    }, 0.036f);
                    mb.Col(steel).BevelBox(V(0, 0.026f, -0.389f), V(0.04f, 0.108f, 0.01f), 0.003f);
                    // wooden furniture round the barrel, lower and upper
                    mb.Grad(woodDark, wood, 0.02f, 0.08f).BevelBox(V(0, 0.048f, 0.31f), V(0.05f, 0.05f, 0.2f), 0.012f);
                    mb.Col(wood).BevelBox(V(0, 0.092f, 0.3f), V(0.04f, 0.03f, 0.17f), 0.012f);
                    for (int i = 0; i < 3; i++) mb.Col(woodDark).Box(V(0, 0.048f, 0.25f + i * 0.045f), V(0.052f, 0.012f, 0.022f));
                    // barrel, gas tube over it, the block that joins them, front sight and muzzle
                    mb.Col(Pal.Hex("#1B1C20")).Rod(V(0, 0.062f, 0.2f), V(0, 0.062f, 0.61f), 0.0105f, 0.0105f, 8);
                    mb.Col(steel).Rod(V(0, 0.094f, 0.38f), V(0, 0.094f, 0.45f), 0.009f, 0.009f, 8);
                    mb.Col(worn).BevelBox(V(0, 0.078f, 0.455f), V(0.028f, 0.052f, 0.03f), 0.004f);
                    mb.Col(steel).Slab(new[] { new Vector2(0.535f, 0.07f), new Vector2(0.565f, 0.07f), new Vector2(0.558f, 0.128f), new Vector2(0.542f, 0.128f) }, 0.014f);
                    mb.Box(V(-0.012f, 0.115f, 0.55f), V(0.003f, 0.03f, 0.018f)).Box(V(0.012f, 0.115f, 0.55f), V(0.003f, 0.03f, 0.018f));
                    mb.Col(Pal.Hex("#1B1C20")).Rod(V(0, 0.062f, 0.61f), V(0, 0.062f, 0.655f), 0.015f, 0.013f, 8);
                    // the curved magazine
                    mb.Shade(0.05f).Grad(Pal.Hex("#1E1F23"), Pal.Hex("#34363D"), -0.19f, 0.02f).Slab(new[]
                    {
                        new Vector2(0.062f, 0.022f), new Vector2(0.125f, 0.022f), new Vector2(0.15f, -0.05f), new Vector2(0.185f, -0.115f),
                        new Vector2(0.215f, -0.165f), new Vector2(0.165f, -0.195f), new Vector2(0.13f, -0.135f), new Vector2(0.095f, -0.06f),
                    }, 0.03f);
                    mb.Pop();
                }).AsTool().Solid(0.6f, 0.2f).Gun(V(0, 0.062f, 0.67f), "gunshot", 0.1f, true, 34f, 0.55f).Shoots(new Color(1f, 0.8f, 0.35f));

            Add("shotgun", "toys", 0.85f, "Toy pump-action shotgun with a wooden stock. Each pull of the trigger fires a spread of balls of light", "toy,gun,shotgun,shoot,weapon,western,hunter,farm", "pump shotgun,pump action,boomstick,12 gauge,double barrel,sawed off,toy shotgun,musket,blunderbuss",
                mb =>
                {
                    Color steel = Pal.Hex("#2C2D32"), wood = Pal.Hex("#7A4A28"), woodDark = Pal.Hex("#5A3419");
                    GunSpace(mb);
                    GunGrip(mb, wood, woodDark, 0.032f, 0.1f);
                    mb.Shade(0.035f).Grad(steel, Pal.Hex("#44464E"), 0.02f, 0.09f).BevelBox(V(0, 0.055f, 0.06f), V(0.042f, 0.068f, 0.25f), 0.01f);
                    mb.Col(Pal.Hex("#15161A")).Box(V(0.0215f, 0.06f, 0.07f), V(0.002f, 0.018f, 0.07f));
                    mb.Grad(woodDark, wood, -0.04f, 0.09f).Slab(new[]
                    {
                        new Vector2(-0.065f, 0.085f), new Vector2(-0.065f, 0.03f), new Vector2(-0.16f, 0.005f), new Vector2(-0.34f, -0.045f),
                        new Vector2(-0.36f, -0.03f), new Vector2(-0.36f, 0.06f), new Vector2(-0.33f, 0.072f),
                    }, 0.038f);
                    mb.Col(Pal.Hex("#1C1D20")).BevelBox(V(0, 0.014f, -0.365f), V(0.042f, 0.11f, 0.012f), 0.004f);
                    // barrel over magazine tube, the forend that slides along it, and a bead to aim by
                    mb.Col(Pal.Hex("#1B1C20")).Rod(V(0, 0.072f, 0.18f), V(0, 0.072f, 0.66f), 0.013f, 0.013f, 8);
                    mb.Col(steel).Rod(V(0, 0.044f, 0.18f), V(0, 0.044f, 0.56f), 0.0115f, 0.0115f, 8);
                    mb.Col(steel).BevelBox(V(0, 0.058f, 0.555f), V(0.022f, 0.05f, 0.018f), 0.003f);
                    mb.Grad(woodDark, wood, 0.02f, 0.07f).BevelBox(V(0, 0.044f, 0.36f), V(0.048f, 0.046f, 0.15f), 0.014f);
                    for (int i = 0; i < 5; i++) mb.Col(woodDark).Box(V(0, 0.044f, 0.305f + i * 0.027f), V(0.05f, 0.03f, 0.007f));
                    mb.Col(Pal.Gold).Sphere(V(0, 0.089f, 0.65f), 0.005f, 0);
                    mb.Pop();
                }).AsTool().Solid(0.7f, 0.2f).Gun(V(0, 0.072f, 0.68f), "boom", 0.6f, false, 24f, 0.5f, 7, 5f).Shoots(new Color(1f, 0.75f, 0.35f));
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
