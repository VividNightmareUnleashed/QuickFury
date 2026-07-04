using System;
using System.Reflection;
using HarmonyLib;

namespace QuickFury {
    /**
     * VRCFury's save phase (SaveAssetsService.Run) deliberately EXITS the StartAssetEditing batch
     * and creates every asset with an individual synchronous import — measured at 9.6s across 84
     * assets. Per VRCFury's own comment, the unbatching exists only because Unity 6+ can't resolve
     * asset paths mid-batch ("This works without WithoutAssetEditing in <2022, but in unity 6+...");
     * batched saving was the shipped behavior on older Unity for years.
     *
     * On Unity < 6 this patch restores batched saving, scoped strictly to SaveAssetsService.Run:
     * - WithoutAssetEditing becomes a pass-through while Run is executing (the main save then stays
     *   inside the build's existing batch).
     * - When Run executes with NO batch active (the parameter compressor's second save), the whole
     *   Run is wrapped in VRCFury's own WithAssetEditing, so that save gets batched too.
     * WithoutAssetEditing calls outside Run (e.g. the temp-folder cleanup flush) are untouched.
     *
     * On Unity 6+ the patch refuses to apply.
     */
    internal static class BatchedSavePatch {
        private static MethodInfo runM;
        private static MethodInfo withAssetEditingM;
        private static bool insideRun;
        private static bool wrapping;

        public static void Apply(Harmony h) {
#if UNITY_6000_0_OR_NEWER
            throw new Exception("disabled on Unity 6+ (VRCFury's unbatched save is required there)");
#else
            var saveService = QfReflect.ReqType("VF.Service.SaveAssetsService");
            var assetDb = QfReflect.ReqType("VF.Utils.VRCFuryAssetDatabase");
            runM = QfReflect.ReqMethod(saveService, "Run");
            withAssetEditingM = QfReflect.ReqMethod(assetDb, "WithAssetEditing", new[] { typeof(Action) });

            h.Patch(runM,
                prefix: new HarmonyMethod(typeof(BatchedSavePatch), nameof(RunPrefix)),
                finalizer: new HarmonyMethod(typeof(BatchedSavePatch), nameof(RunFinalizer)));
            h.Patch(QfReflect.ReqMethod(assetDb, "WithoutAssetEditing", new[] { typeof(Action) }),
                prefix: new HarmonyMethod(typeof(BatchedSavePatch), nameof(WithoutAssetEditingPrefix)));
#endif
        }

        private static bool RunPrefix(object __instance) {
            if (!QfSettings.BatchedSave || !QfState.InBuild) return true;
            if (wrapping) {
                // Inner re-entry from the WithAssetEditing wrapper below: run the original body.
                insideRun = true;
                return true;
            }
            // Ensure a batch surrounds the whole save. If one is already active (main build),
            // VRCFury's WithAssetEditing is a no-op passthrough; if none is (parameter compressor's
            // second save), this opens one.
            wrapping = true;
            try {
                QfReflect.Invoke(withAssetEditingM, null, (Action)(() => {
                    QfReflect.Invoke(runM, __instance);
                }));
            } finally {
                wrapping = false;
            }
            return false;
        }

        private static void RunFinalizer() {
            if (!wrapping) insideRun = false;
        }

        private static bool WithoutAssetEditingPrefix(Action go) {
            if (!QfSettings.BatchedSave || !QfState.InBuild || !insideRun) return true;
            go(); // stay inside the batch instead of flushing it
            return false;
        }
    }
}
