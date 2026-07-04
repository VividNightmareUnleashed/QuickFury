using System.Collections.Generic;
using System.Collections.Immutable;
using HarmonyLib;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace QuickFury {
    /**
     * VF.Utils.AnimatorIterator.Motions.From(Motion) calls the native
     * AnimationUtility.GetAnimationClipSettings for EVERY clip it visits, purely to discover
     * additive reference pose clips — and controller traversals run dozens of times per build
     * (38 GetAllUsedControllers call sites re-walk the same graphs).
     *
     * This replaces the traversal with an identical one that caches clip -> additiveReferencePoseClip
     * per build action. VRCFury's only writer of clip settings during a build (SetLooping) never
     * touches the additive reference pose, and the cache dies at every action boundary anyway.
     */
    internal static class MotionsCachePatch {
        private static readonly QfScopedDict<AnimationClip, Motion> additiveCache =
            new QfScopedDict<AnimationClip, Motion>();

        public static void Apply(Harmony h) {
            var t = QfReflect.ReqType("VF.Utils.AnimatorIterator+Motions");
            h.Patch(QfReflect.ReqMethod(t, "From", new[] { typeof(Motion) }),
                prefix: new HarmonyMethod(typeof(MotionsCachePatch), nameof(Prefix)));
        }

        private static bool Prefix(Motion root, ref IImmutableSet<Motion> __result) {
            if (!QfSettings.ClipSettingsCache || !QfState.InBuild) return true;

            var cache = additiveCache.Get();
            // Same traversal as AnimatorIterator.GetRecursive specialized for Motions.
            var all = new HashSet<Motion>();
            var stack = new Stack<Motion>();
            stack.Push(root);
            while (stack.Count > 0) {
                var one = stack.Pop();
                if (one == null) continue;
                if (!all.Add(one)) continue;
                if (one is BlendTree tree) {
                    foreach (var child in tree.children) {
                        stack.Push(child.motion);
                    }
                } else if (one is AnimationClip clip) {
                    if (!cache.TryGetValue(clip, out var refPose)) {
                        refPose = AnimationUtility.GetAnimationClipSettings(clip).additiveReferencePoseClip;
                        cache[clip] = refPose;
                    }
                    stack.Push(refPose);
                }
            }
            __result = all.ToImmutableHashSet();
            return false;
        }
    }
}
