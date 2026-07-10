using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using QuickFury.Optimization;

namespace QuickFury {
    /// <summary>
    /// Replaces only the compiler-generated path-mapping lambda inside
    /// ObjectMoveService.ApplyDeferred. Controller/clip traversal remains VRCFury's own code.
    /// </summary>
    internal static class OrderedPathRewritePatch {
        [ThreadStatic] private static object activeService;
        [ThreadStatic] private static OrderedPathResolver activeResolver;

        private static VrcfuryCompatibility compatibility;

        internal static void Install(Harmony harmony, VrcfuryCompatibility targets) {
            compatibility = targets;

            harmony.Patch(
                targets.ApplyDeferred,
                prefix: new HarmonyMethod(typeof(OrderedPathRewritePatch), nameof(BeginDeferredRewrite)),
                finalizer: new HarmonyMethod(typeof(OrderedPathRewritePatch), nameof(EndDeferredRewrite))
            );
            harmony.Patch(
                targets.ApplyDeferredPathLambda,
                prefix: new HarmonyMethod(typeof(OrderedPathRewritePatch), nameof(RewritePath))
            );
        }

        private static bool BeginDeferredRewrite(object __instance) {
            activeService = null;
            activeResolver = null;

            if (!QuickFurySettings.OptimizeOrderedPaths && !QuickFurySettings.SkipEmptyDeferredRewrite) {
                return true;
            }

            var moves = ReadMoves(__instance);
            if (moves.Count == 0 && QuickFurySettings.SkipEmptyDeferredRewrite) {
                // The original implementation performs a complete identity rewrite of every managed
                // clip and mask, then clears an already-empty list.
                return false;
            }

            if (QuickFurySettings.OptimizeOrderedPaths && moves.Count >= 2) {
                activeService = __instance;
                activeResolver = new OrderedPathResolver(moves);
            }

            return true;
        }

        private static Exception EndDeferredRewrite(object __instance, Exception __exception) {
            if (ReferenceEquals(activeService, __instance)) {
                activeService = null;
                activeResolver = null;
            }
            return __exception;
        }

        private static bool RewritePath(object __instance, string __0, ref string __result) {
            if (!ReferenceEquals(activeService, __instance) || activeResolver == null) {
                return true;
            }

            __result = activeResolver.Rewrite(__0);
            return false;
        }

        private static List<(string from, string to)> ReadMoves(object service) {
            var output = new List<(string from, string to)>();
            if (service == null || compatibility?.DeferredMoves == null) return output;

            var raw = compatibility.DeferredMoves.GetValue(service) as IEnumerable;
            if (raw == null) return output;

            foreach (var item in raw) {
                if (item is ValueTuple<string, string> move) {
                    output.Add((move.Item1, move.Item2));
                }
            }
            return output;
        }
    }
}
