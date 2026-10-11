# Held-item expansion

100 new built-in, low-poly held-item recipes. All are real physics props, support `give` and `spawn`, and integrate with the existing grab/throw and event system. Five existing recipes also gain behavior: fishing_rod, boomerang, frisbee, flashlight and lantern.

## Controls and behavior

| Family | Operation |
|---|---|
| Firearms | Trigger fires; automatic weapons repeat while held. Distinct barrel lengths, grip details, stock/scope/drum arrangements, cadence, pellets, spread and projectile effects. No magazine/reload system. |
| Grenades | Trigger explains priming; deliberate release starts a 2.5-second fuse, even at low throw speed for desktop use. Forced removal does not arm them. Explosive/fire variants apply radial physics force and knock creatures down; smoke, confetti, paint, splash, freeze and stun variants use their own effects. Single use. Sticky grenades stick on collision until detonation. |
| Fishing rods | Trigger casts an animated line and visible bobber toward the aimed location; another press reels in. Bobbers settle at the water/ground height. Releasing/removing the rod cleans up the line and bobber. No fish-catching simulation. |
| Boomerangs | Deliberate throws travel outward, then steer smoothly toward the throwing hand and wait nearby for another grab. Return sweeps respect obstacles; after eight seconds a blocked return falls normally. |
| Discs | Flat throws spin and glide using bounded, speed-dependent lift/drag. Collisions end assisted flight. Stylized, not a full aerodynamic simulator. |
| Melee tools | Swinging uses existing creature-hit and physics interaction. Axes do not automatically destroy buildings or cut terrain. Powered tools additionally make sparks and short-range strikes while triggered. |
| Lights | Trigger toggles flashlight cones or lantern illumination in the custom object/terrain shaders. Four active portable lights maximum, no real-time shadows. |
| Other gadgets | Trigger actions: aiming beam, scan/identify, local radio chirp, restore loose-object colour/orientation, welding sparks, cooling spray, bubbles, confetti, game screenshots, ground grappling, safe-ground teleport, chimes. |
| Drinks and spray cans | Drinks have three trigger-activated servings and become empty; spray cans recolour targeted loose props. No hydration, healing or inventory system. |

## New items

