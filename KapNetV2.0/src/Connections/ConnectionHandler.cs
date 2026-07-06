using ImageCampus.ToolBox.Events;
using ImageCampus.ToolBox.Services;
using Networking.Core;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;

namespace Net
{
    internal abstract class ConnectionHandler : IReceiveData, IService
    {
        public bool IsPersistance => false;

        protected delegate void PacketTypeDelegate(NetworkPacket networkPacket);
        private delegate void SendPacketMetaDataDelegate(NetworkPacket networkPacket, ref byte[] data);
        private delegate bool RecivePacketMetaDataDelegate(ref NetworkPacket networkPacket, byte[] data);

        private TaskScheduler TaskScheduler => ServiceProvider.Instance.GetService<TaskScheduler>();
        private NetTree NetTree => ServiceProvider.Instance.GetService<NetTree>();
        protected PacketReader packetReader;
        protected EventBus EventBus => ServiceProvider.Instance.GetService<EventBus>();
        private RPCFactory RPCFactory => ServiceProvider.Instance.GetService<RPCFactory>();

        protected UdpConnection connection;

        protected PacketWriter PacketWriter;

        protected PacketFactory packetFactory;

        private PacketResender packetResender;

        private PacketEncryptor packetEncryptor;

        protected CheckSumFactory checksumFactory;

        private const float TIME_BETWEEN_PINGS = 1f;

        protected const double CLIENT_MAX_WAITING_PING_TIME = 3;

        protected const double SERVER_MAX_WAITING_PING_TIME = 5;

        private List<byte[]> cryticalPackets;

        protected Dictionary<Type, PacketType> varaibleTypePacket;
        // KapMatch change: promoted to 'protected' so the MatchMakerConnection subclass can
        // register handlers for the new matchmaker packet types.
        protected Dictionary<PacketType, PacketTypeDelegate> packetTypeStrategy;
        private Dictionary<PacketMetaData, SendPacketMetaDataDelegate> handleSendMetada;
        private Dictionary<PacketMetaData, RecivePacketMetaDataDelegate> handleRecievedMetada;

        private Dictionary<uint, Dictionary<PacketType, SortedDictionary<uint, NetworkPacket>>> ordenablePackets = new Dictionary<uint, Dictionary<PacketType, SortedDictionary<uint, NetworkPacket>>>();

        private Dictionary<uint, Dictionary<PacketType, uint>> lastPacketUsed = new Dictionary<uint, Dictionary<PacketType, uint>>();

        private MethodInfo readPacketWithType;

