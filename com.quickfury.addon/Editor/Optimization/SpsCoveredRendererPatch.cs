using System;
using System.Collections;
using System.Collections.Generic;
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
            internal int Probes;
            internal int Skipped;
            internal readonly HashSet<Transform> CoveredOwners = new HashSet<Transform>();

            internal void AddOwner(Transform owner) {
                if (owner != null) CoveredOwners.Add(owner);
            }
        }

        [ThreadStatic] private static Context active;
        private static Type plugComponentType;
        internal static string LastStats { get; private set; } = "none";

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            var upgraderType = VrcfuryCompatibility.FindType("VF.Builder.Haptics.SpsUpgrader");
            var plugEditorType = VrcfuryCompatibility.FindType("VF.Inspector.VRCFuryHapticPlugEditor");
            var sizeDetectorType = VrcfuryCompatibility.FindType("VF.Builder.Haptics.PlugSizeDetector");
            plugComponentType = VrcfuryCompatibility.FindType("VF.Component.VRCFuryHapticPlug");

            var apply = VrcfuryCompatibility.FindUniqueMethod(
                upgraderType,
                "Apply",
                method => method.GetParameters().Length == 3
            );
            var getRenderers = VrcfuryCompatibility.FindUniqueMethod(
                plugEditorType,
                "GetRenderers",
                method => method.GetParameters().Length == 1
            );
            var getAutoWorldSize = VrcfuryCompatibility.FindUniqueMethod(
                sizeDetectorType,
                "GetAutoWorldSize",
                method => method.GetParameters().Length == 1
                          && method.GetParameters()[0].ParameterType == typeof(Renderer)
            );

            if (apply == null || getRenderers == null || getAutoWorldSize == null || plugComponentType == null) {
                throw new InvalidOperationException("target signature mismatch");
            }

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
        }

        private static void Begin(object mode) {
            active = QuickFurySettings.SpsCoveredRenderer
                     && mode != null
                     && mode.ToString() == "AutomatedForEveryone"
                ? new Context()
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
                // GetRenderers pass. Include those live components as well, and remember
                // them so other renderers under the same plug take the list path above.
                for (var current = owner; current != null; current = current.parent) {
                    if (current.GetComponent(plugComponentType) != null) {
                        context.AddOwner(current);
                        context.Skipped++;
                        return false;
                    }
                }
                var childPlug = owner.GetComponentInChildren(plugComponentType, true);
                if (childPlug != null) {
                    context.AddOwner(childPlug.transform);
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
