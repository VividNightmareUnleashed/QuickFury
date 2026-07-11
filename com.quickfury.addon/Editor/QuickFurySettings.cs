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

        private const string OptimizationsMenu = "Tools/QuickFury/Optimizations/";
        private const string ProfilingMenu = "Tools/QuickFury/Profiling/";
        private const string ConstraintIndexMenu = OptimizationsMenu + "Armature constraint index";
        private const string PhysboneIndexMenu = OptimizationsMenu + "Armature PhysBone index";
        private const string SkinIndexMenu = OptimizationsMenu + "Armature skin index";
        private const string DestroyIndexMenu = OptimizationsMenu + "Armature destroy index";
        private const string SkipArmatureDebugInfoMenu =
            OptimizationsMenu + "Skip Armature Link debug components";
        private const string FastArmatureMoveMenu = OptimizationsMenu + "Fast Armature Link moves";
        private const string SkipTransformAssetScanMenu =
            OptimizationsMenu + "Skip inert Transform asset scans";
        private const string SkipDuplicateRendererAssetScanMenu =
            OptimizationsMenu + "Skip duplicate renderer asset scan";
        private const string RetainSaveAssetsBatchingMenu =
            OptimizationsMenu + "Retain SaveAssets batching (Unity 2022)";
        private const string FastSaveAssetDiscoveryMenu =
            OptimizationsMenu + "Fast generated-asset discovery";
        private const string FastControllerAssetGraphMenu =
            OptimizationsMenu + "Fast controller asset graph";
        private const string ConsolidatedAssetContainerMenu =
            OptimizationsMenu + "Consolidate generated asset files";
        private const string BlendshapeBindingCacheMenu =
            OptimizationsMenu + "Cache blendshape controller bindings";
        private const string SpsCoveredRendererMenu =
            OptimizationsMenu + "Skip covered SPS mesh probes";
        private const string SpsMaterialProbeCacheMenu =
            OptimizationsMenu + "Cache DPS-TPS material probes";
        private const string ControllerParameterIndexMenu =
            OptimizationsMenu + "Controller parameter index";
        private const string LayerToTreeLayerIndexMenu =
            OptimizationsMenu + "Layer-to-tree layer index";
        private const string TrackingBehaviourIndexMenu =
            OptimizationsMenu + "Tracking behaviour index";
        private const string BehaviourContainerFilterMenu =
            OptimizationsMenu + "Filter irrelevant behaviour containers";
        private const string DeduplicateGeneratedClipsMenu =
            OptimizationsMenu + "Deduplicate generated animation clips";
        private const string OrderedPathsMenu =
            OptimizationsMenu + "Ordered animation path rewrite";
        private const string EmptyDeferredRewriteMenu =
            OptimizationsMenu + "Skip empty deferred rewrite";
        private const string DetailedProfilingMenu = ProfilingMenu + "Detailed internal timings";

        private static readonly (string Key, bool Recommended)[] OptimizationDefaults = {
            (OptimizeOrderedPathsKey, true),
            (SkipEmptyDeferredRewriteKey, true),
            (ConstraintIndexKey, true),
            (PhysboneIndexKey, true),
            (SkinIndexKey, true),
            (DestroyIndexKey, true),
            (SkipArmatureDebugInfoKey, true),
            (FastArmatureMoveKey, true),
            (LayerToTreeLayerIndexKey, true),
            (TrackingBehaviourIndexKey, true),
            (BehaviourContainerFilterKey, true),
            (DeduplicateGeneratedClipsKey, true),
            (SkipTransformAssetScanKey, false),
            (SkipDuplicateRendererAssetScanKey, false),
            (RetainSaveAssetsBatchingKey, true),
            (FastSaveAssetDiscoveryKey, true),
            (FastControllerAssetGraphKey, true),
            (ConsolidatedAssetContainerKey, true),
            (BlendshapeBindingCacheKey, true),
            (SpsCoveredRendererKey, true),
            (SpsMaterialProbeCacheKey, true),
            (ControllerParameterIndexKey, true)
        };

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
            foreach (var (key, recommended) in OptimizationDefaults) {
                EditorPrefs.SetBool(key, recommended);
            }
        }

        [MenuItem("Tools/QuickFury/Disable all optimizations", false, 2)]
        private static void DisableAllOptimizations() {
            foreach (var (key, _) in OptimizationDefaults) {
                EditorPrefs.SetBool(key, false);
            }
        }

        [MenuItem(ConstraintIndexMenu)]
        private static void ToggleConstraintIndex() => Toggle(ConstraintIndexKey, ConstraintIndex);

        [MenuItem(ConstraintIndexMenu, true)]
        private static bool ValidateConstraintIndex() => ValidateOptimization(ConstraintIndexMenu, ConstraintIndex);

        [MenuItem(PhysboneIndexMenu)]
        private static void TogglePhysboneIndex() => Toggle(PhysboneIndexKey, PhysboneIndex);

        [MenuItem(PhysboneIndexMenu, true)]
        private static bool ValidatePhysboneIndex() => ValidateOptimization(PhysboneIndexMenu, PhysboneIndex);

        [MenuItem(SkinIndexMenu)]
        private static void ToggleSkinIndex() => Toggle(SkinIndexKey, SkinIndex);

        [MenuItem(SkinIndexMenu, true)]
        private static bool ValidateSkinIndex() => ValidateOptimization(SkinIndexMenu, SkinIndex);

        [MenuItem(DestroyIndexMenu)]
        private static void ToggleDestroyIndex() => Toggle(DestroyIndexKey, DestroyIndex);

        [MenuItem(DestroyIndexMenu, true)]
        private static bool ValidateDestroyIndex() => ValidateOptimization(DestroyIndexMenu, DestroyIndex);

        [MenuItem(SkipArmatureDebugInfoMenu)]
        private static void ToggleSkipArmatureDebugInfo() =>
            Toggle(SkipArmatureDebugInfoKey, SkipArmatureDebugInfo);

        [MenuItem(SkipArmatureDebugInfoMenu, true)]
        private static bool ValidateSkipArmatureDebugInfo() =>
            ValidateOptimization(SkipArmatureDebugInfoMenu, SkipArmatureDebugInfo);

        [MenuItem(FastArmatureMoveMenu)]
        private static void ToggleFastArmatureMove() => Toggle(FastArmatureMoveKey, FastArmatureMove);

        [MenuItem(FastArmatureMoveMenu, true)]
        private static bool ValidateFastArmatureMove() =>
            ValidateOptimization(FastArmatureMoveMenu, FastArmatureMove);

        [MenuItem(SkipTransformAssetScanMenu)]
        private static void ToggleSkipTransformAssetScan() =>
            Toggle(SkipTransformAssetScanKey, SkipTransformAssetScan);

        [MenuItem(SkipTransformAssetScanMenu, true)]
        private static bool ValidateSkipTransformAssetScan() =>
            ValidateOptimization(SkipTransformAssetScanMenu, SkipTransformAssetScan);

        [MenuItem(SkipDuplicateRendererAssetScanMenu)]
        private static void ToggleSkipDuplicateRendererAssetScan() =>
            Toggle(SkipDuplicateRendererAssetScanKey, SkipDuplicateRendererAssetScan);

        [MenuItem(SkipDuplicateRendererAssetScanMenu, true)]
        private static bool ValidateSkipDuplicateRendererAssetScan() =>
            ValidateOptimization(SkipDuplicateRendererAssetScanMenu, SkipDuplicateRendererAssetScan);

        [MenuItem(RetainSaveAssetsBatchingMenu)]
        private static void ToggleRetainSaveAssetsBatching() =>
            Toggle(RetainSaveAssetsBatchingKey, RetainSaveAssetsBatching);

        [MenuItem(RetainSaveAssetsBatchingMenu, true)]
        private static bool ValidateRetainSaveAssetsBatching() {
            return Validate(
                RetainSaveAssetsBatchingMenu,
                RetainSaveAssetsBatching,
                QuickFuryBootstrap.OptimizationCompatible && Application.unityVersion.StartsWith("2022.")
            );
        }

        [MenuItem(FastSaveAssetDiscoveryMenu)]
        private static void ToggleFastSaveAssetDiscovery() =>
            Toggle(FastSaveAssetDiscoveryKey, FastSaveAssetDiscovery);

        [MenuItem(FastSaveAssetDiscoveryMenu, true)]
        private static bool ValidateFastSaveAssetDiscovery() =>
            ValidateOptimization(FastSaveAssetDiscoveryMenu, FastSaveAssetDiscovery);

        [MenuItem(FastControllerAssetGraphMenu)]
        private static void ToggleFastControllerAssetGraph() =>
            Toggle(FastControllerAssetGraphKey, FastControllerAssetGraph);

        [MenuItem(FastControllerAssetGraphMenu, true)]
        private static bool ValidateFastControllerAssetGraph() =>
            ValidateOptimization(FastControllerAssetGraphMenu, FastControllerAssetGraph);

        [MenuItem(ConsolidatedAssetContainerMenu)]
        private static void ToggleConsolidatedAssetContainer() =>
            Toggle(ConsolidatedAssetContainerKey, ConsolidatedAssetContainer);

        [MenuItem(ConsolidatedAssetContainerMenu, true)]
        private static bool ValidateConsolidatedAssetContainer() =>
            ValidateOptimization(ConsolidatedAssetContainerMenu, ConsolidatedAssetContainer);

        [MenuItem(BlendshapeBindingCacheMenu)]
        private static void ToggleBlendshapeBindingCache() =>
            Toggle(BlendshapeBindingCacheKey, BlendshapeBindingCache);

        [MenuItem(BlendshapeBindingCacheMenu, true)]
        private static bool ValidateBlendshapeBindingCache() =>
            ValidateOptimization(BlendshapeBindingCacheMenu, BlendshapeBindingCache);

        [MenuItem(SpsCoveredRendererMenu)]
        private static void ToggleSpsCoveredRenderer() => Toggle(SpsCoveredRendererKey, SpsCoveredRenderer);

        [MenuItem(SpsCoveredRendererMenu, true)]
        private static bool ValidateSpsCoveredRenderer() =>
            ValidateOptimization(SpsCoveredRendererMenu, SpsCoveredRenderer);

        [MenuItem(SpsMaterialProbeCacheMenu)]
        private static void ToggleSpsMaterialProbeCache() =>
            Toggle(SpsMaterialProbeCacheKey, SpsMaterialProbeCache);

        [MenuItem(SpsMaterialProbeCacheMenu, true)]
        private static bool ValidateSpsMaterialProbeCache() =>
            ValidateOptimization(SpsMaterialProbeCacheMenu, SpsMaterialProbeCache);

        [MenuItem(ControllerParameterIndexMenu)]
        private static void ToggleControllerParameterIndex() =>
            Toggle(ControllerParameterIndexKey, ControllerParameterIndex);

        [MenuItem(ControllerParameterIndexMenu, true)]
        private static bool ValidateControllerParameterIndex() =>
            ValidateOptimization(ControllerParameterIndexMenu, ControllerParameterIndex);

        [MenuItem(LayerToTreeLayerIndexMenu)]
        private static void ToggleLayerToTreeLayerIndex() =>
            Toggle(LayerToTreeLayerIndexKey, LayerToTreeLayerIndex);

        [MenuItem(LayerToTreeLayerIndexMenu, true)]
        private static bool ValidateLayerToTreeLayerIndex() =>
            ValidateOptimization(LayerToTreeLayerIndexMenu, LayerToTreeLayerIndex);

        [MenuItem(TrackingBehaviourIndexMenu)]
        private static void ToggleTrackingBehaviourIndex() =>
            Toggle(TrackingBehaviourIndexKey, TrackingBehaviourIndex);

        [MenuItem(TrackingBehaviourIndexMenu, true)]
        private static bool ValidateTrackingBehaviourIndex() =>
            ValidateOptimization(TrackingBehaviourIndexMenu, TrackingBehaviourIndex);

        [MenuItem(BehaviourContainerFilterMenu)]
        private static void ToggleBehaviourContainerFilter() =>
            Toggle(BehaviourContainerFilterKey, BehaviourContainerFilter);

        [MenuItem(BehaviourContainerFilterMenu, true)]
        private static bool ValidateBehaviourContainerFilter() =>
            ValidateOptimization(BehaviourContainerFilterMenu, BehaviourContainerFilter);

        [MenuItem(DeduplicateGeneratedClipsMenu)]
        private static void ToggleDeduplicateGeneratedClips() =>
            Toggle(DeduplicateGeneratedClipsKey, DeduplicateGeneratedClips);

        [MenuItem(DeduplicateGeneratedClipsMenu, true)]
        private static bool ValidateDeduplicateGeneratedClips() =>
            ValidateOptimization(DeduplicateGeneratedClipsMenu, DeduplicateGeneratedClips);

        [MenuItem(OrderedPathsMenu)]
        private static void ToggleOrderedPaths() => Toggle(OptimizeOrderedPathsKey, OptimizeOrderedPaths);

        [MenuItem(OrderedPathsMenu, true)]
        private static bool ValidateOrderedPaths() =>
            ValidateOptimization(OrderedPathsMenu, OptimizeOrderedPaths);

        [MenuItem(EmptyDeferredRewriteMenu)]
        private static void ToggleEmptyDeferredRewrite() =>
            Toggle(SkipEmptyDeferredRewriteKey, SkipEmptyDeferredRewrite);

        [MenuItem(EmptyDeferredRewriteMenu, true)]
        private static bool ValidateEmptyDeferredRewrite() =>
            ValidateOptimization(EmptyDeferredRewriteMenu, SkipEmptyDeferredRewrite);

        [MenuItem(DetailedProfilingMenu)]
        private static void ToggleDetailedProfiling() => Toggle(DetailedProfilingKey, DetailedProfiling);

        [MenuItem(DetailedProfilingMenu, true)]
        private static bool ValidateDetailedProfiling() =>
            Validate(DetailedProfilingMenu, DetailedProfiling, QuickFuryBootstrap.ProfilingAvailable);

        [MenuItem(ProfilingMenu + "Log last report")]
        private static void LogLastReport() {
            var report = QuickFuryProfilerApi.LastReport;
            Debug.Log(string.IsNullOrEmpty(report) ? "[QuickFury] No profile has completed yet." : report);
        }

        private static void Toggle(string key, bool enabled) {
            EditorPrefs.SetBool(key, !enabled);
        }

        private static bool ValidateOptimization(string menuPath, bool enabled) {
            return Validate(menuPath, enabled, QuickFuryBootstrap.OptimizationCompatible);
        }

        private static bool Validate(string menuPath, bool enabled, bool available) {
            Menu.SetChecked(menuPath, enabled);
            return available;
        }
    }
}
