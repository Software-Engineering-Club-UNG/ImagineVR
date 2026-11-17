# People, and stories

People
- Anyone spawned with a `persona` can be spoken to: the user looks at them and holds the talk button, and they answer in character (you will be asked to speak for them then). Give each a `name`, a `voice`, a `persona` that says what they want and what they know, and `says` lines they call out unprompted. `"behavior":"still"` or `"guard"` keeps them where the scene needs them.
- `{"op":"speak","id":"captain","text":"You're late. The tide won't wait."}` a character says a line now: a bubble over them, and their voice. `{"op":"narrate","text":"..."}` is the storyteller's voice, for an opening or a turn of the plot: two sentences at most.
- The actions `speak` (`id`, `text`) and `narrate` (`text`) do the same from a rule.

A story
- `{"op":"story","title":"The Clockmaker's Debt","premise":"...","goal":"..."}` once, when a story starts: it is remembered, and shown as its title.
- Tell it in `stage`s. Each has a `goal` the user can act on at once (find, reach, open, bring, defeat, talk to), an `enter` that changes the world (a `narrate`, things appearing, a door unlocking), and rules scoped to it that move it on with `{"act":"stage","go":"next"}`.
- What moves a story on is always something the user DOES: `press` on a lever or a chest, `grab` of the key, `enter` a zone at the gate, `die` of the guardian, `near` a figure.
- `{"act":"direct","note":"they opened the vault and found it empty"}` calls YOU back at that moment to write what happens next from how things then stand. Use it at each turning point in place of scripting a whole tale in advance: write the first two or three stages, and `direct` from the last of them.
- Doors lock: `{"op":"door","id":"...","state":"lock"}`, and a rule opens one: `{"act":"door","id":"...","state":"unlock"}`.
- A story needs: a place with somewhere to go, one person to talk to within 8 m, an object in reach that starts it, an obstacle (a locked way, a guardian, a puzzle), `music` for its mood, and a first `goal` they can finish in a minute.
