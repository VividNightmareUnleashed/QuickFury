using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace QuickFury {
    /// <summary>
    /// TrackingConflictResolverService first materializes every layer's complete
    /// immutable behaviour set to discover contributors, then asks every layer to
    /// rewrite tracking controls. VFLayer rebuilds the recursive behaviour-container
    /// graph for both operations, including for layers already proven irrelevant.
    /// Keep that graph for the duration of Apply and skip the second traversal when
    /// the discovery pass found no tracking controls on a layer.
    /// </summary>
    internal static class TrackingBehaviourIndexPatch {
        private sealed class Context {
            internal readonly Dictionary<object, object> Containers =
                new Dictionary<object, object>();
            internal readonly Dictionary<object, object> EmptyContainers =
                new Dictionary<object, object>();
            internal readonly Dictionary<object, bool> HasTrackingControl =
                new Dictionary<object, bool>();
        }

        [ThreadStatic] private static Context active;
        private static Type trackingControlType;
        private static FieldInfo stateMachineField;

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            var serviceType = VrcfuryCompatibility.FindType("VF.Service.TrackingConflictResolverService");
            var layerType = VrcfuryCompatibility.FindType("VF.Utils.Controller.VFLayer");
            trackingControlType = VrcfuryCompatibility.FindType(
                "VRC.SDK3.Avatars.Components.VRCAnimatorTrackingControl"
            );

            var apply = serviceType?
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "Apply"
                                           && method.ReturnType == typeof(void)
                                           && method.GetParameters().Length == 0);
            var containerGetter = layerType?
                .GetProperty("allBehaviourContainers", BindingFlags.Instance | BindingFlags.NonPublic)?
                .GetGetMethod(true);
            var behaviourGetter = layerType?
                .GetProperty(
                    "allBehaviours",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                )?
                .GetGetMethod(true);
            stateMachineField = layerType?.GetField(
                "rootStateMachine",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            if (apply == null || containerGetter == null || behaviourGetter == null
                              || trackingControlType == null || stateMachineField == null) {
                Debug.LogWarning("[QuickFury] Tracking behaviour index disabled: target signature mismatch.");
                return;
            }

            try {
                var cachedPrefix = typeof(TrackingBehaviourIndexPatch)
                    .GetMethod(nameof(GetCachedContainers), BindingFlags.Static | BindingFlags.NonPublic)
                    ?.MakeGenericMethod(containerGetter.ReturnType);
                var storePostfix = typeof(TrackingBehaviourIndexPatch)
                    .GetMethod(nameof(StoreContainers), BindingFlags.Static | BindingFlags.NonPublic)
                    ?.MakeGenericMethod(containerGetter.ReturnType);
                if (cachedPrefix == null || storePostfix == null) {
                    throw new MissingMethodException("Unable to close container cache patch methods.");
                }
                harmony.Patch(
                    apply,
                    prefix: new HarmonyMethod(typeof(TrackingBehaviourIndexPatch), nameof(Begin)),
                    finalizer: new HarmonyMethod(typeof(TrackingBehaviourIndexPatch), nameof(End))
                );
                harmony.Patch(
                    containerGetter,
                    prefix: new HarmonyMethod(cachedPrefix),
                    postfix: new HarmonyMethod(storePostfix)
                );
                harmony.Patch(
                    behaviourGetter,
                    postfix: new HarmonyMethod(
                        typeof(TrackingBehaviourIndexPatch),
                        nameof(RecordBehaviourTypes)
                    )
                );
            } catch (Exception e) {
                active = null;
                Debug.LogWarning("[QuickFury] Tracking behaviour index disabled: " + e.Message);
            }
        }

        private static void Begin() {
            active = QuickFurySettings.TrackingBehaviourIndex ? new Context() : null;
        }

        private static Exception End(Exception __exception) {
            active = null;
            return __exception;
        }

        private static bool GetCachedContainers<T>(object __instance, ref T __result) {
            var context = active;
            if (context == null) {
                return BehaviourContainerFilterPatch.Filter(__instance, ref __result);
            }
            if (__instance == null) return true;
            var key = stateMachineField.GetValue(__instance);
            if (key == null) return true;

            if (!context.Containers.TryGetValue(key, out var cached)) return true;
            if (context.HasTrackingControl.TryGetValue(key, out var hasTrackingControl)
                && !hasTrackingControl
                && context.EmptyContainers.TryGetValue(key, out var empty)) {
                cached = empty;
            }
            __result = (T)cached;
            return false;
        }

        private static void StoreContainers<T>(object __instance, T __result) {
            var context = active;
            if (context == null || __instance == null || ReferenceEquals(__result, null)) return;
            var key = stateMachineField.GetValue(__instance);
            if (key != null) context.Containers[key] = __result;
        }

        private static void RecordBehaviourTypes(object __instance, object __result) {
            var context = active;
            if (context == null || __instance == null || !(__result is IEnumerable behaviours)) return;
            var key = stateMachineField.GetValue(__instance);
            if (key == null) return;

            var hasTrackingControl = false;
            foreach (var behaviour in behaviours) {
                if (behaviour == null || !trackingControlType.IsInstanceOfType(behaviour)) continue;
                hasTrackingControl = true;
                break;
            }
            context.HasTrackingControl[key] = hasTrackingControl;
            if (hasTrackingControl) return;
            if (!context.Containers.TryGetValue(key, out var containers)) {
                var sharedEmpty = BehaviourContainerFilterPatch.EmptyContainerSet;
                if (sharedEmpty != null) context.EmptyContainers[key] = sharedEmpty;
                return;
            }

            var clear = containers.GetType().GetMethod(
                "Clear",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null
            );
            var empty = clear?.Invoke(containers, null);
            if (empty != null) context.EmptyContainers[key] = empty;
        }
    }
}
