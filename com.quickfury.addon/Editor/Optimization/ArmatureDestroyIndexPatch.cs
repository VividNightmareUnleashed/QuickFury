using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace QuickFury {
    /// <summary>
    /// VFGameObject.Destroy normally discovers every PhysBone, PhysBone collider, and
    /// contact in the upload root again for every pruned Armature Link object. This patch
    /// snapshots those three ordered component sequences immediately before the first prune
    /// and reuses them while preserving VRCFury's per-object filtering and destruction order.
    /// </summary>
    internal static class ArmatureDestroyIndexPatch {
        private sealed class Category {
            internal Type ComponentType;
            internal MethodInfo GetRootTransform;
            internal readonly List<RootBucket> Roots = new List<RootBucket>();
        }

        private sealed class RootBucket {
            internal GameObject Root;
            internal readonly List<Component> Components = new List<Component>();
            internal bool Built;
        }

        private sealed class Context {
            internal GameObject Avatar;
            internal List<GameObject> UploadRoots;
            internal List<Category> Categories;
        }

        [ThreadStatic] private static Context active;

        private static FieldInfo avatarObjectField;
        private static FieldInfo gameObjectField;
        private static MethodInfo getUploadRoots;
        private static MethodInfo getConstraints;
        private static MethodInfo destroyConstraint;
        private static Category[] categoryTemplates;

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            var armatureType = VrcfuryCompatibility.FindType("VF.Service.ArmatureLinkService");
            var vfGameObjectType = VrcfuryCompatibility.FindType("VF.Utils.VFGameObject");
            var constraintType = VrcfuryCompatibility.FindType("VF.Utils.VFConstraint");

            var apply = armatureType?.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "Apply" && method.GetParameters().Length == 0);
            var destroy = vfGameObjectType?
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "Destroy"
                                           && method.ReturnType == typeof(void)
                                           && method.GetParameters().Length == 0);
            getConstraints = vfGameObjectType?
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => {
                    if (method.Name != "GetConstraints" || !method.ReturnType.IsArray) return false;
                    var parameters = method.GetParameters();
                    return parameters.Length == 2
                           && parameters[0].ParameterType == typeof(bool)
                           && parameters[1].ParameterType == typeof(bool);
                });

            avatarObjectField = armatureType?.GetField("avatarObject", BindingFlags.Instance | BindingFlags.NonPublic);
            gameObjectField = vfGameObjectType?.GetField("_gameObject", BindingFlags.Instance | BindingFlags.NonPublic);
            getUploadRoots = vfGameObjectType?
                .GetProperty("uploadRoots", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetGetMethod(true);
            destroyConstraint = constraintType?
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "Destroy"
                                           && method.ReturnType == typeof(void)
                                           && method.GetParameters().Length == 0);

            categoryTemplates = new[] {
                CreateCategory("VRC.Dynamics.VRCPhysBoneBase"),
                CreateCategory("VRC.Dynamics.VRCPhysBoneColliderBase"),
                CreateCategory("VRC.Dynamics.ContactBase")
            };

            if (apply == null || destroy == null || getConstraints == null || constraintType == null
                              || avatarObjectField == null || gameObjectField == null
                              || getUploadRoots == null || destroyConstraint == null
                              || categoryTemplates.Any(category => category == null)) {
                categoryTemplates = null;
                Debug.LogWarning("[QuickFury] Armature destroy index disabled: target signature mismatch.");
                return;
            }

            try {
                harmony.Patch(
                    apply,
                    prefix: new HarmonyMethod(typeof(ArmatureDestroyIndexPatch), nameof(Begin)),
                    finalizer: new HarmonyMethod(typeof(ArmatureDestroyIndexPatch), nameof(End))
                );
                harmony.Patch(
                    destroy,
                    prefix: new HarmonyMethod(typeof(ArmatureDestroyIndexPatch), nameof(Destroy))
                );
            } catch (Exception e) {
                Debug.LogWarning("[QuickFury] Armature destroy index disabled: " + e.Message);
            }
        }

        private static Category CreateCategory(string typeName) {
            var componentType = VrcfuryCompatibility.FindType(typeName);
            if (componentType == null || !typeof(Component).IsAssignableFrom(componentType)) return null;

            var root = componentType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "GetRootTransform"
                                           && method.ReturnType == typeof(Transform)
                                           && method.GetParameters().Length == 0);
            if (root == null) return null;

            return new Category {
                ComponentType = componentType,
                GetRootTransform = root
            };
        }

        private static void Begin(object __instance) {
            active = null;
            if (!QuickFurySettings.DestroyIndex) return;

            try {
                var avatarWrapper = avatarObjectField.GetValue(__instance);
                var avatar = ArmatureReflection.GetGameObject(avatarWrapper, gameObjectField);
                if (avatar == null) return;
                active = new Context { Avatar = avatar };
            } catch (Exception e) {
                Debug.LogWarning("[QuickFury] Destroy index fell back to VRCFury: " + e.Message);
            }
        }

        private static Exception End(Exception __exception) {
            active = null;
            return __exception;
        }

        private static bool Destroy(object __instance) {
            var context = active;
            if (context == null) return true;

            var targetObject = ArmatureReflection.GetGameObject(__instance, gameObjectField);
            if (targetObject == null || context.Avatar == null
                                     || !targetObject.transform.IsChildOf(context.Avatar.transform)) {
                return true;
            }

            List<GameObject> uploadRoots;
            try {
                uploadRoots = ReadUploadRoots(__instance);
                if (uploadRoots == null || uploadRoots.Any(root => root == null)) return true;

                if (context.UploadRoots == null) {
                    BuildIndex(context, uploadRoots);
                } else if (!SameRoots(context.UploadRoots, uploadRoots)) {
                    // A different upload-root set is outside the cache's exactness boundary.
                    return true;
                }
            } catch (Exception e) {
                active = null;
                Debug.LogWarning("[QuickFury] Destroy index fell back to VRCFury: " + e.Message);
                return true;
            }

            var target = targetObject.transform;
            foreach (var category in context.Categories) {
                foreach (var bucket in category.Roots) {
                    BuildBucketIfNeeded(category, bucket);
                    foreach (var component in bucket.Components) {
                        // A fresh GetComponentsInChildren call would no longer include destroyed
                        // components or components that have left this upload root.
                        if (component == null || !component.transform.IsChildOf(bucket.Root.transform)) continue;

                        var root = InvokeUnwrapped(category.GetRootTransform, component, null) as Transform;
                        if (root.IsChildOf(target)) Object.DestroyImmediate(component);
                    }
                }
            }

            var constraints = InvokeUnwrapped(
                getConstraints,
                __instance,
                new object[] { false, true }
            ) as IEnumerable;
            if (constraints == null) {
                throw new InvalidOperationException("VRCFury GetConstraints returned a non-enumerable result.");
            }
            foreach (var constraint in constraints) {
                InvokeUnwrapped(destroyConstraint, constraint, null);
            }

            Object.DestroyImmediate(targetObject);
            return false;
        }

        private static List<GameObject> ReadUploadRoots(object vfGameObject) {
            var roots = InvokeUnwrapped(getUploadRoots, vfGameObject, null) as IEnumerable;
            if (roots == null) return null;

            var output = new List<GameObject>();
            foreach (var root in roots) {
                output.Add(ArmatureReflection.GetGameObject(root, gameObjectField));
            }
            return output;
        }

        private static void BuildIndex(Context context, List<GameObject> uploadRoots) {
            var categories = new List<Category>(categoryTemplates.Length);
            foreach (var template in categoryTemplates) {
                var category = new Category {
                    ComponentType = template.ComponentType,
                    GetRootTransform = template.GetRootTransform
                };
                foreach (var root in uploadRoots) {
                    category.Roots.Add(new RootBucket { Root = root });
                }
                categories.Add(category);
            }

            context.UploadRoots = new List<GameObject>(uploadRoots);
            context.Categories = categories;
        }

        private static void BuildBucketIfNeeded(Category category, RootBucket bucket) {
            if (bucket.Built) return;

            bucket.Components.AddRange(
                bucket.Root.GetComponentsInChildren(category.ComponentType, true)
                    .OfType<Component>()
                    .Where(component => component != null)
            );
            bucket.Built = true;
        }

        private static bool SameRoots(IReadOnlyList<GameObject> left, IReadOnlyList<GameObject> right) {
            if (left.Count != right.Count) return false;
            for (var i = 0; i < left.Count; i++) {
                if (left[i] != right[i]) return false;
            }
            return true;
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
