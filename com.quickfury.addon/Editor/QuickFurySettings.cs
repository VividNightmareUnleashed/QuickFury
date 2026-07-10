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
        internal const string SkipTransformAssetScanKey = "com.quickfury.skipTransformAssetScan";
        internal const string SkipDuplicateRendererAssetScanKey = "com.quickfury.skipDuplicateRendererAssetScan";
        internal const string RetainSaveAssetsBatchingKey = "com.quickfury.retainSaveAssetsBatching";
        internal const string LayerToTreeLayerIndexKey = "com.quickfury.layerToTreeLayerIndex";
        internal const string DetailedProfilingKey = "com.quickfury.detailedProfiling";

        internal static bool OptimizeOrderedPaths => EditorPrefs.GetBool(OptimizeOrderedPathsKey, true);
        internal static bool SkipEmptyDeferredRewrite => EditorPrefs.GetBool(SkipEmptyDeferredRewriteKey, true);
        internal static bool ConstraintIndex => EditorPrefs.GetBool(ConstraintIndexKey, true);
        internal static bool PhysboneIndex => EditorPrefs.GetBool(PhysboneIndexKey, true);
        internal static bool SkinIndex => EditorPrefs.GetBool(SkinIndexKey, true);
        internal static bool DestroyIndex => EditorPrefs.GetBool(DestroyIndexKey, true);
        internal static bool SkipTransformAssetScan => EditorPrefs.GetBool(SkipTransformAssetScanKey, false);
        internal static bool SkipDuplicateRendererAssetScan =>
            EditorPrefs.GetBool(SkipDuplicateRendererAssetScanKey, false);
        internal static bool RetainSaveAssetsBatching => EditorPrefs.GetBool(RetainSaveAssetsBatchingKey, true);
        internal static bool LayerToTreeLayerIndex => EditorPrefs.GetBool(LayerToTreeLayerIndexKey, true);
        internal static bool DetailedProfiling => EditorPrefs.GetBool(DetailedProfilingKey, false);

        [MenuItem("Tools/QuickFury/Use recommended settings", false, 1)]
        private static void UseRecommendedSettings() {
            EditorPrefs.SetBool(OptimizeOrderedPathsKey, true);
            EditorPrefs.SetBool(SkipEmptyDeferredRewriteKey, true);
            EditorPrefs.SetBool(ConstraintIndexKey, true);
            EditorPrefs.SetBool(PhysboneIndexKey, true);
            EditorPrefs.SetBool(SkinIndexKey, true);
            EditorPrefs.SetBool(DestroyIndexKey, true);
            EditorPrefs.SetBool(LayerToTreeLayerIndexKey, true);
            EditorPrefs.SetBool(SkipTransformAssetScanKey, false);
            EditorPrefs.SetBool(SkipDuplicateRendererAssetScanKey, false);
            EditorPrefs.SetBool(RetainSaveAssetsBatchingKey, true);
        }

        [MenuItem("Tools/QuickFury/Disable all optimizations", false, 2)]
        private static void DisableAllOptimizations() {
            EditorPrefs.SetBool(OptimizeOrderedPathsKey, false);
            EditorPrefs.SetBool(SkipEmptyDeferredRewriteKey, false);
            EditorPrefs.SetBool(ConstraintIndexKey, false);
            EditorPrefs.SetBool(PhysboneIndexKey, false);
            EditorPrefs.SetBool(SkinIndexKey, false);
            EditorPrefs.SetBool(DestroyIndexKey, false);
            EditorPrefs.SetBool(LayerToTreeLayerIndexKey, false);
            EditorPrefs.SetBool(SkipTransformAssetScanKey, false);
            EditorPrefs.SetBool(SkipDuplicateRendererAssetScanKey, false);
            EditorPrefs.SetBool(RetainSaveAssetsBatchingKey, false);
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
