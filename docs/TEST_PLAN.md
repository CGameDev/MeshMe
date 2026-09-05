# Mesh Me360 — Test Plan

This test plan is mandatory for v1.0 qualification. A successful compile is not sufficient.

## 1. Test layers

### A. .NET unit/integration tests
Cover:
- settings schema read/write/migration
- recent-file normalization/de-duplication/retention
- recovery metadata and pruning
- startup argument parsing
- bridge token generation/expiration
- file-association registration data builders
- update preference/throttle logic
- application data path behavior

### B. Web engine tests
Cover preserved upstream behavior using deterministic fixtures and browser-capable test environment where required.

### C. Host/WebView2 integration tests
Cover:
- startup handshake
- internal bridge GET/POST
- native file activation
- download interception
- settings/theme sync
- recovery flow

### D. Manual/automated UI acceptance
Compare against `docs/UI_SPEC.md` and SVG references.

### E. Clean-machine release qualification
Test packaged installer on supported Windows VM.

---

## 2. Required engine fixtures

Create a small repository-controlled fixture set under `tests/fixtures/` with appropriately licensed/original/simple geometry.

Minimum fixtures:

1. `cube_binary.stl`
   - closed manifold cube
   - predictable dimensions

2. `cube_ascii.stl`
   - same simple shape in ASCII

3. `curved_test.stl`
   - curved surface sufficient to exercise triplanar/cylindrical/spherical behavior

4. `open_edge_test.stl`
   - intentional boundary/open edge

5. `two_shells_test.stl`
   - two disconnected shells

6. `simple.obj`

7. `simple.3mf`

8. `simple.step`
   - intentionally simple STEP model licensed/created for test use

9. at least one small custom grayscale texture created specifically for Mesh Me360 tests

Do not use copyrighted commercial models as automated fixtures.

---

## 3. Upstream parity baseline

Before modifying an algorithm, run the pinned upstream baseline on selected deterministic fixture/settings combinations and record baseline results in a machine-readable manifest under:

`tests/baselines/bumpmesh-a6ac179/`

For deterministic operations record as applicable:
- input fixture hash
- settings
- output triangle count
- bounding box
- number of invalid/NaN vertices
- diagnostics counts
- output file parse validity
- stable output hash only where output byte order is known deterministic

Do not require byte-for-byte equivalence when upstream serialization includes nondeterministic ordering. Use structural comparisons instead.

---

## 4. Import tests

For each supported import format verify:
- file is accepted;
- unsupported/corrupt file fails cleanly;
- model is visible;
- triangle count is plausible/expected for fixture;
- bounding dimensions match expected tolerance;
- no NaN/Infinity positions;
- UI leaves Loading state;
- file appears in Recent Files where applicable.

STEP/STP additionally:
- coarse/standard/fine quality options work as upstream defines them;
- worker/runtime files load locally with networking disabled;
- conversion diagnostics remain visible.

---

## 5. Projection tests

For representative fixture and deterministic texture, test:
- Triplanar
- Cubic
- Cylindrical
- Spherical
- Planar XY
- Planar XZ
- Planar YZ

For each:
- preview renders;
- changing U/V scale changes mapping;
- lock U/V follows upstream behavior;
- offset changes placement;
- rotation changes mapping;
- export completes;
- resulting geometry contains no NaN/Infinity;
- result remains within expected structural tolerance of upstream baseline.

Cubic/Cylindrical:
- seam settings affect expected region;
- no regression in seam blending behavior.

Cylindrical:
- cap-angle behavior verified.

---

## 6. Displacement tests

Test:
- amplitude 0 / low / representative mid / maximum supported upstream value;
- symmetric displacement off/on;
- 3D preview off/on;
- overlap warning trigger fixture/settings;
- exported displacement remains structurally valid.

Amplitude 0 should not introduce unintended geometry movement beyond allowed serialization effects.

---

## 7. Mask tests

Test:
- top angle mask;
- bottom angle mask;
- Paint Exclude;
- Paint Include;
- brush radius;
- bucket fill;
- fill threshold;
- Shift/erase;
- clear mask;
- linear transition;
- S-curve transition;
- ease-in transition.

