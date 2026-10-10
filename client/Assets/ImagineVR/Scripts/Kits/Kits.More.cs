using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    public static partial class Library
    {
        static float Face(float x, float z) => KitContext.Facing(x, z, 0f, 0f);

        /// <summary>True where the ground is clear of the water (always true when the terrain has no water).</summary>
        static bool Dry(KitContext k, float x, float z, float margin) => !k.HasWater || k.Ground(x, z) > k.WaterY + margin;

        static partial void RegisterMoreKits()
        {
            Kit("forest", "A deep woodland: mixed trees all around, a clearing where you stand, ferns, mushrooms, logs, deer",
                "woods,trees,nature,woodland,wild", "woods,woodland,jungle,the woods,grove", Forest)
                .Defaults(sky: "morning", terrain: "rolling", material: "forest_floor", height: 0.3f, ambience: "forest")
                .Param("season", "summer, autumn, winter or spring").Greet("Deep in the woods. Listen.");

            Kit("beach", "A sandy shore: sea ahead, palms behind, parasols, deck chairs, a sandcastle, boats on the water",
                "sea,coast,shore,summer,holiday,ocean,tropical", "seaside,shore,coast,tropical beach,the beach", Beach)
                .Defaults(sky: "clear_noon", terrain: "beach", material: "sand", ambience: "ocean_waves").Greet("Sand under your feet, sea ahead.");

            Kit("tropical_island", "A small desert island ringed by sea: palms, a treasure chest, a rowing boat, crabs",
                "island,sea,castaway,pirate,tropical", "desert island,island,castaway island,atoll", Island)
                .Defaults(sky: "clear_noon", terrain: "island", material: "sand", ambience: "ocean_waves").Greet("Your own island. Nobody for miles.");

            Kit("desert", "A wide desert: cacti, wind-carved rock spires, bleached bones, a great pyramid on the horizon",
                "sand,dunes,dry,hot,sahara,egypt", "sahara,dunes,wasteland,egypt", Desert)
                .Defaults(sky: "desert_noon", terrain: "dunes", material: "sand", ambience: "desert_wind")
                .Param("oasis", "true adds a palm-ringed pool").Greet("Nothing but sand and sky.");

            Kit("moon_base", "The surface of the Moon: craters, a lander, a rocket, astronauts, the Earth hanging in a black sky, low gravity",
                "space,moon,lunar,nasa,astronaut,low gravity", "moon,the moon,lunar surface,lunar base,space", MoonBase)
                .Defaults(sky: "deep_space", terrain: "craters", material: "moon", ambience: "space_hum", gravity: 0.16f).Greet("One small step. Things fall slowly here.");

            Kit("deep_space", "Floating in open space on a small platform: asteroids drifting past, planets, a satellite, no gravity",
                "space,stars,zero gravity,floating,cosmos,galaxy", "outer space,space,galaxy,orbit,zero gravity", DeepSpace)
                .Defaults(sky: "deep_space", terrain: "none", ambience: "space_hum", gravity: 0f).Greet("Nothing holds you down out here.");

            Kit("winter_village", "A snowy village: log cabins with lit windows, snow-laden pines, snowmen, a decorated tree",
                "snow,christmas,winter,cold,holiday,cabin", "snowy village,christmas village,winter wonderland,north pole,snow", WinterVillage)
                .Defaults(sky: "winter_day", terrain: "rolling", material: "snow", height: 0.3f, ambience: "wind", weather: "snow").Greet("Snow is falling. The cabins are warm.");

            Kit("coral_reef", "Under the sea: coral, swaying kelp, schools of fish, jellyfish, a turtle, a sunken chest",
                "underwater,ocean,sea,reef,fish,diving", "underwater,under the sea,ocean floor,sea bed,aquarium", CoralReef)
                .Defaults(sky: "underwater", terrain: "rolling", material: "seabed", height: 0.35f, ambience: "underwater", weather: "bubbles", gravity: 0.25f)
                .Greet("Deep breath. You're under the sea.");

            Kit("farm", "A working farm: red barn, windmill, fenced field of animals, hay bales, a scarecrow",
                "countryside,barn,animals,rural,ranch", "ranch,countryside,farmyard,the farm", Farm)
                .Defaults(sky: "morning", terrain: "rolling", material: "grass", height: 0.2f, ambience: "birds_breeze").Greet("Morning on the farm.");

            Kit("castle", "A stone castle: walls and corner towers around a courtyard, a gatehouse facing you, knights, torches",
                "medieval,fortress,knight,kingdom,fantasy", "fortress,citadel,kingdom,medieval castle,stronghold", Castle)
                .Defaults(sky: "golden_hour", terrain: "rolling", material: "grass", height: 0.15f, ambience: "wind").Greet("The gates are open. Welcome to the castle.");

            Kit("medieval_village", "A timber-framed village around a square: houses, a well, market torches, villagers",
                "medieval,town,fantasy,tavern,market", "village,medieval town,fantasy village,hamlet,town square", Village)
                .Defaults(sky: "golden_hour", terrain: "flat", material: "dirt", ambience: "birds_breeze").Greet("The village square, just before dusk.");

            Kit("japanese_garden", "A serene garden: red torii gate, pagoda, cherry trees in blossom, stone lanterns, a bridge over a pond",
                "japan,zen,temple,sakura,asia,peaceful", "zen garden,japan,japanese temple,kyoto,sakura garden", JapaneseGarden)
                .Defaults(sky: "sunset_pink", terrain: "rolling", material: "grass", height: 0.12f, ambience: "zen_chimes", weather: "petals").Greet("Petals on the wind. Take your time.");

            Kit("synthwave", "A retro-future highway: glowing grid to the horizon, a huge low sun, neon palms, purple mountains",
                "retro,neon,80s,vaporwave,outrun,grid,cyber", "vaporwave,outrun,retrowave,80s,neon grid,tron", Synthwave)
                .Defaults(sky: "synthwave", terrain: "flat", material: "neon", ambience: "synth_pad", grid: 0.9f, gridColor: "#FF4FD8").Greet("Welcome to 1986. Sort of.");

            Kit("campsite", "A campsite at night: tent, crackling fire ringed with logs, lantern, pines, fireflies",
                "camping,fire,night,outdoors,woods,cozy", "camp,camping,campfire,camping trip", Campsite)
                .Defaults(sky: "night_stars", terrain: "rolling", material: "forest_floor", height: 0.2f, ambience: "fire_crackle", weather: "fireflies").Greet("Pull up a log. The fire's going.");

            Kit("graveyard", "A haunted graveyard: crooked headstones, bare trees, a mansion on the hill, drifting ghosts, bats",
                "halloween,haunted,spooky,ghost,cemetery,horror", "cemetery,haunted graveyard,halloween,spooky,haunted", Graveyard)
                .Defaults(sky: "spooky", terrain: "rolling", material: "swamp", height: 0.25f, ambience: "spooky").Greet("Something moved. Probably the wind.");

            Kit("candyland", "A land made of sweets: giant lollipops, candy canes, cupcakes, doughnuts and a towering cake",
                "sweet,candy,dessert,pastel,kids,cute", "candy land,candy world,sweet land,sugar land,dessert world", Candyland)
                .Defaults(sky: "candy", terrain: "rolling", material: "candy", height: 0.25f).Greet("Everything here is edible. Probably.");

            Kit("ancient_ruins", "Ruins of a forgotten temple: rows of marble columns, broken arches, statues, an obelisk",
                "greek,roman,temple,ancient,archaeology", "ruins,temple ruins,greek temple,roman ruins,acropolis,stonehenge", Ruins)
                .Defaults(sky: "golden_hour", terrain: "rolling", material: "dry_grass", height: 0.2f, ambience: "wind").Greet("Somebody built this three thousand years ago.");

            Kit("playground", "A park playground: swings, a slide, a seesaw, a trampoline, benches, trees, balls to kick about",
                "park,kids,play,swings,fun,garden", "park,play park,the park,kids park", Playground)
                .Defaults(sky: "clear_noon", terrain: "flat", material: "grass", ambience: "birds_breeze").Greet("Go on. Nobody's watching.");

            Kit("art_gallery", "A bright white gallery: framed paintings all around, sculptures on plinths, an easel and paints beside you",
                "art,museum,studio,gallery,creative,painting,white", "gallery,museum,art studio,studio,exhibition,art museum", ArtGallery)
                .Defaults(sky: "white_room", terrain: "flat", material: "marble").Greet("The gallery is yours. Pick up a brush.");

            Kit("sports_court", "An outdoor court: basketball hoop ahead with balls at your feet, a football goal behind, court lines",
                "basketball,sport,game,football,soccer,court", "basketball court,court,pitch,sports field,stadium,gym", SportsCourt)
                .Defaults(sky: "clear_noon", terrain: "flat", material: "concrete", ambience: "birds_breeze").Greet("Ball's at your feet. Take the shot.");

            Kit("bowling_alley", "A bowling lane: polished wood running ahead to ten pins, bowling balls beside you",
                "bowling,sport,game,pins,strike", "bowling,bowling lane,ten pin", Bowling)
                .Defaults(sky: "night_city", terrain: "flat", material: "concrete").Greet("Ten pins, twelve metres. Grab a ball.");

            Kit("dinosaur_valley", "A prehistoric valley: giant ferns and palms, a towering long-neck, a T. rex, a smoking volcano",
                "dinosaur,jurassic,prehistoric,jungle,volcano", "jurassic park,dinosaurs,prehistoric,dino land,lost world", DinosaurValley)
                .Defaults(sky: "morning", terrain: "valley", material: "forest_floor", height: 0.4f, ambience: "forest").Greet("Sixty-five million years ago. Stay still.");

            Kit("safari", "African savanna: golden grass, flat-topped trees, elephants, giraffes and lions in the distance",
                "africa,savanna,wildlife,animals,grassland", "savanna,savannah,africa,serengeti,wildlife park", Safari)
                .Defaults(sky: "golden_hour", terrain: "rolling", material: "dry_grass", height: 0.15f, ambience: "wind").Greet("Golden hour on the savanna.");

            Kit("fairground", "A funfair at dusk: a giant Ferris wheel, striped tents, balloons, lights, a crowd",
                "carnival,fair,amusement park,rides,circus,festival", "carnival,funfair,amusement park,theme park,circus,fair", Fairground)
                .Defaults(sky: "dusk", terrain: "flat", material: "grass", ambience: "city_night").Greet("Roll up. The wheel's turning.");

            Kit("harbor", "A quiet harbour: a wooden pier, boats at anchor, a lighthouse, crates and barrels on the quay",
                "port,dock,sea,boats,fishing,coast", "harbour,port,docks,marina,fishing village,quay", Harbor)
                .Defaults(sky: "morning", terrain: "beach", material: "rock", ambience: "ocean_waves").Greet("Tide's in. Boats are ready.");

            Kit("western_town", "A Wild West main street: saloons on both sides, cacti, barrels, horses, tumbleweed blowing through",
                "cowboy,wild west,frontier,saloon,desert", "wild west,old west,cowboy town,frontier town,ghost town", WesternTown)
                .Defaults(sky: "desert_noon", terrain: "flat", material: "dirt", ambience: "desert_wind", weather: "dust").Greet("This town ain't big enough.");

            Kit("volcano_land", "A volcanic wasteland: black rock, rivers of glowing lava, burnt trees, an erupting cone, falling embers",
                "lava,fire,hell,volcanic,eruption,danger", "volcano,lava land,hell,inferno,mordor", VolcanoLand)
                .Defaults(sky: "volcanic", terrain: "hills", material: "lava_rock", height: 0.3f, ambience: "fire_crackle", weather: "embers").Greet("Mind the lava.");

            Kit("cloud_kingdom", "Above the clouds: a floating platform in a sea of cloud, a rainbow, balloons and lanterns drifting by",
                "sky,heaven,clouds,dream,floating,flying", "heaven,sky kingdom,above the clouds,dream world,the sky", CloudKingdom)
                .Defaults(sky: "dreamy", terrain: "none", ambience: "zen_chimes", gravity: 0.4f).Greet("Nothing below but cloud.");
        }

        // ================================================================ nature

        static void Forest(KitContext k)
        {
            string season = k.ParamStr("season", "summer");
            string leafy = season == "autumn" ? "autumn_tree" : season == "spring" ? "cherry_tree" : season == "winter" ? "dead_tree" : "oak_tree";
            string conifer = season == "winter" ? "snowy_pine" : "pine_tree";
            float reach = k.Reach;
            k.Scatter(leafy, k.N(90), 7f, reach, 0.8f, 1.6f);
            k.Scatter(conifer, k.N(80), 8f, reach, 0.8f, 1.7f);
            k.Scatter("birch_tree", k.N(40), 7f, reach * 0.8f, 0.8f, 1.4f);
            k.Scatter("bush", k.N(60), 5f, reach * 0.7f, 0.6f, 1.4f);
            k.Scatter("fern", k.N(110), 3.5f, 45f, 0.7f, 1.6f);
            k.Scatter("tall_grass", k.N(90), 3.5f, 40f, 0.8f, 1.5f);
            k.Scatter("mushroom", k.N(26), 3.5f, 22f, 0.8f, 2f);
            k.Scatter("rock", k.N(24), 4f, 50f, 0.7f, 2.2f);
            k.Scatter("boulder", k.N(8), 10f, 60f, 0.7f, 1.4f);
            k.Scatter("log", k.N(8), 5f, 30f, 0.8f, 1.3f);
            k.Scatter("tree_stump", k.N(7), 4f, 25f, 0.8f, 1.3f);
            if (season != "winter") k.Scatter("flower_patch", k.N(40), 3.5f, 28f, 0.8f, 1.5f);
            k.Put("deer", k.Range(-9f, -6f), k.Range(11f, 15f), k.Range(60f, 140f));
            k.Put("deer", k.Range(6f, 10f), k.Range(16f, 21f), k.Range(200f, 300f), 0.85f);
            k.Scatter("rabbit", 3, 5f, 12f, 0.9f, 1.1f);
            k.Put("fox", -11f, -7f, 40f);
        }

        static void Beach(KitContext k)
        {
            // land is behind and around; the sea is ahead (+Z)
            System.Func<float, float, bool> dry = (x, z) => Dry(k, x, z, 0.5f);
            k.Scatter("palm_tree", k.N(34), 7f, k.Reach, 0.8f, 1.3f, null, (x, z) => dry(x, z) && z < 2f);
            k.Scatter("bush", k.N(22), 10f, k.Reach * 0.8f, 0.7f, 1.3f, null, (x, z) => dry(x, z) && z < -6f);
            k.Scatter("rock", k.N(14), 5f, 50f, 0.7f, 2f, null, dry);
            k.Scatter("seashell", k.N(24), 3.5f, 16f, 0.8f, 1.6f, null, dry);
            k.Scatter("starfish", k.N(8), 3.5f, 14f, 0.8f, 1.4f, null, dry);
            k.Scatter("crab", k.N(5), 4f, 14f, 0.8f, 1.2f, null, dry);
            int sets = k.N(4);
            for (int i = 0; i < sets; i++)
            {
                float x = (i - (sets - 1) * 0.5f) * 5.5f + k.Range(-1f, 1f), z = k.Range(-1.5f, 3.5f);
                if (Mathf.Abs(x) < 3.2f) x += x < 0 ? -3.2f : 3.2f;
                Color cloth = k.Pick(Pal.Red, Pal.Blue, Pal.Yellow, Pal.Teal, Pal.Orange);
                k.Put("beach_umbrella", x, z, k.Rand() * 360f, 1f, cloth);
                k.Put("beach_chair", x + 0.9f, z + 0.3f, 180f + k.Range(-20f, 20f));
                if (k.Chance(0.6f)) k.Put("beach_chair", x - 0.9f, z + 0.3f, 180f + k.Range(-20f, 20f));
            }
            k.Put("sandcastle", -2.2f, 4.2f, 30f, 1.3f);
            k.Loose("beach_ball", 1.6f, 3.6f, 0.3f);
            k.Loose("beach_ball", -3.4f, 2.4f, 0.3f, 0f, 1f, Pal.Hex("#FFD0E0"));
            k.Scatter("sailboat", k.N(4), 45f, 100f, 0.9f, 1.3f, null, (x, z) => z > 30f);
            k.Scatter("boat", k.N(3), 18f, 40f, 0.9f, 1.1f, null, (x, z) => z > 16f);
            k.Float("bird", 6f, 9f, 14f, 40f, 1.6f);
            k.Float("bird", -9f, 12f, 22f, 120f, 1.6f);
        }

        static void Island(KitContext k)
        {
            System.Func<float, float, bool> dry = (x, z) => Dry(k, x, z, 0.4f);
            k.Scatter("palm_tree", k.N(26), 5f, 40f, 0.8f, 1.3f, null, dry);
            k.Scatter("bush", k.N(18), 5f, 34f, 0.7f, 1.3f, null, dry);
            k.Scatter("rock", k.N(12), 4f, 36f, 0.7f, 2f, null, dry);
            k.Scatter("fern", k.N(26), 4f, 30f, 0.8f, 1.5f, null, dry);
            k.Scatter("seashell", k.N(14), 3.5f, 14f, 0.8f, 1.5f, null, dry);
            k.Scatter("crab", k.N(4), 4f, 14f, 0.8f, 1.2f, null, dry);
            k.Put("treasure_chest", 2.4f, 5.5f, 200f);
            k.Put("campfire", -3.4f, 4.6f, 0f);
            k.Put("log", -5.2f, 5.6f, 60f);
            k.Scatter("boat", 1, 24f, 34f, 1f, 1f);
            k.Scatter("sailboat", k.N(3), 55f, 100f, 0.9f, 1.4f);
            k.Scatter("pirate_ship", 1, 70f, 95f, 1f, 1f);
            k.Scatter("dolphin", k.N(3), 30f, 60f, 1f, 1.3f, null, null, false);
        }

        static void Desert(KitContext k)
        {
            float reach = k.Reach;
            k.Scatter("cactus", k.N(40), 6f, reach, 0.7f, 1.6f);
            k.Scatter("rock", k.N(30), 4f, reach * 0.8f, 0.8f, 2.6f, Pal.Hex("#E0C8A0"));
            k.Scatter("boulder", k.N(12), 12f, reach, 0.8f, 1.8f, Pal.Hex("#E0C8A0"));
            k.Scatter("rock_spire", k.N(12), 30f, reach, 0.9f, 2.4f);
            k.Scatter("dead_tree", k.N(6), 10f, 60f, 0.6f, 1f);
            k.Scatter("tall_grass", k.N(40), 4f, 50f, 0.7f, 1.3f, Pal.Hex("#D8C070"));
            k.Put("cow_skull", 2.6f, 4.4f, 200f);
            k.Loose("tumbleweed", -4f, 5f, 0.4f);
            k.Loose("tumbleweed", 6f, 8f, 0.4f);
            k.Put("pyramid_tomb", k.Range(-40f, 40f), 190f, 0f, 1.3f);
            k.Put("pyramid_tomb", k.Range(60f, 110f), 230f, 0f, 0.8f);
            if (k.ParamStr("oasis", "false") == "true" || k.Chance(0.35f))
            {
                float ox = k.Range(-10f, 10f), oz = k.Range(20f, 28f);
                k.Patch(ox, oz, 6f, 4.5f, Pal.Hex("#3E9AC8"), 0.12f, 0.15f, 0.15f, 20, "oasis");
                k.Ring("palm_tree", 7, 7.5f, 1f, false, null, 0.3f, ox, oz);
                k.Ring("bush", 9, 6.4f, 0.9f, false, null, 0.4f, ox, oz);
            }
        }

        // ================================================================ space

        static void MoonBase(KitContext k)
        {
            k.Put("moon_lander", -7f, 11f, 150f);
            k.Put("rocket", 14f, 26f, 0f, 1.4f);
            k.Put("flag", 3.2f, 6f, 90f, 0.7f, Pal.Hex("#E8E8FF"));
            k.Put("astronaut", -3f, 7.5f, 160f);
            k.Put("astronaut", 5.5f, 10f, 220f);
            k.Put("satellite", 9f, 6f, 30f, 0.8f, null, 1.2f);
            k.Scatter("rock", k.N(60), 4f, k.Reach, 0.6f, 2.4f, Pal.Hex("#B8B8BE"));
            k.Scatter("boulder", k.N(20), 10f, k.Reach, 0.6f, 1.8f, Pal.Hex("#A8A8AE"));
            k.Scatter("rock_spire", k.N(6), 45f, k.Reach, 0.8f, 1.6f, Pal.Hex("#9A9AA2"));
            for (int i = 0; i < 6; i++) k.Loose("rock", k.Range(-3f, 3f), k.Range(2.5f, 5f), 0.3f, k.Rand() * 360f, k.Range(0.25f, 0.5f), Pal.Hex("#C8C8CE"));
            k.Float("planet", -60f, 85f, 190f, 20f, 26f);
            k.Float("ringed_planet", 150f, 60f, -120f, 0f, 14f);
            k.Float("ufo", 30f, 26f, 60f, 0f, 1.5f);
        }

        static void DeepSpace(KitContext k)
        {
            k.Put("disc", 0f, 0f, 0f, new Vector3(5f, 2f, 5f), Pal.Hex("#3A4252"), -0.14f);
            k.Solid(0f, 0f, new Vector3(5f, 0.2f, 5f), 0f, -0.2f);
            for (int i = 0; i < k.N(36); i++)
            {
                float a = k.Rand() * Mathf.PI * 2f, r = k.Range(9f, 80f);
                k.Float("asteroid", Mathf.Cos(a) * r, k.Range(-25f, 35f), Mathf.Sin(a) * r, k.Rand() * 360f, k.Range(0.6f, 4f));
            }
            k.Float("planet", -90f, 30f, 220f, 0f, 45f);
            k.Float("ringed_planet", 170f, 70f, 60f, 0f, 22f);
            k.Float("planet", 60f, -40f, -180f, 0f, 16f, Pal.Hex("#E8A070"));
            k.Float("satellite", 7f, 3f, 12f, 30f, 1.2f);
            k.Float("ufo", -14f, 6f, 20f, 0f, 1.6f);
            k.Float("rocket", 20f, -2f, 30f, 0f, 1.2f);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                k.Loose("asteroid", Mathf.Cos(a) * 3.2f, Mathf.Sin(a) * 3.2f + 0.5f, k.Range(0.8f, 2f), 0f, k.Range(0.12f, 0.3f));
            }
        }

        // ================================================================ cold and deep

        static void WinterVillage(KitContext k)
        {
            int cabins = k.N(6);
            for (int i = 0; i < cabins; i++)
            {
                float a = (i + 0.5f) / cabins * Mathf.PI * 2f + k.Range(-0.2f, 0.2f), r = k.Range(16f, 26f);
                float x = Mathf.Sin(a) * r, z = Mathf.Cos(a) * r;
                k.Put("log_cabin", x, z, Face(x, z) + k.Range(-15f, 15f));
                k.Put("snowy_pine", x + 6f, z + 2f, -1f, k.Range(0.9f, 1.4f));
                if (k.Chance(0.6f)) k.Put("street_lamp", x * 0.72f, z * 0.72f, Face(x, z), 0.8f);
            }
            k.Put("christmas_tree", 0.5f, 8.5f, 0f, 1.6f);
            foreach (Vector2 g in new[] { new Vector2(-1.2f, 7.2f), new Vector2(1.8f, 7f), new Vector2(0.4f, 6.6f) }) k.Loose("gift_box", g.x, g.y, 0.05f, k.Rand() * 90f, k.Range(0.8f, 1.3f), k.Pick(Pal.Red, Pal.Green, Pal.Blue, Pal.Gold));
            k.Put("snowman", -4.2f, 5.4f, 150f);
            k.Put("snowman", 5.2f, 8f, 210f, 0.8f);
            k.Put("igloo", -10f, 12f, 130f);
            k.Scatter("snowy_pine", k.N(110), 12f, k.Reach, 0.8f, 1.7f);
            k.Scatter("rock", k.N(16), 6f, 50f, 0.8f, 2f, Pal.Hex("#C8D4E0"));
            k.Scatter("ice_crystal", k.N(8), 20f, 70f, 0.7f, 1.6f);
            k.Scatter("penguin", k.N(5), 5f, 13f, 0.9f, 1.1f);
            for (int i = 0; i < 5; i++) k.Loose("snowball", k.Range(-1.5f, 1.5f), k.Range(2.4f, 3.4f), 0.1f);
            k.Put("mountain", -150f, 260f, 0f, 2.2f);
            k.Put("mountain", 190f, 220f, 40f, 1.8f);
        }

        static void CoralReef(KitContext k)
        {
            float reach = Mathf.Min(k.Reach, 70f);
            k.Scatter("coral", k.N(70), 4f, reach, 0.7f, 2.2f, Pal.Hex("#FF8A80"));
            k.Scatter("coral", k.N(50), 4f, reach, 0.6f, 1.8f, Pal.Hex("#FFC060"));
            k.Scatter("coral", k.N(40), 5f, reach, 0.6f, 1.8f, Pal.Hex("#C080FF"));
            k.Scatter("kelp", k.N(90), 5f, reach, 0.7f, 1.8f);
            k.Scatter("rock", k.N(40), 4f, reach, 0.8f, 3f, Pal.Hex("#8A9AA0"));
            k.Scatter("boulder", k.N(14), 10f, reach, 0.8f, 2f, Pal.Hex("#7A8A90"));
            k.Scatter("seashell", k.N(22), 3.5f, 16f, 0.8f, 2f);
            k.Scatter("starfish", k.N(14), 3.5f, 18f, 0.8f, 1.6f);
            k.Scatter("crab", k.N(5), 4f, 14f, 0.8f, 1.3f);
            k.Put("treasure_chest", -2.8f, 5.6f, 160f);
            k.Put("sea_turtle", 4f, 7f, 250f, 1.2f, null, 1.8f);
            k.Put("pirate_ship", -24f, 38f, 70f, new Vector3(1f, 1f, 1f), Pal.Hex("#6A7A70"), -1.6f);
            Color[] school = { Pal.Orange, Pal.Yellow, Pal.Cyan, Pal.Pink, Pal.Hex("#B4E04A") };
            for (int i = 0; i < k.N(46); i++)
            {
                float a = k.Rand() * Mathf.PI * 2f, r = k.Range(5f, 34f);
                k.Float("fish", Mathf.Cos(a) * r, k.Range(1.2f, 9f), Mathf.Sin(a) * r, k.Rand() * 360f, k.Range(0.8f, 1.8f), school[i % school.Length]);
            }
            for (int i = 0; i < k.N(9); i++)
            {
                float a = k.Rand() * Mathf.PI * 2f, r = k.Range(6f, 26f);
                k.Float("jellyfish", Mathf.Cos(a) * r, k.Range(3f, 11f), Mathf.Sin(a) * r, 0f, k.Range(0.8f, 1.8f));
            }
            k.Float("shark", 26f, 9f, 34f, 220f, 1.4f);
            k.Float("whale", -70f, 22f, 90f, 100f, 1.2f);
            k.Float("dolphin", 12f, 12f, 24f, 300f, 1.4f);
            k.Group.Motion = new Vector4(0f, 0.12f, 0.6f, 0f);   // everything rocks gently in the current
        }

        // ================================================================ countryside and history

        static void Farm(KitContext k)
        {
            k.Put("barn", -16f, 26f, 150f);
            k.Put("windmill", 24f, 38f, 200f);
            k.Put("house", 15f, 14f, 230f);
            k.Put("well", 7f, 9f, 0f);
            // a fenced paddock ahead
            float px = -2f, pz = 22f, hw = 9f, hd = 6f;
            for (float x = -hw + 1.5f; x <= hw; x += 3f) { k.Put("fence", px + x, pz - hd, 0f); k.Put("fence", px + x, pz + hd, 0f); }
            for (float z = -hd + 1.5f; z <= hd; z += 3f) { k.Put("fence", px - hw, pz + z, 90f); k.Put("fence", px + hw, pz + z, 90f); }
            string[] herd = { "cow", "cow", "sheep", "sheep", "sheep", "pig", "horse" };
            foreach (string animal in herd) k.Put(animal, px + k.Range(-hw + 1.5f, hw - 1.5f), pz + k.Range(-hd + 1.5f, hd - 1.5f), k.Rand() * 360f);
            k.Scatter("chicken", k.N(7), 4f, 10f, 0.9f, 1.1f);
            k.Put("dog", 3.2f, 4.6f, 200f);
            // a field of crops with a scarecrow
            for (int row = 0; row < 7; row++)
                for (int col = 0; col < k.N(12); col++)
                    k.Put("tall_grass", 16f + col * 1.5f, -8f - row * 2.2f, -1f, k.Range(1.6f, 2.2f), Pal.Hex("#E8C860"));
            k.Put("scarecrow", 24f, -14f, 300f);
            for (int i = 0; i < k.N(6); i++) k.Loose("hay_bale", -9f + k.Range(-3f, 3f), 7f + k.Range(-3f, 3f), 0.1f, k.Rand() * 360f);
            for (int i = 0; i < 3; i++) k.Loose("pumpkin", 2.2f + i * 0.6f, 3.4f + k.Range(-0.3f, 0.3f), 0.05f);
            k.Scatter("oak_tree", k.N(34), 34f, k.Reach, 0.9f, 1.5f);
            k.Scatter("bush", k.N(24), 12f, k.Reach * 0.7f, 0.7f, 1.3f);
            k.Scatter("flower_patch", k.N(44), 4f, 34f, 0.8f, 1.5f);
        }

        static void Castle(KitContext k)
        {
            // a square of walls, its nearest side 22 m ahead, with the gate facing the user
            float half = 26f, cz = 48f, front = cz - half;
            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f }) k.Put("castle_tower", sx * half, cz + sz * half, 0f, 1.25f);
            for (float t = -half + 8f; t < half - 4f; t += 10f)
            {
                if (Mathf.Abs(t) > 6f) k.Put("castle_wall", t, front, 0f);
                k.Put("castle_wall", t, cz + half, 0f);
                k.Put("castle_wall", -half, cz + t, 90f);
                k.Put("castle_wall", half, cz + t, 90f);
            }
            k.Put("castle_wall", -8f, front, 0f, new Vector3(0.6f, 1f, 1f));
            k.Put("castle_wall", 8f, front, 0f, new Vector3(0.6f, 1f, 1f));
            k.Put("castle_gate", 0f, front, 180f, 1.05f);
            k.Put("castle_tower", 0f, cz + 6f, 0f, new Vector3(2.2f, 1.9f, 2.2f));   // the keep
            k.Put("flag", 0f, cz + 6f, 0f, 2f, Pal.Red, 22f);
            k.Ribbon(new List<Vector2> { new Vector2(0f, 3.5f), new Vector2(0f, front - 1f) }, 4f, Pal.Hex("#A89878"), 0.05f, 0f, "road");
            k.Put("knight", -2.6f, front - 2.5f, 180f);
            k.Put("knight", 2.6f, front - 2.5f, 180f);
            k.Put("torch", -3.4f, front - 1f, 0f, 1.3f);
            k.Put("torch", 3.4f, front - 1f, 0f, 1.3f);
            for (int i = 0; i < k.N(6); i++)
            {
                float side = i % 2 == 0 ? -1f : 1f, z = 9f + (i / 2) * 9f;
                k.Put("medieval_house", side * k.Range(12f, 17f), z, side < 0 ? 90f : 270f, k.Range(0.9f, 1.15f));
            }
            k.Put("horse", 5f, 7f, 250f);
            k.Put("well", -6f, 8f, 0f);
            k.Scatter("oak_tree", k.N(50), 34f, k.Reach, 0.9f, 1.6f, null, (x, z) => Mathf.Abs(x) > half + 6f || z < front - 8f || z > cz + half + 6f);
            k.Scatter("bush", k.N(30), 8f, k.Reach * 0.7f, 0.7f, 1.3f, null, (x, z) => Mathf.Abs(x) > 5f && (Mathf.Abs(x) > half + 4f || z < front - 4f));
            k.Put("mountain", -170f, 260f, 0f, 2f);
        }

        static void Village(KitContext k)
        {
            int houses = k.N(9);
            for (int i = 0; i < houses; i++)
            {
                float a = (i + 0.5f) / houses * Mathf.PI * 2f, r = k.Range(15f, 19f);
                float x = Mathf.Sin(a) * r, z = Mathf.Cos(a) * r;
                k.Put("medieval_house", x, z, Face(x, z) + k.Range(-8f, 8f), k.Range(0.9f, 1.2f));
                if (k.Chance(0.5f)) k.Put("medieval_house", x * 1.9f, z * 1.9f, Face(x, z) + k.Range(-30f, 30f), k.Range(0.9f, 1.3f));
            }
            k.Put("well", 0f, 7f, 0f);
            k.Ring("torch", 6, 9f, 1.1f, false, null, 0.15f, 0f, 0f, 30f);
            string[] folk = { "person", "woman", "child", "knight", "wizard", "person", "woman" };
            for (int i = 0; i < k.N(7); i++) { float a = k.Rand() * Mathf.PI * 2f, r = k.Range(4.5f, 12f); k.Put(folk[i % folk.Length], Mathf.Sin(a) * r, Mathf.Cos(a) * r, k.Rand() * 360f, 1f, k.Pick(Pal.White, Pal.Hex("#E8D8B8"), Pal.Hex("#C8D8E8"))); }
            for (int i = 0; i < k.N(5); i++) k.Loose(k.Chance(0.5f) ? "barrel" : "crate", k.Range(4f, 7f) * (k.Chance(0.5f) ? 1f : -1f), k.Range(3.5f, 9f), 0.1f, k.Rand() * 90f);
            k.Put("horse", -5.5f, 6f, 110f);
            k.Put("dog", 3.5f, 4.5f, 220f);
            k.Scatter("chicken", 4, 4f, 9f, 0.9f, 1.1f);
            k.Scatter("oak_tree", k.N(40), 42f, k.Reach, 0.9f, 1.6f);
            k.Put("castle_tower", 60f, 110f, 0f, 2f);
            k.Put("windmill", -70f, 90f, 120f, 1.2f);
        }

        static void JapaneseGarden(KitContext k)
        {
            k.Ribbon(new List<Vector2> { new Vector2(0f, 3f), new Vector2(0f, 14f), new Vector2(2f, 24f), new Vector2(0f, 38f) }, 2.4f, Pal.Hex("#C8C0B0"), 0.05f, 0f, "path");
            k.Put("torii_gate", 0f, 11f, 0f);
            k.Put("pagoda", 0f, 46f, 180f);
            k.Patch(-11f, 20f, 8f, 6f, Pal.Hex("#3E8AB0"), 0.1f, 0.18f, 0.12f, 22, "pond");
            k.Put("bridge", -11f, 20f, 90f, new Vector3(0.6f, 0.5f, 0.75f));
            for (int i = 0; i < 4; i++) k.Put("stone_lantern", (i % 2 == 0 ? -1.8f : 1.8f), 5f + (i / 2) * 9f, 0f);
            k.Scatter("cherry_tree", k.N(30), 7f, 55f, 0.9f, 1.5f, null, (x, z) => Mathf.Abs(x) > 3.5f || z < 0f);
            k.Scatter("bamboo", k.N(34), 14f, 60f, 0.8f, 1.5f, null, (x, z) => x > 6f);
            k.Scatter("zen_rock", k.N(16), 4f, 30f, 0.6f, 1.8f, null, (x, z) => Mathf.Abs(x) > 2.5f);
            k.Scatter("bush", k.N(26), 5f, 40f, 0.6f, 1.2f, null, (x, z) => Mathf.Abs(x) > 2.5f);
            k.Scatter("pine_tree", k.N(40), 40f, k.Reach, 0.9f, 1.6f);
            k.Scatter("flower_patch", k.N(30), 4f, 24f, 0.8f, 1.4f, null, (x, z) => Mathf.Abs(x) > 2f);
            for (int i = 0; i < k.N(12); i++) { float a = k.Rand() * Mathf.PI * 2f, r = k.Range(6f, 30f); k.Float("paper_lantern", Mathf.Cos(a) * r, k.Range(4f, 14f), Mathf.Sin(a) * r, 0f, k.Range(0.8f, 1.4f)); }
            k.Put("mountain", 120f, 250f, 0f, 2f, Pal.Hex("#D8C8E0"));
        }

        static void Ruins(KitContext k)
        {
            // a temple: two rows of columns leading away, some fallen
            for (int i = 0; i < 7; i++)
            {
                float z = 10f + i * 5f;
                foreach (float x in new[] { -6f, 6f })
                {
                    if (k.Chance(0.2f)) { k.Put("greek_column", x + k.Range(-2f, 2f), z, k.Rand() * 360f, new Vector3(1f, k.Range(0.25f, 0.5f), 1f)); continue; }
                    k.Put("greek_column", x, z, 0f, new Vector3(1f, k.Range(0.9f, 1.15f), 1f));
                }
            }
            k.Put("ruined_arch", 0f, 46f, 0f, 2.2f);
            k.Put("statue", 0f, 30f, 180f, 1.3f);
            k.Put("obelisk", -16f, 20f, 0f);
            k.Put("fountain", 0f, 9f, 0f, 0.8f, Pal.Hex("#D8D0C0"));
            k.Scatter("ruined_arch", k.N(6), 20f, 70f, 0.8f, 1.5f, null, (x, z) => Mathf.Abs(x) > 12f);
            k.Scatter("greek_column", k.N(16), 14f, 70f, 0.5f, 1.1f, null, (x, z) => Mathf.Abs(x) > 10f);
            k.Ring("standing_stone", 9, 9f, 1f, true, null, 0.1f, 26f, 16f);
            k.Scatter("boulder", k.N(16), 8f, 70f, 0.6f, 1.6f, Pal.Hex("#D8D0C0"));
            k.Scatter("rock", k.N(40), 4f, 60f, 0.7f, 2.2f, Pal.Hex("#D8D0C0"));
            k.Scatter("cypress_tree", k.N(34), 16f, k.Reach, 0.8f, 1.4f, null, (x, z) => Mathf.Abs(x) > 9f);
            k.Scatter("bush", k.N(30), 6f, 60f, 0.7f, 1.3f, null, (x, z) => Mathf.Abs(x) > 4f);
            k.Scatter("tall_grass", k.N(70), 4f, 50f, 0.8f, 1.6f, Pal.Hex("#D8C878"));
        }

        // ================================================================ moods

        static void Synthwave(KitContext k)
        {
            // the road runs straight ahead into the sun
            k.Ribbon(new List<Vector2> { new Vector2(0f, -40f), new Vector2(0f, 118f) }, 9f, Pal.Hex("#14101E"), 0.03f, 0f, "road");
            var mb = new MeshBuilder();
            mb.Emit(1f).Col(Pal.Hex("#FFE14A"));
            for (float z = -36f; z < 116f; z += 7f) mb.Quad(new Vector3(-0.12f, 0.06f, z), new Vector3(0.12f, 0.06f, z), new Vector3(0.12f, 0.06f, z + 3.2f), new Vector3(-0.12f, 0.06f, z + 3.2f), Vector3.up);
            mb.Col(Pal.Hex("#39F5E0"));
            foreach (float x in new[] { -4.4f, 4.4f }) mb.Quad(new Vector3(x - 0.08f, 0.06f, -40f), new Vector3(x + 0.08f, 0.06f, -40f), new Vector3(x + 0.08f, 0.06f, 118f), new Vector3(x - 0.08f, 0.06f, 118f), Vector3.up);
            k.AddOwn(mb.Build("road_lines"));
            for (float z = -30f; z < 110f; z += 11f)
            {
                k.Put("palm_tree", -7.5f, z, 90f, 1.1f, Pal.Hex("#FF5AC8"));
                k.Put("palm_tree", 7.5f, z + 5.5f, 270f, 1.1f, Pal.Hex("#5AE0FF"));
            }
            for (int i = 0; i < 7; i++)
            {
                float a = (-70f + i * 23f + k.Range(-6f, 6f)) * Mathf.Deg2Rad;
                k.Put("mountain", Mathf.Sin(a) * 250f, Mathf.Cos(a) * 250f, k.Rand() * 360f, new Vector3(1.4f, k.Range(0.8f, 1.6f), 1.4f), Pal.Hex("#7A3AD0"));
            }
            k.Put("car", 2.2f, 9f, 0f, 1f, Pal.Hex("#FF3A8A"));
            for (int i = 0; i < k.N(8); i++) k.Put("neon_sign", (i % 2 == 0 ? -12f : 12f) + k.Range(-2f, 2f), 12f + i * 12f, i % 2 == 0 ? 90f : 270f, 1.6f, k.Pick(Pal.Hex("#FF4FD8"), Pal.Hex("#39F5E0"), Pal.Hex("#FFE14A")));
            for (int i = 0; i < k.N(14); i++) { float side = k.Chance(0.5f) ? 1f : -1f; k.Put("pyramid", side * k.Range(18f, 70f), k.Range(-20f, 100f), k.Rand() * 90f, k.Range(2f, 9f), Pal.Hex("#3A1A6A")); }
        }

        static void Campsite(KitContext k)
        {
            k.Put("campfire", 0f, 4.2f, 0f);
            for (int i = 0; i < 4; i++)
            {
                float a = (50f + i * 85f) * Mathf.Deg2Rad;
                float x = Mathf.Sin(a) * 2.3f, z = 4.2f + Mathf.Cos(a) * 2.3f;
                k.Put("log", x, z, KitContext.Facing(x, z, 0f, 4.2f) + 90f, 0.7f);
            }
            k.Put("tent", -5.5f, 7f, 130f, 1.2f);
            k.Put("tent", 6f, 8.5f, 220f, 1.2f, Pal.Hex("#7AC0E8"));
            k.Put("picnic_table", 5.5f, 2.5f, 20f);
            k.Loose("lantern", 5.3f, 2.4f, 0.85f);
            k.Loose("guitar", -2.6f, 3.2f, 0.3f, 40f);
            for (int i = 0; i < 3; i++) k.Loose("log", -3.6f + i * 0.5f, 1.6f, 0.2f + i * 0.4f, 90f, 0.35f);
            k.Scatter("pine_tree", k.N(120), 11f, k.Reach, 0.9f, 1.8f);
            k.Scatter("oak_tree", k.N(30), 14f, k.Reach, 0.9f, 1.5f);
            k.Scatter("bush", k.N(40), 8f, 50f, 0.7f, 1.3f);
            k.Scatter("fern", k.N(60), 5f, 34f, 0.8f, 1.5f);
            k.Scatter("rock", k.N(20), 5f, 40f, 0.7f, 2f);
            k.Scatter("tree_stump", k.N(5), 5f, 16f, 0.8f, 1.2f);
            k.Put("deer", -12f, 14f, 100f);
            k.Put("mountain", 100f, 250f, 0f, 2f, Pal.Hex("#5A6A8A"));
        }

        static void Graveyard(KitContext k)
        {
            for (int row = 0; row < 6; row++)
            {
                for (int col = -4; col <= 4; col++)
                {
                    if (col == 0 || !k.Chance(Mathf.Lerp(0.5f, 0.95f, k.Density))) continue;
                    float x = col * 3.2f + k.Range(-0.4f, 0.4f), z = 5f + row * 4f + k.Range(-0.5f, 0.5f);
                    k.Put("gravestone", x, z, 180f + k.Range(-14f, 14f), k.Range(0.8f, 1.4f));
                }
            }
            k.Ribbon(new List<Vector2> { new Vector2(0f, 3f), new Vector2(0f, 30f), new Vector2(6f, 52f) }, 2.2f, Pal.Hex("#3A3A30"), 0.05f, 0f, "path");
            k.Put("haunted_house", 8f, 62f, 190f, 1.4f);
            for (float x = -15f; x <= 15f; x += 3f) if (Mathf.Abs(x) > 2f) k.Put("fence", x, 2.6f, 0f, 1.1f, Pal.Hex("#3A3A40"));
            k.Scatter("dead_tree", k.N(30), 6f, k.Reach, 0.9f, 1.8f, null, (x, z) => Mathf.Abs(x) > 16f || z > 30f || z < 0f);
            k.Scatter("rock", k.N(20), 4f, 50f, 0.7f, 2f, Pal.Hex("#6A6A74"));
            k.Scatter("tall_grass", k.N(60), 4f, 40f, 0.8f, 1.6f, Pal.Hex("#5A6A4A"));
            k.Put("jack_o_lantern", -1.6f, 3.6f, 160f);
            k.Put("jack_o_lantern", 1.7f, 3.4f, 200f, 0.8f);
            k.Put("cauldron", 4.5f, 5f, 0f);
            k.Put("spider_web", -3.4f, 2.7f, 0f);
            k.Put("skeleton", -6.4f, 9f, 150f);
            for (int i = 0; i < k.N(6); i++) { float a = k.Rand() * Mathf.PI * 2f, r = k.Range(7f, 24f); k.Float("ghost", Mathf.Cos(a) * r, k.Range(1.2f, 4f), Mathf.Sin(a) * r, k.Rand() * 360f, k.Range(0.9f, 1.3f)); }
            for (int i = 0; i < k.N(14); i++) { float a = k.Rand() * Mathf.PI * 2f, r = k.Range(6f, 40f); k.Float("bat", Mathf.Cos(a) * r, k.Range(5f, 16f), Mathf.Sin(a) * r, k.Rand() * 360f, k.Range(1f, 2f)); }
        }

        static void Candyland(KitContext k)
        {
            k.Ribbon(new List<Vector2> { new Vector2(0f, 3f), new Vector2(-4f, 16f), new Vector2(3f, 30f), new Vector2(0f, 46f) }, 3f, Pal.Hex("#FFF2B0"), 0.05f, 0f, "path");
            k.Put("cake", 0f, 52f, 0f, 22f);
            Color[] sweet = { Pal.Pink, Pal.Hex("#8AD8F0"), Pal.Yellow, Pal.Hex("#B4F0A0"), Pal.Hex("#D8A8FF"), Pal.Orange };
            for (int i = 0; i < k.N(40); i++) { float a = k.Rand() * Mathf.PI * 2f, r = k.Range(7f, k.Reach * 0.8f); k.Put("lollipop", Mathf.Sin(a) * r, Mathf.Cos(a) * r, k.Rand() * 360f, k.Range(1f, 2.6f), k.Pick(sweet)); }
            k.Scatter("candy_cane", k.N(34), 6f, k.Reach * 0.7f, 1f, 2.6f);
            for (int i = 0; i < k.N(20); i++) { float a = k.Rand() * Mathf.PI * 2f, r = k.Range(8f, 60f); k.Put("cupcake", Mathf.Sin(a) * r, Mathf.Cos(a) * r, k.Rand() * 360f, k.Range(8f, 22f), k.Pick(sweet)); }
            for (int i = 0; i < k.N(14); i++) { float a = k.Rand() * Mathf.PI * 2f, r = k.Range(6f, 50f); k.Put("donut", Mathf.Sin(a) * r, Mathf.Cos(a) * r, k.Rand() * 360f, k.Range(6f, 16f), k.Pick(sweet)); }
            for (int i = 0; i < k.N(10); i++) { float a = k.Rand() * Mathf.PI * 2f, r = k.Range(10f, 60f); k.Put("ice_cream", Mathf.Sin(a) * r, Mathf.Cos(a) * r, k.Rand() * 360f, k.Range(12f, 26f), null, 2f); }
            k.Put("medieval_house", -11f, 13f, 120f, 0.9f, Pal.Hex("#E8B080"));
            for (int i = 0; i < 6; i++) k.Loose(k.Pick("donut", "cupcake", "ice_cream"), k.Range(-2f, 2f), k.Range(2.4f, 4f), 0.2f, 0f, k.Range(1f, 1.6f));
            for (int i = 0; i < k.N(16); i++) { float a = k.Rand() * Mathf.PI * 2f, r = k.Range(8f, 50f); k.Float("cloud", Mathf.Cos(a) * r * 2f, k.Range(26f, 50f), Mathf.Sin(a) * r * 2f, k.Rand() * 360f, k.Range(0.8f, 2f), Pal.Hex("#FFD8EC")); }
            k.Put("rainbow", 0f, 150f, 0f, 2.6f);
        }

        // ================================================================ play

        static void Playground(KitContext k)
        {
            k.Patch(0f, 10f, 15f, 12f, Pal.Hex("#D8B878"), 0.05f, 0.06f, 0f, 24, "sand");
            k.Put("swing_set", -7f, 11f, 150f);
            k.Put("slide", 6.5f, 13f, 200f);
            k.Put("seesaw", 0f, 15f, 30f);
            k.Put("trampoline", -3.5f, 6f, 0f);
            k.Solid(-3.5f, 6f, new Vector3(2.2f, 0.45f, 2.2f), 0f, 0f, 1f);
            k.Put("ramp", 4f, 6.5f, 180f);
            k.Solid(4f, 6.5f, new Vector3(2f, 0.5f, 3f));
            k.Put("fountain", 0f, 30f, 0f);
            k.Ring("bench", 6, 20f, 1f, true, null, 0.1f, 0f, 10f, 30f);
            k.Ring("street_lamp", 5, 24f, 0.8f, true, null, 0.1f, 0f, 10f, 0f);
            k.Loose("soccer_ball", 0.8f, 3.2f, 0.2f);
            k.Loose("basketball", -1.2f, 3.4f, 0.2f);
            k.Loose("beach_ball", 2.2f, 4.2f, 0.3f);
            k.Loose("frisbee", -2.4f, 4.4f, 0.1f);
            for (int i = 0; i < 5; i++) k.Loose("building_block", -0.6f + i * 0.34f, 5.2f, 0.05f, 0f, 1f, k.Pick(Pal.Red, Pal.Blue, Pal.Yellow, Pal.Green));
            string[] folk = { "child", "child", "person", "woman", "dog" };
            foreach (string f in folk) { float a = k.Rand() * Mathf.PI * 2f, r = k.Range(8f, 18f); k.Put(f, Mathf.Sin(a) * r, 10f + Mathf.Cos(a) * r * 0.6f, k.Rand() * 360f); }
            k.Scatter("oak_tree", k.N(46), 24f, k.Reach, 0.9f, 1.6f);
            k.Scatter("bush", k.N(34), 20f, k.Reach * 0.7f, 0.7f, 1.3f);
            k.Scatter("flower_patch", k.N(50), 16f, 50f, 0.8f, 1.5f);
            for (int i = 0; i < 6; i++) k.Loose("balloon", k.Range(-5f, 5f), k.Range(6f, 12f), k.Range(1f, 3f), 0f, 1f, k.Pick(Pal.Red, Pal.Blue, Pal.Yellow, Pal.Green, Pal.Pink));
        }

        static void ArtGallery(KitContext k)
        {
            int count = k.N(10);
            for (int i = 0; i < count; i++)
            {
                float a = (i + 0.5f) / count * Mathf.PI * 2f, r = 9f;
                float x = Mathf.Sin(a) * r, z = Mathf.Cos(a) * r;
                k.Put("painting", x, z, Face(x, z), k.Range(1.3f, 2f));
            }
            for (int i = 0; i < k.N(6); i++)
            {
                float a = (i + 0.25f) / 6f * Mathf.PI * 2f, r = 15f;
                k.Put("sculpture", Mathf.Sin(a) * r, Mathf.Cos(a) * r, k.Rand() * 360f, k.Range(1.2f, 2.2f), k.Pick(Pal.White, Pal.Hex("#E8E0D0"), Pal.Hex("#2A2C30")));
            }
            k.Ring("greek_column", 12, 22f, 1.4f, false, Pal.White);
            k.Put("easel", 1.6f, 2.6f, 200f);
            Color[] paint = { Pal.Red, Pal.Blue, Pal.Yellow, Pal.Green, Pal.Purple };
            for (int i = 0; i < 5; i++) k.Loose("paint_bucket", -1.2f - i * 0.36f, 2.5f + (i % 2) * 0.3f, 0.05f, 0f, 1f, paint[i]);
            k.Put("bench", 0f, 5.5f, 180f);
            k.Put("rug", 0f, 3f, 0f, 1.6f);
            for (int i = 0; i < k.N(5); i++) { float a = k.Rand() * Mathf.PI * 2f; k.Put(k.Pick("person", "woman"), Mathf.Sin(a) * 6.5f, Mathf.Cos(a) * 6.5f, a * Mathf.Rad2Deg); }
        }

        static void SportsCourt(KitContext k)
        {
            // basketball half court ahead
            var mb = new MeshBuilder();
            mb.Col(Pal.White);
            void Line(float x0, float z0, float x1, float z1) { mb.Quad(new Vector3(x0, 0.03f, z0), new Vector3(x1, 0.03f, z0), new Vector3(x1, 0.03f, z1), new Vector3(x0, 0.03f, z1), Vector3.up); }
            Line(-7.5f, 2f, 7.5f, 2.1f); Line(-7.5f, 15.9f, 7.5f, 16f); Line(-7.5f, 2f, -7.4f, 16f); Line(7.4f, 2f, 7.5f, 16f);
            Line(-2.4f, 10.2f, 2.4f, 10.3f); Line(-2.4f, 10.2f, -2.3f, 16f); Line(2.3f, 10.2f, 2.4f, 16f);
            k.AddOwn(mb.Build("court_lines"));
            k.Patch(0f, 9f, 7.4f, 6.9f, Pal.Hex("#C8703A"), 0.02f, 0f, 0f, 4, "court");
            k.Put("basketball_hoop", 0f, 16.4f, 180f);
            k.Solid(0f, 16.35f, new Vector3(1.8f, 1.05f, 0.1f), 0f, 3f);
            k.Zone(new Vector3(0f, k.Ground(0f, 16f) + 2.85f, 16.12f), 0.2f, "basketball", "Swish. Two points.");
            for (int i = 0; i < 3; i++) k.Loose("basketball", -0.7f + i * 0.7f, 3.2f, 0.2f);
            // football goal behind
            k.Put("soccer_goal", 0f, -14f, 0f);
            k.Zone(new Vector3(0f, k.Ground(0f, -14f) + 1f, -14.9f), 1.6f, "soccer_ball", "Goal!");
            k.Loose("soccer_ball", 1.8f, -3.4f, 0.2f);
            k.Loose("soccer_ball", -1.6f, -3.6f, 0.2f);
            k.Put("bench", -9.5f, 6f, 90f); k.Put("bench", -9.5f, 11f, 90f);
            k.Ring("stage_light", 4, 19f, 2.2f, true, null, 0f, 0f, 6f, 45f);
            k.Scatter("person", k.N(8), 11f, 20f, 0.95f, 1.1f, null, (x, z) => Mathf.Abs(x) > 9f);
            k.Scatter("oak_tree", k.N(40), 30f, k.Reach, 0.9f, 1.6f);
            k.Put("trophy", 9f, 3f, 0f, 2f);
        }

        static void Bowling(KitContext k)
        {
            const float lane = 16f;
            k.Ribbon(new List<Vector2> { new Vector2(0f, 2.6f), new Vector2(0f, lane + 2f) }, 1.4f, Pal.Hex("#D8A868"), 0.03f, 0f, "lane");
            foreach (float x in new[] { -0.95f, 0.95f })
            {
                k.Ribbon(new List<Vector2> { new Vector2(x, 2.6f), new Vector2(x, lane + 2f) }, 0.3f, Pal.Hex("#3A3C42"), 0.02f, 0f, "gutter");
                k.Solid(x * 1.6f, lane * 0.5f + 2f, new Vector3(0.3f, 0.35f, lane + 2f));
            }
            k.Solid(0f, lane + 3f, new Vector3(4f, 1.2f, 0.4f));
            // ten pins in a triangle
            int n = 0;
            for (int row = 0; row < 4; row++)
                for (int i = 0; i <= row; i++, n++)
                    k.Loose("bowling_pin", (i - row * 0.5f) * 0.31f, lane + row * 0.27f, 0.03f);
            Color[] balls = { Pal.Hex("#2A3A8A"), Pal.Hex("#8A2A3A"), Pal.Hex("#2A8A4A"), Pal.Hex("#8A6A1A") };
            for (int i = 0; i < 4; i++) k.Loose("bowling_ball", 1.4f + (i % 2) * 0.3f, 2.2f + (i / 2) * 0.3f, 0.3f, 0f, 1f, balls[i]);
            k.Put("neon_sign", -4f, lane + 4f, 180f, 1.4f, Pal.Hex("#FF4FD8"));
            k.Put("neon_sign", 4f, lane + 4f, 180f, 1.4f, Pal.Hex("#39F5E0"));
            k.Put("bench", -2.6f, 1.6f, 0f);
            for (int i = 0; i < 4; i++) k.Put("stage_light", (i % 2 == 0 ? -3.5f : 3.5f), 5f + (i / 2) * 8f, i % 2 == 0 ? 90f : 270f, 1f, k.Pick(Pal.Pink, Pal.Cyan, Pal.Yellow));
        }

        // ================================================================ wild and far

        static void DinosaurValley(KitContext k)
        {
            System.Func<float, float, bool> floor = (x, z) => Mathf.Abs(x) < 34f;
            k.Scatter("palm_tree", k.N(60), 8f, k.Reach, 1.2f, 2.4f);
            k.Scatter("fern", k.N(140), 3.5f, 60f, 1.5f, 5f);
            k.Scatter("giant_mushroom", k.N(8), 14f, 70f, 0.5f, 1.2f, Pal.Hex("#C8E0B0"));
            k.Scatter("oak_tree", k.N(50), 14f, k.Reach, 1.2f, 2.2f);
            k.Scatter("boulder", k.N(22), 8f, k.Reach, 0.8f, 2.6f);
            k.Scatter("rock_spire", k.N(8), 30f, k.Reach, 1f, 2.4f);
            k.Put("brontosaurus", -14f, 34f, 110f);
            k.Put("brontosaurus", 26f, 62f, 250f, 0.8f);
            k.Put("t_rex", 13f, 22f, 215f);
            k.Scatter("bird", k.N(8), 10f, 50f, 2f, 4f);
            k.Put("volcano", 20f, 230f, 0f, 2.2f);
            k.Patch(-7f, 14f, 5f, 3.6f, Pal.Hex("#3E8AA0"), 0.1f, 0.2f, 0.1f, 18, "pool");
            for (int i = 0; i < 3; i++) k.Loose("bone", k.Range(-1.5f, 1.5f), k.Range(2.4f, 3.6f), 0.1f, k.Rand() * 360f, 2.5f);
        }

        static void Safari(KitContext k)
        {
            // flat-topped acacias: squashed, widened oaks
            for (int i = 0; i < k.N(44); i++)
            {
                float a = k.Rand() * Mathf.PI * 2f, r = k.Range(10f, k.Reach);
                float s = k.Range(0.9f, 1.5f);
                k.Put("oak_tree", Mathf.Sin(a) * r, Mathf.Cos(a) * r, k.Rand() * 360f, new Vector3(s * 1.7f, s * 0.8f, s * 1.7f), Pal.Hex("#B8C870"));
            }
            k.Scatter("tall_grass", k.N(240), 3.5f, 70f, 1f, 2.4f, Pal.Hex("#E0C868"));
            k.Scatter("bush", k.N(40), 8f, k.Reach * 0.8f, 0.7f, 1.3f, Pal.Hex("#C8C070"));
            k.Scatter("rock", k.N(24), 5f, 60f, 0.8f, 2.4f, Pal.Hex("#C8A888"));
            k.Scatter("boulder", k.N(8), 14f, 70f, 0.8f, 2f, Pal.Hex("#C8A888"));
            k.Put("elephant", -12f, 22f, 110f);
            k.Put("elephant", -17f, 27f, 95f, 0.7f);
            k.Put("giraffe", 14f, 28f, 250f);
            k.Put("giraffe", 20f, 34f, 230f, 0.85f);
            k.Put("lion", 6f, 11f, 205f);
            k.Put("lion", 8.2f, 12.6f, 190f, 0.9f);
            k.Scatter("deer", k.N(7), 26f, 55f, 0.8f, 1f);
            k.Scatter("bird", k.N(7), 10f, 60f, 1.6f, 3f);
            k.Patch(-4f, 34f, 9f, 6f, Pal.Hex("#5A8AA0"), 0.1f, 0.2f, 0.1f, 20, "waterhole");
            k.Put("mountain", -120f, 270f, 0f, new Vector3(2.4f, 1.2f, 2.4f), Pal.Hex("#C8A8A0"));
        }

        static void Fairground(KitContext k)
        {
            k.Put("ferris_wheel", 0f, 52f, 0f);
            Color[] cloth = { Pal.Red, Pal.Blue, Pal.Yellow, Pal.Purple, Pal.Teal };
            int tents = k.N(9);
            for (int i = 0; i < tents; i++)
            {
                float a = (i + 0.5f) / tents * Mathf.PI * 2f, r = k.Range(13f, 20f);
                float x = Mathf.Sin(a) * r, z = Mathf.Cos(a) * r;
                k.Put("tent", x, z, Face(x, z) + 180f, k.Range(1.8f, 2.6f), k.Pick(cloth));
                k.Put("street_lamp", x * 0.7f, z * 0.7f, Face(x, z), 0.8f);
            }
            k.Put("stage_light", -4f, 8f, 30f, 1f, Pal.Pink); k.Put("stage_light", 4f, 8f, 330f, 1f, Pal.Cyan);
            k.Put("speaker", -6f, 6f, 140f); k.Put("speaker", 6f, 6f, 220f);
            string[] folk = { "person", "woman", "child", "person", "child", "woman", "pirate", "wizard" };
            for (int i = 0; i < k.N(14); i++) { float a = k.Rand() * Mathf.PI * 2f, r = k.Range(5f, 14f); k.Put(folk[i % folk.Length], Mathf.Sin(a) * r, Mathf.Cos(a) * r, k.Rand() * 360f, 1f, k.Pick(Pal.White, Pal.Hex("#FFE0C0"), Pal.Hex("#C8E0FF"))); }
            for (int i = 0; i < k.N(16); i++) k.Loose("balloon", k.Range(-9f, 9f), k.Range(3f, 12f), k.Range(0.8f, 4f), 0f, 1f, k.Pick(cloth));
            k.Loose("confetti_popper", 0.8f, 3f, 1f);
            for (int i = 0; i < 3; i++) k.Loose("bowling_pin", -1.6f + i * 0.3f, 4f, 0.05f);
            k.Loose("baseball", -0.6f, 3f, 0.9f);
            k.Float("hot_air_balloon", -40f, 34f, 70f, 0f, 1f);
            k.Float("hot_air_balloon", 55f, 48f, 90f, 0f, 0.8f, Pal.Hex("#C8E8FF"));
            k.Scatter("oak_tree", k.N(40), 34f, k.Reach, 0.9f, 1.6f);
        }

        static void Harbor(KitContext k)
        {
            System.Func<float, float, bool> dry = (x, z) => Dry(k, x, z, 0.5f);
            k.Put("pier", 0f, 9f, 0f, new Vector3(1f, 1f, 1.6f), null, -0.6f);
            k.Solid(0f, 18f, new Vector3(2.4f, 0.3f, 19f), 0f, 0.15f);
            k.Put("lighthouse", -34f, 4f, 0f);
            k.Scatter("house", k.N(7), 14f, 46f, 0.9f, 1.2f, null, (x, z) => dry(x, z) && z < -4f);
            k.Scatter("shop", k.N(3), 12f, 30f, 0.9f, 1.1f, null, (x, z) => dry(x, z) && z < -4f);
            for (int i = 0; i < k.N(8); i++) k.Loose(k.Chance(0.5f) ? "crate" : "barrel", k.Range(-6f, 6f), k.Range(-2f, 3f), 0.1f, k.Rand() * 90f);
            k.Scatter("boat", k.N(6), 12f, 34f, 0.9f, 1.2f, null, (x, z) => z > 10f && Mathf.Abs(x) > 3f);
            k.Scatter("sailboat", k.N(6), 24f, 90f, 0.9f, 1.4f, null, (x, z) => z > 20f);
            k.Scatter("pirate_ship", 1, 70f, 100f, 1f, 1f, null, (x, z) => z > 50f);
            k.Put("street_lamp", 3.4f, 3.4f, 270f, 0.8f); k.Put("street_lamp", -3.4f, 3.4f, 90f, 0.8f);
            k.Put("bench", 5f, -1f, 0f);
            k.Loose("fishing_rod", 1f, 2.6f, 0.9f);
            string[] folk = { "pirate", "person", "woman" };
            foreach (string f in folk) k.Put(f, k.Range(-8f, 8f), k.Range(-6f, 1f), k.Rand() * 360f);
            k.Scatter("oak_tree", k.N(30), 30f, k.Reach, 0.9f, 1.5f, null, (x, z) => dry(x, z) && z < -10f);
            k.Scatter("rock", k.N(24), 6f, 50f, 0.8f, 2.4f, null, dry);
            for (int i = 0; i < 6; i++) k.Float("bird", k.Range(-20f, 20f), k.Range(8f, 20f), k.Range(8f, 40f), k.Rand() * 360f, 1.8f);
        }

        static void WesternTown(KitContext k)
        {
            k.Ribbon(new List<Vector2> { new Vector2(0f, -30f), new Vector2(0f, 90f) }, 12f, Pal.Hex("#B89868"), 0.04f, 0f, "street");
            for (int i = 0; i < k.N(6); i++)
            {
                float z = 10f + i * 13f;
                k.Put("saloon", -14f, z, 90f, k.Range(0.9f, 1.1f), k.Pick(Pal.White, Pal.Hex("#E8D0B0"), Pal.Hex("#D8B8A0")));
                k.Put("saloon", 14f, z + 6f, 270f, k.Range(0.9f, 1.1f), k.Pick(Pal.White, Pal.Hex("#E8D0B0"), Pal.Hex("#C8D0C0")));
            }
            k.Put("horse", -6.6f, 9f, 80f); k.Put("horse", 6.8f, 16f, 280f);
            k.Put("well", 0f, 34f, 0f);
            for (int i = 0; i < k.N(8); i++) k.Loose(k.Chance(0.6f) ? "barrel" : "crate", (k.Chance(0.5f) ? -1f : 1f) * k.Range(6.5f, 8f), k.Range(5f, 40f), 0.1f, k.Rand() * 90f);
            for (int i = 0; i < 4; i++) k.Loose("tumbleweed", k.Range(-4f, 4f), k.Range(5f, 20f), 0.4f);
            k.Put("cow_skull", 3f, 4.5f, 210f);
            k.Scatter("cactus", k.N(40), 24f, k.Reach, 0.7f, 1.6f, null, (x, z) => Mathf.Abs(x) > 24f || z < 0f || z > 90f);
            k.Scatter("rock_spire", k.N(14), 40f, k.Reach, 1f, 3f);
            k.Scatter("boulder", k.N(14), 26f, k.Reach, 0.8f, 2f, Pal.Hex("#D8A880"));
            k.Scatter("dead_tree", k.N(6), 26f, 70f, 0.7f, 1.1f);
            k.Put("person", -5.5f, 12f, 100f, 1f, Pal.Hex("#C8A878"));
            k.Put("mountain", 160f, 250f, 0f, new Vector3(2f, 1f, 2f), Pal.Hex("#D89870"));
            k.Put("mountain", -170f, 220f, 60f, new Vector3(2.4f, 0.9f, 2.4f), Pal.Hex("#D89870"));
        }

        static void VolcanoLand(KitContext k)
        {
            k.Put("volcano", 10f, 150f, 0f, 2f);
            // lava rivers wind down from the cone
            Color lava = Pal.Hex("#FF6A1A");
            for (int r = 0; r < 3; r++)
            {
                var path = new List<Vector2>();
                float x = 10f + (r - 1) * 26f, z = 105f;
                for (int i = 0; i < 9; i++)
                {
                    path.Add(new Vector2(x, z));
                    x += k.Range(-9f, 9f) + (r - 1) * 3f; z -= k.Range(9f, 14f);
                    if (Mathf.Sqrt(x * x + z * z) < 9f) break;
                }
                k.Ribbon(path, k.Range(3f, 5f), lava, 0.12f, 1f, "lava");
            }
            k.Patch(-9f, 12f, 4.5f, 3.2f, lava, 0.12f, 0.2f, 1f, 18, "lava_pool");
            k.Patch(11f, 20f, 6f, 4f, lava, 0.12f, 0.2f, 1f, 18, "lava_pool");
            k.Scatter("rock_spire", k.N(40), 8f, k.Reach, 0.7f, 2.6f, Pal.Hex("#5A4A48"));
            k.Scatter("boulder", k.N(40), 6f, k.Reach, 0.7f, 2.2f, Pal.Hex("#4A3E3E"));
            k.Scatter("rock", k.N(60), 4f, 60f, 0.7f, 2.4f, Pal.Hex("#4A3E3E"));
            k.Scatter("dead_tree", k.N(26), 8f, 80f, 0.8f, 1.6f, Pal.Hex("#3A3030"));
            k.Scatter("crystal", k.N(12), 8f, 60f, 0.8f, 2f, Pal.Hex("#FF8A5A"));
            k.Put("dragon", -16f, 30f, 140f, 1.3f);
            k.Put("skeleton", 3.4f, 5f, 210f);
        }

        static void CloudKingdom(KitContext k)
        {
            k.Put("disc", 0f, 0f, 0f, new Vector3(7f, 2.5f, 7f), Pal.Hex("#F4F0FF"), -0.18f);
            k.Solid(0f, 0f, new Vector3(7f, 0.2f, 7f), 0f, -0.2f);
            // a sea of cloud below, and banks of it around
            for (int i = 0; i < k.N(70); i++)
            {
                float a = k.Rand() * Mathf.PI * 2f, r = k.Range(6f, 110f);
                k.Float("cloud", Mathf.Cos(a) * r, k.Range(-14f, -4f), Mathf.Sin(a) * r, k.Rand() * 360f, k.Range(1.5f, 4.5f));
            }
            for (int i = 0; i < k.N(24); i++)
            {
                float a = k.Rand() * Mathf.PI * 2f, r = k.Range(25f, 110f);
                k.Float("cloud", Mathf.Cos(a) * r, k.Range(4f, 40f), Mathf.Sin(a) * r, k.Rand() * 360f, k.Range(1f, 3f), Pal.Hex("#FFE8F4"));
            }
            k.Float("rainbow", 0f, -12f, 120f, 0f, 2.4f);
            k.Float("hot_air_balloon", -24f, 2f, 40f, 0f, 1f);
            k.Float("hot_air_balloon", 34f, 12f, 60f, 0f, 0.8f, Pal.Hex("#C8E8FF"));
            k.Float("castle_tower", 0f, -6f, 70f, 0f, 2.2f, Pal.White);
            k.Float("castle_tower", -14f, -9f, 80f, 0f, 1.6f, Pal.White);
            k.Float("castle_tower", 14f, -9f, 80f, 0f, 1.6f, Pal.White);
            for (int i = 0; i < k.N(16); i++) { float a = k.Rand() * Mathf.PI * 2f, r = k.Range(5f, 30f); k.Float("paper_lantern", Mathf.Cos(a) * r, k.Range(1f, 14f), Mathf.Sin(a) * r, 0f, k.Range(0.8f, 1.6f), Pal.Hex("#FFE8A0")); }
            for (int i = 0; i < k.N(10); i++) { float a = k.Rand() * Mathf.PI * 2f, r = k.Range(8f, 40f); k.Float("bird", Mathf.Cos(a) * r, k.Range(3f, 20f), Mathf.Sin(a) * r, k.Rand() * 360f, 2f, Pal.White); }
            for (int i = 0; i < 6; i++) k.Loose("balloon", k.Range(-2.5f, 2.5f), k.Range(-2.5f, 2.5f) + 0.5f, k.Range(0.6f, 1.6f), 0f, 1f, k.Pick(Pal.Red, Pal.Yellow, Pal.Pink, Pal.Cyan));
            k.Loose("energy_ball", 1f, 2f, 1f);
        }
    }
}
