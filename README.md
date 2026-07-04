# QuickFury

Bolt-on build performance addon for [VRCFury](https://vrcfury.com). QuickFury profiles and speeds
up VRCFury avatar bakes (play mode, test copies, and uploads) **without modifying or forking
VRCFury** — it hooks VRCFury's build pipeline at runtime using Harmony, the same mechanism VRCFury
itself uses to patch Unity and the VRCSDK. Delete the folder and everything is back to stock.

Built for **Unity 2022.3** with the VRChat Avatars SDK and any recent VRCFury.

## Results

Measured on a heavy production avatar (40 ArmatureLink components, ~4,700 merged bones, 181 FX
layers, 84 generated assets), Unity 2022.3.22f1, VRCFury 1.1348.0, play-mode bake:

| | Stock VRCFury | With QuickFury | Speedup |
| --- | --- | --- | --- |
| Main build | 95.7 s | 24.6 s | **3.9×** |
| ArmatureLink | 60.4 s | 5.3 s | **11×** |
| LayerToTree optimization | 11.1 s | 0.7 s | **17×** |
| Total incl. parameter compressor | ~102 s | ~31 s | **3.3×** |

Your numbers will vary with avatar structure — which is why QuickFury also ships a **build
profiler**: every bake prints a per-pass timing report, so you can see exactly where your bake
spends its time and whether each patch helps.

## Install

Grab the latest `QuickFury-<version>.unitypackage` from the releases page and import it
(Assets → Import Package → Custom Package), or copy `com.quickfury.addon/` from this repo into
your project's `Packages/` folder. On the next script reload the console shows:

```
[QuickFury] active — 15/15 patches applied.
```

To uninstall, delete `Packages/com.quickfury.addon`. QuickFury never touches VRCFury's files or
your assets; every change lives only in memory for the duration of the editor session.

Full documentation — what each of the 15 patches does, the safety design, per-patch toggles, and
troubleshooting — is in the package readme: [com.quickfury.addon/README.md](com.quickfury.addon/README.md).

## How it works

- **Runtime Harmony patches only.** No fork, no file edits, no compile-time dependency on
  VRCFury. Every VRCFury type and member is resolved by name when patches are applied.
- **Fail-open.** If your VRCFury version renamed or changed something a patch needs, that patch is
  skipped with a console warning and VRCFury behaves exactly as stock for that code path.
- **Scoped caches.** Most of the speedup comes from caching full-avatar scans that VRCFury repeats
  per bone / per layer / per binding. Every cache is invalidated at every build-pass boundary, so
  nothing QuickFury caches can outlive the pass that produced it — and the hottest caches are
  additionally self-healing (a lookup miss re-reads live state before answering).

## Repo layout

- `com.quickfury.addon/` — the addon package (this is all that ships).
- `build-unitypackage.ps1` — builds `QuickFury-<version>.unitypackage` from the package folder
  with deterministic GUIDs.

## License

MIT — see [LICENSE.md](LICENSE.md). In short: use it, modify it, ship it, but **keep the license
notice — i.e. credit Vivid Nightmare — when reusing the code.**

QuickFury is an independent project, not affiliated with or endorsed by VRCFury. It does not
include or redistribute any part of VRCFury; see [NOTICE.md](com.quickfury.addon/NOTICE.md).
Huge credit to Senky and the VRCFury contributors for VRCFury itself.
