using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.PackageManager;

namespace QuickFury {
    internal sealed class VrcfuryCompatibility {
        internal const string OptimizedVersion = "1.1348.0";

        internal Assembly AvatarEditorAssembly { get; private set; }
        internal string PackageVersion { get; private set; }
        internal Guid ModuleVersionId { get; private set; }

        internal Type BuilderType { get; private set; }
        internal MethodInfo RunMain { get; private set; }
        internal Type ActionType { get; private set; }
        internal MethodInfo ActionCall { get; private set; }
        internal MethodInfo ActionGetName { get; private set; }
        internal MethodInfo ActionGetService { get; private set; }

        internal Type ObjectMoveServiceType { get; private set; }
        internal MethodInfo ApplyDeferred { get; private set; }
        internal MethodInfo ApplyDeferredPathLambda { get; private set; }
        internal FieldInfo DeferredMoves { get; private set; }

        internal bool OptimizationCompatible => PackageVersion == OptimizedVersion
                                                && ApplyDeferred != null
                                                && ApplyDeferredPathLambda != null
                                                && DeferredMoves != null;

        internal static bool TryCreate(out VrcfuryCompatibility compatibility, out string error) {
            compatibility = null;
            error = null;

            try {
                var output = new VrcfuryCompatibility();
                output.AvatarEditorAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "VRCFury-Editor-Avatars");
                if (output.AvatarEditorAssembly == null) {
                    error = "VRCFury-Editor-Avatars is not loaded";
                    return false;
                }

                output.PackageVersion = PackageInfo.FindForAssembly(output.AvatarEditorAssembly)?.version ?? "unknown";
                output.ModuleVersionId = output.AvatarEditorAssembly.ManifestModule.ModuleVersionId;

                output.BuilderType = output.AvatarEditorAssembly.GetType("VF.Builder.VRCFuryBuilder", false);
                output.RunMain = FindUniqueMethod(
                    output.BuilderType,
                    "RunMain",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    method => method.ReturnType == typeof(void) && method.GetParameters().Length == 1
                );

                output.ActionType = output.AvatarEditorAssembly.GetType("VF.Feature.Base.FeatureBuilderAction", false);
                output.ActionCall = FindUniqueMethod(
                    output.ActionType,
                    "Call",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    method => method.ReturnType == typeof(void) && method.GetParameters().Length == 0
                );
                output.ActionGetName = FindUniqueMethod(
                    output.ActionType,
                    "GetName",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    method => method.ReturnType == typeof(string) && method.GetParameters().Length == 0
                );
                output.ActionGetService = FindUniqueMethod(
                    output.ActionType,
                    "GetService",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    method => method.ReturnType == typeof(object) && method.GetParameters().Length == 0
                );

                output.ObjectMoveServiceType = output.AvatarEditorAssembly.GetType("VF.Service.ObjectMoveService", false);
                output.ApplyDeferred = FindUniqueMethod(
                    output.ObjectMoveServiceType,
                    "ApplyDeferred",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    method => method.ReturnType == typeof(void) && method.GetParameters().Length == 0
                );
                output.ApplyDeferredPathLambda = output.ObjectMoveServiceType?
                    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(method => method.Name.Contains("ApplyDeferred"))
                    .Where(method => method.ReturnType == typeof(string))
                    .Where(method => {
                        var parameters = method.GetParameters();
                        return parameters.Length == 1 && parameters[0].ParameterType == typeof(string);
                    })
                    .SingleOrDefault();
                output.DeferredMoves = output.ObjectMoveServiceType?
                    .GetField("deferred", BindingFlags.Instance | BindingFlags.NonPublic);

                if (output.RunMain == null || output.ActionCall == null
                                           || output.ActionGetName == null || output.ActionGetService == null) {
                    error = "VRCFury profiling targets did not match their expected signatures";
                    return false;
                }

                compatibility = output;
                return true;
            } catch (Exception e) {
                error = e.GetType().Name + ": " + e.Message;
                return false;
            }
        }

        internal static Type FindType(string fullName) {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                var type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        internal static IEnumerable<MethodInfo> FindDeclaredMethods(string typeName, string methodName) {
            var type = FindType(typeName);
            if (type == null) return Array.Empty<MethodInfo>();
            return type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                   BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(method => method.Name == methodName)
                .Where(method => !method.ContainsGenericParameters)
                .Where(method => !method.IsAbstract)
                .Where(method => method.GetMethodBody() != null);
        }

        private static MethodInfo FindUniqueMethod(
            Type type,
            string name,
            BindingFlags flags,
            Func<MethodInfo, bool> predicate
        ) {
            if (type == null) return null;
            return type.GetMethods(flags).Where(method => method.Name == name).Where(predicate).SingleOrDefault();
        }
    }
}
