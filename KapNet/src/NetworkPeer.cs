using KapNet.src.time;
using System;
using System.Collections.Generic;
using System.Net;

namespace KapNet.src
{
    public abstract class NetworkPeer<ClientKey> : IReceiveData, INetworkPeer
    {
        protected delegate void PacketTypeDelegate(NetworkPacket networkPacket);
        private delegate void SendPacketMetaDataDelegate(NetworkPacket networkPacket, byte[] data);
        private delegate bool RecivePacketMetaDataDelegate(NetworkPacket networkPacket);

        protected const uint NULL_NETWORKPEER = 0;

        PacketResender packetResender;

        private List<byte[]> cryticalPackets = new List<byte[]>();

        private Dictionary<ClientKey, Dictionary<PacketType, SortedDictionary<uint, NetworkPacket>>> ordenablePackets = new Dictionary<ClientKey, Dictionary<PacketType, SortedDictionary<uint, NetworkPacket>>>();
        private Dictionary<ClientKey, Dictionary<PacketType, uint>> lastPacketUsed = new Dictionary<ClientKey, Dictionary<PacketType, uint>>();
        private PackectsUsedRegistry<ClientKey, PacketType> packectsUsedRegistry = new PackectsUsedRegistry<ClientKey, PacketType>();

        public bool IsConnected { get; private set; }

        protected Dictionary<PacketType, PacketTypeDelegate> PacketTypeStrategy { get; private set; }
        private Dictionary<PacketMetaData, SendPacketMetaDataDelegate> sendingMetaDataStrategy;
        private Dictionary<PacketMetaData, RecivePacketMetaDataDelegate> recivingMetaDataStrategy;

        private PacketFactory packetFactory = new PacketFactory();

        private UdpConnection connection;

        public NetworkPeer()
        {
            packetResender = new PacketResender(this);

            IsConnected = false;

            PacketTypeStrategy = new Dictionary<PacketType, PacketTypeDelegate>()
            {
                { PacketType.Handshake, HandleHandShake },
                { PacketType.Ping, HandlePing },
                { PacketType.ClientLeft, HandleClientLeft },
                { PacketType.Acknowledgement, HandleAcknowledgement }
            };

            sendingMetaDataStrategy = new Dictionary<PacketMetaData, SendPacketMetaDataDelegate>()
            {
                { PacketMetaData.Reliable, HandleReliableMessageSend },
                { PacketMetaData.Crytical, HandleCriticalMessageSend }
            };

            recivingMetaDataStrategy = new Dictionary<PacketMetaData, RecivePacketMetaDataDelegate>()
            {
                {PacketMetaData.Reliable, HandleReliablePacketRecived },
                {PacketMetaData.Ordenable, HandleOrdenablePacketRecived },
                {PacketMetaData.Crytical, HandleCriticalPacketRecived }
            };
        }

        public virtual void Tick()
        {
            if (connection != null)
                connection.FlushReceiveData();

            packetResender.Tick();
            packectsUsedRegistry.Tick();
        }

        public void Connect(string ip, int port)
        {
            packectsUsedRegistry.Clear();
            packetResender.Clear();

            IsConnected = true;

            if (connection != null)
                connection.Close();

            IPAddress iPAddress = IPAddress.Parse(ip);
            connection = new UdpConnection(iPAddress, port, this);
        }

        public void Connect(IPAddress ipAddress, int port)
        {
            packectsUsedRegistry.Clear();
            packetResender.Clear();

            IsConnected = true;

            if (connection != null)
                connection.Close();

            connection = new UdpConnection(ipAddress, port, this);
        }

        public void Connect(int port)
        {
            packectsUsedRegistry.Clear();
            packetResender.Clear();

            if (connection != null)
                connection.Close();

            connection = new UdpConnection(port, this);
        }

        public virtual void OnReceiveData(byte[] data, IPEndPoint ipEndpoint)
        {
            PacketType type = PacketUtility.GetType(data);
            uint packetID = PacketUtility.GetPacketID(data);
            PacketMetaData metaData = PacketUtility.GetMetaData(data);
            byte[] payload = PacketUtility.GetPayload(data);

            NetworkPacket networkPacket = new NetworkPacket
            (
                data,
                type,
                packetID,
                metaData,
                payload,
                ipEndpoint
            );

            if (!HandleRecivedMetaData(networkPacket))
                return;

            if (PacketTypeStrategy.TryGetValue(networkPacket.type, out PacketTypeDelegate handler))
                handler(networkPacket);
        }

        public void Send(IPEndPoint ip, PacketType type, byte[] payload = null, PacketMetaData metaData = PacketMetaData.None)
        {
            (byte[] data, uint packetId) = packetFactory.Create(type, payload, metaData);

            NetworkPacket networkPacket = new NetworkPacket
            (
                data,
                type,
                packetId,
                metaData,
                payload,
                ip
            );

            HandleSendMetaData(networkPacket, data);

            SendRaw(data, ip);
        }

        public void Send(PacketType type, byte[] payload = null, PacketMetaData metaData = PacketMetaData.None)
        {
            (byte[] data, uint packetId) = packetFactory.Create(type, payload, metaData);

            NetworkPacket networkPacket = new NetworkPacket
            (
                data,
                type,
                packetId,
                metaData,
                payload
            );

            HandleSendMetaData(networkPacket, data);

            SendRaw(data);
        }

