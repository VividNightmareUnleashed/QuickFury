using HarmonyLib;

namespace QuickFury {
    /**
     * In play-mode / test builds (anything that isn't a real upload), VRCFury's ArmatureLink adds a
     * VRCFuryDebugInfo component to EVERY merged bone, building several avatar-path strings per
     * bone and repeatedly appending to a serialized string field. Real uploads skip all of it
     * (ApplyOne's saveDebugInfo is false when actually uploading). On outfit-heavy avatars this is
     * thousands of editor AddComponent calls per play-mode bake — and every added component also
     * inflates the later GetUsageReasons serialized walk and all subsequent full-component scans.
     *
     * This prefix flips ApplyOne's saveDebugInfo argument to false, making play-mode bakes take
     * the same (fast) path as uploads. DELIBERATE BEHAVIOR CHANGE, play mode only: the debug-info
     * components ArmatureLink would attach for inspection are not created. Toggle it off under
     * Tools → QuickFury → Patches when you actually want to debug ArmatureLink decisions.
     *
     * Note: this intentionally does NOT touch IsActuallyUploadingHook.Get() itself — other VRCFury
     * systems (plugin gating, material locking) depend on it.
     */
    internal static class SkipDebugInfoPatch {
        public static void Apply(Harmony h) {
            var t = QfReflect.ReqType("VF.Service.ArmatureLinkService");
            h.Patch(QfReflect.ReqMethod(t, "ApplyOne"),
                prefix: new HarmonyMethod(typeof(SkipDebugInfoPatch), nameof(Prefix)));
        }

        private static void Prefix(ref bool saveDebugInfo) {
            if (!QfSettings.SkipPlayModeDebugInfo || !QfState.InBuild) return;
            saveDebugInfo = false;
        }
    }
}
