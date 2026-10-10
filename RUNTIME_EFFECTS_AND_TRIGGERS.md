# Runtime Effects, Events, and Actions

ImagineVR's runtime-configurable rule system lets the AI connect reusable Unity triggers and actions without generating arbitrary code. The backend validates the rules, and Unity executes them.

## Implementation status

As of October 10, 2026, the implementation contains **7 particle presets, 31 event/trigger types, and 43 action types**.

The runtime-rule implementation is being submitted on a feature branch for review and integration. It has **not been deployed or tested on a headset yet**. Python tests and supplementary C# compilation checks passed; native Unity PlayMode, Android, and Quest testing remain outstanding.

Most actions expose existing Unity capabilities to runtime rules. The seven particle presets are **not seven polished, visually distinct effects**: several currently share a generic burst implementation.

## Particle effects

All presets support optional color, particle count, size, and duration. Rule-generated particles are limited to 200 per burst and a shared budget of 1,000; the runtime clamps duration to 0.1–10 seconds.

| Preset | Current visual effect | Limitations |
| --- | --- | --- |
| `confetti` | Multicolored particles spray upward and fall under gravity. An optional color makes the burst a single color. | Reuses and extends the existing confetti effect. |
| `sparkles` | Bright, additive particle burst. | Currently the same implementation as `sparks`. |
| `sparks` | Bright, additive particle burst. | Not yet a distinct directional spark effect. |
| `smoke` | Larger, slower particles. | Basic approximation; not realistic volumetric smoke. |
| `dust` | Generic particle burst, optionally tinted brown or gray. | Shares its underlying implementation with `explosion`. |
| `splash` | Particle burst with gravity, causing particles to fall. | Not a water simulation. |
| `explosion` | Generic visual particle burst. | No automatic damage, blast force, or fireball. Currently shares the dust implementation. |

An explosion with sound, damage, and knockback must combine separate actions. Selecting `explosion` alone is purely visual.

## Events and triggers

### Object lifecycle

| Trigger | When it fires | Notes |
| --- | --- | --- |
| `spawn` | An object is created and registered with the rule system. | Asset/tag rules can apply to future matching spawns. |
| `place` | A released, non-thrown physics object settles for roughly half a second. | Not a general event for every AI-created object. |
| `respawn` | An object is recreated through a respawn path. | The recreated object has a new instance identity. |
| `remove` | An object is removed through the runtime system. | Can trigger effects as part of removal. |

### Combat and destruction

| Trigger | When it fires | Notes |
| --- | --- | --- |
| `hit` | An object receives an impact through projectile, thrown-object, melee, or collision hooks. | Can filter by impact source and minimum speed. |
| `damage` | The damage system applies damage to an object with health. | Separate from the initial impact event. |
| `health_below` | Health crosses below a configured threshold. | Threshold crossing, rather than firing continuously while health is low. |
| `death` | Health drops from above zero to zero. | Does not automatically remove the object. |
| `break` | Durability drops from above zero to zero. | Does not automatically remove or fracture the object. |

Death and break rules decide what happens next: particles, removal, color changes, respawning, or another supported action.

### Physical interaction

| Trigger | When it fires | Notes |
| --- | --- | --- |
| `collision_enter` | Physical contact begins. | Requires appropriate collision components. |
| `collision_exit` | Physical contact ends. | Uses Unity collision callbacks. |
| `touch_enter` | Contact begins. | Currently the same callback as `collision_enter`, not a separate hand-touch detector. |
| `touch_exit` | Contact ends. | Currently the same callback as `collision_exit`. |
| `grab` | The player grabs an object. | Uses the existing grabbing system. |
| `release` | The player releases an object. | Also occurs when the release is a throw. |
| `throw` | A release is classified as a throw. | Distinguishes throwing from simply letting go. |
| `use_start` | The player starts using a held object. | Uses existing object-use input hooks. |
| `use_end` | The player stops using a held object. | Paired with `use_start`. |
| `land` | A collision meets a vertical-impact check. | Basic heuristic, not a robust grounded-state detector. |

### Location and movement

