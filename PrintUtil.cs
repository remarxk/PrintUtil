using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
public class IgnorePrintAttribute : Attribute { }

public static class PrintUtil
{
    private static readonly Dictionary<Type, Action<StringBuilder, object, int>> s_PrintAction = new();
    private static readonly Stack<StringBuilder> s_stringBuilderPool = new();

    private static readonly MethodInfo Object_ToString;
    private static readonly MethodInfo IEnumerable_GetEnumerator;
    private static readonly MethodInfo IEnumerator_MoveNext;
    private static readonly MethodInfo StringBuilder_Append;
    private static readonly MethodInfo StringBuilder_AppendIndent;
    private static readonly MethodInfo StringBuilder_Remove;
    private static readonly MethodInfo PrintMethod;

    static PrintUtil()
    {
        Object_ToString = typeof(object).GetMethod("ToString");
        IEnumerable_GetEnumerator = typeof(IEnumerable).GetMethod("GetEnumerator");
        IEnumerator_MoveNext = typeof(IEnumerator).GetMethod("MoveNext");
        StringBuilder_Append = typeof(StringBuilder).GetMethod("Append", new Type[] { typeof(string) });
        StringBuilder_AppendIndent = typeof(StringBuilderExtension).GetMethod("AppendIndent", new Type[] { typeof(StringBuilder), typeof(int), typeof(string) });
        StringBuilder_Remove = typeof(StringBuilder).GetMethod("Remove", new Type[] { typeof(int), typeof(int) });
        PrintMethod = typeof(PrintUtil).GetMethod("ExecutePrint", BindingFlags.Static | BindingFlags.NonPublic);
    }

    public static string Print(this object target)
    {
        StringBuilder result = GetStringBuilder();
        ExecutePrint(result, target, 0);
        string printStr = result.ToString();
        PushStringBuilder(result);
        return printStr;
    }

    private static Action<StringBuilder, object, int> CreateFieldPrintAction(Type type)
    {
        static Expression AppendIndent(Expression sb, Expression indent, Expression str)
        {
            return Expression.Call(StringBuilder_AppendIndent, sb, indent, str);
        }

        static Expression Append(Expression sb, Expression str)
        {
            return Expression.Call(sb, StringBuilder_Append, str);
        }

        var result = Expression.Parameter(typeof(StringBuilder), "result");
        var parameter = Expression.Parameter(typeof(object), "param");
        var indent = Expression.Parameter(typeof(int), "indent");
        var variables = new List<ParameterExpression>();
        var body = new List<Expression>();

        var target = Expression.Variable(type, "target");
        var nextIndent = Expression.Variable(typeof(int), "nextIndent");
        var next2Indent = Expression.Variable(typeof(int), "next2Indent");
        var next3Indent = Expression.Variable(typeof(int), "next3Indent");

        variables.Add(target);
        variables.Add(nextIndent);
        variables.Add(next2Indent);
        variables.Add(next3Indent);

        body.Add(Expression.Assign(target, Expression.Convert(parameter, type)));
        body.Add(Expression.Assign(nextIndent, Expression.Add(indent, Expression.Constant(1))));
        body.Add(Expression.Assign(next2Indent, Expression.Add(nextIndent, Expression.Constant(1))));
        body.Add(Expression.Assign(next3Indent, Expression.Add(next2Indent, Expression.Constant(1))));

        body.Add(AppendIndent(result, indent, Expression.Constant("{")));
        body.Add(Append(result, Expression.Constant("\n")));
        foreach (var fieldInfo in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (fieldInfo.GetCustomAttribute<IgnorePrintAttribute>() != null)
                continue;

            var field = Expression.Field(target, fieldInfo);

            body.Add(AppendIndent(result, nextIndent, Expression.Constant($"{fieldInfo.Name} = ")));

            if (fieldInfo.FieldType.IsOverrideToString())
            {
                if (fieldInfo.FieldType.IsValueType)
                {
                    body.Add(Append(result, Expression.Call(field, Object_ToString)));
                }
                else
                {
                    body.Add(
                        Expression.IfThenElse(
                            Expression.NotEqual(field, Expression.Constant(null)),
                            Append(result, Expression.Call(field, Object_ToString)),
                            Append(result, Expression.Constant("null"))
                        )
                    );
                }
            }
            else if (fieldInfo.FieldType.IsIEnumerable())
            {
                var foreachBody = new List<Expression>();
                var enumerator = Expression.Variable(typeof(IEnumerator));
                var breakLabel = Expression.Label();

                foreachBody.Add(enumerator);
                foreachBody.Add(Expression.Assign(enumerator, Expression.Call(field, IEnumerable_GetEnumerator)));

                foreachBody.Add(Append(result, Expression.Constant("\n")));
                foreachBody.Add(AppendIndent(result, next2Indent, Expression.Constant("[")));
                foreachBody.Add(Append(result, Expression.Constant("\n")));

                foreachBody.Add(
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.Call(enumerator, IEnumerator_MoveNext),
                            Expression.Block(
                                Expression.Call(PrintMethod, result, Expression.Property(enumerator, "Current"), next3Indent),
                                Append(result, Expression.Constant(",\n"))
                            ),
                            Expression.Break(breakLabel)
                        ),
                        breakLabel
                    )
                );

                var lastIndex = Expression.Subtract(Expression.Property(result, "Length"), Expression.Constant(1));
                foreachBody.Add(Expression.IfThen(
                    Expression.Equal(
                        Expression.MakeIndex(result, typeof(StringBuilder).GetProperty("Chars"),
                        new Expression[] { lastIndex }), Expression.Constant(',')
                    ),
                    Expression.Call(result, StringBuilder_Remove, lastIndex, Expression.Constant(1))
                ));

                foreachBody.Add(AppendIndent(result, next2Indent, Expression.Constant("]")));

                var foreachCondition = Expression.IfThenElse(
                    Expression.NotEqual(field, Expression.Constant(null)),
                    Expression.Block(new ParameterExpression[] { enumerator }, foreachBody),
                    Append(result, Expression.Constant("null"))
                );

                body.Add(foreachCondition);
            }
            else
            {
                body.Add(Append(result, Expression.Constant("\n")));
                body.Add(Expression.Call(PrintMethod, result, field, next2Indent));
            }

