using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using ImageCampus.ToolBox.Services;

namespace Net
{
    internal class PacketResender
    {
        private const double ResendAfterSeconds = 3d;

        private Dictionary<PacketType, List<PacketAwaitingResponce>> packetsAwaitingResponce = new Dictionary<PacketType, List<PacketAwaitingResponce>>();

        private UdpConnection connection;

        private static readonly FieldInfo ConnectionField =
            typeof(ConnectionHandler).GetField("connection", BindingFlags.Instance | BindingFlags.NonPublic);

        public PacketResender()
        {
        }

        public void SetConnection(UdpConnection connection)
        {
            this.connection = connection;
        }

        private UdpConnection ResolveConnection()
        {
            if (connection != null)
                return connection;

            if (!ServiceProvider.Instance.ContainsService<ConnectionHandler>() || ConnectionField == null)
                return null;

            ConnectionHandler handler = ServiceProvider.Instance.GetService<ConnectionHandler>();
            connection = ConnectionField.GetValue(handler) as UdpConnection;
            return connection;
        }

        public void Tick()
        {
            UdpConnection udpConnection = ResolveConnection();
            if (udpConnection == null)
                return;

            DateTime now = DateTime.UtcNow;

            foreach (KeyValuePair<PacketType, List<PacketAwaitingResponce>> packetsType in packetsAwaitingResponce)
            {
                List<PacketAwaitingResponce> currentList = packetsType.Value;

                for (int i = 0; i < currentList.Count; ++i)
                {
                    PacketAwaitingResponce packet = currentList[i];

                    if ((now - packet.lastTimeSent).TotalSeconds <= ResendAfterSeconds)
                        continue;

                    if (packet.ipEndPoint != null)
                        udpConnection.Send(packet.data, packet.ipEndPoint);
                    else
                        udpConnection.Send(packet.data);

                    packet.lastTimeSent = now;
                }
            }
        }

        public void Add(PacketType packetType, PacketAwaitingResponce packet)
        {
            if (!packetsAwaitingResponce.ContainsKey(packetType))
                packetsAwaitingResponce[packetType] = new List<PacketAwaitingResponce>();

            packetsAwaitingResponce[packetType].Add(packet);
        }

        public void Add(PacketType packetType, byte[] data, uint packetID, IPEndPoint reciver = null)
        {
            if (!packetsAwaitingResponce.ContainsKey(packetType))
                packetsAwaitingResponce[packetType] = new List<PacketAwaitingResponce>();

            packetsAwaitingResponce[packetType].Add(new PacketAwaitingResponce(
              data,
              packetID,
              reciver,
              DateTime.UtcNow
          ));
        }

        public void Remove(PacketType packetType, uint packetID)
        {
            if (!packetsAwaitingResponce.ContainsKey(packetType))
                return;

            packetsAwaitingResponce[packetType].RemoveAll(p => p.packetID == packetID);
        }

        public void Clear()
        {
            packetsAwaitingResponce.Clear();
        }
    }
}
