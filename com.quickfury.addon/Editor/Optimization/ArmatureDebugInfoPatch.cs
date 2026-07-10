using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace QuickFury {
    /// <summary>
    /// In preview/non-upload builds VRCFury adds VRCFuryDebugInfo to every merged bone.
    /// Large outfits can create thousands of diagnostic-only components which are then
    /// carried through pruning, component enumeration and NDMF cloning. Suppress them
    /// only for the Armature Link action; runtime bake output is unchanged.
    /// </summary>
    internal static class ArmatureDebugInfoPatch {
        [ThreadStatic] private static bool suppress;

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            var armatureType = VrcfuryCompatibility.FindType("VF.Service.ArmatureLinkService");
            var uploadHookType = VrcfuryCompatibility.FindType("VF.Hooks.IsActuallyUploadingHook");
            var apply = armatureType?
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "Apply"
                                           && method.ReturnType == typeof(void)
                                           && method.GetParameters().Length == 0);
            var get = uploadHookType?
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "Get"
                                           && method.ReturnType == typeof(bool)
                                           && method.GetParameters().Length == 0);

            if (apply == null || get == null) {
                Debug.LogWarning("[QuickFury] Armature debug-component suppression disabled: target mismatch.");
                return;
            }

            try {
                harmony.Patch(
                    apply,
                    prefix: new HarmonyMethod(typeof(ArmatureDebugInfoPatch), nameof(Begin)),
                    finalizer: new HarmonyMethod(typeof(ArmatureDebugInfoPatch), nameof(End))
                );
                harmony.Patch(
                    get,
                    prefix: new HarmonyMethod(typeof(ArmatureDebugInfoPatch), nameof(ReportUploading))
                );
            } catch (Exception e) {
                Debug.LogWarning("[QuickFury] Armature debug-component suppression disabled: " + e.Message);
            }
        }

        private static void Begin() {
            suppress = QuickFurySettings.SkipArmatureDebugInfo;
        }

        private static Exception End(Exception __exception) {
            suppress = false;
            return __exception;
        }

        private static bool ReportUploading(ref bool __result) {
            if (!suppress) return true;
            __result = true;
            return false;
        }
    }
}
