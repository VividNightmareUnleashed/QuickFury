using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using VRC.Dynamics;
using Object = UnityEngine.Object;

namespace QuickFury {
    /**
     * VF.Utils.VFGameObject.Destroy() does FOUR full-avatar component scans per call (physbones,
     * physbone colliders, contacts, and a full component scan inside GetConstraints) to clean up
     * dynamics that reference the destroyed subtree. ArmatureLink's prune loop calls Destroy once
     * per unused prop object — hundreds of times per bake — making cleanup O(pruned × avatarSize).
     *
     * Behavior-identical replacement: the three typed dynamics scans are cached per build action
     * (components destroyed by earlier Destroy calls in the same batch become unity-null and are
     * skipped, which matches stock behavior where a fresh scan simply no longer returns them).
     * GetConstraints stays a live call (measured separately by the sub-profiler; if it shows up
     * hot it gets its own treatment).
     */
    internal static class DestroyCachePatch {
        private static MethodInfo getConstraintsM;
        private static readonly Dictionary<System.Type, MethodInfo> constraintDestroyMs =
            new Dictionary<System.Type, MethodInfo>();

        private static int cacheVersion = -1;
        private static int cacheKey;
        private static readonly List<VRCPhysBoneBase> physbones = new List<VRCPhysBoneBase>();
        private static readonly List<VRCPhysBoneColliderBase> colliders = new List<VRCPhysBoneColliderBase>();
        private static readonly List<ContactBase> contacts = new List<ContactBase>();

        public static void Apply(Harmony h) {
            var t = QfReflect.VfGoType;
            QfReflect.WarmGo();
            QfReflect.ReqField(t, "getUploadRoots");
            getConstraintsM = QfReflect.ReqMethod(t, "GetConstraints");
            h.Patch(QfReflect.ReqMethod(t, "Destroy"),
                prefix: new HarmonyMethod(typeof(DestroyCachePatch), nameof(Prefix)));
        }

        private static bool Prefix(object __instance) {
            if (!QfSettings.DestroyCache || !QfState.InBuild) return true;
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
                physbones.Clear();
                colliders.Clear();
                contacts.Clear();
                foreach (var root in roots) {
                    var rootGo = QfReflect.Go(root);
                    if (rootGo == null) continue;
                    physbones.AddRange(rootGo.GetComponentsInChildren<VRCPhysBoneBase>(true));
                    colliders.AddRange(rootGo.GetComponentsInChildren<VRCPhysBoneColliderBase>(true));
                    contacts.AddRange(rootGo.GetComponentsInChildren<ContactBase>(true));
                }
                cacheVersion = QfState.scopeVersion;
                cacheKey = key;
            }

            // Identical to stock Destroy(), minus the rescans.
            var t = go.transform;
            foreach (var c in physbones) {
                if (c == null) continue;
                if (c.GetRootTransform().IsChildOf(t)) Object.DestroyImmediate(c);
            }
            foreach (var c in colliders) {
                if (c == null) continue;
                if (c.GetRootTransform().IsChildOf(t)) Object.DestroyImmediate(c);
            }
            foreach (var c in contacts) {
                if (c == null) continue;
                if (c.GetRootTransform().IsChildOf(t)) Object.DestroyImmediate(c);
            }

            var constraints = (IEnumerable)QfReflect.Invoke(getConstraintsM, __instance, false, true);
            if (constraints != null) {
                foreach (var c in constraints) {
                    if (c == null) continue;
                    var type = c.GetType();
                    if (!constraintDestroyMs.TryGetValue(type, out var destroyM)) {
                        constraintDestroyMs[type] = destroyM = AccessTools.Method(type, "Destroy");
                    }
                    if (destroyM != null) QfReflect.Invoke(destroyM, c);
                }
            }

            Object.DestroyImmediate(go);
            return false;
        }
    }
}
