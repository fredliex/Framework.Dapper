using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq.Expressions;

namespace Framework.Data
{
    partial class ModelMerger<T>
    {
        private sealed class Comparer : EqualityComparer<T>
        {
            private readonly Func<T, T, bool> keyEquals;
            private readonly Func<T, T, bool> valueEquals;
            private readonly Func<T, int> keyHashGetter;

            internal Comparer(IEnumerable<MemberInfo> members, IEnumerable<MemberInfo> keyMembers)
            {
                if (keyMembers == null || !keyMembers.Any()) throw new Exception("必須指定Key。");
                keyEquals = GetEquals(keyMembers);
                valueEquals = GetEquals(members.Except(keyMembers));
                keyHashGetter = GetHashGetter(keyMembers);
            }
            
            private static Func<T, T, bool> GetEquals(IEnumerable<MemberInfo> equalMembers)
            {
                //return EqualityComparer<T1>.Default.Equals(x.prop1, y.prop1) && EqualityComparer<T2>.Default.Equals(x.prop2, y.prop2) ...etc
                var expParamX = Expression.Parameter(typeof(T));
                var expParamY = Expression.Parameter(typeof(T));
                Expression expBody = null;
                foreach(var member in equalMembers)
                {
                    var propType = member is PropertyInfo prop ? prop.PropertyType : ((FieldInfo)member).FieldType;
                    var comparerType = typeof(EqualityComparer<>).MakeGenericType(propType);
                    var expCondition = Expression.Call(
                        Expression.Property(null, comparerType.GetProperty(nameof(EqualityComparer<int>.Default))),
                        comparerType.GetMethod(nameof(EqualityComparer<int>.Equals), new[] { propType, propType }),
                        new[] { Expression.MakeMemberAccess(expParamX, member), Expression.MakeMemberAccess(expParamY, member) }
                    );
                    expBody = expBody == null ? (Expression)expCondition : Expression.And(expBody, expCondition);
                }
                if (expBody == null) expBody = Expression.Constant(true);
                return Expression.Lambda<Func<T, T, bool>>(expBody, new[] { expParamX, expParamY }).Compile();
            }


            private static Func<T, int> GetHashGetter(IEnumerable<MemberInfo> hashMembers)
            {
                /*
                 * return CombineHashCodes(new[] { 
                 *      EqualityComparer<T1>.Default.GetHashCode(obj.prop1),
                 *      EqualityComparer<T2>.Default.GetHashCode(obj.prop2),
                 *      ...etc
                 * })
                 */
                var expParamObj = Expression.Parameter(typeof(T));
                var expHashArray = hashMembers.Select(member =>
                {
                    var propType = member is PropertyInfo prop ? prop.PropertyType : ((FieldInfo)member).FieldType;
                    var comparerType = typeof(EqualityComparer<>).MakeGenericType(propType);
                    return Expression.Call(
                        Expression.Property(null, comparerType.GetProperty(nameof(EqualityComparer<int>.Default))),
                        comparerType.GetMethod(nameof(EqualityComparer<int>.GetHashCode), new[] { propType }),
                        new[] { Expression.MakeMemberAccess(expParamObj, member) }
                    );
                });
                var expBody = Expression.Call(
                    typeof(InternalHelper).GetMethod(nameof(InternalHelper.CombineHashCodes), BindingFlags.NonPublic | BindingFlags.Static),
                    Expression.NewArrayInit(typeof(int), expHashArray)
                );
                return Expression.Lambda<Func<T, int>>(expBody, new[] { expParamObj }).Compile();
            }

            public override bool Equals(T x, T y) => keyEquals(x, y);
            public override int GetHashCode(T obj) => keyHashGetter(obj);
            public bool ValueEquals(T x, T y) => valueEquals(x, y);
        }
    }
}
