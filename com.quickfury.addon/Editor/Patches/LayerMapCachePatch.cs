using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEditor.Animations;

namespace QuickFury {
    /**
     * VF.Utils.Controller.VFLayer.Exists() and GetLayerId() each marshal the controller's ENTIRE
     * native layers array per call. LayerToTreeService.OptimizeLayer's cross-layer comparison calls
     * them once per (layer × other-layer) pair — ~97k full-array marshals on a 181-layer FX
     * controller, measured at ~10s of the bake.
     *
     * Caches a stateMachine -> layerIndex map per controller. Safety design (v2, after the v0.4.0
     * incident where the mid-pass DBT layer creation bypassed invalidation and crashed the build):
     *
     * - Served ONLY while inside OptimizeLayer (depth flag), the one method with the 97k-call hotspot.
     * - Every OptimizeLayer entry starts a fresh generation — nothing survives between layers.
     * - Self-healing lookups: a GetLayerId miss always rebuilds from a live ctrl.layers read before
     *   throwing; an Exists miss rebuilds once per generation before returning false. A layer list
     *   change we didn't observe can therefore cost one extra marshal, never a wrong answer.
     * - Known mutation paths still invalidate eagerly: VFLayer.Remove, VFController.NewLayer AND
     *   the ControllerManager.NewLayer override (the path that bit v0.4.0).
     */
    internal static class LayerMapCachePatch {
        private static AccessTools.FieldRef<object, AnimatorController> ctrlF;
        private static AccessTools.FieldRef<object, AnimatorStateMachine> rootSmF;

        private static int optimizeDepth;
        private static int mutationStamp;

        private static int cacheVersion = -1;
        private static int cacheStamp = -1;
        private static bool refreshedThisGen;
        private static readonly Dictionary<AnimatorController, Dictionary<AnimatorStateMachine, int>> maps =
            new Dictionary<AnimatorController, Dictionary<AnimatorStateMachine, int>>();

        public static void Apply(Harmony h) {
            var vfLayer = QfReflect.ReqType("VF.Utils.Controller.VFLayer");
            var vfController = QfReflect.ReqType("VF.Utils.Controller.VFController");
            var layerToTree = QfReflect.ReqType("VF.Service.LayerToTreeService");
            ctrlF = AccessTools.FieldRefAccess<AnimatorController>(vfLayer, "ctrl");
            rootSmF = AccessTools.FieldRefAccess<AnimatorStateMachine>(vfLayer, "rootStateMachine");

            h.Patch(QfReflect.ReqMethod(layerToTree, "OptimizeLayer"),
                prefix: new HarmonyMethod(typeof(LayerMapCachePatch), nameof(ScopePrefix)),
                finalizer: new HarmonyMethod(typeof(LayerMapCachePatch), nameof(ScopeFinalizer)));

            // Eager invalidation on every known layer-list mutation path, including the
            // ControllerManager override that does NOT flow through the base method.
            var mutation = new HarmonyMethod(typeof(LayerMapCachePatch), nameof(MutationPostfix));
            h.Patch(QfReflect.ReqMethod(vfLayer, "Remove"), postfix: mutation);
            h.Patch(QfReflect.ReqMethod(vfController, "NewLayer"), postfix: mutation);
            var controllerManager = AccessTools.TypeByName("VF.Builder.ControllerManager")
                                    ?? AccessTools.TypeByName("VF.Utils.ControllerManager");
            var overrideNewLayer = controllerManager == null
                ? null
                : controllerManager.GetMethod("NewLayer",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.DeclaredOnly);
            if (overrideNewLayer != null) h.Patch(overrideNewLayer, postfix: mutation);

            h.Patch(QfReflect.ReqMethod(vfLayer, "Exists"),
                prefix: new HarmonyMethod(typeof(LayerMapCachePatch), nameof(ExistsPrefix)));
            h.Patch(QfReflect.ReqMethod(vfLayer, "GetLayerId"),
                prefix: new HarmonyMethod(typeof(LayerMapCachePatch), nameof(GetLayerIdPrefix)));
        }

        private static void ScopePrefix() {
            optimizeDepth++;
            mutationStamp++; // fresh generation for every OptimizeLayer call
        }

        private static void ScopeFinalizer() => optimizeDepth--;
        private static void MutationPostfix() => mutationStamp++;

        private static bool Active() {
            return QfSettings.LayerMapCache && QfState.InBuild && optimizeDepth > 0;
        }

        private static Dictionary<AnimatorStateMachine, int> GetMap(AnimatorController ctrl) {
            if (cacheVersion != QfState.scopeVersion || cacheStamp != mutationStamp) {
                maps.Clear();
                cacheVersion = QfState.scopeVersion;
                cacheStamp = mutationStamp;
                refreshedThisGen = false;
            }
            if (!maps.TryGetValue(ctrl, out var map)) {
                map = BuildMap(ctrl);
                maps[ctrl] = map;
            }
            return map;
        }

        private static Dictionary<AnimatorStateMachine, int> BuildMap(AnimatorController ctrl) {
            var map = new Dictionary<AnimatorStateMachine, int>();
            var layers = ctrl.layers; // the one marshal
            for (var i = 0; i < layers.Length; i++) {
                var sm = layers[i].stateMachine;
                if (sm != null && !map.ContainsKey(sm)) map[sm] = i;
            }
            return map;
        }

        private static Dictionary<AnimatorStateMachine, int> Refresh(AnimatorController ctrl) {
            var map = BuildMap(ctrl);
            maps[ctrl] = map;
            refreshedThisGen = true;
            return map;
        }

        private static bool ExistsPrefix(object __instance, ref bool __result) {
            if (!Active()) return true;
            var ctrl = ctrlF(__instance);
            var sm = rootSmF(__instance);
            if (ctrl == null || sm == null) return true;
            var map = GetMap(ctrl);
            if (!map.ContainsKey(sm) && !refreshedThisGen) map = Refresh(ctrl);
            __result = map.ContainsKey(sm);
            return false;
        }

        private static bool GetLayerIdPrefix(object __instance, ref int __result) {
            if (!Active()) return true;
            var ctrl = ctrlF(__instance);
            var sm = rootSmF(__instance);
            if (ctrl == null || sm == null) return true;
            var map = GetMap(ctrl);
            if (!map.TryGetValue(sm, out var id)) {
                // Never trust a miss without a live re-read — an unobserved layer insertion
                // (the v0.4.0 crash) must self-heal, and a genuine error pays one extra marshal.
                map = Refresh(ctrl);
                if (!map.TryGetValue(sm, out id)) {
                    throw new Exception("Layer not found in controller. It may have been accessed after it was removed.");
                }
            }
            __result = id;
            return false;
        }
    }
}