        internal ConnectionHandler()
        {
            packetFactory = new PacketFactory();

            PacketWriter = new PacketWriter();

            packetResender = new PacketResender();

            cryticalPackets = new List<byte[]>();

            packetReader = new PacketReader();

            readPacketWithType = typeof(PacketReader).GetMethod(nameof(packetReader.Read), BindingFlags.Public | BindingFlags.Instance);

            varaibleTypePacket = new Dictionary<Type, PacketType>()
            {
                {typeof(sbyte),   PacketType.vSByte },
                {typeof(byte),    PacketType.vByte },
                {typeof(short),   PacketType.vShort },
                {typeof(ushort),  PacketType.vUShort },
                {typeof(int),     PacketType.vInt },
                {typeof(uint),    PacketType.vUInt },
                {typeof(long),    PacketType.vLong },
                {typeof(ulong),   PacketType.vULong },
                {typeof(float),   PacketType.vfloat },
                {typeof(double),  PacketType.vDouble },
                {typeof(decimal), PacketType.vDecimal },
                {typeof(bool),    PacketType.vBool },
                {typeof(char),    PacketType.vChar },
                {typeof(string),  PacketType.vString }
            };

            packetTypeStrategy = new Dictionary<PacketType, PacketTypeDelegate>()
            {
                { PacketType.Handshake, HandleHandShake },
                { PacketType.Ping, HandlePing },
                { PacketType.ClientLeft, HandleClientLeft },
                { PacketType.Acknowledgement, HandleAcknowledgement },
                { PacketType.vSByte, HandleReadSByte },
                { PacketType.vByte, HandleReadByte },
                { PacketType.vShort, HandleReadShort },
                { PacketType.vUShort, HandleReadUShort },
                { PacketType.vInt, HandleReadInt },
                { PacketType.vUInt, HandleReadUInt },
                { PacketType.vLong, HandleReadLong },
                { PacketType.vULong, HandleReadULong },
                { PacketType.vfloat, HandleReadFloat },
                { PacketType.vDouble, HandleReadDouble },
                { PacketType.vDecimal, HandleReadDecimal },
                { PacketType.vBool, HandleReadBool },
                { PacketType.vChar, HandleReadChar },
                { PacketType.vString, HandleReadString },
                { PacketType.Method, HandleReadMethod },
                { PacketType.vSetNull, HandleSetNullReceived }
            };

            handleSendMetada = new Dictionary<PacketMetaData, SendPacketMetaDataDelegate>()
            {
                { PacketMetaData.Reliable, HandleReliableMessageSend },
                { PacketMetaData.Crytical, HandleCriticalMessageSend},
                { PacketMetaData.Encrypted, HandleEncryptedSend }
            };

            handleRecievedMetada = new Dictionary<PacketMetaData, RecivePacketMetaDataDelegate>()
            {
                { PacketMetaData.Encrypted, HandleEncryptedRecieved },
                { PacketMetaData.Reliable, HandleReliablePacketRecived },
                { PacketMetaData.Ordenable, HandleOrdenablePacketRecived },
                { PacketMetaData.Crytical, HandleCriticalPacketRecived }
            };
        }

        public virtual void Tick(float deltaTime)
        {
            // KapMatch FIX: 'connection' is only created in Connect(). Ticking before connecting
            // (e.g. a player still waiting on the matchmaker) would NullReference here, so treat
            // a not-yet-connected node as a no-op instead of crashing.
            if (connection == null)
                return;

            connection.FlushReceiveData();
            packetResender.Tick();
            TaskScheduler.Tick(deltaTime);
            NetTree.Tick(OnParameterValueChange, OnValueBecameNull);
        }

        protected abstract void OnParameterValueChange(Type valueType, object value, PacketMetaData metaData, uint[] adress);

        protected abstract void OnValueBecameNull(PacketMetaData metadata, uint[] address);

        private void HandleSetNullReceived(NetworkPacket networkPacket)
        {
            uint[] address = packetReader.ReadArray<uint>();
            NetTree.SetValue(null, address);
        }

        protected virtual bool HandleOrdenablePacketRecived(ref NetworkPacket networkPacket, byte[] data)
        {
            uint clientKey = networkPacket.clientID;

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
                // KapMatch FIX (late joiner): baseline the ordered stream at the FIRST id this
                // receiver actually sees for this type, instead of assuming it starts at 1. A
                // client that connects after the server has already sent ordered packets of this
                // type (e.g. someone chatted during the lobby) would otherwise wait forever for
                // id 1 and never process anything.
                lastPackets[networkPacket.type] = networkPacket.packetID - 1;

            while (packets.TryGetValue(lastPackets[networkPacket.type] + 1, out NetworkPacket nextPacket))
            {
                if (packetTypeStrategy.TryGetValue(networkPacket.type, out PacketTypeDelegate handler))
                    handler(nextPacket);

                ++lastPackets[networkPacket.type];
            }

            return false;
        }

        private void HandleEncryptedSend(NetworkPacket packet, ref byte[] data)
        {
            if (packet.payload == null || packet.payload.Length == 0)
                return;

            if (packetEncryptor == null)
                throw new InvalidOperationException("No se estableció el cifrado todavía (falta EstablishEncryption).");

            packet.payload = packetEncryptor.Encrypt(packet.payload, packet.packetID);

            (byte[] newData, uint _) = packetFactory.Create(
                packet.type,
                packet.payload,
                packet.metaData,
                packet.packetID
            );

            data = newData;
        }

