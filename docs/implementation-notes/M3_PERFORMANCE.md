# M3 native stress evidence — 2026-09-07

These are deterministic dense 20 mm box fixtures, not a universal speed SLA.
The actual WPF/WebView2 app imports binary STL through the native stream bridge.
The guarded displacement/export worker runs while the camera orbits and the renderer draws.
A second run uses the preserved upstream worker on identical input; all positions and normals match exactly.

| Input triangles | Native import (s) | Guarded pipeline (s) | Upstream pipeline (s) | Result triangles | STL serialization (ms) | Rendered frames during guarded run |
|---:|---:|---:|---:|---:|---:|---:|
| 97,200 | 0.38 | 0.84 | 0.84 | 16,638 | 2.4 | 49 |
| 995,328 | 3.70 | 8.35 | 7.82 | 167,134 | 22.6 | 498 |
| 2,408,448 | 9.41 | 20.70 | 19.91 | 403,390 | 49.2 | 1,228 |

Pipeline timings include worker setup, input copy, subdivision checking, displacement and decimation.
Resolution is 1 mm, amplitude 0.1 mm, regularization off; maxTriangles is 80% of input.
Original flat-face harvesting can reduce below that target. Subdivision inspects existing fine triangles;
this dense fixture does not require triangle growth. Separate resource-guard regressions exercise growth/refusal.

Finite positions, triangle packing, unchanged authored buffers/revision, binary STL length,
rendered frames and exact upstream parity are asserted. The observed main-page JavaScript heap peaked
at approximately 2.50 GB during the largest case. This is a sampled main-page heap metric, not total
process/GPU/worker peak memory. The slowest event-loop interval was approximately 87 ms.

Limitations: these boxes do not establish all complex-model performance, per-monitor DPI,
whole-process leak freedom, or physical GPU acceptance. Stage progress timestamps are retained
in the raw evidence; comparisons are sequential on the same machine rather than statistically repeated.

Evidence: `artifacts/m2-integration-3bcb0d4bf04d4a1cb1d3c943e2d50fae/stress-results.json`.
Reproduce with the desktop test runner `--stress`; the full locked suite includes it.
