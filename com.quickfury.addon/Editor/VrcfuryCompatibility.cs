using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using UnityEditor.PackageManager;
using Object = UnityEngine.Object;

namespace QuickFury {
    internal sealed class VrcfuryCompatibility {
        internal const string OptimizedVersion = "1.1363.0";

        internal string PackageVersion { get; private set; }
        internal Guid ModuleVersionId { get; private set; }

        internal MethodInfo RunMain { get; private set; }
        internal MethodInfo ActionCall { get; private set; }
        internal MethodInfo ActionGetName { get; private set; }
        internal MethodInfo ActionGetService { get; private set; }

        internal MethodInfo ApplyDeferred { get; private set; }
        internal MethodInfo ApplyDeferredPathLambda { get; private set; }
        internal FieldInfo DeferredMoves { get; private set; }

        internal MethodInfo SaveAssetsRun { get; private set; }
        internal MethodInfo FactoryDidCreate { get; private set; }
        internal FieldInfo VfLayerRootStateMachine { get; private set; }
        internal MethodInfo VfLayerBehaviourContainersGetter { get; private set; }

        // DidCreate runs once per visited node in the controller-graph traversal, so a
        // bound delegate replaces the MethodInfo.Invoke + object[] allocation there.
        private Func<Object, bool> didCreateFast;

        internal bool DidCreate(Object asset) {
            if (didCreateFast != null) return didCreateFast(asset);
            return (bool)InvokeUnwrapped(FactoryDidCreate, null, new object[] { asset });
        }

        internal bool OptimizationCompatible => PackageVersion == OptimizedVersion
                                                && ApplyDeferred != null
                                                && ApplyDeferredPathLambda != null
                                                && DeferredMoves != null;

