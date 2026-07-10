using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEditor.Animations;
using UnityEngine;

namespace QuickFury {
    /// <summary>
    /// VFLayer.GetLayerId and VFLayer.Exists both search AnimatorController.layers.
    /// LayerToTreeService calls them inside its layer-conflict nested loop, turning a
    /// cheap conflict check into thousands of repeated controller-layer scans. Keep a
    /// short-lived state-machine-to-index table while LayerToTreeService.Apply runs.
    /// </summary>
    internal static class LayerToTreeLayerIndexPatch {
        private sealed class ControllerIndex {
            internal AnimatorController Controller;
            internal readonly Dictionary<int, int> LayerByStateMachineId =
                new Dictionary<int, int>();
            // Retained across rebuilds so a wrapper for a layer removed earlier in the
            // pass can return Exists=false without forcing another controller scan.
            internal readonly HashSet<int> ObservedStateMachineIds = new HashSet<int>();
        }

        private sealed class Context {
            internal readonly Dictionary<int, ControllerIndex> Controllers =
                new Dictionary<int, ControllerIndex>();
        }

        [ThreadStatic] private static Context active;

        private static FieldInfo controllerField;
        private static FieldInfo stateMachineField;

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            var serviceType = VrcfuryCompatibility.FindType("VF.Service.LayerToTreeService");
            var layerType = VrcfuryCompatibility.FindType("VF.Utils.Controller.VFLayer");

