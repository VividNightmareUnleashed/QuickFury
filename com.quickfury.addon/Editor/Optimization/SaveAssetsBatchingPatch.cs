using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

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
            var saveAssetsType = compatibility.AvatarEditorAssembly.GetType("VF.Service.SaveAssetsService", false);
            var run = FindUniqueMethod(
                saveAssetsType,
                "Run",
                method => method.ReturnType == typeof(void) && method.GetParameters().Length == 0
            );

            var assetDatabaseType = VrcfuryCompatibility.FindType("VF.Utils.VRCFuryAssetDatabase");
            var withoutAssetEditing = FindUniqueMethod(
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
                Debug.LogWarning(
                    "[QuickFury] SaveAssets batching optimization disabled: expected VRCFury methods were not found."
                );
                return;
            }

            try {
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
            } catch (Exception e) {
                Debug.LogWarning("[QuickFury] SaveAssets batching optimization disabled: " + e.Message);
            }
        }

        private static void RunPrefix(out bool __state) {
            __state = QuickFurySettings.RetainSaveAssetsBatching && IsUnity2022();
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

        private static bool IsUnity2022() {
            return Application.unityVersion.StartsWith("2022.", StringComparison.Ordinal);
        }

        private static MethodInfo FindUniqueMethod(Type type, string name, Func<MethodInfo, bool> predicate) {
            if (type == null) return null;
            return type
                .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                            BindingFlags.DeclaredOnly)
                .Where(method => method.Name == name)
                .Where(method => !method.ContainsGenericParameters)
                .Where(predicate)
                .SingleOrDefault();
        }
    }
}
