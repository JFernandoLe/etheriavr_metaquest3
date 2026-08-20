using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

/// <summary>
/// Utilidades de reflexión para ajustar campos privados del XR Interaction Toolkit.
/// Son necesarias porque el paquete no expone esta configuración públicamente.
/// Todos los buscadores recorren la jerarquía de tipos, ya que el campo puede
/// estar declarado en una clase base del componente.
/// </summary>
public partial class QuestXRInteractionController
{
    private const BindingFlags InstanceFieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags InstancePropertyFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static FieldInfo FindField(object target, string fieldName, System.Func<FieldInfo, bool> matches)
    {
        for (System.Type type = target?.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(fieldName, InstanceFieldFlags);
            if (field != null && matches(field)) return field;
        }

        return null;
    }

    private static PropertyInfo FindProperty(object target, string propertyName, System.Func<PropertyInfo, bool> matches)
    {
        for (System.Type type = target?.GetType(); type != null; type = type.BaseType)
        {
            PropertyInfo property = type.GetProperty(propertyName, InstancePropertyFlags);
            if (property != null && property.CanRead && matches(property)) return property;
        }

        return null;
    }

    private static bool IsUnityObjectField(FieldInfo field) => typeof(Object).IsAssignableFrom(field.FieldType);

    private static bool TrySetBoolField(object target, string fieldName, bool value)
    {
        FieldInfo field = FindField(target, fieldName, f => f.FieldType == typeof(bool));
        if (field == null) return false;

        field.SetValue(target, value);
        return true;
    }

    /// <summary>Devuelve false si el campo ya tenía ese valor, para no contar cambios inexistentes.</summary>
    private static bool TrySetTransformField(object target, string fieldName, Transform value)
    {
        FieldInfo field = FindField(target, fieldName, f => f.FieldType == typeof(Transform));
        if (field == null || ReferenceEquals(field.GetValue(target), value)) return false;

        field.SetValue(target, value);
        return true;
    }

    private static bool TrySetObjectField(object target, string fieldName, Object value)
    {
        FieldInfo field = FindField(target, fieldName,
            f => IsUnityObjectField(f) && (value == null || f.FieldType.IsAssignableFrom(value.GetType())));

        if (field == null || ReferenceEquals(field.GetValue(target), value)) return false;

        field.SetValue(target, value);
        return true;
    }

    private static bool TryGetTransformFieldValue(object target, string fieldName, out Transform value)
    {
        FieldInfo field = FindField(target, fieldName, f => f.FieldType == typeof(Transform));
        value = field?.GetValue(target) as Transform;
        return field != null;
    }

    private static bool TryGetObjectFieldValue(object target, string fieldName, out Object value)
    {
        FieldInfo field = FindField(target, fieldName, IsUnityObjectField);
        value = field?.GetValue(target) as Object;
        return value != null;
    }

    private static bool HasObjectFieldValue(object target, string fieldName) =>
        FindField(target, fieldName,
            f => IsUnityObjectField(f) && f.GetValue(target) is Object value && value != null) != null;

    private static Transform TryGetTransformPropertyValue(object target, string propertyName) =>
        FindProperty(target, propertyName, p => p.PropertyType == typeof(Transform))?.GetValue(target) as Transform;

    private static Object TryGetObjectPropertyValue(object target, string propertyName) =>
        FindProperty(target, propertyName, p => typeof(Object).IsAssignableFrom(p.PropertyType))?.GetValue(target) as Object;

    private static XRInputModalityManager.InputMode GetInputModeField(XRInputModalityManager inputModalityManager, string fieldName)
    {
        FieldInfo field = FindField(inputModalityManager, fieldName,
            f => f.FieldType == typeof(XRInputModalityManager.InputMode));

        return field != null
            ? (XRInputModalityManager.InputMode)field.GetValue(inputModalityManager)
            : XRInputModalityManager.InputMode.None;
    }

    /// <summary>Invoca un método privado sin argumentos, usado para forzar el refresco de modalidad.</summary>
    private static bool TryInvokeNoArgumentMethod(object target, string methodName)
    {
        if (target == null) return false;

        for (System.Type type = target.GetType(); type != null; type = type.BaseType)
        {
            MethodInfo method = type.GetMethod(methodName, InstanceFieldFlags);
            if (method == null) continue;

            method.Invoke(target, null);
            return true;
        }

        return false;
    }
}
