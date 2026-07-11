using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEditor.Animations;
using UnityEngine;

namespace QuickFury {
    /// <summary>
    /// Several late controller services invoke VFLayer.RewriteBehaviours on every
    /// layer even though only a small subset contains the requested behaviour type.
    /// A cheap raw-array scan can prove the others empty without constructing
    /// VRCFury's recursive immutable VFBehaviourContainer graph.
    /// </summary>
    internal static class BehaviourContainerFilterPatch {
        private sealed class Context {
            internal string Name;
            internal Type BehaviourType;
            internal readonly Dictionary<int, bool> HasTargetByStateMachine =
                new Dictionary<int, bool>();
            internal int LayersChecked;
            internal int LayersSkipped;
        }

        [ThreadStatic] private static Context active;
        private static FieldInfo stateMachineField;
        private static object emptyContainers;
        private static Type playableLayerControlType;
        private static Type parameterDriverType;
        private static Type animatorLayerControlType;

        internal static object EmptyContainerSet => emptyContainers;

        internal static string LastStats { get; private set; } = "none";

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            var layerType = VrcfuryCompatibility.FindType("VF.Utils.Controller.VFLayer");
            var containerGetter = layerType?
                .GetProperty("allBehaviourContainers", BindingFlags.Instance | BindingFlags.NonPublic)?
                .GetGetMethod(true);
            stateMachineField = layerType?.GetField(
                "rootStateMachine",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            if (containerGetter == null || stateMachineField == null
                                        || !TryCreateEmptySet(containerGetter.ReturnType)) {
                Debug.LogWarning("[QuickFury] Behaviour container filter disabled: target signature mismatch.");
                return;
            }

            playableLayerControlType = VrcfuryCompatibility.FindType(
                "VRC.SDK3.Avatars.Components.VRCPlayableLayerControl"
            );
            parameterDriverType = VrcfuryCompatibility.FindType(
                "VRC.SDK3.Avatars.Components.VRCAvatarParameterDriver"
            );
            animatorLayerControlType = VrcfuryCompatibility.FindType(
                "VRC.SDK3.Avatars.Components.VRCAnimatorLayerControl"
            );

            var actionApply = FindNoArgVoid("VF.Service.ActionConflictResolverService", "Apply");
            var syncedDriverApply = FindNoArgVoid("VF.Service.MakeAllSyncedDriversLocalService", "Apply");
            var layerControlFix = FindNoArgVoid("VF.Service.AnimatorLayerControlOffsetService", "Fix");

            if (playableLayerControlType == null || parameterDriverType == null || animatorLayerControlType == null
                                              || actionApply == null || syncedDriverApply == null
                                              || layerControlFix == null) {
                Debug.LogWarning("[QuickFury] Behaviour container filter disabled: service target mismatch.");
                return;
            }

            try {
                PatchPhase(
                    harmony,
                    actionApply,
                    nameof(BeginPlayableLayerControls)
                );
                PatchPhase(
                    harmony,
                    syncedDriverApply,
                    nameof(BeginParameterDrivers)
                );
                PatchPhase(
                    harmony,
                    layerControlFix,
                    nameof(BeginAnimatorLayerControls)
                );
            } catch (Exception e) {
                active = null;
                Debug.LogWarning("[QuickFury] Behaviour container filter disabled: " + e.Message);
            }
        }

        private static MethodInfo FindNoArgVoid(string typeName, string methodName) {
            return VrcfuryCompatibility.FindType(typeName)?
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == methodName
                                           && method.ReturnType == typeof(void)
                                           && method.GetParameters().Length == 0);
        }

        private static void PatchPhase(
            Harmony harmony,
            MethodInfo method,
            string prefixName
        ) {
            harmony.Patch(
                method,
                prefix: new HarmonyMethod(typeof(BehaviourContainerFilterPatch), prefixName),
                finalizer: new HarmonyMethod(typeof(BehaviourContainerFilterPatch), nameof(End))
            );
        }

        private static void BeginPlayableLayerControls() {
            Begin(playableLayerControlType, "playableLayerControl");
        }

        private static void BeginParameterDrivers() {
            Begin(parameterDriverType, "parameterDriver");
        }

        private static void BeginAnimatorLayerControls() {
            Begin(animatorLayerControlType, "animatorLayerControl");
        }

        private static void Begin(Type behaviourType, string name) {
            active = QuickFurySettings.BehaviourContainerFilter
                ? new Context { Name = name, BehaviourType = behaviourType }
                : null;
        }

        private static Exception End(Exception __exception) {
            var context = active;
            if (context != null) {
                LastStats = context.Name + "=" + context.LayersSkipped + "/" + context.LayersChecked;
            }
            active = null;
            return __exception;
        }

        internal static bool Filter<T>(object __instance, ref T __result) {
            var context = active;
            if (context == null || __instance == null) return true;

            try {
                var stateMachine = stateMachineField.GetValue(__instance) as AnimatorStateMachine;
                if (stateMachine == null) return true;

                context.LayersChecked++;
                var id = stateMachine.GetInstanceID();
                if (!context.HasTargetByStateMachine.TryGetValue(id, out var hasTarget)) {
                    hasTarget = HasTarget(
                        stateMachine,
                        context.BehaviourType,
                        new HashSet<int>()
                    );
                    context.HasTargetByStateMachine[id] = hasTarget;
                }
                if (hasTarget) return true;

                context.LayersSkipped++;
                __result = (T)emptyContainers;
                return false;
            } catch (Exception e) {
                active = null;
                Debug.LogWarning("[QuickFury] Behaviour container filter fell back to VRCFury: " + e.Message);
                return true;
            }
        }

        private static bool HasTarget(
            AnimatorStateMachine stateMachine,
            Type target,
            HashSet<int> visited
        ) {
            if (stateMachine == null || !visited.Add(stateMachine.GetInstanceID())) return false;

            var stateMachineBehaviours = stateMachine.behaviours;
            if (stateMachineBehaviours != null
                && stateMachineBehaviours.Any(value => value != null && target.IsInstanceOfType(value))) {
                return true;
            }

            foreach (var childState in stateMachine.states) {
                var behaviours = childState.state?.behaviours;
                if (behaviours != null
                    && behaviours.Any(value => value != null && target.IsInstanceOfType(value))) {
                    return true;
                }
            }

            foreach (var childStateMachine in stateMachine.stateMachines) {
                if (HasTarget(childStateMachine.stateMachine, target, visited)) return true;
            }
            return false;
        }

        private static bool TryCreateEmptySet(Type returnType) {
            try {
                var arguments = returnType.GetGenericArguments();
                if (arguments.Length != 1) return false;
                // Unity also loads a private copy inside ReportGeneratorMerged.
                // Resolve from the interface's own assembly so the empty set is
                // assignable to VRCFury's System.Collections.Immutable contract.
                var openType = returnType.Assembly.GetType(
                    "System.Collections.Immutable.ImmutableHashSet`1",
                    false
                );
                if (openType == null) return false;
                var closedType = openType.MakeGenericType(arguments[0]);
                emptyContainers = closedType
                    .GetField("Empty", BindingFlags.Static | BindingFlags.Public)?
                    .GetValue(null);
                return emptyContainers != null && returnType.IsInstanceOfType(emptyContainers);
            } catch {
                emptyContainers = null;
                return false;
            }
        }
    }
}
