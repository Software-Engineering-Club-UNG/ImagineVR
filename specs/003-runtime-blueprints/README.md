# Runtime blueprints implementation and verification

Implemented on `feat/runtime-object-blueprints`, based on main `a615284`. Feature details and examples: [runtime blueprint guide](../../RUNTIME_BLUEPRINTS.md).

## Verification

| Check | Result |
|---|---|
| Backend pytest | 128 passed |
| Backend dependency consistency | `pip check` passed |
| Shared contract parity | Covered by backend test against Unity Resources copy |
| Unity import and native C# compilation | Passed with pinned Unity 6000.3.26f1 |
| Native PlayMode | 16 passed, zero failures (headless editor tests) |
| Android build | Not run: installed editor lacks Android build support |
| Quest interaction, rendering and 72 FPS | Not run: no on-device acceptance evidence |

Backend tests include malformed recipes, cycles, budgets, named component references, example recipes, capability guards, legacy compatibility, coordinate conversion, snapshot reconciliation and generated-asset retry behavior. Native tests cover construction, grabbing/entity identity, staged edits, named-part actions, rule-driven blade toggling, friendly-state projectile cancellation, local damage permission, resets and generic examples.

Run backend checks from `openrouter-api`:

```powershell
./.venv/Scripts/python.exe -m pytest -q
./.venv/Scripts/python.exe -m pip check
```

Run Unity PlayMode tests with editor 6000.3.26f1, project `client`, `-runTests -testPlatform PlayMode`, and inspect the XML result. Local outputs are ignored under `.cache`. Headless PlayMode success is not proof of visual correctness, Android compatibility, headset ergonomics or frame rate.

## Remaining release gate

Task T011 remains open. Install Android build support for the pinned editor, build the Quest client, and test these scenarios on-device before publishing:

1. Give/use a lightsaber, grip alignment, blade/contact visibility and safe removal.
2. Dragon and novel creature silhouettes, articulation, aerial/ground movement and obstacle handling.
3. Explicit hostile behavior, then friendly replacement; confirm no stale attacks.
4. Turret targeting, door proximity animation, instance-only edits and reusable second copies.
5. Repeated creation/removal, large recipes, canceled builds, projectiles and full reset; check resource cleanup.
6. Local damage opt-in, health depletion/reset and no AI-enabled player damage/camera motion.
7. Profile a representative populated scene for the 72 FPS target; tune budgets if necessary.

This feature is submitted on a review branch, not deployed. Existing VM infrastructure and provider secrets were not changed. Merging into main triggers the existing backend deployment workflow; do not merge before the remaining release gate is satisfied.
