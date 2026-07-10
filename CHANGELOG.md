# Changelog

## 1.1.0 — 2026-07-10

Complete rewrite of QuickFury around measured, fail-closed VRCFury optimizations.
The previous patch set has been replaced rather than carried forward.

- Add exact action and optional internal-method profiling for every VRCFury bake.
- Replace repeated Armature Link constraint, PhysBone, skin, and destruction scans with
  short-lived indexed lookups.
- Replace repeated controller-layer searches with a mutation-aware layer index.
- Preserve chronological animation path rewrite semantics with an ordered prefix resolver.
- Keep generated-asset creation inside VRCFury's existing asset-editing batch on Unity 2022,
  reducing the measured SaveAssets phase by 58.7%.
- Fail closed for behavior-changing patches unless VRCFury is exactly 1.1348.0 and every
  expected internal signature matches.
- Add deterministic and 200,000-query randomized differential coverage for ordered path
  resolution, plus documented structural parity measurements on the development avatar.

On the measured avatar, the complete warm VRCFury bake fell from 98.542 seconds to
23.810 seconds (75.8%). Results depend on avatar structure and Editor state.

## 1.0.0 — 2026-07-04

First public release (functionally identical to 0.5.1; adds licensing and packaging for
publication).

- 13 optimization patches, a per-pass build profiler, and a "hot internals" sub-profiler.
- Measured on a heavy test avatar (40 ArmatureLinks / ~4,700 merged bones / 181 FX layers):
  main build 95.7 s → 24.6 s (3.9×), total including the parameter compressor ~102 s → ~31 s.

### Pre-release history

- **0.5.1** — added one-click `Profiler Only (Disable All Optimizations)` and
  `Enable All Optimizations` menu modes.
- **0.5.0** — Batched Asset Saving patch (Unity < 6 only): keeps VRCFury's save phase inside the
  asset-editing batch and batches the parameter compressor's second save.
- **0.4.1** — Layer Map Cache redesigned after the 0.4.0 crash: scoped to `OptimizeLayer`, fresh
  map per entry, self-healing rebuild-on-miss before answering or throwing.
- **0.4.0** — Constraints Scan Cache and Layer Map Cache. *Known bad:* a mid-pass layer insertion
  through the `ControllerManager.NewLayer` override bypassed cache invalidation and could fail
  builds with "Layer not found in controller". Fixed in 0.4.1.
- **0.3.0** — Destroy Scan Cache.
- **0.2.0** — Skip Debug Info In Play Mode.
- **0.1.0** — initial build: profiler, sub-profiler, binding validation cache, PhysBone scan
  cache, clip settings cache, fast VFGameObject hashing, param name fast path, move + path
  rewrite fast path, skin rewrite cache, progress window throttle.
