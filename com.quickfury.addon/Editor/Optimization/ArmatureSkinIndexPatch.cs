using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using UnityEngine;

namespace QuickFury {
    /// <summary>
    /// Replaces Armature Link's per-bone skin mutation with one chronological replay per
    /// skin. VRCFury normally clones and assigns the same bones/bindposes arrays thousands
    /// of times; this records the exact transforms at each call and commits each skin once
    /// immediately before deferred hierarchy moves are applied.
    /// </summary>
    internal static class ArmatureSkinIndexPatch {
        private sealed class Rewrite {
            internal Transform From;
            internal Transform To;
            internal Matrix4x4 BindposeDelta;
        }

        private sealed class Context {
            internal GameObject Avatar;
            internal readonly List<Rewrite> Rewrites = new List<Rewrite>();
            internal bool Flushed;
        }

        [ThreadStatic] private static Context active;

        private static FieldInfo avatarObjectField;
        private static FieldInfo gameObjectField;
        private static MethodInfo getMutableMesh;
        private static MethodInfo dirty;

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            var armatureType = VrcfuryCompatibility.FindType("VF.Service.ArmatureLinkService");
            var vfGameObjectType = VrcfuryCompatibility.FindType("VF.Utils.VFGameObject");
            var rendererExtensions = VrcfuryCompatibility.FindType("VF.Utils.RendererExtensions");
            var dirtyUtils = VrcfuryCompatibility.FindType("VF.Utils.DirtyUtils");