        private bool HandleEncryptedRecieved(ref NetworkPacket networkPacket, byte[] data)
        {
            if (packetEncryptor == null)
                throw new InvalidOperationException("No se estableció el cifrado todavía (falta EstablishEncryption).");

            networkPacket.payload = packetEncryptor.Decrypt(networkPacket.payload, networkPacket.packetID);

            packetReader.AssignData(networkPacket.payload);

            return true;
        }

        protected abstract bool HandleReliablePacketRecived(ref NetworkPacket networkPacket, byte[] _);

        private void HandleCriticalMessageSend(NetworkPacket packet, ref byte[] data)
        {
            cryticalPackets.Add(data);
        }

        private bool HandleCriticalPacketRecived(ref NetworkPacket networkPacket, byte[] data)
        {
            cryticalPackets.Add(data);
            return true;
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

            if (packetTypeStrategy.TryGetValue(networkPacket.type, out PacketTypeDelegate handler))
                handler(networkPacket);
        }

        // KapMatch addition (2B): shared endpoint send used by the MatchMakerConnection
        // (and available to any subclass) to reply to an arbitrary sender. Mirrors the body
        // of ServerConnection's private per-endpoint Send.
        protected void SendTo(IPEndPoint ip, PacketType packetType, PacketMetaData metaData, params object[] parameters)
        {
            PacketWriter.Write(parameters);

            byte[] payload = PacketWriter.GetBytes();

            PacketWriter.Reset();

            (byte[] data, uint packetId) = packetFactory.Create(packetType, payload, metaData);
            NetworkPacket networkPacket = new NetworkPacket(packetType, packetId, metaData, payload);

            HandleSendMetaData(networkPacket, ref data);

            connection.Send(data, ip);
        }

        // KapMatch addition (2B): lets a subclass register a handler for an extra packet type.
        protected void AddPacketStrategy(PacketType type, PacketTypeDelegate handler)
        {
            packetTypeStrategy[type] = handler;
        }

        protected void HandleSendMetaData(NetworkPacket packet, ref byte[] data)
        {
            foreach (KeyValuePair<PacketMetaData, SendPacketMetaDataDelegate> strategy in handleSendMetada)
                if (packet.metaData.HasFlag(strategy.Key))
                    strategy.Value(packet, ref data);
        }

        private bool HandleRecivedMetaData(ref NetworkPacket packet, byte[] data)
        {
            bool handle = true;
            foreach (KeyValuePair<PacketMetaData, RecivePacketMetaDataDelegate> strategy in handleRecievedMetada)
                if (packet.metaData.HasFlag(strategy.Key))
                    if (!strategy.Value(ref packet, data))
                        handle = false;

            return handle;
        }

        private void HandleReliableMessageSend(NetworkPacket networkPacket, ref byte[] data)
        {
            packetResender.Add(networkPacket.type, data, networkPacket.packetID, networkPacket.ipEndPoint);
        }

        protected void RemovePendingResend(PacketType type, uint packetID)
        {
            packetResender.Remove(type, packetID);
        }

        protected void EstablishEncryption(ulong seed)
        {
            packetEncryptor = new PacketEncryptor(seed);
        }

        protected void EstablishChecksum(ulong seed)
        {
            checksumFactory = new CheckSumFactory(seed);
        }

        protected abstract void HandleHandShake(NetworkPacket networkPacket);
        protected abstract void HandlePing(NetworkPacket networkPacket);
        protected abstract void HandleClientLeft(NetworkPacket networkPacket);
        protected void HandleAcknowledgement(NetworkPacket networkPacket)
        {
            PacketType packetType = packetReader.Read<PacketType>();
            uint packetID = packetReader.ReadUInt();

            packetResender.Remove(packetType, packetID);
        }

        protected virtual void PingSender()
        {
            if (connection != null)
                TaskScheduler.Schedule(PingSender, TIME_BETWEEN_PINGS);
        }

        private void HandleReadInt(NetworkPacket networkPacket)
        {
            int value = packetReader.ReadInt();
            uint[] address = packetReader.ReadArray<uint>();

            NetTree.SetValue(value, address);
        }

