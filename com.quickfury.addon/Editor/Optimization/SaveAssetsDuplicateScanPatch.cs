using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace QuickFury {
    /// <summary>
    /// Preserves VRCFury's renderer-first and controller save passes, then replaces the
    /// final scan of every avatar Component with a pass over VrcfObjectFactory's created
    /// objects. Controller-only subassets are deliberately left to their controller root;
    /// remaining standalone generated assets are saved directly.
    /// </summary>
    internal static class SaveAssetsDuplicateScanPatch {
        private sealed class ScanSession {
            internal bool SkipTransforms;
            internal bool SkipDuplicates;
            internal bool FastDiscovery;
            internal bool SecondPass;
            internal readonly HashSet<Component> Seen = new HashSet<Component>();
            internal int SkippedTransforms;
            internal int SkippedDuplicates;
            internal int SkippedComponentScans;
            internal int SavedStandaloneRoots;
        }

        [ThreadStatic] private static Stack<ScanSession> scanSessions;

        private static MethodInfo saveAssetAndChildren;
        private static FieldInfo createdAssets;
        private static MethodInfo didCreate;

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            var saveAssetsType = compatibility.AvatarEditorAssembly.GetType("VF.Service.SaveAssetsService", false);
            var run = VrcfuryCompatibility.FindUniqueMethod(
                saveAssetsType,
                "Run",
                method => method.ReturnType == typeof(void) && method.GetParameters().Length == 0
            );

            var sessionType = VrcfuryCompatibility.FindType("VF.Utils.SaveAssetsSession");
            var saveComponent = VrcfuryCompatibility.FindUniqueMethod(
                sessionType,
                "SaveUnsavedComponentAssets",
                method => {
                    var parameters = method.GetParameters();
                    return method.ReturnType == typeof(void)
                           && parameters.Length == 2
                           && parameters[0].ParameterType == typeof(Component)
                           && parameters[1].ParameterType == typeof(string);
                }
            );
            saveAssetAndChildren = VrcfuryCompatibility.FindUniqueMethod(
                sessionType,
                "SaveAssetAndChildren",
                method => {
                    var parameters = method.GetParameters();
                    return method.ReturnType == typeof(void)
                           && parameters.Length == 4
                           && parameters[0].ParameterType == typeof(Object)
                           && parameters[1].ParameterType == typeof(string)
                           && parameters[2].ParameterType == typeof(string)
                           && parameters[3].ParameterType == typeof(bool);
                }
            );

            var factoryType = VrcfuryCompatibility.FindType("VF.Utils.VrcfObjectFactory");
            createdAssets = factoryType?.GetField("created", BindingFlags.Static | BindingFlags.NonPublic);
            didCreate = factoryType?
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "DidCreate"
                                           && method.ReturnType == typeof(bool)
                                           && method.GetParameters().Length == 1);

            if (run == null || saveComponent == null || saveAssetAndChildren == null
                            || createdAssets == null || didCreate == null) {
                Debug.LogWarning(
                    "[QuickFury] Fast SaveAssets discovery disabled: expected VRCFury members were not found."
                );
                return;
            }

            try {
                harmony.Patch(
                    run,
                    prefix: new HarmonyMethod(typeof(SaveAssetsDuplicateScanPatch), nameof(RunPrefix)),
                    finalizer: new HarmonyMethod(typeof(SaveAssetsDuplicateScanPatch), nameof(RunFinalizer))
                );
                harmony.Patch(
                    saveComponent,
                    prefix: new HarmonyMethod(typeof(SaveAssetsDuplicateScanPatch), nameof(SaveComponentPrefix))
                );
            } catch (Exception e) {
                Debug.LogWarning("[QuickFury] Fast SaveAssets discovery disabled: " + e.Message);
            }
        }

        private static void RunPrefix(out bool __state) {
            var session = new ScanSession {
                SkipTransforms = QuickFurySettings.SkipTransformAssetScan,
                SkipDuplicates = QuickFurySettings.SkipDuplicateRendererAssetScan,
                FastDiscovery = QuickFurySettings.FastSaveAssetDiscovery
            };
            __state = session.SkipTransforms || session.SkipDuplicates || session.FastDiscovery;
            if (!__state) return;

            if (scanSessions == null) scanSessions = new Stack<ScanSession>();
            scanSessions.Push(session);
        }

        private static Exception RunFinalizer(bool __state, Exception __exception) {
            if (!__state || scanSessions == null || scanSessions.Count == 0) return __exception;

            var session = scanSessions.Peek();
            Debug.Log(
                $"[QuickFury] SaveAssets discovery: skipped {session.SkippedComponentScans} component scans, " +
                $"saved {session.SavedStandaloneRoots} standalone generated roots, skipped " +
                $"{session.SkippedTransforms} Transform scans and {session.SkippedDuplicates} duplicates."
            );
            scanSessions.Pop();
            if (scanSessions.Count == 0) scanSessions = null;
            return __exception;
        }

        private static bool SaveComponentPrefix(object __instance, Component component, string tmpDir) {
            if (scanSessions == null || scanSessions.Count == 0 || component == null) return true;

            var session = scanSessions.Peek();
            if (session.FastDiscovery) {
                if (!session.SecondPass && component is Renderer) {
                    // Unity's native collector finds renderer roots without walking the
                    // renderer's large SerializedObject in managed code.
                    try {
                        SaveRendererRoots(session, __instance, (Renderer)component, tmpDir);
                        return false;
                    } catch (Exception e) {
                        Debug.LogWarning(
                            "[QuickFury] Fast renderer asset discovery fell back to VRCFury: " + e.Message
                        );
                        return true;
                    }
                }

                if (!session.SecondPass) {
                    session.SecondPass = true;
                    try {
                        SaveRemainingStandaloneRoots(session, __instance, tmpDir);
                    } catch (Exception e) {
                        // Nothing already saved is invalidated by falling back: VRCFury's
                        // normal scan observes the new paths and continues from there.
                        session.FastDiscovery = false;
                        Debug.LogWarning(
                            "[QuickFury] Fast generated-asset discovery fell back to VRCFury: " + e.Message
                        );
                        return true;
                    }
                }

                if (session.FastDiscovery) {
                    session.SkippedComponentScans++;
                    return false;
                }
            }

            if (session.SkipTransforms && component is Transform) {
                session.SkippedTransforms++;
                return false;
            }
            if (session.SkipDuplicates && !session.Seen.Add(component)) {
                session.SkippedDuplicates++;
                return false;
            }
            return true;
        }

        private static void SaveRemainingStandaloneRoots(ScanSession session, object saveSession, string tmpDir) {
            var created = createdAssets.GetValue(null) as IEnumerable;
            if (created == null) throw new InvalidOperationException("VRCFury created-object registry is unavailable.");

            var candidates = new List<Object>();
            foreach (var item in created) {
                if (!(item is Object asset) || asset == null) continue;
                if (!CanBeStandaloneRoot(asset)) continue;
                if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset))) continue;
                candidates.Add(asset);
            }

            foreach (var asset in candidates
                         .OrderBy(candidate => candidate.GetType().FullName, StringComparer.Ordinal)
                         .ThenBy(candidate => candidate.name, StringComparer.Ordinal)
                         .ThenBy(candidate => candidate.GetInstanceID())) {
                if (asset == null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset))) continue;

                var filename = GetFilename(asset);
                VrcfuryCompatibility.InvokeUnwrapped(
                    saveAssetAndChildren,
                    saveSession,
                    new object[] { asset, filename, tmpDir, true }
                );
                if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset))) {
                    session.SavedStandaloneRoots++;
                }
            }
        }

        private static void SaveRendererRoots(
            ScanSession session,
            object saveSession,
            Renderer renderer,
            string tmpDir
        ) {
            foreach (var asset in EditorUtility.CollectDependencies(new Object[] { renderer })
                         .Where(asset => asset is Material || asset is Mesh)
                         .Where(asset => asset != null && DidCreate(asset))
                         .Distinct()) {
                if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset))) continue;
                VrcfuryCompatibility.InvokeUnwrapped(
                    saveAssetAndChildren,
                    saveSession,
                    new object[] {
                        asset,
                        $"VRCFury {asset.name} - {renderer.gameObject.name}",
                        tmpDir,
                        true
                    }
                );
            }
        }

        private static bool CanBeStandaloneRoot(Object asset) {
            // These objects are meaningful only within an AnimatorController graph. The
            // controller pass has already saved every reachable instance at this point.
            if (asset is AnimatorController
                || asset is AnimatorStateMachine
                || asset is AnimatorState
                || asset is AnimatorTransitionBase
                || asset is StateMachineBehaviour
                || asset is Motion
                || asset is AvatarMask) {
                return false;
            }
            return true;
        }

        private static string GetFilename(Object asset) {
            if (asset.GetType().Name == "VRCExpressionsMenu") return "VRCFury Menu";
            if (asset.GetType().Name == "VRCExpressionParameters") return "VRCFury Params";
            return "VRCFury " + (string.IsNullOrWhiteSpace(asset.name) ? asset.GetType().Name : asset.name);
        }

        private static bool DidCreate(Object asset) {
            return (bool)VrcfuryCompatibility.InvokeUnwrapped(didCreate, null, new object[] { asset });
        }
    }
}
