using UnityEditor;
using UnityEngine;

namespace QuickFury {
    internal static class QuickFurySettings {
        internal const string OptimizeOrderedPathsKey = "com.quickfury.optimizeOrderedPaths";
        internal const string SkipEmptyDeferredRewriteKey = "com.quickfury.skipEmptyDeferredRewrite";
        internal const string ConstraintIndexKey = "com.quickfury.armatureConstraintIndex";
        internal const string PhysboneIndexKey = "com.quickfury.armaturePhysboneIndex";
        internal const string SkinIndexKey = "com.quickfury.armatureSkinIndex";
        internal const string DestroyIndexKey = "com.quickfury.armatureDestroyIndex";
        internal const string SkipArmatureDebugInfoKey = "com.quickfury.skipArmatureDebugInfo";
        internal const string FastArmatureMoveKey = "com.quickfury.fastArmatureMove";
        internal const string SkipTransformAssetScanKey = "com.quickfury.skipTransformAssetScan";
        internal const string SkipDuplicateRendererAssetScanKey = "com.quickfury.skipDuplicateRendererAssetScan";
        internal const string RetainSaveAssetsBatchingKey = "com.quickfury.retainSaveAssetsBatching";
        internal const string FastSaveAssetDiscoveryKey = "com.quickfury.fastSaveAssetDiscovery";
        internal const string FastControllerAssetGraphKey = "com.quickfury.fastControllerAssetGraph";
        internal const string ConsolidatedAssetContainerKey = "com.quickfury.consolidatedAssetContainer";
        internal const string BlendshapeBindingCacheKey = "com.quickfury.blendshapeBindingCache";
        internal const string SpsCoveredRendererKey = "com.quickfury.spsCoveredRenderer";
        internal const string SpsMaterialProbeCacheKey = "com.quickfury.spsMaterialProbeCache";
        internal const string ControllerParameterIndexKey = "com.quickfury.controllerParameterIndex";
        internal const string LayerToTreeLayerIndexKey = "com.quickfury.layerToTreeLayerIndex";
        internal const string TrackingBehaviourIndexKey = "com.quickfury.trackingBehaviourIndex";
        internal const string BehaviourContainerFilterKey = "com.quickfury.behaviourContainerFilter";
        internal const string DeduplicateGeneratedClipsKey = "com.quickfury.deduplicateGeneratedClips";
        internal const string DetailedProfilingKey = "com.quickfury.detailedProfiling";

        internal static bool OptimizeOrderedPaths => EditorPrefs.GetBool(OptimizeOrderedPathsKey, true);
        internal static bool SkipEmptyDeferredRewrite => EditorPrefs.GetBool(SkipEmptyDeferredRewriteKey, true);
        internal static bool ConstraintIndex => EditorPrefs.GetBool(ConstraintIndexKey, true);
        internal static bool PhysboneIndex => EditorPrefs.GetBool(PhysboneIndexKey, true);
        internal static bool SkinIndex => EditorPrefs.GetBool(SkinIndexKey, true);
        internal static bool DestroyIndex => EditorPrefs.GetBool(DestroyIndexKey, true);
        internal static bool SkipArmatureDebugInfo => EditorPrefs.GetBool(SkipArmatureDebugInfoKey, true);
        internal static bool FastArmatureMove => EditorPrefs.GetBool(FastArmatureMoveKey, true);
        internal static bool SkipTransformAssetScan => EditorPrefs.GetBool(SkipTransformAssetScanKey, false);
        internal static bool SkipDuplicateRendererAssetScan =>
            EditorPrefs.GetBool(SkipDuplicateRendererAssetScanKey, false);
        internal static bool RetainSaveAssetsBatching => EditorPrefs.GetBool(RetainSaveAssetsBatchingKey, true);
        internal static bool FastSaveAssetDiscovery => EditorPrefs.GetBool(FastSaveAssetDiscoveryKey, true);
        internal static bool FastControllerAssetGraph => EditorPrefs.GetBool(FastControllerAssetGraphKey, true);
        internal static bool ConsolidatedAssetContainer => EditorPrefs.GetBool(ConsolidatedAssetContainerKey, true);
        internal static bool BlendshapeBindingCache => EditorPrefs.GetBool(BlendshapeBindingCacheKey, true);
        internal static bool SpsCoveredRenderer => EditorPrefs.GetBool(SpsCoveredRendererKey, true);
        internal static bool SpsMaterialProbeCache => EditorPrefs.GetBool(SpsMaterialProbeCacheKey, true);
        internal static bool ControllerParameterIndex => EditorPrefs.GetBool(ControllerParameterIndexKey, true);
        internal static bool LayerToTreeLayerIndex => EditorPrefs.GetBool(LayerToTreeLayerIndexKey, true);
        internal static bool TrackingBehaviourIndex => EditorPrefs.GetBool(TrackingBehaviourIndexKey, true);
        internal static bool BehaviourContainerFilter => EditorPrefs.GetBool(BehaviourContainerFilterKey, true);
        internal static bool DeduplicateGeneratedClips => EditorPrefs.GetBool(DeduplicateGeneratedClipsKey, true);
        internal static bool DetailedProfiling => EditorPrefs.GetBool(DetailedProfilingKey, false);