        private void HandleReadUInt(NetworkPacket networkPacket)
        {
            uint value = packetReader.ReadUInt();
            uint[] address = packetReader.ReadArray<uint>();

            NetTree.SetValue(value, address);
        }

        private void HandleReadShort(NetworkPacket networkPacket)
        {
            short value = packetReader.ReadShort();
            uint[] address = packetReader.ReadArray<uint>();

            NetTree.SetValue(value, address);
        }

        private void HandleReadFloat(NetworkPacket networkPacket)
        {
            float value = packetReader.ReadFloat();
            uint[] address = packetReader.ReadArray<uint>();

            NetTree.SetValue(value, address);
        }

        private void HandleReadDouble(NetworkPacket networkPacket)
        {
            double value = packetReader.ReadDouble();
            uint[] address = packetReader.ReadArray<uint>();

            NetTree.SetValue(value, address);
        }

        private void HandleReadLong(NetworkPacket networkPacket)
        {
            long value = packetReader.ReadLong();
            uint[] address = packetReader.ReadArray<uint>();

            NetTree.SetValue(value, address);
        }

        private void HandleReadSByte(NetworkPacket networkPacket)
        {
            sbyte value = packetReader.ReadSByte();
            uint[] address = packetReader.ReadArray<uint>();

            NetTree.SetValue(value, address);
        }

        private void HandleReadByte(NetworkPacket networkPacket)
        {
            byte value = packetReader.ReadByte();
            uint[] address = packetReader.ReadArray<uint>();

            NetTree.SetValue(value, address);
        }

        private void HandleReadUShort(NetworkPacket networkPacket)
        {
            ushort value = packetReader.ReadUShort();
            uint[] address = packetReader.ReadArray<uint>();

            NetTree.SetValue(value, address);
        }

        private void HandleReadULong(NetworkPacket networkPacket)
        {
            ulong value = packetReader.ReadULong();
            uint[] address = packetReader.ReadArray<uint>();

            NetTree.SetValue(value, address);
        }

        private void HandleReadDecimal(NetworkPacket networkPacket)
        {
            decimal value = packetReader.ReadDecimal();
            uint[] address = packetReader.ReadArray<uint>();

            NetTree.SetValue(value, address);
        }

        private void HandleReadBool(NetworkPacket networkPacket)
        {
            bool value = packetReader.ReadBool();
            uint[] address = packetReader.ReadArray<uint>();

            NetTree.SetValue(value, address);
        }

        private void HandleReadChar(NetworkPacket networkPacket)
        {
            char value = packetReader.ReadChar();
            uint[] address = packetReader.ReadArray<uint>();

            NetTree.SetValue(value, address);
        }

        private void HandleReadString(NetworkPacket networkPacket)
        {
            string value = packetReader.ReadString();
            uint[] address = packetReader.ReadArray<uint>();

            NetTree.SetValue(value, address);
        }

        private void HandleReadMethod(NetworkPacket networkPacket)
        {
            string methodName = packetReader.ReadString();
            uint[] instanceAddress = packetReader.ReadArray<uint>();

            object instance = NetTree.GetValue(instanceAddress);

            object[] parameters = new object[RPCFactory.FunctionsParameters[methodName].Count];

            int index = 0;
            foreach (Type paramType in RPCFactory.FunctionsParameters[methodName])
                parameters[index++] = readPacketWithType.MakeGenericMethod(paramType).Invoke(packetReader, System.Array.Empty<object>());

            RPCFactory.InvokeOriginal(methodName, instance, parameters);
        }
    }

    internal class ClientConnection : ConnectionHandler
    {
        public const uint NULL_CLIENT = 0;

        private string name = "";

        private PackectsUsedRegistry<uint, PacketType> packetUsedRegistry = new PackectsUsedRegistry<uint, PacketType>();

        private ulong serverSeed = 0;
        private uint clientID = NULL_CLIENT;

        private DateTime serverLastPing;

        public ClientConnection(string name) : base()
        {
            this.name = name;
        }

