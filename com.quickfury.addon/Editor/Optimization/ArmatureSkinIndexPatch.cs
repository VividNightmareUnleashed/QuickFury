using System;
using System.Collections.Generic;
using System.Reflection;
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
        }

        [ThreadStatic] private static Context active;

        private static MethodInfo getMutableMesh;
        private static MethodInfo dirty;

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            var armatureType = VrcfuryCompatibility.FindType("VF.Service.ArmatureLinkService");
            var rendererExtensions = VrcfuryCompatibility.FindType("VF.Utils.RendererExtensions");
            var dirtyUtils = VrcfuryCompatibility.FindType("VF.Utils.DirtyUtils");

            var rewriteSkins = VrcfuryCompatibility.FindUniqueMethod(
                armatureType,
                "RewriteSkins",
                method => method.ReturnType == typeof(void) && method.GetParameters().Length == 3
            );
            getMutableMesh = VrcfuryCompatibility.FindMethodWithSignature(
                rendererExtensions,
                "GetMutableMesh",
                typeof(Mesh),
                typeof(Renderer),
                typeof(string)
            );
            dirty = VrcfuryCompatibility.FindMethodWithSignature(
                dirtyUtils,
                "Dirty",
                typeof(void),
                typeof(UnityEngine.Object)
            );

            if (!ArmatureReflection.ArmatureLinkAvailable || rewriteSkins == null
                || compatibility.ApplyDeferred == null || getMutableMesh == null || dirty == null) {
                throw new InvalidOperationException("target signature mismatch");
            }

            harmony.Patch(
                ArmatureReflection.ArmatureLinkApply,
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
        }

        private static void Begin(object __instance) {
            active = null;
            if (!QuickFurySettings.SkinIndex) return;

            try {
                var avatar = ArmatureReflection.GetAvatar(__instance, ArmatureReflection.ArmatureLinkAvatarField);
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
            if (context == null) return true;

            var from = ArmatureReflection.GetGameObject(__0)?.transform;
            var to = ArmatureReflection.GetGameObject(__1)?.transform;
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
            active = null;
            if (context == null) return;

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
                if (bone != null) slotsByBone.GetOrAddList(bone).Add(i);
            }

            Mesh mesh = null;
            Matrix4x4[] bindposes = null;
            var changed = false;

            foreach (var rewrite in rewrites) {
                if (rewrite.From == null || rewrite.To == null) continue;
                if (!slotsByBone.TryGetValue(rewrite.From, out var slots) || slots.Count == 0) continue;

                if (!changed) {
                    mesh = VrcfuryCompatibility.InvokeUnwrapped(getMutableMesh, null, new object[] {
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
                slotsByBone.GetOrAddList(rewrite.To).AddRange(slots);
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
            VrcfuryCompatibility.InvokeUnwrapped(dirty, null, new object[] { skin });
        }
    }
}
