# Issue #1 checkpoint evidence

## M0 — PASS (2026-09-05)

- Exact `a6ac179149b8a17c71a9469dd4cb6f866c0c01d1` tree imported.
- `python build/verify-upstream.py`: PASS, all 117 Git blob identities,
  exact file inventory and complete root AGPL license verified.
- `git check-attr text`: upstream source and LICENSE have text conversion disabled.
- Provenance, attribution, dependencies and traceability recorded in
  `UPSTREAM_BASELINE.md`, `build/upstream-tree.txt`, and `THIRD_PARTY_NOTICES.md`.
- `dotnet build MeshMe360.sln -c Debug`: PASS, 0 warnings, 0 errors;
  .NET 8 WPF executable and Core library built on Windows x64 using SDK 10.0.400.
- No geometry modifications; no algorithm regression gate is triggered by the
  byte-identical import. M0 introduces no host service behavior requiring unit tests.
- Initial archive inherited Windows Git CRLF conversion. Re-extracted with
  `git -c core.autocrlf=false archive`; the identity check now passes.

## M1 — NOT STARTED

M2–M11 remain NOT STARTED. Feature-matrix acceptance is separate from scaffolding.
