using KapNet.src.time;
using System;
using System.Collections.Generic;
using System.Net;

namespace KapNet.src
{
    internal class PacketResender
    {
        private Dictionary<PacketType, List<PacketAwaitingResponce>> packetsAwaitingResponce = new Dictionary<PacketType, List<PacketAwaitingResponce>>();

        private INetworkPeer networkPeer;

        public PacketResender(INetworkPeer networkPeer)
        {
            this.networkPeer = networkPeer;
        }

        public void Tick()
        {
            foreach (KeyValuePair<PacketType, List<PacketAwaitingResponce>> packetsType in packetsAwaitingResponce)
            {
                List<PacketAwaitingResponce> currentList = packetsType.Value;

                for (int i = 0; i < currentList.Count; ++i)
                {
                    PacketAwaitingResponce packet = currentList[i];

                    DateTime dateTime = DateTime.Now;

                    if (Math.Abs((dateTime - packet.lastTimeSent).TotalSeconds) > 3)
                    {
                        if (!networkPeer.IsConnected)
                            networkPeer.SendRaw(packet.data, packet.ipEndPoint);
                        else
                            networkPeer.SendRaw(packet.data);

                        packet.lastTimeSent = dateTime;
                    }
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
