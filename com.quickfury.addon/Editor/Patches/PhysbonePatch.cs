using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace QuickFury {
    /**
     * VF.Utils.PhysboneUtils.RemoveFromPhysbones(obj, force: true) is called twice per merged bone
     * during ArmatureLink (once from GameObjects.Create, once from ObjectMoveService.Move), and each
     * call does a full-avatar GetComponentsInChildren&lt;VRCPhysBone&gt; scan. On a 200-bone outfit that's
     * ~400 whole-hierarchy scans — the single largest ArmatureLink cost.
     *
     * This replaces only the force==true path with the identical logic running against a physbone
     * list cached per build action. The force==false path (which also scans skinned meshes and
     * constraints) is rare and falls through to the original.
     *
     * Cache safety: physbones are neither created nor destroyed inside a single action's move loop,
     * and the cache dies at the next action boundary. Destroyed components are skipped defensively.
     */
    internal static class PhysbonePatch {
        private struct Entry {
            public VRCPhysBone pb;
            public Transform root;
        }

        private static int cacheVersion = -1;
        private static int cacheKey;
        private static readonly List<Entry> cache = new List<Entry>();

        public static void Apply(Harmony h) {
            var t = QfReflect.ReqType("VF.Utils.PhysboneUtils");
            // Resolve VFGameObject plumbing now so a mismatch fails at patch time, not mid-build.
            QfReflect.WarmGo();
            QfReflect.ReqField(QfReflect.VfGoType, "getUploadRoots");
            h.Patch(QfReflect.ReqMethod(t, "RemoveFromPhysbones"),
                prefix: new HarmonyMethod(typeof(PhysbonePatch), nameof(Prefix)));
        }

        private static bool Prefix(object obj, bool force) {
            if (!QfSettings.PhysboneCache || !QfState.InBuild || !force) return true;
            var go = QfReflect.Go(obj);
            if (go == null) return true;

            object[] roots;
            try {
                roots = QfReflect.UploadRoots(obj);
            } catch {
                return true; // let the original produce whatever error it would have
            }

            var key = 17;
            foreach (var root in roots) {
                var rootGo = QfReflect.Go(root);
                key = key * 31 + (ReferenceEquals(rootGo, null) ? 0 : rootGo.GetInstanceID());
            }

            if (cacheVersion != QfState.scopeVersion || cacheKey != key) {
                cache.Clear();
                foreach (var root in roots) {
                    var rootGo = QfReflect.Go(root);
                    if (rootGo == null) continue;
                    foreach (var pb in rootGo.GetComponentsInChildren<VRCPhysBone>(true)) {
                        if (pb == null) continue;
                        cache.Add(new Entry { pb = pb, root = pb.GetRootTransform() });
                    }
                }
                cacheVersion = QfState.scopeVersion;
                cacheKey = key;
            }

            // Identical logic to the original force==true body, minus the rescan.
            var t = go.transform;
            foreach (var entry in cache) {
                var pb = entry.pb;
                var root = entry.root;
                if (pb == null || root == null) continue;
                if (t.gameObject != root.gameObject && t.IsChildOf(root)) {
                    var alreadyExcluded = false;
                    var ignore = pb.ignoreTransforms;
                    for (var i = 0; i < ignore.Count; i++) {
                        var other = ignore[i];
                        if (other != null && t.IsChildOf(other)) {
                            alreadyExcluded = true;
                            break;
                        }
                    }
                    if (!alreadyExcluded) {
                        ignore.Add(t);
                    }
                }
            }
            return false;
        }
    }
}
