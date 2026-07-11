using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace QuickFury {
    /// <summary>
    /// ObjectMoveService.Move rebuilds the complete humanoid immovable-bone set for
    /// every move. Armature Link performs thousands of deferred moves against one
    /// avatar, so build that invariant set once and preserve the original reparent,
    /// safe-name, path-recording and PhysBone-exclusion behavior directly.
    /// </summary>
    internal static class FastArmatureMovePatch {
        private sealed class Context {
            internal GameObject Avatar;
            internal readonly HashSet<int> Immovable = new HashSet<int>();
            internal object DeferredService;
            internal IList Deferred;
        }

        [ThreadStatic] private static Context active;
        private static VrcfuryCompatibility compatibility;

        internal static void Install(Harmony harmony, VrcfuryCompatibility targets) {
            compatibility = targets;

            var moveType = VrcfuryCompatibility.FindType("VF.Service.ObjectMoveService");
            var move = VrcfuryCompatibility.FindUniqueMethod(
                moveType,
                "Move",
                method => method.ReturnType == typeof(void) && method.GetParameters().Length == 5
            );

            if (!ArmatureReflection.ArmatureLinkAvailable || move == null
                || ArmatureReflection.RemoveFromPhysbones == null || targets.DeferredMoves == null) {
                Debug.LogWarning("[QuickFury] Fast Armature Link moves disabled: target mismatch.");
                return;
            }

            try {
                harmony.Patch(
                    ArmatureReflection.ArmatureLinkApply,
                    prefix: new HarmonyMethod(typeof(FastArmatureMovePatch), nameof(Begin)),
                    finalizer: new HarmonyMethod(typeof(FastArmatureMovePatch), nameof(End))
                );
                harmony.Patch(
                    move,
                    prefix: new HarmonyMethod(typeof(FastArmatureMovePatch), nameof(Move))
                );
            } catch (Exception e) {
                Debug.LogWarning("[QuickFury] Fast Armature Link moves disabled: " + e.Message);
            }
        }

        private static void Begin(object __instance) {
            active = null;
            if (!QuickFurySettings.FastArmatureMove) return;

            try {
                var avatar = ArmatureReflection.GetAvatar(__instance, ArmatureReflection.ArmatureLinkAvatarField);
                if (avatar == null) return;

                var context = new Context { Avatar = avatar };
                context.Immovable.Add(avatar.transform.GetInstanceID());
                var animator = avatar.GetComponent<Animator>();
                if (animator != null && animator.isHuman) {
                    for (var i = 0; i < (int)HumanBodyBones.LastBone; i++) {
                        var bone = (HumanBodyBones)i;
                        if (bone == HumanBodyBones.LeftEye || bone == HumanBodyBones.RightEye) continue;
                        var current = animator.GetBoneTransform(bone);
                        while (current != null && current != avatar.transform) {
                            context.Immovable.Add(current.GetInstanceID());
                            current = current.parent;
                        }
                    }
                }
                active = context;
            } catch (Exception e) {
                active = null;
                Debug.LogWarning("[QuickFury] Fast Armature Link moves fell back to VRCFury: " + e.Message);
            }
        }

        private static Exception End(Exception __exception) {
            active = null;
            return __exception;
        }

        private static bool Move(
            object __instance,
            object __0,
            object __1,
            string __2,
            bool __3,
            bool __4
        ) {
            var context = active;
            // Armature Link always defers; retain VRCFury for any unexpected immediate move.
            if (context == null || !__4) return true;

            GameObject obj;
            GameObject newParent;
            IList deferred;
            try {
                obj = ArmatureReflection.GetGameObject(__0);
                newParent = ArmatureReflection.GetGameObject(__1);
                if (obj == null || context.Avatar == null) return true;
                deferred = GetDeferred(context, __instance);
                if (deferred == null) return true;
            } catch (Exception e) {
                active = null;
                Debug.LogWarning("[QuickFury] Fast Armature Link moves fell back to VRCFury: " + e.Message);
                return true;
            }

            if (context.Immovable.Contains(obj.transform.GetInstanceID())) {
                // Deliberately outside the fallback scope: this must reach VRCFury's caller
                // exactly like the stock immovable-object error.
                throw new Exception(
                    $"VRCFury is trying to move the {obj.name} object, but bones / root avatar objects cannot be moved." +
                    " You are probably trying to do something weird in one of your VRCFury components. Don't do that."
                );
            }

            var mutated = false;
            try {
                var oldPath = AnimationUtility.CalculateTransformPath(
                    obj.transform,
                    context.Avatar.transform
                );
                mutated = true;
                if (newParent != null) obj.transform.SetParent(newParent.transform, __3);
                if (__2 != null) obj.name = __2;
                EnsureAnimationSafeName(obj.transform);
                var newPath = AnimationUtility.CalculateTransformPath(
                    obj.transform,
                    context.Avatar.transform
                );

                VrcfuryCompatibility.InvokeUnwrapped(
                    ArmatureReflection.RemoveFromPhysbones,
                    null,
                    new object[] { __0, true }
                );
                deferred.Add((oldPath, newPath));
                return false;
            } catch (Exception e) {
                // Once hierarchy state changed, running VRCFury's method again would
                // record the wrong old path. Fail loudly instead of double-applying.
                if (mutated) throw;
                active = null;
                Debug.LogWarning("[QuickFury] Fast Armature Link moves fell back to VRCFury: " + e.Message);
                return true;
            }
        }

        // The service instance and its deferred list are stable for the whole Apply, so
        // avoid a reflection field read on every one of the thousands of moves.
        private static IList GetDeferred(Context context, object service) {
            if (!ReferenceEquals(context.DeferredService, service)) {
                context.Deferred = compatibility.DeferredMoves.GetValue(service) as IList;
                context.DeferredService = service;
            }
            return context.Deferred;
        }

        private static void EnsureAnimationSafeName(Transform transform) {
            var name = transform.name.Replace("/", "_");
            if (string.IsNullOrEmpty(name)) name = "_";
            var parent = transform.parent;
            if (parent != null) {
                for (var i = 0;; i++) {
                    var finalName = name + (i == 0 ? "" : $" ({i})");
                    var existing = parent.Find(finalName);
                    if (existing != null && existing != transform) continue;
                    name = finalName;
                    break;
                }
            }
            transform.name = name;
        }
    }
}
