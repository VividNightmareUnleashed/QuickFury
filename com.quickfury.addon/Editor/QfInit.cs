using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEditor;

namespace QuickFury {
    internal static class QfInit {
        public const string HarmonyId = "com.quickfury";
        public static readonly Harmony harmony = new Harmony(HarmonyId);

        private class PatchDef {
            public string name;
            public Action<Harmony> apply;
            public string error; // null = applied
        }

        private static readonly List<PatchDef> defs = new List<PatchDef> {
            // ScopePatch must come first: it maintains QfState, which every cache below relies on.
            new PatchDef { name = "Scope tracking & profiler", apply = ScopePatch.Apply },
            new PatchDef { name = "Binding validation cache", apply = BindingValidCachePatch.Apply },
            new PatchDef { name = "PhysBone scan cache", apply = PhysbonePatch.Apply },
            new PatchDef { name = "Clip settings cache", apply = MotionsCachePatch.Apply },
            new PatchDef { name = "Fast VFGameObject hashing", apply = HashCodePatch.Apply },
            new PatchDef { name = "Param name fast path", apply = ParamNamePatch.Apply },
            new PatchDef { name = "Move & path rewrite fast path", apply = MoveCachePatch.Apply },
            new PatchDef { name = "Skin rewrite cache", apply = SkinRewritePatch.Apply },
            new PatchDef { name = "Progress window throttle", apply = ProgressThrottlePatch.Apply },
            new PatchDef { name = "Sub-profiler (hot internals)", apply = SubProfilerPatch.Apply },
            new PatchDef { name = "Skip debug info in play mode", apply = SkipDebugInfoPatch.Apply },
            new PatchDef { name = "Destroy scan cache", apply = DestroyCachePatch.Apply },
            new PatchDef { name = "Constraints scan cache", apply = ConstraintsCachePatch.Apply },
            new PatchDef { name = "Layer map cache", apply = LayerMapCachePatch.Apply },
            new PatchDef { name = "Batched asset saving", apply = BatchedSavePatch.Apply },
        };

        private static bool patched;

        [InitializeOnLoadMethod]
        private static void Init() {
            AssemblyReloadEvents.beforeAssemblyReload += () => harmony.UnpatchAll(HarmonyId);
            // delayCall so every other InitializeOnLoad (including VRCFury's) has finished first.
            EditorApplication.delayCall += () => {
                if (QfSettings.Enabled) ApplyAll();
            };
        }

        public static void ApplyAll() {
            if (patched) return;
            if (AccessTools.TypeByName("VF.Builder.VRCFuryBuilder") == null) {
                QfLog.Info("VRCFury not found in this project — QuickFury is inactive.");
                return;
            }
            var ok = 0;
            foreach (var def in defs) {
                try {
                    def.apply(harmony);
                    def.error = null;
                    ok++;
                } catch (Exception e) {
                    def.error = e.Message;
                }
            }
            patched = true;
            if (ok == defs.Count) {
                QfLog.Info($"active — {ok}/{defs.Count} patches applied.");
            } else {
                var failures = defs.Where(d => d.error != null)
                    .Select(d => $"  {d.name}: {d.error}");
                QfLog.Warn(
                    $"active — {ok}/{defs.Count} patches applied. The following were skipped (usually a VRCFury " +
                    $"version mismatch; VRCFury behaves as stock for these):\n" + string.Join("\n", failures));
            }
        }

        public static void RemoveAll() {
            harmony.UnpatchAll(HarmonyId);
            patched = false;
            QfLog.Info("all patches removed — VRCFury is back to stock behavior.");
        }

        public static string StatusReport() {
            if (!patched) return "QuickFury is not patched in (master toggle off, or VRCFury missing).";
            return string.Join("\n", defs.Select(d => {
                var state = d.error == null ? "applied" : "SKIPPED: " + d.error;
                return $"{d.name}: {state}";
            }));
        }
    }

    internal static class QfMenu {
        private const string Root = "Tools/QuickFury/";
        private const string MEnabled = Root + "Enabled";
        private const string MProfiler = Root + "Profiler Report";
        private const string MBinding = Root + "Patches/Binding Validation Cache";
        private const string MPhysbone = Root + "Patches/PhysBone Scan Cache";
        private const string MClipSettings = Root + "Patches/Clip Settings Cache";
        private const string MHashCode = Root + "Patches/Fast VFGameObject Hashing";
        private const string MParamName = Root + "Patches/Param Name Fast Path";
        private const string MMove = Root + "Patches/Move + Path Rewrite Fast Path";
        private const string MSkin = Root + "Patches/Skin Rewrite Cache";
        private const string MProgress = Root + "Patches/Progress Window Throttle";
        private const string MSkipDebug = Root + "Patches/Skip Debug Info In Play Mode";
        private const string MDestroy = Root + "Patches/Destroy Scan Cache";
        private const string MConstraints = Root + "Patches/Constraints Scan Cache";
        private const string MLayerMap = Root + "Patches/Layer Map Cache";
        private const string MBatchedSave = Root + "Patches/Batched Asset Saving";
        private const string MStatus = Root + "Print Status";

        [MenuItem(MEnabled, priority = 0)]
        private static void ToggleEnabled() {
            QfSettings.Enabled = !QfSettings.Enabled;
            if (QfSettings.Enabled) QfInit.ApplyAll();
            else QfInit.RemoveAll();
        }
        [MenuItem(MEnabled, true)]
        private static bool ToggleEnabledValidate() { Menu.SetChecked(MEnabled, QfSettings.Enabled); return true; }