Verify:
- face counts shown in UI update;
- excluded/included regions affect preview/export as intended;
- undo/redo restores mask states;
- tool exit with Esc is safe.

---

## 8. Subdivision tests

Test:
- representative edge-length values from upstream controls;
- crease-preservation fixture/behavior;
- progress reporting;
- high-triangle warning/safety behavior;
- output contains valid indices/positions.

Test the 10M-triangle cap using estimation/unit paths where creating a real 10M mesh would make routine tests impractical. Add at least one controlled manual stress test before release.

---

## 9. Decimation tests

Test representative target triangle counts.

Validate:
- target behavior follows upstream implementation;
- boundaries/creases are not obviously corrupted compared with upstream result;
- link-condition/normal-flip safety behavior remains active;
- no invalid indices;
- no NaN/Infinity;
- output can re-import.

---

## 10. Diagnostics tests

`open_edge_test.stl`:
- open edge count/diagnostic is non-zero as expected;
- overlay can be enabled/disabled.

`two_shells_test.stl`:
- shell count reports 2 or the exact upstream-equivalent expected value;
- overlay behavior works.

Closed cube:
- no false open-edge error under expected upstream behavior.

---

## 11. Project save/load tests

Test:
- save `.bumpmesh`;
- reload in same Mesh Me360 version;
- load a project created by pinned upstream baseline where compatible fixture is available;
- model, settings, projection, mask, custom texture and other serialized state restore according to upstream format capabilities;
- Save Project clears dirty state after success;
- Export STL/3MF does not clear dirty state.

Round-trip test:
1. load fixture;
2. apply texture/settings;
3. mask faces;
4. save project;
5. close/reopen;
6. compare restored logical state.

---

## 12. Recent-file tests

Verify:
- new open moves path to top;
- same normalized path is de-duplicated case-insensitively;
- list caps at 20;
- spaces/Unicode paths work;
- missing file is marked and removable;
- Clear Recent Files works;
- no file contents are copied into recent metadata.

---

## 13. File activation tests

Command-line:

```text
MeshMe360.exe "C:\Test Models\cube_binary.stl"
```

Verify exact file opens.

Test:
- path with spaces;
- Unicode path;
- `.bumpmesh`;
- each supported shared model type;
- unsupported extension gives clear error without crash;
- nonexistent path gives clear error.

---

## 14. Internal bridge tests

### `GET /file/{token}`
Verify:
- valid token returns exact bytes;
- consumed/expired token is rejected;
- arbitrary token rejected;
- no raw path traversal;
- large test file does not use base64 JSON path.

### `POST /recovery/{token}`
Verify:
- valid token writes atomically;
- invalid token rejected;
- partial/failed write does not replace previous valid snapshot;
- final path remains under recovery root;
- token cannot be reused outside intended policy.

### Control messages
Verify:
- request/response id correlation;
- malformed JSON/message rejected/logged safely;
- unknown message type does not crash host.

---

## 15. Autosave tests

With autosave enabled:
- clean document does not create repeated snapshots;
- dirty document autosaves at 120-second interval;
- autosave does not clear dirty state;
- autosave does not change explicit save path;
- autosave defers during export/unsafe processing;
- maximum 5 snapshots per document enforced;
- global recovery-set cap enforced;
- retention pruning follows rules.

With autosave disabled:
- no periodic recovery snapshot is generated, while crash/session marker behavior remains safe.

---

## 16. Crash recovery tests

Scenario A — unsaved model:
1. load model;
2. make edits;
3. wait for autosave;
4. force terminate process;
5. relaunch;
6. recovery dialog appears;
7. Recover Selected;
8. recovered document is dirty;
9. original source file is unchanged.

Scenario B — saved project with newer changes:
- save project;
- modify;
- autosave;
- terminate;
- recover;
- verify recovered state contains later change while saved project remains untouched.

Scenario C — corrupt latest snapshot:
- retain earlier valid snapshot;
- corrupt latest snapshot;
- relaunch;
- app should fall back/list valid recovery rather than fail startup.

