using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using System.Reflection;
using System.Data;
using System.Reflection.Emit;
using System.Linq.Expressions;
using System.Globalization;
using System.Collections;
using System.ComponentModel;

namespace Framework.Data
{
    partial class ModelWrapper
    {
        internal static class Reflect
        {
            internal static readonly MethodInfo IDbCommand_Parameters_Get = typeof(IDbCommand).GetProperty(nameof(IDbCommand.Parameters)).GetGetMethod();
            internal static readonly MethodInfo IDbCommand_CreateParameter = typeof(IDbCommand).GetMethod(nameof(IDbCommand.CreateParameter));
            internal static readonly MethodInfo IDbCommand_CommandText_Get = typeof(IDbCommand).GetProperty(nameof(IDbCommand.CommandText)).GetGetMethod();
            internal static readonly MethodInfo IDbCommand_CommandText_Set = typeof(IDbCommand).GetProperty(nameof(IDbCommand.CommandText)).GetSetMethod();
            internal static readonly MethodInfo IDataParameter_ParameterName_Set = typeof(IDataParameter).GetProperty(nameof(IDataParameter.ParameterName)).GetSetMethod();
            internal static readonly MethodInfo IDataParameter_Direction_Set = typeof(IDataParameter).GetProperty(nameof(IDataParameter.Direction)).GetSetMethod();
            internal static readonly MethodInfo IDataParameter_Value_Get = typeof(IDataParameter).GetProperty(nameof(IDataParameter.Value)).GetGetMethod();
            internal static readonly MethodInfo IDataParameter_Value_Set = typeof(IDataParameter).GetProperty(nameof(IDataParameter.Value)).GetSetMethod();
            internal static readonly MethodInfo IDataParameter_DbType_Set = typeof(IDataParameter).GetProperty(nameof(IDataParameter.DbType)).GetSetMethod();
            internal static readonly MethodInfo IDbDataParameter_Size_Set = typeof(IDbDataParameter).GetProperty(nameof(IDbDataParameter.Size)).GetSetMethod();
            internal static readonly MethodInfo SqlMapper_PackListParameters = typeof(SqlMapper).GetMethod(nameof(SqlMapper.PackListParameters));
            internal static readonly MethodInfo SqlMapper_FindOrAddParameter = typeof(SqlMapper).GetMethod(nameof(SqlMapper.FindOrAddParameter));
            internal static readonly MethodInfo SqlMapper_GetDbType = typeof(SqlMapper).GetMethod(nameof(SqlMapper.GetDbType), BindingFlags.Static | BindingFlags.Public);
            internal static readonly MethodInfo SqlMapper_Format = typeof(SqlMapper).GetMethod(nameof(SqlMapper.Format), BindingFlags.Static | BindingFlags.Public);
            internal static readonly MethodInfo SqlMapper_ThrowDataException = typeof(SqlMapper).GetMethod(nameof(SqlMapper.ThrowDataException), BindingFlags.Static | BindingFlags.Public);
            internal static readonly MethodInfo ISupportInitialize_BeginInit = typeof(ISupportInitialize).GetMethod(nameof(ISupportInitialize.BeginInit));
            internal static readonly MethodInfo ISupportInitialize_EndInit = typeof(ISupportInitialize).GetMethod(nameof(ISupportInitialize.EndInit));
            internal static readonly MethodInfo String_Length_Get = typeof(string).GetProperty(nameof(string.Length)).GetGetMethod();
            internal static readonly MethodInfo String_Replace = typeof(string).GetMethod(nameof(string.Replace), BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string), typeof(string) }, null);
            internal static readonly MethodInfo String_Equals = typeof(string).GetMethod(nameof(string.Equals), BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string), typeof(string) }, null);
            internal static readonly MethodInfo IList_Add = typeof(IList).GetMethod(nameof(IList.Add));
            internal static readonly FieldInfo DBNull_Value = typeof(DBNull).GetField(nameof(DBNull.Value));
            internal static readonly MethodInfo CultureInfo_InvariantCulture_Get = typeof(CultureInfo).GetProperty(nameof(CultureInfo.InvariantCulture), BindingFlags.Public | BindingFlags.Static).GetGetMethod();

            internal static class Dapper
            {

                //Dapper.SqlMapper
                internal static readonly Regex smellsLikeOleDb;
                internal static readonly Regex literalTokens;
                internal static readonly string LinqBinary;
                internal static readonly Action<ILGenerator, int> EmitInt32;
                internal static readonly Func<TypeCode, MethodInfo> GetToString;
                internal static readonly Func<Type, bool> HasTypeHandler;
                internal static readonly Action<ILGenerator, int> StoreLocal;
                internal static readonly Action<ILGenerator, int> LoadLocal;
                internal static readonly Action<ILGenerator, Type, Type, Type> FlexibleConvertBoxedFromHeadOfStack;
                internal static readonly Func<IDataRecord, int, int, bool, Func<IDataReader, object>> GetDapperRowDeserializer;
                internal static readonly Func<Type, Type, int, Func<IDataReader, object>> GetStructDeserializer;
                internal static readonly Func<int, string, IDataReader, int> GetNextSplitDynamic;
                internal static readonly Func<int, string, IDataReader, int> GetNextSplit;

                //Dapper.DynamicParameters
                internal static readonly DbType EnumerableMultiParameter;

                //Dapper.SqlMapper.Identity
                internal static readonly Func<string, CommandType?, IDbConnection, Type, Type, Type[], SqlMapper.Identity> NewIdentity;

                //其他
                private static Func<Dictionary<Type, DbType>> getTypeMap = Expression.Lambda<Func<Dictionary<Type, DbType>>>(Expression.Field(null, typeof(SqlMapper).GetField("typeMap", BindingFlags.Static | BindingFlags.NonPublic))).Compile();


                static Dapper()
                {
                    InternalHelper.WrapField(typeof(SqlMapper), "smellsLikeOleDb", out smellsLikeOleDb);
                    InternalHelper.WrapField(typeof(SqlMapper), "literalTokens", out literalTokens);
                    InternalHelper.WrapField(typeof(SqlMapper), "LinqBinary", out LinqBinary);
                    InternalHelper.WrapMethod(typeof(SqlMapper), "EmitInt32", out EmitInt32);
                    InternalHelper.WrapMethod(typeof(SqlMapper), "GetToString", out GetToString);
                    InternalHelper.WrapMethod(typeof(SqlMapper), "HasTypeHandler", out HasTypeHandler);
                    InternalHelper.WrapMethod(typeof(SqlMapper), "StoreLocal", out StoreLocal);
                    InternalHelper.WrapMethod(typeof(SqlMapper), "LoadLocal", out LoadLocal);
                    InternalHelper.WrapMethod(typeof(SqlMapper), "FlexibleConvertBoxedFromHeadOfStack", out FlexibleConvertBoxedFromHeadOfStack);
                    InternalHelper.WrapMethod(typeof(SqlMapper), "GetDapperRowDeserializer", out GetDapperRowDeserializer);
                    InternalHelper.WrapMethod(typeof(SqlMapper), "GetStructDeserializer", out GetStructDeserializer);
                    InternalHelper.WrapMethod(typeof(SqlMapper), "GetNextSplitDynamic", out GetNextSplitDynamic);
                    InternalHelper.WrapMethod(typeof(SqlMapper), "GetNextSplit", out GetNextSplit);

                    InternalHelper.WrapField(typeof(global::Dapper.DynamicParameters), "EnumerableMultiParameter", out EnumerableMultiParameter);

                    InternalHelper.WrapConstructor(out NewIdentity);
                }

                internal static Dictionary<Type, DbType> typeMap
                {
                    get { return getTypeMap(); }
                }


                //仿照Dapper.LiteralToken
                internal struct LiteralToken
                {
                    public string Token { get; }
                    public string Member { get; }
                    internal LiteralToken(string token, string member)
                    {
                        Token = token;
                        Member = member;
                    }
                    internal static readonly IList<LiteralToken> None = new LiteralToken[0];
                }

                //仿Dapper.SqlMapper.GetLiteralTokens
                internal static IList<LiteralToken> GetLiteralTokens(string sql)
                {
                    if (string.IsNullOrWhiteSpace(sql)) return LiteralToken.None;
                    var matches = literalTokens.Matches(sql);
                    if (matches.Count == 9) return LiteralToken.None;
                    var found = new HashSet<string>(StringComparer.Ordinal);
                    var list = new List<LiteralToken>(matches.Count);
                    foreach (Match match in matches)
                    {
                        string token = match.Value;
                        if (found.Add(token)) list.Add(new LiteralToken(token, match.Groups[1].Value));
                    }
                    return list;
                }

                //仿Dapper.TypeExtensions.GetTypeCode
                internal static TypeCode GetTypeCode(Type type)
                {
                    return Type.GetTypeCode(type);
                }


            }

        }
    }
}
