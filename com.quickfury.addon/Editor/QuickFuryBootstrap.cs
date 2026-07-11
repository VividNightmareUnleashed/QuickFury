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

            Install("Profiling", ProfilePatches.Install);

            if (compatibility.OptimizationCompatible) {
                Install("Shared armature reflection", (harmony, targets) => ArmatureReflection.Resolve());
                Install("Ordered path rewrite", OrderedPathRewritePatch.Install);
                Install("Armature constraint index", ArmatureConstraintIndexPatch.Install);
                Install("Armature PhysBone index", ArmaturePhysboneIndexPatch.Install);
                Install("Batched Armature skin rewrite", ArmatureSkinIndexPatch.Install);
                Install("Armature destroy index", ArmatureDestroyIndexPatch.Install);
                Install("Armature debug-component suppression", ArmatureDebugInfoPatch.Install);
                Install("Fast Armature Link moves", FastArmatureMovePatch.Install);
                Install("Fast SaveAssets discovery", SaveAssetsDuplicateScanPatch.Install);
                Install("SaveAssets batching", SaveAssetsBatchingPatch.Install);
                Install("Consolidated asset container", ConsolidatedAssetContainerPatch.Install);
                Install("Fast controller asset graph", FastControllerAssetGraphPatch.Install);
                Install("Blendshape binding cache", BlendshapeBindingCachePatch.Install);
                Install("Covered SPS mesh probe skip", SpsCoveredRendererPatch.Install);
                Install("SPS material probe cache", SpsMaterialProbeCachePatch.Install);
                Install("Controller parameter index", ControllerParameterIndexPatch.Install);
                Install("Layer-to-tree layer index", LayerToTreeLayerIndexPatch.Install);
                Install("Tracking behaviour index", TrackingBehaviourIndexPatch.Install);
                Install("Behaviour container filter", BehaviourContainerFilterPatch.Install);
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

        // A patch whose reflection throws (rather than reporting a mismatch) must not
        // prevent the remaining patches from installing.
        private static void Install(string name, Action<Harmony, VrcfuryCompatibility> install) {
            try {
                install(Harmony, compatibility);
            } catch (Exception e) {
                Debug.LogWarning($"[QuickFury] {name} disabled: {e.Message}");
            }
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
