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

        private static VrcfuryCompatibility compatibility;
        private static bool active;
        private static long runStarted;

        internal static void Install(Harmony harmony, VrcfuryCompatibility targets) {
            compatibility = targets;

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

            foreach (var (typeName, methodNames) in DetailedTargets) {
                foreach (var methodName in methodNames) {
                    foreach (var method in VrcfuryCompatibility.FindDeclaredMethods(typeName, methodName)) {
                        try {
                            harmony.Patch(
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
            })
        };

        private static void RunPrefix() {
            Actions.Clear();
            Methods.Clear();
            actionFrames = new Stack<Frame>();
            methodFrames = new Stack<Frame>();
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

        private static void MethodPrefix(MethodBase __originalMethod) {
            if (!active || !QuickFurySettings.DetailedProfiling) return;

            methodFrames.Push(new Frame {
                Key = __originalMethod.DeclaringType?.Name + "." + __originalMethod.Name,
                Started = Stopwatch.GetTimestamp()
            });
        }

        private static Exception MethodFinalizer(MethodBase __originalMethod, Exception __exception) {
            if (!active || !QuickFurySettings.DetailedProfiling || methodFrames == null || methodFrames.Count == 0) {
                return __exception;
            }

            var expected = __originalMethod.DeclaringType?.Name + "." + __originalMethod.Name;
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
            builder.AppendLine("Top actions (exact call duration):");
            AppendAggregates(builder, Actions, 40);

            if (QuickFurySettings.DetailedProfiling) {
                builder.AppendLine("Detailed internals (inclusive / self / calls / max):");
                AppendAggregates(builder, Methods, 80);
            }

            builder.AppendLine(
                $"Optimization flags: orderedPaths={QuickFurySettings.OptimizeOrderedPaths}, " +
                $"skipEmptyDeferred={QuickFurySettings.SkipEmptyDeferredRewrite}, " +
                $"constraintIndex={QuickFurySettings.ConstraintIndex}, " +
                $"physboneIndex={QuickFurySettings.PhysboneIndex}, " +
                $"skinIndex={QuickFurySettings.SkinIndex}, " +
                $"destroyIndex={QuickFurySettings.DestroyIndex}, " +
                $"layerIndex={QuickFurySettings.LayerToTreeLayerIndex}, " +
                $"skipTransformAssetScan={QuickFurySettings.SkipTransformAssetScan}, " +
                $"skipDuplicateRendererAssetScan={QuickFurySettings.SkipDuplicateRendererAssetScan}"
                + $", retainSaveAssetsBatching={QuickFurySettings.RetainSaveAssetsBatching}"
            );
            return builder.ToString();
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

        private static double ToMilliseconds(long ticks) {
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