| Trigger | When it fires | Notes |
| --- | --- | --- |
| `zone_enter` | The player or a filtered object enters a radius around the target. | Currently a spherical distance check. |
| `zone_exit` | The player or filtered object leaves that radius. | Not a separately authored collider zone. |
| `proximity_enter` | Something comes within the configured distance. | Shares the spherical distance mechanism with zone events. |
| `proximity_exit` | Something moves outside the configured distance. | Can track the player or matching entities. |
| `path_complete` | An object finishes its movement path. | Used for movement completion rules. |
| `mount` | The player mounts a rideable object. | Broadcast to matching live rule targets. |
| `dismount` | The player dismounts. | Broadcast to matching live rule targets. |

### Timing and game state

| Trigger | When it fires | Notes |
| --- | --- | --- |
| `timer` | A one-shot timer expires. | Named timers are also supported. |
| `interval` | A repeating period elapses. | Minimum interval: 0.25 seconds. |
| `counter_changed` | A named numeric counter changes. | Useful for score or collected-item counts. |
| `counter_reached` | A counter crosses its configured threshold. | Useful for milestones. |
| `state_changed` | A named state value changes. | Useful for switches or progression flags. |
| `goal_reached` | A named goal-completion event occurs. | Can connect goals to effects or scene changes. |

## Actions

### Appearance and feedback

| Action | Result | Parameters |
| --- | --- | --- |
| `particles` | Emit one of the seven particle presets. | Required: `preset`. Optional: `color`, `count`, `duration`, `size`. |
| `sound` | Play an existing named sound. | Required: `name`. Optional: `volume`, `pitch`. |
| `message` | Display a message through the existing output system. | Required: `text`. |
| `haptic` | Vibrate the controllers. | Optional: `strength`, `duration`. |
| `tint` | Change an object's color. | Required: `color`. |
| `scale` | Resize an object. | Required: `value`. |
| `visibility` | Show or hide an object without removing it. | Required: `enabled`. |

### Creation and removal

| Action | Result | Parameters / limitations |
| --- | --- | --- |
| `spawn` | Create objects from the existing asset catalog. | Required: `asset`. Optional: `count`, `offset`, `scale`, `color`, `behavior`, `tags`. Does not generate new models. |
| `remove` | Remove the targeted object. | No action-specific parameters. |
| `respawn` | Recreate the basic asset at its original position and scale. | Not a complete restoration of every original property, such as tags, behavior, or rotation. |

### Movement and behavior

| Action | Result | Parameters / limitations |
| --- | --- | --- |
| `move_to` | Move an object to a specified position. | Required: `position`. Optional: `duration`. |
| `rotate` | Set an object's rotation. | Required: `rotation`. |
| `follow_path` | Move through a sequence of points. | Required: `path`. Optional: `speed`, `loop`. |
| `set_behavior` | Configure creature-style behavior. | Required: `behavior`: `wander`, `chase`, `flee`, or `still`. |
| `stop_motion` | Stop configured movement, creature behavior, and linear physics velocity. | No action-specific parameters. |
| `impulse` | Apply an instantaneous physical push. | Required: `vector`. Requires a dynamic physics body. |
| `set_velocity` | Set physical movement velocity. | Required: `vector`. Requires a dynamic physics body. |
| `set_gravity` | Change an object's gravity multiplier, or existing world-prop gravity. | Required: `value`. |
| `set_physics` | Enable or disable dynamic physics movement. | Required: `enabled`. |
| `set_collision` | Enable or disable collision. | Required: `enabled`. |

### Health and breakability

| Action | Result | Parameters |
| --- | --- | --- |
| `set_health` | Set health directly. | Required: `value`. |
| `damage` | Apply damage, potentially triggering health/death events. | Required: `value`. |
| `heal` | Restore health. | Required: `value`. |
| `kill` | Reduce health to zero through the damage system. | No action-specific parameters. |
| `set_breakable` | Give an object a durability value. | Required: `value`. |
| `break` | Exhaust durability and trigger its break event. | No action-specific parameters. |

There is no automatic fractured-mesh destruction effect. Breaking into pieces would require spawning suitable existing assets or adding a fragmentation capability.

### Counters, state, rules, and timers

