# Runtime object blueprints

## User stories

- US1 (P1): Ask for an unfamiliar low-poly object, receive a validated procedural object with named parts, reuse it, and edit selected parts without affecting other copies.
- US2 (P1): Attach generic movement, part animation, sensing, state transitions, contact damage and projectile emitters to any object, not a dragon-specific script.
- US3 (P1): Keep the world synchronized across voice turns and resets; preserve legacy clients and safe fallback. Player damage is locally opt-in.

## Acceptance

Lightsaber: grip handle, toggle emissive blade. Dragon: articulated wings, aerial wander/chase, mouth projectile. Turret: rotating head and targeted projectiles. Door: proximity hinge animation. Novel six-legged creature: composed silhouette and limb animation. Friendly replacement cancels hostile actions. Second copy reuses a blueprint. Reset cancels in-flight builds and projectiles.

No arbitrary code, external mesh provider, persistence, multiplayer, full pathfinding or slicing. Low-poly approximations are acceptable; unsupported functions must be reported honestly. Tests, pinned Unity build, Android and Quest acceptance required before release.