        internal static bool TryCreate(out VrcfuryCompatibility compatibility, out string error) {
            compatibility = null;
            error = null;

            try {
                var output = new VrcfuryCompatibility();
                var avatarEditorAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "VRCFury-Editor-Avatars");
                if (avatarEditorAssembly == null) {
                    error = "VRCFury-Editor-Avatars is not loaded";
                    return false;
                }

                output.PackageVersion = PackageInfo.FindForAssembly(avatarEditorAssembly)?.version ?? "unknown";
                output.ModuleVersionId = avatarEditorAssembly.ManifestModule.ModuleVersionId;

                var builderType = avatarEditorAssembly.GetType("VF.Builder.VRCFuryBuilder", false);
                output.RunMain = FindUniqueMethod(
                    builderType,
                    "RunMain",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    method => method.ReturnType == typeof(void) && method.GetParameters().Length == 1
                );

                var actionType = avatarEditorAssembly.GetType("VF.Feature.Base.FeatureBuilderAction", false);
                output.ActionCall = FindUniqueMethod(
                    actionType,
                    "Call",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    method => method.ReturnType == typeof(void) && method.GetParameters().Length == 0
                );
                output.ActionGetName = FindUniqueMethod(
                    actionType,
                    "GetName",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    method => method.ReturnType == typeof(string) && method.GetParameters().Length == 0
                );
                output.ActionGetService = FindUniqueMethod(
                    actionType,
                    "GetService",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    method => method.ReturnType == typeof(object) && method.GetParameters().Length == 0
                );

                var objectMoveServiceType = avatarEditorAssembly.GetType("VF.Service.ObjectMoveService", false);
                output.ApplyDeferred = FindUniqueMethod(
                    objectMoveServiceType,
                    "ApplyDeferred",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    method => method.ReturnType == typeof(void) && method.GetParameters().Length == 0
                );
                var lambdaCandidates = objectMoveServiceType?
                    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(method => method.Name.Contains("ApplyDeferred"))
                    .Where(method => method.ReturnType == typeof(string))
                    .Where(method => {
                        var parameters = method.GetParameters();
                        return parameters.Length == 1 && parameters[0].ParameterType == typeof(string);
                    })
                    .Take(2).ToList();
                output.ApplyDeferredPathLambda = lambdaCandidates?.Count == 1 ? lambdaCandidates[0] : null;
                output.DeferredMoves = objectMoveServiceType?
                    .GetField("deferred", BindingFlags.Instance | BindingFlags.NonPublic);

                var saveAssetsType = avatarEditorAssembly.GetType("VF.Service.SaveAssetsService", false);
                output.SaveAssetsRun = FindNoArgVoid(saveAssetsType, "Run");

                var factoryType = FindType("VF.Utils.VrcfObjectFactory");
                output.FactoryDidCreate = FindUniqueMethod(
                    factoryType,
                    "DidCreate",
                    method => method.ReturnType == typeof(bool) && method.GetParameters().Length == 1
                );
                if (output.FactoryDidCreate != null) {
                    try {
                        output.didCreateFast = (Func<Object, bool>)Delegate.CreateDelegate(
                            typeof(Func<Object, bool>),
                            output.FactoryDidCreate
                        );
                    } catch (ArgumentException) {
                        // Parameter type drifted from Object; the reflection fallback still works.
                    }
                }

                var vfLayerType = FindType("VF.Utils.Controller.VFLayer");
                output.VfLayerRootStateMachine = vfLayerType?
                    .GetField("rootStateMachine", BindingFlags.Instance | BindingFlags.NonPublic);
                output.VfLayerBehaviourContainersGetter = vfLayerType?
                    .GetProperty("allBehaviourContainers", BindingFlags.Instance | BindingFlags.NonPublic)?
                    .GetGetMethod(true);

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

        private static readonly Dictionary<string, Type> TypeCache =
            new Dictionary<string, Type>(StringComparer.Ordinal);

        // The ~40 startup lookups cluster in a handful of VRCFury/VRC SDK assemblies, so
        // probe past hits first instead of scanning the whole AppDomain on every miss.
        private static readonly List<Assembly> HitAssemblies = new List<Assembly>();

        internal static Type FindType(string fullName) {
            if (TypeCache.TryGetValue(fullName, out var cached)) return cached;

            Type found = null;
            foreach (var assembly in HitAssemblies) {
                found = assembly.GetType(fullName, false);
                if (found != null) break;
            }
            if (found == null) {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                    found = assembly.GetType(fullName, false);
                    if (found != null) break;
                }
            }
            if (found != null && !HitAssemblies.Contains(found.Assembly)) {
                HitAssemblies.Add(found.Assembly);
            }
            TypeCache[fullName] = found;
            return found;
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

        internal static MethodInfo FindUniqueMethod(
            Type type,
            string name,
            Func<MethodInfo, bool> predicate
        ) {
            return FindUniqueMethod(
                type,
                name,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                method => !method.ContainsGenericParameters && predicate(method)
            );
        }

        internal static MethodInfo FindNoArgVoid(Type type, string name) {
            return FindMethodWithSignature(type, name, typeof(void));
        }

        internal static MethodInfo FindMethodWithSignature(
            Type type,
            string name,
            Type returnType,
            params Type[] parameterTypes
        ) {
            return FindUniqueMethod(type, name, method => {
                if (method.ReturnType != returnType) return false;
                var parameters = method.GetParameters();
                if (parameters.Length != parameterTypes.Length) return false;
                for (var i = 0; i < parameters.Length; i++) {
                    if (parameters[i].ParameterType != parameterTypes[i]) return false;
                }
                return true;
            });
        }

        internal static object CreateEmptyImmutableSet(Type setType) {
            try {
                var arguments = setType.GetGenericArguments();
                if (arguments.Length != 1) return null;
                // Unity also loads a private copy inside ReportGeneratorMerged. Resolve
                // from the set type's own assembly so the empty set is assignable to
                // VRCFury's System.Collections.Immutable contract.
                var openType = setType.Assembly.GetType(
                    "System.Collections.Immutable.ImmutableHashSet`1",
                    false
                );
                if (openType == null) return null;
                var closedType = openType.MakeGenericType(arguments[0]);
                var empty = closedType
                    .GetField("Empty", BindingFlags.Static | BindingFlags.Public)?
                    .GetValue(null);
                return empty != null && setType.IsInstanceOfType(empty) ? empty : null;
            } catch {
                return null;
            }
        }

        internal static object InvokeUnwrapped(MethodInfo method, object instance, object[] args) {
            try {
                return method.Invoke(instance, args);
            } catch (TargetInvocationException e) when (e.InnerException != null) {
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }

        private static MethodInfo FindUniqueMethod(
            Type type,
            string name,
            BindingFlags flags,
            Func<MethodInfo, bool> predicate
        ) {
            if (type == null) return null;
            // Ambiguity means the pinned signature no longer identifies one method; treat
            // it as missing so the caller disables its patch instead of throwing.
            MethodInfo match = null;
            foreach (var method in type.GetMethods(flags)) {
                if (method.Name != name || !predicate(method)) continue;
                if (match != null) return null;
                match = method;
            }
            return match;
        }
    }
}
