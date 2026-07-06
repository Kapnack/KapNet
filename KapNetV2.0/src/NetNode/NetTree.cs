using ImageCampus.ToolBox.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

internal class NetTree : IService
{
    public bool IsPersistance => false;

    private object _baseObject = null;
    public NetNode root;

    const BindingFlags HIERARCHY_BINDINGS =
        BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static |
        BindingFlags.DeclaredOnly;

    private static IEnumerable<FieldInfo> GetAllFields(Type type)
    {
        for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
            foreach (FieldInfo field in current.GetFields(HIERARCHY_BINDINGS))
                yield return field;
    }

    public NetTree()
    {
    }

    public static NetTree Build(object rootObject)
    {
        NetTree tree = new NetTree();
        tree._baseObject = rootObject;
        tree.root = new NetNode("root", Array.Empty<uint>(), rootObject.GetHashCode());

        tree.Walk(rootObject, rootObject.GetType(), tree.root,
                  new List<uint>(), new List<IAccessor>());

        return tree;
    }

    public void Tick(Action<Type, object, PacketMetaData, uint[]> updateValueEvent, Action<PacketMetaData, uint[]> nullEvent)
    {
        root.Tick(_baseObject, updateValueEvent, nullEvent);
    }

    private void Walk(object obj, Type type, NetNode parentNode,
                      List<uint> addressSoFar, List<IAccessor> chainSoFar)
    {
        if (obj == null)
            return;

        foreach (FieldInfo field in GetAllFields(type))
        {
            NetAttribute attr = field.GetCustomAttribute<NetAttribute>();

            if (attr == null)
                continue;

            List<uint> childAddr = new List<uint>(addressSoFar) { attr.id };
            List<IAccessor> childChain = new List<IAccessor>(chainSoFar) { new FieldAccessor(field) };

            string path = string.Join(" > ", childAddr);

            Type fType = field.FieldType;
            object fVal = field.GetValue(obj);

            NetNode childNode = new NetNode(path, childAddr.ToArray(), fVal?.GetHashCode());
            parentNode.AddChild(attr.id, childNode);

            DispatchNode(fVal, fType, childNode, childAddr, childChain, attr.metaData);
        }
    }

    private void DispatchNode(object value, Type declaredType,
                              NetNode node, List<uint> addr, List<IAccessor> chain,
                              PacketMetaData metaData)
    {
        if (IsPrimitive(declaredType))
        {
            node.SetPrimitiveLeaf(chain.ToArray(), metaData);
            return;
        }

        if (declaredType.IsArray)
        {
            Type elementType = declaredType.GetElementType();

            node.InitContainer(chain.ToArray(), value,
                rebuild: (n, val) =>
                {
                    Array a = (Array)val;
                    WalkArrayDim(a, elementType, n, addr, chain, new int[a.Rank], metaData);
                },
                reconcileSize: null);

            if (value != null)
            {
                Array arr = (Array)value;
                WalkArrayDim(arr, elementType, node, addr, chain, new int[arr.Rank], metaData);
            }

            return;
        }

        (Type keyType, Type valType) = GetDictionaryTypes(declaredType);
        if (keyType != null)
        {
            node.InitContainer(chain.ToArray(), value,
                rebuild: (n, val) => WalkDictionary((IDictionary)val, valType, n, addr, chain, metaData),
                reconcileSize: (n, val) =>
                {
                    n.ClearChildren();
                    WalkDictionary((IDictionary)val, valType, n, addr, chain, metaData);
                });

            if (value != null)
                WalkDictionary((IDictionary)value, valType, node, addr, chain, metaData);

            return;
        }

        Type elemType = GetCollectionElementType(declaredType);
        if (elemType != null)
        {
            node.InitContainer(chain.ToArray(), value,
                rebuild: (n, val) => WalkCollection((IEnumerable)val, elemType, n, addr, chain, metaData),
                reconcileSize: (n, val) => ReconcileCollectionSize(n, val, elemType, addr, chain, metaData));

            if (value != null)
                WalkCollection((IEnumerable)value, elemType, node, addr, chain, metaData);

            return;
        }

        node.InitContainer(chain.ToArray(), value,
            rebuild: (n, val) => Walk(val, declaredType, n, addr, chain),
            reconcileSize: null);

        if (value != null)
            Walk(value, declaredType, node, addr, chain);
    }

    private void WalkArrayDim(Array arr, Type elementType, NetNode parentNode, List<uint> addressSoFar,
                              List<IAccessor> chainSoFar, int[] partialIndices, PacketMetaData metaData, int dim = 0)
    {
        if (dim == arr.Rank)
        {
            uint linearIdx = LinearIndex(arr, partialIndices);

            List<uint> childAddr = new List<uint>(addressSoFar) { linearIdx };

            List<IAccessor> childChain = new List<IAccessor>(chainSoFar)
                                          { new ArrayAccessor((int[])partialIndices.Clone()) };

            string path = string.Join(" > ", childAddr);

            object elem = arr.GetValue(partialIndices);

            NetNode childNode = new NetNode(path, childAddr.ToArray(), elem?.GetHashCode());
            parentNode.AddChild(linearIdx, childNode);

            DispatchNode(elem, elementType, childNode, childAddr, childChain, metaData);
            return;
        }

        int dimLen = arr.GetLength(dim);
        for (int i = 0; i < dimLen; ++i)
        {
            partialIndices[dim] = i;
            WalkArrayDim(arr, elementType, parentNode, addressSoFar, chainSoFar, partialIndices, metaData, dim + 1);
        }
    }

