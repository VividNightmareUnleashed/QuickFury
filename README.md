# QuickFury

QuickFury is an Editor-only, bolt-on performance layer for an existing VRCFury installation.
It profiles VRCFury avatar bakes and replaces measured hot paths with indexed implementations.
It does not ship, fork, or modify VRCFury.

Version 1.1 was a complete rewrite. Its initial benchmark reduced a warm VRCFury bake from
**98.542 seconds to 23.810 seconds (75.8%)**. The subsequent 1.2 optimization pass reached a
best clean measurement of **13.996 seconds**.

| Component | Base VRCFury | QuickFury 1.1 | QuickFury 1.2 |
| --- | ---: | ---: | ---: |
| Full VRCFury bake | 98.542 s (1.00x) | 23.810 s (**4.14x faster**) | **13.996 s (7.04x faster)** |
| Armature Link | 61.762 s (1.00x) | 6.467 s (**9.55x faster**) | **1.255 s (49.21x faster)** |
| Controller layer lookup | 11.844 s (1.00x) | 0.572 s (**20.71x faster**) | 0.572 s* (**20.71x faster**) |
| Deferred animation-path rewrite | ~2.270 s (1.00x) | ~0.140 s (**16.21x faster**) | ~0.140 s* (**16.21x faster**) |
| PhysBone removal | 6.228 s (1.00x) | — | 0.120–0.170 s (**36.64–51.90x faster**) |
| SaveAssets | 14.554 s (1.00x) | 6.365 s (**2.29x faster**) | **2.284 s (6.37x faster)** |

Speedups in the table are relative to the original Base VRCFury measurement. The 1.2 column was
captured in a later profiling session; its same-session control was 31.947 seconds overall,
7.901 seconds for Armature Link, and 7.008 seconds for SaveAssets. These measurements come from
one large avatar and one machine, not universal performance claims. Asterisks mark unchanged
1.1 optimizations whose isolated measurement was carried forward; components without a
meaningful measured reduction are omitted.

### Benchmark avatar specifications

The benchmark avatar is an intentionally extreme, worst-case stress test. The current loaded
source avatar contains inactive outfits and build-time content because VRCFury still has to
inspect and transform that data during a bake.

| Source-avatar metric | Count |
| --- | ---: |
| Hierarchy objects | 8,955 |
| Components | 9,497 |
| Rendered mesh instances | 54 (38 skinned + 16 static) |
| Unique meshes | 42 |
| Rendered geometry | 232,504 vertices / 359,523 triangles |
| Blendshape channels | 767 |
| Distinct transforms referenced as skin bones | 8,484 |
| Materials | 44 unique / 73 renderer slots |
| Source animation setup | 76 clips / 3 controllers / 33 layers / 73 states |
| Controller parameters | 78 |
| VRCFury content | 89 features + 13 haptic sockets |
| Major VRCFury features | 37 toggles / 32 Armature Links / 5 full-controller imports |
| PhysBones | 81 PhysBones / 60 colliders |
| Contacts | 7 senders / 40 receivers |
| Constraints | 36 |

These are live source-scene counts from the current benchmark avatar. The avatar evolved during
development, so an older historical run may differ slightly from this snapshot.

### Scope and diminishing returns

This avatar represents a worst-case workload; a more typical avatar should generally complete
its bake faster in absolute terms, although the relative speedup depends on which VRCFury
features it uses. Eventually the limiting factor also stops being VRCFury's managed code and
becomes the machine, Unity's native asset and serialization work, or other Editor systems.

I was satisfied with the current speedup-to-work ratio after capturing the major low-hanging
bottlenecks. Further speedups are probably possible, but they increasingly require replacing
larger portions of VRCFury's underlying systems and working around—or effectively substituting
for—more Unity-side behavior. At that point substantially more engineering work produces
progressively smaller gains. Further updates will probably focus on cleaning and slimming down
the code.

## Support and warranty

QuickFury is an unofficial third-party addon. It is not supported, endorsed, or maintained by
VRCFury, and it modifies VRCFury's Editor-time behavior through internal patches. Issues may
occur, and the project provides no guarantee that every avatar, Unity project, or future
VRCFury release will work correctly.

Do not report a problem to the VRCFury project while QuickFury is installed. First remove
QuickFury completely, let Unity recompile, and reproduce the problem using stock VRCFury. If
the problem occurs only with QuickFury installed, it is a QuickFury issue and should be reported
to this project instead. QuickFury is provided without warranty; use it at your own risk.

## Compatibility

- Unity 2022.3
- VRChat Avatars SDK 3.10.3 or newer
- VRCFury installed separately
- Behavior-changing optimizations: **VRCFury 1.1363.0 exactly**

QuickFury resolves VRCFury's internal Editor methods at load time. Profiling remains available
when its signatures match, but all behavior-changing patches fail closed on unknown VRCFury
versions or unexpected method signatures.

## Install

Install VRCFury normally first. Then either import the `QuickFury-1.2.4.unitypackage` from the
release, or add `com.quickfury.addon/package.json` through Unity's Package Manager. A local
manifest entry looks like this:

```json
"com.quickfury.addon": "file:C:/path/to/QuickFury/com.quickfury.addon"
```

After compilation, the Console should report:

```text
[QuickFury] Ready for VRCFury 1.1363.0 (...).
```

Controls and profiling reports are under **Tools > QuickFury**. Removing the QuickFury package
and recompiling restores stock VRCFury behavior.

## What the rewrite optimizes

- Armature Link constraint, PhysBone, skin, and destruction queries
- Controller layer-to-tree lookups
- Ordered deferred animation-path rewriting
- Empty deferred-rewrite passes
- Generated-asset saving through Unity 2022 asset-editing batching
- Armature Link object moves and optional debug-component creation
- Generated controller-asset discovery, traversal, and storage
- Controller parameter, tracking-behaviour, and behaviour-container lookups
- Blendshape binding and SPS material/renderer probes
- Exact deduplication of redundant generated animation clips

The package includes exact per-action timing on every bake and optional detailed internal
timings. See [the package README](com.quickfury.addon/README.md) for controls and safety details,
and [the performance report](com.quickfury.addon/PERFORMANCE.md) for methodology and validation.

## Repository layout

- `com.quickfury.addon/` — the installable Unity/VPM package
- `build-unitypackage.ps1` — deterministic `.unitypackage` builder
- `CHANGELOG.md` — release history

## License

QuickFury may be used, modified, forked, and redistributed for free with attribution, but it
may not itself be sold, paywalled, or used to monetize access. Reused code must also clearly
credit QuickFury. See
[LICENSE.md](LICENSE.md) for the complete terms. QuickFury is independent of and is not
endorsed by VRCFury; see the package
[NOTICE.md](com.quickfury.addon/NOTICE.md).
