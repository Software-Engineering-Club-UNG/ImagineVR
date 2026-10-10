# The prompts behind ImagineVR

Everything in this repository grew out of one conversation between Israel and Claude Code on
the night of 2026-10-09. This file keeps Israel's side of it, word for word, plus the inputs
that were given to each Spec Kit command. With these and the rest of `specs/`, the work can be
redone from nothing.

Typing is left exactly as it was.

## Part 1: Israel's prompts, in order

### 1. Getting Spec Kit in place

> Can you help me use github spec kit?

> can you go ahead and integrate the whole spec kit into the claude like you said

*(Spec Kit was already set up for Codex; this added the Claude Code integration.)*

### 2. The idea

> okay help me draft this. ImagineVR is a VR experience for the Creative track for hacklanta. this is a hackathon so we will not need to strictly be ahering to maximum standards and all that as we will want to build quick. We will be using VULTR to have a linux instance which will handle our backbone (I imagine we will use openrouter with AI on the backend). But anyways here is my idea. I want a AI like experience for headsets, infact I will want something very similar to claude code, codex or opencode CLI when the user first enters into the experience. I will want it to be extremely polished too so UNITY seems like the clear case for this. I also want to be able to git clone this repo and have it working on my macbook air m1, currently on my beast of a pc (windows) but will be going back to the venue with my macbook in hand and will be sideloading this project onto my meta quest 3s. But anyways, after the user says something (there will be voice input) they can see their entire environment change as we process their speech (as it will be a prompt) and their envionrment will be built in real time. Adobe will be the sponser for this track, So I would like to incoporate some of their touches to the final output (IE, a paintbrush may be present to the user as a homage to photoshop, but we will not limit ourselves here) or maybe the user can have a drawing board in this game, and have it be turned into a cool 3d object in their environment. they can say maybe "I want to see a golf course" and have what is basically a golf course come up and then say "make me a golf club" and then be able to hold a golf club. I will want lots of textures to be baked on device ofc but would like a way to have a fallback mode just in case it doesnt have something i guess? I will want a VULTR server to be in between the headset and a AI openrouter instance that will interpret the requests, and inject it in real time (IE a hybrid, i imagine going full into the AI making assests and injecting them would be cool, but would be too much i guess? we can discuss this). I should be able to describe a city and be placed into a city. Basically making art with my imagination. what do you think? I imagine elevenlabs integration could be put here somewhere too

### 3. Shaping it

> lets not to Firefly API or whatever, we just need need a little bit of a adobe spice in it, i meant just like the icon for the paintbrush or something that makes it feel like a adobe product for a second. but yeah ofc the 360 skybox will be generated with ai. make your best pick for the 360 degree skybox, also lets not do offline demo if it holds back our online demo

> Blockade? Cant we point openrouter at claude opus 5.5 and have that do it? or some other model? isnt openrouter ideal for this?

> Idk i used claude  before to make a VR game and it was really good? how was that, cant we get this openrouter to return something that would change our scenary?

*(This is where the central design came from: the AI does not generate pictures or models at
runtime. It writes statements in a small scene language, and systems already built into the
app carry them out.)*

> okay that sounds good but mark it as a loop back for later so we can add polish.

*(The AI-generated 360° skybox became a "Loop Back for Polish" item in the constitution.)*

> you can use your judgement for unity verision and then we can discuss further

*(Unity 6000.3.26f1.)*

> can you install all that for me while we start drafting this spec

### 4. The two ideas that define the project

> This all sounds like my vision, at first I will want the user to be in a dark room (similar to a empty terminal) and then have it come to life. although I can not stress this enough, I want us to bake in as much before compile time and I do not mind wasting the tokens to make the prebuild take on as much as the heat, and then at the end we will basically have our custom language that will let us inject things live into the game.

*(The dark-room opening, the "Bake First, Inject Live" principle, and ImagineScript.)*

### 5. Infrastructure questions

> before we move on, you do know that we will be using VULTR? what if I also want to integrate AWS with any of this too? do you see me needing anything on there or not really

> can VULTR transcribe voices for us?

> thats true, update the plan and then lets get this into tasks!

*(Speech-to-text runs on the Vultr server itself with Whisper. AWS was left out.)*

### 6. Handing over the build

> Ill be honest i want to sorta sleep tonight, is there like a big chunk of work I can delegate to you so im not worried about hitting my 5 hour limit at the event? and then i can go to bed while your doing this work, my headset is also sorta in my car rn

> You can go ahead and go and implement all of that in a branch called "Initial Work", And honestly go crazy and make something polished as you can without my input, i really like this idea and see a huge amount of fun to be able to be had with it. Going to sleep so do as much as possible, honestly keep going until you cant stop! It would be cool if you hit my 5 hour limit (like seriously, I want to see how much AI can do with this too, I imagine the more we do before hand, the better as we will have maximum coverage for our AI to make cool worlds)

