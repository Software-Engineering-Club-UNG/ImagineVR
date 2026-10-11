using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>Immutable built-in tuning; state belongs to each spawned item, never its shared recipe.</summary>
    public sealed class HeldProfile
    {
        public string Action, Effect;
        public Color Color;
        public float Fuse = 2.5f, Radius = 5f;
        public bool Sticky;
        public HeldProfile(string action, Color color, string effect = "")
        { Action = action; Color = color; Effect = effect; }
    }

    public static partial class Library
    {
        public static readonly List<string> ExtendedHeldIds = new List<string>();

        static partial void RegisterExtendedHoldables()
        {
            NewHeld("compact_pistol", "firearms", "gun", "firearm", 0);
            NewHeld("heavy_pistol", "firearms", "gun", "firearm", 1);
            NewHeld("service_pistol", "firearms", "gun", "firearm", 2);
            NewHeld("machine_pistol", "firearms", "gun", "firearm", 3);
            NewHeld("target_pistol", "firearms", "gun", "firearm", 4);
            NewHeld("snub_revolver", "firearms", "gun", "firearm", 5);
            NewHeld("long_revolver", "firearms", "gun", "firearm", 6);
            NewHeld("lever_rifle", "firearms", "gun", "firearm", 7);
            NewHeld("bolt_rifle", "firearms", "gun", "firearm", 8);
            NewHeld("hunting_rifle", "firearms", "gun", "firearm", 9);
            NewHeld("carbine", "firearms", "gun", "firearm", 10);
            NewHeld("assault_carbine", "firearms", "gun", "firearm", 11);
            NewHeld("battle_rifle", "firearms", "gun", "firearm", 12);
            NewHeld("marksman_rifle", "firearms", "gun", "firearm", 13);
            NewHeld("pump_shotgun", "firearms", "gun", "firearm", 14);
            NewHeld("double_shotgun", "firearms", "gun", "firearm", 15);
            NewHeld("tactical_shotgun", "firearms", "gun", "firearm", 16);
            NewHeld("compact_smg", "firearms", "gun", "firearm", 17);
            NewHeld("heavy_smg", "firearms", "gun", "firearm", 18);
            NewHeld("rotary_gun", "firearms", "gun", "firearm", 19);
            NewHeld("plasma_pistol", "science_fiction", "gun", "firearm", 0);
            NewHeld("plasma_rifle", "science_fiction", "gun", "firearm", 1);
            NewHeld("pulse_carbine", "science_fiction", "gun", "firearm", 2);
            NewHeld("ion_blaster", "science_fiction", "gun", "firearm", 3);
            NewHeld("railgun", "science_fiction", "gun", "firearm", 4);
            NewHeld("gauss_rifle", "science_fiction", "gun", "firearm", 5);
            NewHeld("tesla_gun", "science_fiction", "gun", "firearm", 6);
            NewHeld("stun_pistol", "science_fiction", "gun", "firearm", 7);
            NewHeld("freeze_ray", "science_fiction", "gun", "firearm", 8);
            NewHeld("flame_projector", "science_fiction", "gun", "firearm", 9);
            NewHeld("rocket_launcher", "science_fiction", "gun", "firearm", 10);
            NewHeld("grenade_launcher", "science_fiction", "gun", "firearm", 11);
            NewHeld("sonic_blaster", "science_fiction", "gun", "firearm", 12);
            NewHeld("gravity_gauntlet", "science_fiction", "gun", "wand", 13);
            NewHeld("tractor_wand", "science_fiction", "gun", "wand", 14);
            NewHeld("hand_axe", "tools", "tool", "melee", 0);
            NewHeld("battle_axe", "tools", "tool", "melee", 1);
            NewHeld("fire_axe", "tools", "tool", "melee", 2);
            NewHeld("prospector_pick", "tools", "tool", "melee", 3);
            NewHeld("hatchet", "tools", "tool", "melee", 4);
            NewHeld("machete", "tools", "tool", "melee", 5);
            NewHeld("katana", "tools", "tool", "melee", 6);
            NewHeld("rapier", "tools", "tool", "melee", 7);
            NewHeld("broadsword", "tools", "tool", "melee", 8);
            NewHeld("dagger", "tools", "tool", "melee", 9);
            NewHeld("spear", "tools", "tool", "melee", 10);
            NewHeld("trident", "tools", "tool", "melee", 11);
            NewHeld("war_hammer", "tools", "tool", "melee", 12);
            NewHeld("crowbar", "tools", "tool", "melee", 13);
            NewHeld("wrench", "tools", "tool", "melee", 14);
            NewHeld("screwdriver", "tools", "tool", "melee", 15);
            NewHeld("folding_shovel", "tools", "tool", "melee", 16);
            NewHeld("chainsaw", "tools", "tool", "powered_tool", 17);
            NewHeld("mining_drill", "tools", "tool", "powered_tool", 18);
            NewHeld("energy_saber", "tools", "tool", "melee", 19);
            NewHeld("frag_grenade", "throwables", "grenade", "explosive", 0);
            NewHeld("sticky_grenade", "throwables", "grenade", "explosive", 1);
            NewHeld("smoke_grenade", "throwables", "grenade", "explosive", 2);
            NewHeld("flash_grenade", "throwables", "grenade", "explosive", 3);
            NewHeld("confetti_grenade", "throwables", "grenade", "explosive", 4);
            NewHeld("plasma_grenade", "throwables", "grenade", "explosive", 5);
            NewHeld("paint_grenade", "throwables", "grenade", "explosive", 6);
            NewHeld("water_grenade", "throwables", "grenade", "explosive", 7);
            NewHeld("snow_grenade", "throwables", "grenade", "explosive", 8);
            NewHeld("spark_grenade", "throwables", "grenade", "explosive", 9);
            NewHeld("wooden_boomerang", "throwables", "boomerang", "returning", 10);
            NewHeld("hunting_boomerang", "throwables", "boomerang", "returning", 11);
            NewHeld("energy_boomerang", "throwables", "boomerang", "returning", 12);
            NewHeld("sport_frisbee", "throwables", "disc", "disc", 13);
            NewHeld("flying_disc", "throwables", "disc", "disc", 14);
            NewHeld("sport_fishing_rod", "fishing", "rod", "fishing", 0);
            NewHeld("fly_rod", "fishing", "rod", "fishing", 1);
            NewHeld("sea_fishing_rod", "fishing", "rod", "fishing", 2);
            NewHeld("ice_fishing_rod", "fishing", "rod", "fishing", 3);
            NewHeld("telescopic_rod", "fishing", "rod", "fishing", 4);
            NewHeld("tactical_flashlight", "gadgets", "gadget", "light", 0);
            NewHeld("camp_lantern", "gadgets", "gadget", "light", 1);
            NewHeld("laser_pointer", "gadgets", "gadget", "beam", 2);
            NewHeld("scanner", "gadgets", "gadget", "scan", 3);
            NewHeld("communicator", "gadgets", "gadget", "signal", 4);
            NewHeld("repair_tool", "gadgets", "gadget", "repair", 5);
            NewHeld("welding_torch", "gadgets", "gadget", "weld", 6);
            NewHeld("fire_extinguisher", "gadgets", "gadget", "extinguish", 7);
            NewHeld("bubble_generator", "gadgets", "gadget", "bubbles", 8);
            NewHeld("confetti_launcher", "gadgets", "gadget", "confetti", 9);
            NewHeld("camera", "gadgets", "gadget", "camera", 10);
            NewHeld("grappling_launcher", "gadgets", "gadget", "grapple", 11);
            NewHeld("teleport_wand", "gadgets", "gadget", "teleport", 12);
            NewHeld("musical_wand", "gadgets", "gadget", "music", 13);
            NewHeld("flare_gun", "gadgets", "gun", "firearm", 14);
            NewHeld("soda_can", "drinks", "drink", "drink", 0);
            NewHeld("cola_can", "drinks", "drink", "drink", 1);
            NewHeld("orange_soda_can", "drinks", "drink", "drink", 2);
            NewHeld("energy_drink", "drinks", "drink", "drink", 3);
            NewHeld("water_bottle", "drinks", "drink", "drink", 4);
            NewHeld("juice_carton", "drinks", "drink", "drink", 5);
            NewHeld("coffee_cup", "drinks", "drink", "drink", 6);
            NewHeld("tea_cup", "drinks", "drink", "drink", 7);
            NewHeld("potion_bottle", "drinks", "drink", "drink", 8);
            NewHeld("spray_can", "drinks", "drink", "spray", 9);
            UpgradeHeld("fishing_rod", "fishing");
            UpgradeHeld("boomerang", "returning");
            UpgradeHeld("frisbee", "disc");
            UpgradeHeld("flashlight", "light");
            UpgradeHeld("lantern", "light");
        }

        static void UpgradeHeld(string id, string action)
        {
            AssetDef def = Assets[id];
            def.Held = new HeldProfile(action, Pal.Cream);
            if (action == "disc" || action == "returning") def.Drag = action == "disc" ? 0.08f : 0.05f;
            def.Desc += " " + HeldDescription(action);
        }

        static string HeldDescription(string action)
        {
            switch (action)
            {
                case "firearm": return "Trigger fires projectiles; cadence, spread and effects depend on this weapon.";
                case "melee": return "Swing to knock creatures down and strike loose physics objects.";
                case "powered_tool": return "Hold trigger for motor sparks and repeated short-range strikes.";
                case "explosive": return "Trigger primes; deliberate release starts a 2.5-second fuse. Consumed on detonation.";
                case "returning": return "Throw to arc outward and fly back toward the throwing hand.";
                case "disc": return "Throw flat to spin and glide with lift and drag.";
                case "fishing": return "Trigger casts a visible line and bobber; trigger again reels it in.";
                case "light": return "Trigger toggles a portable light.";
                case "beam": return "Hold trigger for a visible aiming beam and hit marker.";
                case "scan": return "Trigger identifies the object you point at.";
                case "signal": return "Trigger sends a local radio chirp and acknowledgement.";
                case "repair": return "Trigger stabilizes and restores the colour of a loose object.";
                case "weld": return "Hold trigger for welding sparks and short-range strikes.";
                case "extinguish": return "Hold trigger for a cooling spray; nearby loose props stop moving.";
                case "bubbles": return "Hold trigger to blow bubbles.";
                case "confetti": return "Trigger launches a confetti burst.";
                case "camera": return "Trigger saves a game screenshot, rotating through five local slots.";
                case "grapple": return "Trigger pulls the player to a nearby safe ground landing.";
                case "teleport": return "Trigger relocates the player to a nearby safe ground landing.";
                case "music": return "Trigger plays a chime and sparkles.";
                case "drink": return "Trigger opens and takes a sip; three servings, then empty.";
                case "spray": return "Hold trigger to spray colour onto nearby loose props.";
                case "wand": return "Hold trigger to levitate a loose object you point at.";
                default: throw new ArgumentException(action);
            }
        }

        static void NewHeld(string id, string category, string model, string action, int variant)
        {
            if (Assets.ContainsKey(id)) throw new InvalidOperationException("Duplicate held recipe: " + id);
            Color accent = Color.HSVToRGB((variant * 0.117f + category.Length * 0.03f) % 1f, 0.65f, 0.85f);
            string effect = id.Contains("smoke") ? "smoke" : id == "flash_grenade" || id.Contains("stun") || id.Contains("tesla") ? "stun" :
                id.Contains("confetti") ? "confetti" : id.Contains("paint") ? "paint" : id.Contains("water") ? "splash" :
                id.Contains("snow") || id.Contains("freeze") ? "freeze" : id.Contains("spark") ? "sparks" :
                id.Contains("rocket") || id.Contains("grenade_launcher") || id.Contains("plasma") ? "explosion" :
                id.Contains("flame") || id.Contains("flare") ? "fire" : "";
            float size = model == "rod" ? 1.3f + variant * 0.15f : model == "tool" ? (id == "dagger" || id == "screwdriver" ? 0.3f : 0.8f) : model == "gun" ? GunLength(id) : 0.25f;
            if (model == "tool") size *= id == "hatchet" ? 0.6f : id == "battle_axe" || id == "spear" || id == "trident" ? 1.35f : 1;
            var profile = new HeldProfile(action, accent, effect) { Sticky = id == "sticky_grenade" };
            if (model == "grenade" && effect == "") profile.Effect = "explosion";
            AssetDef def = Add(id, category, size, id.Replace('_', ' ') + ". " + HeldDescription(action),
                "held," + category + "," + action, id.Replace('_', ' '),
                mb => HeldMesh(mb, id, model, accent, variant, size)).Hold().Solid(0.35f, 0.15f);
            def.Held = profile;
            if (model == "tool" || model == "rod" || model == "gadget" || model == "gun") def.AsTool();
            if (action == "wand") def.Does("wand");
            if (action == "bubbles") def.Does("bubbles");
            if (action == "confetti") def.Does("confetti");
            if (action == "disc") { def.Mass = 0.17f; def.Drag = 0.08f; }
            if (action == "returning") { def.Mass = 0.25f; def.Drag = 0.05f; }
            if (action == "firearm")
            {
                bool shotgun = id.Contains("shotgun"), automatic = id.Contains("machine") || id.Contains("smg") || id.Contains("carbine") || id.Contains("rotary") || id.Contains("pulse") || id.Contains("flame");
                float length = GunLength(id);
                def.Gun(V(0, 0.06f, length), shotgun ? "shotgun" : "zap", shotgun ? 0.65f : automatic ? (id == "rotary_gun" ? 0.06f : 0.11f) : 0.32f,
                    automatic, id.Contains("rail") || id.Contains("marksman") ? 65 : 28, 0.6f, shotgun ? 8 : 1, shotgun ? 7 : automatic ? 1.5f : 0.2f).Shoots(accent);
            }
            ExtendedHeldIds.Add(id);
        }

        static float GunLength(string id) => id.Contains("rifle") || id.Contains("carbine") || id.Contains("shotgun") || id.Contains("launcher") || id.Contains("railgun") || id.Contains("rotary") ?
            (id.Contains("hunting") || id.Contains("marksman") ? 0.9f : id.Contains("carbine") ? 0.55f : 0.72f) :
            id.Contains("compact") || id.Contains("snub") ? 0.18f : id.Contains("long") || id.Contains("target") ? 0.35f : 0.25f;

        static void HeldMesh(MeshBuilder mb, string id, string model, Color accent, int variant, float size)
        {
            mb.Shade(0.025f);
            if (model == "gun")
            {
                if (id == "gravity_gauntlet")
                {
                    mb.Col(Pal.MetalDark).Box(V(0, 0.06f, 0), V(0.09f, 0.13f, 0.06f));
                    for (int i = 0; i < 4; i++) mb.Col(accent).Rod(V(-0.035f + i * 0.023f, 0.1f, 0), V(-0.035f + i * 0.023f, 0.2f, 0.025f), 0.009f, 0.007f, 6);
                    mb.Emit(1).Col(accent).Sphere(V(0, 0.075f, 0.04f), 0.02f, 1); return;
                }
                GunSpace(mb);
                GunGrip(mb, Pal.MetalDark, accent, 0.038f, 0.11f);
                float len = GunLength(id), wide = id.Contains("launcher") ? 0.07f : 0.025f;
                mb.Col(Pal.MetalDark).BevelBox(V(0, 0.055f, len * 0.34f), V(0.065f, 0.09f, len * 0.65f), 0.012f);
                mb.Col(Pal.Steel).Rod(V(0, 0.06f, len * 0.5f), V(0, 0.06f, len), wide, wide * 0.85f, 10);
                mb.Col(accent).Box(V(0, 0.106f, len * 0.25f), V(0.07f, 0.012f, len * 0.35f));
                if (len > 0.4f) { mb.Col(Pal.WoodDark).Box(V(0, 0.045f, -0.13f), V(0.055f, 0.1f, 0.22f)); mb.Col(Pal.Metal).Box(V(0, -0.06f, len * 0.3f), V(0.04f, 0.15f, 0.08f)); }
                if (id.Contains("revolver")) mb.Col(Pal.Steel).Push(V(0, 0.06f, 0.06f), V(90, 0, 0)).Cyl(Vector3.zero, 0.043f, 0.043f, 0.065f, 8).Pop();
                if (id.Contains("marksman") || id.Contains("bolt") || id.Contains("rail")) mb.Col(Pal.Black).Rod(V(0, 0.15f, 0.02f), V(0, 0.15f, 0.22f), 0.025f, 0.025f, 8);
                if (id.Contains("double")) mb.Col(Pal.Steel).Rod(V(0.04f, 0.06f, len * 0.5f), V(0.04f, 0.06f, len), 0.02f, 0.02f, 8);
                if (id.Contains("rotary")) for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3; Vector3 p = V(Mathf.Cos(a) * 0.05f, 0.06f + Mathf.Sin(a) * 0.05f, len); mb.Col(Pal.Steel).Rod(p - Vector3.forward * 0.35f, p, 0.014f, 0.014f, 6); }
                if (id.Contains("plasma") || id.Contains("ion") || id.Contains("pulse") || id.Contains("gauss") || id.Contains("tesla")) for (int i = 0; i < 3; i++) mb.Emit(1).Col(accent).Sphere(V(0.045f, 0.06f, len * (0.25f + i * 0.15f)), 0.022f, 0);
                mb.Emit(0); mb.Pop(); return;
            }
            if (model == "rod")
            {
                mb.Col(Pal.Black).Cyl(V(0, -0.1f, 0), 0.018f, 0.015f, 0.32f, 8);
                mb.Col(accent).Rod(V(0, 0.2f, 0), V(0, size, 0), 0.01f, 0.002f, 6);
                mb.Col(Pal.Steel).Push(V(0.05f, 0.18f, 0), V(0, 0, 90)).Cyl(Vector3.zero, 0.04f, 0.04f, 0.025f, 8).Pop();
                for (int i = 1; i < 5; i++) mb.Col(Pal.Steel).Push(V(0.008f, size * i / 5, 0), V(90, 0, 0)).Torus(Vector3.zero, 0.012f, 0.002f, 8, 3).Pop();
                return;
            }
            if (model == "boomerang") { mb.Col(accent).Rod(V(-0.25f, 0.02f, 0.1f), V(0, 0.02f, -0.08f), 0.035f, 0.04f, 4).Rod(V(0, 0.02f, -0.08f), V(0.25f, 0.02f, 0.1f), 0.04f, 0.035f, 4); return; }
            if (model == "disc") { mb.Col(accent).Cyl(Vector3.zero, 0.13f, 0.12f, 0.025f, 20); mb.Col(Pal.White).Torus(V(0, 0.023f, 0), 0.08f, 0.006f, 16, 4); return; }
            if (model == "grenade")
            {
                if (id.Contains("smoke") || id.Contains("flash")) mb.Col(accent).Cyl(Vector3.zero, 0.038f, 0.038f, 0.14f, 10);
                else mb.Col(accent).Sphere(V(0, 0.065f, 0), V(0.045f, 0.065f, 0.045f), 1);
                mb.Col(Pal.MetalDark).Box(V(0, 0.135f, 0), V(0.055f, 0.015f, 0.045f)).Box(V(0.035f, 0.08f, 0), V(0.012f, 0.12f, 0.025f));
                mb.Col(Pal.Steel).Push(V(-0.045f, 0.125f, 0), V(0, 0, 90)).Torus(Vector3.zero, 0.018f, 0.003f, 10, 3).Pop();
                for (int i = 0; i < 3; i++) mb.Col(Pal.Dark(accent)).Torus(V(0, 0.04f + i * 0.026f, 0), 0.039f, 0.004f, 10, 3);
                return;
            }
            if (model == "tool")
            {
                mb.Col(Pal.Wood).Cyl(V(0, -0.09f, 0), 0.019f, 0.015f, size * 0.85f, 8);
                bool sword = id.Contains("sword") || id == "katana" || id == "rapier" || id == "machete" || id == "dagger" || id == "energy_saber";
                mb.Col(Pal.Steel);
                if (sword) { mb.Box(V(0, 0.14f, 0), V(0.18f, 0.025f, 0.035f)); if (id == "energy_saber") mb.Emit(1).Col(accent); mb.Rod(V(0, 0.17f, 0), V(0, size, 0), id == "rapier" ? 0.008f : 0.025f, 0.003f, 4); }
                else if (id.Contains("axe") || id == "hatchet") mb.Wedge(V(0.09f, size * 0.75f, 0), V(0.24f, 0.2f, 0.05f));
                else if (id.Contains("pick")) mb.Rod(V(-0.22f, size * 0.78f, 0), V(0.22f, size * 0.78f, 0), 0.04f, 0.015f, 5);
                else if (id.Contains("hammer")) mb.Box(V(0, size * 0.78f, 0), V(0.28f, 0.16f, 0.14f));
                else if (id == "spear" || id == "trident") { mb.Cone(V(0, size * 0.8f, 0), 0.04f, 0.2f, 6); if (id == "trident") foreach (float x in new[] { -0.09f, 0.09f }) mb.Rod(V(0, size * 0.65f, 0), V(x, size, 0), 0.015f, 0.006f, 6); }
                else if (id == "chainsaw") { mb.Col(accent).Box(V(0, 0.2f, 0), V(0.16f, 0.22f, 0.14f)); mb.Col(Pal.Steel).Box(V(0, 0.5f, 0), V(0.075f, 0.4f, 0.03f)); for (int i = 0; i < 8; i++) mb.Box(V((i % 2 == 0 ? -1 : 1) * 0.05f, 0.32f + i * 0.045f, 0), V(0.025f, 0.03f, 0.03f)); }
                else if (id == "mining_drill") { mb.Col(accent).Box(V(0, 0.2f, 0), V(0.14f, 0.2f, 0.12f)); mb.Col(Pal.Steel).Cone(V(0, 0.3f, 0), 0.05f, 0.25f, 8); }
                else if (id.Contains("shovel")) mb.Box(V(0, size * 0.85f, 0), V(0.18f, 0.24f, 0.02f));
                else if (id == "wrench") mb.Torus(V(0, size * 0.85f, 0), 0.045f, 0.014f, 8, 4);
                else if (id == "crowbar") mb.Rod(V(0, size * 0.7f, 0), V(0.07f, size * 0.85f, 0), 0.015f, 0.012f, 6);
                else mb.Col(accent).Box(V(0, size * 0.8f, 0), V(0.025f, 0.06f, 0.018f));
                return;
            }
            if (model == "drink")
            {
                bool bottle = id.Contains("bottle"), cup = id.Contains("cup"), carton = id.Contains("carton");
                if (carton) { mb.Col(accent).BoxOn(Vector3.zero, V(0.07f, 0.16f, 0.06f)); mb.Col(Pal.White).Wedge(V(0, 0.18f, 0), V(0.07f, 0.05f, 0.06f)); }
                else { mb.Col(accent).Cyl(Vector3.zero, 0.035f, bottle ? 0.03f : 0.035f, bottle ? 0.2f : 0.12f, 12); mb.Col(Pal.Steel).Cyl(V(0, bottle ? 0.2f : 0.12f, 0), bottle ? 0.016f : 0.034f, bottle ? 0.016f : 0.034f, 0.012f, 12); if (cup) mb.Col(accent).Push(V(0.045f, 0.07f, 0), V(0, 0, 90)).Torus(Vector3.zero, 0.025f, 0.008f, 12, 4).Pop(); }
                mb.Col(Pal.White).Box(V(0, 0.065f, 0.035f), V(0.028f, 0.04f, 0.002f)); return;
            }
            // Gadgets get recognisable attachments rather than anonymous cubes.
            mb.Col(Pal.MetalDark).BevelBox(V(0, 0.08f, 0), V(0.065f, 0.2f, 0.06f), 0.008f);
            mb.Col(accent).Box(V(0, 0.11f, 0.032f), V(0.05f, 0.07f, 0.004f));
            if (id.Contains("lantern")) { mb.Col(accent).Cyl(V(0, 0.06f, 0), 0.075f, 0.075f, 0.18f, 8); mb.Emit(1).Col(Pal.Cream).Sphere(V(0, 0.14f, 0), 0.05f, 1); mb.Emit(0).Col(Pal.Steel).Torus(V(0, 0.28f, 0), 0.055f, 0.006f, 12, 4); }
            else if (id.Contains("flashlight")) { mb.Col(Pal.Steel).Cyl(V(0, 0.16f, 0), 0.02f, 0.045f, 0.06f, 10); mb.Emit(1).Col(Pal.Cream).Cyl(V(0, 0.22f, 0), 0.04f, 0.04f, 0.005f, 10); }
            else if (id == "camera") { mb.Col(accent).Box(V(0, 0.12f, 0), V(0.16f, 0.08f, 0.055f)); mb.Col(Pal.Black).Rod(V(0, 0.12f, 0.02f), V(0, 0.12f, 0.08f), 0.03f, 0.026f, 12); }
            else if (id.Contains("extinguisher")) { mb.Col(Pal.Red).Cyl(V(0, 0.02f, 0), 0.06f, 0.06f, 0.25f, 10); mb.Col(Pal.Black).Rod(V(0, 0.27f, 0), V(0.12f, 0.23f, 0), 0.01f, 0.01f, 6); }
            else if (id.Contains("wand")) { mb.Col(accent).Rod(V(0, 0.18f, 0), V(0, 0.45f, 0), 0.013f, 0.008f, 6); mb.Emit(1).Sphere(V(0, 0.45f, 0), 0.035f, 1); }
            else if (id == "communicator") { mb.Col(Pal.Black).Cyl(V(0.02f, 0.18f, 0), 0.005f, 0.003f, 0.14f, 6); }
            else mb.Col(Pal.Steel).Cyl(V(0, 0.18f, 0), 0.025f, 0.015f, 0.05f + variant * 0.002f, 8);
        }
    }
}
