<!--
Sync Impact Report (scratch — remove before committing)
- Version change: (template) → 1.0.0
- Modified principles: none (initial ratification)
- Added sections: Core Principles I–VI, Technology & Constraints, Loop Back for Polish,
  Development Workflow, Governance
- Removed sections: none
- Follow-up TODOs:
  - Unity pinned to 6000.3.26f1; install the same version on the MacBook before the venue
  - Loop back for polish (see section): AI-generated 360° skybox, generated meshes and
    textures, drawing board to 3D object
-->

# ImagineVR Constitution

ImagineVR is a voice-driven VR experience for Meta Quest 3S, built for the Hacklanta Creative
track (sponsor: Adobe). The user starts in front of an agentic-CLI-style terminal, speaks what
they imagine, and watches the world around them get built in real time.

This is a hackathon project. These principles exist to keep us fast and keep the demo alive,
not to enforce production standards.

## Core Principles

### I. Demo-First, Always Shippable

- `main` MUST always build to a Quest 3S APK that launches and runs the core loop.
- The core loop (speak → terminal responds → environment changes) MUST work end to end before
  any secondary feature is started.
- Every feature MUST be cut to the smallest version that looks good in a 3-minute demo.
- Anything that cannot be demoed on the headset by judging time is out of scope.

Rationale: judges see the headset, not the repo. A smaller thing that works beats a bigger
thing that almost works.

### II. Speak → See: Latency Is the Experience

- The user MUST get visible or audible acknowledgement within 1 second of finishing speaking
  (terminal text, sound, or animation).
- The environment MUST begin visibly changing within about 5 seconds of a request.
- Slow work (asset generation, large downloads) MUST run in the background behind a visible
  in-world placeholder and swap in when ready. It MUST NOT block the user or freeze the frame.
- Server responses SHOULD stream so the terminal can show progress as it happens.

Rationale: the magic is the feeling that the world answers you. A long silent wait kills it.

### III. Bake First, Inject Live

- Anything that can be made before the build MUST be made before the build: models,
  materials, shaders, sky and weather presets, terrain types, themed kits (city, golf course,
  forest, and so on), transitions, and sounds all ship inside the app.
- Build-time effort is cheap and runtime effort is expensive. AI tokens and generation time
  SHOULD be spent freely during development to grow and polish the baked library; the
  runtime path stays small and fast.
- At runtime the AI does not create content. It writes statements in the **scene language**:
  one small, declarative, versioned language whose vocabulary is exactly what the baked
  systems expose. The server validates each statement and the headset interprets it live,
  statement by statement, as it streams in.
- The scene language's vocabulary (asset IDs, presets, parameters, limits) MUST be generated
  from the baked library into one catalog that both the app and the server prompt use, so
  the AI can never be told about something the build does not contain.
- World building MUST resolve through tiers, fastest first:
  1. **Baked library** — curated on-device models, materials, and a few default skyboxes,
     referenced by ID.
  2. **Procedural** — sky, fog, lighting, terrain, primitives, tinting, scaling, and layout
     composed at runtime from the AI's scene commands. This tier leads: it is how the world
     is built, sky included.
  3. **Generated** — AI-made images or meshes fetched from the server at runtime. Deferred;
     see Loop Back for Polish.
- Every request MUST produce something. If a higher tier fails or times out, the system MUST
  fall back to a lower tier instead of showing an error or an empty world.
- Unknown asset IDs or malformed scene-language statements MUST degrade to a fallback, never
  crash.
- The online experience is the demo. An offline mode is NOT required and MUST NOT take time
  away from the online path.

Rationale: baked content is instant, polished, and cannot fail on stage. A rich library plus
a small language to compose it gives the feeling of unlimited generation at the cost of a
text response, and falling back to what is on the device keeps the world responding when a
service is slow.

### IV. Thin Headset, Smart Server

- The headset MUST talk only to our Vultr server. It MUST NOT call OpenRouter, ElevenLabs, or
  any other AI provider directly.
- API keys MUST live only on the server (environment variables). They MUST NOT be committed
  to the repo or shipped inside the APK.
- Prompts, model choice, and scene-planning logic MUST live on the server so they can change
  without rebuilding and re-sideloading the app.
- Client and server MUST share one versioned scene-language definition and catalog, kept in
  the repo.

Rationale: a sideload takes minutes, a server restart takes seconds. Keeping the brains on the
server makes iteration fast and keeps secrets out of the build.

### V. Clone-and-Run on Windows and macOS

- A fresh `git clone` MUST open and build for Quest on both Windows and an Apple-silicon Mac
  with no manual fix-up beyond installing the pinned Unity version and the Android module.
