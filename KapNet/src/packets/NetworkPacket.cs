using System.Net;

namespace KapNet
{
    public class NetworkPacket
    {
        public PacketType type;
        public uint packetID;
        public PacketMetaData metaData;
        public byte[] payload;
        public IPEndPoint ipEndPoint;
        public uint clientID;

        public NetworkPacket(PacketType type, uint packetID, PacketMetaData metaData, byte[] payload, IPEndPoint ipEndPoint = null)
        {
            this.type = type;
            this.packetID = packetID;
            this.metaData = metaData;
            this.payload = payload;
            this.ipEndPoint = ipEndPoint;
        }
    }
}