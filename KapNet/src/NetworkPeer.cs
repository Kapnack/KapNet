using KapNet.src.packets;
using KapNet.src.time;
using System;
using System.Collections.Generic;
using System.Net;

namespace KapNet.src
{
    public abstract class NetworkPeer<ClientKey> : IReceiveData, INetworkPeer
    {
        protected delegate void PacketTypeDelegate(NetworkPacket networkPacket);
        private delegate void SendPacketMetaDataDelegate(NetworkPacket networkPacket, ref byte[] data);
        private delegate bool RecivePacketMetaDataDelegate(ref NetworkPacket networkPacket, byte[] data);

        protected const uint NULL_NETWORKPEER = 0;
        public uint NetworkID = NULL_NETWORKPEER;

        private PacketResender packetResender;
        public PacketEncryptor packetEncryptor;

        private List<byte[]> cryticalPackets = new List<byte[]>();

        private Dictionary<ClientKey, Dictionary<PacketType, SortedDictionary<uint, NetworkPacket>>> ordenablePackets = new Dictionary<ClientKey, Dictionary<PacketType, SortedDictionary<uint, NetworkPacket>>>();
        private Dictionary<ClientKey, Dictionary<PacketType, uint>> lastPacketUsed = new Dictionary<ClientKey, Dictionary<PacketType, uint>>();
        private PackectsUsedRegistry<ClientKey, PacketType> packectsUsedRegistry = new PackectsUsedRegistry<ClientKey, PacketType>();

        protected PacketReader packetReader;
        protected PacketWriter packetWriter;

        public bool IsConnected { get; private set; }

        protected Dictionary<PacketType, PacketTypeDelegate> PacketTypeStrategy { get; private set; }
        private Dictionary<PacketMetaData, SendPacketMetaDataDelegate> sendingMetaDataStrategy;
        private Dictionary<PacketMetaData, RecivePacketMetaDataDelegate> recivingMetaDataStrategy;

        protected PacketFactory packetFactory = new PacketFactory();
        private UdpConnection connection;

        public NetworkPeer()
        {
            packetReader = new PacketReader();
            packetWriter = new PacketWriter();

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
                { PacketMetaData.Crytical, HandleCriticalMessageSend },
                { PacketMetaData.Encrypted, HandleEncryptedSend },
            };

            recivingMetaDataStrategy = new Dictionary<PacketMetaData, RecivePacketMetaDataDelegate>()
            {
                { PacketMetaData.Encrypted, HandleEncryptedRecieved },
                { PacketMetaData.Reliable, HandleReliablePacketRecived },
                { PacketMetaData.Ordenable, HandleOrdenablePacketRecived },
                { PacketMetaData.Crytical, HandleCriticalPacketRecived }
            };
        }

        private bool HandleEncryptedRecieved(ref NetworkPacket networkPacket, byte[] data)
        {
            byte[] iv = packetReader.ReadBytes();
            byte[] encrypted = packetReader.ReadBytes();
            networkPacket.payload = packetEncryptor.Decrypt(encrypted, iv);

            return true;
        }

        private void HandleEncryptedSend(NetworkPacket packet, ref byte[] data)
        {
            if (packet.payload == null || packet.payload.Length == 0)
                return;

            (byte[] encrypted, byte[] iv) = packetEncryptor.Encrypt(packet.payload);

            packetWriter.Write(iv);
            packetWriter.Write(encrypted);
            packet.payload = packetWriter.GetBytes();

            packetWriter.Reset();

            (byte[] newData, uint _) = packetFactory.Create(
                packet.type,
                packet.payload,
                packet.metaData,
                packet.packetID
            );

            data = newData;
        }

        public void Send(IPEndPoint ip, PacketType type, PacketMetaData metaData = PacketMetaData.None, params object[] parameters)
        {
            if (connection == null)
                return;

            packetWriter.Write(parameters);

            byte[] payload = packetWriter.GetBytes();

            packetWriter.Reset();

            (byte[] data, uint packetId) = packetFactory.Create(type, payload, metaData);
            NetworkPacket networkPacket = new NetworkPacket(type, packetId, metaData, payload, ip);

            HandleSendMetaData(networkPacket, ref data);
            SendRaw(data, ip);
        }

        public void Send(PacketType type, PacketMetaData metaData = PacketMetaData.None, params object[] parameters)
        {
            if (connection == null)
                return;

            if (parameters != null && parameters.Length > 0)
                packetWriter.Write(parameters);
            else
                packetWriter.Write(new byte[0]);

            packetWriter.Reset();

            byte[] payload = packetWriter.GetBytes();

            (byte[] data, uint packetId) = packetFactory.Create(type, payload, metaData);
            NetworkPacket networkPacket = new NetworkPacket(type, packetId, metaData, payload);

            HandleSendMetaData(networkPacket, ref data);
            SendRaw(data);
        }

        public virtual void OnReceiveData(byte[] data, IPEndPoint sender)
        {
            PacketType type = PacketUtility.GetType(data);
            uint packetID = PacketUtility.GetPacketID(data);
            PacketMetaData metaData = PacketUtility.GetMetaData(data);
            byte[] payload = PacketUtility.GetPayload(data);

            NetworkPacket networkPacket = new NetworkPacket(type, packetID, metaData, payload, sender);

            packetReader.AssignData(payload);

            if (!HandleRecivedMetaData(ref networkPacket, data))
                return;

            if (PacketTypeStrategy.TryGetValue(networkPacket.type, out PacketTypeDelegate handler))
                handler(networkPacket);
            else
                HandleUnhandledPacket(sender, data);
        }

