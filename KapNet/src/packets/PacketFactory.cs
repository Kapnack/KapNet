using System;
using System.Collections.Generic;

namespace KapNet
{
    public class PacketFactory
    {
        private Dictionary<PacketType, uint> packetTypeID = new Dictionary<PacketType, uint>();

        PacketWriter writer;

        public PacketFactory()
        {
            writer = new PacketWriter();
        }

        public (byte[] data, uint packetId) Create(PacketType type, byte[] payload = null, PacketMetaData metaData = PacketMetaData.None, uint? forcedID = null)
        {
            writer.Reset();

            if (!packetTypeID.ContainsKey(type))
                packetTypeID.Add(type, 0);

            uint idToUse = forcedID ?? ++packetTypeID[type];

            payload = payload ?? Array.Empty<byte>();

            writer.Write((int)type);
            writer.Write(idToUse);
            writer.Write((int)metaData);
            writer.WriteRaw(payload);

            writer.Write(PacketUtility.CalculateCheckSum(writer.GetBytes()));

            writer.Write(PacketUtility.CalculateCheckSum(writer.GetBytes()));

            return (writer.GetBytes(), idToUse);

        }
    }
}