using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        [ThreadStatic] private static List<Component> activePhysbones;

        private static Type physboneType;
        private static FieldInfo avatarObjectField;
        private static FieldInfo gameObjectField;
        private static FieldInfo ignoreTransformsField;
        private static MethodInfo getRootTransform;

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            var armatureType = VrcfuryCompatibility.FindType("VF.Service.ArmatureLinkService");
            var vfGameObjectType = VrcfuryCompatibility.FindType("VF.Utils.VFGameObject");
            var physboneUtilsType = VrcfuryCompatibility.FindType("VF.Utils.PhysboneUtils");
            physboneType = VrcfuryCompatibility.FindType(
                "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone"
            );
            var physboneBaseType = VrcfuryCompatibility.FindType("VRC.Dynamics.VRCPhysBoneBase");

            var apply = armatureType?.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "Apply" && method.GetParameters().Length == 0);
            var remove = physboneUtilsType?
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => {
                    if (method.Name != "RemoveFromPhysbones") return false;
                    var parameters = method.GetParameters();
                    return parameters.Length == 2 && parameters[1].ParameterType == typeof(bool);
                });

            avatarObjectField = armatureType?.GetField("avatarObject", BindingFlags.Instance | BindingFlags.NonPublic);
            gameObjectField = vfGameObjectType?.GetField("_gameObject", BindingFlags.Instance | BindingFlags.NonPublic);
            ignoreTransformsField = ArmatureReflection.FindFieldInHierarchy(physboneBaseType, "ignoreTransforms");
            getRootTransform = physboneBaseType?.GetMethod(
                "GetRootTransform",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null
            );

            if (apply == null || remove == null || physboneType == null || avatarObjectField == null
                              || gameObjectField == null || ignoreTransformsField == null
                              || getRootTransform == null) {
                Debug.LogWarning("[QuickFury] Armature PhysBone index disabled: target signature mismatch.");
                return;
            }

            try {
                harmony.Patch(
                    apply,
                    prefix: new HarmonyMethod(typeof(ArmaturePhysboneIndexPatch), nameof(Begin)),
                    finalizer: new HarmonyMethod(typeof(ArmaturePhysboneIndexPatch), nameof(End))
                );
                harmony.Patch(
                    remove,
                    prefix: new HarmonyMethod(typeof(ArmaturePhysboneIndexPatch), nameof(RemoveFromPhysbones))
                );
            } catch (Exception e) {
                Debug.LogWarning("[QuickFury] Armature PhysBone index disabled: " + e.Message);
            }
        }

        private static void Begin(object __instance) {
            activePhysbones = null;
            if (!QuickFurySettings.PhysboneIndex) return;

            try {
                var avatarWrapper = avatarObjectField.GetValue(__instance);
                var avatar = ArmatureReflection.GetGameObject(avatarWrapper, gameObjectField);
                if (avatar == null) return;
                activePhysbones = avatar.GetComponentsInChildren(physboneType, true)
                    .OfType<Component>()
                    .ToList();
            } catch (Exception e) {
                activePhysbones = null;
                Debug.LogWarning("[QuickFury] PhysBone index fell back to VRCFury: " + e.Message);
            }
        }

        private static Exception End(Exception __exception) {
            activePhysbones = null;
            return __exception;
        }

        private static bool RemoveFromPhysbones(object __0, bool __1) {
            var physbones = activePhysbones;
            if (physbones == null || !__1) return true;

            var gameObject = ArmatureReflection.GetGameObject(__0, gameObjectField);
            if (gameObject == null) return true;
            var transform = gameObject.transform;

            foreach (var component in physbones) {
                if (component == null) continue;
                var root = getRootTransform.Invoke(component, null) as Transform;
                if (root == null || transform == root || !transform.IsChildOf(root)) continue;

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

            return false;
        }
    }
}
