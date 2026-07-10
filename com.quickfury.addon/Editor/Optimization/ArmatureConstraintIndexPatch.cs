using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace QuickFury {
    /// <summary>
    /// Replaces Armature Link's thousands of whole-avatar constraint scans with one
    /// per-phase index. Entries retain live Transform references so hierarchy moves keep
    /// parent/child queries correct, and destroyed constraints are filtered at lookup time.
    /// </summary>
    internal static class ArmatureConstraintIndexPatch {
        private sealed class Entry {
            internal object Wrapper;
            internal Component Component;
            internal Transform Affected;
        }

        private sealed class Context {
            internal readonly List<Entry> Entries = new List<Entry>();
        }

        [ThreadStatic] private static Context active;

        private static Type constraintType;
        private static FieldInfo avatarObjectField;
        private static FieldInfo gameObjectField;
        private static MethodInfo createConstraint;
        private static MethodInfo getAffectedObject;
        private static MethodInfo getConstraintComponent;

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            var armatureType = VrcfuryCompatibility.FindType("VF.Service.ArmatureLinkService");
            var vfGameObjectType = VrcfuryCompatibility.FindType("VF.Utils.VFGameObject");
            constraintType = VrcfuryCompatibility.FindType("VF.Utils.VFConstraint");

            var apply = armatureType?.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "Apply" && method.GetParameters().Length == 0);
            var getConstraints = vfGameObjectType?
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => {
                    if (method.Name != "GetConstraints" || !method.ReturnType.IsArray) return false;
                    var parameters = method.GetParameters();
                    return parameters.Length == 2
                           && parameters[0].ParameterType == typeof(bool)
                           && parameters[1].ParameterType == typeof(bool);
                });

            avatarObjectField = armatureType?.GetField("avatarObject", BindingFlags.Instance | BindingFlags.NonPublic);
            gameObjectField = vfGameObjectType?.GetField("_gameObject", BindingFlags.Instance | BindingFlags.NonPublic);
            createConstraint = constraintType?.GetMethod(
                "CreateOrNull",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(Component) },
                null
            );
            getAffectedObject = constraintType?.GetMethod(
                "GetAffectedObject",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            getConstraintComponent = constraintType?.GetMethod(
                "GetComponent",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (apply == null || getConstraints == null || constraintType == null
                              || avatarObjectField == null || gameObjectField == null
                              || createConstraint == null || getAffectedObject == null
                              || getConstraintComponent == null) {
                Debug.LogWarning("[QuickFury] Armature constraint index disabled: target signature mismatch.");
                return;
            }

            try {
                harmony.Patch(
                    apply,
                    prefix: new HarmonyMethod(typeof(ArmatureConstraintIndexPatch), nameof(Begin)),
                    finalizer: new HarmonyMethod(typeof(ArmatureConstraintIndexPatch), nameof(End))
                );
                harmony.Patch(
                    getConstraints,
                    prefix: new HarmonyMethod(typeof(ArmatureConstraintIndexPatch), nameof(GetConstraints))
                );
            } catch (Exception e) {
                Debug.LogWarning("[QuickFury] Armature constraint index disabled: " + e.Message);
            }
        }

        private static void Begin(object __instance) {
            active = null;
            if (!QuickFurySettings.ConstraintIndex) return;

            try {
                var avatarWrapper = avatarObjectField.GetValue(__instance);
                var avatar = ArmatureReflection.GetGameObject(avatarWrapper, gameObjectField);
                if (avatar == null) return;

                var context = new Context();
                foreach (var component in avatar.GetComponentsInChildren<Component>(true)) {
                    if (component == null) continue;
                    var wrapper = createConstraint.Invoke(null, new object[] { component });
                    if (wrapper == null) continue;

                    var affectedWrapper = getAffectedObject.Invoke(wrapper, null);
                    var affected = ArmatureReflection.GetGameObject(affectedWrapper, gameObjectField)?.transform;
                    var constraintComponent = getConstraintComponent.Invoke(wrapper, null) as Component;
                    if (affected == null || constraintComponent == null) continue;

                    context.Entries.Add(new Entry {
                        Wrapper = wrapper,
                        Component = constraintComponent,
                        Affected = affected
                    });
                }
                active = context;
            } catch (Exception e) {
                active = null;
                Debug.LogWarning("[QuickFury] Constraint index fell back to VRCFury: " + e.Message);
            }
        }

        private static Exception End(Exception __exception) {
            active = null;
            return __exception;
        }

        private static bool GetConstraints(
            object __instance,
            bool __0,
            bool __1,
            ref object __result
        ) {
            var context = active;
            if (context == null) return true;

            var requestedObject = ArmatureReflection.GetGameObject(__instance, gameObjectField);
            if (requestedObject == null) return true;
            var requested = requestedObject.transform;

            var matches = new List<object>();
            foreach (var entry in context.Entries) {
                if (entry.Component == null || entry.Affected == null) continue;

                bool match;
                if (__0) {
                    match = requested.IsChildOf(entry.Affected);
                } else if (__1) {
                    match = entry.Affected.IsChildOf(requested);
                } else {
                    match = entry.Affected == requested;
                }

                if (match) matches.Add(entry.Wrapper);
            }

            var output = Array.CreateInstance(constraintType, matches.Count);
            for (var i = 0; i < matches.Count; i++) output.SetValue(matches[i], i);
            __result = output;
            return false;
        }
    }
}
