using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace QuickFury {
    /**
     * VF.Service.ObjectMoveService has two hot spots:
     *
     * 1. Move() rebuilds the "immovable bones" set (every humanoid bone plus its full ancestor
     *    chain, ~300-500 hash inserts) on EVERY call — and ArmatureLink calls Move once per merged
     *    bone. The set depends only on the avatar's humanoid rig, which cannot change mid-action
     *    (Move itself throws if anything tries to move a bone). Cached per build action here.
     *
     * 2. ApplyDeferred() checks every deferred (from -> to) pair against every distinct binding
     *    path, allocating a fresh "from + \"/\"" string on every single iteration. Hoisted so the
     *    prefix strings are built once per batch. (Prefix comparison uses Ordinal, matching how
     *    Unity itself treats animation paths; stock VRCFury mixes culture-sensitive StartsWith
     *    with ordinal ==.)
     *
     * Both methods are full behavior-identical replacements; every reflection member is resolved
     * at patch time so a VRCFury change makes the whole patch fail cleanly rather than half-apply.
     */
    internal static class MoveCachePatch {
        private static FieldInfo avatarObjectF;
        private static FieldInfo deferredF;
        private static FieldInfo allClipsServiceF;
        private static MethodInfo getAllBonesM;
        private static MethodInfo getAnimatedPathM;
        private static MethodInfo ensureAnimationSafeNameM;
        private static MethodInfo removeFromPhysbonesM;
        private static MethodInfo applyDeferredM;
        private static MethodInfo rewritePathM;
        private static MethodInfo rewriteAllClipsM;
        private static PropertyInfo kvpKeyP;
        private static PropertyInfo kvpValueP;

        private static int immovableVersion = -1;
        private static int immovableKey;
        private static readonly HashSet<Transform> immovable = new HashSet<Transform>();

        public static void Apply(Harmony h) {
            var moveService = QfReflect.ReqType("VF.Service.ObjectMoveService");
            var armatureUtils = QfReflect.ReqType("VF.Builder.VRCFArmatureUtils");
            var avatarHook = QfReflect.ReqType("VF.Hooks.VRCFuryAvatarHook");
            var physboneUtils = QfReflect.ReqType("VF.Utils.PhysboneUtils");
            var animationRewriter = QfReflect.ReqType("VF.Utils.AnimationRewriter");
            var allClipsService = QfReflect.ReqType("VF.Service.AllClipsService");
            var vfGo = QfReflect.VfGoType;

            QfReflect.WarmGo();
            avatarObjectF = QfReflect.ReqField(moveService, "avatarObject");
            deferredF = QfReflect.ReqField(moveService, "deferred");
            allClipsServiceF = QfReflect.ReqField(moveService, "allClipsService");
            getAllBonesM = QfReflect.ReqMethod(armatureUtils, "GetAllBones", new[] { vfGo });
            getAnimatedPathM = QfReflect.ReqMethod(avatarHook, "GetAnimatedPath", new[] { vfGo });
            ensureAnimationSafeNameM = QfReflect.ReqMethod(vfGo, "EnsureAnimationSafeName");
            removeFromPhysbonesM = QfReflect.ReqMethod(physboneUtils, "RemoveFromPhysbones");
            applyDeferredM = QfReflect.ReqMethod(moveService, "ApplyDeferred");
            rewritePathM = QfReflect.ReqMethod(animationRewriter, "RewritePath", new[] { typeof(Func<string, string>) });
            rewriteAllClipsM = QfReflect.ReqMethod(allClipsService, "RewriteAllClips");

            h.Patch(QfReflect.ReqMethod(moveService, "Move"),
                prefix: new HarmonyMethod(typeof(MoveCachePatch), nameof(MovePrefix)));
            h.Patch(applyDeferredM,
                prefix: new HarmonyMethod(typeof(MoveCachePatch), nameof(ApplyDeferredPrefix)));
        }

        private static bool MovePrefix(object __instance, object obj, object newParent, string newName, bool worldPositionStays, bool defer) {
            if (!QfSettings.MoveCache || !QfState.InBuild) return true;
            var avatarVf = avatarObjectF.GetValue(__instance);
            var avatarGo = QfReflect.Go(avatarVf);
            var objGo = QfReflect.Go(obj);
            if (avatarGo == null || objGo == null) return true;

            BuildImmovableSet(avatarVf, avatarGo);
            if (immovable.Contains(objGo.transform)) {
                throw new Exception(
                    $"VRCFury is trying to move the {objGo.name} object, but bones / root avatar objects cannot be moved." +
                    $" You are probably trying to do something weird in one of your VRCFury components. Don't do that.");
            }

            var oldPath = (string)QfReflect.Invoke(getAnimatedPathM, null, obj);
            if (!ReferenceEquals(newParent, null)) {
                var newParentGo = QfReflect.Go(newParent);
                if (newParentGo != null) {
                    objGo.transform.SetParent(newParentGo.transform, worldPositionStays);
                }
            }
            if (newName != null) {
                objGo.name = newName;
            }
            QfReflect.Invoke(ensureAnimationSafeNameM, obj);
            var newPath = (string)QfReflect.Invoke(getAnimatedPathM, null, obj);
            QfReflect.Invoke(removeFromPhysbonesM, null, obj, true);
            var deferred = (List<(string, string)>)deferredF.GetValue(__instance);
            deferred.Add((oldPath, newPath));
            if (!defer) {
                QfReflect.Invoke(applyDeferredM, __instance);
            }
            return false;
        }

        private static void BuildImmovableSet(object avatarVf, GameObject avatarGo) {
            var key = avatarGo.GetInstanceID();
            if (immovableVersion == QfState.scopeVersion && immovableKey == key) return;

            immovable.Clear();
            immovable.Add(avatarGo.transform);
            var bones = (IEnumerable)QfReflect.Invoke(getAllBonesM, null, avatarVf);
            foreach (var pair in bones) {
                if (kvpKeyP == null) {
                    var kvpType = pair.GetType();
                    kvpKeyP = kvpType.GetProperty("Key");
                    kvpValueP = kvpType.GetProperty("Value");
                }
                var bone = (HumanBodyBones)kvpKeyP.GetValue(pair);
                // Eyes are excluded, matching stock: vrc controls them and the cross-eye fix moves them
                if (bone == HumanBodyBones.LeftEye || bone == HumanBodyBones.RightEye) continue;
                var boneGo = QfReflect.Go(kvpValueP.GetValue(pair));
                var current = boneGo == null ? null : boneGo.transform;
                while (current != null && current.gameObject != avatarGo) {
                    immovable.Add(current);
                    current = current.parent;
                }
            }
            immovableVersion = QfState.scopeVersion;
            immovableKey = key;
        }

        private static bool ApplyDeferredPrefix(object __instance) {
            if (!QfSettings.MoveCache || !QfState.InBuild) return true;
            var deferred = (List<(string, string)>)deferredF.GetValue(__instance);

            var pairs = new (string from, string fromSlash, string to)[deferred.Count];
            for (var i = 0; i < deferred.Count; i++) {
                var (from, to) = deferred[i];
                pairs[i] = (from, from + "/", to);
            }
            Func<string, string> rewritePath = path => {
                foreach (var p in pairs) {
                    if (path.StartsWith(p.fromSlash, StringComparison.Ordinal) || path == p.from) {
                        path = p.to + path.Substring(p.from.Length);
                    }
                }
                return path;
            };

            var rewriter = QfReflect.Invoke(rewritePathM, null, rewritePath);
            QfReflect.Invoke(rewriteAllClipsM, allClipsServiceF.GetValue(__instance), rewriter);
            deferred.Clear();
            return false;
        }
    }
}
