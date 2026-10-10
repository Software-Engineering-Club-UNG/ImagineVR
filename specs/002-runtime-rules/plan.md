# Runtime rules implementation

Implement the approved conversational plan: a shared allowlisted contract, atomic
backend rule validation, Unity entity registry/event dispatcher/action scheduler,
existing hook integration, per-turn runtime synchronization and capability negotiation.
Rules are session-only; asset selectors include future objects; custom hit rules replace
defaults; player effects require local opt-in. No executable code or external assets.

Acceptance: sheep hit by projectiles emit confetti and only that instance disappears;
replacing the same rule with tint makes later hits turn sheep blue without knocking
them down. Include lifecycle, combat, interaction, spatial, timing and state events,
object/world/player actions, bounded conditions and runtime budgets. Preserve existing
scene commands, instanced rendering, and legacy-client scene-only behavior.

Use Python/FastAPI in openrouter-api and C#/Unity 6000.3.26f1 in client. Shared JSON
capabilities are the source of truth. Tests precede integrations; Unity compilation,
Android build and headset acceptance remain explicit gates, never inferred from Python.
