using System;
using System.Collections.Generic;

namespace KapNet
{
    public class PacketFactory
    {
        private Dictionary<PacketType, uint> packetTypeID = new Dictionary<PacketType, uint>();

        public PacketFactory()
        { }

        public (byte[] data, uint packetId) Create(PacketType type, byte[] payload = null, PacketMetaData metaData = PacketMetaData.None, uint? forcedID = null)
        {
            if (!packetTypeID.ContainsKey(type))
                packetTypeID.Add(type, 0);

            uint idToUse = forcedID ?? ++packetTypeID[type];

            payload = payload ?? Array.Empty<byte>();

            using (PacketWriter writer = new PacketWriter())
            {
                writer.Write((int)type);
                writer.Write(idToUse);
                writer.Write((int)metaData);
                writer.Write(payload);

                byte[] data = writer.GetBytes();

                int checkSum1 = PacketUtility.CalculateCheckSum(data, 0, data.Length - 8);
                int checkSum2 = PacketUtility.CalculateCheckSum(data, 0, data.Length - 4);

                writer.Write(checkSum1);
                writer.Write(checkSum2);

                return (writer.GetBytes(), idToUse);
            }
        }
    }
}