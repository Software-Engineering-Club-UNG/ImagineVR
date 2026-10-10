# Runtime rules — integration and acceptance

The data-only rule system is integrated with the headset testing fixes in PR #5.
Verification: 106 combined backend tests and seven native Unity 6000.3.26f1 PlayMode
tests pass. PR #5 records Android, deployment, and live Quest acceptance results.

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
grips and tap X to toggle; the terminal confirms the state. That entire X press is
excluded from hold-to-reset.
Reset disables the permission. Teleport/resize/mount actions fade and move the rig,
not the independently tracked camera. Headset comfort must be verified on device.

## Acceptance checks

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

The broad toolkit is a deliberate user-requested expansion of the original demo-first
scope. Native tests cover individual sheep hits/replacement, future spawns, invalid
updates, full reset and delayed-action cancellation, queued-turn cancellation, and
registry cleanup. The live acceptance script also exercises the deployed model:
`python scripts/smoke_rules.py --settings /path/to/imagine.json --audio sword.wav`.
Use a 16 kHz mono PCM WAV saying “give me a sword”; the audio argument is optional.
This script uses an isolated simulated client session; physical interaction and
microphone capture still require a Quest user.

Scene clears preserve the session protocol generation; only network session resets
change it. A new session ID starts at generation zero, and old-session cleanup cannot
change the new session. Rule position/path values are transformed into world space at
validation and re-expressed, with runtime snapshots, in the user's current frame for
each model request. Absolute rule heights differ from terrain-relative spawn heights.