            var apply = armatureType?.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "Apply" && method.GetParameters().Length == 0);
            var rewriteSkins = armatureType?
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => {
                    if (method.Name != "RewriteSkins" || method.ReturnType != typeof(void)) return false;
                    return method.GetParameters().Length == 3;
                });

            avatarObjectField = armatureType?.GetField("avatarObject", BindingFlags.Instance | BindingFlags.NonPublic);
            gameObjectField = vfGameObjectType?.GetField("_gameObject", BindingFlags.Instance | BindingFlags.NonPublic);
            getMutableMesh = rendererExtensions?
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => {
                    if (method.Name != "GetMutableMesh" || method.ReturnType != typeof(Mesh)) return false;
                    var parameters = method.GetParameters();
                    return parameters.Length == 2
                           && parameters[0].ParameterType == typeof(Renderer)
                           && parameters[1].ParameterType == typeof(string);
                });
            dirty = dirtyUtils?
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => {
                    if (method.Name != "Dirty" || method.ReturnType != typeof(void)) return false;
                    var parameters = method.GetParameters();
                    return parameters.Length == 1 && parameters[0].ParameterType == typeof(UnityEngine.Object);
                });

            if (apply == null || rewriteSkins == null || compatibility.ApplyDeferred == null
                              || avatarObjectField == null || gameObjectField == null
                              || getMutableMesh == null || dirty == null) {
                Debug.LogWarning("[QuickFury] Batched Armature skin rewrite disabled: target signature mismatch.");
                return;
            }

            try {
                harmony.Patch(
                    apply,
                    prefix: new HarmonyMethod(typeof(ArmatureSkinIndexPatch), nameof(Begin)),
                    finalizer: new HarmonyMethod(typeof(ArmatureSkinIndexPatch), nameof(End))
                );
                harmony.Patch(
                    rewriteSkins,
                    prefix: new HarmonyMethod(typeof(ArmatureSkinIndexPatch), nameof(RecordRewrite))
                );
                harmony.Patch(
                    compatibility.ApplyDeferred,
                    prefix: new HarmonyMethod(typeof(ArmatureSkinIndexPatch), nameof(Flush))
                );
            } catch (Exception e) {
                Debug.LogWarning("[QuickFury] Batched Armature skin rewrite disabled: " + e.Message);
            }
        }

        private static void Begin(object __instance) {
            active = null;
            if (!QuickFurySettings.SkinIndex) return;

            try {
                var avatarWrapper = avatarObjectField.GetValue(__instance);
                var avatar = ArmatureReflection.GetGameObject(avatarWrapper, gameObjectField);
                if (avatar == null) return;
                active = new Context { Avatar = avatar };
            } catch (Exception e) {
                Debug.LogWarning("[QuickFury] Batched skin rewrite fell back to VRCFury: " + e.Message);
            }
        }

        private static Exception End(Exception __exception) {
            active = null;
            return __exception;
        }

        private static bool RecordRewrite(object __0, object __1) {
            var context = active;
            if (context == null || context.Flushed) return true;

            var from = ArmatureReflection.GetGameObject(__0, gameObjectField)?.transform;
            var to = ArmatureReflection.GetGameObject(__1, gameObjectField)?.transform;
            if (from == null || to == null) return true;

            context.Rewrites.Add(new Rewrite {
                From = from,
                To = to,
                // Capture this now. Later Armature Links can align a parent and change
                // from.localToWorldMatrix before the batch is committed.
                BindposeDelta = to.worldToLocalMatrix * from.localToWorldMatrix
            });
            return false;
        }

        private static void Flush() {
            var context = active;
            if (context == null || context.Flushed) return;
            context.Flushed = true;

            if (context.Avatar == null || context.Rewrites.Count == 0) return;

            foreach (var skin in context.Avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                if (skin == null) continue;
                RewriteSkin(skin, context.Rewrites);
            }
        }

        private static void RewriteSkin(SkinnedMeshRenderer skin, IReadOnlyList<Rewrite> rewrites) {
            var bones = skin.bones;
            if (bones == null || bones.Length == 0) return;

            var slotsByBone = new Dictionary<Transform, List<int>>();
            for (var i = 0; i < bones.Length; i++) {
                var bone = bones[i];
                if (bone == null) continue;
                if (!slotsByBone.TryGetValue(bone, out var slots)) {
                    slots = new List<int>();
                    slotsByBone[bone] = slots;
                }
                slots.Add(i);
            }

            Mesh mesh = null;
            Matrix4x4[] bindposes = null;
            var changed = false;

            foreach (var rewrite in rewrites) {
                if (rewrite.From == null || rewrite.To == null) continue;
                if (!slotsByBone.TryGetValue(rewrite.From, out var slots) || slots.Count == 0) continue;

                if (!changed) {
                    mesh = InvokeUnwrapped(getMutableMesh, null, new object[] {
                        skin,
                        "Needed to change bone bind-poses for Armature Link to re-use bones on base armature"
                    }) as Mesh;
                    bindposes = mesh?.bindposes;
                    changed = true;
                }

                foreach (var slot in slots) {
                    if (bindposes != null && slot < bindposes.Length) {
                        bindposes[slot] = rewrite.BindposeDelta * bindposes[slot];
                    }
                    bones[slot] = rewrite.To;
                }

                slotsByBone.Remove(rewrite.From);
                if (!slotsByBone.TryGetValue(rewrite.To, out var destinationSlots)) {
                    destinationSlots = new List<int>();
                    slotsByBone[rewrite.To] = destinationSlots;
                }
                destinationSlots.AddRange(slots);
            }

            if (!changed) return;

            if (mesh != null && bindposes != null) {
                // Enumerable.Zip in VRCFury truncates to the shorter of bones and bindposes
                // on the first rewrite. Preserve that unusual edge case exactly.
                var count = Math.Min(bones.Length, bindposes.Length);
                if (bindposes.Length != count) Array.Resize(ref bindposes, count);
                mesh.bindposes = bindposes;
            }

            skin.bones = bones;
            InvokeUnwrapped(dirty, null, new object[] { skin });
        }

        private static object InvokeUnwrapped(MethodInfo method, object instance, object[] args) {
            try {
                return method.Invoke(instance, args);
            } catch (TargetInvocationException e) when (e.InnerException != null) {
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }
    }
}