> also while your working on this, think about how phyics can play a role and things users can interact with can play a role too, just food for thought, sent this message before i went to bed so not sure how it will look in context with the work you did

*(Physics props, grabbing and throwing, tools such as the paintbrush and blaster, score zones
like the golf hole, and a gravity setting all came from that last message.)*

## Part 2: What was given to each Spec Kit command

Run these in order in a fresh clone to regenerate the documents in this folder.

### `/speckit-constitution`

No single input. The constitution was written from prompts 2, 3 and 4 above. Its six
principles: Demo-First; Speak → See; Bake First, Inject Live; Thin Headset, Smart Server;
Clone-and-Run on Windows and macOS; Polish Over Breadth.

### `/speckit-specify`

```text
Core loop for ImagineVR (Quest 3S VR experience). The user starts in a calm empty space in front of a floating terminal that looks and behaves like an agentic coding CLI (streaming text, visible "tool call" steps, clear listening / thinking / building states). The user speaks a request by voice (for example "I want to see a golf course" or "put me in a city"). Their speech is transcribed and shown in the terminal as their prompt. An AI interprets it and the whole environment around the user is rebuilt in real time: sky, fog, lighting, terrain and objects change through animated transitions while the terminal narrates each step. Follow-up requests modify the current world rather than resetting it (for example "make it sunset", "add more trees"). The user can ask for a holdable object (for example "make me a golf club") and it appears in front of them and can be picked up with the controllers or hands. Objects come from a built-in library of assets plus simple shapes; if something requested is not in the library, the closest substitute or a simple stand-in is used so every request produces a result and never an error or empty world. A small creative-tool visual touch (such as a paintbrush icon / tool-palette styling) gives a brief nod to the track sponsor. Out of scope for this feature (deferred to a later polish pass): AI-generated 360 image skyboxes, AI-generated 3D meshes or textures, drawing-board-to-3D-object, offline mode, accounts, persistence between sessions, multiplayer.
```

Afterwards the spec was edited by hand for prompt 4: the opening became a dark, empty room,
and the library became "large, prepared ahead of time".

### `/speckit-plan`

```text
Unity 6000.3.26f1 (URP, OpenXR-based XR stack, Android build target) for Meta Quest 3S; must clone and build on both Windows and an Apple-silicon Mac, binaries in Git LFS. One Linux server on Vultr sits between the headset and all AI: the headset talks only to it, API keys live only there. LLM via OpenRouter (model is a server setting). Voice: push-to-talk on a controller, audio sent to the server for transcription; ElevenLabs optional for spoken replies and ambient sound (P4, first to drop). Bake first, inject live: models, materials, shaders, sky/weather presets, terrain types, themed kits (city, golf course, forest...), transitions and sounds are all built into the app ahead of time, using AI heavily at development time (procedural meshes/shaders/generators as code, plus CC0 asset packs for detailed props). At runtime the LLM only writes statements in our own small, versioned scene language (recommended syntax: one JSON object per line, streamed and applied statement by statement) that pre-built Unity systems interpret; no runtime code compilation. A single catalog generated from the baked library defines the language's vocabulary (asset IDs, presets, parameters, limits) and is used by both the app and the server prompt. Unknown IDs or malformed statements degrade to a closest match or simple stand-in, never an error. Opening scene is a dark empty room with only the glowing agentic-CLI-style terminal; the first request reveals the world. Hold 72 fps. Hackathon speed: no mandated automated tests; verification is build + run on headset. Leave room in the scene language for the deferred polish items (AI-generated 360 skybox image URL, generated meshes, drawing-to-3D) without building them now.
```

Afterwards the plan was edited for the Whisper decision: transcription runs on the Vultr
instance (`faster-whisper`), with ElevenLabs as the fallback.

### `/speckit-tasks`

```text
Follow the Build order in plan.md so main is always demoable. No automated test tasks (constitution: tests optional); verification tasks are the quickstart.md scenarios on the headset. Transcription is local Whisper on the Vultr server.
```

### `/speckit-implement`

Not run as a command. The implementation was done directly from `tasks.md`; see `STATUS.md`
at the repository root for what was built and where it stopped.

## Part 3: Redoing it from scratch

1. `uvx --python 3.13 --from git+https://github.com/github/spec-kit.git@v1.1.3 specify init --here --ai claude --script ps`
   (use `--script sh` on macOS or Linux; I did not run `init` myself, so check the flag names with
   `specify init --help`. Adding Claude to an existing setup is `specify integration install claude`,
   which I did run.)
2. In Claude Code, started from the repository folder: `/speckit-constitution`, giving it
   prompts 2 to 4 from Part 1.
3. `/speckit-specify`, `/speckit-plan`, `/speckit-tasks` with the inputs in Part 2.
4. For the implementation, the prompt that worked was the second one in section 6 of Part 1:
   build everything on a branch, without stopping to ask, favouring a large baked library.
