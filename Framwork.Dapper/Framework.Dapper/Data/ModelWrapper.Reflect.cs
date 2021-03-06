﻿using System;
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
            internal static readonly MethodInfo IDataReader_IsDBNull = typeof(IDataReader).GetMethod(nameof(IDataReader.IsDBNull));
            internal static readonly MethodInfo IDataReader_GetValue = typeof(IDataReader).GetMethod(nameof(IDataReader.GetValue));
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

                //其他
                private static Func<Dictionary<Type, DbType>> getTypeMap = Expression.Lambda<Func<Dictionary<Type, DbType>>>(Expression.Field(null, typeof(SqlMapper).GetField("typeMap", BindingFlags.Static | BindingFlags.NonPublic))).Compile();


                static Dapper()
                {
                    InternalDbHelper.WrapField(typeof(SqlMapper), "smellsLikeOleDb", out smellsLikeOleDb);
                    InternalDbHelper.WrapField(typeof(SqlMapper), "literalTokens", out literalTokens);
                    InternalDbHelper.WrapField(typeof(SqlMapper), "LinqBinary", out LinqBinary);
                    InternalDbHelper.WrapMethod(typeof(SqlMapper), "EmitInt32", out EmitInt32);
                    InternalDbHelper.WrapMethod(typeof(SqlMapper), "GetToString", out GetToString);
                    InternalDbHelper.WrapMethod(typeof(SqlMapper), "HasTypeHandler", out HasTypeHandler);
                    InternalDbHelper.WrapMethod(typeof(SqlMapper), "StoreLocal", out StoreLocal);
                    InternalDbHelper.WrapMethod(typeof(SqlMapper), "LoadLocal", out LoadLocal);
                    InternalDbHelper.WrapMethod(typeof(SqlMapper), "FlexibleConvertBoxedFromHeadOfStack", out FlexibleConvertBoxedFromHeadOfStack);
                    InternalDbHelper.WrapMethod(typeof(SqlMapper), "GetDapperRowDeserializer", out GetDapperRowDeserializer);
                    InternalDbHelper.WrapMethod(typeof(SqlMapper), "GetStructDeserializer", out GetStructDeserializer);
                    InternalDbHelper.WrapMethod(typeof(SqlMapper), "GetNextSplitDynamic", out GetNextSplitDynamic);
                    InternalDbHelper.WrapMethod(typeof(SqlMapper), "GetNextSplit", out GetNextSplit);

                    InternalDbHelper.WrapField(typeof(global::Dapper.DynamicParameters), "EnumerableMultiParameter", out EnumerableMultiParameter);
                }

                internal static Dictionary<Type, DbType> TypeMap => getTypeMap();

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

                //仿Dapper.SqlMapper.GenerateMapper
                internal static Func<IDataReader, TReturn> GenerateMapper<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(Func<IDataReader, object>[] des, object map)
                {
                    switch (des.Length)
                    {
                        case 2: return r => ((Func<TFirst, TSecond,                                            TReturn>)map)((TFirst)des[0](r), (TSecond)des[1](r));
                        case 3: return r => ((Func<TFirst, TSecond, TThird,                                    TReturn>)map)((TFirst)des[0](r), (TSecond)des[1](r), (TThird)des[2](r));
                        case 4: return r => ((Func<TFirst, TSecond, TThird, TFourth,                           TReturn>)map)((TFirst)des[0](r), (TSecond)des[1](r), (TThird)des[2](r), (TFourth)des[3](r));
                        case 5: return r => ((Func<TFirst, TSecond, TThird, TFourth, TFifth,                   TReturn>)map)((TFirst)des[0](r), (TSecond)des[1](r), (TThird)des[2](r), (TFourth)des[3](r), (TFifth)des[4](r));
                        case 6: return r => ((Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth,           TReturn>)map)((TFirst)des[0](r), (TSecond)des[1](r), (TThird)des[2](r), (TFourth)des[3](r), (TFifth)des[4](r), (TSixth)des[5](r));
                        case 7: return r => ((Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>)map)((TFirst)des[0](r), (TSecond)des[1](r), (TThird)des[2](r), (TFourth)des[3](r), (TFifth)des[4](r), (TSixth)des[5](r), (TSeventh)des[6](r));
                        default: throw new NotSupportedException();
                    }
                }
            }

        }
    }
}