    private static uint LinearIndex(Array arr, int[] indices)
    {
        uint result = 0;
        uint stride = 1;
        for (int d = arr.Rank - 1; d >= 0; --d)
        {
            result += (uint)indices[d] * stride;
            stride *= (uint)arr.GetLength(d);
        }
        return result;
    }

    private void WalkDictionary(IDictionary dict, Type valueType, NetNode parentNode, List<uint> addressSoFar,
                                List<IAccessor> chainSoFar, PacketMetaData metaData)
    {
        int slot = 0;
        foreach (DictionaryEntry entry in dict)
        {
            object key = entry.Key;

            List<uint> childAddr = new List<uint>(addressSoFar) { (uint)slot };
            List<IAccessor> childChain = new List<IAccessor>(chainSoFar) { new DictAccessor(slot) };

            string path = $"{string.Join(" > ", childAddr)} (key={key})";

            NetNode childNode = new NetNode(path, childAddr.ToArray(), entry.Value?.GetHashCode());
            parentNode.AddChild((uint)slot, childNode);

            DispatchNode(entry.Value, valueType, childNode, childAddr, childChain, metaData);
            ++slot;
        }
    }

    private void WalkCollection(IEnumerable collection, Type elementType,
                                NetNode parentNode, List<uint> addressSoFar,
                                List<IAccessor> chainSoFar, PacketMetaData metaData)
    {
        int index = 0;

        foreach (object item in collection)
        {
            List<uint> childAddr = new List<uint>(addressSoFar) { (uint)index };
            List<IAccessor> childChain = new List<IAccessor>(chainSoFar) { new ListAccessor(index) };

            string path = string.Join(" > ", childAddr);

            NetNode childNode = new NetNode(path, childAddr.ToArray(), item?.GetHashCode());
            parentNode.AddChild((uint)index, childNode);

            DispatchNode(item, elementType, childNode, childAddr, childChain, metaData);
            index++;
        }
    }

    private void ReconcileCollectionSize(NetNode node, object value, Type elemType,
                                         List<uint> addr, List<IAccessor> chain, PacketMetaData metaData)
    {
        if (!(value is IList list))
            return;

        int existingCount = node.ChildCount;

        if (list.Count > existingCount)
        {
            for (int i = existingCount; i < list.Count; i++)
            {
                List<uint> childAddr = new List<uint>(addr) { (uint)i };
                List<IAccessor> childChain = new List<IAccessor>(chain) { new ListAccessor(i) };

                string path = string.Join(" > ", childAddr);
                object item = list[i];

                NetNode childNode = new NetNode(path, childAddr.ToArray(), item?.GetHashCode());
                node.AddChild((uint)i, childNode);

                DispatchNode(item, elemType, childNode, childAddr, childChain, metaData);
            }
        }
        else if (list.Count < existingCount)
        {
            node.RemoveChildrenFrom((uint)list.Count);
        }
    }

    private static bool IsPrimitive(Type t) =>
        t.IsPrimitive || t.IsEnum || t == typeof(string) ||
        t == typeof(decimal) || t == typeof(DateTime);


    private static (Type keyType, Type valueType) GetDictionaryTypes(Type t)
    {
        foreach (Type iface in t.GetInterfaces())
        {
            if (iface.IsGenericType &&
                iface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            {
                Type[] args = iface.GetGenericArguments();
                return (args[0], args[1]);
            }
        }

        if (typeof(IDictionary).IsAssignableFrom(t))
            return (typeof(object), typeof(object));

        return (null, null);
    }

    private static Type GetCollectionElementType(Type t)
    {
        if (typeof(IDictionary).IsAssignableFrom(t))
            return null;

        foreach (Type iface in t.GetInterfaces())
        {
            if (iface.IsGenericType &&
                iface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                return null;
        }

        foreach (Type iface in t.GetInterfaces())
        {
            if (iface.IsGenericType &&
                iface.GetGenericTypeDefinition() == typeof(ICollection<>))
                return iface.GetGenericArguments()[0];
        }

        if (!t.IsArray && typeof(ICollection).IsAssignableFrom(t))
            return typeof(object);

        return null;
    }

    public NetNode Get(params uint[] address) => root?.Resolve(address);

    public uint[] GetPath(object instance)
    {
        uint[] path;

        if (!root.GetPath(instance, out path))
            path = Array.Empty<uint>();

        return path;
    }

    public object GetValue(params uint[] address)
    {
        NetNode node = Get(address) ?? throw new KeyNotFoundException($"Address [{string.Join(",", address)}] not found.");

        return node.GetValue(_baseObject);
    }

    public void SetValue(object value, params uint[] address)
    {
        NetNode node = Get(address) ?? throw new KeyNotFoundException($"Address [{string.Join(",", address)}] not found.");

        node.SetValue(_baseObject, value);
    }

    public void Print() => root?.Print(_baseObject);
}