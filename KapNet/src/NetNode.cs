using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

public interface IAccessor
{
    object GetValue(object container);
    void SetValue(object container, object value);
}

public sealed class FieldAccessor : IAccessor
{
    public readonly FieldInfo Field;
    public FieldAccessor(FieldInfo field) => Field = field;

    public object GetValue(object container) => Field.GetValue(container);

    public void SetValue(object container, object value)
    {
        Type ft = Field.FieldType;
        Field.SetValue(container, value == null ? null : Convert.ChangeType(value, ft));
    }

    public override string ToString() => Field.Name;
}

public sealed class ArrayAccessor : IAccessor
{
    public readonly int[] Indices;
    public ArrayAccessor(int[] indices) => Indices = indices;

    public object GetValue(object container)
    {
        if (!(container is Array arr))
            throw new InvalidOperationException($"Expected Array, got '{container.GetType()}'.");

        return arr.GetValue(Indices);
    }

    public void SetValue(object container, object value)
    {
        if (!(container is Array arr))
            throw new InvalidOperationException($"Expected Array, got '{container.GetType()}'.");

        Type et = arr.GetType().GetElementType();
        arr.SetValue(value == null ? null : Convert.ChangeType(value, et), Indices);
    }

    public override string ToString() => $"[{string.Join(",", Indices)}]";
}

public sealed class ListAccessor : IAccessor
{
    public readonly int Index;
    public ListAccessor(int index) => Index = index;

    public object GetValue(object container)
    {
        if (container is IList list)
            return list[Index];

        throw new InvalidOperationException($"Expected IList, got '{container.GetType()}'.");
    }

    public void SetValue(object container, object value)
    {
        if (container is IList list)
        {
            list[Index] = value;
            return;
        }

        throw new InvalidOperationException($"Expected IList, got '{container.GetType()}'.");
    }

    public override string ToString() => $"[{Index}]";
}

public sealed class DictAccessor : IAccessor
{
    public readonly int Index;
    public DictAccessor(int index) => Index = index;

    public object GetValue(object container)
    {
        if (!(container is IDictionary dict))
            throw new InvalidOperationException($"Expected IDictionary, got '{container.GetType()}'.");

        if (Index < 0 || Index >= dict.Count)
            throw new IndexOutOfRangeException($"Index {Index} is out of range for dictionary of size {dict.Count}.");

        object[] values = new object[dict.Count];
        dict.Values.CopyTo(values, 0);
        return values[Index];
    }

    public void SetValue(object container, object value)
    {
        if (!(container is IDictionary dict))
            throw new InvalidOperationException($"Expected IDictionary, got '{container.GetType()}'.");

        if (Index < 0 || Index >= dict.Count)
            throw new IndexOutOfRangeException($"Index {Index} is out of range for dictionary of size {dict.Count}.");

        object[] keys = new object[dict.Count];
        dict.Keys.CopyTo(keys, 0);
        object key = keys[Index];

        Type valueType = typeof(object);
        Type dictType = dict.GetType();
        foreach (Type iface in dictType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            {
                valueType = iface.GetGenericArguments()[1];
                break;
            }
        }

        dict[key] = value == null ? null : Convert.ChangeType(value, valueType);
    }

    public override string ToString() => $"[idx:{Index}]";
}

public class NetNode
{
    public string Path { get; private set; }
    public uint[] Address { get; private set; }

    public bool IsLeaf => _children == null || _children.Count == 0;

    private IAccessor[] _chain;

    private int? _hashCode;

    private readonly Dictionary<uint, NetNode> _children;

    public NetNode(string path, uint[] address, int? hashCode)
    {
        Path = path;
        Address = address;
        _chain = Array.Empty<IAccessor>();
        _children = new Dictionary<uint, NetNode>();
        _hashCode = hashCode;
    }

    internal void Init(IAccessor[] chain)
    {
        _chain = chain;
    }

    internal void SetPrimitiveLeaf(IAccessor[] chain) => Init(chain);

    internal void SetNullClassLeaf(IAccessor[] chain) => Init(chain);

    public void AddChild(uint id, NetNode child) => _children[id] = child;

    public NetNode Resolve(uint[] address, int depth = 0)
    {
        if (depth == address.Length)
            return this;

        if (_children.TryGetValue(address[depth], out NetNode child))
            return child.Resolve(address, ++depth);

        return null;
    }

    //TODO: This should be optimize in the future.
    public bool GetPath(object instance, out uint[] path)
    {
        if (!_hashCode.Equals(instance.GetHashCode()))
            foreach (KeyValuePair<uint, NetNode> child in _children)
            {
                if (child.Value.GetPath(instance, out path))
                    return true;
            }

        path = Address;
        return false;
    }

    public object GetValue(object rootRef)
    {
        if (!IsLeaf)
            throw new InvalidOperationException($"Node '{Path}' is not a leaf.");

        return GetValueInternal(rootRef);
    }

    private object GetValueInternal(object rootRef)
    {
        (object parent, IAccessor lastStep) = WalkChain(rootRef);
        return lastStep.GetValue(parent);
    }

    public bool IsDirty(object rootRef)
    {
        return GetValueInternal(rootRef)?.GetHashCode() != _hashCode;
    }

    public void UpdateHashCode(object rootRef)
    {
        _hashCode = GetValueInternal(rootRef)?.GetHashCode();
    }

    public void Tick(object rootRef, Action<object, uint[]> updateValueEvent)
    {
        if (!IsLeaf && GetValueInternal(rootRef) == null)
            _children.Clear();

        if (IsLeaf)
        {
            if (IsDirty(rootRef))
            {
                UpdateHashCode(rootRef);
                updateValueEvent.Invoke(GetValueInternal(rootRef), Address);
            }
        }
        else
        {
            foreach (KeyValuePair<uint, NetNode> child in _children)
                child.Value.Tick(rootRef, updateValueEvent);
        }
    }

    public void SetValue(object rootRef, object value)
    {
        if (!IsLeaf)
            throw new InvalidOperationException($"Node '{Path}' is not a leaf.");

        (object parent, IAccessor lastStep) = WalkChain(rootRef);

        lastStep.SetValue(parent, value);
    }

    private (object parent, IAccessor lastStep) WalkChain(object rootRef)
    {
        if (_chain.Length == 0)
            throw new InvalidOperationException($"Node '{Path}' has no reflection chain.");

        object current = rootRef;

        for (int i = 0; i < _chain.Length - 1; i++)
        {
            if (current == null)
                throw new NullReferenceException($"Node '{Path}': object at chain step {i} is null – cannot traverse further.");

            current = _chain[i].GetValue(current);
        }

        if (current == null)
            throw new NullReferenceException(
                $"Node '{Path}': parent object is null – cannot apply final step '{_chain[_chain.Length - 1]}'.");

        return (current, _chain[_chain.Length - 1]);
    }

    public void Print(object rootRef, int indent = 0)
    {
        string pad = new string(' ', indent * 2);
        string leaf = "";

        if (IsLeaf)
        {
            try { leaf = $" = {GetValue(rootRef) ?? "null"}"; }
            catch { leaf = " = <error>"; }
        }

        Console.WriteLine($"{pad}[{string.Join(",", Address)}]  {Path}{leaf}");

        foreach (KeyValuePair<uint, NetNode> kv in _children)
            kv.Value.Print(rootRef, ++indent);
    }
}
