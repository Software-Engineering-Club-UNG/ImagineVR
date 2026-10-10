# Research: Voice World Builder (Core Loop)

Decisions made for the plan. Each one lists what was chosen, why, and what was passed over.
Items marked **Verify early** rest on something not yet confirmed on real hardware or a real
account and should be checked in the first hours of the build.

## 1. XR stack

- **Decision**: Unity OpenXR Plugin with the Meta Quest feature set, plus XR Interaction
  Toolkit (XRI) for controllers, rays, and grabbing. Input through the Input System package.
- **Rationale**: Everything installs from the Unity Package Manager, so a fresh clone resolves
  the same packages on Windows and macOS with no Asset Store downloads (Constitution V). XRI
  already provides grab interactables (Story 3) and an in-editor simulator, which is the only
  way to test on the Mac without a headset build since Quest Link is unavailable there.
- **Alternatives considered**: Meta XR All-in-One SDK. More Quest-specific features (hand
  tracking polish, passthrough), but heavier, more project setup, and nothing in this feature
  needs it.

## 2. Rendering and performance budget

- **Decision**: URP, Vulkan, IL2CPP, ARM64, single-pass instanced stereo, 72 Hz, 4x MSAA,
  fixed foveated rendering on. No realtime shadows; grounding comes from baked blob shadows
  under objects. One shared palette-texture material for nearly all library meshes so the SRP
  batcher and GPU instancing keep draw calls low.
- **Budgets** (enforced by catalog limits, see contracts): about 150k visible triangles,
  about 100 draw calls, at most 300 instances per scatter and 1,500 scattered instances in
  the world, at most 60 individually spawned objects.
- **Rationale**: The world is assembled live from unknown requests, so the only safe way to
  hold frame rate (Constitution VI) is to make every baked piece cheap and cap how many the
  language can ask for.
- **Alternatives considered**: Realtime shadows (too costly with an unpredictable object
  count); Unity Terrain (heavy on Quest and slow to reshape at runtime).

## 3. The dark room and the reveal

- **Decision**: The dark room is not a separate scene. It is the same world systems at their
  zero state: black sky, zero ambient light, black fog, no terrain, no objects, only the
  self-lit terminal. The first request's statements transition those systems away from zero.
- **Rationale**: The signature reveal (FR-001a) costs nothing extra to build and uses the
  same transition code as every later change.
- **Alternatives considered**: A hand-authored intro scene with a scene load. Adds a loading
  hitch at the exact moment that must feel magical.

## 4. Sky, light, and atmosphere

- **Decision**: One custom procedural skybox shader with lerpable parameters: three-stop
  gradient (top, horizon, bottom), sun disc and elevation/azimuth, star amount, cloud amount.
  A directional light, ambient colour, and fog follow the sky. Named presets (for example
  `noon`, `golden_hour`, `night_city`, `overcast`, `void`) are baked as data; statements may
  pick a preset and override individual values.
- **Rationale**: Crisp, seamless, instant, and fully animatable. "Make it sunset" is a
  two-second lerp.
- **Alternatives considered**: AI-generated panorama images (deferred to Loop Back for
  Polish; the language reserves an operation for it).

## 5. Terrain

- **Decision**: A runtime-generated ground mesh driven by a small set of baked generator
  types (`flat`, `rolling`, `hills`, `dunes`, `paved`, `water`) with material, tint, height,
  and seed parameters. A flat safe disc of 3 m radius is always kept under the user.
- **Rationale**: Cheap to draw, can rise out of the darkness as an animation, and a handful
  of types covers most requests.
- **Alternatives considered**: Pre-modelled terrain per theme (does not compose with
  follow-ups like "make it hillier").

## 6. Kits (themed generators)

- **Decision**: A *kit* is a baked C# generator that lays out a whole themed set from library
  parts with a few parameters (density, size, seed): for example `city` (street grid of
  building modules with lit windows), `golf_course` (fairway, green, flag, bunkers, trees),
  `forest`, `beach`, `desert`, `space`. One statement invokes a kit; further statements then
  add, scatter, tint, or remove on top of it.
