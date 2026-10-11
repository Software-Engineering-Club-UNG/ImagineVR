using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    public static partial class Library
    {
        static void Ball(string id, float diameter, string desc, string tags, string aliases, Color color, float mass, float bounce, System.Action<MeshBuilder, float> detail = null)
        {
            float r = diameter * 0.5f;
            Add(id, "sports", diameter, desc, tags, aliases, mb =>
            {
                mb.Col(color).Sphere(V(0, r, 0), r, diameter > 0.15f ? 2 : 1);
                detail?.Invoke(mb, r);
            }).Ball(mass, bounce);
        }

        static partial void RegisterMoreHoldables()
        {
            // ---- balls
            Ball("basketball", 0.24f, "Orange basketball", "ball,sport,bounce,hoop,court", "ball", Pal.Hex("#E0782A"), 0.6f, 0.8f, (mb, r) =>
            {
                mb.Col(Pal.Black).Push(V(0, r, 0)).Torus(Vector3.zero, r * 1.005f, r * 0.03f, 16, 3).Pop();
                mb.Push(V(0, r, 0), V(90f, 0, 0)).Torus(Vector3.zero, r * 1.005f, r * 0.03f, 16, 3).Pop();
            });
            Ball("soccer_ball", 0.22f, "Black and white football", "ball,sport,football,kick,pitch", "football,ball", Pal.White, 0.43f, 0.7f, (mb, r) =>
            {
                mb.Col(Pal.Black);
                Vector3[] spots = { Vector3.up, Vector3.down, V(1, 0.4f, 0), V(-1, 0.4f, 0), V(0, 0.4f, 1), V(0, 0.4f, -1), V(0.7f, -0.5f, 0.7f), V(-0.7f, -0.5f, -0.7f) };
                foreach (Vector3 s in spots) mb.Sphere(V(0, r, 0) + s.normalized * r * 0.86f, r * 0.26f, 0);
            });
            Ball("baseball", 0.074f, "White baseball", "ball,sport,throw,catch,bat", "ball,softball", Pal.White, 0.15f, 0.5f);
            Ball("tennis_ball", 0.067f, "Fuzzy yellow tennis ball", "ball,sport,bounce,racket", "ball", Pal.Hex("#D8E84A"), 0.06f, 0.75f);
            Ball("snowball", 0.12f, "Packed snowball, made for throwing", "snow,winter,throw,fight", "snow ball", Pal.Snow, 0.15f, 0.05f);
            Add("football", "sports", 0.18f, "American football", "ball,sport,nfl,throw", "american football,rugby ball,pigskin",
                mb =>
                {
                    mb.Col(Pal.Hex("#8A4A2A")).Sphere(V(0, 0.09f, 0), V(0.09f, 0.09f, 0.15f), 2);
                    mb.Col(Pal.White).Box(V(0, 0.18f, 0), V(0.012f, 0.006f, 0.1f));
                }).Ball(0.42f, 0.5f);
            Add("frisbee", "sports", 0.03f, "Flying disc", "throw,catch,park,beach,toy", "flying disc,disc",
                mb => mb.Col(Pal.Hex("#3E9AD8")).Lathe(Vector3.zero, Profile(0f, 0.02f, 0.11f, 0.025f, 0.135f, 0.015f, 0.135f, 0f), 14)).Hold().Solid(0.17f, 0.3f).Drag = 0.9f;
            Add("rubber_duck", "toys", 0.14f, "Yellow rubber duck", "toy,bath,duck,cute", "duck,duckling,bath toy",
                mb =>
                {
                    mb.Col(Pal.Yellow).Sphere(V(0, 0.055f, 0), V(0.06f, 0.055f, 0.08f), 1).Sphere(V(0, 0.12f, 0.035f), 0.042f, 1).Cone(V(0, 0.07f, -0.07f), 0.03f, 0.05f, 4);
                    mb.Col(Pal.Orange).Box(V(0, 0.115f, 0.08f), V(0.035f, 0.012f, 0.035f));
                    mb.Col(Pal.Black).Sphere(V(0.025f, 0.135f, 0.065f), 0.007f, 0).Sphere(V(-0.025f, 0.135f, 0.065f), 0.007f, 0);
                }).Hold().Solid(0.1f, 0.5f, ColliderShape.Sphere).Floats();
            Add("teddy_bear", "toys", 0.4f, "Soft brown teddy bear", "toy,cute,soft,gift,child", "teddy,stuffed animal,plush,bear toy",
                mb =>
                {
                    Color c = Pal.Hex("#A87848");
                    mb.Shade(0.05f).Col(c).Sphere(V(0, 0.15f, 0), V(0.12f, 0.14f, 0.1f), 1).Sphere(V(0, 0.32f, 0), 0.09f, 1);
                    mb.Sphere(V(-0.07f, 0.39f, 0), 0.035f, 0).Sphere(V(0.07f, 0.39f, 0), 0.035f, 0);
                    foreach (float s in new[] { -1f, 1f }) { mb.Sphere(V(s * 0.13f, 0.2f, 0.03f), V(0.04f, 0.07f, 0.04f), 0).Sphere(V(s * 0.07f, 0.03f, 0.06f), V(0.045f, 0.04f, 0.07f), 0); }
                    mb.Col(Pal.Cream).Sphere(V(0, 0.3f, 0.075f), V(0.04f, 0.035f, 0.03f), 0);
                    mb.Col(Pal.Black).Sphere(V(0, 0.31f, 0.1f), 0.012f, 0).Sphere(V(-0.035f, 0.34f, 0.08f), 0.01f, 0).Sphere(V(0.035f, 0.34f, 0.08f), 0.01f, 0);
                }).Hold().Solid(0.3f, 0.2f);
            Add("dice", "toys", 0.1f, "Large white die", "game,toy,roll,chance", "die,dice cube",
                mb =>
                {
                    mb.Col(Pal.White).BoxOn(Vector3.zero, V(0.1f, 0.1f, 0.1f));
                    mb.Col(Pal.Black).Sphere(V(0, 0.1f, 0), 0.012f, 0).Sphere(V(0.03f, 0.05f, 0.05f), 0.012f, 0).Sphere(V(-0.03f, 0.05f, 0.05f), 0.012f, 0);
                    mb.Sphere(V(0.05f, 0.08f, 0.03f), 0.012f, 0).Sphere(V(0.05f, 0.05f, 0), 0.012f, 0).Sphere(V(0.05f, 0.02f, -0.03f), 0.012f, 0);
                }).Hold().Solid(0.1f, 0.4f);

            // ---- sticks and swingers
            Add("baseball_bat", "sports", 0.85f, "Wooden baseball bat", "sport,swing,hit,bat", "bat,club,cricket bat",
                mb =>
                {
                    mb.Col(Pal.WoodLight).Lathe(V(0, -0.1f, 0), Profile(0f, 0f, 0.022f, 0.01f, 0.014f, 0.04f, 0.016f, 0.3f, 0.032f, 0.65f, 0.034f, 0.9f, 0f, 0.94f), 8);
                    mb.Col(Pal.Black).Cyl(V(0, -0.08f, 0), 0.0165f, 0.0165f, 0.2f, 8, false);
                }).AsTool().Solid(0.9f, 0.3f);
            Add("tennis_racket", "sports", 0.68f, "Tennis racket", "sport,tennis,swing,hit", "racquet,racket,badminton racket",
                mb =>
                {
                    mb.Col(Pal.Black).Cyl(V(0, -0.1f, 0), 0.016f, 0.016f, 0.22f, 6);
                    mb.Col(Pal.Red).Cyl(V(0, 0.12f, 0), 0.012f, 0.012f, 0.14f, 5);
                    mb.Push(V(0, 0.42f, 0), V(90f, 0, 0), V(0.78f, 1f, 1f)).Torus(Vector3.zero, 0.15f, 0.011f, 16, 4);
                    mb.Col(Pal.OffWhite).Cyl(V(0, -0.002f, 0), 0.145f, 0.145f, 0.004f, 14).Pop();
                }).AsTool().Solid(0.35f, 0.6f);
            Add("hammer", "tools", 0.34f, "Claw hammer", "tool,build,hit,workshop", "mallet,sledgehammer",
                mb =>
                {
                    mb.Col(Pal.Wood).Cyl(V(0, -0.1f, 0), 0.014f, 0.012f, 0.36f, 6);
                    mb.Col(Pal.MetalDark).Box(V(0.015f, 0.27f, 0), V(0.11f, 0.04f, 0.035f));
                }).AsTool().Solid(0.8f, 0.2f);
            Add("axe", "tools", 0.7f, "Woodcutter's axe", "tool,wood,chop,forest,viking", "hatchet,battle axe",
                mb =>
                {
                    mb.Col(Pal.Wood).Cyl(V(0, -0.1f, 0), 0.016f, 0.014f, 0.7f, 6);
                    mb.Col(Pal.Steel).Prism(new List<Vector2> { new Vector2(0.01f, -0.012f), new Vector2(0.14f, -0.002f), new Vector2(0.14f, 0.002f), new Vector2(0.01f, 0.012f) }, 0.44f, 0.6f);
                }).AsTool().Solid(1.2f, 0.15f);
            Add("pickaxe", "tools", 0.8f, "Miner's pickaxe", "tool,mine,dig,cave,rock", "pick,mining pick",
                mb =>
                {
                    mb.Col(Pal.Wood).Cyl(V(0, -0.1f, 0), 0.016f, 0.014f, 0.8f, 6);
                    mb.Col(Pal.MetalDark).Rod(V(0, 0.68f, 0), V(0.3f, 0.6f, 0), 0.022f, 0.006f, 5).Rod(V(0, 0.68f, 0), V(-0.3f, 0.6f, 0), 0.022f, 0.006f, 5);
                }).AsTool().Solid(1.4f, 0.15f);
            Add("shovel", "tools", 1f, "Garden spade", "tool,dig,garden,beach,sand", "spade,trowel",
                mb =>
                {
                    mb.Col(Pal.Wood).Cyl(V(0, -0.1f, 0), 0.015f, 0.015f, 0.8f, 6);
                    mb.Col(Pal.Steel).Box(V(0, 0.84f, 0), V(0.18f, 0.28f, 0.012f));
                }).AsTool().Solid(1.1f, 0.15f);
            Add("fishing_rod", "tools", 1.8f, "Fishing rod with a float", "fish,lake,sea,relax,catch", "rod,fishing pole",
                mb =>
                {
                    mb.Col(Pal.Black).Cyl(V(0, -0.1f, 0), 0.014f, 0.012f, 0.3f, 6);
                    mb.Col(Pal.Hex("#3A6A4A")).Cyl(V(0, 0.2f, 0), 0.008f, 0.003f, 1.6f, 5, false);
                    mb.Col(Pal.Metal).Push(V(0.03f, 0.22f, 0), V(0, 0, 90f)).Cyl(V(0, -0.015f, 0), 0.025f, 0.025f, 0.03f, 8).Pop();
                }).AsTool().Solid(0.3f, 0.2f);
            Add("umbrella", "props", 0.9f, "Open umbrella", "rain,weather,shade,walk", "parasol,brolly",
                mb =>
                {
                    mb.Col(Pal.MetalDark).Cyl(V(0, -0.1f, 0), 0.008f, 0.008f, 0.9f, 5);
                    mb.Col(Pal.WoodDark).Cyl(V(0, -0.1f, 0), 0.014f, 0.014f, 0.12f, 6);
                    mb.Col(Pal.Hex("#C0304A"));
                    for (int i = 0; i < 8; i++)
                    {
                        float a0 = i / 8f * Mathf.PI * 2f, a1 = (i + 1) / 8f * Mathf.PI * 2f;
                        mb.TriTwoSided(V(0, 0.8f, 0), V(Mathf.Cos(a0) * 0.5f, 0.6f, Mathf.Sin(a0) * 0.5f), V(Mathf.Cos(a1) * 0.5f, 0.6f, Mathf.Sin(a1) * 0.5f));
                    }
                }).AsTool().Solid(0.4f, 0.2f).Drag = 1.5f;
            Add("flashlight", "tools", 0.2f, "Hand torch with a bright lens", "light,torch,night,explore,cave", "torch,hand torch",
                mb =>
                {
                    mb.Col(Pal.Hex("#2A2C30")).Cyl(V(0, -0.08f, 0), 0.018f, 0.018f, 0.16f, 8).Cyl(V(0, 0.08f, 0), 0.018f, 0.03f, 0.05f, 8);
                    mb.Emit(1f).Col(Pal.Cream).Cyl(V(0, 0.13f, 0), 0.028f, 0.028f, 0.004f, 8);
                    mb.Emit().Glow(V(0, 0.16f, 0), 0.45f, new Color(1f, 0.95f, 0.8f) * 0.9f);
                }).AsTool().Solid(0.2f, 0.2f);
            Add("lantern", "camp", 0.3f, "Glowing camping lantern", "light,camp,night,cozy,explore", "oil lamp,hurricane lamp,lamp",
                mb =>
                {
                    mb.Col(Pal.MetalDark).Cyl(Vector3.zero, 0.07f, 0.07f, 0.04f, 8).Cyl(V(0, 0.2f, 0), 0.07f, 0.04f, 0.05f, 8);
                    mb.Emit(1f).Col(Pal.FireCore).Cyl(V(0, 0.04f, 0), 0.05f, 0.05f, 0.16f, 8, false);
                    mb.Emit().Col(Pal.MetalDark).Push(V(0, 0.27f, 0), V(90f, 0, 0)).Torus(Vector3.zero, 0.05f, 0.005f, 10, 3).Pop();
                    mb.Glow(V(0, 0.12f, 0), 0.9f, new Color(1f, 0.75f, 0.4f) * 0.7f);
                }).Hold().Solid(0.4f, 0.2f);
            Add("shield", "fantasy", 0.7f, "Round knight's shield", "knight,medieval,defend,fantasy,viking", "buckler",
                mb =>
                {
                    mb.Push(V(0, 0, 0), V(90f, 0, 0));
                    mb.Col(Pal.Blue).Cyl(V(0, -0.02f, 0), 0.34f, 0.34f, 0.04f, 14);
                    mb.Col(Pal.Gold).Torus(Vector3.zero, 0.34f, 0.022f, 14, 4).Sphere(V(0, 0.03f, 0), V(0.07f, 0.04f, 0.07f), 1);
                    mb.Pop();
                }).Hold().Solid(1.5f, 0.3f);
            Add("crown", "fantasy", 0.14f, "Golden crown with jewels", "king,queen,royal,gold,treasure", "tiara,diadem",
                mb =>
                {
                    mb.Col(Pal.Gold).Cyl(Vector3.zero, 0.1f, 0.1f, 0.06f, 10, false);
                    for (int i = 0; i < 5; i++) { float a = i / 5f * Mathf.PI * 2f; mb.Cone(V(Mathf.Cos(a) * 0.1f, 0.06f, Mathf.Sin(a) * 0.1f), 0.025f, 0.07f, 4); }
                    mb.Emit(0.5f).Col(Pal.Red).Sphere(V(0, 0.035f, 0.1f), 0.014f, 0).Sphere(V(0.095f, 0.035f, 0.03f), 0.014f, 0).Sphere(V(-0.095f, 0.035f, 0.03f), 0.014f, 0);
                    mb.Emit();
                }).Hold().Solid(0.5f, 0.2f);
            Add("trophy", "sports", 0.32f, "Golden winner's cup", "win,champion,gold,prize,award", "cup,award,prize",
                mb =>
                {
                    mb.Col(Pal.WoodDark).BoxOn(Vector3.zero, V(0.12f, 0.04f, 0.12f));
                    mb.Col(Pal.Gold).Lathe(V(0, 0.04f, 0), Profile(0.05f, 0f, 0.015f, 0.03f, 0.015f, 0.1f, 0.07f, 0.16f, 0.09f, 0.28f), 10);
                    foreach (float s in new[] { -1f, 1f }) mb.Push(V(s * 0.1f, 0.24f, 0), V(90f, 0, 0), V(0.6f, 1f, 1f)).Torus(Vector3.zero, 0.04f, 0.008f, 8, 3).Pop();
                }).Hold().Solid(0.8f, 0.2f);

            // ---- music you can hit
            Add("hand_bell", "music", 0.2f, "Brass hand bell that rings when it hits something", "music,ring,sound,instrument", "bell,school bell",
                mb =>
                {
                    mb.Col(Pal.WoodDark).Cyl(V(0, -0.08f, 0), 0.014f, 0.014f, 0.14f, 6);
                    mb.Col(Pal.Gold).Lathe(V(0, 0.06f, 0), Profile(0.02f, 0f, 0.035f, 0.02f, 0.05f, 0.1f, 0.075f, 0.14f), 10);
                }).AsTool().Solid(0.4f, 0.3f).Does("bell", 1.6f);
            Add("drum", "music", 0.5f, "Big drum that booms when struck", "music,beat,sound,instrument,percussion", "bongo,tom,snare,drums",
                mb =>
                {
                    mb.Col(Pal.Red).Cyl(Vector3.zero, 0.3f, 0.3f, 0.5f, 12, false);
                    mb.Col(Pal.Cream).Cyl(V(0, 0.5f, 0), 0.3f, 0.3f, 0.01f, 12);
                    mb.Col(Pal.Gold).Cyl(V(0, 0.46f, 0), 0.31f, 0.31f, 0.04f, 12, false).Cyl(Vector3.zero, 0.31f, 0.31f, 0.04f, 12, false);
                }).Solid(6f, 0.5f, ColliderShape.Capsule).Does("drum", 1f);
            Add("gong", "music", 1.6f, "Hanging bronze gong", "music,temple,asia,sound,ring", "cymbal,bronze gong",
                mb =>
                {
                    mb.Col(Pal.WoodDark).BoxOn(V(-0.7f, 0, 0), V(0.08f, 1.6f, 0.08f)).BoxOn(V(0.7f, 0, 0), V(0.08f, 1.6f, 0.08f)).Box(V(0, 1.6f, 0), V(1.6f, 0.08f, 0.08f));
                    mb.Col(Pal.Gold).Push(V(0, 0.85f, 0), V(90f, 0, 0)).Cyl(V(0, -0.015f, 0), 0.5f, 0.5f, 0.03f, 16).Pop();
                    mb.Col(Pal.Hex("#B88A2A")).Push(V(0, 0.85f, 0.02f), V(90f, 0, 0)).Cyl(Vector3.zero, 0.14f, 0.1f, 0.03f, 10).Pop();
                }).Solid(15f, 0.3f).Does("bell", 0.45f);
            Add("drumstick", "music", 0.4f, "Wooden drumstick", "music,beat,percussion,hit", "stick,mallet",
                mb => mb.Col(Pal.WoodLight).Cyl(V(0, -0.08f, 0), 0.009f, 0.006f, 0.4f, 6).Sphere(V(0, 0.33f, 0), 0.011f, 0)).AsTool().Solid(0.1f, 0.4f);
            Add("guitar", "music", 1f, "Acoustic guitar", "music,instrument,strum,song,campfire", "acoustic guitar,ukulele,banjo",
                mb =>
                {
                    mb.Col(Pal.Hex("#C88A4A")).Push(V(0, -0.2f, 0), V(90f, 0, 0)).Cyl(V(0, -0.045f, 0), 0.19f, 0.19f, 0.09f, 12).Pop();
                    mb.Push(V(0, 0.05f, 0), V(90f, 0, 0)).Cyl(V(0, -0.045f, 0), 0.14f, 0.14f, 0.09f, 12).Pop();
                    mb.Col(Pal.Black).Push(V(0, -0.02f, 0.046f), V(90f, 0, 0)).Cyl(Vector3.zero, 0.05f, 0.05f, 0.002f, 10).Pop();
                    mb.Col(Pal.WoodDark).Box(V(0, 0.45f, 0.03f), V(0.05f, 0.6f, 0.025f)).Box(V(0, 0.8f, 0.03f), V(0.07f, 0.12f, 0.03f));
                }).Hold().Solid(1.5f, 0.2f).Does("chime", 0.6f);
            Add("microphone", "music", 0.22f, "Stage microphone", "music,sing,stage,karaoke,speech", "mic",
                mb =>
                {
                    mb.Col(Pal.Hex("#2A2C30")).Cyl(V(0, -0.08f, 0), 0.016f, 0.02f, 0.2f, 8);
                    mb.Col(Pal.Metal).Sphere(V(0, 0.15f, 0), 0.034f, 1);
                }).AsTool().Solid(0.3f, 0.2f);

            // ---- food and small things
            Add("apple", "food", 0.08f, "Red apple", "fruit,food,snack,healthy", "fruit",
                mb => { mb.Col(Pal.Red).Sphere(V(0, 0.04f, 0), V(0.04f, 0.038f, 0.04f), 1); mb.Col(Pal.TrunkDark).Cyl(V(0, 0.07f, 0), 0.003f, 0.003f, 0.025f, 3); }).Ball(0.15f, 0.2f).Does("");
            Add("banana", "food", 0.18f, "Yellow banana", "fruit,food,snack,monkey", "fruit",
                mb =>
                {
                    mb.Col(Pal.Yellow);
                    for (int i = 0; i < 5; i++) { float a0 = -0.6f + i * 0.3f, a1 = a0 + 0.3f; mb.Rod(V(Mathf.Sin(a0) * 0.1f, 0.1f - Mathf.Cos(a0) * 0.1f + 0.02f, 0), V(Mathf.Sin(a1) * 0.1f, 0.1f - Mathf.Cos(a1) * 0.1f + 0.02f, 0), 0.017f, 0.017f, 5, i == 0 || i == 4); }
                }).Hold().Solid(0.12f, 0.1f);
            Add("burger", "food", 0.1f, "Cheeseburger", "food,fast food,lunch,snack", "hamburger,cheeseburger,sandwich",
                mb =>
                {
                    mb.Col(Pal.Hex("#D8A058")).Cyl(Vector3.zero, 0.055f, 0.06f, 0.025f, 10).Lathe(V(0, 0.06f, 0), Profile(0.06f, 0f, 0.055f, 0.025f, 0f, 0.045f), 10);
                    mb.Col(Pal.Hex("#5A3020")).Cyl(V(0, 0.025f, 0), 0.062f, 0.062f, 0.02f, 10);
                    mb.Col(Pal.Yellow).BoxOn(V(0, 0.045f, 0), V(0.115f, 0.006f, 0.115f));
                    mb.Col(Pal.Green).Cyl(V(0, 0.051f, 0), 0.066f, 0.066f, 0.008f, 9);
                }).Hold().Solid(0.2f, 0.1f);
            Add("mug", "props", 0.1f, "Coffee mug", "drink,coffee,tea,cup,kitchen", "cup,coffee cup,teacup",
                mb =>
                {
                    mb.Col(Pal.White).Cyl(Vector3.zero, 0.04f, 0.042f, 0.1f, 10);
                    mb.Col(Pal.Hex("#4A3020")).Cyl(V(0, 0.095f, 0), 0.036f, 0.036f, 0.006f, 10);
                    mb.Col(Pal.White).Push(V(0.055f, 0.05f, 0), V(90f, 0, 0), V(0.8f, 1f, 1f)).Torus(Vector3.zero, 0.028f, 0.007f, 8, 3).Pop();
                }).Hold().Solid(0.3f, 0.1f);
            Add("rose", "nature", 0.3f, "Single red rose", "flower,love,romantic,gift", "flower,tulip,bouquet",
                mb =>
                {
                    mb.Col(Pal.Hex("#3E8A4A")).Cyl(V(0, -0.08f, 0), 0.004f, 0.004f, 0.3f, 4);
                    mb.TriTwoSided(V(0, 0.08f, 0), V(0.05f, 0.12f, 0.01f), V(0.01f, 0.13f, 0.03f));
                    mb.Col(Pal.Hex("#C8203A")).Lathe(V(0, 0.2f, 0), Profile(0.008f, 0f, 0.028f, 0.02f, 0.03f, 0.045f, 0.012f, 0.06f, 0f, 0.058f), 7);
                }).AsTool().Solid(0.05f, 0.1f);
            Add("book", "props", 0.04f, "Hardback book", "read,library,study,story", "novel,tome,spellbook",
                mb =>
                {
                    mb.Col(Pal.Hex("#8A2A3A")).BoxOn(Vector3.zero, V(0.16f, 0.035f, 0.22f));
                    mb.Col(Pal.Cream).Box(V(0.004f, 0.0175f, 0), V(0.158f, 0.027f, 0.208f));
                    mb.Col(Pal.Gold).Box(V(0, 0.036f, 0), V(0.09f, 0.002f, 0.02f));
                }).Hold().Solid(0.4f, 0.1f);
            Add("key", "props", 0.1f, "Old golden key", "unlock,door,secret,treasure", "skeleton key",
                mb =>
                {
                    mb.Col(Pal.Gold).Cyl(V(0, -0.02f, 0), 0.005f, 0.005f, 0.09f, 5).Box(V(0.012f, 0.055f, 0), V(0.022f, 0.008f, 0.005f)).Box(V(0.01f, 0.04f, 0), V(0.018f, 0.008f, 0.005f));
                    mb.Push(V(0, -0.035f, 0), V(90f, 0, 0)).Torus(Vector3.zero, 0.018f, 0.005f, 8, 3).Pop();
                }).Hold().Solid(0.05f, 0.2f);
            Add("telescope", "tools", 0.5f, "Brass spyglass", "look,stars,pirate,explore,sea", "spyglass,binoculars",
                mb => mb.Col(Pal.Gold).Cyl(V(0, -0.1f, 0), 0.02f, 0.024f, 0.2f, 8).Cyl(V(0, 0.1f, 0), 0.025f, 0.03f, 0.18f, 8).Cyl(V(0, 0.28f, 0), 0.032f, 0.038f, 0.14f, 8)).AsTool().Solid(0.5f, 0.2f);
            Add("bone", "props", 0.25f, "Big bone", "dog,dinosaur,skeleton,fossil", "dog bone,fossil",
                mb =>
                {
                    mb.Col(Pal.Hex("#E8E0CC")).Cyl(V(0, -0.1f, 0), 0.014f, 0.014f, 0.25f, 6, false);
                    foreach (float y in new[] { -0.1f, 0.15f }) mb.Sphere(V(0.014f, y, 0), 0.022f, 0).Sphere(V(-0.014f, y, 0), 0.022f, 0);
                }).AsTool().Solid(0.2f, 0.3f);
            Add("boomerang", "toys", 0.03f, "Wooden boomerang", "throw,australia,toy,catch", "",
                mb => mb.Col(Pal.WoodLight).BoxOn(V(0.09f, 0, 0), V(0.22f, 0.015f, 0.045f)).Push(V(0, 0, 0.09f), V(0, 90f, 0)).BoxOn(V(0, 0, 0), V(0.22f, 0.015f, 0.045f)).Pop()).Hold().Solid(0.15f, 0.4f).Drag = 0.5f;
        }
    }
}
