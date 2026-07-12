using System;
using HarmonyLib;

namespace QuickFury {
    /// <summary>
    /// Keeps VRCFury's outer AssetDatabase.StartAssetEditing batch active while
    /// SaveAssetsService creates its generated assets. VRCFury leaves the batch to
    /// work around a Unity 6 asset-path issue; Unity 2022 does not require that
    /// workaround and otherwise imports every generated asset individually.
    /// </summary>
    internal static class SaveAssetsBatchingPatch {
        [ThreadStatic] private static int saveAssetsDepth;

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            var run = compatibility.SaveAssetsRun;

            var assetDatabaseType = VrcfuryCompatibility.FindType("VF.Utils.VRCFuryAssetDatabase");
            var withoutAssetEditing = VrcfuryCompatibility.FindUniqueMethod(
                assetDatabaseType,
                "WithoutAssetEditing",
                method => {
                    var parameters = method.GetParameters();
                    return method.IsStatic
                           && method.ReturnType == typeof(void)
                           && parameters.Length == 1
                           && parameters[0].ParameterType == typeof(Action);
                }
            );

            if (run == null || withoutAssetEditing == null) {
                throw new InvalidOperationException("target signature mismatch");
            }

            harmony.Patch(
                run,
                prefix: new HarmonyMethod(typeof(SaveAssetsBatchingPatch), nameof(RunPrefix)),
                finalizer: new HarmonyMethod(typeof(SaveAssetsBatchingPatch), nameof(RunFinalizer))
            );
            harmony.Patch(
                withoutAssetEditing,
                prefix: new HarmonyMethod(
                    typeof(SaveAssetsBatchingPatch),
                    nameof(WithoutAssetEditingPrefix)
                )
            );
        }

        private static void RunPrefix(out bool __state) {
            __state = QuickFurySettings.RetainSaveAssetsBatching && QuickFurySettings.IsUnity2022;
            if (__state) saveAssetsDepth++;
        }

        private static Exception RunFinalizer(bool __state, Exception __exception) {
            if (__state) saveAssetsDepth = Math.Max(0, saveAssetsDepth - 1);
            return __exception;
        }

        private static bool WithoutAssetEditingPrefix(Action go) {
            if (saveAssetsDepth <= 0) return true;

            go();
            return false;
        }
    }
}
