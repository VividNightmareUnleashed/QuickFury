using System;
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
            var run = compatibility.SaveAssetsRun;
            var databaseType = VrcfuryCompatibility.FindType("VF.Utils.VRCFuryAssetDatabase");
            containerType = VrcfuryCompatibility.FindType(
                "VF.Utils.VRCFuryAssetDatabase+BinaryContainer"
            );
            saveAsset = VrcfuryCompatibility.FindMethodWithSignature(
                databaseType,
                "SaveAsset",
                typeof(void),
                typeof(Object),
                typeof(string),
                typeof(string)
            );
            attachAsset = VrcfuryCompatibility.FindMethodWithSignature(
                databaseType,
                "AttachAsset",
                typeof(void),
                typeof(Object),
                typeof(Object)
            );

            if (run == null || containerType == null || saveAsset == null || attachAsset == null) {
                throw new InvalidOperationException("target signature mismatch");
            }

            harmony.Patch(
                run,
                prefix: new HarmonyMethod(typeof(ConsolidatedAssetContainerPatch), nameof(Begin)),
                finalizer: new HarmonyMethod(typeof(ConsolidatedAssetContainerPatch), nameof(End))
            );
            harmony.Patch(
                saveAsset,
                prefix: new HarmonyMethod(typeof(ConsolidatedAssetContainerPatch), nameof(Save))
            );
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
