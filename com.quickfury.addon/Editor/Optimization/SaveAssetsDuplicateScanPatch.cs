using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace QuickFury {
    /// <summary>
    /// Prunes repeated work in VRCFury 1.1348.0's SaveAssets pass: Transforms, which
    /// cannot reference a VrcfObjectFactory-created asset, and Renderers which were
    /// already scanned in the renderer-first pass without being mutated afterward. It
    /// can also memoize temp folders after CreateFolder has successfully verified them.
    /// </summary>
    internal static class SaveAssetsDuplicateScanPatch {
        private sealed class ScanSession {
            internal bool SkipTransforms;
            internal bool SkipDuplicates;
            internal readonly HashSet<Component> Seen = new HashSet<Component>();
            internal int SkippedTransforms;
            internal int SkippedDuplicates;
        }

        [ThreadStatic] private static Stack<ScanSession> scanSessions;

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            var saveAssetsType = compatibility.AvatarEditorAssembly.GetType("VF.Service.SaveAssetsService", false);
            var run = FindUniqueMethod(
                saveAssetsType,
                "Run",
                method => method.ReturnType == typeof(void) && method.GetParameters().Length == 0
            );

            var sessionType = VrcfuryCompatibility.FindType("VF.Utils.SaveAssetsSession");
            var saveComponent = FindUniqueMethod(
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
            if (run == null || saveComponent == null) {
                Debug.LogWarning(
                    "[QuickFury] SaveAssets scan-pruning optimization disabled: expected VRCFury methods were not found."
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
                Debug.LogWarning("[QuickFury] SaveAssets scan pruning disabled: " + e.Message);
            }
        }

        private static void RunPrefix(out bool __state) {
            var session = new ScanSession {
                SkipTransforms = QuickFurySettings.SkipTransformAssetScan,
                SkipDuplicates = QuickFurySettings.SkipDuplicateRendererAssetScan
            };
            __state = session.SkipTransforms || session.SkipDuplicates;
            if (!__state) return;

            if (scanSessions == null) scanSessions = new Stack<ScanSession>();
            scanSessions.Push(session);
        }

        private static Exception RunFinalizer(bool __state, Exception __exception) {
            if (!__state || scanSessions == null || scanSessions.Count == 0) return __exception;

            var session = scanSessions.Peek();
            Debug.Log(
                $"[QuickFury] SaveAssets pruning: skipped {session.SkippedTransforms} Transform scans, " +
                $"and {session.SkippedDuplicates} duplicate component scans."
            );
            scanSessions.Pop();
            if (scanSessions.Count == 0) scanSessions = null;
            return __exception;
        }

        private static bool SaveComponentPrefix(Component component) {
            if (scanSessions == null || scanSessions.Count == 0 || component == null) return true;

            var session = scanSessions.Peek();
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

        private static MethodInfo FindUniqueMethod(Type type, string name, Func<MethodInfo, bool> predicate) {
            if (type == null) return null;
            return type
                .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                            BindingFlags.DeclaredOnly)
                .Where(method => method.Name == name)
                .Where(method => !method.ContainsGenericParameters)
                .Where(predicate)
                .SingleOrDefault();
        }
    }
}
