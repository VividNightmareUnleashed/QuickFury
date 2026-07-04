# QuickFury

Bolt-on build performance addon for [VRCFury](https://vrcfury.com). Profiles and speeds up VRCFury
avatar bakes (play mode, test copies, and uploads) **without modifying or forking VRCFury** — it
hooks VRCFury's build pipeline at runtime using Harmony, the same mechanism VRCFury itself uses to
patch Unity and the VRCSDK.

Built for **Unity 2022.3** with the VRChat Avatars SDK and any recent VRCFury installed.

## Install

Either:

- **Import `QuickFury-<version>.unitypackage`** (Assets → Import Package → Custom Package, or
  drag it into the editor). It installs into `Packages/com.quickfury.addon`.
- **Or copy the `com.quickfury.addon` folder** into your avatar project's `Packages/` folder
  (next to `com.vrcfury.vrcfury`).

Either way, on the next script reload you'll see
`[QuickFury] active — 15/15 patches applied.` in the console.

To uninstall, delete the folder. QuickFury never modifies VRCFury's files or your assets; every
change it makes lives only in memory for the duration of the editor session.

## What you get

### Profiler (the part to look at first)

VRCFury has no timing instrumentation at all. With QuickFury installed, every bake prints:

- a **build report**: total main-build time plus the top ~40 slowest passes, aggregated
  (`ArmatureLinkService.Apply`, `FixWriteDefaultsService...`, etc.)
- a **preprocessor hook summary**: how long each VRCSDK-level hook took, including VRCFury's
  main build and its parameter compressor / second save pass
- an immediate log line for any single hook that takes over 100ms

Use the report to see whether the optimization patches below actually help on *your* avatar, and
where the remaining time goes.

### Optimization patches

All are enabled by default, all take effect only while a VRCFury build is running, and each can be
toggled individually under **Tools → QuickFury → Patches** (no restart needed):

| Patch | What it fixes |
| --- | --- |
| **Binding Validation Cache** | `ValidateBindingsService.IsValid` does a native scene lookup per animation binding and is called per-binding by at least four passes (FullController merge, LayerToTree, CleanupEmptyLayers, FixWriteDefaults). Memoized per build pass. |
| **PhysBone Scan Cache** | Every bone merged by ArmatureLink triggers two full-avatar PhysBone scans. Cached per build pass — the single largest ArmatureLink cost. |
| **Clip Settings Cache** | Controller traversals call native `GetAnimationClipSettings` for every clip, dozens of times per build. Cached per build pass. |
| **Fast VFGameObject Hashing** | VRCFury's `VFGameObject.GetHashCode` allocates a Tuple on every hash operation (tens of thousands per build). Replaced with the identical allocation-free computation. |
| **Param Name Fast Path** | Unique-parameter-name generation re-marshals every controller's full parameter array per lookup (O(n²) on FullController-heavy avatars). Replaced with a per-pass name snapshot. |
| **Move + Path Rewrite Fast Path** | `ObjectMoveService.Move` rebuilds a ~500-entry "immovable bones" set per moved object; the deferred path rewriter allocates a string per (move × binding) comparison. Both cached/hoisted. |
| **Skin Rewrite Cache** | ArmatureLink's bone-reuse mode rescans every SkinnedMeshRenderer on the avatar and re-marshals full bone arrays for every merged bone. Cached per build pass. |
| **Progress Window Throttle** | VRCFury logs + force-repaints its progress window for every single build action (hundreds of synchronous repaints). Rate-limited to 20/sec. |
| **Skip Debug Info In Play Mode** | ⚠ *Play-mode behavior change.* In non-upload builds, ArmatureLink attaches a `VRCFuryDebugInfo` component to every merged bone (thousands of editor `AddComponent` calls + path-string building), which also slows every later component scan. This makes play-mode bakes take the same fast path uploads already take. Toggle it off when you actually want to inspect ArmatureLink's per-bone decisions. |
| **Sub-profiler** | Adds a "Hot internals" section to the build report timing known-hot VRCFury internals (ArmatureLink phases, object moves, clip write-back, save calls, binding validation). Timings are inclusive and may overlap. |
| **Destroy Scan Cache** | `VFGameObject.Destroy` does four full-avatar component scans per call to clean up dynamics; ArmatureLink's prune loop destroys hundreds of objects per bake. The three typed dynamics scans are cached per build pass. |
| **Constraints Scan Cache** | `VFGameObject.GetConstraints` scans every component on the avatar per call, and ArmatureLink calls it once per merged bone and once per pruned object (measured: 21.5s / 9,354 calls). The scan is cached per build pass, scoped to ArmatureLink where constraints are only ever destroyed, never created. |
| **Layer Map Cache** | `VFLayer.Exists`/`GetLayerId` marshal the controller's entire layer array per call; LayerToTree's cross-layer check calls them per layer-pair (~97k marshals on a 181-layer FX). Cached per controller, scoped to the LayerToTree pass, invalidated on layer add/remove — and self-healing: any lookup miss re-reads the live layer list before answering. |
| **Batched Asset Saving** | ⚠ *Unity <6 only.* VRCFury's save phase exits its asset-editing batch (a Unity 6 workaround), paying one synchronous import per asset (measured: 9.6s / 84 assets). This keeps the save inside the batch on Unity 2022, restoring VRCFury's own pre-Unity-6 behavior, and also batches the parameter compressor's second save. If a bake ever errors during "SaveAssets", toggle this off first. |

### Safety design

- **No fork, no file edits.** Everything is a runtime Harmony patch against a stock VRCFury.
- **Fail-open.** Every VRCFury type/member is resolved by name when patches are applied. If your
  VRCFury version renamed or changed something, that patch is *skipped* (with a console warning)
  and VRCFury behaves exactly as stock for that code path. `Tools → QuickFury → Print Status`
  shows what's applied.
- **Cache scoping.** All caches are invalidated at every build-pass boundary and at every
  VRCSDK preprocessor hook boundary. VRCFury mutates the avatar *between* passes, so a cache can't
  go stale while the pass using it runs. Nothing survives past a build.
- **Behavior parity.** The replacement code paths reproduce stock logic exactly, with two
  deliberate, tiny deviations:
  1. Deferred animation-path prefix matching uses ordinal string comparison (stock mixes
     culture-sensitive `StartsWith` with ordinal `==`; ordinal matches how Unity itself treats
     animation paths).
  2. The unique-param-name fast path could miss a name collision if some *other* code path adds a
     parameter literally named like a VRCFury internal one (`VF<number>_...`) in the middle of the
     same build pass. VRCFury's own generated names all flow through the patched method, so this
     requires a user parameter deliberately named like a VRCFury temporary.

## If something looks wrong

1. Toggle patches off one at a time under **Tools → QuickFury → Patches** and rebuild — they take
   effect immediately, so you can bisect in a couple of minutes.
2. **Tools → QuickFury → Enabled** (off) removes every patch instantly and completely.
3. Compare a bake with QuickFury disabled; if a difference persists with QuickFury disabled, it's
   not QuickFury.

## Menu reference (Tools → QuickFury)

- **Enabled** — master switch; unpatches/repatches live.
- **Profiler Report** — build report + hook summary logging.
- **Patches/...** — individual optimization toggles (runtime-checked, instant).
- **Profiler Only (Disable All Optimizations)** — one click to measure stock VRCFury speed with
  timing reports still on.
- **Enable All Optimizations** — one click back to full speed.
- **Print Status** — logs which patches are applied vs skipped and why.

## License & credits

QuickFury is by **Vivid Nightmare**, released under the **MIT license** (see `LICENSE.md`) — reuse
the code however you like, but keep the license notice (i.e. credit Vivid Nightmare).

QuickFury is an independent project, not affiliated with VRCFury. It contains and redistributes
no VRCFury code or assets (see `NOTICE.md`); VRCFury is (c) Senky, https://vrcfury.com.
