using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace QuickFury {
    /// <summary>
    /// Snapshots the set of PhysBones once at the beginning of Armature Link. The original
    /// method searches the complete hierarchy for the same set on every wrapper creation
    /// and every move.
    /// </summary>
    internal static class ArmaturePhysboneIndexPatch {
        private sealed class Context {
            internal readonly Dictionary<int, List<Component>> ByRoot =
                new Dictionary<int, List<Component>>();
        }

        [ThreadStatic] private static Context active;

        private static Type physboneType;
        private static FieldInfo ignoreTransformsField;
        private static MethodInfo getRootTransform;

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            physboneType = VrcfuryCompatibility.FindType(
                "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone"
            );
            var physboneBaseType = VrcfuryCompatibility.FindType("VRC.Dynamics.VRCPhysBoneBase");

            ignoreTransformsField = physboneBaseType?.GetField(
                "ignoreTransforms",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            getRootTransform = physboneBaseType?.GetMethod(
                "GetRootTransform",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null
            );

            if (!ArmatureReflection.ArmatureLinkAvailable || !ArmatureReflection.HapticSocketsAvailable
                || ArmatureReflection.RemoveFromPhysbones == null || physboneType == null
                || ignoreTransformsField == null || getRootTransform == null) {
                throw new InvalidOperationException("target signature mismatch");
            }

            harmony.Patch(
                ArmatureReflection.ArmatureLinkApply,
                prefix: new HarmonyMethod(typeof(ArmaturePhysboneIndexPatch), nameof(Begin)),
                finalizer: new HarmonyMethod(typeof(ArmaturePhysboneIndexPatch), nameof(End))
            );
            harmony.Patch(
                ArmatureReflection.HapticSocketsApply,
                prefix: new HarmonyMethod(typeof(ArmaturePhysboneIndexPatch), nameof(BeginHaptics)),
                finalizer: new HarmonyMethod(typeof(ArmaturePhysboneIndexPatch), nameof(End))
            );
            harmony.Patch(
                ArmatureReflection.RemoveFromPhysbones,
                prefix: new HarmonyMethod(typeof(ArmaturePhysboneIndexPatch), nameof(RemoveFromPhysbones))
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
            if (!QuickFurySettings.PhysboneIndex) return;

            try {
                var avatar = ArmatureReflection.GetAvatar(instance, avatarField);
                if (avatar == null) return;
                var context = new Context();
                foreach (var component in avatar.GetComponentsInChildren(physboneType, true)) {
                    if (component == null) continue;
                    var root = getRootTransform.Invoke(component, null) as Transform;
                    if (root == null) continue;
                    context.ByRoot.GetOrAddList(root.GetInstanceID()).Add(component);
                }
                active = context;
            } catch (Exception e) {
                active = null;
                Debug.LogWarning("[QuickFury] PhysBone index fell back to VRCFury: " + e.Message);
            }
        }

        private static Exception End(Exception __exception) {
            active = null;
            return __exception;
        }

        private static bool RemoveFromPhysbones(object __0, bool __1) {
            var context = active;
            if (context == null || !__1) return true;

            var gameObject = ArmatureReflection.GetGameObject(__0);
            if (gameObject == null) return true;
            var transform = gameObject.transform;

            // Only PhysBones rooted on an ancestor can contain this object. Walking the
            // hierarchy replaces a full PhysBone list scan for every wrapper and move.
            for (var ancestor = transform.parent; ancestor != null; ancestor = ancestor.parent) {
                if (!context.ByRoot.TryGetValue(ancestor.GetInstanceID(), out var physbones)) continue;
                foreach (var component in physbones) {
                    if (component == null) continue;
                    var ignoreTransforms = ignoreTransformsField.GetValue(component) as IList;
                    if (ignoreTransforms == null) return true;

                    var alreadyExcluded = false;
                    foreach (var item in ignoreTransforms) {
                        var ignored = item as Transform;
                        if (ignored != null && transform.IsChildOf(ignored)) {
                            alreadyExcluded = true;
                            break;
                        }
                    }

                    if (!alreadyExcluded) ignoreTransforms.Add(transform);
                }
            }

            return false;
        }
    }
}
