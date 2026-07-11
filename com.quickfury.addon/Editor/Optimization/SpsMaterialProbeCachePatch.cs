using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace QuickFury {
    /// <summary>
    /// HasDpsOrTpsMaterial can force Unity to load and introspect every shader used by
    /// the avatar. Cache the boolean for persistent, clean materials using their GUID,
    /// local file id and full dependency hash. Changes to a material or shader produce a
    /// different key; generated or dirty materials always use VRCFury's live probe.
    /// </summary>
    internal static class SpsMaterialProbeCachePatch {
        private const string CachePrefix = "com.quickfury.spsProbe.v1.";
        private static readonly Dictionary<string, Hash128> DependencyHashes =
            new Dictionary<string, Hash128>(StringComparer.Ordinal);
        private static readonly Dictionary<string, bool> ResultsByKey =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        internal static void Install(Harmony harmony, VrcfuryCompatibility compatibility) {
            var type = VrcfuryCompatibility.FindType("VF.Builder.Haptics.TpsConfigurer");
            var target = VrcfuryCompatibility.FindUniqueMethod(
                type,
                "HasDpsOrTpsMaterial",
                method => method.ReturnType == typeof(bool)
                          && method.GetParameters().Length == 1
                          && method.GetParameters()[0].ParameterType == typeof(Renderer)
            );
            if (target == null) {
                Debug.LogWarning("[QuickFury] SPS material probe cache disabled: target signature mismatch.");
                return;
            }

            try {
                harmony.Patch(
                    target,
                    prefix: new HarmonyMethod(typeof(SpsMaterialProbeCachePatch), nameof(GetCached)),
                    postfix: new HarmonyMethod(typeof(SpsMaterialProbeCachePatch), nameof(Store))
                );
                // The signature is only self-invalidating while the dependency hashes are
                // current; a shader or material edit between two bakes must be observed.
                harmony.Patch(
                    compatibility.RunMain,
                    prefix: new HarmonyMethod(typeof(SpsMaterialProbeCachePatch), nameof(InvalidateDependencyHashes))
                );
            } catch (Exception e) {
                Debug.LogWarning("[QuickFury] SPS material probe cache disabled: " + e.Message);
            }
        }

        private static void InvalidateDependencyHashes() {
            DependencyHashes.Clear();
        }

        private static bool GetCached(Renderer r, ref bool __result, out string __state) {
            __state = null;
            if (!QuickFurySettings.SpsMaterialProbeCache || r == null) return true;

            try {
                var signature = BuildSignature(r.sharedMaterials);
                if (signature == null) return true;
                var key = CachePrefix + Hash128.Compute(signature);
                // The signature key is content-derived, so the in-memory mirror stays
                // valid across bakes and saves a registry read per repeated probe.
                if (ResultsByKey.TryGetValue(key, out var cached)) {
                    __result = cached;
                    return false;
                }
                if (EditorPrefs.HasKey(key)) {
                    __result = EditorPrefs.GetBool(key);
                    ResultsByKey[key] = __result;
                    return false;
                }
                __state = key;
                return true;
            } catch {
                // Persistence is an optional fast path; VRCFury remains authoritative.
                return true;
            }
        }

        private static void Store(string __state, bool __result) {
            if (string.IsNullOrEmpty(__state)) return;
            ResultsByKey[__state] = __result;
            EditorPrefs.SetBool(__state, __result);
        }

        private static string BuildSignature(Material[] materials) {
            var builder = new StringBuilder();
            builder.Append(Application.unityVersion).Append('|');
            foreach (var material in materials) {
                if (material == null) {
                    builder.Append("null;");
                    continue;
                }
                if (EditorUtility.IsDirty(material)) return null;

                var path = AssetDatabase.GetAssetPath(material);
                if (string.IsNullOrEmpty(path)) return null;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        material,
                        out var guid,
                        out long localId
                    )) return null;

                if (!DependencyHashes.TryGetValue(path, out var dependencyHash)) {
                    dependencyHash = AssetDatabase.GetAssetDependencyHash(path);
                    DependencyHashes.Add(path, dependencyHash);
                }
                builder.Append(guid).Append(':').Append(localId).Append(':')
                    .Append(dependencyHash).Append(';');
            }
            return builder.ToString();
        }
    }
}
