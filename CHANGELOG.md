# Changelog

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
