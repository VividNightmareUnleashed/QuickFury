using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace QuickFury {
    /// <summary>
    /// SpsUpgrader's automatic DPS/TPS discovery bakes a renderer's complete mesh to
    /// prove its size before AddPlug checks whether a plug already covers that object.
    /// Preserve the later AddPlug rejection, but avoid the multi-second mesh bake when
    /// the renderer is already above/below an existing or newly unbaked plug.
    /// </summary>
    internal static class SpsCoveredRendererPatch {
        private sealed class Context {
            internal Type PlugType;
            internal int Probes;
            internal int Skipped;
            internal readonly List<Transform> CoveredOwners = new List<Transform>();
            internal readonly HashSet<int> CoveredOwnerIds = new HashSet<int>();

            internal void AddOwner(Transform owner) {
                if (owner == null || !CoveredOwnerIds.Add(owner.GetInstanceID())) return;
                CoveredOwners.Add(owner);
            }
        }

        [ThreadStatic] private static Context active;
        internal static string LastStats { get; private set; } = "none";

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            var upgraderType = VrcfuryCompatibility.FindType("VF.Builder.Haptics.SpsUpgrader");
            var plugEditorType = VrcfuryCompatibility.FindType("VF.Inspector.VRCFuryHapticPlugEditor");
            var sizeDetectorType = VrcfuryCompatibility.FindType("VF.Builder.Haptics.PlugSizeDetector");
            var plugType = VrcfuryCompatibility.FindType("VF.Component.VRCFuryHapticPlug");

            var apply = upgraderType?.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "Apply" && method.GetParameters().Length == 3);
            var getRenderers = plugEditorType?
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "GetRenderers"
                                           && method.GetParameters().Length == 1);
            var getAutoWorldSize = sizeDetectorType?
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "GetAutoWorldSize"
                                           && method.GetParameters().Length == 1
                                           && method.GetParameters()[0].ParameterType == typeof(Renderer));

            if (apply == null || getRenderers == null || getAutoWorldSize == null || plugType == null) {
                Debug.LogWarning("[QuickFury] Covered SPS mesh probe skip disabled: target signature mismatch.");
                return;
            }

            try {
                harmony.Patch(
                    apply,
                    prefix: new HarmonyMethod(typeof(SpsCoveredRendererPatch), nameof(Begin)),
                    finalizer: new HarmonyMethod(typeof(SpsCoveredRendererPatch), nameof(End))
                );
                harmony.Patch(
                    getRenderers,
                    postfix: new HarmonyMethod(typeof(SpsCoveredRendererPatch), nameof(CaptureRenderers))
                );
                harmony.Patch(
                    getAutoWorldSize,
                    prefix: new HarmonyMethod(typeof(SpsCoveredRendererPatch), nameof(SkipCovered))
                );
            } catch (Exception e) {
                Debug.LogWarning("[QuickFury] Covered SPS mesh probe skip disabled: " + e.Message);
            }

            plugComponentType = plugType;
        }

        private static Type plugComponentType;

        private static void Begin(object mode) {
            active = QuickFurySettings.SpsCoveredRenderer
                     && mode != null
                     && mode.ToString() == "AutomatedForEveryone"
                ? new Context { PlugType = plugComponentType }
                : null;
        }

        private static Exception End(Exception __exception) {
            if (active != null) LastStats = active.Skipped + "/" + active.Probes;
            active = null;
            return __exception;
        }

        private static void CaptureRenderers(object plug, object __result) {
            var context = active;
            if (context == null) return;

            if (plug is Component component) context.AddOwner(component.transform);
            if (!(__result is IEnumerable renderers)) return;
            foreach (var item in renderers) {
                if (item is Renderer renderer) context.AddOwner(renderer.transform);
            }
        }

        private static bool SkipCovered(Renderer renderer) {
            var context = active;
            if (context == null || renderer == null) return true;
            context.Probes++;

            try {
                var owner = renderer.transform;
                foreach (var covered in context.CoveredOwners) {
                    if (covered == null) continue;
                    if (owner == covered || owner.IsChildOf(covered) || covered.IsChildOf(owner)) {
                        // Returning false leaves the reference-type result null. The caller
                        // consequently skips AddPlug, which would have rejected this object.
                        context.Skipped++;
                        return false;
                    }
                }

                // Unbaking earlier in SpsUpgrader.Apply can add a plug after the initial
                // GetRenderers pass. Include those live components as well.
                for (var current = owner; current != null; current = current.parent) {
                    if (current.GetComponent(context.PlugType) != null) {
                        context.Skipped++;
                        return false;
                    }
                }
                if (owner.GetComponentsInChildren(context.PlugType, true).Length > 0) {
                    context.Skipped++;
                    return false;
                }
                return true;
            } catch (Exception e) {
                active = null;
                Debug.LogWarning("[QuickFury] Covered SPS mesh probe skip fell back to VRCFury: " + e.Message);
                return true;
            }
        }
    }
}
