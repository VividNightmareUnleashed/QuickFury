using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEditor;

namespace QuickFury {
    /**
     * Fine-grained timing of known-hot VRCFury internals, reported as a "Hot internals" section in
     * the build report. Times are INCLUSIVE and may overlap (e.g. Move includes RemoveFromPhysbones,
     * ApplyOne includes Move) — this is attribution data, not a sum.
     *
     * Each target is patched independently; a missing method (VRCFury version drift) just drops
     * that row. Timing prefixes run at Priority.First so they still measure methods whose other
     * QuickFury prefixes skip the original (cache hits then record ~0ms, which is accurate).
     */
    internal static class SubProfilerPatch {
        private class Agg {
            public double ms;
            public int count;
        }

        private static readonly Dictionary<MethodBase, Agg> byMethod = new Dictionary<MethodBase, Agg>();
        private static readonly Dictionary<MethodBase, string> names = new Dictionary<MethodBase, string>();

        private static readonly (string type, string method, Type[] args)[] targets = {
            ("VF.Service.ArmatureLinkService", "ApplyOne", null),
            ("VF.Service.ArmatureLinkService", "GetUsageReasons", null),
            ("VF.Service.ArmatureLinkService", "GetLinks", null),
            ("VF.Service.ArmatureLinkService", "RewriteSkins", null),
            ("VF.Service.FindAnimatedTransformsService", "Find", null),
            ("VF.Service.ObjectMoveService", "Move", null),
            ("VF.Service.ObjectMoveService", "ApplyDeferred", null),
            ("VF.Utils.GameObjects", "Create", null),
            ("VF.Utils.PhysboneUtils", "RemoveFromPhysbones", null),
            ("VF.Utils.AnimationClipExtensions", "FinalizeAsset", null),
            ("VF.Utils.VRCFuryAssetDatabase", "SaveAsset", new[] { typeof(UnityEngine.Object), typeof(string) }),
            ("VF.Service.LayerToTreeService", "GetBindingsAnimatedInLayer", null),
            ("VF.Service.LayerToTreeService", "OptimizeLayer", null),
            ("VF.Service.LayerToTreeService", "GetTransitionsTo", null),
            ("VF.Service.ValidateBindingsService", "IsValid", new[] { typeof(EditorCurveBinding) }),
            ("VF.Utils.VFGameObject", "Destroy", null),
            ("VF.Utils.VFGameObject", "GetConstraints", null),
            ("VF.Utils.VFGameObject", "GetPath", null),
            ("VF.Service.FindAnimatedTransformsService+AnimatedTransforms", "GetDebugSources", null),
        };

        public static void Apply(Harmony h) {
            var prefix = new HarmonyMethod(typeof(SubProfilerPatch), nameof(Prefix)) { priority = Priority.First };
            var finalizer = new HarmonyMethod(typeof(SubProfilerPatch), nameof(Finalizer));
            var applied = 0;
            foreach (var (typeName, methodName, args) in targets) {
                try {
                    var type = QfReflect.ReqType(typeName);
                    var m = QfReflect.ReqMethod(type, methodName, args);
                    h.Patch(m, prefix: prefix, finalizer: finalizer);
                    names[m] = type.Name + "." + methodName;
                    applied++;
                } catch {
                    // fine — that row is just absent from the report
                }
            }
            if (applied == 0) throw new Exception("no sub-profiler targets resolved");
        }

        public static void Reset() {
            byMethod.Clear();
        }

        public static IEnumerable<string> ReportLines() {
            return byMethod
                .OrderByDescending(p => p.Value.ms)
                .Where(p => p.Value.ms >= 1)
                .Select(p => {
                    var name = names.TryGetValue(p.Key, out var n)
                        ? n
                        : (p.Key.DeclaringType?.Name + "." + p.Key.Name);
                    return $"  {p.Value.ms,8:F0} ms  ×{p.Value.count,-5} {name}";
                });
        }

        private static void Prefix(ref long __state) {
            __state = Stopwatch.GetTimestamp();
        }

        private static void Finalizer(MethodBase __originalMethod, long __state) {
            if (__state == 0) return; // our prefix was skipped by another prefix's short-circuit
            if (!QfSettings.Profiler || !QfState.InBuild) return;
            var ms = (Stopwatch.GetTimestamp() - __state) * 1000.0 / Stopwatch.Frequency;
            if (!byMethod.TryGetValue(__originalMethod, out var agg)) byMethod[__originalMethod] = agg = new Agg();
            agg.ms += ms;
            agg.count++;
        }
    }
}
