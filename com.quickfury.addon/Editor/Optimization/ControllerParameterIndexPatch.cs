using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEditor.Animations;
using UnityEngine;

namespace QuickFury {
    /// <summary>
    /// VFController performs an Array.Find and a full parameters-array copy for every
    /// parameter it creates. Keep an exact name index during the build and use Unity's
    /// native AddParameter API, while invalidating around the handful of bulk rewrites.
    /// </summary>
    internal static class ControllerParameterIndexPatch {
        private sealed class Entry {
            internal AnimatorController Controller;
            internal readonly Dictionary<string, AnimatorControllerParameter> ByName =
                new Dictionary<string, AnimatorControllerParameter>(StringComparer.Ordinal);
        }

        [ThreadStatic] private static Dictionary<int, Entry> active;
        private static FieldInfo controllerField;

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            var type = VrcfuryCompatibility.FindType("VF.Utils.Controller.VFController");
            controllerField = type?.GetField("ctrl", BindingFlags.Instance | BindingFlags.NonPublic);
            var getParam = type?
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "GetParam"
                                           && method.ReturnType == typeof(AnimatorControllerParameter)
                                           && method.GetParameters().Length == 1
                                           && method.GetParameters()[0].ParameterType == typeof(string));
            var newParam = type?
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "_NewParam"
                                           && method.ReturnType == typeof(AnimatorControllerParameter)
                                           && method.GetParameters().Length == 3);
            var mutators = type?
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => new[] {
                    "TakeOwnershipOf", "RemoveParameter", "RemoveInvalidParameters",
                    "RewriteParameters", "UpgradeWrongParamTypes", "set_parameters"
                }.Contains(method.Name))
                .ToArray() ?? Array.Empty<MethodInfo>();

            if (controllerField == null || getParam == null || newParam == null || mutators.Length < 6) {
                Debug.LogWarning("[QuickFury] Controller parameter index disabled: target signature mismatch.");
                return;
            }

            try {
                harmony.Patch(
                    compatibility.RunMain,
                    prefix: new HarmonyMethod(typeof(ControllerParameterIndexPatch), nameof(Begin)),
                    finalizer: new HarmonyMethod(typeof(ControllerParameterIndexPatch), nameof(End))
                );
                harmony.Patch(
                    getParam,
                    prefix: new HarmonyMethod(typeof(ControllerParameterIndexPatch), nameof(GetParam))
                );
                harmony.Patch(
                    newParam,
                    prefix: new HarmonyMethod(typeof(ControllerParameterIndexPatch), nameof(NewParam))
                );
                foreach (var mutator in mutators) {
                    harmony.Patch(
                        mutator,
                        prefix: new HarmonyMethod(typeof(ControllerParameterIndexPatch), nameof(Invalidate)),
                        postfix: new HarmonyMethod(typeof(ControllerParameterIndexPatch), nameof(Invalidate))
                    );
                }
            } catch (Exception e) {
                Debug.LogWarning("[QuickFury] Controller parameter index disabled: " + e.Message);
            }
        }

        private static void Begin() {
            active = QuickFurySettings.ControllerParameterIndex
                ? new Dictionary<int, Entry>()
                : null;
        }

        private static Exception End(Exception __exception) {
            active = null;
            return __exception;
        }

        private static bool GetParam(object __instance, string name, ref AnimatorControllerParameter __result) {
            var entry = GetEntry(__instance);
            if (entry == null) return true;
            entry.ByName.TryGetValue(name, out __result);
            return false;
        }

        private static bool NewParam(
            object __instance,
            string name,
            AnimatorControllerParameterType type,
            Action<AnimatorControllerParameter> with,
            ref AnimatorControllerParameter __result
        ) {
            var entry = GetEntry(__instance);
            if (entry == null) return true;
            if (entry.ByName.TryGetValue(name, out __result)) return false;

            var parameter = new AnimatorControllerParameter { name = name, type = type };
            with?.Invoke(parameter);
            entry.Controller.AddParameter(parameter);
            entry.ByName[name] = parameter;
            __result = parameter;
            return false;
        }

        private static void Invalidate(object __instance) {
            var context = active;
            if (context == null || __instance == null) return;
            var controller = controllerField.GetValue(__instance) as AnimatorController;
            if (controller != null) context.Remove(controller.GetInstanceID());
        }

        private static Entry GetEntry(object wrapper) {
            var context = active;
            if (context == null || wrapper == null) return null;
            var controller = controllerField.GetValue(wrapper) as AnimatorController;
            if (controller == null) return null;
            var id = controller.GetInstanceID();
            if (context.TryGetValue(id, out var existing)) return existing;

            var created = new Entry { Controller = controller };
            foreach (var parameter in controller.parameters) {
                if (parameter != null && !created.ByName.ContainsKey(parameter.name)) {
                    created.ByName.Add(parameter.name, parameter);
                }
            }
            context.Add(id, created);
            return created;
        }
    }
}