- **Rationale**: This is where "bake first" pays off. The AI gets a good-looking place from
  one statement within seconds, and development-time AI effort goes into making each kit
  rich. Requests outside any kit still work through terrain + scatter + spawn.
- **Alternatives considered**: Having the AI place every building and tree individually
  (slow, token-heavy, and layouts look random).

## 7. Asset library

- **Decision**: Low-poly, palette-textured CC0 packs (Kenney, Quaternius) for detailed props,
  plus meshes, shaders, and generators written as code during development. Each library item
  is a ScriptableObject entry with an ID, a one-line description, tags, aliases, size, and a
  holdable flag. Binaries are stored in Git LFS.
- **Rationale**: CC0 means no licence risk; one consistent art style; palette texturing
  allows runtime tinting and one shared material. Detailed props such as a golf club look
  better from a pack than from code.
- **Alternatives considered**: Asset Store packs (licence terms and mixed styles); runtime
  generated meshes (deferred).
- **Verify early**: confirm each pack's licence file says CC0 before committing it.

## 8. The scene language

- **Decision**: Name: **ImagineScript**, version 1. Syntax: newline-delimited JSON, one
  statement per line, each with an `op` field. Applied by the headset one statement at a
  time as lines arrive. Full definition in [contracts/scene-language.md](contracts/scene-language.md).
- **Rationale**: Models write JSON reliably; a line is complete and applicable the moment its
  newline arrives, which gives streaming for free; unknown operations and fields can be
  skipped, which gives forward compatibility for the deferred polish items.
- **The terminal's "tool calls" are the statements themselves.** Each applied statement is
  rendered as a tool-call line (for example `● sky(preset: golden_hour)`), so narration is
  always in step with the world (FR-011) without asking the AI to describe its work twice.