        [MenuItem(MProfiler, priority = 1)]
        private static void ToggleProfiler() => QfSettings.Profiler = !QfSettings.Profiler;
        [MenuItem(MProfiler, true)]
        private static bool ToggleProfilerValidate() { Menu.SetChecked(MProfiler, QfSettings.Profiler); return true; }

        [MenuItem(MBinding)] private static void TB() => QfSettings.BindingCache = !QfSettings.BindingCache;
        [MenuItem(MBinding, true)] private static bool VB() { Menu.SetChecked(MBinding, QfSettings.BindingCache); return true; }

        [MenuItem(MPhysbone)] private static void TP() => QfSettings.PhysboneCache = !QfSettings.PhysboneCache;
        [MenuItem(MPhysbone, true)] private static bool VP() { Menu.SetChecked(MPhysbone, QfSettings.PhysboneCache); return true; }

        [MenuItem(MClipSettings)] private static void TC() => QfSettings.ClipSettingsCache = !QfSettings.ClipSettingsCache;
        [MenuItem(MClipSettings, true)] private static bool VC() { Menu.SetChecked(MClipSettings, QfSettings.ClipSettingsCache); return true; }

        [MenuItem(MHashCode)] private static void TH() => QfSettings.HashCodeFix = !QfSettings.HashCodeFix;
        [MenuItem(MHashCode, true)] private static bool VH() { Menu.SetChecked(MHashCode, QfSettings.HashCodeFix); return true; }

        [MenuItem(MParamName)] private static void TN() => QfSettings.ParamNameFastPath = !QfSettings.ParamNameFastPath;
        [MenuItem(MParamName, true)] private static bool VN() { Menu.SetChecked(MParamName, QfSettings.ParamNameFastPath); return true; }

        [MenuItem(MMove)] private static void TM() => QfSettings.MoveCache = !QfSettings.MoveCache;
        [MenuItem(MMove, true)] private static bool VM() { Menu.SetChecked(MMove, QfSettings.MoveCache); return true; }

        [MenuItem(MSkin)] private static void TS() => QfSettings.SkinRewriteCache = !QfSettings.SkinRewriteCache;
        [MenuItem(MSkin, true)] private static bool VS() { Menu.SetChecked(MSkin, QfSettings.SkinRewriteCache); return true; }

        [MenuItem(MProgress)] private static void TT() => QfSettings.ProgressThrottle = !QfSettings.ProgressThrottle;
        [MenuItem(MProgress, true)] private static bool VT() { Menu.SetChecked(MProgress, QfSettings.ProgressThrottle); return true; }

        [MenuItem(MSkipDebug)] private static void TD() => QfSettings.SkipPlayModeDebugInfo = !QfSettings.SkipPlayModeDebugInfo;
        [MenuItem(MSkipDebug, true)] private static bool VD() { Menu.SetChecked(MSkipDebug, QfSettings.SkipPlayModeDebugInfo); return true; }

        [MenuItem(MDestroy)] private static void TX() => QfSettings.DestroyCache = !QfSettings.DestroyCache;
        [MenuItem(MDestroy, true)] private static bool VX() { Menu.SetChecked(MDestroy, QfSettings.DestroyCache); return true; }

        [MenuItem(MConstraints)] private static void TQ() => QfSettings.ConstraintsCache = !QfSettings.ConstraintsCache;
        [MenuItem(MConstraints, true)] private static bool VQ() { Menu.SetChecked(MConstraints, QfSettings.ConstraintsCache); return true; }

        [MenuItem(MLayerMap)] private static void TL() => QfSettings.LayerMapCache = !QfSettings.LayerMapCache;
        [MenuItem(MLayerMap, true)] private static bool VL() { Menu.SetChecked(MLayerMap, QfSettings.LayerMapCache); return true; }

        [MenuItem(MBatchedSave)] private static void TW() => QfSettings.BatchedSave = !QfSettings.BatchedSave;
        [MenuItem(MBatchedSave, true)] private static bool VW() { Menu.SetChecked(MBatchedSave, QfSettings.BatchedSave); return true; }

        private const string MProfilerOnly = Root + "Profiler Only (Disable All Optimizations)";
        private const string MAllOn = Root + "Enable All Optimizations";

        [MenuItem(MProfilerOnly, priority = 50)]
        private static void ProfilerOnly() {
            QfSettings.SetAllOptimizations(false);
            QfSettings.Profiler = true;
            if (!QfSettings.Enabled) { QfSettings.Enabled = true; QfInit.ApplyAll(); }
            QfLog.Info("profiler-only mode: all optimizations OFF, profiler ON. Bakes now run at stock speed with timing reports.");
        }

        [MenuItem(MAllOn, priority = 51)]
        private static void AllOn() {
            QfSettings.SetAllOptimizations(true);
            QfSettings.Profiler = true;
            if (!QfSettings.Enabled) { QfSettings.Enabled = true; QfInit.ApplyAll(); }
            QfLog.Info("all optimizations ON.");
        }

        [MenuItem(MStatus, priority = 100)]
        private static void PrintStatus() => QfLog.Info("status:\n" + QfInit.StatusReport());
    }
}
