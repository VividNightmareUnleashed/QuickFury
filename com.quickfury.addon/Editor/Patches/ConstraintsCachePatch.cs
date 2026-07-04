using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace QuickFury {
    /**
     * VF.Utils.VFGameObject.GetConstraints scans EVERY component under the upload root and wraps
     * each in a VFConstraint, per call. Profiling showed 9,354 calls / 21.5s on a heavy avatar:
     * ArmatureLink calls it once per merged bone (stale-constraint deletion) and once per pruned
     * object (inside Destroy).
     *
     * The component scan + wrapper creation is cached per build action, scoped to ArmatureLink's
     * Apply only — the one window where we've verified constraints are only ever DESTROYED
     * (handled: destroyed components are skipped, matching a fresh scan), never created. The
     * affected-object filter still runs live per call, exactly like stock, in stock scan order.
     */
    internal static class ConstraintsCachePatch {
        private static Type vfConstraintType;
        private static MethodInfo createOrNullM;
        private static MethodInfo getAffectedObjectM;

        private static int armatureLinkDepth;

        private static int cacheVersion = -1;
        private static int cacheKey;
        private static readonly List<(Component raw, object wrapper)> cache =
            new List<(Component, object)>();

        public static void Apply(Harmony h) {
            var vfGo = QfReflect.VfGoType;
            QfReflect.WarmGo();
            QfReflect.ReqField(vfGo, "getUploadRoots");
            vfConstraintType = QfReflect.ReqType("VF.Utils.VFConstraint");
            createOrNullM = QfReflect.ReqMethod(vfConstraintType, "CreateOrNull");
            getAffectedObjectM = QfReflect.ReqMethod(vfConstraintType, "GetAffectedObject");

            var armatureLink = QfReflect.ReqType("VF.Service.ArmatureLinkService");
            h.Patch(QfReflect.ReqMethod(armatureLink, "Apply"),
                prefix: new HarmonyMethod(typeof(ConstraintsCachePatch), nameof(ScopePrefix)),
                finalizer: new HarmonyMethod(typeof(ConstraintsCachePatch), nameof(ScopeFinalizer)));

            h.Patch(QfReflect.ReqMethod(vfGo, "GetConstraints"),
                prefix: new HarmonyMethod(typeof(ConstraintsCachePatch), nameof(Prefix)));
        }

        private static void ScopePrefix() => armatureLinkDepth++;
        private static void ScopeFinalizer() => armatureLinkDepth--;

        private static bool Prefix(object __instance, bool includeParents, bool includeChildren, ref object __result) {
            if (!QfSettings.ConstraintsCache || !QfState.InBuild || armatureLinkDepth <= 0) return true;
            var go = QfReflect.Go(__instance);
            if (go == null) return true;

            object[] roots;
            try {
                roots = QfReflect.UploadRoots(__instance);
            } catch {
                return true;
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
                    // Same scan + wrap as stock, in stock order — just once per action instead of per call.
                    foreach (var c in rootGo.GetComponentsInChildren<Component>(true)) {
                        if (c == null) continue;
                        var wrapper = QfReflect.Invoke(createOrNullM, null, c);
                        if (wrapper != null) cache.Add((c, wrapper));
                    }
                }
                cacheVersion = QfState.scopeVersion;
                cacheKey = key;
            }

            var t = go.transform;
            var matches = new List<object>();
            foreach (var (raw, wrapper) in cache) {
                if (raw == null) continue; // destroyed since cache build — a fresh scan wouldn't return it
                var affectedGo = QfReflect.Go(QfReflect.Invoke(getAffectedObjectM, wrapper));
                if (affectedGo == null) continue;
                bool match;
                if (includeParents) match = t.IsChildOf(affectedGo.transform);
                else if (includeChildren) match = affectedGo.transform.IsChildOf(t);
                else match = affectedGo == go;
                if (match) matches.Add(wrapper);
            }

            var result = Array.CreateInstance(vfConstraintType, matches.Count);
            for (var i = 0; i < matches.Count; i++) result.SetValue(matches[i], i);
            __result = result;
            return false;
        }
    }
}
