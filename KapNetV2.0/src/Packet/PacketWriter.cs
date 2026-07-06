using ImageCampus.ToolBox.Services;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public class PacketWriter : IDisposable, IService
{
    public bool IsPersistance => false;

    private MemoryStream ms;
    private BinaryWriter writer;

    public PacketWriter()
    {
        ms = new MemoryStream();
        writer = new BinaryWriter(ms);
    }

    public void Write(params object[] data)
    {
        foreach (object o in data)
        {
            switch (o)
            {
                case System.Enum e:
                    Write(System.Convert.ChangeType(e, System.Enum.GetUnderlyingType(e.GetType())));
                    break;
                case object[] nested:
                    Write(nested);
                    break;
                case sbyte sb: Write(sb); break;
                case byte b: Write(b); break;
                case bool b: Write(b); break;
                case short s: Write(s); break;
                case ushort us: Write(us); break;
                case uint ui: Write(ui); break;
                case int i: Write(i); break;
                case long l: Write(l); break;
                case ulong ul: Write(ul); break;
                case float f: Write(f); break;
                case double d: Write(d); break;
                case decimal dec: Write(dec); break;
                case char c: Write(c); break;
                case string str: Write(str); break;
                case byte[] ba: Write(ba); break;
                case uint[] uia: Write<uint>(uia); break;
                case float[] ba: Write<float>(ba); break;
                case bool[] ba: Write<bool>(ba); break;
                case int[] ia: Write<int>(ia); break;
                case short[] sa: Write<short>(sa); break;
                case long[] la: Write<long>(la); break;
                case double[] da: Write<double>(da); break;
                default:
                    throw new NotSupportedException($"Type {o.GetType()} is not supported by PacketWriter.");
            }
        }
    }

    public void Write(sbyte value) => writer.Write(value);
    public void Write(byte value) => writer.Write(value);
    public void Write(bool value) => writer.Write(value);
    public void Write(short value) => writer.Write(value);
    public void Write(ushort value) => writer.Write(value);
    public void Write(uint value) => writer.Write(value);
    public void Write(int value) => writer.Write(value);
    public void Write(long value) => writer.Write(value);
    public void Write(ulong value) => writer.Write(value);
    public void Write(float value) => writer.Write(value);
    public void Write(double value) => writer.Write(value);
    public void Write(char value) => writer.Write((ushort)value);
    public void Write(decimal value) => writer.Write(value);

    public void Write(byte[] value)
    {
        Write(value.Length);
        WriteRaw(value);
    }

    public void WriteRaw(byte[] value)
    {
        writer.Write(value);
    }

    public void Write(string value)
    {
        byte[] stringBytes = Encoding.UTF8.GetBytes(value);
        Write(stringBytes);
    }

    public void Write<T>(T[] values) where T : unmanaged
    {
        Write(values.Length);
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(values.AsSpan());
        writer.Write(bytes.ToArray());
    }

    public byte[] GetBytes() => ms.ToArray();

    public void Reset()
    {
        ms.SetLength(0);
        ms.Position = 0;
    }

    public void Dispose()
    {
        writer.Close();
        ms.Dispose();
    }
}