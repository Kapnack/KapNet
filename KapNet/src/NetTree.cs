using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

public class NetTree
{
    private object _baseObject = null;
    public NetNode root;

    const BindingFlags BINDINGS =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static NetTree Build(object rootObject)
    {
        NetTree tree = new NetTree();
        tree._baseObject = rootObject;
        tree.root = new NetNode("root", Array.Empty<uint>(), rootObject.GetHashCode());

        tree.Walk(rootObject, rootObject.GetType(), tree.root,
                  new List<uint>(), new List<object>());

        return tree;
    }

    public void Tick(Action<object, uint[]> updateValueEvent)
    {
        root.Tick(updateValueEvent);
    }

    private void Walk(object obj, Type type, NetNode parentNode,
                      List<uint> addressSoFar, List<object> chainSoFar)
    {
        if (obj == null)
            return;

        foreach (FieldInfo field in type.GetFields(BINDINGS))
        {
            NetAttribute attr = field.GetCustomAttribute<NetAttribute>();

            if (attr == null) 
                continue;

            List<uint> childAddr = new List<uint>(addressSoFar) { attr.id };
            List<object> childChain = new List<object>(chainSoFar) { field };

            string path = string.Join(" > ", childAddr);
            NetNode childNode = new NetNode(path, childAddr.ToArray(), field.GetValue(obj).GetHashCode());
            parentNode.AddChild(attr.id, childNode);

            Type fType = field.FieldType;
            object fVal = field.GetValue(obj);

            DispatchNode(fVal, fType, childNode, childAddr, childChain);
        }
    }

    private void DispatchNode(object value, Type declaredType,
                              NetNode node, List<uint> addr, List<object> chain)
    {
        if (IsPrimitive(declaredType))
        {
            node.SetPrimitiveLeaf(_baseObject, chain.ToArray());
            return;
        }

        if (value == null)
        {
            node.SetNullClassLeaf(_baseObject, chain.ToArray());
            return;
        }

        node.Init(_baseObject, chain.ToArray());

        if (declaredType.IsArray)
        {
            Array arr = (Array)value;
            Type elementType = declaredType.GetElementType();
            WalkArrayDim(arr, elementType, node, addr, chain,
                         partialIndices: new int[arr.Rank], dim: 0);
            return;
        }

        (Type keyType, Type valType) = GetDictionaryTypes(declaredType);
        if (keyType != null)
        {
            WalkDictionary((IDictionary)value, valType, node, addr, chain);
            return;
        }

        Type elemType = GetCollectionElementType(declaredType);
        if (elemType != null)
        {
            WalkCollection((IEnumerable)value, elemType, node, addr, chain);
            return;
        }

        Walk(value, declaredType, node, addr, chain);
    }

    private void WalkArrayDim(Array arr, Type elementType,
                              NetNode parentNode, List<uint> addressSoFar,
                              List<object> chainSoFar,
                              int[] partialIndices, int dim)
    {
        if (dim == arr.Rank)
        {
            uint linearIdx = LinearIndex(arr, partialIndices);

            List<uint> childAddr = new List<uint>(addressSoFar) { linearIdx };

            List<object> childChain = new List<object>(chainSoFar)
                                          { new MultiIndex((int[])partialIndices.Clone()) };

            string path = string.Join(" > ", childAddr);
            NetNode childNode = new NetNode(path, childAddr.ToArray(), arr.GetValue(partialIndices).GetHashCode());
            parentNode.AddChild(linearIdx, childNode);

            object elem = arr.GetValue(partialIndices);
            DispatchNode(elem, elementType, childNode, childAddr, childChain);
            return;
        }

        int dimLen = arr.GetLength(dim);
        for (int i = 0; i < dimLen; i++)
        {
            partialIndices[dim] = i;
            WalkArrayDim(arr, elementType, parentNode, addressSoFar, chainSoFar,
                         partialIndices, dim + 1);
        }
    }

    private static uint LinearIndex(Array arr, int[] indices)
    {
        uint result = 0;
        uint stride = 1;
        for (int d = arr.Rank - 1; d >= 0; d--)
        {
            result += (uint)indices[d] * stride;
            stride *= (uint)arr.GetLength(d);
        }
        return result;
    }

    private void WalkDictionary(IDictionary dict, Type valueType,
                                NetNode parentNode, List<uint> addressSoFar,
                                List<object> chainSoFar)
    {
        uint slot = 0;
        foreach (DictionaryEntry entry in dict)
        {
            object key = entry.Key;

            List<uint> childAddr = new List<uint>(addressSoFar) { slot };
            List<object> childChain = new List<object>(chainSoFar) { new DictKey(key) };

            string path = $"{string.Join(" > ", childAddr)} (key={key})";
            NetNode childNode = new NetNode(path, childAddr.ToArray(), entry.Value.GetHashCode());
            parentNode.AddChild(slot, childNode);

            DispatchNode(entry.Value, valueType, childNode, childAddr, childChain);
            slot++;
        }
    }

    private void WalkCollection(IEnumerable collection, Type elementType,
                                NetNode parentNode, List<uint> addressSoFar,
                                List<object> chainSoFar)
    {
        bool isList = collection is IList;
        int index = 0;

        foreach (object item in collection)
        {
            List<uint> childAddr = new List<uint>(addressSoFar) { (uint)index };
            List<object> childChain = new List<object>(chainSoFar) { new ListIndex(index) };

            string path = string.Join(" > ", childAddr);
            NetNode childNode = new NetNode(path, childAddr.ToArray(), item.GetHashCode());
            parentNode.AddChild((uint)index, childNode);

            DispatchNode(item, elementType, childNode, childAddr, childChain);
            index++;
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

    public object GetValue(params uint[] address)
    {
        NetNode node = Get(address)
            ?? throw new KeyNotFoundException(
                   $"Address [{string.Join(",", address)}] not found.");



        return node.GetValue();
    }

    public void SetValue(object value, params uint[] address)
    {
        NetNode node = Get(address)
            ?? throw new KeyNotFoundException(
                   $"Address [{string.Join(",", address)}] not found.");
        node.SetValue(value);
    }

    public void Print() => root?.Print();
}
