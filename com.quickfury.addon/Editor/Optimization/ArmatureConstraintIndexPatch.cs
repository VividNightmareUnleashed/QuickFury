using System;
using System.Collections.Generic;
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
            internal int Order;
            internal object Wrapper;
            internal Component Component;
            internal Transform Affected;
        }

        private sealed class Context {
            internal readonly List<Entry> Entries = new List<Entry>();
            internal readonly Dictionary<int, List<Entry>> ByAffectedTransform =
                new Dictionary<int, List<Entry>>();
        }

        [ThreadStatic] private static Context active;

        private static Type constraintType;
        private static MethodInfo createConstraint;
        private static MethodInfo getAffectedObject;
        private static MethodInfo getConstraintComponent;

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            constraintType = VrcfuryCompatibility.FindType("VF.Utils.VFConstraint");

            createConstraint = constraintType?.GetMethod(
                "CreateOrNull",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(Component) },
                null
            );
            getAffectedObject = VrcfuryCompatibility.FindUniqueMethod(
                constraintType,
                "GetAffectedObject",
                method => method.GetParameters().Length == 0
            );
            getConstraintComponent = VrcfuryCompatibility.FindUniqueMethod(
                constraintType,
                "GetComponent",
                method => method.GetParameters().Length == 0
            );

            if (!ArmatureReflection.ArmatureLinkAvailable || !ArmatureReflection.HapticSocketsAvailable
                || ArmatureReflection.GetConstraintsMethod == null || constraintType == null
                || createConstraint == null || getAffectedObject == null
                || getConstraintComponent == null) {
                throw new InvalidOperationException("target signature mismatch");
            }

            harmony.Patch(
                ArmatureReflection.ArmatureLinkApply,
                prefix: new HarmonyMethod(typeof(ArmatureConstraintIndexPatch), nameof(Begin)),
                finalizer: new HarmonyMethod(typeof(ArmatureConstraintIndexPatch), nameof(End))
            );
            harmony.Patch(
                ArmatureReflection.HapticSocketsApply,
                prefix: new HarmonyMethod(typeof(ArmatureConstraintIndexPatch), nameof(BeginHaptics)),
                finalizer: new HarmonyMethod(typeof(ArmatureConstraintIndexPatch), nameof(End))
            );
            harmony.Patch(
                ArmatureReflection.GetConstraintsMethod,
                prefix: new HarmonyMethod(typeof(ArmatureConstraintIndexPatch), nameof(GetConstraints))
            );
        }

        private static void Begin(object __instance) {
            BeginWithField(__instance, ArmatureReflection.ArmatureLinkAvatarField);
        }

        private static void BeginHaptics(object __instance) {
            BeginWithField(__instance, ArmatureReflection.HapticSocketsAvatarField);
        }

        private static void BeginWithField(object instance, FieldInfo avatarField) {
            active = null;
            if (!QuickFurySettings.ConstraintIndex) return;

            try {
                var avatar = ArmatureReflection.GetAvatar(instance, avatarField);
                if (avatar == null) return;

                var context = new Context();
                foreach (var component in avatar.GetComponentsInChildren<Component>(true)) {
                    // CreateOrNull only wraps IConstraint/VRCConstraintBase components, so
                    // don't pay a reflection invoke for the avatar's many Transforms.
                    if (component == null || component is Transform) continue;
                    var wrapper = createConstraint.Invoke(null, new object[] { component });
                    if (wrapper == null) continue;

                    var affectedWrapper = getAffectedObject.Invoke(wrapper, null);
                    var affected = ArmatureReflection.GetGameObject(affectedWrapper)?.transform;
                    var constraintComponent = getConstraintComponent.Invoke(wrapper, null) as Component;
                    if (affected == null || constraintComponent == null) continue;

                    var entry = new Entry {
                        Order = context.Entries.Count,
                        Wrapper = wrapper,
                        Component = constraintComponent,
                        Affected = affected
                    };
                    context.Entries.Add(entry);
                    context.ByAffectedTransform.GetOrAddList(affected.GetInstanceID()).Add(entry);
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

            var requestedObject = ArmatureReflection.GetGameObject(__instance);
            if (requestedObject == null) return true;
            var requested = requestedObject.transform;

            var entries = new List<Entry>();
            if (__0) {
                for (var current = requested; current != null; current = current.parent) {
                    AddBucket(context, current, entries);
                }
            } else if (__1) {
                // The avatar holds few constraints but a pruned subtree can hold thousands
                // of transforms, so test the indexed entries instead of scanning children.
                foreach (var entry in context.Entries) {
                    if (entry.Affected != null && entry.Affected.IsChildOf(requested)) {
                        entries.Add(entry);
                    }
                }
            } else {
                AddBucket(context, requested, entries);
            }

            // The stock component scan is stable. Preserve that order even though the
            // lookup above follows hierarchy ancestry/descendancy.
            entries.RemoveAll(entry => entry.Component == null || entry.Affected == null);
            entries.Sort((left, right) => left.Order.CompareTo(right.Order));

            var output = Array.CreateInstance(constraintType, entries.Count);
            for (var i = 0; i < entries.Count; i++) output.SetValue(entries[i].Wrapper, i);
            __result = output;
            return false;
        }

        private static void AddBucket(Context context, Transform transform, List<Entry> output) {
            if (transform == null) return;
            if (context.ByAffectedTransform.TryGetValue(transform.GetInstanceID(), out var bucket)) {
                output.AddRange(bucket);
            }
        }
    }
}