| Action | Result | Parameters |
| --- | --- | --- |
| `counter_set` | Set a named numeric counter. | Required: `name`, `value`. |
| `counter_add` | Increase or decrease a named counter. | Required: `name`, `value`. |
| `state_set` | Set a named state value. | Required: `name`, `value`. |
| `goal_complete` | Emit a named goal-completion event. | Required: `name`. |
| `rule_enable` | Activate an installed rule. | Required: `id`. |
| `rule_disable` | Disable a rule and cancel its pending actions. | Required: `id`. |
| `timer_start` | Start a named timer, optionally repeating. | Required: `name`, `duration`. Optional: `repeat`. |
| `timer_cancel` | Cancel a named timer. | Required: `name`. |

### Environment

These reuse the project's existing environment systems; they do not create new environment assets.

| Action | Result | Parameters |
| --- | --- | --- |
| `sky` | Select an existing sky preset. | Required: `preset`. |
| `light` | Change lighting intensity and optional color. | Required: `intensity`. Optional: `color`. |
| `fog` | Change fog density and optional color. | Required: `density`. Optional: `color`. |
| `weather` | Activate existing weather with configurable intensity. | Required: `type`. Optional: `intensity`. |
| `ambience` | Play an existing ambient audio loop. | Required: `loop`. Optional: `volume`. |

### Player effects

Player-changing actions are **disabled by default** and require local opt-in for the session.

| Action | Result | Parameters / safeguards |
| --- | --- | --- |
| `teleport_player` | Move the player. | Required: `position`. Includes a fade and destination safety checks. |
| `resize_player` | Change the player's scale. | Required: `value`. |
| `mount` | Put the player onto an existing rideable asset. | Required: `asset`. Optional: `speed`. |
| `dismount` | Get the player off the rideable object. | No action-specific parameters. |

## Rule configuration

| Capability | Supported behavior |
| --- | --- |
| Rule selectors | Target one instance, a group, an asset type, or a tag. |
| Future objects | Asset/tag rules also cover future matching spawns. |
| Action targets | Act on the triggering object (`self`), event source, supported world/player actions, or an explicit selector. |
| Filters | Restrict events by source kind, source asset/tag, speed, radius, threshold, name, or timing, as applicable. |
| Conditions | Combine AND checks on health, durability, speed, source information, counters, state, and chance. |
| Replacement | Update an existing rule by ID; `replace` mode can suppress the matching built-in reaction. |
| Additional behavior | `add` mode attaches actions without replacing the built-in reaction. |
| Cooldown | Limit how frequently a rule fires. |
| One-shot rules | Fire once rather than repeatedly. |
| Delayed sequences | Delay actions to form ordered sequences; cumulative delay is bounded to 60 seconds. |
| Session lifetime | Rules are session-only; they are not persistent arbitrary code. |

### Example: projectile-hit sheep

| Part | Initial rule | Replacement rule |
| --- | --- | --- |
| Target | All sheep | All sheep |
| Trigger | Hit by a projectile | Hit by a projectile |
| Actions | Emit confetti, then remove the individual sheep that was hit | Tint the individual sheep blue |
| Update | Install the rule | Replace the same rule ID so removal is no longer part of it |

The sheep-specific scenario is assembled from generic capabilities; it does not require a new sheep-specific script.

## Runtime limits

| Resource | Limit |
| --- | ---: |
| Installed rules | 64 |
| Actions per rule | 8 |
| Conditions per rule | 8 |
| Timers | 32 |
| Spatial zones | 32 |
| Named counters | 64 |
| Named states | 64 |
| Interactive entities | 64 |
| Queued events/actions | 256 |
| Actions per frame | 128 |
| Event chain depth | 4 |
| Cumulative action delay | 60 seconds |
| Minimum repeating interval | 0.25 seconds |
| Particles per burst | 200 |
| Rule-generated particle budget | 1,000 |
| Entities in a runtime snapshot | 2,048 |

Existing object, creature, and physics budgets also apply. An accepted rule does not bypass these resource limits.

## Source references

- [Shared capability contract](shared/runtime-rules.json)
- [Unity runtime rule engine](client/Assets/ImagineVR/Scripts/World/RuleEngine.cs)
- [Unity interaction and collision hooks](client/Assets/ImagineVR/Scripts/World/RuleBody.cs)
- [Backend rule validation](openrouter-api/imagine/rules.py)
- [Example rule scripts](shared/rule-examples/)
- [Feature acceptance and verification notes](specs/002-runtime-rules/README.md)
