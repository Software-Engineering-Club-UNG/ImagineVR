# Runtime rules — integration and acceptance

The approved design is implemented as data-only rules; **not production activated**.
Backend regression tests and a Roslyn compile against installed Unity 6000.3.22f1
references are available. This does NOT verify Unity 6000.3.26f1 assembly imports,
PlayMode behavior, XR rendering, Android packaging or headset performance.
The pure C# contract validator also accepted all eight example rules and rejected
an executable-code action in a standalone check; this exercises no Unity gameplay.

## Interface

`shared/runtime-rules.json` is the contract. Its embedded copy at
`client/Assets/ImagineVR/Resources/RuntimeRules.json` must match (pytest enforces this).
Health advertises `runtime-rules-v1`. Updated clients send `X-Client-Capabilities`;
legacy clients get the existing scene-only prompt and no rule operations.

`rule` installs/replaces by ID, `rule_remove` removes by ID. Select one instance,
group, asset or tag. Asset/tag rules cover future entities; action `self` is one
entity, not the group. Default mode `replace` suppresses that event's built-in
reaction; `add` keeps it. Updating/removing cancels scheduled actions for that rule.
Cooldown/once are per rule and instance. Conditions are AND comparisons only.
Action delays are cumulative and cannot exceed 60 seconds per sequence.

Register rules before spawning for spawn-triggered effects. Scenery remains GPU
instanced unless its rules need interaction components. Grab/use events promote
objects to bounded physics props. Health/death rules enable health at 100; break
rules enable durability at 100. `death`/`break` do not imply removal. Hit source
kinds are projectile, thrown, melee and collision. Proximity/zone rules are centred
on matching entities; with source_asset/source_tag they watch matching entities,
otherwise they watch the player.

`POST /v1/session/runtime` accepts a bounded JSON snapshot under the existing token
and session headers. Body: generation, sequence, entities, rules, counters, states,
player and failures. Duplicate/out-of-generation snapshots return 409. Reset still
returns 204 and supplies `X-Session-Generation`. No persistence or multiplayer.
Unity synchronizes after earlier streamed commands have applied and before each
voice/text request; failures are visible and do not stop local gameplay.

Player actions are disabled by default. Desktop: local checkbox. Headset: hold both
grips and press the recenter/menu button to toggle; the terminal confirms the state.
Reset disables the permission. Teleport/resize/mount actions fade and move the rig,
not the independently tracked camera. Headset comfort must be verified on device.

## Verification required before merge/deploy

1. Install Unity **6000.3.26f1** with Android support. Do not upgrade project version.
2. Open `client`, run RuntimeRules.Tests in Test Runner (EditMode and PlayMode),
   including real collisions, held-object placement, timers, cancellation and budget
   stress. Check Console for shader/assembly errors.
3. Replay `shared/rule-examples/sheep-confetti.ndjson`, shoot one sheep, verify just
   that sheep disappears. Replay sheep-blue, verify later hits tint surviving/future
   sheep without falling over. Test the other rule examples.
4. Build Android with `scripts/unity.ps1`'s Builder.BuildAndroid method. Test on Quest
   at 72 fps, including player effects disabled/enabled and safe teleport rejection.
5. Deploy the backward-compatible backend via existing GitHub Actions, then test the
   updated client against live `/v1/session/runtime` and voice/text rule requests.
   Keep app tokens outside the repository; do not enable arbitrary code execution.

The broad toolkit is a deliberate user-requested expansion of the original
demo-first scope. Production rollout is held until native acceptance; the healthy
existing deployment is not modified merely to claim this feature is finished.
