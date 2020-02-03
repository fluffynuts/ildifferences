using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace ILDifferences
{
    public interface IShim
    {
        object GetPropertyValue(string name);
        void SetPropertyValue(string name, object value);
    }

    public class Shim : IShim
    {
        private readonly Dictionary<string, object> _values
            = new Dictionary<string, object>();

        public void SetPropertyValue(
            string name,
            object value)
        {
            _values[name] = value;
        }

        public object GetPropertyValue(
            string name
        )
        {
            return _values[name];
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            TryDo(() => Demonstrate(typeof(Shim)), "When field type is concrete");
            Demonstrate(typeof(IShim));
        }

        private static void TryDo(Action toRun, string label)
        {
            try
            {
                Console.WriteLine($"Demo: {label}");
                toRun();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fails: {ex.Message}");
            }
        }

        private static void Demonstrate(Type fieldType)
        {
            var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("__foo__"), AssemblyBuilderAccess.Run
            );
            var modBuilder = asmBuilder.DefineDynamicModule("__bar__");
            var typeBuilder = modBuilder.DefineType("__my_type__", TypeAttributes.Class | TypeAttributes.Public);
            // define field
            var field = typeBuilder.DefineField(
                "_id",
                typeof(object),
                FieldAttributes.Private);
            // define setter
            var propertyMethodAttributes = MethodAttributes.Public |
                MethodAttributes.SpecialName |
                MethodAttributes.Virtual |
                MethodAttributes.HideBySig;

            var setMethod = typeBuilder.DefineMethod(
                "set_Id",
                propertyMethodAttributes,
                null,
                new[] { typeof(int) }
            );
            var il = setMethod.GetILGenerator();
            var shimSetter = fieldType
                .GetMethod("SetPropertyValue");
            var boxed = il.DeclareLocal(typeof(object));
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Box, fieldType);
            il.Emit(OpCodes.Stloc, boxed);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ldstr, "_id_");
            il.Emit(OpCodes.Ldloc, boxed);
            il.Emit(OpCodes.Call, shimSetter);
            il.Emit(OpCodes.Ret);

            // - end as per pb


            // define getter
            var getMethod = typeBuilder.DefineMethod(
                "get_Id",
                propertyMethodAttributes,
                typeof(int),
                new Type[0]
            );
            var shimGetter = fieldType
                .GetMethod(nameof(IShim.GetPropertyValue));

            il = getMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); // this
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ldstr, "_id_");
            il.Emit(OpCodes.Call, shimGetter);
            il.Emit(OpCodes.Ret);

            var type = typeBuilder.CreateTypeInfo();
            var instance = Activator.CreateInstance(type);
            var shimField = type.GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic);
            shimField.SetValue(instance, new Shim());


            var setter = type.GetMethod("set_Id");
            setter.Invoke(instance, new object[] { 42 });
            var getter = type.GetMethod("get_Id");
            var result = getter.Invoke(instance, new object[0]);

            Console.WriteLine($"Stored and retrieved: {result}");
        }
    }
}