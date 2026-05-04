using System;
using System.Collections.Generic;

namespace KapNet
{
    internal class PacketFactory
    {
        private Dictionary<PacketType, uint> packetTypeID = new Dictionary<PacketType, uint>();

        public PacketFactory()
        { }

        public (byte[] data, uint packetId) Create(PacketType type, byte[] payload = null, PacketMetaData metaData = PacketMetaData.None)
        {
            if (!packetTypeID.ContainsKey(type))
                packetTypeID.Add(type, 0);

            if (payload == null)
                payload = new byte[0];

            byte[] data = new byte[PacketLayout.PacketConstSpace + payload.Length];

            BitConverter.GetBytes((int)type).CopyTo(data, PacketLayout.PacketTypeOffSet);
            BitConverter.GetBytes(++packetTypeID[type]).CopyTo(data, PacketLayout.PacketIDOffSet);
            BitConverter.GetBytes((int)metaData).CopyTo(data, PacketLayout.PacketMetaDataOffSet);

            Buffer.BlockCopy(payload, 0, data, PacketLayout.PacketPayloadOffSet, payload.Length);

            BitConverter.GetBytes(PacketUtility.CalculateCheckSum(data, 0, PacketLayout.CheckSum1EndOffSet)).CopyTo(data, data.Length - PacketLayout.CheckSum1EndOffSet);
            BitConverter.GetBytes(PacketUtility.CalculateCheckSum(data, 0, PacketLayout.CheckSum2EndOffSet)).CopyTo(data, data.Length - PacketLayout.CheckSum2EndOffSet);

            return (data, packetTypeID[type]);
        }
    }
}