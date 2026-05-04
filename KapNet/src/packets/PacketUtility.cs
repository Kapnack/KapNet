using System;

namespace KapNet
{
    internal static class PacketUtility
    {
        public static int CalculateCheckSum(byte[] data, int startOffset = 0, int endOffset = 0)
        {
            int checksum = 0;

            for (int i = startOffset; i < data.Length - endOffset; ++i)
                checksum ^= data[i] << (i & 3) << 3;

            return checksum;
        }

        public static byte[] Combine(params byte[][] arrays)
        {
            int totalLength = 0;

            foreach (byte[] arr in arrays)
                totalLength += arr.Length;

            byte[] result = new byte[totalLength];

            int offset = 0;

            foreach (byte[] arr in arrays)
            {
                Buffer.BlockCopy(arr, 0, result, offset, arr.Length);
                offset += arr.Length;
            }

            return result;
        }

        public static PacketType GetType(byte[] data)
        {
            return (PacketType)BitConverter.ToUInt32(data, PacketLayout.PacketTypeOffSet);
        }

        public static uint GetPacketID(byte[] data)
        {
            return BitConverter.ToUInt32(data, PacketLayout.PacketIDOffSet);
        }

        public static PacketMetaData GetMetaData(byte[] data)
        {
            return (PacketMetaData)BitConverter.ToUInt32(data, PacketLayout.PacketMetaDataOffSet);
        }

        public static uint GetClientID(byte[] data)
        {
            return BitConverter.ToUInt32(data, PacketLayout.PacketIDOffSet);
        }

        public static int GetCheckSum1(byte[] data)
        {
            return BitConverter.ToInt32(data, data.Length - PacketLayout.CheckSum1EndOffSet);
        }

        public static int GetCheckSum2(byte[] data)
        {
            return BitConverter.ToInt32(data, data.Length - PacketLayout.CheckSum2EndOffSet);
        }

        public static byte[] GetPayload(byte[] data)
        {
            int payloadSize = data.Length - PacketLayout.PacketConstSpace;
            byte[] payload = new byte[payloadSize];

            Buffer.BlockCopy(data, PacketLayout.PacketPayloadOffSet, payload, 0, payloadSize);

            return payload;
        }
    }
}
