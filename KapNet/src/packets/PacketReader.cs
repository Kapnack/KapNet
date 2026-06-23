using System;
using System.IO;
using System.Runtime;
using System.Runtime.InteropServices;

namespace KapNet.src.packets
{
    public class PacketReader : IDisposable
    {
        private byte[] data;
        private int position = 0;

        public PacketReader(byte[] data)
        {
            this.data = data;
        }

        public PacketReader()
        {
        }

        internal void AssignData(byte[] data)
        {
            this.data = data;
            position = 0;
        }

        public byte ReadByte()
        {
            byte value = data[position];
            position += sizeof(byte);

            return value;
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

        public bool HasData() => position < data.Length;

        public void Dispose()
        {
            data = null;
            position = 0;
        }
    }
}
