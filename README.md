# QuickFury

QuickFury is an Editor-only, bolt-on performance layer for an existing VRCFury installation.
It profiles VRCFury avatar bakes and replaces measured hot paths with indexed implementations.
It does not ship, fork, or modify VRCFury.

Version 1.1 is a complete rewrite. On the development avatar, it reduced a warm VRCFury bake
from **98.542 seconds to 23.810 seconds (75.8%)**. Armature Link fell from 61.762 seconds to
6.467 seconds, and SaveAssets fell from 14.554 seconds to 6.365 seconds. These are measurements
from one large avatar and one machine, not universal performance claims.

## Compatibility

- Unity 2022.3
- VRChat Avatars SDK 3.10.3 or newer
- VRCFury installed separately
- Behavior-changing optimizations: **VRCFury 1.1348.0 exactly**

QuickFury resolves VRCFury's internal Editor methods at load time. Profiling remains available
when its signatures match, but all behavior-changing patches fail closed on unknown VRCFury
versions or unexpected method signatures.

## Install

Install VRCFury normally first. Then either import the `QuickFury-1.1.0.unitypackage` from the
release, or add `com.quickfury.addon/package.json` through Unity's Package Manager. A local
manifest entry looks like this:

```json
"com.quickfury.addon": "file:C:/path/to/QuickFury/com.quickfury.addon"
```

After compilation, the Console should report:

```text
[QuickFury] Ready for VRCFury 1.1348.0 (...).
```

Controls and profiling reports are under **Tools > QuickFury**. Removing the QuickFury package
and recompiling restores stock VRCFury behavior.

## What the rewrite optimizes

- Armature Link constraint, PhysBone, skin, and destruction queries
- Controller layer-to-tree lookups
- Ordered deferred animation-path rewriting
- Empty deferred-rewrite passes
- Generated-asset saving through Unity 2022 asset-editing batching

The package includes exact per-action timing on every bake and optional detailed internal
timings. See [the package README](com.quickfury.addon/README.md) for controls and safety details,
and [the performance report](com.quickfury.addon/PERFORMANCE.md) for methodology and validation.

## Repository layout

- `com.quickfury.addon/` — the installable Unity/VPM package
- `build-unitypackage.ps1` — deterministic `.unitypackage` builder
- `CHANGELOG.md` — release history

## License

MIT — see [LICENSE.md](LICENSE.md). QuickFury is independent of and is not endorsed by VRCFury;
see the package [NOTICE.md](com.quickfury.addon/NOTICE.md).
