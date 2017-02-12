using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using static Framework.Data.ModelWrapper;

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

        internal static void WrapConstructor<T>(out T lambda)
        {
            var parmeterTypes = typeof(T).GetGenericArguments();
            var type = parmeterTypes.Last();
            parmeterTypes = parmeterTypes.Take(parmeterTypes.Length - 1).ToArray();
            var constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, parmeterTypes, null);
            var parmeters = parmeterTypes.Select((p, i) => Expression.Parameter(p, "p" + i)).ToArray();
            var body = Expression.New(constructor, parmeters);
            lambda = Expression.Lambda<T>(body, parmeters).Compile();
        }

        internal static void EmitConstant(this ILGenerator il, object value, Type type = null)
        {
            funcEmitConstant(il, value, type);
        }

        internal static void EmitConstant(this ILGenerator il, object value)
        {
            funcEmitConstant(il, value, value.GetType());
        }

        internal static bool IsEnumerableParameter(object param)
        {
            return param != null && param is IEnumerable && !(param is string) && param.GetType().FullName != Reflect.Dapper.LinqBinary;
        }

        internal static bool IsEnumerableParameter(Type type)
        {
            //非string, 是IEnumerable, 非LinqBinary
            return type != null && type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type) && type.FullName != Reflect.Dapper.LinqBinary;
        }

        /// <summary>
        /// 判斷是否為可null的類型。可能為物件類型或是nullable類型。
        /// </summary>
        /// <param name="type"></param>
        /// <param name="nullableType">如果是nullable類型的話，回傳基礎型別</param>
        /// <returns></returns>
        internal static bool IsNullType(Type type, out Type nullableType)
        {
            nullableType = null;
            return !type.IsValueType || (nullableType = Nullable.GetUnderlyingType(type)) != null;
        }

        internal static bool IsNullType(Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }

        /// <summary>
        /// 取得集合的元素類型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        internal static Type GetElementType(Type collectionType)
        {
            return collectionType.GetInterfaces().FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));        
        }
    }
}
