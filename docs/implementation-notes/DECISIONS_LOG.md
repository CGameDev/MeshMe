# Implementation decisions

## 2026-09-05 — M0

- Imported the entire pinned upstream tree so source, benchmark scripts, textures,
  notices and provenance remain inspectable. Deployment will select runtime assets
  only; upstream Docker/server instructions do not apply to the desktop application.
- Preserve upstream bytes with Git attributes and a recorded blob inventory.
- Create only Desktop and Core .NET projects now. Architecture section 2 makes a
  separate Updater project conditional; no empty second executable is needed.
- Reserve the specified test/app/vendor directories; add actual tests with their
  milestone behavior, rather than placeholder passing unit tests.
- The host has SDK 10.0.400; the project still targets .NET 8 and Windows x64.
  Build targeting packs are development dependencies, not an end-user requirement.
