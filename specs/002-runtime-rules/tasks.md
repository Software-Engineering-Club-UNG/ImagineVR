# Runtime rules tasks

- [X] T001 Shared contract and backend validation tests.
- [X] T002 Backend rule validation, session state, snapshot endpoint and capability gating.
- [X] T003 Unity entity registry, rule scheduler and bounded action execution (code; native tests pending).
- [X] T004 Connect creature/prop/grab/projectile/lifecycle hooks and instanced entities (code; native tests pending).
- [X] T005 Unity per-turn snapshot sync and local player-effects toggle (code; native tests pending).
- [X] T006 Regression tests, demos, contract documentation and supplementary C# compile check.
- [ ] T007 Pinned Unity compilation, EditMode/PlayMode tests, Android/headset acceptance and production rollout after verified client build.

Verification: 78 Python tests passed; runtime C# compiles against installed
6000.3.22f1 reference assemblies. 6000.3.26f1 is absent. T007 is a real gate: no
push/deployment or headset success is claimed. See README.md for acceptance steps.
