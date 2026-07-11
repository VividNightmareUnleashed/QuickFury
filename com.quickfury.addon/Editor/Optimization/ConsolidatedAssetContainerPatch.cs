using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace QuickFury {
    /// <summary>
    /// VRCFury creates a separate asset file for every generated material, mesh,
    /// texture, menu and parameter root. Unity spends far more time importing those
    /// files than attaching subassets. Keep controllers as main .controller assets and
    /// consolidate all other roots into one generated container per SaveAssets pass.
    /// </summary>
    internal static class ConsolidatedAssetContainerPatch {
        private sealed class Context {
            internal Object Container;
            internal bool CreatingContainer;
        }

        [ThreadStatic] private static Context active;
        private static Type containerType;
        private static MethodInfo saveAsset;
        private static MethodInfo attachAsset;

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            var saveAssetsType = compatibility.AvatarEditorAssembly.GetType("VF.Service.SaveAssetsService", false);
            var run = saveAssetsType?
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "Run"
                                           && method.ReturnType == typeof(void)
                                           && method.GetParameters().Length == 0);
            var databaseType = VrcfuryCompatibility.FindType("VF.Utils.VRCFuryAssetDatabase");
            containerType = VrcfuryCompatibility.FindType(
                "VF.Utils.VRCFuryAssetDatabase+BinaryContainer"
            );
            saveAsset = databaseType?
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => {
                    if (method.Name != "SaveAsset" || method.ReturnType != typeof(void)) return false;
                    var parameters = method.GetParameters();
                    return parameters.Length == 3
                           && parameters[0].ParameterType == typeof(Object)
                           && parameters[1].ParameterType == typeof(string)
                           && parameters[2].ParameterType == typeof(string);
                });
            attachAsset = databaseType?
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "AttachAsset"
                                           && method.ReturnType == typeof(void)
                                           && method.GetParameters().Length == 2
                                           && method.GetParameters()[0].ParameterType == typeof(Object)
                                           && method.GetParameters()[1].ParameterType == typeof(Object));

            if (run == null || containerType == null || saveAsset == null || attachAsset == null) {
                Debug.LogWarning("[QuickFury] Consolidated asset container disabled: target mismatch.");
                return;
            }

            try {
                harmony.Patch(
                    run,
                    prefix: new HarmonyMethod(typeof(ConsolidatedAssetContainerPatch), nameof(Begin)),
                    finalizer: new HarmonyMethod(typeof(ConsolidatedAssetContainerPatch), nameof(End))
                );
                harmony.Patch(
                    saveAsset,
                    prefix: new HarmonyMethod(typeof(ConsolidatedAssetContainerPatch), nameof(Save))
                );
            } catch (Exception e) {
                Debug.LogWarning("[QuickFury] Consolidated asset container disabled: " + e.Message);
            }
        }

        private static void Begin() {
            active = QuickFurySettings.ConsolidatedAssetContainer ? new Context() : null;
        }

        private static Exception End(Exception __exception) {
            active = null;
            return __exception;
        }

        private static bool Save(Object obj, string dir) {
            var context = active;
            if (context == null || context.CreatingContainer
                                || obj == null || obj is AnimatorController) return true;

            try {
                if (context.Container == null) {
                    context.Container = ScriptableObject.CreateInstance(containerType);
                    context.Container.name = "VRCFury Generated Assets";
                    context.CreatingContainer = true;
                    try {
                        VrcfuryCompatibility.InvokeUnwrapped(
                            saveAsset,
                            null,
                            new object[] { context.Container, dir, "VRCFury Generated Assets" }
                        );
                    } finally {
                        context.CreatingContainer = false;
                    }
                }

                VrcfuryCompatibility.InvokeUnwrapped(attachAsset, null, new[] { obj, context.Container });
                return false;
            } catch (Exception e) {
                active = null;
                Debug.LogWarning(
                    "[QuickFury] Consolidated asset container fell back to separate files: " + e.Message
                );
                return true;
            }
        }
    }
}
