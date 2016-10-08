//#define saveParamAssembly

using Dapper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Dapper.SqlMapper;


namespace Framework.Data
{
    partial class ModelWrapper
    {
        internal static object WrapParam(object param, CommandType commandType, string sql)
        {
            if (param is IDynamicParameters) return param;
            var paramGeneratorBuilder = new ParamGeneratorBuilder(param.GetType(), commandType, sql, false);
            var paramGenerator = paramGeneratorBuilder.CreateGenerator();
            var models = param as IEnumerable;
            if (models != null && !(param is string || param is IEnumerable<KeyValuePair<string, object>>)) return new EnumerableParamWrapper(models, paramGenerator);
            return new ParamWrapper { Model = param, ParamGenerator = paramGenerator };
        }

        private sealed class ParamGeneratorBuilder
        {
            private Type modelType;
            private CommandType commandType;
            private string sql;
            private bool checkForDuplicates;
            private DynamicMethod dm;
            private ILGenerator il;
            private Queue<OpCode> _varOpCodes = new Queue<OpCode>(new[] { OpCodes.Stloc_1, OpCodes.Stloc_2 });  //區域變數數量, 一開始就會有一個區域變數, 就是model本身, 所以從Stloc_1起跳
            private OpCode? _varSize = null;  //區域變數放置ParameterSize 的OpCode
            private OpCode? _varDbType = null;  //區域變數放置ParameterDbType 的OpCode
            private Type _memberType;   //成員類型
            private DbType? _dbType;
            private ITypeHandler _handler;

            private string memberName;
            private Type memberType
            {
                get { return _memberType; }
                set
                {
                    _dbType = null;
                    _handler = null;
                    _memberType = value;
                }
            }
            private DbType dbType
            {
                get
                {
                    if (!_dbType.HasValue) _setDbTypeAndHandler();
                    return _dbType.Value;
                }
            }
            private ITypeHandler handler
            {
                get
                {
                    if (_handler == null) _setDbTypeAndHandler();
                    return _handler;
                }
            }
            private void _setDbTypeAndHandler()
            {
                var nullUnderlyingType = Nullable.GetUnderlyingType(memberType);
#pragma warning disable 618
                _dbType = LookupDbType(nullUnderlyingType ?? memberType, memberName, true, out _handler);
#pragma warning restore 618
            }

            private OpCode variableSize
            {
                get { return _varSize ?? (_varSize = _varOpCodes.Dequeue()).Value; }
            }
            private OpCode variableDbType
            {
                get { return _varDbType ?? (_varDbType = _varOpCodes.Dequeue()).Value; }
            }

            internal ParamGeneratorBuilder(Type modelType, CommandType commandType, string sql, bool checkForDuplicates)
            {
                this.modelType = modelType;
                this.commandType = commandType;
                this.sql = sql;
                this.checkForDuplicates = checkForDuplicates;
#if saveParamAssembly
                var assemblyName = new AssemblyName(string.Format("WrapParamInfo", type.Name.StartsWith("<") ? Guid.NewGuid().ToString("N") : type.Name));
			    var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
			    var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name, assemblyName.Name + ".dll");
			    var builder = moduleBuilder.DefineType(type + "ParamInfoGenerator", TypeAttributes.Public);
			    var dm = builder.DefineMethod("DynamicCreate", MethodAttributes.Public | MethodAttributes.Static, null, new[] { typeof(IDbCommand), typeof(object) });
#else
                dm = new DynamicMethod("WrapParamInfo" + Guid.NewGuid().ToString(), null, new[] { typeof(IDbCommand), typeof(object) }, modelType, true);
#endif
                il = dm.GetILGenerator();
            }