        public ClientConnection(string ip, int port, string name) : this(name)
        {
            Connect(ip, port);
        }

        public ClientConnection(IPAddress ipAdress, int port, string name) : this(name)
        {
            Connect(ipAdress, port);
        }

        public void Connect(IPAddress ipAdress, int port)
        {
            connection = new UdpConnection(ipAdress, port, this);
        }

        public void Connect(string ipAdress, int port)
        {
            connection = new UdpConnection(IPAddress.Parse(ipAdress), port, this);
            serverLastPing = DateTime.UtcNow;
            SendHandShake();

            PingSender();
        }

        private void SendHandShake()
        {
            Send(PacketType.Handshake, PacketMetaData.Reliable, name);
        }

        protected override void PingSender()
        {
            Send(PacketType.Ping, PacketMetaData.None, DateTime.UtcNow.Ticks);
            base.PingSender();
        }

        protected override void HandleClientLeft(NetworkPacket networkPacket)
        {
            uint clientID = packetReader.ReadUInt();
            EventBus.Raise<ClientJoinEvent>(clientID);
        }

        protected override void HandleHandShake(NetworkPacket networkPacket)
        {
            ConnectionRole connectionRole = packetReader.Read<ConnectionRole>();

            switch (connectionRole)
            {
                case ConnectionRole.Client:
                    break;
                case ConnectionRole.Server:

                    serverSeed = packetReader.ReadULong();

                    EstablishEncryption(serverSeed);

                    ulong checksumSeed = packetReader.Read<ulong>();
                    EstablishChecksum(checksumSeed);

                    clientID = packetReader.ReadUInt();

                    EventBus.Raise<HandShackeRecievedEvent>(clientID);

                    break;
                default:
                    break;
            }
        }

        protected override void HandlePing(NetworkPacket networkPacket)
        {
            long ticks = packetReader.ReadLong();

            serverLastPing = new DateTime(ticks);
        }

        public override void Tick(float deltaTime)
        {
            if (connection != null && (DateTime.UtcNow - serverLastPing).TotalSeconds > CLIENT_MAX_WAITING_PING_TIME)
                ForcedDisconection();

            base.Tick(deltaTime);
        }

        public void VoluntaryDisconnect()
        {
            Send(PacketType.ClientLeft, PacketMetaData.None, clientID);
            CloseConnection();
        }

        private void ForcedDisconection()
        {
            EventBus.Raise<ServerDisconectedEvent>();
            CloseConnection();
        }

        private void CloseConnection()
        {
            connection.Close();
            connection = null;
        }

        public void Send(PacketType packetType, PacketMetaData metaData = PacketMetaData.None, params object[] parameters)
        {
            if (connection == null)
                throw new NullReferenceException("No connection");

            if (metaData.HasFlag(PacketMetaData.Reliable))
                PacketWriter.Write(clientID);

            PacketWriter.Write(parameters);

            byte[] payload = PacketWriter.GetBytes();

            PacketWriter.Reset();

            (byte[] data, uint packetId) = packetFactory.Create(packetType, payload, metaData);
            NetworkPacket networkPacket = new NetworkPacket(packetType, packetId, metaData, payload);

            HandleSendMetaData(networkPacket, ref data);

            SendRaw(data);
        }

        private void SendRaw(byte[] data)
        {
            connection.Send(data);
        }

        protected override bool HandleReliablePacketRecived(ref NetworkPacket networkPacket, byte[] _)
        {
            // -----------------------------------------------------------------------------
            // KapMatch FIX B (reliable de-align on the client):
            // Only CLIENT->SERVER reliable packets prepend a clientID (see ClientConnection.Send).
            // SERVER->CLIENT reliable packets (handshake reply + all RPC replication) do NOT.
            // The original code called packetReader.ReadUInt() here unconditionally, eating the
            // first 4 bytes of the real payload and mis-parsing every reliable packet the server
            // sends. Since a client only ever receives from the single server, we do not read a
            // clientID and instead key de-duplication under a constant "server" sender id.
            // -----------------------------------------------------------------------------
            uint senderKey = NULL_CLIENT;

            networkPacket.clientID = senderKey;

            Send(PacketType.Acknowledgement, PacketMetaData.None, (int)networkPacket.type, networkPacket.packetID);

            if (packetUsedRegistry.ContainsPacket(senderKey, networkPacket.type, networkPacket.packetID))
            {
                packetUsedRegistry.SetPacket(senderKey, networkPacket.type, networkPacket.packetID);
                return false;
            }

            packetUsedRegistry.SetPacket(senderKey, networkPacket.type, networkPacket.packetID);

            return true;
        }

