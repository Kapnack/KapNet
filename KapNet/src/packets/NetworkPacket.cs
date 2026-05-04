using System;
using System.Net;

namespace KapNet
{
    public class NetworkPacket
    {
        public byte[] rawPacket;
        public PacketType type;
        public uint packetID;
        public PacketMetaData metaData;
        public byte[] payload;
        public IPEndPoint ipEndPoint;

        public NetworkPacket(byte[] rawPacket, PacketType type, uint packetID, PacketMetaData metaData, byte[] payload, IPEndPoint ipEndPoint = null)
        {
            this.rawPacket = rawPacket;
            this.type = type;
            this.packetID = packetID;
            this.metaData = metaData;
            this.payload = payload;
            this.ipEndPoint = ipEndPoint;
        }
    }
}