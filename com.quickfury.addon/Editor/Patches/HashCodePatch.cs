using HarmonyLib;

namespace QuickFury {
    /**
     * VF.Utils.VFGameObject.GetHashCode is implemented as Tuple.Create(gameObject).GetHashCode(),
     * which allocates a Tuple on EVERY hash operation. VFGameObjects are used as keys in HashSets
     * and dictionaries all over the build (ArmatureLink alone does tens of thousands of set
     * operations), so this is pure GC churn.
     *
     * Tuple<T1>.GetHashCode == EqualityComparer<T>.Default.GetHashCode(item), which is
     * 0 for a reference-null item and item.GetHashCode() otherwise. This prefix computes exactly
     * that, without the allocation. Applied unconditionally (also outside builds) — it has no
     * cache and no behavior delta.
     */
    internal static class HashCodePatch {
        public static void Apply(Harmony h) {
            var t = QfReflect.VfGoType;
            QfReflect.WarmGo();
            h.Patch(QfReflect.ReqMethod(t, "GetHashCode"),
                prefix: new HarmonyMethod(typeof(HashCodePatch), nameof(Prefix)));
        }

        private static bool Prefix(object __instance, ref int __result) {
            if (!QfSettings.HashCodeFix) return true;
            var go = QfReflect.Go(__instance);
            __result = ReferenceEquals(go, null) ? 0 : go.GetHashCode();
            return false;
        }
    }
}
