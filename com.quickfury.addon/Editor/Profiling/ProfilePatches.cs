using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace QuickFury {
    internal static class ProfilePatches {
        private sealed class Aggregate {
            internal long Count;
            internal long InclusiveTicks;
            internal long SelfTicks;
            internal long MaxTicks;
        }

        private sealed class Frame {
            internal string Key;
            internal long Started;
            internal long ChildTicks;
        }

        private static readonly Dictionary<string, Aggregate> Actions = new Dictionary<string, Aggregate>();
        private static readonly Dictionary<string, Aggregate> Methods = new Dictionary<string, Aggregate>();

        [ThreadStatic] private static Stack<Frame> actionFrames;
        [ThreadStatic] private static Stack<Frame> methodFrames;
        [ThreadStatic] private static bool detailed;

        private static VrcfuryCompatibility compatibility;
        private static Harmony harmonyInstance;
        private static bool detailedTargetsInstalled;
        private static bool active;
        private static long runStarted;

        internal static void Install(Harmony harmony, VrcfuryCompatibility targets) {
            compatibility = targets;
            harmonyInstance = harmony;
            detailedTargetsInstalled = false;

            harmony.Patch(
                targets.RunMain,
                prefix: new HarmonyMethod(typeof(ProfilePatches), nameof(RunPrefix)),
                finalizer: new HarmonyMethod(typeof(ProfilePatches), nameof(RunFinalizer))
            );
            harmony.Patch(
                targets.ActionCall,
                prefix: new HarmonyMethod(typeof(ProfilePatches), nameof(ActionPrefix)),
                finalizer: new HarmonyMethod(typeof(ProfilePatches), nameof(ActionFinalizer))
            );

            // The ~40 per-method patches put permanent Harmony trampolines on VRCFury's
            // hottest internals, so only install them while detailed profiling is wanted.
            // Turning the toggle on later installs them on the spot; turning it off stops
            // the timing per run and sheds the trampolines on the next domain reload.
            if (QuickFurySettings.DetailedProfiling) EnsureDetailedTargetsInstalled();
        }

        internal static void EnsureDetailedTargetsInstalled() {
            if (detailedTargetsInstalled || harmonyInstance == null) return;
            detailedTargetsInstalled = true;

            foreach (var (typeName, methodNames) in DetailedTargets) {
                foreach (var methodName in methodNames) {
                    foreach (var method in VrcfuryCompatibility.FindDeclaredMethods(typeName, methodName)) {
                        try {
                            harmonyInstance.Patch(
                                method,
                                prefix: new HarmonyMethod(typeof(ProfilePatches), nameof(MethodPrefix)),
                                finalizer: new HarmonyMethod(typeof(ProfilePatches), nameof(MethodFinalizer))
                            );
                        } catch (Exception e) {
                            UnityEngine.Debug.LogWarning(
                                $"[QuickFury] Could not profile {typeName}.{methodName}: {e.Message}"
                            );
                        }
                    }
                }
            }
        }

        private static readonly (string TypeName, string[] MethodNames)[] DetailedTargets = {
            ("VF.Service.ArmatureLinkService", new[] {
                "Apply", "ApplyOne", "RewriteSkins", "GetUsageReasons", "GetRootName", "GetLinks"
            }),
            ("VF.Service.ObjectMoveService", new[] { "Move", "ApplyDeferred" }),
            ("VF.Service.FindAnimatedTransformsService", new[] { "Find" }),
            ("VF.Service.AllClipsService", new[] { "RewriteAllClips" }),
            ("VF.Utils.PhysboneUtils", new[] { "RemoveFromPhysbones" }),
            ("VF.Utils.VFGameObject", new[] { "GetConstraints", "Destroy" }),
            ("VF.Service.LayerToTreeService", new[] { "Apply", "OptimizeLayer", "GetBindingsAnimatedInLayer" }),
            ("VF.Service.SaveAssetsService", new[] { "Run" }),
            ("VF.Utils.SaveAssetsSession", new[] {
                "SaveUnsavedComponentAssets", "GetUnsavedChildren", "SaveAssetAndChildren", "RecordWorkLog",
                "FlushWorkLogManifest", "WriteWorkLogManifest"
            }),
            ("VF.Utils.VRCFuryAssetDatabase", new[] {
                "SaveAsset", "AttachAsset", "CreateFolder", "GetUniquePath", "WithoutAssetEditing"
            }),
            ("VF.Utils.MutableManager", new[] {
                "ForEachChild", "ForEachChildObjectReference", "RewriteInternals"
            }),
            ("VF.Inspector.VRCFuryHapticSocketEditor", new[] { "Bake" }),
            ("VF.Builder.Haptics.SpsUpgrader", new[] { "Apply" }),
            ("VF.Service.HapticContactsService", new[] { "AddReceiver" }),
            ("VF.Service.HapticAnimContactsService", new[] { "CreateAnims" }),
            ("VF.Builder.Haptics.PlugSizeDetector", new[] { "GetAutoWorldSize" }),
            ("VF.Builder.MeshBaker", new[] { "BakeMesh" }),
            ("VF.Inspector.VRCFuryHapticPlugEditor", new[] { "GetRenderers" }),
            ("VF.Builder.Haptics.TpsConfigurer", new[] { "HasDpsOrTpsMaterial" }),
            ("VF.Builder.Haptics.PlugRendererFinder", new[] { "GetAutoRenderer" }),
            ("VF.Builder.Haptics.PlugMaskGenerator", new[] { "GetMask" })
        };

        private static void RunPrefix() {
            Actions.Clear();
            Methods.Clear();
            actionFrames = new Stack<Frame>();
            detailed = QuickFurySettings.DetailedProfiling;
            methodFrames = detailed ? new Stack<Frame>() : null;
            runStarted = Stopwatch.GetTimestamp();
            active = true;
        }

        private static Exception RunFinalizer(Exception __exception) {
            if (!active) return __exception;

            var elapsed = Stopwatch.GetTimestamp() - runStarted;
            active = false;
            QuickFuryProfilerApi.SetLastReport(BuildReport(elapsed, __exception));
            UnityEngine.Debug.Log(QuickFuryProfilerApi.LastReport);
            return __exception;
        }

        private static void ActionPrefix(object __instance) {
            if (!active) return;

            string key;
            try {
                var service = compatibility.ActionGetService.Invoke(__instance, null);
                var methodName = compatibility.ActionGetName.Invoke(__instance, null) as string ?? "?";
                key = (service?.GetType().Name ?? "?") + "." + methodName;
            } catch {
                key = "UnknownAction";
            }

            actionFrames.Push(new Frame { Key = key, Started = Stopwatch.GetTimestamp() });
        }

        private static Exception ActionFinalizer(Exception __exception) {
            if (!active || actionFrames == null || actionFrames.Count == 0) return __exception;

            var frame = actionFrames.Pop();
            var elapsed = Stopwatch.GetTimestamp() - frame.Started;
            Add(Actions, frame.Key, elapsed, elapsed);
            return __exception;
        }

        private static readonly Dictionary<MethodBase, string> MethodKeys =
            new Dictionary<MethodBase, string>();

        private static string BuildKey(MethodBase method) {
            if (!MethodKeys.TryGetValue(method, out var key)) {
                key = method.DeclaringType?.Name + "." + method.Name;
                MethodKeys[method] = key;
            }
            return actionFrames != null && actionFrames.Count > 0
                ? actionFrames.Peek().Key + " > " + key
                : key;
        }

        private static void MethodPrefix(MethodBase __originalMethod) {
            if (!active || !detailed) return;

            methodFrames.Push(new Frame {
                Key = BuildKey(__originalMethod),
                Started = Stopwatch.GetTimestamp()
            });
        }

        private static Exception MethodFinalizer(MethodBase __originalMethod, Exception __exception) {
            if (!active || !detailed || methodFrames == null || methodFrames.Count == 0) {
                return __exception;
            }

            var expected = BuildKey(__originalMethod);
            var frame = methodFrames.Pop();
            if (frame.Key != expected) {
                methodFrames.Clear();
                return __exception;
            }
            var elapsed = Stopwatch.GetTimestamp() - frame.Started;
            var self = Math.Max(0, elapsed - frame.ChildTicks);
            Add(Methods, frame.Key, elapsed, self);

            if (methodFrames.Count > 0) {
                methodFrames.Peek().ChildTicks += elapsed;
            }
            return __exception;
        }

        private static void Add(Dictionary<string, Aggregate> output, string key, long inclusive, long self) {
            if (!output.TryGetValue(key, out var aggregate)) {
                aggregate = new Aggregate();
                output[key] = aggregate;
            }
            aggregate.Count++;
            aggregate.InclusiveTicks += inclusive;
            aggregate.SelfTicks += self;
            aggregate.MaxTicks = Math.Max(aggregate.MaxTicks, inclusive);
        }

        private static string BuildReport(long elapsedTicks, Exception exception) {
            var builder = new StringBuilder();
            builder.AppendLine(
                $"[QuickFury] VRCFury profile: {ToMilliseconds(elapsedTicks):F3} ms total" +
                (exception == null ? "" : $" (failed: {exception.GetType().Name})")
            );
            if (exception != null) {
                builder.AppendLine("Failure: " + FormatException(exception));
            }
            builder.AppendLine("Top actions (exact call duration):");
            AppendAggregates(builder, Actions, 40);

            if (detailed) {
                builder.AppendLine("Detailed internals (inclusive / self / calls / max):");
                AppendAggregates(builder, Methods, 80);
            }

            builder.AppendLine(
                "Optimization flags: " + QuickFurySettings.DescribeOptimizationFlags()
                + $", controllerGraphStats={FastControllerAssetGraphPatch.LastStats}"
                + $", spsProbeStats={SpsCoveredRendererPatch.LastStats}"
                + $", behaviourFilterStats={BehaviourContainerFilterPatch.LastStats}"
            );
            return builder.ToString();
        }

        private static string FormatException(Exception exception) {
            var parts = new List<string>();
            for (var current = exception; current != null && parts.Count < 6; current = current.InnerException) {
                parts.Add(current.GetType().Name + ": " + current.Message.Replace('\r', ' ').Replace('\n', ' '));
            }
            return string.Join(" -> ", parts);
        }

        private static void AppendAggregates(
            StringBuilder builder,
            Dictionary<string, Aggregate> values,
            int limit
        ) {
            foreach (var pair in values.OrderByDescending(pair => pair.Value.InclusiveTicks).Take(limit)) {
                var value = pair.Value;
                builder.AppendLine(
                    $"{ToMilliseconds(value.InclusiveTicks),12:F3} / " +
                    $"{ToMilliseconds(value.SelfTicks),12:F3} / " +
                    $"{value.Count,7} / {ToMilliseconds(value.MaxTicks),12:F3} ms  {pair.Key}"
                );
            }
        }

        internal static double ToMilliseconds(long ticks) {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }
    }

    public static class QuickFuryProfilerApi {
        public static string LastReport { get; private set; }

        internal static void SetLastReport(string report) {
            LastReport = report ?? "";
            SessionState.SetString("QuickFury.LastProfile", LastReport);
        }
    }
}
