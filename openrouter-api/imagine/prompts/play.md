# Making it a game: counting, places that trigger, stages, health

Counters
- `{"op":"var","name":"strokes","value":0,"label":"Strokes"}` a number the headset keeps. `label` shows it in front of them. `rate` changes it every second (`"rate":-1` counts down), `min`, `max`, `"as":"time"` or `"bar"`. `{"op":"timer","name":"clock","seconds":60}` is a countdown.
- `{"op":"hud","objective":"Knock down all ten"}` the line that says what to do. `"board":{"at":[0,3,19],"title":"LANE 7","show":["frame","score"]}` stands a scoreboard in the world.

Places that trigger
- `{"op":"zone","id":"cup","at":[0,0.1,60],"radius":0.3}` an unseen volume (`radius`, or `size` [w,h,d]; `at` is its centre). `"show":"ring"`, `"beam"`, `"flag"` or `"glow"` marks it. `"on":"<id>"` fixes it to a thing. `"effect":{"damage":10}` works each second on what is inside: `damage`, `heal`, `slow`, `boost`, `gravity`, `push` [x,y,z], `kill`.

Stages: rounds, waves, chapters
- `{"op":"stage","id":"wave1","goal":"Hold the gate","first":true,"enter":[...actions...],"next":"wave2","timeout":90,"fail":"lost"}` Entering a stage shows its `goal` and runs `enter`. A rule with `"stage":"wave1"` only works during it.

More rules
- `event` also: `enter` / `exit` (`"zone"`, and `target`: who, the `player` unless said), `tip` (it fell over), `rest` (it stopped moving), `strike` (hit with a swung club, bat or racket), `collide` (`"with"`: the other thing), `drop`, `hurt`, `lap` (a circuit road completed), `stage` (`target`: the stage entered), `becomes` (`"test"`: fires the moment it turns true).
- Any rule takes `"if"`, `"once":true`, `"cooldown"` seconds. Any ACTION takes `"if"` too: that is how one rule does different things.
- Tests and amounts are small sums over counters and the world: `throws >= 2 or standing(pins) == 0`, `dist(golf_ball, cup) < 0.4`, `alive(zombie) == 0 and time > 5`. Functions: `count(x)`, `alive(x)`, `standing(x)`, `tipped(x)`, `moving(x)`, `dist(a,b)`, `inside(zone)` or `inside(zone,x)`, `holding(x)`, `riding(x)`, `speed(x)`, `height(x)`, `health(x)`, `rand(a,b)`, `min max abs floor round`. `x` is an id or an asset; `player` is the user; `time` is seconds in this stage; an id covers its pieces (`pins` counts `pins_1`, `pins_2`).
- `do` also: `set` (`var`, `to`), `add` (`var`, `by`), `objective` (`text`), `reset` (`target`: stands a group back where it began, remaking what was lost), `teleport` (`who`: `player` or an id; `to`: a place or an id), `give` / `take` (`asset`), `stage` (`go`: an id or `next`), `damage` / `heal` (`amount`, `who`, `radius`), `enable` / `disable` (`rule`), `modify` (`target`, and anything `modify` takes), `music` (`mood`), `fx` (`kind`, `seconds`), `fade`, `slowmo`, `shake`, `checkpoint`, `win` / `lose` (`text`). Text may show a counter: `"Lap {lap}: {lap_time}s"`.

Being hurt
- `{"op":"player","health":100}` turns it on: a bar, and when it runs out they wake at the last `checkpoint` (or a `die` rule on `player` decides). Enemies need `behavior` `attack` or `shoot`, a `damage`, and `health` if one hit should not finish them. `"team":"player"` fights beside the user.
- Waves are stages: each `enter` spawns (`{"act":"spawn","asset":"zombie","count":6,"behavior":"attack","health":3,"damage":10,"near":"player","radius":22}`), and a `becomes` rule on `alive(zombie) == 0 and time > 3` goes `next`.

The method, for any game
1. Lay out the surface and give it its `surface` (a lane is `wood` with `"finish":"gloss"`, a rink `ice`, a putting green a `regions` patch of `green`).
2. Loose pieces get honest `mass` and `bounce`; a formation is rows of `repeat` sharing an id root (`pins_1` ... `pins_4`).
3. One counter for each thing worth knowing, with a `label`; an `objective`.
4. A `zone` wherever arriving matters: a cup, a hoop, a goal mouth, a finish line, the pit behind the pins.
5. Rules that count, then ONE `becomes` rule that ends the turn or round: it scores, `reset`s the pieces and moves the counter on.
6. `win` when the last round is done.

{"op":"when","id":"roll","event":"enter","zone":"pit","target":"bowling_ball","do":[{"act":"remove"},{"act":"add","var":"throws","delay":2.5},{"act":"give","asset":"bowling_ball","delay":3}]}
{"op":"when","id":"frame_over","event":"becomes","test":"throws >= 2 or (throws >= 1 and standing(pins) == 0)","do":[{"act":"banner","text":"STRIKE!","if":"throws == 1"},{"act":"add","var":"score","by":"tipped(pins)"},{"act":"reset","target":"pins"},{"act":"set","var":"throws","to":0},{"act":"add","var":"frame"}]}
{"op":"when","id":"holed","event":"enter","zone":"cup","target":"golf_ball","if":"speed(golf_ball) < 4","do":[{"act":"sound","name":"coin"},{"act":"remove"},{"act":"win","text":"In the hole in {strokes}!"}]}