            body.Add(Append(result, Expression.Constant(",\n")));
        }

        var sbLastIndex = Expression.Subtract(Expression.Property(result, "Length"), Expression.Constant(1));
        var condition = Expression.IfThen(
            Expression.Equal(
                Expression.MakeIndex(result, typeof(StringBuilder).GetProperty("Chars"),
                new Expression[] { sbLastIndex }), Expression.Constant(',')
            ),
            Expression.Call(result, StringBuilder_Remove, sbLastIndex, Expression.Constant(1))
        );

        body.Add(condition);
        body.Add(AppendIndent(result, indent, Expression.Constant("}")));

        var returnLabel = Expression.Label(typeof(string));
        body.Add(Expression.Return(returnLabel, Expression.Call(result, Object_ToString)));

        body.Add(Expression.Label(returnLabel, Expression.Constant("")));

        var block = Expression.Block(variables, body);
        var lambda = Expression.Lambda<Action<StringBuilder, object, int>>(block, result, parameter, indent);

        return lambda.Compile();
    }

    private static Action<StringBuilder, object, int> CreatePropertyPrintAction(Type type)
    {
        static Expression AppendIndent(Expression sb, Expression indent, Expression str)
        {
            return Expression.Call(StringBuilder_AppendIndent, sb, indent, str);
        }

        static Expression Append(Expression sb, Expression str)
        {
            return Expression.Call(sb, StringBuilder_Append, str);
        }

        var result = Expression.Parameter(typeof(StringBuilder), "result");
        var parameter = Expression.Parameter(typeof(object), "param");
        var indent = Expression.Parameter(typeof(int), "indent");

        var variables = new List<ParameterExpression>();
        var body = new List<Expression>();

        var target = Expression.Variable(type, "target");
        var nextIndent = Expression.Variable(typeof(int), "nextIndent");
        var next2Indent = Expression.Variable(typeof(int), "next2Indent");
        var next3Indent = Expression.Variable(typeof(int), "next3Indent");

        variables.Add(target);
        variables.Add(nextIndent);
        variables.Add(next2Indent);
        variables.Add(next3Indent);

        body.Add(Expression.Assign(target, Expression.Convert(parameter, type)));
        body.Add(Expression.Assign(nextIndent, Expression.Add(indent, Expression.Constant(1))));
        body.Add(Expression.Assign(next2Indent, Expression.Add(nextIndent, Expression.Constant(1))));
        body.Add(Expression.Assign(next3Indent, Expression.Add(next2Indent, Expression.Constant(1))));

        body.Add(AppendIndent(result, indent, Expression.Constant("{")));
        body.Add(Append(result, Expression.Constant("\n")));
        foreach (var propertyInfo in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (propertyInfo.GetCustomAttribute<IgnorePrintAttribute>() != null)
                continue;

            var field = Expression.Property(target, propertyInfo);

            body.Add(AppendIndent(result, nextIndent, Expression.Constant($"{propertyInfo.Name} = ")));

            if (propertyInfo.PropertyType.IsOverrideToString())
            {
                if (propertyInfo.PropertyType.IsValueType)
                {
                    body.Add(Append(result, Expression.Call(field, Object_ToString)));
                }
                else
                {
                    body.Add(
                        Expression.IfThenElse(
                            Expression.NotEqual(field, Expression.Constant(null)),
                            Append(result, Expression.Call(field, Object_ToString)),
                            Append(result, Expression.Constant("null"))
                        )
                    );
                }
            }
            else if (propertyInfo.PropertyType.IsIEnumerable())
            {
                var foreachBody = new List<Expression>();
                var enumerator = Expression.Variable(typeof(IEnumerator));
                var breakLabel = Expression.Label();

                foreachBody.Add(enumerator);
                foreachBody.Add(Expression.Assign(enumerator, Expression.Call(field, IEnumerable_GetEnumerator)));

                foreachBody.Add(Append(result, Expression.Constant("\n")));
                foreachBody.Add(AppendIndent(result, next2Indent, Expression.Constant("[")));
                foreachBody.Add(Append(result, Expression.Constant("\n")));

                foreachBody.Add(
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.Call(enumerator, IEnumerator_MoveNext),
                            Expression.Block(
                                Expression.Call(PrintMethod, result, Expression.Property(enumerator, "Current"), next3Indent),
                                Append(result, Expression.Constant(",\n"))
                            ),
                            Expression.Break(breakLabel)
                        ),
                        breakLabel
                    )
                );

                var lastIndex = Expression.Subtract(Expression.Property(result, "Length"), Expression.Constant(1));
                foreachBody.Add(Expression.IfThen(
                    Expression.Equal(
                        Expression.MakeIndex(result, typeof(StringBuilder).GetProperty("Chars"),
                        new Expression[] { lastIndex }), Expression.Constant(',')
                    ),
                    Expression.Call(result, StringBuilder_Remove, lastIndex, Expression.Constant(1))
                ));

                foreachBody.Add(AppendIndent(result, next2Indent, Expression.Constant("]")));

                var foreachCondition = Expression.IfThenElse(
                    Expression.NotEqual(field, Expression.Constant(null)),
                    Expression.Block(new ParameterExpression[] { enumerator }, foreachBody),
                    Append(result, Expression.Constant("null"))
                );

                body.Add(foreachCondition);
            }
            else
            {
                body.Add(Append(result, Expression.Constant("\n")));
                body.Add(Expression.Call(PrintMethod, result, field, next2Indent));
            }

            body.Add(Append(result, Expression.Constant(",\n")));
        }

        var sbLastIndex = Expression.Subtract(Expression.Property(result, "Length"), Expression.Constant(1));
        var condition = Expression.IfThen(
            Expression.Equal(
                Expression.MakeIndex(result, typeof(StringBuilder).GetProperty("Chars"),
                new Expression[] { sbLastIndex }), Expression.Constant(',')
            ),
            Expression.Call(result, StringBuilder_Remove, sbLastIndex, Expression.Constant(1))
        );

        body.Add(condition);
        body.Add(AppendIndent(result, indent, Expression.Constant("}")));

        var block = Expression.Block(variables, body);
        var lambda = Expression.Lambda<Action<StringBuilder, object, int>>(block, result, parameter, indent);

        return lambda.Compile();
    }

    private static void ExecutePrint(StringBuilder result, object target, int indent)
    {
        if (target == null)
        {
            result.AppendIndent(indent, "null");
            return;
        }

        Type type = target.GetType();

        if (s_PrintAction.ContainsKey(type))
        {
            s_PrintAction[type](result, target, indent);
            return;
        }

        if (type.IsOverrideToString())
        {
            result.AppendIndent(indent, target.ToString());
            return;
        }

        if (type.IsIEnumerable())
        {
            IEnumerable enumerable = target as IEnumerable;

            result.AppendIndent(indent, '[');
            result.AppendLine();
            foreach (var item in enumerable)
            {
                ExecutePrint(result, item, indent + 1);
                result.AppendLine(",");
            }
            if (result[^1] == ',')
                result.Remove(result.Length - 1, 1);
            result.AppendIndent(indent, ']');
            result.AppendLine();

            return;
        }

        var action = CreatePropertyPrintAction(type);
        //var action = CreateFieldPrintAction(type);
        s_PrintAction[type] = action;

        s_PrintAction[type](result, target, indent);
    }

    private static Expression Debug(Expression param)
    {
        var method = typeof(Console).GetMethod("WriteLine", new Type[] { typeof(string) });
        return Expression.Call(method, param);
    }

    private static StringBuilder GetStringBuilder()
    {
        if (s_stringBuilderPool.Count > 0)
            return s_stringBuilderPool.Pop();

        var sb = new StringBuilder();
        return sb;
    }

    private static void PushStringBuilder(StringBuilder stringBuilder)
    {
        stringBuilder.Clear();
        s_stringBuilderPool.Push(stringBuilder);
    }
}