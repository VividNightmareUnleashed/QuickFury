using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace QuickFury {
    /// <summary>
    /// BlendshapeOptimizerBuilder used to enumerate every clip and float curve in
    /// every controller once for every skinned mesh. The controller graph is not
    /// mutated during this action, so cache the exact GetBindings result for each
    /// (owner, controller) pair for the duration of Apply.
    /// </summary>
    internal static class BlendshapeBindingCachePatch {
        private sealed class CacheKey : IEquatable<CacheKey> {
            internal readonly object Owner;
            internal readonly object Controller;

            internal CacheKey(object owner, object controller) {
                Owner = owner;
                Controller = controller;
            }

            public bool Equals(CacheKey other) {
                return other != null
                       && ReferenceEquals(Owner, other.Owner)
                       && ReferenceEquals(Controller, other.Controller);
            }

            public override bool Equals(object obj) {
                return Equals(obj as CacheKey);
            }

            public override int GetHashCode() {
                unchecked {
                    return (RuntimeHelpers.GetHashCode(Owner) * 397)
                           ^ RuntimeHelpers.GetHashCode(Controller);
                }
            }
        }

        private sealed class Context {
            internal readonly Dictionary<CacheKey, ICollection<(EditorCurveBinding, AnimationCurve)>> Results =
                new Dictionary<CacheKey, ICollection<(EditorCurveBinding, AnimationCurve)>>();
        }

        [ThreadStatic] private static Context active;

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            var type = VrcfuryCompatibility.FindType("VF.Feature.BlendshapeOptimizerBuilder");
            var apply = type?.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "Apply"
                                           && method.ReturnType == typeof(void)
                                           && method.GetParameters().Length == 0);
            var getBindings = type?.GetMethods(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                )
                .SingleOrDefault(method => method.Name == "GetBindings"
                                           && method.GetParameters().Length == 2);

            if (apply == null || getBindings == null) {
                Debug.LogWarning("[QuickFury] Blendshape binding cache disabled: target signature mismatch.");
                return;
            }

            try {
                harmony.Patch(
                    apply,
                    prefix: new HarmonyMethod(typeof(BlendshapeBindingCachePatch), nameof(Begin)),
                    finalizer: new HarmonyMethod(typeof(BlendshapeBindingCachePatch), nameof(End))
                );
                harmony.Patch(
                    getBindings,
                    prefix: new HarmonyMethod(typeof(BlendshapeBindingCachePatch), nameof(GetCached)),
                    postfix: new HarmonyMethod(typeof(BlendshapeBindingCachePatch), nameof(Store))
                );
            } catch (Exception e) {
                Debug.LogWarning("[QuickFury] Blendshape binding cache disabled: " + e.Message);
            }
        }

        private static void Begin() {
            active = QuickFurySettings.BlendshapeBindingCache ? new Context() : null;
        }

        private static Exception End(Exception __exception) {
            active = null;
            return __exception;
        }

        private static bool GetCached(
            object obj,
            object controller,
            ref ICollection<(EditorCurveBinding, AnimationCurve)> __result,
            out CacheKey __state
        ) {
            __state = null;
            var context = active;
            if (context == null || obj == null || controller == null) return true;

            var key = new CacheKey(obj, controller);
            if (context.Results.TryGetValue(key, out var cached)) {
                __result = cached;
                return false;
            }

            __state = key;
            return true;
        }

        private static void Store(
            CacheKey __state,
            ICollection<(EditorCurveBinding, AnimationCurve)> __result
        ) {
            if (__state == null || __result == null) return;
            var context = active;
            if (context != null) context.Results[__state] = __result;
        }
    }
}
