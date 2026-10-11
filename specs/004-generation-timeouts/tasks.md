# Generation timeout tasks

- [X] T001 Add regression tests for longer first-text/content gaps, bounded stalls, environment overrides and Unity settings parity.
- [X] T002 Update backend and Unity defaults; document configurable limits.
- [X] T003 Run backend tests and dependency/diff checks; record results.

Execute in order. No gameplay, model-selection or fallback changes. Native headset/build acceptance remains a release check; numeric Unity settings changes do not require new runtime code.

Verification: 146 backend tests passed, including six new timeout/settings cases; pip check passed; git diff --check passed. The new delayed-stream regression failed under the old limits and passed under the new ones. No provider requests, deployment or headset run performed. Unity asset/code values are checked for parity; an updated client build is needed to ship the 15/90-second client settings.
