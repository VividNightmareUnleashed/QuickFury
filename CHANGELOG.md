# Changelog

## 1.2.1 — 2026-07-11

Simplify the 1.2 Editor implementation without changing its public behavior or compatibility
boundary.

- Centralize reflection method selection and inner-exception propagation shared by optimizers.
- Consolidate optimization defaults, menu paths, toggle handlers, and validation callbacks.
- Remove redundant lifecycle state from scoped Armature Link, behaviour, and asset patches.
- Avoid repeated profiling preference reads, controller-graph iterator allocations, clip-settings
  reflection, and blendshape cache-key allocations in measured hot paths.
- Install detailed-profiling method patches only while the profiling toggle is enabled, and
  isolate per-patch installation so one failed target cannot disable the remaining patches.
- Refresh SPS material dependency hashes at the start of each bake and narrow the Armature Link
  debug-component suppression to the single upload-state read that controls it.
- Use object-typed Harmony patch methods for the layer behaviour-container getter: Harmony's
  token-based shared state cannot represent closed generic patch methods, which corrupted the
  getter's patch list on any second patch or on unpatching during assembly reload.
- Preserve the existing fail-closed, per-optimizer target checks and rollback controls.

## 1.2.0 — 2026-07-10

Extend the 1.1 rewrite into the remaining measured VRCFury bake hot paths.

- Apply large Armature Link move sets directly and skip optional Editor-only debug records.
- Replace repeated generated-asset and controller-graph traversal with scoped identity indexes.
- Consolidate generated controller assets into a fixed pair of files instead of repeatedly
  creating separate asset files.
- Index controller parameters and tracking behaviours, and exclude irrelevant behaviour
  containers from repeated conflict passes.
- Cache repeated blendshape bindings and SPS renderer/material probes.
- Deduplicate only exact self-originating generated animation clips before finalization; all 21
  replacements matched after same-bake shadow finalization.
- Preserve fail-closed compatibility checks and individual rollback toggles for every new path.

The best clean bake on the measured avatar fell from the earlier 1.1 result of 23.810 seconds
to 13.996 seconds (41.2% faster). Against the 31.947-second control captured at the start of
the same profiling session, 1.2 was 56.2% faster. Armature Link fell from 7.901 to 1.255
seconds, and SaveAssets fell from 7.008 to 2.284 seconds.

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
