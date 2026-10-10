# Feature Specification: Voice World Builder (Core Loop)

**Feature Branch**: `001-voice-world-builder`

**Created**: 2026-10-09

**Status**: Draft

**Input**: User description: "Core loop for ImagineVR (Quest 3S VR experience). The user starts in a calm empty space in front of a floating terminal that looks and behaves like an agentic coding CLI. The user speaks a request by voice; it is transcribed, shown in the terminal as their prompt, interpreted by an AI, and the whole environment is rebuilt in real time through animated transitions while the terminal narrates each step. Follow-up requests modify the current world. The user can ask for a holdable object and pick it up. Objects come from a built-in library plus simple shapes, with a substitute or stand-in when something is missing so every request produces a result. A small creative-tool visual touch nods to the track sponsor. Out of scope: AI-generated 360 image skyboxes, AI-generated 3D meshes or textures, drawing-board-to-3D-object, offline mode, accounts, persistence, multiplayer."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Speak a world into existence (Priority: P1)

The user puts on the headset and finds themselves in a dark, empty room, like a blank
terminal window: nothing but blackness and a softly glowing floating terminal that looks like
an agentic command-line assistant. They hold a button, say what they imagine ("I want to see
a golf course"), and release. Their words appear in the terminal as a prompt. The terminal
shows the assistant working through visible steps, and the darkness comes to life around
them: a sky opens, the ground forms, light floods in, and objects appear, until they are
standing in the place they described.

**Why this priority**: This is the whole product. Without it there is nothing to demo; with
only this, the demo already works.

**Independent Test**: Launch the experience, speak one request for a place, and confirm that
the prompt appears in the terminal and the surroundings change to match within the time
limits in Success Criteria.

**Acceptance Scenarios**:

1. **Given** the user has just entered the experience, **When** they look around, **Then**
   everything is dark and empty except a readable, glowing terminal ahead of them showing a
   short welcome and how to speak a request.
2. **Given** the terminal is idle, **When** the user holds the talk button, **Then** the
   terminal clearly shows it is listening.
3. **Given** the user has finished speaking, **When** they release the button, **Then** their
   transcribed words appear in the terminal as their prompt and the terminal shows it is
   thinking.
4. **Given** a request for a place has been understood, **When** building starts, **Then** the
   terminal lists each step as it happens (for example sky, terrain, objects) and each part of
   the world changes through a smooth animated transition.
5. **Given** building has finished, **When** the user looks around in every direction, **Then**
   the surroundings match the request and the terminal shows it is ready for the next request.

---

### User Story 2 - Reshape the world with follow-ups (Priority: P2)

Standing in the world they asked for, the user speaks again ("make it sunset", "add more
trees", "now put me in a city"). Small requests adjust the current world and keep everything
else in place. A request for a completely different place replaces the world.

**Why this priority**: Iterating is what makes it feel like a conversation with an assistant
and not a one-shot trick. It depends on Story 1.

**Independent Test**: After building one world, speak an adjustment and confirm only the
requested aspect changes; then ask for a different place and confirm the world is replaced.

**Acceptance Scenarios**:

1. **Given** a world exists, **When** the user asks for a change to one aspect (time of day,
   weather, more or fewer of something), **Then** that aspect changes and the rest of the
   world stays as it was.
2. **Given** a world exists, **When** the user asks for a clearly different place, **Then**
   the old world transitions out and the new one transitions in.
3. **Given** several requests have been made, **When** the user looks at the terminal,
   **Then** earlier prompts and steps are still visible as a scrolling history.

---

### User Story 3 - Ask for something to hold (Priority: P3)

The user asks for an object ("make me a golf club"). It appears within reach in front of
them. They grab it, move it, swing it, and let go.

**Why this priority**: Holding something you asked for makes the world feel real and makes
the demo memorable, but the experience is already complete without it.

**Independent Test**: In any world, ask for a holdable object from the library and confirm it
appears within reach, can be picked up, moved, and released.

**Acceptance Scenarios**:

1. **Given** a world exists, **When** the user asks for a holdable object, **Then** it appears
   within arm's reach in front of them and the terminal reports it.
2. **Given** a holdable object is in front of the user, **When** they grab it, **Then** it
   follows their hand naturally until they release it.
3. **Given** the user releases an object, **When** it leaves their hand, **Then** it behaves
   believably (falls, rests on the ground) and can be picked up again.

---

### User Story 4 - The assistant talks back (Priority: P4)

When the assistant responds, the user also hears a short spoken reply and ambient sound that
suits the new world, so the experience works even when they are not looking at the terminal.

**Why this priority**: Adds a lot of presence, but it is an enhancement on top of a working
visual loop.

**Independent Test**: Make any request and confirm a short spoken reply is heard and that the
ambient sound changes with the world.

**Acceptance Scenarios**:

1. **Given** a request has been understood, **When** the assistant responds, **Then** the user
   hears a brief spoken confirmation that matches the terminal text.
2. **Given** a new world has been built, **When** building finishes, **Then** the ambient
   sound suits that world.

---

### Edge Cases

- The user asks for something not in the built-in library: the closest match or a simple
  stand-in shape is used, and the terminal says what was substituted.
- Speech is silent, too short, or unintelligible: the terminal says it did not catch that and
  returns to ready; the world is left unchanged.
- The request is not about the world (a question, small talk): the assistant replies briefly
  in the terminal and the world is left unchanged.
- The connection drops or the assistant takes too long: the terminal says so in plain words,
  the current world stays intact, and the user can try again.
- The user speaks again while a build is still in progress: the new request is accepted and
  handled after the current build, without leaving the world half-built.
- The request asks for far more than the headset can show smoothly (for example "ten thousand
  trees"): the amount is capped so the experience stays comfortable, and the terminal says so.
- The request is inappropriate or offensive: the assistant declines politely and the world is
  left unchanged.
- The user turns away or moves: the terminal remains easy to find and read.

## Requirements *(mandatory)*

### Functional Requirements

**Terminal**

- **FR-001**: The experience MUST start in a dark, empty room, like a blank terminal window,
  with a glowing floating terminal positioned comfortably in front of the user as the only
  thing visible.
- **FR-001a**: The first request MUST bring the dark room to life as a deliberate reveal:
  the change from darkness to a world is the signature moment of the experience.
- **FR-002**: The terminal MUST look and behave like an agentic command-line assistant: a
  prompt line, text that streams in as it is produced, and visibly distinct steps for each
  action the assistant takes.
- **FR-003**: The terminal MUST always show exactly one clear state: ready, listening,
  thinking, building, or problem.
- **FR-004**: The terminal MUST keep a scrolling history of the session's prompts and steps.
- **FR-005**: The terminal MUST remain readable and easy to find wherever the user turns.

**Voice**

- **FR-006**: Users MUST be able to make a request by holding a button, speaking, and
  releasing.
- **FR-007**: The system MUST transcribe the speech and show it in the terminal as the user's
  prompt before acting on it.
- **FR-008**: The system MUST acknowledge the end of speech within 1 second.

**World building**

- **FR-009**: The system MUST interpret a spoken request and change the user's surroundings
  to match, covering at least: sky, lighting and time of day, atmosphere (fog/haze), ground or
  terrain, and placed objects.
- **FR-010**: Every change to the world MUST happen as an animated transition; nothing may
  appear or vanish abruptly.
- **FR-011**: The terminal MUST narrate each building step as it happens, in step with what
  the user sees.
- **FR-012**: Follow-up requests MUST modify the current world unless the user asks for a
  different place, in which case the world is replaced.
- **FR-013**: The world MUST be built from a large built-in library prepared ahead of time
  (objects, materials, skies, weather, terrain types, and themed sets such as city or golf
  course) plus simple shapes. Nothing is created from scratch while the user waits.
- **FR-013a**: The assistant MUST be able to combine and adjust library content freely
  (choose, place, repeat, scale, recolour, light, and time transitions) so that requests
  beyond the rehearsed ones still produce a fitting world.
- **FR-014**: When a requested thing is not in the library, the system MUST use the closest
  match or a simple stand-in and say so in the terminal. Every valid request MUST produce a
  visible result.
- **FR-015**: The system MUST limit how much it places in a world so the experience stays
  smooth and comfortable.
- **FR-016**: The world MUST NOT move the user's viewpoint against their will; changes happen
  around a stationary user.

**Objects**

- **FR-017**: Users MUST be able to ask for a holdable object and have it appear within reach.
- **FR-018**: Users MUST be able to grab, move, and release holdable objects with the
  controllers.

**Problems**

- **FR-019**: When speech is not understood, the request is not about the world, or the
  request is declined, the system MUST say so in the terminal and leave the world unchanged.
- **FR-020**: When the connection fails or a response takes too long, the system MUST say so
  in plain words, keep the current world intact, and let the user try again.
- **FR-021**: A new request made while a build is in progress MUST NOT leave the world in a
  broken or half-built state.

**Sound and style**

- **FR-022**: The assistant SHOULD give a short spoken reply to each request, and the ambient
  sound SHOULD suit the current world.
- **FR-023**: The experience MUST include one small creative-tool visual touch (for example a
  paintbrush icon or tool-palette styling) as a nod to the track sponsor, without using the
  sponsor's actual logos or trademarks.

### Key Entities

- **Request**: one spoken instruction from the user. Has the transcribed text and an outcome
  (world changed, object given, answered without change, not understood, declined, failed).
- **World**: the user's current surroundings. Made up of sky, lighting, atmosphere, terrain,
  and a set of placed objects. Exactly one exists at a time.
- **Build Step**: one visible action the assistant takes while fulfilling a request (for
  example "set the sky", "plant trees"). Shown in the terminal and reflected in the world.
- **Library Item**: something the experience already knows how to show: an object, a
  material, or a simple shape. Some items are holdable.
- **Session**: one run of the experience, from launch to exit. Holds the terminal history and
  the current world. Nothing carries over to the next session.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After finishing speaking, the user sees or hears acknowledgement within 1 second.
- **SC-002**: The surroundings begin visibly changing within 5 seconds of the user finishing
  a request.
- **SC-003**: A typical request for a place is fully built within 20 seconds.
- **SC-004**: A first-time user builds their first world within 60 seconds of entering, with
  no instructions beyond what the terminal shows.
- **SC-005**: Across 10 varied requests for places, 10 out of 10 produce a visible change to
  the world, and none end in an error or an empty space.
- **SC-006**: The three rehearsed demo requests (a golf course, a city, and a holdable golf
  club) each work 5 times in a row on the headset.
- **SC-007**: The experience stays visually smooth throughout a 5-minute session, with no
  noticeable stutter during world changes.
- **SC-008**: A full demo of entering, building a world, changing it, and holding an object
  fits within 3 minutes.

## Assumptions

- Voice input is push-to-talk (hold a controller button). Chosen over always-listening because
  the demo venue will be noisy.
- Requests are spoken in English.
- Controllers are the supported way to grab objects. Hand tracking is not required.
- The headset has a working internet connection for the whole session; there is no offline
  mode.
- One user at a time, standing or seated in place. No walking around large worlds and no
  teleporting in this feature.
- Worlds have a stylised look built from the library and simple shapes, not a photographic
  one.
- As much as possible is prepared and built into the experience ahead of time. The built-in
  library starts with the rehearsed demo requests and is then grown as large as time allows,
  since library breadth is what makes arbitrary requests look good.
- Nothing is saved between sessions, and there are no accounts.
- Deferred to a later polish pass and not part of this feature: AI-generated 360° image
  skyboxes, AI-generated 3D objects or textures, and turning a drawing into a 3D object.
- Story 4 (spoken replies and ambient sound) is the first thing to drop if time runs short.
