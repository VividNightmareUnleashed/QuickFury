using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEditor;

namespace QuickFury {
    /**
     * VF.Service.ValidateBindingsService.IsValid(EditorCurveBinding) does a native transform.Find +
     * GetComponent per call, and at least four separate VRCFury passes (FullController nearest-match
     * rewriting, LayerToTree, CleanupEmptyLayers, FixWriteDefaults) call it per binding, per clip,
     * with heavy repetition. This memoizes the result per service instance, per build action.
     *
     * Pure wrapper — the original method still computes every fresh value, so behavior is identical
     * regardless of VRCFury version. The cache is invalidated at every action boundary (see QfState),
     * so hierarchy changes between passes are always observed.
     */
    internal static class BindingValidCachePatch {
        private class BindingComparer : IEqualityComparer<EditorCurveBinding> {
            public static readonly BindingComparer Instance = new BindingComparer();
            public bool Equals(EditorCurveBinding a, EditorCurveBinding b) {
                return a.path == b.path && a.propertyName == b.propertyName && a.type == b.type;
            }
            public int GetHashCode(EditorCurveBinding b) {
                unchecked {
                    var hash = b.path != null ? b.path.GetHashCode() : 0;
                    hash = hash * 31 + (b.propertyName != null ? b.propertyName.GetHashCode() : 0);
                    hash = hash * 31 + (b.type != null ? b.type.GetHashCode() : 0);
                    return hash;
                }
            }
        }

        private class Holder {
            public int version = -1;
            public readonly Dictionary<EditorCurveBinding, bool> map =
                new Dictionary<EditorCurveBinding, bool>(BindingComparer.Instance);
        }

        private static readonly ConditionalWeakTable<object, Holder> holders =
            new ConditionalWeakTable<object, Holder>();

        public static void Apply(Harmony h) {
            var t = QfReflect.ReqType("VF.Service.ValidateBindingsService");
            h.Patch(QfReflect.ReqMethod(t, "IsValid", new[] { typeof(EditorCurveBinding) }),
                prefix: new HarmonyMethod(typeof(BindingValidCachePatch), nameof(Prefix)),
                postfix: new HarmonyMethod(typeof(BindingValidCachePatch), nameof(Postfix)));
        }

        private static bool Prefix(object __instance, EditorCurveBinding binding, ref bool __result, ref bool __state) {
            __state = false;
            if (!QfSettings.BindingCache || !QfState.InBuild) return true;
            var holder = holders.GetOrCreateValue(__instance);
            if (holder.version != QfState.scopeVersion) {
                holder.map.Clear();
                holder.version = QfState.scopeVersion;
            }
            if (holder.map.TryGetValue(binding, out var cached)) {
                __result = cached;
                return false;
            }
            __state = true; // we passed through; store the fresh result in Postfix
            return true;
        }

        private static void Postfix(object __instance, EditorCurveBinding binding, bool __result, bool __state) {
            if (!__state) return;
            if (holders.TryGetValue(__instance, out var holder) && holder.version == QfState.scopeVersion) {
                holder.map[binding] = __result;
            }
        }
    }
}