        [MenuItem("Tools/QuickFury/Use recommended settings", false, 1)]
        private static void UseRecommendedSettings() {
            EditorPrefs.SetBool(OptimizeOrderedPathsKey, true);
            EditorPrefs.SetBool(SkipEmptyDeferredRewriteKey, true);
            EditorPrefs.SetBool(ConstraintIndexKey, true);
            EditorPrefs.SetBool(PhysboneIndexKey, true);
            EditorPrefs.SetBool(SkinIndexKey, true);
            EditorPrefs.SetBool(DestroyIndexKey, true);
            EditorPrefs.SetBool(SkipArmatureDebugInfoKey, true);
            EditorPrefs.SetBool(FastArmatureMoveKey, true);
            EditorPrefs.SetBool(LayerToTreeLayerIndexKey, true);
            EditorPrefs.SetBool(TrackingBehaviourIndexKey, true);
            EditorPrefs.SetBool(BehaviourContainerFilterKey, true);
            EditorPrefs.SetBool(DeduplicateGeneratedClipsKey, true);
            EditorPrefs.SetBool(SkipTransformAssetScanKey, false);
            EditorPrefs.SetBool(SkipDuplicateRendererAssetScanKey, false);
            EditorPrefs.SetBool(RetainSaveAssetsBatchingKey, true);
            EditorPrefs.SetBool(FastSaveAssetDiscoveryKey, true);
            EditorPrefs.SetBool(FastControllerAssetGraphKey, true);
            EditorPrefs.SetBool(ConsolidatedAssetContainerKey, true);
            EditorPrefs.SetBool(BlendshapeBindingCacheKey, true);
            EditorPrefs.SetBool(SpsCoveredRendererKey, true);
            EditorPrefs.SetBool(SpsMaterialProbeCacheKey, true);
            EditorPrefs.SetBool(ControllerParameterIndexKey, true);
        }