- **Alternatives considered**: A custom text grammar (needs a parser, and models make more
  syntax errors in an unfamiliar grammar); one large JSON document (cannot be applied until
  complete, so nothing happens for seconds); LLM tool-calling API (ties the format to one
  provider's protocol and streams less predictably).

## 9. Catalog: one source of vocabulary

- **Decision**: A Unity editor menu command exports every library entry, preset, kit,
  terrain type, ambience loop, and limit into `shared/catalog.json`, which is committed. The
  server loads that file at startup to build the AI's instructions; the app embeds the same
  file. Both report its version, and the server logs a warning on mismatch.
- **Rationale**: The AI can only be told about things the build contains (Constitution III).
- **Fallback resolution** happens twice: the server rewrites unknown IDs to the nearest
  catalog entry by alias and tag before forwarding, and the headset does the same again and
  finally falls back to a labelled simple shape. Either layer alone is enough to guarantee
  FR-014.

## 10. Headset-to-server transport

- **Decision**: Plain HTTPS. One `POST /v1/turn` per request; the response body is a stream
  of ImagineScript lines read incrementally with `UnityWebRequest` and a streaming download
  handler. The body is either WAV audio or JSON text. Details in
  [contracts/server-api.md](contracts/server-api.md).
- **Rationale**: No connection lifecycle to manage, works through `curl` so the whole server
  can be developed and tested without a headset, and the text body lets the editor and the
  Mac drive the world from the keyboard.
- **Alternatives considered**: WebSocket (lower overhead and bidirectional, but needs
  reconnect handling and is harder to poke by hand; nothing in this feature needs the server
  to push unprompted).

## 11. Server

- **Decision**: Python 3.12, FastAPI, uvicorn, httpx, managed with `uv`. In-memory session
  state. Runs as a systemd service on one Ubuntu LTS instance on Vultr behind Caddy for
  automatic TLS. If there is no domain, use an `sslip.io` hostname for the instance IP so
  Caddy can still obtain a certificate.
- **Rationale**: Smallest thing that streams well; `uv` is already installed; restart takes
  seconds (Constitution IV). TLS avoids Android's cleartext-traffic restrictions.
- **Alternatives considered**: Node/Express (equally viable; Python chosen for brevity);
  serverless (cold starts hurt the latency targets).
- **Verify early**: reach `/v1/health` from the headset's browser on venue Wi-Fi.

## 12. AI model access

- **Decision**: OpenRouter chat completions with streaming. The model is the
  `IMAGINE_MODEL` environment variable; start with a fast Claude model and move up only if
  scene quality needs it. The catalog and language reference form a long, stable system
  prompt placed first so provider-side prompt caching can apply.
- **Rationale**: First-statement latency drives SC-002, so speed matters more than depth for
  this task. Keeping the model a server setting allows switching mid-demo without a rebuild.
- **Verify early**: time-to-first-line for two candidate models with the real catalog.

## 13. Speech-to-text

- **Decision**: Record on the headset with Unity's `Microphone` API at 16 kHz mono while the
  talk button is held, encode WAV, upload with the turn. The server transcribes behind one
  `transcribe()` function. Default: Whisper running on the Vultr instance itself, through
  the `faster-whisper` library with a small English model (`base.en`, or `small.en` if the
  instance keeps up), loaded once at startup and kept in memory. Fallback behind the same
  function: ElevenLabs speech-to-text.
- **Rationale**: No extra provider, key, or network hop for the step that happens on every
  request, so one fewer thing can fail during the demo. Utterances are a few seconds long,
  which a small model handles on CPU. The Vultr server does real work.
- **Instance size**: at least 4 vCPUs and 8 GB of memory. No GPU required.
- **Alternatives considered**: An audio-capable model through OpenRouter (extra round trip
  and unproven for plain transcription); Amazon Transcribe (a second cloud for no clear
  gain); on-device dictation (needs Meta's Voice SDK, which the chosen XR stack avoids).
- **Verify early**: measure transcription time for a 5-second clip on the actual instance,
  and accuracy in a noisy room. If either is poor, try the next model size, then switch the
  function to the fallback.

## 14. Push-to-talk control

- **Decision**: Hold the right controller's primary button (A) to talk; grip grabs objects.
  In the editor: hold Space to talk, or type into a debug text field.
- **Rationale**: Keeps talking and grabbing on different buttons so holding an object does
  not trigger listening.

## 15. Spoken replies and ambient sound (Story 4)

- **Decision**: Ambient sound is baked: a set of loops in the library, selected by an
  `ambience` statement. Spoken replies use ElevenLabs text-to-speech on the server for `say`
  statements; the statement carries an audio URL the headset plays.
- **Rationale**: Baked ambience has no runtime cost or failure mode. Speech is isolated to
  one optional field, so Story 4 can be dropped by turning one server setting off.
- **Verify early**: ElevenLabs key and quota.

## 16. Concurrency and failure behaviour

- **Decision**: The server handles one turn per session at a time; a second request waits.
  The headset applies statements through a single ordered build queue. If no line arrives
  within 8 seconds, or the stream ends without `done`, the terminal shows the problem state
  and whatever was already applied stays (FR-020, FR-021).
- **Rationale**: Statement-by-statement application means there is no half-applied
  transaction to roll back: every applied statement left the world valid.

## 17. Access control

- **Decision**: Provider keys exist only as server environment variables. The app sends a
  low-value app token header so strangers cannot spend the AI budget; the server also caps
  requests per minute, and the OpenRouter key has a spend limit.
- **Rationale**: Satisfies Constitution IV. The app token is not a provider secret and can
  be rotated on the server at any time.

## 18. Developing without the full chain

- **Decision**: Two tools from day one: (a) a script that posts text to the server and prints
  the stream, and (b) a Unity editor window that replays a saved ImagineScript file into the
  scene. Example files live in `shared/examples/`.
- **Rationale**: The Unity side and the server side can be built in parallel against the
  contract, and rehearsed demo scenes can be tuned by hand.

## 19. Testing

- **Decision**: No automated test suite. Verification is the scenarios in
  [quickstart.md](quickstart.md), run on the headset. The server validates every statement
  against the catalog at runtime, which doubles as a live contract check.
- **Rationale**: Constitution, Development Workflow.
