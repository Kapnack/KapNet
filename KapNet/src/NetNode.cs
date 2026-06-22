using System.Collections;
using System.Reflection;

public readonly struct MultiIndex
{
    public readonly int[] Indices;
    public MultiIndex(int[] indices) => Indices = indices;
    public override string ToString() => $"[{string.Join(",", Indices)}]";
}

public readonly struct DictKey
{
    public readonly object Key;
    public DictKey(object key) => Key = key;
    public override string ToString() => $"[key:{Key}]";
}

public readonly struct ListIndex
{
    public readonly int Index;
    public ListIndex(int index) => Index = index;
    public override string ToString() => $"[{Index}]";
}

public class NetNode
{
    public string Path { get; private set; }
    public uint[] Address { get; private set; }

    public bool IsLeaf { get; private set; }

    private object[] _chain = Array.Empty<object>();

    private object? _rootRef;

    private readonly Dictionary<uint, NetNode> _children = new();

    public NetNode(string path, uint[] address)
    {
        Path = path;
        Address = address;
    }

    internal void Init(object rootRef, object[] chain)
    {
        _rootRef = rootRef;
        _chain = chain;
    }

    internal void SetPrimitiveLeaf(object rootRef, object[] chain)
    {
        IsLeaf = true;
        Init(rootRef, chain);
    }

    internal void SetNullClassLeaf(object rootRef, object[] chain)
    {
        IsLeaf = true;
        Init(rootRef, chain);
    }

    public void AddChild(uint id, NetNode child) => _children[id] = child;

    public NetNode? Resolve(uint[] address, int depth = 0)
    {
        if (depth == address.Length)
            return this;

        if (_children.TryGetValue(address[depth], out NetNode? child))
            return child.Resolve(address, depth + 1);

        return null;
    }

    public object? GetValue()
    {
        //if (!IsLeaf)
        //    throw new InvalidOperationException($"Node '{Path}' is not a leaf.");

        (object? parent, object lastStep) = WalkChain();

        return ApplyRead(parent, lastStep);
    }

    public void SetValue(object? value)
    {
        //if (!IsLeaf)
        //    throw new InvalidOperationException($"Node '{Path}' is not a leaf.");

        (object? parent, object lastStep) = WalkChain();

        ApplyWrite(parent, lastStep, value);
    }

    private (object? parent, object lastStep) WalkChain()
    {
        if (_chain.Length == 0)
            throw new InvalidOperationException($"Node '{Path}' has no reflection chain.");

        object? current = _rootRef;

        for (int i = 0; i < _chain.Length - 1; i++)
        {
            if (current == null)
                throw new NullReferenceException(
                    $"Node '{Path}': object at chain step {i} is null – cannot traverse further.");

            current = ApplyRead(current, _chain[i]);
        }

        return (current, _chain[^1]);
    }

    private static object? ApplyRead(object? obj, object step)
    {
        if (obj == null)
            return null; //throw new NullReferenceException($"Cannot apply step '{step}' on a null object.");

        return step switch
        {
            FieldInfo fi => fi.GetValue(obj),
            MultiIndex mi => ReadArray(obj, mi.Indices),
            ListIndex li => ReadList(obj, li.Index),
            DictKey dk => ReadDict(obj, dk.Key),
            _ => throw new InvalidOperationException($"Unknown chain step type: {step.GetType()}")
        };
    }

    private static void ApplyWrite(object? obj, object step, object? value)
    {
        if (obj == null)
            throw new NullReferenceException($"Cannot apply step '{step}' on a null object.");

        switch (step)
        {
            case FieldInfo fi:
                Type ft = fi.FieldType;
                fi.SetValue(obj, value == null ? null : Convert.ChangeType(value, ft));
                break;

            case MultiIndex mi:
                WriteArray(obj, mi.Indices, value);
                break;

            case ListIndex li:
                WriteList(obj, li.Index, value);
                break;

            case DictKey dk:
                WriteDict(obj, dk.Key, value);
                break;

            default:
                throw new InvalidOperationException($"Unknown chain step type: {step.GetType()}");
        }
    }

    private static object? ReadArray(object container, int[] indices)
    {
        if (container is not Array arr)
            throw new InvalidOperationException(
                $"Expected Array for MultiIndex step, got '{container.GetType()}'.");

        return arr.GetValue(indices);
    }

    private static void WriteArray(object container, int[] indices, object? value)
    {
        if (container is not Array arr)
            throw new InvalidOperationException(
                $"Expected Array for MultiIndex step, got '{container.GetType()}'.");

        Type et = arr.GetType().GetElementType()!;
        arr.SetValue(value == null ? null : Convert.ChangeType(value, et), indices);
    }

    private static object? ReadList(object container, int index)
    {
        if (container is IList list)
            return list[index];

        throw new InvalidOperationException(
            $"Expected IList for ListIndex step, got '{container.GetType()}'.");
    }

    private static void WriteList(object container, int index, object? value)
    {
        if (container is IList list)
        { list[index] = value; return; }

        throw new InvalidOperationException(
            $"Expected IList for ListIndex step, got '{container.GetType()}'.");
    }

    private static object? ReadDict(object container, object key)
    {
        if (container is IDictionary dict)
            return dict[key];

        throw new InvalidOperationException(
            $"Expected IDictionary for DictKey step, got '{container.GetType()}'.");
    }

    private static void WriteDict(object container, object key, object? value)
    {
        if (container is IDictionary dict)
        { dict[key] = value; return; }

        throw new InvalidOperationException(
            $"Expected IDictionary for DictKey step, got '{container.GetType()}'.");
    }

    public void Print(int indent = 0)
    {
        string pad = new string(' ', indent * 2);
        string leaf = "";

        if (IsLeaf)
        {
            try { leaf = $" = {GetValue() ?? "null"}"; }
            catch { leaf = " = <error>"; }
        }

        Console.WriteLine($"{pad}[{string.Join(",", Address)}]  {Path}{leaf}");

        foreach (KeyValuePair<uint, NetNode> kv in _children)
            kv.Value.Print(indent + 1);
    }
}