            var apply = serviceType?
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "Apply"
                                           && method.ReturnType == typeof(void)
                                           && method.GetParameters().Length == 0);
            var getLayerId = layerType?
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "GetLayerId"
                                           && method.ReturnType == typeof(int)
                                           && method.GetParameters().Length == 0);
            var exists = layerType?
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "Exists"
                                           && method.ReturnType == typeof(bool)
                                           && method.GetParameters().Length == 0);
            var remove = layerType?
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "Remove"
                                           && method.ReturnType == typeof(void)
                                           && method.GetParameters().Length == 0);
            var move = layerType?
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "Move"
                                           && method.ReturnType == typeof(void)
                                           && method.GetParameters().Length == 1
                                           && method.GetParameters()[0].ParameterType == typeof(int));

            controllerField = layerType?.GetField("ctrl", BindingFlags.Instance | BindingFlags.NonPublic);
            stateMachineField = layerType?.GetField(
                "rootStateMachine",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            if (apply == null || getLayerId == null || exists == null || remove == null || move == null
                              || controllerField == null || stateMachineField == null) {
                Debug.LogWarning("[QuickFury] Layer-to-tree layer index disabled: target signature mismatch.");
                return;
            }

            try {
                harmony.Patch(
                    apply,
                    prefix: new HarmonyMethod(typeof(LayerToTreeLayerIndexPatch), nameof(Begin)),
                    finalizer: new HarmonyMethod(typeof(LayerToTreeLayerIndexPatch), nameof(End))
                );
                harmony.Patch(
                    getLayerId,
                    prefix: new HarmonyMethod(typeof(LayerToTreeLayerIndexPatch), nameof(GetLayerId))
                );
                harmony.Patch(
                    exists,
                    prefix: new HarmonyMethod(typeof(LayerToTreeLayerIndexPatch), nameof(Exists))
                );
                harmony.Patch(
                    remove,
                    postfix: new HarmonyMethod(typeof(LayerToTreeLayerIndexPatch), nameof(LayerOrderChanged))
                );
                harmony.Patch(
                    move,
                    postfix: new HarmonyMethod(typeof(LayerToTreeLayerIndexPatch), nameof(LayerOrderChanged))
                );
            } catch (Exception e) {
                Debug.LogWarning("[QuickFury] Layer-to-tree layer index disabled: " + e.Message);
            }
        }

        private static void Begin() {
            active = QuickFurySettings.LayerToTreeLayerIndex ? new Context() : null;
        }

        private static Exception End(Exception __exception) {
            active = null;
            return __exception;
        }

        private static bool GetLayerId(object __instance, ref int __result) {
            var context = active;
            if (context == null) return true;

            try {
                var controller = controllerField.GetValue(__instance) as AnimatorController;
                var stateMachine = stateMachineField.GetValue(__instance) as AnimatorStateMachine;
                if (controller == null || stateMachine == null) return true;

                var index = GetOrBuild(context, controller);
                var stateMachineId = stateMachine.GetInstanceID();
                if (!index.LayerByStateMachineId.TryGetValue(stateMachineId, out __result)) {
                    if (index.ObservedStateMachineIds.Contains(stateMachineId)) return true;

                    // A layer may have been appended since this controller was first seen
                    // (the direct-tree layer itself is created lazily). Rebuild on a miss.
                    Rebuild(index);
                    if (!index.LayerByStateMachineId.TryGetValue(stateMachineId, out __result)) {
                        index.ObservedStateMachineIds.Add(stateMachineId);
                        // Preserve VRCFury's exception behavior for a removed/unknown layer.
                        return true;
                    }
                }
                return false;
            } catch (Exception e) {
                active = null;
                Debug.LogWarning("[QuickFury] Layer-to-tree layer index fell back to VRCFury: " + e.Message);
                return true;
            }
        }

        private static bool Exists(object __instance, ref bool __result) {
            var context = active;
            if (context == null) return true;

            try {
                var controller = controllerField.GetValue(__instance) as AnimatorController;
                var stateMachine = stateMachineField.GetValue(__instance) as AnimatorStateMachine;
                if (controller == null || stateMachine == null) return true;

                var index = GetOrBuild(context, controller);
                var stateMachineId = stateMachine.GetInstanceID();
                if (index.LayerByStateMachineId.ContainsKey(stateMachineId)) {
                    __result = true;
                    return false;
                }

                if (index.ObservedStateMachineIds.Contains(stateMachineId)) {
                    __result = false;
                    return false;
                }

                // This also discovers layers appended by the lazy direct-tree creation.
                Rebuild(index);
                __result = index.LayerByStateMachineId.ContainsKey(stateMachineId);
                if (!__result) index.ObservedStateMachineIds.Add(stateMachineId);
                return false;
            } catch (Exception e) {
                active = null;
                Debug.LogWarning("[QuickFury] Layer-to-tree layer index fell back to VRCFury: " + e.Message);
                return true;
            }
        }

        private static void LayerOrderChanged(object __instance) {
            var context = active;
            if (context == null) return;

            try {
                var controller = controllerField.GetValue(__instance) as AnimatorController;
                if (controller == null) return;
                if (context.Controllers.TryGetValue(controller.GetInstanceID(), out var index)) {
                    Rebuild(index);
                }
            } catch (Exception e) {
                active = null;
                Debug.LogWarning("[QuickFury] Layer-to-tree layer index fell back to VRCFury: " + e.Message);
            }
        }

        private static ControllerIndex GetOrBuild(Context context, AnimatorController controller) {
            var controllerId = controller.GetInstanceID();
            if (context.Controllers.TryGetValue(controllerId, out var existing)) return existing;

            var created = new ControllerIndex { Controller = controller };
            Rebuild(created);
            context.Controllers.Add(controllerId, created);
            return created;
        }

        private static void Rebuild(ControllerIndex index) {
            index.LayerByStateMachineId.Clear();
            var layers = index.Controller.layers;
            for (var i = 0; i < layers.Length; i++) {
                var stateMachine = layers[i].stateMachine;
                if (stateMachine != null) {
                    var stateMachineId = stateMachine.GetInstanceID();
                    index.LayerByStateMachineId[stateMachineId] = i;
                    index.ObservedStateMachineIds.Add(stateMachineId);
                }
            }
        }
    }
}
