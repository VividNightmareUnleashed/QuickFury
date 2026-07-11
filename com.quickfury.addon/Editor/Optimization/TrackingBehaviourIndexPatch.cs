using System;
using System.Collections;
using System.Collections.Generic;
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
            // Layers whose discovery pass proved they hold no tracking controls; these
            // return the shared empty set instead of rebuilding the container graph.
            internal readonly HashSet<object> ProvenEmpty = new HashSet<object>();
        }

        [ThreadStatic] private static Context active;
        private static Type trackingControlType;
        private static FieldInfo stateMachineField;
        private static object emptyContainerSet;

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            var serviceType = VrcfuryCompatibility.FindType("VF.Service.TrackingConflictResolverService");
            var layerType = VrcfuryCompatibility.FindType("VF.Utils.Controller.VFLayer");
            trackingControlType = VrcfuryCompatibility.FindType(
                "VRC.SDK3.Avatars.Components.VRCAnimatorTrackingControl"
            );

            var apply = VrcfuryCompatibility.FindNoArgVoid(serviceType, "Apply");
            var containerGetter = compatibility.VfLayerBehaviourContainersGetter;
            var behaviourGetter = layerType?
                .GetProperty(
                    "allBehaviours",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                )?
                .GetGetMethod(true);
            stateMachineField = compatibility.VfLayerRootStateMachine;

            if (apply == null || containerGetter == null || behaviourGetter == null
                              || trackingControlType == null || stateMachineField == null) {
                Debug.LogWarning("[QuickFury] Tracking behaviour index disabled: target signature mismatch.");
                return;
            }

            // Optional: without an empty set the graph cache still works, the discovery
            // shortcut is simply never taken.
            emptyContainerSet = VrcfuryCompatibility.CreateEmptyImmutableSet(containerGetter.ReturnType);

            try {
                harmony.Patch(
                    apply,
                    prefix: new HarmonyMethod(typeof(TrackingBehaviourIndexPatch), nameof(Begin)),
                    finalizer: new HarmonyMethod(typeof(TrackingBehaviourIndexPatch), nameof(End))
                );
                // object-typed __result instead of a MakeGenericMethod-closed prefix:
                // Harmony's shared state stores patch methods by metadata token, which
                // cannot encode generic arguments, so a closed generic patch breaks any
                // later Patch/UnpatchAll that re-reads this method's patch list.
                harmony.Patch(
                    containerGetter,
                    prefix: new HarmonyMethod(typeof(TrackingBehaviourIndexPatch), nameof(GetCachedContainers)),
                    postfix: new HarmonyMethod(typeof(TrackingBehaviourIndexPatch), nameof(StoreContainers))
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

        private static bool GetCachedContainers(object __instance, ref object __result) {
            var context = active;
            if (context == null || __instance == null) return true;
            var key = stateMachineField.GetValue(__instance);
            if (key == null) return true;

            if (context.ProvenEmpty.Contains(key)) {
                __result = emptyContainerSet;
                return false;
            }
            if (!context.Containers.TryGetValue(key, out var cached)) return true;
            __result = cached;
            return false;
        }

        private static void StoreContainers(object __instance, object __result) {
            var context = active;
            if (context == null || __instance == null || __result == null) return;
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
            if (hasTrackingControl) {
                context.ProvenEmpty.Remove(key);
            } else if (emptyContainerSet != null) {
                context.ProvenEmpty.Add(key);
            }
        }
    }
}
