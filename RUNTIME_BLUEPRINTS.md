# Runtime object blueprints

The backend can describe a new low-poly object as a validated recipe. Unity builds its geometry and executes reusable movement, animation and event/action capabilities. No generated C#, external mesh service, or new provider key is involved. Existing baked assets remain the first choice when they match the request.

This is a compositional approximation system, not a promise of arbitrary simulation: a recognizable dragon can flap, fly, chase and emit projectiles; it cannot acquire a brand-new capability that Unity has never implemented.

## Capabilities

| Component | Available options | Example |
|---|---|---|
| Geometry | Cube, sphere, cylinder, cone, capsule, pyramid, torus, disc, wedge, plank, tube, two-sided surface, baked asset | A handle and emissive blade; wings and a curved tail |
| Hierarchy | Named parts, parent pivots, local transforms | Wings rotate around shoulder joints |
| Interaction | Grabbable, configured grip pose, mass, physics, gravity | Hold a tool by its handle |
| Movement | Still, wander, follow, chase, flee, orbit, path; ground or airborne | Wander peacefully, then chase when requested |
| Animation | Oscillation, rotation/spin, keyframe offsets; named tracks | Flapping wings, swinging legs, opening a hinge |
| Health | Object health and durability | Reuse existing damage/death/break rules |
| Contacts | Named part sensors, target selection, damage, cooldown | Blade contact, claw contact |
| Projectiles | Named socket, visual asset, target, velocity, gravity, lifetime, damage, cooldown, impact particles | Fireballs from a mouth socket |
| States | Named movement and enabled animation/contact/emitter sets | Friendly and hostile states |

Wrap a baked model in an `asset` part when it needs these new capabilities. Blueprint-only actions do not attach themselves to ordinary baked instances.

## New event/action extensions

These extend, rather than replace, the existing runtime rule system.

| Event | Meaning |
|---|---|
| `part_contact` | Contact with the named part in `filters.part` |
| `state_enter` / `state_exit` | Named actor state transition; filter with `filters.name` |
| `projectile_impact` | An emitted projectile impacts something |

| Action | Configuration |
|---|---|
| `motion_config` | `config`: validated movement object |
| `animation_play` | `name`, optional `enabled` |
| `part_transform` | `part`, optional position/rotation/scale/visibility |
| `emit_projectile` | Emitter `name`; requires an enabled emitter |
| `actor_state` | State `name` |

Rules can also test `actor_state` and require `line_of_sight`. Entering a friendly state disables omitted attacks, cancels queued offensive actions for that actor, and invalidates its in-flight projectiles. Existing particles, sound, tint, removal, spawn and other rules remain reusable.

## Definition versus instance

| Operation | Effect |
|---|---|
| `blueprint` | Register a complete recipe before referencing it; redefine to change future copies |
| `spawn` / `give` | Instantiate a registered blueprint ID |
| `blueprint_edit` | Replace selected instances with a complete staged recipe; preserve entity identity, pose and current health |
| `blueprint_remove` | Remove the reusable definition, not existing instances |
| `part_transform` | Edit one instance's named part; report its override in the next snapshot |
| Ordinary scene clear | Cancel pending construction/projectiles; retain reusable definitions |
| Full session reset | Also remove definitions and reset local health/damage permission |

Recipes are session-only, not saved assets. A definition update does not silently alter already-spawned copies. Construction shows a labeled placeholder. Failed validation/construction reports a failure; failed edits keep the old instance. Build scheduling cooperatively targets roughly 2 ms per frame, not a hard real-time guarantee.

## Try it

Run the updated backend and Unity client together. The client advertises `runtime-rules-v1` and `runtime-blueprints-v1`; legacy clients retain the old fallback. Existing OpenRouter and local Whisper configuration is unchanged.

Suggested voice requests:

- “Give me a low-poly lightsaber with a blue blade. Toggle the blade when I use it.”
- “Spawn a friendly dragon with flapping wings that flies around.”
- “Make that dragon chase me and shoot fireballs.” Then: “Make it friendly again.”
- “Spawn a six-legged creature that wanders around.”
- “Make a door that opens when I approach it.”

Deterministic protocol examples are in [shared/blueprint-examples](shared/blueprint-examples). Define `dragon.ndjson` first before applying its hostile/friendly rule replacement examples. These demonstrate generic components, not special-case generators. Model output quality still needs live voice/Quest evaluation.

Player damage is disabled by default and cannot be enabled by AI commands. Use the desktop local toggle or both grips + Y/Fold on the headset. Local health starts at 100; at zero, further damage stops until a local reset. There is no forced camera movement or respawn teleport.

## Limits and release status

| Resource | Limit |
|---|---|
| Registered recipes / parts per recipe / hierarchy depth | 64 / 64 / 8 |
| Actual mesh triangles per object | 8,000 |
| Collider/contact-region budget / animation tracks / sockets | 16 / 16 / 16 |
| Emitters / contacts / named states | 8 each |
| Pending builds / active emitted projectiles | 16 / 32 |
| Recipe / registry / instance-override JSON | 48 KB / 256 KB / 128 KB |

Existing interaction, motion, object and physics budgets also apply. Generated projectile visuals use at most eight parts and a 16-template cache; larger recipes or a full cache use a reported sphere approximation. Movement uses basic obstacle probes, not navigation meshes or full pathfinding. Blueprint scattering, persistence, multiplayer, arbitrary executable logic, cutting and realistic rigging are not implemented.

See [verification and acceptance](specs/003-runtime-blueprints/README.md). This feature is submitted for review, not deployed. Android build and on-device Quest visual/performance acceptance are required before release.