        protected override void OnParameterValueChange(Type valueType, object value, PacketMetaData metaData, uint[] adress)
        {
            Send(varaibleTypePacket[valueType], metaData, value, adress);
        }

        protected override void OnValueBecameNull(PacketMetaData metadata, uint[] address)
        {
            Send(PacketType.vSetNull, metadata, address);
        }
    }

    internal class Client
    {
        public uint id;
        public string name;
        public DateTime lastResponce;
        public IPEndPoint iPEndPoint;
        public IPAddress address;
        public bool isConnected;

        public Client(uint id, string name, DateTime dateTime, IPEndPoint iPEndPoint, IPAddress address, bool connected = true)
        {
            this.id = id;
            this.name = name;
            lastResponce = dateTime;
            this.iPEndPoint = iPEndPoint;
            this.address = address;
            this.isConnected = connected;
        }
    }

    internal class ServerConnection : ConnectionHandler
    {
        int port = 0;
        private Dictionary<IPEndPoint, uint> clientsIDPerEndPoint;
        private Dictionary<uint, Client> clients;
        uint currentClientID = ClientConnection.NULL_CLIENT;

        private IPEndPoint matchMakerIP = null;

        private PackectsUsedRegistry<uint, PacketType> packetUsedRegistry = new PackectsUsedRegistry<uint, PacketType>();

        private ulong matchEncryptionSeed;
        private ulong matchChecksumSeed;

        public ServerConnection() : base()
        {
            clientsIDPerEndPoint = new Dictionary<IPEndPoint, uint>();
            clients = new Dictionary<uint, Client>();
        }

        public ServerConnection(int port) : this()
        {
            Connect(port);
        }

        public ServerConnection(IPEndPoint matchMakerIP, int port) : this(port)
        {
            this.matchMakerIP = matchMakerIP;
        }

        public ServerConnection(string matchMakerAddress, string matchMakerPort, int port) : this(port)
        {
            IPAddress ip = IPAddress.Parse(matchMakerAddress);

            if (!int.TryParse(matchMakerPort, out int matchMakerIntPort))
                throw new Exception("Server was not able to Parse the Port");

            matchMakerIP = new IPEndPoint(ip, matchMakerIntPort);

            Send(matchMakerIP, PacketType.Handshake, PacketMetaData.Reliable, ConnectionRole.Server);
        }

        public void Connect(int port)
        {
            this.port = port;
            connection = new UdpConnection(port, this);

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                byte[] seedBytes = new byte[sizeof(long) + sizeof(ulong)];
                rng.GetBytes(seedBytes);

                matchEncryptionSeed = BitConverter.ToUInt64(seedBytes, 0);
                matchChecksumSeed = BitConverter.ToUInt64(seedBytes, sizeof(long));
            }

            EstablishEncryption(matchEncryptionSeed);
            EstablishChecksum(matchChecksumSeed);

            PingSender();
        }

        // KapMatch addition (2B): the authoritative server announces itself to the matchmaker,
        // sending the port it is listening on. The matchmaker records (senderIP, port) and hands
        // that endpoint to players who ask for a match. Kept unencrypted (matchmaker control
        // channel), and unreliable with periodic re-announce so a late-starting matchmaker still
        // learns about the server.
        public void RegisterWithMatchMaker(IPEndPoint matchMaker)
        {
            matchMakerIP = matchMaker;
            SendTo(matchMakerIP, PacketType.ServerRegister, PacketMetaData.None, port);
        }

