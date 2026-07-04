using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace QuickFury {
    /**
     * VF.Service.ArmatureLinkService.RewriteSkins runs once per merged bone (in "reuse bone" mode)
     * and, per call, scans the ENTIRE avatar for SkinnedMeshRenderers and marshals each skin's
     * native bones array up to three times (Contains + Zip + Select). With a 200-bone outfit and
     * 8 skins that's 200 full-avatar scans and thousands of full-array copies.
     *
     * Behavior-identical replacement backed by two per-action caches:
     * - the SkinnedMeshRenderer list for the avatar (1 scan per action instead of per bone)
     * - each skin's managed bones array, write-through (1 native read per skin per action)
     * Only this method writes skin.bones during the ArmatureLink action, so the write-through
     * cache cannot go stale; both caches die at the next action boundary.
     */
    internal static class SkinRewritePatch {
        private static MethodInfo getMutableMeshM;
        private static MethodInfo dirtyM;

        private static int skinsVersion = -1;
        private static int skinsKey;
        private static SkinnedMeshRenderer[] skins;

        private static readonly QfScopedDict<SkinnedMeshRenderer, Transform[]> bonesCache =
            new QfScopedDict<SkinnedMeshRenderer, Transform[]>();

        public static void Apply(Harmony h) {
            var armatureLink = QfReflect.ReqType("VF.Service.ArmatureLinkService");
            var rendererExt = QfReflect.ReqType("VF.Utils.RendererExtensions");
            var dirtyUtils = QfReflect.ReqType("VF.Utils.DirtyUtils");
            QfReflect.WarmGo();
            getMutableMeshM = QfReflect.ReqMethod(rendererExt, "GetMutableMesh", new[] { typeof(Renderer), typeof(string) });
            dirtyM = QfReflect.ReqMethod(dirtyUtils, "Dirty", new[] { typeof(Object) });
            h.Patch(QfReflect.ReqMethod(armatureLink, "RewriteSkins"),
                prefix: new HarmonyMethod(typeof(SkinRewritePatch), nameof(Prefix)));
        }

        private static bool Prefix(object fromBone, object toBone, object avatarObject) {
            if (!QfSettings.SkinRewriteCache || !QfState.InBuild) return true;
            var fromGo = QfReflect.Go(fromBone);
            var toGo = QfReflect.Go(toBone);
            var avatarGo = QfReflect.Go(avatarObject);
            if (fromGo == null || toGo == null || avatarGo == null) return true;
            var fromT = fromGo.transform;
            var toT = toGo.transform;

            var key = avatarGo.GetInstanceID();
            if (skinsVersion != QfState.scopeVersion || skinsKey != key) {
                var found = avatarGo.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var list = new List<SkinnedMeshRenderer>(found.Length);
                foreach (var s in found) if (s != null) list.Add(s);
                skins = list.ToArray();
                skinsVersion = QfState.scopeVersion;
                skinsKey = key;
            }

            var cache = bonesCache.Get();
            foreach (var skin in skins) {
                if (skin == null) continue;
                if (!cache.TryGetValue(skin, out var bones)) {
                    bones = skin.bones;
                    cache[skin] = bones;
                }
                if (Array.IndexOf(bones, fromT) < 0) continue;

                var mesh = (Mesh)QfReflect.Invoke(getMutableMeshM, null, skin,
                    "Needed to change bone bind-poses for Armature Link to re-use bones on base armature");
                if (mesh != null) {
                    var bindposes = mesh.bindposes;
                    // Stock uses bones.Zip(bindposes), which truncates to the shorter of the two.
                    var n = Math.Min(bones.Length, bindposes.Length);
                    var newBindposes = new Matrix4x4[n];
                    for (var i = 0; i < n; i++) {
                        var bone = bones[i];
                        newBindposes[i] = bone == fromT
                            ? toT.worldToLocalMatrix * bone.localToWorldMatrix * bindposes[i]
                            : bindposes[i];
                    }
                    mesh.bindposes = newBindposes;
                }

                var newBones = new Transform[bones.Length];
                for (var i = 0; i < bones.Length; i++) {
                    newBones[i] = bones[i] == fromT ? toT : bones[i];
                }
                skin.bones = newBones;
                cache[skin] = newBones;
                QfReflect.Invoke(dirtyM, null, (Object)skin);
            }
            return false;
        }
    }
}