            internal Action<IDbCommand, object> CreateGenerator()
            {
                var table = GetTableInfo(modelType);

                il.Emit(OpCodes.Ldarg_1); // stack is now [untyped-param]
                if (table.IsStruct)
                {
                    il.DeclareLocal(modelType.MakePointerType());    //例如int*
                    il.Emit(OpCodes.Unbox, modelType); // stack is now [typed-param]
                }
                else
                {
                    il.DeclareLocal(modelType); // 0
                    il.Emit(OpCodes.Castclass, modelType); // stack is now [typed-param]
                }
                il.Emit(OpCodes.Stloc_0);// stack is now empty

                il.Emit(OpCodes.Ldarg_0); // stack is now [command]
                il.EmitCall(OpCodes.Callvirt, Reflect.IDbCommand_Parameters_Get, null); // stack is now [parameters]

                //取得要處理的欄位資訊
                var columns = table.Columns;
                //過濾不必要的欄位
                var actualColumns = (commandType == CommandType.Text && !Reflect.Dapper.smellsLikeOleDb.IsMatch(sql)) ? FilterParameters(columns, sql) : columns;

                //循環欄位
                foreach (var column in actualColumns)
                {
                    memberName = column.MemberName;
                    memberType = column.ValueType;

                    //如果有實作Dapper.SqlMapper.ICustomQueryParameter的話就按照Dapper的邏輯處理
                    if (typeof(ICustomQueryParameter).IsAssignableFrom(memberType))
                    {
                        il.Emit(OpCodes.Ldloc_0); // stack is now [parameters] [typed-param]
                        column.EmitGenerateGet(il); // stack is [parameters] [custom]
                        il.Emit(OpCodes.Ldarg_0); // stack is now [parameters] [custom] [command]
                        il.Emit(OpCodes.Ldstr, memberName); // stack is now [parameters] [custom] [command] [name]
                        il.EmitCall(OpCodes.Callvirt, memberType.GetMethod(nameof(ICustomQueryParameter.AddParameter)), null); // stack is now [parameters]
                        continue;
                    }

                    //如果是集合的話, 就處理"in", 這邊按原本Dapper邏輯處理
                    if (dbType == Reflect.Dapper.EnumerableMultiParameter)
                    {
                        // this actually represents special handling for list types;
                        il.Emit(OpCodes.Ldarg_0); // stack is now [parameters] [command]
                        il.Emit(OpCodes.Ldstr, memberName); // stack is now [parameters] [command] [name]
                        il.Emit(OpCodes.Ldloc_0); // stack is now [parameters] [command] [name] [typed-param]
                        column.EmitGenerateGet(il);  // stack is [parameters] [command] [name] [typed-value]
                        if (memberType.IsValueType) il.Emit(OpCodes.Box, memberType); // stack is [parameters] [command] [name] [boxed-value]
                        il.EmitCall(OpCodes.Call, Reflect.SqlMapper_PackListParameters, null); // stack is [parameters]
                        continue;
                    }
                    il.Emit(OpCodes.Dup); // stack is now [parameters] [parameters]

                    il.Emit(OpCodes.Ldarg_0); // stack is now [parameters] [parameters] [command]

                    //判斷是否要檢查有無重複的欄位定義, 這邊也是比照Dapper的處理方式
                    if (checkForDuplicates)
                    {
                        // need to be a little careful about adding; use a utility method
                        il.Emit(OpCodes.Ldstr, memberName); // stack is now [parameters] [parameters] [command] [name]
                        il.EmitCall(OpCodes.Call, Reflect.SqlMapper_FindOrAddParameter, null); // stack is [parameters] [parameter]
                    }
                    else
                    {
                        // no risk of duplicates; just blindly add
                        il.EmitCall(OpCodes.Callvirt, Reflect.IDbCommand_CreateParameter, null);// stack is now [parameters] [parameters] [parameter]

                        il.Emit(OpCodes.Dup);// stack is now [parameters] [parameters] [parameter] [parameter]
                        il.Emit(OpCodes.Ldstr, memberName); // stack is now [parameters] [parameters] [parameter] [parameter] [name]
                        il.EmitCall(OpCodes.Callvirt, Reflect.IDataParameter_ParameterName_Set, null);// stack is now [parameters] [parameters] [parameter]
                    }
                    il.Emit(OpCodes.Dup);// stack is now [parameters] [parameters] [parameter] [parameter]
                    Reflect.Dapper.EmitInt32(il, (int)ParameterDirection.Input);// stack is now [parameters] [parameters] [parameter] [parameter] [dir]
                    il.EmitCall(OpCodes.Callvirt, Reflect.IDataParameter_Direction_Set, null);// stack is now [parameters] [[parameters]] [parameter]

                    il.Emit(OpCodes.Dup);// stack is now [parameters] [parameters] [parameter] [parameter]
                    il.Emit(OpCodes.Ldloc_0); // stack is now [parameters] [parameters] [parameter] [parameter] [typed-param]
                    column.EmitGenerateGet(il); // stack is [parameters] [parameters] [parameter] [parameter] [typed-value]

                    /*
                     * 注意1. 可以null的型別才會有NullMapping
                     * 注意2. 如果有handler的話, DbType會是DbType.Object
                     * 注意3. DbType是字串是指DbType.String或DbType.AnsiString
                     * 注意4. 可為null的話 表示為Nullable或物件類型
                     * 
                     * 
                     * if (memberType無handler 且有EnumValue)  //type換成EnumValue            01.    value = getEnumValue(value);
                     * if (可為null) {
                     *    if (Nullable<>) {                                                   02.    if ( value.HasValue )
                     *    } else {                               //這時type一定是物件類型     02.    if ( value != null )
                     *    }                                      
                     *                                                                        03.        goto notNullHandle:
                     *    if (有NullMapping) {                   //type換成NullMapping        04.    value = NullMapping;
                     *    } else {                                                            04.    value = DBNull.Value;
                     *       if (DbType是字串)                                                05.    loc_1 = 0;
                     *                                                                        06.    goto nullHandleDone:
                     *    }
                     *                                                                        07. notNullHandle:
                     * }
                     * if (是值類型) {
                     *    if (Nullable)                          //type由Nullable<T>換成T     08.    value = value.GetValueOrDefault()
                     *    if (是Enum 且 無handler) {             //type換成(Enum基礎型別)     
                     *                                                                        09.    value = (object)value;
                     * } else {
                     *    if (是System.Data.Linq.Binary) {       //type換成bye[]              10.    value = value.ToArray();
                     *    } else if (DbType是字串) {                                          11.    loc_1 = value.length > 4000 ? -1 : 4000;
                     *    }
                     * }
                     * if (前面有goto nullHandleDone)                                         12. nullHandleDone:
                     * if (有handler) {                                                       13.    SqlMapper.TypeHandlerCache<T>.SetValue(parameter, value);
                     * } else {                                                               14.    paramter.Value = value;
                     *    if (DbType非Time)                                                   15.    paramter.DbType = dbType;
                     *    if (DbType是字串)                                                   17.    if (loc_1 != 0) paramter.Size = loc_1;
                     * }
                     *                                                                        18.    paramters.Add(paramter);
                     */

                    //01. memberType沒有handler時判斷 有定義EnumValue的話, 取得對應值, 同時memberType換成EnumValue
                    if (handler != null)
                    {
                        Type enumValueType;
                        var enumValueGetter = EnumValueHelper.GetValueGetterMethod(memberType, out enumValueType);
                        if (enumValueGetter != null)
                        {
                            il.EmitCall(OpCodes.Call, enumValueGetter, null);     // stack is [parameters] [parameters] [parameter] [parameter] [typed-value]
                            memberType = enumValueType;
                        }
                    }

                    Label? nullHandleDone = null;
                    Type underlyingType = null;
                    //如果可能為null的話, 才做null的判斷處理
                    if (!memberType.IsValueType || (underlyingType = Nullable.GetUnderlyingType(memberType)) != null)
                    {
                        var notNullHandle = il.DefineLabel();
                        //02. 判斷非null的話跳notNullHandle
                        il.Emit(OpCodes.Dup);    // stack is [parameters] [[parameters]] [parameter] [parameter] [typed-value] [typed-value]
                        if (underlyingType != null) il.EmitCall(OpCodes.Call, memberType.GetProperty(nameof(Nullable<int>.HasValue)).GetGetMethod(), null); // stack is [parameters] [parameters] [parameter] [parameter] [typed-value] [bool]
                        //03. goto notNullHandle
                        il.Emit(OpCodes.Brtrue_S, notNullHandle); // stack is [parameters] [parameters] [parameter] [parameter] [typed-value]

                        if (column.NullMapping != null)
                        {
                            //04. value = NullMapping;
                            il.Emit(OpCodes.Pop);       // stack is [parameters] [parameters] [parameter] [parameter]
                            il.EmitConstant(column.NullMapping);     // stack is [parameters] [parameters] [parameter] [parameter] [typed-value]
                            memberType = column.NullMapping.GetType();
                        }
                        else
                        {
                            //05. 如果DbType是字串, 設定 區域變數size = 0
                            if (dbType == DbType.String || dbType == DbType.AnsiString)
                            {
                                Reflect.Dapper.EmitInt32(il, 0);
                                il.Emit(variableSize);
                            }
                            //06. 跳到nullHandleDone
                            nullHandleDone = il.DefineLabel();
                            il.Emit(OpCodes.Br_S, nullHandleDone.Value);
                        }
                        //07. 標記notNullHandle
                        il.MarkLabel(notNullHandle);
                    }

                    if (memberType.IsValueType)
                    {
                        //08. 如果是Nullable<T>的話, 取出T來
                        if ((underlyingType = Nullable.GetUnderlyingType(memberType)) != null)
                        {
                            il.EmitCall(OpCodes.Call, memberType.GetMethod(nameof(Nullable<int>.GetValueOrDefault)), null); // stack is [parameters] [parameters] [parameter] [parameter] [typed-value]
                            memberType = underlyingType;
                        }
                        //判斷是Enum的話換成Enum的基礎型別
                        if (memberType.IsEnum && handler == null)
                        {
                            switch (Type.GetTypeCode(Enum.GetUnderlyingType(memberType)))
                            {
                                case TypeCode.Byte: memberType = typeof(byte); break;
                                case TypeCode.SByte: memberType = typeof(sbyte); break;
                                case TypeCode.Int16: memberType = typeof(short); break;
                                case TypeCode.Int32: memberType = typeof(int); break;
                                case TypeCode.Int64: memberType = typeof(long); break;
                                case TypeCode.UInt16: memberType = typeof(ushort); break;
                                case TypeCode.UInt32: memberType = typeof(uint); break;
                                case TypeCode.UInt64: memberType = typeof(ulong); break;
                            }
                        }
                        //09. value = (object)value
                        il.Emit(OpCodes.Box, memberType); // stack is [parameters] [parameters] [parameter] [parameter] [boxed-value]
                    }
                    else
                    {
                        //判斷是是System.Data.Linq.Binary的話, 呼叫value.ToArray()取得byte[]
                        if (memberType.FullName == Reflect.Dapper.LinqBinary)  //System.Data.Linq.Binary
                        {
                            //10. value = value.ToArray();
                            il.EmitCall(OpCodes.Callvirt, memberType.GetMethod("ToArray", BindingFlags.Public | BindingFlags.Instance), null); // stack is [parameters] [parameters] [parameter] [parameter] [byte[]]
                            memberType = typeof(byte[]);
                        }
                        else if (dbType == DbType.String || dbType == DbType.AnsiString)
                        {
                            //11. size = value.length > 4000 ? -1 : 4000;
                            il.Emit(OpCodes.Dup);    // stack is [parameters] [parameters] [parameter] [parameter] [typed-value] [typed-value]
                            il.EmitCall(OpCodes.Callvirt, Reflect.String_Length_Get, null); // // stack is [parameters] [parameters] [parameter] [parameter] [typed-value] [string length]
                            Reflect.Dapper.EmitInt32(il, DbString.DefaultLength); // stack is [parameters] [parameters] [parameter] [parameter] [typed-value] [string length] [4000]
                            il.Emit(OpCodes.Cgt); // stack is [parameters] [parameters] [parameter] [parameter] [typed-value] [0 or 1]
                            Label isLong = il.DefineLabel(), lenDone = il.DefineLabel();
                            il.Emit(OpCodes.Brtrue_S, isLong); // stack is [parameters] [parameters] [parameter] [parameter] [typed-value]
                            Reflect.Dapper.EmitInt32(il, DbString.DefaultLength); // stack is [parameters] [parameters] [parameter] [parameter] [typed-value] [4000]
                            il.Emit(OpCodes.Br_S, lenDone);
                            il.MarkLabel(isLong);
                            Reflect.Dapper.EmitInt32(il, -1); // stack is [parameters] [parameters] [parameter] [parameter] [typed-value] [-1]
                            il.MarkLabel(lenDone);
                            il.Emit(variableSize); // stack is [parameters] [parameters] [parameter] [parameter] [typed-value]
                        }
                    }

                    //12. 有nullHandleDone的話, 標記nullHandleDone
                    if (nullHandleDone.HasValue) il.MarkLabel(nullHandleDone.Value);

                    if (handler != null)
                    {
                        //13. 呼叫SqlMapper.TypeHandlerCache<T>.SetValue(parameter, value);
#pragma warning disable 618
                        il.Emit(OpCodes.Call, typeof(TypeHandlerCache<>).MakeGenericType(memberType).GetMethod(nameof(TypeHandlerCache<int>.SetValue))); // stack is now [parameters] [parameters] [parameter]
#pragma warning restore 618
                    }
                    else
                    {
                        //14. dbType非Time的話設定 paramter.DbType;
                        if (dbType != DbType.Time)
                        {
                            if (dbType == DbType.Object && memberType == typeof(object)) // includes dynamic
                            {
                                // look it up from the param value
                                il.Emit(OpCodes.Dup); // stack is now [parameters] [parameters] [parameter] [parameter] [typed-value] [typed-value]
                                il.Emit(OpCodes.Call, Reflect.SqlMapper_GetDbType); // stack is now [parameters] [[parameters]] [parameter] [parameter] [typed-value] [db-type]
                            }
                            else
                            {
                                Reflect.Dapper.EmitInt32(il, (int)dbType);// stack is now [parameters] [parameters] [parameter] [parameter] [typed-value] [db-type]
                            }
                            il.EmitCall(OpCodes.Callvirt, Reflect.IDataParameter_DbType_Set, null);// stack is now [parameters] [parameters] [parameter] [typed-value]
                        }

                        //15. paramter.Value = value;
                        il.EmitCall(OpCodes.Callvirt, Reflect.IDataParameter_Value_Set, null);// stack is now [parameters] [parameters] [parameter]

                        //16. if (loc_1 != 0) paramter.Size = loc_1;
                        if (dbType == DbType.String || dbType == DbType.AnsiString)
                        {
                            var endOfSize = il.DefineLabel();
                            // don't set if 0
                            il.Emit(variableSize); // [parameters] [parameters] [parameter] [size]
                            il.Emit(OpCodes.Brfalse_S, endOfSize); // [parameters] [parameters] [parameter]

                            il.Emit(OpCodes.Dup);// stack is now [parameters] [parameters] [parameter] [parameter]
                            il.Emit(variableSize); // stack is now [parameters] [parameters] [parameter] [parameter] [size]
                            il.EmitCall(OpCodes.Callvirt, Reflect.IDbDataParameter_Size_Set, null); // stack is now [parameters] [parameters] [parameter]

                            il.MarkLabel(endOfSize);
                        }
                    }
                    //17. checkForDuplicates的話, 前面已經有FindOrAdd過了, 直接釋放。非checkForDuplicates的話, paramters.Add(paramter)
                    if (checkForDuplicates)
                    {
                        // stack is now [parameters] [parameter]
                        il.Emit(OpCodes.Pop); // don't need parameter any more
                    }
                    else
                    {
                        // stack is now [parameters] [parameters] [parameter]
                        // blindly add
                        il.EmitCall(OpCodes.Callvirt, Reflect.IList_Add, null); // stack is now [parameters]
                        il.Emit(OpCodes.Pop); // IList.Add returns the new index (int); we don't care
                    }
                }

                // stack is currently [parameters]
                il.Emit(OpCodes.Pop); // stack is now empty

                //處理Literal
                ReplaceLiteral(columns);

                il.Emit(OpCodes.Ret);

#if saveParamAssembly
                var t = builder.CreateType();
                assemblyBuilder.Save(assemblyName.Name + ".dll");
                return (Action<IDbCommand, object>)Delegate.CreateDelegate(typeof(Action<IDbCommand, object>), t.GetMethod(dm.Name));
#else
                return (Action<IDbCommand, object>)dm.CreateDelegate(typeof(Action<IDbCommand, object>));
#endif
            }

