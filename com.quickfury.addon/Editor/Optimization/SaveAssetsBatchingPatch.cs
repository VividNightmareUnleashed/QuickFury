using System;
using System.Reflection;
using HarmonyLib;
using UnityEditor;

namespace QuickFury {
    /// <summary>
    /// Keeps VRCFury's outer AssetDatabase.StartAssetEditing batch active while
    /// SaveAssetsService creates its generated assets. VRCFury leaves the batch to
    /// work around a Unity 6 asset-path issue; Unity 2022 does not require that
    /// workaround and otherwise imports every generated asset individually.
    ///
    /// Two operations still need the batch to genuinely pause:
    /// - Nested WithoutAssetEditing calls must reach Unity. TmpFilePackage.Cleanup
    ///   flushes its folder deletions with an empty action; swallowing that flush
    ///   leaves the asset database describing folders that are gone from disk.
    /// - AssetDatabase.CreateFolder is deferred while a batch is active, so creating
    ///   the bake's build folder inside the retained batch would leave the following
    ///   CreateAsset without a parent directory.
    /// </summary>
    internal static class SaveAssetsBatchingPatch {
        [ThreadStatic] private static int saveAssetsDepth;
        [ThreadStatic] private static bool retainedSaveActive;
        [ThreadStatic] private static bool creatingFolder;

        private static MethodInfo createFolder;

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
            createFolder = VrcfuryCompatibility.FindMethodWithSignature(
                assetDatabaseType,
                "CreateFolder",
                typeof(void),
                typeof(string)
            );

            if (run == null || withoutAssetEditing == null || createFolder == null) {
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
                ),
                finalizer: new HarmonyMethod(
                    typeof(SaveAssetsBatchingPatch),
                    nameof(WithoutAssetEditingFinalizer)
                )
            );
            harmony.Patch(
                createFolder,
                prefix: new HarmonyMethod(typeof(SaveAssetsBatchingPatch), nameof(CreateFolderPrefix))
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

        private static bool WithoutAssetEditingPrefix(Action go, out bool __state) {
            __state = false;
            if (saveAssetsDepth <= 0) return true;

            if (retainedSaveActive) {
                // A nested call is one of VRCFury's deliberate flush points. Let the
                // original genuinely leave the batch, and suspend the folder-creation
                // escape while no batch is active.
                retainedSaveActive = false;
                __state = true;
                return true;
            }

            retainedSaveActive = true;
            try {
                go();
            } finally {
                retainedSaveActive = false;
            }
            return false;
        }

        private static void WithoutAssetEditingFinalizer(bool __state) {
            if (__state) retainedSaveActive = true;
        }

        private static bool CreateFolderPrefix(string path) {
            if (!retainedSaveActive || creatingFolder) return true;
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return true;

            // The folder does not exist yet, and creating it inside the retained batch
            // would be deferred until the batch ends. Pause the batch so the folder is
            // real before the caller's CreateAsset targets it.
            creatingFolder = true;
            try {
                AssetDatabase.StopAssetEditing();
                try {
                    VrcfuryCompatibility.InvokeUnwrapped(createFolder, null, new object[] { path });
                } finally {
                    AssetDatabase.StartAssetEditing();
                }
            } finally {
                creatingFolder = false;
            }
            return false;
        }
    }
}
