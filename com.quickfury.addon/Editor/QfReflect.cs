using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using UnityEngine;

namespace QuickFury {
    /**
     * All access to VRCFury internals goes through here. VRCFury is deliberately NOT a compile-time
     * dependency: every type/member is resolved by name once, at patch time. If a member is missing
     * (VRCFury updated / not installed), the patch that needs it simply fails to apply and VRCFury
     * behaves exactly as stock.
     */
    internal static class QfReflect {
        public static Type ReqType(string fullName) {
            var t = AccessTools.TypeByName(fullName);
            if (t == null) throw new Exception("type not found: " + fullName);
            return t;
        }

        public static MethodInfo ReqMethod(Type type, string name, Type[] args = null) {
            var m = args == null ? AccessTools.Method(type, name) : AccessTools.Method(type, name, args);
            if (m == null) throw new Exception($"method not found: {type.FullName}.{name}");
            return m;
        }

        public static FieldInfo ReqField(Type type, string name) {
            var f = AccessTools.Field(type, name);
            if (f == null) throw new Exception($"field not found: {type.FullName}.{name}");
            return f;
        }

        /** Invoke, but rethrow the real exception instead of TargetInvocationException so
         * VRCFury's error dialogs show the same cause they would without QuickFury. */
        public static object Invoke(MethodInfo method, object target, params object[] args) {
            try {
                return method.Invoke(target, args);
            } catch (TargetInvocationException tie) when (tie.InnerException != null) {
                ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                throw; // unreachable
            }
        }

        private static Type _vfGoType;
        public static Type VfGoType => _vfGoType ?? (_vfGoType = ReqType("VF.Utils.VFGameObject"));

        private static Func<object, GameObject> _goGetter;

        /**
         * Builds the VFGameObject._gameObject accessor eagerly. Call from a patch's Apply() so a
         * VRCFury layout change fails the patch at patch time instead of mid-build.
         */
        public static void WarmGo() {
            if (_goGetter != null) return;
            var field = ReqField(VfGoType, "_gameObject");
            try {
                // Harmony's skip-visibility codegen: the reliable way to read a private field fast.
                var fieldRef = AccessTools.FieldRefAccess<GameObject>(VfGoType, "_gameObject");
                _goGetter = o => fieldRef(o);
            } catch {
                var p = Expression.Parameter(typeof(object), "o");
                _goGetter = Expression.Lambda<Func<object, GameObject>>(
                    Expression.Field(Expression.Convert(p, VfGoType), field), p).Compile();
            }
        }

        /** Unwraps VFGameObject -> raw GameObject reference (no unity fake-null coercion). */
        public static GameObject Go(object vfGameObject) {
            if (vfGameObject == null) return null;
            if (_goGetter == null) WarmGo();
            return _goGetter(vfGameObject);
        }

        private static FieldInfo _getUploadRootsField;
        /** Calls VRCFury's own uploadRoots resolution (VFGameObject.getUploadRoots delegate). */
        public static object[] UploadRoots(object vfGameObject) {
            if (_getUploadRootsField == null) _getUploadRootsField = ReqField(VfGoType, "getUploadRoots");
            var del = (Delegate)_getUploadRootsField.GetValue(null);
            if (del == null) throw new Exception("VFGameObject.getUploadRoots is not set");
            var arr = (Array)del.DynamicInvoke(vfGameObject);
            if (arr == null) return Array.Empty<object>();
            var result = new object[arr.Length];
            for (var i = 0; i < arr.Length; i++) result[i] = arr.GetValue(i);
            return result;
        }
    }
}
