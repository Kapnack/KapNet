using HarmonyLib;
using ImageCampus.ToolBox.Services;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Net
{
    public class RPCAttribute : Attribute
    {
        public PacketMetaData metaData;

        public RPCAttribute(PacketMetaData metaData = PacketMetaData.None)
        {
            this.metaData = metaData;
        }
    }

    public readonly struct RPCMethod
    {
        public readonly MethodInfo Patched;
        public readonly MethodInfo Original;
        public readonly PacketMetaData MetaData;
        public readonly bool IsOriginalStatic;

        public RPCMethod(MethodInfo patched, MethodInfo original, PacketMetaData metaData, bool isOriginalStatic)
        {
            Patched = patched;
            Original = original;
            MetaData = metaData;
            IsOriginalStatic = isOriginalStatic;
        }
    }

    internal class RPCFactory : IService
    {
        public bool IsPersistance => false;
        private static NetTree NetTree => ServiceProvider.Instance.GetService<NetTree>();

        private static readonly Dictionary<string, RPCMethod> registry = new Dictionary<string, RPCMethod>();

        public static readonly Dictionary<string, List<Type>> FunctionsParameters = new Dictionary<string, List<Type>>();

        private static ConnectionHandler ConnectionHandler => ServiceProvider.Instance.GetService<ConnectionHandler>();

        private static bool initialized;

        internal void Init()
        {
            if (initialized)
                return;

            initialized = true;

            Harmony harmony = new Harmony("RPC Hooks");

            HarmonyMethod postfix = new HarmonyMethod(GetType().GetMethod(nameof(PatchingMethod), BindingFlags.NonPublic | BindingFlags.Static));

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = Array.FindAll(ex.Types, t => t != null);
                }
                catch
                {
                    continue;
                }

                foreach (Type type in types)
                    foreach (MethodInfo method in type.GetMethods(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                    {
                        RPCAttribute attr = method.GetCustomAttribute<RPCAttribute>();
                        if (attr == null)
                            continue;

                        MethodInfo originalCopy = BuildReversePatchedCopy(harmony, method);

                        harmony.Patch(method, postfix: postfix);

                        string key = $"{method.DeclaringType.FullName}.{method.Name}";

                        registry[key] = new RPCMethod(method, originalCopy, attr.metaData, method.IsStatic);
                    }
            }
        }

        private static MethodInfo BuildReversePatchedCopy(Harmony harmony, MethodInfo original)
        {
            List<Type> standInParamTypes = new List<Type>();

            List<Type> argOnlyTypes = new List<Type>();

            if (!original.IsStatic)
                standInParamTypes.Add(original.DeclaringType);

            foreach (ParameterInfo parameter in original.GetParameters())
            {
                standInParamTypes.Add(parameter.ParameterType);
                argOnlyTypes.Add(parameter.ParameterType);
            }

            FunctionsParameters[$"{original.DeclaringType.FullName}.{original.Name}"] = argOnlyTypes;

            DynamicMethod standin = new DynamicMethod($"{original.Name}_Original", original.ReturnType, standInParamTypes.ToArray(), original.DeclaringType, skipVisibility: true);

            ILGenerator il = standin.GetILGenerator();
            if (original.ReturnType != typeof(void))
            {
                if (original.ReturnType.IsValueType)
                    il.Emit(OpCodes.Ldloca_S, il.DeclareLocal(original.ReturnType).LocalIndex);
                else
                    il.Emit(OpCodes.Ldnull);
            }

            il.Emit(OpCodes.Ret);

            harmony.CreateReversePatcher(original, new HarmonyMethod(standin)).Patch();

            return standin;
        }

        private static void PatchingMethod(object __instance, MethodBase __originalMethod, object[] __args)
        {
            PacketMetaData metaData = __originalMethod.GetCustomAttribute<RPCAttribute>().metaData;

            object[] resolvedArgs = ResolveArgs(__args);

            string methodName = $"{__originalMethod.DeclaringType.FullName}.{__originalMethod.Name}";
            uint[] path = NetTree.GetPath(__instance);

            object[] payload = new object[2 + resolvedArgs.Length];
            payload[0] = methodName;
            payload[1] = path;
            Array.Copy(resolvedArgs, 0, payload, 2, resolvedArgs.Length);

            if (ConnectionHandler is ServerConnection serverConnection)
                serverConnection.BroadCast(PacketType.Method, metaData, payload);
            else if (ConnectionHandler is ClientConnection clientConnection)
                clientConnection.Send(PacketType.Method, metaData, payload);
        }

        private static object[] ResolveArgs(object[] args)
        {
            object[] resolved = new object[args.Length];

            for (int i = 0; i < args.Length; i++)
            {
                object arg = args[i];

                if (arg == null)
                {
                    resolved[i] = null;
                    continue;
                }

                Type argType = arg.GetType();

                if (IsPrimitive(argType))
                {
                    resolved[i] = arg;
                    continue;
                }

                uint[] path = NetTree.GetPath(arg);

                if (path.Length > 0)
                {
                    resolved[i] = path;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"RPC argument of type '{argType.FullName}' at index {i} is not tracked by NetTree " +
                        $"and cannot be resolved to a path.");
                }
            }

            return resolved;
        }

        private static bool IsPrimitive(Type t) =>
            t.IsPrimitive || t.IsEnum || t == typeof(string) ||
            t == typeof(decimal) || t == typeof(DateTime);

        internal object InvokeOriginal(string methodKey, object instance, params object[] args)
        {
            if (!registry.TryGetValue(methodKey, out RPCMethod rpc))
                throw new KeyNotFoundException($"No RPC registered for '{methodKey}'.");

            return rpc.Original.Invoke(null, rpc.IsOriginalStatic ? args : Prepend(instance, args));
        }

        private static object[] Prepend(object first, object[] rest)
        {
            object[] result = new object[rest.Length + 1];
            result[0] = first;
            Array.Copy(rest, 0, result, 1, rest.Length);
            return result;
        }
    }
}