| Item ID | Family | Built-in action |
|---|---|---|
| `compact_pistol` | firearms | firearm |
| `heavy_pistol` | firearms | firearm |
| `service_pistol` | firearms | firearm |
| `machine_pistol` | firearms | firearm |
| `target_pistol` | firearms | firearm |
| `snub_revolver` | firearms | firearm |
| `long_revolver` | firearms | firearm |
| `lever_rifle` | firearms | firearm |
| `bolt_rifle` | firearms | firearm |
| `hunting_rifle` | firearms | firearm |
| `carbine` | firearms | firearm |
| `assault_carbine` | firearms | firearm |
| `battle_rifle` | firearms | firearm |
| `marksman_rifle` | firearms | firearm |
| `pump_shotgun` | firearms | firearm |
| `double_shotgun` | firearms | firearm |
| `tactical_shotgun` | firearms | firearm |
| `compact_smg` | firearms | firearm |
| `heavy_smg` | firearms | firearm |
| `rotary_gun` | firearms | firearm |
| `plasma_pistol` | science fiction | firearm |
| `plasma_rifle` | science fiction | firearm |
| `pulse_carbine` | science fiction | firearm |
| `ion_blaster` | science fiction | firearm |
| `railgun` | science fiction | firearm |
| `gauss_rifle` | science fiction | firearm |
| `tesla_gun` | science fiction | firearm |
| `stun_pistol` | science fiction | firearm |
| `freeze_ray` | science fiction | firearm |
| `flame_projector` | science fiction | firearm |
| `rocket_launcher` | science fiction | firearm |
| `grenade_launcher` | science fiction | firearm |
| `sonic_blaster` | science fiction | firearm |
| `gravity_gauntlet` | science fiction | wand |
| `tractor_wand` | science fiction | wand |
| `hand_axe` | tools | melee |
| `battle_axe` | tools | melee |
| `fire_axe` | tools | melee |
| `prospector_pick` | tools | melee |
| `hatchet` | tools | melee |
| `machete` | tools | melee |
| `katana` | tools | melee |
| `rapier` | tools | melee |
| `broadsword` | tools | melee |
| `dagger` | tools | melee |
| `spear` | tools | melee |
| `trident` | tools | melee |
| `war_hammer` | tools | melee |
| `crowbar` | tools | melee |
| `wrench` | tools | melee |
| `screwdriver` | tools | melee |
| `folding_shovel` | tools | melee |
| `chainsaw` | tools | powered_tool |
| `mining_drill` | tools | powered_tool |
| `energy_saber` | tools | melee |
| `frag_grenade` | throwables | explosive |
| `sticky_grenade` | throwables | explosive |
| `smoke_grenade` | throwables | explosive |
| `flash_grenade` | throwables | explosive |
| `confetti_grenade` | throwables | explosive |
| `plasma_grenade` | throwables | explosive |
| `paint_grenade` | throwables | explosive |
| `water_grenade` | throwables | explosive |
| `snow_grenade` | throwables | explosive |
| `spark_grenade` | throwables | explosive |
| `wooden_boomerang` | throwables | returning |
| `hunting_boomerang` | throwables | returning |
| `energy_boomerang` | throwables | returning |
| `sport_frisbee` | throwables | disc |
| `flying_disc` | throwables | disc |
| `sport_fishing_rod` | fishing | fishing |
| `fly_rod` | fishing | fishing |
| `sea_fishing_rod` | fishing | fishing |
| `ice_fishing_rod` | fishing | fishing |
| `telescopic_rod` | fishing | fishing |
| `tactical_flashlight` | gadgets | light |
| `camp_lantern` | gadgets | light |
| `laser_pointer` | gadgets | beam |
| `scanner` | gadgets | scan |
| `communicator` | gadgets | signal |
| `repair_tool` | gadgets | repair |
| `welding_torch` | gadgets | weld |
| `fire_extinguisher` | gadgets | extinguish |
| `bubble_generator` | gadgets | bubbles |
| `confetti_launcher` | gadgets | confetti |
| `camera` | gadgets | camera |
| `grappling_launcher` | gadgets | grapple |
| `teleport_wand` | gadgets | teleport |
| `musical_wand` | gadgets | music |
| `flare_gun` | gadgets | firearm |
| `soda_can` | drinks | drink |
| `cola_can` | drinks | drink |
| `orange_soda_can` | drinks | drink |
| `energy_drink` | drinks | drink |
| `water_bottle` | drinks | drink |
| `juice_carton` | drinks | drink |
| `coffee_cup` | drinks | drink |
| `tea_cup` | drinks | drink |
| `potion_bottle` | drinks | drink |
| `spray_can` | drinks | spray |

## Safety and integration

- Per-instance state: shared mesh recipes do not share fuse timers, drink servings, cast lines or cooldowns.
- Throwing keeps the shared hand-physics path: the mean of the three fastest recent hand samples, centre-of-mass angular contribution, launch multiplier and angular clamp. Native behaviors receive that final velocity afterward; discs and boomerangs add flight/spin without replacing launch velocity.
- Slow deliberate drops stay put and do not start disc/boomerang flight. A deliberate grenade drop starts its fuse; forced cleanup release does not. Generic thrown-tool recovery waits while a native flight, fuse or fishing line is active.
- Explosions and status effects are bounded by radius and solid-wall line of sight; no player health/destruction system is invented. Flash effects are local particles, not a full-screen VR flash.
- Freeze/stun temporarily suspend creature movement and loose-prop physics. Existing hit rules can replace creature hit responses; existing use rules can replace held-item trigger actions.
- Grappling is a short ground-level pull through the walking capsule, not a rope-swing simulator. Movement input cancels it; walls block it. Teleport uses a clear, nearby ground destination.
- Screenshots are of the game view, written to Unity's persistent data directory in five rotating slots.
- The Unity-exported catalog includes held_action/held_effect metadata. The AI prompt groups items by native action and retains every exact ID; named variants identify their effects. Held items use their default life-size scale. Non-held asset dimensions remain available for environment layout.
- Export with ImagineVR/Export Catalog after changing recipes. Deploy backend/catalog and rebuild/install the client together; an older APK does not contain these 100 models or behaviors.

## Verification

Run backend tests from openrouter-api, Unity PlayMode tests (including HeldItemTests), and a Windows player build. On headset verify grabbing, free-hand throwing, trigger debounce, rod line/reeling, boomerang return, disc flight, portable lights, visual scale and sustained automatic-fire performance. Headless tests do not verify XR visuals or comfort.
