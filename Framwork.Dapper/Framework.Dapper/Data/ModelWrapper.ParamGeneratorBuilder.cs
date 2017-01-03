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
        internal sealed class ParamGeneratorBuilder
        {
            private Type modelType;
            private CommandType commandType;
            private string sql;
            private bool checkForDuplicates;
            private DynamicMethod dm;
            private ILGenerator il;
            private bool _hasDefineSizeVariable = false;  //是否已經定義區域變數size
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
            private void setSizeVariable()
            {
                if (!_hasDefineSizeVariable) il.DeclareLocal(typeof(int));
                il.Emit(OpCodes.Stloc_1);
            }
            private void getSizeVariable()
            {
                il.Emit(OpCodes.Ldloc_1);
            }


#if saveParamAssembly
            private Func<Delegate> GetDelegate;
#endif

            internal ParamGeneratorBuilder(Type modelType, CommandType commandType, string sql, bool checkForDuplicates)
            {
                this.modelType = modelType;
                this.commandType = commandType;
                this.sql = sql;
                this.checkForDuplicates = checkForDuplicates;
#if saveParamAssembly
                var assemblyName = new AssemblyName(string.Format("WrapParamInfo", modelType.Name.StartsWith("<") ? Guid.NewGuid().ToString("N") : modelType.Name));
			    var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
			    var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name, assemblyName.Name + ".dll");
			    var builder = moduleBuilder.DefineType(modelType + "ParamGenerator", TypeAttributes.Public);
			    var dm = builder.DefineMethod("DynamicCreate", MethodAttributes.Public | MethodAttributes.Static, null, new[] { typeof(IDbCommand), typeof(object) });

                GetDelegate = () =>
                {
                    var t = builder.CreateType();
                    assemblyBuilder.Save(assemblyName.Name + ".dll");
                    return Delegate.CreateDelegate(typeof(Action<IDbCommand, object>), t.GetMethod(dm.Name));
                };

#else
                dm = new DynamicMethod("WrapParamInfo" + Guid.NewGuid().ToString(), null, new[] { typeof(IDbCommand), typeof(object) }, modelType, true);
#endif
                il = dm.GetILGenerator();
            }

            internal Action<IDbCommand, object> CreateGenerator()
            {
                var table = TableInfo.Get(modelType);

                il.Emit(OpCodes.Ldarg_1); // stack is now [untyped-param]
                if (table.IsStructType)
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

                        //有定義EnumValue的話, 做轉換處理
                        Type enumValueType;
                        var enumValuesGetter = EnumValueHelper.GetValuesGetterMethod(memberType, out enumValueType);
                        if (enumValuesGetter != null) il.EmitCall(OpCodes.Call, enumValuesGetter, null);     //stack is [parameters] [parameters] [parameter] [parameter] [typed-value]

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
                     * if (是值類型) {                                
                     *    if (無 handler) {
                     *        if (有EnumValue)                   //type換成EnumValue          01.    value = getEnumValue(value);
                     *        if (是Enum)                        //type換成Enum基礎類型       
                     *                                                                        02.    value = (object)value;     //boxed
                     *    }
                     * }
                     * if (可為null) {                                                        03.    if ( value != null ) goto notNullHandle:
                     *    if (有NullMapping) {                   //type換成NullMapping        04.    value = NullMapping;       //NullMapping一定不會是null
                     *       if (是值類型)                                                    05.    value = (object)value;     //boxed
                     *    } else {                                                            06.    value = DBNull.Value;                                                                    
                     *       if (DbType是字串)                                                07.    loc_1 = 0;
                     *                                                                        08.    goto nullHandleDone:
                     *    }
                     *                                                                        09. notNullHandle:
                     * }
                     * if (是值類型) {
                     *    if (是Enum或Nullable<Enum>且無handler) //type換成Enum基礎型別       10.    value = (object)(Enum基礎型別)value;  //boxed
                     * } else {
                     *    if (是System.Data.Linq.Binary) {       //type換成bye[]              11.    value = value.ToArray();
                     *    } else if (DbType是字串) {                                          12.    loc_1 = value.length > 4000 ? -1 : 4000;
                     *    }
                     * }   
                     * if (前面有goto nullHandleDone)                                         13. nullHandleDone:
                     * if (有handler) {                                                       14.    SqlMapper.TypeHandlerCache<T>.SetValue(parameter, value);
                     * } else {                                                               15.    paramter.Value = value;
                     *    if (DbType非Time) {                                                 
                     *       if (型別為物件 且 DbType是Object) {  //dynamic的意思             16.    paramter.DbType = SqlMapper.GetDbType(paramter.Value);
                     *       } else {                                                         17.    paramter.DbType = dbType;
                     *       }
                     *    }
                     *    if (DbType是字串)                                                   18.    if (loc_1 != 0) paramter.Size = loc_1;
                     * }
                     *                                                                        19.    paramters.Add(paramter);
                     */


                    if (memberType.IsValueType)
                    {
                        if (handler == null)
                        {
                            //01. 有定義EnumValue的話, 取得對應值, 同時memberType換成EnumValue
                            Type enumValueType;
                            var enumValueGetter = EnumValueHelper.GetValueGetterMethod(memberType, out enumValueType);
                            if (enumValueGetter != null)
                            {
                                il.EmitCall(OpCodes.Call, enumValueGetter, null);     //stack is [parameters] [parameters] [parameter] [parameter] [typed-value]
                                memberType = enumValueType;
                            }

                            //是Enum的話, 直接換成基礎型別
                            if (memberType.IsEnum) memberType = GetEnumUnderlyingType(memberType);

                            //02. value = (object)value;     //boxed
                            il.Emit(OpCodes.Box, memberType);  //stack is [parameters] [parameters] [parameter] [parameter] [boxed-value]
                        }
                    }


                    Label? nullHandleDone = null;
                    Type underlyingType = null;
                    //如果可能為null的話, 才做null的判斷處理
                    if (!memberType.IsValueType || (underlyingType = Nullable.GetUnderlyingType(memberType)) != null)
                    {
                        var notNullHandle = il.DefineLabel();
                        //03. 判斷非null的話跳notNullHandle
                        il.Emit(OpCodes.Dup);    // stack is [parameters] [[parameters]] [parameter] [parameter] [boxed-value] [boxed-value]
                        il.Emit(OpCodes.Brtrue_S, notNullHandle); // stack is [parameters] [parameters] [parameter] [parameter] [boxed-value]

                        if (column.NullMapping != null)
                        {
                            //04. value = NullMapping;
                            il.Emit(OpCodes.Pop);       // stack is [parameters] [parameters] [parameter] [parameter]
                            il.EmitConstant(column.NullMapping);     // stack is [parameters] [parameters] [parameter] [parameter] [typed-value]
                            memberType = column.NullMapping.GetType();

                            //05. 是值類型的話, box
                            if (memberType.IsValueType) il.Emit(OpCodes.Box, memberType);  //stack is [parameters] [parameters] [parameter] [parameter] [boxed-value]
                        }
                        else
                        {
                            //06. value = DBNull.Value;
                            il.Emit(OpCodes.Pop); // relative stack empty
                            il.Emit(OpCodes.Ldsfld, Reflect.DBNull_Value); // relative stack [DBNull]
                            //07. 如果DbType是字串, 設定 區域變數size = 0
                            if (dbType == DbType.String || dbType == DbType.AnsiString)
                            {
                                Reflect.Dapper.EmitInt32(il, 0);
                                setSizeVariable();
                            }
                            //08. 跳到nullHandleDone
                            nullHandleDone = il.DefineLabel();
                            il.Emit(OpCodes.Br_S, nullHandleDone.Value);
                        }
                        //09. 標記notNullHandle
                        il.MarkLabel(notNullHandle);
                    }

                    if (memberType.IsValueType)
                    {
                        //10. 如果是Enum或Nullable<Enum>且無handler, value = (object)(Enum基礎型別)value;  //boxed
                        var nullType = Nullable.GetUnderlyingType(memberType);
                        if (nullType != null) memberType = nullType;
                        if (memberType.IsEnum && handler == null)
                        {
                            //il.Emit(OpCodes.Unbox, memberType);
                            il.Emit(OpCodes.Unbox_Any, memberType);
                            memberType = GetEnumUnderlyingType(memberType);
                            il.Emit(OpCodes.Box, memberType);
                        }
                    }
                    else
                    {
                        if (memberType.FullName == Reflect.Dapper.LinqBinary)  //System.Data.Linq.Binary
                        {
                            //11. 是System.Data.Linq.Binary的話, value = value.ToArray();
                            il.EmitCall(OpCodes.Callvirt, memberType.GetMethod("ToArray", BindingFlags.Public | BindingFlags.Instance), null); // stack is [parameters] [parameters] [parameter] [parameter] [byte[]]
                            memberType = typeof(byte[]);
                        }
                        else if (dbType == DbType.String || dbType == DbType.AnsiString)
                        {
                            //12. size = value.length > 4000 ? -1 : 4000;
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
                            setSizeVariable();  // stack is [parameters] [parameters] [parameter] [parameter] [typed-value]
                        }
                    }
                    //13. 有nullHandleDone的話, 標記nullHandleDone
                    if (nullHandleDone.HasValue) il.MarkLabel(nullHandleDone.Value);

                    if (handler != null)
                    {
                        //14. 有handler的話, 呼叫SqlMapper.TypeHandlerCache<T>.SetValue(parameter, value);
#pragma warning disable 618
                        il.Emit(OpCodes.Call, typeof(TypeHandlerCache<>).MakeGenericType(memberType).GetMethod(nameof(TypeHandlerCache<int>.SetValue))); // stack is now [parameters] [parameters] [parameter]
#pragma warning restore 618
                    } 
                    else
                    {
                        //15. paramter.Value = value;
                        il.EmitCall(OpCodes.Callvirt, Reflect.IDataParameter_Value_Set, null);// stack is now [parameters] [parameters] [parameter]

                        if (dbType != DbType.Time)
                        {
                            il.Emit(OpCodes.Dup); // stack is now [parameters] [parameters] [parameter] [parameter]
                            
                            if (dbType == DbType.Object && memberType == typeof(object)) // includes dynamic
                            {
                                //16. 型別為物件 且 DbType是Object, paramter.DbType = SqlMapper.GetDbType(paramter.Value)
                                // look it up from the param value
                                il.Emit(OpCodes.Dup); // stack is now [parameters] [parameters] [parameter] [parameter] [parameter]
                                il.EmitCall(OpCodes.Callvirt, Reflect.IDataParameter_Value_Get, null);// stack is now [parameters] [parameters] [parameter] [parameter] [boxed-value]
                                il.Emit(OpCodes.Call, Reflect.SqlMapper_GetDbType); // stack is now [parameters] [parameters] [parameter] [parameter] [dbType]
                            }
                            else
                            {
                                //17. paramter.DbType = dbType;
                                Reflect.Dapper.EmitInt32(il, (int)dbType);// stack is now [parameters] [parameters] [parameter] [parameter] [db-type]
                            }
                            il.EmitCall(OpCodes.Callvirt, Reflect.IDataParameter_DbType_Set, null);// stack is now [parameters] [parameters] [parameter]
                        }

                        //18. if (loc_1 != 0) paramter.Size = loc_1;
                        if (dbType == DbType.String || dbType == DbType.AnsiString)
                        {
                            var endOfSize = il.DefineLabel();
                            // don't set if 0
                            getSizeVariable();  // [parameters] [parameters] [parameter] [size]
                            il.Emit(OpCodes.Brfalse_S, endOfSize); // [parameters] [parameters] [parameter]

                            il.Emit(OpCodes.Dup);// stack is now [parameters] [parameters] [parameter] [parameter]
                            getSizeVariable();   // stack is now [parameters] [parameters] [parameter] [parameter] [size]
                            il.EmitCall(OpCodes.Callvirt, Reflect.IDbDataParameter_Size_Set, null); // stack is now [parameters] [parameters] [parameter]

                            il.MarkLabel(endOfSize);
                        }
                    }
                    //19. checkForDuplicates的話, 前面已經有FindOrAdd過了, 直接釋放。非checkForDuplicates的話, paramters.Add(paramter)
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
                return (Action<IDbCommand, object>)GetDelegate();
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

            private static Type GetEnumUnderlyingType(Type enumType)
            {
                switch (Type.GetTypeCode(Enum.GetUnderlyingType(enumType)))
                {
                    case TypeCode.Byte: return typeof(byte); 
                    case TypeCode.SByte: return typeof(sbyte); 
                    case TypeCode.Int16: return typeof(short); 
                    case TypeCode.Int32: return typeof(int); 
                    case TypeCode.Int64: return typeof(long); 
                    case TypeCode.UInt16: return typeof(ushort); 
                    case TypeCode.UInt32: return typeof(uint); 
                    case TypeCode.UInt64: return typeof(ulong); 
                }
                return enumType;
            }
        }
    }
}