            private void ReplaceLiteral(ColumnInfo[] columns)
            {
                //仿Dapper的邏輯處理 {=aaaa} 這種東西, 簡單的說就是sql字串替換
                var literals = Reflect.Dapper.GetLiteralTokens(sql);
                if (literals.Count != 0)
                {
                    il.Emit(OpCodes.Ldarg_0); // command
                    il.Emit(OpCodes.Ldarg_0); // command, command
                    il.EmitCall(OpCodes.Callvirt, Reflect.IDbCommand_CommandText_Get, null); // command, sql
                    Dictionary<Type, LocalBuilder> locals = null;
                    LocalBuilder local = null;
                    foreach (var literal in literals)
                    {
                        // find the best member, preferring case-sensitive
                        ColumnInfo exact = null, fallback = null;
                        string huntName = literal.Member;
                        foreach (var column in columns)
                        {
                            string thisName = column.MemberName;
                            if (string.Equals(thisName, huntName, StringComparison.OrdinalIgnoreCase))
                            {
                                fallback = column;
                                if (string.Equals(thisName, huntName, StringComparison.Ordinal))
                                {
                                    exact = fallback;
                                    break;
                                }
                            }
                        }
                        var prop = exact ?? fallback;
                        if (prop != null)
                        {
                            il.Emit(OpCodes.Ldstr, literal.Token);
                            il.Emit(OpCodes.Ldloc_0); // command, sql, typed parameter
                            prop.EmitGenerateGet(il); // command, sql, typed value
                            Type propType = prop.ValueType;
                            var typeCode = Reflect.Dapper.GetTypeCode(propType);
                            switch (typeCode)
                            {
                                case TypeCode.Boolean:
                                    Label ifTrue = il.DefineLabel(), allDone = il.DefineLabel();
                                    il.Emit(OpCodes.Brtrue_S, ifTrue);
                                    il.Emit(OpCodes.Ldstr, "0");
                                    il.Emit(OpCodes.Br_S, allDone);
                                    il.MarkLabel(ifTrue);
                                    il.Emit(OpCodes.Ldstr, "1");
                                    il.MarkLabel(allDone);
                                    break;
                                case TypeCode.Byte:
                                case TypeCode.SByte:
                                case TypeCode.UInt16:
                                case TypeCode.Int16:
                                case TypeCode.UInt32:
                                case TypeCode.Int32:
                                case TypeCode.UInt64:
                                case TypeCode.Int64:
                                case TypeCode.Single:
                                case TypeCode.Double:
                                case TypeCode.Decimal:
                                    // need to stloc, ldloca, call
                                    // re-use existing locals (both the last known, and via a dictionary)
                                    var convert = Reflect.Dapper.GetToString(typeCode);
                                    if (local == null || local.LocalType != propType)
                                    {
                                        if (locals == null)
                                        {
                                            locals = new Dictionary<Type, LocalBuilder>();
                                            local = null;
                                        }
                                        else
                                        {
                                            if (!locals.TryGetValue(propType, out local)) local = null;
                                        }
                                        if (local == null)
                                        {
                                            local = il.DeclareLocal(propType);
                                            locals.Add(propType, local);
                                        }
                                    }
                                    il.Emit(OpCodes.Stloc, local); // command, sql
                                    il.Emit(OpCodes.Ldloca, local); // command, sql, ref-to-value
                                    il.EmitCall(OpCodes.Call, Reflect.CultureInfo_InvariantCulture_Get, null); // command, sql, ref-to-value, culture
                                    il.EmitCall(OpCodes.Call, convert, null); // command, sql, string value
                                    break;
                                default:
                                    if (propType.IsValueType) il.Emit(OpCodes.Box, propType); // command, sql, object value
                                    il.EmitCall(OpCodes.Call, Reflect.SqlMapper_Format, null); // command, sql, string value
                                    break;

                            }
                            il.EmitCall(OpCodes.Callvirt, Reflect.String_Replace, null);
                        }
                    }
                    il.EmitCall(OpCodes.Callvirt, Reflect.IDbCommand_CommandText_Set, null); // empty
                }
            }

