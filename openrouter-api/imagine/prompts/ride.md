# Driving, flying, roads

What they ride drives as what it is. Name the controls in your `say`:
- `car`, `bike`, `tank`, `mount` (a horse, anything on legs), `boat`: right trigger goes, left trigger brakes and then reverses, left stick steers. A car with a steering wheel can also be steered by gripping the wheel with either hand and turning it.
- `plane`, `glider`: left stick up and down is the throttle; the right stick pitches (pull back to climb) and rolls. It needs speed to leave the ground and stalls when slow.
- `heli`, `hover`, `space`, and anything alive with wings: the left stick flies it level, the right stick turns it, and up or down on the right stick climbs or descends.
- `rail`: it follows its `path` (a train, a rollercoaster: give the path height). `simple`: a sofa, a snail: it goes where the stick points.
- Something that `fires` shoots with the B button from a car or a tank, and with the right trigger from anything that flies.
- Clicking the right stick gets out. `speed` scales the top speed; `grip` 0.3 drifts, 1 holds the road; `"altitude":150` starts a flyer already in the air. `{"op":"modify","id":"ride","speed":2}` tunes it while they drive.
- Driving fast or flying needs room and a reason: `"size":"vast"` terrain (`wide` for a town), somewhere worth crossing, and things in the way (cones, crates, traffic, ramps, rings to fly through, rivals on a `path`). A plane starts at the end of a `runway` road 300 m or longer.

Vehicles you make
- Give them `role` parts: `wheel`s (cylinders or torus lying on their sides, `"rotation":[0,0,90]`, `"mirror":"x"`), a `steer`ing wheel, `lamp`s at the front, a `seat` where the driver sits (or `"seat":[x,y,z]`, their eyes, on the make), a `spinner` for each propeller or rotor, `thruster`s for exhausts, `gun`s. They sit INSIDE it: say in `refine` what they see from the seat (dashboard, wheel, bonnet, windscreen frame, canopy).
- `"drive"` on the make fixes how it handles.

Roads
- `{"op":"road","id":"main","kind":"street","shape":"grid","blocks":[3,3],"block":[60,45],"sidewalks":true,"lamps":true,"traffic":{"assets":["car","taxi","bus"],"count":10},"walkers":{"count":8},"buildings":{"assets":["office_block","shop","apartment_block","glass_tower"],"density":0.9}}`
- `kind`: {{ROAD_KINDS}} (it sets width, surface and markings). `shape`: {{ROAD_SHAPES}}, placed by `at` and `rot_y` and sized by `size` [w,d], `radius` or `length`; or `points:[[x,z],...]` for a road of your own drawing (`"loop":true` closes it). Also `width`, `lanes`, `barriers`, `height` (raised: a bridge, a coaster).
- `traffic` drives its lanes and stops for what is ahead; `walkers` use the sidewalks; `buildings` line it and face it (give three to five kinds).
- `"checkpoints":6` on a closed road makes it a circuit: it keeps the counters `lap`, `lap_time` and `best_lap`, and a `lap` event fires each time round.
- `ride` after the `road` and they start on it, facing along it.
- A city is a `grid` road with buildings and traffic on `wide` or `vast` `flat` ground of `asphalt` or `concrete`; then landmarks, a park (`regions` of `grass` with trees), and street life placed by hand near the user. A race is a `loop` or `oval` `track` with `barriers`, `checkpoints`, a grandstand, rivals on its `path`, and a rule on `lap`.
