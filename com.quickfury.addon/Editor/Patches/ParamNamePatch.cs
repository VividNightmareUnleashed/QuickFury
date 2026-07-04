using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEditor.Animations;

namespace QuickFury {
    /**
     * VF.Service.ControllersService.MakeUniqueParamName -> IsParamUsed rebuilds a VFController
     * wrapper list and marshals the ENTIRE AnimatorController.parameters native array per lookup.
     * FullController-heavy avatars route hundreds of parameters through this: O(paramsCreated ×
     * totalParams × controllers) native array copies.
     *
     * Replacement: snapshot every used parameter name into a HashSet once per build action, check
     * candidates against the set, and add each name this method hands out. Identical output except
     * for one documented edge: a parameter added to a controller by some *other* code path during
     * the same action, whose name is exactly "VF<number>_<something>" that this method is about to
     * generate, would no longer be detected as a collision. VRCFury routes its generated parameter
     * names through this method, so that situation requires a user parameter literally named like
     * a VRCFury internal one.
     */
    internal static class ParamNamePatch {
        private static FieldInfo globalsF;
        private static FieldInfo paramsServiceF;
        private static FieldInfo parameterSourceF;
        private static FieldInfo currentFeatureNumF;
        private static FieldInfo currentFeatureObjectPathF;
        private static MethodInfo getReadOnlyParamsM;
        private static MethodInfo getAllReadOnlyControllersM;
        private static MethodInfo getRawM;
        private static MethodInfo recordParamSourceM;

        // Resolved lazily from the live VRCExpressionParameters instance (avoids a hard SDK dependency).
        private static FieldInfo expressionParamsArrayF;
        private static FieldInfo expressionParamNameF;

        private class Holder {
            public int version = -1;
            public HashSet<string> names;
        }

        private static readonly ConditionalWeakTable<object, Holder> holders =
            new ConditionalWeakTable<object, Holder>();

        public static void Apply(Harmony h) {
            var controllers = QfReflect.ReqType("VF.Service.ControllersService");
            var globals = QfReflect.ReqType("VF.Service.GlobalsService");
            var paramsService = QfReflect.ReqType("VF.Service.ParamsService");
            var paramSource = QfReflect.ReqType("VF.Service.ParameterSourceService");
            var vfController = QfReflect.ReqType("VF.Utils.Controller.VFController");

            globalsF = QfReflect.ReqField(controllers, "globals");
            paramsServiceF = QfReflect.ReqField(controllers, "paramsService");
            parameterSourceF = QfReflect.ReqField(controllers, "parameterSourceService");
            currentFeatureNumF = QfReflect.ReqField(globals, "currentFeatureNum");
            currentFeatureObjectPathF = QfReflect.ReqField(globals, "currentFeatureObjectPath");
            getReadOnlyParamsM = QfReflect.ReqMethod(paramsService, "GetReadOnlyParams");
            getAllReadOnlyControllersM = QfReflect.ReqMethod(controllers, "GetAllReadOnlyControllers");
            getRawM = QfReflect.ReqMethod(vfController, "GetRaw");
            recordParamSourceM = QfReflect.ReqMethod(paramSource, "RecordParamSource");
            if (recordParamSourceM.GetParameters().Length != 3)
                throw new Exception("RecordParamSource signature changed");

            h.Patch(QfReflect.ReqMethod(controllers, "MakeUniqueParamName", new[] { typeof(string) }),
                prefix: new HarmonyMethod(typeof(ParamNamePatch), nameof(Prefix)));
        }

        private static bool Prefix(object __instance, string originalName, ref string __result) {
            if (!QfSettings.ParamNameFastPath || !QfState.InBuild) return true;
            try {
                var holder = holders.GetOrCreateValue(__instance);
                if (holder.version != QfState.scopeVersion || holder.names == null) {
                    holder.names = BuildSnapshot(__instance);
                    holder.version = QfState.scopeVersion;
                }

                var globals = globalsF.GetValue(__instance);
                var name = "VF" + (int)currentFeatureNumF.GetValue(globals) + "_" + originalName;

                var offset = 1;
                string attempt;
                while (true) {
                    attempt = name + (offset == 1 ? "" : offset.ToString());
                    if (!holder.names.Contains(attempt)) break;
                    offset++;
                }

                QfReflect.Invoke(recordParamSourceM, parameterSourceF.GetValue(__instance),
                    attempt, (string)currentFeatureObjectPathF.GetValue(globals), originalName);
                holder.names.Add(attempt);
                __result = attempt;
                return false;
            } catch (Exception e) {
                QfLog.Warn("param name fast path failed, falling back to stock VRCFury: " + e.Message);
                return true;
            }
        }

        private static HashSet<string> BuildSnapshot(object controllersService) {
            var names = new HashSet<string>();

            // Expression parameters (VRCExpressionParameters.parameters[].name)
            var expressionParams = QfReflect.Invoke(getReadOnlyParamsM, paramsServiceF.GetValue(controllersService));
            if (expressionParams != null) {
                if (expressionParamsArrayF == null) {
                    expressionParamsArrayF = expressionParams.GetType().GetField("parameters");
                }
                if (expressionParamsArrayF?.GetValue(expressionParams) is Array arr) {
                    foreach (var p in arr) {
                        if (p == null) continue;
                        if (expressionParamNameF == null) expressionParamNameF = p.GetType().GetField("name");
                        if (expressionParamNameF?.GetValue(p) is string n) names.Add(n);
                    }
                }
            }

            // Animator parameters across every controller on the descriptor
            var controllers = (IEnumerable)QfReflect.Invoke(getAllReadOnlyControllersM, controllersService);
            foreach (var vfc in controllers) {
                if (vfc == null) continue;
                var raw = QfReflect.Invoke(getRawM, vfc) as AnimatorController;
                if (raw == null) continue;
                foreach (var p in raw.parameters) names.Add(p.name);
            }

            return names;
        }
    }
}