        protected override void PingSender()
        {
            long ticks = DateTime.UtcNow.Ticks;

            BroadCast(PacketType.Ping, PacketMetaData.None, ticks);

            // KapMatch: keep re-announcing to the matchmaker so it can recover the registration
            // if it (re)starts after the server.
            if (matchMakerIP != null)
                SendTo(matchMakerIP, PacketType.ServerRegister, PacketMetaData.None, port);

            if (matchMakerIP != null)
                Send(matchMakerIP, PacketType.Ping, PacketMetaData.None, ticks);

            base.PingSender();
        }

        public void BroadCast(PacketType packetType, PacketMetaData metaData, params object[] parameters)
        {
            // -----------------------------------------------------------------------------
            // KapMatch FIX (multi-client ordered delivery):
            // Serialize the payload and assign the packet id ONCE, then send the SAME bytes to
            // every client. The original code called the private Send() per client, which called
            // PacketFactory.Create() per client, advancing the per-type packet-id counter once
            // per recipient. With 2+ clients each client then saw a NON-consecutive id stream
            // (1,3,5.. vs 2,4,6..), and the client's Ordenable receiver only releases id
            // lastUsed+1, so it stalled forever on the first gap. Result: replication (match
            // start) and chat froze the instant the second player connected. Sending identical
            // bytes gives every client one consecutive stream.
            // -----------------------------------------------------------------------------
            PacketWriter.Write(parameters);
            byte[] payload = PacketWriter.GetBytes();
            PacketWriter.Reset();

            (byte[] data, uint packetId) = packetFactory.Create(packetType, payload, metaData);

            foreach (KeyValuePair<uint, Client> client in clients)
            {
                if (!client.Value.isConnected)
                    continue;

                byte[] clientData = data;
                NetworkPacket networkPacket = new NetworkPacket(packetType, packetId, metaData, payload, client.Value.iPEndPoint);

                HandleSendMetaData(networkPacket, ref clientData);
                SendRaw(clientData, client.Value.iPEndPoint);
            }
        }

        public void BroadCast(IPEndPoint exeption, PacketType packetType, PacketMetaData metaData, params object[] parameters)
        {
            foreach (KeyValuePair<uint, Client> client in clients)
                if (!client.Value.iPEndPoint.Equals(exeption) && client.Value.isConnected)
                    Send(client.Value.iPEndPoint, packetType, metaData, parameters);

        }

        private void Send(IPEndPoint ip, PacketType packetType, PacketMetaData metaData, params object[] parameters)
        {
            PacketWriter.Write(parameters);

            byte[] payload = PacketWriter.GetBytes();

            PacketWriter.Reset();

            (byte[] data, uint packetId) = packetFactory.Create(packetType, payload, metaData);
            NetworkPacket networkPacket = new NetworkPacket(packetType, packetId, metaData, payload);

            HandleSendMetaData(networkPacket, ref data);
            SendRaw(data, ip);
        }

        private void SendRaw(byte[] data, IPEndPoint IpEndPoint)
        {
            connection.Send(data, IpEndPoint);
        }

        // KapMatch addition (relay topology): forward an already-serialized RPC method payload
        // to every connected client except the sender. Used by RelayConnection so a
        // non-authoritative ("dumb") server can shuttle RPCs between authoritative players
        // without interpreting or deciding anything. 'methodPayload' must be the method bytes
        // WITHOUT the 4-byte client-id prefix that client->server reliable packets carry, since
        // server->client packets do not include it.
        protected void ForwardMethodToOthers(byte[] methodPayload, IPEndPoint sender)
        {
            foreach (KeyValuePair<uint, Client> client in clients)
            {
                if (!client.Value.isConnected)
                    continue;
                if (client.Value.iPEndPoint.Equals(sender))
                    continue;

                (byte[] data, uint packetId) = packetFactory.Create(PacketType.Method, methodPayload, PacketMetaData.Ordenable);
                NetworkPacket networkPacket = new NetworkPacket(PacketType.Method, packetId, PacketMetaData.Ordenable, methodPayload, client.Value.iPEndPoint);

                HandleSendMetaData(networkPacket, ref data);
                connection.Send(data, client.Value.iPEndPoint);
            }
        }

