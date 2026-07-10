using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using UnityEngine;

namespace QuickFury {
    /// <summary>
    /// Indexes skin bone usage once, then runs VRCFury's bind-pose formula only on skins
    /// that actually contain the source bone. VRCFury's original implementation scans every
    /// SkinnedMeshRenderer for every reusable merged bone.
    /// </summary>
    internal static class ArmatureSkinIndexPatch {
        private sealed class Context {
            internal readonly Dictionary<Transform, List<SkinnedMeshRenderer>> ByBone =
                new Dictionary<Transform, List<SkinnedMeshRenderer>>();
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

            if (apply == null || rewriteSkins == null || avatarObjectField == null || gameObjectField == null
                              || getMutableMesh == null || dirty == null) {
                Debug.LogWarning("[QuickFury] Armature skin index disabled: target signature mismatch.");
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
                    prefix: new HarmonyMethod(typeof(ArmatureSkinIndexPatch), nameof(RewriteSkins))
                );
            } catch (Exception e) {
                Debug.LogWarning("[QuickFury] Armature skin index disabled: " + e.Message);
            }
        }

        private static void Begin(object __instance) {
            active = null;
            if (!QuickFurySettings.SkinIndex) return;

            try {
                var avatarWrapper = avatarObjectField.GetValue(__instance);
                var avatar = ArmatureReflection.GetGameObject(avatarWrapper, gameObjectField);
                if (avatar == null) return;

                var context = new Context();
                foreach (var skin in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                    if (skin == null) continue;
                    foreach (var bone in skin.bones.Where(bone => bone != null).Distinct()) {
                        Add(context, bone, skin);
                    }
                }
                active = context;
            } catch (Exception e) {
                active = null;
                Debug.LogWarning("[QuickFury] Skin index fell back to VRCFury: " + e.Message);
            }
        }

        private static Exception End(Exception __exception) {
            active = null;
            return __exception;
        }

        private static bool RewriteSkins(object __0, object __1) {
            var context = active;
            if (context == null) return true;

            var from = ArmatureReflection.GetGameObject(__0, gameObjectField)?.transform;
            var to = ArmatureReflection.GetGameObject(__1, gameObjectField)?.transform;
            if (from == null || to == null) return true;

            if (!context.ByBone.TryGetValue(from, out var indexedSkins) || indexedSkins.Count == 0) {
                return false;
            }

            foreach (var skin in indexedSkins.ToArray()) {
                if (skin == null) continue;
                var originalBones = skin.bones;
                if (!originalBones.Contains(from)) continue;

                var mesh = InvokeUnwrapped(getMutableMesh, null, new object[] {
                    skin,
                    "Needed to change bone bind-poses for Armature Link to re-use bones on base armature"
                }) as Mesh;
                if (mesh != null) {
                    var originalBindposes = mesh.bindposes;
                    var count = Math.Min(originalBones.Length, originalBindposes.Length);
                    var rewrittenBindposes = new Matrix4x4[count];
                    for (var i = 0; i < count; i++) {
                        var bone = originalBones[i];
                        var bindpose = originalBindposes[i];
                        rewrittenBindposes[i] = bone == from
                            ? to.worldToLocalMatrix * bone.localToWorldMatrix * bindpose
                            : bindpose;
                    }
                    mesh.bindposes = rewrittenBindposes;
                }

                var currentBones = skin.bones;
                var rewrittenBones = new Transform[currentBones.Length];
                var replaced = false;
                for (var i = 0; i < currentBones.Length; i++) {
                    if (currentBones[i] == from) {
                        rewrittenBones[i] = to;
                        replaced = true;
                    } else {
                        rewrittenBones[i] = currentBones[i];
                    }
                }
                skin.bones = rewrittenBones;
                InvokeUnwrapped(dirty, null, new object[] { skin });

                if (replaced) Add(context, to, skin);
            }

            context.ByBone.Remove(from);
            return false;
        }

        private static void Add(Context context, Transform bone, SkinnedMeshRenderer skin) {
            if (!context.ByBone.TryGetValue(bone, out var skins)) {
                skins = new List<SkinnedMeshRenderer>();
                context.ByBone[bone] = skins;
            }
            if (!skins.Contains(skin)) skins.Add(skin);
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