        private bool HandleRecivedMetaData(NetworkPacket packet)
        {
            bool handle = true;

            foreach (KeyValuePair<PacketMetaData, RecivePacketMetaDataDelegate> strategy in recivingMetaDataStrategy)
            {
                if (packet.metaData.HasFlag(strategy.Key))
                {
                    if (!strategy.Value(packet))
                        handle = false;
                }
            }

            return handle;
        }

        private void HandleSendMetaData(NetworkPacket packet, byte[] data)
        {
            foreach (KeyValuePair<PacketMetaData, SendPacketMetaDataDelegate> strategy in sendingMetaDataStrategy)
            {
                if (packet.metaData.HasFlag(strategy.Key))
                {
                    strategy.Value(packet, data);
                }
            }
        }

        public void SendRaw(byte[] data, IPEndPoint ip)
        {
            connection.Send(data, ip);
        }

        public void SendRaw(byte[] data)
        {
            connection.Send(data);
        }

        protected void Disconnect()
        {
            connection.Close();
            connection = null;
            IsConnected = false;
        }

        protected abstract void HandleHandShake(NetworkPacket networkPacket);
        protected abstract void HandlePing(NetworkPacket networkPacket);
        protected abstract void HandleClientLeft(NetworkPacket networkPacket);

        private void HandleAcknowledgement(NetworkPacket networkPacket)
        {
            PacketType packetType = (PacketType)BitConverter.ToInt32(networkPacket.payload, 0);
            uint packetID = BitConverter.ToUInt32(networkPacket.payload, sizeof(PacketType));
            packetResender.Remove(packetType, packetID);
        }

        private void HandleReliableMessageSend(NetworkPacket networkPacket, byte[] data)
        {
            packetResender.Add(networkPacket.type, data, networkPacket.packetID, networkPacket.ipEndPoint);
        }

        private void HandleCriticalMessageSend(NetworkPacket packet, byte[] data)
        {

        }

        private bool HandleReliablePacketRecived(NetworkPacket networkPacket)
        {
            uint clientID = networkPacket.payload.Length != 0 ? BitConverter.ToUInt32(networkPacket.payload, 0) : 0;

            byte[] payload = new byte[sizeof(PacketType) + sizeof(uint)];

            BitConverter.GetBytes((int)networkPacket.type).CopyTo(payload, 0);
            BitConverter.GetBytes(networkPacket.packetID).CopyTo(payload, sizeof(uint));

            if (IsConnected)
                Send(PacketType.Acknowledgement, payload);
            else
                Send(networkPacket.ipEndPoint, PacketType.Acknowledgement, payload);

            ClientKey clientKey = typeof(ClientKey) == typeof(IPEndPoint) ? (ClientKey)(object)networkPacket.ipEndPoint : (ClientKey)(object)clientID;

            bool shouldBeProcess = false;

            if (packectsUsedRegistry.ContainsPacket(clientKey, networkPacket.type, networkPacket.packetID))
                shouldBeProcess = false;
            else
                shouldBeProcess = true;

            packectsUsedRegistry.SetPacket(clientKey, networkPacket.type, networkPacket.packetID);

            return shouldBeProcess;
        }

        private bool HandleOrdenablePacketRecived(NetworkPacket networkPacket)
        {
            ClientKey clientKey = typeof(ClientKey) == typeof(IPEndPoint) ? (ClientKey)(object)networkPacket.ipEndPoint : (ClientKey)(object)BitConverter.ToUInt32(networkPacket.payload, 0);

            if (!ordenablePackets.TryGetValue(clientKey, out Dictionary<PacketType, SortedDictionary<uint, NetworkPacket>> clientsPackets))
            {
                clientsPackets = new Dictionary<PacketType, SortedDictionary<uint, NetworkPacket>>();
                ordenablePackets[clientKey] = clientsPackets;
            }

            if (!clientsPackets.TryGetValue(networkPacket.type, out SortedDictionary<uint, NetworkPacket> packets))
            {
                packets = new SortedDictionary<uint, NetworkPacket>();
                clientsPackets[networkPacket.type] = packets;
            }

            packets[networkPacket.packetID] = networkPacket;

            if (!lastPacketUsed.TryGetValue(clientKey, out Dictionary<PacketType, uint> lastPackets))
            {
                lastPackets = new Dictionary<PacketType, uint>();
                lastPacketUsed[clientKey] = lastPackets;
            }

            if (!lastPackets.ContainsKey(networkPacket.type))
            {
                lastPackets[networkPacket.type] = 0;
            }

            while (packets.TryGetValue(lastPackets[networkPacket.type] + 1, out NetworkPacket nextPacket))
            {
                if (PacketTypeStrategy.TryGetValue(networkPacket.type, out PacketTypeDelegate handler))
                    handler(nextPacket);

                ++lastPackets[networkPacket.type];
            }

            return false;
        }

        private bool HandleCriticalPacketRecived(NetworkPacket networkPacket)
        {
            return false;
        }
    }
}
