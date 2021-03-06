﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CS.Util;
using CS.Util.Extensions;

namespace CS.Util.Dynamic
{
    public delegate object CompiledMethodDelegate(object target, object[] args);
    public delegate object DynamicInterfaceMethodHandler(MethodInfo method, object[] args);

    public static class DynamicTypeFactory
    {
        private static readonly AssemblyBuilder asmBuilder;
        private static readonly ModuleBuilder modBuilder;
        private const int ranLength = 12;
        private static bool emitSymbols = false;
        private static Dictionary<string, Type> _implementerCache = new Dictionary<string, Type>();

        static DynamicTypeFactory()
        {
            asmBuilder = Thread.GetDomain().DefineDynamicAssembly(new AssemblyName("dynamic_type_factory"), AssemblyBuilderAccess.Run);

#if DEBUG
            if (Debugger.IsAttached)
            {
                emitSymbols = true;
                Type daType = typeof(DebuggableAttribute);
                ConstructorInfo daCtor = daType.GetConstructor(new Type[] { typeof(DebuggableAttribute.DebuggingModes) });
                CustomAttributeBuilder daBuilder = new CustomAttributeBuilder(daCtor, new object[]
                {
                    DebuggableAttribute.DebuggingModes.DisableOptimizations |
                    DebuggableAttribute.DebuggingModes.Default
                });
                asmBuilder.SetCustomAttribute(daBuilder);
            }
#endif

            modBuilder = asmBuilder.DefineDynamicModule("dynamic_type_factory_module", emitSymbols);
        }

        public static InterfaceModelBuilder Interface()
        {
            return new InterfaceModelBuilder(modBuilder);
        }

        public static InterfaceModelBuilder Interface<T>()
        {
            return new InterfaceModelBuilder(modBuilder, typeof(T));
        }

        public static InterfaceModelBuilder Interface(Type baseType)
        {
            return new InterfaceModelBuilder(modBuilder, baseType);
        }

        public static ClassModelBuilder Class()
        {
            return new ClassModelBuilder(modBuilder);
        }

        public static Type Merge<T1, T2>()
        {
            return Merge(typeof(T1), typeof(T2));
        }

        public static Type Merge<T1, T2, T3>()
        {
            return Merge(typeof(T1), typeof(T2), typeof(T3));
        }

        public static Type Merge<T1, T2, T3, T4>()
        {
            return Merge(typeof(T1), typeof(T2), typeof(T3), typeof(T4));
        }

        public static Type Merge<T1, T2, T3, T4, T5>()
        {
            return Merge(typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5));
        }

