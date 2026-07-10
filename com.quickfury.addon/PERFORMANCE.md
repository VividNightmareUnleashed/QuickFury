# Performance investigation

## Result

On the development avatar, the complete tested set reduced a warm VRCFury bake from **98.542 s to 23.810 s** (**75.8%**). Armature Link fell from **61.762 s to 6.467 s** (**89.5%**), layer-to-tree conversion fell from **11.844 s to 0.572 s** (**95.2%**), and SaveAssets fell from **14.554 s to 6.365 s** (**56.3%**).

These numbers describe one large avatar and one machine; they are evidence for the hot paths, not a universal speed claim.

## Method

- Unity 2022.3.22f1, VRCFury 1.1348.0
- Avatar root: `Goddess - Casual`
- VRCFury bake triggered by entering Play mode in the same loaded project
- Warm baseline used after the initial cold bake
- QuickFury's `Stopwatch` aggregation measured the complete VRCFury run and exact `FeatureBuilderAction.Call` durations
- Optimization variants were enabled cumulatively; all other test conditions were kept as stable as practical
- Detailed profiling was enabled for hot-path attribution

Editor caches, garbage collection, asset database work, and unrelated bake actions still introduce run-to-run variance. The direct Armature Link action timings are therefore more diagnostic than small differences between total times.

## Benchmark

| Cumulative configuration | Full bake | Armature Link | Layer-to-tree | SaveAssets |
| --- | ---: | ---: | ---: | ---: |
| Optimizers disabled (final control) | 98.542 s | 61.762 s | 11.844 s | 14.554 s |
| Constraint index | 67.621 s | 28.189 s | — | — |
| + PhysBone index | 59.366 s | 22.465 s | — | — |
| + skin index | 57.695 s | 15.436 s | — | — |
| + destroy index | 48.436 s | 8.389 s | 11.901 s | 14.605 s |
| + layer index | 34.045 s | 8.356 s | 0.582 s | 14.677 s |
| + ordered paths and SaveAssets scan pruning | **32.228 s** | **6.346 s** | **0.643 s** | **14.322 s** |
| + retain SaveAssets batching on Unity 2022 | **23.810 s** | **6.467 s** | **0.572 s** | **6.365 s** |

The unoptimized detailed run exposed the repeated whole-avatar queries behind the result:

| Hot path | Inclusive time | Calls |
| --- | ---: | ---: |
| `VFGameObject.GetConstraints` | 33.258 s | 9,354 |
| `VFGameObject.Destroy` | 21.604 s | 4,456 |
| `ArmatureLinkService.RewriteSkins` | 11.108 s | 4,747 |
| `PhysboneUtils.RemoveFromPhysbones` | 6.228 s | 11,514 |
| `ObjectMoveService.Move` | 3.084 s | 4,747 |
| `ObjectMoveService.ApplyDeferred` | 2.140 s | 1 |

The destroy index removes most of the remaining Armature prune scans. The layer index is especially effective because VRCFury's conflict loop repeatedly calls `VFLayer.Exists()` and `GetLayerId()`, and both linearly search the controller's layer array. The temporary index changes those lookups to O(1) while rebuilding after the same mutations VRCFury performs.

Detailed profiling split the original SaveAssets cost into about 10.5 seconds of native `AssetDatabase.RemoveObjectFromAsset` / `CreateAsset` work and about 4.5 seconds of serialized object-reference traversal. VRCFury's outer build already uses `AssetDatabase.StartAssetEditing`, but `SaveAssetsService` temporarily exits the batch for a Unity 6 asset-path workaround. Keeping that batch active only on Unity 2022 reduced a same-session warm SaveAssets run from 15.414 seconds to 6.365 seconds (58.7%) and the full VRCFury run from 33.190 seconds to 23.810 seconds (28.3%).

Skipping 3,401 inert Transform scans and 39 repeated Renderer scans reduced traversal counts, but their end-to-end change remained small relative to normal variance. Those two switches therefore remain experimental and default off. Serialized object-reference traversal is now the largest part of SaveAssets and the next research target.

## Validation status

**Status: structural parity passed on the loaded development avatar.**

- [x] QuickFury Editor assembly compiles in the live project.
- [x] Constraint-only, constraint + PhysBone, and constraint + PhysBone + skin builds completed VRCFury's final validation without reported errors.
- [x] An independent differential harness matched the ordered path resolver against the chronological reference implementation for 200,000 randomized queries.
- [x] Fully optimized and unoptimized outputs matched exactly for 3,401 hierarchy/component entries, 30 skin structures, 23,616 bindpose matrices (377,856 float values), and 83 PhysBones with every ordered ignore entry.
- [x] All 12,428 controller/state/motion tokens matched after normalizing VRCFury's deliberate per-run numeric IDs and treating binding/state enumeration order as unordered. Raw textual ordering differed only because randomized `[VF###]` paths were sorted before normalization.
- [x] The Unity-2022 SaveAssets batching run completed twice without VRCFury import errors. The baked avatar graph contained 18,734 reachable objects and 4,994 saved assets, with zero reachable non-scene assets missing an asset path.
- [ ] Perform avatar-specific visual and behavioral smoke tests before treating the optimizations as production-ready.

The package contains `Tests/Editor/OrderedPathResolverTests.cs`, including deterministic cases and seeded randomized comparisons. The live project's Unity Test Runner did not discover this package test assembly because the local package was not listed in the project's `testables`; do not interpret unrelated Test Runner results as QuickFury coverage. To run the included tests, add `com.quickfury.addon` to the host project's `Packages/manifest.json` `testables` array and run the Edit Mode assembly.

## Interpretation and caveats

- Successful structural validation is strong evidence, not a substitute for an avatar-specific visual and behavioral smoke test.
- QuickFury relies on reflected VRCFury internals and Harmony patches. It intentionally enables behavior changes only for the exact tested VRCFury release.
- The skin index duplicates VRCFury 1.1348.0's bind-pose update behavior for only affected skins; its complete bone ordering and all bindpose values matched the unoptimized control.
- The ordered path resolver has 200,000-query differential coverage and reduced deferred animation rewriting from roughly 2.27 seconds to 0.14 seconds on this avatar.
- SaveAssets batching is deliberately disabled outside Unity 2022 because VRCFury documents a Unity 6 asset-path failure while saving inside an asset-editing batch.
- A raw Unity Profiler capture from the initial investigation grew to 15.6 GiB and froze the Editor while loading. QuickFury's aggregate timers are the recommended measurement path for these long bakes.