            //仿Dapper.SqlMapper.FilterParameters
            private static IEnumerable<ColumnInfo> FilterParameters(IEnumerable<ColumnInfo> parameters, string sql)
            {
                return parameters.Where(p => Regex.IsMatch(sql, @"[?@:]" + p.MemberName + "([^a-z0-9_]+|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant));
            }
        }
    }
}

#if false
            private static Action<IDbCommand, object> CreateParamInfoGenerator(Type type, CommandType commandType, string sql, bool checkForDuplicates)
            {
                var table = GetTableInfo(type);

                var dm = new DynamicMethod("WrapParamInfo" + Guid.NewGuid().ToString(), null, new[] { typeof(IDbCommand), typeof(object) }, type, true);
                var il = dm.GetILGenerator();
                bool haveInt32Arg1 = false;  //是否已經定義了區域變數 int loc_1

                il.Emit(OpCodes.Ldarg_1); // stack is now [untyped-param]
                if (table.IsStruct)
                {
                    il.DeclareLocal(type.MakePointerType());    //例如int*
                    il.Emit(OpCodes.Unbox, type); // stack is now [typed-param]
                }
                else
                {
                    il.DeclareLocal(type); // 0
                    il.Emit(OpCodes.Castclass, type); // stack is now [typed-param]
                }
                il.Emit(OpCodes.Stloc_0);// stack is now empty

                il.Emit(OpCodes.Ldarg_0); // stack is now [command]
                il.EmitCall(OpCodes.Callvirt, Reflect.IDbCommand_Parameters_Get, null); // stack is now [parameters]

                //取得要處理的欄位資訊
                var columns = table.Columns;
                //過濾不必要的欄位
                var actualColumns = (commandType == CommandType.Text && !Reflect.Dapper.smellsLikeOleDb.IsMatch(sql)) ? FilterParameters(columns, sql) : columns;

                //循環欄位
                foreach (var column in actualColumns)
                {
                    var memberName = column.MemberName;
                    var memberType = column.ValueType;

                    //如果有實作Dapper.SqlMapper.ICustomQueryParameter的話就按照Dapper的邏輯處理
                    if (typeof(ICustomQueryParameter).IsAssignableFrom(memberType))
                    {
                        il.Emit(OpCodes.Ldloc_0); // stack is now [parameters] [typed-param]
                        column.EmitGenerateGet(il); // stack is [parameters] [custom]
                        il.Emit(OpCodes.Ldarg_0); // stack is now [parameters] [custom] [command]
                        il.Emit(OpCodes.Ldstr, memberName); // stack is now [parameters] [custom] [command] [name]
                        il.EmitCall(OpCodes.Callvirt, memberType.GetMethod(nameof(ICustomQueryParameter.AddParameter)), null); // stack is now [parameters]
                        continue;
                    }

                    var dbTypeInfos = new DbTypeInfoStorage(memberName);

                    /*
                    Type cacheMemberType = null;
                    ITypeHandler cacheHandler = null;
                    DbType cacheDbType = default(DbType);
                    Func<DbType> getDbType = () =>
                    {
                        if (cacheMemberType == memberType) return cacheDbType;
#pragma warning disable 618
                        return cacheDbType = LookupDbType(cacheMemberType = memberType, memberName, true, out cacheHandler);
#pragma warning restore 618
                    };
                    cacheDbType = getDbType();
                    var handler = cacheHandler;
                    */

                    //如果還未宣告區域變數loc_1的話, 宣告區域變數
                    Action declareIntArg1 = () =>
                {
                    if (haveInt32Arg1) return;
                    il.DeclareLocal(typeof(int));
                    haveInt32Arg1 = true;
                };

                //如果是集合的話, 就處理"in", 這邊按原本Dapper邏輯處理
                if (dbTypeInfos.Get(memberType).DbType == Reflect.Dapper.EnumerableMultiParameter)
                {
                    // this actually represents special handling for list types;
                    il.Emit(OpCodes.Ldarg_0); // stack is now [parameters] [command]
                    il.Emit(OpCodes.Ldstr, memberName); // stack is now [parameters] [command] [name]
                    il.Emit(OpCodes.Ldloc_0); // stack is now [parameters] [command] [name] [typed-param]
                    column.EmitGenerateGet(il);  // stack is [parameters] [command] [name] [typed-value]
                    if (memberType.IsValueType) il.Emit(OpCodes.Box, memberType); // stack is [parameters] [command] [name] [boxed-value]
                    il.EmitCall(OpCodes.Call, Reflect.SqlMapper_PackListParameters, null); // stack is [parameters]
                    continue;
                }
                il.Emit(OpCodes.Dup); // stack is now [parameters] [parameters]

                il.Emit(OpCodes.Ldarg_0); // stack is now [parameters] [parameters] [command]

                //判斷是否要檢查有無重複的欄位定義, 這邊也是比照Dapper的處理方式
                if (checkForDuplicates)
                {
                    // need to be a little careful about adding; use a utility method
                    il.Emit(OpCodes.Ldstr, memberName); // stack is now [parameters] [parameters] [command] [name]
                    il.EmitCall(OpCodes.Call, Reflect.SqlMapper_FindOrAddParameter, null); // stack is [parameters] [parameter]
                }
                else
                {
                    // no risk of duplicates; just blindly add
                    il.EmitCall(OpCodes.Callvirt, Reflect.IDbCommand_CreateParameter, null);// stack is now [parameters] [parameters] [parameter]

                    il.Emit(OpCodes.Dup);// stack is now [parameters] [parameters] [parameter] [parameter]
                    il.Emit(OpCodes.Ldstr, memberName); // stack is now [parameters] [parameters] [parameter] [parameter] [name]
                    il.EmitCall(OpCodes.Callvirt, Reflect.IDataParameter_ParameterName_Set, null);// stack is now [parameters] [parameters] [parameter]
                }
                il.Emit(OpCodes.Dup);// stack is now [parameters] [[parameters]] [parameter] [parameter]
                Reflect.Dapper.EmitInt32(il, (int)ParameterDirection.Input);// stack is now [parameters] [[parameters]] [parameter] [parameter] [dir]
                il.EmitCall(OpCodes.Callvirt, Reflect.IDataParameter_Direction_Set, null);// stack is now [parameters] [[parameters]] [parameter]

                il.Emit(OpCodes.Dup);// stack is now [parameters] [[parameters]] [parameter] [parameter]
                il.Emit(OpCodes.Ldloc_0); // stack is now [parameters] [[parameters]] [parameter] [parameter] [typed-param]
                column.EmitGenerateGet(il); // stack is [parameters] [[parameters]] [parameter] [parameter] [typed-value]

                /*
                 * 注意1. 可以null的型別才會有NullMapping
                 * 注意2. 如果有handler的話, DbType會是DbType.Object
                 * 注意3. DbType是字串是指DbType.String或DbType.AnsiString
                 * 注意4. 可為null的話 表示為Nullable或物件類型
                 * 
                 * 
                 * if (memberType無handler 且有EnumValue)  //type換成EnumValue            01.    value = getEnumValue(value);
                 * if (可為null) {
                 *    if (Nullable<>) {                                                   02.    if ( value.HasValue )
                 *    } else {                               //這時type一定是物件類型     02.    if ( value != null )
                 *    }                                      
                 *                                                                        03.        goto notNullHandle:
                 *    if (有NullMapping) {                   //type換成NullMapping        04.    value = NullMapping;
                 *    } else {                                                            04.    value = DBNull.Value;
                 *       if (DbType是字串)                                                05.    size = 0;
                 *                                                                        06.    goto nullHandleDone:
                 *    }
                 *                                                                        07. notNullHandle:
                 * }
                 * if (是值類型) {
                 *    if (Nullable)                          //type由Nullable<T>換成T     08.    value = value.GetValueOrDefault()
                 *    if (是Enum 且 無handler) {             //type換成(Enum基礎型別)     
                 *                                                                        09.    value = (object)value;
                 * } else {
                 *    if (是System.Data.Linq.Binary) {       //type換成bye[]              10.    value = value.ToArray();
                 *    } else if (DbType是字串) {                                          11.    size = value.length > 4000 ? -1 : 4000;
                 *    }
                 * }
                 * if (前面有goto nullHandleDone)                                         12. nullHandleDone:
                 * if (有handler) {                                                       13.    SqlMapper.TypeHandlerCache<T>.SetValue(parameter, value);
                 * } else {                                                               
                 *    if (DbType非Time)                                                   14.    paramter.DbType = dbType;
                 *                                                                        15.    paramter.Value = value;
                 *    if (DbType是字串)                                                   16.    if (size != 0) paramter.Size = size;
                 * }
                 *                                                                        17.    paramters.Add(paramter);
                 */

                //01. memberType沒有handler時判斷 有定義EnumValue的話, 取得對應值 
                if (dbTypeInfos.Get(memberType).Handler != null)
                { 
                    Type enumValueType;
                    var enumValueGetter = EnumValueHelper.GetValueGetterMethod(memberType, out enumValueType);
                    if (enumValueGetter != null)
                    {
                        il.EmitCall(OpCodes.Call, enumValueGetter, null);     // stack is [parameters] [[parameters]] [parameter] [parameter] [typed-value]
                        memberType = enumValueType;
                    }
                }


                Label? nullHandleDone = null;
                Type underlyingType = null;
                //如果可能為null的話, 才做null的判斷處理
                if (!memberType.IsValueType || (underlyingType = Nullable.GetUnderlyingType(memberType)) != null)
                {
                    var notNullHandle = il.DefineLabel();
                    //02. 判斷非null的話跳notNullHandle
                    il.Emit(OpCodes.Dup);    // stack is [parameters] [[parameters]] [parameter] [parameter] [typed-value] [typed-value]
                    if (underlyingType == null) il.EmitCall(OpCodes.Call, memberType.GetProperty(nameof(Nullable<int>.HasValue)).GetGetMethod(), null); // stack is [parameters] [[parameters]] [parameter] [parameter] [typed-value] [bool]
                    //03. goto notNullHandle
                    il.Emit(OpCodes.Brtrue_S, notNullHandle); // stack is [parameters] [[parameters]] [parameter] [parameter] [typed-value]

                    if (column.NullMapping != null)
                    {
                        //04. value = NullMapping;
                        il.Emit(OpCodes.Pop);       // stack is [parameters] [[parameters]] [parameter] [parameter]
                        il.EmitConstant(column.NullMapping);     // stack is [parameters] [[parameters]] [parameter] [parameter] [NullMapping]
                        memberType = column.NullMapping.GetType();
                    }
                    else
                    {
                        //05. 如果DbType是字串, 設定 loc_1 = 0
                        if (dbTypeInfos.Get(memberType).IsString())
                        {
                            declareIntArg1();
                            Reflect.Dapper.EmitInt32(il, 0);
                            il.Emit(OpCodes.Stloc_1);
                        }
                        //06. 跳到nullHandleDone
                        nullHandleDone = il.DefineLabel();
                        il.Emit(OpCodes.Br_S, nullHandleDone.Value);
                    }
                    //07. 標記notNullHandle
                    il.MarkLabel(notNullHandle);
                }

                if (memberType.IsValueType)
                {
                    //08. 如果是Nullable<T>的話, 取出T來
                    if ((underlyingType = Nullable.GetUnderlyingType(memberType)) != null)
                    {
                        il.EmitCall(OpCodes.Call, memberType.GetMethod(nameof(Nullable<int>.GetValueOrDefault)), null); // stack is [parameters] [[parameters]] [parameter] [parameter] [typed-value]
                        memberType = underlyingType;
                    }
                    if (memberType.IsEnum && dbTypeInfos.Get(memberType).Handler == null)
                    {
                        switch (Type.GetTypeCode(Enum.GetUnderlyingType(memberType)))
                        {
                            case TypeCode.Byte: memberType = typeof(byte); break;
                            case TypeCode.SByte: memberType = typeof(sbyte); break;
                            case TypeCode.Int16: memberType = typeof(short); break;
                            case TypeCode.Int32: memberType = typeof(int); break;
                            case TypeCode.Int64: memberType = typeof(long); break;
                            case TypeCode.UInt16: memberType = typeof(ushort); break;
                            case TypeCode.UInt32: memberType = typeof(uint); break;
                            case TypeCode.UInt64: memberType = typeof(ulong); break;
                        }
                    }
                    //09. value = (object)value
                    il.Emit(OpCodes.Box, memberType); // stack is [parameters] [[parameters]] [parameter] [parameter] [boxed-value]
                }
                else
                {
                    //判斷是是System.Data.Linq.Binary的話, 呼叫value.ToArray()取得byte[]
                    if (memberType.FullName == Reflect.Dapper.LinqBinary)  //System.Data.Linq.Binary
                    {
                        //10. value = value.ToArray();
                        il.EmitCall(OpCodes.Callvirt, memberType.GetMethod("ToArray", BindingFlags.Public | BindingFlags.Instance), null); // stack is [parameters] [[parameters]] [parameter] [parameter] [byte[]]
                        memberType = typeof(byte[]);
                    } else if (dbTypeInfos.Get(memberType).IsString()) {
                        //11. loc_1 = value.length > 4000 ? -1 : 4000;
                        il.Emit(OpCodes.Dup);    // stack is [parameters] [[parameters]] [parameter] [parameter] [typed-value] [typed-value]
                        il.EmitCall(OpCodes.Callvirt, Reflect.String_Length_Get, null); // // stack is [parameters] [[parameters]] [parameter] [parameter] [typed-value] [string length]
                        Reflect.Dapper.EmitInt32(il, DbString.DefaultLength); // stack is [parameters] [[parameters]] [parameter] [parameter] [typed-value] [string length] [4000]
                        il.Emit(OpCodes.Cgt); // stack is [parameters] [[parameters]] [parameter] [parameter] [typed-value] [0 or 1]
                        Label isLong = il.DefineLabel(), lenDone = il.DefineLabel();
                        il.Emit(OpCodes.Brtrue_S, isLong); // stack is [parameters] [[parameters]] [parameter] [parameter] [typed-value]
                        Reflect.Dapper.EmitInt32(il, DbString.DefaultLength); // stack is [parameters] [[parameters]] [parameter] [parameter] [typed-value] [4000]
                        il.Emit(OpCodes.Br_S, lenDone);
                        il.MarkLabel(isLong);
                        Reflect.Dapper.EmitInt32(il, -1); // stack is [parameters] [[parameters]] [parameter] [parameter] [typed-value] [-1]
                        il.MarkLabel(lenDone);
                        declareIntArg1();
                        il.Emit(OpCodes.Stloc_1); // stack is [parameters] [[parameters]] [parameter] [parameter] [typed-value]
                    }
                }

                //12. 有nullHandleDone的話, 標記nullHandleDone
                if (nullHandleDone.HasValue) il.MarkLabel(nullHandleDone.Value);

                var dbTypeInfo = dbTypeInfos.Get(memberType);
                if (dbTypeInfo.Handler != null)
                {
                    //13. 呼叫SqlMapper.TypeHandlerCache<T>.SetValue(parameter, value);
#pragma warning disable 618
                    il.Emit(OpCodes.Call, typeof(TypeHandlerCache<>).MakeGenericType(memberType).GetMethod(nameof(TypeHandlerCache<int>.SetValue))); // stack is now [parameters] [[parameters]] [parameter]
#pragma warning restore 618
                }
                else
                {
                    if (dbTypeInfo.DbType != DbType.Time)
                    {
                        il.Emit(OpCodes.Dup); // stack is now [parameters] [[parameters]] [parameter] [parameter]
                        if (dbTypeInfo.DbType == DbType.Object && memberType == typeof(object)) // includes dynamic
                        {
                            // look it up from the param value
                            il.Emit(OpCodes.Ldloc_0); // stack is now [parameters] [[parameters]] [parameter] [parameter] [typed-param]
                            column.EmitGenerateGet(il);  // stack is [parameters] [[parameters]] [parameter] [parameter] [object-value]
                            il.Emit(OpCodes.Call, Reflect.SqlMapper_GetDbType); // stack is now [parameters] [[parameters]] [parameter] [parameter] [db-type]
                        }
                        else
                        {
                            Reflect.Dapper.EmitInt32(il, (int)dbType);// stack is now [parameters] [[parameters]] [parameter] [parameter] [db-type]
                        }
                        il.EmitCall(OpCodes.Callvirt, Reflect.IDataParameter_DbType_Set, null);// stack is now [parameters] [[parameters]] [parameter]
                    }



                    //14. paramter.Value = value;
                    il.EmitCall(OpCodes.Callvirt, Reflect.IDataParameter_Value_Set, null);// stack is now [parameters] [[parameters]] [parameter]
                    //15. DbType非Time的話設定paramter.DbType = dbType;
                    if (dbTypeInfo.DbType != DbType.Time)
                    {
                        il.Emit(OpCodes.Dup); // stack is now [parameters] [[parameters]] [parameter] [parameter]
                        if (dbTypeInfo.DbType == DbType.Object && memberType == typeof(object)) // includes dynamic
                        {
                            // look it up from the param value
                            il.Emit(OpCodes.Ldloc_0); // stack is now [parameters] [[parameters]] [parameter] [parameter] [typed-param]
                            column.EmitGenerateGet(il);  // stack is [parameters] [[parameters]] [parameter] [parameter] [object-value]
                            il.Emit(OpCodes.Call, Reflect.SqlMapper_GetDbType); // stack is now [parameters] [[parameters]] [parameter] [parameter] [db-type]
                        }
                        else
                        {
                            Reflect.Dapper.EmitInt32(il, (int)dbType);// stack is now [parameters] [[parameters]] [parameter] [parameter] [db-type]
                        }
                        il.EmitCall(OpCodes.Callvirt, Reflect.IDataParameter_DbType_Set, null);// stack is now [parameters] [[parameters]] [parameter]
                    }
                }







                Label? nullMappingDone = null;
                if (column.NullMapping != null) //如果有設定NullMapping, 這邊只有是nullable<>和物件類型才有可能會有NullMapping
                {
                    nullMappingDone = il.DefineLabel();
                    var nullMappingNone = il.DefineLabel();
                    //01. 判斷model成員值是否為null, 不是null的話跳到nullMappingNone
                    il.Emit(OpCodes.Dup);   // stack is [parameters] [[parameters]] [parameter] [parameter] [typed-value] [typed-value]
                    if (isStructMember) il.Emit(OpCodes.Box, memberType);    // stack is [parameters] [[parameters]] [parameter] [parameter] [typed-value] [boxed-value]
                    il.Emit(OpCodes.Brtrue_S, nullMappingNone);     // stack is [parameters] [[parameters]] [parameter] [parameter] [typed-value]
#region 處理null對應
                    //02. paramter.Value = NullMapping, 因為paramter.Value 是物件型態, 所以如果NullMapping是值類型的話先box
                    il.Emit(OpCodes.Pop);       // stack is [parameters] [[parameters]] [parameter] [parameter]
                    il.EmitConstant(column.NullMapping);     // stack is [parameters] [[parameters]] [parameter] [parameter] [typed null value]
                    var nullMappingType = column.NullMapping.GetType();
                    if (nullMappingType.IsValueType) il.Emit(OpCodes.Box, nullMappingType);  // stack is [parameters] [[parameters]] [parameter] [parameter] [boxed null value]
                    il.EmitCall(OpCodes.Callvirt, Reflect.IDataParameter_Value_Set, null);// stack is now [parameters] [[parameters]] [parameter]

#pragma warning disable 618
                    var nullMappingDbType = LookupDbType(nullMappingType, memberName, true, out handler);
#pragma warning restore 618
                    //03. 如果NullMapping的DBType != Time, 要設定paramter.DbType;
                    if (nullMappingDbType != DbType.Time && handler == null)
                    {
                        il.Emit(OpCodes.Dup);// stack is now [parameters] [[parameters]] [parameter] [parameter]
                        Reflect.Dapper.EmitInt32(il, (int)nullMappingDbType);// stack is now [parameters] [[parameters]] [parameter] [parameter] [db-type]
                        il.EmitCall(OpCodes.Callvirt, Reflect.IDataParameter_DbType_Set, null);// stack is now [parameters] [[parameters]] [parameter]
                    }
                    //04. paramter.Size = nullValue.length > 4000 ? -1 : 4000;
                    if (nullMappingDbType == DbType.String || nullMappingDbType == DbType.AnsiString)
                    {
                        il.Emit(OpCodes.Dup);// stack is now [parameters] [[parameters]] [parameter] [parameter]
                        Reflect.Dapper.EmitInt32(il, ((string)column.NullMapping).Length > DbString.DefaultLength ? -1 : DbString.DefaultLength);    // stack is now [parameters] [[parameters]] [parameter] [parameter] [string-length]
                        il.EmitCall(OpCodes.Callvirt, Reflect.IDbDataParameter_Size_Set, null); // stack is now [parameters] [[parameters]] [parameter]
                    }
#endregion
                    //05. 跳nullMappingDone
                    il.Emit(OpCodes.Br_S, nullMappingDone.Value);
                    //06. 標記標籤nullMappingNone
                    il.MarkLabel(nullMappingNone);
                }

                //07. 處理enum mapping。 如果沒有對應的話, 則要box, 以便後續處理
                //Type enumMappingType;
                //var enumMappingGetter = EnumMappingInfo.GetEnumToDbMethod(memberType, out enumMappingType);
                if (enumValueGetter == null)
                {
                    if (isStructMember)
                    {
                        var nullType = Nullable.GetUnderlyingType(memberType);
                        bool callSanitize = false;
                        if ((nullType ?? memberType).IsEnum)
                        {
                            if (nullType != null)
                            {
                                callSanitize = true;
                            }
                            else
                            {
                                switch (Type.GetTypeCode(Enum.GetUnderlyingType(memberType)))
                                {
                                    case TypeCode.Byte: memberType = typeof(byte); break;
                                    case TypeCode.SByte: memberType = typeof(sbyte); break;
                                    case TypeCode.Int16: memberType = typeof(short); break;
                                    case TypeCode.Int32: memberType = typeof(int); break;
                                    case TypeCode.Int64: memberType = typeof(long); break;
                                    case TypeCode.UInt16: memberType = typeof(ushort); break;
                                    case TypeCode.UInt32: memberType = typeof(uint); break;
                                    case TypeCode.UInt64: memberType = typeof(ulong); break;
                                }
                            }
                        }
                        il.Emit(OpCodes.Box, memberType);  // stack is [parameters] [[parameters]] [parameter] [parameter] [boxed value]
                        if(callSanitize)
                        {

                        }
                    }
                }
                else
                {
                    il.EmitCall(OpCodes.Call, enumValueGetter, null);     // stack is [parameters] [[parameters]] [parameter] [parameter] [boxed-enum-value]
                }

                var valueType = enumValueType ?? memberType;
#pragma warning disable 618
                dbType = LookupDbType(valueType, memberName, true, out handler);
#pragma warning restore 618
                var checkForNull = false; //判斷是否要檢查null
                Type nullableUnderlyingType = null;  //Nullable<>的基礎型別
                var needSetStringSize = false; //判斷若為字串的話後續要設定paramter.Size
                if (valueType.IsValueType)
                {
                    nullableUnderlyingType = Nullable.GetUnderlyingType(valueType);
                    checkForNull = nullableUnderlyingType != null;
                }
                else
                {
                    checkForNull = true;
                    needSetStringSize = dbType == DbType.String || dbType == DbType.AnsiString;
                }

                //如果value有可能為null的話才要檢查null
                if (checkForNull)
                {
                    //如果還未宣告區域變數loc_1的話, 宣告區域變數
                    if (needSetStringSize && !haveInt32Arg1)
                    {
                        il.DeclareLocal(typeof(int));
                        haveInt32Arg1 = true;
                    }
                    Label nullHandleDone = il.DefineLabel();
                    Label? notNullHandle = needSetStringSize ? il.DefineLabel() : (Label?)null;
                    //08. 判斷是否為null, 不是null的話跳到notNullHandle
                    il.Emit(OpCodes.Dup); // stack is [parameters] [[parameters]] [parameter] [parameter] [boxed value] [boxed value]
                    il.Emit(OpCodes.Brtrue_S, notNullHandle ?? nullHandleDone);   // stack is [parameters] [[parameters]] [parameter] [parameter] [boxed value]
                    //09. 把stack最後一個值由value換成DBNull.Value
                    il.Emit(OpCodes.Pop);       // stack is [parameters] [[parameters]] [parameter] [parameter]
                    il.Emit(OpCodes.Ldsfld, Reflect.DBNull_Value); // stack is [parameters] [[parameters]] [parameter] [parameter] [DBNull]
                    //10. 如果是字串的話, 設定loc_1 = 0, 以便後續對paramter.Size設值
                    if (needSetStringSize)
                    {
                        Reflect.Dapper.EmitInt32(il, 0); 
                        il.Emit(OpCodes.Stloc_1);
                    }
                    il.Emit(OpCodes.Br_S, nullHandleDone);
                    //12. 標記notNullHandle
                    if(notNullHandle.HasValue) il.MarkLabel(notNullHandle.Value);
                    //13. 如果是字串的話, loc_1 = value.length > 4000 ? -1 : 4000
                    if (needSetStringSize)
                    {
                        il.Emit(OpCodes.Dup);    // stack is [parameters] [[parameters]] [parameter] [parameter] [boxed value] [boxed value]
                        il.EmitCall(OpCodes.Callvirt, Reflect.String_Length_Get, null); // // stack is [parameters] [[parameters]] [parameter] [parameter] [boxed value] [string length]
                        Reflect.Dapper.EmitInt32(il, DbString.DefaultLength); // stack is [parameters] [[parameters]] [parameter] [parameter] [boxed value] [string length] [4000]
                        il.Emit(OpCodes.Cgt); // stack is [parameters] [[parameters]] [parameter] [parameter] [boxed value] [0 or 1]
                        Label isLong = il.DefineLabel(), lenDone = il.DefineLabel();
                        il.Emit(OpCodes.Brtrue_S, isLong); // stack is [parameters] [[parameters]] [parameter] [parameter] [boxed value]
                        Reflect.Dapper.EmitInt32(il, DbString.DefaultLength); // stack is [parameters] [[parameters]] [parameter] [parameter] [boxed value] [4000]
                        il.Emit(OpCodes.Br_S, lenDone);
                        il.MarkLabel(isLong);
                        Reflect.Dapper.EmitInt32(il, -1); // stack is [parameters] [[parameters]] [parameter] [parameter] [boxed value] [-1]
                        il.MarkLabel(lenDone);
                        il.Emit(OpCodes.Stloc_1); // stack is [parameters] [[parameters]] [parameter] [parameter] [boxed value]
                    }
                    //14. 標記nullHandleDone
                    il.MarkLabel(nullHandleDone);
                }
                //15. 對paramter.Value設值, 可能是透過handler或是直接設值, 判斷如果是System.Data.Linq.Binary的話, 呼叫ToArray取得byte[]
                if (valueType.FullName == Reflect.Dapper.LinqBinary)  //System.Data.Linq.Binary
                {
                    il.EmitCall(OpCodes.Callvirt, valueType.GetMethod("ToArray", BindingFlags.Public | BindingFlags.Instance), null);
                }
                if (handler != null)
                {
#pragma warning disable 618
                    il.Emit(OpCodes.Call, typeof(TypeHandlerCache<>).MakeGenericType(valueType).GetMethod(nameof(TypeHandlerCache<int>.SetValue))); // stack is now [parameters] [[parameters]] [parameter]
#pragma warning restore 618
                }
                else
                {
                    il.EmitCall(OpCodes.Callvirt, Reflect.IDataParameter_Value_Set, null);// stack is now [parameters] [[parameters]] [parameter]
                    //16. 如果非Time的話, 設定paramter.DbType, 如果是dynamic的話需額外呼叫GetDbType來取得dbType
                    if (dbType != DbType.Time)
                    {
                        il.Emit(OpCodes.Dup); // stack is now [parameters] [[parameters]] [parameter] [parameter]
                        if (dbType == DbType.Object && valueType == typeof(object)) // includes dynamic
                        {
                            // look it up from the param value
                            il.Emit(OpCodes.Ldloc_0); // stack is now [parameters] [[parameters]] [parameter] [parameter] [typed-param]
                            column.EmitGenerateGet(il);  // stack is [parameters] [[parameters]] [parameter] [parameter] [object-value]
                            il.Emit(OpCodes.Call, Reflect.SqlMapper_GetDbType); // stack is now [parameters] [[parameters]] [parameter] [parameter] [db-type]
                        }
                        else
                        {
                            Reflect.Dapper.EmitInt32(il, (int)dbType);// stack is now [parameters] [[parameters]] [parameter] [parameter] [db-type]
                        }
                        il.EmitCall(OpCodes.Callvirt, Reflect.IDataParameter_DbType_Set, null);// stack is now [parameters] [[parameters]] [parameter]
                    }
                }

                //17. 如果是字串的話, if (loc_1 != 0) paramter.Size = loc_1
                if (needSetStringSize)
                {
                    var endOfSize = il.DefineLabel();
                    // don't set if 0
                    il.Emit(OpCodes.Ldloc_1); // [parameters] [[parameters]] [parameter] [size]
                    il.Emit(OpCodes.Brfalse_S, endOfSize); // [parameters] [[parameters]] [parameter]

                    il.Emit(OpCodes.Dup);// stack is now [parameters] [[parameters]] [parameter] [parameter]
                    il.Emit(OpCodes.Ldloc_1); // stack is now [parameters] [[parameters]] [parameter] [parameter] [size]
                    il.EmitCall(OpCodes.Callvirt, Reflect.IDbDataParameter_Size_Set, null); // stack is now [parameters] [[parameters]] [parameter]

                    il.MarkLabel(endOfSize);
                }
                //18. 標記nullMappingDone
                if (nullMappingDone.HasValue) il.MarkLabel(nullMappingDone.Value);

                //19. checkForDuplicates的話, 前面已經有FindOrAdd過了, 直接釋放. 非checkForDuplicates的話, paramters.Add(paramter)
                if (checkForDuplicates)
                {
                    // stack is now [parameters] [parameter]
                    il.Emit(OpCodes.Pop); // don't need parameter any more
                }
                else
                {
                    // stack is now [parameters] [parameters] [parameter]
                    // blindly add
                    il.EmitCall(OpCodes.Callvirt, Reflect.IList_Add, null); // stack is now [parameters]
                    il.Emit(OpCodes.Pop); // IList.Add returns the new index (int); we don't care
                }
            }

            // stack is currently [parameters]
            il.Emit(OpCodes.Pop); // stack is now empty


            //仿Dapper的邏輯處理 {=aaaa} 這種東西, 簡單的說就是sql字串替換
            var literals = Reflect.Dapper.GetLiteralTokens(sql);
            if (literals.Count != 0)
            {
                il.Emit(OpCodes.Ldarg_0); // command
                il.Emit(OpCodes.Ldarg_0); // command, command
                il.EmitCall(OpCodes.Callvirt, Reflect.IDbCommand_CommandText_Get, null); // command, sql
                Dictionary<Type, LocalBuilder> locals = null;
                LocalBuilder local = null;
                foreach (var literal in literals)
                {
                    // find the best member, preferring case-sensitive
                    ColumnInfo exact = null, fallback = null;
                    string huntName = literal.Member;
                    foreach (var column in columns)
                    {
                        string thisName = column.MemberName;
                        if (string.Equals(thisName, huntName, StringComparison.OrdinalIgnoreCase))
                        {
                            fallback = column;
                            if (string.Equals(thisName, huntName, StringComparison.Ordinal))
                            {
                                exact = fallback;
                                break;
                            }
                        }
                    }
                    var prop = exact ?? fallback;
                    if (prop != null)
                    {
                        il.Emit(OpCodes.Ldstr, literal.Token);
                        il.Emit(OpCodes.Ldloc_0); // command, sql, typed parameter
                        prop.EmitGenerateGet(il); // command, sql, typed value
                        Type propType = prop.ValueType;
                        var typeCode = Reflect.Dapper.GetTypeCode(propType);
                        switch (typeCode)
                        {
                            case TypeCode.Boolean:
                                Label ifTrue = il.DefineLabel(), allDone = il.DefineLabel();
                                il.Emit(OpCodes.Brtrue_S, ifTrue);
                                il.Emit(OpCodes.Ldstr, "0");
                                il.Emit(OpCodes.Br_S, allDone);
                                il.MarkLabel(ifTrue);
                                il.Emit(OpCodes.Ldstr, "1");
                                il.MarkLabel(allDone);
                                break;
                            case TypeCode.Byte:
                            case TypeCode.SByte:
                            case TypeCode.UInt16:
                            case TypeCode.Int16:
                            case TypeCode.UInt32:
                            case TypeCode.Int32:
                            case TypeCode.UInt64:
                            case TypeCode.Int64:
                            case TypeCode.Single:
                            case TypeCode.Double:
                            case TypeCode.Decimal:
                                // need to stloc, ldloca, call
                                // re-use existing locals (both the last known, and via a dictionary)
                                var convert = Reflect.Dapper.GetToString(typeCode);
                                if (local == null || local.LocalType != propType)
                                {
                                    if (locals == null)
                                    {
                                        locals = new Dictionary<Type, LocalBuilder>();
                                        local = null;
                                    }
                                    else
                                    {
                                        if (!locals.TryGetValue(propType, out local)) local = null;
                                    }
                                    if (local == null)
                                    {
                                        local = il.DeclareLocal(propType);
                                        locals.Add(propType, local);
                                    }
                                }
                                il.Emit(OpCodes.Stloc, local); // command, sql
                                il.Emit(OpCodes.Ldloca, local); // command, sql, ref-to-value
                                il.EmitCall(OpCodes.Call, Reflect.CultureInfo_InvariantCulture_Get, null); // command, sql, ref-to-value, culture
                                il.EmitCall(OpCodes.Call, convert, null); // command, sql, string value
                                break;
                            default:
                                if (propType.IsValueType) il.Emit(OpCodes.Box, propType); // command, sql, object value
                                il.EmitCall(OpCodes.Call, Reflect.SqlMapper_Format, null); // command, sql, string value
                                break;

                        }
                        il.EmitCall(OpCodes.Callvirt, Reflect.String_Replace, null);
                    }
                }
                il.EmitCall(OpCodes.Callvirt, Reflect.IDbCommand_CommandText_Set, null); // empty
            }
            il.Emit(OpCodes.Ret);

#if saveParamAssembly
            var t = builder.CreateType();
            assemblyBuilder.Save(assemblyName.Name + ".dll");
            return (Action<IDbCommand, object>)Delegate.CreateDelegate(typeof(Action<IDbCommand, object>), t.GetMethod(dm.Name));
#else
			return (Action<IDbCommand, object>)dm.CreateDelegate(typeof(Action<IDbCommand, object>));
#endif
        }

#endif
