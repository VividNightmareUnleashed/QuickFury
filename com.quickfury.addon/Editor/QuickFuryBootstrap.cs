using System;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace QuickFury {
    [InitializeOnLoad]
    internal static class QuickFuryBootstrap {
        internal const string HarmonyId = "com.quickfury.optimizer";

        private static readonly Harmony Harmony = new Harmony(HarmonyId);
        private static VrcfuryCompatibility compatibility;

        internal static bool ProfilingAvailable => compatibility != null;
        internal static bool OptimizationCompatible => compatibility?.OptimizationCompatible == true;

        static QuickFuryBootstrap() {
            AssemblyReloadEvents.beforeAssemblyReload += Unpatch;
            EditorApplication.delayCall += Initialize;
        }

        private static void Initialize() {
            Unpatch();

            if (!VrcfuryCompatibility.TryCreate(out compatibility, out var error)) {
                Debug.LogWarning("[QuickFury] Disabled: " + error);
                return;
            }

            ProfilePatches.Install(Harmony, compatibility);

            if (compatibility.OptimizationCompatible) {
                OrderedPathRewritePatch.Install(Harmony, compatibility);
                ArmatureConstraintIndexPatch.Install(Harmony, compatibility);
                ArmaturePhysboneIndexPatch.Install(Harmony, compatibility);
                ArmatureSkinIndexPatch.Install(Harmony, compatibility);
                ArmatureDestroyIndexPatch.Install(Harmony, compatibility);
                SaveAssetsDuplicateScanPatch.Install(Harmony, compatibility);
                SaveAssetsBatchingPatch.Install(Harmony, compatibility);
                LayerToTreeLayerIndexPatch.Install(Harmony, compatibility);
            } else {
                Debug.LogWarning(
                    $"[QuickFury] Profiling is active, but behavior-changing optimizations are disabled for " +
                    $"VRCFury {compatibility.PackageVersion}. Tested version: {VrcfuryCompatibility.OptimizedVersion}."
                );
            }

            Debug.Log(
                $"[QuickFury] Ready for VRCFury {compatibility.PackageVersion} " +
                $"(MVID {compatibility.ModuleVersionId})."
            );
        }

        private static void Unpatch() {
            try {
                Harmony.UnpatchAll(HarmonyId);
            } catch (Exception e) {
                Debug.LogWarning("[QuickFury] Failed to remove old patches: " + e.Message);
            }
        }
    }
}
