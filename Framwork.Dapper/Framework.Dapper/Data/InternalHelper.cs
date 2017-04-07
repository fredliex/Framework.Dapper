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

        [Obsolete]
        internal static bool IsEnumerableParameter(object param)
        {
            //非string, 是IEnumerable, 非LinqBinary
            return param != null && param is IEnumerable && !(param is string) && param.GetType().FullName != Reflect.Dapper.LinqBinary;
        }

        [Obsolete]
        internal static bool IsEnumerableParameter(Type type)
        {
            //非string, 是IEnumerable, 非LinqBinary
            return type != null && type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type) && type.FullName != Reflect.Dapper.LinqBinary;
        }

        public static IEnumerable<object> GetElementValues(object obj)
        {
            if (obj == null) return null;
            var objs = obj as IEnumerable<object>;
            return objs != null && !(obj is string) && obj.GetType().FullName != Reflect.Dapper.LinqBinary ? objs : null;
        }

        /// <summary>取得集合的元素類型，這邊是資料庫的邏輯。譬如字串為非集合。</summary>
        /// <param name="type"></param>
        /// <returns>非集合的話回傳null</returns>
        internal static Type GetElementType(Type collectionType)
        {
            //非string, 是IEnumerable, 非LinqBinary
            if (collectionType != null && collectionType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(collectionType) && collectionType.FullName != Reflect.Dapper.LinqBinary)
            {
                var enumerableType = collectionType.GetInterfaces().FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
                //如果沒有實作IEnumerable<>表示為IEnumerable, 型別只是object
                return enumerableType == null ? typeof(object) : enumerableType.GetGenericArguments().First();
            }
            return null;
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

        /// <summary>將Dictionary的Value處理Enum轉換</summary>
        /// <param name="dict"></param>
        /// <returns></returns>
        internal static Dictionary<string, object> WrapDictionaryParam(IEnumerable<KeyValuePair<string, object>> dict)
        {
            return dict.ToDictionary(n => n.Key, n =>
            {
                var value = n.Value;
                if (value == null) return value;

                var valueType = value.GetType();
                var elemType = GetElementType(valueType);
                var isCollection = elemType != null;
                if (elemType == null) elemType = valueType;
                var nullableType = Nullable.GetUnderlyingType(elemType);
                var isNullableType = nullableType != null;
                if (nullableType != null) elemType = nullableType;
                if (elemType.IsEnum)
                {
                    var method = ModelWrapper.EnumInfo.Get(elemType).Metadata.GetConverter(isNullableType, isCollection);
                    return method.Invoke(null, new object[] { value });
                }
                return value;
            });
        }

    }
}
