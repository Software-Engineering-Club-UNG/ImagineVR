# Runtime blueprints tasks

## Setup and foundation

- [X] T001 Record design and procedural-generation governance amendment in .specify/memory/constitution.md.
- [X] T002 Define versioned capability and write contract tests in shared/runtime-blueprints.json and openrouter-api/tests/test_blueprints.py.

## US1: Create and edit unfamiliar objects

- [X] T003 [US1] Implement strict blueprint validation and registry operations in openrouter-api/imagine/blueprints.py and script.py.
- [X] T004 [US1] Implement incremental construction, registry, atomic instance edits and cleanup in client/Assets/ImagineVR/Scripts/World/BlueprintSystem.cs.
- [X] T005 [US1] Route generated spawn/give and preserve interaction/entity identity in client/Assets/ImagineVR/Scripts/World/World.cs and PropSystem.cs.

## US2: Generic behavior

- [X] T006 [US2] Add generic actor movement, animation, state and emitters in client/Assets/ImagineVR/Scripts/World/BlueprintActor.cs.
- [X] T007 [US2] Integrate named-part rules/contact damage/player health in client/Assets/ImagineVR/Scripts/World/RuleEngine.cs and BlueprintActor.cs.

## US3: Integration and safety

- [X] T008 [US3] Add capability-aware prompting, sculpt fallback, snapshots and resets in openrouter-api/imagine/main.py, turn.py and client/Assets/ImagineVR/Scripts/Net/TurnClient.cs.
- [X] T009 [US3] Add generic examples, backend regression and Unity tests in shared/blueprint-examples/ and client/Assets/ImagineVR/Tests/BlueprintTests.cs.

## Acceptance

- [X] T010 Run backend tests, contract parity, native C# compilation and document results in specs/003-runtime-blueprints/README.md.
- [ ] T011 Run pinned Unity import/PlayMode/Android/Quest acceptance and 72FPS checks in client/; publish only after native acceptance.

T011 partial: pinned Unity import/compilation and 16 native PlayMode tests passed. Android support is absent from the installed editor; Quest visual/interaction/72FPS acceptance has not been run. No publication performed.

Dependencies: T001->T002->T003->T004->T005->T006->T007->T008->T009->T010->T011. Sequential ownership avoids edits colliding in shared integration files. MVP US1 can be evaluated before behavior; final acceptance requires all stories. Separate example/contract tests may run concurrently with native validation after implementation.
