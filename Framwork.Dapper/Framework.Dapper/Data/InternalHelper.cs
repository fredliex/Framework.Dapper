using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;


namespace Framework.Data
{
    internal static class InternalHelper
    {
        private static readonly Action<ILGenerator, object, Type> funcEmitConstant;

        static InternalHelper()
        {
            var typeILGen = typeof(Expression).Assembly.GetType("System.Linq.Expressions.Compiler.ILGen");
            InternalHelper.WrapMethod(typeILGen, "EmitConstant", out funcEmitConstant);
        }

        internal static T GetAttribute<T>(this MemberInfo member, bool inhert) where T : Attribute
        {
            return (T)member.GetCustomAttributes(typeof(T), inhert).FirstOrDefault();
        }

        internal static void WrapField<T>(Type type, string fieldName, out T value, object obj = null)
        {
            value = (T)type.GetField(fieldName, BindingFlags.NonPublic | (obj == null ? BindingFlags.Static : BindingFlags.Instance)).GetValue(obj);
        }

        internal static void WrapMethod<T>(Type type, string methodName, out T lambda, bool isStatic = true) where T : class
        {
            var parmeterTypes = typeof(T).GetGenericArguments();
            if (typeof(T).FullName.StartsWith("System.Func")) parmeterTypes = parmeterTypes.Take(parmeterTypes.Length - 1).ToArray();
            var method = type.GetMethod(methodName, (isStatic ? BindingFlags.Static : BindingFlags.Instance) | BindingFlags.NonPublic, null, parmeterTypes, null);
            var instance = isStatic ? null : Expression.Parameter(type, "instance");
            var parmeters = parmeterTypes.Select((p, i) => Expression.Parameter(p, "p" + i)).ToArray();
            var body = Expression.Call(instance, method, parmeters);
            lambda = Expression.Lambda<T>(body, isStatic ? parmeters : new[] { instance }.Concat(parmeters)).Compile();
        }

        internal static void EmitConstant(this ILGenerator il, object value, Type type = null)
        {
            funcEmitConstant(il, value, type);
        }

        internal static void EmitConstant(this ILGenerator il, object value)
        {
            funcEmitConstant(il, value, value.GetType());
        }


    }
}
