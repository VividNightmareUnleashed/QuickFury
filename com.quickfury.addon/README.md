# QuickFury

QuickFury is an Editor-only, bolt-on performance layer for an existing VRCFury installation. It profiles VRCFury's bake and can replace a few measured hot paths with indexed implementations. It does not ship, fork, or modify VRCFury.

QuickFury 1.2 is tested against VRCFury 1.1348.0. Its initial rewrite benchmark reduced a warm VRCFury bake from 98.54 seconds to 23.81 seconds. Version 1.2 reached a best clean measurement of 13.996 seconds, 56.2% below a 31.947-second same-session control. Read [PERFORMANCE.md](PERFORMANCE.md) for the measurements and validation boundary.

## Requirements and compatibility

- Unity 2022.3
- VRChat Avatars SDK 3.10.3 or newer
- VRCFury installed separately
- Behavior-changing optimizations: **VRCFury 1.1348.0 exactly**

QuickFury discovers VRCFury's internal Editor methods at load time. Profiling remains available when its profiling signatures match, but optimization menus are disabled unless the installed VRCFury version is exactly `1.1348.0`. Each optimizer also checks its own target signatures and stays disabled if they differ. This is deliberately fail-closed because VRCFury does not expose a public extension API for these bake internals.

The package's VPM dependency is a minimum needed for installation resolution; it does not mean later VRCFury versions are optimization-compatible.

## Install

1. Install VRCFury normally and confirm the avatar builds without QuickFury.
2. In Unity, choose **Window > Package Manager**, use **+ > Add package from disk**, and select this package's `package.json`.
3. Wait for the Editor to recompile. The Console should report `[QuickFury] Ready for VRCFury 1.1348.0`.

For a local file dependency, the equivalent `Packages/manifest.json` entry is:

```json
"com.quickfury.addon": "file:C:/path/to/QuickFury/com.quickfury.addon"
```

Keep QuickFury as its own package. Do not copy files into the VRCFury package, which would make upgrades and rollback harder.

## Use

All controls are under **Tools > QuickFury**. Settings are stored in Unity `EditorPrefs`, so they apply to the current Editor user rather than being serialized into the avatar.

The optimization toggles are:

- **Armature constraint index**: indexes avatar constraints once for the Armature Link phase instead of repeatedly scanning the hierarchy.
- **Armature PhysBone index**: snapshots PhysBones once for Armature Link's repeated removal/update queries.
- **Armature skin index**: maps bones to affected skinned meshes so skin rewriting does not scan every renderer for every bone.
- **Armature destroy index**: snapshots dynamics-component sequences at the first prune instead of rescanning the avatar for every deleted wrapper.
- **Layer-to-tree layer index**: replaces repeated controller-layer searches with a short-lived state-machine-to-index map.
- **Ordered animation path rewrite**: resolves deferred path moves with an ordered prefix index while preserving chronological rewrite semantics.
- **Skip empty deferred rewrite**: avoids clip traversal when there are no deferred moves.
- **Retain SaveAssets batching (Unity 2022)**: keeps VRCFury's outer `AssetDatabase.StartAssetEditing` batch active while generated assets are created. VRCFury exits that batch for a Unity 6 workaround; QuickFury restores batching only on Unity 2022.
- **Skip Armature Link debug components**: avoids creating Editor-only debug records during normal optimized bakes.
- **Fast Armature Link moves**: applies the large, already-validated Armature Link move set without repeated wrapper bookkeeping.
- **Fast generated-asset discovery** and **Fast controller asset graph**: replace repeated reflective graph walks with scoped, identity-aware traversal.
- **Consolidate generated asset files**: stores the generated controller graph in a small fixed set of asset containers instead of repeatedly creating separate files.
- **Cache blendshape controller bindings**: reuses equivalent blendshape binding discovery within the bake.
- **Skip covered SPS mesh probes** and **Cache DPS-TPS material probes**: avoid repeated SPS renderer and material inspection.
- **Controller parameter index**: replaces repeated linear parameter-name searches with a mutation-aware index.
- **Tracking behaviour index** and **Filter irrelevant behaviour containers**: narrow repeated state-machine behaviour work while preserving generated tracking-driver output.
- **Deduplicate generated animation clips**: merges only exact, self-originating generated clips before finalization.
- **Skip inert Transform asset scans** and **Skip duplicate renderer asset scan**: conservative SaveAssets experiments. They reduced scan counts but did not materially improve SaveAssets time on the measured avatar, so they default off.

The measured, parity-checked optimizations default on for a fresh install. Use **Tools > QuickFury > Use recommended settings** to restore that set, or **Disable all optimizations** for an immediate VRCFury control run. Individual toggles remain available for diagnosis and rollback.

### Profiling

QuickFury always records total bake time and exact VRCFury action durations when compatible profiling targets are present. Enable **Tools > QuickFury > Profiling > Detailed internal timings** for method-level inclusive/self time and call counts. After a bake, use **Log last report** to print the most recent report again.

The public `QuickFury.QuickFuryProfilerApi.LastReport` property exposes the current in-memory report. QuickFury also stores it in the Editor session under `QuickFury.LastProfile`.

Detailed timings add some overhead, so compare runs with the same profiling setting. The aggregate timer is usually more practical than recording every Unity Profiler sample, which can generate very large captures during a long avatar bake.

## Safety and rollback

QuickFury patches Editor methods at assembly load and removes only patches registered under its own Harmony ID before reload. It never changes VRCFury package files. To roll back, disable the toggles, remove the QuickFury package dependency, and let Unity recompile.

**QuickFury is an unofficial third-party addon. It is not supported, endorsed, or maintained by VRCFury, and no guarantee is made that it will work correctly for every avatar, project, or future release. Do not report a problem to VRCFury while QuickFury is installed. Remove QuickFury completely, let Unity recompile, and reproduce the issue with stock VRCFury first. Problems that occur only with QuickFury installed belong in the QuickFury issue tracker. QuickFury is provided without warranty and is used at your own risk.**

Treat any VRCFury upgrade as unsupported until QuickFury is re-profiled and revalidated against that exact release. Unknown versions retain profiling but fail closed for every behavior-changing patch. The package has also been checked structurally, but avatar-specific visual and behavioral smoke tests remain prudent.
