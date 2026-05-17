using System;
using System.IO;
using System.Text;

public class PacketWriter : IDisposable
{
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
                case byte b: Write(b); break;
                case bool b: Write(b); break;
                case short s: Write(s); break;
                case uint ui: Write(ui); break;
                case int i: Write(i); break;
                case float f: Write(f); break;
                case long l: Write(l); break;
                case double d: Write(d); break;
            }
        }
    }

    public void Write(byte value) => writer.Write(value);
    public void Write(bool value) => writer.Write(value);
    public void Write(short value) => writer.Write(value);
    public void Write(uint value) => writer.Write(value);
    public void Write(int value) => writer.Write(value);
    public void Write(float value) => writer.Write(value);
    public void Write(long value) => writer.Write(value);
    public void Write(double value) => writer.Write(value);

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