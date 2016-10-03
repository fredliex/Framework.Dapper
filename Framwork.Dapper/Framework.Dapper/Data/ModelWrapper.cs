#define GenDebugAssembly1

using Dapper;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Dapper.SqlMapper;

namespace Framework.Data
{
    public static partial class ModelWrapper
    {
        //private static string assemblyName = "ModelWrapper";
#if GenDebugAssembly
        private static AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.RunAndSave);
        private static ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName, assemblyName + ".dll");
        public static void SaveAssembly() {
            System.IO.File.Delete(assemblyName + ".dll");
            assemblyBuilder.Save(assemblyName + ".dll");
        }
#else
        //private static ReflectionPermission pset = new ReflectionPermission(ReflectionPermissionFlag.MemberAccess);
        //private static AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
        //private static ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName);
        private static AssemblyBuilder assemblyBuilder;
        private static ModuleBuilder moduleBuilder;
#endif

        private static ConcurrentDictionary<Type, TableInfo> tableInfoCache = new ConcurrentDictionary<Type, TableInfo>();
        /// <summary>取得Table資訊</summary>
        private static TableInfo GetTableInfo(Type modelType)
        {
            return tableInfoCache.GetOrAdd(modelType, t => new TableInfo(t));
        }





















#if false
        static ModelWrapperBuilder()
        {
            //new ReflectionPermission(ReflectionPermissionFlag.MemberAccess).Demand();
            //new ReflectionPermission(PermissionState.Unrestricted).Assert();
            //assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
            //moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName);

            /*
            List<CustomAttributeBuilder> assemblyAttributes = new List<CustomAttributeBuilder>();

            ConstructorInfo transparencyCtor = typeof(SecurityTransparentAttribute).GetConstructor(Type.EmptyTypes);
            CustomAttributeBuilder transparencyAttribute = new CustomAttributeBuilder(transparencyCtor, new Object[0]);
            assemblyAttributes.Add(transparencyAttribute);

            //Core的話沒有SecurityRulesAttribute
            ConstructorInfo securityRulesCtor = typeof(SecurityRulesAttribute).GetConstructor(new Type[] { typeof(SecurityRuleSet) });
            CustomAttributeBuilder securityRulesAttribute = new CustomAttributeBuilder(securityRulesCtor, new object[] { SecurityRuleSet.Level1 });
            assemblyAttributes.Add(securityRulesAttribute);


            AssemblyName assemblyName = new AssemblyName("ModelWrapper");
            //StackCrawlMark stackMark = StackCrawlMark.LookForMe;
            assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run, assemblyAttributes);
            moduleBuilder = assemblyBuilder.DefineDynamicModule("ModelWrapper");
            */

            var module = (Module)typeof(DynamicMethod).GetMethod("GetDynamicMethodsModule", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[0]);

        }


        public static object Wrap<T>(T model)
        {
            var wrapperType = CreateWrapperType(typeof(T));
            var wrapper = Activator.CreateInstance(wrapperType);
            wrapperType.GetField("model").SetValue(wrapper, model);
            return wrapper;
        }

        public static Type CreateWrapperType(Type modelType)
        {
            var typeName = string.Format($"{modelType.Name}Wrapper{modelType.GetHashCode()}");
            //var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Sealed | TypeAttributes.Public);
            var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Sealed | TypeAttributes.Public);

            var fieldModel = typeBuilder.DefineField("model", modelType, FieldAttributes.Public);

            var tableInfo = new TableInfo(modelType);

            var isStructModel = modelType.IsValueType;
            var getModelOpCode = isStructModel ? OpCodes.Ldflda : OpCodes.Ldfld;  //若model為struct的話, 要用Ldflda而非Ldfld
            var invokeModelMethodOpCode = isStructModel ? OpCodes.Call : OpCodes.Callvirt; //若model為struct的話, 要用Call而非Callvirt

            foreach (var column in tableInfo.Columns)
            {
                //property get
                var getMethodBuilder = typeBuilder.DefineMethod($"get_{column.Name}", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, column.ValueType, Type.EmptyTypes);
                var getMethodIL = getMethodBuilder.GetILGenerator();
                getMethodIL.Emit(OpCodes.Ldarg_0);
                getMethodIL.Emit(getModelOpCode, fieldModel);
                if (column.MemberType == MemberTypes.Field)
                    getMethodIL.Emit(OpCodes.Ldfld, (FieldInfo)column.Member);
                else
                    getMethodIL.Emit(invokeModelMethodOpCode, ((PropertyInfo)column.Member).GetGetMethod(true));
                getMethodIL.Emit(OpCodes.Ret);
                
                //property set
                var setMethodBuilder = typeBuilder.DefineMethod($"set_{column.Name}", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, null, new[] { column.ValueType });
                var setMethodIL = setMethodBuilder.GetILGenerator();
                setMethodIL.Emit(OpCodes.Ldarg_0);
                setMethodIL.Emit(getModelOpCode, fieldModel);
                setMethodIL.Emit(OpCodes.Ldarg_1);
                if (column.MemberType == MemberTypes.Field)
                    setMethodIL.Emit(OpCodes.Stfld, (FieldInfo)column.Member);
                else
                    setMethodIL.Emit(invokeModelMethodOpCode, ((PropertyInfo)column.Member).GetSetMethod(true));
                setMethodIL.Emit(OpCodes.Ret);

                //property
                var prop = typeBuilder.DefineProperty(column.Name, PropertyAttributes.HasDefault, CallingConventions.HasThis, column.ValueType, null);
                prop.SetGetMethod(getMethodBuilder);
                prop.SetSetMethod(setMethodBuilder);
            }
            var type = typeBuilder.CreateType();
            return type;
        }
#endif
    }
}
