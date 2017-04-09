//#define saveParamAssembly

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    public static partial class DbWrapperHelper
    {
        private static IDbCommandIntercept dbCommandIntercept;
        static DbWrapperHelper()
        {
            var dbCommandInterceptType = ConfigurationManager.AppSettings["DbCommandInterceptType"];
            dbCommandIntercept = string.IsNullOrWhiteSpace(dbCommandInterceptType) ? 
                null : (IDbCommandIntercept)Activator.CreateInstance(Type.GetType(dbCommandInterceptType));
        }

        private static string assemblyName = "DbWrapper";
#if saveParamAssembly
        private static AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.RunAndSave);
        private static ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName, assemblyName + ".dll");
#else
        private static AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
        private static ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName);
#endif

        internal static DbProviderFactory Wrap(DbProviderFactory factory)
        {
            var factoryWrapper = (DbProviderFactory)typeof(DbIntercept<>).MakeGenericType(factory.GetType()).GetMethod(nameof(DbIntercept<DbProviderFactory>.WrapDbProviderFactory)).Invoke(null, new object[] { factory });
#if saveParamAssembly
            assemblyBuilder.Save(assemblyName + ".dll");
#endif
            return factoryWrapper;
        }

        private static void SetWrapper<T, TWrapper>(Type sourceType, out Func<T, TWrapper> wrapper) where TWrapper : T, IWrappedDb
        {
            var constructor = CreateWrapperType(sourceType, typeof(TWrapper)).GetConstructor(new[] { sourceType });
            var instance = Expression.Parameter(typeof(T));
            var body = Expression.New(constructor, Expression.Convert(instance, sourceType));
            wrapper = Expression.Lambda<Func<T, TWrapper>>(body, instance).Compile();
        }

        /// <summary>建立Wrapper型別</summary>
        /// <param name="instanceType">封裝的型別。例如SqlConnection。</param>
        /// <param name="baseType">模擬型別。例如DbWrapper.ConnectionWrapper。</param>
        /// <returns></returns>
        private static Type CreateWrapperType(Type instanceType, Type baseType)
        {
            var typeName = string.Format("{0}Wrapper{1}", instanceType.Name, instanceType.GetHashCode());
            var interceptMethods = baseType.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(method => new { method, name = method.Name, paramTypes = method.GetParameters().Select(n => n.ParameterType).ToList() }).ToList();

            //不實作界面, 要實作界面的話由baseType負責
            //var interfaceTypes = instanceType.GetInterfaces();
            //var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Sealed | (instanceType.IsPublic ? TypeAttributes.Public : TypeAttributes.NotPublic), baseType, interfaceTypes);
            var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Sealed | (instanceType.IsPublic ? TypeAttributes.Public : TypeAttributes.NotPublic), baseType);

            //private InstanceType instance;
            var fieldInstance = typeBuilder.DefineField("instance", instanceType, FieldAttributes.Private);
            /*
             * 建構式 ctor(InstanceType instance) : base(instance) {
             *    this.instance = instance;
             * }
             */
            var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { instanceType });
            constructorBuilder.DefineParameter(1, ParameterAttributes.None, "instance");
            var constructorIL = constructorBuilder.GetILGenerator();
            if (baseType != null)
            {
                constructorIL.Emit(OpCodes.Ldarg_0);
                var baseConstructor = baseType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { instanceType }, null);
                if (baseConstructor != null) 
                    constructorIL.Emit(OpCodes.Ldarg_1);
                else
                    baseConstructor = baseType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                constructorIL.Emit(OpCodes.Call, baseConstructor);
            }
            constructorIL.Emit(OpCodes.Ldarg_0);
            constructorIL.Emit(OpCodes.Ldarg_1);
            constructorIL.Emit(OpCodes.Stfld, fieldInstance);
            constructorIL.Emit(OpCodes.Ret);

            //實作WrapInstance get
            var methodWrapInstanceGet = typeBuilder.DefineMethod($"get_{nameof(IWrappedDb<int>.Instance)}", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual, instanceType, null);
            var methodWrapInstanceGetIl = methodWrapInstanceGet.GetILGenerator();
            methodWrapInstanceGetIl.Emit(OpCodes.Ldarg_0);
            methodWrapInstanceGetIl.Emit(OpCodes.Ldfld, fieldInstance);
            methodWrapInstanceGetIl.Emit(OpCodes.Ret);
            var propWrapInstance = typeBuilder.DefineProperty(nameof(IWrappedDb<int>.Instance), PropertyAttributes.None, instanceType, null);
            propWrapInstance.SetGetMethod(methodWrapInstanceGet);

            //包裝method, property, event
            var existMethods = new Dictionary<string, MethodInfo>();
            var existProperties = new Dictionary<string, PropertyBuilder>();
            var existEvents = new Dictionary<string, EventBuilder>();
            var curType = instanceType;
            while (true)
            {
                //Debug.WriteLine("Type:{0}", curType);
                //Method
                foreach (var instanceMethod in curType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    MethodAttributes attributes = instanceMethod.Attributes;
                    MethodInfo implement = null;
                    if (instanceMethod.IsVirtual)
                    {
                        if (instanceMethod.IsFinal)
                        { //EIMI
                            continue;
                            /*
#region 取得EIMI實作的method
                            var methodName = instanceMethod.Name;
                            var interfaceNamePos = methodName.LastIndexOf('.');
                            if (interfaceNamePos >= 0)
                            {
                                var interfaceTypeName = methodName.Substring(0, interfaceNamePos);
                                var interfaceMethodName = methodName.Substring(interfaceNamePos + 1);
                                implement = interfaceTypes.First(x => x.FullName == interfaceTypeName).GetMethods().First(x => x.Name == interfaceMethodName && EqualMethodSignature(x, instanceMethod));
                            }
#endregion
                            if (implement == null) continue;
                            */
                        }
                        else
                        {    //abstract, virtual
                            implement = instanceMethod.GetBaseDefinition();
                            if (!implement.IsVirtual) continue;
                            if (curType == typeof(object) && instanceMethod.Name == nameof(object.Equals)) continue; //object.Equals不複寫
                            attributes &= ~MethodAttributes.NewSlot;
                        }
                    }
                    else
                    {
                        continue;  //非override的部份全部pass
                        //if (curType != sourceType || !n.IsPublic) continue; //非override的public只處理sourceType
                    }
                    var signature = GetMethodSignature(instanceMethod);
                    if (!existMethods.ContainsKey(signature))
                    {
                        System.Diagnostics.Debug.WriteLine("{0}\t ---> \t{1}",
                            string.Format("{0}\t{1}\t{2}({3})", attributes.ToString().Replace(" ", ""), instanceMethod.DeclaringType.Name, instanceMethod.Name, string.Join(",", instanceMethod.GetParameters().Select(p => p.ParameterType.Name)), instanceMethod.ReflectedType.Name),
                            implement == null ? "" : string.Format("{0}\t{1}\t{2}({3})", implement.Attributes.ToString().Replace(" ", ""), implement.DeclaringType.Name, implement.Name, string.Join(",", implement.GetParameters().Select(p => p.ParameterType.Name)), implement.ReflectedType.Name)
                        );

                        var interceptMethod = interceptMethods.FirstOrDefault(m => {
                            if (m.name != instanceMethod.Name) return false;
                            var parameters = instanceMethod.GetParameters();
                            if (m.paramTypes.Count != parameters.Length) return false;
                            return m.paramTypes.SequenceEqual(parameters.Select(n => n.ParameterType));
                        });
                        existMethods.Add(signature, interceptMethod?.method ?? CreateMethodWrapper(typeBuilder, instanceMethod, attributes, implement, fieldInstance));
                    }
                }

                //Property
                foreach (var property in curType.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    MethodInfo tmpMethod;
                    MethodBuilder getMethodWrap = null, setMethodWrap = null;
                    if (property.CanRead)
                    {
                        var getMethod = property.GetGetMethod(true);
                        if (getMethod != null && existMethods.TryGetValue(GetMethodSignature(getMethod), out tmpMethod)) getMethodWrap = tmpMethod as MethodBuilder;
                    }
                    if (property.CanWrite)
                    {
                        var setMethod = property.GetSetMethod(true);
                        if (setMethod != null && existMethods.TryGetValue(GetMethodSignature(setMethod), out tmpMethod)) setMethodWrap = tmpMethod as MethodBuilder;
                    }
                    if (getMethodWrap == null && setMethodWrap == null) continue;
                    var propertySignature = property.Name;
                    if (existProperties.ContainsKey(propertySignature)) continue;
                    var propertyBuilder = typeBuilder.DefineProperty(property.Name, property.Attributes, property.PropertyType, null);
                    //Debug.WriteLine("Property:{0}, {1}, {2}", property.Name, getMethodWrap == null ? "" : getMethodWrap.Name, setMethodWrap == null ? "" : setMethodWrap.Name);
                    if (getMethodWrap != null) propertyBuilder.SetGetMethod(getMethodWrap);
                    if (setMethodWrap != null) propertyBuilder.SetSetMethod(setMethodWrap);
                    existProperties.Add(propertySignature, propertyBuilder);
                }

                //Event
                foreach (var ev in curType.GetEvents(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    var eventSignature = ev.Name;
                    if (existEvents.ContainsKey(eventSignature)) continue;
                    MethodInfo tmpMethod;
                    MethodBuilder addMethodWrap = null, removeMethodWrap = null;
                    var addMethod = ev.GetAddMethod(true) ?? ev.GetAddMethod(false);
                    if (addMethod != null && existMethods.TryGetValue(GetMethodSignature(addMethod), out tmpMethod)) addMethodWrap = tmpMethod as MethodBuilder;
                    var removeMethod = ev.GetRemoveMethod(true) ?? ev.GetRemoveMethod(false);
                    if (removeMethod != null && existMethods.TryGetValue(GetMethodSignature(removeMethod), out tmpMethod)) removeMethodWrap = tmpMethod as MethodBuilder;
                    if (addMethodWrap == null && removeMethodWrap == null) continue;
                    var eventBuilder = typeBuilder.DefineEvent(ev.Name, ev.Attributes, ev.EventHandlerType);
                    if (addMethodWrap != null) eventBuilder.SetAddOnMethod(addMethodWrap);
                    if (removeMethodWrap != null) eventBuilder.SetRemoveOnMethod(removeMethodWrap);
                    existEvents.Add(eventSignature, eventBuilder);
                }
                if (curType == typeof(object)) break;
                curType = curType.BaseType;
            }

            return typeBuilder.CreateType();
        }

        private static bool EqualMethodSignature(MethodInfo method1, MethodInfo method2)
        {
            if (method1.GetGenericArguments().Length != method2.GetGenericArguments().Length) return false;
            var params1 = method1.GetParameters();
            var params2 = method2.GetParameters();
            if (params1.Length != params2.Length) return false;
            for (var i = 0; i < params1.Length; i++)
            {
                if (params1[i].ParameterType != params2[i].ParameterType) return false;
            }
            return true;
        }

        private static string GetMethodSignature(MethodInfo method)
        {
            var sb = new StringBuilder();
            sb.Append(method.Name);
            //泛型的不知道怎樣處理比較恰當, 這邊先簡單處理
            var genericArguments = method.GetGenericArguments();
            if (genericArguments.Length > 0) sb.AppendFormat("`{0}", genericArguments.Length);
            sb.AppendFormat("({0})", string.Join(",", method.GetParameters().Select(x => x.ParameterType.FullName ?? x.ParameterType.Name)));
            return sb.ToString();
        }

        private static MethodInfo CreateMethodWrapper(TypeBuilder typeBuilder, MethodInfo wrap, MethodAttributes attributes, MethodInfo implemented, FieldInfo instance)
        {
            var paramters = wrap.GetParameters().ToArray();
            var methodBuilder = typeBuilder.DefineMethod(wrap.Name, attributes, wrap.ReturnType, paramters.Select(p => p.ParameterType).ToArray());
            for (var i = 0; i < paramters.Length; i++)
            {
                methodBuilder.DefineParameter(i + 1, paramters[i].Attributes, paramters[i].Name);
            }
            var il = methodBuilder.GetILGenerator();
            int argLoadStartPos = 0;
            var realMethod = implemented ?? wrap;
            if (!wrap.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);   //this
                il.Emit(OpCodes.Ldfld, instance);
                argLoadStartPos = 1;
            }
            for (var i = 0; i < paramters.Length; i++)
            {
                EmitLoadArgument(il, i + argLoadStartPos);
            }
            il.EmitCall(GetCallOpCode(realMethod), realMethod, null);
            il.Emit(OpCodes.Ret);
            if (implemented != null) typeBuilder.DefineMethodOverride(methodBuilder, implemented);
            return methodBuilder;
        }

        private static void EmitLoadArgument(ILGenerator il, int index)
        {
            switch (index)
            {
                case 0: il.Emit(OpCodes.Ldarg_0); break;
                case 1: il.Emit(OpCodes.Ldarg_1); break;
                case 2: il.Emit(OpCodes.Ldarg_2); break;
                case 3: il.Emit(OpCodes.Ldarg_3); break;
                default: il.Emit(OpCodes.Ldarg_S, index); break;
            }
        }

        private static OpCode GetCallOpCode(MethodInfo method)
        {
            //參考 System.Linq.Expressions.Compiler.LambdaCompiler.EmitMethodCall
            return !method.IsStatic && !method.DeclaringType.IsValueType ? OpCodes.Callvirt : OpCodes.Call;
        }
    }
}
