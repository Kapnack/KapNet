using HarmonyLib;
using ImageCampus.ToolBox.Services;
using KapNet.src;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

namespace KapNet
{
    public class RPCAttribute : Attribute
    {
        public PacketMetaData metaData;
        RPCAttribute()
        {
            metaData = PacketMetaData.None;
        }
    }

    public struct RPCMethod
    {
        public MethodInfo method;
        public PacketMetaData metaData;

        public RPCMethod(MethodInfo method, PacketMetaData metaData)
        {
            this.method = method;
            this.metaData = metaData;
        }
    }

    internal class RPCFactory
    {
        private static NetworkPeer<IPEndPoint> NetworkPeer => ServiceProvider.Instance.GetService<NetworkPeer<IPEndPoint>>();
        private static NetTree NetTree => ServiceProvider.Instance.GetService<NetTree>();

        internal RPCFactory()
        { }

        internal void Init()
        {
            Harmony harmony = new Harmony("RPC Hooks");

            List<MethodInfo> rpcMethods = new List<MethodInfo>();

            RPCAttribute RPCAttribute = null;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                foreach (Type type in assembly.GetTypes())
                    foreach (MethodInfo method in type.GetMethods())
                    {
                        RPCAttribute = method.GetCustomAttribute<RPCAttribute>();

                        if (RPCAttribute == null)
                            continue;

                        rpcMethods.Add(method);
                    }

            foreach (MethodInfo method in rpcMethods)
            {
                HarmonyMethod patch = new HarmonyMethod(GetType().GetMethod(nameof(PatchingMethod), BindingFlags.NonPublic | BindingFlags.Static));
                harmony.Patch(method, postfix: patch);
            }
        }

        private static void PatchingMethod(object __instance, object __originalMethod)
        {
            PacketMetaData metaData = __originalMethod.GetType().GetCustomAttribute<RPCAttribute>().metaData;

            NetworkPeer.Send(PacketType.Method, metaData, NetTree.GetPath(__instance));
        }
    }
}
