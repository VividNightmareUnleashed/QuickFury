using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace QuickFury {
    internal static class QfLog {
        public static void Info(string msg) => Debug.Log("[QuickFury] " + msg);
        public static void Warn(string msg) => Debug.LogWarning("[QuickFury] " + msg);
    }

    /**
     * All toggles are EditorPrefs-backed and checked at runtime inside each patch prefix,
     * so flipping them takes effect immediately without a domain reload.
     * The master toggle actually unpatches/repatches.
     */
    internal static class QfSettings {
        private class Toggle {
            private readonly string key;
            private readonly bool def;
            private bool value;
            public Toggle(string key, bool def) {
                this.key = key;
                this.def = def;
                value = EditorPrefs.GetBool(key, def);
            }
            public bool Value {
                get => value;
                set {
                    this.value = value;
                    EditorPrefs.SetBool(key, value);
                }
            }
        }

        private static readonly Toggle enabled = new Toggle("quickfury.enabled", true);
        private static readonly Toggle profiler = new Toggle("quickfury.profiler", true);
        private static readonly Toggle bindingCache = new Toggle("quickfury.patch.bindingCache", true);
        private static readonly Toggle physboneCache = new Toggle("quickfury.patch.physboneCache", true);
        private static readonly Toggle clipSettingsCache = new Toggle("quickfury.patch.clipSettingsCache", true);
        private static readonly Toggle hashCodeFix = new Toggle("quickfury.patch.hashCodeFix", true);
        private static readonly Toggle paramNameFastPath = new Toggle("quickfury.patch.paramNameFastPath", true);
        private static readonly Toggle moveCache = new Toggle("quickfury.patch.moveCache", true);
        private static readonly Toggle skinRewriteCache = new Toggle("quickfury.patch.skinRewriteCache", true);
        private static readonly Toggle progressThrottle = new Toggle("quickfury.patch.progressThrottle", true);
        private static readonly Toggle skipPlayModeDebugInfo = new Toggle("quickfury.patch.skipPlayModeDebugInfo", true);
        private static readonly Toggle destroyCache = new Toggle("quickfury.patch.destroyCache", true);
        private static readonly Toggle constraintsCache = new Toggle("quickfury.patch.constraintsCache", true);
        private static readonly Toggle layerMapCache = new Toggle("quickfury.patch.layerMapCache", true);
        private static readonly Toggle batchedSave = new Toggle("quickfury.patch.batchedSave", true);

        public static bool Enabled { get => enabled.Value; set => enabled.Value = value; }
        public static bool Profiler { get => profiler.Value; set => profiler.Value = value; }
        public static bool BindingCache { get => bindingCache.Value; set => bindingCache.Value = value; }
        public static bool PhysboneCache { get => physboneCache.Value; set => physboneCache.Value = value; }
        public static bool ClipSettingsCache { get => clipSettingsCache.Value; set => clipSettingsCache.Value = value; }
        public static bool HashCodeFix { get => hashCodeFix.Value; set => hashCodeFix.Value = value; }
        public static bool ParamNameFastPath { get => paramNameFastPath.Value; set => paramNameFastPath.Value = value; }
        public static bool MoveCache { get => moveCache.Value; set => moveCache.Value = value; }
        public static bool SkinRewriteCache { get => skinRewriteCache.Value; set => skinRewriteCache.Value = value; }
        public static bool ProgressThrottle { get => progressThrottle.Value; set => progressThrottle.Value = value; }
        public static bool SkipPlayModeDebugInfo { get => skipPlayModeDebugInfo.Value; set => skipPlayModeDebugInfo.Value = value; }
        public static bool DestroyCache { get => destroyCache.Value; set => destroyCache.Value = value; }
        public static bool ConstraintsCache { get => constraintsCache.Value; set => constraintsCache.Value = value; }
        public static bool LayerMapCache { get => layerMapCache.Value; set => layerMapCache.Value = value; }
        public static bool BatchedSave { get => batchedSave.Value; set => batchedSave.Value = value; }

        /** Flips every optimization on/off at once, leaving the profiler (and scope tracking) alone. */
        public static void SetAllOptimizations(bool on) {
            BindingCache = on;
            PhysboneCache = on;
            ClipSettingsCache = on;
            HashCodeFix = on;
            ParamNameFastPath = on;
            MoveCache = on;
            SkinRewriteCache = on;
            ProgressThrottle = on;
            SkipPlayModeDebugInfo = on;
            DestroyCache = on;
            ConstraintsCache = on;
            LayerMapCache = on;
            BatchedSave = on;
        }
    }

    /**
     * Build-scope tracking, maintained by ScopePatch:
     * - hookDepth > 0 while any VRCFury preprocessor hook is running (upload, play mode, or test copy).
     * - scopeVersion is bumped at every safety boundary: each preprocessor hook start/end and each
     *   FeatureBuilderAction start. All QuickFury caches key off scopeVersion, so nothing QuickFury
     *   caches ever survives past a single VRCFury build pass. Hierarchy/asset mutations happen
     *   between actions, never "behind the back" of the pass that is currently using a cache.
     */
    internal static class QfState {
        public static int scopeVersion;
        public static int hookDepth;
        public static bool InBuild => hookDepth > 0;
        public static void BumpScope() => scopeVersion++;
    }

    /** A dictionary that silently empties itself whenever the build scope changes. */
    internal class QfScopedDict<TKey, TValue> {
        private readonly Dictionary<TKey, TValue> dict;
        private int version = -1;

        public QfScopedDict(IEqualityComparer<TKey> comparer = null) {
            dict = comparer == null ? new Dictionary<TKey, TValue>() : new Dictionary<TKey, TValue>(comparer);
        }

        public Dictionary<TKey, TValue> Get() {
            if (version != QfState.scopeVersion) {
                dict.Clear();
                version = QfState.scopeVersion;
            }
            return dict;
        }
    }
}