        public static Type Merge<T1, T2, T3, T4, T5, T6>()
        {
            return Merge(typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6));
        }

        public static Type Merge(params Type[] types)
        {
            if (!types.All(t => t.IsInterface))
                throw new ArgumentException("One or more provided types are not an interface.");

            var name = $"dynMerge({RandomEx.GetString(ranLength)})_" + String.Join("_", types.Select(t => t.Name));

            var typeBuilder = modBuilder.DefineType(
                name, TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);

            foreach (Type t in types)
                typeBuilder.AddInterfaceImplementation(t);

            return typeBuilder.CreateType();
        }

        public static T Implement<T>(IDictionary<string, Func<object[], object>> methods)
        {
            return (T)Implement(typeof(T), methods);
        }

        public static object Implement(Type target, IDictionary<string, Func<object[], object>> methods)
        {
            var interfaceMethods = target.GetMethods().Concat(target.GetInterfaces().SelectMany(inter => inter.GetMethods()));
            if (!interfaceMethods.All(m => methods.ContainsKey(m.Name)))
                throw new ArgumentException("All methods of the specified interface must be implemented.", nameof(methods));

            DynamicInterfaceMethodHandler handler = (methodInfo, arguments) => methods[methodInfo.Name](arguments);
            return Implement(target, handler);
        }

        public static object Implement(Type target, DynamicInterfaceMethodHandler implementer)
        {
            Type createdType;
            if (_implementerCache.ContainsKey(target.AssemblyQualifiedName))
            {
                createdType = _implementerCache[target.AssemblyQualifiedName];
            }
            else
            {
                createdType = CreateImplementerType(target, implementer);
                _implementerCache[target.AssemblyQualifiedName] = createdType;
            }

            var constructor = createdType.GetConstructor(new Type[] { typeof(DynamicInterfaceMethodHandler) });
            return constructor.Invoke(new object[] { implementer });
        }

        private static Type CreateImplementerType(Type target, DynamicInterfaceMethodHandler implementer)
        {
            if (!target.IsInterface)
                throw new ArgumentException("target must be an interface type.");
            var name = $"dynImplement({RandomEx.GetString(ranLength)})_" + target.Name;
            var typeBuilder = modBuilder.DefineType(name, TypeAttributes.Public | TypeAttributes.Class);
            typeBuilder.AddInterfaceImplementation(target);

            var implementerField = typeBuilder.DefineField("_implementer", typeof(DynamicInterfaceMethodHandler), FieldAttributes.Private);

            var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public,
                CallingConventions.Standard | CallingConventions.HasThis,
                new Type[] { typeof(DynamicInterfaceMethodHandler) });

            // emit constructor
            var constructorIl = constructorBuilder.GetILGenerator();
            constructorIl.Emit(OpCodes.Ldarg_0);
            constructorIl.Emit(OpCodes.Ldarg_1);
            constructorIl.Emit(OpCodes.Stfld, implementerField);
            constructorIl.Emit(OpCodes.Ret);

            string debugPath = null;
            ISymbolDocumentWriter doc = null;
            if (emitSymbols)
            {
                debugPath = Path.GetTempFileName();
                doc = modBuilder.DefineDocument(debugPath, Guid.Empty, Guid.Empty, Guid.Empty);
            }

            // implement all interface methods.
            Dictionary<string, MethodBuilder> builders = new Dictionary<string, MethodBuilder>();
            var methods = target.GetMethods().Concat(target.GetInterfaces().SelectMany(inter => inter.GetMethods()));
            foreach (var method in methods)
            {
                if (emitSymbols)
                    File.AppendAllText(debugPath, ".method " + method.Name + Environment.NewLine);

                var methodParamaters = method.GetParameters().Select(p => p.ParameterType).ToArray();
                var methodBuilder = typeBuilder.DefineMethod(method.Name, MethodAttributes.Public | MethodAttributes.Virtual, method.CallingConvention,
                    method.ReturnType, methodParamaters);

                builders[method.Name] = methodBuilder;

                var methodIl = emitSymbols
                    ? (ILGeneratorInterface)new DebuggableILGenerator(methodBuilder.GetILGenerator(), doc, debugPath)
                    : new StandardILGenerator(methodBuilder.GetILGenerator());

                var objLocalIndex = methodIl.DeclareLocal(typeof(object[])).LocalIndex;
                var retIsOk = methodIl.DefineLabel();

                // create new object[] to hold paramaters and store it to local 
                methodIl.Emit(OpCodes.Ldc_I4, methodParamaters.Length);
                methodIl.Emit(OpCodes.Newarr, typeof(object));
                methodIl.Emit(OpCodes.Stloc, objLocalIndex);

                // fill object array with method parameters
                for (int i = 0; i < methodParamaters.Length; i++)
                {
                    methodIl.Emit(OpCodes.Ldloc, objLocalIndex);
                    methodIl.Emit(OpCodes.Ldc_I4, i);
                    methodIl.Emit(OpCodes.Ldarg, i + 1);
                    if (methodParamaters[i].IsValueType)
                        methodIl.Emit(OpCodes.Box, methodParamaters[i]);
                    methodIl.Emit(OpCodes.Stelem_Ref);
                }

                // load current delegate, MethodInfo and object array to the stack
                methodIl.Emit(OpCodes.Ldarg_0);
                methodIl.Emit(OpCodes.Ldfld, implementerField);
                methodIl.Emit(OpCodes.Ldtoken, method);
                methodIl.Emit(OpCodes.Call, typeof(MethodBase).GetMethod("GetMethodFromHandle", new[] { typeof(RuntimeMethodHandle) }));
                methodIl.Emit(OpCodes.Castclass, typeof(MethodInfo));
                methodIl.Emit(OpCodes.Ldloc, objLocalIndex);

                // call it.
                methodIl.Emit(method.IsFinal ? OpCodes.Call : OpCodes.Callvirt, typeof(DynamicInterfaceMethodHandler).GetMethod("Invoke"));

                // handle return type mismatches.
                if (method.ReturnType == typeof(void))
                {
                    methodIl.Emit(OpCodes.Ldnull);
                    // if the last two stack elements are equal (ldnull and delegate result) goto return statement.
                    methodIl.Emit(OpCodes.Beq, retIsOk);
                    // else throw exception
                    methodIl.Emit(OpCodes.Ldstr,
                        "Method return type mismatch: The implementing dynamic delegate tried to return a non-null value, " +
                        "when the actual return value is void.");
                    methodIl.Emit(OpCodes.Newobj, typeof(TargetException).GetConstructor(new Type[] { typeof(string) }));
                    methodIl.Emit(OpCodes.Throw);
                }
                else
                {
                    // check the value for null, if its null just return it
                    methodIl.Emit(OpCodes.Dup);
                    methodIl.Emit(OpCodes.Ldnull);
                    methodIl.Emit(OpCodes.Beq, retIsOk);

                    methodIl.Emit(OpCodes.Dup);
                    methodIl.Emit(OpCodes.Callvirt, typeof(object).GetMethod("GetType", BindingFlags.Instance | BindingFlags.Public));
                    methodIl.EmitType(method.ReturnType, false);
                    // if the last two stack elements are equal (getType result and the returntype) goto return statement
                    methodIl.Emit(OpCodes.Callvirt, typeof(Type).GetMethod("Equals", new Type[] { typeof(Type) }));
                    methodIl.Emit(OpCodes.Ldc_I4_1);
                    methodIl.Emit(OpCodes.Beq, retIsOk);

                    methodIl.Emit(OpCodes.Ldstr,
                        "Method return type mismatch: The implementing dynamic delegate tried to return a type that does not " +
                        "match the return type of this method.");
                    methodIl.Emit(OpCodes.Newobj, typeof(TargetException).GetConstructor(new Type[] { typeof(string) }));
                    methodIl.Emit(OpCodes.Throw);
                }

                methodIl.MarkLabel(retIsOk);
                if (method.ReturnType.IsValueType && method.ReturnType != typeof(void))
                    methodIl.Emit(OpCodes.Unbox_Any, method.ReturnType);
                methodIl.Emit(OpCodes.Ret);
            }

            // create properties and match them up to appropriate methods.
            var properties = target.GetProperties().Concat(target.GetInterfaces().SelectMany(inter => inter.GetProperties()));
            foreach (var prop in properties)
            {
                var property = typeBuilder.DefineProperty(prop.Name, PropertyAttributes.HasDefault, prop.PropertyType, Type.EmptyTypes);
                if (builders.ContainsKey("get_" + prop.Name))
                    property.SetGetMethod(builders["get_" + prop.Name]);
                if (builders.ContainsKey("set_" + prop.Name))
                    property.SetSetMethod(builders["set_" + prop.Name]);
            }

            return typeBuilder.CreateType();
        }

        public static CompiledMethodDelegate Compile(MethodInfo method)
        {
            ParameterInfo[] parms = method.GetParameters();
            int numberOfParameters = parms.Length;
            Type[] args = { typeof(object), typeof(object[]) };
            DynamicMethod dynam = new DynamicMethod(String.Empty, typeof(object), args, typeof(CompiledMethodDelegate), true);
            ILGenerator il = dynam.GetILGenerator();
            Label argsOk = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldlen);
            il.Emit(OpCodes.Ldc_I4, numberOfParameters);
            il.Emit(OpCodes.Beq, argsOk);
            il.Emit(OpCodes.Newobj, typeof(TargetParameterCountException).GetConstructor(Type.EmptyTypes));
            il.Emit(OpCodes.Throw);
            il.MarkLabel(argsOk);
            if (!method.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
            }
            int i = 0;
            while (i < numberOfParameters)
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldelem_Ref);
                Type parmType = parms[i].ParameterType;
                if (parmType.IsValueType)
                {
                    il.Emit(OpCodes.Unbox_Any, parmType);
                }
                i++;
            }
            il.Emit(method.IsFinal ? OpCodes.Call : OpCodes.Callvirt, method);
            if (method.ReturnType != typeof(void))
            {
                if (method.ReturnType.IsValueType)
                {
                    il.Emit(OpCodes.Box, method.ReturnType);
                }
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }
            il.Emit(OpCodes.Ret);
            return (CompiledMethodDelegate)dynam.CreateDelegate(typeof(CompiledMethodDelegate));
        }
    }


    public class InterfaceModelBuilder
    {
        public Dictionary<string, PropertyDeclaration> Properties { get; private set; } = new Dictionary<string, PropertyDeclaration>();
        public Dictionary<string, MethodDeclaration> Methods { get; private set; } = new Dictionary<string, MethodDeclaration>();

        private ModuleBuilder _modBuilder;

        public InterfaceModelBuilder(ModuleBuilder modBuilder)
        {
            _modBuilder = modBuilder;
        }

        public InterfaceModelBuilder(ModuleBuilder modBuilder, Type baseType) : this(modBuilder)
        {
            foreach (var prop in baseType.GetProperties())
                Properties[prop.Name] = PropInfoToDeclaration(prop);

            foreach (var mth in baseType.GetMethods(BindingFlags.Public | BindingFlags.DeclaredOnly))
                Methods[mth.Name] = new MethodDeclaration(mth.Name, mth.ReturnType, mth.GetParameters().Select(p => p.ParameterType).ToArray());
        }

        public InterfaceModelBuilder Property<T>(string name, PropertyMethods methods = PropertyMethods.Both)
        {
            return Property(name, typeof(T));
        }

        public InterfaceModelBuilder Property(string name, Type type, PropertyMethods methods = PropertyMethods.Both)
        {
            Properties[name] = new PropertyDeclaration(name, type, methods);
            return this;
        }

        public InterfaceModelBuilder Method(string name, Type returnType, params Type[] parameterTypes)
        {
            Methods[name] = new MethodDeclaration(name, returnType, parameterTypes);
            return this;
        }

        public Type Build()
        {
            MethodAttributes attr = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual | MethodAttributes.Abstract;
            var builder = _modBuilder.DefineType(Guid.NewGuid().ToString(), TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);

            foreach (var propKvp in Properties)
            {
                var prop = propKvp.Value;
                var propBuilder = builder.DefineProperty(prop.Name, PropertyAttributes.HasDefault, prop.Type, Type.EmptyTypes);

                if (prop.Methods.HasFlag(PropertyMethods.Get))
                {
                    MethodBuilder methodBuilder = builder.DefineMethod("get_" + prop.Name, attr, prop.Type, Type.EmptyTypes);
                    propBuilder.SetGetMethod(methodBuilder);
                }

                if (prop.Methods.HasFlag(PropertyMethods.Set))
                {
                    MethodBuilder methodBuilder = builder.DefineMethod("set_" + prop.Name, attr, typeof(void), new Type[] { prop.Type });
                    propBuilder.SetSetMethod(methodBuilder);
                }
            }

            foreach (var methKvp in Methods)
            {
                var meth = methKvp.Value;
                var methBuilder = builder.DefineMethod(meth.Name, MethodAttributes.Abstract | MethodAttributes.Virtual, meth.ReturnType, meth.ParameterTypes);
            }

            var type = builder.CreateType();
            return type;
        }

        private PropertyDeclaration PropInfoToDeclaration(PropertyInfo prop)
        {
            PropertyMethods method = PropertyMethods.Get;
            if (prop.CanRead && prop.CanWrite)
                method = PropertyMethods.Both;
            else if (prop.CanWrite)
                method = PropertyMethods.Set;

            return new PropertyDeclaration(prop.Name, prop.PropertyType, method);
        }

        public class PropertyDeclaration
        {
            public readonly string Name;
            public readonly Type Type;
            public readonly PropertyMethods Methods;

            public PropertyDeclaration(string name, Type type, PropertyMethods methods)
            {
                Name = name;
                Type = type;
                Methods = methods;
            }
        }

        public class MethodDeclaration
        {
            public readonly string Name;
            public readonly Type ReturnType;
            public readonly Type[] ParameterTypes;

            public MethodDeclaration(string name, Type returnType, Type[] parameterTypes)
            {
                Name = name;
                ReturnType = returnType;
                ParameterTypes = parameterTypes;
            }
        }

        [Flags]
        public enum PropertyMethods
        {
            Get = 1,
            Set = 2,
            Both = Get | Set,
        }
    }
}
