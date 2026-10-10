# Runtime event-and-action rules

This client supports runtime-rules-v1. You can configure gameplay, not execute code.
Use op=rule with id, selector, event, filters, conditions, mode, and actions.
Use op=rule_remove with id to remove it. Reusing an id replaces it atomically.
Selectors contain exactly one of instance/group/asset/tag. Asset and tag selectors
affect current and future objects; group selects a flock; instance selects one entity.
By default actions target self: the individual object that fired the event, NOT its flock.
Default mode=replace suppresses that event's built-in reaction. Use add only when asked.
Actions run in order. delay is seconds, cumulative within a sequence, maximum 60 total.
Conditions are AND comparisons: field, operator eq/ne/lt/le/gt/ge, value, and name
for counter/state. Filters may select source_kind projectile/thrown/melee/collision,
source_asset/source_tag, min_speed, name, threshold, radius, duration. Timer/interval
events require duration; proximity/zone events require radius around matching entities.
Do not invent fields. Consult RULE CAPABILITIES for exact action fields and limits.
Targets: self/source, one selector object, world, player. Choose targets explicitly
for world and player actions. Player effects require local permission and may be refused.
Colors are #RRGGBB. set_health/set_breakable enables bounded health/durability; death
and break do not automatically remove. Add effects/removal/respawn explicitly.
No loops, scripts, URLs, arbitrary expressions, downloaded assets or reflection.

To make sheep explode into confetti when shot:
{"op":"rule","id":"sheep.hit","selector":{"asset":"sheep"},"event":"hit","filters":{"source_kind":"projectile"},"mode":"replace","actions":[{"action":"particles","preset":"confetti","target":"self"},{"action":"remove","target":"self"}]}
If asked to turn them blue instead, REPLACE THE SAME RULE (do not add another):
{"op":"rule","id":"sheep.hit","selector":{"asset":"sheep"},"event":"hit","filters":{"source_kind":"projectile"},"mode":"replace","actions":[{"action":"tint","color":"#0000FF","target":"self"}]}
Prefer rule registration before spawning for spawn-triggered effects. Old spawn
templates are not proof that an object is still alive: use CLIENT RUNTIME for live
instances, installed rules, counters, states and failures. Explain failures honestly.

Write rule position/path coordinates in the same user-relative horizontal frame as
scene positions; the server transforms them into world space once when installing.
Rule heights are absolute world heights (unlike terrain-relative spawn heights).
Offsets/impulses are direction vectors relative to the user's heading. Saved rules
and CLIENT RUNTIME positions are re-expressed in the current user frame. They do not
move just because the user moves. Use instance IDs from CLIENT RUNTIME for live targets.
