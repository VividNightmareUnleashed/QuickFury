using System.Diagnostics;
using HarmonyLib;

namespace QuickFury {
    /**
     * VF.VRCFProgressWindow.Progress does a Debug.Log (which captures a stack trace) plus a
     * synchronous RepaintImmediately for EVERY build action. On toggle-heavy avatars that's
     * hundreds of forced UI repaints per bake.
     *
     * This rate-limits the whole method to one update per 50ms. Skipped calls lose their log line
     * and progress-bar tick; the build itself is untouched, and any build failure still names the
     * failing component in its exception message.
     */
    internal static class ProgressThrottlePatch {
        private const double MinIntervalMs = 50;
        private static long lastTicks;

        public static void Apply(Harmony h) {
            var t = QfReflect.ReqType("VF.VRCFProgressWindow");
            h.Patch(QfReflect.ReqMethod(t, "Progress"),
                prefix: new HarmonyMethod(typeof(ProgressThrottlePatch), nameof(Prefix)));
        }

        private static bool Prefix() {
            if (!QfSettings.ProgressThrottle) return true;
            var now = Stopwatch.GetTimestamp();
            if ((now - lastTicks) * 1000.0 / Stopwatch.Frequency < MinIntervalMs) return false;
            lastTicks = now;
            return true;
        }
    }
}
