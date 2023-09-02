using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

public static class TypeExtension
{
    public static List<(Type type, FieldInfo fieldInfo)> GetAllFields(this Type type, BindingFlags flags)
    {
        List<(Type type, FieldInfo fieldInfo)> list = new();

        if (type == null)
            return list;

        if (type.BaseType != null)
            list.AddRange(type.BaseType.GetAllFields(flags));

        foreach (var fieldInfo in type.GetFields(flags))
            list.Add((type, fieldInfo));

        return list;
    }

    public static List<(Type type, PropertyInfo propertyInfo)> GetAllProperties(this Type type, BindingFlags flags)
    {
        List<(Type type, PropertyInfo propertyInfo)> list = new();

        if (type == null)
            return list;

        if (type.BaseType != null)
            list.AddRange(type.BaseType.GetAllProperties(flags));

        foreach (var propertyInfo in type.GetProperties(flags))
            list.Add((type, propertyInfo));

        return list;
    }

    public static bool IsOverrideToString(this Type type)
    {
        if (type.IsPrimitive || type == typeof(string))
            return true;

        MethodInfo methodInfo = type.GetMethod("ToString", new Type[] {});
        return methodInfo.GetCustomAttribute<IgnorePrintAttribute>() == null
        && methodInfo.DeclaringType == type;
    }

    public static bool IsIEnumerable(this Type type)
    {
        return typeof(IEnumerable).IsAssignableFrom(type);
    }
}