        protected override void HandleHandShake(NetworkPacket networkPacket)
        {
            if (!clientsIDPerEndPoint.ContainsKey(networkPacket.ipEndPoint))
            {
                clientsIDPerEndPoint.Add(networkPacket.ipEndPoint, ++currentClientID);

                string name = packetReader.ReadString();

                clients.Add(currentClientID, new Client(currentClientID, name, DateTime.UtcNow, networkPacket.ipEndPoint, networkPacket.ipEndPoint.Address));

                Send(networkPacket.ipEndPoint, PacketType.Handshake, PacketMetaData.Reliable, ConnectionRole.Server, matchEncryptionSeed, matchChecksumSeed, currentClientID);
            }
            else
            {
                Client client = clients[clientsIDPerEndPoint[networkPacket.ipEndPoint]];
                client.isConnected = true;
                client.lastResponce = DateTime.UtcNow;

                Send(networkPacket.ipEndPoint, PacketType.Handshake, PacketMetaData.None, ConnectionRole.Server, matchEncryptionSeed, matchChecksumSeed, client.id);
            }
        }

        public override void Tick(float deltaTime)
        {
            DateTime utcNow = DateTime.UtcNow;

            foreach (KeyValuePair<uint, Client> client in clients)
                if (client.Value.isConnected && (utcNow - client.Value.lastResponce).TotalSeconds > SERVER_MAX_WAITING_PING_TIME)
                {
                    client.Value.isConnected = false;
                    BroadCast(client.Value.iPEndPoint, PacketType.ClientLeft, PacketMetaData.Reliable, client.Key);
                }

            base.Tick(deltaTime);
        }

        protected override void HandlePing(NetworkPacket networkPacket)
        {
            long ticks = packetReader.ReadLong();

            // KapMatch FIX: a client sends its first Ping (unreliable) right after the reliable
            // handshake, so a Ping can arrive and be processed BEFORE HandleHandShake has
            // registered the endpoint. Guard the lookup (as HandleClientLeft already does) instead
            // of throwing KeyNotFoundException and killing the server tick. An unknown endpoint's
            // ping is simply ignored; its handshake will register it a moment later.
            if (clientsIDPerEndPoint.TryGetValue(networkPacket.ipEndPoint, out uint id) &&
                clients.TryGetValue(id, out Client client))
            {
                client.lastResponce = new DateTime(ticks);
            }
        }

        protected override void HandleClientLeft(NetworkPacket networkPacket)
        {
            if (clientsIDPerEndPoint.TryGetValue(networkPacket.ipEndPoint, out uint id) &&
                clients.TryGetValue(id, out Client client))
            {
                client.isConnected = false;
            }
        }

        protected override bool HandleReliablePacketRecived(ref NetworkPacket networkPacket, byte[] _)
        {
            uint clientID = packetReader.ReadUInt();

            networkPacket.clientID = clientID;

            Send(networkPacket.ipEndPoint, PacketType.Acknowledgement, PacketMetaData.None, (int)networkPacket.type, networkPacket.packetID);

            if (packetUsedRegistry.ContainsPacket(clientID, networkPacket.type, networkPacket.packetID))
            {
                packetUsedRegistry.SetPacket(clientID, networkPacket.type, networkPacket.packetID);
                return false;
            }

            packetUsedRegistry.SetPacket(clientID, networkPacket.type, networkPacket.packetID);

            return true;
        }

        protected override void OnParameterValueChange(Type valueType, object value, PacketMetaData metaData, uint[] adress)
        {
            BroadCast(varaibleTypePacket[valueType], metaData, value, adress);
        }

        protected override void OnValueBecameNull(PacketMetaData metadata, uint[] address)
        {
            BroadCast(PacketType.vSetNull, metadata, address);
        }
    }
}