using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace QuickFury {
    /**
     * Always-on patch (when QuickFury is enabled). Two jobs:
     *
     * 1. Scope tracking: bumps QfState at every VRCFury preprocessor hook boundary and every
     *    FeatureBuilderAction boundary. Every QuickFury cache keys off this, which is what makes
     *    the caches safe: VRCFury mutates the avatar *between* actions, so nothing cached within
     *    one action can go stale while it's being used.
     *
     * 2. Profiler (toggle: Tools/QuickFury/Profiler Report): times every build action and every
     *    preprocessor hook. VRCFury has no timing instrumentation of its own; this report is how
     *    you find out where a slow bake actually spends its time.
     *
     * Patch targets:
     * - VF.Feature.Base.FeatureBuilderAction.Call()            (every build pass runs through this)
     * - VF.Hooks.VrcfAvatarPreprocessor.OnPreprocessAvatar()   (base class of all VRCFury SDK hooks)
     * - VF.Builder.VRCFuryBuilder.RunMain()                    (the main build; report is dumped here)
     */
    internal static class ScopePatch {
        private static MethodInfo getServiceM;
        private static MethodInfo getNameM;

        private class Agg {
            public double ms;
            public int count;
        }

        private static readonly Dictionary<string, Agg> actionTotals = new Dictionary<string, Agg>();
        private static readonly List<(string hook, double ms)> hookTimes = new List<(string, double)>();
        private static Stopwatch buildSw;
        private static string buildName = "?";
        private static long lastHookEndTicks;

        public static void Apply(Harmony h) {
            var fba = QfReflect.ReqType("VF.Feature.Base.FeatureBuilderAction");
            getServiceM = QfReflect.ReqMethod(fba, "GetService");
            getNameM = QfReflect.ReqMethod(fba, "GetName");
            h.Patch(QfReflect.ReqMethod(fba, "Call"),
                prefix: new HarmonyMethod(typeof(ScopePatch), nameof(CallPrefix)),
                finalizer: new HarmonyMethod(typeof(ScopePatch), nameof(CallFinalizer)));

            var pre = QfReflect.ReqType("VF.Hooks.VrcfAvatarPreprocessor");
            h.Patch(QfReflect.ReqMethod(pre, "OnPreprocessAvatar"),
                prefix: new HarmonyMethod(typeof(ScopePatch), nameof(HookPrefix)),
                finalizer: new HarmonyMethod(typeof(ScopePatch), nameof(HookFinalizer)));

            var builder = QfReflect.ReqType("VF.Builder.VRCFuryBuilder");
            h.Patch(QfReflect.ReqMethod(builder, "RunMain"),
                prefix: new HarmonyMethod(typeof(ScopePatch), nameof(RunMainPrefix)),
                finalizer: new HarmonyMethod(typeof(ScopePatch), nameof(RunMainFinalizer)));
        }

        private static double MsSince(long startTicks) {
            return (Stopwatch.GetTimestamp() - startTicks) * 1000.0 / Stopwatch.Frequency;
        }

        // ---- FeatureBuilderAction.Call ----

        private static void CallPrefix(ref long __state) {
            QfState.BumpScope();
            __state = Stopwatch.GetTimestamp();
        }

        private static void CallFinalizer(object __instance, long __state) {
            if (!QfSettings.Profiler) return;
            var ms = MsSince(__state);
            string key;
            try {
                var service = getServiceM.Invoke(__instance, null);
                key = service.GetType().Name + "." + (string)getNameM.Invoke(__instance, null);
            } catch {
                key = "(unknown action)";
            }
            if (!actionTotals.TryGetValue(key, out var agg)) actionTotals[key] = agg = new Agg();
            agg.ms += ms;
            agg.count++;
        }

        // ---- VrcfAvatarPreprocessor.OnPreprocessAvatar (all VRCFury SDK hooks) ----

        private static void HookPrefix(ref long __state) {
            if (QfState.hookDepth == 0) {
                // New preprocessor chain. If the previous chain never hit its natural dump point
                // (e.g. it aborted), don't let stale entries leak into this chain's report.
                if (hookTimes.Count > 0 && MsSince(lastHookEndTicks) > 10000) hookTimes.Clear();
            }
            QfState.hookDepth++;
            QfState.BumpScope();
            __state = Stopwatch.GetTimestamp();
        }

        private static void HookFinalizer(object __instance, long __state) {
            QfState.hookDepth--;
            QfState.BumpScope();
            lastHookEndTicks = Stopwatch.GetTimestamp();
            if (!QfSettings.Profiler) return;

            var ms = MsSince(__state);
            var name = __instance.GetType().Name;
            hookTimes.Add((name, ms));
            if (ms >= 100) QfLog.Info($"{name}: {ms:F0}ms");

            // ParameterCompressorHook is the last heavyweight VRCFury hook in the chain
            // (order int.MaxValue-100), so it's the natural place to dump the hook summary.
            if (name == "ParameterCompressorHook") {
                var sb = new StringBuilder();
                sb.AppendLine($"Preprocessor hook summary for '{buildName}':");
                foreach (var (hook, hookMs) in hookTimes.OrderByDescending(t => t.ms)) {
                    if (hookMs < 1) continue;
                    sb.AppendLine($"  {hookMs,8:F0} ms  {hook}");
                }
                QfLog.Info(sb.ToString().TrimEnd());
                hookTimes.Clear();
            }
        }

        // ---- VRCFuryBuilder.RunMain (the main build) ----

        private static void RunMainPrefix(object avatarObject) {
            actionTotals.Clear();
            SubProfilerPatch.Reset();
            buildSw = Stopwatch.StartNew();
            try {
                var go = QfReflect.Go(avatarObject);
                buildName = go != null ? go.name : "?";
            } catch {
                buildName = "?";
            }
        }

        private static void RunMainFinalizer() {
            if (buildSw == null) return;
            buildSw.Stop();
            if (!QfSettings.Profiler) return;

            var sb = new StringBuilder();
            sb.AppendLine($"Build report for '{buildName}' — main build took {buildSw.Elapsed.TotalSeconds:F2}s. Slowest passes:");
            var shown = 0;
            foreach (var pair in actionTotals.OrderByDescending(p => p.Value.ms)) {
                if (pair.Value.ms < 1 || shown >= 40) break;
                sb.AppendLine($"  {pair.Value.ms,8:F0} ms  ×{pair.Value.count,-4} {pair.Key}");
                shown++;
            }
            if (shown == 0) sb.AppendLine("  (no individual pass took over 1ms)");
            var subLines = SubProfilerPatch.ReportLines().ToList();
            if (subLines.Count > 0) {
                sb.AppendLine("Hot internals (inclusive, may overlap):");
                foreach (var line in subLines) sb.AppendLine(line);
            }
            QfLog.Info(sb.ToString().TrimEnd());
        }
    }
}