---

## 17. Texture Library tests

Verify:
- all upstream built-ins present;
- built-ins cannot be deleted/renamed;
- import supported custom texture;
- thumbnail generated;
- metadata persisted;
- rename/tags/favorite persist across restart;
- search works;
- filters work;
- delete custom item removes managed copy only;
- deleting library copy does not delete original external source image;
- project-embedded custom texture can be added to library;
- offline restart still shows library.

---

## 18. UI tests

At 1600×900 compare to main mockup:
- top menu present;
- workspace tabs correct;
- Browser ≈250 px;
- Properties ≈320 px;
- viewport dominant;
- bottom status visible;
- view cube top-right of viewport;
- model stats visible without blocking model.

At 1280×720:
- essential commands reachable;
- panel scrolling works;
- viewport not covered by Properties content;
- no clipped Save/Export access.

DPI tests:
- 100%
- 125%
- 150%
- 175%
- 200%

Theme tests:
- System initial behavior;
- Light;
- Dark;
- live theme switch;
- persistence after restart.

---

## 19. Keyboard/accessibility tests

Verify minimum shortcuts from product requirements.

Verify:
- focus indicator visible;
- icon-only commands have tooltips/accessible names;
- Tab navigation does not trap user;
- Esc exits active transient tool/dialog appropriately;
- disabled controls cannot be invoked via keyboard;
- color is not sole indicator for critical warning/error state.

---

## 20. Offline tests

With network adapter disabled before launch:
- application starts;
- no fatal resource errors;
- WebView2 fixed runtime starts;
- STL/OBJ/3MF/STEP import paths work using local dependencies;
- built-in textures load;
- custom textures load;
- 3D preview works;
- project save/load works;
- STL/3MF export works;
- Texture Library works;
- autosave/recovery works.

Expected network-dependent actions:
- Check for Updates reports offline/non-blocking state;
- external documentation/source links may fail normally because network is unavailable.

Any required CDN request is a release blocker.

---

## 21. Installer/uninstaller tests

Clean Windows VM:
- installer completes;
- Start menu shortcut launches;
- app works without developer tooling;
- `.bumpmesh` association registered;
- shared formats have Mesh Me360 Open With entry;
- existing default STL/3MF app is not replaced;
- uninstall removes app and owned registrations;
- user-created projects outside app data remain untouched.

Upgrade test:
- install older test build;
- create settings/recent/library/recovery state;
- upgrade;
- verify user state persists.

---

## 22. Updater tests

Use test channel/feed.

Verify:
- no update case;
- newer update detected;
- manual check bypasses time throttle;
- automatic check honors 24-hour throttle;
- update download progress visible;
- Install action updates application;
- Later action defers safely;
- unsaved work is not terminated without normal save confirmation;
- settings/recent/library/recovery preserved;
- offline update check is non-blocking.

---

## 23. Performance/stress tests

Use representative medium and large models.

Record:
- load time;
- preview responsiveness;
- subdivision time;
- displacement time;
- decimation time;
- export time;
- peak memory where practical.

The purpose is regression detection, not a hard universal performance SLA because hardware/model complexity varies.

Release blocker examples:
- UI freezes indefinitely while host synchronously reads model;
- memory leak grows materially after repeated open/close cycles;
- new desktop bridge makes file open dramatically slower due to binary-to-base64 conversion;
- viewport becomes unusable at model sizes the upstream baseline handles acceptably on the same machine.

---

## 24. Release sign-off checklist

Before v1.0 tag:
- `docs/FEATURE_MATRIX.md`: all required rows PASS;
- .NET tests PASS;
- web engine tests PASS;
- upstream parity regression PASS;
- clean offline install PASS;
- file association PASS;
- autosave/recovery PASS;
- updater test-channel PASS;
- light/dark/system themes PASS;
- 1280×720 and 1600×900 UI PASS;
- high-DPI PASS;
- license/source/third-party notices reviewed;
- no required CDN/network calls remain;
- no Autodesk proprietary assets included.