        [MenuItem("Tools/QuickFury/Disable all optimizations", false, 2)]
        private static void DisableAllOptimizations() {
            EditorPrefs.SetBool(OptimizeOrderedPathsKey, false);
            EditorPrefs.SetBool(SkipEmptyDeferredRewriteKey, false);
            EditorPrefs.SetBool(ConstraintIndexKey, false);
            EditorPrefs.SetBool(PhysboneIndexKey, false);
            EditorPrefs.SetBool(SkinIndexKey, false);
            EditorPrefs.SetBool(DestroyIndexKey, false);
            EditorPrefs.SetBool(SkipArmatureDebugInfoKey, false);
            EditorPrefs.SetBool(FastArmatureMoveKey, false);
            EditorPrefs.SetBool(LayerToTreeLayerIndexKey, false);
            EditorPrefs.SetBool(TrackingBehaviourIndexKey, false);
            EditorPrefs.SetBool(BehaviourContainerFilterKey, false);
            EditorPrefs.SetBool(DeduplicateGeneratedClipsKey, false);
            EditorPrefs.SetBool(SkipTransformAssetScanKey, false);
            EditorPrefs.SetBool(SkipDuplicateRendererAssetScanKey, false);
            EditorPrefs.SetBool(RetainSaveAssetsBatchingKey, false);
            EditorPrefs.SetBool(FastSaveAssetDiscoveryKey, false);
            EditorPrefs.SetBool(FastControllerAssetGraphKey, false);
            EditorPrefs.SetBool(ConsolidatedAssetContainerKey, false);
            EditorPrefs.SetBool(BlendshapeBindingCacheKey, false);
            EditorPrefs.SetBool(SpsCoveredRendererKey, false);
            EditorPrefs.SetBool(SpsMaterialProbeCacheKey, false);
            EditorPrefs.SetBool(ControllerParameterIndexKey, false);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Armature constraint index")]
        private static void ToggleConstraintIndex() {
            EditorPrefs.SetBool(ConstraintIndexKey, !ConstraintIndex);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Armature constraint index", true)]
        private static bool ValidateConstraintIndex() {
            Menu.SetChecked("Tools/QuickFury/Optimizations/Armature constraint index", ConstraintIndex);
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Armature PhysBone index")]
        private static void TogglePhysboneIndex() {
            EditorPrefs.SetBool(PhysboneIndexKey, !PhysboneIndex);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Armature PhysBone index", true)]
        private static bool ValidatePhysboneIndex() {
            Menu.SetChecked("Tools/QuickFury/Optimizations/Armature PhysBone index", PhysboneIndex);
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Armature skin index")]
        private static void ToggleSkinIndex() {
            EditorPrefs.SetBool(SkinIndexKey, !SkinIndex);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Armature skin index", true)]
        private static bool ValidateSkinIndex() {
            Menu.SetChecked("Tools/QuickFury/Optimizations/Armature skin index", SkinIndex);
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Armature destroy index")]
        private static void ToggleDestroyIndex() {
            EditorPrefs.SetBool(DestroyIndexKey, !DestroyIndex);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Armature destroy index", true)]
        private static bool ValidateDestroyIndex() {
            Menu.SetChecked("Tools/QuickFury/Optimizations/Armature destroy index", DestroyIndex);
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Skip Armature Link debug components")]
        private static void ToggleSkipArmatureDebugInfo() {
            EditorPrefs.SetBool(SkipArmatureDebugInfoKey, !SkipArmatureDebugInfo);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Skip Armature Link debug components", true)]
        private static bool ValidateSkipArmatureDebugInfo() {
            Menu.SetChecked(
                "Tools/QuickFury/Optimizations/Skip Armature Link debug components",
                SkipArmatureDebugInfo
            );
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Fast Armature Link moves")]
        private static void ToggleFastArmatureMove() {
            EditorPrefs.SetBool(FastArmatureMoveKey, !FastArmatureMove);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Fast Armature Link moves", true)]
        private static bool ValidateFastArmatureMove() {
            Menu.SetChecked(
                "Tools/QuickFury/Optimizations/Fast Armature Link moves",
                FastArmatureMove
            );
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Skip inert Transform asset scans")]
        private static void ToggleSkipTransformAssetScan() {
            EditorPrefs.SetBool(SkipTransformAssetScanKey, !SkipTransformAssetScan);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Skip inert Transform asset scans", true)]
        private static bool ValidateSkipTransformAssetScan() {
            Menu.SetChecked(
                "Tools/QuickFury/Optimizations/Skip inert Transform asset scans",
                SkipTransformAssetScan
            );
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Skip duplicate renderer asset scan")]
        private static void ToggleSkipDuplicateRendererAssetScan() {
            EditorPrefs.SetBool(SkipDuplicateRendererAssetScanKey, !SkipDuplicateRendererAssetScan);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Skip duplicate renderer asset scan", true)]
        private static bool ValidateSkipDuplicateRendererAssetScan() {
            Menu.SetChecked(
                "Tools/QuickFury/Optimizations/Skip duplicate renderer asset scan",
                SkipDuplicateRendererAssetScan
            );
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Retain SaveAssets batching (Unity 2022)")]
        private static void ToggleRetainSaveAssetsBatching() {
            EditorPrefs.SetBool(RetainSaveAssetsBatchingKey, !RetainSaveAssetsBatching);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Retain SaveAssets batching (Unity 2022)", true)]
        private static bool ValidateRetainSaveAssetsBatching() {
            Menu.SetChecked(
                "Tools/QuickFury/Optimizations/Retain SaveAssets batching (Unity 2022)",
                RetainSaveAssetsBatching
            );
            return QuickFuryBootstrap.OptimizationCompatible && Application.unityVersion.StartsWith("2022.");
        }

        [MenuItem("Tools/QuickFury/Optimizations/Fast generated-asset discovery")]
        private static void ToggleFastSaveAssetDiscovery() {
            EditorPrefs.SetBool(FastSaveAssetDiscoveryKey, !FastSaveAssetDiscovery);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Fast generated-asset discovery", true)]
        private static bool ValidateFastSaveAssetDiscovery() {
            Menu.SetChecked(
                "Tools/QuickFury/Optimizations/Fast generated-asset discovery",
                FastSaveAssetDiscovery
            );
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Fast controller asset graph")]
        private static void ToggleFastControllerAssetGraph() {
            EditorPrefs.SetBool(FastControllerAssetGraphKey, !FastControllerAssetGraph);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Fast controller asset graph", true)]
        private static bool ValidateFastControllerAssetGraph() {
            Menu.SetChecked(
                "Tools/QuickFury/Optimizations/Fast controller asset graph",
                FastControllerAssetGraph
            );
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Consolidate generated asset files")]
        private static void ToggleConsolidatedAssetContainer() {
            EditorPrefs.SetBool(ConsolidatedAssetContainerKey, !ConsolidatedAssetContainer);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Consolidate generated asset files", true)]
        private static bool ValidateConsolidatedAssetContainer() {
            Menu.SetChecked(
                "Tools/QuickFury/Optimizations/Consolidate generated asset files",
                ConsolidatedAssetContainer
            );
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Cache blendshape controller bindings")]
        private static void ToggleBlendshapeBindingCache() {
            EditorPrefs.SetBool(BlendshapeBindingCacheKey, !BlendshapeBindingCache);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Cache blendshape controller bindings", true)]
        private static bool ValidateBlendshapeBindingCache() {
            Menu.SetChecked(
                "Tools/QuickFury/Optimizations/Cache blendshape controller bindings",
                BlendshapeBindingCache
            );
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Skip covered SPS mesh probes")]
        private static void ToggleSpsCoveredRenderer() {
            EditorPrefs.SetBool(SpsCoveredRendererKey, !SpsCoveredRenderer);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Skip covered SPS mesh probes", true)]
        private static bool ValidateSpsCoveredRenderer() {
            Menu.SetChecked(
                "Tools/QuickFury/Optimizations/Skip covered SPS mesh probes",
                SpsCoveredRenderer
            );
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Cache DPS-TPS material probes")]
        private static void ToggleSpsMaterialProbeCache() {
            EditorPrefs.SetBool(SpsMaterialProbeCacheKey, !SpsMaterialProbeCache);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Cache DPS-TPS material probes", true)]
        private static bool ValidateSpsMaterialProbeCache() {
            Menu.SetChecked(
                "Tools/QuickFury/Optimizations/Cache DPS-TPS material probes",
                SpsMaterialProbeCache
            );
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Controller parameter index")]
        private static void ToggleControllerParameterIndex() {
            EditorPrefs.SetBool(ControllerParameterIndexKey, !ControllerParameterIndex);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Controller parameter index", true)]
        private static bool ValidateControllerParameterIndex() {
            Menu.SetChecked(
                "Tools/QuickFury/Optimizations/Controller parameter index",
                ControllerParameterIndex
            );
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Layer-to-tree layer index")]
        private static void ToggleLayerToTreeLayerIndex() {
            EditorPrefs.SetBool(LayerToTreeLayerIndexKey, !LayerToTreeLayerIndex);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Layer-to-tree layer index", true)]
        private static bool ValidateLayerToTreeLayerIndex() {
            Menu.SetChecked(
                "Tools/QuickFury/Optimizations/Layer-to-tree layer index",
                LayerToTreeLayerIndex
            );
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Tracking behaviour index")]
        private static void ToggleTrackingBehaviourIndex() {
            EditorPrefs.SetBool(TrackingBehaviourIndexKey, !TrackingBehaviourIndex);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Tracking behaviour index", true)]
        private static bool ValidateTrackingBehaviourIndex() {
            Menu.SetChecked(
                "Tools/QuickFury/Optimizations/Tracking behaviour index",
                TrackingBehaviourIndex
            );
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Filter irrelevant behaviour containers")]
        private static void ToggleBehaviourContainerFilter() {
            EditorPrefs.SetBool(BehaviourContainerFilterKey, !BehaviourContainerFilter);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Filter irrelevant behaviour containers", true)]
        private static bool ValidateBehaviourContainerFilter() {
            Menu.SetChecked(
                "Tools/QuickFury/Optimizations/Filter irrelevant behaviour containers",
                BehaviourContainerFilter
            );
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Deduplicate generated animation clips")]
        private static void ToggleDeduplicateGeneratedClips() {
            EditorPrefs.SetBool(DeduplicateGeneratedClipsKey, !DeduplicateGeneratedClips);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Deduplicate generated animation clips", true)]
        private static bool ValidateDeduplicateGeneratedClips() {
            Menu.SetChecked(
                "Tools/QuickFury/Optimizations/Deduplicate generated animation clips",
                DeduplicateGeneratedClips
            );
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Ordered animation path rewrite")]
        private static void ToggleOrderedPaths() {
            EditorPrefs.SetBool(OptimizeOrderedPathsKey, !OptimizeOrderedPaths);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Ordered animation path rewrite", true)]
        private static bool ValidateOrderedPaths() {
            Menu.SetChecked("Tools/QuickFury/Optimizations/Ordered animation path rewrite", OptimizeOrderedPaths);
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Optimizations/Skip empty deferred rewrite")]
        private static void ToggleEmptyDeferredRewrite() {
            EditorPrefs.SetBool(SkipEmptyDeferredRewriteKey, !SkipEmptyDeferredRewrite);
        }

        [MenuItem("Tools/QuickFury/Optimizations/Skip empty deferred rewrite", true)]
        private static bool ValidateEmptyDeferredRewrite() {
            Menu.SetChecked("Tools/QuickFury/Optimizations/Skip empty deferred rewrite", SkipEmptyDeferredRewrite);
            return QuickFuryBootstrap.OptimizationCompatible;
        }

        [MenuItem("Tools/QuickFury/Profiling/Detailed internal timings")]
        private static void ToggleDetailedProfiling() {
            EditorPrefs.SetBool(DetailedProfilingKey, !DetailedProfiling);
        }

        [MenuItem("Tools/QuickFury/Profiling/Detailed internal timings", true)]
        private static bool ValidateDetailedProfiling() {
            Menu.SetChecked("Tools/QuickFury/Profiling/Detailed internal timings", DetailedProfiling);
            return QuickFuryBootstrap.ProfilingAvailable;
        }

        [MenuItem("Tools/QuickFury/Profiling/Log last report")]
        private static void LogLastReport() {
            var report = QuickFuryProfilerApi.LastReport;
            Debug.Log(string.IsNullOrEmpty(report) ? "[QuickFury] No profile has completed yet." : report);
        }
    }
}