- The Unity version MUST be pinned and committed; nobody upgrades it mid-hackathon.
- Binary assets MUST go through Git LFS. `Library/`, `Temp/`, and build output MUST be ignored.
- Plugins or tools that work on only one desktop OS MUST NOT be required to build or run.
- The workflow MUST NOT depend on Quest Link, which is unavailable on macOS; in-editor
  simulation and build-to-device are the supported test paths.

Rationale: development moves from a Windows PC to a MacBook Air M1 at the venue. A broken
clone there costs hours we do not have.

### VI. Polish Over Breadth

- The app MUST hold the Quest 3S native refresh rate (72 fps minimum) in every demoed scene.
  Generated and baked assets MUST respect triangle and texture budgets set in the plan.
- The opening terminal MUST feel like a real agentic CLI: streaming text, visible "tool call"
  steps, clear listening / thinking / building states.
- World changes MUST be animated transitions, never hard pops.
- The sponsor nod is visual only: a small creative-tool touch (for example a paintbrush icon
  or tool-palette styling) that makes it feel like an Adobe product for a moment. No Adobe
  API or service is used, and Adobe logos or trademarks MUST NOT be copied.
- Given a choice between a new feature and making an existing one feel great, choose polish.

Rationale: this is the Creative track. Comfort and craft are what the judges will remember.

## Technology & Constraints

- **Client**: Unity 6.3 LTS, pinned to **6000.3.26f1**, URP, Android build target, Meta
  Quest 3S. OpenXR-based XR stack. Both machines install this exact version with the Android
  Build Support module (Apple-silicon editor on the Mac).
- **Server**: one Linux instance on Vultr exposing a single HTTP/WebSocket API to the headset.
- **AI**: LLM access through OpenRouter; the model is a server-side setting and swappable.
- **Voice**: microphone audio is captured on the headset and sent to the server for
  transcription. ElevenLabs MAY be used for the assistant's voice and generated sound.
- **Scene building**: the LLM writes scene-language statements (sky, fog, lighting, terrain,
  objects, transitions) that pre-built Unity systems interpret. No code is compiled at
  runtime. The concrete syntax is decided in the plan; it MUST be streamable one statement
  at a time and easy for an LLM to write correctly.
- **Opening scene**: a dark, empty room with only the terminal lit, which the first request
  brings to life.
- **Runtime assets**: generated meshes are delivered as glTF/GLB and loaded at runtime.
- **Comfort**: no forced camera motion; environment changes happen around a stationary user.
- **Out of scope**: accounts, persistence across sessions, multiplayer, store submission.

## Loop Back for Polish

Deferred on purpose. Revisit only once the core loop runs well on the headset.

- **AI-generated 360° skybox**: an image-output model through OpenRouter, prompted by the
  server for an equirectangular panorama and layered behind the procedural scene. The server
  owns seam and pole clean-up. Keep the provider behind one server function so it can be
  swapped (for example to Blockade Labs Skybox AI) if quality is not good enough.
- **Generated 3D meshes and textures**: text-to-3D for objects not in the library.
- **Drawing board to 3D object**: the user sketches and the sketch becomes an object.

The scene-command schema SHOULD leave room for these (for example an optional skybox image
URL) so adding them later does not require a redesign.

## Development Workflow

- Automated tests are OPTIONAL. The required check is: it builds, it runs on the headset, the
  core loop still works.
- Commit small and often to short-lived branches; merge to `main` only when it builds.
- Spec Kit flow per feature is `specify → plan → tasks → implement`. `clarify` and `analyze`
  are optional and used only when a spec is unclear.
- Keep a running list of rehearsed demo prompts and re-test them on device after any change
  to the scene-command schema, the asset library, or the server prompts.
- Feature freeze before judging: the last stretch is for polish and rehearsal only.

## Governance

- This constitution guides every spec, plan, and task list in this repo. When a plan conflicts
  with it, either change the plan or amend the constitution; do not ignore it silently.
- Any team member MAY amend it by editing this file in a commit that states what changed and
  why. No formal review is required.
- Versioning: MAJOR for removing or redefining a principle, MINOR for adding a principle or
  section, PATCH for wording.
- Complexity that goes against Principle I or VI MUST be justified in the plan in one line.

### Runtime procedural blueprint amendment (2026-10-10)

User-requested expansion: the AI MAY compose unfamiliar low-poly objects using a
bounded, versioned declarative blueprint language. Geometry, named-part animation,
movement, sensing, health and emitters are preimplemented capabilities, not executable
model code. Blueprint recipes are session-only and use the procedural tier; no external
mesh provider or additional provider secret is required. This exception expands the
original demo-first scope to generic composition, while keeping baked assets preferred,
safe fallback, no forced camera motion, and native Quest acceptance before release.
Player damage MUST require local session opt-in. Procedural recipes need not use GLB;
the GLB constraint continues to apply only to externally generated binary models.

**Version**: 1.1.0 | **Ratified**: 2026-10-09 | **Last Amended**: 2026-10-10
