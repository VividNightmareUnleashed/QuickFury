using System;
using System.Reflection;
using UnityEngine;

namespace QuickFury {
    internal static class ArmatureReflection {
        internal static FieldInfo FindFieldInHierarchy(Type type, string name) {
            while (type != null) {
                var field = type.GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly
                );
                if (field != null) return field;
                type = type.BaseType;
            }
            return null;
        }

        internal static GameObject GetGameObject(object vfGameObject, FieldInfo backingField) {
            if (vfGameObject == null || backingField == null) return null;
            return backingField.GetValue(vfGameObject) as GameObject;
        }
    }
}