        protected virtual void HandleUnhandledPacket(IPEndPoint packet, byte[] data)
        { }

        private void HandleAcknowledgement(NetworkPacket networkPacket)
        {
            PacketType packetType = (PacketType)packetReader.ReadInt();
            uint packetID = packetReader.ReadUInt();
            packetResender.Remove(packetType, packetID);
        }

        private bool HandleReliablePacketRecived(ref NetworkPacket networkPacket, byte[] _)
        {
            uint clientID = 0;

            if (networkPacket.payload.Length >= sizeof(uint))
                clientID = packetReader.ReadUInt();

            networkPacket.clientID = clientID;

            if (IsConnected)
                Send(PacketType.Acknowledgement, PacketMetaData.None, (int)networkPacket.type, networkPacket.packetID);
            else
                Send(networkPacket.ipEndPoint, PacketType.Acknowledgement, PacketMetaData.None, (int)networkPacket.type, networkPacket.packetID);

            ClientKey clientKey = GetClientKey(networkPacket, clientID);

            if (packectsUsedRegistry.ContainsPacket(clientKey, networkPacket.type, networkPacket.packetID))
                return false;

            packectsUsedRegistry.SetPacket(clientKey, networkPacket.type, networkPacket.packetID);
            return true;
        }

        private bool HandleOrdenablePacketRecived(ref NetworkPacket networkPacket, byte[] data)
        {
            ClientKey clientKey = GetClientKey(networkPacket, networkPacket.clientID);

            if (!ordenablePackets.TryGetValue(clientKey, out var clientsPackets))
            {
                clientsPackets = new Dictionary<PacketType, SortedDictionary<uint, NetworkPacket>>();
                ordenablePackets[clientKey] = clientsPackets;
            }

            if (!clientsPackets.TryGetValue(networkPacket.type, out var packets))
            {
                packets = new SortedDictionary<uint, NetworkPacket>();
                clientsPackets[networkPacket.type] = packets;
            }

            packets[networkPacket.packetID] = networkPacket;

            if (!lastPacketUsed.TryGetValue(clientKey, out var lastPackets))
            {
                lastPackets = new Dictionary<PacketType, uint>();
                lastPacketUsed[clientKey] = lastPackets;
            }

            if (!lastPackets.ContainsKey(networkPacket.type))
                lastPackets[networkPacket.type] = 0;

            while (packets.TryGetValue(lastPackets[networkPacket.type] + 1, out NetworkPacket nextPacket))
            {
                if (PacketTypeStrategy.TryGetValue(networkPacket.type, out PacketTypeDelegate handler))
                    handler(nextPacket);

                ++lastPackets[networkPacket.type];
            }

            return false;
        }

        private ClientKey GetClientKey(NetworkPacket packet, uint clientID)
        {
            if (typeof(ClientKey) == typeof(IPEndPoint))
                return (ClientKey)(object)packet.ipEndPoint;

            return (ClientKey)(object)clientID;
        }

        public virtual void Tick()
        {
            if (connection == null)
                return;

            connection.FlushReceiveData();

            packetResender.Tick();
            packectsUsedRegistry.Tick();
        }

        public void Connect(string ip, int port)
        {
            Disconnect();
            connection = new UdpConnection(IPAddress.Parse(ip), port, this);
        }

        public void Connect(int port)
        {
            Disconnect();
            connection = new UdpConnection(port, this);
        }

        public void Connect(IPAddress ipAdress, int port)
        {
            Disconnect();
            connection = new UdpConnection(ipAdress, port, this);
        }

        public void Disconnect()
        {
            packectsUsedRegistry.Clear();
            packetResender.Clear();

            if (connection != null)
                connection.Close();
        }

        public void SendRaw(byte[] data, IPEndPoint ip) => connection.Send(data, ip);
        public void SendRaw(byte[] data) => connection.Send(data);

        protected abstract void HandleHandShake(NetworkPacket networkPacket);
        protected abstract void HandlePing(NetworkPacket networkPacket);
        protected abstract void HandleClientLeft(NetworkPacket networkPacket);

        private void HandleReliableMessageSend(NetworkPacket networkPacket, ref byte[] data)
        {
            packetResender.Add(networkPacket.type, data, networkPacket.packetID, networkPacket.ipEndPoint);
        }
        private void HandleCriticalMessageSend(NetworkPacket packet, ref byte[] data) => cryticalPackets.Add(data);
        private bool HandleCriticalPacketRecived(ref NetworkPacket networkPacket, byte[] data)
        {
            cryticalPackets.Add(data);
            return true;
        }

        private void HandleSendMetaData(NetworkPacket packet, ref byte[] data)
        {
            foreach (KeyValuePair<PacketMetaData, SendPacketMetaDataDelegate> strategy in sendingMetaDataStrategy)
                if (packet.metaData.HasFlag(strategy.Key)) strategy.Value(packet, ref data);
        }

        private bool HandleRecivedMetaData(ref NetworkPacket packet, byte[] data)
        {
            bool handle = true;
            foreach (KeyValuePair<PacketMetaData, RecivePacketMetaDataDelegate> strategy in recivingMetaDataStrategy)
                if (packet.metaData.HasFlag(strategy.Key))
                    if (!strategy.Value(ref packet, data)) handle = false;
            return handle;
        }
    }
}