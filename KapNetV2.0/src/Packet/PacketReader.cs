using ImageCampus.ToolBox.Services;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Net
{
    internal class PacketReader : IDisposable, IService
    {
        public bool IsPersistance => false;

        private byte[] data;
        private int position = 0;

        MethodInfo readUnmanageByType;

        public PacketReader(byte[] data) : this()
        {
            AssignData(data);
        }

        public PacketReader()
        {
            readUnmanageByType = GetType().GetMethod(nameof(ReadUnmanaged), BindingFlags.NonPublic | BindingFlags.Instance);
        }

        internal void AssignData(byte[] data)
        {
            this.data = data;
            position = 0;
        }

        public sbyte ReadSByte()
        {
            sbyte value = (sbyte)data[position];
            position += sizeof(sbyte);

            return value;
        }

        public byte ReadByte()
        {
            byte value = data[position];
            position += sizeof(byte);

            return value;
        }

        public char ReadChar()
        {
            char value = BitConverter.ToChar(data, position);
            position += sizeof(char);

            return value;
        }

        public ushort ReadUShort()
        {
            ushort value = BitConverter.ToUInt16(data, position);
            position += sizeof(ushort);

            return value;
        }

        public decimal ReadDecimal()
        {
            int[] bits = new int[4];
            for (int i = 0; i < 4; i++)
            {
                bits[i] = BitConverter.ToInt32(data, position);
                position += sizeof(int);
            }

            return new decimal(bits);
        }

        public bool ReadBool()
        {
            bool value = BitConverter.ToBoolean(data, position);
            position += sizeof(bool);

            return value;
        }

        public short ReadShort()
        {
            short value = BitConverter.ToInt16(data, position);
            position += sizeof(short);

            return value;
        }
        public uint ReadUInt()
        {
            uint value = BitConverter.ToUInt32(data, position);
            position += sizeof(uint);

            return value;
        }

        public int ReadInt()
        {
            int value = BitConverter.ToInt32(data, position);
            position += sizeof(int);

            return value;
        }

        public float ReadFloat()
        {
            float value = BitConverter.ToSingle(data, position);
            position += sizeof(float);

            return value;
        }

        public long ReadLong()
        {
            long value = BitConverter.ToInt64(data, position);
            position += sizeof(long);

            return value;
        }

        public ulong ReadULong()
        {
            ulong value = BitConverter.ToUInt64(data, position);
            position += sizeof(ulong);

            return value;
        }

        public double ReadDouble()
        {
            double value = BitConverter.ToDouble(data, position);
            position += sizeof(double);

            return value;
        }

        public string ReadString()
        {
            int length = ReadInt();
            string value = System.Text.Encoding.UTF8.GetString(data, position, length);
            position += length;

            return value;
        }

        public byte[] ReadBytes(int length)
        {
            byte[] result = new byte[length];
            Buffer.BlockCopy(data, position, result, 0, length);
            position += length;
            return result;
        }

        public byte[] ReadBytes()
        {
            int length = ReadInt();
            return ReadBytes(length);
        }

        public PrimitiveType[] ReadArray<PrimitiveType>() where PrimitiveType : unmanaged
        {
            int length = ReadInt();
            return ReadArray<PrimitiveType>(length);
        }

        public PrimitiveType[] ReadArray<PrimitiveType>(int length) where PrimitiveType : unmanaged
        {
            int totalBytes = length * Marshal.SizeOf(typeof(PrimitiveType));

            PrimitiveType[] result = new PrimitiveType[length];

            MemoryMarshal.Cast<byte, PrimitiveType>(data.AsSpan(position, totalBytes)).CopyTo(result);

            position += totalBytes;

            return result;
        }

        public byte[] GetRemaining()
        {
            int remainingCount = data.Length - position;

            if (remainingCount <= 0)
                return Array.Empty<byte>();

            byte[] remaining = new byte[remainingCount];

            Buffer.BlockCopy(data, position, remaining, 0, remainingCount);

            position = data.Length;

            return remaining;
        }

        public T Read<T>()
        {
            Type type = typeof(T);

            if (type == typeof(string))
                return (T)(object)ReadString();

            if (type.IsEnum)
            {
                Type underlyingType = Enum.GetUnderlyingType(type);
                object underlyingValue = readUnmanageByType.MakeGenericMethod(underlyingType).Invoke(this, null);
                return (T)Enum.ToObject(type, underlyingValue);
            }

            if (type.IsArray)
            {
                Type elementType = type.GetElementType();

                int length = ReadInt();

                int elementSize = Marshal.SizeOf(elementType);
                int totalBytes = length * elementSize;

                Array array = Array.CreateInstance(elementType, length);

                var span = data.AsSpan(position, totalBytes);

                Buffer.BlockCopy(span.ToArray(), 0, array, 0, totalBytes);

                position += totalBytes;

                return (T)(object)array;
            }

            return (T)readUnmanageByType.MakeGenericMethod(type).Invoke(this, null);
        }

        private T ReadUnmanaged<T>() where T : unmanaged
        {
            int size = Marshal.SizeOf<T>();

            T result = MemoryMarshal.Read<T>(data.AsSpan(position, size));

            position += size;

            return result;
        }

        public bool HasData() => position < data.Length;

        public void Dispose()
        {
            data = null;
            position = 0;
        }
    }
}
